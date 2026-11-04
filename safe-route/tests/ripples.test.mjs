import test from 'node:test';
import assert from 'node:assert/strict';
import { ripples, causeOf, CAUSES, PREP, whenText } from '../dist/game/ripples.js';
import { createGame, recordEvent, buy, check } from '../dist/game/state.js';
import { recordEncounter } from '../dist/game/drills.js';

const at = (G, day, minute) => { G.day = day; G.minute = minute; };

test('every PREP id but judgement has a cause; labels are Korean', () => {
  for (const id of Object.keys(PREP)) {
    assert.match(PREP[id], /[가-힣]/, id);
    if (id === 'judgement') { assert.equal(CAUSES[id], undefined); continue; }
    assert.ok(CAUSES[id]?.length, `cause for ${id}`);
    for (const c of CAUSES[id]) assert.match(c.text, /[가-힣]/);
  }
  for (const id of Object.keys(CAUSES)) assert.ok(PREP[id], `${id} has a label`);
  assert.equal(whenText(-1, 9 * 60), 'D-1 오전');
  assert.equal(whenText(0, 20 * 60 + 6), 'D-day 저녁');
  assert.equal(whenText(12, 22 * 60), 'D+12 밤');
});

test('an empty game has no ripples', () => {
  assert.deepEqual(ripples(createGame({ seed: 1 })), []);
});

test('bag + tub: two rows, cause before effect, sorted by gap', () => {
  const G = createGame({ seed: 1 });
  at(G, -1, 9 * 60); recordEvent(G, 'bag_packed', {});
  at(G, -1, 22 * 60); recordEvent(G, 'tub_filled', {});
  at(G, 0, 20 * 60 + 6); recordEvent(G, 'payoff', { prep: 'bag', moment: '바로 나섰다' });
  at(G, 1, 8 * 60); recordEvent(G, 'payoff', { prep: 'tub', moment: '받아 둔 물로 세수했다' });
  at(G, 2, 8 * 60); recordEvent(G, 'payoff', { prep: 'bag', moment: '두 번째' });
  recordEvent(G, 'payoff', { prep: 'judgement', moment: '기다렸다' });
  recordEvent(G, 'payoff', { prep: 'radio', moment: '원인 없음' });
  const r = ripples(G);
  assert.deepEqual(r.map(x => x.prep), ['bag', 'tub'], 'bag: 35 h, tub: 34 h');
  for (const x of r) {
    assert.ok(x.cause.day * 1440 + x.cause.minute <= x.effect.day * 1440 + x.effect.minute);
    assert.ok(x.gap > 0);
    for (const t of [x.label, x.cause.text, x.cause.when, x.effect.when]) assert.match(t, /[가-힣]|D/);
  }
  assert.equal(r[0].effect.text, '바로 나섰다', 'first payoff only');
  assert.equal(r[0].cause.when, 'D-1 오전');
  assert.deepEqual(causeOf(G, 'bag'), { day: -1, minute: 540, when: 'D-1 오전', text: '비상가방을 꾸려 현관에 두었다' });
  assert.equal(causeOf(G, 'radio'), null);
});

test('causes match on details: check id, bought cart, practice drill, latest cause wins', () => {
  const G = createGame({ seed: 2 });
  G.money = 1e6;
  at(G, -2, 10 * 60); buy(G, { water_jug: 1, batteries_aa: 2, radio: 1 }, { water_jug: 7000, batteries_aa: 4500, radio: 24000 });
  check(G, 'radio');
  at(G, -1, 15 * 60); check(G, 'flashlight');
  recordEncounter(G, 'extinguisher', 'risky');
  recordEncounter(G, 'extinguisher', 'practice');
  at(G, -1, 19 * 60); recordEvent(G, 'bag_packed', {});
  at(G, -1, 20 * 60); recordEvent(G, 'bag_packed', {});
  at(G, 1, 9 * 60);
  for (const prep of ['water_jug', 'radio', 'flashlight', 'extinguisher', 'bag']) recordEvent(G, 'payoff', { prep, moment: '순간' });
  const by = Object.fromEntries(ripples(G).map(x => [x.prep, x]));
  assert.equal(by.water_jug.cause.text, '접이식 물통을 사 두었다');
  assert.equal(by.radio.cause.day, -2);
  assert.equal(by.flashlight.cause.day, -1);
  assert.equal(by.extinguisher.cause.minute, 15 * 60);
  assert.equal(by.bag.cause.minute, 20 * 60, 'the latest cause before the payoff');
});
