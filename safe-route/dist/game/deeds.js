// The ledger of deeds: what the player did for whom, derived from G.events. Existing events are matched
// (src 'event'); new choices write a 'deed' event through remember() (src 'new'). Bonds, promises and the
// endings are pure functions of this ledger, so old saves and retries need no schema change.
import { recordEvent } from './state.js';

export const AXES = ['seoyeon', 'manager', 'clerk', 'elder', 'town', 'info', 'logistics', 'harm'];
export const CH = { prologue: '프롤로그', prep: '1장', dday: '2장', endure: '3장', crossroads: '4장', evac: '5장' };
export const NEIGHBOURS = { seoyeon: { name: '오서연' }, manager: { name: '박영수 소장' }, clerk: { name: '이수아' }, elder: { name: '304호 어르신' } };

// Matchers for existing events. Untagged help events fall back to their old `what` text.
const ev = (type, pred = () => true) => (e) => e.type === type && pred(e.details ?? {});
const help = (kind, fb) => ev('help', (d) => d.who === 'seoyeon' && (d.kind ? d.kind === kind : fb(d.what ?? '')));
const fields = (f) => ev('docs_done', (d) => Array.isArray(d.fields) && d.fields.includes(f));
const res = (r) => (typeof r === 'string' ? r : r?.result); // recordEncounter takes a string or an eval object
const ext = (e) => e.type === 'encounter' && e.details?.tool === 'extinguisher';
const shelterRumor = (d) => (d.id ?? 'shelter_closed') === 'shelter_closed'; // game/rumors.js ids; old saves carry none
const stayMove = (G) => G.events.findLast((e) => e.type === 'decision')?.details?.stayMove ?? G.know?.stayMove;

// [id, chapter, weights, cue, facts, short, line, match?]  no match = src 'new' (written by remember()).
const TABLE = [
  ['signals_clear', 'prologue', { info: 2 }, null, ['info_rumor'], '신호를 사실과 소문으로 나눴다', '쏟아지는 신호를 확인된 사실과 소문으로 나눠 보았다.', ev('signal_sorted', (d) => d.ok === true)],
  ['lead_honest', 'prologue', { info: 1, logistics: 1 }, '윤 팀장은', ['info_scope'], '확인된 것만 안내하겠다고 했다', '윤 팀장에게 확인된 것만 안내하겠다고 말했다.', ev('talk_lead', (d) => d.pick === 'ok')],
  ['driver_text', 'prologue', { logistics: 2 }, '김 기사님은', [], '김 기사님께 안부 문자를 보냈다', '출차하지 못한 김 기사님께 무슨 일 있으면 연락 달라고 문자를 보냈다.'],
  ['seoyeon_contact', 'prep', { seoyeon: 1 }, null, ['people_card'], '서연과 연락 방법을 정했다', '303호 서연과 비상시 연락 방법을 정했다.', ev('neighbors_done', (d) => d.seoyeon === true)],
  ['promise_note', 'prep', { seoyeon: 1 }, '서연은', ['comms_text'], '문자와 쪽지로 알리기로 했다', '무슨 일이 생기면 문자와 문 앞 쪽지로 알려 주겠다고 서연과 약속했다.'],
  ['took_batteries', 'prep', { seoyeon: 1 }, null, [], '서연이 준 건전지를 받았다', '서연이 남는다며 건넨 건전지를 고맙게 받았다.', ev('gift', (d) => d.from === 'seoyeon')],
  ['manager_contact', 'prep', { manager: 1 }, null, [], '박 소장과 연락 방법을 정했다', '관리사무소 박 소장과 비상시 연락 방법을 정했다.', ev('neighbors_done', (d) => d.manager === true)],
  ['clerk_contact', 'prep', { clerk: 1 }, null, [], '수아와 연락 방법을 정했다', '바로편의점 수아와 비상시 연락 방법을 정했다.', ev('neighbors_done', (d) => d.clerk === true)],
  ['promise_store', 'prep', { clerk: 1 }, '수아는', [], '가게 셔터를 봐 주겠다고 했다', '본가에 가는 수아에게, 가게를 비우면 셔터라도 봐 주겠다고 했다.'],
  ['elder_met', 'prep', { elder: 2 }, '304호 어르신은', [], '304호 어르신께 인사드렸다', '304호 어르신 댁에 들러 처음으로 인사를 드렸다.'],
  ['card_neighbors', 'prep', { seoyeon: 1, manager: 1 }, null, ['contact_card'], '카드에 이웃 연락처를 적었다', '비상연락카드에 303호와 관리사무소 연락처를 적었다.', fields('neighbors')],
  ['card_meet', 'prep', { info: 1 }, null, ['contact_meet'], '만남의 장소를 정해 적었다', '비상연락카드에 만남의 장소 세 곳을 적었다.', fields('meet')],
  ['card_family', 'prep', {}, null, ['contact_card'], '부모님 연락처를 카드에 적었다', '대구 부모님 연락처를 종이 카드에 적어 두었다.', fields('family')],
  ['mart_share', 'prep', { town: 2 }, '그분은', ['people_order'], '할머니께 생수를 나눠 드렸다', '마트 계산대 뒤에 선 할머니께 생수 한 병을 나눠 드렸다.'],
  ['hoard', 'prep', { town: -2, harm: 1 }, null, ['home_nohoard'], '물건을 한꺼번에 많이 샀다', '불안한 마음에 같은 물건을 한꺼번에 많이 샀다.', ev('hoard')],
  ['blackout_radio', 'dday', { info: 1 }, null, ['radio_listen'], '정전 뒤 라디오부터 켰다', '정전이 되자 먼저 라디오를 켜 안내를 들었다.'],
  ['hall_checked', 'dday', { elder: 1 }, null, [], '정전 뒤 복도를 살폈다', '정전된 저녁, 손전등을 들고 3층 복도를 살폈다.'],
  ['elder_candle_tip', 'dday', { elder: 1 }, '304호 어르신은', ['candle_rules'], '어르신께 촛불을 꼭 끄시라 했다', '304호 어르신께 자리를 비우실 땐 촛불을 꼭 꺼 달라고 말씀드렸다.'],
  ['candle_left', 'dday', {}, null, ['candle_rules'], '촛불을 켜 둔 채 집을 나섰다', '경보가 울리던 밤, 촛불을 켜 둔 채 집을 나섰다.', ev('candle_left')],
  ['elder_alert_knock', 'dday', { elder: 2, town: 1 }, '304호 어르신은', ['people_ask', 'alert_stairs'], '경보 때 함께 계단을 내려갔다', '공습경보가 울리던 캄캄한 계단에서 304호 어르신을 부축해 함께 내려갔다.',
    (e) => (e.type === 'elder_choice' && e.details?.pick === 'help') || (e.type === 'help' && e.details?.who === 'elder')],
  ['elder_alert_told', 'dday', { elder: 1, manager: 1 }, '박 소장은', ['alert_stairs'], '어르신 일을 박 소장에게 알렸다', '경보 속 계단 앞에 계신 304호 어르신을 박 소장에게 알려 모시고 내려오게 했다.',
    ev('help', (d) => d.who === 'manager' && /304호/.test(d.what ?? ''))],
  ['manager_detour', 'dday', { manager: 1, info: 1 }, '박 소장은', [], '관리사무소에 들러 연락을 정했다', '대피하는 길에 관리사무소 창구에 들러 연락 방법을 정했다.'],
  ['handover_precise', 'dday', { seoyeon: 2, info: 2 }, null, ['info_scope', 'comms_text'], '서연에게 본 것만 정확히 전했다', '서연에게 내가 본 시각과 자리만 정확히 적어 보냈다.', ev('handover_written', (d) => d.kind === 'precise')],
  ['handover_partial', 'dday', { seoyeon: 1, info: 1 }, null, ['info_scope'], '서연에게 본 것을 일부 전했다', '서연에게 내가 본 것 가운데 일부를 적어 보냈다.', ev('handover_written', (d) => d.kind === 'partial')],
  ['handover_overgeneral', 'dday', { info: -1 }, null, ['info_scope'], '본 것보다 넓게 말해 전했다', '서연에게 내가 본 것보다 넓은 말로 적어 보냈다.', ev('handover_written', (d) => d.kind === 'generalized')],
  ['shelter_water_given', 'dday', { town: 2 }, '그 가족은', ['people_order'], '옆자리 아이에게 물을 나눴다', '대피소 옆자리 아이에게 가방 속 물 한 병을 나눠 주었다.'],
  ['shelter_water_received', 'dday', {}, null, [], '대피소에서 물을 나눠 받았다', '빈손으로 온 대피소에서 옆 사람에게 물을 나눠 받았다.'],
  ['register_helped', 'dday', { town: 1 }, '그분은', ['people_order'], '명부 대신 적기를 먼저 여쭸다', '안경을 두고 온 할아버지께 먼저 여쭌 뒤 명부를 대신 적어 드렸다.'],
  ['stairs_asked', 'dday', { seoyeon: 3 }, '서연은', ['people_ask'], '계단에서 먼저 물었다', '대피소 계단에서 어떻게 도울지 서연에게 먼저 묻고 보행기를 들었다.', help('ask', (w) => w.includes('먼저 묻고'))],
  ['stairs_carried', 'dday', { seoyeon: 1 }, null, [], '계단에서 보행기를 들어 주었다', '대피소 계단에서 서연의 보행기를 옆에서 들어 주었다.', help('carry', (w) => w.startsWith('계단에서 보행기'))],
  ['looter_gave', 'endure', { town: 2 }, null, ['people_order'], '낯선 사람에게 먹을 것을 내줬다', '먹을 것을 달라는 낯선 사람에게 가진 것을 내주었다.', ev('looter', (d) => d.result === 'gave')],
  ['looter_escaped', 'endure', {}, null, [], '시비를 피해 자리를 떴다', '시비가 붙자 맞서지 않고 자리를 피했다.', ev('looter', (d) => d.result === 'escaped')],
  ['looter_fought', 'endure', {}, null, [], '덤벼든 사람에게 맞섰다', '덤벼드는 사람에게서 몸을 지키려 맞섰다.', ev('looter', (d) => d.result === 'fought')],
  ['violence', 'dday', { harm: 1 }, null, ['people_order'], '먼저 사람에게 손을 댔다', '시비도 없던 사람에게 먼저 손을 댔다.', ev('violence'), true],
  ['water_for_seoyeon', 'endure', { seoyeon: 3 }, '서연은', ['water_point'], '서연 몫의 물을 날라 주었다', '급수소에서 물 한 통을 받아 3층 서연의 집까지 날라 주었다.', ev('water_fetched', (d) => d.forSeoyeon === true)],
  ['water_declined', 'endure', {}, null, [], '오늘은 내 물만 들 수 있다고 했다', '내 몫만 들기도 벅차 서연의 물은 함께 받지 못했다.'],
  ['board_offer', 'endure', { town: 2, manager: 1, logistics: 1 }, '이웃들은', ['shelter_share'], '게시판에 급수 심부름을 적었다', '단지 게시판에 급수소에 다녀온다고, 필요한 분은 쪽지를 달라고 적었다.'],
  ['queue_carry_elder', 'endure', { elder: 2, town: 1 }, '304호 어르신은', ['people_order', 'water_point'], '어르신께 여쭙고 물통을 들었다', '급수 줄에서 304호 어르신께 먼저 여쭌 뒤 물통을 들어 드렸다.'],
  ['store_paid', 'endure', { clerk: 2 }, '수아는', ['bag_cash'], '편의점 현금 통에 값을 넣었다', '불 꺼진 편의점에서 물건을 챙기고 현금 통에 값을 넣었다.', ev('buy', (d) => d.paid === 'cash')],
  ['store_iou', 'endure', { clerk: 2 }, '수아는', [], '편의점에 금액 쪽지를 남겼다', '현금이 없어 건전지 한 팩만 챙기고 쪽지에 302호와 금액을 적었다.'],
  ['store_took', 'endure', { clerk: -2, harm: 1 }, '수아는', ['people_order'], '값을 남기지 않고 물건을 챙겼다', '현금이 없어 편의점 건전지와 물을 값을 남기지 않고 가져왔다.'],
  ['store_shuttered', 'endure', { clerk: 2 }, '수아는', [], '편의점 셔터를 내려 두었다', '반쯤 열린 편의점 셔터를 내리고 302호라고 쪽지를 남겼다.'],
  ['rumor_checked', 'endure', { info: 2, town: 1 }, null, ['info_rumor', 'shelter_rumor'], '대피소 폐쇄 소문을 확인했다', '대피소가 폐쇄됐다는 소문을 듣고 먼저 공식 안내로 확인했다.', ev('rumor_checked', shelterRumor)],
  ['rumor_spread', 'endure', { info: -2, town: -1, harm: 1 }, null, ['info_harm'], '대피소 폐쇄 소문을 전했다', '대피소가 폐쇄됐다는 확인되지 않은 말을 이웃에게 전했다.', ev('rumor_spread', shelterRumor)],
  ['rumor_corrected', 'endure', { info: 1, town: 1 }, '그 주민은', ['info_harm'], '102동 주민에게 확인한 걸 알렸다', '102동 주민에게 대피소가 정상 운영 중이라고 확인한 내용을 알렸다.'],
  ['knock_helped', 'endure', { town: 1 }, '그 이웃은', ['shelter_share'], '밤에 찾아온 이웃을 도왔다', '밤늦게 문을 두드린 이웃에게 물이나 급수소 소식을 건넸다.',
    (e) => (e.type === 'knock' && ['give', 'info'].includes(e.details?.pick)) || (e.type === 'help' && e.details?.who === 'stranger')],
  ['fire_warned', 'endure', { elder: 2, town: 1 }, '304호 어르신은', ['fire_shout'], '"불이야!" 외쳐 이웃을 깨웠다', '복도에 불이 나자 "불이야!" 외쳐 이웃들을 깨웠다.', ev('fire_shout')],
  ['fire_handled', 'endure', { manager: 1, town: 1 }, null, ['fire_small', 'fire_big'], '복도 불 앞에서 판단해 움직였다', '복도 불 앞에서 끌지 물러날지 판단해 움직였다.',
    (e) => e.type === 'fire_retreat' || (ext(e) && e.chapter === 'endure' && ['out', 'retreat-ok'].includes(res(e.details.result)))],
  ['elder_visit', 'endure', { elder: 1 }, '304호 어르신은', [], '304호 어르신께 안부를 여쭸다', '불이 난 다음 날, 304호에 들러 어르신께 안부를 여쭸다.'],
  ['notes_exchange', 'endure', { seoyeon: 2, info: 1 }, null, ['shelter_share'], '서연과 라디오 메모를 나눴다', '서연과 라디오 메모와 급수소 소식을 서로 나누기로 했다.', help('memo', (w) => w.startsWith('라디오 메모'))],
  ['seoyeon_note_reply', 'crossroads', { seoyeon: 1, info: 1 }, '서연은', ['info_scope'], '서연 메모에 급수 시간을 적었다', '서연의 메모에 급수 시간과 그 말을 들은 곳을 덧붙여 적었다.'],
  ['yoon_reply', 'crossroads', { logistics: 2 }, '윤 팀장은', [], '윤 팀장에게 돕겠다고 답했다', '구호물자 분류를 도와 달라는 윤 팀장의 문자에 가능한 날 나가겠다고 답했다.'],
  ['smoke_judged', 'crossroads', { info: 1, manager: 1 }, null, ['fire_stay', 'fire_judge'], '연기 속에서 집 안에 머물렀다', '윗집 연기가 퍼질 때 문을 닫고 집 안에서 기다렸다.', ev('smoke_choice', (d) => d.pick === 'stay')],
  ['shelter_fire_judged', 'crossroads', { town: 1 }, null, ['fire_big'], '대피소 불 앞에서 판단했다', '대피소 주차장의 큰 불 앞에서 무리하지 않고 판단했다.',
    (e, G) => ext(e) && e.chapter === 'crossroads' && res(e.details.result) !== 'escalated' && stayMove(G) === 'move'],
  // §2 rule "move = shelter_fire_judged +1 and fire_report +1" (equal to smoke_judged +2) wins over the table's info+1, town+1.
  ['fire_report', 'crossroads', { info: 1 }, null, ['info_scope', 'shelter_rules'], '본 대로 시각과 자리를 알렸다', '불이 난 시각과 자리, 다친 사람이 있었는지를 본 대로 알렸다.'],
  ['rounds_with_manager', 'crossroads', { manager: 3, town: 1 }, '박 소장은', [], '박 소장과 빈 집 문단속을 돌았다', '박 소장과 함께 빈 집들의 문단속을 돌았다.'],
  ['sort_supplies', 'crossroads', { logistics: 3, town: 1 }, '이웃들은', [], '대피소 구호품 정리를 도왔다', '물류 일 하던 솜씨로 대피소 구호품 상자 정리를 도왔다.'],
  ['door_note', 'evac', { town: 1, seoyeon: 1, manager: 1 }, null, ['evac_note'], '떠나며 문에 쪽지를 남겼다', '집을 나서며 언제 어디로 가는지 문에 쪽지를 남겼다.', ev('note_left')],
  ['seoyeon_card_photo', 'evac', { seoyeon: 1 }, null, ['contact_card'], '서연과 연락처를 다시 확인했다', '떠나기 전 서연과 서로의 연락처를 다시 확인했다.', help('card', (w) => w.startsWith('떠나기 전'))],
  ['seoyeon_waited', 'evac', { seoyeon: 3 }, null, ['people_unhcr'], '서연의 지원 차량을 함께 기다렸다', '지원 차량이 올 때까지 서연 곁을 지켰다.', ev('path', (d) => d.path === 'seoyeon')],
  ['stock_to_manager', 'evac', { manager: 2, town: 2, logistics: 1 }, '박 소장은', ['people_order'], '남은 물과 통조림을 맡겼다', '남는 물과 통조림을 관리사무소에 맡기고 나섰다.'],
  ['bus_helped', 'evac', { town: 1 }, '그분은', ['people_order'], '리프트 앞 어르신 짐을 들어 드렸다', '버스 리프트 앞에서 어르신께 먼저 여쭌 뒤 짐을 들어 드렸다.'],
  ['volunteered', 'evac', { town: 2 }, null, ['water_point'], '급수 자원봉사에 이름을 적었다', '버스 대신 행정복지센터 급수 자원봉사에 이름을 적었다.', ev('path', (d) => d.path === 'volunteer')],
  ['stayed', 'evac', { manager: 1 }, null, [], '박 소장과 단지에 남았다', '버스 대신 박 소장과 함께 단지에 남기로 했다.', ev('path', (d) => d.path === 'stay')],
];

const MATCH = {};
export const DEEDS = Object.fromEntries(TABLE.map(([id, ch, w, cue, facts, short, line, match, each], i) => {
  const isNew = !match;
  MATCH[id] = match ?? ((e) => e.type === 'deed' && e.details?.id === id);
  return [id, { no: i + 1, ch, src: isNew ? 'new' : 'event', w, cue, short, line, facts, ...(each ? { each: true } : {}) }];
}));
export const NEW_DEEDS = Object.keys(DEEDS).filter((id) => DEEDS[id].src === 'new');
const MIN = { signals_clear: 4 }; // the 4th correctly sorted signal is the deed

// Writes a new-choice deed once. Unknown ids and event-derived ids are developer errors.
export function remember(G, id, details = {}) {
  const d = DEEDS[id];
  if (!d) throw new Error(`unknown deed: ${id}`);
  if (d.src !== 'new') throw new Error(`deed ${id} is derived from events, not remembered`);
  if (!d.each && G.events.some(MATCH[id])) return null;
  return recordEvent(G, 'deed', { ...details, id });
}

// [{ id, seq, chapter, day, minute, n, details }] unique by id, in first-seen order.
export function deeds(G) {
  const out = [];
  for (const id of Object.keys(DEEDS)) {
    const ms = G.events.filter((e) => MATCH[id](e, G));
    const k = MIN[id] ?? 1;
    if (ms.length < k) continue;
    const e = ms[k - 1];
    out.push({ id, seq: e.seq, chapter: e.chapter, day: e.day, minute: e.minute, n: ms.length, details: e.details ?? {}, all: ms });
  }
  return out.sort((a, b) => a.seq - b.seq).map(({ all, ...d }) => (d.id === 'violence' ? { ...d, ko: all.filter((e) => e.details?.ko).length } : d));
}

const PROMISES = [
  { id: 'note_for_seoyeon', who: 'seoyeon', made: 'promise_note', kept: ['seoyeon_note_reply', 'door_note'] },
  { id: 'store_watch', who: 'clerk', made: 'promise_store', kept: ['store_shuttered'] },
  { id: 'news_for_seoyeon', who: 'seoyeon', made: 'notes_exchange', kept: ['seoyeon_note_reply'] },
];

// Only promises that were made. Open ones count as broken once the run is over ({ final: true }).
export function promises(G, { final = false } = {}, ds = deeds(G)) {
  const has = new Set(ds.map((d) => d.id));
  return PROMISES.filter((p) => has.has(p.made)).map((p) => ({ id: p.id, who: p.who, state: p.kept.some((k) => has.has(k)) ? 'kept' : final ? 'broken' : 'open' }));
}

export function axes(G, { final = false } = {}) {
  const a = Object.fromEntries(AXES.map((k) => [k, 0]));
  const ds = deeds(G);
  for (const d of ds) {
    if (d.id === 'violence') { a.harm += d.n + 2 * d.ko; continue; } // +1 a hit, +3 a knock-out
    for (const [k, v] of Object.entries(DEEDS[d.id].w)) a[k] += v;
  }
  for (const p of promises(G, { final }, ds)) if (p.state === 'broken') a[p.who] -= 1;
  return a;
}

export const newDeeds = (G, afterSeq) => deeds(G).filter((d) => d.seq > afterSeq).map(({ id, seq }) => ({ id, seq }));

export const cueText = (id) => (DEEDS[id]?.cue ? `${DEEDS[id].cue} 이 일을 기억할 것이다.` : null);
