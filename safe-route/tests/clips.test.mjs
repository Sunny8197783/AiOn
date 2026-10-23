import test from 'node:test';
import assert from 'node:assert/strict';
import { kf, clip, clipLook, maskOf, CLIP_NAMES } from '../dist/engine/people-clips.js';
import { PLEN, OFF } from '../dist/engine/people-anim.js';
import { dims } from '../dist/engine/people-body.js';

test('keyframe tracks hold their ends and pass through every key', () => {
  const keys = [[0, 1], [0.4, 3], [1, -1]];
  assert.equal(kf(-1, keys), 1); assert.equal(kf(0.4, keys), 3); assert.equal(kf(2, keys), -1);
  const mid = kf(0.2, keys); assert.ok(mid > 1 && mid < 3);
});

test('upper-body clips leave the legs and pelvis to the gait; every clip yields finite poses', () => {
  const c = { t: 1, ph: 0.5, amp: 0.8, d: dims({ H: 1.72, sex: 'm' }), o: 0.3, seat: 0.45, stoop: 0, power: 0.7 };
  for (const name of CLIP_NAMES) for (const k of [0, 0.3, 0.6, 1]) {
    const P = new Float32Array(PLEN).fill(0.123), out = clip(name, P.slice(), c, k);
    assert.ok(out.every(Number.isFinite), `${name} at ${k}`);
    const m = maskOf(name);
    if (m) for (let i = 0; i < OFF + 3; i++) if (!m.includes(Math.floor(i / 3)) || i >= OFF) assert.equal(out[i], P[i], `${name} touched ${i}`);
  }
  assert.ok(clipLook('pickup', 0.45) < -0.5); assert.equal(clipLook('nope', 0.5), 0);
});
