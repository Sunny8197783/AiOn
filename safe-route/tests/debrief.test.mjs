import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { FAIL_TYPES, failDebrief, chapterSummary, endingRecord, PREP, PEOPLE } from '../dist/game/debrief.js';
import { simulateDays, applyLedger } from '../dist/game/ledger.js';
import { createGame, recordEvent, buy, pack, check, consume, note, spendSlot, adjust, hhmm, trade, drainCharge } from '../dist/game/state.js';
import { recordEncounter, evalExtinguisher, evalBag } from '../dist/game/drills.js';
import { CATALOG, BAG_LIMIT } from '../dist/game/items.js';
import { SOURCES, FACTS, fact } from '../dist/game/facts.js';

const prices = Object.fromEntries(Object.entries(CATALOG).map(([id, c]) => [id, c.price]));
function game(cart = {}) {
  const G = createGame({ seed: 5 });
  G.money = 1e7;
  buy(G, cart, prices);
  return G;
}
const srcIds = new Set(SOURCES.map(s => s.id));
const BLAME = /잘못|실수|탓|게으|무책임|undefined|null|NaN|\[object/;

// What the director really passes (dist/story/*.js), read from source so new calls are covered too.
const storyDir = new URL('../dist/story/', import.meta.url);
const story = readdirSync(storyDir).filter(f => f.endsWith('.js')).map(f => readFileSync(new URL(f, storyDir), 'utf8')).join('\n');
const storyResults = [...new Set([...story.matchAll(/recordEncounter\(G, '\w+', '([\w-]+)'\)/g)].map(m => m[1]))];
const storyPreps = [...new Set([...story.matchAll(/payoff\('(\w+)'/g)].map(m => m[1]))];
const storyContacts = [...new Set([...story.matchAll(/\.contact = (?:[^;]*?)'(\w+)'/g)].map(m => m[1]))];

test('every FACTS sourceId exists; SOURCES are unique https links', () => {
  assert.equal(srcIds.size, SOURCES.length);
  assert.equal(new Set(FACTS.map(f => f.id)).size, FACTS.length);
  for (const f of FACTS) {
    assert.ok(srcIds.has(f.sourceId), `${f.id} -> ${f.sourceId}`);
    assert.ok(f.text.trim(), f.id);
  }
  for (const s of SOURCES) assert.equal(new URL(s.url).protocol, 'https:', s.id);
});

test('failDebrief gives three calm Korean lines and real sources for every type', () => {
  assert.deepEqual(FAIL_TYPES, ['dead', 'late', 'exhausted', 'fire', 'lostWay']);
  const fresh = createGame({ seed: 9 });
  const ready = alertReadyGame();
  for (const G of [fresh, ready]) for (const type of FAIL_TYPES) {
    const d = failDebrief(G, type);
    assert.deepEqual(Object.keys(d), ['title', 'seen', 'missed', 'next', 'sourceIds']);
    for (const k of ['title', 'seen', 'missed', 'next']) {
      assert.ok(typeof d[k] === 'string' && d[k].trim(), `${type}.${k}`);
      assert.match(d[k], /[가-힣]/);
      assert.doesNotMatch(d[k], BLAME, `${type}.${k}: ${d[k]}`);
    }
    assert.ok(d.sourceIds.length);
    assert.equal(new Set(d.sourceIds).size, d.sourceIds.length);
    for (const s of d.sourceIds) assert.ok(srcIds.has(s), s);
  }
  assert.throws(() => failDebrief(fresh, 'boom'));
  assert.throws(() => failDebrief(fresh));
});

function alertReadyGame() {
  const G = game({ water_500: 1 });
  Object.assign(G.know, { shelterLocated: true, entrancesChecked: true, routeChecked: true, extinguisherLocated: true, gateClosedSeen: true });
  pack(G, 'water_500');
  return G;
}

test('late: lines follow what was checked', () => {
  const G = createGame({ seed: 1 });
  let d = failDebrief(G, 'late');
  assert.match(d.seen, /확인 전/);
  assert.match(d.next, /대피소·경로 확인/);
  G.know.shelterLocated = true;
  assert.match(failDebrief(G, 'late').seen, /생활문화센터/);
  Object.assign(G.know, { entrancesChecked: true, routeChecked: true, gateClosedSeen: true });
  d = failDebrief(G, 'late');
  assert.match(d.seen, /동쪽 출입구/);
  assert.match(d.next, /비상가방 꾸리기/);
});

test('exhausted: overweight bag, no water, or no rest', () => {
  const G = game({ water_2l: 2 });
  pack(G, 'water_2l', 4);
  let d = failDebrief(G, 'exhausted');
  assert.ok(d.missed.includes('8.2kg') && d.missed.includes(`${BAG_LIMIT.weightKg}kg`), d.missed);
  assert.ok(d.sourceIds.includes(fact('bag_foil').sourceId));
  const dry = createGame({ seed: 2 });
  assert.match(failDebrief(dry, 'exhausted').missed, /마실 물/);
  assert.match(failDebrief(alertReadyGame(), 'exhausted').next, /휴식/);
  assert.match(failDebrief(dry, 'exhausted').seen, /갈증은 좋음/);
  adjust(dry, 'thirst', 80);
  assert.match(failDebrief(dry, 'exhausted').seen, /갈증은 나쁨/);
});

test('fire: the last extinguisher encounter picks the FACTS line', () => {
  const G = createGame({ seed: 3 });
  assert.equal(failDebrief(G, 'fire').missed, fact('fire_shout').text);
  assert.match(failDebrief(G, 'fire').seen, /확인하기 전/);
  recordEncounter(G, 'extinguisher', 'escalated'); // what story/evac.js passes
  assert.equal(failDebrief(G, 'fire').missed, fact('fire_big').text);
  recordEncounter(G, 'extinguisher', evalExtinguisher(['shout', 'aim', 'pin', 'squeeze', 'sweep'], { fire: 'small' }));
  const d = failDebrief(G, 'fire');
  assert.equal(d.missed, fact('ext_1').text, 'first error was the pin');
  assert.ok(d.sourceIds.includes('mois_extinguisher'));
  G.know.extinguisherLocated = true;
  assert.match(failDebrief(G, 'fire').seen, /알고 있었습니다/);
});

test('lostWay uses the seed times', () => {
  let d;
  const L = createGame({ seed: 11 });
  d = failDebrief(L, 'lostWay');
  assert.ok(d.seen.includes(hhmm(L.cond.commsWeakAt)));
  assert.equal(d.missed, fact('shelter_signs').text);
  L.know.routeChecked = true;
  assert.match(failDebrief(L, 'lostWay').next, /두 번째 경로/);
});

test('chapterSummary counts only that chapter', () => {
  const G = game();
  G.chapter = 'prep';
  spendSlot(G, 'mart', 2);
  spendSlot(G, 'bag', 1);
  buy(G, { water_500: 3, batteries_aa: 1 }, prices);
  check(G, 'flashlight');
  recordEncounter(G, 'bag', 'partial');
  recordEncounter(G, 'extinguisher', evalExtinguisher(['retreat'], { fire: 'small' }));
  note(G, '라디오 89.1MHz 재난방송', 'official');
  note(G, '내일 물이 끊긴대', 'rumor');
  recordEvent(G, 'help', { who: 'seoyeon', what: '연락 방법을 정했다' });
  recordEvent(G, 'payoff', { prep: 'bag', moment: '현관에 둔 배낭을 메고 바로 나섰다' });
  G.chapter = 'dday';
  recordEvent(G, 'payoff', { prep: 'radio', moment: '안내를 바로 들었다' });

  const s = chapterSummary(G, 'prep');
  assert.deepEqual(s.lines, [
    '준비에 쓴 시간: 3칸',
    '산 물건: 생수 500mL 3병, AA 건전지 4개 1팩',
    '점검해서 쓸 수 있게 된 것: 손전등(LED)',
    '비상가방: 몇 가지가 빠졌습니다',
    '소화기: 대피하며 알렸습니다',
    '수첩에 적은 정보 2개 (공식 안내 1, 소문·미확인 1)',
    '오서연: 연락 방법을 정했다',
    '지금 상태: 체력 좋음, 갈증 좋음, 피로 좋음, 긴장 좋음',
  ]);
  assert.deepEqual(s.payoffs, [{ prep: '비상가방', moment: '현관에 둔 배낭을 메고 바로 나섰다', id: 'bag' }]);
  const d = chapterSummary(G, 'dday');
  assert.equal(d.lines.length, 1);
  assert.deepEqual(d.payoffs.map(p => p.id), ['radio']);
  assert.deepEqual(chapterSummary(createGame({ seed: 1 }), 'nothing'), { lines: ['지금 상태: 체력 좋음, 갈증 좋음, 피로 좋음, 긴장 좋음'], payoffs: [] });
  adjust(G, 'thirst', 80);
  adjust(G, 'fatigue', 30);
  adjust(G, 'health', -75);
  assert.equal(chapterSummary(G, 'x').lines[0], '지금 상태: 체력 나쁨, 갈증 나쁨, 피로 보통, 긴장 좋음');
});

test('no raw result id or prep id reaches the player', () => {
  assert.ok(storyResults.length >= 5 && storyPreps.length >= 5, 'story calls were found');
  const G = createGame({ seed: 1 });
  G.chapter = 'c';
  const results = [...storyResults, 'out', 'incomplete', 'escalated', 'retreat-ok', 'something-new',
    evalExtinguisher(['retreat'], { fire: 'large' }), evalBag(G, 'alert'), { station: null }, null];
  for (const r of results) recordEncounter(G, 'radio', r);
  for (const r of ['sorted', 'misled', 'checked', 'doubted']) recordEncounter(G, 'info', r);
  for (const prep of [...storyPreps, ...Object.keys(PREP)]) recordEvent(G, 'payoff', { prep, moment: '순간' });
  for (const who of Object.keys(PEOPLE)) recordEvent(G, 'help', { who, what: '도왔다' });
  trade(G, { ramen: 1 }, { batteries_old: 1 }, 'trader');
  G.items.flashlight.checked = true; drainCharge(G, 'flashlight', 100);
  applyLedger(G, simulateDays(G, { water: 2, meals: 2, power: 'radio' }, 2));
  const s = chapterSummary(G, 'c');
  for (const line of s.lines) assert.doesNotMatch(line.replace(/AA|LED|\d+m?L/g, ''), /[A-Za-z]/, line); // units and item names (2L, 500mL, AA, LED) are fine
  for (const p of [...s.payoffs, ...endingRecord(G).helped]) assert.notEqual(p.prep, p.id, `label for payoff '${p.id}'`);
  assert.ok(storyContacts.length >= 3, 'contact literals were found');
  for (const c of storyContacts) { G.rel.seoyeon.contact = c; for (const p of endingRecord(G).people) assert.doesNotMatch(`${p.who}${p.what}`, /[A-Za-z]/, p.what); }
  for (const x of endingRecord(G).ripples) assert.doesNotMatch(`${x.label}${x.cause.text}`, /[A-Za-z]/);
});

test('endingRecord on a fresh game', () => {
  const r = endingRecord(createGame({ seed: 1 }));
  assert.deepEqual(r.helped, []);
  assert.deepEqual(r.unused, [{ item: CATALOG.flashlight.name, why: '건전지를 넣지 않아 켜 볼 수 없었습니다.' }]);
  assert.equal(r.unchecked.length, 9);
  assert.deepEqual(r.people, []);
  assert.deepEqual(r.practice, { text: fact('shelter_find').text, sourceId: 'kr_shelters' });
});

test('endingRecord follows payoffs, unused items, know flags and people', () => {
  const G = game({ radio: 1, batteries_aa: 1, powerbank: 1, tape: 1, water_500: 2 });
  check(G, 'radio');
  consume(G, 'water_500', 1);
  G.items.bogus = { home: 1, bag: 0, bought: 1, checked: false, used: 0 }; // dropped from the catalogue
  recordEvent(G, 'payoff', { prep: 'flashlight', moment: '정전 직후 손전등을 바로 켰다' });
  recordEvent(G, 'help', { who: 'seoyeon', what: '계단에서 보행기를 옆에서 들어 주었다' });
  G.rel.manager.contact = 'both';
  G.rel.seoyeon.contact = 'sms';
  G.know.shelterLocated = true;
  const r = endingRecord(G);
  assert.deepEqual(r.helped, [{ prep: '손전등 건전지', moment: '정전 직후 손전등을 바로 켰다', id: 'flashlight' }]);
  const why = Object.fromEntries(r.unused.map(u => [u.item, u.why]));
  assert.deepEqual(Object.keys(why).sort(), ['flashlight', 'radio', 'powerbank', 'tape'].map(id => CATALOG[id].name).sort());
  assert.equal(why[CATALOG.powerbank.name], '충전해 두지 않아 쓸 수 없었습니다.');
  assert.equal(why[CATALOG.tape.name], '사 두었지만 쓸 일이 없었습니다.');
  assert.ok(!(CATALOG.water_500.name in why) && !(CATALOG.batteries_aa.name in why), 'used items are not unused');
  assert.ok(!r.unchecked.includes('가까운 대피소 위치'));
  assert.deepEqual(r.people, [
    { who: '오서연', what: '계단에서 보행기를 옆에서 들어 주었다' },
    { who: '박영수 소장', what: '비상 연락 방법을 정했습니다 (문자·문에 쪽지)' },
    { who: '오서연', what: '비상 연락 방법을 정했습니다 (문자)' },
  ]);
  assert.deepEqual(r.practice, { text: fact('shelter_signs').text, sourceId: 'law_sign' }, 'next unchecked flag');

  Object.assign(G.know, { entrancesChecked: true, routeChecked: true, extinguisherLocated: true, radioChecked: true, contactsCard: true, docsCopied: true, meetPoint: '놀이터', waterPoint: true });
  const all = endingRecord(G);
  assert.deepEqual(all.unchecked, []);
  assert.deepEqual(all.practice, { text: fact('shelter_know').text, sourceId: 'mois_four' });
  assert.ok(srcIds.has(all.practice.sourceId));
});

test('fire fail card follows the scene it came from', () => {
  const G = createGame({ seed: 1 });
  const up = failDebrief(G, 'fire', { scene: 'upstairs' }), sh = failDebrief(G, 'fire', { scene: 'shelter' }), co = failDebrief(G, 'fire');
  assert.match(up.missed, /창문을 닫고|대기/);
  assert.notEqual(up.title, co.title); assert.notEqual(sh.title, co.title);
  for (const d of [up, sh]) assert.ok(d.sourceIds.length && d.next);
});

test('lostWay via rumor: three calm lines and the info sources', () => {
  const G = createGame({ seed: 2 });
  const d = failDebrief(G, 'lostWay', { via: 'rumor' });
  assert.equal(d.title, '소문을 따라가느라 길을 잃었습니다');
  assert.equal(d.missed, fact('info_rumor').text);
  assert.match(d.next, /한 번 더 확인/);
  for (const k of ['seen', 'missed', 'next']) assert.doesNotMatch(d[k], BLAME);
  assert.deepEqual(d.sourceIds, [...new Set(['info_rumor', 'info_harm', 'evac_note'].map(id => fact(id).sourceId))]);
  assert.notEqual(failDebrief(G, 'lostWay').title, d.title, 'the plain lostWay card stays');
});

test('chapter summary lists trades, the day ledger and flat batteries in Korean', () => {
  const G = game({ bars: 3, radio: 1, batteries_aa: 1 });
  check(G, 'radio');
  G.chapter = 'endure';
  trade(G, { bars: 3 }, { batteries_old: 1 }, 'trader');
  applyLedger(G, simulateDays(G, { water: 2, meals: 2, power: 'powerbank' }, 2));
  drainCharge(G, 'radio', 100);
  const lines = chapterSummary(G, 'endure').lines;
  assert.ok(lines.includes('물물교환(102동 주민): 건빵·에너지바 3봉 → AA 건전지(쓰던 것) 1팩'), lines.join('\n'));
  assert.ok(lines.includes('하루 배분: 물 2L · 두 끼 · 보조배터리'));
  assert.ok(lines.includes('건전지가 다 닳은 것: 휴대용 라디오(건전지식)'));
  const why = Object.fromEntries(endingRecord(G).unused.map(u => [u.item, u.why]));
  assert.equal(why['휴대용 라디오(건전지식)'], '건전지가 다 닳아 더 켤 수 없었습니다.');
});

test('endingRecord: one people row per person, new neighbours named, ripples, curtain counted', () => {
  const G = game({ blackout_curtain: 1 });
  for (let i = 0; i < 3; i++) recordEvent(G, 'help', { who: 'manager', what: '마지막까지 건물을 지키며 길을 안내했다' });
  recordEvent(G, 'help', { who: 'seoyeon', what: '계단에서 보행기를 옆에서 들어 주었다' });
  recordEvent(G, 'help', { who: 'seoyeon', what: '급수소에서 물 한 통을 받아 3층까지 날라 주었다' });
  recordEvent(G, 'help', { who: 'elder', what: '304호 어르신을 부축해 함께 내려갔다' });
  recordEvent(G, 'help', { who: 'stranger', what: '정보' });
  const r = endingRecord(G);
  assert.deepEqual(r.people, [
    { who: '박영수 소장', what: '마지막까지 건물을 지키며 길을 안내했다' },
    { who: '오서연', what: '계단에서 보행기를 옆에서 들어 주었다 · 급수소에서 물 한 통을 받아 3층까지 날라 주었다' },
    { who: '304호 어르신', what: '304호 어르신을 부축해 함께 내려갔다' },
    { who: '문을 두드린 이웃', what: '정보' },
  ]);
  assert.deepEqual(r.ripples, []);
  assert.ok(r.unused.some(u => u.item === '암막 커튼'), 'bought and never hung');
  G.know.curtain = true;
  assert.ok(!endingRecord(G).unused.some(u => u.item === '암막 커튼'), 'hung: used');
  G.chapter = 'prep'; recordEvent(G, 'tub_filled', {}); G.day = 1; recordEvent(G, 'payoff', { prep: 'tub', moment: '세수했다' });
  assert.equal(endingRecord(G).ripples.length, 1);
});
