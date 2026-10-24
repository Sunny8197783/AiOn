import test from 'node:test';
import assert from 'node:assert/strict';
import { readEcho } from '../dist/story/echo.js';

test('echoes from storage keep only well-formed marks, last 3 runs', () => {
  assert.deepEqual(readEcho('not json'), { runs: [] });
  const runs = Array.from({ length: 5 }, (_, i) => ({ trail: [['town', i, 2], ['town', 'x', 1], 'bad'], deaths: [{ lv: 'town', x: 1, z: 2, why: 'blast' }, { x: 1 }], notes: [{ lv: 'mart', x: 0, z: 0, text: '물 있음'.repeat(10) }] }));
  const e = readEcho(JSON.stringify({ runs }));
  assert.equal(e.runs.length, 3);
  assert.deepEqual(e.runs[0].trail, [['town', 2, 2]]);
  assert.equal(readEcho(JSON.stringify({ runs: [{ trail: [['town', 1, 1]] }] })).runs.length, 0); // left no mark
  assert.equal(e.runs[0].deaths.length, 1);
  assert.equal(e.runs[0].notes[0].text.length, 24);
});
