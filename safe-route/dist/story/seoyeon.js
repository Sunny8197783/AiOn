// "같은 문장, 다른 사람": 정민 texts 서연 from the shelter; 서연 reads it at her own eye height,
// sees why it does not carry over to her, and decides with the notice in front of her.
import { makeHandover, buildPins, closingLines, relayTimes, noteContext, ENTRANCES, hhmm } from '../game/relay.js';
import { recordEvent, use, usable } from '../game/state.js';
import { PLACES, SPAWNS } from '../game/townmap.js';
import { fact } from '../game/facts.js';
import { compose, showNote, hideNote, rereadCaption } from '../ui/compose.js';
import { createReread } from '../notecheck.js';
import { SEOYEON_CARD } from './prep-actions.js';
import { drainBattery } from './common.js';

const CAFE = { x: -118, y: 0, z: -12.5, yaw: Math.PI };
const ANCHOR = { stairs: PLACES.shelterA, ramp: PLACES.shelterB, notice: PLACES.shelterNotice, gate: PLACES.shelterEast, access: PLACES.shelterC, start0: SPAWNS.home, start1: CAFE };
const toMin = (s) => { const [h, m] = s.split(':').map(Number); return h * 60 + m; };

export async function seoyeonRelay(ctx) {
  const G = ctx.G;
  const gate = G.events.find(e => e.type === 'gate_observed' && e.details.who === 'jeongmin');
  G.relay = relayTimes(G.minute + 2, gate?.details.minute ?? G.minute - 3);
  G.handover = null;
  if (!G.rel.seoyeon.contact) {
    await ctx.card({ kind: 'info', title: '서연에게 연락할 방법이 없다', meta: '303호 이웃', body: ['서연의 번호를 모른다. 서연은 오늘 저녁 밖에 있다고 했던 것 같은데….', '준비할 때 이웃과 연락 방법을 정해 두지 않았다.'], actions: [{ id: 'ok', label: '계속', primary: true }] });
  } else {
    if (G.phone.battery <= 3 && usable(G, 'powerbank')) { use(G, 'powerbank'); G.phone.battery = 45; ctx.payoff('powerbank', '꺼져 가던 휴대폰을 보조배터리로 살렸다'); }
    if (G.phone.battery <= 0) {
      await ctx.card({ kind: 'info', title: '휴대폰이 꺼졌다', meta: '배터리 0%', body: ['서연에게 문자를 보내려 했지만 휴대폰이 켜지지 않는다.'], actions: [{ id: 'ok', label: '계속', primary: true }] });
    } else {
      const res = await ctx.modal(() => compose({ ...noteContext(G), gateSeenAt: G.relay.gateSeenAt, sentAt: G.relay.sentAt }));
      makeHandover(G, res);
      drainBattery(G, 2);
      ctx.toast(`${fact('comms_text').text}`, 'official', 5000);
    }
  }

  // 서연's side. The clock is hers now.
  G.minute = toMin(G.relay.receivedAt);
  ctx.hud.setChapter('2장', '같은 문장, 다른 사람');
  ctx.refresh();
  await ctx.card({ kind: 'info', title: '시점 전환 · 오서연', meta: `${G.relay.receivedAt} · 카페 쉼표 앞`,
    body: ['이제 303호 서연입니다. 서연은 보행 보조기를 쓰고, 병원 진료 뒤 카페에 있다가 경보를 들었습니다.', ...SEOYEON_CARD.map(l => `· ${l}`)],
    actions: [{ id: 'ok', label: '서연으로', primary: true }] });
  ctx.setLevelState('town', 'gates', { east: 'closed', stairs: 'open', ramp: 'open', access: 'open' });
  ctx.setLevelState('town', 'crowd', 'evacuating');
  await ctx.goto('town', CAFE);
  ctx.setPlayer?.('seoyeon');
  ctx.speedOverride = 0.55;
  ctx.refresh();
  if (G.handover) await reread(ctx);
  else await ctx.card({ kind: 'sms', title: '받은 문자 없음', meta: G.relay.receivedAt, body: ['서연에게 정민의 문자는 오지 않았습니다. 서연은 지금의 현장 안내만으로 판단합니다.'], actions: [{ id: 'ok', label: '계속', primary: true }] });
  await walk(ctx);
  ctx.speedOverride = null;
  ctx.setPlayer?.('jeongmin');
  G.minute = Math.max(G.minute, toMin(G.relay.receivedAt) + 5);
  ctx.hud.setChapter('2장', '정전과 경보');
}

async function reread(ctx) {
  const G = ctx.G, h = G.handover, pins = buildPins(G);
  const { engine } = ctx;
  engine.setControls(false);
  ctx.hud.setCinematic(true);
  const fx = createReread(engine, { start: CAFE, reduceMotion: !!ctx.settings.reduceMotion });
  const placePin = (i) => {
    const p = pins[i];
    fx.pin(i + 1, ANCHOR[p.anchor], p.was, p.now);
    if (p.also) fx.pin('✕', ANCHOR[p.also.anchor], ENTRANCES[p.also.anchor].label, p.also.now, 2.2);
    if (p.line) fx.link(ANCHOR[p.anchor], ANCHOR[p.line]);
    return p;
  };
  const steps = [
    () => { engine.setView('first'); fx.eye(-0.5); showNote(h, 'phone'); return rereadCaption('방금 정민으로서 쓴 문장입니다. 서연에게 닿은 것은 이 문장뿐입니다.'); },
    () => { showNote(h, 'marked'); return rereadCaption(h.kind === 'unparsed' ? '이 문장에서 입구·시각·범위를 찾지 못했어요.' : '문장을 나눠 봅니다. 표시한 부분이 이 문장이 서연에게 말하는 것입니다.'); },
    () => { hideNote(); engine.setView('third'); fx.crane(); fx.showPath(ctx.trail || []); return rereadCaption('정민이 실제로 걸은 길 — 당신은 알고 있지만, 서연은 보지 못했습니다.'); },
    () => rereadCaption('정민이 들어갈 수 있던 입구는 둘 다 서연의 기준과 다릅니다.', placePin(0)),
    () => rereadCaption(`메시지는 ${h.sentAt}에 보낸 것입니다. 지금은 ${h.receivedAt}, 그 사이 안내판이 바뀌었습니다.`, placePin(1)),
    () => rereadCaption(closingLines(h.kind).join(' '), placePin(2), true),
  ];
  for (let i = 0; i < steps.length; i++) {
    // Esc: jump to the closing step with every pin placed (step 3+i places pin i).
    if ((await steps[i]()) === 'skip' && i < steps.length - 1) {
      hideNote();
      if (i < 2) { engine.setView('third'); fx.crane(); fx.showPath(ctx.trail || []); }
      for (let j = Math.max(0, i - 2); j < 2; j++) placePin(j);
      i = steps.length - 2;
    }
  }
  recordEvent(G, 'reread_done', { pins: pins.map(p => p.factor) });
  fx.dispose(); hideNote();
  ctx.hud.setCinematic(false);
  engine.setView(ctx.settings.view);
  engine.setControls(true);
}

async function walk(ctx) {
  const G = ctx.G, t = G.relay;
  let read = false;
  const show = () => ctx.objective('서연 · 지금의 안내로 대피하기', '정민의 문자는 참고만 합니다. 현장 안내판을 보고, 서연에게 맞는 출입구로 갑니다.', [{ text: '생활문화센터 현장 안내판 확인', done: read }, { text: '서연에게 맞는 출입구로', done: false }]);
  show();
  await ctx.roam({ rate: 2, doors: false,
    on: {
      shelterNotice: async () => {
        const pick = await ctx.ui.card({ kind: 'notice', title: '지금 운영 중인 출입구', meta: `현장 안내 · ${t.accessOpenAt} 갱신 · 이 시설`,
          body: ['동쪽 출입구: 폐쇄 유지', 'A 계단: 운영', 'B 지하주차장 진출입로: 운영 · 차량 통제 · 내리막이 가파름', `C 경사로: ${t.accessOpenAt} 개방 · 유도블록 · 안내 요원 있음`, '어느 입구로 갈지는 서연이 정합니다.'],
          actions: [{ id: 'access', label: 'C로 갈게요', primary: true }, { id: 'stairs', label: 'A로 갈게요' }, { id: 'ramp', label: 'B로 갈게요' }] });
        read = true;
        recordEvent(G, 'notice_read', { who: 'seoyeon', minute: G.minute });
        recordEvent(G, 'notice_choice', { who: 'seoyeon', route: pick });
        ctx.note(`현장 안내 ${t.accessOpenAt}: C 경사로 개방 (서연이 확인)`, 'site');
        if (pick === 'access') ctx.toast(`C 경사로 · ${t.accessOpenAt}에 열림 · 정민이 문자를 보낸 ${t.sentAt} 뒤의 일`, 'site', 5000);
        show();
      },
    },
    shelter: async (point) => {
      if (point.id === 'shelterEast') return void ctx.toast('동쪽 출입구 폐쇄 · 안내판을 확인하세요', 'site');
      if (point.id === 'shelterNotice') return;
      if (point.id === 'shelterC') {
        recordEvent(G, 'route_attempt', { who: 'seoyeon', route: 'access', minute: G.minute });
        await ctx.ui.card({ kind: 'info', title: '서연이 출입구 C로 대피했습니다', meta: hhmm(G.minute), body: ['완만한 경사로를 보조기와 함께 내려갔습니다.', fact('shelter_rules').text], actions: [{ id: 'ok', label: '정민에게로', primary: true }] });
        return 'in';
      }
      const route = point.id === 'shelterA' ? 'stairs' : 'ramp';
      recordEvent(G, 'route_attempt', { who: 'seoyeon', route, minute: G.minute });
      const pick = await ctx.ui.talk({ speaker: '오서연', role: '가까이 와서 보니', lines: [
        route === 'stairs' ? '계단은 혼자 내려가지 않아요. 보조기와 떨어지지도 않을 거예요.' : '이 내리막은 가팔라서 혼자 내려가지 않아요.',
        `입구의 안내 요원이 C 경사로가 ${t.accessOpenAt}에 열렸다고 알려 준다.`],
        choices: [{ id: 'self', label: 'C 경사로로 계획을 바꾼다' }, { id: 'escort', label: '안내 요원에게 C까지 함께 걸어 달라고 한다' }] });
      recordEvent(G, 'help_choice', { who: 'seoyeon', choice: pick });
      if (pick === 'escort') {
        await ctx.ui.talk({ speaker: '안내 요원', role: '출입구', lines: ['어떻게 도와드릴까요?'] });
        await ctx.ui.talk({ speaker: '오서연', role: '도움 요청', lines: ['C까지 옆에서 같이 걸어 주세요. 보조기는 제가 밀게요.', fact('people_ask').text] });
      }
      ctx.toast('C 경사로로 계획을 바꿨다.', 'self');
    },
  });
}
