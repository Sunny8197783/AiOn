// 〈경보 이전〉 shell: boot, settings, save/continue, the chapter runner with soft-fail retries, global keys.
import { createEngine } from './engine/core.js';
import { createActions } from './engine/actions.js';
import { shop3d, bag3d } from './ui/grab3d.js';
import { createDanger } from './story/danger.js';
import { createEcho } from './story/echo.js';
import { createHud } from './ui/hud.js';
import { talk, card, timeSkip } from './ui/dialog.js';
import { landing, pause, settings as settingsMenu, about } from './ui/menu.js';
import { failCard, chapterCard, ending as endingCard } from './ui/debrief.js';
import { phone, emergencyAlert } from './ui/phone.js';
import { notebook, inventory } from './ui/notebook.js';
import { planner } from './ui/planner.js';
import { radio } from './ui/radio.js';
import { extinguisher } from './ui/extinguisher.js';
import { docs } from './ui/docs.js';
import { routemap } from './ui/routemap.js';
import { createAudio } from './audio.js';
import { createGame, save, load, checkpoint, restore, bagLoad } from './game/state.js';
import { failDebrief } from './game/debrief.js';
import { SOURCES } from './game/facts.js';
import { CATALOG, BAG_LIMIT } from './game/items.js';
import { NODES, dist } from './game/townmap.js';
import { createRuntime, Fail } from './story/runtime.js';
import { lightSource, radioScene, currentBroadcast } from './story/common.js';
import { groundAt } from './engine/controls.js';
import { prologue } from './story/prologue.js';
import { prep } from './story/prep.js';
import { dday } from './story/dday.js';
import { endure } from './story/endure.js';
import { crossroads, evacuation, ending } from './story/evac.js';

const CHAPTERS = [['prologue', prologue], ['prep', prep], ['dday', dday], ['endure', endure], ['crossroads', crossroads], ['evac', evacuation], ['ending', ending]];
const SAVE_KEY = 'gyeongbo.save.v2', SETTINGS_KEY = 'gyeongbo.settings.v1';
const LEVELS = Object.fromEntries([['town', 'Town'], ['apartment', 'Apartment'], ['mart', 'Mart'], ['store', 'Store'], ['office', 'Office'], ['shelter', 'Shelter']]
  .map(([id, name]) => [id, async (kit, people, fx) => (await import(`./levels/${id}.js`))[`build${name}`](kit, people, fx)]));

// Browser storage is a convenience: every access may throw (private mode), and the game runs without it.
const store = {
  get(k) { try { return localStorage.getItem(k); } catch { return null; } },
  set(k, v) { try { localStorage.setItem(k, v); } catch { /* not saved */ } },
};

const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
const settings = { quality: 'high', volume: 0.7, mute: false, captions: true, reduceMotion: reduced, textScale: 1, view: 'third',
  sensitivity: 1, minimap: true, volAmbience: 1, volEffects: 1, volUi: 1, profile: 'none', ...safeParse(store.get(SETTINGS_KEY)) };
function safeParse(s) { try { return JSON.parse(s) || {}; } catch { return {}; } }

// Experience modes (체험 모드): how the body moves and what the senses get. speed multiplies the story's speed.
const PROFILES = {
  none: { run: true, jump: true, eye: 1.62, stairs: true, speed: 1 },
  mobility: { run: false, jump: false, eye: 1.62, stairs: true, speed: 0.62 },   // 보행 장애: no running, slow
  wheelchair: { run: false, jump: false, eye: 1.18, stairs: false, speed: 0.85 }, // seated eye height, ramps only
  lowvision: { run: true, jump: true, eye: 1.62, stairs: true, speed: 0.8 },      // 저시력: blurred world, spoken prompts
  deaf: { run: true, jump: true, eye: 1.62, stairs: true, speed: 1 },             // 청각 장애: no sound, captions and cues
  guardian: { run: false, jump: false, eye: 1.62, stairs: true, speed: 0.72 },    // 지체 장애: a guardian walks along
};
const profile = () => PROFILES[settings.profile] ? settings.profile : 'none';
function hasLeg() { return !!G?.injury?.includes('leg'); }
function applySettings() {
  const p = PROFILES[profile()];
  document.documentElement.style.setProperty('--text-scale', String(settings.textScale));
  document.documentElement.classList.toggle('reduce-motion', !!settings.reduceMotion);
  document.documentElement.classList.toggle('sim-lowvision', profile() === 'lowvision');
  audio.setVolume(settings.volume); audio.setMute(settings.mute || profile() === 'deaf');
  for (const [bus, key] of [['ambience', 'volAmbience'], ['effects', 'volEffects'], ['ui', 'volUi']]) audio.setBus(bus, settings[key] ?? 1);
  engine.setView(settings.view);
  engine.setQuality(settings.quality); engine.setReduceMotion(settings.reduceMotion);
  engine.setSensitivity(settings.sensitivity);
  engine.setProfile({ ...p, run: p.run && !hasLeg() }); // a broken leg ends running for the rest of the run
  ctx.profileSpeed = p.speed; if (G) ctx.refresh();
  store.set(SETTINGS_KEY, JSON.stringify(settings));
}

const DEBUG = new URLSearchParams(location.search).has('debug');
// ?debug keeps frames coming while the page is hidden (an embedded pane can stop rAF yet still report
// "visible"), so automated play-throughs work in the background.
// ponytail: MessageChannel spin that stands in when rAF is 50 ms late, debug only.
if (DEBUG) {
  const raf = requestAnimationFrame.bind(window), ch = new MessageChannel(), q = [];
  ch.port1.onmessage = () => {
    const t = performance.now();
    for (let i = q.length - 1; i >= 0; i--) if (q[i].ran) q.splice(i, 1); else if (t - q[i].at > 50) { q.splice(i, 1)[0].run(t); }
    if (q.length) ch.port2.postMessage(0);
  };
  window.requestAnimationFrame = (cb) => {
    const f = { at: performance.now(), ran: false, run(t) { if (!f.ran) { f.ran = true; cb(t); } } };
    q.push(f); raf((t) => f.run(t)); ch.port2.postMessage(0);
    return 0;
  };
}

const audio = createAudio();
const engine = createEngine({ canvas: document.getElementById('world'), quality: settings.quality, reduceMotion: settings.reduceMotion, levels: LEVELS,
  onQualityChange: (q) => { settings.quality = q; store.set(SETTINGS_KEY, JSON.stringify(settings)); } });
const hud = createHud(document.getElementById('ui'));

// 청각 장애 mode: the sound is off, so loud sounds also arrive as on-screen cues.
const SOUND_CUES = { boom: '[소리] 쾅— 큰 폭발음. 바닥이 울린다.', phoneAlert: '[진동] 휴대폰이 울리며 떨린다.', siren: '[소리] 사이렌이 울리기 시작했다.' };
for (const [fn, text] of Object.entries(SOUND_CUES)) {
  const raw = audio[fn];
  audio[fn] = (...a) => { raw(...a); if (profile() === 'deaf' && (fn !== 'siren' || a[0])) hud.toast(text, { label: 'site', ms: 4200 }); };
}
// 저시력 mode: what is in front of you and the captions are read aloud (Korean speech, when the browser has it).
function speak(text) {
  if (profile() !== 'lowvision' || !globalThis.speechSynthesis) return;
  try { speechSynthesis.cancel(); const u = new SpeechSynthesisUtterance(String(text)); u.lang = 'ko-KR'; speechSynthesis.speak(u); } catch { /* no voice */ }
}
{ const rawCaption = hud.caption; hud.caption = (text, o) => { speak(text); return rawCaption(text, o); }; }
engine.onNearest((p) => { if (p) speak(p.label); });
// Shopping and packing happen by hand in the 3D scene (ui/grab3d.js).
const ui = { talk, card, timeSkip, phone, emergencyAlert, notebook, inventory, planner, shop: shop3d(engine), bag: bag3d(engine), radio, extinguisher, docs, routemap, failCard, chapterCard, ending: endingCard };

let G = null;
const progress = { index: 0 };
// Runtime state a checkpoint needs besides G; it is saved with G so a reload resumes the same world.
// Canvases (the office dashboard) are redrawn by their chapter, so they are left out.
const plain = (v) => JSON.parse(JSON.stringify(v, (k, x) => (x instanceof HTMLCanvasElement ? undefined : x)));
const snapshot = () => plain({ levelStates: ctx.levelStates, power: ctx.power, outdoorEnv: ctx.outdoorEnv, indoorEnv: ctx.indoorEnv, lightSource: ctx.lightSource ?? null, trail: ctx.trail || [], seoyeonAsk: ctx.seoyeonAsk ?? null });
// A saved snapshot comes from browser storage: keep only known keys with sane values.
const ENVS = new Set(['day', 'afternoon', 'dusk', 'night', 'blackout', 'interior', 'interiorNight', 'interiorDark', 'underground']);
function readSnap(s) {
  const o = s && typeof s === 'object' ? plain(s) : {}, out = { levelStates: {} };
  if (o.levelStates && typeof o.levelStates === 'object') for (const [k, v] of Object.entries(o.levelStates)) if (LEVELS[k] && v && typeof v === 'object') out.levelStates[k] = v;
  if (typeof o.power === 'boolean') out.power = o.power;
  for (const k of ['outdoorEnv', 'indoorEnv']) if (ENVS.has(o[k])) out[k] = o[k];
  if (['flashlight', 'phone'].includes(o.lightSource)) out.lightSource = o.lightSource;
  if (Array.isArray(o.trail)) out.trail = o.trail.filter(p => Number.isFinite(p?.x) && Number.isFinite(p?.z)).slice(-2000);
  if (['yes', 'no'].includes(o.seoyeonAsk)) out.seoyeonAsk = o.seoyeonAsk;
  return out;
}
const saveNow = () => store.set(SAVE_KEY, JSON.stringify({ index: progress.index, G: JSON.parse(save(G)), snap: ctx.snap }));
const persist = {
  checkpoint(key) { checkpoint(G, key); ctx.snap = snapshot(); saveNow(); },
};
const ctx = createRuntime({ engine, hud, ui, audio, settings, persist, getG: () => G, setG: (g) => { G = g; } });

// The 서연 interlude swaps who you are; the engine keeps one controller, so swap the model it drives.
ctx.setPlayer = (preset) => engine.setPlayerPreset?.(preset);

applySettings();
// ?debug exposes the game for automated play-throughs (teleport to a point, read state).
if (DEBUG) window.__game = { ctx, engine, hud, get G() { return G; }, get danger() { return danger; } };

// ---- the body: falls, fights, thrown things (engine/actions.js) ----
const OUCH = ['“아악! 왜 이래요!”', '“뭐 하는 거야!”', '“사람 살려요!”', '“그만해요, 제발!”', '“미쳤어?!”'];
let itemLabel = null;
function hurt(n, why) {
  if (!G || !(n > 0)) return;
  ctx.deathCause = why; ctx.stat('health', -n); hud.hurt(n / 40);
}
const actions = createActions(engine, {
  onHit(p, how, ko) {
    if (!G || danger.isHostile(p) || how === 'debris') return; // self-defence against a looter is not counted
    G.karma = (G.karma ?? 0) - (ko ? 3 : 1);
    ctx.event('violence', { how, ko });
    const who = Object.entries(engine.level?.npcs || {}).find(([, v]) => v === p)?.[0];
    if (who && G.rel[who]) G.rel[who].trust -= 2;
    ctx.toast(ko ? '상대가 바닥에 쓰러졌다.' : OUCH[Math.floor(Math.random() * OUCH.length)], 'site', 2600);
  },
  onHurt: hurt,
  onSound: (n) => audio.hit(n),
  onNear(label) { itemLabel = label; if (!engine.nearest) hud.prompt(label); },
});
engine.onNearest((p) => { if (!p && itemLabel) hud.prompt(itemLabel); });
engine.onLand(({ drop }) => {
  audio.hit('land');
  if (!G || drop < 3.2) return;
  if (drop > 5.5 && !hasLeg()) {
    (G.injury ||= []).push('leg'); applySettings(); ctx.event('injury', { part: 'leg', drop: Math.round(drop) });
    ctx.toast('착지하는 순간 다리가 꺾였다. 뼈가 부러진 것 같다 — 이제 달릴 수 없다.', 'self', 6500);
  }
  hurt(drop > 10 ? 200 : Math.round((drop - 3) * 15), 'fall');
});
const danger = createDanger({ ctx, actions, hurt });
const echo = createEcho({ engine, ctx, store });
// X: the first-aid kit (in the bag, or at home) patches you up; a broken leg stays broken.
function firstAid() {
  const e = G.items.first_aid, atHome = engine.level?.id === 'apartment';
  if (!e || !(e.bag > 0 || (atHome && e.home > 0))) return void ctx.toast('쓸 수 있는 구급함이 없다.');
  if (G.stats.health >= 95) return void ctx.toast('지금은 처치할 상처가 없다.');
  if (e.bag > 0) e.bag--; else e.home--;
  e.used++; ctx.stat('health', 35); ctx.event('first_aid', {}); ctx.toast('상처를 소독하고 붕대를 감았다. 조금 낫다.', 'self');
}
{ const goto = ctx.goto; ctx.goto = (...a) => { actions.drop(); return goto(...a); }; }

// Global keys while roaming. Menus open only during free roam, so no scripted step runs behind them.
let flashOn = false, flashT = 0;
engine.onKey(async ({ code }) => {
  if (!G || !engine.controlsEnabled) return;
  if (code === 'KeyF') {
    if (!flashOn && (!ctx.lightSource || (ctx.lightSource === 'phone' && G.phone.battery <= 0))) return void ctx.toast('켤 수 있는 불빛이 없다.');
    flashOn = !flashOn; engine.player.setFlashlight(flashOn);
  } else if (code === 'KeyV') { settings.view = settings.view === 'first' ? 'third' : 'first'; store.set(SETTINGS_KEY, JSON.stringify(settings)); }
  else if (!ctx.roaming) return;
  else if (code === 'Tab') await ctx.modal(() => notebook(G.info));
  else if (code === 'KeyI') await ctx.modal(() => inventory({ home: G.items, bag: G.items, catalog: CATALOG, load: bagLoad(G), limit: BAG_LIMIT }));
  else if (code === 'KeyH') await ctx.modal(() => controlsCard());
  else if (code === 'KeyX') firstAid();
  else if (code === 'KeyB') await echo.chalk();
  else if (code === 'KeyP') await ctx.modal(() => openPhone());
  else if (code === 'KeyM') await ctx.modal(() => routemap({ mode: 'view', from: nearestNode(), to: 'shelter', options: [], closures: knownClosures(), gates: {}, known: { closures: [] }, playerAt: engine.level?.id === 'town' ? { x: engine.player.x, z: engine.player.z } : null }));
  else if (code === 'Escape') await openPause();
});
// The map shows closures in force now, and only once the player has learned of them this phase.
function knownClosures() {
  const knows = G.chapter === 'evac' ? G.know.evacClosuresKnown : G.know.closureSeen || G.know.closureHeard;
  return knows ? ctx.levelStates.town?.closures || [] : [];
}
// Per-scene state that must not leak into a retry or the next run.
function resetTransient() {
  ctx.speedOverride = null; engine.player.carrying = null;
  flashOn = false; engine.player.setFlashlight(false);
  ctx.setPlayer('jeongmin'); hud.setCinematic(false); engine.setControls(true);
  actions.drop(); danger.reset(); ctx.deathCause = null; applySettings();
}
function resetRun(snap) {
  for (const k of ['lightSource', 'seoyeonAsk', 'trail', 'snap']) delete ctx[k];
  Object.assign(ctx, { levelStates: {}, power: true, outdoorEnv: 'day', indoorEnv: 'interior', rate: 0 });
  if (snap) { ctx.snap = readSnap(snap); Object.assign(ctx, readSnap(ctx.snap)); }
  // Saves from before the snapshot existed: the light in hand follows from G.
  if (!ctx.lightSource && G.events.some(e => e.type === 'blackout')) ctx.lightSource = lightSource(ctx);
  resetTransient();
}
engine.onFrame((dt) => {
  if (!flashOn || ctx.lightSource !== 'phone' || !G) return;
  if ((flashT += dt) > 20) { flashT = 0; G.phone.battery = Math.max(0, G.phone.battery - 1); hud.setBattery(G.phone.battery); if (!G.phone.battery) { flashOn = false; engine.player.setFlashlight(false); ctx.toast('휴대폰이 꺼졌다.'); } }
});
function nearestNode() {
  if (engine.level?.id !== 'town') return 'home';
  const p = { x: engine.player.x, z: engine.player.z };
  return Object.entries(NODES).sort((a, b) => dist(a[1], p) - dist(b[1], p))[0][0];
}

// ---- controls card (start of a new game, and H while roaming) ----
const KEYS = [['W A S D · 방향키', '이동'], ['마우스', '둘러보기 — 화면을 한 번 클릭하면 마우스만 움직여도 시점이 돕니다 (Esc로 커서 되찾기)'],
  ['Shift', '달리기 (기력이 줄어듭니다. 가방이 무거우면 더 빨리 지칩니다)'], ['Space', '점프 · 낮은 담/차/난간 앞에서 누르면 넘거나 올라감'],
  ['클릭 · Q', '주먹 (손에 든 게 있으면 던지기)'], ['G', '바닥의 돌·병 줍기 / 던지기'], ['C', '엎드리기/웅크리기 (폭발·파편 피해가 줄어듭니다. Space로 일어서기)'], ['X', '구급함 쓰기'],
  ['B', '분필로 바닥에 적기 — 다음 회차의 내가 보게 됩니다'], ['E · Enter', '살피기 · 대화 · 사용'],
  ['F', '손전등 켜기/끄기 (정전 후)'], ['V', '1인칭 / 3인칭 전환'], ['P', '휴대폰 (문자 · FM 라디오)'], ['Tab', '수첩'], ['I', '소지품'], ['M', '지도'], ['H', '이 조작법 다시 보기'], ['Esc', '일시정지 · 설정']];
const controlsCard = () => card({ kind: 'info', title: '조작법', meta: '언제든 H 키로 다시 볼 수 있습니다',
  body: KEYS.map(([k, t]) => `${k} — ${t}`), actions: [{ id: 'ok', label: '확인', primary: true }] });

// ---- who is who: before the story starts ----
async function intro() {
  await card({ kind: 'info', title: '주인공 · 정민', meta: '31세 · 한결물류 연무센터 운영팀 (배송 운영 담당)',
    body: ['연무동 한빛아파트 103동 302호에 혼자 산다. 회사는 걸어서 10분 거리의 물류센터.', '꼼꼼하지만 비상용품은 "언젠가 사야지" 하고 미뤄 둔 평범한 직장인. 손전등은 있는데 건전지가 없다.',
      '이 이야기는 전쟁 직전 사흘과 그 뒤 열이틀 동안, 정민이 무엇을 확인하고 누구와 함께 버티는지를 따라갑니다.'],
    actions: [{ id: 'ok', label: '주변 사람들', primary: true }] });
  await card({ kind: 'info', title: '정민의 주변 사람들', meta: '나이와 인물은 모두 가상 설정입니다',
    body: ['오서연 (36세) · 옆집 303호. 보행 보조기를 쓴다. 평지와 완만한 경사로는 혼자 다니지만 계단은 어렵다.',
      '박영수 소장 (58세) · 한빛아파트 관리사무소장. 1층 창구에 있다. 단지 사정을 가장 잘 안다.',
      '이수아 (24세) · 단지 앞 바로편의점 직원. 본가가 지방이다.', '윤 팀장 · 정민의 회사 팀장. 확인된 것만 말하라고 늘 강조한다.',
      '304호 어르신 · 같은 층에 혼자 사는 노인.'],
    actions: [{ id: 'ok', label: '다음', primary: true }] });
  const pick = await card({ kind: 'info', title: '체험 모드', meta: '설정(Esc)에서 언제든 바꿀 수 있습니다',
    body: ['같은 재난도 몸과 감각에 따라 전혀 다른 길이 됩니다. 정민의 조건을 골라 보세요.',
      '보행 장애: 달릴 수 없고 걸음이 느립니다. · 휠체어: 앉은 눈높이, 계단은 도움 없이 못 지나고 경사로만 씁니다.',
      '저시력: 앞이 흐릿하게 보이고, 눈앞의 대상과 자막을 소리로 읽어 줍니다. · 청각 장애: 소리가 들리지 않고 자막과 화면 신호로만 알 수 있습니다.',
      '지체 장애(보호자 동반): 보호자가 곁에서 함께 걷습니다. 달릴 수 없습니다.'],
    actions: [{ id: 'none', label: '기본', primary: true }, { id: 'mobility', label: '보행 장애' }, { id: 'wheelchair', label: '휠체어' },
      { id: 'lowvision', label: '저시력' }, { id: 'deaf', label: '청각 장애' }, { id: 'guardian', label: '보호자 동반' }] });
  if (PROFILES[pick]) { settings.profile = pick; if (pick === 'deaf') settings.captions = true; applySettings(); }
  await controlsCard();
}

// ---- P: the phone (recent official texts, FM radio app) ----
async function openPhone() {
  if (G.phone.battery <= 0) return void ctx.toast('휴대폰이 꺼져 있다.');
  const messages = G.info.filter((i) => i.label === 'official').slice(-4).map((i) => ({ from: '알림', kind: 'safety', time: '', text: i.text }));
  const pick = await phone({ battery: G.phone.battery, signal: G.phone.signal || 'ok', messages, actions: [{ id: 'radio', label: 'FM 라디오 앱' }, { id: 'close', label: '닫기' }] });
  if (pick === 'radio') await radioScene(ctx, currentBroadcast(G), { device: 'phone', title: '휴대폰' });
}

// ---- per frame: stamina, compass and minimap, the pointer-lock hint ----
const lockHint = Object.assign(document.createElement('p'), { className: 'lock-hint', hidden: true, textContent: '화면을 한 번 클릭하면 마우스만 움직여 둘러볼 수 있습니다' });
document.body.append(lockHint);
const touchOnly = matchMedia('(hover: none)').matches;
let mapT = 0;
engine.onFrame((dt) => {
  if (!G) { lockHint.hidden = true; return; }
  const p = engine.player;
  hud.setStamina(p.stamina, p.winded);
  lockHint.hidden = touchOnly || !ctx.roaming || !engine.controlsEnabled || engine.pointerLocked;
  if ((mapT -= dt) > 0) return;
  mapT = 0.066;
  hud.setCompass((-p.yaw * 180) / Math.PI);
  const L = engine.level;
  hud.setMap(settings.minimap === false || !L ? null : { x: p.x, y: p.y, z: p.z, yaw: p.yaw, walk: L.walk, solids: L.solids, points: L.points, scale: L.id === 'town' ? 2 : 9 });
});

// ---- wheelchair: a stair flight is a wall; ask for help to be carried across it ----
let helpBusy = false, helpedAt = -1e9;
const steep = (w) => w.ramp && Math.abs(w.ramp.y1 - w.ramp.y0) / ((w.ramp.axis === 'x' ? w.x1 - w.x0 : w.z1 - w.z0) || 1) > 0.2;
engine.onBlocked(async (b) => {
  if (!G || !ctx.roaming || helpBusy || profile() !== 'wheelchair') return;
  const w = (engine.level?.walk || []).find((z) => steep(z) && b.toX >= z.x0 && b.toX <= z.x1 && b.toZ >= z.z0 && b.toZ <= z.z1);
  if (!w) return;
  helpBusy = true;
  try {
    const recent = performance.now() - helpedAt < 90000; // the same helper keeps going down the next flights
    if (!recent) {
      const pick = await ctx.modal(() => talk({ speaker: '계단', role: '휠체어로는 혼자 이용할 수 없다', lines: ['계단 앞이다. 휠체어로는 오르내릴 수 없다.', '주변 사람에게 도움을 청할까? 시간이 걸린다. 경사로나 엘리베이터가 있다면 그쪽이 낫다.'],
        choices: [{ id: 'help', label: '도움을 청한다' }, { id: 'no', label: '다른 길을 찾는다' }] }));
      if (pick !== 'help') return;
    }
    const r = w.ramp, alongX = r.axis === 'x', lo = alongX ? w.x0 : w.z0, hi = alongX ? w.x1 : w.z1, cur = alongX ? b.x : b.z;
    const up = Math.abs(cur - lo) < Math.abs(cur - hi), end = up ? hi + 0.45 : lo - 0.45, y = up ? r.y1 : r.y0, mid = alongX ? (w.z0 + w.z1) / 2 : (w.x0 + w.x1) / 2;
    p_teleport(alongX ? { x: end, y, z: mid } : { x: mid, y, z: end });
    helpedAt = performance.now(); ctx.pass(2);
    ctx.toast(recent ? '도움을 받아 다음 계단도 옮겼다.' : '이웃 두 사람이 휠체어를 함께 들어 계단을 옮겨 주었다.', 'neighbor');
  } finally { helpBusy = false; }
});
const p_teleport = (s) => { const yaw = engine.player.yaw; engine.player.teleport({ ...s, yaw }); };

// ---- 지체 장애 mode: a guardian walks a step behind, to the right ----
let buddy = null, buddyLevel = null, buddyAnim = '';
engine.onFrame((dt) => {
  const on = !!G && profile() === 'guardian' && !!engine.level;
  if (!on) { if (buddy) buddy.group.visible = false; return; }
  if (!buddy) { try { buddy = engine.people.createPerson({ preset: 'crowd', seed: 23 }); engine.scene.add(buddy.group); } catch (err) { console.error('[app] guardian model failed', err); return; } }
  const p = engine.player, L = engine.level, g = buddy.group, s = Math.sin(p.yaw), c = Math.cos(p.yaw);
  const tx = p.x + s * 1.3 + c * 0.6, tz = p.z + c * 1.3 - s * 0.6; // behind (+sin, +cos) and right (cos, -sin)
  g.visible = true;
  const dx = tx - g.position.x, dz = tz - g.position.z, d = Math.hypot(dx, dz);
  let moving = false;
  if (L !== buddyLevel || d > 8) { g.position.set(tx, p.y, tz); buddyLevel = L; }
  else if (d > 0.25) { const v = Math.min(d * 2.4, 4.2) * dt; g.position.x += (dx / d) * v; g.position.z += (dz / d) * v; g.rotation.y = Math.atan2(-dx, -dz); moving = true; }
  const ground = groundAt(L.walk, g.position.x, g.position.z, g.position.y + 0.3, false, 1.5);
  g.position.y += ((ground ? ground.y : p.y) - g.position.y) * Math.min(1, dt * 12);
  const anim = moving ? 'walk' : 'idle';
  if (anim !== buddyAnim) { buddyAnim = anim; buddy.setAnim?.(anim); }
  buddy.setSpeed?.(moving ? Math.min(d * 2.4, 4.2) : 0);
  try { buddy.update?.(dt); } catch { /* model stand-in */ }
});

async function openPause() {
  const pick = await ctx.modal(() => pause());
  if (pick === 'settings') { Object.assign(settings, await ctx.modal(() => settingsMenu({ ...settings }))); applySettings(); }
  else if (pick === 'save') { if (G?.checkpoint) saveNow(); ctx.toast('마지막 체크포인트를 저장했습니다.'); }
  else if (pick === 'menu') location.reload();
}

async function run(start, resumeKey) {
  for (let i = start; i < CHAPTERS.length; i++) {
    const [id, fn] = CHAPTERS[i];
    progress.index = i;
    ctx.resume = resumeKey ?? null;
    hud.setObjective(null);
    if (!resumeKey) persist.checkpoint(id);
    resumeKey = null;
    for (;;) {
      try { const r = await fn(ctx); if (id === 'ending') return r; break; }
      catch (err) {
        if (!(err instanceof Fail)) throw err;
        if (err.type === 'dead') echo.death(err.detail?.why);
        engine.setControls(false); audio.siren(false); audio.fire(0); hud.setCinematic(false);
        const d = failDebrief(G, err.type, err.detail);
        const pick = await failCard({ ...d, sources: (d.sourceIds || []).map(s => SOURCES.find(x => x.id === s)).filter(Boolean) });
        if (pick !== 'retry') { engine.setControls(true); return 'menu'; }
        const key = G.checkpoint.key;
        restore(G);
        Object.assign(ctx, readSnap(ctx.snap));
        resetTransient();
        ctx.resume = key === id ? null : key;
      }
    }
  }
}

async function boot() {
  document.getElementById('loading').hidden = true;
  for (;;) {
    const saved = safeParse(store.get(SAVE_KEY));
    const pick = await landing({ hasSave: !!saved.G });
    audio.unlock();
    if (pick === 'settings') { Object.assign(settings, await settingsMenu({ ...settings })); applySettings(); continue; }
    if (pick === 'about') { await about(SOURCES); continue; }
    let start = 0, resume = null;
    if (pick === 'continue' && saved.G) {
      try { G = load(saved.G); } catch (e) { console.error('save rejected', e); G = null; }
    }
    if (G?.checkpoint) { const key = G.checkpoint.key; restore(G); start = Number.isInteger(saved.index) && saved.index >= 0 && saved.index < CHAPTERS.length ? saved.index : 0; resume = CHAPTERS[start][0] === key ? null : key; }
    else G = createGame({ seed: (crypto.getRandomValues(new Uint32Array(1))[0]) >>> 1 });
    const fresh = !G.checkpoint;
    if (fresh) echo.begin(); else echo.resume();
    resetRun(G.checkpoint && saved.snap);
    engine.setView(settings.view);
    if (fresh) { engine.setControls(false); await intro(); engine.setControls(true); }
    hud.show();
    const result = await run(start, resume);
    hud.hide();
    if (result === 'restart') store.set(SAVE_KEY, '');
    G = null;
  }
}

boot().catch((err) => {
  console.error(err);
  const el = document.getElementById('loading');
  el.hidden = false;
  document.getElementById('loading-text').textContent = '문제가 생겨 게임을 계속할 수 없습니다. 새로고침해 주세요.';
});
