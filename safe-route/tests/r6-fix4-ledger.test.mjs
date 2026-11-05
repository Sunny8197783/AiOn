// r6 fixer 4: ledger regressions (S3 tools used, S6 particle, S8 powerbank top-up, S13 shelter thirst).
import test from 'node:test';
import assert from 'node:assert/strict';
import { simulateDays, applyLedger } from '../dist/game/ledger.js';
import { createGame, buy, check } from '../dist/game/state.js';
import { endingRecord } from '../dist/game/debrief.js';
import { CATALOG } from '../dist/game/items.js';

const prices = Object.fromEntries(Object.entries(CATALOG).map(([id, c]) => [id, c.price]));
function game(cart = {}) {
  const G = createGame({ seed: 8 });
  G.money = 1e7; G.day = 3; G.minute = 10 * 60;
  buy(G, cart, prices);
  return G;
}

test('S3: a ledger that cooked and fetched marks the stove and jug used', () => {
  const G = game({ gas_stove: 1, butane: 1, water_jug: 1, ramen: 10 });
  const r = simulateDays(G, { water: 2, meals: 3, power: 'phone' }, 5);
  assert.ok(r.summary.cooked > 0 && r.summary.trips > 0);
  applyLedger(G, r);
  const names = endingRecord(G).unused.map(u => u.item);
  assert.ok(!names.includes(CATALOG.gas_stove.name), names.join(','));
  assert.ok(!names.includes(CATALOG.water_jug.name), names.join(','));
});

test('S3: a jug with no trip stays unused', () => {
  const G = game({ water_jug: 1, water_2l: 10 });
  const r = simulateDays(G, { water: 2, meals: 1, power: 'phone' }, 2);
  assert.equal(r.summary.trips, 0);
  assert.equal(r.usedJug, false);
});

test('S6: 으로 after 충전, 로 after vowel endings', () => {
  const G = game();
  assert.match(simulateDays(G, { water: 2, meals: 3, power: 'phone' }, 2).lines[0], /휴대폰 충전으로 2일을/);
  assert.match(simulateDays(G, { water: 2, meals: 3, power: 'radio' }, 2).lines[0], /라디오 매일 듣기로 2일을/);
  assert.match(simulateDays(G, { water: 2, meals: 3, power: 'powerbank' }, 2).lines[0], /보조배터리로 2일을/);
});

test('S8: the powerbank tops the phone up once and counts as used', () => {
  const G = game({ powerbank: 1 });
  G.phone.battery = 40;
  const r = simulateDays(G, { water: 2, meals: 2, power: 'powerbank' }, 5);
  assert.equal(r.usedBank, true);
  assert.ok(r.phone >= 50, `phone ${r.phone}`);
  applyLedger(G, r);
  assert.ok(G.items.powerbank.checked && G.items.powerbank.used > 0);
  const short = simulateDays(game({ powerbank: 1 }), { water: 2, meals: 2, power: 'powerbank' }, 2);
  assert.equal(short.usedBank, false, 'full phone needs no top-up');
});

test('S13: no thirst penalty or dry-throat line at the shelter', () => {
  const G = game();
  const r = simulateDays(G, { water: 1, meals: 2, power: 'phone' }, 5, { place: 'shelter' });
  const two = simulateDays(G, { water: 2, meals: 2, power: 'phone' }, 5, { place: 'shelter' });
  assert.equal(r.stats.thirst, two.stats.thirst, 'shelter water does not depend on the pick');
  assert.ok(!r.lines.some(l => l.includes('물을 아끼느라')));
  const home = simulateDays(game({ water_2l: 10 }), { water: 1, meals: 2, power: 'phone' }, 5);
  assert.ok(home.stats.thirst > 0 && home.lines.some(l => l.includes('물을 아끼느라')));
});
