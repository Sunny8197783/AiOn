// 2장 정전과 경보 · D-day 금요일. 19:40 blackout, 20:05 air-raid alert, the first evacuation.
// What was prepared shows up here as time saved, not as a score.
import { recordEvent, hhmm, use } from '../game/state.js';
import { chapterSummary } from '../game/debrief.js';
import { ladderStage, recordEncounter, evalBag } from '../game/drills.js';
import { CLOSURES } from '../game/townmap.js';
import { BAG_LIMIT, CATALOG } from '../game/items.js';
import { fact } from '../game/facts.js';
import { applyHome, applyBag } from './prep-actions.js';
import { radioScene, emergencyText, factCard, lightSource, drainBattery, has, AREA, BROADCASTS } from './common.js';
import { seoyeonRelay } from './seoyeon.js';

const ALERT = 20 * 60 + 5, LATE = 20 * 60 + 30;

export async function dday(ctx) {
  const G = ctx.G;
  G.chapter = 'dday';
  ctx.hud.setChapter('2장', '정전과 경보');
  if (ctx.resume !== 'alert') {
    await evening(ctx);
    ctx.checkpoint('alert');
  } else {
    blackoutState(ctx);
    await ctx.goto('apartment', 'home');
  }
  await alert(ctx);
  await shelter(ctx);
  await seoyeonRelay(ctx);
  await afterAlert(ctx);
}

function blackoutState(ctx) {
  ctx.power = false; ctx.outdoorEnv = 'blackout'; ctx.indoorEnv = 'interior';
  ctx.setLevelState('apartment', 'power', 'off');
  ctx.setLevelState('apartment', 'windowView', 'blackout');
  ctx.setLevelState('store', 'power', 'off');
  ctx.G.phone.signal = 'weak';
  applyHome(ctx);
}

async function evening(ctx) {
  const G = ctx.G;
  G.day = 0; G.minute = 19 * 60 + 30;
  ctx.power = true; ctx.indoorEnv = 'interiorNight'; ctx.outdoorEnv = 'night';
  ctx.setLevelState('apartment', 'windowView', 'night');
  applyHome(ctx);
  await ctx.goto('apartment', 'home');
  ctx.refresh();
  if (G.rel.seoyeon.contact) await ctx.card({ kind: 'sms', title: '오서연', meta: '19:32 · 문자', body: ['정민 씨, 병원 끝나고 카페 쉼표에서 잠깐 쉬고 있어요. 9시쯤 들어가요.'], actions: [{ id: 'ok', label: '확인', primary: true }] });
  ctx.objective('금요일 저녁', '집에서 쉬는 중입니다.', []);
  let t = 0;
  await ctx.roam({ doors: false, tick: (dt) => ((t += dt) > 7 ? 'go' : undefined), on: { '*': async () => 'go' } });

  // 19:40 the lights go out.
  G.minute = 19 * 60 + 40;
  blackoutState(ctx);
  ctx.engine.setEnv('interiorDark', { seconds: 0.4 });
  ctx.audio.ambience('blackout', 0.3);
  recordEvent(G, 'blackout', {});
  ctx.refresh();
  await ctx.caption('정전 — 집 안의 불이 한꺼번에 꺼졌다. 창밖 건너편 동도 캄캄하다.', null, 3500);
  const src = lightSource(ctx);
  ctx.lightSource = src;
  ctx.toast(src === 'flashlight' ? 'F 키: 손전등 켜기/끄기' : src === 'phone' ? 'F 키: 휴대폰 손전등 (배터리가 빨리 닳습니다)' : '빛이 없다. 천천히 움직이자.', 'self', 5000);
  if (src === 'flashlight') { use(G, 'flashlight'); ctx.payoff('flashlight', '정전 직후 손전등을 바로 켰다'); }

  const done = new Set();
  while (!done.has('radio') && !done.has('phone')) {
    const choices = [
      { id: 'radio', label: '라디오를 켠다', disabled: !has(G, 'radio'), reason: '라디오가 없다' },
      { id: 'phone', label: '휴대폰을 본다' },
      { id: 'window', label: '창밖을 본다', disabled: done.has('window') },
      { id: 'hall', label: '복도에 나가 본다', disabled: done.has('hall') },
    ];
    if (has(G, 'candles') && !G.know.candleLit) choices.push({ id: 'candle', label: '양초를 켠다' });
    const pick = await ctx.say('정전', ['무엇부터 할까?'], choices, '19:41');
    done.add(pick);
    if (pick === 'radio') {
      const heard = await ctx.modal(() => radioScene(ctx, 'blackout'));
      if (heard && G.know.radioChecked) ctx.payoff('radio', '맞춰 둔 주파수로 정전 안내를 바로 들었다');
      if (!heard) done.delete('radio');
    } else if (pick === 'phone') {
      drainBattery(G, 3);
      await ctx.modal(() => ctx.ui.phone({ battery: G.phone.battery, signal: 'weak', mapApp: 'fail', messages: [
        { from: '안전안내문자', time: '19:46', kind: 'safety', text: `[안전안내문자] 19:46 연무동 일대 정전 발생. 복구 시간 미정. 추가 안내 예정 ${AREA}` },
      ], actions: [{ id: 'close', label: '닫기' }] }));
      ctx.note('19:46 연무동 일대 정전, 복구 시간 미정 (안전안내문자)', 'official');
      ctx.toast('지도앱이 열리지 않는다. 데이터가 거의 잡히지 않는다.', 'site');
    } else if (pick === 'window') {
      await ctx.say('창밖', ['연무동 전체가 어둡다. 건너편 동 창문에 휴대폰 불빛이 몇 개 흔들린다.'], null, '정전');
    } else if (pick === 'hall') {
      await ctx.say('3층 복도', ['비상구 표시등만 초록색으로 켜져 있다. 304호 어르신이 문을 열고 내다본다.', '"정전인가 봐요. 나는 초 하나 켜 놨어요."'], null, '정전');
    } else if (pick === 'candle') {
      G.know.candleLit = true;
      ctx.setLevelState('apartment', 'candles', 'lit');
      recordEvent(G, 'candle_lit', {});
      await factCard(ctx, '양초에 대한 안내는 공식 자료끼리 다릅니다', ['candle_prep', 'candle_no', 'candle_rules'], '판단이 필요한 정보');
    }
  }
  ctx.pass(ALERT - G.minute > 0 ? ALERT - G.minute : 0);
}

async function alert(ctx) {
  const G = ctx.G;
  G.minute = ALERT;
  ctx.refresh();
  ctx.audio.siren(true);
  ctx.caption('공습경보 사이렌 — 1분간 오르내리는 소리 (5초 상승 · 3초 하강 반복)', null, 6000);
  await emergencyText(ctx, { kind: '위급재난문자', time: '20:05', text: `[위급재난문자] 20:05 미사일 발사, 연무동지역 공습경보, 지하시설 또는 건물 내로 대피. EVACUATION, MISSILES ATTACK ${AREA}` });
  recordEvent(G, 'alert', { minute: ALERT });
  await factCard(ctx, '공습경보', ['alert_airraid', 'bag_take', 'alert_stairs', 'alert_night']);

  // Town for the evacuation.
  const closures = G.cond.closures.dday;
  ctx.setLevelState('town', 'traffic', 'none');
  ctx.setLevelState('town', 'crowd', 'evacuating');
  ctx.setLevelState('town', 'closures', closures);
  ctx.setLevelState('town', 'gates', { east: 'closed', stairs: 'open', ramp: 'open', access: 'closed' });
  ctx.setLevelState('town', 'notices', 'dday');

  let left = false, inStairs = false;
  const trail = ctx.trail = [];
  const steps = () => [{ text: '배낭 챙기기', done: !!G.know.bagOn || left }, { text: '계단으로 1층까지', done: inStairs }, { text: '가까운 대피소로', done: false }];
  const show = () => ctx.objective('대피하기', '지하시설이나 유리창 없는 건물 안으로. 엘리베이터는 쓰지 않습니다.', steps());
  show();

  const takeBag = async () => {
    if (G.know.bagOn) return;
    if (G.know.bagPacked) { G.know.bagOn = true; ctx.payoff('bag', '현관에 둔 배낭을 메고 바로 나섰다'); recordEvent(G, 'departure', { bag: 'packed' }); return; }
    const pick = await ctx.ui.talk({ speaker: '현관', role: '배낭이 비어 있다', lines: ['지금 챙길까, 그냥 나갈까?'], choices: [{ id: 'pack', label: '급히 챙긴다 (시간이 든다)' }, { id: 'go', label: '휴대폰만 들고 나간다' }] });
    if (pick !== 'pack') { recordEvent(G, 'departure', { bag: 'none' }); return; }
    const items = Object.entries(G.items).filter(([id, v]) => CATALOG[id] && v.home + v.bag > 0).map(([id, v]) => ({ id, name: CATALOG[id].name, weightKg: CATALOG[id].weightKg, volumeL: CATALOG[id].volumeL, home: v.home, bag: v.bag, tip: '', tags: [CATALOG[id].category] }));
    const mode = ladderStage(G, 'bag');
    const res = await ctx.ui.bag({ mode, context: 'alert', limit: BAG_LIMIT, items, goals: ['물', '조리 없이 먹을 음식', '빛', '약', '연락처'], tips: [] });
    applyBag(G, res?.bag || {});
    const ev = evalBag(G, 'alert');
    recordEncounter(G, 'bag', ev.missing?.length ? 'partial' : 'complete');
    ctx.pass(4);
    G.know.bagOn = true;
    ctx.toast('물건을 고르느라 4분이 지났다.', 'self');
    recordEvent(G, 'departure', { bag: 'late', delay: 4 });
  };

  await ctx.roam({
    rate: 3, deadline: { day: 0, minute: LATE, type: 'late' },
    tick: () => {
      const p = ctx.engine.player;
      if (ctx.engine.level?.id === 'town') { const last = trail.at(-1); if (!last || Math.hypot(last.x - p.x, last.z - p.z) > 1.5) trail.push({ x: p.x, z: p.z }); }
      if (!inStairs && ctx.engine.level?.id === 'apartment' && p.y < 1 && left) { inStairs = true; show(); if (ctx.lightSource === 'flashlight') ctx.payoff('flashlight', '손전등으로 어두운 계단을 내려갔다'); }
    },
    on: {
      homeBag: async () => { await takeBag(); show(); },
      homeRadio: async () => {
        if (G.know.candleLit && !G.know.candleOut) { G.know.candleOut = true; ctx.setLevelState('apartment', 'candles', 'none'); recordEvent(G, 'candle_out', {}); return void ctx.toast('촛불을 껐다.'); }
        ctx.toast('지금은 대피가 먼저다.');
      },
      homeExitDoor: async () => {
        if (G.know.candleLit && !G.know.candleOut) {
          const pick = await ctx.ui.talk({ speaker: '거실', role: '촛불', lines: ['탁자 위 촛불이 아직 켜져 있다.'], choices: [{ id: 'out', label: '끄고 나간다' }, { id: 'leave', label: '그대로 나간다' }] });
          if (pick === 'out') { G.know.candleOut = true; ctx.setLevelState('apartment', 'candles', 'none'); }
          recordEvent(G, pick === 'out' ? 'candle_out' : 'candle_left', {});
          if (pick !== 'out') ctx.toast(fact('candle_rules').text, 'official', 6000);
        }
        await takeBag();
        left = true;
        await ctx.goto('apartment', 'corridor3F');
        show();
      },
      elevator: async () => { ctx.toast(`정전으로 멈춰 있다. ${fact('alert_stairs').text}`, 'official', 5000); recordEvent(G, 'elevator_tried', {}); },
      managerWindow: async () => {
        if (G.rel.manager.contact) {
          await ctx.ui.talk({ speaker: '박영수 소장', role: '1층 현관', lines: ['정민 씨! 생활문화센터로 가요. 연무로 가운데는 통제한대요. 단지길이나 시장길로 돌아가요!'] });
          G.know.closureHeard = true; ctx.note('박 소장: 연무로 중앙 구간 통제, 단지길·시장길로 우회', 'neighbor');
        } else await ctx.ui.talk({ speaker: '관리사무소장', role: '1층 현관', lines: ['생활문화센터 대피소로 가세요! 길 막힌 데 있으니 안내 잘 보시고요!'] });
      },
      ...Object.fromEntries(Object.keys(CLOSURES).map(id => [`closure_${id}`, async () => {
        await ctx.ui.talk({ speaker: '안내 요원', role: CLOSURES[id].label, lines: ['이 구간은 통제 중입니다. 돌아가 주세요. 생활문화센터는 단지길이나 시장길로 갈 수 있습니다.'] });
        G.know.closureSeen = true; ctx.note(`${CLOSURES[id].label} (현장 안내 요원)`, 'site');
      }])),
      '*': async (point) => { if (point.kind === 'notice') await ctx.ui.talk({ speaker: point.label, role: '안내', lines: ['대피소: 연무 생활문화센터 지하주차장. 유도표지판의 화살표를 따라가세요.'] }); },
    },
    shelter: (point) => enterGate(ctx, point),
  });
}

async function enterGate(ctx, point) {
  const G = ctx.G;
  const gateSeen = () => G.events.some(e => e.type === 'gate_observed' && e.details.who === 'jeongmin');
  if (point.id === 'shelterEast') {
    if (!gateSeen()) recordEvent(G, 'gate_observed', { who: 'jeongmin', via: 'sign', minute: G.minute });
    G.know.gateClosedSeen = true;
    await ctx.ui.card({ kind: 'notice', title: '동쪽 출입구 폐쇄', meta: `현장 안내 · ${hhmm(G.minute)}`, body: ['동쪽 출입구 폐쇄 · A 계단 / B 진출입로 이용', '확인한 사실: 이 출입구가 닫혀 있음. 이것만으로는 시설 전체의 운영 여부를 알 수 없다.'], actions: [{ id: 'ok', label: '다른 출입구로', primary: true }] });
    if (G.know.entrancesChecked) ctx.payoff('entrances', '닫힌 정문 앞에서 망설이지 않고 다른 출입구로 향했다');
    return;
  }
  if (point.id === 'shelterC') return void ctx.toast('셔터가 내려져 있다: "준비 중"', 'site');
  if (point.id === 'shelterNotice') return void ctx.ui.talk({ speaker: '현장 안내판', role: hhmm(G.minute), lines: ['동쪽 출입구 폐쇄 · A 계단 운영 · B 진출입로 운영 (차량 통제) · C 경사로 준비 중'] });
  const route = point.id === 'shelterA' ? 'stairs' : 'ramp';
  if (!gateSeen()) recordEvent(G, 'gate_observed', { who: 'jeongmin', via: 'notice_after', minute: G.minute });
  recordEvent(G, 'route_attempt', { who: 'jeongmin', route, minute: G.minute });
  recordEvent(G, 'arrived', { minute: G.minute, late: G.minute > ALERT + 15 });
  if (G.know.routeChecked) ctx.payoff('route', '확인해 둔 경로 덕분에 막힌 길에서 돌아갈 길을 알았다');
  ctx.audio.siren(false);
  await ctx.goto('shelter', route === 'stairs' ? 'stairs' : 'ramp');
  return 'arrived';
}

async function shelter(ctx) {
  const G = ctx.G;
  ctx.setLevelState('shelter', 'crowd', 'arriving');
  ctx.setLevelState('shelter', 'phase', 'alert');
  ctx.setLevelState('shelter', 'power', 'emergency');
  ctx.engine.setEnv('underground', { seconds: 0.8 });
  let signed = false;
  ctx.objective('대피소 안에서', '접수대에서 명부를 쓰고 자리를 잡습니다.', [{ text: '명부 쓰기 (접수대)', done: false }, { text: '자리 잡기', done: false }]);
  await ctx.roam({ rate: 0, on: {
    reception: async () => {
      if (signed) return void ctx.toast('명부를 썼다.');
      const card = (G.items.contact_card?.bag ?? 0) > 0 || (G.items.doc_copies?.bag ?? 0) > 0;
      await ctx.ui.talk({ speaker: '대피소 관리자', role: '접수대', lines: ['이름이랑 연락처, 복용하는 약 있으면 여기 적어 주세요.', card ? '(배낭 앞주머니의 비상연락카드를 보고 바로 적었다.)' : '(휴대폰을 보며 기억나는 대로 적었다. 배터리가 아깝다.)'] });
      if (card) ctx.payoff('docs', '비상연락카드를 보고 명부를 바로 적었다'); else drainBattery(G, 2);
      await ctx.ui.talk({ speaker: '대피소 관리자', role: '접수대', lines: [fact('shelter_rules').text, fact('shelter_rumor').text] });
      signed = true;
      ctx.objective('대피소 안에서', '자리를 잡습니다.', [{ text: '명부 쓰기 (접수대)', done: true }, { text: '자리 잡기', done: false }]);
    },
    shelterRadio: async () => { await ctx.ui.talk({ speaker: '공용 라디오', role: '재난방송 (가상)', lines: BROADCASTS.shelter }); },
    shelterBoard: async () => { await ctx.ui.talk({ speaker: '대피소 게시판', role: '안내', lines: ['연무 생활문화센터 지하주차장 · 민방위 대피소', fact('shelter_share').text] }); },
    spot: async () => (signed ? 'sit' : void ctx.toast('먼저 접수대에서 명부를 써 주세요.', 'site')),
  } });
  if (G.know.bagOn) for (const id of ['water_500', 'water_2l', 'blanket']) if (G.items[id]?.bag) { use(G, id); ctx.payoff('bag', '배낭에서 물과 담요를 꺼냈다'); break; }
  if (!G.know.bagOn) { ctx.stat('thirst', 6); ctx.toast('맨손으로 나와 마실 물이 없다. 옆 사람이 물 한 모금을 나눠 주었다.', 'neighbor'); }
}

async function afterAlert(ctx) {
  const G = ctx.G;
  const h = G.handover;
  await ctx.goto('shelter', 'spot');
  ctx.setLevelState('shelter', 'crowd', 'settled');
  ctx.engine.level?.npcs?.seoyeon && (ctx.engine.level.npcs.seoyeon.group.visible = true);
  const lines = !h ? ['정민 씨! 다행이다. 저는 C로 들어왔어요. 안내판에 C가 열렸다고 해서요.']
    : h.kind === 'generalized' ? ['메시지 고마워요. 대피소가 닫혔다길래 잠깐 놀랐는데, 안내판엔 C가 열렸다고 해서 다시 봤어요.']
    : h.kind === 'precise' ? ['메시지 고마워요. 언제 뭘 봤는지, 뭘 모르는지 써 줘서 판단하기 쉬웠어요. 저는 C로 왔어요.']
    : ['메시지 고마워요. 저는 안내판 보고 C로 왔어요. 계단은 저한테 안 맞아서요.'];
  await ctx.say('오서연', lines, null, '대피소');
  G.rel.seoyeon.trust += 1;

  G.minute = 20 * 60 + 35; ctx.refresh();
  await emergencyText(ctx, { kind: '위급재난문자', time: '20:35', text: `[위급재난문자] 20:35 연무동지역 경계경보로 변경, 대피소에서 나와 경계 태세를 유지하며 이동 가능 ${AREA}` });
  await factCard(ctx, '경계경보로 바뀌었습니다', ['alert_downgrade', 'alert_caution']);
  G.minute = 20 * 60 + 50; ctx.refresh();
  await emergencyText(ctx, { kind: '안전안내문자', time: '20:50', text: `[안전안내문자] 20:50 연무동지역 경보해제. 정부 안내방송을 계속 들어 주십시오 ${AREA}` });

  // Home together. The building is still dark; 서연 lives on the same floor.
  ctx.outdoorEnv = 'blackout';
  ctx.setLevelState('town', 'crowd', 'sparse');
  await ctx.goto('apartment', 'lobby');
  const pick = await ctx.say('오서연', ['엘리베이터가 안 되네요. 3층이니까 계단으로 천천히 갈게요.'], [{ id: 'ask', label: '"어떻게 도와드릴까요?"' }, { id: 'carry', label: '보행기를 들어 드릴게요' }, { id: 'go', label: '먼저 올라갈게요' }], '1층 계단 앞');
  if (pick === 'ask') {
    await ctx.say('오서연', ['보행기를 접어서 제 옆에서 들어 주세요. 저는 난간 잡고 천천히 갈게요. 떨어져서 먼저 가지만 않으면 돼요.'], null, '1층 계단 앞');
    ctx.stat('fatigue', 6); G.rel.seoyeon.trust += 2; recordEvent(G, 'help', { who: 'seoyeon', what: '어떻게 도울지 먼저 묻고, 계단에서 보행기를 옆에서 들어 주었다' });
    ctx.toast(fact('people_ask').text, 'official', 6000);
  } else if (pick === 'carry') {
    await ctx.say('오서연', ['아, 잠깐만요. 보행기는 제 옆에 있어야 해서요. 접어서 바로 옆에서 들어 주시면 좋겠어요.'], null, '1층 계단 앞');
    ctx.stat('fatigue', 6); G.rel.seoyeon.trust += 1; recordEvent(G, 'help', { who: 'seoyeon', what: '계단에서 보행기를 옆에서 들어 주었다' });
  } else {
    await ctx.say('오서연', ['네, 천천히 갈게요. 소장님이 계단 비춰 주신대요.'], null, '1층 계단 앞');
  }
  G.day = 0; G.minute = 21 * 60 + 20;
  await ctx.goto('apartment', 'home');
  const sum = chapterSummary(G, 'dday');
  await ctx.modal(() => ctx.ui.chapterCard({ chapter: '2장', title: '정전과 경보', lines: sum.lines, payoffs: sum.payoffs }));
}
