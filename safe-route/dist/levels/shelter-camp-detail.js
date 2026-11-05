// Detail for the shelter camp (shelter-camp.js), by graphics tier (soft-goods.js detailOf; '완전 낮음' keeps the
// plain boxes): what evacuees brought as soft shapes (backpacks and 보스턴백 with straps and handles, folded
// blankets, puffed pillows, sneakers on soles), phones, cup noodles, opened water cartons and a crumpled blanket on
// the mats, cardboard partitions between back-to-back rows (one solid each, the walk gaps left open), the phone
// charging corner at the pillar by the south bays (cable reel, power strips, cables, a hand-written note) and the
// parked cars as the town's extruded sedans (town-traffic.js profile: instanced parts tinted per car) with a pleated
// cloth cover over the covered one. Every random choice here uses its own rng: the camp's seats and mats stay put.
import * as THREE from 'three';
import { mergeVertices } from 'three/addons/utils/BufferGeometryUtils.js';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { FONT } from '../engine/kit.js';
import { detailOf, join, pillowGeo, foldedGeo, drapeGeo, backpackGeo, duffelGeo, shoeGeo, soleGeo } from './soft-goods.js';

// Geometry for the instanced belongings (base on y = 0). lv 0: the original boxes, no extras.
export function goodsGeo(kit) {
  const lv = detailOf(kit.Q), r = kit.rng(4242), B = (w, h, d) => new THREE.BoxGeometry(w, h, d).translate(0, h / 2, 0);
  if (!lv) return { lv, bag: B(0.32, 0.42, 0.2), blanket: B(0.62, 0.1, 0.42), pillow: B(0.42, 0.1, 0.28), shoe: B(0.1, 0.08, 0.27) };
  return { lv, bag: backpackGeo(lv), duffel: duffelGeo(lv), blanket: foldedGeo(0.62, 0.1, 0.42, r, lv > 1 ? 3 : 2), pillow: pillowGeo(0.42, 0.11, 0.28, lv),
    shoe: shoeGeo(), sole: soleGeo(0.022, 0.28) };
}

// Partition label atlas (2 x 2 cells: three hand-written family notes and one plain cell) on corrugated board.
function boardTex(kit) {
  return kit.memo('sh:boardTex', () => kit.texture(kit.canvas(512, 256, (g) => {
    const r = kit.rng(77);
    g.fillStyle = '#b8935f'; g.fillRect(0, 0, 512, 256);
    for (let x = 0; x < 512; x += 5) { g.fillStyle = `rgba(80,52,24,${0.05 + r() * 0.05})`; g.fillRect(x, 0, 2, 256); }
    for (let i = 0; i < 18; i++) { g.fillStyle = `rgba(60,40,20,${0.04 + r() * 0.06})`; g.fillRect(r() * 512, r() * 256, 10 + r() * 60, 2 + r() * 5); }
    g.fillStyle = 'rgba(214,200,160,0.85)'; for (const [x, y] of [[20, 14], [276, 14], [20, 142], [370, 150]]) g.fillRect(x, y, 70, 16); // packing tape
    g.fillStyle = '#26262a'; g.textAlign = 'center';
    [['3동 박씨네', '4명 · 할머니 포함'], ['어르신 계세요', '조용히 부탁드려요'], ['202호', '물 받음 ✓']].forEach(([a, b], i) => {
      const cx = (i % 2) * 256 + 128, cy = (i >> 1) * 128;
      g.save(); g.translate(cx, cy + 64); g.rotate((r() - 0.5) * 0.08);
      g.font = `700 30px ${FONT}`; g.fillText(a, 0, -4); g.font = `500 22px ${FONT}`; g.fillText(b, 0, 28); g.restore();
    });
  }), { repeat: [1, 1] }));
}
const boardMat = (kit) => kit.memo('sh:boardMat', () => new THREE.MeshStandardMaterial({ map: boardTex(kit), roughness: 0.92 }));
const cellUV = (g, cell, backCell = 3) => { // BoxGeometry faces: +x -x +y -y +z -z, four vertices each
  const uv = g.attributes.uv;
  for (let i = 0; i < uv.count; i++) { const c = i >= 20 ? backCell : i >= 16 ? cell : 3; uv.setXY(i, (c % 2) * 0.5 + uv.getX(i) * 0.5, 0.5 - (c >> 1) * 0.5 + uv.getY(i) * 0.5); }
  return g;
};

// Everything on and between the mats past the instanced belongings. at: { inst, add, M, mats, seats, spot,
// rowPairs: [[z, z]] back-to-back rows, blanketCols, cartonMat }. Returns the partitions' solids.
export function dressMats(kit, at) {
  const lv = detailOf(kit.Q); if (!lv) return [];
  const { inst, add, M, mats, seats, spot, rowPairs, blanketCols } = at, r = kit.rng(5150), solids = [];
  const edgeOf = (z) => (z < 0 ? (z < -2.5 ? -1 : 1) : (z > 2.5 ? 1 : -1)); // as the camp: the mat side toward its walkway
  // ---- cardboard partitions between back-to-back rows (gaps between mats stay open for the walk columns) ----
  const board = boardMat(kit), parts = [];
  for (const [za, zb] of rowPairs) {
    const zp = (za + zb) / 2, near = mats.filter((m) => m.z === za || m.z === zb);
    for (const x of [...new Set(near.map((m) => m.x))]) {
      const pair = near.filter((m) => m.x === x), used = pair.some((m) => m.used);
      if (!used || r() > 0.55 || pair.some((m) => m.spot) || (Math.abs(x - spot.x) < 2.5 && Math.abs(zp - spot.z) < 2.5)) continue;
      const w = 1.5, h = 0.9, ry = (r() - 0.5) * 0.04, cell = r() < 0.35 ? (r() * 3) | 0 : 3;
      const g = cellUV(new THREE.BoxGeometry(w, h, 0.012), cell, r() < 0.2 ? (r() * 3) | 0 : 3).translate(0, h / 2, 0);
      const feet = [-0.55, 0.55].map((dx) => new THREE.BoxGeometry(0.012, 0.22, 0.24).translate(dx, 0.11, 0));
      parts.push(join([g, ...feet.map((f) => cellUV(f, 3))]).rotateY(ry).translate(x + (r() - 0.5) * 0.06, 0, zp));
      solids.push({ x0: x - 0.77, x1: x + 0.77, z0: zp - 0.13, z1: zp + 0.13, y0: -1, y1: h, noVault: true, noClimb: true });
    }
  }
  if (parts.length) { const m = add(new THREE.Mesh(join(parts), board)); m.castShadow = m.receiveShadow = true; }
  // ---- small things beside the people: phones, cup noodles / paper cups, an opened water carton, a loose blanket ----
  const phones = [], cups = [], cartons = [], loose = [], M4 = (x, y, z, ry, s = 1) => new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), ry), new THREE.Vector3(s, s, s));
  const caseCols = ['#1d1f24', '#e9e6e0', '#d89aa5', '#2d3a55', '#6f7f6a'].map((c) => new THREE.Color(c));
  for (const s of seats) {
    const m = mats[s.mat], e = edgeOf(m.z), side = s.x < m.x ? -1 : 1;
    if (r() < 0.4) phones.push({ m: M4(s.x + side * 0.27, 0.014, s.z + e * (0.12 + r() * 0.2), r() * 6.28), c: caseCols[(r() * caseCols.length) | 0] });
    if (r() < 0.22) cups.push({ m: M4(s.x - side * 0.26, 0.012, s.z + e * (0.25 + r() * 0.15), r() * 6.28, r() < 0.4 ? 0.72 : 1) });
  }
  mats.forEach((m) => {
    if (!m.used) return;
    const e = edgeOf(m.z);
    if (r() < 0.3) cartons.push({ m: M4(m.x + (r() < 0.5 ? -0.74 : 0.74), 0.012, m.z + e * 0.74, (r() - 0.5) * 0.3) });
    if (r() < 0.28) loose.push({ m: M4(m.x + (r() - 0.5) * 0.5, 0.012, m.z + e * (0.35 + r() * 0.2), (r() - 0.5) * 1.2), c: blanketCols[(r() * blanketCols.length) | 0] });
  });
  inst(new RoundedBoxGeometry(0.075, 0.009, 0.155, 1, 0.004), M.plastic, phones, false);
  const cupMat = kit.memo('sh:cupMat', () => new THREE.MeshStandardMaterial({ roughness: 0.6, map: kit.texture(kit.canvas(128, 64, (g) => {
    g.fillStyle = '#f2efe8'; g.fillRect(0, 0, 128, 64); g.fillStyle = '#c4261d'; g.fillRect(0, 18, 128, 22); g.fillStyle = '#f6d66b'; g.fillRect(0, 40, 128, 4);
    g.fillStyle = '#fff'; g.font = `800 15px ${FONT}`; g.textAlign = 'center'; g.fillText('얼큰한 컵', 64, 34);
  }), { repeat: [1, 1] }) }));
  inst(new THREE.LatheGeometry([[0, 0], [0.036, 0], [0.047, 0.095], [0.05, 0.1], [0, 0.1]].map(([x, y]) => new THREE.Vector2(x, y)), lv > 1 ? 14 : 8), cupMat, cups, false);
  const carton = [new THREE.BoxGeometry(0.28, 0.18, 0.21).translate(0, 0.09, 0)];
  for (const [dx, dz, rx, rz] of [[0, 0.105, 0.5, 0], [0, -0.105, -0.5, 0]]) carton.push(new THREE.BoxGeometry(0.28, 0.004, 0.1).translate(0, 0, dz > 0 ? 0.05 : -0.05).rotateX(rx).translate(dx, 0.18, dz));
  for (const [dx, rz] of [[0.14, -0.6], [-0.14, 0.6]]) carton.push(new THREE.BoxGeometry(0.08, 0.004, 0.2).translate(dx > 0 ? 0.04 : -0.04, 0, 0).rotateZ(rz).translate(dx, 0.18, 0));
  for (let i = 0; i < 6; i++) carton.push(new THREE.CylinderGeometry(0.014, 0.014, 0.02, 6).translate(-0.09 + (i % 3) * 0.09, 0.185, i < 3 ? -0.05 : 0.05));
  inst(join(carton), at.cartonMat, cartons);
  inst(drapeGeo({ w: 0.62, d: 0.5, top: 0.03, hang: [0.04, 0.04, 0.04, 0.04], puff: 0.025, amp: 0.012, seed: 5, nx: 10, nz: 8, out: 0.004 }), M.fabric, loose, false);
  chargeCorner(kit, add, M);
  return solids;
}

// The phone charging corner on the west side of pillar (-8.1, 10.5): a cable reel plugged into an outlet box on the
// pillar, two power strips on a flattened box, phones charging on it, a note taped above.
function chargeCorner(kit, add, M) {
  const px = -8.1 - 0.37, pz = 10.5, list = { white: [], dark: [], orange: [], steel: [], board: [] }, r = kit.rng(31);
  list.board.push(cellUV(new THREE.BoxGeometry(0.86, 0.006, 0.56), 3).translate(px - 0.5, 0.003, pz + 0.05));
  for (const dz of [-0.12, 0.14]) {
    list.white.push(new THREE.BoxGeometry(0.06, 0.035, 0.34).translate(px - 0.3, 0.023, pz + dz));
    list.orange.push(new THREE.BoxGeometry(0.03, 0.012, 0.03).translate(px - 0.3, 0.046, pz + dz + 0.15));
  }
  const cable = (pts, rr = 0.0035) => new THREE.TubeGeometry(new THREE.CatmullRomCurve3(pts.map(([x, y, z]) => new THREE.Vector3(x, y, z))), 12, rr, 4);
  for (let i = 0; i < 7; i++) {
    const strip = i < 4 ? -0.12 : 0.14, sz = pz + strip - 0.12 + (i % 4) * 0.075, x = px - 0.62 - r() * 0.15, z = pz - 0.18 + i * 0.065 + (r() - 0.5) * 0.02;
    list.dark.push(new RoundedBoxGeometry(0.075, 0.009, 0.155, 1, 0.004).rotateY(Math.PI / 2 + (r() - 0.5) * 0.4).translate(x, 0.011, z));
    list.white.push(cable([[px - 0.33, 0.03, sz], [px - 0.42, 0.01, sz + (r() - 0.5) * 0.1], [x + 0.12, 0.009, z + (r() - 0.5) * 0.05], [x + 0.07, 0.012, z]]));
  }
  // 전선릴: two flanges, the wound drum, a frame and handle, then the lead up to the outlet box
  const rx = px - 0.25, rz = pz - 0.62, rr = 0.15;
  for (const dx of [-0.075, 0.075]) list.orange.push(new THREE.CylinderGeometry(rr, rr, 0.014, 18).rotateZ(Math.PI / 2).translate(rx + dx, rr + 0.03, rz));
  list.orange.push(new THREE.CylinderGeometry(0.105, 0.105, 0.136, 18).rotateZ(Math.PI / 2).translate(rx, rr + 0.03, rz));
  for (const dx of [-0.09, 0.09]) { list.steel.push(new THREE.BoxGeometry(0.012, 0.36, 0.02).translate(rx + dx, 0.18, rz)); list.steel.push(new THREE.BoxGeometry(0.012, 0.012, 0.3).translate(rx + dx, 0.006, rz)); }
  list.steel.push(new THREE.CylinderGeometry(0.01, 0.01, 0.19, 6).rotateZ(Math.PI / 2).translate(rx, 0.36, rz));
  list.dark.push(cable([[rx, 0.12, rz + 0.12], [rx - 0.02, 0.006, rz + 0.3], [px - 0.36, 0.006, pz - 0.2], [px - 0.33, 0.02, pz - 0.14]], 0.006));
  list.dark.push(cable([[rx + 0.05, 0.2, rz + 0.1], [px - 0.04, 0.01, rz + 0.2], [px - 0.012, 0.15, pz - 0.2], [px - 0.012, 0.36, pz - 0.15]], 0.006));
  list.steel.push(new THREE.BoxGeometry(0.03, 0.1, 0.08).translate(px - 0.005, 0.4, pz - 0.15));
  const mats = { white: M.plastic, dark: M.seat, orange: kit.mat('paint:#d8641e', { roughness: 0.5 }), steel: M.legs, board: boardMat(kit) };
  for (const [k, geos] of Object.entries(list)) { const m = add(new THREE.Mesh(join(geos), mats[k])); m.castShadow = k === 'orange' || k === 'steel'; m.receiveShadow = true; }
  const note = add(kit.sign(['휴대폰 충전', '1인 30분 · 순서대로'], { w: 0.34, h: 0.22, bg: '#f7f5ee', fg: '#1d1d1f', single: true }));
  note.position.set(px - 0.004, 0.78, pz + 0.05); note.rotation.set(0, -Math.PI / 2, (r() - 0.5) * 0.06);
}

// ---- parked cars: the town's sedan (town-traffic.js TYPES.sedan / vehicleGeo), as instanced parts ----
const SEDAN = { L: 4.7, W: 1.82, r: 0.32, axles: [0.95, 3.85],
  top: [[0.05, 0.32], [0, 0.55], [0.08, 0.72], [0.9, 0.86], [1.65, 0.95], [2.25, 1.4], [3.45, 1.42], [4.15, 1.0], [4.62, 0.95], [4.7, 0.62], [4.66, 0.32]],
  glass: [[1.62, 0.955], [2.24, 1.415], [2.32, 1.36], [3.4, 1.37], [3.46, 1.428], [4.17, 1.005], [4.1, 0.96], [1.7, 0.94]], pillar: 2.85, lights: [0.68, 0.86, -0.01, 4.69] };
function bodyShape(T) {
  const out = [...T.top], [af, ar] = T.axles, R = T.r + 0.06, by = T.top[0][1];
  const arch = (c) => { const p = []; for (let k = 0; k <= 10; k++) { const a = k / 10 * Math.PI; p.push([c + R * Math.cos(a), Math.max(by, T.r + R * Math.sin(a) * 0.95)]); } return p; };
  out.push(...arch(ar), ...arch(af));
  return new THREE.Shape(out.map(([s, y]) => new THREE.Vector2(s, y)));
}
function sedanGeo(lo) {
  const T = SEDAN, { L, W, r } = T, bev = 0.05, ws = lo ? 8 : 16, hs = lo ? 6 : 12, z = (s) => s - L / 2, hx = W / 2 - 0.13;
  const toCar = (g, depth) => g.translate(-L / 2, 0, -depth / 2).rotateY(-Math.PI / 2);
  const body = toCar(new THREE.ExtrudeGeometry(bodyShape(T), { depth: W - 2 * bev, bevelEnabled: true, bevelThickness: bev, bevelSize: bev, bevelSegments: lo ? 1 : 2, curveSegments: lo ? 2 : 4 }), W - 2 * bev);
  const pillar = new THREE.BoxGeometry(W + 0.02, 0.42, 0.09).translate(0, (T.glass[2][1] + T.glass[0][1]) / 2, T.pillar - L / 2);
  const glass = toCar(new THREE.ExtrudeGeometry(new THREE.Shape(T.glass.map(([s, y]) => new THREE.Vector2(s, y))), { depth: W + 0.012, bevelEnabled: false }), W + 0.012);
  const dark = [];
  for (const s of T.axles) for (const sx of [-1, 1]) {
    dark.push(new THREE.CylinderGeometry(r, r, 0.22, ws).rotateZ(Math.PI / 2).translate(sx * hx, r, z(s)));
    dark.push(new THREE.CylinderGeometry(r * 0.62, r * 0.62, 0.02, hs).rotateZ(Math.PI / 2).translate(sx * (hx + 0.115), r, z(s)));
  }
  dark.push(new THREE.BoxGeometry(W * 0.98, 0.16, 0.12).translate(0, T.top[0][1] + 0.06, z(0.04)), new THREE.BoxGeometry(W * 0.98, 0.16, 0.12).translate(0, T.top[0][1] + 0.06, z(L - 0.04)),
    new THREE.BoxGeometry(W * 0.5, 0.17, 0.04).translate(0, T.lights[0] - 0.12, z(0) - 0.01), new THREE.BoxGeometry(0.52, 0.11, 0.02).translate(0, T.top[0][1] + 0.2, z(L) + 0.07),
    ...[-1, 1].map((sx) => new THREE.BoxGeometry(0.1, 0.09, 0.16).translate(sx * (W / 2 + 0.07), T.glass[0][1] + 0.06, z(T.glass[0][0] + 0.15))));
  const lamp = (s, y, w, d, u) => [-1, 1].map((sx) => { const g = new THREE.BoxGeometry(w, 0.12, d).translate(sx * (W / 2 - 0.22 - w / 2 + 0.1), y, z(s)), uv = g.attributes.uv; for (let k = 0; k < uv.count; k++) uv.setXY(k, u, 0.5); return g; });
  return { body: join([body, pillar]), glass, dark: join(dark), lamp: join([...lamp(T.lights[2], T.lights[0], 0.36, 0.07, 0.25), ...lamp(T.lights[3], T.lights[1], 0.32, 0.06, 0.75)]) };
}
// A car cover: the sedan's body puffed out along its normals with pleats, the hem hanging to 12 cm over the wheels.
function coverGeo(lo) {
  const T = SEDAN, g0 = new THREE.ExtrudeGeometry(bodyShape(T), { depth: T.W - 0.1, bevelEnabled: true, bevelThickness: 0.05, bevelSize: 0.05, bevelSegments: lo ? 1 : 2, curveSegments: lo ? 2 : 4 })
    .translate(-T.L / 2, 0, -(T.W - 0.1) / 2).rotateY(-Math.PI / 2);
  g0.deleteAttribute('normal'); g0.deleteAttribute('uv');
  const g = mergeVertices(g0, 1e-3); g.computeVertexNormals();
  const p = g.attributes.position, n = g.attributes.normal;
  for (let i = 0; i < p.count; i++) {
    let x = p.getX(i), y = p.getY(i), z = p.getZ(i);
    const k = 0.05 + 0.022 * Math.sin(z * 5.3 + x * 2.1) * Math.sin(y * 6.1 + z * 1.7) + 0.012 * Math.sin(z * 13 + y * 4);
    x += n.getX(i) * k; y += n.getY(i) * k; z += n.getZ(i) * k;
    if (y < 0.7) { y = 0.12 + 0.02 * Math.sin(z * 9); x *= 1.03; z *= 1.01; }
    p.setXYZ(i, x, y, z);
  }
  g.computeVertexNormals();
  const uv = new Float32Array(p.count * 2); g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
  return g;
}
// cars: [[x, z, ry, color, covered]]; M: the camp materials (glass, tyre, rim). Adds instanced parts and the cover.
export function carPark(kit, add, M, cars, cover, plate) {
  const lo = kit.Q?.physicalPaint === false, geo = sedanGeo(lo), open = cars.filter((c) => !c[4]);
  const paint = lo ? kit.memo('sh:carPaintLo', () => new THREE.MeshStandardMaterial({ color: 0xffffff, metalness: 0.5, roughness: 0.34 }))
    : kit.memo('sh:carPaint', () => new THREE.MeshPhysicalMaterial({ color: 0xffffff, metalness: 0.55, roughness: 0.32, clearcoat: 1, clearcoatRoughness: 0.08 }));
  const lampTex = kit.memo('sh:carLampTex', () => { const t = kit.texture(kit.canvas(2, 1, (g) => { g.fillStyle = '#e9e4d8'; g.fillRect(0, 0, 1, 1); g.fillStyle = '#7c1616'; g.fillRect(1, 0, 1, 1); }), { repeat: [1, 1] }); t.magFilter = t.minFilter = THREE.NearestFilter; t.generateMipmaps = false; return t; });
  const mats = { body: paint, glass: M.glass, dark: kit.mat('paint:#141517', { roughness: 0.7 }), lamp: kit.memo('sh:carLamp', () => new THREE.MeshStandardMaterial({ map: lampTex, roughness: 0.25 })) };
  const m = new THREE.Matrix4(), q = new THREE.Quaternion(), up = new THREE.Vector3(0, 1, 0), c = new THREE.Color();
  for (const part of ['body', 'glass', 'dark', 'lamp']) {
    const im = new THREE.InstancedMesh(geo[part], mats[part], open.length); im.name = `shelter:car:${part}`;
    open.forEach(([x, z, ry, col], i) => { im.setMatrixAt(i, m.compose(new THREE.Vector3(x, 0, z), q.setFromAxisAngle(up, ry), new THREE.Vector3(1, 1, 1))); if (part === 'body') im.setColorAt(i, c.set(col)); });
    im.castShadow = part === 'body'; im.receiveShadow = part !== 'lamp'; im.instanceMatrix.needsUpdate = true; if (im.instanceColor) im.instanceColor.needsUpdate = true;
    im.computeBoundingSphere(); add(im);
  }
  for (const [x, z, ry, , cov] of cars) {
    const g = new THREE.Group(); g.position.set(x, 0, z); g.rotation.y = ry; add(g);
    if (cov) {
      const cv = new THREE.Mesh(coverGeo(lo), cover); cv.castShadow = cv.receiveShadow = true; g.add(cv);
      for (const sx of [-0.78, 0.78]) for (const sz of [-1.4, 1.5]) g.add(kit.cyl(0.32, 0.32, 0.2, M.tyre, { x: sx, y: 0.22, z: sz, rz: Math.PI / 2 }, lo ? 8 : 14));
      continue;
    }
    for (const [pz, py] of [[-2.43, 0.45], [2.445, 0.52]]) { const pl = plate.clone(); pl.position.set(0, py, pz); pl.rotation.y = pz < 0 ? Math.PI : 0; g.add(pl); }
  }
}
