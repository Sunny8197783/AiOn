// Scenes several chapters reuse: the radio, the phone, emergency texts, the light in your hand.
import { check, recordEvent, use, note, drainCharge, consume } from '../game/state.js';
import { CATALOG } from '../game/items.js';
import { ladderStage, recordEncounter } from '../game/drills.js';
import { fact } from '../game/facts.js';
import { PICKS, simulateDays, applyLedger } from '../game/ledger.js';
import { RADIO_FREQ } from './prep-actions.js';
import { CLOSURES } from '../game/townmap.js';

const has = (G, id) => { const e = G.items[id]; return !!e && e.home + e.bag > 0; };
export const AREA = '[연무구(가상)]';
export const RADIO_DRAIN = 4; // % of the radio's charge one listen uses (design M3)

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
      const flat = G.items.radio.charge === 0; // it ran down (drainCharge), not one that never had batteries
      const r = check(G, 'radio');
      if (r.ok) { extraSeconds = 40; ctx.pass(2); ctx.toast(flat ? '다 닳은 건전지를 빼고 새 건전지로 갈아 끼웠다.' : '어둠 속에서 건전지를 찾아 넣었다. 시간이 조금 걸렸다.'); }
      else { await ctx.ui.talk({ speaker: title, role: '휴대용 라디오', lines: [flat ? '건전지가 다 닳아 켜지지 않는다. 갈아 끼울 건전지가 없다.' : '건전지가 없어 켜지지 않는다.', fact('radio_batt').text] }); recordEvent(G, 'radio_blocked', { script, flat }); dev = null; }
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
  if (dev === 'phone') { drainBattery(G, 3); ctx.refresh?.(); } else {
    use(G, 'radio');
    if (drainCharge(G, 'radio', RADIO_DRAIN) === 0) ctx.toast('라디오 소리가 점점 작아지더니 꺼졌다. 건전지가 다 닳았다.', 'self', 4800);
  }
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

// Official guidance arrives as notices that do not stop play, and lands in the phone's messages (P) to reread.
export function factCard(ctx, title, ids) {
  const lines = ids.map(id => fact(id)?.text).filter(Boolean);
  lines.forEach((t, i) => { note(ctx.G, t, 'official', ctx.engine.level?.id ?? null); setTimeout(() => ctx.toast(i ? t : `${title} — ${t}`, 'official', 6500), i * 2600); });
  return Promise.resolve();
}

export const drainBattery = (G, n) => { G.phone.battery = Math.max(0, G.phone.battery - n); };

// Last year's bag snack is past its date (game/items.js old_bars): eating one is unpleasant, never a fail.
export function eatOldBars(ctx) {
  const G = ctx.G, e = G.items.old_bars;
  if (!e || e.home + e.bag <= 0) return false;
  consume(G, 'old_bars', 1, e.home > 0 ? 'home' : 'bag');
  ctx.stat('health', -4); ctx.stat('tension', 4);
  recordEvent(G, 'ate_old_bars', {});
  ctx.toast('맛이 조금 이상하고 속이 더부룩하다. 비상식량은 기한을 확인해 바꿔 두면 좋겠다.', 'self', 5200);
  return true;
}

// 베란다 수납장: what is stored at home. Last year's bag snack carries its warning until the dates are read
// (prep '집 안 대비'), and one can be eaten here.
export async function stockLook(ctx) {
  const G = ctx.G;
  const tag = (id, v) => id === 'old_bars' ? (G.know.expiryChecked ? ' (기한 지남)' : ' (작년 가방에서 나온 것 · 기한 확인 필요)')
    : CATALOG[id]?.needsCheck === 'batteries' && v.charge === 0 ? ' (건전지 다 닳음)' : CATALOG[id]?.needsCheck && !v.checked ? ' (아직 점검 안 함)' : '';
  const lines = Object.entries(G.items).filter(([id, v]) => v.home > 0 && CATALOG[id] && !(CATALOG[id].fixture && G.know.curtain))
    .map(([id, v]) => `${CATALOG[id].name} ${v.home}${CATALOG[id].unit ?? ''}${tag(id, v)}`);
  const old = (G.items.old_bars?.home ?? 0) > 0;
  const pick = await ctx.ui.talk({ speaker: '베란다 수납장', role: '집 비축품', lines: [lines.join(' · ') || '비어 있다.'],
    choices: old ? [{ id: 'eat', label: '작년 건빵을 하나 먹어 본다' }, { id: 'close', label: '닫는다' }] : null });
  if (pick === 'eat') eatOldBars(ctx);
}

// The family photo album, looked at on a quiet night at home or in the shelter: a little calmer, once a run.
export async function lookAlbum(ctx) {
  const G = ctx.G, e = G.items.photo_album;
  if (!e || e.home + e.bag <= 0) return false;
  const first = !G.know.albumSeen;
  await ctx.ui.talk({ speaker: '가족사진 앨범', role: e.bag > 0 && ctx.engine.level?.id !== 'apartment' ? '가방에서 꺼내' : '책장에서 꺼내',
    lines: ['대구 부모님 집 마당, 졸업식, 회사 첫 출근 날. 한 장씩 넘겨 본다.', first ? '마음이 조금 가라앉는다. 다들 무사할 거라고 믿기로 했다.' : '오늘은 한 장만 보고 덮었다.'] });
  if (first) { G.know.albumSeen = true; ctx.stat('tension', -5); recordEvent(G, 'album', {}); }
  return true;
}

// Day ledger (game/ledger.js) before a time skip: three picks (water, meals, the power hour), then the days
// are played out. Returns the result lines for the time-skip text. Uses ctx.ui.talk: wrap it in ctx.modal.
const litres = (G, from) => from.reduce((s, k) => s + (G.items.water_2l?.[k] ?? 0) * 2 + (G.items.water_500?.[k] ?? 0) * 0.5, 0);
const meals = (G, from) => ['canned', 'rice_instant', 'bars', 'ramen'].reduce((s, id) => s + from.reduce((t, k) => t + (G.items[id]?.[k] ?? 0), 0), 0);
export async function dayLedger(ctx, days, place = 'home') {
  const G = ctx.G, from = place === 'home' ? ['home', 'bag'] : ['bag'], at = place === 'home' ? '집' : '임시 거처';
  const opts = (k) => PICKS[k].map(p => ({ id: p.id, label: p.label }));
  const role = `앞으로 ${days}일 · ${at}`;
  const water = await ctx.ui.talk({ speaker: '하루 장부', role, lines: [
    `앞으로 ${days}일을 어떻게 나눠 쓸까? 물, 끼니, 전기 쓰는 시간을 정한다.`,
    place === 'home' ? `마실 물은 지금 약 ${litres(G, from)}L. 모자라면 급수소에 다녀와야 한다.` : '임시 거처에서는 마실 물을 받을 수 있다.',
    fact('water_sphere').text, '하루에 물을 얼마나 마실까?'], choices: opts('water') }) ?? 2;
  const stove = place === 'home' && (G.items.gas_stove?.home ?? 0) + (G.items.gas_stove?.bag ?? 0) > 0;
  const food = await ctx.ui.talk({ speaker: '하루 장부', role, lines: [
    `먹을 것은 ${meals(G, from)}개 남았다.`, place === 'shelter' ? '임시 거처에서는 하루 두 번 배식이 있다. 세 번째 끼니는 가방에서 꺼내 먹는다.' : stove ? '휴대용 가스레인지가 있어 라면을 끓일 수 있다.' : '조리 없이 먹을 수 있는 것부터 먹게 된다.',
    '하루에 몇 끼를 먹을까?'], choices: opts('meals') }) ?? 2;
  const radioOk = !!G.items.radio?.checked, bank = (G.items.powerbank?.home ?? 0) + (G.items.powerbank?.bag ?? 0) > 0;
  const power = await ctx.ui.talk({ speaker: '하루 장부', role, lines: ['전기가 들어오는 하루 한두 시간에는 무엇을 할까?'],
    choices: PICKS.power.map(p => ({ id: p.id, label: p.label,
      note: p.id === 'radio' ? (radioOk ? `건전지가 닳는다 (지금 ${G.items.radio.charge ?? 100}%)` : '라디오가 켜지지 않는다') : p.id === 'powerbank' ? (bank ? '휴대폰 대신 보조배터리를 채워 둔다 · 밖에서 꺼질 때 대비' : '보조배터리가 없다') : p.id === 'phone' ? `지금 ${G.phone.battery}%` : '' })) }) ?? 'phone';
  const res = simulateDays(G, { water, meals: food, power }, days, { place });
  applyLedger(G, res);
  ctx.refresh();
  return res.lines;
}
export { has };
