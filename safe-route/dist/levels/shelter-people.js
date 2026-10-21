// People in the shelter. Every evacuee owns a seat on a mat (the camp already laid out bags and shoes there);
// arriving, leaving and toilet trips move that same person between an entrance and the seat, so nobody pops in
// or swaps outfits. Plus the reception queue, a volunteer and two radio listeners on chairs, the shelter manager
// (vest, no baton) and 서연 (hidden until the director shows her). Works without engine/crowd.js.
import { SPOT, RECEPTION, RADIO, CHAIRS } from './shelter-camp.js';

const yawTo = (dx, dz) => Math.atan2(-dx, -dz);
const turn = (a, b) => Math.atan2(Math.sin(b - a), Math.cos(b - a));
const AN = -6.9, AS = 9.0;   // north / south driving aisles (z)
// Entrance paths [x, z]: they start out of sight (stair flight, ramp slopes, lobby and toilet doors) and end on an aisle.
const WAYS = {
  stairs: [[4.3, -19.6], [4.3, -17.3], [5, -16.0], [5, -10.4], [3.5, -9.2], [3.5, AN]],
  access: [[-13, -23], [-13, -15.6], [-13, -10.0], [-12.4, AN]],
  east: [[24.6, -3.4], [22.8, -0.5], [20.3, -1.2], [20.3, AN]],
  toilet: [[24.6, 3.4], [22.8, 0.5], [20.3, -1.2], [20.3, AN]],
  ramp: [[11, 23], [11, 15.4], [11, 10.6], [10.2, AS]],
};
const IN = ['stairs', 'stairs', 'stairs', 'east', 'ramp', 'access'], OUT = ['stairs', 'stairs', 'east', 'access'];
export const ENTRANCES = ['stairs', 'ramp', 'access', 'east'];
const DESK = { x: 2.55, z: -9.5, wait: 2.6, anim: 'talk', face: Math.PI };   // arrivals by stair A write in the roster
const COLS = [-19.3, -17.1, -14.9, -12.7, -10.5, -3.9, -1.7, 2.7, 4.9];      // gaps between mats clear of pillars, radio table, bins
const QUOTA = { high: { seats: 26, walk: 4 }, medium: { seats: 16, walk: 3 }, low: { seats: 8, walk: 2 } };
const FIRE_CLEAR = 6.5;   // seats this close to a fire are left (their people moved away)

// Walk-zone height at (x, z); the lowest zone wins where zones overlap (stair foot under the top landing).
export function heightAt(walk, x, z) {
  let y = Infinity;
  for (const w of walk) {
    if (x < w.x0 || x > w.x1 || z < w.z0 || z > w.z1) continue;
    const r = w.ramp, h = r ? r.y0 + (r.y1 - r.y0) * (r.axis === 'x' ? (x - w.x0) / (w.x1 - w.x0) : (z - w.z0) / (w.z1 - w.z0)) : w.y;
    if (h < y) y = h;
  }
  return y === Infinity ? 0 : y;
}

// Entrance -> aisle -> gap column -> seat row -> seat. Reverse it to leave.
function seatPath(way, s, desk = false) {
  const pts = WAYS[way].map(([x, z]) => ({ x, z }));
  if (desk && way === 'stairs') pts.splice(4, 0, { ...DESK });
  const az = pts[pts.length - 1].z, gx = COLS.reduce((a, c) => (Math.abs(c - s.x) < Math.abs(a - s.x) ? c : a));
  pts.push({ x: gx, z: az }, { x: gx, z: s.z }, { x: s.x, z: s.z });
  return pts;
}

export function buildPeople(kit, people, { seats, walk }) {
  const { THREE } = kit;
  const group = new THREE.Group(); group.name = 'shelter-people';
  const Q = QUOTA[kit.quality] || QUOTA.high, r = kit.rng(733);
  const pick = (list) => list[(r() * list.length) | 0];
  function at(preset, seed, x, z, yaw, anim, y = 0) {
    let p = null;
    try { p = people?.createPerson?.({ preset, seed }); } catch (err) { console.error('[shelter] createPerson failed', preset, err); }
    if (!p?.group) p = { group: new THREE.Group(), setAnim() {}, setSpeed() {}, setProp() {}, lookAt() {}, update() {} };
    p.group.position.set(x, y, z); p.group.rotation.y = yaw; p.setAnim(anim, { instant: true });
    group.add(p.group);
    return p;
  }

  // ---- evacuees, one per chosen seat; a shuffled order decides who arrives / leaves first ----
  const n = Math.min(Q.seats, seats.length);
  const ev = Array.from({ length: n }, (_, i) => {
    const s = seats[Math.floor(i * seats.length / n)];
    return { p: at('crowd', 600 + i, s.x, s.z, s.yaw, 'sit', 0.012), home: s, mode: 'seat', path: null, i: 0, pause: 0, face: null, done: null,
      speed: 0.95 + r() * 0.4, brisk: false, acc: 0, away: 0, rank: 0 };
  });
  const order = ev.slice();
  for (let i = order.length - 1; i > 0; i--) { const j = (r() * (i + 1)) | 0; [order[i], order[j]] = [order[j], order[i]]; }
  order.forEach((e, k) => { e.rank = k; });

  // ---- staff, queue, listeners, 서연 ----
  const manager = at('guide', 41, RECEPTION.x - 1.25, RECEPTION.z + 0.1, yawTo(0.9, -0.95), 'idle');
  manager.setProp('baton', false);
  const chair = ([dx, dz, ry], b) => ({ x: b.x + dx, z: b.z + dz, ry });
  const vc = chair(CHAIRS.reception[1], RECEPTION);
  const volunteer = at('crowd', 42, vc.x, vc.z, vc.ry, 'sitChair');
  const listeners = CHAIRS.radio.slice(0, 2).map((c, i) => { const q = chair(c, RADIO); return at(i ? 'elder' : 'crowd', 43 + i, q.x, q.z, q.ry, 'sitChair'); });
  const queue = [[1.6, -9.5], [1.9, -10.35], [2.05, -11.15]].slice(0, Q.walk > 2 ? 3 : 2)
    .map(([x, z], i) => at('crowd', 50 + i, x, z, Math.PI + (i ? (r() - 0.5) * 0.4 : 0), 'idle'));
  const seoyeon = at('seoyeon', 7, SPOT.x + 1.5, SPOT.z + 1.9, yawTo(-1.5, -0.5), 'idle');
  seoyeon.group.visible = false;   // the director shows her
  const still = [manager, volunteer, ...listeners, ...queue, seoyeon];

  let mode = 'settled', phase = 'calm', fire = null, timer = 1, frame = 0, last = null, ways = ENTRANCES;
  const open = (list) => { const l = list.filter((w) => ways.includes(w)); return l.length ? l : ways; };
  const out = (h) => !!fire && Math.hypot(h.x - fire.x, h.z - fire.z) < FIRE_CLEAR;
  const nearPlayer = (e) => !!last && Math.hypot(e.home.x - last.x, e.home.z - last.z) < 3;
  const busy = () => ev.reduce((k, e) => k + (e.mode === 'walk' || e.mode === 'away'), 0);

  function seat(e, snap = false) {
    const g = e.p.group;
    e.mode = 'seat'; e.path = null; g.visible = true; g.position.set(e.home.x, 0.012, e.home.z);
    if (snap) g.rotation.y = e.home.yaw;
    e.p.setAnim('sit', snap ? { instant: true } : {}); e.p.setProp('backpack', false);
  }
  function hide(e) { e.mode = 'gone'; e.path = null; e.p.group.visible = false; }
  function go(e, path, done, fromSeat = false) {
    const g = e.p.group;
    e.mode = 'walk'; e.path = path; e.done = done; e.face = null; g.visible = true;
    e.brisk = phase === 'alert' && mode === 'arriving';
    e.p.setSpeed(e.speed * (e.brisk ? 1.3 : 1));
    if (fromSeat) { e.i = 0; e.pause = 1.0; e.p.setAnim('idle'); }   // stand up first
    else { const p0 = path[0]; g.position.set(p0.x, heightAt(walk, p0.x, p0.z), p0.z); g.rotation.y = yawTo(path[1].x - p0.x, path[1].z - p0.z); e.i = 1; e.pause = 0; }
  }
  function reset() {
    timer = 1;
    for (const e of ev) {
      if (out(e.home) || (mode === 'arriving' && e.rank >= Math.round(ev.length * 0.55))) hide(e);
      else seat(e, true);
    }
    for (const q of queue) q.group.visible = mode === 'arriving';
    listeners.forEach((l, i) => { l.group.visible = mode !== 'leaving' || i === 0; });
    applyPhase();
  }
  function applyPhase() {
    const alert = phase === 'alert';
    manager.setAnim(alert || mode === 'arriving' ? 'talk' : 'idle');
    queue.forEach((q, i) => q.setAnim(alert && i === 1 ? 'phone' : 'idle'));
  }
  function launch() {
    if (busy() >= (mode === 'settled' ? Math.min(2, Q.walk) : Q.walk)) return;
    if (mode === 'arriving') {
      const e = order.find((x) => x.mode === 'gone' && !out(x.home));
      if (e) { e.p.setProp('backpack', r() < 0.5); go(e, seatPath(pick(open(IN)), e.home, true), seat); }
    } else if (mode === 'leaving') {
      if (ev.reduce((k, x) => k + (x.mode === 'seat'), 0) <= Math.round(ev.length * 0.25)) return;
      const e = order.find((x) => x.mode === 'seat' && !nearPlayer(x));
      if (e) { e.p.setProp('backpack', true); go(e, seatPath(pick(open(OUT)), e.home).reverse(), hide, true); }
    } else if (!fire && r() < 0.6) {   // settled: now and then someone goes to the toilets by the lift hall
      const e = pick(ev.filter((x) => x.mode === 'seat' && !nearPlayer(x)));
      if (e) go(e, seatPath('toilet', e.home).reverse(), (x) => { x.mode = 'away'; x.p.group.visible = false; x.away = 10 + r() * 12; }, true);
    }
  }
  function step(e, dt, pl) {
    const g = e.p.group;
    if (e.pause > 0) {
      e.pause -= dt;
      if (e.face != null) g.rotation.y += turn(g.rotation.y, e.face) * Math.min(1, dt * 5);
      if (e.pause > 0) return;
      e.face = null;
    }
    if (e.i >= e.path.length) { const f = e.done; e.done = null; f?.(e); return; }
    const t = e.path[e.i], dx = t.x - g.position.x, dz = t.z - g.position.z, d = Math.hypot(dx, dz);
    if (d < 0.06) { e.i++; if (t.wait) { e.pause = t.wait; e.face = t.face ?? null; e.p.setAnim(t.anim || 'idle'); } return; }
    if (pl) {   // wait for the player instead of walking through them
      const px = pl.x - g.position.x, pz = pl.z - g.position.z;
      if (px * px + pz * pz < 1.2 && px * dx + pz * dz > 0) { e.p.setAnim('idle'); return; }
    }
    const k = Math.min(d, e.speed * (e.brisk ? 1.3 : 1) * dt) / d;
    g.position.x += dx * k; g.position.z += dz * k; g.position.y = heightAt(walk, g.position.x, g.position.z);
    g.rotation.y += turn(g.rotation.y, yawTo(dx, dz)) * Math.min(1, dt * 7);
    const stair = g.position.x > 3.6 && g.position.x < 4.95 && g.position.z < -17.45;
    e.p.setAnim(stair ? (dz < 0 ? 'stairsUp' : 'stairsDown') : e.brisk ? 'brisk' : 'walk');
  }

  function update(dt, t, pl) {
    frame++; if (pl) last = pl;
    timer -= dt;
    if (timer <= 0) { timer = mode === 'arriving' ? 2.2 + r() * 1.5 : mode === 'leaving' ? 2.8 + r() * 2 : 7 + r() * 6; launch(); }
    for (const e of ev) {
      if (e.mode === 'walk') { step(e, dt, pl); e.p.update(dt); }
      else if (e.mode === 'away') { e.away -= dt; if (e.away <= 0) go(e, seatPath('toilet', e.home), seat); }
      else if (e.mode === 'seat') {   // seated people animate at 1/3 rate (breathing only)
        e.acc += dt;
        if ((frame + e.rank) % 3) continue;
        const g = e.p.group, a = turn(g.rotation.y, e.home.yaw);
        if (Math.abs(a) > 0.01) g.rotation.y += a * Math.min(1, e.acc * 4);
        e.p.update(e.acc); e.acc = 0;
      }
    }
    for (const p of still) if (p.group.visible) p.update(dt);
    const near = (p, d) => pl && Math.hypot(pl.x - p.group.position.x, pl.z - p.group.position.z) < d;
    if (near(manager, 5)) manager.lookAt(pl.x, pl.z); else manager.lookAt(null);
    if (seoyeon.group.visible && near(seoyeon, 6)) seoyeon.lookAt(pl.x, pl.z); else seoyeon.lookAt(null);
  }

  reset();
  return {
    group, npcs: { shelterManager: manager, seoyeon, volunteer },
    setCrowd(m) { mode = m; reset(); },
    setPhase(p) { phase = p; applyPhase(); },
    setFire(pos) { fire = pos; reset(); },
    setWays(list) { ways = list; },   // open entrances: arrivals and leavers only use these   // {x, z} or null: clears the seats around it and pauses toilet trips
    update,
  };
}
