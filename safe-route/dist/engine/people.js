// People: procedural, skinned, clothed adults and elders with blended procedural animation and props.
// createPerson({ preset, seed }) -> Person; createCrowd lives in crowd.js (re-exported here).
// One SkinnedMesh per person (1 draw call); each visible prop adds one. Feet at the origin, facing -z.
// Origin rule: standing/crouch keep the feet at the origin; 'sit' and 'sitChair' put the hips there.
import * as THREE from 'three';
import { builder, vpbrMaterial } from './people-geo.js';
import { B, PARENT, buildBody } from './people-body.js';
import { pose, gaitStep, gripOf, ANIMS, GAIT, NB, OFF, PLEN } from './people-anim.js';
import * as PR from './people-props.js';
import { presetSpec } from './people-outfit.js';

export { ANIMS };
export { createCrowd } from './crowd.js';
export const PROPS = ['backpack', 'waterJug', 'walker', 'flashlight', 'phone', 'baton', 'basket', 'cart'];
const BONE_NAMES = Object.keys(B);
const AUTO = { phone: 'phone', carry: 'waterJug', walker: 'walker' }; // animations that need a prop shown
const FADE = 0.2;
const STAIRS = new Set(['stairsUp', 'stairsDown']);
const _v = new THREE.Vector3(), _w = new THREE.Vector3(), _S = new THREE.Vector3(), _D = new THREE.Vector3(), _N = new THREE.Vector3(),
  _U = new THREE.Vector3(), _X = new THREE.Vector3(), _Y = new THREE.Vector3(), _Z = new THREE.Vector3(), _m = new THREE.Matrix4(),
  _q = new THREE.Quaternion(), _pq = new THREE.Quaternion(), _gq = new THREE.Quaternion(), _e = new THREE.Euler();
const clamp = (x, a, b) => Math.min(b, Math.max(a, x));
let autoSeed = 1;

export function createPerson({ preset = 'crowd', seed } = {}) {
  const spec = presetSpec(preset, seed ?? autoSeed++);
  const { b, d, J, rows, chestF } = buildBody(spec);
  const back = (y) => { const r = rows.reduce((a, q) => (Math.abs(q.y - y) < Math.abs(a.y - y) ? q : a)); return r.z + r.rz + (spec.outer ? 0.012 * d.H : 0.004 * d.H); };
  const P0 = { d, front: chestF, back };
  if (spec.bake?.backpack) PR.backpack(b, { ...P0, c: spec.bake.backpack, skin: B.chest });
  if (spec.bake?.crossbody) PR.crossbody(b, { ...P0, c: spec.bake.crossbody, skinChest: B.chest, skinPelvis: B.pelvis });
  const mat = vpbrMaterial();

  // skeleton in rest pose (identity local rotations)
  const bones = PARENT.map((p, i) => { const bone = new THREE.Bone(); bone.name = BONE_NAMES[i]; return bone; });
  bones.forEach((bone, i) => {
    const p = PARENT[i], q = p >= 0 ? J[p] : [0, 0, 0];
    bone.position.set(J[i][0] - q[0], J[i][1] - q[1], J[i][2] - q[2]);
    if (p >= 0) bones[p].add(bone);
  });
  const mesh = new THREE.SkinnedMesh(b.build(), mat);
  mesh.add(bones[0]); mesh.updateMatrixWorld(true);
  mesh.bind(new THREE.Skeleton(bones));
  mesh.boundingSphere = new THREE.Sphere(new THREE.Vector3(0, d.H * 0.5, -0.15), d.H * 0.85); // covers sit/crouch/reach
  mesh.castShadow = mesh.receiveShadow = true;
  mesh.name = `person:${preset}`;
  const group = new THREE.Group();
  group.name = mesh.name; group.add(mesh);

  // ---- state ----
  const P = new Float32Array(PLEN), Pp = new Float32Array(PLEN);
  const o = ((seed ?? 7) * 0.618034) % 1;
  let cur = 'idle', prev = 'idle', fade = 1, speed = null, t = o * 50, ph = o * 6.28, amp = 0, ikW = 0, lookOn = false, lookYaw = 0, wheelA = 0, shadow = true, lastGrip = null;
  const look = { x: 0, z: 0 };
  const want = Object.fromEntries(PROPS.map(n => [n, (spec.props ?? []).includes(n)]));
  const objs = {}, hangers = [];
  const k = (d.H / 1.72) * (d.f ? 0.86 : 1);

  function handHolder(side) { // prop holder in hand space; flashlight goes left when a baton is in the right hand
    return bones[side < 0 ? B.lHand : B.rHand];
  }
  function makeProp(name) {
    let obj;
    const side = name === 'basket' || (name === 'flashlight' && want.baton) ? -1 : 1;
    const add = (geo, parent, cast) => { const m = new THREE.Mesh(geo, mat); m.castShadow = cast && shadow; m.userData.cast = cast; parent.add(m); return m; };
    if (name === 'phone') obj = add(PR.phoneGeo(side, k), handHolder(side), false);
    else if (name === 'baton') obj = add(PR.batonGeo(side, k), handHolder(side), false);
    else if (name === 'flashlight') obj = add(PR.flashlightGeo(side, k), handHolder(side), false);
    else if (name === 'waterJug' || name === 'basket') {
      obj = new THREE.Object3D(); obj.position.set(-side * 0.012 * k, -0.072 * k, 0); handHolder(side).add(obj);
      add(name === 'basket' ? PR.basketGeo() : PR.waterJugGeo(), obj, true);
      hangers.push(obj);
    } else if (name === 'backpack') {
      const bb = builder(false); PR.backpack(bb, { ...P0, c: '#2b3242', m: new THREE.Matrix4().makeTranslation(...J[B.chest].map(v => -v)) });
      obj = add(bb.build(), bones[B.chest], true);
    } else if (name === 'cart') obj = add(PR.cartGeo(), group, true);
    else if (name === 'walker') {
      const hy = d.wrY - 0.01, wb = [new THREE.Bone(), ...PR.WALKER_WHEELS.map(() => new THREE.Bone())];
      PR.WALKER_WHEELS.forEach((p, i) => { wb[i + 1].position.set(...p); wb[0].add(wb[i + 1]); });
      obj = new THREE.SkinnedMesh(PR.walkerGeo(hy), mat);
      obj.add(wb[0]); obj.updateMatrixWorld(true); obj.bind(new THREE.Skeleton(wb));
      obj.boundingSphere = new THREE.Sphere(new THREE.Vector3(0, 0.45, -0.55), 0.75);
      obj.castShadow = shadow; obj.userData.cast = true; obj.userData.wheels = wb.slice(1); obj.userData.hy = hy;
      group.add(obj);
    }
    objs[name] = obj;
    return obj;
  }
  function syncProps() {
    for (const n of PROPS) {
      const on = want[n] || AUTO[cur] === n || (fade < 1 && AUTO[prev] === n);
      if (on && !objs[n]) makeProp(n);
      if (objs[n]) objs[n].visible = on;
    }
  }

  function applyPose(Q) {
    for (let i = 1; i < NB; i++) bones[i].rotation.set(Q[i * 3], Q[i * 3 + 1], Q[i * 3 + 2]);
    bones[B.pelvis].position.set(J[B.pelvis][0] + Q[OFF], J[B.pelvis][1] + Q[OFF + 1], J[B.pelvis][2] + Q[OFF + 2]);
  }

  // Ground contact for the procedural gaits (ground = plane y = 0 under the person): lift the pelvis when the
  // lower ankle is below its standing height (never more, so no foot floats), then pitch each foot within the
  // range that keeps both its toe and heel sole points at or above the ground.
  const shoeL = 0.155 * d.H * (d.f ? 0.97 : 1), zt = -0.74 * shoeL, zh = 0.2 * shoeL;
  const rt = Math.hypot(d.ankleY, zt), rh = Math.hypot(d.ankleY, zh), FM = [new THREE.Matrix4(), new THREE.Matrix4()];
  const asinC = (x) => Math.asin(clamp(x, -1, 1));
  function plantFeet() {
    const feet = [B.lFoot, B.rFoot];
    feet.forEach((f, k) => {
      FM[k].identity();
      for (const i of [B.root, B.pelvis, f - 2, f - 1, f]) { bones[i].updateMatrix(); FM[k].multiply(bones[i].matrix); }
    });
    const lift = Math.max(0, d.ankleY - Math.min(FM[0].elements[13], FM[1].elements[13]));
    if (lift > 0) { bones[B.pelvis].position.y += lift; for (const M of FM) M.elements[13] += lift; }
    feet.forEach((f, k) => {
      const M = FM[k], ya = M.elements[13];
      const yt = _v.set(0, -d.ankleY, zt).applyMatrix4(M).y, yh = _w.set(0, -d.ankleY, zh).applyMatrix4(M).y;
      const lo = asinC((ya - yt) / rt) - asinC(ya / rt); // toe-up rotation that puts the toe on the ground
      const hi = asinC(ya / rh) - asinC((ya - yh) / rh); // ... and the heel
      bones[f].rotation.x += lo <= hi ? clamp(0, lo, hi) : (lo + hi) / 2;
    });
  }

  // Two-bone arm IK in world space: wrist to target, elbow toward pole (world direction), weight w.
  function solveArm(side, target, pole, w) {
    const a = side < 0 ? B.lArm : B.rArm, up = bones[a], fo = bones[a + 1], L1 = d.U1, L2 = d.U2;
    up.getWorldPosition(_S);
    _D.subVectors(target, _S);
    const dist = clamp(_D.length(), Math.abs(L1 - L2) + 1e-3, L1 + L2 - 1e-3);
    _D.normalize();
    const A = Math.acos(clamp((L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist), -1, 1));
    const E = Math.PI - Math.acos(clamp((L1 * L1 + L2 * L2 - dist * dist) / (2 * L1 * L2), -1, 1));
    _N.copy(pole).addScaledVector(_D, -pole.dot(_D)).normalize();
    _U.copy(_D).multiplyScalar(Math.cos(A)).addScaledVector(_N, Math.sin(A));
    _Y.copy(_U).negate();
    _Z.copy(_N).addScaledVector(_U, -_N.dot(_U)).normalize();
    _X.crossVectors(_Y, _Z);
    _q.setFromRotationMatrix(_m.makeBasis(_X, _Y, _Z));
    up.parent.getWorldQuaternion(_pq);
    _q.premultiply(_pq.invert());
    up.quaternion.slerp(_q, w);
    fo.quaternion.slerp(_q.setFromEuler(_e.set(E, 0, 0)), w);
    up.updateMatrixWorld(true);
  }

  function gripTargets(mode, side) { // wrist target (world) + elbow pole (world)
    const s = side;
    if (mode === 'walker') { const w = objs.walker; _v.set(s * 0.26, w.userData.hy + 0.045, -0.255); w.localToWorld(_v); }
    else if (mode === 'cart') { _v.set(s * 0.2, 1.045, -0.35); group.localToWorld(_v); }
    else { bones[s < 0 ? B.lShin : B.rShin].getWorldPosition(_v); _w.set(s * 0.01, 0.07, 0.03).applyQuaternion(_gq); _v.add(_w); }
    _w.set(s * (mode === 'knees' ? 0.7 : 0.45), -0.35, 0.75).normalize().applyQuaternion(_gq);
    return [_v.clone(), _w.clone()];
  }

  function update(dt) {
    dt = Math.min(dt, 0.1);
    t += dt;
    // with the rollator out, plain walking becomes walker walking
    const eff = (n) => (objs.walker?.visible && (n === 'walk' || n === 'brisk' || n === 'run') ? 'walker' : n);
    const step = gaitStep(eff(cur), speed, d.H);
    ph = (ph + step.rate * dt) % (Math.PI * 200);
    amp += (step.amp - amp) * Math.min(1, dt * 6);
    if (fade < 1) fade = Math.min(1, fade + dt / FADE);
    const c = { t, ph, amp, d, o, seat: +api.seat || 0.45, stoop: spec.stoop ?? 0 };
    pose(eff(cur), P, c);
    if (fade < 1) {
      pose(eff(prev), Pp, c);
      const w = fade * fade * (3 - 2 * fade);
      for (let i = 0; i < PLEN; i++) P[i] = Pp[i] + (P[i] - Pp[i]) * w;
    }
    if (lookOn) { // turn head, neck and a little chest toward a world point
      group.updateWorldMatrix(true, false);
      _v.set(look.x, 0, look.z); group.worldToLocal(_v);
      const yaw = clamp(Math.atan2(-_v.x, -_v.z), -1.25, 1.25);
      lookYaw += (yaw - lookYaw) * Math.min(1, dt * 4);
    } else lookYaw += (0 - lookYaw) * Math.min(1, dt * 3);
    P[B.chest * 3 + 1] += lookYaw * 0.25; P[B.neck * 3 + 1] += lookYaw * 0.3; P[B.head * 3 + 1] += lookYaw * 0.45;
    syncProps();
    if (objs.walker) { // parked beside a seated person, rolling otherwise
      const parked = cur === 'sit' || cur === 'sitChair';
      objs.walker.position.set(parked ? 0.62 : 0, 0, parked ? -0.1 : 0); objs.walker.rotation.y = parked ? Math.PI / 2 : 0;
      const v = GAIT[eff(cur)] ? (speed ?? GAIT[eff(cur)].v) : 0;
      wheelA -= v * dt / PR.WALKER_R;
      for (const wb of objs.walker.userData.wheels) wb.rotation.x = wheelA;
    }
    applyPose(P);
    if (!STAIRS.has(cur) && !(fade < 1 && STAIRS.has(prev))) plantFeet(); // on stairs the ground is not the plane y = 0
    const mode = gripOf(eff(cur), { walker: objs.walker?.visible, cart: objs.cart?.visible });
    ikW += ((mode ? 1 : 0) - ikW) * Math.min(1, dt / FADE * 1.5);
    const needWorld = ikW > 0.01 || hangers.some(h => h.visible);
    if (needWorld) { group.updateWorldMatrix(true, true); group.getWorldQuaternion(_gq); }
    if (mode) lastGrip = mode;
    if (ikW > 0.01 && lastGrip && objs[lastGrip]?.visible !== false) {
      for (const s of [-1, 1]) { const [tg, pl] = gripTargets(lastGrip, s); solveArm(s, tg, pl, ikW); }
    }
    for (const h of hangers) { // keep jugs and baskets hanging plumb, with a little swing
      if (!h.visible) continue;
      h.parent.getWorldQuaternion(_pq);
      _e.set(0.1 * amp * Math.sin(ph - 0.6) + 0.02 * Math.sin(t * 1.3), 0, 0.03 * Math.sin(t * 0.9 + o));
      h.quaternion.copy(_pq.invert()).multiply(_gq).multiply(_q.setFromEuler(_e));
    }
  }

  const api = {
    group, mesh, height: d.H, preset, spec, seat: 0.45,
    bones: Object.fromEntries(BONE_NAMES.map((n, i) => [n, bones[i]])),
    get anim() { return cur; },
    setAnim(name, opts = {}) {
      if (!ANIMS.includes(name)) { console.error(`people: unknown animation "${name}" for ${preset}`); return; }
      if (opts.speed !== undefined) speed = opts.speed;
      if (name === cur) return;
      prev = cur; cur = name; fade = opts.instant ? 1 : 0;
      if (opts.instant) prev = name;
    },
    setSpeed(mps) { speed = mps == null ? null : Math.max(0, +mps || 0); },
    setProp(name, on = true) {
      if (!PROPS.includes(name)) { console.error(`people: unknown prop "${name}" for ${preset}`); return; }
      want[name] = !!on; syncProps();
    },
    hasProp: (name) => !!want[name],
    lookAt(x, z) { if (x == null || z == null) { lookOn = false; return; } lookOn = true; look.x = x; look.z = z; },
    setShadow(on) {
      shadow = !!on; mesh.castShadow = shadow;
      for (const ob of Object.values(objs)) ob.traverse(m => { if (m.isMesh) m.castShadow = shadow && !!(m.userData.cast ?? ob.userData.cast); });
    },
    update,
    dispose() { mesh.geometry.dispose(); group.removeFromParent(); },
  };
  syncProps();
  update(0);
  return api;
}
