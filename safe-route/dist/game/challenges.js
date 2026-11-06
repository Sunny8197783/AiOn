// Challenges (도전 과제) and run records for coming back: missions read from the deed ledger as it stood at the
// ending (story/evac.js ending()), chapter times from the chapter_time events app.js writes, best times and a top-5 run board kept per
// browser beside the ending gallery. Runs rank by challenges first, retries next, time last. Pure: no DOM.
import { deeds, promises } from './deeds.js';
import { ENDINGS, pickEnding, readGallery, addToGallery } from './endings.js';

export const PUBLIC_URL = 'https://sunny8197783.github.io/AiOn/';
export const TIMED = ['prologue', 'prep', 'dday', 'endure', 'crossroads', 'evac'];
const TOP = 5, DAY = /^\d{4}-\d{2}-\d{2}$/, IDS = new Set(ENDINGS.map((e) => e.id));

// test({ has, count, ps, st, seen }): ps = promises at the end, st = runStats, seen = endings this browser has seen.
export const CHALLENGES = [
  { id: 'info_first', title: '공식 안내부터', hint: '정전되면 라디오부터 켜고, 대피소 소문은 공식 안내로 확인하기', test: (c) => c.has('blackout_radio') && c.has('rumor_checked') },
  { id: 'rumor_stop', title: '소문은 나에게서 멈춘다', hint: '확인한 내용을 소문을 들은 이웃에게 알려 주기', test: (c) => c.has('rumor_corrected') && !c.has('rumor_spread') },
  { id: 'precise', title: '본 것만 정확히', hint: '서연에게 내가 본 시각과 자리만 정확히 전하기', test: (c) => c.has('handover_precise') },
  { id: 'network', title: '연락망', hint: '서연·박 소장·수아와 비상 연락 방법을 모두 정하기', test: (c) => c.count(['seoyeon_contact', 'manager_contact', 'clerk_contact']) === 3 },
  { id: 'ask_first', title: '먼저 묻고 돕기', hint: '도와드리기 전에 먼저 여쭙기, 세 번', test: (c) => c.count(['stairs_asked', 'register_helped', 'queue_carry_elder', 'bus_helped']) >= 3 },
  { id: 'share', title: '나누며 버티기', hint: '가진 물과 먹을 것을 두 번 이상 이웃과 나누기', test: (c) => c.count(['mart_share', 'shelter_water_given', 'stock_to_manager']) >= 2 },
  { id: 'elder', title: '304호를 잊지 않았다', hint: '304호 어르신을 세 번 이상 챙기기', test: (c) => c.count(['elder_met', 'elder_candle_tip', 'elder_alert_knock', 'elder_alert_told', 'queue_carry_elder', 'elder_visit', 'fire_warned']) >= 3 },
  { id: 'promises', title: '지킨 약속', hint: '이웃과 약속을 두 개 이상 하고, 하나도 어기지 않기', test: (c) => c.ps.length >= 2 && c.ps.every((p) => p.state === 'kept') },
  { id: 'fire', title: '불 앞의 판단', hint: '복도에 불이 나면 "불이야!" 외치고, 끌지 물러날지 판단하기', test: (c) => c.has('fire_warned') && c.has('fire_handled') },
  { id: 'no_hoard', title: '사재기 없이', hint: '사재기도, 값을 남기지 않고 가져온 물건도 없이 끝까지', test: (c) => !c.has('hoard') && !c.has('store_took') },
  { id: 'hands_off', title: '누구에게도 손대지 않고', hint: '시비가 붙어도 맞서지 않고 자리를 피하기', test: (c) => !c.has('violence') && !c.has('looter_fought') },
  { id: 'clean', title: '한 번에', hint: '한 번도 다시 하지 않고 처음부터 엔딩까지', test: (c) => c.st.complete && c.st.retries === 0 },
  { id: 'endings3', title: '세 갈래 길', hint: '서로 다른 엔딩 세 개 보기', test: (c) => c.seen >= 3 },
];

const okMs = (v) => Number.isInteger(v) && v > 0 && v < 1e9;
const byRank = (a, b) => b.n - a.n || a.retries - b.retries || a.ms - b.ms;
export const fmtMs = (ms) => {
  const s = Math.round(ms / 1000), h = Math.floor(s / 3600), m = Math.floor(s / 60) % 60;
  return h ? `${h}시간 ${m}분` : `${m}분 ${String(s % 60).padStart(2, '0')}초`;
};

// { by: { [chapter]: { id, ms, retries, partial? } }, ms, retries, complete } — complete = every chapter timed from its start.
export function runStats(G) {
  const by = {};
  for (const e of G.events) if (e.type === 'chapter_time' && TIMED.includes(e.details?.id) && okMs(e.details.ms)) by[e.details.id] = e.details;
  const ts = Object.values(by);
  return { by, ms: ts.reduce((s, t) => s + t.ms, 0), retries: ts.reduce((s, t) => s + (t.retries ?? 0), 0), complete: TIMED.every((id) => by[id] && !by[id].partial) };
}

export function runChallenges(G, seen = 0) {
  const done = new Set(deeds(G).map((d) => d.id)), has = (id) => done.has(id);
  const c = { has, count: (ids) => ids.filter(has).length, ps: promises(G, { final: true }), st: runStats(G), seen };
  return CHALLENGES.filter((ch) => ch.test(c)).map((ch) => ch.id);
}

// Sanitises the stored { done: { [challengeId]: day }, best: { [chapter]: ms }, runs: [{ at, ending, n, ms, retries }] }.
export function readRecords(raw) {
  let r = raw;
  if (typeof raw === 'string') try { r = JSON.parse(raw); } catch { r = null; }
  const obj = (v) => (v && typeof v === 'object' && !Array.isArray(v) ? v : {});
  r = obj(r);
  const done = {}, best = {};
  for (const [id, at] of Object.entries(obj(r.done))) if (CHALLENGES.some((c) => c.id === id) && typeof at === 'string' && DAY.test(at)) done[id] = at;
  for (const [id, ms] of Object.entries(obj(r.best))) if (TIMED.includes(id) && okMs(ms)) best[id] = ms;
  const runs = (Array.isArray(r.runs) ? r.runs : []).filter((x) => x && typeof x.at === 'string' && DAY.test(x.at) && IDS.has(x.ending)
    && Number.isInteger(x.n) && x.n >= 0 && x.n <= CHALLENGES.length && okMs(x.ms) && Number.isInteger(x.retries) && x.retries >= 0)
    .map(({ at, ending, n, ms, retries }) => ({ at, ending, n, ms, retries })).sort(byRank).slice(0, TOP);
  return { done, best, runs };
}

// Adds a finished run. news: { done, fresh (first time ever), bests (beat an earlier best), rank (1..5 or null) }.
export function addRun(raw, G, ending, seen, at) {
  const rec = readRecords(raw), st = runStats(G), done = runChallenges(G, seen);
  const fresh = done.filter((id) => !rec.done[id]);
  for (const id of fresh) rec.done[id] = at;
  const bests = [];
  for (const [id, t] of Object.entries(st.by)) {
    if (t.partial || (rec.best[id] !== undefined && rec.best[id] <= t.ms)) continue;
    if (rec.best[id] !== undefined) bests.push(id);
    rec.best[id] = t.ms;
  }
  let rank = null;
  if (st.complete && IDS.has(ending) && DAY.test(at)) {
    const mine = { at, ending, n: done.length, ms: st.ms, retries: st.retries };
    rec.runs = [...rec.runs, mine].sort(byRank).slice(0, TOP);
    rank = rec.runs.indexOf(mine) + 1 || null;
  }
  return { rec, news: { done, fresh, bests, rank } };
}

// A finished run, as stored: the ending gallery and the records with it, plus what this run added (news).
export function finishRun(G, galleryRaw, recordsRaw, at) {
  const fate = pickEnding(G), gal = addToGallery(readGallery(galleryRaw), fate.id, fate.trace.map((t) => t.id), at);
  return { gal, ...addRun(recordsRaw, G, fate.id, Object.keys(gal.seen).length, at) };
}

// Wordle-style result to paste to friends: same squares for the same challenges, so runs compare at a glance.
export function shareText(fate, news, st) {
  const done = new Set(news?.done ?? []);
  return [`〈경보 이전〉 엔딩 ${fate.no} · ${fate.title}`,
    `도전 과제 ${done.size}/${CHALLENGES.length} ${CHALLENGES.map((c) => (done.has(c.id) ? '🟩' : '⬜')).join('')}`,
    st.complete ? `기록 ${fmtMs(st.ms)} · 다시 하기 ${st.retries}번` : null,
    PUBLIC_URL].filter(Boolean).join('\n');
}
