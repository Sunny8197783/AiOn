// One person's body: anatomy rows (fractions of height H), clothing layers, hair, face planes and small
// accessories, lofted into a single geometry skinned to an 18-bone skeleton (one draw call per person).
// Frame: feet at origin, facing -z, person's right hand on +x. Rest pose: arms hanging straight down.
import * as THREE from 'three';
import { builder, zoned, ringAt, PBR, col } from './people-geo.js';

export const B = { root: 0, pelvis: 1, spine: 2, chest: 3, neck: 4, head: 5, lArm: 6, lFore: 7, lHand: 8, rArm: 9, rFore: 10, rHand: 11, lThigh: 12, lShin: 13, lFoot: 14, rThigh: 15, rShin: 16, rFoot: 17 };
export const PARENT = [-1, 0, 1, 2, 3, 4, 3, 6, 7, 3, 9, 10, 1, 12, 13, 1, 15, 16];

const PI = Math.PI;
export const ss = (a, b, x) => { const t = Math.min(1, Math.max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
const gauss = (x, s) => Math.exp(-((x / s) ** 2));

// Body measurements in metres from a spec { H, sex, girth }.
export function dims(spec) {
  const H = spec.H, f = spec.sex === 'f', g = spec.girth ?? 1;
  const d = {
    H, f, g, hh: H / 7.5,
    ankleY: 0.05 * H, kneeY: 0.285 * H, hipY: 0.53 * H, pelvisY: 0.565 * H, waistY: 0.635 * H, chestY: 0.75 * H,
    shY: (f ? 0.815 : 0.818) * H, neckY: 0.852 * H, headY: 0.9 * H, elY: 0.63 * H, wrY: 0.485 * H,
    shX: (f ? 0.104 : 0.115) * H * (0.55 + 0.45 * g), hipX: (f ? 0.056 : 0.052) * H * (0.7 + 0.3 * g),
  };
  d.L1 = d.hipY - d.kneeY; d.L2 = d.kneeY - d.ankleY; d.U1 = d.shY - d.elY; d.U2 = d.elY - d.wrY;
  return d;
}

// Rest joint positions [x,y,z] per bone (root space).
export function joints(d) {
  const J = [];
  J[B.root] = [0, 0, 0]; J[B.pelvis] = [0, d.pelvisY, 0]; J[B.spine] = [0, d.waistY, 0]; J[B.chest] = [0, d.chestY, 0];
  J[B.neck] = [0, d.neckY, 0.005 * d.H]; J[B.head] = [0, d.headY, 0];
  for (const s of [-1, 1]) {
    const a = s < 0 ? B.lArm : B.rArm, l = s < 0 ? B.lThigh : B.rThigh;
    J[a] = [s * d.shX, d.shY, 0]; J[a + 1] = [s * d.shX, d.elY, 0]; J[a + 2] = [s * d.shX, d.wrY, 0];
    J[l] = [s * d.hipX, d.hipY, 0]; J[l + 1] = [s * d.hipX, d.kneeY, 0]; J[l + 2] = [s * d.hipX, d.ankleY, 0];
  }
  return J;
}

// Skin along a descending chain of joints: [{ y, up, dn, w }] (blend half-width w around each joint).
function chain(y, segs) {
  for (const j of segs) {
    if (y > j.y + j.w) return [j.up, 1];
    if (y > j.y - j.w) { const t = ss(j.y + j.w, j.y - j.w, y); return [j.up, 1 - t, j.dn, t]; }
  }
  return [segs[segs.length - 1].dn, 1];
}

function torsoSkin(d) {
  const H = d.H;
  return (p) => {
    const [x, y] = p, ax = Math.abs(x);
    if (y < d.pelvisY + 0.02 * H) return [B.pelvis, 1];
    if (y < d.waistY + 0.03 * H) { const t = ss(d.pelvisY + 0.02 * H, d.waistY + 0.03 * H, y); return [B.pelvis, 1 - t, B.spine, t]; }
    if (y < d.chestY) { const t = ss(d.waistY + 0.03 * H, d.chestY, y); return [B.spine, 1 - t, B.chest, t]; }
    const arm = 0.55 * ss(d.shX - 0.045 * H, d.shX + 0.005 * H, ax) * ss(d.shY - 0.07 * H, d.shY - 0.01 * H, y);
    if (arm > 0.01) return [B.chest, 1 - arm, x < 0 ? B.lArm : B.rArm, arm];
    if (y > d.neckY - 0.012 * H) return [B.chest, 0.6, B.neck, 0.4];
    return [B.chest, 1];
  };
}
// Garment parts hanging below the hips follow both thighs (front/back stays between them).
function skirtSkin(d, hemY) {
  const top = d.hipY + 0.02 * d.H, ts = torsoSkin(d);
  return (p) => {
    const k = 0.9 * Math.min(1, Math.max(0, (top - p[1]) / Math.max(0.01, top - hemY)));
    if (k <= 0) return ts(p);
    const wx = ss(-0.06 * d.H, 0.06 * d.H, p[0]);
    return [B.pelvis, 1 - k, B.lThigh, k * (1 - wx), B.rThigh, k * wx];
  };
}
const headChain = (d) => [{ y: d.headY - 0.006 * d.H, up: B.head, dn: B.neck, w: 0.016 * d.H }, { y: d.neckY, up: B.neck, dn: B.chest, w: 0.014 * d.H }];

// Torso surface rows (crotch -> neck base), fractions of H.
function torsoRows(d, spec) {
  const H = d.H, f = d.f, g = d.g, bl = spec.belly ?? 0;
  const sx = (f ? 0.9 : 1) * g, hx = (f ? 1.09 : 1) * (0.5 + 0.5 * g), wx = (f ? 0.86 : 1) * g;
  const R = (y, rx, rz, z = 0, p = 2.2) => ({ y: y * H, rx: rx * H, rz: rz * H, z: z * H, p });
  return [
    R(0.494, 0.066 * hx, 0.05, 0.004), R(0.52, 0.087 * hx, 0.058, 0.007), R(0.553, 0.099 * hx, 0.062, 0.007),
    R(0.59, 0.095 * hx, 0.06 + bl * 0.3, 0.004 - bl * 0.3), R(0.625, 0.087 * wx, 0.057 + bl * 0.8, -bl * 0.8),
    R(0.66, 0.089 * wx, 0.058 + bl, -0.002 - bl), R(0.70, 0.093 * sx, 0.062 + bl * 0.5, -0.003 - bl * 0.5),
    R(0.74, 0.099 * sx, f ? 0.069 : 0.066, f ? -0.011 : -0.006), R(0.775, 0.103 * sx, f ? 0.064 : 0.065, -0.005),
    R(0.803, 0.107 * sx, 0.061, -0.001, 2.6), R(0.822, 0.101 * sx, 0.054, 0.002, 2.8), R(0.840, 0.07 * sx, 0.044, 0.004, 2.4),
    R(0.852, f ? 0.036 : 0.04, 0.036, 0.005),
  ];
}

// Head rows: [f (0 chin .. 1 crown), rx, rz, z] in units of head height.
const HEAD = [[0, 0.1, 0.1, -0.24], [0.05, 0.19, 0.2, -0.17], [0.12, 0.27, 0.32, -0.1], [0.17, 0.29, 0.36, -0.075], [0.2, 0.3, 0.38, -0.06],
  [0.3, 0.315, 0.41, -0.04], [0.42, 0.33, 0.425, -0.03], [0.46, 0.332, 0.428, -0.028], [0.5, 0.335, 0.43, -0.025], [0.535, 0.337, 0.432, -0.022],
  [0.565, 0.34, 0.435, -0.02], [0.6, 0.34, 0.435, -0.017], [0.7, 0.335, 0.43, -0.01], [0.82, 0.3, 0.39, 0], [0.92, 0.22, 0.29, 0.01], [1, 0.03, 0.04, 0.01]];
// Hair: [f, thickness, half arc around the back (PI = full ring)] per style. hang: falls straight below the skull.
const HAIR = {
  short: { rows: [[0.28, 0.03, 1.0], [0.38, 0.035, 1.55], [0.5, 0.04, 2.0], [0.6, 0.05, 2.3], [0.68, 0.06, 2.6], [0.76, 0.065, PI], [0.86, 0.07, PI], [0.94, 0.06, PI], [1, 0.05, PI]] },
  tied: { rows: [[0.22, 0.03, 1.2], [0.36, 0.04, 1.7], [0.5, 0.05, 2.1], [0.6, 0.055, 2.4], [0.7, 0.06, 2.7], [0.74, 0.06, PI], [0.86, 0.065, PI], [0.94, 0.06, PI], [1, 0.05, PI]] },
  perm: { pbr: PBR.perm, rows: [[0.3, 0.06, 1.3], [0.42, 0.1, 1.9], [0.55, 0.12, 2.3], [0.66, 0.13, 2.65], [0.72, 0.13, PI], [0.84, 0.13, PI], [0.94, 0.11, PI], [1, 0.09, PI]] },
  bob: { hang: true, rows: [[0.08, 0.09, 1.85], [0.2, 0.1, 1.95], [0.35, 0.095, 2.05], [0.5, 0.085, 2.2], [0.6, 0.08, 2.45], [0.62, 0.08, PI], [0.72, 0.08, PI], [0.84, 0.085, PI], [0.94, 0.07, PI], [1, 0.06, PI]] },
  long: { hang: true, rows: [[-1.05, 0.0, 0.9], [-0.95, 0.0, 1.2], [-0.5, 0.0, 1.35], [-0.2, 0.0, 1.55], [0.08, 0.09, 1.85], [0.2, 0.1, 1.95], [0.35, 0.095, 2.05], [0.5, 0.085, 2.2], [0.6, 0.08, 2.45], [0.62, 0.08, PI], [0.72, 0.08, PI], [0.84, 0.085, PI], [0.94, 0.07, PI], [1, 0.06, PI]] },
};
// Below-chin hair rows hug the back: [f] -> { rx, rz, z } in head units.
const LONG_BACK = { '-1.05': [0.22, 0.12, 0.34], '-0.95': [0.3, 0.17, 0.33], '-0.5': [0.34, 0.2, 0.3], '-0.2': [0.33, 0.27, 0.18] };

const LEG = [[0.555, 0.052], [0.5, 0.05], [0.42, 0.043], [0.335, 0.034], [0.285, 0.031, -0.002], [0.25, 0.033, 0.004], [0.2, 0.033, 0.007], [0.13, 0.025, 0.003], [0.075, 0.0195], [0.05, 0.018]];
const ARM = [[0.022, 0.008], [0.015, 0.021], [0, 0.028], [-0.02, 0.029], [-0.06, 0.026], [-0.12, 0.0235]];
const HAND = [[0, 0.0105, 0.015, 0], [-0.017, 0.0125, 0.0225, -0.001], [-0.046, 0.0115, 0.0255, -0.0015], [-0.064, 0.0092, 0.0238, -0.0035], [-0.087, 0.0068, 0.021, -0.0093], [-0.1, 0.0045, 0.016, -0.015]];

const LEN = { hip: 0.515, waist: 0.56, thigh: 0.45, knee: 0.3, calf: 0.2 };

// Build the body into a skinned builder; returns { b, d, J, rows } so props can be baked in before b.build().
export function buildBody(spec) {
  const d = dims(spec), J = joints(d), H = d.H, hh = d.hh, b = builder(true);
  const skin = col(spec.skin), hairC = col(spec.hair?.c ?? '#1c1814');
  const top = spec.top, outer = spec.outer, bot = spec.bottom, ex = spec.extras ?? {};
  const pb = (n) => (Array.isArray(n) ? n : PBR[n] ?? PBR.fabric);
  const rows = torsoRows(d, spec), ts = torsoSkin(d);
  const front = (y, t = 0) => { const r = ringAt(rows, y); return r.z - r.rz - t; };

  // ---- torso base layer: bottom garment below the top's hem, optional belt, top above ----
  const tucked = top.kind === 'shirt' || top.kind === 'blouse';
  const hemTop = tucked ? 0.603 * H : 0.535 * H;
  const zones = [{ y: -1, c: col(bot.c), pbr: pb(bot.pbr ?? 'fabric'), t: 0.004 * H }];
  if (tucked) zones.push({ y: 0.603 * H, c: col(bot.belt ?? '#2a221d'), pbr: PBR.leather, t: 0.005 * H });
  zones.push({ y: tucked ? 0.618 * H : hemTop, c: col(top.c), pbr: pb(top.pbr), t: 0.004 * H });
  b.loft(zoned(rows, zones), { seg: 24, cap: true, skin: ts });
  if (bot.kind === 'skirt') { // A-line skirt over the hips
    const hem = (bot.len ?? 0.33) * H;
    const sk = [{ y: hem, rx: 0.113 * H, rz: 0.074 * H, z: 0.004 * H }, { y: 0.49 * H, rx: 0.106 * H, rz: 0.069 * H, z: 0.006 * H },
      { y: 0.553 * H, rx: 0.102 * H, rz: 0.066 * H, z: 0.007 * H }, { y: 0.61 * H, rx: 0.092 * H * d.g, rz: 0.062 * H, z: 0.002 * H }];
    b.loft(sk.map(r => ({ ...r, p: 2.1 })), { seg: 24, color: col(bot.c), pbr: pb(bot.pbr ?? 'fabric'), skin: skirtSkin(d, hem) });
  }
  if (top.kind === 'shirt' && !(outer && outer.kind !== 'vest' && !outer.open)) // shirt collar
    b.loft([{ y: 0.842 * H, rx: 0.046 * H, rz: 0.042 * H, z: 0.004 * H, a: PI - 0.42 }, { y: 0.866 * H, rx: 0.042 * H, rz: 0.04 * H, z: 0.008 * H, a: PI - 0.5 }], { seg: 16, color: col(top.collar ?? top.c), pbr: pb(top.pbr), skin: ts });
  if (top.kind === 'hoodie') {
    b.blob([0, 0.852 * H, 0.05 * H], [0.07 * H, 0.032 * H, 0.036 * H], { color: col(top.c), pbr: PBR.knit, skin: ts, seg: 12 });
    for (const s of [-1, 1]) b.pipe([[s * 0.016 * H, 0.842 * H, front(0.84 * H, 0.006 * H)], [s * 0.017 * H, 0.79 * H, front(0.79 * H, 0.006 * H)]], 0.0022 * H, { color: col('#e8e4dc'), skin: B.chest, seg: 5 });
  }

  // ---- outer garment ----
  if (outer) {
    const hem = (LEN[outer.len] ?? LEN.hip) * H, t = (outer.kind === 'padded' ? 0.02 : outer.kind === 'coat' ? 0.012 : 0.008) * H;
    const hipR = ringAt(rows, 0.553 * H), lapel = outer.kind === 'vest' || outer.kind === 'cardigan' ? 0.7 : outer.kind === 'safety' ? 0.76 : 0.765;
    const open = outer.open ? (outer.kind === 'cardigan' ? 0.38 : 0.3) : outer.kind === 'coat' ? 0.1 : 0;
    const aAt = (y) => {
      let a = PI - open;
      if (y > lapel * H) a = Math.min(a, PI - (outer.kind === 'vest' || outer.kind === 'cardigan' ? 0.25 : 0.12) - ss(lapel * H, 0.85 * H, y) * (outer.kind === 'vest' ? 0.75 : 0.55));
      if (outer.kind === 'coat' && y < 0.5 * H) a = Math.min(a, PI - 0.22);
      return a;
    };
    const oRows = [];
    if (hem < 0.494 * H) { // long coat skirt below the crotch
      for (const [y, rx, rz] of [[hem, 0.118, 0.079], [(hem + 0.494 * H) / 2, 0.114, 0.076]]) oRows.push({ y, rx: rx * H, rz: rz * H, z: 0.006 * H, p: 2.2 });
    }
    const startY = Math.max(hem, 0.494 * H);
    for (const r of [ringAt(rows, startY), ...rows.filter(r => r.y > startY && r.y < 0.85 * H)]) {
      const below = r.y < 0.553 * H;
      oRows.push({ ...r, rx: Math.max(r.rx, below ? hipR.rx * 1.04 : 0) + t, rz: Math.max(r.rz, below ? hipR.rz : 0) + t });
    }
    oRows.push({ y: 0.85 * H, rx: 0.052 * H, rz: 0.046 * H, z: 0.005 * H, p: 2.2 });
    for (const r of oRows) r.a = aAt(r.y);
    const oz = [{ y: -1, c: col(outer.c), pbr: pb(outer.pbr ?? (outer.kind === 'padded' ? 'quilt' : 'fabric')), t: 0 }];
    if (outer.kind === 'safety') for (const y of [0.635, 0.705]) oz.push({ y: y * H, c: col('#c9ced3'), pbr: PBR.reflect, t: 0.0015 * H }, { y: (y + 0.022) * H, c: col(outer.c), pbr: PBR.fluo, t: 0 });
    b.loft(zoned(oRows, oz), { seg: 24, skin: skirtSkin(d, Math.min(hem, 0.52 * H)) });
    if (outer.kind === 'jacket' || outer.kind === 'coat' || outer.kind === 'padded') // standing collar
      b.loft([{ y: 0.838 * H, rx: 0.056 * H, rz: 0.05 * H, z: 0.004 * H, a: PI - 0.55 }, { y: 0.874 * H, rx: 0.05 * H, rz: 0.047 * H, z: 0.01 * H, a: PI - 0.7 }], { seg: 16, color: col(outer.c), pbr: pb(outer.pbr ?? 'fabric'), skin: ts });
  }

  // ---- neck, head, face, hair ----
  b.loft([{ y: 0.836 * H, rx: (d.f ? 0.029 : 0.033) * H, rz: 0.031 * H, z: 0.006 * H }, { y: 0.875 * H, rx: (d.f ? 0.027 : 0.031) * H, rz: 0.03 * H, z: 0.004 * H },
    { y: 0.91 * H, rx: (d.f ? 0.027 : 0.03) * H, rz: 0.03 * H, z: 0 }], { seg: 14, color: skin, pbr: PBR.skin, skin: (p) => chain(p[1], headChain(d)) });
  const y0 = H - hh, hRow = (f) => { const r = ringAt(HEAD.map(([f0, rx, rz, z]) => ({ y: f0, rx, rz, z })), f); return { y: y0 + f * hh, rx: r.rx * hh, rz: r.rz * hh, z: r.z * hh }; };
  const lipC = skin.clone().multiply(new THREE.Color(1.06, 0.78, 0.76)), browC = skin.clone().lerp(hairC, 0.45);
  b.loft(HEAD.map(([f]) => ({ ...hRow(f), f, p: f < 0.2 ? 2.3 : 2.05 })), {
    seg: 26, pbr: PBR.skin, skin: (p) => chain(p[1], headChain(d)),
    color: (r, th) => { // faceless but with soft planes: eye sockets, brow line, lips
      const fr = -Math.cos(th); if (fr < 0.5) return skin;
      const sx = Math.abs(Math.sin(th)), k = ss(0.5, 0.85, fr);
      const c = skin.clone().multiplyScalar(1 - 0.2 * k * gauss(r.f - 0.5, 0.03) * gauss(sx - 0.4, 0.14));
      c.lerp(browC, k * gauss(r.f - 0.565, 0.025) * gauss(sx - 0.38, 0.17));
      return c.lerp(lipC, 0.7 * k * gauss(r.f - 0.185, 0.03) * gauss(sx, 0.16));
    },
  });
  const hs = (p) => chain(p[1], headChain(d));
  if (!ex.mask) b.loft([{ y: y0 + 0.29 * hh, rx: 0.04 * hh, rz: 0.03 * hh, z: -0.45 * hh }, { y: y0 + 0.31 * hh, rx: 0.065 * hh, rz: 0.07 * hh, z: -0.45 * hh },
    { y: y0 + 0.38 * hh, rx: 0.045 * hh, rz: 0.05 * hh, z: -0.445 * hh }, { y: y0 + 0.48 * hh, rx: 0.02 * hh, rz: 0.02 * hh, z: -0.43 * hh }], { seg: 8, cap: true, color: skin, pbr: PBR.skin, skin: B.head });
  for (const s of [-1, 1]) b.blob([s * 0.33 * hh, y0 + 0.45 * hh, 0.02 * hh], [0.04 * hh, 0.13 * hh, 0.08 * hh], { color: skin.clone().multiplyScalar(0.95), pbr: PBR.skin, skin: B.head, seg: 8, rings: 6 });
  hair(b, spec.hair ?? { style: 'short' }, hRow, hh, y0, hairC, hs, ex.cap);
  if (ex.cap) {
    b.loft([0.6, 0.7, 0.82, 0.92, 1].map(f => { const r = hRow(f), t = 0.075 * hh; return { ...r, rx: r.rx + t, rz: r.rz + t }; }).concat([{ y: H + 0.07 * hh, rx: 0.02 * hh, rz: 0.02 * hh, z: 0.01 * hh }]), { seg: 22, color: col(ex.cap), pbr: PBR.fabric, skin: B.head });
    b.rbox(0.78 * hh, 0.05 * hh, 0.36 * hh, 0.15 * hh, { p: 2.2, seg: 16, color: col(ex.cap).multiplyScalar(0.8), pbr: PBR.fabric, skin: B.head, m: new THREE.Matrix4().makeRotationX(-0.12).setPosition(0, y0 + 0.63 * hh, -0.6 * hh) });
  }
  if (ex.mask) b.loft([0.08, 0.15, 0.25, 0.33, 0.42, 0.47].map(f => { const r = hRow(f), t = (f > 0.25 && f < 0.45 ? 0.07 : 0.035) * hh; return { ...r, rx: r.rx + t, rz: r.rz + t, z: r.z - 0.01 * hh, a: 1.25, ac: PI }; }), { seg: 12, color: col(ex.mask), pbr: PBR.fabric, skin: B.head });
  if (ex.glasses) {
    const gc = col(ex.glasses), fz = -0.47 * hh, gy = y0 + 0.5 * hh;
    for (const s of [-1, 1]) {
      const ring = []; for (let k = 0; k <= 12; k++) { const a = 2 * PI * k / 12; ring.push([s * 0.14 * hh + Math.cos(a) * 0.1 * hh, gy + Math.sin(a) * 0.07 * hh, fz]); }
      b.pipe(ring, 0.006 * hh, { color: gc, pbr: PBR.glossy, skin: B.head, seg: 4 });
      b.pipe([[s * 0.24 * hh, gy + 0.03 * hh, fz + 0.01 * hh], [s * 0.35 * hh, gy + 0.03 * hh, -0.15 * hh], [s * 0.35 * hh, gy, 0.08 * hh]], 0.006 * hh, { color: gc, pbr: PBR.glossy, skin: B.head, seg: 4 });
    }
    b.pipe([[-0.04 * hh, gy + 0.02 * hh, fz], [0.04 * hh, gy + 0.02 * hh, fz]], 0.006 * hh, { color: gc, pbr: PBR.glossy, skin: B.head, seg: 4 });
  }

  // ---- arms and hands ----
  const sleeve = outer && outer.kind !== 'vest' && outer.kind !== 'safety' ? outer : top;
  const shortSl = !(outer && sleeve === outer) && top.sleeves === 'short';
  const rf = d.f ? 0.86 : 1;
  for (const s of [-1, 1]) {
    const a = s < 0 ? B.lArm : B.rArm, x0 = s * d.shX;
    const arm = [...ARM.map(([dy, r]) => ({ y: d.shY + dy * H, r: r * H })), { y: d.elY + 0.03 * H, r: 0.021 * H }, { y: d.elY, r: 0.0205 * H }, { y: d.elY - 0.045 * H, r: 0.0225 * H }, { y: d.wrY + 0.03 * H, r: 0.0165 * H }, { y: d.wrY - 0.004 * H, r: 0.0145 * H }]
      .map(({ y, r }) => ({ y, rx: r * rf * d.g, rz: r * rf * d.g * 1.04, x: x0 + s * 0.009 * H * ss(d.shY, d.wrY, y), p: 2 })).reverse();
    const cuff = shortSl ? d.shY - 0.075 * H : d.wrY + 0.004 * H, st = sleeve === outer ? (outer.kind === 'padded' ? 0.012 : 0.007) * H : 0.003 * H;
    const az = [{ y: -1, c: skin, pbr: PBR.skin, t: 0 }, { y: cuff, c: col(sleeve.c), pbr: pb(sleeve.pbr ?? (sleeve.kind === 'padded' ? 'quilt' : 'fabric')), t: st }];
    b.loft(zoned(arm, az), { seg: 12, cap: true, skin: (p) => chain(p[1], [{ y: d.shY - 0.01 * H, up: B.chest, dn: a, w: 0.022 * H }, { y: d.elY, up: a, dn: a + 1, w: 0.022 * H }, { y: d.wrY, up: a + 1, dn: a + 2, w: 0.01 * H }]) });
    const hx = x0 + s * 0.009 * H;
    b.loft(HAND.map(([dy, rx, rz, dx]) => ({ y: d.wrY + dy * H * rf, rx: rx * H * rf, rz: rz * H * rf, x: hx + s * dx * H * rf, p: 2.3 })).reverse(), { seg: 10, cap: true, color: skin, pbr: PBR.skin, skin: (p) => chain(p[1], [{ y: d.wrY + 0.005 * H, up: a + 1, dn: a + 2, w: 0.008 * H }]) });
    b.pipe([[hx - s * 0.004 * H, d.wrY - 0.012 * H, -0.014 * H], [hx - s * 0.009 * H, d.wrY - 0.034 * H, -0.024 * H], [hx - s * 0.014 * H, d.wrY - 0.052 * H, -0.026 * H]].map(([x, y, z]) => [x, y, z * rf]), [0.0062 * H * rf, 0.0058 * H * rf, 0.0046 * H * rf], { color: skin, pbr: PBR.skin, skin: a + 2, seg: 7 });
  }

  // ---- legs and shoes ----
  const lf = d.f ? [1.06, 0.95] : [1, 1], trousers = bot.kind !== 'skirt';
  const legC = trousers ? col(bot.c) : col(bot.legs ?? spec.skin), legP = trousers ? pb(bot.pbr ?? 'fabric') : bot.legs ? PBR.knit : PBR.skin;
  const straight = (bot.kind === 'jeans' ? 0.032 : bot.kind === 'slacks' ? 0.035 : 0.037) * H;
  for (const s of [-1, 1]) {
    const l = s < 0 ? B.lThigh : B.rThigh;
    const leg = LEG.map(([y, r, z = 0]) => {
      const yy = y * H, k = yy > d.kneeY ? lf[0] : lf[1];
      let rr = r * H * k * (0.6 + 0.4 * d.g) + (trousers ? 0.005 * H : 0.001 * H);
      if (trousers && yy < d.kneeY + 0.02 * H) rr = Math.max(rr, straight - (d.kneeY - yy) * 0.04);
      return { y: yy, rx: rr, rz: rr * 1.03, z: z * H, x: s * d.hipX * (1 - 0.07 * ss(d.hipY, d.kneeY, yy)) };
    }).reverse();
    b.loft(leg, { seg: 12, color: legC, pbr: legP, skin: (p) => chain(p[1], [{ y: d.hipY + 0.012 * H, up: B.pelvis, dn: l, w: 0.03 * H }, { y: d.kneeY, up: l, dn: l + 1, w: 0.028 * H }, { y: d.ankleY + 0.006 * H, up: l + 1, dn: l + 2, w: 0.012 * H }]) });
    shoe(b, s * d.hipX * 0.95, d, spec.shoes ?? { kind: 'sneaker', c: '#333' }, l + 2);
  }

  // ---- small accessories baked into the body ----
  const chestF = (y, t = 0.006 * H) => front(y, (outer ? (outer.kind === 'padded' ? 0.02 : 0.01) * H : 0.004 * H) + t);
  if (ex.scarf) {
    const sc = col(ex.scarf), sp = PBR.check;
    b.loft([{ y: 0.83 * H, rx: 0.06 * H, rz: 0.056 * H, z: 0.002 * H }, { y: 0.848 * H, rx: 0.062 * H, rz: 0.058 * H, z: 0.002 * H }, { y: 0.87 * H, rx: 0.05 * H, rz: 0.047 * H, z: 0.006 * H }], { seg: 18, cap: true, color: sc, pbr: sp, skin: (p) => chain(p[1], headChain(d)) });
    b.rbox(0.058 * H, 0.14 * H, 0.012 * H, 0.008 * H, { p: 4, seg: 10, color: sc, pbr: sp, skin: B.chest, m: new THREE.Matrix4().makeRotationZ(0.08).setPosition(-0.026 * H, 0.70 * H, chestF(0.76 * H, 0.012 * H)) });
  }
  if (ex.lanyard) {
    const lc = col(ex.lanyard), cz = chestF(0.735 * H);
    for (const s of [-1, 1]) b.pipe([[s * 0.03 * H, 0.858 * H, 0.025 * H], [s * 0.046 * H, 0.846 * H, -0.01 * H], [s * 0.036 * H, 0.8 * H, chestF(0.8 * H, 0.004 * H)], [s * 0.008 * H, 0.75 * H, cz]], 0.0025 * H, { color: lc, skin: B.chest, seg: 4 });
    b.rbox(0.032 * H, 0.046 * H, 0.003 * H, 0.003 * H, { p: 6, seg: 8, color: col('#f3f1ec'), pbr: PBR.plastic, skin: B.chest, m: new THREE.Matrix4().setPosition(0, 0.703 * H, cz - 0.003 * H) });
  }
  if (ex.nameTag) b.rbox(0.04 * H, 0.014 * H, 0.003 * H, 0.002 * H, { p: 6, seg: 8, color: col('#f4f2ec'), pbr: PBR.plastic, skin: B.chest, m: new THREE.Matrix4().setPosition(-0.05 * H, 0.75 * H, chestF(0.757 * H, 0.002 * H)) });
  if (ex.tie) b.loft([{ y: 0.69 * H, rx: 0.002 * H, rz: 0.002 * H }, { y: 0.7 * H, rx: 0.017 * H, rz: 0.003 * H }, { y: 0.83 * H, rx: 0.008 * H, rz: 0.003 * H }, { y: 0.842 * H, rx: 0.012 * H, rz: 0.006 * H }].map(r => ({ ...r, z: chestF(r.y, 0.001 * H) })), { seg: 8, cap: true, color: col(ex.tie), pbr: PBR.glossy, skin: B.chest });
  return { b, d, J, rows, chestF };
}

function hair(b, h, hRow, hh, y0, hairC, hs, cap) {
  const st = HAIR[h.style] ?? HAIR.short;
  if (h.style === 'bald') return;
  const rows = [];
  for (const [f, t, a] of st.rows) {
    if (cap && f > 0.62 && a >= PI) continue;
    if (f < 0) { const [rx, rz, z] = LONG_BACK[String(f)]; rows.push({ y: y0 + f * hh, rx: rx * hh, rz: rz * hh, z: z * hh, a }); continue; }
    const r = hRow(f);
    let rx = r.rx, rz = r.rz, z = r.z;
    if (st.hang && f < 0.45) { rx = Math.max(rx, 0.31 * hh); rz = Math.max(rz, 0.4 * hh); z = Math.max(z, 0.01 * hh); }
    rows.push({ y: r.y, rx: rx + t * hh, rz: rz + t * hh, z: z + (st.hang ? 0.02 * hh : 0), a });
  }
  if (!cap) rows.push({ y: y0 + (1 + 0.05) * hh + (st === HAIR.perm ? 0.08 * hh : 0), rx: 0.02 * hh, rz: 0.02 * hh, z: 0.01 * hh, a: PI });
  b.loft(rows, { seg: 22, color: (r, th) => (Math.abs(th) > 2.6 ? hairC.clone().multiplyScalar(0.9) : hairC), pbr: st.pbr ?? PBR.hair, skin: hs });
  if (h.style === 'tied') { // low ponytail
    const t = [[0, y0 + 0.55 * hh, 0.47 * hh], [0, y0 + 0.4 * hh, 0.55 * hh], [0, y0 + 0.05 * hh, 0.56 * hh], [0, y0 - 0.35 * hh, 0.5 * hh]];
    b.pipe(t, [0.07 * hh, 0.085 * hh, 0.065 * hh, 0.03 * hh], { color: hairC, pbr: PBR.hair, skin: hs, seg: 8 });
  }
}

const SHOE = { // [u along foot, half width, half height] ; sole height ; length factor
  sneaker: { rows: [[0, 0.016, 0.017], [0.05, 0.021, 0.026], [0.25, 0.024, 0.027], [0.5, 0.028, 0.021], [0.72, 0.029, 0.018], [0.9, 0.026, 0.015], [1, 0.014, 0.01]], sole: 0.009, len: 0.155, pbr: PBR.fabric },
  dress: { rows: [[0, 0.015, 0.016], [0.05, 0.02, 0.024], [0.25, 0.022, 0.025], [0.5, 0.026, 0.019], [0.72, 0.027, 0.016], [0.92, 0.021, 0.012], [1, 0.01, 0.008]], sole: 0.004, len: 0.158, pbr: PBR.leather },
  flat: { rows: [[0, 0.014, 0.012], [0.05, 0.019, 0.02], [0.25, 0.021, 0.02], [0.5, 0.025, 0.014], [0.72, 0.026, 0.012], [0.9, 0.022, 0.01], [1, 0.012, 0.007]], sole: 0.004, len: 0.148, pbr: PBR.leather },
  boot: { rows: [[0, 0.017, 0.035], [0.05, 0.022, 0.045], [0.25, 0.025, 0.05], [0.45, 0.028, 0.03], [0.72, 0.029, 0.019], [0.9, 0.026, 0.016], [1, 0.014, 0.011]], sole: 0.008, len: 0.158, pbr: PBR.leather },
};
function shoe(b, x, d, sh, bone) {
  const S = SHOE[sh.kind] ?? SHOE.sneaker, H = d.H, L = S.len * H * (d.f ? 0.97 : 1), heel = 0.22 * L;
  const up = col(sh.c), sole = col(sh.sole ?? '#1d1d1f'), so = S.sole * H;
  const rows = S.rows.map(([u, w, h]) => ({ y: u * L - heel, rx: w * H, rz: h * H, z: h * H, p: 2.6 }));
  const m = new THREE.Matrix4().makeRotationX(-PI / 2).setPosition(x, 0, 0);
  b.loft(rows, {
    seg: 12, cap: true, m, skin: bone,
    color: (r, th, i, p) => (p[2] < so ? sole : up),
    pbr: (r, th, i, p) => (p[2] < so ? PBR.rubber : S.pbr),
  });
}
