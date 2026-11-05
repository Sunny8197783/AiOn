import test from 'node:test';
import assert from 'node:assert/strict';
import { failDebrief, chapterSummary } from '../dist/game/debrief.js';
import { createGame, recordEvent } from '../dist/game/state.js';
import { recordEncounter } from '../dist/game/drills.js';
import { fact } from '../dist/game/facts.js';

const src = (...ids) => [...new Set(ids.map(id => fact(id).sourceId))];

test('4장 stay smoke judgement is a fire judgement, not an extinguisher use', () => {
  const G = createGame({ seed: 4 });
  G.chapter = 'evac';
  recordEvent(G, 'smoke_choice', { pick: 'stay' });
  recordEncounter(G, 'extinguisher', 'retreat-ok');
  const lines = chapterSummary(G, 'evac').lines;
  assert.ok(lines.includes('화재 판단: 문을 닫고 신고한 뒤 안내를 기다렸습니다'), lines.join('\n'));
  assert.ok(!lines.some(l => l.startsWith('소화기')));
  const R = createGame({ seed: 4 });
  R.chapter = 'evac';
  recordEvent(R, 'smoke_choice', { pick: 'run' });
  recordEncounter(R, 'extinguisher', 'risky');
  assert.ok(chapterSummary(R, 'evac').lines.includes('화재 판단: 연기 속으로 바로 나섰습니다'));
});

test('dead by smoke: indoor smoke card with fire_big / fire_lift, towel-aware missed line', () => {
  const G = createGame({ seed: 4 });
  let d = failDebrief(G, 'dead', { why: 'smoke' });
  assert.equal(d.title, '연기를 너무 많이 마셨습니다');
  assert.equal(d.next, fact('fire_big').text);
  assert.match(d.missed, /젖은 수건/);
  assert.deepEqual(d.sourceIds, src('fire_big', 'fire_lift'));
  recordEvent(G, 'towel_wet', { with: 'tub' });
  assert.match(failDebrief(G, 'dead', { why: 'smoke' }).missed, /몸을 낮추지/);
  assert.doesNotMatch(failDebrief(G, 'dead', { why: 'fire' }).next, /바람이 불어오는/);
});

test('dead / exhausted cards cite related sources', () => {
  const G = createGame({ seed: 4 });
  assert.deepEqual(failDebrief(G, 'dead', { why: 'fall' }).sourceIds, src('alert_stairs'));
  assert.deepEqual(failDebrief(G, 'dead', { why: 'fight' }).sourceIds, src('people_order'));
  assert.deepEqual(failDebrief(G, 'dead', { why: 'hurt' }).sourceIds, src('home_stock'));
  assert.deepEqual(failDebrief(G, 'dead').sourceIds, src('home_stock'));
  assert.deepEqual(failDebrief(G, 'exhausted', { why: 'carry' }).sourceIds, src('water_point', 'water_sphere'));
});

test('late: a packed bag by the door is not told to pack again', () => {
  const G = createGame({ seed: 4 });
  Object.assign(G.know, { shelterLocated: true, entrancesChecked: true, routeChecked: true, bagPacked: true });
  let d = failDebrief(G, 'late'); // never left home
  assert.equal(d.missed, '경보가 울린 뒤에도 집을 나서지 않았습니다.');
  assert.equal(d.next, fact('bag_take').text);
  recordEvent(G, 'departure', { bag: 'packed' });
  d = failDebrief(G, 'late');
  assert.doesNotMatch(d.next, /비상가방 꾸리기/);
  recordEvent(G, 'departure', { bag: 'late', delay: 4 });
  assert.match(failDebrief(G, 'late').next, /비상가방 꾸리기/);
});
