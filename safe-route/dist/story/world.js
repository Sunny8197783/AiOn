// Things the player can use or look at in any chapter, when the chapter itself has no handler for the point:
// ATMs, vending machines, the bus stop, the wallet with the ID card, and a short description for everything
// else that has one (point.desc on the level, or DESC below). runtime.roam calls this before a chapter's '*'.
import { buy, recordEvent } from '../game/state.js';
import { fact } from '../game/facts.js';

const has = (G, id) => (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0) > 0;

// level:point -> lines (or (ctx) => lines). Chapters that handle a point override these.
const DESC = {
  'apartment:homeSink': (c) => (c.G.day >= 1 ? ['수도꼭지를 돌려도 물이 나오지 않는다. 단수다.'] : ['설거지가 조금 남아 있다. 물은 잘 나온다.']),
  'apartment:homeWindow': (c) => (c.power ? ['건너편 동 창문마다 불이 켜져 있다. 평소와 같은 저녁 풍경.'] : ['건너편 동이 캄캄하다. 창문 몇 곳에서 손전등 불빛이 흔들린다.']),
  'apartment:homeFridge': (c) => (c.power ? ['반찬 통 몇 개와 남은 고기. 냉장고는 잘 돌아간다.'] : ['냉장고가 조용하다. 문을 자주 열면 안의 냉기가 빨리 빠진다.', fact('food_door')?.text].filter(Boolean)),
  'apartment:homeGas': ['레인지 옆 가스 중간밸브. 손잡이가 배관과 나란하면 열림, 직각이면 잠김이다.'],
  'apartment:homeBreaker': ['현관 위 분전반. 누전차단기와 개폐기가 줄지어 있다.'],
  'apartment:homeDesk': (c) => (c.power ? ['노트북과 스탠드. 회사 메일은 확인할 게 없다.'] : ['노트북 화면이 꺼져 있다. 배터리를 아끼려면 켜지 않는 게 낫다.']),
  'apartment:homeBed': ['아직 잘 시간은 아니다.'],
  'apartment:homeStock': ['베란다 수납장. 생수와 비상용품을 넣어 두는 곳이다.'],
  'apartment:homePlanner': ['이번 주 계획판. 관리비 25일까지, 분리수거 화·금.'],
  'apartment:homeRadio': (c) => (has(c.G, 'radio') ? ['거실 탁자 위 휴대용 라디오.'] : ['라디오가 없다. 휴대폰 FM 라디오 앱은 쓸 수 있다 (P).']),
  'apartment:homeBag': ['현관 옆 배낭. 대피할 때 들고 나갈 것을 담아 두는 곳이다.'],
  'apartment:lobbyNotice': ['단지 게시판: 관리비 안내, 분리수거 요일, 승강기 정기 점검 일정.'],
  'apartment:managerWindow': ['관리사무소 창구. 박 소장이 전화를 받고 있다.'],
  'apartment:elevator': (c) => (c.power ? ['엘리베이터. 정전되면 멈추니 비상시에는 계단을 쓴다.'] : ['엘리베이터가 멈춰 있다. 버튼에 불이 들어오지 않는다.', fact('alert_stairs')?.text].filter(Boolean)),
  'apartment:stairs1F': ['계단실. 층마다 비상조명등이 있다.'], 'apartment:stairs3F': ['계단실. 4층 쪽은 물청소 중이라 막혀 있다.'],
  'apartment:extinguisher': ['복도 옥내소화전 옆 분말소화기. 안전핀 → 노즐 → 손잡이 → 쓸듯이.'],
  'apartment:seoyeonDoor': ['303호. 오서연 씨 집이다.'],
  'apartment:homeDoor': ['302호, 우리 집.'],
  'town:center': ['연무동 행정복지센터. 민원실 유리문 안쪽으로 번호표 기계가 보인다.'],
  'town:cafe': (c) => (c.power ? ['카페 쉼표. 커피 냄새가 난다.'] : ['카페 쉼표. 문이 잠겨 있고 "정전으로 임시 휴업" 종이가 붙어 있다.']),
  'town:aptNotice': ['한빛아파트 단지 게시판.'],
  'town:centerNotice': ['행정복지센터 게시판. 민방위 대피소 안내도가 붙어 있다.'],
  'town:shelterNotice': ['연무 생활문화센터 지하주차장 · 민방위 대피소 안내판.'],
};

export function isCommon(ctx, point) {
  const lv = ctx.engine.level?.id;
  return point.kind === 'atm' || point.kind === 'vending' || point.kind === 'busStop' || point.id === 'homeWallet' || !!point.desc || !!DESC[`${lv}:${point.id}`];
}

export async function common(ctx, point) {
  const lv = ctx.engine.level?.id;
  if (point.kind === 'atm') return atm(ctx);
  if (point.kind === 'vending') return vending(ctx);
  if (point.kind === 'busStop') return busStop(ctx);
  if (point.id === 'homeWallet') return wallet(ctx);
  const d = point.desc ?? DESC[`${lv}:${point.id}`];
  const lines = [].concat(typeof d === 'function' ? d(ctx) : d).filter(Boolean);
  if (lines.length) await ctx.ui.talk({ speaker: point.label, role: '살펴보기', lines });
}

async function atm(ctx) {
  const G = ctx.G;
  if (!ctx.power) return void await ctx.ui.talk({ speaker: 'ATM', role: '화면 꺼짐', lines: ['화면이 까맣다. 정전으로 자동화기기가 멈췄다.', '전기가 끊기면 카드도 ATM도 쓸 수 없다. 현금은 미리 찾아 둬야 한다.'] });
  const pick = await ctx.ui.talk({ speaker: 'ATM', role: '현금 인출', lines: ['카드를 넣고 비밀번호를 눌렀다.', `계좌 잔액 ${G.money.toLocaleString()}원. 얼마를 찾을까?`],
    choices: [{ id: '1', label: '5만 원', disabled: G.money < 50000 }, { id: '2', label: '10만 원', disabled: G.money < 100000 }, { id: '3', label: '15만 원', disabled: G.money < 150000 }, { id: 'no', label: '취소' }] });
  const n = Number(pick);
  if (!n) return;
  buy(G, { cash: n }, { cash: 50000 });
  ctx.event('cash', { n });
  ctx.toast(`현금 ${n * 5}만 원을 찾아 지갑에 넣었다.`);
}

async function vending(ctx) {
  const G = ctx.G;
  if (!ctx.power) return void await ctx.ui.talk({ speaker: '음료 자판기', role: '전원 꺼짐', lines: ['불이 꺼진 자판기 안이 어둡다. 버튼을 눌러도, 동전을 넣어도 반응이 없다.'] });
  const pick = await ctx.ui.talk({ speaker: '음료 자판기', role: '카드·현금', lines: ['생수 500mL 1,000원.'], choices: [{ id: 'buy', label: '생수 한 병 산다', disabled: G.money < 1000 }, { id: 'no', label: '그만둔다' }] });
  if (pick !== 'buy') return;
  buy(G, { water_500: 1 }, { water_500: 1000 });
  ctx.toast('생수 한 병을 샀다.');
}

async function busStop(ctx) {
  const lines = ctx.power ? ['도착 안내 전광판: "마을 3 곧 도착 · 720 6분"', '오늘은 버스를 탈 일이 없다.']
    : ['도착 안내 전광판이 꺼져 있다. 기둥에 종이가 붙어 있다.', '"정전·교통 통제로 시내버스 운행을 중단합니다. 주민 이동 지원 차량은 행정복지센터 앞에서 출발합니다. (가상 안내)"'];
  await ctx.ui.talk({ speaker: '버스 정류장', role: '연무동 행정복지센터 방면', lines });
}

async function wallet(ctx) {
  const G = ctx.G;
  const e = (G.items.id_card ??= { home: 0, bag: 0, bought: 0, checked: true, used: 0 });
  if (e.home + e.bag > 0) return void ctx.toast('신분증은 이미 챙겼다.');
  e.bag = 1;
  G.know.idTaken = true;
  ctx.setLevelState('apartment', 'wallet', 'taken');
  recordEvent(G, 'id_taken', {});
  ctx.toast(`지갑과 신분증을 챙겼다. ${fact('docs_take')?.text ?? ''}`, 'self', 5000);
}
