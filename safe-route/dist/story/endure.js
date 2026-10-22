// 3장 버티기 · D+1 ~ D+3. Blackout and water cut. Food safety, the water point, a rumour,
// a small fire in the corridor. Helping costs rest; refusing costs nothing but the help.
import { recordEvent, consume, use, usable, adjust } from '../game/state.js';
import { ladderStage, recordEncounter, evalExtinguisher } from '../game/drills.js';
import { chapterSummary } from '../game/debrief.js';
import { STORE_ITEMS, CATALOG } from '../game/items.js';
import { applyHome, goOut } from './prep-actions.js';
import { radioScene, factCard } from './common.js';

const waterId = (G) => ['water_500', 'water_2l'].find(id => (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0) > 0);

function drink(ctx) {
  const G = ctx.G, id = waterId(G);
  if (!id) { ctx.toast('마실 물이 없다.', 'self'); return false; }
  consume(G, id, 1, G.items[id].home > 0 ? 'home' : 'bag');
  adjust(G, 'thirst', id === 'water_2l' ? -45 : -20);
  ctx.refresh();
  return true;
}

// The body giving out is a soft failure: a neighbour finds 정민, and the day starts over.
const bodyCheck = (ctx) => () => { const s = ctx.G.stats; if (s.thirst >= 95 || s.health <= 8) ctx.fail('exhausted'); };
const onThird = (ctx) => ctx.engine.level?.id === 'apartment' && ctx.engine.player.y > 4;

export async function endure(ctx) {
  const G = ctx.G;
  G.chapter = 'endure';
  ctx.hud.setChapter('3장', '버티기');
  ctx.power = false; ctx.outdoorEnv = 'day'; ctx.indoorEnv = 'interior';
  ctx.setLevelState('apartment', 'power', 'off');
  ctx.setLevelState('store', 'power', 'off');
  ctx.setLevelState('store', 'clerk', 'out'); // 이수아 left town the morning after the alert
  if (ctx.resume !== 'fire') {
    await ctx.skip('D-day 금 밤', 'D+1 토 07:00', '전기는 밤새 돌아오지 않았다. 아침에는 수도꼭지에서도 물이 나오지 않았다.');
    G.day = 1; G.minute = 7 * 60;
    ctx.setLevelState('apartment', 'windowView', 'day');
    applyHome(ctx);
    await ctx.goto('apartment', 'home', 'interiorDark');
    ctx.refresh();
    await morning(ctx);
    await waterTrip(ctx);
    await evening(ctx);
    ctx.checkpoint('fire');
  }
  await corridorFire(ctx);
  await dayThree(ctx);
  const sum = chapterSummary(G, 'endure');
  await ctx.modal(() => ctx.ui.chapterCard({ chapter: '3장', title: '버티기', lines: sum.lines, payoffs: sum.payoffs }));
}

async function morning(ctx) {
  const G = ctx.G;
  const done = { fridge: false, drink: false, info: false };
  const show = () => ctx.objective('정전 이틀째 아침', '냉장고를 정리하고, 물을 마시고, 오늘의 안내를 확인합니다. 다 했으면 달력에서 하루를 시작합니다.', [
    { text: '냉장고 정리', done: done.fridge }, { text: '물 마시기 (베란다 수납장)', done: done.drink }, { text: '안내 확인 (라디오·이웃·1층 게시판)', done: done.info }]);
  show();
  await ctx.roam({ tick: bodyCheck(ctx), on: {
    homeFridge: async () => {
      if (done.fridge) return void ctx.toast('냉장고는 정리했다.');
      await factCard(ctx, '정전 12시간째 냉장고', ['food_fridge', 'food_4h', 'food_door']);
      for (const id of ['fridge_food', 'cooked_meat']) {
        const n = G.items[id]?.home ?? 0;
        if (!n) continue;
        const pick = await ctx.ui.talk({ speaker: CATALOG[id].name, role: `냉장고 · ${n}${CATALOG[id].unit}`,
          lines: [id === 'cooked_meat' ? '익힌 고기라 괜찮지 않을까? 냄새는 아직 잘 모르겠다.' : '반찬 통이 미지근하다.'],
          choices: [{ id: 'eat', label: '먹는다' }, { id: 'toss', label: '버린다' }] });
        if (pick === 'eat') {
          consume(G, id, 1); adjust(G, 'health', -12); adjust(G, 'tension', 8);
          recordEvent(G, 'ate_spoiled', { id });
          ctx.toast('오후 내내 배가 아팠다.', 'self');
          if (id === 'cooked_meat') await factCard(ctx, '익혀도 마찬가지입니다', ['myth_meat', 'food_doubt'], '오해 바로잡기');
        } else { consume(G, id, n); recordEvent(G, 'tossed_food', { id }); }
      }
      done.fridge = true; show();
    },
    homeStock: async () => {
      if (done.drink) return void ctx.toast('아까 마셨다. 아껴 두자.');
      if (!drink(ctx)) return;
      done.drink = true; show();
      if (G.know.tubFilled) ctx.payoff('tub', '받아 둔 물로 세수하고 변기 물을 내렸다');
    },
    homeRadio: async () => {
      if (await radioScene(ctx, 'water')) { G.know.waterPoint = true; done.info = true; show(); }
    },
    homeExitDoor: async () => { await ctx.goto('apartment', 'corridor3F'); },
    homeDoor: async () => { await ctx.goto('apartment', 'homeEntry'); },
    seoyeonDoor: async () => {
      if (ctx.seoyeonAsk) return void ctx.toast('"이따 봬요."', 'neighbor');
      if (G.rel.seoyeon.trust < 1) return void ctx.ui.talk({ speaker: '303호', role: '문 너머', lines: ['"누구세요? …아, 302호요. 지금은 좀 바빠서요."'] });
      const pick = await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['아침 라디오에서 들었어요. 행정복지센터 마당에서 비상급수를 한대요. 9시 반부터요.', '저는 계단이 어려워서… 제 것도 한 통 부탁드려도 될까요? 물통은 제 걸 빌려 드릴게요. 대신 충전해 둔 보조배터리 하나 드릴게요.'],
        choices: [{ id: 'yes', label: '같이 받아 올게요' }, { id: 'no', label: '오늘은 제 것만 겨우 들 것 같아요' }] });
      ctx.seoyeonAsk = pick;
      G.know.waterPoint = true; done.info = true;
      ctx.note('서연: 행정복지센터 마당 비상급수 09:30부터', 'neighbor');
      if (pick === 'no') await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['괜찮아요. 소장님께 여쭤볼게요.'] });
      show();
    },
    lobbyNotice: async () => {
      await ctx.ui.card({ kind: 'notice', title: '관리사무소 안내', meta: 'D+1 07:30 · 박영수 소장', body: ['단수가 계속되고 있습니다.', '행정복지센터 마당 비상급수시설 운영: 09:30 ~ 17:00 (물통 지참)', '엘리베이터 운행 중지. 계단에서는 손전등을 쓰세요.'], actions: [{ id: 'ok', label: '확인', primary: true }] });
      G.know.waterPoint = true; done.info = true; ctx.note('관리사무소: 비상급수 09:30~17:00, 물통 지참', 'official'); show();
    },
    homePlanner: async () => (done.info ? 'done' : void ctx.toast('먼저 오늘의 안내를 확인하자.')),
  } });
  G.minute = Math.max(G.minute, 9 * 60 + 20);
  ctx.refresh();
}

async function waterTrip(ctx) {
  const G = ctx.G;
  const jugs = (G.items.water_jug?.home ?? 0) + (G.items.water_jug?.bag ?? 0);
  const forSeoyeon = ctx.seoyeonAsk === 'yes';
  ctx.setLevelState('town', 'traffic', 'none');
  ctx.setLevelState('town', 'crowd', 'queue');
  ctx.setLevelState('town', 'waterPoint', 'on');
  ctx.setLevelState('town', 'closures', []);
  ctx.outdoorEnv = 'day';
  await goOut(ctx, '비상급수시설로', '행정복지센터 마당의 비상급수시설로 갑니다.');
  ctx.objective('물 받기', '행정복지센터 마당 급수대에서 줄을 섭니다.', []);
  await ctx.roam({ rate: 2, tick: bodyCheck(ctx), on: { water: async () => 'queue' } });
  await factCard(ctx, '비상급수시설', ['water_point', 'water_sphere', 'water_hands']);
  ctx.pass(25);
  let carry;
  if (jugs) { use(G, 'water_jug'); carry = 10; ctx.payoff('water_jug', '접이식 물통으로 급수소 물을 받아 왔다'); }
  else { carry = 4; ctx.toast('물통이 없어 빈 생수병 두 개에 4리터만 받았다.', 'site'); }
  if (forSeoyeon) carry += 10;
  recordEvent(G, 'water_fetched', { litres: carry, forSeoyeon });
  ctx.speedOverride = carry > 12 ? 0.62 : 0.78;
  ctx.engine.player.carrying = 'water';
  adjust(G, 'fatigue', carry > 12 ? 14 : 8);
  ctx.refresh();
  ctx.objective('집으로', `물 ${carry}리터를 들고 103동 3층까지 돌아갑니다. 무겁다.`, []);
  await ctx.roam({ rate: 2, tick: () => { bodyCheck(ctx)(); return onThird(ctx) ? 'up' : undefined; }, on: {
    elevator: async () => { ctx.toast('엘리베이터가 멈춰 있다. 계단으로.'); },
  } });
  ctx.speedOverride = null;
  ctx.engine.player.carrying = null;
  ctx.refresh();
  (G.items.water_2l ??= { home: 0, bag: 0, bought: 0, checked: false, used: 0 }).home += forSeoyeon ? Math.floor((carry - 10) / 2) : Math.floor(carry / 2);
  if (forSeoyeon) {
    ctx.objective('303호에 물 전하기', '서연의 문을 두드립니다.', []);
    await ctx.roam({ on: { seoyeonDoor: async () => 'give' } });
    await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['정말 고마워요. 덕분에 오늘은 버틸 수 있겠어요.', '약속한 보조배터리예요. 저는 하나 더 있어요.'] });
    G.items.powerbank = { home: 0, bag: 0, bought: 0, checked: false, used: 0, ...(G.items.powerbank || {}) };
    G.items.powerbank.home += 1; G.items.powerbank.checked = true;
    G.rel.seoyeon.trust += 2;
    recordEvent(G, 'help', { who: 'seoyeon', what: '급수소에서 물 한 통을 받아 3층까지 날라 주었다' });
    recordEvent(G, 'help', { who: 'seoyeon', what: '서연이 충전된 보조배터리를 나눠 주었다' });
  }
  await ctx.goto('apartment', 'home');
  G.minute = Math.max(G.minute, 13 * 60);
  ctx.refresh();
}

async function evening(ctx) {
  const G = ctx.G;
  G.minute = 17 * 60 + 30;
  ctx.outdoorEnv = 'dusk';
  ctx.setLevelState('town', 'waterPoint', 'off'); // closed at 17:00: tank, table and E all go
  ctx.setLevelState('apartment', 'windowView', 'dusk');
  ctx.refresh();
  let visited = false;
  ctx.objective('저녁 · 할 수 있는 일', '편의점에 들러 볼 수도 있고, 집에서 쉴 수도 있습니다. 침대에서 하루를 마칩니다.', [{ text: '바로편의점 (선택)', done: false }, { text: '침대에서 쉬기', done: false }]);
  await ctx.roam({ tick: bodyCheck(ctx), on: {
    homeExitDoor: async () => { await ctx.goto('apartment', 'corridor3F'); },
    homeDoor: async () => { await ctx.goto('apartment', 'homeEntry'); },
    homeStock: async () => { drink(ctx); },
    storeCounter: async () => {
      if (visited) return void ctx.toast('계산대에는 아무도 없다.');
      visited = true;
      await storeEvening(ctx);
      ctx.objective('저녁 · 할 수 있는 일', '집으로 돌아가 침대에서 하루를 마칩니다.', [{ text: '바로편의점 (선택)', done: true }, { text: '침대에서 쉬기', done: false }]);
    },
    homeBed: async () => 'sleep',
  } });
  adjust(G, 'fatigue', -30); adjust(G, 'tension', -8);
  G.day = 2; G.minute = 21 * 60;
}

// Nobody is behind the counter: 이수아 has gone to her family and left a note and a cash box.
async function storeEvening(ctx) {
  const G = ctx.G;
  await ctx.ui.card({ kind: 'notice', title: '계산대 위 쪽지', meta: '바로편의점 · 비상등만 켜져 있다',
    body: ['정전으로 영업을 멈춥니다. 본가에 다녀올게요. — 이수아', '급하신 분은 남은 건전지·생수를 가져가시고, 현금은 계산대 옆 통에 넣어 주세요. 카드 단말기는 안 됩니다.'],
    actions: [{ id: 'ok', label: '둘러본다', primary: true }] });
  const cash = (G.items.cash?.home ?? 0) + (G.items.cash?.bag ?? 0);
  if (cash > 0) {
    const pick = await ctx.ui.talk({ speaker: '계산대 옆 현금 통', role: '현금만', lines: ['선반에 건전지 한 팩과 작은 생수 몇 병이 남아 있다.'], choices: [{ id: 'buy', label: '건전지 한 팩과 생수 두 병을 챙기고 현금을 넣는다' }, { id: 'no', label: '그냥 둔다' }] });
    if (pick === 'buy') {
      consume(G, 'cash', 1, G.items.cash.home > 0 ? 'home' : 'bag');
      const cart = { batteries_aa: 1, water_500: 2 };
      for (const [id, n] of Object.entries(cart)) { const e = (G.items[id] ??= { home: 0, bag: 0, bought: 0, checked: false, used: 0 }); e.home += n; e.bought += n; }
      recordEvent(G, 'buy', { cart, total: STORE_ITEMS.batteries_aa + STORE_ITEMS.water_500 * 2, paid: 'cash' });
      ctx.payoff('cash', '카드가 안 되는 편의점에서 현금으로 건전지와 물을 샀다');
    }
  } else await ctx.ui.talk({ speaker: '정민', role: '지갑', lines: ['현금이 없다. 카드뿐이다. 카드로는 아무것도 살 수 없다.'] });
  const pick = await ctx.ui.talk({ speaker: '102동 주민', role: '바로편의점 앞', lines: ['여기 주인도 떠났나 봐요. 참, 그 얘기 들었어요? 생활문화센터 대피소가 폐쇄됐대요. SNS에 돌던데… 저도 확실한 건 모르겠어요.'],
    choices: [{ id: 'spread', label: '큰일이네요. 다른 이웃들한테도 알려야겠어요' }, { id: 'check', label: '어디서 나온 말인지 먼저 확인해 볼게요' }] });
  recordEvent(G, pick === 'check' ? 'rumor_checked' : 'rumor_spread', {});
  ctx.note('SNS: 생활문화센터 대피소 폐쇄? (출처 불명)', 'rumor', 'store');
  if (pick === 'check') {
    const heard = usable(G, 'radio') ? await radioScene(ctx, 'rumor') : null;
    await ctx.ui.talk({ speaker: heard ? '재난방송' : '행정복지센터 게시판(주민의 휴대폰 사진)', role: '공식 안내', lines: ['생활문화센터 대피소는 정상 운영 중입니다. 확인되지 않은 정보에 주의해 주십시오. (가상 안내)'] });
    ctx.note('생활문화센터 대피소 정상 운영 (공식 안내로 확인)', 'official');
  }
  await factCard(ctx, '소문을 만났을 때', ['info_rumor', 'shelter_rumor', 'info_harm']);
  recordEvent(G, 'help', { who: 'clerk', what: '가게를 닫으며 쪽지와 현금 통을 남겨 이웃이 필요한 물건을 가져가게 했다' });
  G.know.clerkLeft = true;
}

// D+2 night: a candle left burning by 304 tipped onto parcel boxes. Encounter 2 for the extinguisher.
async function corridorFire(ctx) {
  const G = ctx.G;
  G.day = 2; G.minute = 21 * 60 + 40;
  ctx.outdoorEnv = 'blackout';
  ctx.setLevelState('apartment', 'windowView', 'blackout');
  ctx.setLevelState('apartment', 'hallFire', 'small');
  ctx.setLevelState('apartment', 'smoke', 0.25);
  await ctx.goto('apartment', 'home', 'interiorDark');
  ctx.refresh();
  ctx.audio.fire(0.4);
  await ctx.caption('복도에서 타는 냄새가 난다. 문틈으로 주황빛이 흔들린다.', null, 3200);
  let size = 'small', t = 0, shouted = false, resolved = null;
  const grow = () => { if (size === 'small' && !resolved) { size = 'large'; ctx.setLevelState('apartment', 'hallFire', 'large'); ctx.setLevelState('apartment', 'smoke', 0.7); ctx.audio.fire(0.9); ctx.toast('불이 상자 더미로 번졌다. 천장까지 닿을 것 같다.', 'site'); } };
  ctx.objective('복도의 불', '판단하세요. 작은 불이면 소화기로, 커졌다면 알리고 대피합니다.', []);
  const fight = async () => {
    const mode = ladderStage(G, 'extinguisher');
    const fire = size; // judged on the fire the player saw when they chose
    const res = await ctx.ui.extinguisher({ mode, fire });
    const ev = evalExtinguisher(res?.sequence || [], { fire });
    recordEncounter(G, 'extinguisher', ev.result);
    if (ev.result === 'out') {
      resolved = 'out'; ctx.setLevelState('apartment', 'hallFire', 'out'); ctx.setLevelState('apartment', 'smoke', 0.1); ctx.audio.fire(0);
      if (G.know.extinguisherLocated) ctx.payoff('extinguisher', '소화기 위치와 순서를 알고 있어 작은 불을 바로 껐다');
      return 'out';
    }
    if (ev.result === 'retreat-ok') return retreat();
    if (ev.result === 'escalated') { ctx.audio.fire(0); ctx.fail('fire'); }
    grow();
  };
  const retreat = async () => {
    resolved = 'retreat';
    await factCard(ctx, '대피하며 알리기', ['fire_shout', 'fire_119', 'fire_lift', 'fire_big']);
    ctx.audio.fire(0);
    ctx.setLevelState('apartment', 'hallFire', 'out');
    recordEvent(G, 'fire_retreat', { size });
    await ctx.ui.talk({ speaker: '박영수 소장', role: '1층', lines: ['다들 내려왔죠? 119에 신고했어요. 소방대가 금방 껐대요. 304호 어르신도 무사하시고요.'] });
    return 'retreat';
  };
  // The fire spreads only while the player is free to act; dialogs and the drill have their own pace.
  await ctx.roam({ rate: 0, tick: (dt) => { if (!resolved && ctx.engine.controlsEnabled && (t += dt) > 28) grow(); }, on: {
    homeExitDoor: async () => { await ctx.goto('apartment', 'corridor3F'); },
    hallFire: async () => {
      const pick = await ctx.ui.talk({ speaker: '복도', role: size === 'small' ? '택배 상자에 붙은 작은 불' : '천장까지 닿는 불길', lines: [size === 'small' ? '304호 앞 택배 상자 위로 촛불이 넘어져 있다. 불길은 아직 작다.' : '불길이 천장에 닿았다. 연기가 복도를 채운다.'],
        choices: [{ id: 'shout', label: '"불이야!" 외친다', disabled: shouted }, { id: 'water', label: '물을 끼얹는다' }, { id: 'retreat', label: '대피하며 알린다 (계단 · 119)' }] });
      if (pick === 'shout') { shouted = true; recordEvent(G, 'fire_shout', {}); ctx.toast('문이 열리고 이웃들이 나온다. 304호 어르신이 1층에서 올라오고 있다.', 'site'); return; }
      // Once the fire reaches the ceiling the candle advice (extinguisher, blanket) no longer applies.
      if (pick === 'water') { if (size === 'small') await factCard(ctx, '촛불 화재에 물은 위험할 수 있습니다', ['fire_candle'], '공식 안내'); else await factCard(ctx, '불길이 이미 커졌습니다', ['fire_big', 'fire_shout'], '공식 안내'); grow(); return; }
      return retreat();
    },
    extinguisher: async () => fight(),
  } });
  ctx.audio.fire(0);
  adjust(G, 'tension', 10);
}

async function dayThree(ctx) {
  const G = ctx.G;
  G.day = 3; G.minute = 10 * 60;
  ctx.outdoorEnv = 'day';
  ctx.setLevelState('apartment', 'windowView', 'day');
  ctx.objective(null);
  ctx.setLevelState('apartment', 'smoke', 0);
  await ctx.goto('apartment', 'home', 'interiorDark');
  ctx.refresh();
  if (G.stats.tension > 55) await factCard(ctx, '긴장이 높을 때', ['mind_breath', 'mind_call'], '마음 돌보기');
  const pick = await ctx.say('오서연', ['어젯밤 괜찮으셨어요? 저 라디오 들은 거 메모해 뒀어요. 전기 복구 시점은 아직 정해지지 않았대요.', '앞으로 저녁마다 들은 걸 정리해서 문 앞에 붙여 둘게요. 필요하면 보세요.'],
    [{ id: 'thanks', label: '고마워요. 저는 급수소 소식을 전해 드릴게요' }, { id: 'ok', label: '네, 볼게요' }], '303호 앞');
  if (pick === 'thanks') { G.rel.seoyeon.trust += 1; recordEvent(G, 'help', { who: 'seoyeon', what: '라디오 메모와 급수소 소식을 서로 나누기로 했다' }); }
  ctx.note('서연 메모: 전기 복구 시점 미정 (라디오)', 'neighbor');
  adjust(G, 'fatigue', -20); adjust(G, 'tension', -10);
  ctx.refresh();
}
