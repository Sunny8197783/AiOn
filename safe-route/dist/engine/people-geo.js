// Geometry + material helpers for people and their props. Everything is built from lofted rings with
// per-vertex colour, roughness/metalness/emissive/pattern (aPbr) and optional skin weights, so one person
// (or one prop) is a single mesh with one shared material = one draw call.
import * as THREE from 'three';

// Procedural surface patterns evaluated in the fragment shader from pUv (metres): fabric folds, checks...
export const PAT = { none: 0, fabric: 1, check: 2, knit: 3, denim: 4, hair: 5, quilt: 6, skin: 7, grid: 8 };
// [roughness, metalness, emissive (x vertex colour), pattern]
export const PBR = {
  skin: [0.55, 0, 0, PAT.skin], fabric: [0.93, 0, 0, PAT.fabric], knit: [0.97, 0, 0, PAT.knit],
  denim: [0.9, 0, 0, PAT.denim], check: [0.9, 0, 0, PAT.check], quilt: [0.58, 0, 0, PAT.quilt],
  nylon: [0.6, 0, 0, PAT.fabric], hair: [0.5, 0, 0, PAT.hair], perm: [0.72, 0, 0, PAT.knit],
  leather: [0.42, 0, 0, 0], suede: [0.85, 0, 0, PAT.fabric], rubber: [0.88, 0, 0, 0], plastic: [0.45, 0, 0, 0],
  metal: [0.3, 0.85, 0, 0], paint: [0.38, 0.55, 0, 0], fluo: [0.7, 0, 0.14, PAT.fabric], reflect: [0.28, 0.6, 0.3, 0],
  grid: [0.5, 0, 0, PAT.grid], paper: [0.75, 0, 0, 0], glossy: [0.25, 0, 0, 0],
};
// sRGB hex -> linear working colour, optionally darkened/brightened.
export const col = (hex, k = 1) => new THREE.Color(hex).multiplyScalar(k);

let shared = null;
// One MeshStandardMaterial for every person and prop: vertex colours + per-vertex PBR + patterns.
export function vpbrMaterial() {
  if (shared) return shared;
  const m = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 1, metalness: 0 });
  m.onBeforeCompile = (sh) => {
    sh.vertexShader = sh.vertexShader
      .replace('#include <common>', '#include <common>\nattribute vec4 aPbr;\nattribute vec2 pUv;\nvarying vec4 vPbr;\nvarying vec2 vPuv;')
      .replace('#include <uv_vertex>', '#include <uv_vertex>\nvPbr = aPbr; vPuv = pUv;');
    sh.fragmentShader = sh.fragmentShader
      .replace('#include <common>', `#include <common>
varying vec4 vPbr;
varying vec2 vPuv;
float pH(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float pN(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(pH(i), pH(i + vec2(1.0, 0.0)), f.x), mix(pH(i + vec2(0.0, 1.0)), pH(i + vec2(1.0, 1.0)), f.x), f.y); }`)
      .replace('#include <color_fragment>', `#include <color_fragment>
{
  int pt = int(vPbr.w + 0.5); vec2 q = vPuv; float m = 1.0;
  if (pt == 1) m = 0.9 + 0.16 * pN(q * vec2(7.0, 11.0));
  else if (pt == 2) { vec2 g = abs(fract(q * 15.0) - 0.5); m = (0.93 + 0.1 * pN(q * 8.0)) * (1.0 - 0.15 * (step(0.33, g.x) + step(0.33, g.y))); }
  else if (pt == 3) m = 0.8 + 0.3 * pN(q * 85.0);
  else if (pt == 4) m = 0.82 + 0.22 * pN(q * vec2(5.0, 16.0));
  else if (pt == 5) m = 0.78 + 0.32 * pN(vec2(q.x * 140.0, q.y * 5.0));
  else if (pt == 6) m = (0.94 + 0.1 * pN(q * 6.0)) * (1.0 - 0.32 * smoothstep(0.72, 1.0, abs(fract(q.y * 7.0) - 0.5) * 2.0));
  else if (pt == 7) m = 0.96 + 0.07 * pN(q * 28.0);
  else if (pt == 8) { vec2 g = abs(fract(q * 28.0) - 0.5); m = mix(1.0, 0.32, step(0.36, max(g.x, g.y))); }
  diffuseColor.rgb *= m;
}`)
      .replace('#include <roughnessmap_fragment>', 'float roughnessFactor = vPbr.x;')
      .replace('#include <metalnessmap_fragment>', 'float metalnessFactor = vPbr.y;')
      .replace('#include <emissivemap_fragment>', '#include <emissivemap_fragment>\ntotalEmissiveRadiance += diffuseColor.rgb * vPbr.z;');
  };
  m.customProgramCacheKey = () => 'people-vpbr-1';
  shared = m;
  return m;
}

const _v = new THREE.Vector3(), _n = new THREE.Vector3(), _m3 = new THREE.Matrix3();

// Accumulates lofted parts into one indexed BufferGeometry (with skin attributes when skinned).
export function builder(skinned = false) {
  const P = [], N = [], C = [], U = [], R = [], SI = [], SW = [], I = [];

  function addPart(pos, cols, uvs, pbrs, skin, idx, m) {
    const base = P.length / 3, n = pos.length / 3, nrm = new Float32Array(pos.length);
    for (let t = 0; t < idx.length; t += 3) { // area-weighted face normals
      const a = idx[t] * 3, b = idx[t + 1] * 3, c = idx[t + 2] * 3;
      const e1x = pos[b] - pos[a], e1y = pos[b + 1] - pos[a + 1], e1z = pos[b + 2] - pos[a + 2];
      const e2x = pos[c] - pos[a], e2y = pos[c + 1] - pos[a + 1], e2z = pos[c + 2] - pos[a + 2];
      const nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x;
      for (const k of [a, b, c]) { nrm[k] += nx; nrm[k + 1] += ny; nrm[k + 2] += nz; }
    }
    if (m) _m3.getNormalMatrix(m);
    for (let i = 0; i < n; i++) {
      _v.fromArray(pos, i * 3); _n.fromArray(nrm, i * 3);
      if (m) { _v.applyMatrix4(m); _n.applyMatrix3(_m3); }
      if (_n.lengthSq() < 1e-20) _n.set(0, 1, 0);
      _n.normalize();
      P.push(_v.x, _v.y, _v.z); N.push(_n.x, _n.y, _n.z);
    }
    C.push(...cols); U.push(...uvs); R.push(...pbrs);
    if (skinned) for (let i = 0; i < n; i++) { SI.push(skin[i * 4], skin[i * 4 + 2], 0, 0); SW.push(skin[i * 4 + 1], skin[i * 4 + 3], 0, 0); }
    for (const k of idx) I.push(k + base);
  }

  // rings: [{ y, rx, rz?, x?, z?, p? (superellipse, 2 = ellipse), a? (half arc), ac? (arc centre), c? (Color), pbr? }]
  // theta 0 points +z (back), +-PI points -z (front), +PI/2 points +x. Arc mode (any ring has a) leaves a gap.
  // o: { seg, color: Color | fn(ring, theta, i, p), pbr, skin: bone | fn(p, ring, i, theta) -> [b0,w0,b1,w1],
  //      cap (close both ends), flip (inward faces), m (Matrix4 applied after normals) }
  function loft(rings0, o = {}) {
    let rings = rings0;
    if (o.cap) {
      const f = rings[0], l = rings[rings.length - 1];
      rings = [{ ...f, rx: 0.0006, rz: 0.0006 }, ...rings, { ...l, rx: 0.0006, rz: 0.0006 }];
    }
    const seg = o.seg ?? 12, arc = rings.some(r => r.a != null), n = arc ? seg + 1 : seg;
    const desc = rings[rings.length - 1].y < rings[0].y;
    const pos = [], cols = [], uvs = [], pbrs = [], skin = [], idx = [];
    let v = 0;
    rings.forEach((r, i) => {
      if (i) { const q = rings[i - 1]; v += Math.hypot(r.y - q.y, (r.x ?? 0) - (q.x ?? 0), (r.z ?? 0) - (q.z ?? 0)); }
      const rz = r.rz ?? r.rx, a = r.a ?? Math.PI, ac = r.ac ?? 0, e = 2 / (r.p ?? 2), circ = (r.rx + rz) / 2;
      for (let k = 0; k < n; k++) {
        const th = arc ? ac - a + 2 * a * k / seg : -Math.PI + 2 * Math.PI * k / seg;
        const s = Math.sin(th), c = Math.cos(th);
        const p = [(r.x ?? 0) + r.rx * Math.sign(s) * Math.abs(s) ** e, r.y, (r.z ?? 0) + rz * Math.sign(c) * Math.abs(c) ** e];
        pos.push(p[0], p[1], p[2]);
        const cc = typeof o.color === 'function' ? o.color(r, th, i, p) : (r.c ?? o.color);
        cols.push(cc.r, cc.g, cc.b);
        uvs.push(th * circ, v);
        pbrs.push(...(r.pbr ?? o.pbr ?? PBR.fabric));
        const sk = typeof o.skin === 'function' ? o.skin(p, r, i, th) : [o.skin ?? 0, 1, 0, 0];
        skin.push(sk[0], sk[1], sk[2] ?? 0, sk[3] ?? 0);
      }
    });
    const out = desc !== !!o.flip;
    for (let i = 0; i < rings.length - 1; i++) for (let k = 0; k < seg; k++) {
      const k1 = arc ? k + 1 : (k + 1) % seg;
      const a = i * n + k, b = i * n + k1, c = (i + 1) * n + k, d = (i + 1) * n + k1;
      if (out) idx.push(a, c, b, b, c, d); else idx.push(a, b, c, b, d, c);
    }
    addPart(pos, cols, uvs, pbrs, skin, idx, o.m);
  }

  // Tube through 3D points [[x,y,z],...]; r: number or per-point array. Ends are capped.
  function pipe(pts, r, o = {}) {
    const seg = o.seg ?? 8, V = pts.map(p => new THREE.Vector3(...p)), n = V.length;
    const T = V.map((p, i) => V[Math.min(n - 1, i + 1)].clone().sub(V[Math.max(0, i - 1)]).normalize());
    let nr = Math.abs(T[0].y) < 0.9 ? new THREE.Vector3(0, 1, 0) : new THREE.Vector3(1, 0, 0);
    const rad = (i) => (Array.isArray(r) ? r[i] : r);
    const ringsP = [];
    V.forEach((p, i) => {
      nr.sub(T[i].clone().multiplyScalar(nr.dot(T[i]))).normalize();
      const b = T[i].clone().cross(nr);
      ringsP.push({ p, nr: nr.clone(), b, r: rad(i) });
    });
    const all = [{ ...ringsP[0], r: 0.0004 }, ...ringsP, { ...ringsP[n - 1], r: 0.0004 }];
    const pos = [], cols = [], uvs = [], pbrs = [], skin = [], idx = [];
    const cc = o.color, pb = o.pbr ?? PBR.plastic;
    let v = 0;
    all.forEach((R0, i) => {
      if (i) v += R0.p.distanceTo(all[i - 1].p);
      for (let k = 0; k < seg; k++) {
        const th = 2 * Math.PI * k / seg, c = Math.cos(th), s = Math.sin(th);
        pos.push(R0.p.x + R0.r * (R0.nr.x * c + R0.b.x * s), R0.p.y + R0.r * (R0.nr.y * c + R0.b.y * s), R0.p.z + R0.r * (R0.nr.z * c + R0.b.z * s));
        cols.push(cc.r, cc.g, cc.b); uvs.push(th * R0.r, v); pbrs.push(...pb);
        const sk = typeof o.skin === 'function' ? o.skin([R0.p.x, R0.p.y, R0.p.z]) : [o.skin ?? 0, 1, 0, 0];
        skin.push(sk[0], sk[1], sk[2] ?? 0, sk[3] ?? 0);
      }
    });
    for (let i = 0; i < all.length - 1; i++) for (let k = 0; k < seg; k++) {
      const k1 = (k + 1) % seg, a = i * seg + k, b = i * seg + k1, c = (i + 1) * seg + k, d = (i + 1) * seg + k1;
      idx.push(a, b, c, b, d, c);
    }
    addPart(pos, cols, uvs, pbrs, skin, idx, o.m);
  }

  // Rounded box standing on y=0 (w along x, d along z), optional matrix o.m to place it.
  function rbox(w, h, d, rad, o = {}) {
    const hw = w / 2, hd = d / 2, rr = Math.min(rad, hw, hd, h / 2), p = o.p ?? 6;
    const ring = (y, k) => ({ y, rx: Math.max(0.0006, hw - rr * k), rz: Math.max(0.0006, hd - rr * k), p });
    const rings = [ring(0, 1), ring(rr * 0.29, 0.29), ring(rr, 0), ring(h - rr, 0), ring(h - rr * 0.29, 0.29), ring(h, 1)];
    loft(rings, { seg: o.seg ?? 16, cap: true, ...o });
  }

  function build() {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(P, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(N, 3));
    g.setAttribute('color', new THREE.Float32BufferAttribute(C, 3));
    g.setAttribute('aPbr', new THREE.Float32BufferAttribute(R, 4));
    g.setAttribute('pUv', new THREE.Float32BufferAttribute(U, 2));
    if (skinned) {
      g.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(SI, 4));
      g.setAttribute('skinWeight', new THREE.Float32BufferAttribute(SW, 4));
    }
    g.setIndex(I);
    g.computeBoundingBox(); g.computeBoundingSphere();
    return g;
  }

  return { loft, pipe, rbox, build, vertexCount: () => P.length / 3 };
}

// Linear interpolation of ring rows (ascending y) at height y; numeric fields only.
export function ringAt(rows, y) {
  if (y <= rows[0].y) return { ...rows[0], y };
  for (let i = 0; i < rows.length - 1; i++) {
    const a = rows[i], b = rows[i + 1];
    if (y <= b.y) {
      const t = b.y === a.y ? 0 : (y - a.y) / (b.y - a.y), out = { ...a, y };
      for (const k of ['rx', 'rz', 'x', 'z', 'p']) if (a[k] != null || b[k] != null) out[k] = (a[k] ?? 0) + ((b[k] ?? 0) - (a[k] ?? 0)) * t;
      return out;
    }
  }
  return { ...rows[rows.length - 1], y };
}

// Dress ascending rows in zones [{ y (from here upward), c, pbr, t (thickness) }]: each row gets its zone's
// colour/finish/thickness; at each zone edge two rings are inserted so colours and cuffs stay crisp.
export function zoned(rows, zones) {
  const zs = [...zones].sort((a, b) => a.y - b.y);
  const zoneAt = (y) => { let z = zs[0]; for (const q of zs) if (y >= q.y) z = q; return z; };
  const dress = (r, z) => ({ ...r, rx: r.rx + (z.t ?? 0), rz: (r.rz ?? r.rx) + (z.t ?? 0), c: z.c, pbr: z.pbr });
  const out = [];
  const y0 = rows[0].y, y1 = rows[rows.length - 1].y;
  const edges = zs.slice(1).map(z => z.y).filter(y => y > y0 && y < y1);
  let e = 0;
  for (const r of rows) {
    while (e < edges.length && edges[e] <= r.y) {
      const y = edges[e], base = ringAt(rows, y);
      if (y !== r.y || true) { out.push(dress(base, zoneAt(y - 1e-6)), dress(base, zoneAt(y))); }
      e++;
    }
    if (!out.length || out[out.length - 1].y !== r.y || out[out.length - 1].c !== zoneAt(r.y).c) out.push(dress(r, zoneAt(r.y)));
  }
  return out;
}
