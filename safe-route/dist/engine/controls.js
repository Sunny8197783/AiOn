// Player, input and camera rig. WASD/arrows move, Shift runs, mouse/touch drag (or Alt+arrows) looks,
// E/Enter interacts, V toggles the view; M, Tab, I, Esc, F and V are also emitted to the director (onKey).
// Ground height comes from level.walk zones, level.solids are AABBs the capsule slides along, and the
// third-person camera is pulled in when a solid blocks its line of sight. No pointer lock.
// Yaw uses the three.js convention: rotation.y = yaw, yaw 0 faces north (-z), positive yaw turns left (west).
import * as THREE from 'three';

export const PLAYER = { radius: 0.32, height: 1.72, eye: 1.62, walk: 3.0, run: 5.2 };
const TP = { dist: 3.4, height: 1.7, shoulder: 0.45 };
const FOV = 70, FOV_RUN = 4, D2R = Math.PI / 180, LOOK = 0.0042;
const UI_KEYS = new Set(['KeyM', 'Tab', 'KeyI', 'Escape', 'KeyF', 'KeyV']);
const MOVE = { KeyW: [0, 1], ArrowUp: [0, 1], KeyS: [0, -1], ArrowDown: [0, -1], KeyA: [-1, 0], ArrowLeft: [-1, 0], KeyD: [1, 0], ArrowRight: [1, 0] };
const damp = (k, dt) => 1 - Math.exp(-k * dt);
const wrap = (a) => Math.atan2(Math.sin(a), Math.cos(a));
const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const editable = (t) => !!t && (t.isContentEditable || /^(INPUT|SELECT|TEXTAREA)$/.test(t.tagName || ''));

// Ground under (x, z): the highest walk surface within 0.6 m below / 0.45 m above feetY, or null.
export function groundAt(walk, x, z, feetY, any = false) {
  let best = null, slope = 0;
  for (const w of walk || []) {
    if (x < w.x0 - 0.02 || x > w.x1 + 0.02 || z < w.z0 - 0.02 || z > w.z1 + 0.02) continue;
    let y = w.y ?? 0, s = 0;
    if (w.ramp) {
      const r = w.ramp, span = r.axis === 'x' ? w.x1 - w.x0 : w.z1 - w.z0;
      const k = clamp(((r.axis === 'x' ? x - w.x0 : z - w.z0) / (span || 1)), 0, 1);
      y = r.y0 + (r.y1 - r.y0) * k; s = Math.abs(r.y1 - r.y0) / (span || 1);
    }
    if (!any && (y > feetY + 0.45 || y < feetY - 0.6)) continue;
    if (best === null || y > best) { best = y; slope = s; }
  }
  return best === null ? null : { y: best, slope };
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
  let anim = '', lastGround = 0, snap = true, beamStrength = 1;

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
    position, speedMul: 1, radius: PLAYER.radius, height: PLAYER.height, eye: PLAYER.eye,
    get model() { return model; },
    get flashlight() { return flashOn; },
    setFlashlight(on) {
      flashOn = !!on; spot.intensity = flashOn ? 12 : 0; spot.shadow.autoUpdate = flashOn && spot.castShadow;
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
      look.pitch = cam.pitch = -0.05; vel.set(0, 0); lastGround = position.y; snap = true;
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
    if (MOVE[e.code] || e.code.startsWith('Shift') || e.code.startsWith('Alt')) {
      keys.add(e.code);
      if (e.code.startsWith('Arrow') || e.code.startsWith('Alt')) e.preventDefault();
    }
  }
  const onKeyUp = (e) => { keys.delete(e.code); };
  const clearKeys = () => { keys.clear(); drag = null; };
  let drag = null;
  function onPointerDown(e) {
    if (!enabled || e.button > 2 || e.button === 1) return;
    drag = { id: e.pointerId, x: e.clientX, y: e.clientY };
    try { canvas.setPointerCapture(e.pointerId); } catch { /* pointer already gone */ }
    canvas.focus?.({ preventScroll: true });
  }
  function onPointerMove(e) {
    if (!drag || e.pointerId !== drag.id || !enabled) return;
    const dx = e.clientX - drag.x, dy = e.clientY - drag.y; drag.x = e.clientX; drag.y = e.clientY;
    look.yaw -= dx * LOOK; look.pitch = clamp(look.pitch - dy * LOOK * 0.85, -1.25, 1.1);
  }
  const onPointerUp = (e) => { if (drag && e.pointerId === drag.id) drag = null; };
  const onContext = (e) => e.preventDefault();
  canvas.style.touchAction = 'none';
  addEventListener('keydown', onKeyDown); addEventListener('keyup', onKeyUp); addEventListener('blur', clearKeys);
  canvas.addEventListener('pointerdown', onPointerDown); canvas.addEventListener('pointermove', onPointerMove);
  canvas.addEventListener('pointerup', onPointerUp); canvas.addEventListener('pointercancel', onPointerUp);
  canvas.addEventListener('contextmenu', onContext);

  function setView(v) { if (v === 'first' || v === 'third') view = v; else console.error('[engine/controls] unknown view', v); }
  function setEnabled(on) { enabled = !!on; if (!enabled) clearKeys(); }
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
    const running = len > 0 && (keys.has('ShiftLeft') || keys.has('ShiftRight'));
    const speed = (running ? PLAYER.run : PLAYER.walk) * clamp(Number(player.speedMul) || 1, 0.2, 1.5);
    const sy = Math.sin(look.yaw), cy = Math.cos(look.yaw);
    const tx = (-sy * kz + cy * kx) * speed, tz = (-cy * kz - sy * kx) * speed;
    const a = damp(len > 0 ? 9 : 12, dt);
    vel.x += (tx - vel.x) * a; vel.y += (tz - vel.y) * a;
    if (Math.abs(vel.x) < 1e-3 && Math.abs(vel.y) < 1e-3) vel.set(0, 0);

    // move in sub-steps (no tunnelling through thin walls), slide along solids
    const near = allSolids.filter((s) => s && !s.off && s.x0 - 3 < position.x && s.x1 + 3 > position.x && s.z0 - 3 < position.z && s.z1 + 3 > position.z);
    const steps = Math.max(1, Math.ceil((Math.hypot(vel.x, vel.y) * dt) / 0.12));
    let px = position.x, pz = position.z;
    for (let i = 0; i < steps; i++) [px, pz] = collide(px + (vel.x * dt) / steps, pz + (vel.y * dt) / steps, position.y, near);
    if (dt > 0) { // never keep pushing into a wall
      const ax = (px - position.x) / dt, az = (pz - position.z) / dt;
      if (Math.abs(ax) < Math.abs(vel.x)) vel.x = ax;
      if (Math.abs(az) < Math.abs(vel.y)) vel.y = az;
    }
    position.x = px; position.z = pz;
    const g = groundAt(walk, px, pz, position.y);
    let climb = 0;
    if (g) {
      climb = g.y - lastGround; lastGround = g.y;
      position.y = snap || Math.abs(g.y - position.y) < 0.002 ? g.y : position.y + (g.y - position.y) * damp(g.y > position.y ? 22 : 15, dt);
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
    want.set(position.x, position.y + PLAYER.eye + (TP.height - PLAYER.eye) * viewK, position.z);
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
    camera.updateMatrixWorld();

    // model
    if (model?.group) {
      model.group.position.copy(position); model.group.rotation.y = bodyYaw;
      model.group.visible = override ? view !== 'first' : viewK > 0.35;
      let next = hs < 0.15 ? 'idle' : carrying === 'water' ? 'carry' : hs > 4 ? 'run' : 'walk';
      if (g && g.slope > 0.35 && hs > 0.15 && carrying !== 'water') next = climb >= 0 ? 'stairsUp' : 'stairsDown';
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
      beam.material.uniforms.uStrength.value = 0.07 * beamStrength * (0.3 + 0.7 * viewK);
    }

    const n = enabled ? findNearest(L) : null;
    if (n !== nearest) { nearest = n; emit(nearestFns, n); }
    snap = false;
  }

  function dispose() {
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
    setBeamStrength(v) { beamStrength = v; },
    setQuality(q) { spot.castShadow = q === 'high'; spot.shadow.autoUpdate = flashOn && spot.castShadow; },
    setReduceMotion(v) { RM = !!v; },
    snap() { snap = true; },
  };
}
