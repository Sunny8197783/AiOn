// 1장 준비 · D-2 수요일 18:30 ~ D-1 목요일 밤. Six time slots, more things to do than fit.
import { spendSlot, usable, adjust } from '../game/state.js';
import { CATALOG } from '../game/items.js';
import { doMart, doShelterCheck, doEquip, doDocs, doNeighbors, doBag, goHome, applyHome } from './prep-actions.js';

// Slot start times: [day, minute]. Slot 0 is tonight; slots 1-5 are tomorrow.
const SLOT_AT = [[-2, 18 * 60 + 30], [-1, 9 * 60], [-1, 11 * 60], [-1, 13 * 60 + 30], [-1, 15 * 60 + 30], [-1, 17 * 60 + 30], [-1, 19 * 60 + 30]];
const SLOT_LABEL = ['D-2 저녁', 'D-1 오전', 'D-1 오전', 'D-1 오후', 'D-1 오후', 'D-1 저녁'];

export const ACTIONS = [
  { id: 'mart', label: '연무마트 장보기', cost: 2, icon: '🛒', desc: '물, 식량, 건전지, 라디오 같은 물품을 산다. 사람이 몰려 시간이 걸린다.' },
  { id: 'shelter', label: '대피소·출입구 확인', cost: 1, icon: '🧭', desc: '지정 대피소까지 직접 걸어가 출입구와 경로를 확인한다.' },
  { id: 'equip', label: '집 장비 점검', cost: 1, icon: '🔦', desc: '라디오·손전등에 건전지를 넣어 보고, 보조배터리를 충전하고, 소화기와 가스 밸브 위치를 확인한다.' },
  { id: 'docs', label: '서류·연락처 정리', cost: 1, icon: '🗂', desc: '비상연락카드를 쓰고 중요 서류 사본을 만든다.' },
  { id: 'neighbors', label: '이웃과 비상 연락 정하기', cost: 1, icon: '🤝', desc: '303호 서연, 관리사무소 박 소장, 편의점 수아와 이야기한다.' },
  { id: 'bag', label: '비상가방 꾸리기', cost: 1, icon: '🎒', desc: '집에 있는 물품 가운데 들고 나갈 것을 배낭에 담아 현관에 둔다.' },
  { id: 'rest', label: '휴식', cost: 1, icon: '🛏', desc: '쉬면서 피로와 긴장을 덜어 낸다.' },
];

function setClockToSlot(G, i) { const [d, m] = SLOT_AT[Math.min(i, SLOT_AT.length - 1)]; G.day = d; G.minute = m; }
function envForSlot(i) { return i === 0 ? 'night' : i >= 5 ? 'dusk' : 'day'; }

export async function prep(ctx) {
  const { G } = ctx;
  G.chapter = 'prep';
  ctx.indoorEnv = 'interiorNight'; ctx.power = true; ctx.outdoorEnv = 'night';
  ctx.hud.setChapter('1장', '준비 — 남은 시간 여섯 칸');
  applyHome(ctx);
  await ctx.goto('apartment', 'home');
  ctx.refresh();
  await ctx.card({ kind: 'info', title: '103동 302호', meta: 'D-2 수요일 18:30',
    body: ['오늘 저녁 한 칸, 내일 다섯 칸. 모든 것을 다 할 수는 없다.', '거실 벽의 달력에서 할 일을 고르자. 물건을 많이 사는 것보다, 실제로 쓸 수 있게 해 두는 것이 중요하다.'],
    actions: [{ id: 'ok', label: '달력 보기', primary: true }] });

  let opened = false;
  while (G.slots.used < G.slots.total) {
    const slot = G.slots.used;
    setClockToSlot(G, slot);
    ctx.outdoorEnv = envForSlot(slot);
    ctx.indoorEnv = slot === 0 ? 'interiorNight' : 'interior';
    ctx.engine.setEnv(ctx.indoorEnv, { seconds: 0.5 });
    ctx.setLevelState('apartment', 'windowView', slot === 0 ? 'night' : slot >= 5 ? 'dusk' : 'day');
    ctx.refresh();
    ctx.objective(`${SLOT_LABEL[slot]} · 무엇을 할까`, '거실 벽의 달력(계획판)에서 고릅니다. 집 안을 둘러볼 수도 있습니다.',
      [{ text: `남은 칸 ${G.slots.total - G.slots.used}`, done: false }]);
    const pick = opened ? await ctx.roam({ doors: false, on: { homePlanner: () => openPlanner(ctx, slot), ...homeFree(ctx) } }) : await openPlanner(ctx, slot);
    opened = true;
    if (!pick) continue;
    const action = ACTIONS.find(a => a.id === pick);
    spendSlot(G, action.id, action.cost);
    ctx.event('prep_action', { id: action.id, slot });
    await runAction(ctx, action, slot);
    if (slot === 0 && G.slots.used < G.slots.total) {
      await ctx.skip('D-2 수 밤', 'D-1 목 09:00', '밤새 뉴스 속보 알림이 몇 번 울렸다. 정민은 잠을 설쳤다.');
      adjust(G, 'fatigue', 6); adjust(G, 'tension', 6);
    }
  }
  await eveningBeforeAlert(ctx);
}

async function openPlanner(ctx, slot) {
  const { G } = ctx;
  const left = G.slots.total - G.slots.used;
  const done = new Set(G.slots.done.map(d => (typeof d === 'string' ? d : d.id)));
  const actions = ACTIONS.map(a => ({ ...a, done: done.has(a.id) && a.id !== 'rest',
    disabled: a.cost > left || (done.has(a.id) && a.id !== 'rest') || (slot === 0 && a.cost > 1),
    reason: slot === 0 && a.cost > 1 ? '마트는 곧 문을 닫습니다. 내일 하세요.' : a.cost > left ? `남은 칸이 ${left}칸이라 할 수 없습니다.` : done.has(a.id) ? '이미 했습니다.' : '' }));
  const hint = slot === 0 ? '오늘 저녁은 한 칸뿐입니다. 마트는 두 칸이 드는 데다 곧 문을 닫습니다.'
    : !usable(G, 'flashlight') && !G.items.batteries_aa?.home ? '손전등은 있지만 건전지가 없습니다. 있다는 것과 쓸 수 있다는 것은 다릅니다.' : '';
  return ctx.ui.planner({ day: SLOT_LABEL[slot], slots: { total: G.slots.total, used: G.slots.used }, actions, hint });
}

async function runAction(ctx, action, slot) {
  const { G } = ctx;
  const end = SLOT_AT[Math.min(slot + action.cost, SLOT_AT.length - 1)];
  switch (action.id) {
    case 'mart': await doMart(ctx, slot); break;
    case 'shelter': await doShelterCheck(ctx); break;
    case 'equip': await doEquip(ctx); break;
    case 'docs': await doDocs(ctx); break;
    case 'neighbors': await doNeighbors(ctx); break;
    case 'bag': await doBag(ctx); break;
    case 'rest':
      await ctx.card({ kind: 'info', title: '휴식', meta: SLOT_LABEL[slot], body: ['휴대폰을 내려놓고 잠깐 눈을 붙였다. 몸이 조금 가벼워졌다.'], actions: [{ id: 'ok', label: '계속', primary: true }] });
      adjust(G, 'fatigue', -25); adjust(G, 'tension', -12);
      break;
  }
  G.day = end[0]; G.minute = Math.max(G.minute, end[1]);
  if (ctx.engine.level?.id !== 'apartment') await goHome(ctx);
  applyHome(ctx);
}

// Things the player can do at home without spending a slot.
function homeFree(ctx) {
  const { G } = ctx;
  return {
    homeWindow: async () => { await ctx.ui.talk({ speaker: '창밖', role: '연무동', lines: [ctx.outdoorEnv === 'day' ? '평소처럼 버스가 다닌다. 마트 주차장 쪽에 차가 많다.' : '건너편 동의 불빛이 하나둘 켜진다. 평소와 다를 것 없어 보인다.'] }); },
    homeFridge: async () => { await ctx.ui.talk({ speaker: '냉장고', role: '주방', lines: ['반찬 통 네 개, 어제 남은 익힌 고기 한 통. 전기가 끊기면 오래 버티지 못하는 것들이다.'] }); },
    homeStock: async () => {
      const lines = Object.entries(G.items).filter(([, v]) => v.home > 0).map(([id, v]) => `${CATALOG[id]?.name ?? id} ${v.home}${CATALOG[id]?.unit ?? ''}${CATALOG[id]?.needsCheck && !v.checked ? ' (아직 점검 안 함)' : ''}`);
      await ctx.ui.talk({ speaker: '베란다 수납장', role: '집 비축품', lines: [lines.join(' · ') || '비어 있다.'] });
    },
    homeBed: async () => { ctx.toast('아직 잘 시간이 아니다.'); },
    homeDesk: async () => {
      if (G.know.shelterLocated) return void ctx.toast('지도앱에 생활문화센터 대피소를 저장해 두었다.');
      const pick = await ctx.ui.talk({ speaker: '휴대폰', role: '지도앱', lines: ['지도앱에서 "민방위 대피소"를 검색해 볼까?'], choices: [{ id: 'yes', label: '검색한다' }, { id: 'no', label: '나중에' }] });
      if (pick !== 'yes') return;
      G.know.shelterLocated = true;
      ctx.event('shelter_located', { via: 'map_app' });
      await ctx.ui.talk({ speaker: '지도앱', role: '검색 결과', lines: ['가장 가까운 민방위 대피소: 연무 생활문화센터 지하주차장 (도보 약 4분).', '이름과 주소만 나온다. 출입구가 어디인지, 지금 열려 있는지는 지도만으로 알 수 없다.'] });
      ctx.note('가장 가까운 대피소: 연무 생활문화센터 지하주차장', 'official');
    },
  };
}

// D-1 evening: news, the bathtub, then sleep. Leaves the clock at D-day 19:30.
async function eveningBeforeAlert(ctx) {
  const { G } = ctx;
  G.day = -1; G.minute = 20 * 60;
  ctx.outdoorEnv = 'night'; ctx.indoorEnv = 'interiorNight';
  ctx.engine.setEnv('interiorNight', { seconds: 1 });
  ctx.setLevelState('apartment', 'windowView', 'night');
  if (ctx.engine.level?.id !== 'apartment') await goHome(ctx);
  ctx.refresh();
  await ctx.card({ kind: 'news', title: '저녁 뉴스 (가상)', meta: 'D-1 목 20:00',
    body: ['정부는 국민들에게 비상시 국민행동요령을 미리 확인해 달라고 당부했습니다.', '일부 지역에서 생수와 건전지를 대량으로 사 가는 일이 이어지자, 사재기를 자제하고 필요한 만큼만 구입해 달라고 요청했습니다.'],
    actions: [{ id: 'ok', label: 'TV를 끈다', primary: true }] });
  ctx.objective('잠들기 전에', '할 수 있는 일이 남아 있다면 해 두고, 침대에서 잠듭니다.', [{ text: '주방에서 큰 통·냄비에 물 받아 두기 — 선택', done: !!G.know.tubFilled }, { text: '침대에서 잠들기', done: false }]);
  await ctx.roam({ doors: false, on: {
    homeSink: async () => {
      if (G.know.tubFilled) return void ctx.toast('큰 통과 냄비에 물을 받아 두었다.');
      const pick = await ctx.ui.talk({ speaker: '주방', role: '단수 대비', lines: ['단수에 대비해 큰 통과 냄비에 물을 받아 둘까? 씻거나 변기 물을 내리는 데 쓸 수 있다.'], choices: [{ id: 'yes', label: '받아 둔다' }, { id: 'no', label: '그만둔다' }] });
      if (pick === 'yes') { G.know.tubFilled = true; ctx.event('tub_filled', {}); ctx.toast(ctx.factLine('home_tub'), 'official', 6000); }
    },
    homeBed: async () => 'sleep',
    ...homeFree(ctx),
  } });
  await ctx.skip('D-1 목 밤', 'D-day 금 19:30', '다음 날, 회사는 오후 4시에 문을 닫았다. 정민은 일찍 집에 들어와 저녁을 먹었다.');
  G.day = 0; G.minute = 19 * 60 + 30;
  adjust(G, 'fatigue', -10);
}
