// What became of the neighbours and the town by D+14. Pure functions of the deed
// ledger and the last-day path. No neighbour dies in any line and no line blames anyone.
import { deeds, axes, promises, NEIGHBOURS } from './deeds.js';

export const PATHS = ['bus', 'walk', 'seoyeon', 'missed', 'stay', 'volunteer'];
export const LEAVE = new Set(['bus', 'walk', 'seoyeon']);
export const TOWN = new Set(['missed', 'stay', 'volunteer']);
export const pathOf = (G) => (PATHS.includes(G.know?.path) ? G.know.path : 'bus');

function context(G, ending) {
  const ds = deeds(G), by = new Map(ds.map((d) => [d.id, d]));
  const ps = new Map(promises(G, { final: true }, ds).map((p) => [p.id, p.state]));
  const path = pathOf(G);
  return { has: (id) => by.has(id), n: (id) => by.get(id)?.n ?? 0, detail: (id) => by.get(id)?.details ?? {}, a: axes(G, { final: true }),
    path, ending, promise: (id) => ps.get(id) ?? 'open', town: TOWN.has(path) };
}

// [pred, line, present] per neighbour; the first match wins. present: met again on the D+14 roam.
const RULES = {
  seoyeon: [
    [(c) => c.path === 'seoyeon', '서연과 C 경사로를 함께 점검했다. 경사로는 이제 안내 요원이 없어도 열려 있다.', false],
    [(c) => c.has('stairs_asked') && c.has('water_for_seoyeon'), '서연에게서 문자가 왔다. "그날 계단에서 먼저 물어봐 줘서, 물도 날라 줘서 고마웠어요."', false],
    [(c) => c.has('handover_precise'), '서연은 그날 밤의 문자를 아직 지우지 않았다고 했다. 시각과 자리가 정확히 적힌 문자였다.', false],
    [(c) => c.promise('note_for_seoyeon') === 'broken' || c.promise('news_for_seoyeon') === 'broken', '서연은 그 뒤로 소식을 받지 못해 걱정했다고 했다. 다음에는 연락 방법을 하나 더 정해 두자고 했다.', false],
    [(c) => !c.has('seoyeon_contact'), '303호 문 앞에는 "먼저 떠납니다 — 서연"이라는 메모가 붙어 있었다. 연락처는 적혀 있지 않았다.', false],
    [() => true, '서연에게서 짧은 문자가 왔다. "덕분에 무사히 도착했어요."', false],
  ],
  manager: [
    [(c) => c.ending === 'commons', '박 소장은 천막 기둥에 남은 집 명단을 붙여 두고, 하루 두 번 이름을 부른다.', true],
    [(c) => c.has('stock_to_manager'), '박 소장은 맡기고 간 물과 통조림을 어느 집에 몇 개 나눴는지 공책에 적어 두었다.', true],
    [(c) => c.has('candle_left'), '경보가 울린 밤, 박 소장은 빈 302호에 켜진 촛불을 꺼 두었다. 그 얘기를 한 번도 꺼내지 않았다.', true],
    [(c) => !c.town && !c.has('manager_contact') && !c.has('door_note'), '박 소장은 302호에 사람이 있는지 몰라 사흘 동안 문을 두드렸다고 했다.', true],
    [(c) => c.has('water_declined'), '그날 서연의 물은 박 소장이 대신 날랐다. "그럴 때 부르라고 내가 있는 거예요."', true],
    [(c) => c.has('fire_handled'), '"그날 복도 불, 판단한 사람 덕에 다들 무사했어요." 박 소장은 지금도 그 얘기를 한다.', true],
    [() => true, '박 소장은 오늘도 관리사무소 창구를 지킨다. 빈 집 문단속도 여전히 그의 몫이다.', true],
  ],
  clerk: [
    [(c) => c.ending === 'reopen', '수아는 나눔 선반 장부에 가져간 것과 두고 간 것을 나란히 적는다.', true],
    [(c) => c.has('store_took'), '수아는 재고 장부의 빈 칸을 지우지 않고 남겨 두었다. 누구였는지는 묻지 않았다.', false],
    [(c) => c.has('store_shuttered'), '수아는 셔터에 붙어 있던 쪽지를 사진으로 찍어 두었다고 했다.', false],
    [(c) => c.promise('store_watch') === 'broken', '수아가 돌아왔을 때 가게는 반쯤 열린 채 비어 있었다. 수아는 말없이 선반부터 닦았다.', false],
    [(c) => c.has('clerk_contact'), '수아에게서 가끔 소식이 온다. 본가 쪽은 무사하다고 했다.', false],
    [() => true, '바로편의점 셔터는 아직 내려가 있다.', false],
  ],
  elder: [
    [(c) => (c.has('elder_alert_knock') || c.has('elder_alert_told')) && c.town, '304호 어르신은 급수차가 오면 302호 몫까지 물을 받아 두신다.', true],
    [(c) => c.has('elder_alert_knock'), '304호 어르신이 박 소장 편에 안부를 전해 왔다. "그날 계단에서 정말 고마웠어요."', false],
    [(c) => c.has('elder_alert_told'), '304호 어르신이 박 소장 편에 안부를 전해 왔다. "그날 소장님한테 알려 줘서 고마웠어요."', false],
    [(c) => c.has('elder_candle_tip') && c.has('fire_warned'), '"촛불 말해 준 거 깜빡했어요. 그래도 그 밤 \'불이야\' 소리에 바로 나왔지."', true],
    [(c) => c.has('fire_warned'), '304호 어르신은 그 밤 "불이야" 소리를 듣고 바로 나왔다며 고마워하셨다.', true],
    [(c) => c.has('elder_candle_tip'), '304호 어르신은 이제 손전등을 머리맡에 두고 주무신다.', true],
    [(c) => !['elder_met', 'hall_checked', 'fire_warned', 'elder_alert_knock', 'elder_alert_told'].some(c.has), '304호 어르신의 이름은 끝내 몰랐다. 아드님이 모시러 왔다는 얘기만 들었다.', false],
    [() => true, '304호 어르신은 복도에서 마주치면 먼저 고개를 끄덕여 주신다.', true],
  ],
};

// [pred, line]: people met once. At most three are shown, in this order.
const STRANGERS = [
  [(c) => c.has('driver_text') && c.path !== 'walk', '김 기사님에게서 문자가 왔다. 화물차로 구호물자를 나르고 있다고 했다.'],
  [(c) => c.has('looter_gave'), '먹을 것을 나눠 준 그 사람을 몇 주 뒤 배급소에서 다시 만났다. 그는 고개를 숙여 인사했다.'],
  [(c) => c.has('knock_helped'), '그날 밤 문 너머로 물을 청하던 사람을, 며칠 뒤 조끼를 입은 자원봉사자로 다시 만났다.'],
  [(c) => c.has('looter_escaped'), '골목에서 물러섰던 날을 가끔 떠올린다. 물러선 것을 후회하지 않는다.'],
  [(c) => c.has('looter_fought'), '골목에서 주먹을 휘둘렀던 날의 기억은 오래 꿈에 나왔다.'],
  [(c) => c.has('shelter_water_received'), '대피소에서 물을 건네받았던 일을 기억한다. 급수 줄에서, 이번에는 내가 물을 건넸다.'],
  [(c) => c.has('shelter_water_given'), '대피소 옆자리 아이의 가족이 급수 줄에서 먼저 알아보고 손을 흔들었다.'],
  [(c) => c.has('mart_share'), '마트에서 생수를 나눠 드린 할머니를 급수 줄에서 다시 만났다.'],
  [(c) => c.has('rumor_corrected'), (c) => (c.detail('rumor_corrected').after === 'spread'
    ? '102동 주민은 다시 알려 줘서 고맙다고 했다. 고친 말도 처음 말처럼 빨리 돌았다.'
    : '102동 주민은 그 뒤로 들은 말을 옮기기 전에 출처부터 묻는다고 했다.')],
  [(c) => c.has('lead_honest') && c.has('yoon_reply') && c.ending !== 'relief', '윤 팀장에게서 문자가 왔다. "확인된 것만 말하던 사람, 일손이 필요할 때 생각나더라."'],
];

// The bus ending's mailbox: a short letter from each neighbour with a bond of 3+ who had a way to reach you.
const LETTERS = {
  seoyeon: ['seoyeon_contact', '"저도 무사히 도착했어요. 연락 방법을 미리 정해 둔 덕분에 서로 소식을 놓치지 않았네요."'],
  manager: ['manager_contact', '"302호, 잘 지내지요? 단지는 내가 지키고 있어요. 빈 집 문단속도 계속하고 있고요."'],
  clerk: ['clerk_contact', '"본가 쪽은 다들 무사해요. 가게 다시 열면 제일 먼저 알려 드릴게요."'],
  elder: ['elder_met', '"302호 청년, 그때 고마웠어요. 끼니 거르지 말고 몸 성히 지내요."'],
};
export const letterOf = (who, a, has) => (a[who] >= 3 && has(LETTERS[who][0]) ? LETTERS[who][1] : null);

export function peopleLines(G, endingId) {
  const c = context(G, endingId);
  const out = Object.keys(NEIGHBOURS).map((who) => {
    const [, line, present] = RULES[who].find(([pred]) => pred(c));
    return { who, name: NEIGHBOURS[who].name, line, present, letter: endingId === 'bus' ? letterOf(who, c.a, c.has) : null };
  });
  for (const [pred, line] of STRANGERS.filter(([pred]) => pred(c)).slice(0, 3)) out.push({ who: 'stranger', name: '', line: typeof line === 'function' ? line(c) : line, present: false });
  return out;
}

export function townLines(G, endingId) {
  const c = context(G, endingId), t = c.a.town;
  const band = t >= 10 ? '단지 게시판에는 물을 나눈 사람의 이름과 날짜가 빼곡히 적혀 있다.'
    : t >= 4 ? '급수대에서 남의 물통을 들어 주는 일이 흔해졌다.' : '단지의 문들은 대체로 닫혀 있다.';
  const out = c.town || c.path === 'seoyeon' ? [band] : ['연무동 소식은 박 소장의 문자로 들었다.', band]; // the seoyeon path is back in town by D+14
  if (c.has('rumor_corrected') || c.has('rumor_checked')) out.push('대피소가 폐쇄됐다는 소문은 하루를 넘기지 못했다.');
  else if (c.has('rumor_spread')) out.push('대피소가 폐쇄됐다는 소문은 한참 동안 돌았다.');
  if (out.length < 2) out.push('급수차는 매일 단지 입구에 섰다.');
  return out;
}
