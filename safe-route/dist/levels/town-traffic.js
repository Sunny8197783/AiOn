// Traffic: three procedural vehicle types (sedan, SUV, 1t box truck; vehicle-geo.js) drawn as instanced parts with
// clear-coat paint tinted per car, parked cars in every marked bay row, moving cars on LANES that follow
// each other (IDM), stop at red/amber, for people on the road (player and crowd, ctx.peds) and short of a
// zebra someone is on, signal heads and pedestrian signals that run the CYCLE from town-plan, headlights
// and road glow at night. Moving cars carry live solids. Parked cars are static instances in 128 m cells, so the
// camera and the sun's shadow camera skip the cells out of view; the graphics tier (kit.Q) sets paint, share kept
// and wheel detail.
import * as THREE from 'three';
import { CROSSWALKS, ROADS } from '../game/townmap.js';
import { LANES, LANE_SPAN, CURB, signalAt } from './town-plan.js';
import { yawTo } from './town-util.js';
import { zebraZones, busyZebras } from './town-geo.js';
import { TYPES, vehicleGeo } from './vehicle-geo.js';

export { TYPES, vehicleGeo }; // shared: the shelter's parked cars and the 302호 window view use the same cars
const PARTS = ['body', 'glass', 'dark', 'lamp'];

// Rows of marked bays (same lines as town-ground): [x0, x1, zLine, depth, step, kind]
// The 102/101 rows stop short of the entrance canopies (x 84..92); trucks back onto the dock face (z -59.1).
const ROWS = [[-35, 3, -22, 5, 2.5], [6, 40, -51.5, 5, 2.5], [6, 40, -10.5, -5, 2.5], [6, 40, -31, 5, 2.5], [6, 40, -31, -5, 2.5],
  [74.5, 82, -10.5, -5.5, 2.5], [92, 126, -10.5, -5.5, 2.5], [104.4, 123.6, -59.1, 6, 3.2, 'truck'], [-124, -90, 59.5, -5, 2.5],
  [60, 126, 13.5, 5, 2.5], [62, 84, 57, -5, 2.5], [92, 116, 57, -5, 2.5], [62, 84, 85, -5, 2.5], [92, 116, 85, -5, 2.5], [14, 40, 51.5, -5, 2.5], [14, 40, 22.5, 5, 2.5]];
const KEEP_CLEAR = [{ x0: -20.5, x1: -11.5, z0: -23, z1: -16 }, { x0: -9.5, x1: -5.5, z0: -23, z1: -16 }, { x0: 83.5, x1: 92.5, z0: 51, z1: 58 }, { x0: 83.5, x1: 92.5, z0: 79, z1: 86 }];
const YIELD = 2.4; // a person this close to a lane's centre line (on or stepping onto it) stops the cars coming at them
const CHUNK = 128; // parked-car cell size (m): a quarter of the town each (smaller cells cull more triangles but add draw calls)
const byU = (a, b) => a.u - b.u;
const PALETTE = [['#f2f2ef', 35], ['#8d9196', 20], ['#16181b', 15], ['#c4c7ca', 12], ['#1f2f4f', 8], ['#8e1f22', 4], ['#c9b89a', 3], ['#2f4a3a', 3]];

export function buildTraffic(kit) {
  const group = new THREE.Group(); group.name = 'traffic';
  const rand = kit.rng(31337), solids = [], Q = kit.Q ?? {};
  const keep = Q.parked ?? 1, lo = Q.physicalPaint === false; // lower tiers: some bays left empty, simpler cars
  const pickW = (list) => { let t = rand() * list.reduce((s, [, w]) => s + w, 0); for (const [v, w] of list) if ((t -= w) <= 0) return v; return list[0][0]; };
  const randType = () => pickW(Object.entries(TYPES).map(([k, T]) => [k, T.weight]));

  // ---- parked cars ----
  const parked = [];
  for (const [x0, x1, zl, depth, step, kind] of ROWS) for (let x = x0; x + step <= x1 + 1e-6; x += step) {
    const cx = x + step / 2, cz = zl + depth / 2;
    if (rand() > (kind ? 0.7 : 0.62) || KEEP_CLEAR.some(r => cx > r.x0 && cx < r.x1 && cz > r.z0 && cz < r.z1)) continue;
    if (keep < 1 && rand() > keep) continue;
    const type = kind || randType();
    parked.push({ type, x: cx + (rand() - 0.5) * 0.2, z: cz + (rand() - 0.5) * 0.3, yaw: (kind ? Math.PI : rand() < 0.5 ? 0 : Math.PI) + (rand() - 0.5) * 0.05 });
  }
  for (const [zc, yaw] of [[57.3, Math.PI / 2], [62.7, -Math.PI / 2], [-62.7, Math.PI / 2], [-57.3, -Math.PI / 2]]) for (let x = -40; x <= 40; x += 6.5) {
    if (Math.abs(x) < 5 || rand() < 0.45 || (keep < 1 && rand() > keep)) continue;
    parked.push({ type: rand() < 0.3 ? 'truck' : randType(), x: x + rand(), z: zc, yaw });
  }
  for (const p of parked) {
    const T = TYPES[p.type], along = Math.abs(Math.sin(p.yaw)) > 0.5, hx = (along ? T.L : T.W) / 2 + 0.05, hz = (along ? T.W : T.L) / 2 + 0.05;
    solids.push({ x0: p.x - hx, x1: p.x + hx, z0: p.z - hz, z1: p.z + hz, y0: -0.5, y1: 1.8, id: 'car' });
    p.color = p.type === 'truck' ? pickW([['#f2f2ef', 6], ['#2d5fa8', 3], ['#c4c7ca', 1]]) : pickW(PALETTE);
  }

  // ---- moving pool ----
  const pool = [];
  LANES.forEach((lane, li) => {
    const n = lane.axis === 'x' ? 4 : 2;
    for (let i = 0; i < n; i++) {
      const type = randType();
      pool.push({ lane: li, type, rank: i, u: -LANE_SPAN + (i + 0.2 + rand() * 0.6) * (2 * LANE_SPAN / n), v: lane.vmax * 0.8, v0: lane.vmax * TYPES[type].v * (0.9 + rand() * 0.2), len: TYPES[type].L, active: true,
        color: pickW(PALETTE), solid: { x0: 0, x1: 0, z0: 0, z1: 0, y0: -0.5, y1: 1.8, id: 'movingCar', off: true } });
    }
  });
  for (const c of pool) solids.push(c.solid);

  // ---- instanced parts per type ----
  const paint = lo ? kit.memo('town:carPaintLo', () => new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.3, metalness: 0.45 }))
    : kit.memo('town:carPaint', () => new THREE.MeshPhysicalMaterial({ color: 0xffffff, roughness: 0.32, metalness: 0.45, clearcoat: 1, clearcoatRoughness: 0.08 }));
  const lampTex = kit.memo('town:carLampTex', () => { const t = kit.texture(kit.canvas(2, 1, (g) => { g.fillStyle = '#fff4dc'; g.fillRect(0, 0, 1, 1); g.fillStyle = '#ff2a1c'; g.fillRect(1, 0, 1, 1); }), { repeat: [1, 1] }); t.magFilter = t.minFilter = THREE.NearestFilter; t.generateMipmaps = false; return t; });
  // Lamps: moving cars light up at dusk and at night; parked cars are switched off (their own material), except a
  // couple with the engine running (a truck at the dock, a car leaving the mart lot), which share the moving meshes.
  const lampMat = new THREE.MeshStandardMaterial({ map: lampTex, emissiveMap: lampTex, emissive: 0xffffff, emissiveIntensity: 0.25, roughness: 0.25 });
  const parkedLampMat = lampMat.clone();
  const mats = { body: paint, glass: kit.mat('darkGlass', { roughness: 0.04 }), dark: kit.mat('paint:#141517', { roughness: 0.7 }), lamp: lampMat };
  const meshes = {}, slots = new Map(), movingIMs = [], poolTypes = new Set(pool.map(c => c.type));
  const running = [];
  for (const [x, z] of [[114, -56.5], [-16, -24.5]]) {
    const p = parked.filter(q => poolTypes.has(q.type) && !running.includes(q)).sort((a, b) => Math.hypot(a.x - x, a.z - z) - Math.hypot(b.x - x, b.z - z))[0];
    if (p && Math.hypot(p.x - x, p.z - z) < 12) running.push(p);
  }
  const _m = new THREE.Matrix4(), _q = new THREE.Quaternion(), _p = new THREE.Vector3(), _s = new THREE.Vector3(1, 1, 1), _z = new THREE.Vector3(0, 0, 0), _c = new THREE.Color(), UP = new THREE.Vector3(0, 1, 0);
  for (const [type, T] of Object.entries(TYPES)) {
    const moving = pool.filter(c => c.type === type), still = parked.filter(p => p.type === type && !running.includes(p));
    const idling = running.filter(p => p.type === type);
    if (!moving.length && !still.length) continue;
    const geo = vehicleGeo(T, lo);
    const make = (count, tag, lamp = lampMat) => PARTS.map(part => { // tyres and bumpers sit in the body's shadow: only the body casts
      const im = new THREE.InstancedMesh(geo[part], part === 'lamp' ? lamp : mats[part], count);
      im.castShadow = part === 'body'; im.receiveShadow = part !== 'lamp';
      im.name = `car:${type}:${part}${tag}`; group.add(im); return im;
    });
    if (moving.length) { // the moving pool spans the lanes far into the city ring: drawn whatever the view
      meshes[type] = make(moving.length + idling.length, '');
      for (const im of meshes[type]) { im.frustumCulled = false; movingIMs.push(im); }
      moving.forEach((c, i) => { slots.set(c, i); meshes[type][0].setColorAt(i, _c.set(c.color)); });
      idling.forEach((p, i) => { // fixed slots after the pool's: update() never moves them
        _m.compose(_p.set(p.x, 0, p.z), _q.setFromAxisAngle(UP, p.yaw), _s.set(1, 1, 1));
        for (const im of meshes[type]) im.setMatrixAt(moving.length + i, _m);
        meshes[type][0].setColorAt(moving.length + i, _c.set(p.color));
      });
      meshes[type][0].instanceColor.needsUpdate = true;
    }
    const cells = new Map();
    for (const p of still) { const k = `${Math.floor(p.x / CHUNK)},${Math.floor(p.z / CHUNK)}`; if (!cells.has(k)) cells.set(k, []); cells.get(k).push(p); }
    for (const [k, list] of cells) {
      const ims = make(list.length, `:parked:${k}`, parkedLampMat);
      list.forEach((p, i) => {
        _m.compose(_p.set(p.x, 0, p.z), _q.setFromAxisAngle(UP, p.yaw), _s.set(1, 1, 1));
        for (const im of ims) im.setMatrixAt(i, _m);
        ims[0].setColorAt(i, _c.set(p.color));
      });
      for (const im of ims) { im.instanceMatrix.needsUpdate = true; im.computeBoundingSphere(); im.matrixAutoUpdate = false; }
      ims[0].instanceColor.needsUpdate = true;
    }
  }

  // Headlight glow on the road in front of moving cars (night only).
  const beamTex = kit.memo('town:beamTex', () => kit.texture(kit.canvas(64, 256, (g, w, h) => {
    const gr = g.createLinearGradient(0, h, 0, 0); gr.addColorStop(0, 'rgba(255,255,255,0.9)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = gr; g.beginPath(); g.moveTo(w * 0.3, h); g.lineTo(w * 0.7, h); g.lineTo(w, 0); g.lineTo(0, 0); g.fill();
  }), { repeat: [1, 1] }));
  const beams = new THREE.InstancedMesh(new THREE.PlaneGeometry(3.4, 11).rotateX(-Math.PI / 2).translate(0, 0.03, -5.5),
    new THREE.MeshBasicMaterial({ map: beamTex, color: 0xffe9c4, transparent: true, opacity: 0.32, blending: THREE.AdditiveBlending, depthWrite: false }), pool.length);
  beams.frustumCulled = false; beams.visible = false; beams.name = 'carBeams'; group.add(beams);

  // ---- signal heads and pedestrian signals ----
  const sig = new THREE.Group(); sig.name = 'signals'; group.add(sig);
  const lamp = (hex) => new THREE.MeshStandardMaterial({ color: 0x1b1c1e, emissive: hex, emissiveIntensity: 0, roughness: 0.3 });
  const L = { EW: { red: lamp(0xff2a1a), amber: lamp(0xffa424), green: lamp(0x27e08a) }, NS: { red: lamp(0xff2a1a), amber: lamp(0xffa424), green: lamp(0x27e08a) } };
  const pedTex = (walk) => kit.memo(`town:ped:${walk}`, () => kit.texture(kit.canvas(64, 64, (g, w) => {
    g.fillStyle = '#000'; g.fillRect(0, 0, w, w); g.fillStyle = '#fff'; g.beginPath(); g.arc(32, 12, 6, 0, 7); g.fill(); g.lineWidth = 7; g.strokeStyle = '#fff'; g.lineCap = 'round';
    if (walk) { g.beginPath(); g.moveTo(32, 22); g.lineTo(29, 40); g.lineTo(20, 56); g.moveTo(29, 40); g.lineTo(40, 55); g.moveTo(31, 26); g.lineTo(20, 36); g.moveTo(31, 26); g.lineTo(42, 34); g.stroke(); }
    else { g.beginPath(); g.moveTo(32, 22); g.lineTo(32, 42); g.lineTo(27, 58); g.moveTo(32, 42); g.lineTo(37, 58); g.moveTo(32, 24); g.lineTo(24, 40); g.moveTo(32, 24); g.lineTo(40, 40); g.stroke(); }
  }), { repeat: [1, 1] }));
  const pedMat = (walk, hex) => new THREE.MeshStandardMaterial({ color: 0x151515, emissive: hex, emissiveMap: pedTex(walk), emissiveIntensity: 0, roughness: 0.3 });
  const P = { EW: { red: pedMat(false, 0xff3322), green: pedMat(true, 0x2ae68e) }, NS: { red: pedMat(false, 0xff3322), green: pedMat(true, 0x2ae68e) } };
  const housing = kit.mat('paint:#2a2c2f', { roughness: 0.5, metalness: 0.4 }), pole = kit.mat('paint:#8a9196', { roughness: 0.45, metalness: 0.6 });
  const addLampDisc = (mat, x, y, z, nx, nz, r = 0.13) => { const m = new THREE.Mesh(new THREE.CircleGeometry(r, 18), mat); m.position.set(x + nx * 0.16, y, z + nz * 0.16); m.rotation.y = Math.atan2(nx, nz); sig.add(m); };
  const heads = new Map();
  for (const lane of LANES) for (const st of lane.stops) heads.set(`${lane.axis}:${lane.dir}:${st}`, { lane, st });
  for (const { lane, st } of heads.values()) {
    const road = lane.axis === 'x' ? ROADS[0] : ROADS.find(r => r.z1 - r.z0 > r.x1 - r.x0 && lane.c > r.x0 && lane.c < r.x1);
    const cwAt = CROSSWALKS.find(c => c.road === road.id && Math.abs(c.at - (st + lane.dir * 2.9)) < 1.2);
    const far = cwAt ? cwAt.at + lane.dir * 3.3 : st + lane.dir * 10.6;
    const lat = lane.axis === 'x' ? (lane.dir > 0 ? road.z1 + 0.65 : road.z0 - 0.65) : (lane.dir < 0 ? road.x1 + 0.65 : road.x0 - 0.65);
    const centre = lane.axis === 'x' ? (road.z0 + road.z1) / 2 : (road.x0 + road.x1) / 2, armLen = Math.abs(lat - centre) * 0.78;
    const [px, pz] = lane.axis === 'x' ? [far, lat] : [lat, far];
    const toC = Math.sign(centre - lat), [ax, az] = lane.axis === 'x' ? [0, toC] : [toC, 0];
    sig.add(kit.cyl(0.11, 0.14, 6.2, pole, { x: px, y: CURB, z: pz }, 12));
    const arm = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.08, armLen, 10), pole); arm.rotation.set(az ? Math.PI / 2 : 0, 0, ax ? Math.PI / 2 : 0);
    arm.position.set(px + ax * armLen / 2, CURB + 6.0, pz + az * armLen / 2); arm.castShadow = true; sig.add(arm);
    solids.push({ x0: px - 0.2, x1: px + 0.2, z0: pz - 0.2, z1: pz + 0.2, y0: -0.5, y1: 4, id: 'signalPole' });
    const hx = px + ax * (armLen - 0.3), hz = pz + az * (armLen - 0.3), hy = CURB + 5.6;
    const nx = lane.axis === 'x' ? -lane.dir : 0, nz = lane.axis === 'z' ? -lane.dir : 0; // faces the approaching cars
    const lx = lane.axis === 'x' ? 0 : lane.dir, lz = lane.axis === 'x' ? -lane.dir : 0; // driver's left (red side)
    const hb = kit.box(lane.axis === 'x' ? 0.3 : 1.35, 0.42, lane.axis === 'x' ? 1.35 : 0.3, housing, { x: hx, y: hy - 0.21, z: hz }); sig.add(hb);
    sig.add(kit.box(0.04, 0.5, 0.04, pole, { x: hx, y: hy + 0.2, z: hz }));
    const G = L[lane.group];
    addLampDisc(G.red, hx + lx * 0.42, hy, hz + lz * 0.42, nx, nz); addLampDisc(G.amber, hx, hy, hz, nx, nz); addLampDisc(G.green, hx - lx * 0.42, hy, hz - lz * 0.42, nx, nz);
  }
  for (const c of CROSSWALKS) {
    const road = ROADS.find(r => r.id === c.road), ew = road.z1 - road.z0 < road.x1 - road.x0;
    if (!['yeonmu', 'munhwa', 'hanbit'].includes(road.id)) continue;
    const group = ew ? 'NS' : 'EW'; // crossing an EW road is allowed while NS traffic has green
    for (const side of [-1, 1]) {
      const edge = side < 0 ? (ew ? road.z0 : road.x0) - 1.6 : (ew ? road.z1 : road.x1) + 1.6;
      const off = ew ? (side < 0 ? 2.9 : -2.9) : (side < 0 ? -2.9 : 2.9), [x, z] = ew ? [c.at + off, edge] : [edge, c.at + off];
      const nx = ew ? 0 : -side, nz = ew ? -side : 0;
      sig.add(kit.cyl(0.06, 0.07, 2.4, pole, { x, y: CURB, z }, 10), kit.box(ew ? 0.36 : 0.26, 0.78, ew ? 0.26 : 0.36, housing, { x: x + nx * 0.12, y: CURB + 2.15, z: z + nz * 0.12 }));
      solids.push({ x0: x - 0.15, x1: x + 0.15, z0: z - 0.15, z1: z + 0.15, y0: -0.5, y1: 3, id: 'pedSignal' });
      for (const [mat, dy] of [[P[group].red, 0.18], [P[group].green, -0.18]]) {
        const m = new THREE.Mesh(new THREE.PlaneGeometry(0.28, 0.28), mat); m.position.set(x + nx * 0.26, CURB + 2.54 + dy, z + nz * 0.26); m.rotation.y = Math.atan2(nx, nz); sig.add(m);
      }
    }
  }
  kit.merge(sig);

  // ---- per-frame ----
  let mode = 'normal', power = true, night = false, lampEnv = '';
  const cfg = { normal: { x: 4, z: 2 }, light: { x: 2, z: 1 }, none: { x: 0, z: 0 } };
  function setMode(m) {
    if (!cfg[m]) { console.error(`[town] traffic "${m}" unknown; use normal | light | none`); return; }
    mode = m;
    for (const c of pool) c.active = c.rank < cfg[m][LANES[c.lane].axis];
  }
  function setLamps(t) {
    const s = signalAt(t);
    for (const g of ['EW', 'NS']) {
      const st = s[g];
      L[g].red.emissiveIntensity = power && st === 'red' ? 5 : 0;
      L[g].amber.emissiveIntensity = power && st === 'amber' ? 5 : 0;
      L[g].green.emissiveIntensity = power && st === 'green' ? 5 : 0;
      const walk = st === 'green' && !(s.left < 5 && Math.floor(t * 2) % 2);
      P[g].green.emissiveIntensity = power && walk ? 4 : 0;
      P[g].red.emissiveIntensity = power && st !== 'green' ? 4 : 0;
    }
    return s;
  }
  const _beam = new THREE.Matrix4(), _fwd = new THREE.Matrix4(), inLane = [], zebra = [];
  // Zebra crossings (town-geo) and, per lane, the indices of the ones on its road; busy: someone is on it now.
  const ZONES = zebraZones(CROSSWALKS, ROADS), ZEBRA = LANES.map(lane => {
    const road = lane.axis === 'x' ? ROADS[0] : ROADS.find(r => r.z1 - r.z0 > r.x1 - r.x0 && lane.c > r.x0 && lane.c < r.x1);
    return CROSSWALKS.map((c, k) => (c.road === road.id ? k : -1)).filter(k => k >= 0);
  });
  const busy = new Set(), onFoot = [], byLane = LANES.map((_, li) => pool.filter(c => c.lane === li)), cars = [], wrapLead = { u: 0, v: 0, len: 0 };
  const STOPS_U = LANES.map(lane => lane.stops.map(x => x * lane.dir).sort((a, b) => a - b));
  function update(dt, t, ctx = {}) {
    const env = ctx.env;
    power = ctx.power !== false && env !== 'blackout';
    night = env === 'night' || env === 'blackout' || env === 'dusk';
    if (env !== lampEnv) { // parked lamps: a glint of the street lights at night, nothing in a blackout
      lampEnv = env; lampMat.emissiveIntensity = night ? 2 : 0.25; parkedLampMat.emissiveIntensity = env === 'blackout' ? 0 : night ? 0.04 : 0.25;
    }
    beams.visible = night && mode !== 'none';
    const s = setLamps(t), pl = ctx.player, peds = ctx.peds || [];
    onFoot.length = 0; if (pl && pl.y < 0.6) onFoot.push(pl); for (const p of peds) onFoot.push(p);
    busyZebras(ZONES, onFoot, busy);
    for (let li = 0; li < LANES.length; li++) {
      const lane = LANES[li];
      cars.length = 0; for (const c of byLane[li]) if (c.active) cars.push(c);
      cars.sort(byU);
      const stopsU = STOPS_U[li];
      const light = power ? s[lane.group] : 'green';
      // people on this lane, as lane coordinates u (the player only when on the road, not up on the curb)
      inLane.length = 0;
      if (pl && pl.y < 0.6 && Math.abs((lane.axis === 'x' ? pl.z : pl.x) - lane.c) < YIELD) inLane.push((lane.axis === 'x' ? pl.x : pl.z) * lane.dir);
      for (const p of peds) if (Math.abs((lane.axis === 'x' ? p.z : p.x) - lane.c) < YIELD) inLane.push((lane.axis === 'x' ? p.x : p.z) * lane.dir);
      // anyone on a zebra of this road: wait at its stop line (1.2 m short of the stripes) until it is clear
      zebra.length = 0;
      for (const k of ZEBRA[li]) if (busy.has(k)) zebra.push(CROSSWALKS[k].at * lane.dir - 3.2);
      for (let i = 0; i < cars.length; i++) {
        const c = cars[i], lead = cars[i + 1] || (cars.length > 1 ? ((wrapLead.u = cars[0].u + 2 * LANE_SPAN), (wrapLead.v = cars[0].v), (wrapLead.len = cars[0].len), wrapLead) : null);
        const front = c.u + c.len / 2;
        let gap = lead ? lead.u - c.u - (c.len + lead.len) / 2 : 1e9, vl = lead ? lead.v : c.v0;
        if (light !== 'green') for (const su of stopsU) {
          const d = su - front; if (d < -0.5) continue;
          if (d < 80 && (light === 'red' || d > c.v * c.v / 8 + 2) && d < gap) { gap = Math.max(0.05, d); vl = 0; }
          break;
        }
        for (const pu of inLane) { const d = pu - front - 1.4; if (d > -2.5 && d < 30 && d < gap) { gap = Math.max(0.05, d); vl = 0; } }
        for (const su of zebra) { const d = su - front; if (d > -0.5 && d < 40 && d < gap) { gap = Math.max(0.05, d); vl = 0; } }
        const dv = c.v - vl, sStar = 1.6 + Math.max(0, c.v * 1.2 + c.v * dv / (2 * Math.sqrt(2.2 * 3.2)));
        const acc = Math.max(-9, 2.2 * (1 - (c.v / c.v0) ** 4 - (sStar / Math.max(gap, 0.05)) ** 2));
        c.v = Math.max(0, c.v + acc * dt); c.u += c.v * dt;
        if (c.u > LANE_SPAN) c.u -= 2 * LANE_SPAN;
      }
    }
    for (const im of movingIMs) im.instanceMatrix.needsUpdate = true;
    pool.forEach((c, k) => {
      const lane = LANES[c.lane], list = meshes[c.type], slot = slots.get(c), s0 = c.u * lane.dir;
      const x = lane.axis === 'x' ? s0 : lane.c, z = lane.axis === 'x' ? lane.c : s0;
      const yaw = lane.axis === 'x' ? yawTo(lane.dir, 0) : yawTo(0, lane.dir);
      if (c.active) _m.compose(_p.set(x, 0, z), _q.setFromAxisAngle(UP, yaw), _s.set(1, 1, 1)); else _m.compose(_p.set(x, -50, z), _q, _z);
      for (const im of list) im.setMatrixAt(slot, _m);
      beams.setMatrixAt(k, c.active ? _beam.copy(_m).multiply(_fwd.makeTranslation(0, 0, -c.len / 2)) : _m);
      const S = c.solid, hl = c.len / 2 + 0.05, hw = TYPES[c.type].W / 2 + 0.05;
      S.off = !c.active || Math.abs(x) > 135 || Math.abs(z) > 115;
      if (lane.axis === 'x') { S.x0 = x - hl; S.x1 = x + hl; S.z0 = z - hw; S.z1 = z + hw; } else { S.x0 = x - hw; S.x1 = x + hw; S.z0 = z - hl; S.z1 = z + hl; }
    });
    beams.instanceMatrix.needsUpdate = true;
  }
  setMode('normal');
  update(0, 0, {});
  return { group, solids, setMode, update, get mode() { return mode; } };
}
