// Mart produce shapes, one unit-size geometry per kind (mart.js KINDS order): 사과 apple, 귤 tangerine, 감자 potato,
// 양파 onion, 양배추 cabbage, 참외 melon, 무 radish. Each is non-indexed position+normal, within ±1.3 on every axis.
// seg = the tier's goodsSeg (5..12): radial segments; the lighter tiers also thin the lathe profiles (9 / 6 / 4 points from 높음 / 중간 / 낮음 down)
// and the cabbage's leaves, so 완전 낮음 stays near the old 20-triangle balls.
import * as THREE from 'three';

// Concatenate geometries (position + normal only) into one non-indexed geometry.
function join(list) {
  const parts = list.map((g) => (g.index ? g.toNonIndexed() : g));
  const n = parts.reduce((a, g) => a + g.attributes.position.count, 0), pos = new Float32Array(n * 3), nor = new Float32Array(n * 3);
  let o = 0;
  for (const g of parts) { pos.set(g.attributes.position.array, o); nor.set(g.attributes.normal.array, o); o += g.attributes.position.count * 3; }
  const out = new THREE.BufferGeometry();
  out.setAttribute('position', new THREE.BufferAttribute(pos, 3)); out.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
  return out;
}
// A lathe from [r, y] pairs, thinned to n evenly picked points (always both ends).
function lathe(pts, seg) {
  const n = seg >= 10 ? pts.length : seg >= 8 ? 6 : 4, use = Array.from({ length: n }, (_, i) => pts[Math.round((i * (pts.length - 1)) / (n - 1))]);
  return new THREE.LatheGeometry(use.map(([r, y]) => new THREE.Vector2(r, y)), seg);
}
const sphere = (seg, sx = 1, sy = 1, sz = 1) => new THREE.SphereGeometry(1, seg, Math.max(3, Math.round(seg * 0.45))).scale(sx, sy, sz);
// Seeded lumpiness: the offset depends on the vertex position, so the sphere seam's duplicate vertices move together.
function lumpy(g, amt) {
  const p = g.attributes.position, v = new THREE.Vector3();
  for (let i = 0; i < p.count; i++) {
    v.fromBufferAttribute(p, i);
    const h = Math.sin(v.x * 12.9898 + v.y * 78.233 + v.z * 37.719) * 43758.5453, k = 1 + (h - Math.floor(h) - 0.5) * 2 * amt;
    p.setXYZ(i, v.x * k, v.y * k, v.z * k);
  }
  g.computeVertexNormals(); return g;
}

export function produceGeos(seg = 10) {
  const apple = lathe([[0, -0.84], [0.42, -0.9], [0.8, -0.62], [0.98, -0.12], [0.95, 0.38], [0.72, 0.78], [0.36, 0.88], [0.1, 0.72], [0, 0.66]], seg);
  const tangerine = sphere(seg, 1, 0.82, 1);
  const potato = lumpy(sphere(seg, 1.16, 0.8, 0.95), 0.1);
  const onion = lathe([[0, -0.82], [0.36, -0.86], [0.8, -0.56], [0.97, -0.08], [0.82, 0.38], [0.48, 0.72], [0.18, 0.96], [0.05, 1.16], [0, 1.2]], seg);
  // cabbage: a core and three loose outer leaves, each a cap a little larger and tipped a different way
  const rings = Math.max(2, Math.round(seg * 0.3));
  const leaves = [[0.5, 0], [0.45, 2.1], [0.55, 4.2]].slice(0, seg >= 7 ? 3 : 2).map(([tip, yaw]) => new THREE.SphereGeometry(1.12, seg, rings, 0, Math.PI * 2, 0, 1.25).rotateX(tip).rotateY(yaw).translate(0, -0.06, 0));
  const cabbage = join([sphere(seg, 1, 0.9, 1), ...leaves]);
  const melon = lumpy(sphere(seg, 1.25, 0.84, 0.84), 0.025);
  // radish: lies along x once mart.js turns it (rz = π/2); a white body with a rounded shoulder, tapering to a tip
  const radish = lathe([[0, -1.26], [0.16, -1.05], [0.3, -0.6], [0.4, 0], [0.44, 0.55], [0.42, 0.95], [0.3, 1.14], [0.12, 1.22], [0, 1.24]], seg);
  return [apple, tangerine, potato, onion, cabbage, melon, radish].map((g) => (g.index ? g.toNonIndexed() : g)).map((g) => { if (!g.attributes.normal) g.computeVertexNormals(); g.deleteAttribute?.('uv'); return g; });
}
