// r6 fixer 4: light/grade regressions (V2 ultralow town night, V3 day shade fill, V5 interiorNight blow-out).
import test from 'node:test';
import assert from 'node:assert/strict';
import { PROFILES } from '../dist/engine/quality.js';
import { PRESETS } from '../dist/engine/presets.js';

test('V2: 완전 낮음 / 낮음 keep the 103동 lot lamp (canopy + stops[0] + stops[1] in town-props)', () => {
  assert.ok(PROFILES.ultralow.keyLights >= 3 && PROFILES.low.keyLights >= 3);
});
test('V3: the day shade has fill (hemi and env above the old crushed values)', () => {
  assert.ok(PRESETS.day.hemi[2] >= 0.45 && PRESETS.day.envI >= 0.65 && PRESETS.day.grade.contrast <= 1.1);
});
test('V5: interiorNight is not exposed above the day interior and blooms only real emitters', () => {
  const N = PRESETS.interiorNight, D = PRESETS.interior;
  assert.ok(N.exposure + (N.grade.exposure ?? 0) <= D.exposure + 0.1 + (D.grade.exposure ?? 0));
  assert.ok(N.bloom[2] >= 1.25 && N.bloom[0] <= 0.3);
});
