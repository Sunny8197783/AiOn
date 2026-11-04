import test from 'node:test';
import assert from 'node:assert/strict';
import { STAIR, RISER, STEPS, tread, flightRamps, rampY, leafBoxes } from '../dist/levels/apartment-geo.js';

const near = (a, b, eps = 1e-9) => Math.abs(a - b) < eps;

test('every tread of both lanes sits on its walk ramp (lane B starts at the mid landing)', () => {
  for (const y0 of [0, 2.8, 5.6]) {
    const [ra, rb] = flightRamps(y0);
    for (let i = 0; i < STEPS; i++) {
      for (const [lane, w] of [['A', ra], ['B', rb]]) {
        const t = tread(y0, lane, i), zc = (t.z0 + t.z1) / 2;
        assert.ok(zc >= w.z0 - 1e-9 && zc <= w.z1 + 1e-9, `lane ${lane} tread ${i} centre inside its ramp`);
        assert.ok(near(rampY(w, zc), t.top, 1e-6), `y0 ${y0} lane ${lane} tread ${i}: ramp ${rampY(w, zc)} vs tread ${t.top}`);
      }
    }
    // lane A ends on the mid landing, lane B on the next floor
    assert.ok(near(tread(y0, 'A', STEPS - 1).top, y0 + 1.4) && near(tread(y0, 'B', STEPS - 1).top, y0 + 2.8));
    assert.ok(near(tread(y0, 'B', 0).top, y0 + 1.4 + RISER), 'lane B starts one riser above the mid landing');
  }
});

test('the flights stay between the floor landing and the mid landing', () => {
  for (const lane of ['A', 'B']) for (let i = 0; i < STEPS; i++) {
    const t = tread(0, lane, i);
    assert.ok(t.z0 >= STAIR.zLand - 1e-9 && t.z1 <= STAIR.zMid + 1e-9, `lane ${lane} tread ${i} z ${t.z0}..${t.z1}`);
  }
});

test('door leaf boxes follow the leaf and leave the hinge corner to the wall', () => {
  const pad = 0.03, lw = 0.92;
  const closed = leafBoxes(0, 0, lw, 0, 0, pad);          // closed along +x
  assert.ok(near(closed[0].x0, 0.17 * lw - pad) && near(closed[2].x1, lw + pad));
  for (const b of closed) assert.ok(near(b.z0, -pad) && near(b.z1, pad));
  const open = leafBoxes(0, 0, lw, 0, -Math.PI / 2, pad); // swung 90° toward +z
  for (const b of open) { assert.ok(b.x0 > -pad - 1e-9 && b.x1 < pad + 1e-9, 'flat against the normal'); assert.ok(b.z0 >= 0.17 * lw - pad - 1e-9); }
  assert.ok(near(open[2].z1, lw + pad));
  const diag = leafBoxes(1, 2, 0, lw, 1.0, pad);         // an 'x'-axis door half open: boxes hug the diagonal
  for (const b of diag) assert.ok((b.x1 - b.x0) < lw / 2 && (b.z1 - b.z0) < lw / 2, 'each third is small');
});
