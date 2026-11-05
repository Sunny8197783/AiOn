import test from 'node:test';
import assert from 'node:assert/strict';
import { createLayout, addUnit, removeUnit, togglePocket, listOf } from '../dist/ui/bag-grid.js';
import { packGrid } from '../dist/game/pack.js';
import { CATALOG } from '../dist/game/items.js';

const empty = () => createLayout([]).layout;
const refits = (L) => packGrid(listOf(L), { cols: L.cols, rows: L.rows }).fits;
function rng(seed) { let a = seed >>> 0; return () => { a = (a + 0x6D2B79F5) >>> 0; let t = Math.imul(a ^ (a >>> 15), a | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }

// Review round 3, defect 4: a bag packed (and partly unpacked) by hand in this order held everything at HEAD, but the
// biggest-first re-pack on reopening did not (found by fuzzing the round-3 code).
const OPS = ['+clothes', '+water_2l', '+photo_album', '+towel', '+radio', '+water_jug', '+gas_stove', '+water_2l', '+first_aid', '+clothes',
  '-clothes', '+radio', '+first_aid', '+photo_album', '+water_500', '-towel', '+water_jug'];
const replay = (L, ops) => ops.map((op) => (op[0] === '+' ? addUnit(L, op.slice(1)) : removeUnit(L, op.slice(1))));

test('a hand-packed bag always re-packs when reopened (the review repro)', () => {
  const L = empty(), got = [];
  for (const op of OPS) { got.push(replay(L, [op])[0]); assert.ok(refits(L), `after ${op}`); }
  assert.ok(got.some((g, i) => OPS[i][0] === '+' && g !== 'grid'), 'the unit the re-pack cannot hold stays out of the main part');
  assert.deepEqual(createLayout(listOf(L)).overflow, []);
});

test('random packing, removing and pocket toggles never leave a bag that does not re-pack', () => {
  const ids = Object.keys(CATALOG).filter((id) => CATALOG[id].cells && CATALOG[id].packable !== false);
  const pocketIds = ids.filter((id) => CATALOG[id].pocket), big = OPS.filter((op) => op[0] === '+').map((op) => op.slice(1));
  const r = rng(77);
  for (let run = 0; run < 1500; run++) {
    const L = empty();
    for (let k = 0; k < 40; k++) {
      const x = r();
      if (x < 0.75) { const pool = r() < 0.7 ? big : ids, id = pool[(r() * pool.length) | 0], res = addUnit(L, id); assert.ok(['grid', 'pocket', 'tidy', 'full'].includes(res)); }
      else if (x < 0.85) { const p = L.pieces[(r() * L.pieces.length) | 0]; if (p) assert.equal(removeUnit(L, p.id), 'grid'); }
      else { const id = pocketIds[(r() * pocketIds.length) | 0]; if (id) togglePocket(L, id); }
      assert.ok(refits(L), `run ${run} step ${k}: ${JSON.stringify(listOf(L))}`);
    }
  }
});

test('moving pocket units back to the main part is refused when the re-pack would not hold them', () => {
  const L = empty();
  replay(L, OPS);
  for (const id of Object.keys(CATALOG).filter((k) => CATALOG[k].pocket && CATALOG[k].cells)) {
    const before = JSON.stringify(L), res = togglePocket(L, id);
    if (res.refused) assert.equal(JSON.stringify(L), before, `${id}: a refusal changes nothing`);
    assert.ok(refits(L), id);
  }
});
