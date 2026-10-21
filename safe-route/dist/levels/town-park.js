// Trees and 연무공원. Crowns are instanced leaf cards with outward (spherical) normals, ambient-occlusion
// vertex colours, autumn tints per instance and wind sway. Park: paths, playground, 정자, benches,
// outdoor exercise frames, park lamps and fallen leaves. treeSpots() is pure so props can avoid trees.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { CROSSWALKS } from '../game/townmap.js';
import { CURB } from './town-plan.js';
import { sway, yawTo } from './town-util.js';
import { strip } from './town-ground.js';

const Y = CURB;
const TYPES = {
  ginkgo: { n: 46, rx: 1.9, ry: 3.3, rz: 1.9, cy: 5.7, size: 1.5, trunk: 7.2, r0: 0.17, tex: 'fan', tints: ['#f4c531', '#eab526', '#f0cf47', '#d4c446', '#e2a82a', '#b9b84a'] },
  zelkova: { n: 72, rx: 4.1, ry: 2.7, rz: 4.1, cy: 6.4, size: 2.0, trunk: 6.6, r0: 0.25, tex: 'oval', tints: ['#c98b3c', '#b8732f', '#a39143', '#c9a64c', '#8f9a45', '#b0602c'] },
  maple: { n: 40, rx: 2.4, ry: 1.8, rz: 2.4, cy: 3.8, size: 1.2, trunk: 4.0, r0: 0.12, tex: 'oval', tints: ['#c8402c', '#d65a2a', '#a8302a', '#e2782c', '#b84a2e'] },
  shrub: { n: 20, rx: 0.85, ry: 0.5, rz: 0.85, cy: 0.5, size: 0.62, trunk: 0, r0: 0, tex: 'oval', tints: ['#4f6b35', '#5b7a3c', '#6a7f3e', '#56703a', '#8a3b2c'] },
};
export const PARK_PATH = [[0, 12], [0, 14], [-15, 22], [-15, 40], [-30, 44], [-38, 30.5], [-42, 30.5]];
const BRANCHES = [[[-15, 32], [-3.6, 37.2]], [[-15, 25.5], [-8.5, 24.5]]];
const PLAY = { x0: -37, x1: -23, z0: 14.5, z1: 26 }, PAV = { x: -1, z: 38, s: 4.8 }, GYM = { x0: -10, x1: -1, z0: 20.5, z1: 28.5 };

const segDist = (p, a, b) => {
  const dx = b[0] - a[0], dz = b[1] - a[1], t = Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dz) / (dx * dx + dz * dz)));
  return Math.hypot(p[0] - a[0] - dx * t, p[1] - a[1] - dz * t);
};
const nearPath = (p, d) => [PARK_PATH, ...BRANCHES].some(L => L.some((a, i) => i > 0 && segDist(p, L[i - 1], a) < d));
const inRect = (p, r, m = 0) => p[0] > r.x0 - m && p[0] < r.x1 + m && p[1] > r.z0 - m && p[1] < r.z1 + m;

// Every tree in town: [{ x, z, type, s, ry, tint }]. Street trees stand in pits 1.4 m behind the curb.
export function treeSpots(rand) {
  const out = [], add = (x, z, type) => out.push({ x, z, type, s: 0.85 + rand() * 0.3, ry: rand() * 6.28, tint: rand() });
  const near = (v, list, d) => list.some(a => Math.abs(v - a) < d);
  const cwY = CROSSWALKS.filter(c => c.road === 'yeonmu').map(c => c.at);
  for (const z of [-6.9, 6.9]) for (let x = -124; x <= 124; x += 9) {
    if (near(x, [...cwY, -50, 50], 5.5)) continue;
    if (z < 0 && ((x > -21 && x < -10) || (x > 60 && x < 74) || (x > 82 && x < 92) || (x > 7 && x < 17))) continue;
    if (z > 0 && near(x, [-86, -104, 36, 78, -94], 3.6)) continue;
    add(x, z, 'ginkgo');
  }
  for (const [x, skip] of [[-54.9, [[-52, -25], [24, 37], [43, 63]]], [-45.1, [[25, 36]]], [45.1, []], [54.9, [[13, 27]]]]) {
    for (let z = -104; z <= 104; z += 10) {
      if (Math.abs(z) < 18 || Math.abs(z + 60) < 7 || Math.abs(z - 60) < 7 || skip.some(([a, b]) => z > a && z < b)) continue;
      add(x, z, 'ginkgo');
    }
  }
  for (let x = -39; x <= 9; x += 6) for (let z = 15; z <= 49; z += 6) {
    const p = [x + (rand() - 0.5) * 3, z + (rand() - 0.5) * 3];
    if (rand() < 0.35 || nearPath(p, 2.8) || inRect(p, PLAY, 1.5) || inRect(p, GYM, 1) || Math.hypot(p[0] - PAV.x, p[1] - PAV.z) < 5.5) continue;
    if (p[0] < -40 || p[0] > 10.5 || p[1] < 14 || p[1] > 50.5) continue;
    add(p[0], p[1], rand() < 0.6 ? 'zelkova' : 'maple');
  }
  for (const z of [48.5, 77, 105]) for (let x = 64; x <= 124; x += 7.5) add(x + (rand() - 0.5), z, (x / 7.5 | 0) % 3 === 0 ? 'maple' : 'zelkova');
  for (let x = 63; x <= 125; x += 9) add(x, 11.6, 'zelkova');
  for (const [x, z] of [[61, 32], [61, 37], [61, 41], [114, 32], [114, 40], [-127, 20], [-122, 27], [-127, 34], [-118, 41], [-121, 44]]) add(x, z, 'zelkova');
  return out;
}

export function buildPark(kit, { spots } = {}) {
  const group = new THREE.Group(); group.name = 'park';
  const stat = new THREE.Group(); group.add(stat);
  const solids = [], walk = [];
  const rand = kit.rng(4242);
  spots ||= treeSpots(kit.rng(77));

  // ---- foliage materials ----
  const leafTex = (kind) => kit.memo(`town:leafTex:${kind}`, () => {
    const r = kit.rng(kind === 'fan' ? 11 : 12);
    return kit.texture(kit.canvas(256, 256, (g, w, h) => {
      g.strokeStyle = 'rgba(78,60,44,0.95)'; g.lineWidth = 2.2; g.lineCap = 'round';
      for (let i = 0; i < 7; i++) { g.beginPath(); g.moveTo(w / 2, h / 2); g.quadraticCurveTo(w / 2 + (r() - 0.5) * w * 0.5, h / 2 + (r() - 0.5) * h * 0.5, w / 2 + (r() - 0.5) * w * 0.85, h / 2 + (r() - 0.5) * h * 0.85); g.stroke(); }
      for (let i = 0; i < 120; i++) {
        const a = r() * 6.283, d = Math.sqrt(r()) * w * 0.43, s = 8 + r() * 9, v = 170 + (r() * 85 | 0);
        g.fillStyle = `rgb(${v},${v},${Math.max(0, v - (r() * 25 | 0))})`;
        g.save(); g.translate(w / 2 + Math.cos(a) * d, h / 2 + Math.sin(a) * d); g.rotate(r() * 6.283); g.beginPath();
        if (kind === 'fan') { g.moveTo(0, 0); g.arc(0, 0, s, -0.95, -0.1); g.lineTo(0, 0); g.arc(0, 0, s, 0.1, 0.95); g.closePath(); }
        else g.ellipse(s * 0.5, 0, s * 0.62, s * 0.3, 0, 0, 7);
        g.fill(); g.restore();
      }
    }), { repeat: [1, 1] });
  });
  // Outward normals must survive double-sided rendering: drop the back-face normal flip.
  const foliage = (kind, amp) => kit.memo(`town:foliage:${kind}:${amp}`, () => {
    const m = new THREE.MeshStandardMaterial({ map: leafTex(kind), alphaTest: 0.5, side: THREE.DoubleSide, roughness: 0.82, vertexColors: true });
    sway(m, { amp, freq: 1.6, ref: 0 });
    m.customProgramCacheKey = () => `foliage:${amp}`;
    const base = m.onBeforeCompile;
    m.onBeforeCompile = (sh, r) => { base(sh, r); sh.fragmentShader = sh.fragmentShader.replace('#include <normal_fragment_begin>', THREE.ShaderChunk.normal_fragment_begin.replace(/normal \*= faceDirection;/g, '')); };
    return m;
  });
  const bark = kit.memo('town:bark', () => {
    const r = kit.rng(5);
    const h = kit.canvas(64, 256, (g, w, hh) => { g.fillStyle = '#808080'; g.fillRect(0, 0, w, hh); for (let i = 0; i < 90; i++) { g.fillStyle = r() < 0.5 ? '#4a4a4a' : '#b0b0b0'; g.fillRect(r() * w, r() * hh, 1 + r() * 2, 8 + r() * 30); } });
    const c = kit.canvas(64, 256, (g, w, hh) => { g.fillStyle = '#5a4a3c'; g.fillRect(0, 0, w, hh); for (let i = 0; i < 90; i++) { g.fillStyle = r() < 0.5 ? 'rgba(30,24,18,0.5)' : 'rgba(140,128,112,0.35)'; g.fillRect(r() * w, r() * hh, 1 + r() * 2, 8 + r() * 30); } });
    return new THREE.MeshStandardMaterial({ map: kit.texture(c, { repeat: [2, 1] }), normalMap: kit.texture(kit.normalFrom(h, 3), { repeat: [2, 1], color: false }), roughness: 0.95 });
  });

  function crownGeo(T, r) {
    const pos = [], nor = [], uv = [], col = [], idx = [];
    const q = new THREE.Quaternion(), e = new THREE.Euler(), v = new THREE.Vector3(), d = new THREE.Vector3(), nn = new THREE.Vector3();
    for (let i = 0; i < T.n; i++) {
      d.set(r() * 2 - 1, r() * 2 - 1, r() * 2 - 1); if (d.lengthSq() < 1e-4) d.set(0, 1, 0); d.normalize();
      const t = 0.4 + 0.6 * Math.sqrt(r()), c = [d.x * T.rx * t, T.cy + d.y * T.ry * t, d.z * T.rz * t];
      const s = T.size * (0.7 + 0.6 * r());
      q.setFromEuler(e.set(r() * 6.28, r() * 6.28, r() * 6.28));
      const ao = Math.min(1, 0.42 + 0.4 * t + 0.25 * (d.y * 0.5 + 0.5));
      const base = pos.length / 3;
      for (const [a, b] of [[-0.5, -0.5], [0.5, -0.5], [0.5, 0.5], [-0.5, 0.5]]) {
        v.set(a * s, b * s, 0).applyQuaternion(q).add({ x: c[0], y: c[1], z: c[2] });
        pos.push(v.x, v.y, v.z);
        nn.set(v.x / (T.rx * T.rx), (v.y - T.cy) / (T.ry * T.ry) + 0.12, v.z / (T.rz * T.rz)).normalize();
        nor.push(nn.x, nn.y, nn.z); uv.push(a + 0.5, b + 0.5); col.push(ao, ao, ao);
      }
      idx.push(base, base + 1, base + 2, base, base + 2, base + 3);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setAttribute('normal', new THREE.Float32BufferAttribute(nor, 3));
    g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2)); g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3)); g.setIndex(idx);
    return g;
  }
  function trunkGeo(T, r) {
    const parts = [new THREE.CylinderGeometry(T.r0 * 0.55, T.r0, T.trunk, 7).translate(0, T.trunk / 2, 0)];
    for (let i = 0; i < 4; i++) {
      const len = Math.max(T.rx, T.ry) * (0.7 + r() * 0.3), b = new THREE.CylinderGeometry(T.r0 * 0.18, T.r0 * 0.38, len, 5).translate(0, len / 2, 0);
      b.rotateZ(0.55 + r() * 0.35).rotateY(i * 1.57 + r() * 0.6).translate(0, T.cy - T.ry * 0.55 + r() * 0.6, 0);
      parts.push(b);
    }
    return mergeGeometries(parts.map(p => p.toNonIndexed()));
  }
  const byType = {};
  for (const s of spots) (byType[s.type] ||= []).push(s);
  for (const [type, list] of Object.entries(byType)) {
    const T = TYPES[type], r = kit.rng(type.length * 31);
    const tf = list.map(s => ({ x: s.x, y: Y, z: s.z, ry: s.ry, s: s.s }));
    const crown = kit.instanced(crownGeo(T, r), foliage(T.tex, type === 'shrub' ? 0.02 : 0.011), tf, { cast: true });
    const c = new THREE.Color();
    list.forEach((s, i) => { crown.setColorAt(i, c.set(T.tints[Math.floor(s.tint * T.tints.length) % T.tints.length])); });
    crown.instanceColor.needsUpdate = true; crown.name = `crowns:${type}`;
    group.add(crown);
    if (T.trunk) {
      group.add(kit.instanced(trunkGeo(T, r), bark, tf));
      for (const s of list) solids.push({ x0: s.x - 0.3, x1: s.x + 0.3, z0: s.z - 0.3, z1: s.z + 0.3, y0: -0.5, y1: 3, id: 'tree' });
    }
  }
  // Tree-pit grates under street trees (gingkos on sidewalks).
  const grates = spots.filter(s => s.type === 'ginkgo').map(s => ({ x: s.x, y: Y + 0.006, z: s.z }));
  const gr = kit.memo('town:treeGrate', () => new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(128, 128, (g, w) => { g.fillStyle = '#3b3a37'; g.fillRect(0, 0, w, w); g.strokeStyle = '#1d1d1c'; g.lineWidth = 3; for (let i = 1; i < 6; i++) { g.beginPath(); g.arc(64, 64, i * 11, 0, 7); g.stroke(); } for (let a = 0; a < 12; a++) { g.beginPath(); g.moveTo(64, 64); g.lineTo(64 + Math.cos(a / 1.91) * 64, 64 + Math.sin(a / 1.91) * 64); g.stroke(); } g.fillStyle = '#2a2018'; g.beginPath(); g.arc(64, 64, 10, 0, 7); g.fill(); })), roughness: 0.6, metalness: 0.6, polygonOffset: true, polygonOffsetFactor: -2 }));
  group.add(kit.instanced(new THREE.PlaneGeometry(1.3, 1.3).rotateX(-Math.PI / 2), gr, grates, { cast: false }));

  // ---- fallen leaves: under street trees, gutters, lawns ----
  const leafOne = kit.memo('town:leafOne', () => new THREE.MeshStandardMaterial({
    map: kit.texture(kit.canvas(64, 64, (g) => { g.fillStyle = '#ffffff'; g.beginPath(); g.moveTo(32, 58); g.arc(32, 58, 46, -2.5, -1.62); g.lineTo(32, 58); g.arc(32, 58, 46, -1.52, -0.64); g.closePath(); g.fill(); g.strokeStyle = 'rgba(120,100,60,0.6)'; g.beginPath(); g.moveTo(32, 58); g.lineTo(32, 64); g.stroke(); })),
    alphaTest: 0.5, side: THREE.DoubleSide, roughness: 0.9, polygonOffset: true, polygonOffsetFactor: -3,
  }));
  const fallen = [], fcol = [], LT = ['#e9b628', '#d9a325', '#c7802c', '#b5582a', '#8a6a3a', '#e5c84a'];
  const drop = (x, y, z) => { fallen.push({ x, y, z, ry: rand() * 6.28, rz: (rand() - 0.5) * 0.3, s: 0.7 + rand() * 0.7 }); fcol.push(LT[(rand() * LT.length) | 0]); };
  for (const s of spots) {
    const n = s.type === 'shrub' ? 0 : s.type === 'ginkgo' ? 14 : 18;
    for (let i = 0; i < n; i++) { const a = rand() * 6.28, d = Math.sqrt(rand()) * 3.2; drop(s.x + Math.cos(a) * d, Y + 0.008, s.z + Math.sin(a) * d * (s.type === 'ginkgo' ? 0.55 : 1)); }
  }
  for (let i = 0; i < 260; i++) { const x = -128 + rand() * 256, side = rand() < 0.5 ? -1 : 1; drop(x, 0.006, side * (5.3 + rand() * 0.6)); }
  for (let i = 0; i < 380; i++) drop(-41 + rand() * 52, Y + 0.008, 13 + rand() * 38);
  const leaves = kit.instanced(new THREE.PlaneGeometry(0.15, 0.15).rotateX(-Math.PI / 2), leafOne, fallen, { cast: false });
  const lc = new THREE.Color(); fcol.forEach((h, i) => leaves.setColorAt(i, lc.set(h))); leaves.instanceColor.needsUpdate = true;
  group.add(leaves);

  // ---- 연무공원: paths ----
  const pathMat = kit.surface('concrete', { color: 0xd8c39d, roughness: 0.96 });
  const disc = (p, w) => { const m = new THREE.Mesh(new THREE.CircleGeometry(w / 2, 20).rotateX(-Math.PI / 2), pathMat); m.position.set(p[0], Y + 0.012, p[1]); m.receiveShadow = true; stat.add(m); };
  for (const [L, w] of [[PARK_PATH, 2.6], ...BRANCHES.map(b => [b, 1.8])]) {
    for (let i = 1; i < L.length; i++) stat.add(strip(L[i - 1], L[i], w, Y + 0.012, pathMat));
    for (const p of L.slice(1)) disc(p, w);
  }
  // Curb stones along the main path.
  const edge = kit.mat('curb', { roughness: 0.85 });
  for (let i = 2; i < PARK_PATH.length; i++) {
    const a = PARK_PATH[i - 1], b = PARK_PATH[i], dx = b[0] - a[0], dz = b[1] - a[1], len = Math.hypot(dx, dz), nx = -dz / len, nz = dx / len;
    for (const s of [-1, 1]) { const m = kit.box(len, 0.08, 0.1, edge, { x: (a[0] + b[0]) / 2 + nx * 1.35 * s, y: Y, z: (a[1] + b[1]) / 2 + nz * 1.35 * s, ry: -Math.atan2(dz, dx), cast: false }); stat.add(m); }
  }

  // ---- playground (놀이터) ----
  const epdm = kit.memo('town:epdm', () => {
    const r = kit.rng(9), c = kit.canvas(256, 256, (g, w) => { g.fillStyle = '#3f7a52'; g.fillRect(0, 0, w, w); for (let i = 0; i < 9000; i++) { g.fillStyle = ['#4a8a5e', '#346b45', '#5a9a6a', '#2e5f3e'][(r() * 4) | 0]; g.fillRect(r() * w, r() * w, 1.5, 1.5); } });
    return new THREE.MeshStandardMaterial({ map: kit.texture(c, { repeat: [1 / 2, 1 / 2] }), roughness: 0.95 });
  });
  stat.add(kit.plane(PLAY.x1 - PLAY.x0, PLAY.z1 - PLAY.z0, epdm, { x: (PLAY.x0 + PLAY.x1) / 2, y: Y + 0.02, z: (PLAY.z0 + PLAY.z1) / 2 }));
  stat.add(kit.box(PLAY.x1 - PLAY.x0 + 0.3, 0.12, 0.15, edge, { x: (PLAY.x0 + PLAY.x1) / 2, y: Y, z: PLAY.z0 }), kit.box(PLAY.x1 - PLAY.x0 + 0.3, 0.12, 0.15, edge, { x: (PLAY.x0 + PLAY.x1) / 2, y: Y, z: PLAY.z1 }));
  const red = kit.mat('paint:#c8463a', { roughness: 0.45 }), blue = kit.mat('paint:#2f6fb5', { roughness: 0.45 }), yel = kit.mat('paint:#f2c230', { roughness: 0.4 }), steel = kit.mat('steel'), green = kit.mat('paint:#3f9a5a', { roughness: 0.45 });
  const tower = (x, z, roof) => {
    for (const [dx, dz] of [[-0.8, -0.8], [0.8, -0.8], [0.8, 0.8], [-0.8, 0.8]]) stat.add(kit.box(0.12, 2.5, 0.12, steel, { x: x + dx, y: Y, z: z + dz }));
    stat.add(kit.box(1.75, 0.08, 1.75, kit.surface('wood', { color: 0xc8a070 }), { x, y: Y + 1.2, z }));
    const cone = new THREE.Mesh(new THREE.ConeGeometry(1.45, 1.0, 4), roof); cone.rotation.y = Math.PI / 4; cone.position.set(x, Y + 3.0, z); cone.castShadow = true; stat.add(cone);
    for (const s of [-1, 1]) stat.add(kit.box(1.7, 0.06, 0.05, roof, { x, y: Y + 2.0, z: z + 0.85 * s }));
  };
  tower(-33, 18.6, red); tower(-28.4, 18.6, blue);
  stat.add(kit.box(2.9, 0.07, 0.9, green, { x: -30.7, y: Y + 1.2, z: 18.6 }));
  for (const s of [-1, 1]) stat.add(kit.box(2.9, 0.05, 0.05, yel, { x: -30.7, y: Y + 1.95, z: 18.6 + 0.45 * s }));
  const slide = kit.box(3.2, 0.06, 0.6, yel, { x: -26.0, y: Y + 0.68, z: 18.6, rz: -0.34 }); stat.add(slide);
  for (const s of [-1, 1]) stat.add(kit.box(3.2, 0.22, 0.04, yel, { x: -26.0, y: Y + 0.72, z: 18.6 + 0.3 * s, rz: -0.34 }));
  for (let i = 0; i < 5; i++) stat.add(kit.box(0.05, 0.05, 0.6, steel, { x: -34.0, y: Y + 0.25 + i * 0.24, z: 18.6 }));
  // Swing: two A-frames and a top bar; seats on chains.
  for (const x of [-32.6, -27.4]) for (const s of [-1, 1]) stat.add(kit.cyl(0.05, 0.05, 2.5, steel, { x, y: Y, z: 23.6 + s * 0.3, rx: -s * 0.24 }, 8));
  const bar = kit.cyl(0.055, 0.055, 5.3, steel, { x: -30, y: Y + 2.4, z: 23.6, rz: Math.PI / 2 }, 8); bar.position.y = Y + 2.42; stat.add(bar);
  for (const x of [-31.2, -28.8]) {
    for (const dx of [-0.22, 0.22]) stat.add(kit.cyl(0.012, 0.012, 1.95, steel, { x: x + dx, y: Y + 0.47, z: 23.6 }, 4));
    stat.add(kit.box(0.5, 0.04, 0.2, kit.mat('rubber'), { x, y: Y + 0.44, z: 23.6 }));
  }
  for (const [x, z, m] of [[-24.8, 23.2, red], [-24.8, 21.4, blue]]) stat.add(kit.cyl(0.08, 0.08, 0.35, steel, { x, y: Y, z }, 8), kit.rbox(0.7, 0.4, 0.3, 0.12, m, { x, y: Y + 0.35, z }));
  solids.push({ x0: -34.3, x1: -24.2, z0: 17.6, z1: 19.6, y0: -0.5, y1: 3, id: 'play' }, { x0: -32.8, x1: -27.2, z0: 22.9, z1: 24.3, y0: -0.5, y1: 3, id: 'swing' });

  // ---- 정자 pavilion: raised wooden floor you can step onto, round columns, hipped tile roof ----
  const wood = kit.surface('wood', { color: 0xc49a6c }), col = kit.mat('paint:#7a3b2a', { roughness: 0.7 });
  const tile = kit.memo('town:giwa', () => {
    const h = kit.canvas(128, 128, (g, w) => { for (let y = 0; y < w; y++) { const v = 128 + 110 * Math.sin(y / w * Math.PI * 12); g.fillStyle = `rgb(${v},${v},${v})`; g.fillRect(0, y, w, 1); } });
    return new THREE.MeshStandardMaterial({ color: 0x4b5157, normalMap: kit.texture(kit.normalFrom(h, 4), { repeat: [3, 3], color: false }), roughness: 0.7, metalness: 0.1, flatShading: false });
  });
  const P = PAV, half = P.s / 2;
  stat.add(kit.slab(P.s, 0.4, P.s, wood, { x: P.x, y: Y, z: P.z }));
  for (const [dx, dz] of [[-1, -1], [0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0]]) {
    stat.add(kit.cyl(0.13, 0.14, 2.6, col, { x: P.x + dx * (half - 0.2), y: Y + 0.4, z: P.z + dz * (half - 0.2) }, 12));
    if (dx && dz) solids.push({ x0: P.x + dx * (half - 0.2) - 0.18, x1: P.x + dx * (half - 0.2) + 0.18, z0: P.z + dz * (half - 0.2) - 0.18, z1: P.z + dz * (half - 0.2) + 0.18, y0: -0.5, y1: 3.5, id: 'pavilion' });
  }
  for (const s of [-1, 1]) stat.add(kit.box(P.s, 0.22, 0.2, col, { x: P.x, y: Y + 2.9, z: P.z + s * (half - 0.2) }), kit.box(0.2, 0.22, P.s, col, { x: P.x + s * (half - 0.2), y: Y + 2.9, z: P.z }));
  const roof = new THREE.Mesh(new THREE.ConeGeometry(P.s * 0.98, 1.6, 4, 1, true), tile); roof.rotation.y = Math.PI / 4; roof.position.set(P.x, Y + 3.9, P.z); roof.castShadow = roof.receiveShadow = true;
  const under = new THREE.Mesh(new THREE.ConeGeometry(P.s * 0.97, 1.55, 4, 1, true), kit.mat('paint:#8a5a3c', { roughness: 0.8, side: THREE.BackSide })); under.rotation.y = Math.PI / 4; under.position.copy(roof.position); under.position.y -= 0.05;
  stat.add(roof, under);
  walk.push({ x0: P.x - half, x1: P.x + half, z0: P.z - half, z1: P.z + half, y: Y + 0.4 });

  // ---- benches (instanced wood + cast legs), facing the path ----
  const benchWood = mergeGeometries([0, 1, 2].map(i => new THREE.BoxGeometry(1.7, 0.035, 0.11).translate(0, 0.43, -0.16 + i * 0.13).toNonIndexed())
    .concat([0, 1].map(i => new THREE.BoxGeometry(1.7, 0.1, 0.03).rotateX(-0.2).translate(0, 0.62 + i * 0.14, 0.17 + i * 0.03).toNonIndexed())));
  const benchLeg = mergeGeometries([-0.7, 0.7].map(x => new THREE.BoxGeometry(0.06, 0.85, 0.48).translate(x, 0.42, 0.02).toNonIndexed()));
  const benches = [];
  for (const L of [PARK_PATH, ...BRANCHES]) for (let i = 2; i < L.length; i++) {
    const a = L[i - 1], b = L[i], dx = b[0] - a[0], dz = b[1] - a[1], len = Math.hypot(dx, dz);
    if (len < 7) continue;
    for (const k of [0.33, 0.7]) {
      const s = (benches.length % 2) ? 1 : -1, nx = -dz / len * s, nz = dx / len * s;
      const x = a[0] + dx * k + nx * 2.0, z = a[1] + dz * k + nz * 2.0;
      if (inRect([x, z], PLAY, 0.5)) continue;
      benches.push({ x, y: Y, z, ry: yawTo(-nx, -nz) });
      solids.push({ x0: x - 0.6, x1: x + 0.6, z0: z - 0.6, z1: z + 0.6, y0: -0.5, y1: 0.9, id: 'bench' });
    }
  }
  group.add(kit.instanced(benchWood, kit.surface('wood', { color: 0xb07a4c, repeat: [1, 1] }), benches), kit.instanced(benchLeg, kit.mat('darkMetal'), benches));

  // ---- outdoor exercise frames (야외 운동기구) ----
  const gx = (GYM.x0 + GYM.x1) / 2, gz = (GYM.z0 + GYM.z1) / 2;
  for (const [x, z] of [[gx - 3, gz - 2], [gx, gz - 2], [gx + 3, gz - 2]]) {
    stat.add(kit.cyl(0.07, 0.07, 2.2, blue, { x, y: Y, z }, 10), kit.cyl(0.05, 0.05, 1.2, yel, { x, y: Y + 1.5, z, rz: Math.PI / 2 }, 8));
    stat.add(kit.cyl(0.05, 0.05, 0.9, yel, { x: x + 0.45, y: Y + 0.3, z, rx: 0.3 }, 8), kit.box(0.35, 0.05, 0.28, kit.mat('rubber'), { x: x + 0.45, y: Y + 0.32, z: z + 0.15 }));
    solids.push({ x0: x - 0.7, x1: x + 0.8, z0: z - 0.5, z1: z + 0.5, y0: -0.5, y1: 2.3, id: 'gym' });
  }
  for (const [x, z] of [[gx - 1.5, gz + 2], [gx + 1.5, gz + 2]]) {
    stat.add(kit.cyl(0.08, 0.08, 1.1, yel, { x, y: Y, z }, 10), kit.cyl(0.4, 0.4, 0.06, blue, { x, y: Y + 0.18, z }, 18));
    solids.push({ x0: x - 0.45, x1: x + 0.45, z0: z - 0.45, z1: z + 0.45, y0: -0.5, y1: 1.2, id: 'gym' });
  }
  const gymSign = kit.sign(['야외 운동기구', '바른 자세로 천천히 이용하세요'], { w: 0.9, h: 0.5, bg: '#2f6fb5', single: true });
  gymSign.position.set(gx + 4.3, Y + 1.25, gz + 3.2); gymSign.rotation.y = -Math.PI / 4; stat.add(gymSign, kit.box(0.06, 1.0, 0.06, steel, { x: gx + 4.3, y: Y, z: gz + 3.24 }));

  // ---- park lamps (short, globe heads, light group 'street'), bins, fountain, the gate stone ----
  const lampMat = kit.memo('town:parkLamp', () => kit.lit('street', new THREE.MeshStandardMaterial({ color: 0xfff3dc, emissive: 0xffe2b0, emissiveIntensity: 3, roughness: 0.3 }), 3));
  const lamps = [[-2, 16], [-13, 23], [-13.4, 34], [-22, 43.6], [-35, 36], [-40, 32.4], [3, 30], [-19, 16]].map(([x, z]) => ({ x, y: Y, z }));
  group.add(kit.instanced(new THREE.CylinderGeometry(0.05, 0.07, 3.6, 8).translate(0, 1.8, 0), kit.mat('darkMetal'), lamps));
  group.add(kit.instanced(new THREE.SphereGeometry(0.2, 14, 10).translate(0, 3.75, 0), lampMat, lamps, { cast: false }));
  for (const p of lamps) solids.push({ x0: p.x - 0.15, x1: p.x + 0.15, z0: p.z - 0.15, z1: p.z + 0.15, y0: -0.5, y1: 3, id: 'lamp' });
  for (const [x, z] of [[-12.8, 27.6], [-16.8, 39], [-31, 41.5], [2, 15]]) stat.add(kit.cyl(0.24, 0.22, 0.85, kit.mat('paint:#3f6b4a', { roughness: 0.5 }), { x, y: Y, z }, 14), kit.cyl(0.26, 0.26, 0.06, kit.mat('darkMetal'), { x, y: Y + 0.85, z }, 14));
  stat.add(kit.cyl(0.22, 0.26, 0.9, kit.mat('curb'), { x: -16.8, y: Y, z: 20.5 }, 10), kit.cyl(0.03, 0.03, 0.12, kit.mat('metal'), { x: -16.8, y: Y + 0.9, z: 20.5 }, 6));
  stat.add(kit.rbox(1.7, 0.8, 0.45, 0.06, kit.surface('concrete', { color: 0xb9b2a5 }), { x: 5.2, y: Y, z: 13.4 }));
  const name = kit.sign(['연무공원'], { w: 1.4, h: 0.36, bg: '#6c665c', fg: '#f4efe4', single: true }); name.position.set(5.2, Y + 0.5, 13.17); name.rotation.y = Math.PI; stat.add(name);
  solids.push({ x0: 4.3, x1: 6.1, z0: 13.1, z1: 13.7, y0: -0.5, y1: 1, id: 'parkStone' });

  kit.merge(stat);
  return { group, solids, walk, spots };
}
