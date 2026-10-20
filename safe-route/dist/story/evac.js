// 4장 남을까 떠날까 (D+6) and 5장 공식 대피 (D+12). Both choices in 4장 are valid under different
// conditions; the fire in 4장 is the third encounter, where stepping back is the success.
import { recordEvent, adjust, hhmm, bagLoad } from '../game/state.js';
import { ladderStage, recordEncounter, evalBag, evalExtinguisher } from '../game/drills.js';
import { chapterSummary, endingRecord } from '../game/debrief.js';
import { CLOSURES } from '../game/townmap.js';
import { BAG_LIMIT, CATALOG } from '../game/items.js';
import { fact, SOURCES } from '../game/facts.js';
import { applyHome, applyBag } from './prep-actions.js';
import { radioScene, emergencyText, factCard, AREA } from './common.js';

const stock = (G, ids) => ids.reduce((s, id) => s + (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0), 0);

export async function crossroads(ctx) {
  const G = ctx.G;
  G.chapter = 'crossroads';
  ctx.hud.setChapter('4장', '남을까, 떠날까');
  ctx.power = false; ctx.outdoorEnv = 'day';
  if (ctx.resume !== 'fire4') {
    await ctx.skip('D+3 월', 'D+6 목 10:00', '하루 한두 시간씩 전기가 들어왔다 나갔다. 급수소 줄은 날마다 길어졌다.');
    G.day = 6; G.minute = 10 * 60;
    applyHome(ctx);
    await ctx.goto('apartment', 'home', 'interiorDark');
    ctx.refresh();
    const heard = await ctx.modal(() => radioScene(ctx, 'rumor'));
    await ctx.card({ kind: 'notice', title: '연무구 안내 (가상)', meta: 'D+6 · 행정복지센터 게시판·라디오',
      body: ['희망하는 주민은 생활문화센터 임시 거처로 옮길 수 있습니다. (권고, 명령 아님)', '임시 거처: 급수·급식 지원, 공용 공간, 개인 공간 협소', '집에 머무는 주민도 비상급수시설을 계속 이용할 수 있습니다.', heard ? '(라디오에서 같은 내용을 들었다.)' : '(서연이 문 앞에 붙여 둔 메모에서 같은 내용을 읽었다.)'],
      actions: [{ id: 'ok', label: '지금 상황 보기', primary: true }] });
    const water = stock(G, ['water_2l', 'water_500']), food = stock(G, ['rice_instant', 'canned', 'bars', 'ramen']);
    G.know.stayMove = await ctx.card({ kind: 'info', title: '지금 내 상황', meta: '판단 재료',
      body: [`마실 물 ${water}병 · 먹을 것 ${food}개`, `휴대폰 ${G.phone.battery}% · 몸 상태: 피로 ${G.stats.fatigue} · 긴장 ${G.stats.tension}`,
        `박 소장: "저는 남아서 건물 봐야죠." · 서연: "저는 주민센터에 이동 지원을 신청해 뒀어요."`, G.know.clerkLeft ? '바로편의점 문에 쪽지: "본가에 다녀옵니다 — 이수아"' : '바로편의점은 문을 닫았다.',
        '어느 쪽도 정답이 정해져 있지 않습니다. 지금의 조건으로 고르세요.'],
      actions: [{ id: 'stay', label: '집에 남는다' }, { id: 'move', label: '생활문화센터 임시 거처로 옮긴다' }] });
    recordEvent(G, 'decision', { stayMove: G.know.stayMove, water, food });
    ctx.checkpoint('fire4');
  }
  if (G.know.stayMove === 'move') await moveAndFire(ctx); else await stayAndSmoke(ctx);
  const sum = chapterSummary(G, 'crossroads');
  await ctx.modal(() => ctx.ui.chapterCard({ chapter: '4장', title: '남을까, 떠날까', lines: sum.lines, payoffs: sum.payoffs }));
}

// Stay: smoke from another flat. The fact-based answer is to judge, not to run or to fight.
async function stayAndSmoke(ctx) {
  const G = ctx.G;
  G.day = 7; G.minute = 22 * 60 + 10; ctx.outdoorEnv = 'blackout';
  ctx.setLevelState('apartment', 'smoke', 0.35);
  await ctx.goto('apartment', 'home', 'interiorDark');
  ctx.refresh();
  await ctx.caption('밤 10시. 위층 어딘가에서 화재경보가 울리고, 복도 쪽에서 연기 냄새가 난다.', null, 3500);
  let felt = false;
  for (;;) {
    const pick = await ctx.say('판단', ['우리 집 안으로 연기나 불길이 들어오지는 않았다. 어떻게 할까?'], [
      { id: 'handle', label: '현관 문손잡이를 손등으로 만져 본다', disabled: felt },
      { id: 'fight', label: '복도로 나가 소화기로 끄러 간다' },
      { id: 'run', label: '바로 복도로 뛰어나가 계단으로 내려간다' },
      { id: 'stay', label: '창문을 닫고 119에 신고한 뒤 안내방송에 따른다' }], '22:10');
    recordEvent(G, 'smoke_choice', { pick });
    if (pick === 'handle') { felt = true; await ctx.say('현관 문손잡이', ['뜨겁다. 문 너머 복도에 연기가 차 있는 것 같다.', fact('fire_door').text], null, '손등으로'); continue; }
    if (pick === 'fight') { recordEncounter(G, 'extinguisher', 'escalated'); ctx.fail('fire'); }
    if (pick === 'run') {
      adjust(G, 'health', -15); recordEncounter(G, 'extinguisher', 'risky');
      await factCard(ctx, '연기 속으로 나가기 전에', ['fire_judge', 'fire_door', 'fire_stay'], '되돌아보기');
      break;
    }
    recordEncounter(G, 'extinguisher', 'retreat-ok');
    await factCard(ctx, '상황을 보고 판단했습니다', ['fire_stay', 'fire_judge', 'fire_119']);
    ctx.payoff('judgement', '불을 끄러 가지도, 연기 속으로 뛰어들지도 않고 상황을 보고 기다렸다');
    break;
  }
  ctx.setLevelState('apartment', 'smoke', 0);
  await ctx.say('박영수 소장', ['5층 세대 주방에서 난 불이었대요. 소방대가 껐고 다친 사람은 없대요.'], null, '방송');
}

// Move: temporary stay in the shelter; someone cooks with a burner and the boxes catch.
async function moveAndFire(ctx) {
  const G = ctx.G;
  G.day = 7; G.minute = 19 * 60 + 30;
  ctx.setLevelState('shelter', 'crowd', 'settled');
  ctx.setLevelState('shelter', 'phase', 'calm');
  ctx.setLevelState('shelter', 'power', 'emergency');
  ctx.setLevelState('shelter', 'fire', 'large');
  await ctx.goto('shelter', 'spot');
  ctx.refresh();
  await ctx.caption('저녁. 주차장 구석에서 누군가 휴대용 버너로 라면을 끓이다 쌓아 둔 상자에 불이 옮겨 붙었다. 불길이 천장 배관에 닿는다.', null, 4200);
  await factCard(ctx, '대피소에서는', ['shelter_rules'], '공식 행동요령');
  const res = await ctx.modal(() => ctx.ui.extinguisher({ mode: ladderStage(G, 'extinguisher'), fire: 'large' }));
  const ev = evalExtinguisher(res?.sequence || [], { fire: 'large' });
  recordEncounter(G, 'extinguisher', ev.result);
  ctx.setLevelState('shelter', 'fire', 'none');
  if (ev.result === 'escalated') ctx.fail('fire');
  await factCard(ctx, '물러서는 것도 성공입니다', ['fire_big', 'fire_shout', 'fire_119']);
  ctx.payoff('judgement', '천장까지 닿은 불 앞에서 물러나 알리고 대피했다');
}

export async function evacuation(ctx) {
  const G = ctx.G;
  G.chapter = 'evac';
  ctx.hud.setChapter('5장', '공식 대피');
  ctx.outdoorEnv = 'day';
  if (ctx.resume !== 'depart') {
    await ctx.skip('D+7', 'D+12 화', G.know.stayMove === 'move' ? '닷새가 더 지났다. 임시 거처에서 지내다 오늘 아침 짐을 챙기러 집에 들렀다.' : '닷새가 더 지났다. 이웃 몇 집은 이미 떠났다.');
    G.day = 12; G.minute = G.cond.evacNoticeAt;
    applyHome(ctx);
    await ctx.goto('apartment', 'home', 'interiorDark');
    ctx.refresh();
    await emergencyText(ctx, { kind: '안전안내문자', time: hhmm(G.minute), text: `[안전안내문자] 연무동 주민 이동 지원. 오늘 14:00까지 행정복지센터 앞 임시 집결지로 이동. 1인 1가방, 신분증 지참 ${AREA}` });
    await factCard(ctx, '대피 안내를 받았을 때', ['evac_order', 'evac_kit', 'evac_note']);
    const heard = await ctx.modal(() => radioScene(ctx, 'evac'));
    G.know.evacClosuresKnown = !!heard;
    // One bag, third encounter: no tips unless the ladder says so.
    const items = Object.entries(G.items).filter(([id, v]) => CATALOG[id] && v.home + v.bag > 0).map(([id, v]) => ({ id, name: CATALOG[id].name, weightKg: CATALOG[id].weightKg, volumeL: CATALOG[id].volumeL, home: v.home, bag: v.bag, tip: '', tags: [CATALOG[id].category, CATALOG[id].risk].filter(Boolean) }));
    const mode = ladderStage(G, 'bag');
    const res = await ctx.modal(() => ctx.ui.bag({ mode, context: 'evac', limit: BAG_LIMIT, items, goals: ['물', '조리 없이 먹을 음식', '빛', '정보', '약', '연락처 카드', '서류 사본', '현금', '보온', '위생'], tips: [] }));
    applyBag(G, res?.bag || {});
    const ev = evalBag(G, 'evac');
    recordEncounter(G, 'bag', ev.missing?.length ? 'partial' : 'complete');
    G.know.bagOn = true;
    if (G.know.bagPacked && !ev.missing?.length) ctx.payoff('bag', '미리 꾸려 본 경험으로 한 가방을 빠르게 챙겼다');
    G.minute = Math.max(G.minute, 13 * 60 + 15);
    ctx.checkpoint('depart');
  }
  await depart(ctx);
}

async function depart(ctx) {
  const G = ctx.G;
  const closures = G.cond.closures.evac;
  ctx.setLevelState('town', 'closures', closures);
  ctx.setLevelState('town', 'crowd', 'assembly');
  ctx.setLevelState('town', 'buses', 'on');
  ctx.setLevelState('town', 'waterPoint', 'off');
  ctx.setLevelState('town', 'notices', 'evac');
  await ctx.goto('apartment', 'home', 'interiorDark');
  ctx.refresh();
  let noted = false, said = false;
  ctx.objective('임시 집결지로', `14:00까지 행정복지센터 앞 임시 집결지에 도착합니다. ${G.know.evacClosuresKnown ? `오늘 통제: ${closures.map(c => CLOSURES[c].label).join(', ')}` : '어디가 통제인지는 아직 모릅니다.'}`, [{ text: '문에 쪽지 남기기 (선택)', done: false }, { text: '집결지 도착', done: false }]);
  await ctx.roam({ rate: 4, deadline: { day: 12, minute: 14 * 60, type: 'missedBus' }, on: {
    homeExitDoor: async () => {
      if (!noted) {
        const pick = await ctx.ui.talk({ speaker: '현관', role: '나가기 전에', lines: ['문에 쪽지를 남길까?'], choices: [{ id: 'note', label: `"${hhmm(G.minute)} 행정복지센터 집결지로 떠납니다 — 302호 정민"` }, { id: 'no', label: '그냥 나간다' }] });
        noted = true;
        if (pick === 'note') { recordEvent(G, 'note_left', {}); ctx.toast(fact('evac_note').text, 'official', 5000); }
      }
      await ctx.goto('apartment', 'corridor3F');
    },
    homeDoor: async () => { await ctx.goto('apartment', 'homeEntry'); },
    seoyeonDoor: async () => {
      if (said) return;
      said = true;
      const pick = await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['저는 지원 차량이 3시에 와요. 집결지에서 다시 만나요.', '같이 기다려 줄 필요는 없어요. 정민 씨는 버스 시간 맞춰 가세요.'], choices: [{ id: 'go', label: '집결지에서 만나요' }, { id: 'card', label: '제 연락처 카드 사진 찍어 두세요' }] });
      if (pick === 'card') recordEvent(G, 'help', { who: 'seoyeon', what: '떠나기 전 서로의 연락처를 다시 확인했다' });
    },
    managerWindow: async () => { await ctx.ui.talk({ speaker: '박영수 소장', role: '관리사무소', lines: ['저는 남아요. 건물 지키는 사람도 있어야죠. 조심히 가요, 302호.'] }); recordEvent(G, 'help', { who: 'manager', what: '마지막까지 건물을 지키며 길을 안내했다' }); },
    assembly: async () => 'arrived',
    ...Object.fromEntries(Object.keys(CLOSURES).map(id => [`closure_${id}`, async () => { await ctx.ui.talk({ speaker: '안내 요원', role: CLOSURES[id].label, lines: ['이 구간은 통제 중입니다. 다른 길로 돌아가 주세요.'] }); }])),
  } });
  recordEvent(G, 'arrived_assembly', { minute: G.minute });
  if (G.know.routeChecked) ctx.payoff('route', '동네 길을 미리 알아 둔 덕분에 통제 구간을 돌아 집결지에 닿았다');
  const card = (G.items.contact_card?.bag ?? 0) + (G.items.doc_copies?.bag ?? 0) > 0;
  await ctx.say('집결지 접수', ['이름, 연락처, 복용약을 적어 주세요. 신분증 있으세요?', card ? '(가방 앞주머니의 연락처 카드와 서류 사본을 보여 주었다.)' : '(기억나는 대로 적었다. 신분증 원본만 있었다.)'], null, '임시 집결지');
  if (card) ctx.payoff('docs', '연락처 카드와 서류 사본으로 접수를 바로 마쳤다');
  await ctx.caption('버스 창밖으로 연무동이 멀어진다.', null, 3500);
}

export async function ending(ctx) {
  const G = ctx.G;
  G.chapter = 'ending';
  recordEvent(G, 'ending', { load: bagLoad(G).weightKg });
  return ctx.modal(() => ctx.ui.ending(endingRecord(G), SOURCES));
}
