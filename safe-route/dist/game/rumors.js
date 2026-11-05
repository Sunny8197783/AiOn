// 소식 가려듣기: three moments where a claim reaches 정민 together with other things heard about it.
// Pure logic. Safety lines come from FACTS (info_rumor, info_scope, ...); the posts themselves are fiction.
//
// RUMORS: [{ id, chapter, road?(G), facts, items: [{ key, channel, from, meta, label, truth, text(G) }] }]
//   items[0] (key 'claim') is the claim the player judges; the others are what can confirm or correct it.
//   label = the notebook label it really deserves (official | site | neighbor | rumor). Every set holds at
//   least one TRUE item that is not official, so the lesson is "check the source", not "only trust officials".
// rumor(id) -> the set. posts(G, id) -> items with text evaluated (for talk lines, the phone and notes).
// judge(G, id, pick, { item = 'claim' } = {}) -> { ok, label, truth, result, facts }
//   pick: the notebook label the player gave the item. ok = pick matches its real label, or the player chose
//   'rumor' (unconfirmed) for a false claim. result 'sorted' | 'doubted' (a true item held back as rumor) | 'misled' can go to recordEncounter(G, 'info', result).
//   Records 'rumor_judged' { id, item, pick, ok }.
// believe(G, id) -> true when the claim was false (G.know.misled = id). Records 'rumor_believed' { id, truth }.
// verify(G, id) -> true items to show; clears G.know.misled if it was this id. Records 'rumor_checked' { id }.
// misled(G) -> the id the player is acting on, or null. lostByRumor(G) -> that id once a 'rumor_followed'
//   { id } event exists (story records it at the closed road), else null.
// seenRumors(G) -> [{ id, claim }] judged or believed so far, oldest first (phone SNS list).
import { CLOSURES } from './townmap.js';
import { fact } from './facts.js';
import { recordEvent } from './state.js';

// evac_shortcut names closures.evac[0], which conditions() always closes (state.js CLOSURE_SETS).
const shortcut = (G) => G.cond.closures.evac[0];
const road = (G) => CLOSURES[shortcut(G)]?.label ?? '통제 구간';

export const RUMORS = [
  {
    id: 'shelter_closed', chapter: 'endure', facts: ['shelter_rumor', 'info_rumor', 'info_harm'],
    items: [
      { key: 'claim', channel: 'sns', from: 'SNS (102동 주민이 전해 줌)', meta: '작성자·시각 없음', label: 'rumor', truth: false,
        text: () => '생활문화센터 대피소가 폐쇄됐대요. 다들 다른 데로 가래요.' },
      { key: 'manager', channel: 'neighbor', from: '박영수 소장', meta: '오늘 오후 · 직접 다녀옴', label: 'site', truth: true,
        text: () => '아까 생활문화센터 앞까지 다녀왔는데, 출입구 A는 열려 있고 안내 요원도 있었어요.' },
      { key: 'official', channel: 'radio', from: '재난방송·행정복지센터 게시판', meta: '공식 안내 (가상)', label: 'official', truth: true,
        text: () => '생활문화센터 대피소는 정상 운영 중입니다. 확인되지 않은 정보에 주의해 주십시오.' },
    ],
  },
  {
    id: 'camp_food', chapter: 'crossroads', facts: ['info_scope', 'info_rumor'],
    items: [
      { key: 'claim', channel: 'sns', from: 'SNS 퍼온 글', meta: '사진 없음 · 시각 없음', label: 'rumor', truth: false,
        text: () => '[퍼옴] 생활문화센터 임시 거처 급식 끊김. 가면 굶는다고 함.' },
      { key: 'seoyeon', channel: 'neighbor', from: '오서연의 메모', meta: '문 앞 쪽지 · 오늘 아침', label: 'neighbor', truth: true,
        text: () => '주민센터에 직접 물어봤어요. 임시 거처 배식은 하루 두 번(12시·18시)이래요. — 303호' },
      { key: 'official', channel: 'notice', from: '연무구 안내 (가상)', meta: '행정복지센터 게시판·라디오', label: 'official', truth: true,
        text: () => '임시 거처: 급수·급식 지원, 공용 공간, 개인 공간 협소' },
    ],
  },
  {
    id: 'evac_shortcut', chapter: 'evac', facts: ['evac_note', 'info_rumor', 'info_area'], road: shortcut,
    items: [
      { key: 'claim', channel: 'sns', from: 'SNS 단체방', meta: '누가 확인했는지 없음', label: 'rumor', truth: false,
        text: (G) => `${road(G)} 풀림! 그쪽으로 가면 집결지까지 지름길이래요.` },
      { key: 'manager', channel: 'neighbor', from: '박영수 소장', meta: '1층 관리사무소 · 방금 보고 옴', label: 'site', truth: true,
        text: (G) => `${road(G).replace(/ 통제$/, '')} 쪽은 아직 바리케이드가 있어요. 안내 요원 있는 큰길로 가세요.` },
      { key: 'official', channel: 'notice', from: '연무구 대피 안내 (가상)', meta: 'D+12 · 재난문자·게시판', label: 'official', truth: true,
        text: () => '임시 집결지까지는 안내 요원이 있는 권고 경로로 이동하십시오. 통제 구간은 지나갈 수 없습니다.' },
    ],
  },
];

const byId = new Map(RUMORS.map(r => [r.id, r]));
const LABELS = ['official', 'site', 'neighbor', 'rumor', 'self'];

export function rumor(id) {
  const r = byId.get(id);
  if (!r) throw new Error(`unknown rumor: ${id}`);
  return r;
}
const itemOf = (r, key) => { const it = r.items.find(i => i.key === key); if (!it) throw new Error(`rumor ${r.id} has no item ${key}`); return it; };

export const posts = (G, id) => rumor(id).items.map(({ text, ...it }) => ({ ...it, text: text(G) }));

export function judge(G, id, pick, { item = 'claim' } = {}) {
  const r = rumor(id), it = itemOf(r, item);
  if (!LABELS.includes(pick)) throw new Error(`unknown info label: ${pick}`);
  const ok = pick === it.label || (!it.truth && pick === 'rumor');
  recordEvent(G, 'rumor_judged', { id, item, pick, ok });
  return { ok, label: it.label, truth: it.truth, result: ok ? 'sorted' : it.truth ? 'doubted' : 'misled', facts: [...r.facts] };
}

export function believe(G, id) {
  const it = itemOf(rumor(id), 'claim');
  recordEvent(G, 'rumor_believed', { id, truth: it.truth });
  if (!it.truth) G.know.misled = id;
  return !it.truth;
}

export function verify(G, id) {
  rumor(id);
  recordEvent(G, 'rumor_checked', { id });
  if (G.know.misled === id) G.know.misled = null;
  return posts(G, id).filter(p => p.truth);
}

export const misled = (G) => (byId.has(G.know?.misled) ? G.know.misled : null);
export function lostByRumor(G) {
  const id = misled(G);
  return id && G.events.some(e => e.type === 'rumor_followed' && e.details?.id === id) ? id : null;
}

export function seenRumors(G) {
  const ids = [...new Set(G.events.filter(e => (e.type === 'rumor_judged' || e.type === 'rumor_believed') && byId.has(e.details?.id)).map(e => e.details.id))];
  return ids.map(id => ({ id, claim: posts(G, id)[0] }));
}

// Every fact a set cites, resolved (cards after a judgement).
export const rumorFacts = (id) => rumor(id).facts.map(fact).filter(Boolean);
