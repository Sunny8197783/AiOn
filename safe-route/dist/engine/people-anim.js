// Procedural poses. A pose is a Float32Array: per bone local Euler (x, y, z) + pelvis offset (x, y, z) at the end.
// Conventions (rest = arms hanging, facing -z, right side +x): +x flexes thighs/upper arms/elbows forward and
// tilts spine/neck/head back; knee flexion is -x; +y turns left; +z tilts the top toward -x (right-arm abduction).
import { B } from './people-body.js';

export const NB = 18, OFF = NB * 3, PLEN = OFF + 3;
const { sin, cos, max, min, PI, acos } = Math;
export const ANIMS = ['idle', 'walk', 'run', 'brisk', 'carry', 'walker', 'sit', 'sitChair', 'talk', 'phone', 'wave', 'point', 'crouch', 'stairsUp', 'stairsDown'];
// Locomotion: nominal speed (m/s), metres per full gait cycle at that speed (for a 1.72 m adult), shape.
export const GAIT = {
  walk: { v: 1.3, stride: 1.42, A: 0.36, K: 1.0, load: 0.14, As: 0.3, el: 0.2, bob: 0.022, lean: 0.03 },
  brisk: { v: 1.8, stride: 1.7, A: 0.45, K: 1.1, load: 0.16, As: 0.48, el: 0.55, bob: 0.028, lean: 0.09 },
  run: { v: 3.6, stride: 2.5, A: 0.62, K: 1.75, load: 0.3, As: 0.62, el: 1.45, bob: 0.045, lean: 0.2, run: true },
  carry: { v: 1.0, stride: 1.2, A: 0.28, K: 0.9, load: 0.15, As: 0.22, el: 0.2, bob: 0.024, lean: 0.05 },
  walker: { v: 0.8, stride: 0.95, A: 0.25, K: 0.85, load: 0.12, As: 0, el: 0.35, bob: 0.012, lean: 0.24, fixedLean: true },
  stairsUp: { v: 0.55, stride: 0.6, A: 0.42, K: 1.35, load: 0.38, As: 0.14, el: 0.25, bob: 0.03, lean: 0.16, bias: 0.35, fixedLean: true },
  stairsDown: { v: 0.6, stride: 0.62, A: 0.3, K: 0.6, load: 0.55, As: 0.12, el: 0.25, bob: 0.03, lean: -0.05, bias: 0.06, fixedLean: true },
};

const add = (P, b, x, y = 0, z = 0) => { P[b * 3] += x; P[b * 3 + 1] += y; P[b * 3 + 2] += z; };
const n2 = (t, a, b, p = 0) => sin(t * a + p) * 0.6 + sin(t * b + p * 1.7) * 0.4; // soft organic noise

// Keep the feet planted when the pelvis slides sideways by dx.
function plantX(P, c, dx) {
  const a = dx / (c.d.L1 + c.d.L2);
  for (const t of [B.lThigh, B.rThigh]) { add(P, t, 0, 0, -a); add(P, t + 2, 0, 0, a); }
}

function gait(P, g, c) {
  const { ph, amp } = c;
  for (const s of [-1, 1]) {
    const q = ph + (s > 0 ? PI : 0), th0 = s < 0 ? B.lThigh : B.rThigh;
    const sw = max(0, cos(q + 0.35));
    const th = amp * (g.A * sin(q) + 0.04) + (g.bias ?? 0) * sw * min(1, amp * 2);
    const kn = -amp * (g.K * sw ** 1.6 + g.load * max(0, sin(2 * q - PI))) - 0.04 - (g.bias ?? 0) * sw * 1.2 * min(1, amp * 2);
    const ft = -(th + kn) * 0.8 - amp * 0.35 * max(0, sin(-q)) ** 2 + amp * 0.12 * max(0, sin(q)) ** 4;
    add(P, th0, th, 0, s * 0.03); add(P, th0 + 1, kn); add(P, th0 + 2, ft, 0, -s * 0.03);
  }
  const a = amp;
  P[OFF + 1] += g.run ? -g.bob * a * (1 + cos(2 * ph)) / 2 : -g.bob * a * (1 - cos(2 * ph)) / 2 - 0.01 * a;
  const dx = a * (g.run ? 0.006 : 0.014) * cos(ph);
  P[OFF] += dx; plantX(P, c, dx);
  const py = -0.25 * g.A * a * sin(ph), lean = g.lean * (g.fixedLean ? 1 : a);
  add(P, B.pelvis, -lean * 0.3, py, 0.03 * a * sin(2 * ph));
  add(P, B.spine, -lean * 0.5, -py * 0.6); add(P, B.chest, -lean * 0.3 + (g.run ? 0.02 * sin(2 * ph) : 0), -py * 0.9);
  add(P, B.neck, lean * 0.4); add(P, B.head, lean * 0.6, py * 0.4);
  for (const s of [-1, 1]) {
    const arm = s < 0 ? B.lArm : B.rArm, f = s * g.As * a * sin(ph);
    add(P, arm, f, 0, s * (0.1 + 0.06 * a)); add(P, arm + 1, g.run ? g.el : g.el + 0.45 * max(0, f) * (g.As > 0.4 ? 1.4 : 1)); add(P, arm + 2, 0.08);
  }
}

function idle(P, c, k = 1) {
  const t = c.t + c.o * 37, br = sin(t * 1.65), sh = (sin(t * 0.31) * 0.8 + sin(t * 0.13 + 1) * 0.2) * k;
  const dx = 0.02 * sh; P[OFF] += dx; plantX(P, c, dx);
  add(P, B.pelvis, 0, 0, -0.03 * sh); add(P, B.spine, 0, 0, 0.018 * sh); add(P, B.chest, 0.012 * br, 0, 0.012 * sh);
  for (const s of [-1, 1]) { // the unweighted leg relaxes
    const th = s < 0 ? B.lThigh : B.rThigh, r = max(0, -s * sh);
    add(P, th, 0.06 * r, 0, s * 0.035); add(P, th + 1, -0.13 * r - 0.03); add(P, th + 2, 0.07 * r, 0, -s * 0.035);
    const arm = s < 0 ? B.lArm : B.rArm;
    add(P, arm, 0.04 + 0.01 * br, 0, s * (0.09 + 0.008 * br)); add(P, arm + 1, 0.16); add(P, arm + 2, 0.06);
  }
  add(P, B.neck, -0.006 * br); add(P, B.head, -0.03 + 0.03 * sin(t * 0.17) * k, 0.22 * sin(t * 0.21) * sin(t * 0.083 + 1) * k);
}

// Leg angles for a seated/crouched pose: thigh flex th, hip-joint height hy; ankle at ankle height.
// back: shins slope back from the knees (squat) instead of forward (sitting).
function legsFold(P, c, th, hy, abd = 0.12, back = false) {
  const d = c.d, cc = (back ? -1 : 1) * acos(Math.min(1, Math.max(-1, (hy - d.L1 * cos(th) - d.ankleY) / d.L2)));
  for (const s of [-1, 1]) { const t = s < 0 ? B.lThigh : B.rThigh; add(P, t, th, 0, s * abd); add(P, t + 1, cc - th); add(P, t + 2, -cc, 0, -s * abd); }
  P[OFF + 1] += hy - d.hipY;
  return d.L1 * sin(th) + d.L2 * sin(cc); // how far the ankles end up in front of the hips
}

const POSES = {
  idle,
  walk: (P, c) => gait(P, GAIT.walk, c),
  brisk: (P, c) => gait(P, GAIT.brisk, c),
  run: (P, c) => { gait(P, GAIT.run, c); add(P, B.head, -0.05); },
  stairsUp: (P, c) => gait(P, GAIT.stairsUp, c),
  stairsDown: (P, c) => { gait(P, GAIT.stairsDown, c); add(P, B.lArm, 0, 0, -0.1); add(P, B.rArm, 0, 0, 0.1); },
  carry(P, c) { // heavy container in the right hand: lean away, free arm out for balance
    gait(P, GAIT.carry, c); if (c.amp < 0.05) idle(P, c, 0.4);
    const sw = c.amp * sin(c.ph);
    P[B.rArm * 3] = 0.05 * sw; P[B.rArm * 3 + 2] = 0.2; P[B.rFore * 3] = 0.05;
    P[B.lArm * 3 + 2] = -0.36; add(P, B.lFore, 0.15);
    add(P, B.spine, 0, 0, 0.1); add(P, B.chest, 0, 0, 0.05); add(P, B.head, 0, 0, -0.12);
    P[OFF] += 0.02; plantX(P, c, 0.02);
  },
  walker(P, c) { gait(P, GAIT.walker, c); if (c.amp < 0.05) idle(P, c, 0.25); add(P, B.head, -0.08); },
  talk(P, c) {
    idle(P, c, 0.6);
    const t = c.t + c.o * 23, g1 = 0.5 + 0.5 * n2(t, 1.3, 0.7), g2 = 0.5 + 0.5 * n2(t, 0.9, 1.6, 2);
    add(P, B.rArm, 0.22 + 0.22 * g1 + 0.05 * sin(t * 2.9), 0, 0.05); add(P, B.rFore, 0.95 + 0.4 * g1 + 0.12 * sin(t * 3.7 + 2), -0.5 - 0.35 * g1); add(P, B.rHand, 0.12 * sin(t * 2.3 + 1));
    add(P, B.lArm, 0.12 + 0.16 * g2, 0, -0.04); add(P, B.lFore, 0.55 + 0.45 * g2, 0.3 * g2);
    add(P, B.chest, 0, 0.07 * n2(t, 0.9, 0.4, 2)); add(P, B.head, 0.045 * sin(t * 2.1 + 0.5), 0.1 * n2(t, 0.6, 0.25, 1));
  },
  phone(P, c) {
    idle(P, c, 0.5);
    const t = c.t + c.o * 11, tap = 0.06 * max(0, sin(t * 7.5)) * (sin(t * 0.8) > 0 ? 1 : 0);
    add(P, B.neck, -0.18); add(P, B.head, -0.4);
    P[B.rArm * 3] = 0.34; P[B.rArm * 3 + 2] = -0.13; P[B.rFore * 3] = 1.6; P[B.rFore * 3 + 1] = -1.25; P[B.rHand * 3] = 0.1 + tap;
  },
  wave(P, c) {
    idle(P, c, 0.5);
    const t = c.t * 6.8;
    P[B.rArm * 3] = 0.25; P[B.rArm * 3 + 2] = 2.45 + 0.12 * sin(t);
    P[B.rFore * 3] = 0.3; P[B.rFore * 3 + 1] = -1.4; P[B.rFore * 3 + 2] = 0.32 * sin(t + 0.6); P[B.rHand * 3] = 0.1;
    add(P, B.spine, 0, 0, 0.04); add(P, B.head, 0.04, -0.05);
  },
  point(P, c) {
    idle(P, c, 0.4);
    P[B.rArm * 3] = 1.42 + 0.05 * sin(c.t * 2.2); P[B.rArm * 3 + 2] = 0.18; P[B.rFore * 3] = 0.08; P[B.rFore * 3 + 1] = -0.3;
    add(P, B.chest, 0, -0.12); add(P, B.head, 0.03, -0.15);
  },
  crouch(P, c) {
    const d = c.d, th = 1.9, ccw = -0.35, hy = d.ankleY + d.L1 * cos(th) + d.L2 * cos(ccw);
    const fwd = legsFold(P, c, th, hy, 0.2, true);
    P[OFF + 2] += fwd; // feet stay at the origin, hips go back
    add(P, B.spine, -0.42); add(P, B.chest, -0.15); add(P, B.neck, 0.15); add(P, B.head, 0.32);
    for (const s of [-1, 1]) { const a = s < 0 ? B.lArm : B.rArm; add(P, a, 0.85, 0, s * 0.18); add(P, a + 1, 0.55); }
    add(P, B.chest, 0.01 * sin(c.t * 1.65));
  },
  sit(P, c) { // on a floor mat, knees up; origin = under the hips. Hands rest on the knees (IK, see people.js)
    legsFold(P, c, 2.0, 0.058 * c.d.H, 0.2);
    const t = c.t + c.o * 19;
    add(P, B.pelvis, -0.12); add(P, B.lThigh, 0.12); add(P, B.rThigh, 0.12); // tilt the hips, not the planted legs
    add(P, B.spine, -0.08, 0, 0.02 * sin(t * 0.2)); add(P, B.chest, -0.05 + 0.012 * sin(t * 1.5));
    add(P, B.head, 0.06 + 0.04 * sin(t * 0.15), 0.3 * sin(t * 0.11) * sin(t * 0.05 + 2));
    for (const s of [-1, 1]) { const a = s < 0 ? B.lArm : B.rArm; add(P, a, 0.7, 0, s * 0.1); add(P, a + 1, 0.6); }
  },
  sitChair(P, c) { // origin = centre of the seat on the floor; seat height c.seat
    legsFold(P, c, 1.5, c.seat + 0.04 * c.d.H, 0.1);
    const t = c.t + c.o * 7, k = 0.5 + 0.5 * sin(t * 0.4);
    add(P, B.spine, 0.04); add(P, B.chest, 0.01 * sin(t * 1.6)); add(P, B.head, -0.12 + 0.05 * sin(t * 0.13), 0.15 * sin(t * 0.09));
    for (const s of [-1, 1]) {
      const a = s < 0 ? B.lArm : B.rArm;
      add(P, a, 0.3, 0, s * 0.08); add(P, a + 1, 1.15 + 0.04 * k * sin(t * (s < 0 ? 5 : 6.3)), s * 0.25); add(P, a + 2, -0.1);
    }
  },
};

// Additive posture for age (stoop 0..0.3).
function posture(P, stoop) {
  if (!stoop) return;
  add(P, B.spine, -stoop * 0.5); add(P, B.chest, -stoop * 0.6); add(P, B.neck, -stoop * 0.2); add(P, B.head, stoop * 0.85);
  for (const t of [B.lThigh, B.rThigh]) { add(P, t, stoop * 0.12); add(P, t + 1, -stoop * 0.22); add(P, t + 2, stoop * 0.1); }
  P[OFF + 1] -= stoop * 0.02;
}

// Fill P with the pose of `name`. c = { t, ph, amp, d, o, seat, stoop }.
export function pose(name, P, c) {
  P.fill(0);
  (POSES[name] ?? idle)(P, c);
  posture(P, c.stoop);
  return P;
}

// Which hands hold something with IK this frame: 'walker' | 'cart' | 'knees' | null.
export function gripOf(name, props) {
  if (name === 'sit') return 'knees';
  if (name === 'sitChair' || name === 'crouch' || name === 'phone' || name === 'wave' || name === 'point' || name === 'carry') return null;
  if (props.walker) return 'walker';
  if (props.cart) return 'cart';
  return null;
}

// Gait phase speed and amplitude for an animation at a given speed and person height.
export function gaitStep(name, speed, H) {
  const g = GAIT[name]; if (!g) return { rate: 0, amp: 0 };
  const v = speed ?? g.v, r = Math.max(0, v) / g.v;
  const stride = g.stride * (H / 1.72) * Math.max(0.45, Math.sqrt(Math.min(1.6, r)));
  return { rate: 2 * PI * v / stride, amp: Math.min(1.25, Math.sqrt(r)) };
}
