import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createGame, recordEvent } from '../dist/game/state.js';
import { SOURCES } from '../dist/game/facts.js';
import { DEEDS, AXES, deeds } from '../dist/game/deeds.js';
import { peopleLines, townLines } from '../dist/game/fates.js';
import { simulateDays, applyLedger } from '../dist/game/ledger.js';
import { PATHS, LEAVE, TOWN, ORDER, ENDINGS, pickEnding, trace, nearMisses, readGallery, addToGallery, galleryView } from '../dist/game/endings.js';
import { run } from './deed-fixtures.mjs';

const COMMONS = ['manager_contact', 'manager_detour', 'rounds_with_manager', 'elder_met', 'elder_alert_knock', 'board_offer', 'mart_share'];
const FIX = {
  bus: ['bus'], walk: ['walk'], seoyeon: ['seoyeon'], missed: ['missed'], stay: ['stay'], volunteer: ['volunteer'],
  commons: ['stay', COMMONS],
  reopen: ['missed', ['clerk_contact', 'promise_store', 'store_paid', 'store_shuttered']],
  relief: ['volunteer', ['driver_text', 'yoon_reply', 'sort_supplies']],
  board: ['stay', ['signals_clear', 'rumor_checked', 'handover_precise', 'card_meet', 'blackout_radio']],
  shut: ['stay', ['hoard'], [['violence', { how: 'punch', ko: true }]]],
};
const build = (id, seed) => run(FIX[id][0], FIX[id][1], FIX[id][2], seed);
const srcIds = new Set(SOURCES.map((s) => s.id));

test('11 endings in order; each is reached by a minimal run', () => {
  assert.deepEqual(ENDINGS.map((e) => e.no), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]);
  assert.deepEqual(ENDINGS.map((e) => e.id), Object.keys(FIX));
  assert.equal(new Set(ENDINGS.map((e) => e.title)).size, 11);
  for (const id of Object.keys(FIX)) {
    const f = pickEnding(build(id));
    assert.equal(f.id, id);
    assert.equal(f.no, ENDINGS.find((e) => e.id === id).no);
    assert.ok(f.lines.length >= 1 && f.lines.every((l) => typeof l === 'string' && /[가-힣]/.test(l)), id);
  }
  assert.ok(PATHS.every((p) => LEAVE.has(p) !== TOWN.has(p)));
});

test('the same run always reaches the same ending, whatever the seed', () => {
  for (const id of Object.keys(FIX)) {
    const G = build(id);
    assert.deepEqual(pickEnding(G), pickEnding(G));
    assert.deepEqual(pickEnding(structuredClone(G)), pickEnding(G));
    for (let seed = 1; seed <= 20; seed++) assert.equal(pickEnding(build(id, seed)).id, id);
  }
});

test('selection: shut only in town, leaving never opens a world ending, ORDER breaks ties, no path is the bus', () => {
  const all = [...COMMONS, 'clerk_contact', 'promise_store', 'store_paid', 'store_shuttered', 'driver_text', 'yoon_reply', 'sort_supplies', 'signals_clear', 'rumor_checked'];
  for (const p of LEAVE) {
    assert.equal(pickEnding(run(p, all)).id, p);
    const f = pickEnding(run(p, ['hoard'], [['violence', { how: 'punch', ko: true }]]));
    assert.equal(f.id, p);
    assert.ok(f.lines.includes('몇몇 이웃은 끝내 눈을 마주치지 않았다.'));
  }
  // commons and reopen both fit exactly 1: commons comes first in ORDER
  const tie = run('stay', [...COMMONS, 'clerk_contact', 'store_paid', 'store_shuttered']);
  assert.equal(pickEnding(tie).id, 'commons');
  assert.equal(ORDER[0], 'commons');
  assert.equal(pickEnding(run('stay', [...COMMONS, 'clerk_contact', 'store_paid', 'store_shuttered', 'promise_store'])).id, 'reopen', 'higher fit wins');
  assert.equal(pickEnding(run('stay', [...COMMONS, 'hoard'], [['violence', { how: 'punch', ko: true }]])).id, 'shut', 'harm comes first');
  const harm3 = pickEnding(run('stay', ['hoard', 'rumor_spread', 'store_took']));
  assert.equal(harm3.id, 'stay');
  assert.ok(harm3.lines.includes('몇몇 이웃은 끝내 눈을 마주치지 않았다.'));
  assert.equal(pickEnding(createGame({ seed: 3 })).id, 'bus');
  const odd = createGame({ seed: 3 }); odd.know.path = 'teleport';
  assert.equal(pickEnding(odd).id, 'bus');
});

test('ending lines follow the run', () => {
  const walk = run('walk', ['driver_text', 'card_family']);
  assert.ok(pickEnding(walk).lines.some((l) => l.includes('김 기사님')) && pickEnding(walk).lines.some((l) => l.includes('대구 부모님')));
  const leg = run('walk'); leg.injury = ['leg'];
  assert.ok(pickEnding(leg).lines.some((l) => l.includes('구호 트럭')));
  assert.equal(pickEnding(leg).lines.filter((l) => l.includes('부러진 다리')).length, 1, 'the leg is told once on the walk');
  const legDriver = run('walk', ['driver_text']); legDriver.injury = ['leg'];
  assert.ok(pickEnding(legDriver).lines.some((l) => l.includes('욱신')), 'a ride from 김 기사님 does not tell the leg, so the lasting line does');
  const weak = run('walk'); weak.stats.health = 30;
  assert.ok(pickEnding(weak).lines.some((l) => l.includes('실패가 아니었다')));
  assert.ok(pickEnding(run('bus')).lines.some((l) => l.includes('우편함은 비어')));
  const letters = pickEnding(run('bus', ['seoyeon_contact', 'stairs_asked', 'water_for_seoyeon', 'bus_helped']));
  assert.ok(!letters.lines.some((l) => l.includes('우편함은 비어')) && letters.lines.some((l) => l.includes('리프트')));
  assert.deepEqual(letters.people.filter((p) => p.letter).map((p) => p.who), ['seoyeon']);
  assert.ok(pickEnding(run('missed', ['manager_contact', 'manager_detour', 'rounds_with_manager'])).lines.some((l) => l.includes('박 소장이 문을')));
  assert.ok(pickEnding(run('missed')).lines.some((l) => l.includes('아무도 문을')));
  assert.ok(pickEnding(run('seoyeon', ['stairs_asked'])).lines.some((l) => l.includes('먼저 물어볼 것')));
  assert.ok(pickEnding(run('stay', [...COMMONS, 'stock_to_manager'])).lines.includes("첫 상자에는 '302호'라고 적혀 있었다."));
});

test('reachability: each world ending has a legal in-town run that meets every threshold', () => {
  // Choices that exclude each other in play: keep one side of each.
  const EXCL = [['store_paid', 'store_iou', 'store_took'], ['handover_precise', 'handover_partial', 'handover_overgeneral'], ['stairs_asked', 'stairs_carried'],
    ['looter_gave', 'looter_escaped', 'looter_fought'], ['rumor_checked', 'rumor_spread'], ['water_for_seoyeon', 'water_declined'],
    ['shelter_water_given', 'shelter_water_received'], ['elder_alert_knock', 'elder_alert_told']];
  const BRANCH = { stay: ['rounds_with_manager', 'smoke_judged'], move: ['sort_supplies', 'shelter_fire_judged', 'fire_report'] };
  const PATHED = { seoyeon_waited: 'seoyeon', volunteered: 'volunteer', stayed: 'stay' };
  for (const e of ENDINGS.filter((x) => x.kind === 'world')) {
    const ok = [...TOWN].some((path) => ['stay', 'move'].some((branch) => [0, 1, 2].some((store) => {
      const drop = new Set([...EXCL.flatMap((g) => g.filter((id, i) => i !== (g[0] === 'store_paid' ? store : 0))), ...BRANCH[branch === 'stay' ? 'move' : 'stay']]);
      if (path === 'stay') drop.add('stock_to_manager');
      const ids = Object.keys(DEEDS).filter((id) => !drop.has(id) && (!PATHED[id] || PATHED[id] === path) && Object.values(DEEDS[id].w).every((v) => v >= 0));
      const sum = Object.fromEntries(AXES.map((k) => [k, ids.reduce((s, id) => s + (DEEDS[id].w[k] ?? 0), 0)]));
      return e.need.every(([k, t]) => sum[k] >= t);
    })));
    assert.ok(ok, e.id);
  }
});

test('trace: 3–5 items in order, from deeds present, with real sources', () => {
  for (const id of ['commons', 'reopen', 'relief', 'board', 'shut']) {
    const G = build(id), t = trace(G, id), present = new Set(deeds(G).map((d) => d.id));
    assert.ok(t.length >= 3 && t.length <= 5, `${id}: ${t.length}`);
    for (const item of t) {
      assert.ok(item.id === 'path' || present.has(item.id), item.id);
      assert.ok(item.text && item.when, item.id);
      assert.ok(item.sourceIds.every((s) => srcIds.has(s)), item.id);
    }
    const seqs = t.map((x) => (x.id === 'path' ? G.events.findLast((e) => e.type === 'path').seq : deeds(G).find((d) => d.id === x.id).seq));
    assert.deepEqual(seqs, [...seqs].sort((a, b) => a - b), id);
    assert.equal(t[0].id === 'path' || t.some((x) => x.id === 'path'), id !== 'shut', id);
  }
  const big = run('stay', [...COMMONS, 'card_neighbors', 'door_note', 'water_for_seoyeon']);
  assert.equal(trace(big, 'commons').length, 5);
  for (const id of ['bus', 'walk', 'stay']) assert.ok(trace(run(id), id).length <= 5);
  assert.match(trace(build('board'), 'board').find((x) => x.id === 'path').when, /^5장 · D\+12 13:00$/);
});

test('near misses: at most two, never the ending reached, with a tip', () => {
  for (const id of Object.keys(FIX)) {
    const G = build(id), m = nearMisses(G, pickEnding(G).id);
    assert.ok(m.length <= 2 && m.every((x) => x.id !== pickEnding(G).id && x.title && x.hint && x.tip), id);
    if (LEAVE.has(id)) assert.ok(m.every((x) => x.hint.startsWith('연무동에 남았다면 — ')));
  }
  assert.match(nearMisses(build('reopen'), 'reopen')[0].tip, / — /);
});

test('people: four neighbours for every ending, then strangers; lines follow what was done', () => {
  for (const id of Object.keys(FIX)) {
    const p = peopleLines(build(id), id);
    assert.deepEqual(p.slice(0, 4).map((x) => x.who), ['seoyeon', 'manager', 'clerk', 'elder'], id);
    assert.ok(p.length <= 7 && p.slice(4).every((x) => x.who === 'stranger' && x.name === '' && x.present === false));
    assert.ok(p.every((x) => typeof x.line === 'string' && x.line && typeof x.present === 'boolean'));
    assert.equal(p[0].present, false);
  }
  const line = (G, who) => peopleLines(G, pickEnding(G).id).find((x) => x.who === who);
  assert.match(line(run('bus', ['seoyeon_contact', 'promise_note']), 'seoyeon').line, /소식을 받지 못해/);
  assert.doesNotMatch(line(run('bus', ['seoyeon_contact', 'promise_note'], [['note_left', {}]]), 'seoyeon').line, /소식을 받지 못해/);
  assert.match(line(run('bus', ['manager_contact', 'candle_left']), 'manager').line, /촛불을 꺼 두었다/);
  assert.match(line(run('bus', ['clerk_contact', 'store_took']), 'clerk').line, /빈 칸/);
  const stay = line(run('stay', ['elder_alert_knock']), 'elder'), bus = line(run('bus', ['elder_alert_knock']), 'elder');
  assert.match(stay.line, /302호 몫/); assert.equal(stay.present, true);
  assert.match(bus.line, /안부/); assert.equal(bus.present, false);
  assert.equal(line(run('stay'), 'elder').present, false, 'never met: not on the roam');
  assert.equal(line(build('reopen'), 'clerk').present, true);
  const many = peopleLines(run('bus', ['driver_text', 'looter_gave', 'mart_share', 'shelter_water_given', 'rumor_corrected']), 'bus');
  assert.equal(many.length, 7);
  const rumor = run('stay', ['rumor_spread']); // told once, in the town lines
  assert.ok(!peopleLines(rumor, 'stay').some((p) => p.line.includes('폐쇄')) && townLines(rumor, 'stay').some((l) => l.includes('한참 동안')));
});

test('town lines are plain strings', () => {
  for (const id of Object.keys(FIX)) {
    const t = townLines(build(id), id);
    assert.ok(t.length >= 2 && t.length <= 3 && t.every((x) => typeof x === 'string' && x), id);
  }
  assert.equal(townLines(run('bus'), 'bus')[0], '연무동 소식은 박 소장의 문자로 들었다.');
});

// Map labels (C 경사로) and day counters (D+14) are the game's own notation; any other Latin is a leak.
const LATIN_OK = /D[+-]\d+|[ABC](?= ?(경사로|계단|진출입로|출입구))/g;
const BANNED = ['활성탄', '입을 벌', '무조건 높은', '지하철로', '마트로 대피', '터널', '생김새', '수상해 보이는', '날고기', '죽었', '사망'];

test('content guard: no stray Latin and no unverified or grim wording', () => {
  const strings = [];
  for (const d of Object.values(DEEDS)) strings.push(d.short, d.line, d.cue ?? '');
  for (const e of ENDINGS) strings.push(e.title, e.place, e.hint);
  const runs = [...Object.keys(FIX).map((id) => build(id)), run('bus', Object.keys(DEEDS).filter((id) => DEEDS[id].src === 'new')),
    run('stay', ['elder_candle_tip', 'fire_warned', 'water_declined', 'store_iou', 'looter_escaped', 'looter_fought', 'shelter_water_received', 'lead_honest', 'yoon_reply'])];
  for (const G of runs) {
    const f = pickEnding(G);
    strings.push(f.title, f.place, ...f.lines, ...f.people.map((p) => p.line), ...f.town,
      ...f.trace.flatMap((t) => [t.text, t.when]), ...f.missed.flatMap((m) => [m.title, m.hint, m.tip]));
  }
  for (const s of strings) {
    assert.doesNotMatch(s.replace(LATIN_OK, ''), /[A-Za-z]/, s);
    for (const b of BANNED) assert.ok(!s.includes(b), `${b}: ${s}`);
  }
  const src = ['deeds', 'fates', 'endings'].map((f) => readFileSync(new URL(`../dist/game/${f}.js`, import.meta.url), 'utf8')).join('\n');
  for (const b of BANNED) assert.ok(!src.includes(b), b);
});

test('an old save with only legacy events still gets an ending', () => {
  const G = createGame({ seed: 8 });
  G.karma = -5;
  for (const [type, d] of [['help', { who: 'seoyeon', what: '계단에서 보행기를 옆에서 들어 주었다' }], ['help', { who: 'manager', what: '마지막까지 건물을 지키며 길을 안내했다' }],
    ['looter', { result: 'gave' }], ['candle_left', {}], ['path', { path: 'stay' }]]) recordEvent(G, type, d);
  G.know.path = 'stay';
  const f = pickEnding(G);
  assert.equal(f.id, 'stay');
  assert.ok(f.trace.length >= 1 && f.people.length >= 4);
});

test('gallery: sanitised reads, round trip, unseen endings stay hidden', () => {
  for (const raw of [null, undefined, 42, 'not json', '{"seen":[1]}', { seen: { nope: { at: '2026-10-30', runs: 1, trace: [] } } }, { seen: { bus: { at: 'yesterday' } } }]) {
    assert.deepEqual(readGallery(raw), { seen: {} });
  }
  assert.deepEqual(readGallery({ seen: { bus: { at: '2026-10-30', runs: -2, trace: ['stairs_asked', 'evil', 7, 'path'] } } }),
    { seen: { bus: { at: '2026-10-30', runs: 1, trace: ['stairs_asked'] } } });
  let g = addToGallery(readGallery(null), 'reopen', ['path', 'clerk_contact', 'store_paid', 'store_shuttered'], '2026-10-30');
  assert.deepEqual(readGallery(JSON.stringify(g)), g);
  g = addToGallery(g, 'reopen', ['store_paid'], '2026-11-02');
  assert.deepEqual(g.seen.reopen, { at: '2026-10-30', runs: 2, trace: ['store_paid'] });
  assert.deepEqual(addToGallery(g, 'nope', [], '2026-11-02'), g);
  const v = galleryView(addToGallery(g, 'bus', ['stairs_asked', 'water_for_seoyeon', 'door_note', 'card_meet'], '2026-11-03'));
  assert.equal(v.length, 11);
  assert.deepEqual(v.map((x) => x.id), ENDINGS.map((e) => e.id));
  const bus = v.find((x) => x.id === 'bus'), walk = v.find((x) => x.id === 'walk');
  assert.equal(bus.title, '남쪽에서 온 편지'); assert.equal(bus.shorts.length, 3); assert.equal(bus.at, '2026-11-03');
  assert.equal(walk.seen, false); assert.equal(walk.title, null); assert.equal(walk.place, null); assert.ok(walk.hint);
});

// Carried over from the final build's own ending tests (Round 6): the same intents, read from the deed ledger.
test('wording: the 서연 and missed openings, and no blame at low health', () => {
  const sy = createGame({ seed: 3 }); sy.know.path = 'seoyeon';
  assert.doesNotMatch(pickEnding(sy).lines[0], /휠체어/);
  const m = createGame({ seed: 3 }); m.know.path = 'missed';
  assert.doesNotMatch(pickEnding(m).lines[0], /집결지에 닿았을 때/, 'missed can happen anywhere');
  for (const path of PATHS) {
    const g = createGame({ seed: 3 }); g.know.path = path; g.stats.health = 20;
    const f = pickEnding(g);
    for (const l of [...f.lines, ...f.people.map((p) => p.line), ...f.town]) assert.doesNotMatch(l, /탓|잘못|실수/);
  }
});

test('help pressed again, or kindness received, never opens the shared kitchen; deeds do', () => {
  const G = run('stay');
  for (let i = 0; i < 4; i++) recordEvent(G, 'help', { who: 'manager', what: '같은 말' });
  recordEvent(G, 'help', { who: 'clerk', what: '쪽지와 현금 통', by: 'other' });
  recordEvent(G, 'help', { who: 'seoyeon', kind: 'powerbank', what: '보조배터리', by: 'other' });
  assert.equal(pickEnding(G).id, 'stay');
  assert.ok(!pickEnding(G).lines.some((l) => /공동 부엌/.test(l)));
  assert.ok(pickEnding(build('commons')).lines.some((l) => /공동 부엌/.test(l)));
});

test('elder, night knock and day ledger: people lines and one ledger line', () => {
  const G = run('bus');
  recordEvent(G, 'help', { who: 'elder', what: '부축했다' });
  recordEvent(G, 'help', { who: 'stranger', what: '물 한 병' });
  G.day = 3;
  applyLedger(G, simulateDays(G, { water: 2, meals: 2, power: 'phone' }, 2));
  const f = pickEnding(G), people = f.people.map((p) => p.line);
  assert.ok(people.some((l) => /304호 어르신/.test(l) && /안부/.test(l)));
  assert.ok(people.some((l) => /자원봉사자/.test(l)));
  assert.equal(f.lines.filter((l) => /하루치/.test(l)).length, 1);
});
