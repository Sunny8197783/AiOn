import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame } from '../dist/game/state.js';
import { pickEnding } from '../dist/game/endings.js';

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
