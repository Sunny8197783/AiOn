import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, recordEvent } from '../dist/game/state.js';
import { pickEnding, MAX_EXTRA } from '../dist/game/endings.js';
import { simulateDays, applyLedger } from '../dist/game/ledger.js';

test('each last-day path has its own ending, coloured by the run', () => {
  const titles = new Set();
  for (const path of ['bus', 'missed', 'stay', 'seoyeon', 'volunteer', 'walk']) {
    const G = createGame({ seed: 3 }); G.know.path = path;
    const e = pickEnding(G);
    assert.equal(e.id, path); titles.add(e.title);
    assert.ok(e.lines.length >= 1 && e.lines.every((l) => /[가-힣]/.test(l)));
  }
  assert.equal(titles.size, 6);
  const G = createGame({ seed: 3 }); G.know.path = 'walk'; G.injury = ['leg']; G.karma = -4;
  const e = pickEnding(G);
  assert.ok(e.lines.some((l) => /구호 트럭/.test(l)) && e.lines.some((l) => /소문/.test(l)));
  assert.equal(pickEnding(createGame({ seed: 3 })).id, 'bus'); // no path yet: the default
});

test('elder, stranger and ledger lines; at most MAX_EXTRA lines after the opening', () => {
  const G = createGame({ seed: 3 }); G.know.path = 'bus';
  recordEvent(G, 'help', { who: 'elder', what: '부축했다' });
  recordEvent(G, 'help', { who: 'stranger', what: '물 한 병' });
  G.day = 3;
  applyLedger(G, simulateDays(G, { water: 2, meals: 2, power: 'phone' }, 2));
  const e = pickEnding(G);
  assert.ok(e.lines.some((l) => /304호 어르신/.test(l)));
  assert.ok(e.lines.some((l) => /자원봉사자/.test(l)));
  assert.ok(e.lines.some((l) => /하루치/.test(l)));
  const busy = createGame({ seed: 3 }); busy.know.path = 'walk'; busy.injury = ['leg']; busy.karma = -4;
  for (const who of ['seoyeon', 'elder', 'stranger']) recordEvent(busy, 'help', { who, what: 'x' });
  recordEvent(busy, 'looter', { result: 'gave' }); recordEvent(busy, 'looter', { result: 'fought' });
  const b = pickEnding(busy);
  assert.equal(b.lines.length, 1 + MAX_EXTRA);
  assert.equal(MAX_EXTRA, 5);
});

test('the same help pressed again does not unlock the shared-kitchen line; wording fixes', () => {
  const G = createGame({ seed: 3 }); G.know.path = 'stay';
  for (let i = 0; i < 4; i++) recordEvent(G, 'help', { who: 'manager', what: '같은 말' });
  assert.ok(!pickEnding(G).lines.some((l) => /공동 부엌/.test(l)));
  recordEvent(G, 'help', { who: 'seoyeon', what: '물' });
  assert.ok(pickEnding(G).lines.some((l) => /공동 부엌/.test(l)));
  const s = createGame({ seed: 3 }); s.know.path = 'seoyeon';
  assert.doesNotMatch(pickEnding(s).lines[0], /휠체어/);
  const m = createGame({ seed: 3 }); m.know.path = 'missed';
  assert.doesNotMatch(pickEnding(m).lines[0], /집결지에 닿았을 때/, 'missed can happen anywhere');
  for (const path of ['bus', 'missed', 'stay', 'seoyeon', 'volunteer', 'walk']) {
    const g = createGame({ seed: 3 }); g.know.path = path; g.stats.health = 20;
    for (const l of pickEnding(g).lines) assert.doesNotMatch(l, /탓|잘못|실수/);
  }
});
