import test from 'node:test';
import assert from 'node:assert/strict';
import { camTop, headroomAt } from '../dist/engine/space.js';

// A stairwell column: floor 0, a landing at 2.8 (underside 2.6) and the next one at 7.0 (underside 6.8).
const L = {
  id: 'stairs', env: 'interior',
  walk: [{ x0: 0, x1: 4, z0: 0, z1: 2, y: 0 }, { x0: 0, x1: 4, z0: 0, z1: 2, y: 2.8 }, { x0: 0, x1: 4, z0: 0, z1: 2, y: 7.0 }],
  ceilings: [{ x0: 0, x1: 4, z0: 0, z1: 2, y: 2.6 }, { x0: 0, x1: 4, z0: 0, z1: 2, y: 6.8 }],
};

test('third-person camera: walking down from a landing it is not clamped under that landing', () => {
  // feet 2.08 on the flight below, head 3.78: the camera behind still stands over the 2.8 landing
  assert.equal(camTop(L, null, 1, 1, 2.08, 3.78), 6.8 - 0.15);
  // headroom for vaults and jumps is unchanged: the lowest ceiling over feet + 0.5
  assert.equal(headroomAt(L, 1, 1, 2.08), 2.6);
});

test('third-person camera: a landing above the head, and a room ceiling, still cap it', () => {
  assert.equal(camTop(L, null, 1, 1, 5.0, 6.7), 6.8 - 0.15);  // climbing toward the next landing
  assert.equal(camTop(L, null, 1, 1, 0, 1.7), 2.6 - 0.15);    // standing on the ground floor
  assert.equal(camTop(L, null, 1, 1, 0.55, 2.25), 2.6 - 0.15); // at the top of a jump
});
