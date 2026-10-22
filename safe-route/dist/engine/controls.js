// Player, input and camera rig. WASD/arrows move, Shift runs (stamina), Space jumps, the mouse looks
// (pointer lock after one click; touch drag and Alt+arrows also work), E/Enter interacts, V toggles the view;
// M, Tab, I, P, H, Esc, F and V are also emitted to the director (onKey).
// Ground height comes from level.walk zones, level.solids are AABBs the capsule slides along, and the
// third-person camera is pulled in when a solid or a stair/floor surface blocks its line of sight.
// A profile (setProfile) models mobility: no running, no jumping, a seated eye height, no stairs.
// Yaw uses the three.js convention: rotation.y = yaw, yaw 0 faces north (-z), positive yaw turns left (west).
import * as THREE from 'three';

export const PLAYER = { radius: 0.32, height: 1.72, eye: 1.62, walk: 3.0, run: 5.2 };
const TP = { dist: 3.4, height: 1.7, shoulder: 0.45 };
const FOV = 70, FOV_RUN = 4, D2R = Math.PI / 180, LOOK = 0.0042, LOCK_LOOK = 0.0024;
const JUMP_V = 3.6, GRAVITY = 12, STAIR_SLOPE = 0.2;
const FLASH = 6; // flashlight candela; dimmed further when the beam hits something close
const UI_KEYS = new Set(['KeyM', 'Tab', 'KeyI', 'Escape', 'KeyF', 'KeyV', 'KeyP', 'KeyH', 'KeyG', 'KeyQ']);
const MOVE = { KeyW: [0, 1], ArrowUp: [0, 1], KeyS: [0, -1], ArrowDown: [0, -1], KeyA: [-1, 0], ArrowLeft: [-1, 0], KeyD: [1, 0], ArrowRight: [1, 0] };
const damp = (k, dt) => 1 - Math.exp(-k * dt);
const wrap = (a) => Math.atan2(Math.sin(a), Math.cos(a));
const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const editable = (t) => !!t && (t.isContentEditable || /^(INPUT|SELECT|TEXTAREA)$/.test(t.tagName || ''));

// Ground under (x, z): the highest walk surface within `below` m below / 0.45 m above feetY, or null.
export function groundAt(walk, x, z, feetY, any = false, below = 0.6) {
  let best = null, slope = 0;
  for (const w of walk || []) {
    if (x < w.x0 - 0.02 || x > w.x1 + 0.02 || z < w.z0 - 0.02 || z > w.z1 + 0.02) continue;
    let y = w.y ?? 0, s = 0;
    if (w.ramp) {
      const r = w.ramp, span = r.axis === 'x' ? w.x1 - w.x0 : w.z1 - w.z0;
      const k = clamp(((r.axis === 'x' ? x - w.x0 : z - w.z0) / (span || 1)), 0, 1);
      y = r.y0 + (r.y1 - r.y0) * k; s = Math.abs(r.y1 - r.y0) / (span || 1);
    }
    if (!any && (y > feetY + 0.45 || y < feetY - below)) continue;
    if (best === null || y > best) { best = y; slope = s; }
  }
  return best === null ? null : { y: best, slope };
}

// Tops of solids you can stand on (cars, walls, fences, crates) join the walk zones as flat surfaces.
const topsOf = (solids) => solids.map((s) => ({ x0: s.x0, x1: s.x1, z0: s.z0, z1: s.z1, y: s.y1 }));
// Ground with a little footprint: a seam narrower than the body between two walk zones is not a hole.
function groundNear(walk, x, z, feetY) {
  for (const [ox, oz] of [[0, 0], [0.25, 0], [-0.25, 0], [0, 0.25], [0, -0.25]]) { const g = groundAt(walk, x + ox, z + oz, feetY); if (g) return g; }
  return null;
}

// Push a circle (radius r) out of every solid that overlaps the vertical span [feetY, feetY + height].
export function collide(px, pz, feetY, solids, r = PLAYER.radius, h = PLAYER.height) {
  for (let pass = 0; pass < 3; pass++) {
    let moved = false;
    for (const s of solids) {
      if (s.off || s.y1 <= feetY + 0.05 || s.y0 >= feetY + h) continue;
      const cx = clamp(px, s.x0, s.x1), cz = clamp(pz, s.z0, s.z1), dx = px - cx, dz = pz - cz, d2 = dx * dx + dz * dz;
      if (d2 >= r * r) continue;
      if (d2 > 1e-10) { const d = Math.sqrt(d2); px += (dx / d) * (r - d); pz += (dz / d) * (r - d); }
      else { // centre inside the box: leave by the shallowest side
        const l = px - s.x0, rr = s.x1 - px, f = pz - s.z0, b = s.z1 - pz, m = Math.min(l, rr, f, b);
        if (m === l) px = s.x0 - r; else if (m === rr) px = s.x1 + r; else if (m === f) pz = s.z0 - r; else pz = s.z1 + r;
      }
      moved = true;
    }
    if (!moved) break;
  }
  return [px, pz];
}

// Segment o→o+d (t in 0..1) against an AABB grown by pad; returns entry t, or Infinity (also when o is inside).
function segBox(o, d, s, pad) {
  let t0 = 0, t1 = 1;
  const lo = [s.x0 - pad, s.y0 - pad, s.z0 - pad], hi = [s.x1 + pad, s.y1 + pad, s.z1 + pad], O = [o.x, o.y, o.z], D = [d.x, d.y, d.z];
  if (O[0] > lo[0] && O[0] < hi[0] && O[1] > lo[1] && O[1] < hi[1] && O[2] > lo[2] && O[2] < hi[2]) return Infinity;
  for (let i = 0; i < 3; i++) {
    if (Math.abs(D[i]) < 1e-9) { if (O[i] < lo[i] || O[i] > hi[i]) return Infinity; continue; }
    let a = (lo[i] - O[i]) / D[i], b = (hi[i] - O[i]) / D[i];
    if (a > b) [a, b] = [b, a];
    t0 = Math.max(t0, a); t1 = Math.min(t1, b);
    if (t0 > t1) return Infinity;
  }
  return t0;
}

function makeBeam() {
  const LEN = 7, R = Math.tan(0.42) * LEN;
  const geo = new THREE.ConeGeometry(R, LEN, 32, 1, true).translate(0, -LEN / 2, 0).rotateX(-Math.PI / 2); // apex at origin, opens toward +z
  const mat = new THREE.ShaderMaterial({
    uniforms: { uLen: { value: LEN }, uStrength: { value: 0 }, uColor: { value: new THREE.Color(1, 0.93, 0.82) } },
    vertexShader: `uniform float uLen; varying float vT; varying vec3 vN, vV;
      void main() { vT = position.z / uLen; vec4 mv = modelViewMatrix * vec4(position, 1.0); vV = -mv.xyz; vN = normalMatrix * normal; gl_Position = projectionMatrix * mv; }`,
    fragmentShader: `uniform vec3 uColor; uniform float uStrength; varying float vT; varying vec3 vN, vV;
      void main() { float f = abs(dot(normalize(vN), normalize(vV)));
        float a = uStrength * pow(f, 1.5) * smoothstep(0.0, 0.18, vT) * pow(1.0 - vT, 1.6); gl_FragColor = vec4(uColor * a, 1.0); }`,
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, fog: false,
  });
  const m = new THREE.Mesh(geo, mat);
  m.userData.noAO = true; m.castShadow = m.receiveShadow = false; m.visible = false; m.renderOrder = 10;
  return m;
}

export function createControls({ camera, canvas, scene, getLevel, reduceMotion = false, quality = 'high' }) {
  let RM = !!reduceMotion, enabled = true, view = 'first', viewK = 0, override = null, blend = null;
  const keys = new Set(), keyFns = new Set(), interactFns = new Set(), nearestFns = new Set();
  const position = new THREE.Vector3(), vel = new THREE.Vector2(), pivot = new THREE.Vector3();
  const look = { yaw: 0, pitch: -0.05 }, cam = { yaw: 0, pitch: -0.05 };
  let bodyYaw = 0, phase = 0, bobAmp = 0, camFrac = 1, nearest = null, model = null, carrying = null, flashOn = false;
  let anim = '', lastGround = 0, snap = true, beamStrength = 1, flashK = 1;
  let shakeT = 0, shakeDur = 1, shakeA = 0;
  let vy = 0, air = false, stamina = 1, winded = false, blocked = false, sens = 1, peakY = 0, move = null, actAnim = null, actT = 0;
  const prof = { run: true, jump: true, eye: PLAYER.eye, stairs: true };
  const blockedFns = new Set(), landFns = new Set();

  const emit = (set, arg) => { for (const fn of [...set]) { try { fn(arg); } catch (err) { console.error('[engine/controls] listener failed', err); } } };
  const sub = (set, fn) => { set.add(fn); return () => set.delete(fn); };

  // Flashlight: one SpotLight that always stays in the light list (intensity 0 when off) so toggling it
  // never recompiles shaders, plus a faint additive cone for the beam.
  const spot = new THREE.SpotLight(0xfff1dc, 0, 28, 0.42, 0.6, 1.6);
  spot.shadow.mapSize.set(512, 512); spot.shadow.camera.near = 0.4; spot.shadow.bias = -0.0006; spot.shadow.autoUpdate = false;
  spot.castShadow = quality === 'high';
  const beam = makeBeam(), aim = new THREE.Vector3();
  scene.add(spot, spot.target, beam);

  const player = {
    get x() { return position.x; }, set x(v) { position.x = v; },
    get y() { return position.y; }, set y(v) { position.y = v; },
    get z() { return position.z; }, set z(v) { position.z = v; },
    get yaw() { return look.yaw; }, set yaw(v) { look.yaw = cam.yaw = bodyYaw = v; },
    position, speedMul: 1, staminaUse: 1, radius: PLAYER.radius, height: PLAYER.height, eye: PLAYER.eye,
    get model() { return model; },
    get stamina() { return stamina; },
    get winded() { return winded; },
    get flashlight() { return flashOn; },
    setFlashlight(on) {
      flashOn = !!on; spot.intensity = flashOn ? FLASH * flashK : 0; spot.shadow.autoUpdate = flashOn && spot.castShadow;
      beam.visible = flashOn; model?.setProp?.('flashlight', flashOn);
    },
    get carrying() { return carrying; },
    set carrying(v) {
      carrying = v === 'bag' || v === 'water' ? v : null;
      model?.setProp?.('backpack', carrying === 'bag'); model?.setProp?.('waterJug', carrying === 'water');
    },
    // spawn: a name in level.spawns or { x, y?, z, yaw? }. Returns false when the name is unknown.
    teleport(spawn) {
      const L = getLevel();
      const s = typeof spawn === 'string' ? L?.spawns?.[spawn] : spawn;
      if (!s || !Number.isFinite(s.x) || !Number.isFinite(s.z)) { console.error('[engine/controls] unknown spawn', spawn, 'in level', L?.id); return false; }
      const g = groundAt(L?.walk, s.x, s.z, s.y ?? 0, s.y === undefined);
      position.set(s.x, s.y ?? g?.y ?? 0, s.z);
      if (Number.isFinite(s.yaw)) player.yaw = s.yaw;
      look.pitch = cam.pitch = -0.05; vel.set(0, 0); lastGround = position.y; snap = true; vy = 0; air = false; move = null;
      return true;
    },
  };

  function setModel(m) {
    if (model?.group) model.group.removeFromParent();
    model = m; if (!m?.group) return;
    m.group.traverse((o) => { if (o.isMesh) o.castShadow = true; });
    scene.add(m.group);
    player.carrying = carrying; m.setProp?.('flashlight', flashOn); anim = '';
  }

  // ---- input ----
  const inWorld = (t) => !t || t === document.body || t === canvas || t === document.documentElement;
  function onKeyDown(e) {
    if (editable(e.target)) return;
    if (UI_KEYS.has(e.code)) {
      if (!enabled || e.repeat) return;
      if (e.code === 'Tab') { if (!keyFns.size || !inWorld(e.target)) return; e.preventDefault(); }
      if (e.code === 'KeyV') setView(view === 'first' ? 'third' : 'first');
      emit(keyFns, { code: e.code, key: e.key });
      return;
    }
    if (!enabled) return;
    if (e.code === 'KeyE' || ((e.code === 'Enter' || e.code === 'NumpadEnter') && inWorld(e.target))) {
      e.preventDefault();
      if (!e.repeat && nearest) emit(interactFns, nearest);
      return;
    }
    if (e.code === 'Space' && inWorld(e.target)) {
      e.preventDefault();
      if (!e.repeat && !air && !move && prof.jump && !override && !vault()) { vy = JUMP_V; air = true; peakY = position.y; stamina = Math.max(0, stamina - 0.06); }
      return;
    }
    if (MOVE[e.code] || e.code.startsWith('Shift') || e.code.startsWith('Alt')) {
      keys.add(e.code);
      if (e.code.startsWith('Arrow') || e.code.startsWith('Alt')) e.preventDefault();
    }
  }
  const onKeyUp = (e) => { keys.delete(e.code); };
  const clearKeys = () => { keys.clear(); drag = null; };
  // Mouse: one click on the world locks the pointer, then plain mouse movement looks around (no button held).
  // Touch (and a mouse when the lock is refused) falls back to dragging.
  let drag = null, relock = false;
  const locked = () => document.pointerLockElement === canvas;
  const lock = () => { try { const p = canvas.requestPointerLock?.(); p?.catch?.(() => {}); } catch { /* refused: drag still works */ } };
  function onPointerDown(e) {
    if (!enabled || e.button > 2 || e.button === 1) return;
    canvas.focus?.({ preventScroll: true });
    if (e.pointerType === 'mouse' && !locked()) lock();
    else if (locked() && e.button === 0) emit(keyFns, { code: 'Mouse0', key: '' });
    if (locked()) return;
    drag = { id: e.pointerId, x: e.clientX, y: e.clientY };
    try { canvas.setPointerCapture(e.pointerId); } catch { /* pointer already gone */ }
  }
  const turn = (dx, dy, k) => { look.yaw -= dx * k * sens; look.pitch = clamp(look.pitch - dy * k * sens * 0.85, -1.25, 1.1); };
  function onPointerMove(e) {
    if (!enabled) return;
    if (locked()) { if (e.pointerType === 'mouse' || !e.pointerType) turn(e.movementX || 0, e.movementY || 0, LOCK_LOOK); return; }
    if (!drag || e.pointerId !== drag.id) return;
    const dx = e.clientX - drag.x, dy = e.clientY - drag.y; drag.x = e.clientX; drag.y = e.clientY;
    turn(dx, dy, LOOK);
  }
  const onPointerUp = (e) => { if (drag && e.pointerId === drag.id) drag = null; };
  const onContext = (e) => e.preventDefault();
  canvas.style.touchAction = 'none';
  addEventListener('keydown', onKeyDown); addEventListener('keyup', onKeyUp); addEventListener('blur', clearKeys);
  canvas.addEventListener('pointerdown', onPointerDown); canvas.addEventListener('pointermove', onPointerMove);
  canvas.addEventListener('pointerup', onPointerUp); canvas.addEventListener('pointercancel', onPointerUp);
  canvas.addEventListener('contextmenu', onContext);

  function setView(v) { if (v === 'first' || v === 'third') view = v; else console.error('[engine/controls] unknown view', v); }
  // Menus need the cursor: release the lock while controls are off and take it back afterwards
  // (the click that closed the menu counts as the user gesture the browser asks for).
  function setEnabled(on) {
    enabled = !!on;
    if (!enabled) { clearKeys(); if (locked()) { relock = true; document.exitPointerLock?.(); } }
    else if (relock) { relock = false; lock(); }
  }
  function setOverride(fn) {
    if (!fn && override && !RM) blend = { k: 0, pos: camera.position.clone(), quat: camera.quaternion.clone() };
    override = typeof fn === 'function' ? fn : null;
  }

  // A point is behind a wall when solids block the line to it at both 0.3 m and 1.5 m (low furniture does not
  // hide it). Its own furniture is ignored: solids holding the point, and the last 0.35 m before it.
  const so = { x: 0, y: 0, z: 0 }, sd = { x: 0, y: 0, z: 0 };
  function hidden(p, solids) {
    const dx = p.x - position.x, dz = p.z - position.z, d = Math.hypot(dx, dz);
    if (d < 0.4) return false;
    const k = (d - 0.35) / d, dy = ((p.y ?? position.y) - position.y) * k;
    return [0.3, 1.5].every((h) => {
      so.x = position.x; so.y = position.y + h; so.z = position.z; sd.x = dx * k; sd.y = dy; sd.z = dz * k;
      return solids.some((s) => s && !s.off && !(p.x >= s.x0 && p.x <= s.x1 && p.z >= s.z0 && p.z <= s.z1) && segBox(so, sd, s, 0) <= 1);
    });
  }

  // ---- interaction: nearest enabled point within r, in front (< 75°) of the camera or the body, not behind a wall ----
  function findNearest(L) {
    let best = null, bestScore = Infinity;
    for (const p of L?.points || []) {
      if (!p || p.enabled === false) continue;
      const dx = p.x - position.x, dz = p.z - position.z, d = Math.hypot(dx, dz);
      if (d > (p.r ?? 1.6) || Math.abs((p.y ?? position.y) - position.y) > 2.2) continue;
      let off = 0;
      if (d > 0.45) {
        const a = Math.atan2(-dx, -dz);
        off = Math.min(Math.abs(wrap(a - cam.yaw)), Math.abs(wrap(a - bodyYaw)));
        if (off > 75 * D2R) continue;
      }
      const score = d + off * 0.6;
      if (score < bestScore && !hidden(p, L.solids || [])) { bestScore = score; best = p; }
    }
    return best;
  }

  function shake(amount = 0.15, seconds = 1.2) { shakeA = amount; shakeDur = shakeT = Math.max(0.05, seconds); }

  // ---- vault / climb (Space in front of a low solid) ----
  // A solid 0.3..1.5 m tall and up to 1 m thick with a free landing behind it is vaulted; anything up to
  // 2.3 m with room on top is climbed onto. Returns false when there is nothing to get over (a plain jump).
  function vault() {
    const L = getLevel(); if (!L || !prof.jump) return false;
    const yaw = Math.hypot(vel.x, vel.y) > 0.3 ? Math.atan2(-vel.x, -vel.y) : view === 'first' ? cam.yaw : bodyYaw;
    const dx = -Math.sin(yaw), dz = -Math.cos(yaw), feet = position.y, solids = (L.solids || []).filter((s) => s && !s.off);
    so.x = position.x; so.y = feet + 0.25; so.z = position.z; sd.x = dx; sd.y = 0; sd.z = dz;
    let best = null, bt = Infinity;
    for (const s of solids) {
      const h = s.y1 - feet; if (h < 0.3 || h > 2.3 || s.y0 > feet + 0.3) continue;
      const t = segBox(so, sd, s, PLAYER.radius * 0.6); if (t < bt) { bt = t; best = s; }
    }
    if (!best) return false;
    const exitT = Math.min(dx > 1e-6 ? (best.x1 - position.x) / dx : dx < -1e-6 ? (best.x0 - position.x) / dx : Infinity,
      dz > 1e-6 ? (best.z1 - position.z) / dz : dz < -1e-6 ? (best.z0 - position.z) / dz : Infinity);
    const h = best.y1 - feet, thick = exitT - bt, walk = L.walk || [];
    const free = (x, z, y) => { const [cx, cz] = collide(x, z, y, solids); return Math.hypot(cx - x, cz - z) < 0.05; };
    const surf = walk.concat(topsOf(solids.filter((s) => s !== best)));
    if (h <= 1.5 && thick <= 1.0) { // over it
      const lx = position.x + dx * (exitT + 0.5), lz = position.z + dz * (exitT + 0.5);
      const gl = groundAt(surf, lx, lz, best.y1, false, 60);
      if (gl && free(lx, lz, gl.y)) {
        move = { t: 0, dur: 0.5 + h * 0.15, x0: position.x, z0: position.z, y0: feet, x1: lx, z1: lz, y1: Math.max(gl.y, feet - 0.4), arc: best.y1 + 0.2 - (feet + Math.max(gl.y, feet - 0.4)) / 2, anim: 'run' };
        stamina = Math.max(0, stamina - 0.08); return true;
      }
    }
    if (thick >= 0.4) { // onto it
      const k = bt + Math.min(0.45, thick / 2), lx = position.x + dx * k, lz = position.z + dz * k;
      if (!free(lx, lz, best.y1)) return false;
      move = { t: 0, dur: 0.35 + h * 0.3, x0: position.x, z0: position.z, y0: feet, x1: lx, z1: lz, y1: best.y1, arc: 0.2, anim: h > 1 ? 'stairsUp' : 'run' };
      stamina = Math.max(0, stamina - 0.05 - h * 0.05); return true;
    }
    return false;
  }

  // ---- per frame ----
  const fwd = new THREE.Vector3(), right = new THREE.Vector3(), up = new THREE.Vector3(), want = new THREE.Vector3(), seg = new THREE.Vector3();
  const euler = new THREE.Euler(0, 0, 0, 'YXZ'), handLocal = new THREE.Vector3(), origin = new THREE.Vector3(), tmp = new THREE.Vector3();
  function update(dt) {
    const L = getLevel(), walk = L?.walk || [], allSolids = L?.solids || [];
    // look + move input
    let kx = 0, kz = 0;
    const alt = keys.has('AltLeft') || keys.has('AltRight');
    for (const c of keys) {
      const m = MOVE[c]; if (!m) continue;
      if (alt && c.startsWith('Arrow')) { look.yaw -= m[0] * 1.9 * dt; look.pitch = clamp(look.pitch + m[1] * 1.2 * dt, -1.25, 1.1); continue; }
      kx += m[0]; kz += m[1];
    }
    const len = Math.hypot(kx, kz); if (len > 1) { kx /= len; kz /= len; }
    // Running spends stamina (faster with a heavy bag); empty, you walk until it is back to 35 %.
    const running = len > 0 && prof.run && !winded && enabled && (keys.has('ShiftLeft') || keys.has('ShiftRight'));
    const use = clamp(Number(player.staminaUse) || 1, 0.5, 3);
    stamina = clamp(stamina + (running ? -dt / 7 * use : dt / (len > 0 ? 11 : 5)), 0, 1);
    if (stamina <= 0) winded = true; else if (winded && stamina > 0.35) winded = false;
    const speed = (running ? PLAYER.run : PLAYER.walk) * clamp(Number(player.speedMul) || 1, 0.2, 1.5) * (air ? 0.9 : 1);
    const sy = Math.sin(look.yaw), cy = Math.cos(look.yaw);
    const tx = (-sy * kz + cy * kx) * speed, tz = (-cy * kz - sy * kx) * speed;
    const a = damp(len > 0 ? 9 : 12, dt);
    vel.x += (tx - vel.x) * a; vel.y += (tz - vel.y) * a;
    if (Math.abs(vel.x) < 1e-3 && Math.abs(vel.y) < 1e-3) vel.set(0, 0);

    // move in sub-steps (no tunnelling through thin walls), slide along solids
    const near = allSolids.filter((s) => s && !s.off && s.x0 - 3 < position.x && s.x1 + 3 > position.x && s.z0 - 3 < position.z && s.z1 + 3 > position.z);
    const surf = near.length ? walk.concat(topsOf(near)) : walk;
    let g = null, climb = 0;
    if (move) { // scripted vault / climb: ease along, with an arc over the top
      move.t = Math.min(1, move.t + dt / move.dur);
      const k = move.t, e = k * k * (3 - 2 * k);
      position.set(move.x0 + (move.x1 - move.x0) * e, move.y0 + (move.y1 - move.y0) * e + Math.sin(Math.PI * k) * move.arc, move.z0 + (move.z1 - move.z0) * e);
      vel.set(0, 0);
      if (k >= 1) { move = null; lastGround = peakY = position.y; air = true; vy = 0; }
    } else {
      const steps = Math.max(1, Math.ceil((Math.hypot(vel.x, vel.y) * dt) / 0.12));
      let px = position.x, pz = position.z;
      for (let i = 0; i < steps; i++) [px, pz] = collide(px + (vel.x * dt) / steps, pz + (vel.y * dt) / steps, position.y, near);
      // Without stairs (wheelchair) a stair flight is a wall; the director may offer help (onBlocked).
      if (!prof.stairs && !air) {
        const ng = groundAt(walk, px, pz, position.y);
        const stop = !!ng && ng.slope > STAIR_SLOPE && !(groundAt(walk, position.x, position.z, position.y)?.slope > STAIR_SLOPE);
        if (stop) { if (!blocked) emit(blockedFns, { kind: 'stairs', x: position.x, y: position.y, z: position.z, toX: px, toZ: pz }); px = position.x; pz = position.z; vel.set(0, 0); }
        blocked = stop;
      }
      if (dt > 0) { // never keep pushing into a wall
        const ax = (px - position.x) / dt, az = (pz - position.z) / dt;
        if (Math.abs(ax) < Math.abs(vel.x)) vel.x = ax;
        if (Math.abs(az) < Math.abs(vel.y)) vel.y = az;
      }
      position.x = px; position.z = pz;
      g = groundAt(surf, px, pz, position.y, false, air ? 50 : 0.6);
      // Walked off a ledge (a floor further down, and no floor within a body width): fall.
      if (!air && !g && groundAt(surf, px, pz, position.y, false, 60) && !groundNear(surf, px, pz, position.y)) { air = true; vy = 0; peakY = position.y; }
      if (air) { // jump / fall arc; land on the highest surface under the feet
        vy -= GRAVITY * dt; position.y += vy * dt; peakY = Math.max(peakY, position.y);
        if (!g) g = groundAt(surf, px, pz, position.y, false, 60);
        if (vy <= 0 && g && position.y <= g.y) {
          const drop = peakY - g.y;
          position.y = lastGround = g.y; air = false; vy = 0;
          if (drop > 1.2) { shake(Math.min(0.35, drop * 0.03), 0.4); emit(landFns, { drop, x: px, y: g.y, z: pz }); }
        } else if (!g && position.y < lastGround - 3) { position.y = lastGround; air = false; vy = 0; } // no floor at all: never fall forever
      } else if (g) {
        climb = g.y - lastGround; lastGround = g.y;
        position.y = snap || Math.abs(g.y - position.y) < 0.002 ? g.y : position.y + (g.y - position.y) * damp(g.y > position.y ? 22 : 15, dt);
      }
    }
    const hs = Math.hypot(vel.x, vel.y);

    // body faces the move direction (third person) or the camera (first person)
    if (view === 'first' && viewK < 0.5) bodyYaw = cam.yaw;
    else if (hs > 0.25) bodyYaw += wrap(Math.atan2(-vel.x, -vel.y) - bodyYaw) * damp(10, dt);

    // camera rig
    cam.yaw += wrap(look.yaw - cam.yaw) * damp(24, dt); cam.pitch += (look.pitch - cam.pitch) * damp(24, dt);
    viewK += ((view === 'third' ? 1 : 0) - viewK) * (snap || RM ? 1 : damp(7, dt));
    if (view === 'first' && viewK < 0.002) viewK = 0;
    if (view === 'third') look.pitch = clamp(look.pitch, -0.95, 0.6);
    phase += (hs * dt * Math.PI * 2) / 1.44; // one stride (two steps) per 1.44 m
    bobAmp += ((RM ? 0 : Math.min(1, hs / PLAYER.walk) * (running ? 1.3 : 1)) - bobAmp) * damp(6, dt);
    const roll = Math.sin(phase) * 0.004 * bobAmp * (1 - viewK);
    euler.set(cam.pitch, cam.yaw, roll); camera.quaternion.setFromEuler(euler);
    fwd.set(0, 0, -1).applyQuaternion(camera.quaternion); right.set(Math.cos(cam.yaw), 0, -Math.sin(cam.yaw));
    up.set(0, 1, 0).applyQuaternion(camera.quaternion);
    const eye = prof.eye, tpH = TP.height * (eye / PLAYER.eye);
    want.set(position.x, position.y + eye + (tpH - eye) * viewK, position.z);
    if (snap) pivot.copy(want);
    else {
      const kxz = damp(60 + (13 - 60) * viewK, dt), kyy = damp(60 + (9 - 60) * viewK, dt);
      pivot.x += (want.x - pivot.x) * kxz; pivot.z += (want.z - pivot.z) * kxz; pivot.y += (want.y - pivot.y) * kyy;
    }
    camera.position.copy(pivot);
    if (viewK > 0.01) { // over the shoulder, pulled in by solids between the head and the camera
      seg.copy(right).multiplyScalar(TP.shoulder * viewK).addScaledVector(fwd, -TP.dist * viewK);
      const segLen = seg.length();
      let t = 1;
      for (const s of allSolids) { if (s && !s.off) t = Math.min(t, segBox(pivot, seg, s, 0.18)); }
      // Stair flights and floors are walk zones, not solids: stop before the camera sinks into one
      // (walking down a stair, the camera behind you would otherwise end up inside the steps).
      for (let i = 1; i <= 8 && t > 0; i++) {
        const k = (i / 8) * t, sx = pivot.x + seg.x * k, sy = pivot.y + seg.y * k, sz = pivot.z + seg.z * k;
        const f = groundAt(walk, sx, sz, pivot.y + 0.35, false, 50);
        if (f && sy < f.y + 0.3) { t = ((i - 1) / 8) * t; break; }
      }
      const frac = clamp((t * segLen - 0.12) / segLen, 0.05, 1);
      camFrac = frac < camFrac || snap ? frac : camFrac + (frac - camFrac) * damp(3.5, dt);
      camera.position.addScaledVector(seg, camFrac);
      camera.position.y = Math.max(camera.position.y, position.y + 0.3);
    }
    const bob = 1 - viewK; // gentle first-person head bob
    camera.position.addScaledVector(up, Math.sin(phase * 2) * 0.028 * bobAmp * bob).addScaledVector(right, Math.sin(phase) * 0.018 * bobAmp * bob);
    const fov = FOV + (running && hs > 3.6 && !RM ? FOV_RUN : 0);
    if (Math.abs(camera.fov - fov) > 0.01) { camera.fov += (fov - camera.fov) * damp(6, dt); camera.updateProjectionMatrix(); }

    if (override) {
      try { override(camera, dt); } catch (err) { console.error('[engine/controls] camera override failed; clearing it', err); override = null; }
    } else if (blend) { // ease back from a cinematic camera
      blend.k = Math.min(1, blend.k + dt / 0.8); const e = blend.k * blend.k * (3 - 2 * blend.k);
      camera.position.lerpVectors(blend.pos, tmp.copy(camera.position), e);
      camera.quaternion.slerpQuaternions(blend.quat, camera.quaternion.clone(), e);
      if (blend.k >= 1) blend = null;
    }
    if (shakeT > 0) { // a blast nearby: short decaying jolt (off with reduced motion)
      shakeT = Math.max(0, shakeT - dt);
      const k = RM ? 0 : shakeA * (shakeT / shakeDur) ** 2;
      camera.position.x += (Math.random() - 0.5) * k; camera.position.y += (Math.random() - 0.5) * k * 0.6; camera.position.z += (Math.random() - 0.5) * k;
    }
    camera.updateMatrixWorld();

    // model
    if (model?.group) {
      model.group.position.copy(position); model.group.rotation.y = bodyYaw;
      model.group.visible = override ? view !== 'first' : viewK > 0.35;
      let next = hs < 0.15 ? 'idle' : carrying === 'water' ? 'carry' : hs > 4 ? 'run' : 'walk';
      if (g && g.slope > 0.35 && hs > 0.15 && carrying !== 'water') next = climb >= 0 ? 'stairsUp' : 'stairsDown';
      if (move) next = move.anim;
      if ((actT -= dt) > 0 && actAnim) next = actAnim;
      if (next !== anim) { anim = next; model.setAnim?.(next); }
      model.setSpeed?.(hs);
      try { model.update?.(dt); } catch (err) { console.error('[engine/controls] player model update failed', err); model.update = null; }
    }

    // flashlight: at the right hand in third person, just below the camera in first person; hand-held lag
    if (flashOn) {
      handLocal.set(0.26, 1.05, -0.3).applyAxisAngle(THREE.Object3D.DEFAULT_UP, bodyYaw).add(position);
      origin.copy(camera.position).addScaledVector(right, 0.2).addScaledVector(up, -0.24).addScaledVector(fwd, 0.15);
      origin.lerp(handLocal, viewK);
      tmp.copy(camera.position).addScaledVector(fwd, 16);
      if (snap) aim.copy(tmp); else aim.lerp(tmp, damp(RM ? 30 : 14, dt));
      spot.position.copy(origin); spot.target.position.copy(aim); spot.target.updateMatrixWorld();
      beam.position.copy(origin); beam.lookAt(aim);
      // A torch held close to a wall or a person floods it: dim with the distance to whatever the beam hits.
      seg.subVectors(aim, origin);
      let hit = 1;
      for (const s of near) hit = Math.min(hit, segBox(origin, seg, s, 0));
      const hitD = hit * seg.length(), wantK = clamp((hitD / 4.5) ** 1.5, 0.12, 1);
      flashK += (wantK - flashK) * damp(8, dt);
      spot.intensity = FLASH * flashK;
      beam.material.uniforms.uStrength.value = 0.05 * beamStrength * (0.3 + 0.7 * viewK) * (0.4 + 0.6 * flashK);
    }

    const n = enabled ? findNearest(L) : null;
    if (n !== nearest) { nearest = n; emit(nearestFns, n); }
    snap = false;
  }

  function dispose() {
    if (locked()) document.exitPointerLock?.();
    removeEventListener('keydown', onKeyDown); removeEventListener('keyup', onKeyUp); removeEventListener('blur', clearKeys);
    canvas.removeEventListener('pointerdown', onPointerDown); canvas.removeEventListener('pointermove', onPointerMove);
    canvas.removeEventListener('pointerup', onPointerUp); canvas.removeEventListener('pointercancel', onPointerUp);
    canvas.removeEventListener('contextmenu', onContext);
    spot.removeFromParent(); spot.target.removeFromParent(); beam.removeFromParent(); model?.group?.removeFromParent();
  }

  return {
    player, update, setModel, setView, setEnabled, setOverride, dispose,
    get view() { return view; }, get enabled() { return enabled; }, get nearest() { return nearest; },
    onKey: (fn) => sub(keyFns, fn), onInteract: (fn) => sub(interactFns, fn), onNearest: (fn) => sub(nearestFns, fn),
    onBlocked: (fn) => sub(blockedFns, fn),
    onLand: (fn) => sub(landFns, fn),
    // Play a one-off body animation (punch, throw, hurt) for `seconds`, over whatever the legs do.
    act(name, seconds = 0.4) { actAnim = name; actT = seconds; },
    get busy() { return !!move; },
    get locked() { return locked(); },
    // { run, jump, eye, stairs } — any subset; the rest keeps its value.
    setProfile(p = {}) { for (const k of Object.keys(prof)) if (k in p) prof[k] = p[k]; },
    shake,
    setSensitivity(v) { sens = clamp(Number(v) || 1, 0.3, 3); },
    setBeamStrength(v) { beamStrength = v; },
    setQuality(q) { spot.castShadow = q === 'high'; spot.shadow.autoUpdate = flashOn && spot.castShadow; },
    setReduceMotion(v) { RM = !!v; },
    snap() { snap = true; },
  };
}
