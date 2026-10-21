import test from 'node:test';
import assert from 'node:assert/strict';
import {
  hhmm, ENTRANCES, MAX_NOTE, relayTimes, noteContext, composeNote, parseNote, noteKind,
  makeHandover, buildPins, closingLines, readClaims,
} from '../dist/game/relay.js';
import { createGame, recordEvent, hhmm as stateHhmm } from '../dist/game/state.js';
import { SOURCES } from '../dist/game/facts.js';

const GATE = 20 * 60 + 9;
const BLAME = /실수|잘못|탓|서연(이|은)\s*못/;

// 정민 sees the east gate, enters A or B; the D-day chapter then sets G.relay (story/seoyeon.js).
function played(route = 'stairs', via = 'sign', { gate = GATE, sent = GATE + 4 } = {}) {
  const G = createGame({ seed: 1 });
  recordEvent(G, 'gate_observed', { who: 'jeongmin', via, minute: gate });
  recordEvent(G, 'route_attempt', { who: 'jeongmin', route, minute: gate + 2 });
  G.relay = relayTimes(sent, gate);
  return G;
}

// Every combination of chips the compose screen allows.
function* allChips() {
  for (const what of ['gate_closed', 'all_closed'])
    for (const time of [false, true]) for (const route of [false, true]) for (const unknowns of [false, true])
      for (const advice of [null, 'follow_me', 'direction', 'check_notice']) yield { what, time, route, unknowns, advice };
}

test('relayTimes: C opens after the note is sent and before 서연 reads it', () => {
  assert.deepEqual(relayTimes(20 * 60 + 13), { gateSeenAt: '20:10', sentAt: '20:13', accessOpenAt: '20:16', receivedAt: '20:17' });
  assert.deepEqual(relayTimes(20 * 60 + 30, GATE), { gateSeenAt: '20:09', sentAt: '20:30', accessOpenAt: '20:33', receivedAt: '20:34' });
  assert.equal(relayTimes(20 * 60 + 5, 20 * 60 + 8).sentAt, '20:08', 'never sent before the gate was seen');
  for (let sent = 20 * 60; sent <= 21 * 60 + 30; sent++) for (let gate = sent - 20; gate <= sent + 5; gate++) {
    const t = relayTimes(sent, gate);
    assert.ok(t.gateSeenAt <= t.sentAt && t.sentAt < t.accessOpenAt && t.accessOpenAt < t.receivedAt, JSON.stringify(t));
    assert.ok(t.accessOpenAt >= '20:16');
  }
  assert.equal(hhmm, stateHhmm);
});

test('entrances open to 정민 are outside 서연\'s card; only C fits it', () => {
  assert.deepEqual(Object.keys(ENTRANCES), ['stairs', 'ramp', 'access']);
  for (const id of ['stairs', 'ramp']) {
    assert.equal(ENTRANCES[id].seoyeonOk, false);
    assert.ok(ENTRANCES[id].card && ENTRANCES[id].short && ENTRANCES[id].label);
  }
  assert.equal(ENTRANCES.access.seoyeonOk, true);
});

test('noteContext reads 정민\'s real entrance and how the gate was learned', () => {
  assert.deepEqual(noteContext(played('ramp', 'notice_after')), { route: 'ramp', observedVia: 'notice_after' });
  const G = played('stairs');
  recordEvent(G, 'route_attempt', { who: 'seoyeon', route: 'ramp' });
  recordEvent(G, 'route_attempt', { who: 'jeongmin', route: 'access' });
  recordEvent(G, 'gate_observed', { who: 'jeongmin', via: 'notice_after' });
  assert.deepEqual(noteContext(G), { route: 'stairs', observedVia: 'sign' }, '서연\'s moves, C and later notices do not count');
  recordEvent(G, 'route_attempt', { who: 'jeongmin', route: 'ramp' });
  assert.equal(noteContext(G).route, 'ramp', 'the last entrance 정민 used');
  for (const G0 of [createGame({ seed: 2 }), undefined, {}]) assert.deepEqual(noteContext(G0), { route: 'stairs', observedVia: 'sign' });
});

test('every chip note fits, never mentions C, and parses back to exactly the chips', () => {
  for (const gateSeenAt of ['20:09', relayTimes(20 * 60 + 14).gateSeenAt])
    for (const route of ['stairs', 'ramp']) for (const observedVia of ['sign', 'notice_after']) for (const chips of allChips()) {
      const text = composeNote(chips, { route, observedVia, gateSeenAt });
      assert.ok(text.length <= MAX_NOTE, text);
      assert.doesNotMatch(text, /C|경사로/);
      assert.equal(text.includes(gateSeenAt), chips.time, text);
      const c = parseNote(text);
      assert.equal(c.what, chips.what, text);
      assert.equal(Boolean(c.time), chips.time, text);
      assert.equal(c.route, chips.route ? route : null, text);
      assert.equal(c.unknowns, chips.unknowns, text);
      assert.equal(c.advice, chips.advice, text);
      for (const quote of Object.values(c.quotes)) assert.ok(text.includes(quote), quote);
    }
  assert.equal(composeNote(), '');
  assert.equal(composeNote({ route: true }, { route: 'access' }), composeNote({ route: true }, { route: 'stairs' }), 'unknown route falls back to stairs');
});

test('parseNote reads free text by rule', () => {
  const cases = [
    ['동쪽문 막힘ㅠ 난 계단으로 옴 이쪽으로 ㄱㄱ', { what: 'gate_closed', route: 'stairs' }],
    ['대피소 전부 닫힘', { what: 'all_closed' }],
    ['다른 입구는 모두 확인 못 했어', { what: null, unknowns: true }],
    ['C로 와', { route: 'access' }],
    ['ㅋㅋㅋ', { what: null, route: null, advice: null, time: null }],
    ['2시 10분에 동쪽 출입구 통제 중', { what: 'gate_closed', time: '2시 10분' }],
    ['20:09 대피소 동쪽 출입구 닫힘. 다른 입구는 몰라', { what: 'gate_closed', time: '20:09', unknowns: true }],
    ['나 대피소에 막 도착했어', { what: null }],
    ['대피시설이 닫혀 있었어', { what: 'all_closed' }],
    ['20:09 동쪽 닫힘. 나머지 다 닫혔는지 확인 못 함', { what: 'gate_closed', unknowns: true }],
    ['한빛 생활문화센터 지하주차장으로 와', { route: null }],
    ['계단 말고 다른 입구로 가', { route: null }],
    ['1시간 전에 동쪽 문 닫혀 있었어', { time: null, what: 'gate_closed' }],
    ['20:09 동쪽 폐쇄. B 진출입로 운영. C는 몰라. 안내 다시 확인!', { what: 'gate_closed', time: '20:09', route: 'ramp', unknowns: true, advice: 'check_notice' }],
  ];
  for (const [text, expected] of cases) {
    const c = parseNote(text);
    for (const [k, v] of Object.entries(expected)) assert.equal(c[k], v, `${text} → ${k}`);
  }
  for (const empty of ['', undefined, null, 42]) {
    const c = parseNote(empty);
    assert.deepEqual([c.what, c.time, c.route, c.unknowns, c.advice], [null, null, null, false, null]);
  }
  assert.equal(noteKind(parseNote('20:09 대피소 동쪽 출입구 닫힘. 다른 입구는 몰라')), 'precise');
  assert.equal(noteKind(parseNote('ㅋㅋㅋ')), 'unparsed');
  assert.equal(noteKind(parseNote('대피소 전부 닫힘')), 'generalized');
  assert.equal(noteKind(parseNote('동쪽 출입구가 닫혀 있었어')), 'partial');
  assert.equal(noteKind(parseNote('20:09에 동쪽 출입구가 닫혀 있었어. 다른 입구 상태는 확인 못 했어.')), 'precise');
});

test('makeHandover keeps the player\'s words, sanitizes edits, and falls back to chips', () => {
  const chips = { what: 'gate_closed', time: true, route: true, unknowns: true, advice: null };
  const G = played('ramp');
  const h = makeHandover(G, { chips });
  assert.equal(h.text, composeNote(chips, { route: 'ramp', observedVia: 'sign', gateSeenAt: G.relay.gateSeenAt }));
  assert.ok(h.text.includes('20:09'));
  assert.equal(h.kind, 'precise');
  assert.equal(h.claims.parsedBy, 'chips');
  assert.equal(h.actualRoute, 'ramp');
  for (const k of ['gateSeenAt', 'sentAt', 'accessOpenAt', 'receivedAt']) assert.equal(h[k], G.relay[k], k);
  assert.equal(G.handover, h);
  assert.deepEqual(G.events.at(-1).details, { kind: 'precise', edited: false, length: h.text.length });
  assert.equal(G.events.at(-1).type, 'handover_written');
  chips.what = 'all_closed';
  assert.equal(h.chips.what, 'gate_closed', 'chips are copied');

  const edited = makeHandover(played(), { chips, edited: true, text: `  동쪽\u0007 막힘 ${'가'.repeat(200)}` });
  assert.equal(edited.text.length, MAX_NOTE);
  assert.doesNotMatch(edited.text, /[\u0000-\u001f\u007f]/);
  assert.equal(edited.claims.parsedBy, 'rules');
  assert.equal(edited.edited, true);

  for (const text of ['   ', '\u0000\u0007\n', undefined]) {
    const blank = makeHandover(played(), { chips, edited: true, text });
    assert.equal(blank.text, composeNote(chips, { route: 'stairs', observedVia: 'sign', gateSeenAt: '20:09' }));
    assert.equal(blank.edited, false);
  }
  assert.equal(makeHandover(played(), { chips, text: '무시됨' }).claims.parsedBy, 'chips', 'text counts only when edited');

  const noRelay = createGame({ seed: 3 });
  assert.equal(makeHandover(noRelay, { chips }).gateSeenAt, relayTimes(20 * 60 + 13).gateSeenAt, 'default times without G.relay');
  assert.equal(makeHandover(createGame({ seed: 3 })).kind, 'unparsed');
  for (const bad of [undefined, null, {}, { events: 'x' }]) assert.throws(() => makeHandover(bad, { chips }), TypeError);
});

test('buildPins always gives body, time and one more reason, whatever was written', () => {
  const texts = ['동쪽문 막힘ㅠ 난 계단으로 옴 이쪽으로 ㄱㄱ', 'C로 와', 'ㅋㅋㅋ', '대피소 전부 닫힘', '20:09 동쪽 폐쇄. B 진출입로 운영. C 경사로는 몰라. 안내 다시 확인'];
  const games = [];
  for (const route of ['stairs', 'ramp']) {
    for (const chips of allChips()) { const G = played(route); makeHandover(G, { chips }); games.push(G); }
    for (const text of texts) { const G = played(route); makeHandover(G, { edited: true, text }); games.push(G); }
  }
  games.push(createGame({ seed: 4 }), played('ramp'));
  const anchors = new Set(['stairs', 'ramp', 'notice', 'gate', 'access', 'start0', 'start1']);
  for (const G of games) {
    const pins = buildPins(G);
    assert.equal(pins.length, 3);
    assert.equal(pins[0].factor, 'body');
    assert.equal(pins[1].factor, 'time');
    assert.ok(['scope', 'advice', 'direction', 'place'].includes(pins[2].factor));
    for (const pin of pins) {
      assert.ok(anchors.has(pin.anchor), pin.anchor);
      assert.ok(pin.was && pin.now);
      assert.doesNotMatch(pin.was + pin.now, BLAME);
    }
    assert.equal(ENTRANCES[pins[0].anchor].seoyeonOk, false);
    if (pins[1].anchor === 'notice') assert.ok(pins[1].now.includes(G.relay?.receivedAt ?? '20:17'));
  }
  const scoped = played(); makeHandover(scoped, { chips: { what: 'all_closed' } });
  assert.equal(buildPins(scoped)[2].factor, 'scope');
  const access = played(); makeHandover(access, { edited: true, text: 'C로 와' });
  assert.equal(buildPins(access)[1].anchor, 'access');
  assert.equal(buildPins(access)[0].anchor, 'stairs', 'body pin falls back to where 정민 entered');
  // 정민 who only read the notice after arriving never "saw" the gate.
  const told = played('stairs', 'notice_after'); makeHandover(told, { chips: { what: 'all_closed' } });
  assert.doesNotMatch(buildPins(told)[2].now + readClaims(told).map(r => r.why).join(' '), /직접/);
});

test('closingLines: two calm lines for every kind', () => {
  for (const kind of ['precise', 'partial', 'generalized', 'unparsed', undefined]) {
    const lines = closingLines(kind);
    assert.equal(lines.length, 2);
    for (const l of lines) assert.doesNotMatch(l, BLAME);
  }
  assert.notDeepEqual(closingLines('generalized'), closingLines('precise'));
});

test('readClaims always lists C as after the note and cites real sources', () => {
  const statuses = new Set(['helped', 'recheck', 'mismatch', 'missing']);
  const G = played(); makeHandover(G, { chips: { what: 'gate_closed', route: true, advice: 'follow_me' } });
  const rows = readClaims(G);
  assert.ok(rows.some(r => r.label === 'C 경사로' && r.status === 'missing' && r.why.includes(G.relay.accessOpenAt)));
  assert.ok(rows.some(r => r.label === '들어간 길' && r.status === 'mismatch'));
  assert.ok(rows.some(r => r.label === '시각' && r.status === 'missing'));
  assert.ok(rows.some(r => r.label === '본 것' && r.status === 'recheck'));
  const precise = played(); makeHandover(precise, { chips: { what: 'gate_closed', time: true, unknowns: true, advice: 'check_notice' } });
  const p = readClaims(precise);
  assert.ok(p.some(r => r.label === '본 것' && r.status === 'helped'));
  assert.ok(!p.some(r => r.status === 'mismatch'));
  const access = played(); makeHandover(access, { edited: true, text: 'C로 와' });
  assert.equal(readClaims(access).find(r => r.label === 'C 경사로').status, 'recheck');
  for (const row of [...rows, ...p, ...readClaims(access)]) {
    assert.ok(SOURCES.some(s => s.id === row.sourceId), row.sourceId);
    assert.ok(statuses.has(row.status), row.status);
    assert.doesNotMatch(row.why, BLAME);
  }
  assert.deepEqual(readClaims(createGame({ seed: 5 })), []);
  assert.deepEqual(readClaims(undefined), []);
});
