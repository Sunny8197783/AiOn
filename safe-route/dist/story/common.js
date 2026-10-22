// Scenes several chapters reuse: the radio, the phone, emergency texts, the light in your hand.
import { check, recordEvent, use } from '../game/state.js';
import { ladderStage, recordEncounter } from '../game/drills.js';
import { fact } from '../game/facts.js';
import { RADIO_FREQ } from './prep-actions.js';
import { CLOSURES } from '../game/townmap.js';

const has = (G, id) => { const e = G.items[id]; return !!e && e.home + e.bag > 0; };
export const AREA = '[연무구(가상)]';

// Broadcast scripts. Every line is fiction for the scenario; official advice lines quote FACTS.
export const BROADCASTS = {
  normal: ['지금은 평상시 프로그램입니다. (가상 방송)', '비상시에는 이 방송에서 정부와 지자체 안내를 전합니다.'],
  blackout: ['연무구청이 알려 드립니다. (가상 방송)', '오늘 19시 40분쯤 연무동 일대에 정전이 발생했습니다. 원인을 확인하고 있으며 복구 시간은 아직 정해지지 않았습니다.', '라디오와 재난문자로 전달되는 안내를 계속 들어 주십시오.'],
  shelter: ['공습경보가 발령 중입니다. (가상 방송)', '대피소 안에서는 관리자의 안내를 따르고, 출입구와 통로를 막지 마십시오.', fact('radio_listen').text],
  water: ['연무구청이 알려 드립니다. (가상 방송)', '단수가 이어지고 있어 연무동 행정복지센터 마당의 비상급수시설을 운영합니다.', `운영 시간은 오전 9시 30분부터 오후 5시까지, 물통을 가져오십시오. ${fact('water_point').text}`],
  rumor: ['연무구청이 알려 드립니다. (가상 방송)', '생활문화센터 대피소는 정상 운영 중입니다. 확인되지 않은 정보에 주의해 주십시오.', fact('info_rumor').text],
  crossroads: ['연무구청이 알려 드립니다. (가상 방송)', '희망하는 주민은 생활문화센터 임시 거처로 옮길 수 있습니다. 권고이며 명령이 아닙니다.', '집에 머무는 주민도 행정복지센터 비상급수시설을 계속 이용할 수 있습니다.', fact('info_rumor').text],
  // Today's closures come from this run's conditions, so the broadcast never names an open road.
  evac: (G) => ['연무구청이 알려 드립니다. (가상 방송)', '연무동 주민 이동 지원을 시작합니다. 행정복지센터 앞 임시 집결지에서 오후 2시까지 버스가 출발합니다.', `한 사람당 가방 하나, 신분증을 챙기십시오. 오늘 통제 구간: ${G.cond.closures.evac.map(id => CLOSURES[id].label.replace(/ 통제$/, '')).join(', ')}. 현장 안내를 따르십시오.`],
};

// What the disaster-broadcast station is saying right now (for the phone radio opened with P).
export function currentBroadcast(G) {
  const after = (t) => G.events.some((e) => e.type === t);
  if (G.chapter === 'dday') return after('alert') ? 'shelter' : after('blackout') ? 'blackout' : 'normal';
  if (G.chapter === 'endure') return G.day <= 1 ? 'water' : 'rumor';
  if (G.chapter === 'crossroads') return 'crossroads';
  if (G.chapter === 'evac' || G.chapter === 'ending') return 'evac';
  return 'normal';
}

// Radio scene. Returns heard lines, or null when it cannot be played. Without a working radio the phone's
// FM app (earphones as the antenna) is offered while the phone has battery; it costs battery.
// device: 'phone' skips the portable radio (the P key).
export async function radioScene(ctx, script, { title = '라디오', device = null } = {}) {
  const G = ctx.G;
  let dev = device, extraSeconds = 0;
  if (!dev && has(G, 'radio')) {
    dev = 'radio';
    if (!G.items.radio.checked) {
      const r = check(G, 'radio');
      if (r.ok) { extraSeconds = 40; ctx.pass(2); ctx.toast('어둠 속에서 건전지를 찾아 넣었다. 시간이 조금 걸렸다.'); }
      else { await ctx.ui.talk({ speaker: title, role: '휴대용 라디오', lines: ['건전지가 없어 켜지지 않는다.', fact('radio_batt').text] }); recordEvent(G, 'radio_blocked', { script }); dev = null; }
    }
  }
  if (!dev) {
    if (G.phone.battery <= 0) { await ctx.ui.talk({ speaker: title, role: '라디오 없음', lines: ['라디오가 없고, 휴대폰도 꺼져 있다.'] }); return null; }
    const pick = await ctx.ui.talk({ speaker: title, role: '라디오 없음', lines: ['쓸 수 있는 라디오가 없다. 휴대폰 FM 라디오 앱으로 들어 볼까?', `(배터리가 닳는다 · 지금 ${G.phone.battery}%)`],
      choices: [{ id: 'phone', label: '휴대폰 FM 라디오를 켠다' }, { id: 'no', label: '그만둔다' }] });
    if (pick !== 'phone') return null;
    dev = 'phone';
  }
  if (dev === 'phone' && G.phone.battery <= 0) { await ctx.ui.talk({ speaker: title, role: '휴대폰', lines: ['휴대폰이 꺼져 있다.'] }); return null; }
  const lines = typeof BROADCASTS[script] === 'function' ? BROADCASTS[script](G) : BROADCASTS[script];
  const mode = ladderStage(G, 'radio');
  const start = mode === 'free' ? 89.5 : (G.know.radioFreq ?? 89.5);
  const res = await ctx.ui.radio({ mode, start, battery: true, extraSeconds, device: dev,
    onSignal: (c) => ctx.audio.radio({ on: c !== null, clarity: c ?? 0 }), stations: [
    { freq: 95.1, name: '음악 방송', kind: 'music', lines: ['(잔잔한 음악이 흐른다)'] },
    { freq: 99.3, name: '알 수 없는 방송', kind: 'rumor', lines: ['…들은 얘긴데요, 그쪽 대피소는 이미 꽉 찼다더라고요… (출처를 밝히지 않는 방송)'] },
    { freq: RADIO_FREQ, name: `재난방송 주관방송사 라디오 (게임 속 주파수 ${RADIO_FREQ}MHz)`, kind: 'official', lines },
  ] });
  if (dev === 'phone') { drainBattery(G, 3); ctx.refresh?.(); } else use(G, 'radio');
  recordEncounter(G, 'radio', res?.official ? 'tuned' : 'missed');
  if (res?.official) G.know.radioFreq = RADIO_FREQ;
  for (const n of res?.noted || []) ctx.note(n.text, n.kind === 'official' ? 'official' : 'rumor');
  return res?.official ? lines : null;
}

// Light in the dark: flashlight if usable, else the phone (drains battery).
export function lightSource(ctx) {
  const G = ctx.G;
  if (G.items.flashlight?.checked && has(G, 'flashlight')) return 'flashlight';
  return G.phone.battery > 0 ? 'phone' : null;
}

export async function emergencyText(ctx, { kind = '위급재난문자', time, text }) {
  const G = ctx.G;
  recordEvent(G, 'sms', { kind, time });
  if (G.phone.battery <= 0) { ctx.toast('휴대폰이 꺼져 있어 문자를 볼 수 없다. 주변 안내를 따른다.', 'site'); return; }
  if (kind === '위급재난문자') ctx.audio.phoneAlert('emergency'); else ctx.audio.phoneAlert('safety');
  ctx.note(text, 'official');
  await ctx.modal(() => ctx.ui.emergencyAlert({ kind, text, time }));
}

export const factCard = (ctx, title, ids, meta = '공식 행동요령') => ctx.card({ kind: 'info', title, meta,
  body: ids.map(id => fact(id)?.text).filter(Boolean), actions: [{ id: 'ok', label: '확인', primary: true }] });

export const drainBattery = (G, n) => { G.phone.battery = Math.max(0, G.phone.battery - n); };
export { has };
