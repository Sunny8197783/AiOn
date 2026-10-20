// Town ground: roads with markings, curbs, 보도블록 sidewalks, tactile paving at crossings, lots, lawns.
// Block tops sit at curb height (0.12) and are carved around the shelter pits so ramps can go below.
import * as THREE from 'three';
import { ROADS, CROSSWALKS, BOUNDS } from '../game/townmap.js';
import { BLOCKS, CURB, LANES } from './town-plan.js';
import { carve, flat, flatGeo } from './town-util.js';

// Interior surfaces [x0, x1, z0, z1, surface]. They must not overlap each other.
const OVERLAYS = [
  [-36, 4, -22, -12, 'asphalt'], [4.2, 41.8, -51.8, -10.2, 'asphalt'], [58, 130, -60, -10, 'asphalt'],
  [12.2, 41.8, 22.2, 51.8, 'asphalt'], [-42, 12, 12, 52, 'grass'], [-126, -58.2, 47, 59.5, 'asphalt'],
  [58.2, 130, 13, 24.5, 'asphalt'], [58.2, 130, 45, 52, 'grass'], [60, 118, 52, 57, 'asphalt'], [58.2, 130, 73, 80, 'grass'],
  [60, 118, 80, 85, 'asphalt'], [58.2, 130, 101, 110, 'grass'], [58.2, 64, 30, 42, 'grass'], [112, 116, 30, 42, 'grass'],
  [-42, 42, -110, -68, 'concrete'], [-42, 42, 68, 110, 'concrete'], [-130, -58.2, -110, -56.5, 'concrete'],
  [-130, -58.2, 60, 110, 'concrete'], [-130, -112, 16.7, 45.8, 'grass'], [58.2, 130, -110, -60, 'concrete'],
];

export function buildGround(kit, { holes = [] } = {}) {
  const group = new THREE.Group(); group.name = 'ground';
  const stat = new THREE.Group(); group.add(stat);
  const S = (name, o) => kit.surface(name, o);
  const autumnGrass = S('grass', { color: 0xe6dcb4 });
  const matOf = { asphalt: S('asphalt'), concrete: S('concrete'), grass: autumnGrass, sidewalk: S('sidewalk') };
  const walk = [];

  // ---- far ground and road extensions (outside the playable bounds) ----
  const outer = flat({ x0: -900, x1: 900, z0: -900, z1: 900 }, -0.06, S('concrete', { color: 0x8c8a84 }));
  group.add(outer);
  const ext = 360;
  const roadRects = [
    { x0: -ext, x1: ext, z0: -6, z1: 6 },
    { x0: -54, x1: -46, z0: -ext, z1: -6 }, { x0: -54, x1: -46, z0: 6, z1: ext },
    { x0: 46, x1: 54, z0: -ext, z1: -6 }, { x0: 46, x1: 54, z0: 6, z1: ext },
    { x0: -46, x1: 46, z0: -64, z1: -56 }, { x0: -46, x1: 46, z0: 56, z1: 64 },
  ];
  for (const r of roadRects) {
    stat.add(flat(r, 0, matOf.asphalt));
    walk.push({ x0: Math.max(r.x0, BOUNDS.x0), x1: Math.min(r.x1, BOUNDS.x1), z0: Math.max(r.z0, BOUNDS.z0), z1: Math.min(r.z1, BOUNDS.z1), y: 0 });
  }
  // A strip of pavement either side of the far roads so they do not float on bare ground.
  for (const r of [{ x0: -ext, x1: BOUNDS.x0 }, { x0: BOUNDS.x1, x1: ext }]) {
    stat.add(flat({ ...r, z0: -10, z1: -6 }, CURB * 0.5, matOf.sidewalk), flat({ ...r, z0: 6, z1: 10 }, CURB * 0.5, matOf.sidewalk));
  }
  for (const xs of [[-58, -54], [-46, -42], [42, 46], [54, 58]]) for (const zs of [[-ext, BOUNDS.z0], [BOUNDS.z1, ext]]) {
    stat.add(flat({ x0: xs[0], x1: xs[1], z0: zs[0], z1: zs[1] }, CURB * 0.5, matOf.sidewalk));
  }

  // ---- block tops: sidewalk pavers everywhere, interior overlays on top (no overlap), pits carved ----
  const overlays = OVERLAYS.map(([x0, x1, z0, z1, s]) => ({ x0, x1, z0, z1, s }));
  for (const b of BLOCKS) {
    for (const r of carve({ ...b }, [...holes, ...overlays])) stat.add(flat(r, CURB, matOf.sidewalk));
    for (const r of carve({ ...b, y: CURB }, holes)) walk.push(r);
  }
  for (const o of overlays) for (const r of carve(o, holes)) stat.add(flat(r, CURB, matOf[o.s]));

  // ---- curbs (granite, 15 cm) along every block edge that faces a road ----
  const curbMat = kit.mat('curb');
  for (const b of BLOCKS) {
    const edges = [];
    if (b.x0 > BOUNDS.x0) edges.push([b.x0 + 0.075, (b.z0 + b.z1) / 2, 0.15, b.z1 - b.z0]);
    if (b.x1 < BOUNDS.x1) edges.push([b.x1 - 0.075, (b.z0 + b.z1) / 2, 0.15, b.z1 - b.z0]);
    if (b.z0 > BOUNDS.z0) edges.push([(b.x0 + b.x1) / 2, b.z0 + 0.075, b.x1 - b.x0, 0.15]);
    if (b.z1 < BOUNDS.z1) edges.push([(b.x0 + b.x1) / 2, b.z1 - 0.075, b.x1 - b.x0, 0.15]);
    for (const [x, z, w, d] of edges) stat.add(kit.box(w, CURB + 0.03, d, curbMat, { x, z, y: -0.02, cast: false }));
  }

  // ---- markings: one instanced quad mesh per paint colour ----
  const white = [], yellow = [];
  const quad = (list, x0, x1, z0, z1) => list.push({ x: (x0 + x1) / 2, y: 0.008, z: (z0 + z1) / 2, sx: x1 - x0, sz: z1 - z0 });
  // Intervals along a road minus gaps -> callback per kept interval.
  const spans = (a, b, gaps, fn) => {
    let cur = a;
    for (const [g0, g1] of [...gaps].sort((p, q) => p[0] - q[0])) { if (g0 > cur) fn(cur, Math.min(g0, b)); cur = Math.max(cur, g1); }
    if (cur < b) fn(cur, b);
  };
  const cw = (road) => CROSSWALKS.filter(c => c.road === road).map(c => [c.at - 2.4, c.at + 2.4]);
  // 연무로: double yellow centre, dashed white lane lines, interrupted at crossings and junctions.
  const yGaps = [...cw('yeonmu'), [-54.5, -45.5], [45.5, 54.5]];
  spans(-ext, ext, yGaps, (a, b) => {
    quad(yellow, a, b, -0.27, -0.12); quad(yellow, a, b, 0.12, 0.27);
    for (const z of [-3, 3]) for (let x = a + 1; x + 3 < b; x += 8) quad(white, x, x + 3, z - 0.07, z + 0.07);
  });
  for (const [id, cx] of [['munhwa', -50], ['hanbit', 50]]) {
    const gaps = [...cw(id), [-6.5, 6.5], [-64.5, -55.5], [55.5, 64.5]];
    spans(-ext, ext, gaps, (a, b) => { quad(yellow, cx - 0.27, cx - 0.12, a, b); quad(yellow, cx + 0.12, cx + 0.27, a, b); });
  }
  for (const [id, cz] of [['sijang', -60], ['danji', 60]]) spans(-46, 46, cw(id), (a, b) => quad(yellow, a, b, cz - 0.08, cz + 0.08));
  // Zebra crossings (bars parallel to traffic, 50 cm, 50 cm apart) and tactile paving at both ends.
  const tactile = [];
  for (const c of CROSSWALKS) {
    const r = ROADS.find(q => q.id === c.road);
    if (r.z1 - r.z0 < r.x1 - r.x0) { // east-west road
      for (let z = r.z0 + 0.45; z + 0.5 <= r.z1 - 0.2; z += 1) quad(white, c.at - 2, c.at + 2, z, z + 0.5);
      tactile.push([c.at - 2, c.at + 2, r.z0 - 0.75, r.z0 - 0.15], [c.at - 2, c.at + 2, r.z1 + 0.15, r.z1 + 0.75]);
    } else {
      for (let x = r.x0 + 0.45; x + 0.5 <= r.x1 - 0.2; x += 1) quad(white, x, x + 0.5, c.at - 2, c.at + 2);
      tactile.push([r.x0 - 0.75, r.x0 - 0.15, c.at - 2, c.at + 2], [r.x1 + 0.15, r.x1 + 0.75, c.at - 2, c.at + 2]);
    }
  }
  // Stop lines just ahead of where cars wait, across the lane's half of the road.
  const arrows = [];
  for (const L of LANES) for (const s of L.stops) {
    const at = s + L.dir * 0.4;
    if (L.axis === 'x') quad(white, at - 0.22, at + 0.22, L.c - 1.4, L.c + 1.4);
    else quad(white, L.c - 1.4, L.c + 1.4, at - 0.22, at + 0.22);
    const back = s - L.dir * 14;
    arrows.push(L.axis === 'x' ? { x: back, y: 0.009, z: L.c, ry: L.dir > 0 ? -Math.PI / 2 : Math.PI / 2 } : { x: L.c, y: 0.009, z: back, ry: L.dir > 0 ? Math.PI : 0 });
  }
  // Parking bays: mart lot, the walled public lot, the logistics yard, the culture centre rear lot, the complex.
  const bays = (x0, x1, zLine, depth, step = 2.5) => {
    for (let x = x0; x <= x1 + 1e-6; x += step) quad(white, x - 0.06, x + 0.06, Math.min(zLine, zLine + depth), Math.max(zLine, zLine + depth));
    quad(white, x0, x1, zLine - 0.06, zLine + 0.06);
  };
  bays(-35, 3, -22, 5); bays(6, 40, -51.5, 5); bays(6, 40, -10.5, -5); bays(6, 40, -31, 5); bays(6, 40, -31, -5);
  bays(74.5, 82, -10.5, -5.5); bays(92, 126, -10.5, -5.5); bays(108, 126, -60, 6, 3.2); bays(-124, -90, 59.5, -5); bays(60, 126, 13.5, 5);
  bays(62, 116, 57, -5); bays(62, 116, 85, -5); bays(14, 40, 51.5, -5); bays(14, 40, 22.5, 5);
  const plane = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2);
  const paint = (hex) => new THREE.MeshStandardMaterial({ color: hex, roughness: 0.62, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
  group.add(kit.instanced(plane, paint(0xe9e8e1), white, { cast: false }));
  group.add(kit.instanced(plane, paint(0xe8b21e), yellow, { cast: false }));
  const arrowShape = new THREE.Shape([[-0.15, -2.4], [0.15, -2.4], [0.15, 0.2], [0.45, 0.2], [0, 1.4], [-0.45, 0.2], [-0.15, 0.2]].map(([x, y]) => new THREE.Vector2(x, y)));
  group.add(kit.instanced(new THREE.ShapeGeometry(arrowShape).rotateX(-Math.PI / 2), paint(0xe9e8e1), arrows, { cast: false }));

  // Tactile paving (점형블록): yellow with raised dots.
  const tac = kit.memo('town:tactile', () => {
    const dots = (g, w, fg) => { for (let y = 0; y < 6; y++) for (let x = 0; x < 6; x++) { g.fillStyle = fg; g.beginPath(); g.arc((x + 0.5) * w / 6, (y + 0.5) * w / 6, w / 22, 0, 7); g.fill(); } };
    const map = kit.texture(kit.canvas(128, 128, (g, w) => { g.fillStyle = '#e2a91c'; g.fillRect(0, 0, w, w); dots(g, w, '#f0bd33'); g.strokeStyle = 'rgba(0,0,0,0.25)'; g.strokeRect(0, 0, w, w); }), { repeat: [1 / 0.3, 1 / 0.3] });
    const nrm = kit.texture(kit.normalFrom(kit.canvas(128, 128, (g, w) => { g.fillStyle = '#404040'; g.fillRect(0, 0, w, w); dots(g, w, '#e0e0e0'); }), 3), { repeat: [1 / 0.3, 1 / 0.3], color: false });
    return new THREE.MeshStandardMaterial({ map, normalMap: nrm, roughness: 0.6, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1 });
  });
  for (const [x0, x1, z0, z1] of tactile) stat.add(flat({ x0, x1, z0, z1 }, CURB + 0.004, tac));

  // Manholes and drain grates on the roads.
  const holeMat = kit.memo('town:manhole', () => new THREE.MeshStandardMaterial({
    map: kit.texture(kit.canvas(128, 128, (g, w) => {
      g.fillStyle = '#2b2c2e'; g.fillRect(0, 0, w, w); g.strokeStyle = '#4a4b4e'; g.lineWidth = 5;
      g.beginPath(); g.arc(64, 64, 58, 0, 7); g.stroke();
      for (let i = 16; i < 112; i += 12) { g.beginPath(); g.moveTo(i, 18); g.lineTo(i, 110); g.stroke(); }
    })), roughness: 0.45, metalness: 0.7, polygonOffset: true, polygonOffsetFactor: -2,
  }));
  const holesList = [];
  for (let x = -122; x < 130; x += 31) holesList.push({ x, y: 0.01, z: x % 2 ? 3 : -3 });
  for (let z = -100; z < 110; z += 34) holesList.push({ x: -51.6, y: 0.01, z }, { x: 51.6, y: 0.01, z: z + 9 });
  group.add(kit.instanced(new THREE.CircleGeometry(0.36, 20).rotateX(-Math.PI / 2), holeMat, holesList, { cast: false }));
  const grate = kit.memo('town:grate', () => kit.mat('darkMetal', { roughness: 0.6 }));
  const grates = [];
  for (const [z, xs] of [[-5.6, [-120, -90, -30, 20, 80, 110]], [5.6, [-110, -75, -20, 30, 90, 120]]]) for (const x of xs) grates.push({ x, y: 0.012, z, sx: 0.9, sz: 0.35 });
  group.add(kit.instanced(plane, grate, grates, { cast: false }));

  kit.merge(stat);
  return { group, walk };
}

// Thin quad from p to q (world xz) of width w at height y: park paths, painted bands.
export function strip(p, q, w, y, material) {
  const dx = q[0] - p[0], dz = q[1] - p[1], len = Math.hypot(dx, dz);
  const g = flatGeo({ x0: -len / 2, x1: len / 2, z0: -w / 2, z1: w / 2 }, 0);
  const m = new THREE.Mesh(g, material);
  m.position.set((p[0] + q[0]) / 2, y, (p[1] + q[1]) / 2); m.rotation.y = -Math.atan2(dz, dx);
  m.receiveShadow = true;
  return m;
}
