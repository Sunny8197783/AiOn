// Traffic: three procedural vehicle types (sedan, SUV, 1t box truck) drawn as instanced parts with
// clear-coat paint tinted per car, parked cars in every marked bay row, moving cars on LANES that follow
// each other (IDM), stop at red/amber and for the player, signal heads and pedestrian signals that run
// the CYCLE from town-plan, headlights and road glow at night. Moving cars carry live solids.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { CROSSWALKS, ROADS } from '../game/townmap.js';
import { LANES, LANE_SPAN, CURB, signalAt } from './town-plan.js';
import { yawTo } from './town-util.js';

const NG = (g) => (g.index ? g.toNonIndexed() : g);
const merge = (list) => mergeGeometries(list.map(NG));
const PARTS = ['body', 'glass', 'dark', 'lamp'];

// Side profiles in (s from the front bumper, y). Wheel arches are cut into the bottom edge.
const TYPES = {
  sedan: { L: 4.7, W: 1.82, r: 0.32, axles: [0.95, 3.85], weight: 0.5, v: 1,
    top: [[0.05, 0.32], [0, 0.55], [0.08, 0.72], [0.9, 0.86], [1.65, 0.95], [2.25, 1.4], [3.45, 1.42], [4.15, 1.0], [4.62, 0.95], [4.7, 0.62], [4.66, 0.32]],
    glass: [[1.62, 0.955], [2.24, 1.415], [2.32, 1.36], [3.4, 1.37], [3.46, 1.428], [4.17, 1.005], [4.1, 0.96], [1.7, 0.94]], pillar: 2.85, lights: [0.68, 0.86, -0.01, 4.69] },
  suv: { L: 4.6, W: 1.88, r: 0.36, axles: [1.0, 3.75], weight: 0.3, v: 0.95,
    top: [[0.05, 0.4], [0, 0.66], [0.1, 0.92], [1.0, 1.02], [1.55, 1.08], [2.08, 1.66], [4.18, 1.69], [4.52, 1.5], [4.6, 1.02], [4.58, 0.4]],
    glass: [[1.52, 1.09], [2.06, 1.69], [2.14, 1.62], [4.12, 1.63], [4.21, 1.705], [4.545, 1.505], [4.5, 1.08], [1.6, 1.07]], pillar: 2.95, lights: [0.8, 1.05, -0.01, 4.65] },
  truck: { L: 5.1, W: 1.76, r: 0.33, axles: [0.78, 3.9], weight: 0.2, v: 0.85,
    top: [[0, 0.42], [0, 1.0], [0.08, 1.72], [0.34, 1.98], [1.62, 1.98], [1.62, 0.95], [5.1, 0.95], [5.1, 0.42]],
    glass: [[0.035, 1.12], [0.115, 1.78], [0.36, 1.995], [1.0, 1.995], [1.0, 1.12]], box: [1.7, 5.1, 0.98, 2.42], pillar: 0, lights: [0.62, 0.8, -0.05, 5.14] },
};

function vehicleGeo(T) {
  const { L, W, r } = T, bev = 0.05;
  const outline = [...T.top];
  const [af, ar] = T.axles, R = r + 0.06, by = T.top[0][1];
  const arch = (c) => { const pts = []; for (let k = 0; k <= 10; k++) { const a = k / 10 * Math.PI; pts.push([c + R * Math.cos(a), Math.max(by, r + R * Math.sin(a) * 0.95)]); } return pts; };
  outline.push(...arch(ar), ...arch(af));
  const shape = new THREE.Shape(outline.map(([s, y]) => new THREE.Vector2(s, y)));
  const toCar = (g, depth) => g.translate(-L / 2, 0, -depth / 2).rotateY(-Math.PI / 2);
  const bodyParts = [toCar(new THREE.ExtrudeGeometry(shape, { depth: W - 2 * bev, bevelEnabled: true, bevelThickness: bev, bevelSize: bev, bevelSegments: 2, curveSegments: 4 }), W - 2 * bev)];
  if (T.box) { const [s0, s1, y0, y1] = T.box; bodyParts.push(new THREE.BoxGeometry(W + 0.14, y1 - y0, s1 - s0).translate(0, (y0 + y1) / 2, (s0 + s1) / 2 - L / 2)); }
  if (T.pillar) bodyParts.push(new THREE.BoxGeometry(W + 0.02, 0.42, 0.09).translate(0, (T.glass[2][1] + T.glass[0][1]) / 2, T.pillar - L / 2));
  const glass = toCar(new THREE.ExtrudeGeometry(new THREE.Shape(T.glass.map(([s, y]) => new THREE.Vector2(s, y))), { depth: W + 0.012, bevelEnabled: false }), W + 0.012);
  const z = (s) => s - L / 2, hx = W / 2 - 0.13;
  const tyres = [], hubs = [];
  for (const s of T.axles) for (const sx of [-1, 1]) {
    tyres.push(new THREE.CylinderGeometry(r, r, 0.22, 16).rotateZ(Math.PI / 2).translate(sx * hx, r, z(s)));
    hubs.push(new THREE.CylinderGeometry(r * 0.62, r * 0.62, 0.02, 12).rotateZ(Math.PI / 2).translate(sx * (hx + 0.115), r, z(s)));
  }
  const dark = [...tyres,
    new THREE.BoxGeometry(W * 0.98, 0.16, 0.12).translate(0, T.top[0][1] + 0.06, z(0.04)), new THREE.BoxGeometry(W * 0.98, 0.16, 0.12).translate(0, T.top[0][1] + 0.06, z(L - 0.04)),
    new THREE.BoxGeometry(W * 0.5, 0.17, 0.04).translate(0, T.lights[0] - 0.12, z(0) - 0.01),
    ...[-1, 1].map(sx => new THREE.BoxGeometry(0.1, 0.09, 0.16).translate(sx * (W / 2 + 0.07), T.glass[0][1] + 0.06, z(T.glass[0][0] + 0.15)))];
  // Head and tail lamps share one material: UVs pick the white or red half of a 2 px texture.
  const lamp = (s, y, w, d, u) => [-1, 1].map(sx => { const g = new THREE.BoxGeometry(w, 0.12, d).translate(sx * (W / 2 - 0.22 - w / 2 + 0.1), y, z(s)), uv = g.attributes.uv; for (let k = 0; k < uv.count; k++) uv.setXY(k, u, 0.5); return g; });
  return {
    body: merge(bodyParts), glass, dark: merge([...dark, ...hubs, new THREE.BoxGeometry(0.52, 0.11, 0.02).translate(0, T.top[0][1] + 0.2, z(L) + 0.07)]),
    lamp: merge([...lamp(T.lights[2], T.lights[0], 0.36, 0.07, 0.25), ...lamp(T.lights[3], T.lights[1], 0.32, 0.06, 0.75)]),
  };
}

// Rows of marked bays (same lines as town-ground): [x0, x1, zLine, depth, step, kind]
const ROWS = [[-35, 3, -22, 5, 2.5], [6, 40, -51.5, 5, 2.5], [6, 40, -10.5, -5, 2.5], [6, 40, -31, 5, 2.5], [6, 40, -31, -5, 2.5],
  [74.5, 82, -10.5, -5.5, 2.5], [92, 126, -10.5, -5.5, 2.5], [104.4, 123.6, -60, 6, 3.2, 'truck'], [-124, -90, 59.5, -5, 2.5],
  [60, 126, 13.5, 5, 2.5], [62, 116, 57, -5, 2.5], [62, 116, 85, -5, 2.5], [14, 40, 51.5, -5, 2.5], [14, 40, 22.5, 5, 2.5]];
const KEEP_CLEAR = [{ x0: -20.5, x1: -11.5, z0: -23, z1: -16 }, { x0: -9.5, x1: -5.5, z0: -23, z1: -16 }];
const PALETTE = [['#f2f2ef', 35], ['#8d9196', 20], ['#16181b', 15], ['#c4c7ca', 12], ['#1f2f4f', 8], ['#8e1f22', 4], ['#c9b89a', 3], ['#2f4a3a', 3]];

export function buildTraffic(kit) {
  const group = new THREE.Group(); group.name = 'traffic';
  const rand = kit.rng(31337), solids = [];
  const pickW = (list) => { let t = rand() * list.reduce((s, [, w]) => s + w, 0); for (const [v, w] of list) if ((t -= w) <= 0) return v; return list[0][0]; };
  const randType = () => pickW(Object.entries(TYPES).map(([k, T]) => [k, T.weight]));

  // ---- parked cars ----
  const parked = [];
  for (const [x0, x1, zl, depth, step, kind] of ROWS) for (let x = x0; x + step <= x1 + 1e-6; x += step) {
    const cx = x + step / 2, cz = zl + depth / 2;
    if (rand() > (kind ? 0.7 : 0.62) || KEEP_CLEAR.some(r => cx > r.x0 && cx < r.x1 && cz > r.z0 && cz < r.z1)) continue;
    const type = kind || randType();
    parked.push({ type, x: cx + (rand() - 0.5) * 0.2, z: cz + (rand() - 0.5) * 0.3, yaw: (kind ? Math.PI : rand() < 0.5 ? 0 : Math.PI) + (rand() - 0.5) * 0.05 });
  }
  for (const [zc, yaw] of [[57.3, Math.PI / 2], [62.7, -Math.PI / 2], [-62.7, Math.PI / 2], [-57.3, -Math.PI / 2]]) for (let x = -40; x <= 40; x += 6.5) {
    if (Math.abs(x) < 5 || rand() < 0.45) continue;
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
  const paint = kit.memo('town:carPaint', () => new THREE.MeshPhysicalMaterial({ color: 0xffffff, roughness: 0.32, metalness: 0.45, clearcoat: 1, clearcoatRoughness: 0.08 }));
  const lampTex = kit.memo('town:carLampTex', () => { const t = kit.texture(kit.canvas(2, 1, (g) => { g.fillStyle = '#fff4dc'; g.fillRect(0, 0, 1, 1); g.fillStyle = '#ff2a1c'; g.fillRect(1, 0, 1, 1); }), { repeat: [1, 1] }); t.magFilter = t.minFilter = THREE.NearestFilter; t.generateMipmaps = false; return t; });
  const lampMat = new THREE.MeshStandardMaterial({ map: lampTex, emissiveMap: lampTex, emissive: 0xffffff, emissiveIntensity: 0.25, roughness: 0.25 });
  const mats = { body: paint, glass: kit.mat('darkGlass', { roughness: 0.04 }), dark: kit.mat('paint:#141517', { roughness: 0.7 }), lamp: lampMat };
  const meshes = {}, slots = new Map();
  const _m = new THREE.Matrix4(), _q = new THREE.Quaternion(), _p = new THREE.Vector3(), _s = new THREE.Vector3(1, 1, 1), _z = new THREE.Vector3(0, 0, 0), _c = new THREE.Color(), UP = new THREE.Vector3(0, 1, 0);
  for (const [type, T] of Object.entries(TYPES)) {
    const moving = pool.filter(c => c.type === type), still = parked.filter(p => p.type === type), count = moving.length + still.length;
    if (!count) continue;
    const geo = vehicleGeo(T);
    meshes[type] = PARTS.map(part => {
      const im = new THREE.InstancedMesh(geo[part], mats[part], count);
      im.castShadow = part === 'body' || part === 'dark'; im.receiveShadow = part !== 'lamp';
      im.frustumCulled = false; im.name = `car:${type}:${part}`; group.add(im); return im;
    });
    moving.forEach((c, i) => slots.set(c, i));
    still.forEach((p, i) => {
      _m.compose(_p.set(p.x, 0, p.z), _q.setFromAxisAngle(UP, p.yaw), _s.set(1, 1, 1));
      for (const im of meshes[type]) im.setMatrixAt(moving.length + i, _m);
      meshes[type][0].setColorAt(moving.length + i, _c.set(p.color));
    });
    moving.forEach((c, i) => meshes[type][0].setColorAt(i, _c.set(c.color)));
    meshes[type][0].instanceColor.needsUpdate = true;
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
  let mode = 'normal', power = true, night = false;
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
  const _beam = new THREE.Matrix4(), _fwd = new THREE.Matrix4();
  function update(dt, t, ctx = {}) {
    const env = ctx.env;
    power = ctx.power !== false && env !== 'blackout';
    const isNight = env === 'night' || env === 'blackout' || env === 'dusk';
    if (isNight !== night) { night = isNight; lampMat.emissiveIntensity = night ? 3 : 0.25; }
    beams.visible = night && mode !== 'none';
    const s = setLamps(t), pl = ctx.player;
    for (let li = 0; li < LANES.length; li++) {
      const lane = LANES[li], cars = pool.filter(c => c.lane === li && c.active).sort((a, b) => a.u - b.u);
      const stopsU = lane.stops.map(x => x * lane.dir).sort((a, b) => a - b);
      const light = power ? s[lane.group] : 'green';
      const pu = pl && pl.y < 0.6 && Math.abs((lane.axis === 'x' ? pl.z : pl.x) - lane.c) < 1.7 ? (lane.axis === 'x' ? pl.x : pl.z) * lane.dir : null;
      for (let i = 0; i < cars.length; i++) {
        const c = cars[i], lead = cars[i + 1] || (cars.length > 1 ? { u: cars[0].u + 2 * LANE_SPAN, v: cars[0].v, len: cars[0].len } : null);
        const front = c.u + c.len / 2;
        let gap = lead ? lead.u - c.u - (c.len + lead.len) / 2 : 1e9, vl = lead ? lead.v : c.v0;
        if (light !== 'green') for (const su of stopsU) {
          const d = su - front; if (d < -0.5) continue;
          if (d < 80 && (light === 'red' || d > c.v * c.v / 8 + 2) && d < gap) { gap = Math.max(0.05, d); vl = 0; }
          break;
        }
        if (pu !== null) { const d = pu - front - 1.4; if (d > -2.5 && d < 30 && d < gap) { gap = Math.max(0.05, d); vl = 0; } }
        const dv = c.v - vl, sStar = 1.6 + Math.max(0, c.v * 1.2 + c.v * dv / (2 * Math.sqrt(2.2 * 3.2)));
        const acc = Math.max(-9, 2.2 * (1 - (c.v / c.v0) ** 4 - (sStar / Math.max(gap, 0.05)) ** 2));
        c.v = Math.max(0, c.v + acc * dt); c.u += c.v * dt;
        if (c.u > LANE_SPAN) c.u -= 2 * LANE_SPAN;
      }
    }
    for (const [type, list] of Object.entries(meshes)) for (const im of list) im.instanceMatrix.needsUpdate = true;
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
