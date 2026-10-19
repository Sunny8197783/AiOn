import test from 'node:test';
import assert from 'node:assert/strict';
import { SOURCES, createRun, recordEvent, makeHandover, buildDebrief } from '../dist/learning.js';

test('run and events are serializable, independent, and ordered', () => {
  const run = createRun();
  const other = createRun();
  const details = { chapter: 0, location: { x: 12, z: 2 } };
  const first = recordEvent(run, 'gate_observed', details);
  details.location.x = -99;
  const second = recordEvent(run, 'notice_read', { chapter: 1 });
  assert.equal(run.version, 1);
  assert.equal(run.chapter, 0);
  assert.equal(other.events.length, 0);
  assert.equal(first.seq, 1);
  assert.equal(second.seq, 2);
  assert.equal(first.details.location.x, 12);
  assert.ok(Number.isFinite(Date.parse(first.at)));
  assert.deepEqual(JSON.parse(JSON.stringify(run)), run);
});

test('handover preserves the actual choice and appends its evidence once', () => {
  for (const kind of ['precise', 'generalized']) {
    const run = createRun();
    const note = makeHandover(run, kind);
    assert.equal(run.handover, note);
    assert.equal(note.kind, kind);
    assert.equal(note.time, '14:10');
    assert.equal(run.events.length, 1);
    assert.deepEqual(run.events[0].details, { kind });
  }
  const run = createRun();
  assert.throws(() => makeHandover(run, 'invented'), RangeError);
  assert.equal(run.handover, null);
  assert.equal(run.events.length, 0);
});

test('accurate scope earns feedback only when there is an observation', () => {
  const run = createRun();
  makeHandover(run, 'precise');
  assert.equal(buildDebrief(run).strengths.length, 0);
  recordEvent(run, 'gate_observed');
  assert.match(buildDebrief(run).strengths.join(' '), /미확인/);
});

test('precise handover includes the actual A arrival and preserves its information source', () => {
  for (const [route, label, source] of [
    ['stairs', '계단', '현장 출입 통제 표지'],
    ['ramp', '경사로', '대피 후 현장 안내문'],
  ]) {
    const run = createRun();
    recordEvent(run, 'gate_observed', { chapter: 0, source, time: '14:10' });
    recordEvent(run, 'route_attempt', { chapter: 0, route });
    const note = makeHandover(run, 'precise');
    assert.equal(note.source, source);
    assert.match(note.text, new RegExp(`${label} 입구로 대피시설에 도착`));
    assert.match(note.text, /보장은 없으며/);
    assert.match(note.text, /나머지 입구는 미확인/);
    assert.doesNotMatch(note.text, /다른 출입구는 확인하지 못/);
    assert.doesNotMatch(buildDebrief(run).strengths.join(' '), /동쪽 출입구에서/);
  }
});

test('handover uses A latest route only and never invents a direct observation source', () => {
  const run = createRun();
  assert.equal(makeHandover(run, 'precise').source, '주민 A의 전달');
  recordEvent(run, 'route_attempt', { chapter: 0, route: 'stairs' });
  recordEvent(run, 'chapter_complete', { chapter: 0, route: 'ramp' });
  recordEvent(run, 'route_attempt', { chapter: 1, route: 'stairs' });
  recordEvent(run, 'gate_observed', { chapter: 1, source: '두 번째 주민 안내', time: '14:16' });
  const note = makeHandover(run, 'precise');
  assert.match(note.text, /경사로 입구로 대피시설에 도착/);
  assert.equal(note.time, '14:10');
  assert.equal(note.source, '주민 A의 전달');
});

test('overbroad handover takes practice priority over route and freshness', () => {
  const run = createRun();
  makeHandover(run, 'generalized');
  recordEvent(run, 'route_attempt', { chapter: 1, route: 'stairs' });
  const debrief = buildDebrief(run);
  assert.equal(debrief.practice.id, 'scope');
  assert.match(debrief.improvements.join(' '), /전체/);
  assert.equal(debrief.improvements.length, 3);
});

test('only B stair attempts trigger access practice; successful replanning is retained', () => {
  const run = createRun();
  recordEvent(run, 'route_attempt', { chapter: 0, route: 'stairs' });
  recordEvent(run, 'chapter_complete', { chapter: 0, route: 'stairs' });
  assert.equal(buildDebrief(run).practice.id, 'communication');
  recordEvent(run, 'route_attempt', { chapter: 1, route: 'stairs' });
  recordEvent(run, 'notice_read', { chapter: 1 });
  recordEvent(run, 'chapter_complete', { chapter: 1, route: 'ramp' });
  const debrief = buildDebrief(run);
  assert.equal(debrief.practice.id, 'access');
  assert.match(debrief.strengths.join(' '), /수정/);
  assert.match(debrief.summary, /2\/2/);
});

test('A notice does not count as B current information', () => {
  const run = createRun();
  recordEvent(run, 'notice_read', { chapter: 0 });
  recordEvent(run, 'chapter_complete', { chapter: 1, route: 'ramp' });
  assert.equal(buildDebrief(run).practice.id, 'freshness');
  recordEvent(run, 'notice_read', { chapter: 1 });
  assert.equal(buildDebrief(run).practice.id, 'communication');
});

test('debrief does not mutate input or share mutable practice options', () => {
  const run = createRun();
  recordEvent(run, 'gate_observed');
  makeHandover(run, 'precise');
  const snapshot = structuredClone(run);
  const debrief = buildDebrief(run);
  debrief.practice.options[0].label = 'changed externally';
  assert.deepEqual(run, snapshot);
  assert.notEqual(buildDebrief(run).practice.options[0].label, 'changed externally');
});

test('empty and incomplete records are safe and never imply completion', () => {
  for (const run of [undefined, {}, createRun(), { events: [null, {}, { type: 'unknown' }] }]) {
    const debrief = buildDebrief(run);
    assert.equal(debrief.practice.id, 'communication');
    assert.equal(debrief.strengths.length, 0);
    assert.equal(debrief.improvements.length, 0);
    assert.match(debrief.summary, /기록이 없습니다/);
  }
});

test('each practice has three unique options, one correct answer, and a real source', () => {
  const runs = [createRun(), createRun(), createRun(), createRun()];
  makeHandover(runs[0], 'generalized');
  recordEvent(runs[1], 'route_attempt', { chapter: 1, route: 'stairs' });
  recordEvent(runs[2], 'route_attempt', { chapter: 1, route: 'ramp' });
  const practiceIds = new Set();
  for (const run of runs) {
    const { practice, cards } = buildDebrief(run);
    practiceIds.add(practice.id);
    assert.equal(practice.options.length, 3);
    assert.equal(new Set(practice.options.map(option => option.id)).size, 3);
    assert.equal(practice.options.filter(option => option.correct).length, 1);
    for (const item of [practice, ...cards]) assert.ok(SOURCES.some(source => source.id === item.sourceId));
    for (const option of practice.options) assert.ok(option.feedback.length > 0);
  }
  assert.equal(practiceIds.size, 4);
  assert.equal(new Set(SOURCES.map(source => source.id)).size, SOURCES.length);
  for (const source of SOURCES) assert.equal(new URL(source.url).protocol, 'https:');
});

test('invalid event input is rejected without changing the run', () => {
  const run = createRun();
  assert.throws(() => recordEvent(run, ''), TypeError);
  assert.throws(() => recordEvent(run, 'notice_read', null), TypeError);
  assert.throws(() => recordEvent({}, 'notice_read'), TypeError);
  assert.equal(run.events.length, 0);
});
