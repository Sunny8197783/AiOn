import test from 'node:test';
import assert from 'node:assert/strict';
import { createLayout, addUnit, removeUnit, togglePocket, tidyLayout, sameLayout, listOf, pocketLoad, usedCells } from '../dist/ui/bag-grid.js';
import { packGrid } from '../dist/game/pack.js';
import { POCKET_L } from '../dist/game/items.js';

const empty = () => createLayout([]).layout;
// 50 one-cell units, canned and rice taking turns in reading order: a full bag.
function fullOfSingles() {
  const L = empty();
  for (let i = 0; i < 50; i++) assert.equal(addUnit(L, i % 2 ? 'rice_instant' : 'canned'), 'grid');
  return L;
}

test('createLayout follows packGrid and reports what does not fit', () => {
  const list = [{ id: 'water_2l', n: 2 }, { id: 'id_card', n: 1, pocket: 1 }];
  const { layout, overflow } = createLayout(list);
  assert.deepEqual(overflow, []);
  assert.deepEqual(layout.pieces, packGrid(list).placed);
  assert.deepEqual(layout.pocket, { id_card: 1 });
  assert.deepEqual(listOf(layout), [{ id: 'water_2l', n: 2, pocket: 0 }, { id: 'id_card', n: 1, pocket: 1 }]);
  assert.deepEqual(createLayout([{ id: 'gas_stove', n: 3 }]).overflow, [{ id: 'gas_stove', n: 1 }]);
});

test('a bag packed bit by bit can need tidying; tidying makes the room', () => {
  const L = fullOfSingles();
  assert.equal(addUnit(L, 'canned'), 'full');
  for (let i = 0; i < 5; i++) assert.equal(removeUnit(L, 'rice_instant'), 'grid'); // five holes on the last row, none side by side
  assert.equal(usedCells(L), 45);
  assert.equal(addUnit(L, 'water_500'), 'tidy'); // 2x1: no two free cells together
  assert.equal(usedCells(L), 45, 'a refused unit changes nothing');
  const T = tidyLayout(L);
  assert.ok(T && !sameLayout(T, L));
  assert.equal(addUnit(T, 'water_500'), 'grid');
  assert.equal(addUnit(T, 'water_2l'), 'full'); // 3x2 does not fit in the three cells left
  assert.ok(sameLayout(tidyLayout(tidyLayout(T)), tidyLayout(T)), 'tidying twice changes nothing more');
});

test('pocket items go to the front pocket when the main part is full, up to 0.5 L', () => {
  const L = fullOfSingles();
  assert.equal(addUnit(L, 'bars'), 'pocket');
  assert.equal(addUnit(L, 'bars'), 'pocket');
  assert.equal(addUnit(L, 'bars'), 'pocket');
  assert.equal(addUnit(L, 'contact_card'), 'pocket');
  assert.equal(pocketLoad(L), 0.46);
  assert.equal(addUnit(L, 'bars'), 'full');
  assert.ok(pocketLoad(L) <= POCKET_L);
  assert.equal(addUnit(L, 'blackout_curtain'), 'full', 'a fixture never goes in');
});

test('pressing a held chip moves it between the pocket and the main part', () => {
  const L = empty();
  addUnit(L, 'id_card'); addUnit(L, 'water_2l');
  assert.deepEqual(togglePocket(L, 'id_card'), { to: 'pocket', n: 1 });
  assert.deepEqual(L.pocket, { id_card: 1 });
  assert.equal(L.pieces.some(p => p.id === 'id_card'), false);
  assert.deepEqual(togglePocket(L, 'id_card'), { to: 'grid', n: 1 });
  assert.deepEqual(L.pocket, {});
  assert.deepEqual(togglePocket(L, 'water_2l'), { refused: 'notPocket' });
  // As many as fit move in; the rest stay in the main part.
  const B = empty();
  for (let i = 0; i < 4; i++) addUnit(B, 'bars');
  assert.deepEqual(togglePocket(B, 'bars'), { to: 'pocket', n: 3 });
  assert.deepEqual(togglePocket(B, 'bars'), { refused: 'pocketFull' });
  // Back out of the pocket with no room: refused, nothing moves.
  const F = fullOfSingles();
  removeUnit(F, 'canned'); addUnit(F, 'meds'); togglePocket(F, 'meds'); addUnit(F, 'canned');
  assert.deepEqual(F.pocket, { meds: 1 });
  assert.deepEqual(togglePocket(F, 'meds'), { refused: 'full' });
  assert.deepEqual(F.pocket, { meds: 1 });
  assert.equal(usedCells(F), 50);
});

test('removing takes from the main part first, then the pocket', () => {
  const L = empty();
  addUnit(L, 'bars'); addUnit(L, 'bars'); togglePocket(L, 'bars');
  addUnit(L, 'bars');
  assert.equal(removeUnit(L, 'bars'), 'grid');
  assert.equal(removeUnit(L, 'bars'), 'pocket');
  assert.equal(removeUnit(L, 'bars'), 'pocket');
  assert.equal(removeUnit(L, 'bars'), null);
  assert.deepEqual(L.pocket, {});
});

test('bagInfo adds the pocket, a loaded device\'s charge and the old bars\' date; applyPocket writes the pocket back', async () => {
  const { bagInfo, applyPocket, itemNote } = await import('../dist/ui/bag.js');
  const { createGame, pack } = await import('../dist/game/state.js');
  const G = createGame({ seed: 2 });
  G.items.radio = { home: 1, bag: 0, bought: 1, checked: true, used: 0, charge: 30 };
  G.items.flashlight.checked = true; // no charge field: a full pack
  G.items.contact_card = { home: 1, bag: 0, bought: 0, checked: false, used: 0 };
  const info = (ids) => Object.fromEntries(bagInfo(G, ids.map(id => ({ id, tags: id === 'old_bars' ? ['food', 'expired'] : [] }))).map(i => [i.id, i]));
  let it = info(['radio', 'flashlight', 'old_bars', 'water_2l', 'batteries_old', 'batteries_aa']);
  assert.equal(itemNote(it.radio), '건전지 30%');
  assert.equal(itemNote(it.flashlight), '건전지 100%');
  assert.equal(itemNote(it.old_bars), '기한 확인 필요');
  assert.equal(itemNote(it.water_2l), '');
  assert.equal(itemNote(it.batteries_old), '건전지 30%', 'a half-used pack says how much is left');
  assert.equal(itemNote(it.batteries_aa), '');
  G.know.expiryChecked = true;
  assert.equal(itemNote(info(['old_bars']).old_bars), '기한 지남');
  pack(G, 'contact_card', 1); pack(G, 'old_bars', 2);
  applyPocket(G, { contact_card: 1, old_bars: 5, water_2l: 1 });
  assert.equal(G.items.contact_card.pocket, 1);
  assert.equal(G.items.old_bars.pocket, 2, 'no more than the bag holds');
  assert.equal(G.items.water_2l.pocket, undefined, 'not a pocket item');
  assert.equal(info(['contact_card']).contact_card.pocket, 1);
  applyPocket(G, {});
  assert.equal(G.items.contact_card.pocket, undefined);
  applyPocket(G, null); // a bag UI without a pocket result changes nothing
});

test('an SNS post shows the same share count every time it is reread', async () => {
  const { shareCount } = await import('../dist/ui/phone.js');
  const t = '[퍼옴] 임시 거처 급식 끊김';
  assert.equal(shareCount(t), shareCount(t));
  for (const s of [t, '', 'a', '시장길 풀림, 집결지 지름길']) { const n = shareCount(s); assert.ok(Number.isInteger(n) && n >= 40 && n < 300, `${s}: ${n}`); }
});
