// r6 fixer 1: endings judge water in litres, received kindness is not 정민's help, and no two ending lines
// contradict each other (walk collapse vs "버틸 수 있었다", two leg lines).
import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, recordEvent } from '../dist/game/state.js';
import { pickEnding, waterLitres, STOCK_OK } from '../dist/game/endings.js';
import { simulateDays, applyLedger } from '../dist/game/ledger.js';

const game = (path, items = {}) => {
  const G = createGame({ seed: 3 }); G.know.path = path;
  for (const id of Object.keys(G.items)) G.items[id].home = G.items[id].bag = 0; // an empty flat: only what the test lays in
  for (const [id, n] of Object.entries(items)) G.items[id] = { home: n, bag: 0, bought: 0, checked: false, used: 0 };
  return G;
};

test('stock is judged in litres: four 500 mL bottles are 2 L, not "enough for the days"', () => {
  const few = game('stay', { water_500: 4, bars: 4 });
  assert.equal(waterLitres(few), 2);
  assert.ok(pickEnding(few).lines.some((l) => /바닥났다/.test(l)));
  const lots = game('stay', { water_2l: 5, bars: 4 });
  assert.equal(waterLitres(lots), 10); assert.ok(waterLitres(lots) >= STOCK_OK.litres);
  const l = pickEnding(lots).lines;
  assert.ok(l.some((x) => /버틸 수 있었다/.test(x)));
  assert.ok(!l.some((x) => /열흘/.test(x)), 'path-neutral: missed says "사흘 뒤"');
});

test('kindness received (by: other) does not unlock the shared-kitchen line', () => {
  const G = game('stay');
  recordEvent(G, 'help', { who: 'manager', what: '박 소장과 함께 남아 건물을 지키기로 했다' });
  recordEvent(G, 'help', { who: 'clerk', what: '쪽지와 현금 통', by: 'other' });
  recordEvent(G, 'help', { who: 'seoyeon', what: '보조배터리', by: 'other' });
  assert.ok(!pickEnding(G).lines.some((l) => /공동 부엌/.test(l)));
  recordEvent(G, 'help', { who: 'elder', what: '부축했다' });
  assert.ok(pickEnding(G).lines.some((l) => /공동 부엌/.test(l)));
});

test('walk: a collapse is not followed by "버틸 수 있었다", and the leg is told once', () => {
  const G = game('walk'); G.stats.health = 30; G.day = 3;
  applyLedger(G, simulateDays(G, { water: 1, meals: 1, power: 'phone' }, 2));
  const lines = pickEnding(G).lines;
  assert.ok(lines.some((l) => /주저앉/.test(l)));
  assert.ok(lines.some((l) => /끼니를 거른 날/.test(l)), 'the skipped-meal line is there, in its weak form');
  assert.ok(!lines.some((l) => /마지막까지 버틸 수 있었다/.test(l)), lines.join(' / '));
  const leg = game('walk'); leg.injury = ['leg'];
  const ll = pickEnding(leg).lines.filter((l) => /부러진 다리/.test(l));
  assert.equal(ll.length, 1, ll.join(' / '));
  const stay = game('stay'); stay.injury = ['leg'];
  assert.ok(pickEnding(stay).lines.some((l) => /부러진 다리는/.test(l)));
});

test('elder line fits paths that leave 연무동', () => {
  for (const [path, re] of [['bus', /안부/], ['stay', /손을 꼭/]]) {
    const G = game(path); recordEvent(G, 'help', { who: 'elder', what: '부축했다' });
    assert.ok(pickEnding(G).lines.some((l) => re.test(l)), path);
  }
});
