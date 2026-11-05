// 4장 남을까 떠날까 (D+6) and 5장 공식 대피 (D+12). Both choices in 4장 are valid under different
// conditions; the fire in 4장 is the third encounter, where stepping back is the success.
import { recordEvent, adjust, hhmm, bagLoad, inPocket } from '../game/state.js';
import { ladderStage, recordEncounter, evalBag, evalExtinguisher, BAG_GOALS } from '../game/drills.js';
import { chapterSummary, endingRecord } from '../game/debrief.js';
import { CLOSURES } from '../game/townmap.js';
import { BAG_LIMIT } from '../game/items.js';
import { fact, SOURCES } from '../game/facts.js';
import { waterLitres } from '../game/endings.js';
import { posts, verify, misled, lostByRumor, rumor } from '../game/rumors.js';
import { applyHome, applyBag, bagItems, goHome } from './prep-actions.js';
import { radioScene, emergencyText, factCard, dayLedger, stockLook, AREA } from './common.js';
import { hearClaim, boardWalk } from './rumor-scene.js';
import { bagInfo, applyPocket } from '../ui/bag.js';
import { ripples } from '../game/ripples.js';
import { rippleMontage } from './cutscenes.js';

const stock = (G, ids) => ids.reduce((s, id) => s + (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0), 0);

export async function crossroads(ctx) {
  const G = ctx.G;
  G.chapter = 'crossroads';
  ctx.hud.setChapter('4장', '남을까, 떠날까');
  ctx.power = false; ctx.outdoorEnv = 'day';
  if (ctx.resume !== 'fire4') {
    const days = Math.max(1, 6 - G.day);
    const ledger = await ctx.modal(() => dayLedger(ctx, days, 'home'));
    await ctx.skip('D+3 월', 'D+6 목 10:00', ['하루 한두 시간씩 전기가 들어왔다 나갔다. 급수소 줄은 날마다 길어졌다.', ...ledger.slice(0, 4)]);
    G.day = 6; G.minute = 10 * 60;
    applyHome(ctx);
    await ctx.goto('apartment', 'home', 'interiorDark');
    ctx.refresh();
    const how = await campFood(ctx);
    if (how === 'radio') await noticeCard(ctx, how);
    else if (!how) {
      const memo = posts(G, 'camp_food').find(p => p.key === 'seoyeon');
      await ctx.say(memo.from, ['현관문 틈에 서연이 끼워 둔 메모가 있다.', memo.text, '희망하는 주민은 생활문화센터 임시 거처로 옮길 수 있다고 한다. 권고이고, 명령은 아니라고 했다.'], null, memo.meta);
    }
    const water = waterLitres(G), food = stock(G, ['rice_instant', 'canned', 'bars', 'ramen']);
    const heardLine = misled(G) === 'camp_food' ? '마음에 걸리는 말: "임시 거처 급식이 끊겼다" (SNS 퍼온 글 · 확인하지 않음)'
      : G.events.some(e => e.type === 'rumor_checked' && e.details.id === 'camp_food') ? '확인한 것: 임시 거처는 급수·급식을 지원한다 (공식 안내)' : null;
    G.know.stayMove = await ctx.card({ kind: 'info', title: '지금 내 상황', meta: '판단 재료',
      body: [`마실 물 약 ${water}L · 먹을 것 ${food}개`, `휴대폰 ${G.phone.battery}% · 몸 상태: 피로 ${G.stats.fatigue} · 긴장 ${G.stats.tension}`, heardLine,
        `박 소장: "저는 남아서 건물 봐야죠." · 서연: "저는 주민센터에 이동 지원을 신청해 뒀어요."`, G.know.clerkLeft ? '바로편의점 문에 쪽지: "본가에 다녀옵니다 — 이수아"' : '바로편의점은 문을 닫았다.',
        '어느 쪽도 정답이 정해져 있지 않습니다. 지금의 조건으로 고르세요.'].filter(Boolean),
      actions: [{ id: 'stay', label: '집에 남는다' }, { id: 'move', label: '생활문화센터 임시 거처로 옮긴다' }] });
    recordEvent(G, 'decision', { stayMove: G.know.stayMove, water, food });
    ctx.checkpoint('fire4');
  }
  if (G.know.stayMove === 'move') await moveAndFire(ctx); else await stayAndSmoke(ctx);
  const sum = chapterSummary(G, 'crossroads');
  await ctx.modal(() => ctx.ui.chapterCard({ chapter: '4장', title: '남을까, 떠날까', lines: sum.lines, payoffs: sum.payoffs, ripples: ripples(G) }));
}

// "임시 거처 급식 끊김" (game/rumors.js camp_food) arrives before the stay/move choice. Returns how it was
// checked: 'radio' | 'board' | null (acted on the judgement as it was).
async function campFood(ctx) {
  const heard = await ctx.modal(() => hearClaim(ctx, 'camp_food', { lead: ['휴대폰이 짧게 울린다. 동네 단톡방에 퍼온 글이 올라왔다.'] }));
  if (heard.act !== 'check') return null;
  if (await ctx.modal(() => radioScene(ctx, 'crossroads'))) { verify(ctx.G, 'camp_food'); return 'radio'; }
  const go = await ctx.say('정민', ['라디오로는 확인하지 못했다. 아파트 앞 단지 게시판까지 나가 볼까? 정전이라 계단으로 내려가야 한다.'],
    [{ id: 'board', label: '게시판까지 다녀온다' }, { id: 'no', label: '그만두고 집에 있는다' }], '한 번 더 확인');
  if (go !== 'board') return null;
  await boardWalk(ctx, '게시판에서 확인하기', '아파트 앞 단지 게시판이나 행정복지센터 게시판에서 공식 안내를 읽습니다. 엘리베이터는 멈춰 있어 계단으로 내려갑니다.',
    () => ctx.ui.card(NOTICE('board')));
  verify(ctx.G, 'camp_food');
  ctx.pass(15);
  await goHome(ctx);
  ctx.refresh();
  return 'board';
}
// The official notice, read on the radio or at a board.
const NOTICE = (how) => ({ kind: 'notice', title: '연무구 안내 (가상)', meta: 'D+6 · 행정복지센터 게시판·라디오',
  body: ['희망하는 주민은 생활문화센터 임시 거처로 옮길 수 있습니다. (권고, 명령 아님)', '임시 거처: 급수·급식 지원, 공용 공간, 개인 공간 협소', '집에 머무는 주민도 비상급수시설을 계속 이용할 수 있습니다.',
    how === 'radio' ? '(라디오에서 같은 내용을 들었다.)' : '(단지 게시판에서 직접 읽었다.)'],
  actions: [{ id: 'ok', label: '지금 상황 보기', primary: true }] });
const noticeCard = (ctx, how) => ctx.card(NOTICE(how));

// Stay: smoke from another flat. The fact-based answer is to judge, not to run or to fight.
async function stayAndSmoke(ctx) {
  const G = ctx.G;
  await ctx.skip('D+6 목', 'D+7 금 밤', '집에 남기로 했다. 하루가 더 지났다. 물을 아껴 쓰며 라디오 안내를 기다렸다.');
  G.day = 7; G.minute = 22 * 60 + 10; ctx.outdoorEnv = 'blackout';
  ctx.setLevelState('apartment', 'windowView', 'blackout');
  ctx.setLevelState('apartment', 'smoke', 0.35);
  ctx.setLevelState('apartment', 'haze', 0); // the smoke is in the corridor; none came into 302호 (the scene's premise)
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
    if (pick === 'fight') { recordEncounter(G, 'extinguisher', 'escalated'); ctx.fail('fire', { scene: 'upstairs' }); }
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
  ctx.setLevelState('apartment', 'haze', 'follow');
  await ctx.say('박영수 소장', ['5층 세대 주방에서 난 불이었대요. 소방대가 껐고 다친 사람은 없대요.'], null, '방송');
}

// Move: temporary stay in the shelter; someone cooks with a burner and the boxes catch.
async function moveAndFire(ctx) {
  const G = ctx.G;
  await ctx.skip('D+6 목', 'D+7 금 저녁', '짐을 꾸려 생활문화센터 임시 거처로 옮겼다. 하루가 지났다.');
  G.day = 7; G.minute = 19 * 60 + 30;
  ctx.setLevelState('shelter', 'crowd', 'settled');
  ctx.setLevelState('shelter', 'phase', 'calm');
  ctx.setLevelState('shelter', 'power', 'emergency');
  ctx.setLevelState('shelter', 'fire', 'large');
  await ctx.goto('shelter', 'spot');
  { const pl = ctx.engine.player, f = ctx.engine.level.anchors?.fire; if (f) pl.yaw = Math.atan2(-(f.x - pl.x), -(f.z - pl.z)); } // face the burning corner
  ctx.refresh();
  if (misled(G) === 'camp_food') { // the claim met what is really here
    G.know.misled = null; recordEvent(G, 'rumor_corrected', { id: 'camp_food' });
    await ctx.caption('점심과 저녁에 배식 줄이 섰다. 급식이 끊겼다던 SNS 글과는 달랐다.', null, 3800);
  }
  await ctx.caption('저녁. 주차장 구석에서 누군가 휴대용 버너로 라면을 끓이다 쌓아 둔 상자에 불이 옮겨 붙었다. 불길이 천장 배관에 닿는다.', null, 4200);
  await factCard(ctx, '대피소에서는', ['shelter_rules'], '공식 행동요령');
  const res = await ctx.modal(() => ctx.ui.extinguisher({ mode: ladderStage(G, 'extinguisher'), fire: 'large' }));
  const ev = evalExtinguisher(res?.sequence || [], { fire: 'large' });
  recordEncounter(G, 'extinguisher', ev.result);
  ctx.setLevelState('shelter', 'fire', 'none');
  if (ev.result === 'escalated') ctx.fail('fire', { scene: 'shelter' });
  await factCard(ctx, '물러서는 것도 성공입니다', ['fire_big', 'fire_shout', 'fire_119']);
  ctx.payoff('judgement', '천장까지 닿은 불 앞에서 물러나 알리고 대피했다');
}

export async function evacuation(ctx) {
  const G = ctx.G;
  G.chapter = 'evac';
  ctx.hud.setChapter('5장', '공식 대피');
  ctx.outdoorEnv = 'day';
  if (ctx.resume !== 'depart') {
    const place = G.know.stayMove === 'move' ? 'shelter' : 'home';
    const ledger = await ctx.modal(() => dayLedger(ctx, Math.max(1, 12 - G.day), place));
    await ctx.skip('D+7', 'D+12 수', [place === 'shelter' ? '닷새가 더 지났다. 임시 거처에서 지내다 오늘 아침 짐을 챙기러 집에 들렀다.' : '닷새가 더 지났다. 이웃 몇 집은 이미 떠났다.', ...ledger.slice(0, 4)]);
    G.day = 12; G.minute = G.cond.evacNoticeAt;
    applyHome(ctx);
    await ctx.goto('apartment', 'home', 'interiorDark');
    ctx.refresh();
    await emergencyText(ctx, { kind: '안전안내문자', time: hhmm(G.minute), text: `[안전안내문자] 연무동 주민 이동 지원. 오늘 14:00까지 행정복지센터 앞 임시 집결지로 이동. 1인 1가방, 신분증 지참 ${AREA}` });
    await factCard(ctx, '대피 안내를 받았을 때', ['evac_order', 'evac_kit', 'evac_note']);
    const heard = await ctx.modal(() => radioScene(ctx, 'evac'));
    G.know.evacClosuresKnown = !!heard;
    // One bag, third encounter: no tips unless the ladder says so.
    const items = bagInfo(G, bagItems(G));
    const mode = ladderStage(G, 'bag');
    const res = await ctx.modal(() => ctx.ui.bag({ mode, context: 'evac', limit: BAG_LIMIT, items, goals: BAG_GOALS.evac, tips: [] }));
    applyBag(G, res?.bag || {});
    applyPocket(G, res?.pocket);
    const ev = evalBag(G, 'evac');
    recordEncounter(G, 'bag', ev.missing?.length ? 'partial' : 'complete');
    G.know.bagOn = true;
    if (G.know.bagPacked && !ev.missing?.length) ctx.payoff('bag', '미리 꾸려 본 경험으로 한 가방을 빠르게 챙겼다');
    if (G.minute < 13 * 60 + 15) await ctx.skip(`D+12 수 ${hhmm(G.minute)}`, 'D+12 수 13:15', '짐을 싸고 집을 정리하다 보니 오후가 되었다.');
    G.minute = Math.max(G.minute, 13 * 60 + 15);
    ctx.checkpoint('depart');
  }
  await depart(ctx);
}

const roadName = (id) => CLOSURES[id]?.label.replace(/ 통제$/, '') ?? id;

export async function depart(ctx) {
  const G = ctx.G;
  const closures = G.cond.closures.evac;
  ctx.setLevelState('town', 'closures', closures);
  ctx.setLevelState('town', 'crowd', 'assembly');
  ctx.setLevelState('town', 'buses', 'on');
  ctx.setLevelState('town', 'waterPoint', 'off');
  ctx.setLevelState('town', 'notices', 'evac');
  await ctx.goto('apartment', 'home', 'interiorDark');
  ctx.refresh();
  // After the 'depart' checkpoint, so a retry asks again: "the closed road is open, a shortcut" (always false).
  const road = rumor('evac_shortcut').road(G), shortcut = roadName(road);
  const claim = await ctx.modal(() => hearClaim(ctx, 'evac_shortcut', { lead: ['나가려는데 휴대폰이 울린다. 동네 단체방에 새 글이 올라왔다.'] }));
  let checking = claim.act === 'check', checked = false;
  let followed = G.events.some(e => e.type === 'rumor_followed' && e.details?.id === 'evac_shortcut');
  if (checking && await ctx.modal(() => radioScene(ctx, 'evac'))) { checked = true; G.know.evacClosuresKnown = true; verify(G, 'evac_shortcut'); ctx.toast(`라디오: 오늘 통제 구간에 ${shortcut}도 들어 있다. 풀렸다는 글은 사실이 아니었다.`, 'official', 5200); }
  let noted = false, said = false;
  // The last choice is yours: the bus is one way out, not the answer. Each path ends the story differently.
  const show = () => ctx.objective('오늘 어떻게 할까', [
    misled(G) === 'evac_shortcut' ? (followed ? `${shortcut} 쪽은 막혀 있었다. 안내 요원이 있는 큰길로 돌아가자.` : `단체방 글대로라면 ${shortcut} 쪽이 집결지까지 지름길이다.`) : null,
    `14:00 버스는 행정복지센터 앞 집결지에서 떠납니다. ${G.know.evacClosuresKnown ? `오늘 통제: ${closures.map(c => CLOSURES[c].label).join(', ')}` : '어디가 통제인지는 아직 모릅니다.'}`].filter(Boolean).join(' '), [
    ...(checking ? [{ text: '한 번 더 확인하기: 단지 게시판 · 행정복지센터 게시판 · 관리사무소', done: checked }] : []),
    { text: '문에 쪽지 남기기 (선택)', done: noted }, { text: '버스 타기: 14:00까지 집결지', done: false }, { text: '남기: 관리사무소 박 소장에게', done: false },
    { text: '서연과 함께: 303호', done: false }, { text: '돕기: 행정복지센터 자원봉사', done: false }, { text: '걸어서 떠나기: 큰길 끝까지', done: false }]);
  show();
  // Reaching the closed road the post sent you down (by E on the guide, or walking up to the barricade).
  const follow = () => {
    if (followed || misled(G) !== 'evac_shortcut') return false;
    followed = true; recordEvent(G, 'rumor_followed', { id: 'evac_shortcut', road });
    G.know.evacClosuresKnown = true; // you have stood at one: the objective now lists today's closures
    show(); return true;
  };
  const official = (where) => async () => {
    const o = posts(G, 'evac_shortcut').find(p => p.key === 'official');
    await ctx.ui.talk({ speaker: where, role: `${o.from} · 공식 안내`, lines: [o.text, `오늘 통제 구간: ${closures.map(c => roadName(c)).join(', ')}. 현장 안내 요원의 안내를 따르십시오. (가상 안내)`] });
    G.know.evacClosuresKnown = true;
    if (!checked) { checked = true; verify(G, 'evac_shortcut'); if (claim.taken) ctx.toast(`${shortcut}은 오늘도 통제 중이다. 단체방 글과 달랐다.`, 'self', 4800); }
    show();
  };
  const B = { x0: -124, x1: 124, z0: -104, z1: 104 }; // past these, on a road, you have walked out of town
  const path = await ctx.roam({ rate: 4, tick: () => {
    const p = ctx.engine.player, L = ctx.engine.level;
    if (L?.id === 'town' && (p.x < B.x0 || p.x > B.x1 || p.z < B.z0 || p.z > B.z1)) return 'walk';
    if (L?.id === 'town' && !followed && misled(G) === 'evac_shortcut') {
      const at = L.points?.find(q => q.id === `closure_${road}`) ?? centre(CLOSURES[road]?.barricades?.[0]);
      if (at && Math.hypot(p.x - at.x, p.z - at.z) < 7 && follow()) ctx.toast(`${shortcut}: 바리케이드가 그대로 있다. 풀렸다던 글과 다르다.`, 'site', 5200);
    }
    if (G.day > 12 || G.minute >= 14 * 60) { if (lostByRumor(G)) ctx.fail('lostWay', { via: 'rumor' }); return 'missed'; }
  }, on: {
    homeExitDoor: async () => {
      if (!noted) {
        const pick = await ctx.ui.talk({ speaker: '현관', role: '나가기 전에', lines: ['문에 쪽지를 남길까?'], choices: [{ id: 'note', label: `"${hhmm(G.minute)} 행정복지센터 집결지로 떠납니다 — 302호 정민"` }, { id: 'no', label: '그냥 나간다' }] });
        noted = true;
        if (pick === 'note') { recordEvent(G, 'note_left', {}); ctx.toast(fact('evac_note').text, 'official', 5000); }
        show();
      }
      await ctx.goto('apartment', 'corridor3F');
    },
    homeDoor: async () => { await ctx.goto('apartment', 'homeEntry'); },
    homeStock: () => stockLook(ctx),
    seoyeonDoor: async () => {
      // A first answer never closes the door: 서연 can be asked again (F6).
      const pick = said
        ? await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['정민 씨, 아직 안 가셨어요? 버스 시간 괜찮아요?'], choices: [{ id: 'go', label: '이제 갈게요. 집결지에서 만나요' }, { id: 'wait', label: '같이 기다렸다가 지원 차량으로 같이 가요' }] })
        : await ctx.ui.talk({ speaker: '오서연', role: '303호', lines: ['저는 지원 차량이 3시에 와요. 집결지에서 다시 만나요.', '같이 기다려 줄 필요는 없어요. 정민 씨는 버스 시간 맞춰 가세요.'], choices: [{ id: 'go', label: '집결지에서 만나요' }, { id: 'card', label: '제 연락처 카드 사진 찍어 두세요' }, { id: 'wait', label: '같이 기다렸다가 지원 차량으로 같이 가요' }] });
      if (pick === 'card' && !said) recordEvent(G, 'help', { who: 'seoyeon', what: '떠나기 전 서로의 연락처를 다시 확인했다' });
      said = true;
      if (pick === 'wait') { recordEvent(G, 'help', { who: 'seoyeon', what: '지원 차량이 올 때까지 곁을 지켰다' }); return 'seoyeon'; }
    },
    managerWindow: async () => {
      const site = posts(G, 'evac_shortcut').find(p => p.key === 'manager');
      const pick = await ctx.ui.talk({ speaker: '박영수 소장', role: '관리사무소', lines: [site.text, '저는 남아요. 건물 지키는 사람도 있어야죠. 조심히 가요, 302호.'], choices: [{ id: 'go', label: '소장님도 조심하세요' }, { id: 'stay', label: '저도 남겠습니다. 같이 지켜요' }] });
      if (!checked) { checked = true; verify(G, 'evac_shortcut'); show(); }
      // Only staying is something 정민 does for the building; the manager's own work is not 정민's help (F7).
      if (pick === 'stay') { recordEvent(G, 'help', { who: 'manager', what: '박 소장과 함께 남아 건물을 지키기로 했다' }); return 'stay'; }
    },
    center: async () => {
      const pick = await ctx.ui.talk({ speaker: '행정복지센터', role: '자원봉사 접수', lines: ['급수 안내하고 거동 불편한 분들께 물 나를 사람이 모자라요.', '남아서 도와주실 수 있어요? 버스는 놓치게 될 거예요.'], choices: [{ id: 'yes', label: '남아서 돕겠습니다' }, { id: 'no', label: '죄송해요, 버스를 타야 해요' }] });
      if (pick === 'yes') return 'volunteer';
    },
    aptNotice: official('한빛아파트 단지 게시판'),
    centerNotice: official('행정복지센터 게시판'),
    assembly: async () => 'bus',
    ...Object.fromEntries(Object.keys(CLOSURES).map(id => [`closure_${id}`, async () => {
      if (id === road) follow();
      await ctx.ui.talk({ speaker: '안내 요원', role: CLOSURES[id].label, lines: ['이 구간은 통제 중입니다. 다른 길로 돌아가 주세요.',
        ...(id === road && misled(G) === 'evac_shortcut' ? ['단체방에 여기가 풀렸다는 글이 돈다면서요. 아니에요. 안내 요원이 있는 큰길로 가세요.'] : [])] });
    }])),
  } });
  G.know.path = path;
  recordEvent(G, 'path', { path });
  if (path !== 'bus') return void await ctx.caption({ missed: missedCaption(ctx), stay: '가방을 내려놓았다. 오늘부터 이 건물이 내 자리다.', seoyeon: '서연의 문 앞에 가방을 내려놓고 앉았다. 지원 차량은 늦어지고 있었다.',
    volunteer: '조끼를 받아 입었다. 급수대 앞 줄이 길다.', walk: '도시의 끝, 통제선을 지나 국도로 들어섰다. 뒤에서 또 포성이 울렸다.' }[path], null, 4000);
  recordEvent(G, 'arrived_assembly', { minute: G.minute });
  if (G.know.routeChecked && !followed) ctx.payoff('route', '동네 길을 미리 알아 둔 덕분에 통제 구간을 돌아 집결지에 닿았다');
  const card = (G.items.contact_card?.bag ?? 0) + (G.items.doc_copies?.bag ?? 0) > 0;
  const deep = card && !inPocket(G, 'contact_card') && !inPocket(G, 'doc_copies'); // game rule (M8): the pocket comes out at once
  const id = (G.items.id_card?.bag ?? 0) > 0; // carried in the bag or its front pocket (the pocket is part of bag); left at 집 = not shown
  await ctx.say('집결지 접수', ['이름, 연락처, 복용약을 적어 주세요. 신분증 있으세요?',
    id ? '(지갑에서 신분증을 꺼내 보여 주었다.)' : '(신분증을 집에 두고 나왔다. 이름과 주소, 생년월일로 본인 확인을 하느라 한참 걸렸다.)',
    !card ? '(연락처는 기억나는 대로 적었다.)' : deep ? '(연락처 카드와 서류 사본은 가방 깊숙이 있었다. 꺼내느라 잠깐 걸렸다.)' : '(가방 앞주머니의 연락처 카드와 서류 사본도 함께 건넸다.)'], null, '임시 집결지');
  if (deep) ctx.pass(2);
  if (!id) { ctx.pass(15); ctx.toast(fact('docs_take').text, 'official', 5000); }
  // The docs payoff credits the D-1 card/papers (game/ripples.js CAUSES.docs); the 신분증 is not a D-1 prep.
  if (card) ctx.payoff('docs', `가방에 챙긴 연락처 카드와 서류 사본으로 접수를 ${deep ? '조금 늦게나마 ' : '바로 '}마쳤다`);
  await ctx.caption('버스 창밖으로 연무동이 멀어진다.', null, 3500);
}

const centre = (b) => (b ? { x: (b.x0 + b.x1) / 2, z: (b.z0 + b.z1) / 2 } : null);
// 'missed' happens wherever the clock passes 14:00: only at the assembly point is it an empty bay (F11).
export function missedCaption(ctx) {
  const L = ctx.engine.level, p = ctx.engine.player, a = L?.id === 'town' ? L.points?.find(q => q.id === 'assembly') : null;
  return a && Math.hypot(p.x - a.x, p.z - a.z) < 25 ? '버스는 이미 떠났다. 텅 빈 집결지에 바람만 분다.' : '시계를 보니 2시가 넘었다. 버스는 이미 떠났다.';
}

export async function ending(ctx) {
  const G = ctx.G;
  G.chapter = 'ending';
  recordEvent(G, 'ending', { load: bagLoad(G).weightKg });
  // C2: what the early choices led to, over the town at dusk, then where 정민 is now. Never blocks the ending.
  try { await rippleMontage(ctx, ripples(G), G.know.path); } catch (err) { console.error('[cine] ripple montage failed; the story goes on', err); }
  return ctx.modal(() => ctx.ui.ending(endingRecord(G), SOURCES));
}
