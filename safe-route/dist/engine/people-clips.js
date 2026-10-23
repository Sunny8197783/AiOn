// One-shot action clips layered over whatever the legs are doing: each is keyed like hand animation —
// anticipation, the action, follow-through, recovery — and eased between keys. A clip writes only the bones it
// names ('upper' clips leave the legs to the gait; 'full' clips own the whole body, pelvis included).
// Optional look track: the pitch the eyes take during the clip (the first-person camera follows it).
// clip(name, P, c, k): P is filled from the base pose already (same layout as people-anim.js), k = 0..1.
// Conventions as in people-anim.js: +x flexes arms/elbows/thighs forward and tilts spine/neck/head back,
// knee flexion is -x, +y turns left (a right-hand strike drives the chest toward +y), +z tilts toward -x.
import { B } from './people-body.js';
import { pose, OFF } from './people-anim.js';

const ss = (x) => x * x * (3 - 2 * x);
// Keyframe track: [[t, v], ...] sorted by t; eased between neighbours, held at the ends.
function kf(k, keys) {
  if (k <= keys[0][0]) return keys[0][1];
  for (let i = 1; i < keys.length; i++) {
    const [t1, v1] = keys[i];
    if (k <= t1) { const [t0, v0] = keys[i - 1]; return v0 + (v1 - v0) * ss((k - t0) / (t1 - t0)); }
  }
  return keys[keys.length - 1][1];
}
const T = (times, vals) => times.map((t, i) => [t, vals[i]]);
// Set bone b's rotation from three tracks (x, y, z), each a values list over the clip's key times (or a number).
function set(P, b, k, times, x = 0, y = 0, z = 0) {
  const v = (a) => (Array.isArray(a) ? kf(k, T(times, a)) : a);
  P[b * 3] = v(x); P[b * 3 + 1] = v(y); P[b * 3 + 2] = v(z);
}
const add = (P, b, x, y = 0, z = 0) => { P[b * 3] += x; P[b * 3 + 1] += y; P[b * 3 + 2] += z; };
const tmp = new Float32Array(OFF + 3);

const UPPER = [B.spine, B.chest, B.neck, B.head, B.lArm, B.lFore, B.lHand, B.rArm, B.rFore, B.rHand];
const CLIPS = {
  // Right straight: weight back and the shoulder turned away, then the hip and chest drive the fist out along
  // the line of sight, a beat at full reach, back to guard. The left hand stays up by the face throughout.
  punch: { mask: 'upper', dur: 0.42, look: [[0, 0], [0.46, -0.04], [1, 0]], f(P, c, k) {
    const t = [0, 0.28, 0.46, 0.62, 1];
    set(P, B.rArm, k, t, [0.35, 0.6, 1.68, 1.6, 0.2], 0, [0.18, 0.32, 0.02, 0.05, 0.12]);
    set(P, B.rFore, k, t, [1.3, 2.1, 0.06, 0.2, 0.35], [0, -0.3, -0.1, -0.1, 0]);
    set(P, B.rHand, k, t, 0, 0, 0);
    set(P, B.lArm, k, t, [0.55, 0.75, 0.7, 0.7, 0.15], 0, [-0.22, -0.28, -0.25, -0.25, -0.12]);
    set(P, B.lFore, k, t, [1.6, 2.0, 1.95, 1.9, 0.4], [0, 0.35, 0.3, 0.3, 0]);
    set(P, B.spine, k, t, [0, 0.03, -0.1, -0.08, 0], [0, -0.14, 0.18, 0.15, 0]);
    set(P, B.chest, k, t, [0, 0.02, -0.06, -0.05, 0], [0, -0.3, 0.38, 0.32, 0]);
    set(P, B.neck, k, t, 0, [0, 0.2, -0.25, -0.2, 0]);
    set(P, B.head, k, t, [0, 0, 0.05, 0.04, 0], [0, 0.24, -0.3, -0.27, 0]);
  } },
  // Overhand throw: the off hand points at the target while the arm cocks back behind the head and the chest
  // turns away; then hips, chest and arm whip through in order, the hand releases high in front and follows
  // through across the body.
  throw: { mask: 'upper', dur: 0.6, f(P, c, k) {
    const t = [0, 0.38, 0.52, 0.7, 1];
    set(P, B.rArm, k, t, [0.3, 2.55, 1.85, 0.75, 0.2], 0, [0.12, 0.6, 0.3, -0.25, 0.1]);
    set(P, B.rFore, k, t, [0.4, 1.9, 0.35, 0.5, 0.3], [0, -0.4, 0, 0.2, 0]);
    set(P, B.rHand, k, t, [0, -0.5, 0.3, 0.2, 0]);
    set(P, B.lArm, k, t, [0.1, 1.25, 0.8, 0.25, 0.1], 0, [-0.1, -0.15, -0.35, -0.3, -0.1]);
    set(P, B.lFore, k, t, [0.3, 0.25, 0.9, 1.1, 0.3]);
    set(P, B.spine, k, t, [0, 0.12, -0.12, -0.22, 0], [0, -0.25, 0.12, 0.28, 0]);
    set(P, B.chest, k, t, [0, 0.1, -0.05, -0.12, 0], [0, -0.45, 0.3, 0.52, 0]);
    set(P, B.neck, k, t, 0, [0, 0.3, -0.18, -0.35, 0]);
    set(P, B.head, k, t, [0, -0.05, 0.05, 0.08, 0], [0, 0.35, -0.22, -0.4, 0]);
  } },
  // Bend down and pick something off the ground in front of the right foot: hips back and down, back fairly
  // flat, the right hand reaches the floor, closes, and the body comes back up.
  pickup: { mask: 'full', dur: 0.8, look: [[0, 0], [0.4, -1.05], [0.6, -0.95], [1, 0]], f(P, c, k) {
    const bend = kf(k, [[0, 0], [0.42, 1], [0.58, 1], [1, 0]]);
    pose('crouch', tmp, c);
    for (let i = 0; i < tmp.length; i++) P[i] += (tmp[i] - P[i]) * bend * 0.85;
    const t = [0, 0.42, 0.55, 0.7, 1];
    add(P, B.spine, kf(k, T(t, [0, -0.35, -0.38, -0.2, 0])));
    add(P, B.chest, kf(k, T(t, [0, -0.1, -0.1, -0.05, 0])), kf(k, T(t, [0, 0.15, 0.15, 0.05, 0])));
    set(P, B.rArm, k, t, [0.1, 1.35, 1.25, 0.7, 0.15], 0, [0.1, 0.02, 0.02, 0.1, 0.1]);
    set(P, B.rFore, k, t, [0.2, 0.15, 0.45, 0.9, 0.25]);
    set(P, B.rHand, k, t, [0, 0.25, 0.5, 0.3, 0]);
    set(P, B.lArm, k, t, [0.05, 0.55, 0.55, 0.3, 0.05], 0, [-0.1, -0.25, -0.25, -0.15, -0.1]);
    set(P, B.lFore, k, t, [0.2, 0.7, 0.7, 0.4, 0.2]);
    add(P, B.head, kf(k, T(t, [0, 0.25, 0.25, 0.1, 0])));
  } },
  // Vault over a low wall: both hands go down onto the top, the body leans over them, the knees tuck up to the
  // side, then the legs swing through and reach for the ground on the far side; land soft.
  vault: { mask: 'full', dur: 0.6, look: [[0, 0], [0.22, -0.6], [0.5, -0.35], [0.85, 0.05], [1, 0]], f(P, c, k) {
    const t = [0, 0.22, 0.5, 0.75, 1];
    for (const s of [-1, 1]) {
      const a = s < 0 ? B.lArm : B.rArm, th = s < 0 ? B.lThigh : B.rThigh;
      set(P, a, k, t, [0.4, 1.25, 0.65, 0.3, 0.2], 0, [s * 0.15, s * 0.12, s * 0.35, s * 0.55, s * 0.2]);
      set(P, a + 1, k, t, [0.6, 0.25, 0.15, 0.35, 0.3]);
      set(P, a + 2, k, t, [0, -0.6, -0.4, 0, 0]);
      set(P, th, k, t, [0.2, 0.6, s < 0 ? 1.7 : 1.45, 0.55, 0.15], 0, [0, 0, s < 0 ? -0.05 : 0.25, 0, 0]);
      set(P, th + 1, k, t, [-0.3, -0.9, -2.0, -0.5, -0.35]);
      set(P, th + 2, k, t, [0.1, 0.3, 0.4, 0.1, 0.15]);
    }
    set(P, B.spine, k, t, [-0.1, -0.45, -0.3, -0.05, -0.08], 0, [0, 0, 0.12, 0.04, 0]);
    set(P, B.chest, k, t, [0, -0.15, -0.1, 0, 0]);
    set(P, B.neck, k, t, [0, 0.15, 0.1, 0, 0]);
    set(P, B.head, k, t, [0, 0.3, 0.2, 0.05, 0]);
    P[OFF + 1] = kf(k, T(t, [-0.05, -0.08, 0.05, 0, -0.08]));
  } },
  // Climb onto something chest-high: reach up, hands on the edge, pull while one knee comes up onto it, stand.
  climb: { mask: 'full', dur: 0.8, look: [[0, 0], [0.25, 0.2], [0.6, -0.3], [1, 0]], f(P, c, k) {
    const t = [0, 0.25, 0.55, 0.8, 1];
    for (const s of [-1, 1]) {
      const a = s < 0 ? B.lArm : B.rArm, th = s < 0 ? B.lThigh : B.rThigh, lead = s > 0;
      set(P, a, k, t, [0.3, 2.5, 1.0, 0.4, 0.15], 0, [s * 0.1, s * 0.2, s * 0.35, s * 0.25, s * 0.1]);
      set(P, a + 1, k, t, [0.4, 0.3, 1.6, 0.9, 0.3]);
      set(P, th, k, t, [0.1, 0.25, lead ? 1.9 : 0.4, lead ? 0.9 : 0.2, 0.05]);
      set(P, th + 1, k, t, [-0.2, -0.3, lead ? -2.1 : -0.6, lead ? -1.2 : -0.3, -0.08]);
      set(P, th + 2, k, t, [0.1, 0.15, lead ? 0.3 : 0.2, 0.2, 0]);
    }
    set(P, B.spine, k, t, [0, 0.05, -0.4, -0.2, 0]);
    set(P, B.chest, k, t, [0, 0.1, -0.15, -0.05, 0]);
    set(P, B.head, k, t, [0, -0.25, 0.2, 0.1, 0]);
  } },
  // Landing: the legs take the drop — knees and hips fold, arms come forward for balance — and straighten.
  // c.power 0..1 scales how deep (a hop vs. a two-storey drop).
  land: { mask: 'full', dur: 0.4, look: [[0, 0], [0.25, -0.18], [1, 0]], f(P, c, k) {
    const w = kf(k, [[0, 0.2], [0.22, 1], [0.5, 0.6], [1, 0]]) * (0.35 + 0.65 * (c.power ?? 0.5));
    pose('crouch', tmp, c);
    for (let i = 0; i < tmp.length; i++) P[i] += (tmp[i] - P[i]) * w;
    for (const s of [-1, 1]) { const a = s < 0 ? B.lArm : B.rArm; add(P, a, 0.5 * w, 0, s * 0.35 * w); }
  } },
  // Hit: the head and chest snap back and away, the arms come up, then settle.
  hurt: { mask: 'upper', dur: 0.45, look: [[0, 0], [0.15, 0.12], [1, 0]], f(P, c, k) {
    const t = [0, 0.15, 0.4, 1];
    set(P, B.spine, k, t, [0, 0.25, 0.15, 0], [0, 0.12, 0.06, 0]);
    set(P, B.chest, k, t, [0, 0.18, 0.1, 0], [0, 0.15, 0.08, 0]);
    set(P, B.neck, k, t, [0, 0.2, 0.1, 0]);
    set(P, B.head, k, t, [0, 0.35, 0.15, 0], [0, 0.3, 0.1, 0], [0, 0.15, 0.05, 0]);
    for (const s of [-1, 1]) {
      const a = s < 0 ? B.lArm : B.rArm;
      set(P, a, k, t, [0.1, 0.9, 0.7, 0.1], 0, [s * 0.1, s * 0.3, s * 0.25, s * 0.1]);
      set(P, a + 1, k, t, [0.2, 1.7, 1.5, 0.3]);
    }
  } },
};

export const CLIP_NAMES = Object.keys(CLIPS);
export const clipInfo = (name) => CLIPS[name] && { mask: CLIPS[name].mask, dur: CLIPS[name].dur };
// Bones a clip writes: everything for 'full', the torso and arms for 'upper'.
export const maskOf = (name) => (CLIPS[name].mask === 'full' ? null : UPPER);
// Where the eyes go during a clip (radians of pitch, - = down): the first-person camera follows it.
export const clipLook = (name, k, power = 1) => (CLIPS[name]?.look ? kf(Math.min(1, Math.max(0, k)), CLIPS[name].look) * (name === 'land' ? power : 1) : 0);
export function clip(name, P, c, k) { CLIPS[name].f(P, c, Math.min(1, Math.max(0, k))); return P; }
export { kf };
