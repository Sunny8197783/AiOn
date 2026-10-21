import test from 'node:test';
import assert from 'node:assert/strict';
import {
  ladderStage, recordEncounter, BAG_GOALS, evalBag, RADIO_WIDTH, radioSignal, radioNeeds,
  EXT_STEPS, EXT_CHOICES, evalExtinguisher, DOC_FIELDS, evalDocs,
} from '../dist/game/drills.js';
import { createGame, buy, pack, check } from '../dist/game/state.js';
import { CATALOG } from '../dist/game/items.js';
import { fact } from '../dist/game/facts.js';

const prices = Object.fromEntries(Object.entries(CATALOG).map(([id, c]) => [id, c.price]));
function game(cart = {}) {
  const G = createGame({ seed: 3 });
  G.money = 1e7;
  buy(G, cart, prices);
  return G;
}
const packAll = (G, list) => { for (const [id, n] of Object.entries(list)) pack(G, id, n); };
const assertFacts = (ids) => { for (const id of ids) assert.ok(fact(id), `fact ${id} exists`); };

// A bag that covers every 'alert' goal with usable (checked) items.
function alertBag(extraCart = {}) {
  const G = game({ water_500: 2, bars: 2, radio: 1, batteries_aa: 2, blanket: 1, contact_card: 1, ...extraCart });
  assert.equal(check(G, 'flashlight').ok, true);
  assert.equal(check(G, 'radio').ok, true);
  packAll(G, { water_2l: 1, bars: 1, flashlight: 1, radio: 1, meds: 1, contact_card: 1, blanket: 1 }); // water counts in litres: 2 L
  return G;
}

test('every goal, step and field points at real items and FACTS', () => {
  for (const [ctx, goals] of Object.entries(BAG_GOALS)) {
    assert.ok(goals.length, ctx);
    assert.equal(new Set(goals.map(g => g.id)).size, goals.length, `${ctx} goal ids unique`);
    for (const g of goals) {
      assert.ok(g.label && g.items.length && g.facts.length, `${ctx}.${g.id}`);
      for (const id of g.items) assert.ok(CATALOG[id], `${ctx}.${g.id}: ${id}`);
      assertFacts(g.facts);
    }
  }
  for (const s of [...EXT_STEPS, ...EXT_CHOICES, ...DOC_FIELDS]) assertFacts(s.facts);
  for (const [id, c] of Object.entries(CATALOG)) for (const f of c.facts ?? []) assert.ok(fact(f), `${id} cites ${f}`);
});

test('ladderStage climbs guided -> hint -> free per tool; recordEncounter logs it', () => {
  const G = createGame({ seed: 1 });
  assert.equal(ladderStage(G, 'radio'), 'guided');
  const r = recordEncounter(G, 'radio', 'tuned');
  assert.deepEqual(r, { day: -3, minute: 17 * 60 + 30, stage: 'guided', result: 'tuned' });
  assert.deepEqual(G.events.at(-1).details, { tool: 'radio', result: 'tuned', stage: 'guided' });
  assert.equal(G.events.at(-1).type, 'encounter');
  assert.equal(ladderStage(G, 'radio'), 'hint');
  assert.equal(recordEncounter(G, 'radio', 'missed').stage, 'hint');
  assert.equal(ladderStage(G, 'radio'), 'free');
  recordEncounter(G, 'radio', 'tuned');
  assert.equal(ladderStage(G, 'radio'), 'free', 'stays free');
  assert.equal(G.skill.radio.length, 3);
  for (const tool of ['bag', 'route', 'extinguisher', 'docs']) assert.equal(ladderStage(G, tool), 'guided');
  const n = G.events.length;
  assert.throws(() => ladderStage(G, 'nope'));
  assert.throws(() => recordEncounter(G, 'nope', 'x'));
  assert.equal(G.events.length, n, 'a bad tool records nothing');
  assert.throws(() => ladderStage(undefined, 'radio'));
  assert.throws(() => ladderStage({}, 'radio'));
});

test('evalBag: empty bag misses every goal', () => {
  const G = createGame({ seed: 2 });
  for (const ctx of Object.keys(BAG_GOALS)) {
    const r = evalBag(G, ctx);
    assert.equal(r.ok, false);
    assert.deepEqual(r.covered, []);
    assert.deepEqual(r.missing, BAG_GOALS[ctx].map(g => g.id));
    assert.deepEqual(r.extra, []);
    assert.equal(r.weightKg, 0);
    assert.equal(r.volumeL, 0);
    assert.ok(r.tips.length);
    assertFacts(r.tips);
  }
  assert.throws(() => evalBag(G, 'picnic'));
  assert.throws(() => evalBag(G));
});

test('evalBag: a full alert bag with checked gear is ok', () => {
  const r = evalBag(alertBag(), 'alert');
  assert.equal(r.ok, true);
  assert.deepEqual(r.covered, BAG_GOALS.alert.map(g => g.id));
  assert.deepEqual(r.missing, []);
  assert.deepEqual(r.extra, []);
  assert.deepEqual(r.unready, []);
  assert.deepEqual(r.tips, []);
  assert.equal(r.weightKg, 2.82); // 1.29 kg of gear + one 2 L bottle instead of 500 mL
});

test('evalBag: bought is not usable — unchecked gear does not cover a goal', () => {
  const G = game({ radio: 1 });
  packAll(G, { radio: 1, flashlight: 1 });
  const r = evalBag(G, 'alert');
  assert.ok(r.missing.includes('info') && r.missing.includes('light'));
  assert.deepEqual(r.unready.sort(), ['flashlight', 'radio']);
  assert.ok(r.tips.includes('radio_batt'));
  assertFacts(r.tips);
});

test('evalBag: extras get a FACTS tip, overweight fails even when covered', () => {
  const G = alertBag({ gas_stove: 1, candles: 1, water_2l: 2 });
  packAll(G, { gas_stove: 1, candles: 1, ramen: 1 });
  let r = evalBag(G, 'alert');
  assert.deepEqual(r.extra.sort(), ['candles', 'gas_stove', 'ramen']);
  for (const t of ['shelter_rules', 'candle_no', 'bag_nocook']) assert.ok(r.tips.includes(t), t);
  assert.equal(r.ok, true, 'extras alone do not fail the bag');
  pack(G, 'water_2l', 3);
  r = evalBag(G, 'alert');
  assert.ok(r.weightKg > 8);
  assert.deepEqual(r.missing, []);
  assert.equal(r.ok, false);
  assert.ok(r.tips.includes('bag_foil'));
  assertFacts(r.tips);
});

test('evalBag water trip: an empty jug and a light bag', () => {
  const G = game({ water_jug: 1 });
  pack(G, 'water_jug');
  let r = evalBag(G, 'water');
  assert.equal(r.ok, true);
  assert.deepEqual(r.covered, ['container']);
  assert.deepEqual(r.tips, []);
  pack(G, 'water_2l');
  r = evalBag(G, 'water');
  assert.equal(r.ok, false, 'over 2 kg before the walk back');
  assert.deepEqual(r.extra, ['water_2l']);
  assert.ok(r.tips.includes('water_point'));
});

test('evalBag evac: water counts in litres, food in units', () => {
  const G = game({ water_500: 4, bars: 3 });
  packAll(G, { water_500: 1, bars: 2 });
  let r = evalBag(G, 'evac');
  assert.ok(r.missing.includes('water') && r.missing.includes('food'), 'half a litre is not enough water');
  packAll(G, { water_500: 3, canned: 1 });
  r = evalBag(G, 'evac');
  assert.ok(r.covered.includes('water') && r.covered.includes('food'));
  assertFacts(r.tips);
});

test('radioSignal: clarity falls off within ±RADIO_WIDTH and picks the nearest station', () => {
  const a = { freq: 89.1, name: 'A' }, b = { freq: 90.0, name: 'B' };
  assert.equal(RADIO_WIDTH, 0.6);
  assert.deepEqual(radioSignal(89.1, [a, b]), { station: a, clarity: 1 });
  assert.deepEqual(radioSignal(89.4, [a]), { station: a, clarity: 0.5 });
  assert.deepEqual(radioSignal(89.7, [a]), { station: null, clarity: 0 }, 'edge of the band is silent');
  assert.deepEqual(radioSignal(95.0, [a, b]), { station: null, clarity: 0 });
  assert.equal(radioSignal(89.7, [a, b]).station, b);
  assert.equal(radioSignal(89.5, [a, b]).station, a);
  for (const s of [[], undefined, null]) assert.deepEqual(radioSignal(89.1, s), { station: null, clarity: 0 });
  for (const f of [NaN, Infinity, '89.1', undefined]) assert.throws(() => radioSignal(f, [a]), TypeError);
});

test('radioNeeds: no radio, no batteries, unchecked (slow), checked', () => {
  const G = createGame({ seed: 4 });
  G.money = 1e7;
  assert.deepEqual(radioNeeds(G), { ok: false, blocks: ['noRadio'], extraSeconds: 0, tips: ['power_prep'] });
  buy(G, { radio: 1 }, prices);
  assert.deepEqual(radioNeeds(G).blocks, ['noBatteries']);
  assert.equal(radioNeeds(G).ok, false);
  buy(G, { batteries_aa: 1 }, prices);
  pack(G, 'radio');
  assert.deepEqual(radioNeeds(G), { ok: true, blocks: ['notChecked'], extraSeconds: 40, tips: ['radio_batt'] }, 'radio in the bag counts');
  check(G, 'radio');
  assert.deepEqual(radioNeeds(G), { ok: true, blocks: [], extraSeconds: 0, tips: [] });
});

test('EXT_STEPS keep the official order and choices are fight/retreat', () => {
  assert.deepEqual(EXT_STEPS.map(s => s.id), ['shout', 'pin', 'aim', 'squeeze', 'sweep']);
  assert.deepEqual(EXT_CHOICES.map(c => c.id), ['fight', 'retreat']);
  for (const s of [...EXT_STEPS, ...EXT_CHOICES]) assert.match(s.label, /[가-힣]/);
});

test('evalExtinguisher: small fire, right and wrong orders', () => {
  const small = { fire: 'small' };
  assert.deepEqual(evalExtinguisher(['fight', 'shout', 'pin', 'aim', 'squeeze', 'sweep'], small), { result: 'out', correct: true, firstError: null });
  assert.deepEqual(evalExtinguisher(['fight', 'pin', 'aim', 'squeeze', 'sweep'], small), { result: 'out', correct: false, firstError: 'shout' }, 'out, but nobody was alerted');
  assert.deepEqual(evalExtinguisher(['shout', 'aim', 'pin', 'squeeze', 'sweep'], small), { result: 'incomplete', correct: false, firstError: 'pin' });
  assert.deepEqual(evalExtinguisher(['shout', 'pin', 'aim', 'squeeze'], small), { result: 'incomplete', correct: false, firstError: 'sweep' });
  assert.equal(evalExtinguisher(['shout', 'pin', 'pin', 'aim', 'squeeze', 'sweep'], small).result, 'out', 'repeated presses are ignored');
  assert.deepEqual(evalExtinguisher(['fight'], small), { result: 'incomplete', correct: false, firstError: 'shout' });
  assert.deepEqual(evalExtinguisher([], small), { result: 'incomplete', correct: false, firstError: 'shout' });
});

test('evalExtinguisher: retreating is always a success; fighting a large fire is not', () => {
  for (const fire of ['small', 'large']) assert.deepEqual(evalExtinguisher(['retreat'], { fire }), { result: 'retreat-ok', correct: true, firstError: null });
  assert.equal(evalExtinguisher(['shout', 'retreat'], { fire: 'small' }).result, 'retreat-ok');
  assert.deepEqual(evalExtinguisher(['fight', 'shout', 'pin', 'aim', 'squeeze', 'sweep'], { fire: 'large' }), { result: 'escalated', correct: false, firstError: 'retreat' });
  assert.deepEqual(evalExtinguisher(['fight', 'pin', 'retreat'], { fire: 'large' }), { result: 'retreat-ok', correct: false, firstError: 'retreat' }, 'late retreat still gets out');
  assert.deepEqual(evalExtinguisher([], { fire: 'large' }), { result: 'incomplete', correct: false, firstError: 'retreat' });
});

test('evalExtinguisher rejects bad input', () => {
  assert.throws(() => evalExtinguisher([], {}));
  assert.throws(() => evalExtinguisher([]));
  assert.throws(() => evalExtinguisher([], { fire: 'medium' }));
  assert.throws(() => evalExtinguisher('pin', { fire: 'small' }), TypeError);
  assert.throws(() => evalExtinguisher(null, { fire: 'small' }), TypeError);
  assert.throws(() => evalExtinguisher(['pin', 'water'], { fire: 'small' }));
});

test('DOC_FIELDS: six recommended fields, each explained by its FACTS text', () => {
  assert.deepEqual(DOC_FIELDS.map(f => f.id), ['contacts', 'meds', 'shelter', 'meet', 'idCopy', 'cash']);
  for (const f of DOC_FIELDS) {
    assert.equal(f.recommended, true);
    assert.equal(f.why, fact(f.facts[0]).text);
    assert.match(f.label, /[가-힣]/);
  }
});

test('evalDocs: full card in the bag, card left at home, empty and blank values', () => {
  const full = Object.fromEntries(DOC_FIELDS.map(f => [f.id, '적음']));
  assert.deepEqual(evalDocs(full, 'bag'), { ok: true, covered: DOC_FIELDS.map(f => f.id), missing: [], tips: [] });
  assert.equal(evalDocs(full).ok, true, 'where is optional');
  assert.deepEqual(evalDocs(full, 'home'), { ok: false, covered: DOC_FIELDS.map(f => f.id), missing: [], tips: ['contact_card'] });
  for (const card of [undefined, null, {}, { contacts: '   ', meds: false, shelter: null, meet: '' }]) {
    const r = evalDocs(card, 'bag');
    assert.equal(r.ok, false);
    assert.deepEqual(r.covered, []);
    assert.deepEqual(r.missing, DOC_FIELDS.map(f => f.id));
    assertFacts(r.tips);
  }
  const r = evalDocs({ contacts: '엄마 010', meds: 0 }, 'bag');
  assert.deepEqual(r.covered, ['contacts', 'meds']);
  assert.ok(!r.missing.includes('contacts'));
});
