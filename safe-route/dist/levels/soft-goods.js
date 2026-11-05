// Soft household goods and plants shared by 302호 (apartment-home-detail.js) and the shelter camp
// (shelter-camp-detail.js): pillows and folded blankets with welded, puffed surfaces, cloth draped over a box
// (duvet) or along a profile (a throw over the sofa back, a towel on a rail), a backpack and a 보스턴백, sneakers
// with a separate sole, slippers, and potted plants with real leaves (strips folded along the midrib, drooping,
// golden-angle spirals) in lathe pots with a rolled rim. Every builder returns BufferGeometry with position, normal
// and uv (ready for kit.merge and InstancedMesh); shapes vary only through the seeded rng passed in.
import * as THREE from 'three';
import { mergeGeometries, mergeVertices } from 'three/addons/utils/BufferGeometryUtils.js';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';

// How much small detail a level models, from the tier's goods knob (engine/quality.js PROFILES.goodsSeg):
// 0 on '완전 낮음' (the plain shapes stay), 1 on '낮음' (fewer segments, less clutter), 2 from '중간' up.
export const detailOf = (Q = {}) => { const s = Q.goodsSeg ?? 10; return s >= 8 ? 2 : s >= 7 ? 1 : 0; };

const NG = (g) => (g.index ? g.toNonIndexed() : g);
export const join = (list) => mergeGeometries(list.map(NG));
const V2 = (pts) => pts.map(([x, y]) => new THREE.Vector2(x, y));
function planarUV(g) {
  const p = g.attributes.position, uv = new Float32Array(p.count * 2);
  for (let i = 0; i < p.count; i++) { uv[2 * i] = p.getX(i) + p.getY(i) * 0.5; uv[2 * i + 1] = p.getZ(i) + p.getY(i) * 0.5; }
  g.setAttribute('uv', new THREE.BufferAttribute(uv, 2)); return g;
}
// Weld (positions only), move every vertex with fn(v), then smooth normals: soft shapes without seams.
function soften(geo, fn) {
  geo.deleteAttribute('normal'); geo.deleteAttribute('uv');
  const g = mergeVertices(geo, 1e-4), p = g.attributes.position, v = new THREE.Vector3();
  for (let i = 0; i < p.count; i++) { v.fromBufferAttribute(p, i); fn(v); p.setXYZ(i, v.x, v.y, v.z); }
  g.computeVertexNormals();
  return planarUV(g);
}

// ---- bedding ----
// Pillow lying flat (base on y = 0): full in the middle, corners pinched thin, seams drawn in.
export function pillowGeo(w, h, d, lv = 2) {
  return soften(new THREE.BoxGeometry(w, h, d, lv > 1 ? 8 : 4, lv > 1 ? 2 : 1, lv > 1 ? 6 : 3), (v) => {
    const a = Math.max(0, 1 - (2 * v.x / w) ** 2), b = Math.max(0, 1 - (2 * v.z / d) ** 2), puff = Math.sqrt(a * b);
    v.y *= v.y > 0 ? 0.28 + 0.72 * puff : 0.7 + 0.3 * puff;
    v.x *= 1 - 0.06 * (1 - b); v.z *= 1 - 0.06 * (1 - a); v.y += h * 0.5;
  });
}
// Blanket folded in `layers`, each a soft slab a little smaller and askew, the fold edge (+x) rounded over.
export function foldedGeo(w, h, d, r, layers = 3) {
  const t = h / layers, out = [];
  for (let k = 0; k < layers; k++) {
    const lw = w * (1 - 0.025 * k), ld = d * (1 - 0.02 * k);
    const g = soften(new THREE.BoxGeometry(lw, t, ld, 4, 1, 3), (v) => {
      const a = 2 * v.x / lw, b = 2 * v.z / ld;
      v.y *= 1 - 0.35 * b ** 4; if (a > 0.5) { v.y *= 1 + 0.4 * (a - 0.5); v.x += 0.01 * (a - 0.5); }
    });
    out.push(g.rotateY((r() - 0.5) * 0.07).translate((r() - 0.5) * 0.02, t * (k + 0.5), (r() - 0.5) * 0.02));
  }
  return join(out);
}
// Folds: two crossing waves (deterministic from the seed), scaled by `amp`.
const folds = (seed, amp) => {
  const f = [1 + (seed * 7 % 5), 2 + (seed * 3 % 4), (seed * 1.7) % 6.28, (seed * 2.3) % 6.28];
  return (u, v) => amp * (Math.sin(u * 6 * f[0] / 3 + v * 2.2 + f[2]) * 0.6 + Math.sin(v * 5 * f[1] / 3 - u * 1.3 + f[3]) * 0.4);
};
// Cloth over a box top w x d at height `top`, hanging hang[] = [-x, +x, -z, +z] down the sides (0: ends at the edge),
// puffed by `puff` in the middle (a duvet), with folds; centred on x = z = 0.
export function drapeGeo({ w, d, top, hang = [0.2, 0.2, 0.2, 0.2], puff = 0, amp = 0.012, seed = 1, nx = 18, nz = 16, out = 0.012 }) {
  const W = w + hang[0] + hang[1], D = d + hang[2] + hang[3];
  const g = new THREE.PlaneGeometry(W, D, nx, nz).rotateX(-Math.PI / 2).translate((hang[1] - hang[0]) / 2, 0, (hang[3] - hang[2]) / 2);
  const p = g.attributes.position, F = folds(seed, amp);
  for (let i = 0; i < p.count; i++) {
    const u = p.getX(i), v = p.getZ(i), ex = Math.max(0, Math.abs(u) - w / 2), ez = Math.max(0, Math.abs(v) - d / 2);
    const cu = Math.sign(u) * Math.min(Math.abs(u), w / 2), cv = Math.sign(v) * Math.min(Math.abs(v), d / 2);
    if (!ex && !ez) {
      const pf = puff * Math.sqrt(Math.max(0, (1 - (2 * u / w) ** 2) * (1 - (2 * v / d) ** 2)));
      p.setXYZ(i, u, top + pf + F(u, v) * (0.4 + pf * 8), v); continue;
    }
    const drop = Math.hypot(ex, ez), wav = F(u * 2, v * 2) * 1.6; // hanging: folds become vertical pleats
    p.setXYZ(i, cu + Math.sign(u) * (ex ? out + wav * (ex / (ex + 0.05)) : 0), top - drop * 0.97, cv + Math.sign(v) * (ez ? out + wav * (ez / (ez + 0.05)) : 0));
  }
  g.computeVertexNormals();
  return g;
}
// Cloth laid along a profile [[x, y], ...] (a throw over a sofa back, a towel over a rail), `width` along z,
// centred on z = 0, with pleats across it.
export function ribbonGeo(path, width, { n = 14, m = 6, amp = 0.01, seed = 1 } = {}) {
  const curve = new THREE.CatmullRomCurve3(path.map(([x, y]) => new THREE.Vector3(x, y, 0)), false, 'centripetal');
  const pts = curve.getSpacedPoints(n), F = folds(seed, amp), pos = [], uv = [], idx = [];
  for (let i = 0; i <= n; i++) {
    const a = pts[Math.max(0, i - 1)], b = pts[Math.min(n, i + 1)], tx = b.x - a.x, ty = b.y - a.y, l = Math.hypot(tx, ty) || 1;
    for (let j = 0; j <= m; j++) {
      const z = (j / m - 0.5) * width, f = F(i / n * 2, z * 3);
      pos.push(pts[i].x - ty / l * f, pts[i].y + tx / l * f, z + f * 0.4); uv.push(j / m, i / n);
      if (i < n && j < m) { const k = i * (m + 1) + j; idx.push(k, k + 1, k + m + 1, k + 1, k + m + 2, k + m + 1); }
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  g.setIndex(idx); g.computeVertexNormals();
  return g;
}

// ---- bags and shoes (base on y = 0) ----
// Backpack: padded body, front pocket, top grab loop, two shoulder straps down the back (-z).
export function backpackGeo(lv = 2) {
  const parts = [ // ~520 triangles on '중간' and up
    new RoundedBoxGeometry(0.3, 0.4, 0.18, lv > 1 ? 2 : 1, 0.06).translate(0, 0.2, 0),
    new RoundedBoxGeometry(0.22, 0.17, 0.07, 1, 0.03).translate(0, 0.12, 0.1),
    new THREE.TorusGeometry(0.035, 0.008, 4, 6, Math.PI).translate(0, 0.395, -0.03),
  ];
  for (const x of [-0.075, 0.075]) parts.push(new THREE.TubeGeometry(new THREE.QuadraticBezierCurve3(new THREE.Vector3(x, 0.37, -0.085), new THREE.Vector3(x * 1.25, 0.22, -0.15), new THREE.Vector3(x * 1.5, 0.05, -0.085)), lv > 1 ? 4 : 3, 0.012, 4));
  return join(parts);
}
// 보스턴백: a soft barrel bag sagging onto a flat bottom, two handles over the top.
export function duffelGeo(lv = 2) {
  const g = new THREE.CapsuleGeometry(0.12, 0.3, lv > 1 ? 3 : 2, lv > 1 ? 10 : 8).rotateZ(Math.PI / 2), p = g.attributes.position;
  for (let i = 0; i < p.count; i++) p.setXYZ(i, p.getX(i), Math.max(p.getY(i), -0.085) * (p.getY(i) > 0 ? 0.9 : 1), p.getZ(i) * 0.82);
  g.computeVertexNormals(); g.translate(0, 0.085, 0);
  const handles = [-0.07, 0.07].map((x) => new THREE.TorusGeometry(0.06, 0.008, 4, 10, Math.PI).rotateY(Math.PI / 2).translate(x, 0.17, 0));
  return join([g, ...handles]);
}
// Sneaker upper from a side profile (toe toward +z), tapered at the toe and heel; pairs with soleGeo.
export function shoeGeo() {
  const L = 0.27, prof = [[0, 0.02], [0.262, 0.02], [0.272, 0.034], [0.258, 0.058], [0.2, 0.071], [0.14, 0.083], [0.1, 0.098], [0.035, 0.1], [0.004, 0.088], [-0.004, 0.05]];
  const g = new THREE.ExtrudeGeometry(new THREE.Shape(V2(prof)), { depth: 0.066, bevelEnabled: true, bevelThickness: 0.012, bevelSize: 0.008, bevelSegments: 1, curveSegments: 2 }); // ~76 triangles: a hundred of them in the shelter
  g.translate(-L / 2, 0, -0.033).rotateY(-Math.PI / 2);
  const p = g.attributes.position;
  for (let i = 0; i < p.count; i++) { const t = THREE.MathUtils.clamp(p.getZ(i) / L + 0.5, 0, 1); p.setX(i, p.getX(i) * (0.72 + 0.28 * Math.sin(Math.PI * (0.15 + 0.75 * t)))); }
  g.computeVertexNormals();
  return g;
}
// Sole: a foot-shaped plate (narrow heel, wide ball, round toe), 2.2 cm thick.
export function soleGeo(h = 0.022, L = 0.28) {
  const s = new THREE.Shape();
  s.moveTo(0, -L / 2); s.quadraticCurveTo(0.052, -L / 2, 0.052, -L * 0.25); s.quadraticCurveTo(0.044, L * 0.05, 0.054, L * 0.22);
  s.quadraticCurveTo(0.054, L / 2, 0, L / 2); s.quadraticCurveTo(-0.05, L / 2, -0.05, L * 0.22); s.quadraticCurveTo(-0.04, 0, -0.045, -L * 0.25);
  s.quadraticCurveTo(-0.045, -L / 2, 0, -L / 2);
  return new THREE.ExtrudeGeometry(s, { depth: h, bevelEnabled: false, curveSegments: 2 }).rotateX(-Math.PI / 2).rotateY(Math.PI);
}
// 실내화 (slipper): sole plus a soft band over the forefoot.
export function slipperGeo(lv = 2) {
  const band = new THREE.CylinderGeometry(0.054, 0.054, 0.085, lv > 1 ? 12 : 6, 1, true, -Math.PI / 2, Math.PI).rotateX(-Math.PI / 2).scale(1, 0.75, 1).translate(0, 0.016, 0.055);
  return join([soleGeo(0.02, 0.26), band]);
}

// Both faces of a thin sheet for a single-sided material (each triangle again, reversed, normals flipped).
export function twoSided(geo) {
  const g = NG(geo), back = g.clone(), n = back.attributes.normal;
  for (const k of Object.keys(back.attributes)) {
    const a = back.attributes[k], s = a.itemSize;
    for (let i = 0; i < a.count; i += 3) for (let c = 0; c < s; c++) { const t = a.array[(i + 1) * s + c]; a.array[(i + 1) * s + c] = a.array[(i + 2) * s + c]; a.array[(i + 2) * s + c] = t; }
  }
  for (let i = 0; i < n.array.length; i++) n.array[i] = -n.array[i];
  return mergeGeometries([g, back]);
}

// ---- plants ----
// One blade along +y, folded along the midrib, drooping back (+z) toward the tip. kind: 'oval' | 'sword' | 'heart'.
export function leafGeo(len, wid, { n = 6, droop = 0.3, fold = 0.25, kind = 'oval' } = {}) {
  const pos = [], idx = [], uv = [];
  for (let i = 0; i <= n; i++) {
    const t = i / n, w = wid * (kind === 'sword' ? Math.min(1, (1 - t) * 3) ** 0.7 * (0.75 + 0.25 * Math.sin(t * 9))
      : kind === 'heart' ? Math.sin(Math.PI * Math.min(1, t * 1.15)) ** 0.6 * (1 - 0.35 * t) : Math.sin(Math.PI * t) ** 0.75);
    const y = len * t * (1 - droop * t * 0.5), z = len * droop * t * t;
    for (const [k, x] of [[0, -w / 2], [1, 0], [2, w / 2]]) { pos.push(x, y, z + (k === 1 ? 0 : fold * w * 0.5)); uv.push(k / 2, t); }
    if (i < n) { const a = i * 3; idx.push(a, a + 1, a + 4, a, a + 4, a + 3, a + 1, a + 2, a + 5, a + 1, a + 5, a + 4); }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  g.setIndex(idx); g.computeVertexNormals();
  return g;
}
// Pot with a rolled rim (lathe), soil a little below the rim, on a saucer. Returns { pot, soil } at base y = 0.
export function potGeo(rt, h, lv = 2, { rb = rt * 0.74, saucer = true } = {}) {
  if (!lv) { // 완전 낮음: a plain tapered pot, no saucer
    const pot = new THREE.LatheGeometry(V2([[0, 0.012], [rb, 0.012], [rt * 1.05, h], [rt * 0.92, h - 0.05], [0, h - 0.05]]), 8);
    return { pot, soil: new THREE.CircleGeometry(rt * 0.93, 8).rotateX(-Math.PI / 2).translate(0, h - 0.045, 0) };
  }
  const seg = lv > 1 ? 20 : 12;
  const pot = new THREE.LatheGeometry(V2([[0, 0.012], [rb, 0.012], [rb * 1.02, 0.02], [rt * 0.97, h - 0.03], [rt * 1.05, h - 0.026], [rt * 1.07, h - 0.008], [rt * 1.03, h], [rt * 0.95, h - 0.004], [rt * 0.92, h - 0.05], [0, h - 0.05]]), seg);
  const parts = [pot];
  if (saucer) parts.push(new THREE.LatheGeometry(V2([[0, 0], [rb * 1.25, 0], [rb * 1.36, 0.022], [rb * 1.3, 0.024], [rb * 1.2, 0.01], [0, 0.01]]), seg));
  return { pot: join(parts), soil: new THREE.CircleGeometry(rt * 0.93, seg).rotateX(-Math.PI / 2).translate(0, h - 0.045, 0) };
}
// Leaves and stems of a potted plant standing on y = 0 (the soil). kind: 'rubber' (인도고무나무: stems with broad
// glossy leaves spiralling up), 'snake' (산세베리아: upright sword blades), 'pothos' (스킨답서스: heart leaves
// spilling over the rim). Returns { leaves, stems | null }.
export function plantGeo(kind, r, lv = 2, s = 1) {
  const leaves = [], stems = [], n = lv > 1 ? 6 : 3, GA = 2.39996;
  if (kind === 'rubber') {
    for (const [sx, sz, H, k0] of [[0.015, 0, 0.95, 0], [-0.05, 0.035, 0.68, 1.3]]) {
      const lean = (r() - 0.5) * 0.12, cnt = lv > 1 ? 10 : lv ? 7 : 4;
      stems.push(new THREE.CylinderGeometry(0.009, 0.014, H * s, 5).translate(0, H * s / 2, 0).rotateZ(lean).translate(sx, 0, sz));
      for (let k = 0; k < cnt; k++) {
        const t = 0.26 + 0.74 * k / (cnt - 1), y = H * s * t, L = (0.22 + 0.09 * (1 - t) + r() * 0.04) * s;
        leaves.push(leafGeo(L, L * 0.46, { n, droop: 0.22 + r() * 0.12, fold: 0.18 }).rotateX(0.55 + 0.55 * (1 - t) + r() * 0.15).rotateY(k0 + k * GA + r() * 0.3)
          .translate(sx - Math.sin(lean) * y, y, sz));
      }
      leaves.push(leafGeo(0.09 * s, 0.035 * s, { n: 3, droop: 0.05, fold: 0.5 }).rotateX(0.12).rotateY(k0).translate(sx - Math.sin(lean) * H * s, H * s - 0.01, sz)); // the rolled new leaf
    }
  } else if (kind === 'snake') {
    const cnt = lv > 1 ? 9 : lv ? 7 : 5;
    for (let k = 0; k < cnt; k++) {
      const a = k * GA + r() * 0.4, d = 0.02 + r() * 0.05, L = (0.32 + r() * 0.26) * s;
      leaves.push(leafGeo(L, (0.045 + r() * 0.02) * s, { n, droop: 0.04 + r() * 0.06, fold: 0.35, kind: 'sword' }).rotateY(r() * 6.28).rotateX(0.06 + r() * 0.16).rotateY(a).translate(Math.sin(a) * d, -0.01, Math.cos(a) * d));
    }
  } else {
    const cnt = lv > 1 ? 13 : lv ? 9 : 6;
    for (let k = 0; k < cnt; k++) {
      const a = k * GA, over = k % 3 !== 0, d = over ? 0.11 * s : 0.03, L = (0.075 + r() * 0.03) * s;
      leaves.push(leafGeo(L, L * 0.85, { n: Math.max(3, n - 2), droop: over ? 0.5 : 0.25, fold: 0.15, kind: 'heart' }).rotateX(over ? 1.5 + r() * 0.5 : 0.5 + r() * 0.4).rotateY(a)
        .translate(Math.sin(a) * d, over ? 0.05 * s : 0.02, Math.cos(a) * d));
    }
  }
  return { leaves: join(leaves), stems: stems.length ? join(stems) : null };
}
