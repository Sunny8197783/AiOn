// Street furniture: street lights (instanced heads in light group 'street', ground glow, a few real
// lights), 전봇대 with sagging wires and service drops, the bus stop, bollards, hydrants, planters,
// trash bags at collection points, bicycles, parcels at the logistics centre, AC units, 현수막,
// vending machines and the apartment recycling shed. Fences live in town-fences.js.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { CROSSWALKS, ROADS, BUILDINGS } from '../game/townmap.js';
import { CURB } from './town-plan.js';
import { sway, yawTo } from './town-util.js';

const Y = CURB;
const NG = (g) => (g.index ? g.toNonIndexed() : g);
const mergeAll = (list) => mergeGeometries(list.map(NG));

// Planter boxes with shrubs; pure so the tree builder can add the shrubs to its instancing.
export const PLANTERS = [
  [-30, -23.3, 0], [-4, -23.3, 0], [-98, -26.6, 0], [-80, -26.6, 0], [74, -20.8, 0], [100, -20.8, 0],
  [-66, 21, Math.PI / 2], [-66, 41, Math.PI / 2], [69.5, 29, 0], [88, 29, 0], [-123, -14.6, 0], [18, 10.9, 0],
];
export function shrubSpots(rand) {
  const out = [];
  for (const [x, z, ry] of PLANTERS) for (const k of [-0.75, 0, 0.75]) out.push({ x: x + Math.cos(ry) * k, z: z - Math.sin(ry) * k, type: 'shrub', s: 0.55 + rand() * 0.2, ry: rand() * 6.28, tint: rand() });
  for (let x = 66; x <= 110; x += 2.2) out.push({ x, z: 42.9, type: 'shrub', s: 0.7 + rand() * 0.3, ry: rand() * 6.28, tint: rand() });
  for (let z = 18; z <= 44; z += 2.4) out.push({ x: -113.2, z, type: 'shrub', s: 0.8, ry: rand() * 6.28, tint: rand() });
  return out;
}

export function buildProps(kit, { avoid = [] } = {}) {
  const group = new THREE.Group(); group.name = 'props';
  const stat = new THREE.Group(); group.add(stat);
  const solids = [];
  const rand = kit.rng(2024);
  const box = (x0, x1, z0, z1, y1 = 2.5, id = 'prop') => solids.push({ x0, x1, z0, z1, y0: -0.5, y1, id });
  const clear = (x, z, d = 2.4) => !avoid.some(a => Math.hypot(a.x - x, a.z - z) < d);
  const steel = kit.mat('steel'), dark = kit.mat('darkMetal'), metal = kit.mat('metal');

  // ---- street lights ----
  const cw = (road) => CROSSWALKS.filter(c => c.road === road).map(c => c.at);
  const lights = [];
  const addLight = (x, z, ry) => { if (clear(x, z)) lights.push({ x, y: Y, z, ry }); };
  for (let x = -122; x <= 122; x += 26) {
    for (const [z, ry, off] of [[6.55, 0, 0], [-6.55, Math.PI, 13]]) {
      const xx = x + off;
      if (xx > 126 || cw('yeonmu').some(a => Math.abs(a - xx) < 4.6) || Math.abs(Math.abs(xx) - 50) < 5.2) continue;
      addLight(xx, z, ry);
    }
  }
  for (const [x, ry, road] of [[-54.55, -Math.PI / 2, 'munhwa'], [-45.45, Math.PI / 2, 'munhwa'], [45.45, -Math.PI / 2, 'hanbit'], [54.55, Math.PI / 2, 'hanbit']]) {
    for (let z = -102 + (x > 0 ? 13 : 0); z <= 104; z += 26) {
      if (Math.abs(z) < 9 || Math.abs(Math.abs(z) - 60) < 5.5 || cw(road).some(a => Math.abs(a - z) < 4.6)) continue;
      addLight(x, z, ry);
    }
  }
  for (let x = -38; x <= 40; x += 26) { addLight(x, -64.55, Math.PI); addLight(x + 13, 64.55, 0); }
  const poleGeo = mergeAll([
    new THREE.CylinderGeometry(0.075, 0.115, 8.6, 10).translate(0, 4.3, 0),
    new THREE.BoxGeometry(0.32, 0.55, 0.26).translate(0, 0.28, 0),
    new THREE.CylinderGeometry(0.045, 0.06, 1.0, 8).rotateX(-0.9).translate(0, 8.75, -0.38),
    new THREE.CylinderGeometry(0.045, 0.045, 1.5, 8).rotateX(Math.PI / 2).translate(0, 9.05, -1.5),
    new THREE.BoxGeometry(0.34, 0.14, 0.72).translate(0, 9.0, -2.45),
  ]);
  const lensMat = kit.memo('town:streetLens', () => kit.lit('street', new THREE.MeshStandardMaterial({ color: 0xfff6e8, emissive: 0xfff0d8, emissiveIntensity: 6, roughness: 0.3 }), 6));
  group.add(kit.instanced(poleGeo, kit.mat('paint:#7d848a', { roughness: 0.45, metalness: 0.6 }), lights));
  group.add(kit.instanced(new THREE.BoxGeometry(0.26, 0.03, 0.6).translate(0, 8.92, -2.45), lensMat, lights, { cast: false }));
  for (const p of lights) box(p.x - 0.18, p.x + 0.18, p.z - 0.18, p.z + 0.18, 4, 'streetlight');
  // Light pools on the ground under each head (additive, follows the street group).
  const glowTex = kit.memo('town:glowTex', () => kit.texture(kit.canvas(128, 128, (g, w) => {
    const r = g.createRadialGradient(w / 2, w / 2, 0, w / 2, w / 2, w / 2);
    r.addColorStop(0, 'rgba(255,255,255,1)'); r.addColorStop(0.45, 'rgba(255,255,255,0.45)'); r.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = r; g.fillRect(0, 0, w, w);
  }), { repeat: [1, 1] }));
  const poolMat = kit.memo('town:pool', () => kit.lit('street', new THREE.MeshStandardMaterial({ color: 0x000000, emissive: 0xffdcae, emissiveMap: glowTex, emissiveIntensity: 0.55, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, polygonOffset: true, polygonOffsetFactor: -4 }), 0.55));
  const pools = lights.map(p => ({ x: p.x - Math.sin(p.ry) * 2.45, y: 0.02, z: p.z - Math.cos(p.ry) * 2.45, s: 1 }));
  group.add(kit.instanced(new THREE.PlaneGeometry(10, 10).rotateX(-Math.PI / 2), poolMat, pools, { cast: false, receive: false }));
  // A few real lights at the places the story stops (others glow only).
  const keyLights = [[78, 25.5], [-95, 10.5], [36, 8.4], [-80, -15], [-63, 31], [-61, -34], [0, 9]];
  for (const [x, z] of keyLights.slice(0, { low: 2, medium: 4 }[kit.quality] ?? keyLights.length)) {
    const L = new THREE.PointLight(0xffd9a8, 260, 30, 2); L.position.set(x, 7.6, z);
    kit.lit('street', L, 260); group.add(L);
  }

  // ---- 전봇대 (concrete utility poles), sagging power and telecom lines, service drops ----
  const lines = [
    { pts: [-40, -20, 0, 20, 40].map(x => [x, -52.6]), across: [0, 1] },
    { pts: [-40, -20, 0, 20, 40].map(x => [x, 52.6]), across: [0, 1] },
    { pts: [-104, -88, -72].map(z => [-57.6, z]), across: [1, 0] }, { pts: [70, 87, 104].map(z => [-57.6, z]), across: [1, 0] },
    { pts: [-104, -87, -70].map(z => [57.4, z]), across: [1, 0] }, { pts: [72, 88, 104].map(z => [57.4, z]), across: [1, 0] },
  ];
  const conc = kit.surface('concrete', { color: 0xbab6ad, repeat: [1, 3] });
  const upoles = lines.flatMap(L => L.pts.map(([x, z]) => ({ x, y: Y, z, ry: L.across[0] ? Math.PI / 2 : 0 })));
  group.add(kit.instanced(new THREE.CylinderGeometry(0.12, 0.19, 11, 12).translate(0, 5.5, 0), conc, upoles));
  group.add(kit.instanced(mergeAll([
    new THREE.BoxGeometry(2.2, 0.1, 0.1).translate(0, 10.3, 0), new THREE.BoxGeometry(1.6, 0.09, 0.09).translate(0, 9.5, 0),
    ...[-0.95, 0, 0.95].map(x => new THREE.CylinderGeometry(0.05, 0.06, 0.18, 8).translate(x, 10.44, 0)),
    new THREE.BoxGeometry(0.34, 0.12, 0.34).translate(0, 6.1, 0), new THREE.BoxGeometry(0.3, 0.1, 0.3).translate(0, 5.6, 0),
    ...[0, 1, 2, 3, 4, 5].map(i => new THREE.BoxGeometry(0.03, 0.03, 0.3).translate(0, 2.4 + i * 0.45, 0).rotateY(i * 1.57)),
  ]), steel, upoles));
  const trafos = upoles.filter((_, i) => i % 3 === 1);
  group.add(kit.instanced(new THREE.CylinderGeometry(0.32, 0.32, 1.05, 14).translate(0.42, 8.1, 0), kit.mat('paint:#8c9297', { roughness: 0.5, metalness: 0.4 }), trafos));
  for (const p of upoles) box(p.x - 0.22, p.x + 0.22, p.z - 0.22, p.z + 0.22, 4, 'utilityPole');
  const wire = [];
  const sag = (a, b, s, n = 14) => { for (let i = 0; i < n; i++) { const t0 = i / n, t1 = (i + 1) / n; for (const t of [t0, t1]) wire.push(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t - 4 * s * t * (1 - t), a[2] + (b[2] - a[2]) * t); } };
  for (const L of lines) for (let i = 1; i < L.pts.length; i++) {
    const [ax, az] = L.pts[i - 1], [bx, bz] = L.pts[i], [ux, uz] = L.across;
    for (const o of [-0.95, 0, 0.95]) sag([ax + ux * o, Y + 10.5, az + uz * o], [bx + ux * o, Y + 10.5, bz + uz * o], 0.42);
    for (const [o, h, s] of [[-0.12, 6.15, 0.7], [0.1, 6.05, 0.85], [0, 5.62, 0.95], [0.15, 5.55, 1.1]]) sag([ax + ux * o, Y + h, az + uz * o], [bx + ux * o, Y + h, bz + uz * o], s);
  }
  for (const L of lines) for (const [x, z] of L.pts) { // service drops to the nearest building
    const b = BUILDINGS.reduce((best, q) => { const cx = Math.max(q.x0, Math.min(x, q.x1)), cz = Math.max(q.z0, Math.min(z, q.z1)); const d = Math.hypot(cx - x, cz - z); return d < best.d ? { d, cx, cz } : best; }, { d: Infinity });
    if (b.d < 16) for (const dz of [-0.6, 0.5]) sag([x, Y + 6, z], [b.cx + (L.across[0] ? 0 : dz), Y + 5.2, b.cz + (L.across[0] ? dz : 0)], 0.5, 8);
  }
  const wg = new THREE.BufferGeometry(); wg.setAttribute('position', new THREE.Float32BufferAttribute(wire, 3));
  const wires = new THREE.LineSegments(wg, new THREE.LineBasicMaterial({ color: 0x17181a })); wires.name = 'wires'; group.add(wires);

  // ---- bus stop (연무로 north side, westbound) ----
  const glass = kit.mat('glass'), roofMat = kit.mat('paint:#3a4a5a', { roughness: 0.5, metalness: 0.5 });
  const bx = 11.5, bz = -9.0;
  stat.add(kit.box(4.2, 0.1, 1.8, roofMat, { x: bx, y: Y + 2.55, z: bz + 0.1 }));
  for (const dx of [-2, 2]) stat.add(kit.box(0.09, 2.55, 0.09, steel, { x: bx + dx, y: Y, z: bz - 0.62 }), kit.box(0.09, 2.55, 0.09, steel, { x: bx + dx, y: Y, z: bz + 0.6 }));
  const back = kit.box(4.0, 2.1, 0.02, glass, { x: bx, y: Y + 0.3, z: bz - 0.64, cast: false }); back.userData.keep = true; stat.add(back);
  stat.add(kit.box(2.8, 0.05, 0.4, kit.surface('wood', { color: 0xb88a5c }), { x: bx - 0.4, y: Y + 0.44, z: bz - 0.35 }), kit.box(2.8, 0.44, 0.06, dark, { x: bx - 0.4, y: Y, z: bz - 0.35 }));
  const mapPanel = kit.sign(['연무동 행정복지센터 · 12-345', '마을 3  ·  720  ·  1115', '다음: 연무마트 → 한빛아파트'], { w: 1.4, h: 1.9, bg: '#eef2f5', fg: '#16324f', single: true, emissive: 0.15 });
  kit.lit('shops', mapPanel.material, 0.6);
  mapPanel.position.set(bx + 2.0, Y + 1.35, bz - 0.01); mapPanel.rotation.y = -Math.PI / 2; stat.add(mapPanel);
  const bis = kit.sign(['마을 3  곧 도착', '720  6분'], { w: 1.2, h: 0.32, bg: '#120c08', fg: '#ffb340', single: true, emissive: 1 });
  kit.lit('shops', bis.material, 1.6); bis.position.set(bx - 1.2, Y + 2.3, bz + 0.98); bis.rotation.y = 0; stat.add(bis);
  stat.add(kit.cyl(0.05, 0.05, 2.9, steel, { x: bx + 3.2, y: Y, z: -6.9 }, 8));
  const stopSign = kit.sign(['버스', '연무동 행정복지센터'], { w: 0.7, h: 0.6, bg: '#1f5fa8', single: false }); stopSign.position.set(bx + 3.2, Y + 2.6, -6.9); stopSign.rotation.y = Math.PI / 2; stat.add(stopSign);
  box(bx - 2.1, bx + 2.1, bz - 0.72, bz - 0.56, 2.6, 'busStop'); box(bx + 1.9, bx + 2.1, bz - 0.7, bz + 0.7, 2.6, 'busStop');

  // ---- bollards at the crossings, hydrants ----
  const bol = [];
  for (const c of CROSSWALKS) {
    const r = ROADS.find(q => q.id === c.road), ew = r.z1 - r.z0 < r.x1 - r.x0;
    for (const side of [-1, 1]) for (const k of [-2.6, 2.6]) {
      const edge = side < 0 ? (ew ? r.z0 : r.x0) - 1.05 : (ew ? r.z1 : r.x1) + 1.05;
      const [x, z] = ew ? [c.at + k, edge] : [edge, c.at + k];
      if (clear(x, z, 0.8)) bol.push({ x, y: Y, z });
    }
  }
  for (const x of [31, 33.5, 38.5, 41]) bol.push({ x, y: Y, z: 10.4 });
  group.add(kit.instanced(new THREE.CylinderGeometry(0.1, 0.11, 0.85, 14).translate(0, 0.425, 0), kit.mat('metal', { roughness: 0.25 }), bol));
  group.add(kit.instanced(new THREE.CylinderGeometry(0.112, 0.112, 0.07, 14).translate(0, 0.7, 0), kit.mat('paint:#f4f4f0', { roughness: 0.2, emissive: new THREE.Color(0x222222) }), bol, { cast: false }));
  for (const p of bol) box(p.x - 0.13, p.x + 0.13, p.z - 0.13, p.z + 0.13, 1, 'bollard');
  const hyd = [[-70, -8.8], [-20, 8.8], [26, -8.8], [62, 8.8], [-44.4, 40], [44.4, -40], [-56.8, -84], [56.8, 84], [-120, 8.8], [100, -8.8]].filter(([x, z]) => clear(x, z, 1.2)).map(([x, z]) => ({ x, y: Y, z, ry: rand() * 6.28 }));
  const red = kit.mat('paint:#c42b25', { roughness: 0.45 });
  group.add(kit.instanced(mergeAll([new THREE.CylinderGeometry(0.16, 0.2, 0.62, 14).translate(0, 0.31, 0), new THREE.SphereGeometry(0.17, 14, 8, 0, 6.29, 0, 1.6).translate(0, 0.62, 0), new THREE.CylinderGeometry(0.07, 0.07, 0.5, 10).rotateZ(Math.PI / 2).translate(0, 0.42, 0), new THREE.CylinderGeometry(0.06, 0.06, 0.12, 6).translate(0, 0.8, 0)]), red, hyd));
  for (const p of hyd) box(p.x - 0.25, p.x + 0.25, p.z - 0.25, p.z + 0.25, 1, 'hydrant');

  // ---- planter boxes (shrubs come from shrubSpots) ----
  const plMat = kit.surface('concrete', { color: 0xa9a59c }), soil = kit.mat('paint:#3b2e22', { roughness: 1 });
  for (const [x, z, ry] of PLANTERS) {
    stat.add(kit.slab(2.6, 0.5, 0.95, plMat, { x, y: Y, z, ry }), kit.box(2.45, 0.02, 0.8, soil, { x, y: Y + 0.46, z, ry, cast: false }));
    const ax = Math.abs(Math.cos(ry)) * 1.3 + Math.abs(Math.sin(ry)) * 0.48, az = Math.abs(Math.sin(ry)) * 1.3 + Math.abs(Math.cos(ry)) * 0.48;
    box(x - ax, x + ax, z - az, z + az, 0.9, 'planter');
  }

  // ---- 종량제 봉투 at collection points ----
  const bags = [], bagCol = [];
  for (const [cx, cz, n] of [[-20, -53.4, 9], [20, 53.4, 8], [40, -53.4, 6], [-57.1, 88, 7], [57.0, -88, 6], [43.2, 23, 5]]) {
    for (let i = 0; i < n; i++) {
      const a = rand() * 6.28, d = rand() * 0.8, s = 0.75 + rand() * 0.5;
      bags.push({ x: cx + Math.cos(a) * d, y: Y + 0.24 * s + (i > n - 3 ? 0.35 : 0), z: cz + Math.sin(a) * d * 0.6, ry: rand() * 6.28, sx: s, sy: s * (0.75 + rand() * 0.35), sz: s });
      bagCol.push(['#ecead8', '#f1e6a2', '#e9e3cc', '#cfdff0', '#f6f2e4'][(rand() * 5) | 0]);
    }
    box(cx - 0.9, cx + 0.9, cz - 0.6, cz + 0.6, 1, 'trash');
  }
  const bagMesh = kit.instanced(new THREE.IcosahedronGeometry(0.3, 1), kit.mat('paint:#ffffff', { roughness: 0.32, metalness: 0 }), bags);
  const bc = new THREE.Color(); bagCol.forEach((h, i) => bagMesh.setColorAt(i, bc.set(h))); bagMesh.instanceColor.needsUpdate = true; group.add(bagMesh);

  // ---- bicycles (frame tinted per instance) ----
  const bikes = [];
  for (const [x, z, ry, n] of [[30.6, 11.2, 0, 3], [-5.5, 11.0, 0, 2], [64.5, 27.4, Math.PI / 2, 4], [-62, 13.4, 0, 2], [15.8, -9.4, 0, 2]]) {
    for (let i = 0; i < n; i++) bikes.push({ x: x + (ry ? 0 : i * 0.75), y: Y, z: z + (ry ? i * 0.75 : 0), ry: ry + (rand() - 0.5) * 0.15 });
    const ex = ry ? 0.6 : (n - 1) * 0.75 + 0.3, ez = ry ? (n - 1) * 0.75 + 0.6 : 0.6;
    box(x - (ry ? ex : 0.3), x + ex, z - (ry ? 0.6 : ez), z + (ry ? ez : ez), 1.1, 'bikes');
  }
  const wheelL = (x) => new THREE.TorusGeometry(0.33, 0.022, 6, 22).rotateY(Math.PI / 2).translate(0, 0.35, x);
  const tube = (a, b, r = 0.018) => { const d = new THREE.Vector3(...b).sub(new THREE.Vector3(...a)), g = new THREE.CylinderGeometry(r, r, d.length(), 6); g.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.clone().normalize())); return g.translate((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2); };
  const frame = mergeAll([tube([0, 0.35, 0.52], [0, 0.42, -0.05]), tube([0, 0.42, -0.05], [0, 0.82, 0.38]), tube([0, 0.42, -0.05], [0, 0.86, -0.12]), tube([0, 0.86, -0.12], [0, 0.82, 0.38]), tube([0, 0.35, 0.52], [0, 0.86, -0.12]), tube([0, 0.82, 0.38], [0, 0.35, 0.58]), tube([0, 0.82, 0.38], [0, 1.0, 0.42]), tube([-0.28, 1.0, 0.42], [0.28, 1.0, 0.42], 0.016), new THREE.BoxGeometry(0.14, 0.05, 0.26).translate(0, 0.98, -0.16)]);
  const bikeFrames = kit.instanced(frame, kit.mat('paint:#ffffff', { roughness: 0.4, metalness: 0.5 }), bikes);
  bikes.forEach((_, i) => bikeFrames.setColorAt(i, bc.set(['#2a5b8f', '#c8c8c8', '#a12c2c', '#2f2f33', '#6b8f3a', '#e0e0d8'][(rand() * 6) | 0]))); bikeFrames.instanceColor.needsUpdate = true;
  group.add(bikeFrames, kit.instanced(mergeAll([wheelL(-0.55), wheelL(0.55)]), kit.mat('rubber'), bikes));
  stat.add(kit.box(2.4, 0.05, 0.05, metal, { x: 31.4, y: Y + 0.6, z: 11.6 }));

  // ---- parcels at 한결물류 (pallets, cardboard stacks, roll cages) ----
  const card = kit.memo('town:cardboard', () => {
    const c = kit.canvas(128, 128, (g, w) => { g.fillStyle = '#b48a5a'; g.fillRect(0, 0, w, w); g.fillStyle = '#c9a777'; g.fillRect(0, w * 0.44, w, w * 0.12); g.fillStyle = '#f2efe6'; g.fillRect(w * 0.58, w * 0.62, w * 0.3, w * 0.2); g.fillStyle = '#333'; for (let i = 0; i < 6; i++) g.fillRect(w * 0.6 + i * 5, w * 0.66, 2, w * 0.1); });
    return new THREE.MeshStandardMaterial({ map: kit.texture(c), roughness: 0.88 });
  });
  const parcels = [], pallets = [];
  for (const [px, pz] of [[97, -18.6], [98.4, -18.6], [99.8, -18.6], [97.6, -16.9], [99.2, -16.9], [102.2, -18.4]]) {
    pallets.push({ x: px, y: Y, z: pz });
    for (let lay = 0; lay < 2 + ((rand() * 3) | 0); lay++) for (const [dx, dz] of [[-0.27, -0.27], [0.27, -0.27], [-0.27, 0.27], [0.27, 0.27]]) {
      if (rand() < 0.15) continue;
      const s = 0.42 + rand() * 0.1, h = 0.3 + rand() * 0.15;
      parcels.push({ x: px + dx, y: Y + 0.15 + lay * 0.46 + h / 2, z: pz + dz, ry: (rand() - 0.5) * 0.2, sx: s, sy: h, sz: s });
    }
  }
  for (let i = 0; i < 9; i++) parcels.push({ x: 89 + rand() * 3, y: Y + 0.15, z: -21.4 + rand() * 0.8, ry: rand(), sx: 0.4, sy: 0.3, sz: 0.35 });
  group.add(kit.instanced(new THREE.BoxGeometry(1, 1, 1), card, parcels));
  group.add(kit.instanced(mergeAll([new THREE.BoxGeometry(1.1, 0.04, 1.1).translate(0, 0.13, 0), ...[-0.45, 0, 0.45].map(x => new THREE.BoxGeometry(0.1, 0.1, 1.1).translate(x, 0.06, 0))]), kit.surface('wood', { color: 0xa8865c }), pallets));
  box(96.3, 103, -19.3, -16.2, 2.2, 'parcels');
  const cage = kit.memo('town:cageMesh', () => new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(64, 128, (g, w, h) => { g.strokeStyle = '#c9ced2'; g.lineWidth = 3; for (let x = 2; x < w; x += 12) { g.beginPath(); g.moveTo(x, 0); g.lineTo(x, h); g.stroke(); } for (let y = 2; y < h; y += 14) { g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.stroke(); } })), alphaTest: 0.5, side: THREE.DoubleSide, metalness: 0.7, roughness: 0.4 }));
  for (const [x, z] of [[93.2, -20.6], [94.2, -20.6], [95.2, -20.6]]) {
    stat.add(kit.box(0.8, 1.7, 0.62, cage, { x, y: Y + 0.12, z, cast: false }), kit.box(0.82, 0.1, 0.64, steel, { x, y: Y + 0.02, z }));
    parcels.length && stat.add(kit.box(0.6, 0.5, 0.45, card, { x, y: Y + 0.22, z }));
  }
  box(92.7, 95.7, -21, -20.2, 2, 'cages');

  // ---- AC outdoor units (실외기) on shop/villa/civic walls ----
  const acs = [];
  for (const b of BUILDINGS) {
    if (b.kind === 'apartment' || b.kind === 'warehouse' || b.id === 'culture') continue;
    const fh = b.kind === 'villa' ? 2.9 : 3.5, floors = Math.min(b.floors, 4), r = kit.rng(b.x0 * 7 + b.z0);
    for (const [side, len] of [['s', b.x1 - b.x0], ['e', b.z1 - b.z0], ['w', b.z1 - b.z0], ['n', b.x1 - b.x0]]) {
      if (side === b.door || (b.kind === 'shop' && b.floors <= 2 && side === 'n')) continue;
      for (let a = 2.5; a < len - 2; a += 4 + r() * 4) for (let f = 0; f < floors; f++) {
        if (r() < (f ? 0.55 : 0.4)) continue;
        const y = Y + f * fh + (f ? 0.15 : 0), along = side === 's' || side === 'n' ? b.x0 + a : b.z0 + a;
        const t = { s: [along, b.z1 + 0.18, 0], n: [along, b.z0 - 0.18, Math.PI], e: [b.x1 + 0.18, along, -Math.PI / 2], w: [b.x0 - 0.18, along, Math.PI / 2] }[side];
        acs.push({ x: t[0], y, z: t[1], ry: t[2] });
        if (!f) box(t[0] - 0.45, t[0] + 0.45, t[1] - 0.45, t[1] + 0.45, 1, 'ac');
      }
    }
  }
  const acMat = kit.memo('town:ac', () => new THREE.MeshStandardMaterial({ roughness: 0.5, map: kit.texture(kit.canvas(128, 96, (g, w, h) => {
    g.fillStyle = '#e4e3de'; g.fillRect(0, 0, w, h); g.fillStyle = '#9da09f'; g.beginPath(); g.arc(w * 0.38, h / 2, h * 0.38, 0, 7); g.fill();
    g.strokeStyle = '#6a6d6c'; g.lineWidth = 2; for (let i = 0; i < 7; i++) { g.beginPath(); g.arc(w * 0.38, h / 2, h * 0.06 * i, 0, 7); g.stroke(); }
    g.fillStyle = '#c9c8c2'; for (let y = 8; y < h - 8; y += 6) g.fillRect(w * 0.74, y, w * 0.2, 2);
  })) }));
  group.add(kit.instanced(mergeAll([new THREE.BoxGeometry(0.85, 0.62, 0.3).translate(0, 0.31, 0), new THREE.BoxGeometry(0.04, 0.04, 0.4).translate(-0.35, 0, -0.05), new THREE.BoxGeometry(0.04, 0.04, 0.4).translate(0.35, 0, -0.05)]), acMat, acs));

  // ---- 현수막 between street trees (cloth sways a little) ----
  const BANNERS = [
    [['연무동 가을 문화축제', '10월 31일(토) 연무공원 · 주민 누구나'], '#fff6e0', '#a33a1e', 12, 6.9],
    [['연무시장 김장철 큰 장터', '11월 첫째 주말 · 시장길'], '#1f5fa8', '#ffffff', -30, -6.9],
    [['불법 주정차 집중 단속 구역', '연무동 행정복지센터'], '#ffffff', '#c42b25', 66, 6.9],
    [['한빛아파트 가을 대청소', '10월 25일(일) 오전 10시'], '#2f7a4a', '#ffffff', 100, 6.9],
  ];
  const trees = avoid.filter(t => t.type === 'ginkgo');
  for (const [text, bg, fg, want, z] of BANNERS) {
    const row = trees.filter(t => Math.abs(t.z - z) < 0.3).sort((a, b) => a.x - b.x);
    let pair = null;
    for (let i = 1; i < row.length; i++) if (row[i].x - row[i - 1].x < 10.5 && (!pair || Math.abs((row[i].x + row[i - 1].x) / 2 - want) < Math.abs((pair[0] + pair[1]) / 2 - want))) pair = [row[i - 1].x, row[i].x];
    const [a, b] = pair || [want - 4.5, want + 4.5], x = (a + b) / 2, zz = z + Math.sign(z) * 0.2, ry = z > 0 ? Math.PI : 0;
    const m = kit.sign(text, { w: 5.2, h: 0.9, bg, fg, single: true });
    const backMat = new THREE.MeshStandardMaterial({ color: bg, roughness: 0.9 });
    const backing = new THREE.Mesh(new THREE.PlaneGeometry(5.2, 0.9), backMat); backing.rotation.y = Math.PI;
    for (const mm of [m.material, backMat]) sway(mm, { amp: 0.05, freq: 2.3, ref: 0.5 });
    const cloth = new THREE.Group(); cloth.add(m, backing); cloth.position.set(x, Y + 2.75, zz); cloth.rotation.y = ry; group.add(cloth);
    m.castShadow = true; m.userData.keep = backing.userData.keep = true;
    for (const [from, to] of [[a + 0.2, x - 2.6], [x + 2.6, b - 0.2]]) for (const dy of [0.4, -0.4]) { const rope = new THREE.Mesh(new THREE.CylinderGeometry(0.007, 0.007, to - from, 4).rotateZ(Math.PI / 2), dark); rope.position.set((from + to) / 2, Y + 2.75 + dy, zz); stat.add(rope); }
    if (!pair) for (const px of [a, b]) { stat.add(kit.cyl(0.05, 0.05, 3.3, steel, { x: px, y: Y, z: zz }, 8)); box(px - 0.1, px + 0.1, zz - 0.1, zz + 0.1, 3, 'bannerPost'); }
  }

  // ---- vending machines (light group 'shops') ----
  const vend = kit.memo('town:vending', () => {
    const map = kit.texture(kit.canvas(256, 512, (g, w, h) => {
      g.fillStyle = '#d8262e'; g.fillRect(0, 0, w, h); g.fillStyle = '#141414'; g.fillRect(12, 40, w - 24, h * 0.58);
      const cols = ['#2f7dd1', '#e94e3c', '#f2c230', '#3ca55c', '#f08a2c', '#8a5bd1', '#e0e0e0'];
      for (let r = 0; r < 5; r++) for (let c = 0; c < 6; c++) { const x = 22 + c * 37, y = 52 + r * 58; g.fillStyle = cols[(r * 3 + c) % cols.length]; g.fillRect(x, y, 24, 40); g.fillStyle = 'rgba(255,255,255,0.5)'; g.fillRect(x + 4, y + 4, 4, 30); g.fillStyle = '#fff'; g.font = '700 11px sans-serif'; g.fillText(`${(r + 1) * 200 + 700}`, x - 2, y + 52); }
      g.fillStyle = '#ffffff'; g.font = `800 26px sans-serif`; g.textAlign = 'center'; g.fillText('시원한 음료', w / 2, 30);
      g.fillStyle = '#222'; g.fillRect(w * 0.6, h * 0.72, w * 0.3, h * 0.12); g.fillStyle = '#111'; g.fillRect(30, h * 0.88, w - 60, h * 0.07);
    }));
    return kit.lit('shops', new THREE.MeshStandardMaterial({ map, emissiveMap: map, emissive: 0xffffff, emissiveIntensity: 0.9, roughness: 0.35 }), 0.9);
  });
  for (const [x, z, ry] of [[-60.6, 37.6, -Math.PI / 2], [43.4, 15.2, -Math.PI / 2], [7.2, 11.3, 0]]) {
    stat.add(kit.box(0.95, 1.83, 0.72, kit.mat('paint:#b82028', { roughness: 0.4 }), { x, y: Y, z, ry }));
    const face = new THREE.Mesh(new THREE.PlaneGeometry(0.9, 1.78), vend); face.position.set(x - Math.sin(ry) * 0.365, Y + 0.93, z - Math.cos(ry) * 0.365); face.rotation.y = ry + Math.PI; stat.add(face);
    const hx = Math.abs(Math.sin(ry)) * 0.36 + Math.abs(Math.cos(ry)) * 0.48, hz = Math.abs(Math.cos(ry)) * 0.36 + Math.abs(Math.sin(ry)) * 0.48;
    box(x - hx, x + hx, z - hz, z + hz, 2, 'vending');
  }

  // ---- 분리수거장 (recycling shed) by 103동 ----
  const shx = 117.5, shz = 43.6;
  stat.add(kit.box(5.4, 0.08, 2.2, kit.mat('paint:#4f7a5a', { roughness: 0.5, metalness: 0.4 }), { x: shx, y: Y + 2.3, z: shz }));
  for (const dx of [-2.6, 2.6]) for (const dz of [-1, 1]) stat.add(kit.box(0.08, 2.3, 0.08, steel, { x: shx + dx, y: Y, z: shz + dz }));
  stat.add(kit.box(5.3, 1.6, 0.04, kit.mat('paint:#d8d4ca', { roughness: 0.8 }), { x: shx, y: Y, z: shz - 1.02 }));
  const bins = [['종이', '#2f6fb5'], ['캔·고철', '#e0a52a'], ['플라스틱', '#e06a2a'], ['유리병', '#3a8a4e'], ['비닐', '#8a8f94']];
  bins.forEach(([, c], i) => stat.add(kit.rbox(0.9, 1.05, 0.8, 0.05, kit.mat(`paint:${c}`, { roughness: 0.5 }), { x: shx - 2.1 + i * 1.05, y: Y, z: shz - 0.4 })));
  const tags = kit.sign([bins.map(([l]) => l).join('     ')], { w: 5.2, h: 0.24, bg: '#ffffff', fg: '#2f3a44', single: true }); tags.position.set(shx, Y + 1.32, shz - 0.99); stat.add(tags);
  const shedSign = kit.sign(['분리수거장', '수요일 · 일요일 배출'], { w: 1.6, h: 0.45, bg: '#2f5a3e', single: true }); shedSign.position.set(shx, Y + 1.95, shz - 0.99); stat.add(shedSign);
  box(shx - 2.7, shx + 2.7, shz - 1.1, shz + 0.05, 2.4, 'recycling');

  kit.merge(stat);
  return { group, solids, lights };
}
