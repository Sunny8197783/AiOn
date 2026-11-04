import test from 'node:test';
import assert from 'node:assert/strict';
import { packGrid, bagList, canAdd } from '../dist/game/pack.js';
import { CATALOG, BAG_GRID, BAG_LIMIT, POCKET_L } from '../dist/game/items.js';
import { createGame, pack, setPocket } from '../dist/game/state.js';

test('every packable item has cells that hold its volume; the grid is the bag', () => {
  assert.equal(BAG_GRID.cols * BAG_GRID.rows * BAG_GRID.cellL, BAG_LIMIT.volumeL);
  for (const [id, c] of Object.entries(CATALOG)) {
    if (c.fixture) { assert.equal(c.cells, undefined, id); continue; }
    assert.ok(Array.isArray(c.cells) && c.cells.every(n => Number.isInteger(n) && n >= 1), `${id} has cells`);
    assert.ok(c.cells[0] * c.cells[1] * BAG_GRID.cellL >= c.volumeL, `${id}: ${c.cells} for ${c.volumeL} L`);
    if (c.pocket) assert.ok(c.volumeL <= 0.2, `${id} is small enough for the pocket`);
  }
});

test('first-fit decreasing is deterministic and biggest first', () => {
  const list = [{ id: 'bars', n: 3 }, { id: 'water_2l', n: 2 }, { id: 'gas_stove', n: 1 }, { id: 'radio', n: 1 }];
  const a = packGrid(list), b = packGrid(list);
  assert.deepEqual(a, b);
  assert.equal(a.fits, true);
  assert.deepEqual(a.placed[0], { id: 'gas_stove', x: 0, y: 0, w: 4, h: 3, rotated: false });
  assert.deepEqual(a.placed[1], { id: 'water_2l', x: 4, y: 0, w: 3, h: 2, rotated: false });
  assert.equal(a.used, 12 + 6 + 6 + 2 + 3);
  assert.equal(a.free, 50 - a.used);
  // No two pieces overlap and all stay inside.
  const seen = new Set();
  for (const p of a.placed) for (let y = p.y; y < p.y + p.h; y++) for (let x = p.x; x < p.x + p.w; x++) {
    assert.ok(x < 10 && y < 5); const k = `${x},${y}`; assert.ok(!seen.has(k), k); seen.add(k);
  }
});

test('rotation is used only when needed', () => {
  const r = packGrid([{ id: 'gas_stove', n: 1 }], { cols: 3, rows: 4 });
  assert.deepEqual(r.placed, [{ id: 'gas_stove', x: 0, y: 0, w: 3, h: 4, rotated: true }]);
  assert.equal(packGrid([{ id: 'gas_stove', n: 1 }], { cols: 3, rows: 3 }).fits, false);
  const full = packGrid([{ id: 'gas_stove', n: 3 }]);
  assert.deepEqual(full.overflow, [{ id: 'gas_stove', n: 1 }], 'a third stove does not fit');
  assert.equal(canAdd([{ id: 'gas_stove', n: 2 }], 'gas_stove'), false);
  assert.equal(canAdd([{ id: 'gas_stove', n: 2 }], 'bars'), true);
});

test('the front pocket takes pocket items up to 0.5 L, the rest goes in the main grid', () => {
  const r = packGrid([{ id: 'powerbank', n: 4, pocket: 4 }, { id: 'contact_card', n: 1, pocket: 1 }, { id: 'water_500', n: 1, pocket: 1 }]);
  assert.ok(r.pocketL <= POCKET_L);
  assert.deepEqual(r.pocket, [{ id: 'powerbank', n: 3 }, { id: 'contact_card', n: 1 }]);
  assert.equal(r.placed.filter(p => p.id === 'powerbank').length, 1);
  assert.equal(r.placed.filter(p => p.id === 'water_500').length, 1, 'not a pocket item');
  assert.throws(() => packGrid([{ id: 'nope', n: 1 }]), /unknown item/);
  assert.throws(() => packGrid([{ id: 'bars', n: 1, pocket: 2 }]));
  assert.deepEqual(packGrid([{ id: 'blackout_curtain', n: 1 }]).overflow, [{ id: 'blackout_curtain', n: 1 }]);
});

test('bagList reads the bag and pocket from G', () => {
  const G = createGame({ seed: 1 });
  pack(G, 'meds'); pack(G, 'water_2l', 2); setPocket(G, 'meds', 1);
  assert.deepEqual(bagList(G), [{ id: 'water_2l', n: 2, pocket: 0 }, { id: 'meds', n: 1, pocket: 1 }]);
  const r = packGrid(bagList(G));
  assert.deepEqual(r.pocket, [{ id: 'meds', n: 1 }]);
  assert.equal(r.fits, true);
});
