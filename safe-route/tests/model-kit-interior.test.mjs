import test from 'node:test';
import assert from 'node:assert/strict';
import { SECTIONS, LAB, FACING, INTERIORS, ROOM } from '../dist/levels/town-interior.js';

test('shop-window shelves stock only known packages that have a facing size', () => {
  for (const [kind, secs] of Object.entries(SECTIONS)) {
    assert.ok(INTERIORS.includes(kind), kind);
    for (const sec of secs) for (const key of sec) {
      const L = LAB(key);
      assert.ok(L, `${kind}: ${key} is not a commerce-goods label`);
      assert.ok(FACING[L[0]], `${kind}: ${key} style ${L[0]}`);
      assert.match(L[1], /^#[0-9a-f]{6}$/i); assert.match(L[3], /^#[0-9a-f]{6}$/i);
    }
  }
  assert.equal(INTERIORS.length, ROOM.cols * ROOM.rows); // one atlas slot per interior
});
