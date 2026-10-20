import test from 'node:test';
import assert from 'node:assert/strict';
import {
  SOURCES, ENTRANCES, STORY, MAX_NOTE, createRun, recordEvent, noteContext, composeNote, parseNote, noteKind,
  makeHandover, buildPins, closingLines, readClaims, buildDebrief,
} from '../dist/learning.js';

function playedRun(route = 'stairs', via = 'sign') {
  const run = createRun();
  recordEvent(run, 'gate_observed', { chapter: 0, via, time: STORY.gateSeenAt });
  recordEvent(run, 'route_attempt', { chapter: 0, route });
  return run;
}

// Every combination of chips the compose screen allows.
function* allChips() {
  for (const what of ['gate_closed', 'all_closed'])
    for (const time of [false, true]) for (const route of [false, true]) for (const unknowns of [false, true])
      for (const advice of [null, 'follow_me', 'direction', 'check_notice']) yield { what, time, route, unknowns, advice };
}

test('events are serializable, independent, and ordered', () => {
  const run = createRun();
  const details = { chapter: 0, location: { x: 12 } };
  const first = recordEvent(run, 'gate_observed', details);
  details.location.x = -99;
  assert.equal(first.details.location.x, 12);
  assert.equal(recordEvent(run, 'notice_read', { chapter: 1 }).seq, 2);
  assert.deepEqual(JSON.parse(JSON.stringify(run)), run);
  assert.throws(() => recordEvent(run, ''), TypeError);
  assert.throws(() => recordEvent(run, 'x', null), TypeError);
  assert.equal(run.events.length, 2);
});

test('every entrance open to 준 is outside 서연\'s card, and C opens after the note is sent', () => {
  for (const e of Object.values(ENTRANCES)) if (e.chapters.includes(0)) assert.equal(e.seoyeonOk, false);
  assert.ok(ENTRANCES.access.seoyeonOk && !ENTRANCES.access.chapters.includes(0));
  assert.ok(STORY.sentAt < STORY.accessOpenAt && STORY.accessOpenAt < STORY.receivedAt);
});

test('noteContext reads 준\'s real entrance and how he learned about the gate', () => {
  assert.deepEqual(noteContext(playedRun('ramp', 'notice_after')), { route: 'ramp', observedVia: 'notice_after' });
  const run = playedRun('stairs');
  recordEvent(run, 'route_attempt', { chapter: 1, route: 'ramp' });
  assert.equal(noteContext(run).route, 'stairs');
});

test('every chip note fits, never mentions C, and parses back to exactly the chips', () => {
  for (const route of ['stairs', 'ramp']) for (const observedVia of ['sign', 'notice_after']) for (const chips of allChips()) {
    const text = composeNote(chips, { route, observedVia });
    assert.ok(text.length <= MAX_NOTE, text);
    assert.doesNotMatch(text, /C|경사로/);
    const c = parseNote(text);
    assert.equal(c.what, chips.what, text);
    assert.equal(Boolean(c.time), chips.time, text);
    assert.equal(c.route, chips.route ? route : null, text);
    assert.equal(c.unknowns, chips.unknowns, text);
    assert.equal(c.advice, chips.advice, text);
    for (const quote of Object.values(c.quotes)) assert.ok(text.includes(quote), quote);
  }
});

test('parseNote reads free text by rule', () => {
  const cases = [
    ['동쪽문 막힘ㅠ 난 계단으로 옴 이쪽으로 ㄱㄱ', { what: 'gate_closed', route: 'stairs' }],
    ['대피소 전부 닫힘', { what: 'all_closed' }],
    ['다른 입구는 모두 확인 못 했어', { what: null, unknowns: true }],
    ['C로 와', { route: 'access' }],
    ['ㅋㅋㅋ', { what: null, route: null, advice: null, time: null }],
    ['2시 10분에 동쪽 출입구 통제 중', { what: 'gate_closed', time: '02:10' }],
    ['14:10 동쪽 폐쇄. B 진출입로 운영. C는 몰라. 안내 다시 확인!', { what: 'gate_closed', time: '14:10', route: 'ramp', unknowns: true, advice: 'check_notice' }],
  ];
  for (const [text, expected] of cases) {
    const c = parseNote(text);
    for (const [k, v] of Object.entries(expected)) assert.equal(c[k], v, `${text} → ${k}`);
  }
  assert.equal(noteKind(parseNote('ㅋㅋㅋ')), 'unparsed');
  assert.equal(noteKind(parseNote('대피소 전부 닫힘')), 'generalized');
  assert.equal(noteKind(parseNote('14:10에 동쪽 출입구가 닫혀 있었어. 다른 입구 상태는 확인 못 했어.')), 'precise');
});

test('makeHandover keeps the player\'s words, sanitizes edits, and falls back to chips', () => {
  const chips = { what: 'gate_closed', time: true, route: true, unknowns: true, advice: null };
  const run = playedRun('ramp');
  const h = makeHandover(run, { chips });
  assert.equal(h.text, composeNote(chips, { route: 'ramp', observedVia: 'sign' }));
  assert.equal(h.kind, 'precise');
  assert.equal(h.claims.parsedBy, 'chips');
  assert.equal(run.events.at(-1).type, 'handover_written');

  const edited = makeHandover(playedRun(), { chips, edited: true, text: `  동쪽\u0007 막힘 ${'가'.repeat(200)}` });
  assert.equal(edited.text.length, MAX_NOTE);
  assert.doesNotMatch(edited.text, /[\u0000-\u001f]/);
  assert.equal(edited.claims.parsedBy, 'rules');

  const blank = makeHandover(playedRun(), { chips, edited: true, text: '   ' });
  assert.equal(blank.text, composeNote(chips, { route: 'stairs', observedVia: 'sign' }));
  assert.equal(blank.edited, false);
});

test('buildPins always gives body, time and one more reason, whatever was written', () => {
  const texts = ['동쪽문 막힘ㅠ 난 계단으로 옴 이쪽으로 ㄱㄱ', 'C로 와', 'ㅋㅋㅋ', '대피소 전부 닫힘', '14:10 동쪽 폐쇄. B 진출입로 운영. C 경사로는 몰라. 안내 다시 확인'];
  const runs = [];
  for (const route of ['stairs', 'ramp']) {
    for (const chips of allChips()) { const run = playedRun(route); makeHandover(run, { chips }); runs.push(run); }
    for (const text of texts) { const run = playedRun(route); makeHandover(run, { edited: true, text }); runs.push(run); }
  }
  runs.push(createRun());
  const anchors = new Set(['stairs', 'ramp', 'notice', 'gate', 'access', 'start0', 'start1']);
  for (const run of runs) {
    const pins = buildPins(run);
    assert.equal(pins.length, 3);
    assert.equal(pins[0].factor, 'body');
    assert.equal(pins[1].factor, 'time');
    assert.ok(['scope', 'advice', 'direction', 'place'].includes(pins[2].factor));
    for (const pin of pins) {
      assert.ok(anchors.has(pin.anchor), pin.anchor);
      assert.ok(pin.was && pin.now);
      // No blame on either character.
      assert.doesNotMatch(pin.was + pin.now, /실수|잘못|서연(이|은)\s*못/);
    }
    assert.equal(ENTRANCES[pins[0].anchor].seoyeonOk, false);
  }
  const scoped = playedRun(); makeHandover(scoped, { chips: { what: 'all_closed' } });
  assert.equal(buildPins(scoped)[2].factor, 'scope');
  // 준 who only read the notice after arriving never "saw" the gate.
  const told = playedRun('stairs', 'notice_after'); makeHandover(told, { chips: { what: 'all_closed' } });
  assert.doesNotMatch(buildPins(told)[2].now + readClaims(told).map(r => r.why).join(' '), /직접/);
  for (const kind of ['precise', 'partial', 'generalized', 'unparsed']) assert.equal(closingLines(kind).length, 2);
});

test('readClaims always lists C as after the note and cites real sources', () => {
  const run = playedRun(); makeHandover(run, { chips: { what: 'gate_closed', route: true, advice: 'follow_me' } });
  const rows = readClaims(run);
  assert.ok(rows.some(r => r.label === 'C 경사로' && r.status === 'missing'));
  assert.ok(rows.some(r => r.label === '들어간 길' && r.status === 'mismatch'));
  assert.ok(rows.some(r => r.label === '시각' && r.status === 'missing'));
  for (const row of rows) assert.ok(SOURCES.some(s => s.id === row.sourceId), row.sourceId);
  assert.deepEqual(readClaims(createRun()), []);
});

test('practice selection follows the record', () => {
  const generalized = playedRun(); makeHandover(generalized, { chips: { what: 'all_closed' } });
  assert.equal(buildDebrief(generalized).practice.id, 'scope');
  const tried = playedRun(); makeHandover(tried, { chips: { what: 'gate_closed' } });
  recordEvent(tried, 'route_attempt', { chapter: 1, route: 'stairs' });
  assert.equal(buildDebrief(tried).practice.id, 'access');
  const calm = playedRun(); makeHandover(calm, { chips: { what: 'gate_closed' } });
  assert.equal(buildDebrief(calm).practice.id, 'freshness');
});

test('debrief is safe on empty input and only cites real https sources', () => {
  for (const run of [undefined, {}, createRun(), { events: [null, {}, { type: 'x' }] }]) {
    const d = buildDebrief(run);
    assert.equal(d.strengths.length, 0);
    assert.equal(d.improvements.length, 0);
    for (const item of [d.practice, ...d.cards]) assert.ok(SOURCES.some(s => s.id === item.sourceId), item.sourceId);
    assert.equal(d.practice.options.filter(o => o.correct).length, 1);
  }
  for (const s of SOURCES) assert.equal(new URL(s.url).protocol, 'https:');
  assert.equal(new Set(SOURCES.map(s => s.id)).size, SOURCES.length);
});
