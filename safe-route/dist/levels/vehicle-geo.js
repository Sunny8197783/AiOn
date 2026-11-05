// The town's three procedural vehicles (sedan, SUV, 1t box truck) as side profiles extruded across the car, with
// wheel arches cut into the sill, glass, B-pillar, tyres and hubs, bumpers, grille, mirrors, plate and lamps. Shared
// by the town traffic (town-traffic.js), the shelter's parked cars (shelter-camp-detail.js) and the 302호 window view
// (apartment-view.js). Pure geometry, no materials: vehicleGeo returns { body, glass, dark, lamp }, front bumper at
// -z, wheels on y = 0; the lamp part's UVs pick the white (u 0.25) or red (u 0.75) half of a 2 px lamp texture.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

const NG = (g) => (g.index ? g.toNonIndexed() : g);
const merge = (list) => mergeGeometries(list.map(NG));

// Side profiles in (s from the front bumper, y). Wheel arches are cut into the bottom edge. panes: the T.top edges
// that carry the windscreen and the rear window.
export const TYPES = {
  sedan: { L: 4.7, W: 1.82, r: 0.32, axles: [0.95, 3.85], weight: 0.5, v: 1,
    top: [[0.05, 0.32], [0, 0.55], [0.08, 0.72], [0.9, 0.86], [1.65, 0.95], [2.25, 1.4], [3.45, 1.42], [4.15, 1.0], [4.62, 0.95], [4.7, 0.62], [4.66, 0.32]],
    glass: [[1.62, 0.955], [2.24, 1.415], [2.32, 1.36], [3.4, 1.37], [3.46, 1.428], [4.17, 1.005], [4.1, 0.96], [1.7, 0.94]], pillar: 2.85, lights: [0.68, 0.86, -0.01, 4.69], panes: [[4, 5], [6, 7]] },
  suv: { L: 4.6, W: 1.88, r: 0.36, axles: [1.0, 3.75], weight: 0.3, v: 0.95,
    top: [[0.05, 0.4], [0, 0.66], [0.1, 0.92], [1.0, 1.02], [1.55, 1.08], [2.08, 1.66], [4.18, 1.69], [4.52, 1.5], [4.6, 1.02], [4.58, 0.4]],
    glass: [[1.52, 1.09], [2.06, 1.69], [2.14, 1.62], [4.12, 1.63], [4.21, 1.705], [4.545, 1.505], [4.5, 1.08], [1.6, 1.07]], pillar: 2.95, lights: [0.8, 1.05, -0.01, 4.65], panes: [[4, 5], [6, 7]] },
  truck: { L: 5.1, W: 1.76, r: 0.33, axles: [0.78, 3.9], weight: 0.2, v: 0.85,
    top: [[0, 0.42], [0, 1.0], [0.08, 1.72], [0.34, 1.98], [1.62, 1.98], [1.62, 0.95], [5.1, 0.95], [5.1, 0.42]],
    glass: [[0.035, 1.12], [0.115, 1.78], [0.36, 1.995], [1.0, 1.995], [1.0, 1.12]], box: [1.7, 5.1, 0.98, 2.42], pillar: 0, lights: [0.62, 0.8, -0.05, 5.14], panes: [[1, 2]] },
};

// The body's side outline (s along the car, y up) with both wheel arches of n segments, as a Shape to extrude (a car
// cover uses it too).
export function bodyShape(T, n = 10) {
  const outline = [...T.top];
  const [af, ar] = T.axles, r = T.r, R = r + 0.06, by = T.top[0][1];
  const arch = (c) => { const pts = []; for (let k = 0; k <= n; k++) { const a = k / n * Math.PI; pts.push([c + R * Math.cos(a), Math.max(by, r + R * Math.sin(a) * 0.95)]); } return pts; };
  outline.push(...arch(ar), ...arch(af));
  return new THREE.Shape(outline.map(([s, y]) => new THREE.Vector2(s, y)));
}

// lo: fewer bevel, curve and wheel segments (the lower tiers); far: seen from 10 m and more (the 302호 window view),
// the dark part keeps only tyres and bumpers and the arches are coarser.
export function vehicleGeo(T, lo = false, far = false) {
  const { L, W, r } = T, bev = 0.05, ws = far ? 6 : lo ? 8 : 16, hs = lo ? 6 : 12, shape = bodyShape(T, far ? 6 : 10);
  const toCar = (g, depth) => g.translate(-L / 2, 0, -depth / 2).rotateY(-Math.PI / 2);
  const bodyParts = [toCar(new THREE.ExtrudeGeometry(shape, { depth: W - 2 * bev, bevelEnabled: true, bevelThickness: bev, bevelSize: bev, bevelSegments: lo ? 1 : 2, curveSegments: lo ? 2 : 4 }), W - 2 * bev)];
  if (T.box) { const [s0, s1, y0, y1] = T.box; bodyParts.push(new THREE.BoxGeometry(W + 0.14, y1 - y0, s1 - s0).translate(0, (y0 + y1) / 2, (s0 + s1) / 2 - L / 2)); }
  if (T.pillar) bodyParts.push(new THREE.BoxGeometry(W + 0.02, 0.42, 0.09).translate(0, (T.glass[2][1] + T.glass[0][1]) / 2, T.pillar - L / 2));
  // Side windows: the glass outline, flush with the doors. The body's bevel puffs its outline out by `bev`, which
  // buries that outline's windscreen and rear window, so those are separate panes 6 mm over the puffed body (T.top
  // edges T.panes), inside the A/C-pillars and the frame.
  const side = new THREE.ExtrudeGeometry(new THREE.Shape(T.glass.map(([s, y]) => new THREE.Vector2(s, y))), { depth: W + 0.012, bevelEnabled: false });
  const gy = Math.min(...T.glass.map(([, y]) => y)), panes = (T.panes || []).map(([i, j]) => {
    const [a, b] = [T.top[i], T.top[j]], len = Math.hypot(b[0] - a[0], b[1] - a[1]), d = [(b[0] - a[0]) / len, (b[1] - a[1]) / len], n = [-d[1], d[0]];
    const t0 = Math.max(0.04, d[1] > 0 ? (gy - a[1]) / d[1] : 0), t1 = Math.min(len - 0.04, d[1] < 0 ? (gy - a[1]) / d[1] : len), o = bev + 0.006; // above the belt line
    const at = (t) => [a[0] + d[0] * t + n[0] * o, a[1] + d[1] * t + n[1] * o], [p0, p1] = [at(t0), at(t1)], hw = W / 2 - 0.12;
    const g = new THREE.PlaneGeometry(1, 1), q = g.attributes.position; // corners (+-0.5, +-0.5): x across the car, y along the edge
    for (let k = 0; k < q.count; k++) { const e = q.getY(k) > 0 ? p1 : p0; q.setXYZ(k, q.getX(k) * 2 * hw, e[1], e[0] - L / 2); }
    g.computeVertexNormals(); if (g.attributes.normal.getY(0) < 0) g.index.array.reverse();
    g.computeVertexNormals(); return g;
  });
  const glass = merge([toCar(side, W + 0.012), ...panes]);
  const z = (s) => s - L / 2, hx = W / 2 - 0.13;
  const tyres = [], hubs = [];
  for (const s of T.axles) for (const sx of [-1, 1]) {
    tyres.push(new THREE.CylinderGeometry(r, r, 0.22, ws).rotateZ(Math.PI / 2).translate(sx * hx, r, z(s)));
    hubs.push(new THREE.CylinderGeometry(r * 0.62, r * 0.62, 0.02, hs).rotateZ(Math.PI / 2).translate(sx * (hx + 0.115), r, z(s)));
  }
  const dark = [...tyres,
    new THREE.BoxGeometry(W * 0.98, 0.16, 0.12).translate(0, T.top[0][1] + 0.06, z(0.04)), new THREE.BoxGeometry(W * 0.98, 0.16, 0.12).translate(0, T.top[0][1] + 0.06, z(L - 0.04)),
    new THREE.BoxGeometry(W * 0.5, 0.17, 0.04).translate(0, T.lights[0] - 0.12, z(0) - 0.01),
    ...[-1, 1].map(sx => new THREE.BoxGeometry(0.1, 0.09, 0.16).translate(sx * (W / 2 + 0.07), T.glass[0][1] + 0.06, z(T.glass[0][0] + 0.15)))];
  // Head and tail lamps share one material: UVs pick the white or red half of a 2 px texture.
  const lamp = (s, y, w, d, u) => [-1, 1].map(sx => { const g = new THREE.BoxGeometry(w, 0.12, d).translate(sx * (W / 2 - 0.22 - w / 2 + 0.1), y, z(s)), uv = g.attributes.uv; for (let k = 0; k < uv.count; k++) uv.setXY(k, u, 0.5); return g; });
  return {
    body: merge(bodyParts), glass, dark: merge(far ? dark.slice(0, 6) : [...dark, ...hubs, new THREE.BoxGeometry(0.52, 0.11, 0.02).translate(0, T.top[0][1] + 0.2, z(L) + 0.07)]),
    lamp: merge([...lamp(T.lights[2], T.lights[0], 0.36, 0.07, 0.25), ...lamp(T.lights[3], T.lights[1], 0.32, 0.06, 0.75)]),
  };
}
