// Prop geometry for people, all in the shared vertex-PBR material (one draw call per prop).
// Hand props are built in hand space (origin at the wrist, rest orientation: fingers -y, palm toward the body,
// thumb forward). Body props are built in body rest space. The walker is skinned so its wheels can spin.
import * as THREE from 'three';
import { builder, PBR, col } from './people-geo.js';

const PI = Math.PI;
const at = (x, y, z, rx = 0, ry = 0, rz = 0) => new THREE.Matrix4().makeRotationFromEuler(new THREE.Euler(rx, ry, rz)).setPosition(x, y, z);
const mul = (a, b) => a.clone().multiply(b);
const GLOW_RED = [0.3, 0, 7, 0], LENS = [0.2, 0, 5, 0], SCREEN = [0.2, 0, 1.6, 0], CHROME = [0.28, 0.9, 0, 8];
const memo = new Map();
const once = (key, make) => { if (!memo.has(key)) memo.set(key, make()); return memo.get(key); };

// Point along a direction from the grip.
const along = (g, v, t) => [g[0] + v[0] * t, g[1] + v[1] * t, g[2] + v[2] * t];
const norm = (v) => { const l = Math.hypot(...v); return v.map(x => x / l); };

// Wheel centred on the origin, axle along x. Hub has 5 light spokes so spinning reads.
function wheel(b, m, r, w, skin, hubC = '#8d9196') {
  const tire = col('#141416'), hub = col(hubC), hubD = col(hubC, 0.45), h = w / 2;
  const rows = [[-h, 0.2], [-h, 0.45], [-h, 0.66], [-h, 0.88], [-h * 0.63, 1], [h * 0.63, 1], [h, 0.88], [h, 0.66], [h, 0.45], [h, 0.2]]
    .map(([y, k]) => ({ y, rx: r * k, k }));
  b.loft(rows, {
    seg: 20, cap: true, skin, m: mul(m, at(0, 0, 0, 0, 0, PI / 2)),
    color: (ring, th) => (ring.k > 0.7 ? tire : Math.cos(5 * th) > 0.15 && ring.k > 0.3 ? hub : hubD),
    pbr: (ring) => (ring.k > 0.7 ? PBR.rubber : PBR.plastic),
  });
}

// ---- hand props (s = +1 right hand, -1 left; k = hand scale) ----
function grip(s, k) { return [-s * 0.012 * k, -0.072 * k, 0]; }

export function phoneGeo(s, k) {
  return once(`phone:${s}:${k.toFixed(2)}`, () => {
    const b = builder(false), palm = 0.02 * k, cx = -s * (palm + 0.0045), cy = -0.078 * k;
    b.rbox(0.0085, 0.155, 0.074, 0.006, { p: 5, seg: 12, color: col('#1c1d21'), pbr: PBR.glossy, m: at(cx, cy - 0.0775, -0.006) });
    b.rbox(0.0012, 0.145, 0.066, 0.004, { p: 6, seg: 10, color: col('#a9ccff'), pbr: SCREEN, m: at(cx - s * 0.0043, cy - 0.0725, -0.006) });
    return b.build();
  });
}

export function batonGeo(s, k) { // traffic light baton (경광봉): black grip, glowing red tube
  return once(`baton:${s}:${k.toFixed(2)}`, () => {
    const b = builder(false), g = grip(s, k), v = norm([0, -0.5, -0.87]);
    b.pipe([along(g, v, -0.06), along(g, v, 0.12)], 0.016, { color: col('#1a1a1c'), pbr: PBR.rubber, seg: 10 });
    b.pipe([along(g, v, 0.12), along(g, v, 0.125)], 0.02, { color: col('#d9d9d9'), pbr: PBR.plastic, seg: 10 });
    b.pipe([along(g, v, 0.125), along(g, v, 0.5)], [0.018, 0.017], { color: col('#ff2b1c'), pbr: GLOW_RED, seg: 10 });
    return b.build();
  });
}

export function flashlightGeo(s, k) {
  return once(`flash:${s}:${k.toFixed(2)}`, () => {
    const b = builder(false), g = grip(s, k), v = norm([0, -0.3, -0.95]);
    b.pipe([along(g, v, -0.05), along(g, v, 0.09)], 0.015, { color: col('#2b2d31'), pbr: PBR.metal, seg: 10 });
    b.pipe([along(g, v, 0.09), along(g, v, 0.11), along(g, v, 0.15)], [0.017, 0.022, 0.023], { color: col('#34373c'), pbr: PBR.metal, seg: 12 });
    b.pipe([along(g, v, 0.15), along(g, v, 0.153)], 0.019, { color: col('#fff1d2'), pbr: LENS, seg: 12 });
    return b.build();
  });
}

// Hanging props: origin at the grip, built hanging straight down (people.js keeps them vertical).
export function waterJugGeo() { // 10 L water container, about 70 % full
  return once('jug', () => {
    const b = builder(false), body = col('#dfe8ec'), water = col('#9fbccb');
    b.pipe([[0, -0.055, 0.085], [0, -0.015, 0.07], [0, 0, 0.035], [0, 0, -0.035], [0, -0.015, -0.07], [0, -0.055, -0.085]], 0.011, { color: body, pbr: PBR.plastic, seg: 6 });
    b.rbox(0.14, 0.3, 0.24, 0.03, { p: 6, seg: 14, m: at(0, -0.36, 0), color: (r, th, i, p) => (p[1] < 0.2 ? water : body), pbr: PBR.plastic });
    b.loft([{ y: -0.065, rx: 0.022, z: -0.085 }, { y: -0.035, rx: 0.022, z: -0.085 }], { seg: 12, cap: true, color: col('#2f6db0'), pbr: PBR.plastic });
    return b.build();
  });
}

export function basketGeo(c = '#c22f38') { // mart hand basket with a few groceries
  return once(`basket:${c}`, () => {
    const b = builder(false), red = col(c), y0 = -0.38, y1 = -0.17;
    const walls = (k, flip) => b.loft([{ y: y0, rx: 0.125 * k, rz: 0.19 * k, p: 7 }, { y: y1, rx: 0.135 * k, rz: 0.21 * k, p: 7 }], { seg: 24, color: red, pbr: PBR.grid, flip });
    walls(1, false); walls(0.95, true);
    b.rbox(0.25, 0.012, 0.38, 0.03, { p: 7, seg: 16, color: col(c, 0.7), pbr: PBR.plastic, m: at(0, y0, 0) });
    const rim = []; for (let i = 0; i <= 24; i++) { const a = 2 * PI * i / 24, s = Math.sin(a), cc = Math.cos(a); rim.push([0.132 * Math.sign(s) * Math.abs(s) ** (2 / 7), y1, 0.207 * Math.sign(cc) * Math.abs(cc) ** (2 / 7)]); }
    b.pipe(rim, 0.008, { color: col(c, 0.85), pbr: PBR.plastic, seg: 5 });
    for (const s of [-1, 1]) b.pipe([[s * 0.135, y1 + 0.01, -0.07], [s * 0.09, -0.06, -0.06], [s * 0.02, -0.012, -0.05], [s * 0.012, 0, -0.03], [s * 0.012, 0, 0.03], [s * 0.02, -0.012, 0.05], [s * 0.09, -0.06, 0.06], [s * 0.135, y1 + 0.01, 0.07]], 0.008, { color: col(c, 0.75), pbr: PBR.plastic, seg: 5 });
    b.rbox(0.16, 0.05, 0.11, 0.01, { color: col('#e8b33a'), pbr: PBR.paper, m: at(0.03, y0 + 0.012, 0.08) });
    for (const z of [-0.12, -0.03]) {
      b.loft([{ y: y0 + 0.012, rx: 0.048 }, { y: y0 + 0.24, rx: 0.048 }, { y: y0 + 0.27, rx: 0.018 }, { y: y0 + 0.29, rx: 0.016 }].map(r => ({ ...r, x: -0.06, z })), { seg: 12, cap: true, color: (r) => (r.y > y0 + 0.25 ? col('#2f6db0') : r.y > y0 + 0.1 && r.y < y0 + 0.16 ? col('#3b8fd6') : col('#d3e4ee')), pbr: PBR.glossy });
    }
    return b.build();
  });
}

// ---- root props (origin at the feet, forward -z) ----
export function cartGeo() { // mart shopping cart, handle 1.0 m high, 0.4 m ahead
  return once('cart', () => {
    const b = builder(false), chrome = col('#b9bdc3'), red = col('#c9343c');
    const tilt = at(0, 0, 0, -PI / 2, 0, 0);
    const shell = (k, flip) => b.loft([{ y: 0.45, rx: 0.27 * k, rz: 0.2 * k, z: 0.78, p: 7, a: 2.25, ac: PI }, { y: 1.2, rx: 0.22 * k, rz: 0.16 * k, z: 0.82, p: 7, a: 2.25, ac: PI }],
      { seg: 24, color: chrome, pbr: CHROME, flip, m: tilt });
    shell(1, false); shell(0.96, true);
    for (const [z, w, h, y] of [[-0.45, 0.54, 0.4, 0.58], [-1.2, 0.44, 0.32, 0.66]]) b.rbox(w, h, 0.012, 0.01, { p: 7, color: chrome, pbr: CHROME, m: at(0, y, z) });
    for (const s of [-1, 1]) {
      b.pipe([[s * 0.21, 0.15, -0.45], [s * 0.16, 0.13, -1.18]], 0.012, { color: chrome, pbr: PBR.metal, seg: 6 });
      b.pipe([[s * 0.24, 0.15, -0.46], [s * 0.26, 0.6, -0.44], [s * 0.27, 0.99, -0.4]], 0.012, { color: chrome, pbr: PBR.metal, seg: 6 });
      for (const [x, z] of [[s * 0.2, -0.47], [s * 0.15, -1.15]]) {
        b.pipe([[x, 0.14, z], [x, 0.07, z + 0.02]], 0.008, { color: col('#555'), pbr: PBR.metal, seg: 5 });
        wheel(b, at(x, 0.055, z + 0.025), 0.055, 0.026, undefined, '#9b2b30');
      }
    }
    b.rbox(0.4, 0.012, 0.66, 0.01, { p: 7, color: chrome, pbr: CHROME, m: at(0, 0.17, -0.82) });
    b.pipe([[-0.29, 1.0, -0.38], [0.29, 1.0, -0.38]], 0.017, { color: red, pbr: PBR.plastic, seg: 10 });
    b.rbox(0.3, 0.2, 0.22, 0.02, { color: col('#d9e6ee'), pbr: PBR.glossy, m: at(-0.08, 0.5, -0.7) });
    b.rbox(0.22, 0.12, 0.3, 0.02, { color: col('#f2f0ea'), pbr: PBR.fabric, m: at(0.12, 0.5, -0.95) });
    b.rbox(0.2, 0.08, 0.14, 0.01, { color: col('#d8452e'), pbr: PBR.paper, m: at(0.1, 0.5, -0.6) });
    return b.build();
  });
}

export const WALKER_WHEELS = [[-0.27, 0.1, -0.86], [0.27, 0.1, -0.86], [-0.27, 0.1, -0.3], [0.27, 0.1, -0.3]];
export const WALKER_R = 0.1;
// 4-wheel rollator, skinned to bones [frame, FL, FR, RL, RR]; handle grips at height hy, about 0.27 m ahead.
export function walkerGeo(hy) {
  return once(`walker:${hy.toFixed(2)}`, () => {
    const b = builder(true), paint = col('#2c3e66'), pp = [0.35, 0.6, 0, 0], dark = col('#1c1d21'), o = { color: paint, pbr: pp, seg: 8, skin: 0 };
    for (const s of [-1, 1]) {
      const x = s * 0.26;
      b.pipe([[x, 0.13, -0.3], [x, hy - 0.06, -0.33], [x, hy, -0.35]], 0.013, o);
      b.pipe([[x, hy, -0.35], [x, hy + 0.004, -0.21]], 0.018, { ...o, color: dark, pbr: PBR.rubber });
      b.pipe([[x, hy - 0.025, -0.37], [x, 0.5, -0.63], [x, 0.23, -0.84]], 0.013, o);
      b.pipe([[x, 0.23, -0.84], [x, 0.1, -0.86]], 0.01, { ...o, color: dark, pbr: PBR.metal });
      b.pipe([[x, 0.55, -0.37], [x, 0.55, -0.68]], 0.011, o);
      b.pipe([[x, 0.3, -0.31], [x, 0.33, -0.74]], 0.01, o);
      b.pipe([[x, hy - 0.015, -0.345], [x, hy - 0.035, -0.26], [x, hy - 0.028, -0.22]], 0.006, { ...o, color: col('#9aa0a6'), pbr: PBR.metal });
      b.pipe([[x, hy - 0.02, -0.34], [x + s * 0.025, hy - 0.15, -0.32], [x + s * 0.02, 0.22, -0.3]], 0.003, { ...o, color: dark, pbr: PBR.rubber });
    }
    for (const z of [-0.38, -0.69]) b.pipe([[-0.26, 0.52, z], [0.26, 0.52, z]], 0.011, o);
    b.pipe([[-0.26, 0.33, -0.55], [0.26, 0.5, -0.55]], 0.009, o); b.pipe([[-0.26, 0.5, -0.55], [0.26, 0.33, -0.55]], 0.009, o);
    b.rbox(0.5, 0.045, 0.3, 0.015, { p: 6, seg: 16, color: col('#22232a'), pbr: PBR.fabric, skin: 0, m: at(0, 0.555, -0.53) });
    b.rbox(0.52, 0.075, 0.03, 0.012, { p: 6, seg: 16, color: col('#22232a'), pbr: PBR.fabric, skin: 0, m: at(0, hy - 0.17, -0.335) });
    b.rbox(0.36, 0.17, 0.26, 0.02, { p: 7, seg: 16, color: col('#3a3d44'), pbr: PBR.grid, skin: 0, m: at(0, 0.31, -0.53) });
    WALKER_WHEELS.forEach((p, i) => wheel(b, at(...p), WALKER_R, 0.035, i + 1));
    return b.build();
  });
}

// ---- body props in rest space. P = { d, front(y) -> z, back(y) -> z, c, skin, m } ----
export function backpack(b, P) {
  const { d } = P, H = d.H, m = P.m ?? new THREE.Matrix4(), sk = P.skin ?? 0, main = col(P.c ?? '#2b3242');
  const yb = d.waistY - 0.05, bz = Math.max(P.back(yb + 0.12), P.back(d.chestY), P.back(d.shY - 0.05)) + 0.004;
  b.rbox(0.29, 0.42, 0.14, 0.045, { p: 3, seg: 16, color: main, pbr: PBR.nylon, skin: sk, m: mul(m, at(0, yb, bz + 0.07)) });
  b.rbox(0.23, 0.19, 0.05, 0.03, { p: 3, seg: 12, color: col(P.c ?? '#2b3242', 0.8), pbr: PBR.nylon, skin: sk, m: mul(m, at(0, yb + 0.03, bz + 0.155)) });
  b.pipe([[-0.1, yb + 0.205, bz + 0.17], [0.1, yb + 0.205, bz + 0.17]], 0.004, { color: col('#111'), skin: sk, m, seg: 4 });
  b.pipe([[-0.04, yb + 0.415, bz + 0.07], [-0.02, yb + 0.45, bz + 0.07], [0.02, yb + 0.45, bz + 0.07], [0.04, yb + 0.415, bz + 0.07]], 0.007, { color: col('#1d1e21'), skin: sk, m, seg: 5 });
  for (const s of [-1, 1]) {
    const x = s * 0.55 * d.shX, f = (y, e = 0.008) => P.front(y) - e;
    b.pipe([[s * 0.08, yb + 0.39, bz + 0.01], [x, 0.836 * H + 0.012, P.back(0.83 * H) * 0.6], [x * 1.04, 0.846 * H + 0.01, 0.004 * H], [x * 1.06, 0.82 * H, f(0.82 * H)],
      [x * 1.08, 0.76 * H, f(0.76 * H)], [x * 1.12, 0.70 * H, f(0.70 * H, 0.006)], [s * 0.16 * H / 1.72, 0.665 * H, f(0.665 * H) * 0.3], [s * 0.14, yb + 0.03, bz + 0.01]], 0.011, { color: col('#1d1e21'), pbr: PBR.nylon, skin: sk, m, seg: 6 });
  }
}

export function crossbody(b, P) { // small bag on a strap from the right shoulder to the left hip
  const { d } = P, H = d.H, c = col(P.c ?? '#3a2e28'), f = (y, e = 0.006) => P.front(y) - e;
  b.pipe([[0.1, 0.846 * H + 0.01, 0.0], [0.08, 0.81 * H, f(0.81 * H)], [0.0, 0.72 * H, f(0.72 * H)], [-0.08, 0.63 * H, f(0.63 * H)], [-0.12, 0.575 * H, f(0.575 * H, 0.02)]], 0.006, { color: c, pbr: PBR.leather, skin: P.skinChest, seg: 5 });
  b.rbox(0.19, 0.14, 0.055, 0.02, { p: 4, seg: 12, color: c, pbr: PBR.leather, skin: P.skinPelvis, m: at(-0.12, 0.49 * H, f(0.53 * H, 0.03)) });
}
