// Greenery for the 한결물류 office (office.js): the yard's street trees built like town-park.js (instanced
// leaf-card crowns with ambient-occlusion vertex colours and outward normals, autumn tints per tree, trunks with
// four branches) and the office plants: a 떡갈고무나무 with broad leaves on a stem in each floor pot and small
// 다육이 rosettes on the desks, all from one alpha-tested leaf material (one draw call once merged). On the tiers
// without mesh detail (detailOf: 완전 낮음, 낮음) crowns and plants keep about the old triangle and material count.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { detailOf } from './town-buildings-detail.js';

const TREE = { n: 110, rx: 2.6, ry: 2.0, rz: 2.6, cy: 4.7, size: 1.9, trunk: 4.8, r0: 0.16, branches: 4 };
const TREE_LITE = { ...TREE, n: 40, size: 2.5, branches: 0 }; // ~ the old icosahedron's 80 triangles
const TINTS = ['#c98b3c', '#b8732f', '#a39143', '#c9a64c', '#8f9a45', '#b0602c'];

// A canvas of small oval leaves on twigs, white so the instance tint colours it.
const crownTex = (kit) => kit.memo('office:crownTex', () => {
  const r = kit.rng(12);
  return kit.texture(kit.canvas(256, 256, (g, w, h) => {
    g.strokeStyle = 'rgba(78,60,44,0.95)'; g.lineWidth = 2.2; g.lineCap = 'round';
    for (let i = 0; i < 7; i++) { g.beginPath(); g.moveTo(w / 2, h / 2); g.quadraticCurveTo(w / 2 + (r() - 0.5) * w * 0.5, h / 2 + (r() - 0.5) * h * 0.5, w / 2 + (r() - 0.5) * w * 0.85, h / 2 + (r() - 0.5) * h * 0.85); g.stroke(); }
    for (let i = 0; i < 120; i++) {
      const a = r() * 6.283, d = Math.sqrt(r()) * w * 0.43, s = 8 + r() * 9, v = 170 + (r() * 85 | 0);
      g.fillStyle = `rgb(${v},${v},${Math.max(0, v - (r() * 25 | 0))})`;
      g.save(); g.translate(w / 2 + Math.cos(a) * d, h / 2 + Math.sin(a) * d); g.rotate(r() * 6.283); g.beginPath(); g.ellipse(s * 0.5, 0, s * 0.62, s * 0.3, 0, 0, 7); g.fill(); g.restore();
    }
  }), { repeat: [1, 1] });
});
// Double-sided cards that keep their outward (crown-shaped) normals on the back faces too.
const keepNormals = (m, key) => {
  m.onBeforeCompile = (sh) => { sh.fragmentShader = sh.fragmentShader.replace('#include <normal_fragment_begin>', THREE.ShaderChunk.normal_fragment_begin.replace(/normal \*= faceDirection;/g, '')); };
  m.customProgramCacheKey = () => key;
  return m;
};
const crownMat = (kit) => kit.memo('office:crown', () => keepNormals(new THREE.MeshStandardMaterial({ map: crownTex(kit), alphaTest: 0.5, side: THREE.DoubleSide, roughness: 0.82, vertexColors: true }), 'officeCrown'));

function crownGeo(T, r) {
  const pos = [], nor = [], uv = [], col = [], idx = [];
  const q = new THREE.Quaternion(), e = new THREE.Euler(), v = new THREE.Vector3(), d = new THREE.Vector3(), nn = new THREE.Vector3();
  for (let i = 0; i < T.n; i++) {
    d.set(r() * 2 - 1, r() * 2 - 1, r() * 2 - 1); if (d.lengthSq() < 1e-4) d.set(0, 1, 0); d.normalize();
    const t = 0.4 + 0.6 * Math.sqrt(r()), c = [d.x * T.rx * t, T.cy + d.y * T.ry * t, d.z * T.rz * t], s = T.size * (0.7 + 0.6 * r());
    q.setFromEuler(e.set(r() * 6.28, r() * 6.28, r() * 6.28));
    const ao = Math.min(1, 0.42 + 0.4 * t + 0.25 * (d.y * 0.5 + 0.5)), base = pos.length / 3; // darker deep inside and underneath
    for (const [a, b] of [[-0.5, -0.5], [0.5, -0.5], [0.5, 0.5], [-0.5, 0.5]]) {
      v.set(a * s, b * s, 0).applyQuaternion(q).add({ x: c[0], y: c[1], z: c[2] }); pos.push(v.x, v.y, v.z);
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
  for (let i = 0; i < T.branches; i++) {
    const len = Math.max(T.rx, T.ry) * (0.7 + r() * 0.3), b = new THREE.CylinderGeometry(T.r0 * 0.18, T.r0 * 0.38, len, 5).translate(0, len / 2, 0);
    parts.push(b.rotateZ(0.55 + r() * 0.35).rotateY(i * 1.57 + r() * 0.6).translate(0, T.cy - T.ry * 0.55 + r() * 0.6, 0));
  }
  return mergeGeometries(parts.map((p) => p.toNonIndexed()));
}
// The yard's trees. list: [{ x, y, z, ry, s }] -> [trunks, crowns] (two instanced draw calls, as before).
export function yardTrees(kit, list) {
  const T = detailOf(kit) ? TREE : TREE_LITE, r = kit.rng(733), crowns = kit.instanced(crownGeo(T, r), crownMat(kit), list), c = new THREE.Color();
  list.forEach((t, i) => crowns.setColorAt(i, c.set(TINTS[(i * 7 + 3) % TINTS.length])));
  crowns.instanceColor.needsUpdate = true;
  return [kit.instanced(trunkGeo(T, r), kit.mat('bark'), list), crowns];
}

// One broad leaf (떡갈고무나무: wavy edge, pale midrib and veins), glossy.
const leafMat = (kit) => kit.memo('office:leaf', () => new THREE.MeshStandardMaterial({ roughness: 0.45, alphaTest: 0.5, side: THREE.DoubleSide, map: kit.texture(kit.canvas(128, 128, (g, w, h) => {
  const body = new Path2D(); body.moveTo(w / 2, h - 4);
  body.bezierCurveTo(w * 0.05, h * 0.75, w * 0.02, h * 0.3, w * 0.22, h * 0.12); body.bezierCurveTo(w * 0.34, h * 0.02, w * 0.66, h * 0.02, w * 0.78, h * 0.12);
  body.bezierCurveTo(w * 0.98, h * 0.3, w * 0.95, h * 0.75, w / 2, h - 4);
  const gr = g.createLinearGradient(0, h, 0, 0); gr.addColorStop(0, '#2f4a24'); gr.addColorStop(1, '#4b6b33'); g.fillStyle = gr; g.fill(body);
  g.strokeStyle = 'rgba(190,205,150,0.55)'; g.lineWidth = 2.5; g.beginPath(); g.moveTo(w / 2, h - 4); g.lineTo(w / 2, h * 0.08); g.stroke();
  g.lineWidth = 1.2; for (let y = h * 0.75; y > h * 0.15; y -= h * 0.11) for (const s of [-1, 1]) { g.beginPath(); g.moveTo(w / 2, y); g.quadraticCurveTo(w / 2 + s * w * 0.2, y - h * 0.04, w / 2 + s * w * 0.36, y - h * 0.1); g.stroke(); }
})) }));
const card = (w, h, tilt, yaw, x, y, z) => new THREE.PlaneGeometry(w, h).translate(0, h / 2, 0).rotateX(tilt).rotateY(yaw).translate(x, y, z).toNonIndexed();

// A 떡갈고무나무 in a floor pot whose rim is at (x, top, z): soil, a stem and 15 leaves up it, facing out. plain: an
// existing material for stem and soil on the tiers without mesh detail (no extra draw calls there).
export function floorPlant(kit, x, top, z, seed, plain) {
  const r = kit.rng(seed * 17 + 5), leaves = [], H = 1.0 + r() * 0.3, d = detailOf(kit), n = d ? 15 : 9;
  for (let i = 0; i < n; i++) { const t = 0.18 + (i / n) * 0.82, a = i * 2.4 + r() * 0.6, s = 0.22 + 0.1 * (1 - t) + r() * 0.05; leaves.push(card(s * 0.8, s, 0.55 + r() * 0.5, a, x + Math.sin(a) * 0.03, top + t * H, z + Math.cos(a) * 0.03)); }
  return [new THREE.Mesh(mergeGeometries(leaves), leafMat(kit)),
    new THREE.Mesh(new THREE.CylinderGeometry(0.018, 0.026, H + 0.05, 6).translate(x, top + H / 2, z), d ? kit.mat('bark') : plain),
    new THREE.Mesh(new THREE.CircleGeometry(0.18, 14).rotateX(-Math.PI / 2).translate(x, top - 0.03, z), d ? kit.mat('paint:#3a2c20', { roughness: 1 }) : plain)];
}
// A small 다육이 rosette sitting in a desk pot whose soil is at (x, y, z).
export function deskPlant(kit, x, y, z, seed) {
  const r = kit.rng(seed * 31 + 9), leaves = [];
  for (let i = 0; i < 11; i++) { const a = i * 2.4, s = 0.05 + r() * 0.03; leaves.push(card(s * 0.7, s, 0.35 + (i % 3) * 0.3, a, x, y, z)); }
  return new THREE.Mesh(mergeGeometries(leaves), leafMat(kit));
}
