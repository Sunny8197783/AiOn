import test from 'node:test';
import assert from 'node:assert/strict';
import { simulateDays, applyLedger, planLine, ledgerSummary, PICKS } from '../dist/game/ledger.js';
import { createGame, buy, check, load, save } from '../dist/game/state.js';
import { CATALOG } from '../dist/game/items.js';

const prices = Object.fromEntries(Object.entries(CATALOG).map(([id, c]) => [id, c.price]));
function game(cart = {}) {
  const G = createGame({ seed: 8 });
  G.money = 1e7; G.day = 3; G.minute = 10 * 60;
  buy(G, cart, prices);
  return G;
}
const plan = (water = 2, meals = 2, power = 'phone') => ({ water, meals, power });
const total = (G, id) => (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0);

test('deterministic and pure', () => {
  const G = game({ water_500: 4, canned: 2 });
  const before = save(G);
  const a = simulateDays(G, plan(), 3), b = simulateDays(G, plan(), 3);
  assert.deepEqual(a, b);
  assert.equal(save(G), before, 'simulateDays does not touch G');
  assert.deepEqual(a.rows.map(r => r.day), [3, 4, 5]);
  assert.ok(a.lines.length >= 1 && a.lines.length <= 5);
  for (const l of a.lines) assert.match(l, /[가-힣]/);
  assert.doesNotMatch(a.lines.join(' '), /undefined|NaN|null/);
  assert.throws(() => simulateDays(G, plan(4), 3));
  assert.throws(() => simulateDays(G, plan(2, 2, 'tv'), 3));
  assert.throws(() => simulateDays(G, plan(), 0));
  assert.throws(() => simulateDays(G, plan(), 2, { place: 'car' }));
  assert.equal(PICKS.water.length, 3);
});

test('stock never goes negative, whatever the plan', () => {
  for (const w of [1, 2, 3]) for (const m of [1, 2, 3]) for (const place of ['home', 'shelter']) {
    const G = game({ water_500: 1, bars: 1, gas_stove: 1, butane: 1 });
    applyLedger(G, simulateDays(G, plan(w, m, 'radio'), 6, { place }));
    for (const [id, e] of Object.entries(G.items)) { assert.ok(e.home >= 0 && e.bag >= 0, `${id} ${w}/${m}/${place}`); }
    assert.ok(G.stats.thirst <= 80, 'the ledger alone never exhausts');
    assert.ok(G.stats.fatigue <= 85);
    assert.ok(G.stats.health >= 20);
    assert.deepEqual(load(save(G)).ledger, G.ledger);
  }
});

test('ramen only with stove and butane, and only at home', () => {
  const plain = game();
  const r0 = simulateDays(plain, plan(2, 3), 2);
  assert.equal(r0.use.ramen, undefined, 'no stove: no ramen');
  const G = game({ gas_stove: 1, butane: 1 });
  const home = simulateDays(G, plan(2, 3), 2);
  assert.equal(home.use.ramen.home, 5, 'all five START_HOME ramen cooked');
  assert.equal(home.use.butane.home, 1);
  assert.equal(home.rows.at(-1).gasLeft, 12 - 5);
  assert.match(home.lines.join(' '), /라면을 5번/);
  const shelter = simulateDays(G, plan(2, 3), 2, { place: 'shelter' });
  assert.equal(shelter.use.ramen, undefined, 'never at the shelter');
  assert.equal(shelter.use.butane, undefined);
  const noGas = game({ gas_stove: 1 });
  assert.equal(simulateDays(noGas, plan(2, 3), 2).use.ramen, undefined, 'stove without butane');
});

test('water point: 10 L with the folding jug, 4 L without', () => {
  const dry = (cart) => { const G = game(cart); G.items.water_2l.home = 0; return G; };
  const no = simulateDays(dry({}), plan(2), 1), jug = simulateDays(dry({ water_jug: 1 }), plan(2), 1);
  assert.equal(no.rows[0].trips, 1); assert.equal(no.rows[0].loose, 2);
  assert.equal(jug.rows[0].trips, 1); assert.equal(jug.rows[0].loose, 8);
  assert.match(jug.lines.join(' '), /10L/);
  assert.match(no.lines.join(' '), /4L/);
  assert.equal(no.stats.fatigue, 6 + 1, 'one trip plus two meals a day');
  // The jug water lasts: three days at 2 L need one trip, not three.
  const G = dry({ water_jug: 1 });
  assert.equal(simulateDays(G, plan(2), 3).summary.trips, 1);
  const G2 = dry({});
  assert.equal(simulateDays(G2, plan(2), 3).summary.trips, 2);
});

test('daily radio listening drains 8 per day and can run it flat', () => {
  const G = game({ radio: 1, batteries_aa: 1 });
  check(G, 'radio');
  const r = simulateDays(G, plan(2, 2, 'radio'), 3);
  assert.equal(r.radioDrain, 24);
  applyLedger(G, r);
  assert.equal(G.items.radio.charge, 76);
  assert.equal(G.know.radioDaily, true);
  G.items.radio.charge = 10;
  applyLedger(G, simulateDays(G, plan(2, 2, 'radio'), 3));
  assert.equal(G.items.radio.checked, false, 'flat: needs new batteries');
  assert.ok(G.events.some(e => e.type === 'battery_dead' && e.details.id === 'radio'));
  const none = game();
  const n = simulateDays(none, plan(2, 2, 'radio'), 2);
  assert.equal(n.radioDrain, 0); assert.equal(n.radioDaily, false); assert.equal(n.phone, 100);
  assert.match(n.lines.join(' '), /라디오를 켤 수 없어/);
});

test('applyLedger consumes, records rows and an event; summary and plan line', () => {
  const G = game({ powerbank: 1, water_2l: 2 });
  const r = simulateDays(G, plan(3, 2, 'powerbank'), 2);
  const w = total(G, 'water_2l');
  applyLedger(G, r);
  assert.equal(total(G, 'water_2l'), w - 3, 'six litres = three 2 L bottles');
  assert.equal(G.items.powerbank.checked, true);
  assert.equal(G.ledger.length, 2);
  assert.deepEqual(Object.keys(G.ledger[0]).slice(0, 5), ['day', 'water', 'meals', 'power', 'place']);
  const e = G.events.at(-1);
  assert.equal(e.type, 'ledger');
  assert.deepEqual(e.details.plan, plan(3, 2, 'powerbank'));
  assert.equal(planLine(e.details.plan), '하루 배분: 물 3L · 두 끼 · 보조배터리');
  assert.deepEqual(ledgerSummary(G), { days: 2, trips: 0, cooked: 0, skipped: 0, minWater: 3 });
  assert.throws(() => applyLedger(G, {}));
});

test('old bars are the last resort and hurt a little', () => {
  const G = game();
  for (const id of ['ramen', 'rice_instant', 'canned']) G.items[id].home = 0;
  const r = simulateDays(G, plan(2, 2), 2);
  assert.equal(r.use.old_bars.home, 2);
  assert.equal(r.summary.skipped, 2);
  assert.equal(r.stats.health, -8);
  assert.match(r.lines.join(' '), /건빵/);
});
