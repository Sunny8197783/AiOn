import test from 'node:test';
import assert from 'node:assert/strict';
import {
  createGame, conditions, recordEvent, fmtClock, hhmm, advance, adjust, spendSlot, buy, pack, unpack, bagLoad,
  usable, check, consume, use, note, speedMul, save, load, checkpoint, restore,
  migrate, trade, drainCharge, setPocket, pocketLoad, inPocket,
} from '../dist/game/state.js';
import { CATALOG, START_HOME, BAG_LIMIT, STORE_ITEMS, POCKET_L, packable } from '../dist/game/items.js';
import { readFileSync } from 'node:fs';

const martPrices = Object.fromEntries(Object.entries(CATALOG).filter(([, c]) => !c.notSold && !c.atmOnly).map(([id, c]) => [id, c.price]));

test('createGame starts from the spec state and START_HOME', () => {
  const G = createGame({ seed: 7 });
  assert.equal(G.v, 2);
  assert.equal(G.seed, 7);
  assert.equal(fmtClock(G), 'D-3 17:30');
  assert.deepEqual(G.slots, { total: 6, used: 0, done: [] });
  assert.equal(G.money, 150000);
  assert.deepEqual(G.stats, { health: 100, thirst: 10, fatigue: 20, tension: 25 });
  assert.deepEqual(Object.keys(G.items).sort(), Object.keys(START_HOME).sort());
  for (const [id, n] of Object.entries(START_HOME)) assert.deepEqual(G.items[id], { home: n, bag: 0, bought: 0, checked: false, used: 0 });
  assert.equal(usable(G, 'flashlight'), false, 'owned flashlight has no batteries');
  assert.equal(usable(G, 'water_2l'), true);
  assert.equal(usable(G, 'radio'), false);
  assert.deepEqual(G.cond, conditions(7));
  assert.equal(G.know.meetPoint, null);
  assert.deepEqual(G.rel.seoyeon, { trust: 0, contact: null });
  assert.deepEqual(Object.keys(G.skill), ['radio', 'bag', 'route', 'extinguisher', 'docs', 'info']);
  assert.deepEqual(G.ledger, []);
  assert.equal(G.checkpoint, null);
  assert.ok(Number.isInteger(createGame().seed), 'random seed when none is given');
  assert.throws(() => createGame({ seed: 'x' }));
});

test('conditions are deterministic and keep the safe facts fixed', () => {
  const seen = new Set();
  for (let s = 1; s <= 500; s++) {
    const a = conditions(s);
    assert.deepEqual(a, conditions(s));
    assert.ok(a.closures.dday.includes('yeonmuCenter'));
    assert.equal(a.eastGateClosed, true);
    assert.equal(a.rumorTrue, false);
    assert.equal(a.accessOpensAt, '20:16');
    assert.equal(a.seoyeonAt, 'cafe');
    assert.ok(a.commsWeakAt > 19 * 60 + 40 && a.commsWeakAt < 20 * 60 + 5);
    assert.ok(a.soldOut.late.includes('water'));
    seen.add(JSON.stringify(a));
  }
  assert.ok(seen.size > 10, 'seeds vary the situation');
  assert.throws(() => conditions(NaN));
});

test('recordEvent numbers events and stamps time and chapter', () => {
  const G = createGame({ seed: 1 });
  const a = recordEvent(G, 'payoff', { prep: 'x', moment: 'y' });
  const b = recordEvent(G, 'help');
  assert.equal(a.seq, 1);
  assert.equal(b.seq, 2);
  assert.equal(a.chapter, 'prologue');
  assert.equal(a.day, -3);
  assert.deepEqual(b.details, {});
  assert.throws(() => recordEvent(G, ''));
});

test('fmtClock and hhmm', () => {
  const G = createGame({ seed: 1 });
  G.day = 0; G.minute = 20 * 60 + 5;
  assert.equal(fmtClock(G), 'D-day 20:05');
  G.day = 12; G.minute = 7;
  assert.equal(fmtClock(G), 'D+12 00:07');
  assert.equal(hhmm(15 * 60 + 20), '15:20');
});

test('advance moves the clock across days and drifts stats', () => {
  const G = createGame({ seed: 1 });
  advance(G, 60);
  assert.equal(fmtClock(G), 'D-3 18:30');
  assert.equal(G.stats.thirst, 13);
  assert.equal(G.stats.fatigue, 22);
  for (let i = 0; i < 10; i++) advance(G, 2); // small steps add up like one long step
  assert.equal(G.stats.thirst, 14);
  advance(G, 6 * 60);
  assert.equal(fmtClock(G), 'D-2 00:50');
  const f = G.stats.fatigue;
  advance(G, 120, { awake: false });
  assert.equal(G.stats.fatigue, f);
  advance(G, 0);
  assert.throws(() => advance(G, -5));
  assert.throws(() => advance(G, NaN));
});

test('adjust clamps to 0..100 and rejects unknown stats', () => {
  const G = createGame({ seed: 1 });
  assert.equal(adjust(G, 'health', 50), 100);
  assert.equal(adjust(G, 'thirst', -99), 0);
  assert.equal(adjust(G, 'tension', 10), 35);
  assert.throws(() => adjust(G, 'mood', 1));
  assert.throws(() => adjust(G, 'health', 'a'));
});

test('spendSlot spends until the planner is full', () => {
  const G = createGame({ seed: 1 });
  assert.equal(spendSlot(G, 'mart', 2), 4);
  assert.equal(spendSlot(G, 'bag'), 3);
  assert.deepEqual(G.slots.done, ['mart', 'bag']);
  assert.throws(() => spendSlot(G, 'x', 4), /not enough slots/);
  assert.throws(() => spendSlot(G, 'x', 0));
  assert.equal(G.slots.used, 3);
  assert.equal(G.events.filter(e => e.type === 'slot').length, 2);
});

test('buy checks stock and money, adds to home and bought', () => {
  const G = createGame({ seed: 1 });
  assert.equal(buy(G, { radio: 1, batteries_aa: 2, water_2l: 0 }, martPrices), 24000 + 9000);
  assert.equal(G.money, 150000 - 33000);
  assert.deepEqual(G.items.radio, { home: 1, bag: 0, bought: 1, checked: false, used: 0 });
  assert.equal(G.items.water_2l.home, START_HOME.water_2l);
  assert.throws(() => buy(G, { radio: 1 }, STORE_ITEMS), /not in stock/);
  assert.throws(() => buy(G, { nope: 1 }, martPrices), /unknown item/);
  assert.throws(() => buy(G, { water_2l: -1 }, martPrices));
  assert.throws(() => buy(G, { water_2l: 1.5 }, martPrices));
  assert.throws(() => buy(G, { gas_stove: 10 }, martPrices), /not enough money/);
  assert.equal(G.money, 117000, 'failed purchases change nothing');
  assert.equal(buy(G, {}, martPrices), 0);
  assert.equal(buy(G, null, martPrices), 0);
});

test('cash comes from an ATM and lives in items', () => {
  const G = createGame({ seed: 1 });
  buy(G, { cash: 1 }, { cash: CATALOG.cash.price });
  assert.equal(G.money, 100000);
  assert.equal(G.items.cash.home, 1);
  assert.equal(usable(G, 'cash'), true);
});

test('pack, unpack and bagLoad', () => {
  const G = createGame({ seed: 1 });
  assert.deepEqual(bagLoad(G), { weightKg: 0, volumeL: 0, over: { weight: false, volume: false } });
  pack(G, 'water_2l', 2);
  pack(G, 'clothes');
  assert.equal(G.items.water_2l.home, 0);
  assert.equal(G.items.water_2l.bag, 2);
  assert.deepEqual(bagLoad(G), { weightKg: 4.7, volumeL: 7.4, over: { weight: false, volume: false } });
  assert.throws(() => pack(G, 'water_2l', 1), /only 0/);
  assert.throws(() => pack(G, 'radio', 1), /only 0/);
  assert.throws(() => pack(G, 'nope', 1), /unknown item/);
  unpack(G, 'water_2l');
  assert.equal(G.items.water_2l.home, 1);
  buy(G, { water_2l: 4 }, martPrices);
  pack(G, 'water_2l', 5);
  assert.equal(bagLoad(G).over.weight, true);
  assert.ok(bagLoad(G).weightKg > BAG_LIMIT.weightKg);
});

test('check turns owned into usable, batteries are used up', () => {
  const G = createGame({ seed: 1 });
  assert.deepEqual(check(G, 'radio'), { ok: false, reason: '가지고 있지 않습니다.' });
  assert.equal(check(G, 'flashlight').ok, false, 'no batteries yet');
  buy(G, { batteries_aa: 1, radio: 1, powerbank: 1 }, martPrices);
  assert.deepEqual(check(G, 'flashlight'), { ok: true });
  assert.equal(usable(G, 'flashlight'), true);
  assert.equal(G.items.batteries_aa.home, 0);
  assert.equal(G.items.batteries_aa.used, 1);
  assert.equal(check(G, 'radio').ok, false, 'the one pack is gone');
  assert.deepEqual(check(G, 'flashlight'), { ok: true }, 'checking twice is free');
  assert.equal(check(G, 'powerbank').ok, true);
  assert.equal(usable(G, 'powerbank'), true);
  assert.equal(check(G, 'water_2l').ok, true);
  assert.throws(() => check(G, 'nope'));
});

test('consume and use', () => {
  const G = createGame({ seed: 1 });
  pack(G, 'ramen', 2);
  consume(G, 'ramen', 1, 'bag');
  consume(G, 'ramen', 3);
  assert.deepEqual(G.items.ramen, { home: 0, bag: 1, bought: 0, checked: false, used: 4 });
  assert.throws(() => consume(G, 'ramen', 1), /only 0/);
  assert.throws(() => consume(G, 'ramen', 1, 'car'));
  assert.throws(() => consume(G, 'ramen', 0, 'bag'));
  assert.throws(() => use(G, 'flashlight'), /not usable/);
  buy(G, { batteries_aa: 1 }, martPrices);
  check(G, 'flashlight');
  use(G, 'flashlight');
  assert.equal(G.items.flashlight.used, 1);
  assert.equal(G.items.flashlight.home, 1);
});

test('note records labelled info once', () => {
  const G = createGame({ seed: 1 });
  const n = note(G, '  급수차 9시  ', 'official', 'centerNotice');
  assert.deepEqual(n, { chapter: 'prologue', day: -3, minute: 1050, text: '급수차 9시', label: 'official', place: 'centerNotice' });
  assert.equal(note(G, '급수차 9시', 'official'), n);
  note(G, '급수차 9시', 'rumor');
  assert.equal(G.info.length, 2);
  assert.throws(() => note(G, '', 'self'));
  assert.throws(() => note(G, 'x', 'gossip'));
});

test('speedMul stays within 0.6..1 and falls with fatigue and load', () => {
  const G = createGame({ seed: 1 });
  const fresh = speedMul(G);
  assert.equal(fresh, 1);
  adjust(G, 'fatigue', 80);
  const tired = speedMul(G);
  assert.ok(tired < fresh && tired >= 0.6);
  buy(G, { water_2l: 20 }, martPrices);
  pack(G, 'water_2l', 22);
  assert.equal(speedMul(G), tired, 'a bag left by the door slows nobody');
  G.know.bagOn = true;
  assert.equal(speedMul(G), 0.6);
});

test('save/load round trip and validation', () => {
  const G = createGame({ seed: 3 });
  buy(G, { radio: 1 }, martPrices);
  note(G, '라디오 샀음', 'self');
  checkpoint(G, 'ch1');
  const back = load(save(G));
  assert.deepEqual(back, G);
  assert.deepEqual(load(G), G, 'accepts an object too');
  assert.throws(() => load('{oops'), /not JSON/);
  assert.throws(() => load('null'), /invalid save/);
  assert.throws(() => load(JSON.stringify({ ...G, v: 1 })), /invalid save/);
  assert.throws(() => load(JSON.stringify({ ...G, stats: { ...G.stats, health: 300 } })), /stats/);
  assert.throws(() => load(JSON.stringify({ ...G, items: { radio: { home: -1, bag: 0, bought: 0, used: 0, checked: false } } })), /item radio/);
  assert.throws(() => load(JSON.stringify({ ...G, info: [{ text: 1, label: 'self' }] })), /info/);
  const old = JSON.parse(save(G));
  old.items.retired_item = { home: 1 };
  assert.equal(load(JSON.stringify(old)).items.retired_item, undefined, 'items removed from the catalogue are dropped');
});

test('checkpoint and restore roll back in place', () => {
  const G = createGame({ seed: 5 });
  const ref = G;
  checkpoint(G, 'dday');
  buy(G, { radio: 1 }, martPrices);
  advance(G, 90);
  recordEvent(G, 'payoff', {});
  restore(G);
  assert.equal(G, ref);
  assert.equal(G.money, 150000);
  assert.equal(G.items.radio, undefined);
  assert.equal(G.events.length, 0);
  assert.equal(G.checkpoint.key, 'dday');
  G.money = 1;
  restore(G);
  assert.equal(G.money, 150000, 'can retry twice from the same checkpoint');
  assert.throws(() => restore(createGame({ seed: 1 })), /no checkpoint/);
  assert.throws(() => checkpoint(G, ''));
});

test('a broken leg slows the run for good', () => {
  const G = createGame({ seed: 4 }), base = speedMul(G);
  G.injury = ['leg'];
  assert.equal(speedMul(G), Math.round(base * 0.7 * 100) / 100);
  assert.deepEqual(load(save(G)).injury, ['leg']);
});

// A save written by the build before skill.info, ledger, charge and pocket existed (tests/fixtures).
const OLD_SAVE = readFileSync(new URL('./fixtures/save-v2-pre-ledger.json', import.meta.url), 'utf8');

test('migrate: a pre-ledger save and its nested checkpoint still load', () => {
  const raw = JSON.parse(OLD_SAVE);
  assert.equal(raw.skill.info, undefined, 'fixture really is the old shape');
  assert.equal(raw.ledger, undefined);
  assert.equal(raw.checkpoint.state.skill.info, undefined);
  const G = load(OLD_SAVE);
  assert.deepEqual(G.skill.info, []);
  assert.deepEqual(G.ledger, []);
  assert.deepEqual(G.checkpoint.state.skill.info, []);
  assert.deepEqual(G.checkpoint.state.ledger, []);
  assert.equal(G.skill.bag.length, 1, 'old ladder kept');
  assert.equal(usable(G, 'radio'), true, 'checked before charges existed: still usable');
  assert.equal(drainCharge(G, 'radio', 10), 90, 'and counts as full');
  restore(G);
  assert.deepEqual(load(save(G)), G);
  assert.equal(migrate(null), null);
  assert.deepEqual(load(JSON.stringify({ ...raw, skill: { radio: [] }, checkpoint: null })).skill.info, [], 'any missing ladder is filled');
  assert.throws(() => load(JSON.stringify({ ...raw, skill: null })), /skill/);
});

test('validate: charge 0..100, pocket <= bag, ledger rows', () => {
  const G = createGame({ seed: 2 });
  const bad = (f) => { const o = JSON.parse(save(G)); f(o); return JSON.stringify(o); };
  assert.throws(() => load(bad(o => { o.items.flashlight.charge = 150; })), /charge/);
  assert.throws(() => load(bad(o => { o.items.meds.pocket = 1; })), /pocket/);
  assert.throws(() => load(bad(o => { o.ledger = [{ day: 'x' }]; })), /ledger/);
  assert.throws(() => load(bad(o => { o.ledger = {}; })), /ledger/);
  assert.ok(load(bad(o => { o.items.meds.bag = 1; o.items.meds.pocket = 1; o.items.flashlight.charge = 30; })));
});

test('check sets charge from the pack; drainCharge runs it down to 0', () => {
  const G = createGame({ seed: 1 });
  G.money = 1e6;
  buy(G, { radio: 1, batteries_aa: 1 }, martPrices);
  trade(G, { ramen: 1 }, { batteries_old: 1 }, 'trader');
  check(G, 'radio');
  assert.equal(G.items.radio.charge, 100, 'a new pack first');
  assert.equal(G.items.batteries_aa.home, 0);
  check(G, 'flashlight');
  assert.equal(G.items.flashlight.charge, 30, 'then the half-used one');
  assert.equal(G.items.batteries_old.used, 1);
  assert.equal(check(G, 'flashlight').ok, true);
  assert.equal(drainCharge(G, 'flashlight', 12.5), 17.5);
  assert.equal(drainCharge(G, 'flashlight', 50), 0);
  assert.equal(usable(G, 'flashlight'), false, 'dead batteries: not usable');
  assert.equal(G.events.filter(e => e.type === 'battery_dead').length, 1);
  assert.equal(drainCharge(G, 'flashlight', 5), 0, 'a dead device stays at 0');
  assert.equal(check(G, 'flashlight').ok, false, 'no packs left');
  buy(G, { batteries_aa: 1 }, martPrices);
  check(G, 'flashlight');
  assert.equal(G.items.flashlight.charge, 100, 'a spare pack revives it');
  assert.throws(() => drainCharge(G, 'water_2l', 1), /batteries/);
  assert.throws(() => drainCharge(G, 'radio', -1));
  assert.throws(() => check(G, 'radio', { using: 'water_2l' }), /battery pack/);
  assert.deepEqual(G.events.filter(e => e.type === 'check').map(e => e.details), [{ id: 'radio', charge: 100 }, { id: 'flashlight', charge: 30 }, { id: 'flashlight', charge: 100 }]);
});

test('trade is all or nothing and records the barter', () => {
  const G = createGame({ seed: 1 });
  G.money = 1e6;
  buy(G, { bars: 3 }, martPrices);
  pack(G, 'bars', 2);
  assert.throws(() => trade(G, { bars: 4 }, { batteries_aa: 1 }, 'trader'), /only 3/);
  assert.throws(() => trade(G, { bars: 1 }, { nope: 1 }, 'trader'), /unknown item/);
  assert.throws(() => trade(G, { bars: 1 }, {}, 'trader'));
  assert.throws(() => trade(G, { bars: 1.5 }, { batteries_aa: 1 }, 'trader'));
  assert.throws(() => trade(G, { bars: 1 }, { batteries_aa: 1 }, ''));
  assert.deepEqual(G.items.bars, { home: 1, bag: 2, bought: 3, checked: false, used: 0 }, 'failed trades change nothing');
  const e = trade(G, { bars: 3 }, { batteries_old: 1 }, 'trader');
  assert.deepEqual(G.items.bars, { home: 0, bag: 0, bought: 3, checked: false, used: 3 }, 'home first, then the bag');
  assert.deepEqual(G.items.batteries_old, { home: 1, bag: 0, bought: 0, checked: false, used: 0 });
  assert.deepEqual(e.details, { give: { bars: 3 }, get: { batteries_old: 1 }, from: 'trader' });
  assert.equal(e.type, 'trade');
});

test('front pocket: pocket items only, up to POCKET_L, emptied first when the bag shrinks', () => {
  const G = createGame({ seed: 1 });
  G.money = 1e6;
  buy(G, { powerbank: 4 }, martPrices);
  pack(G, 'powerbank', 4); pack(G, 'meds'); pack(G, 'water_2l');
  setPocket(G, 'powerbank', 3);
  assert.equal(pocketLoad(G), 0.45);
  assert.throws(() => setPocket(G, 'powerbank', 4), /pocket full/);
  assert.throws(() => setPocket(G, 'meds', 1), /pocket full/, `${POCKET_L} L is the limit`);
  assert.throws(() => setPocket(G, 'water_2l', 1), /does not fit/);
  assert.throws(() => setPocket(G, 'bars', 1), /only 0/);
  setPocket(G, 'powerbank', 2);
  setPocket(G, 'meds', 1);
  assert.equal(inPocket(G, 'meds'), true);
  unpack(G, 'powerbank', 3);
  assert.equal(G.items.powerbank.pocket, 1, 'pocket never holds more than the bag');
  consume(G, 'meds', 1, 'bag');
  assert.equal(inPocket(G, 'meds'), false);
  assert.equal('pocket' in G.items.meds, false);
  assert.deepEqual(load(save(G)), G);
});

test('fixtures stay at home', () => {
  const G = createGame({ seed: 1 });
  buy(G, { blackout_curtain: 1 }, martPrices);
  assert.equal(packable('blackout_curtain'), false);
  assert.equal(packable('water_2l'), true);
  assert.throws(() => pack(G, 'blackout_curtain'), /fixture/);
});
