import test from 'node:test';
import assert from 'node:assert/strict';
import { recordEvent } from '../dist/game/state.js';
import { DEEDS } from '../dist/game/deeds.js';
import { CHALLENGES, TIMED, runStats, runChallenges, readRecords, addRun, finishRun, shareText, fmtMs, PUBLIC_URL } from '../dist/game/challenges.js';
import { pickEnding } from '../dist/game/endings.js';
import { run } from './deed-fixtures.mjs';

const timed = (G, ms = 60000, retries = 0) => { for (const id of TIMED) recordEvent(G, 'chapter_time', { id, ms, retries }); return G; };

test('challenges are unique and only name real deeds', () => {
  assert.equal(new Set(CHALLENGES.map((c) => c.id)).size, CHALLENGES.length);
  const src = CHALLENGES.map((c) => c.test.toString()).join(' ');
  for (const [, id] of src.matchAll(/'([a-z_]+)'/g)) if (id !== 'kept') assert.ok(DEEDS[id], `unknown deed ${id}`);
});

test('a run earns the challenges its ledger shows; time ones need every chapter timed', () => {
  const G = run('stay', ['blackout_radio', 'rumor_checked', 'rumor_corrected', 'seoyeon_contact', 'manager_contact', 'clerk_contact']);
  let done = runChallenges(G, 0);
  for (const id of ['info_first', 'rumor_stop', 'network', 'no_hoard', 'hands_off']) assert.ok(done.includes(id), id);
  for (const id of ['clean', 'endings3', 'precise', 'promises']) assert.ok(!done.includes(id), id);
  done = runChallenges(timed(G), 3);
  assert.ok(done.includes('clean') && done.includes('endings3'));
  assert.ok(!runChallenges(run('stay', ['rumor_checked', 'rumor_corrected', 'rumor_spread']), 0).includes('rumor_stop'));
  assert.ok(!runChallenges(run('stay', ['hoard']), 0).includes('no_hoard'));
});

test('run stats add chapter times; a chapter continued mid-way leaves the run incomplete', () => {
  const G = timed(run('bus'), 90000, 1);
  assert.deepEqual([runStats(G).ms, runStats(G).retries, runStats(G).complete], [90000 * TIMED.length, TIMED.length, true]);
  recordEvent(G, 'chapter_time', { id: 'dday', ms: 1000, retries: 0, partial: true });
  assert.equal(runStats(G).complete, false);
  assert.equal(fmtMs(65000), '1분 05초');
  assert.equal(fmtMs(3725000), '1시간 2분');
});

test('records: first-time challenges, beaten bests and a top-5 board ranked by challenges, retries, time', () => {
  for (const raw of [null, '', '{', '[]', 7, { done: { clean: 'x', nope: '2026-11-02' }, best: { dday: -1, nope: 5 }, runs: [{ at: '2026-11-02', ending: 'nope', n: 1, ms: 5, retries: 0 }] }])
    assert.deepEqual(readRecords(raw), { done: {}, best: {}, runs: [] });
  const slow = timed(run('stay', ['blackout_radio', 'rumor_checked']), 120000);
  let { rec, news } = addRun(null, slow, 'stay', 1, '2026-11-01');
  assert.ok(news.fresh.includes('info_first'));
  assert.deepEqual([news.bests, news.rank], [[], 1]);
  const fast = timed(run('stay', ['blackout_radio', 'rumor_checked']), 60000);
  ({ rec, news } = addRun(JSON.stringify(rec), fast, 'stay', 1, '2026-11-02'));
  assert.ok(!news.fresh.includes('info_first'));
  assert.deepEqual([news.bests.length, news.rank, rec.best.dday], [TIMED.length, 1, 60000]);
  const fewer = timed(run('bus', []), 1000);
  ({ rec, news } = addRun(rec, fewer, 'bus', 1, '2026-11-02'));
  assert.equal(news.rank, 3); // fewer challenges ranks below, however fast
  for (let i = 0; i < 4; i++) ({ rec } = addRun(rec, timed(run('stay', ['blackout_radio', 'rumor_checked']), 50000 + i), 'stay', 1, '2026-11-03'));
  assert.equal(rec.runs.length, 5);
  assert.ok(!rec.runs.some((r) => r.ending === 'bus'));
  assert.deepEqual(readRecords(JSON.stringify(rec)), rec);
  assert.equal(addRun(rec, run('stay', []), 'stay', 1, '2026-11-03').news.rank, null); // untimed run: no rank
});

test('share text: ending, one square per challenge, time, link', () => {
  const G = timed(run('stay', ['blackout_radio', 'rumor_checked']));
  const { news } = addRun(null, G, 'stay', 1, '2026-11-02');
  const t = shareText(pickEnding(G), news, runStats(G)).split('\n');
  assert.match(t[0], /^〈경보 이전〉 엔딩 \d+ · /);
  assert.equal([...t[1].matchAll(/🟩|⬜/gu)].length, CHALLENGES.length);
  assert.match(t[2], /^기록 6분 00초 · 다시 하기 0번$/);
  assert.equal(t[3], PUBLIC_URL);
});

test('finishRun stores the ending in the gallery and the run in the records together', () => {
  const G = timed(run('stay', ['blackout_radio', 'rumor_checked']));
  const first = finishRun(G, null, null, '2026-11-06');
  assert.deepEqual(Object.keys(first.gal.seen), ['stay']);
  assert.deepEqual([first.news.rank, first.rec.runs.length], [1, 1]);
  assert.ok(first.news.done.includes('info_first'));
  const again = finishRun(G, JSON.stringify(first.gal), JSON.stringify(first.rec), '2026-11-07');
  assert.equal(again.gal.seen.stay.runs, 2);
  assert.deepEqual(again.news.fresh, []);
});
