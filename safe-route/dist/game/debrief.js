// Fail cards, chapter summaries and the ending's 생존 기록. Lines describe the situation, never
// blame the player or anyone else; safety statements are FACTS texts, game advice names planner actions.
import { CATALOG, BAG_LIMIT, itemName } from './items.js';
import { fact } from './facts.js';
import { bagLoad, hhmm } from './state.js';
import { pickEnding } from './endings.js';
import { EXT_STEPS } from './drills.js';
import { PREP, ripples } from './ripples.js';
import { planLine } from './ledger.js';

export { PREP };
// 'help' / 'trade' who ids. elder and stranger have no G.rel entry (state.js PEOPLE); trader is the barter.
export const PEOPLE = { manager: '박영수 소장', clerk: '이수아', seoyeon: '오서연', elder: '304호 어르신', stranger: '문을 두드린 이웃', trader: '102동 주민' };
const CONTACT = { sms: '문자', door: '문 두드리기', both: '문자·문에 쪽지', office: '관리사무소 창구', store: '가게로 찾아가기' };
const TOOL = { radio: '라디오', bag: '비상가방', route: '경로 정하기', extinguisher: '소화기', docs: '비상연락처 카드', info: '정보 가려 듣기' };
// Every result string the story passes to recordEncounter, so no raw id reaches the player.
const RESULT = {
  out: '불을 껐습니다', 'retreat-ok': '대피하며 알렸습니다', escalated: '불이 커져 물러났습니다', incomplete: '끝까지 마치지 못했습니다',
  risky: '연기 속으로 바로 나섰습니다', practice: '사용 순서를 익혔습니다', tuned: '공식 방송을 찾아 들었습니다', opened: '라디오를 켜 보았습니다',
  missed: '공식 방송은 찾지 못했습니다', complete: '필요한 것을 다 챙겼습니다', partial: '몇 가지가 빠졌습니다', planned: '경로를 정해 두었습니다',
  written: '카드를 적어 두었습니다', sorted: '출처를 가려 들었습니다', misled: '소문을 따라갔습니다', checked: '한 번 더 확인했습니다',
};
const STAT = { health: '체력', thirst: '갈증', fatigue: '피로', tension: '긴장' };
const LABEL = { official: '공식 안내', site: '현장 확인', neighbor: '이웃에게 들음', rumor: '소문·미확인', self: '내 기록' };

const payoffOf = (e) => ({ prep: PREP[e.details.prep] ?? e.details.prep, moment: e.details.moment, id: e.details.prep });
const sourcesOf = (ids) => [...new Set(ids.map(id => fact(id)?.sourceId).filter(Boolean))];
const lastEncounter = (G, tool) => G.events.findLast(e => e.type === 'encounter' && e.details?.tool === tool);
// HUD value words; health is better high, the other stats are better low.
const word = (stat, v) => { const good = stat === 'health' ? v : 100 - v; return good >= 60 ? '좋음' : good >= 30 ? '보통' : '나쁨'; };
const resultWord = (r) => typeof r === 'string' ? RESULT[r] ?? '해 봤습니다'
  : r?.result ? RESULT[r.result] ?? '해 봤습니다'
    : typeof r?.ok === 'boolean' ? (r.ok ? '잘 해냈습니다' : '아쉬움이 남았습니다') : '해 봤습니다';

const FAIL = {
  // Health ran out: a fall, a fight, a blast or debris. detail.why says which.
  dead(G, { why = 'hurt' } = {}) {
    const W = {
      fall: ['높은 곳에서 떨어졌습니다', '떨어지기 전, 아래까지의 높이를 가늠할 시간이 있었습니다.', '내려갈 때는 계단·사다리처럼 몸을 붙일 수 있는 길을 먼저 찾기'],
      fight: ['몸싸움 끝에 쓰러졌습니다', '상대가 먼저 물러설 여지가 있었습니다.', '시비가 붙으면 물건을 내주거나 자리를 피하는 것도 선택지로 두기'],
      blast: ['폭발에 휘말렸습니다', '휘파람 같은 낙하음이 들린 뒤 몇 초의 여유가 있었습니다.', "낙하음이 들리면 바로 엎드리고, 건물 안쪽·지하로 몸을 숨기기"],
      debris: ['무너진 잔해에 다쳤습니다', '금이 가고 먼지가 떨어지는 건물은 무너지기 전에 신호를 보냅니다.', '흔들리거나 금 간 건물 곁에서는 멀리 떨어져 걷기'],
      fire: ['불길 속에서 쓰러졌습니다', '연기가 짙어지기 전에 빠져나갈 길이 있었습니다.', '불이 번진 곳은 자세를 낮추고 바람이 불어오는 쪽으로 빠져나가기'],
      hurt: ['더 이상 버틸 수 없을 만큼 다쳤습니다', '쉬면서 상처를 돌볼 수 있는 순간이 있었습니다.', '구급함을 챙기고, 다쳤을 때는 안전한 곳에서 먼저 처치하기'],
    }[why] ?? null;
    const [title, seen, next] = W ?? ['더 이상 버틸 수 없을 만큼 다쳤습니다', '쉬면서 상처를 돌볼 수 있는 순간이 있었습니다.', '다친 뒤에는 먼저 안전한 곳에서 처치하기'];
    const facts = { blast: ['alert_airraid', 'shelter_near'], debris: ['alert_airraid', 'shelter_near'], fire: ['fire_big'] }[why] ?? ['mind_breath'];
    return { title, seen, missed: G.injury?.length ? '이미 다친 몸으로 움직이고 있었습니다.' : '몸 상태를 살필 여유가 없었습니다.', next, facts };
  },
  late(G) {
    const k = G.know;
    return {
      title: '대피소에 제때 닿지 못했습니다',
      seen: k.gateClosedSeen ? '동쪽 출입구가 닫혀 있다는 것은 현장에서 확인했습니다.'
        : k.shelterLocated ? '대피소가 생활문화센터 지하라는 것은 알고 있었습니다.'
          : '경보는 들었지만, 가까운 대피소 위치는 아직 확인 전이었습니다.',
      missed: !k.entrancesChecked ? '출입구 A·B·C 가운데 어디가 열려 있는지 미리 알 방법이 없었습니다.'
        : !k.routeChecked ? '통제된 길을 돌아가는 경로는 아직 정해 두지 않은 상태였습니다.'
          : '경보가 울린 뒤 출발하기까지 시간이 걸렸습니다.',
      next: !k.shelterLocated || !k.entrancesChecked ? "준비 단계에서 '대피소·경로 확인'으로 출입구 A·B·C를 둘러보기"
        : "'비상가방 꾸리기'로 가방을 현관에 두고 바로 나설 수 있게 하기",
      facts: ['shelter_know', 'shelter_near', 'bag_take'],
    };
  },
  exhausted(G) {
    const s = G.stats, load = bagLoad(G);
    const water = ['water_500', 'water_2l'].some(id => (G.items[id]?.bag ?? 0) > 0);
    return {
      title: '더 걷기 어려울 만큼 지쳤습니다',
      seen: `그때 갈증은 ${word('thirst', s.thirst)}, 피로는 ${word('fatigue', s.fatigue)} 상태였습니다.`,
      missed: load.over.weight ? `가방이 ${load.weightKg}kg으로, 들고 걷기 편한 ${BAG_LIMIT.weightKg}kg을 넘었습니다.`
        : !water ? '가방 안에 마실 물이 없었습니다.' : '중간에 쉴 시간이 따로 없었습니다.',
      next: load.over.weight ? "'비상가방 꾸리기'에서 무게 막대를 보며 꼭 필요한 것부터 담기"
        : !water ? "'비상가방 꾸리기'에서 작은 생수병을 먼저 담기" : "준비 칸 하나를 '휴식'에 남겨 두기",
      facts: load.over.weight ? ['bag_foil', 'bag_days'] : ['bag_days'],
    };
  },
  // detail.scene: 'corridor' (3장, default), 'upstairs' (4장 stay: smoke from another flat), 'shelter' (4장 move).
  fire(G, { scene = 'corridor' } = {}) {
    if (scene === 'upstairs') return {
      title: '연기 속으로 불을 끄러 나갔습니다',
      seen: '우리 집 안으로는 연기나 불길이 들어오지 않은 상태였습니다.',
      missed: fact('fire_stay').text,
      next: '문손잡이를 손등으로 먼저 만져 보고, 연기가 들어오지 않으면 집 안에서 창문을 닫고 119에 신고하기',
      facts: ['fire_stay', 'fire_judge', 'fire_door'],
    };
    if (scene === 'shelter') return {
      title: '대피소의 큰 불 앞에서 버텼습니다',
      seen: '불길이 이미 천장 배관에 닿아 있었습니다.',
      missed: fact('fire_big').text,
      next: '천장까지 닿은 불은 끄려 하지 말고, "불이야!" 외치며 물러나 119에 알리기',
      facts: ['fire_big', 'fire_shout', 'fire_119'],
    };
    const r = lastEncounter(G, 'extinguisher')?.details.result;
    const res = typeof r === 'string' ? r : r?.result;
    const stepFact = EXT_STEPS.find(s => s.id === r?.firstError)?.facts[0];
    const factId = res === 'escalated' ? 'fire_big' : stepFact ?? 'fire_shout';
    return {
      title: '복도의 불이 커졌습니다',
      seen: G.know.extinguisherLocated ? '복도 소화기가 어디 있는지는 알고 있었습니다.' : '복도 소화기 위치는 확인하기 전이었습니다.',
      missed: fact(factId).text,
      next: G.know.extinguisherLocated ? '불길이 천장에 닿았는지 먼저 보고, 끌지 대피할지 정하기'
        : "'집 장비 점검' 때 복도 소화기 위치와 사용 순서를 확인하기",
      facts: [factId, 'fire_small', 'ext_1'],
    };
  },
  // detail.via: 'rumor' when the player acted on a false post (game/rumors.js); otherwise lost on the way.
  lostWay(G, { via } = {}) {
    if (via === 'rumor') return {
      title: '소문을 따라가느라 길을 잃었습니다',
      seen: 'SNS 글에는 누가 언제 확인했는지가 없었습니다.',
      missed: fact('info_rumor').text,
      next: '재난문자·라디오·게시판으로 한 번 더 확인하고 움직이기',
      facts: ['info_rumor', 'info_harm', 'evac_note'],
    };
    const k = G.know;
    return {
      title: '길을 찾느라 시간이 걸렸습니다',
      seen: k.routeChecked ? '미리 본 경로가 현장 통제로 막혀 있었습니다.' : `${hhmm(G.cond.commsWeakAt)}쯤부터 휴대폰 지도가 잘 열리지 않았습니다.`,
      missed: fact('shelter_signs').text,
      next: k.routeChecked ? '돌아가는 두 번째 경로까지 정해 두기' : "'대피소·경로 확인'으로 경로를 미리 걸어 보고 수첩에 적어 두기",
      facts: ['shelter_signs', 'evac_note'],
    };
  },
};

export const FAIL_TYPES = Object.keys(FAIL);

// { title, seen, missed, next, sourceIds }: 당시 확인한 정보 / 판단에서 놓친 부분 / 다음에 시도할 준비.
export function failDebrief(G, type, detail = {}) {
  const make = FAIL[type];
  if (!make) throw new Error(`unknown fail type: ${type}`);
  const { facts, ...d } = make(G, detail);
  return { ...d, sourceIds: sourcesOf(facts) };
}

export function chapterSummary(G, chapter) {
  const evs = G.events.filter(e => e.chapter === chapter);
  const of = (type) => evs.filter(e => e.type === type);
  const lines = [];
  const slots = of('slot');
  if (slots.length) lines.push(`준비에 쓴 시간: ${slots.reduce((s, e) => s + (e.details.cost ?? 0), 0)}칸`);
  const bought = {};
  for (const e of of('buy')) for (const [id, n] of Object.entries(e.details.cart ?? {})) bought[id] = (bought[id] ?? 0) + n;
  if (Object.keys(bought).length) lines.push(`산 물건: ${Object.entries(bought).map(([id, n]) => `${itemName(id)} ${n}${CATALOG[id]?.unit ?? '개'}`).join(', ')}`);
  const checked = of('check').map(e => itemName(e.details.id));
  if (checked.length) lines.push(`점검해서 쓸 수 있게 된 것: ${checked.join(', ')}`);
  for (const e of of('encounter')) lines.push(`${TOOL[e.details.tool] ?? e.details.tool}: ${resultWord(e.details.result)}`);
  const notes = G.info.filter(i => i.chapter === chapter);
  if (notes.length) {
    const byLabel = Object.entries(LABEL).map(([k, l]) => [l, notes.filter(i => i.label === k).length]).filter(([, n]) => n);
    lines.push(`수첩에 적은 정보 ${notes.length}개 (${byLabel.map(([l, n]) => `${l} ${n}`).join(', ')})`);
  }
  for (const e of of('help')) lines.push(`${PEOPLE[e.details.who] ?? e.details.who}: ${e.details.what}`);
  for (const e of of('trade')) lines.push(`물물교환(${PEOPLE[e.details.from] ?? e.details.from}): ${cartText(e.details.give)} → ${cartText(e.details.get)}`);
  for (const e of of('ledger')) if (e.details.plan) lines.push(planLine(e.details.plan));
  const dead = of('battery_dead').map(e => itemName(e.details.id));
  if (dead.length) lines.push(`건전지가 다 닳은 것: ${[...new Set(dead)].join(', ')}`);
  lines.push(`지금 상태: ${Object.entries(STAT).map(([s, l]) => `${l} ${word(s, G.stats[s])}`).join(', ')}`);
  const payoffs = of('payoff').map(payoffOf);
  return { lines, payoffs };
}

const cartText = (cart) => Object.entries(cart ?? {}).map(([id, n]) => `${itemName(id)} ${n}${CATALOG[id]?.unit ?? '개'}`).join(', ');
const CHECK_WHY = { batteries: '건전지를 넣지 않아 켜 볼 수 없었습니다.', charge: '충전해 두지 않아 쓸 수 없었습니다.' };
const FIXTURE_DONE = { blackout_curtain: 'curtain' }; // a fixture counts as used once its know flag is set
const KNOW_TEXT = {
  shelterLocated: '가까운 대피소 위치',
  entrancesChecked: '대피소 출입구 A·B·C와 경사로',
  routeChecked: '통제 구간을 돌아가는 경로',
  extinguisherLocated: '복도 소화기 위치',
  radioChecked: '라디오 주파수와 건전지',
  contactsCard: '종이에 적은 비상연락처',
  docsCopied: '중요 서류 사본',
  meetPoint: '만남의 장소',
  waterPoint: '비상급수시설 위치',
};
// First unchecked know flag picks the real-life practice line.
const PRACTICE = [
  ['shelterLocated', 'shelter_find'], ['entrancesChecked', 'shelter_signs'], ['contactsCard', 'contact_card'], ['meetPoint', 'contact_meet'],
  ['radioChecked', 'radio_batt'], ['extinguisherLocated', 'ext_1'], ['docsCopied', 'bag_copies'], ['waterPoint', 'water_point'],
];

// One row per person (the same help pressed twice is one row): their last two different moments, oldest first.
function helpRows(G) {
  const by = new Map();
  for (const e of G.events) if (e.type === 'help' && e.details?.who && e.details.what) {
    const list = by.get(e.details.who) ?? [];
    if (!list.includes(e.details.what)) list.push(e.details.what);
    by.set(e.details.who, list);
  }
  return [...by].map(([who, whats]) => ({ who: PEOPLE[who] ?? who, what: whats.slice(-2).join(' · ') }));
}

// Durable items count as used when items[id].used > 0 (state.use / state.consume).
export function endingRecord(G) {
  const helped = G.events.filter(e => e.type === 'payoff').map(payoffOf);
  const unused = [];
  for (const [id, e] of Object.entries(G.items)) {
    const c = CATALOG[id];
    if (!c) continue;
    if (c.needsCheck && !e.checked && e.home + e.bag > 0) unused.push({ item: c.name, why: e.charge === 0 ? '건전지가 다 닳아 더 켤 수 없었습니다.' : CHECK_WHY[c.needsCheck] });
    else if (e.bought > 0 && e.used === 0 && !G.know[FIXTURE_DONE[id]]) unused.push({ item: c.name, why: '사 두었지만 쓸 일이 없었습니다.' });
  }
  const unchecked = Object.entries(KNOW_TEXT).filter(([k]) => !G.know[k]).map(([, t]) => t);
  const people = helpRows(G);
  for (const [k, r] of Object.entries(G.rel)) if (r.contact) people.push({ who: PEOPLE[k] ?? k, what: `비상 연락 방법을 정했습니다 (${CONTACT[r.contact] ?? r.contact})` });
  const f = fact(PRACTICE.find(([k]) => !G.know[k])?.[1] ?? 'shelter_know');
  return { fate: pickEnding(G), helped, unused, unchecked, people, ripples: ripples(G), practice: { text: f.text, sourceId: f.sourceId } };
}
