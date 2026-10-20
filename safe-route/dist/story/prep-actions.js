// The six preparation actions, each played in 3D (walk there, look, decide).
import { buy, pack, unpack, check, consume, recordEvent } from '../game/state.js';
import { routeOptions } from '../game/routes.js';
import { evalBag, recordEncounter, ladderStage } from '../game/drills.js';
import { CATALOG, MART_SECTIONS, BAG_LIMIT } from '../game/items.js';
import { fact } from '../game/facts.js';

export const SEOYEON_CARD = ['보행 보조기를 씁니다', '평지와 완만한 경사로는 혼자 갑니다', '계단과 가파른 내리막은 혼자 내려가지 않습니다', '보조기와 떨어지지 않습니다', '도움이 필요하면 제가 말합니다'];
export const RADIO_FREQ = 102.1; // game frequency of the disaster-broadcast station (fictional)

const has = (G, id, where = 'home') => (G.items[id]?.[where] ?? 0) > 0;
const tips = (...ids) => ids.map(id => fact(id)?.text).filter(Boolean);

export function applyHome(ctx) {
  const { G } = ctx;
  const stock = Object.fromEntries(Object.entries(G.items).map(([id, v]) => [id, v.home]));
  ctx.setLevelState('apartment', 'stock', stock);
  ctx.setLevelState('apartment', 'bag', G.know.bagPacked ? 'packed' : 'empty');
  ctx.setLevelState('apartment', 'radio', has(G, 'radio') || has(G, 'radio', 'bag') ? (G.know.radioOn ? 'on' : 'off') : 'none');
  ctx.setLevelState('apartment', 'power', ctx.power ? 'on' : 'off');
}

// Back home after an errand: the walk takes time, heavier with shopping.
export async function goHome(ctx, carryKg = 0) {
  const minutes = 6 + Math.round(Math.max(0, carryKg - 6) * 1.5);
  ctx.pass(minutes);
  await ctx.goto('apartment', 'home');
}

// Leave 302호 for the town. With power on the elevator takes you straight down.
export async function goOut(ctx, title, body) {
  ctx.objective(title, body, []);
  if (ctx.engine.level?.id === 'town') return;
  await ctx.roam({ on: {
    homeExitDoor: async () => {
      if (!ctx.power) { await ctx.goto('apartment', 'corridor3F'); return; }
      const pick = await ctx.ui.talk({ speaker: '현관', role: '302호', lines: ['나가자.'], choices: [{ id: 'lift', label: '엘리베이터로 1층까지' }, { id: 'hall', label: '복도로 나간다' }] });
      if (pick === 'lift') { await ctx.goto('town', 'home'); return 'out'; }
      await ctx.goto('apartment', 'corridor3F');
    },
    homeDoor: async () => { await ctx.goto('apartment', 'homeEntry'); },
    elevator: async () => {
      if (!ctx.power) return void ctx.toast('엘리베이터가 멈춰 있다. 계단을 이용하자.');
      await ctx.goto('apartment', ctx.engine.player.y > 1 ? 'lobby' : 'corridor3F');
    },
  }, tick: () => (ctx.engine.level?.id === 'town' ? 'out' : undefined) });
}

function shopItems(G, section, soldOut) {
  return MART_SECTIONS[section].items.map(id => {
    const c = CATALOG[id];
    const note = c.needsCheck === 'batteries' ? '건전지 별매' : c.needsCheck === 'charge' ? '사용 전 충전 필요' : c.risk === 'fire' ? '화기 주의' : c.needs?.includes('hot_water') ? '끓는 물 필요' : '';
    return { id, name: c.name, price: c.price, unit: c.unit, weightKg: c.weightKg, volumeL: c.volumeL, stock: soldOut.includes(id) ? 'out' : id === 'water_2l' ? 'low' : 'ok', note, label: '' };
  });
}
function soldOutAt(G, slot) {
  const so = G.cond?.soldOut || {};
  const list = slot <= 0 ? [] : slot <= 2 ? (so.early || []) : (so.late || so.early || []);
  return list.flatMap(x => MART_SECTIONS[x] ? MART_SECTIONS[x].items : [x]);
}

export async function doMart(ctx, slot) {
  const { G } = ctx;
  const soldOut = soldOutAt(G, slot);
  ctx.setLevelState('mart', 'crowd', 'busy');
  ctx.setLevelState('mart', 'soldOut', Object.keys(MART_SECTIONS).filter(s => MART_SECTIONS[s].items.every(i => soldOut.includes(i))));
  await goOut(ctx, '연무마트에 가기', '단지 정문으로 나가 연무로 건너편 연무마트로 갑니다.');
  ctx.objective('장보기', '진열대에서 고르고 계산대에서 계산합니다. 필요한 만큼, 들고 올 수 있는 만큼.', [{ text: '진열대 둘러보기', done: false }, { text: '계산하기', done: false }]);
  let carried = 0, boughtAny = false, hoard = false;
  const info = [{ text: '생수 1인 6병 한정', label: 'site' }];
  if (G.rel.clerk?.contact) info.push({ text: '수아: "건전지는 오늘 오전에 더 들어온다고 했어요. 확실하진 않아요."', label: 'neighbor' });
  const section = (sec) => async () => {
    const res = await ctx.ui.shop({ title: MART_SECTIONS[sec].label, money: G.money, items: shopItems(G, sec, soldOut), info, budgetNote: `남은 돈 ${G.money.toLocaleString()}원` });
    if (!res || !res.total) return;
    const cart = { ...res.cart };
    if (cart.water_2l) { const already = G.items.water_2l?.bought ?? 0; cart.water_2l = Math.max(0, Math.min(cart.water_2l, 6 - already)); }
    for (const id of Object.keys(cart)) if (!cart[id] || soldOut.includes(id)) delete cart[id];
    if (!Object.keys(cart).length) return void ctx.toast('담을 수 있는 물건이 없습니다.', 'site');
    if (Object.values(cart).some(q => q > 8)) hoard = true;
    const prices = Object.fromEntries(Object.keys(cart).map(id => [id, CATALOG[id].price]));
    try { buy(G, cart, prices); } catch (e) { return void ctx.toast(e.message || '살 수 없습니다.', 'site'); }
    boughtAny = true;
    carried += Object.entries(cart).reduce((s, [id, q]) => s + CATALOG[id].weightKg * q, 0);
    ctx.event('bought', { cart, slot });
  };
  await ctx.roam({ on: {
    shelfWater: section('water'), shelfFood: section('food'), shelfDaily: section('daily'), shelfElec: section('elec'), shelfHealth: section('health'),
    atm: async () => {
      const pick = await ctx.ui.talk({ speaker: 'ATM', role: '연무마트 입구', lines: [`잔액 ${G.money.toLocaleString()}원. 현금을 뽑을까?`], choices: [{ id: '1', label: '5만 원', disabled: G.money < 50000 }, { id: '2', label: '10만 원', disabled: G.money < 100000 }, { id: 'no', label: '그만' }] });
      if (pick === '1' || pick === '2') { buy(G, { cash: Number(pick) }, { cash: 50000 }); ctx.event('cash', { n: Number(pick) }); ctx.toast(`현금 ${Number(pick) * 5}만 원을 찾았다.`); }
    },
    checkout: async () => {
      if (!boughtAny) return void ctx.toast('아직 고른 물건이 없습니다.');
      if (hoard) {
        await ctx.ui.talk({ speaker: '계산원', role: '연무마트', lines: ['오늘 많이들 사 가셔서요… 다른 분들 몫도 남겨 주시면 감사하겠습니다.'] });
        ctx.event('hoard', {}); ctx.toast(fact('home_nohoard').text, 'official', 6000);
      }
      await ctx.ui.talk({ speaker: '계산 완료', role: `남은 돈 ${G.money.toLocaleString()}원`, lines: [`산 물건 무게 약 ${carried.toFixed(1)}kg. ${carried > 12 ? '한 번에 들고 가기 버겁다.' : '들고 갈 만하다.'}`] });
      return 'done';
    },
    martExit: async () => {
      if (boughtAny) return void ctx.toast('계산대에서 계산을 마치세요.');
      const pick = await ctx.ui.talk({ speaker: '연무마트', role: '출입구', lines: ['아무것도 사지 않고 나갈까?'], choices: [{ id: 'yes', label: '나간다' }, { id: 'no', label: '더 둘러본다' }] });
      if (pick === 'yes') return 'done';
    },
  } });
  if (carried > 12) ctx.toast('무거워서 두 번에 나눠 옮겼다.');
  await goHome(ctx, carried);
}

const ENTRANCE_TEXT = {
  shelterEast: ['동쪽 출입구 · 생활문화센터 정문', '유리문 안쪽 계단과 엘리베이터로 지하주차장에 내려간다. 평소 가장 많이 쓰는 문이다.'],
  shelterA: ['출입구 A · 계단', '지하주차장으로 바로 내려가는 계단 출입구. 계단 양쪽에 손잡이가 있다.'],
  shelterB: ['출입구 B · 지하주차장 진출입로', '차가 드나드는 경사로다. 경사가 가파르고, 평소에는 차가 다닌다.'],
  shelterC: ['출입구 C · 경사로', '완만한 경사로와 양쪽 손잡이. 셔터가 내려져 있고 안내문이 붙어 있다: "공사 중 · 10월 말 개방 예정. 비상시에는 현장 안내에 따라 열 수 있습니다."'],
};

export async function doShelterCheck(ctx) {
  const { G } = ctx;
  await goOut(ctx, '생활문화센터 대피소 확인하기', '연무로를 따라 서쪽, 연무 생활문화센터로 갑니다.');
  const seen = new Set(); let board = false;
  const steps = () => [{ text: `출입구 살펴보기 (${seen.size}/4)`, done: seen.size >= 3 }, { text: '현장 안내판 읽기', done: board }];
  ctx.objective('출입구와 안내판 확인', '각 출입구 앞에서 E로 살펴봅니다.', steps());
  const look = (id) => async () => {
    const [title, text] = ENTRANCE_TEXT[id];
    await ctx.ui.talk({ speaker: title, role: '현장 확인', lines: [text] });
    if (!seen.has(id)) { seen.add(id); ctx.note(`${title}: ${text}`, 'site', 'culture'); }
    ctx.objective('출입구와 안내판 확인', '각 출입구 앞에서 E로 살펴봅니다.', steps());
    if (seen.size >= 3 && board) return 'done';
  };
  await ctx.roam({ doors: false, on: {
    shelterEast: look('shelterEast'), shelterA: look('shelterA'), shelterB: look('shelterB'), shelterC: look('shelterC'),
    shelterNotice: async () => {
      await ctx.ui.card({ kind: 'notice', title: '대피소', meta: '빨간 바탕의 대피소 표지판 · SHELTER',
        body: ['연무 생활문화센터 지하주차장 · 민방위 대피소', '출입구: 동쪽 정문, A 계단, B 지하주차장 진출입로 (C 경사로 공사 중)', fact('shelter_signs').text],
        actions: [{ id: 'ok', label: '확인', primary: true }] });
      board = true; ctx.note('생활문화센터 대피소 출입구: 동쪽 정문, A 계단, B 진출입로 (C 공사 중)', 'site', 'culture');
      ctx.objective('출입구와 안내판 확인', '각 출입구 앞에서 E로 살펴봅니다.', steps());
      if (seen.size >= 3) return 'done';
    },
  } });
  const options = routeOptions(G, 'home', 'shelter', { closures: [], gates: { east: true, stairs: true, ramp: true, access: false }, mobility: 'walk' });
  const mode = ladderStage(G, 'route');
  const res = await ctx.modal(() => ctx.ui.routemap({ mode, from: 'home', to: 'shelter', options, closures: [], gates: { east: 'open', stairs: 'open', ramp: 'open', access: 'closed' }, known: { closures: [] }, playerAt: { x: ctx.engine.player.x, z: ctx.engine.player.z } }));
  recordEncounter(G, 'route', 'planned');
  G.know.shelterLocated = true; G.know.entrancesChecked = seen.size >= 3; G.know.routeChecked = true; G.know.altRoute = options.length > 1;
  G.know.plannedRoute = options[res?.choice ?? 0]?.nodes ?? null;
  recordEvent(G, 'shelter_checked', { seen: [...seen], board });
  ctx.note('집 → 대피소 경로를 확인했다. 막히면 돌아갈 길도 지도에서 봐 두었다.', 'self');
}

export async function doEquip(ctx) {
  const { G } = ctx;
  const done = { radio: false, light: false, power: false, ext: false, gas: false, breaker: false };
  const steps = () => [
    { text: '라디오 (거실 탁자)', done: done.radio }, { text: '손전등 (베란다 수납장)', done: done.light }, { text: '보조배터리 (책상)', done: done.power },
    { text: '소화기 위치 (3층 복도)', done: done.ext }, { text: '가스 밸브 (주방)', done: done.gas }, { text: '분전반 (현관)', done: done.breaker }];
  const show = () => ctx.objective('집 장비 점검', '다 마치면 달력(계획판)에서 끝냅니다.', steps());
  show();
  await ctx.roam({ on: {
    homeRadio: async () => {
      if (done.radio) return void ctx.toast('라디오는 점검을 마쳤다.');
      if (!has(G, 'radio')) { await ctx.ui.talk({ speaker: '거실 탁자', role: '라디오 자리', lines: ['라디오가 없다. 정전되면 방송을 들을 방법을 따로 찾아야 한다.'] }); done.radio = true; ctx.event('radio_missing', {}); return show(); }
      const ok = check(G, 'radio');
      if (!ok.ok) { await ctx.ui.talk({ speaker: '휴대용 라디오', role: '건전지식', lines: ['건전지가 없어 켜지지 않는다. 라디오는 있지만 지금은 쓸 수 없다.'] }); ctx.event('radio_blocked', { why: 'batteries' }); return; }
      const res = await ctx.ui.radio({ mode: 'guided', start: 89.5, battery: true, extraSeconds: 0, stations: [
        { freq: 95.1, name: '음악 방송', kind: 'music', lines: ['(잔잔한 음악이 흐른다)'] },
        { freq: RADIO_FREQ, name: `재난방송 주관방송사 라디오 (게임 속 주파수 ${RADIO_FREQ}MHz)`, kind: 'official', lines: ['지금은 평상시 프로그램입니다.', '비상시에는 이 방송에서 정부와 지자체 안내를 전합니다. (가상 방송)'] },
      ] });
      recordEncounter(G, 'radio', res?.station ? 'tuned' : 'opened');
      G.know.radioChecked = true; G.know.radioFreq = res?.station?.freq ?? RADIO_FREQ;
      ctx.note(`라디오 점검 완료. 재난방송 주파수 ${RADIO_FREQ}MHz에 맞춰 두었다.`, 'self');
      ctx.toast(fact('radio_batt').text, 'official', 5000);
      done.radio = true; show();
    },
    homeStock: async () => {
      if (done.light) return void ctx.toast('손전등은 점검을 마쳤다.');
      if (!check(G, 'flashlight').ok) { await ctx.ui.talk({ speaker: '손전등', role: '베란다 수납장', lines: ['손전등은 있는데 건전지가 없다. 켜지지 않는다.'] }); ctx.event('light_blocked', {}); return; }
      done.light = true; ctx.toast('손전등이 켜진다. 현관 신발장 위에 두었다.'); show();
    },
    homeDesk: async () => {
      if (done.power) return void ctx.toast('보조배터리는 충전해 두었다.');
      if (!has(G, 'powerbank')) { ctx.toast('보조배터리가 없다. 휴대폰을 완충해 두는 수밖에 없다.'); G.phone.battery = 100; done.power = true; return show(); }
      check(G, 'powerbank'); G.phone.battery = 100; done.power = true; ctx.toast('보조배터리와 휴대폰을 완충했다.'); show();
    },
    extinguisher: async () => {
      if (done.ext) return void ctx.toast('소화기 위치를 확인했다.');
      await ctx.ui.card({ kind: 'notice', title: '3층 복도 소화기', meta: '옥내소화전 옆', body: [fact('ext_1').text, fact('ext_2').text, fact('ext_3').text], actions: [{ id: 'ok', label: '사용 순서 익혀 보기', primary: true }] });
      await ctx.ui.extinguisher({ mode: 'guided', fire: 'small', practice: true });
      recordEncounter(G, 'extinguisher', 'practice');
      G.know.extinguisherLocated = true; done.ext = true; show();
    },
    homeGas: async () => { G.know.gasValve = true; done.gas = true; await ctx.ui.talk({ speaker: '가스 중간 밸브', role: '주방', lines: ['레인지 옆 가스 중간 밸브 위치를 확인했다.', fact('alert_caution').text] }); show(); },
    homeBreaker: async () => { G.know.breaker = true; done.breaker = true; await ctx.ui.talk({ speaker: '분전반', role: '현관 위', lines: ['누전차단기와 개폐기가 있다.', fact('power_home').text] }); show(); },
    homePlanner: async () => {
      const pick = await ctx.ui.talk({ speaker: '점검', role: '달력', lines: ['점검을 마칠까?'], choices: [{ id: 'yes', label: '마친다' }, { id: 'no', label: '조금 더 한다' }] });
      if (pick === 'yes') return 'done';
    },
  } });
  ctx.event('equip_done', { ...done });
}

export async function doDocs(ctx) {
  const { G } = ctx;
  ctx.objective('서류·연락처 정리', '작은방 책상에서 비상연락카드를 씁니다.', []);
  await ctx.roam({ on: { homeDesk: async () => 'go' } });
  const fields = [
    { id: 'me', label: '개인사항 (이름·주소·생년월일)', value: '정민 · 연무동 한빛아파트 103동 302호', why: fact('contact_fields').text, recommended: true },
    { id: 'blood', label: '혈액형', value: 'A형', why: '다쳤을 때 도움이 됩니다.', recommended: true },
    { id: 'meds', label: '복용약', value: '알레르기약 (1일 1회)', why: '약이 떨어지거나 잃어버렸을 때 필요합니다.', recommended: true },
    { id: 'family', label: '가족 연락처', value: '부모님(대구) 010-○○○○-○○○○', why: fact('contact_card').text, recommended: true },
    { id: 'work', label: '직장 연락처', value: '한결물류 연무센터 운영팀', why: '집·직장 연락처를 함께 적습니다.', recommended: true },
    { id: 'neighbors', label: '이웃 연락처', value: '303호 오서연 · 관리사무소', why: '휴대폰이 꺼져도 연락할 사람을 찾을 수 있습니다.', recommended: true },
    { id: 'agencies', label: '신고·구호기관', value: '119 · 112 · 정전 123', why: fact('power_123').text, recommended: true },
    { id: 'meet', label: '만남의 장소 1·2·3차', value: '① 생활문화센터 대피소 ② 행정복지센터 ③ 연무공원 정자', why: fact('contact_meet').text, recommended: true },
    { id: 'idcopy', label: '신분증·통장·계약서 사본', value: '프린터로 사본 1부씩', why: fact('bag_copies').text, recommended: true },
  ];
  const res = await ctx.modal(() => ctx.ui.docs({ mode: 'guided', fields, carry: ['bag', 'home'] }));
  recordEncounter(G, 'docs', 'written');
  const card = res?.card || {};
  const where = res?.where === 'bag' ? 'bag' : 'home';
  G.items.contact_card = { home: 0, bag: 0, bought: 0, checked: true, used: 0, ...(G.items.contact_card || {}) };
  G.items.contact_card[where] += 1;
  G.know.contactsCard = true;
  G.know.cardFields = Object.keys(card);
  if (card.idcopy) { G.items.doc_copies = { home: 0, bag: 0, bought: 0, checked: true, used: 0, ...(G.items.doc_copies || {}) }; G.items.doc_copies[where] += 1; G.know.docsCopied = true; }
  if (card.meet) G.know.meetPoint = '생활문화센터 대피소';
  ctx.event('docs_done', { fields: Object.keys(card), where });
  ctx.toast(where === 'bag' ? '비상연락카드를 배낭 앞주머니에 넣었다.' : '비상연락카드를 책상 서랍에 넣었다.');
}

export async function doNeighbors(ctx) {
  const { G } = ctx;
  const met = { seoyeon: false, manager: false, clerk: false };
  const steps = () => [{ text: '303호 서연', done: met.seoyeon }, { text: '관리사무소 박 소장 (1층)', done: met.manager }, { text: '바로편의점 수아', done: met.clerk }];
  const show = () => ctx.objective('이웃과 비상 연락 정하기', '세 사람을 만나 이야기합니다. 다 만나면 집으로 돌아옵니다.', steps());
  show();
  await ctx.roam({ on: {
    homeExitDoor: async () => { await ctx.goto('apartment', 'corridor3F'); },
    homeDoor: async () => { await ctx.goto('apartment', 'homeEntry'); if (Object.values(met).every(Boolean)) return 'done'; },
    elevator: async () => { await ctx.goto('apartment', ctx.engine.player.y > 1 ? 'lobby' : 'corridor3F'); },
    seoyeonDoor: async () => { if (met.seoyeon) return void ctx.toast('"내일 봬요."', 'neighbor'); await talkSeoyeon(ctx); met.seoyeon = true; show(); },
    managerWindow: async () => { if (met.manager) return void ctx.toast('박 소장은 전화 통화 중이다.'); await talkManager(ctx); met.manager = true; show(); },
    storeCounter: async () => { if (met.clerk) return void ctx.toast('"또 오세요."', 'neighbor'); await talkClerk(ctx); met.clerk = true; show(); },
    homePlanner: async () => {
      const pick = await ctx.ui.talk({ speaker: '이웃', role: '달력', lines: ['이 정도로 마칠까?'], choices: [{ id: 'yes', label: '마친다' }, { id: 'no', label: '더 만난다' }] });
      if (pick === 'yes') return 'done';
    },
  } });
  ctx.event('neighbors_done', { ...met });
}

async function talkSeoyeon(ctx) {
  const { G } = ctx;
  ctx.setLevelState('apartment', 'seoyeonDoor', 'open');
  await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['아, 302호 정민 씨죠? 오서연이에요.', '저는 보행 보조기를 써요. 이 카드에 제 상황을 적어 뒀어요.'] });
  await ctx.ui.card({ kind: 'info', title: '서연의 재난 대비 카드', meta: '서연이 직접 쓴 카드', body: [...SEOYEON_CARD, fact('people_card').text], actions: [{ id: 'ok', label: '읽었다', primary: true }] });
  const pick = await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['비상시에 서로 어떻게 연락할까요? 문자가 제일 편해요.', '혹시 대피소 출입구 상황을 알게 되면, 확인한 것과 못 한 것을 나눠서 보내 줄래요? 저한테 맞는 입구는 제가 다시 볼게요.'],
    choices: [{ id: 'sms', label: '문자로 연락해요' }, { id: 'both', label: '문자도 하고, 문에 쪽지도 남길게요' }] });
  G.rel.seoyeon.contact = pick === 'both' ? 'both' : 'sms';
  G.rel.seoyeon.trust += 1;
  if (!has(G, 'batteries_aa')) {
    const b = await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['건전지 있으세요? 저 두 팩 사 놨는데 한 팩 나눠 드릴까요?'], choices: [{ id: 'yes', label: '고마워요, 받을게요' }, { id: 'no', label: '괜찮아요' }] });
    if (b === 'yes') { G.items.batteries_aa = { home: 0, bag: 0, bought: 0, checked: false, used: 0, ...(G.items.batteries_aa || {}) }; G.items.batteries_aa.home += 1; ctx.event('gift', { from: 'seoyeon', item: 'batteries_aa' }); }
  }
  await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['내일 저녁엔 병원 진료가 있어서 좀 늦게 들어와요. 무슨 일 있으면 문자 주세요.'] });
  ctx.note('서연(303호): 문자로 연락. 출입구 상황은 확인한 것과 못 한 것을 나눠서.', 'neighbor', 'apartment');
  ctx.setLevelState('apartment', 'seoyeonDoor', 'closed');
}

async function talkManager(ctx) {
  const { G } = ctx;
  await ctx.ui.talk({ speaker: '박영수 소장', role: '관리사무소', lines: ['아이고, 302호. 오늘 민원이 밀려서 길게는 못 해요.', '정전되면 엘리베이터는 못 써요. 계단 비상등은 배터리로 켜지고요.', '우리 단지 대피소는 길 건너 생활문화센터 지하예요. 정문이 동쪽 문이고, 계단 A, 차 다니는 진출입로 B. C 경사로는 아직 공사 중이에요.', '소화기는 층마다 복도 소화전 옆에 있어요.'] });
  G.rel.manager.contact = 'office'; G.rel.manager.trust += 1;
  G.know.entrancesHeard = true;
  ctx.note('박 소장: 대피소 출입구는 동쪽 정문·A 계단·B 진출입로 (C 공사 중). 정전 시 엘리베이터 사용 불가.', 'neighbor', 'apartment');
}

async function talkClerk(ctx) {
  const { G } = ctx;
  await ctx.ui.talk({ speaker: '이수아', role: '바로편의점', lines: ['어서 오세요. 건전지요? 오늘 거의 다 나갔어요. 내일 오전에 더 들어온다고는 했는데, 확실하진 않아요.', '생수는 마트가 더 싸요. 1인 6병 한정이래요.', '정전되면 카드 단말기가 안 될 수도 있대요. 현금 조금 있으면 좋을 거예요. 저도 들은 얘기지만요.'] });
  await ctx.ui.talk({ speaker: '이수아', role: '바로편의점', lines: ['무슨 일 있으면 가게로 오세요. 근데 저도 언제까지 일할지는 몰라요. 본가가 지방이라서요.'] });
  G.rel.clerk.contact = 'store'; G.rel.clerk.trust += 1;
  ctx.note('수아: 건전지 내일 오전 입고 예정(확실하지 않음). 정전 시 현금만 받을 수도 있음.', 'neighbor', 'store');
}

export async function doBag(ctx) {
  const { G } = ctx;
  ctx.objective('비상가방 꾸리기', '현관의 배낭에 들고 나갈 물건을 담습니다.', []);
  await ctx.roam({ on: { homeBag: async () => 'go' } });
  const mode = ladderStage(G, 'bag');
  const items = Object.entries(G.items).filter(([id, v]) => CATALOG[id] && (v.home > 0 || v.bag > 0) && id !== 'cash').map(([id, v]) => ({ id, name: CATALOG[id].name, weightKg: CATALOG[id].weightKg, volumeL: CATALOG[id].volumeL, home: v.home, bag: v.bag, tip: fact(CATALOG[id].facts?.[0])?.text ?? '', tags: [CATALOG[id].category, CATALOG[id].risk].filter(Boolean) }));
  if (G.items.cash && (G.items.cash.home || G.items.cash.bag)) items.push({ id: 'cash', name: CATALOG.cash.name, weightKg: 0.01, volumeL: 0.01, home: G.items.cash.home, bag: G.items.cash.bag, tip: fact('bag_cash').text, tags: ['docs'] });
  const goals = ['물과 조리 없이 먹을 음식 (2~3일분)', '빛(손전등)과 정보(라디오)', '복용약과 구급품', '비상연락카드·서류 사본·현금', '보온(은박담요·여벌옷)'];
  const res = await ctx.modal(() => ctx.ui.bag({ mode, context: 'alert', limit: BAG_LIMIT, items, goals, tips: tips('bag_days', 'bag_nocook', 'bag_copies', 'bag_foil', 'bag_door') }));
  applyBag(G, res?.bag || {});
  const ev = evalBag(G, 'alert');
  recordEncounter(G, 'bag', ev.missing?.length ? 'partial' : 'complete');
  G.know.bagPacked = true;
  ctx.setLevelState('apartment', 'bag', 'packed');
  ctx.event('bag_packed', { weightKg: ev.weightKg, missing: ev.missing });
  ctx.toast(ev.missing?.length ? `배낭을 현관에 두었다. 빠진 것: ${ev.missing.join(', ')}` : '배낭을 현관에 두었다. 필요한 것을 고루 담았다.');
}

export function applyBag(G, target) {
  for (const [id, v] of Object.entries(G.items)) {
    const want = Math.max(0, Math.min((target[id] ?? 0), v.home + v.bag));
    if (want > v.bag) pack(G, id, want - v.bag); else if (want < v.bag) unpack(G, id, v.bag - want);
  }
}
