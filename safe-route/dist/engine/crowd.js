// Crowds: seeded neighbourhood people (people.createPerson 'crowd' preset) moving along polylines with
// simple separation steering, a keep-right lane on sidewalks, player avoidance and distance LOD.
// createCrowd({ count, seed, people, quality|kit, heightAt, player })
//   -> { group, update(dt, ctx?), setMode(mode, opts), setPaths(paths), setArea(rect), mode, members, active, dispose }
// Modes and their opts (all optional unless noted):
//   stroll  wander along paths (keep right, change paths at shared ends, short stops: phone / talk / idle);
//           with no paths: wander inside the area.
//   brisk   hurry to the END of each path (paths should end at the targets) or, with no paths, straight to the
//           nearest of opts.targets [{x,z}]. opts.then: 'respawn' (default: reappear at a path start out of the
//           viewer's sight, a continuous stream) | 'despawn' (hide) | 'idle' (stand near the end).
//   queue   line at opts.at {x,z} (required), extending along opts.dir {x,z} (default {0,1}), opts.spacing 0.75 m;
//           every opts.serve s (default 10) the head leaves along opts.leave {x,z} (default: right of the line)
//           carrying opts.carry (a prop name, e.g. 'waterJug') and rejoins at the tail. Others shuffle forward.
//   sit     seat on opts.spots [{x,z,yaw?}] or a grid over the area (opts.spacing 1.5 m); opts.standing (0.12)
//           of them stand beside their spot; opts.y lifts sitters (mat thickness, default 0.03).
//   idle    stand where they are (or a random placement): idle / phone / talk (talkers face a neighbour).
//   none    hide everyone.
// Shared opts: count (active members, capped by quality: high 40, medium 24, low 12), paths, area, heightAt,
//   props { name: probability } (e.g. { flashlight: 0.3 } at night), instant (snap into place; default true for
//   queue / sit, else members keep their positions and walk to the new behaviour).
// update(dt, ctx): ctx.camera / ctx.player are optional (the level's ctx works as is). Without them the viewer
// is taken from the camera that last rendered a member and the player is assumed to be at the viewer.
import * as THREE from 'three';
import { rng } from './kit.js';
import { createPerson as basePerson } from './people.js';

const CAP = { high: 40, medium: 24, low: 12 };
const MODES = ['stroll', 'brisk', 'queue', 'sit', 'idle', 'none'];
const STAND = ['idle', 'idle', 'phone', 'talk'];
const SEP = 0.62, AVOID = 1.05, LOD_ANIM = 45, LOD_SHADOW = 25, RESPAWN_HIDE = 20;
const TAU = Math.PI * 2;
const clamp = (x, a, b) => Math.min(b, Math.max(a, x));
const lerpAngle = (a, b, k) => a + ((((b - a + Math.PI) % TAU) + TAU) % TAU - Math.PI) * k;
const okPt = (q) => Number.isFinite(q?.x) && Number.isFinite(q?.z);
const dist = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);

function prepPath(pts) {
  const p = Array.isArray(pts) ? pts.filter(okPt) : [];
  if (p.length < 2) return null;
  const cum = [0];
  for (let i = 1; i < p.length; i++) cum.push(cum[i - 1] + Math.hypot(p[i].x - p[i - 1].x, p[i].z - p[i - 1].z));
  return cum[cum.length - 1] > 0.5 ? { p, cum, len: cum[cum.length - 1] } : null;
}
// Point and unit tangent at arc length s.
function at(P, s, out) {
  s = clamp(s, 0, P.len);
  let i = 1;
  while (i < P.p.length - 1 && P.cum[i] < s) i++;
  const a = P.p[i - 1], b = P.p[i], L = P.cum[i] - P.cum[i - 1] || 1, t = (s - P.cum[i - 1]) / L;
  out.x = a.x + (b.x - a.x) * t; out.z = a.z + (b.z - a.z) * t; out.tx = (b.x - a.x) / L; out.tz = (b.z - a.z) / L;
  return out;
}
// Nearest path and arc length to (x, z).
function project(paths, x, z) {
  let best = null, bd = Infinity;
  paths.forEach((P, pi) => {
    for (let i = 1; i < P.p.length; i++) {
      const a = P.p[i - 1], b = P.p[i], dx = b.x - a.x, dz = b.z - a.z, L2 = dx * dx + dz * dz || 1;
      const t = clamp(((x - a.x) * dx + (z - a.z) * dz) / L2, 0, 1), d = (a.x + dx * t - x) ** 2 + (a.z + dz * t - z) ** 2;
      if (d < bd) { bd = d; best = { pi, s: P.cum[i - 1] + Math.sqrt(L2) * t }; }
    }
  });
  return best;
}

export function createCrowd({ count = 24, seed = 1, people = null, quality, kit, heightAt = null, player = null } = {}) {
  const q = quality ?? kit?.quality ?? 'high';
  const cap = CAP[q] ?? CAP.high;
  const make = typeof people?.createPerson === 'function' ? people.createPerson : basePerson;
  const r = rng(((seed * 2654435761) ^ 0x5bd1e995) >>> 0);
  const pick = (arr) => arr[(r() * arr.length) | 0];
  const group = new THREE.Group(); group.name = 'crowd';
  const viewer = new THREE.Vector3(); let hasViewer = false;
  const hook = (renderer, scene, camera) => { viewer.setFromMatrixPosition(camera.matrixWorld); hasViewer = true; };
  let H = typeof heightAt === 'function' ? heightAt : () => 0;
  let paths = [], area = null, mode = 'none', M = {}, active = 0, frame = 0, serveT = 0, warned = false;
  const line = [];
  const tmp = { x: 0, z: 0, tx: 0, tz: 1 };

  // ---- members (built up front so the level build absorbs the cost) ----
  const n = Math.max(0, Math.min(count | 0, cap));
  const agents = [];
  for (let i = 0; i < n; i++) {
    let p;
    try { p = make({ preset: 'crowd', seed: seed * 1000 + i + 1 }); } catch (err) { console.error('[crowd] createPerson failed', err); continue; }
    if (!p?.group) { console.error('[crowd] createPerson returned no group'); continue; }
    if (p.mesh) p.mesh.onBeforeRender = hook;
    p.group.visible = false;
    group.add(p.group);
    agents.push({
      p, i, x: 0, z: 0, vx: 0, vz: 0, yaw: 0, face: null, on: false, placed: false, state: 'stand', path: -1, s: 0, dir: 1,
      lane: 0, speed: 1.2, goal: null, timer: 0, stopT: 0, anim: '', standAnim: 'idle', moveAnim: 'walk', moving: false,
      acc: 0, sh: true, dy: 0, props: new Set(), jit: 0, slow: (p.spec?.stoop ?? 0) > 0.1 ? 0.72 : 1,
    });
  }

  const show = (a, on) => { a.p.group.visible = on; };
  function setProps(a, want) {
    for (const name of a.props) if (!want.has(name)) a.p.setProp?.(name, false);
    for (const name of want) if (!a.props.has(name)) a.p.setProp?.(name, true);
    a.props = want;
  }
  function randomPathPoint(a) {
    const total = paths.reduce((s, P) => s + P.len, 0);
    let u = r() * total, pi = 0;
    while (pi < paths.length - 1 && u > paths[pi].len) u -= paths[pi++].len;
    a.path = pi; a.s = clamp(u, 0, paths[pi].len);
  }
  function areaPoint() {
    if (area) return { x: area.x0 + r() * (area.x1 - area.x0), z: area.z0 + r() * (area.z1 - area.z0) };
    return { x: (r() - 0.5) * 10, z: (r() - 0.5) * 10 };
  }
  function laneGoal(a) { // carrot on the path, offset to the walker's right (keep right on sidewalks)
    at(paths[a.path], a.s, tmp);
    const dx = tmp.tx * a.dir, dz = tmp.tz * a.dir;
    return [tmp.x - dz * a.lane, tmp.z + dx * a.lane];
  }
  function placeAt(a, x, z, yaw = a.yaw) { a.x = x; a.z = z; a.vx = a.vz = 0; a.yaw = yaw; a.placed = true; }

  // ---- per-mode setup ----
  function setupPath(a, keep, brisk) {
    if (keep) { const pr = project(paths, a.x, a.z); a.path = pr.pi; a.s = pr.s; } else randomPathPoint(a);
    at(paths[a.path], a.s, tmp);
    const ahead = -Math.sin(a.yaw) * tmp.tx - Math.cos(a.yaw) * tmp.tz; // keep walking the way they face
    a.dir = brisk ? 1 : keep ? (ahead >= 0 ? 1 : -1) : (r() < 0.5 ? 1 : -1);
    a.lane = brisk ? (r() - 0.5) * 1.4 : 0.25 + r() * 0.6;
    if (!keep) { const [gx, gz] = laneGoal(a); placeAt(a, gx, gz, Math.atan2(-tmp.tx * a.dir, -tmp.tz * a.dir)); }
  }
  function slot(k) {
    const d = M.dirN, s = M.spacing ?? 0.75;
    return { x: M.at.x + d.x * (0.5 + k * s), z: M.at.z + d.z * (0.5 + k * s) };
  }
  function setupSit(list) { // always snaps onto the spots
    let spots = Array.isArray(M.spots) ? M.spots.filter(okPt) : null;
    if (!spots?.length) {
      if (!area) { console.error('[crowd] sit mode needs opts.spots or an area (setArea / opts.area)'); spots = [{ x: 0, z: 0 }]; }
      else {
        const sp = M.spacing ?? 1.5; spots = [];
        for (let z = area.z0 + sp / 2; z < area.z1; z += sp) for (let x = area.x0 + sp / 2; x < area.x1; x += sp) spots.push({ x: x + (r() - 0.5) * 0.3, z: z + (r() - 0.5) * 0.3 });
      }
    }
    spots = spots.map(s => ({ ...s, k: r() })).sort((a, b) => a.k - b.k);
    list.forEach((a, k) => {
      const s = spots[k];
      if (!s) { a.state = 'gone'; show(a, false); return; }
      const yaw = Number.isFinite(s.yaw) ? s.yaw + (r() - 0.5) * 0.4 : pick([0, 1, 2, 3]) * Math.PI / 2 + (r() - 0.5) * 0.6;
      const standing = r() < (M.standing ?? 0.12);
      a.state = 'stand'; a.goal = null;
      if (standing) { a.standAnim = pick(['idle', 'phone', 'talk']); a.dy = 0; placeAt(a, s.x + Math.cos(yaw) * 0.65, s.z - Math.sin(yaw) * 0.65, yaw); }
      else { a.standAnim = 'sit'; a.dy = M.y ?? 0.03; placeAt(a, s.x, s.z, yaw); }
      a.face = a.yaw;
    });
  }
  function faceNeighbour(a) {
    let best = null, bd = 9;
    for (const b of agents) if (b !== a && b.on && b.state !== 'gone') { const d = (b.x - a.x) ** 2 + (b.z - a.z) ** 2; if (d < bd) { bd = d; best = b; } }
    return best ? Math.atan2(-(best.x - a.x), -(best.z - a.z)) : a.yaw;
  }

  function apply() {
    const want = mode === 'none' ? 0 : Math.max(0, Math.min(M.count ?? agents.length, agents.length));
    active = want;
    const instant = M.instant ?? (mode === 'queue' || mode === 'sit');
    const list = [];
    agents.forEach((a, k) => {
      const on = k < want;
      if (!on) { a.on = false; show(a, false); setProps(a, new Set()); return; }
      const keep = !instant && a.on && a.placed && a.state !== 'gone';
      a.on = true; a.dy = 0; a.face = null; a.goal = null; a.stopT = 4 + r() * 16; a.timer = 0;
      const props = new Set();
      for (const [name, prob] of Object.entries(M.props ?? {})) if (r() < prob) props.add(name);
      setProps(a, props);
      list.push(a);
      if (mode === 'stroll' || mode === 'brisk') {
        const brisk = mode === 'brisk';
        a.speed = (brisk ? 1.55 + r() * 0.45 : 1.0 + r() * 0.4) * a.slow;
        a.moveAnim = brisk ? 'brisk' : 'walk'; a.standAnim = brisk ? 'idle' : pick(STAND); a.state = 'move';
        if (paths.length) setupPath(a, keep, brisk);
        else {
          a.path = -1;
          if (!keep) { const p = areaPoint(); placeAt(a, p.x, p.z, r() * TAU); }
          if (brisk && !M.targets.length && !warned) { warned = true; console.warn('[crowd] brisk mode without paths or targets: members stroll in the area'); }
        }
      } else if (mode === 'idle') {
        if (!keep) { if (paths.length) setupPath(a, false, false); else { const p = areaPoint(); placeAt(a, p.x, p.z, r() * TAU); } }
        a.state = 'stand'; a.standAnim = pick(STAND);
      } else if (mode === 'queue') {
        a.speed = 0.9 * a.slow; a.moveAnim = 'walk'; a.standAnim = r() < 0.3 ? 'phone' : 'idle'; a.state = 'line'; a.jit = (r() - 0.5) * 0.18;
        if (!a.placed) { const p = areaPoint(); placeAt(a, p.x, p.z); }
      }
      if (!keep) { a.p.setAnim?.(a.standAnim, { instant: true }); a.anim = a.standAnim; a.moving = false; }
      show(a, true);
    });
    if (mode === 'sit') setupSit(list);
    if (mode === 'idle') for (const a of list) a.face = a.standAnim === 'talk' ? faceNeighbour(a) : a.yaw;
    if (mode === 'queue') {
      if (!okPt(M.at)) { console.error('[crowd] queue mode needs opts.at {x,z}'); M.at = area ? { x: (area.x0 + area.x1) / 2, z: (area.z0 + area.z1) / 2 } : { x: 0, z: 0 }; }
      const d = okPt(M.dir) ? M.dir : { x: 0, z: 1 }, l = Math.hypot(d.x, d.z) || 1;
      M.dirN = { x: d.x / l, z: d.z / l };
      const lv = okPt(M.leave) ? M.leave : { x: -M.dirN.z, z: M.dirN.x }, ll = Math.hypot(lv.x, lv.z) || 1;
      M.leaveN = { x: lv.x / ll, z: lv.z / ll };
      line.length = 0;
      list.sort((a, b) => dist(a, M.at) - dist(b, M.at)).forEach((a) => line.push(a));
      if (instant) line.forEach((a, k) => { const s = slot(k); placeAt(a, s.x + M.dirN.z * a.jit, s.z - M.dirN.x * a.jit, Math.atan2(M.dirN.x, M.dirN.z)); });
      serveT = (M.serve ?? 10) * (0.5 + r());
    }
    for (const a of list) if (a.state !== 'gone') pose(a);
  }

  // ---- behaviour ----
  function endOfPath(a) {
    const P = paths[a.path], end = a.dir > 0 ? P.p[P.p.length - 1] : P.p[0];
    if (mode === 'brisk') { arrive(a, end); return; }
    const next = [];
    paths.forEach((Q, qi) => {
      if (qi === a.path) return;
      if (dist(Q.p[0], end) < 4) next.push([qi, 0, 1]);
      if (dist(Q.p[Q.p.length - 1], end) < 4) next.push([qi, Q.len, -1]);
    });
    if (next.length && r() > 0.15) { const [qi, s, d] = pick(next); a.path = qi; a.s = s; a.dir = d; }
    else { a.dir = -a.dir; a.s = clamp(a.s, 0, P.len); if (r() < 0.5) stop(a, 2 + r() * 4); }
  }
  function arrive(a, end) {
    const then = M.then ?? 'respawn';
    if (then === 'idle') { a.state = 'stand'; a.goal = null; a.standAnim = pick(['idle', 'idle', 'phone']); a.face = Math.atan2(-(end.x - a.x), -(end.z - a.z)); return; }
    a.state = 'gone'; a.timer = 0.5 + r(); show(a, false);
  }
  function stop(a, s) { a.state = 'stand'; a.timer = s; a.standAnim = pick(STAND); a.face = a.standAnim === 'talk' ? faceNeighbour(a) : null; }
  function respawn(a, dt) {
    if ((M.then ?? 'respawn') !== 'respawn' || !paths.length) return;
    if ((a.timer -= dt) > 0) return;
    a.timer = 0.6;
    const pi = (r() * paths.length) | 0, st = paths[pi].p[0];
    if (hasViewer && Math.hypot(st.x - viewer.x, st.z - viewer.z) < RESPAWN_HIDE) return;
    a.path = pi; a.s = 0; a.dir = 1; a.lane = (r() - 0.5) * 1.4;
    const [gx, gz] = laneGoal(a);
    placeAt(a, gx, gz, Math.atan2(-tmp.tx, -tmp.tz)); a.state = 'move'; show(a, true);
  }

  function queueTick(dt) {
    serveT -= dt;
    if (serveT <= 0 && line.length > 1) {
      serveT = (M.serve ?? 10) * (0.7 + 0.6 * r());
      const a = line.shift();
      a.state = 'leave'; a.goal = { x: M.at.x + M.leaveN.x * 18, z: M.at.z + M.leaveN.z * 18 };
      if (M.carry) { setProps(a, new Set([...a.props, M.carry])); if (M.carry === 'waterJug') a.moveAnim = 'carry'; }
    }
  }

  // Steer toward (gx, gz) at speed v with separation and player avoidance. Returns the distance to the goal.
  function steer(a, gx, gz, v, dt, pl) {
    const dx = gx - a.x, dz = gz - a.z, d = Math.hypot(dx, dz);
    let wx = 0, wz = 0;
    if (d > 0.04) { const k = Math.min(v, d * 1.8) / d; wx = dx * k; wz = dz * k; }
    for (const b of agents) {
      if (b === a || !b.on || b.state === 'gone') continue;
      const ex = a.x - b.x, ez = a.z - b.z, e2 = ex * ex + ez * ez;
      if (e2 < SEP * SEP && e2 > 1e-6) { const e = Math.sqrt(e2), f = ((SEP - e) / SEP) * 1.5 / e; wx += ex * f; wz += ez * f; }
    }
    if (pl) {
      const ex = a.x - pl.x, ez = a.z - pl.z, e = Math.hypot(ex, ez);
      if (e < AVOID * 2.4 && e > 1e-3 && d > 0.04 && (dx * -ex + dz * -ez) / d > 0.2 * e) { // player ahead: step to the right
        const k = 0.6 * (1 - e / (AVOID * 2.4)) * v; wx += (-dz / d) * k; wz += (dx / d) * k;
      }
      if (e < AVOID && e > 1e-3) { const f = ((AVOID - e) / AVOID) * 2.2 / e; wx += ex * f; wz += ez * f; }
    }
    const k = Math.min(1, dt * 5);
    a.vx += (wx - a.vx) * k; a.vz += (wz - a.vz) * k;
    a.x += a.vx * dt; a.z += a.vz * dt;
    return d;
  }

  function tick(a, dt, pl) {
    if (a.state === 'gone') { respawn(a, dt); return; }
    let goal = null, v = a.speed;
    if (mode === 'stroll' || mode === 'brisk') {
      if (a.state === 'stand') {
        a.timer -= dt;
        if (a.timer <= 0 && (mode === 'stroll' || (a.path < 0 && !M.targets.length))) a.state = 'move';
      } else if (a.path >= 0) {
        if (mode === 'stroll' && (a.stopT -= dt) <= 0) { a.stopT = 10 + r() * 20; if (r() < 0.3) stop(a, 3 + r() * 6); }
        const [gx, gz] = laneGoal(a);
        if (Math.hypot(gx - a.x, gz - a.z) < 1.4) a.s += a.dir * a.speed * dt * 1.15;
        if (a.dir > 0 ? a.s >= paths[a.path].len : a.s <= 0) {
          const near = Math.hypot(gx - a.x, gz - a.z) < (mode === 'brisk' ? 1.0 : 1.6);
          if (near) endOfPath(a);
        }
        if (a.state === 'gone') return;
        if (a.state === 'move') goal = laneGoal(a);
      } else if (mode === 'brisk' && M.targets.length) { // straight to the nearest target
        const t = M.targets.reduce((b, t) => (dist(t, a) < dist(b, a) ? t : b));
        if (dist(t, a) < 1.0) arrive(a, t); else goal = [t.x, t.z];
      } else { // wander in the area
        if (!a.goal || dist(a.goal, a) < 0.6) { if (a.goal) stop(a, 2 + r() * 4); a.goal = areaPoint(); }
        if (a.state === 'move') goal = [a.goal.x, a.goal.z];
      }
    } else if (mode === 'queue') {
      if (a.state === 'line') {
        const k = line.indexOf(a), s = slot(k);
        goal = [s.x + M.dirN.z * a.jit, s.z - M.dirN.x * a.jit]; v = 0.9 * a.slow;
        a.face = Math.atan2(M.dirN.x, M.dirN.z);
      } else if (a.state === 'leave') {
        goal = [a.goal.x, a.goal.z]; v = 1.1 * a.slow;
        if (dist(a.goal, a) < 0.8) { // out of the scene: rejoin at the tail
          setProps(a, new Set([...a.props].filter(nm => nm !== M.carry))); a.moveAnim = 'walk';
          const s = slot(line.length + 9); placeAt(a, s.x, s.z); a.state = 'line'; line.push(a); goal = null;
        }
      }
    }
    if (goal) steer(a, goal[0], goal[1], v, dt, pl);
    else steer(a, a.x, a.z, 0, dt, pl); // standing: only step aside for neighbours and the player
  }

  // Animation, facing, placement and LOD for one member.
  function pose(a, dt = 0) {
    const sp = Math.hypot(a.vx, a.vz);
    if (sp > 0.25) a.moving = true; else if (sp < 0.12) a.moving = false;
    const anim = a.moving ? a.moveAnim : a.standAnim;
    if (anim !== a.anim) { a.p.setAnim?.(anim); a.anim = anim; }
    a.p.setSpeed?.(a.moving ? sp : null);
    const target = a.moving ? Math.atan2(-a.vx, -a.vz) : a.face ?? a.yaw;
    a.yaw = dt ? lerpAngle(a.yaw, target, Math.min(1, dt * 5)) : target;
    a.p.group.position.set(a.x, H(a.x, a.z) + a.dy, a.z);
    a.p.group.rotation.y = a.yaw;
  }

  function update(dt, ctx) {
    dt = Math.min(Math.max(0, +dt || 0), 0.1);
    frame++;
    if (ctx?.camera?.matrixWorld) { viewer.setFromMatrixPosition(ctx.camera.matrixWorld); hasViewer = true; }
    const pl = ctx?.player ?? player ?? (hasViewer ? viewer : null);
    if (!active || !dt) return;
    if (mode === 'queue') queueTick(dt);
    for (const a of agents) {
      if (!a.on) continue;
      if (mode !== 'sit') tick(a, dt, pl);
      if (a.state === 'gone') continue;
      pose(a, dt);
      const d2 = hasViewer ? (a.x - viewer.x) ** 2 + (a.z - viewer.z) ** 2 : 0;
      const sh = d2 < LOD_SHADOW * LOD_SHADOW;
      if (sh !== a.sh) { a.sh = sh; a.p.setShadow?.(sh); }
      a.acc += dt;
      if (d2 < LOD_ANIM * LOD_ANIM || (frame + a.i) % 3 === 0) { a.p.update?.(a.acc); a.acc = 0; }
    }
  }

  function setMode(m, opts = {}) {
    if (!MODES.includes(m)) { console.error(`[crowd] unknown mode "${m}"; known: ${MODES.join(', ')}`); return; }
    if (opts.paths) setPaths(opts.paths, false);
    if (opts.area) setArea(opts.area, false);
    if (typeof opts.heightAt === 'function') H = opts.heightAt;
    mode = m; M = { ...opts, targets: Array.isArray(opts.targets) ? opts.targets.filter(okPt) : [] };
    apply();
  }
  function setPaths(list, reapply = true) {
    if (!Array.isArray(list)) { console.error('[crowd] setPaths expects an array of [{x,z},...] polylines', list); return; }
    paths = list.map(prepPath).filter(Boolean);
    if (paths.length < list.length) console.warn(`[crowd] ${list.length - paths.length} path(s) ignored (need 2+ finite {x,z} points)`);
    if (reapply && mode !== 'none') apply();
  }
  function setArea(rect, reapply = true) {
    if (!rect || ![rect.x0, rect.x1, rect.z0, rect.z1].every(Number.isFinite) || rect.x1 <= rect.x0 || rect.z1 <= rect.z0) {
      console.error('[crowd] setArea expects {x0,x1,z0,z1} with x0<x1, z0<z1', rect); return;
    }
    area = { x0: rect.x0, x1: rect.x1, z0: rect.z0, z1: rect.z1 };
    if (reapply && (mode === 'sit' || ((mode === 'stroll' || mode === 'idle') && !paths.length))) apply();
  }

  return {
    group, update, setMode, setPaths, setArea,
    get mode() { return mode; },
    get active() { return active; },
    members: agents.map(a => a.p),
    dispose() { for (const a of agents) a.p.dispose?.(); group.removeFromParent(); },
  };
}
