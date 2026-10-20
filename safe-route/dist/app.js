// 〈경보 이전〉 shell: boot, settings, save/continue, the chapter runner with soft-fail retries, global keys.
import { createEngine } from './engine/core.js';
import { createHud } from './ui/hud.js';
import { talk, card, timeSkip } from './ui/dialog.js';
import { landing, pause, settings as settingsMenu, about } from './ui/menu.js';
import { failCard, chapterCard, ending as endingCard } from './ui/debrief.js';
import { phone, emergencyAlert } from './ui/phone.js';
import { notebook, inventory } from './ui/notebook.js';
import { planner } from './ui/planner.js';
import { shop } from './ui/shop.js';
import { bag } from './ui/bag.js';
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
const settings = { quality: 'high', volume: 0.7, mute: false, captions: true, reduceMotion: reduced, textScale: 1, view: 'third', ...safeParse(store.get(SETTINGS_KEY)) };
function safeParse(s) { try { return JSON.parse(s) || {}; } catch { return {}; } }
function applySettings() {
  document.documentElement.style.setProperty('--text-scale', String(settings.textScale));
  document.documentElement.classList.toggle('reduce-motion', !!settings.reduceMotion);
  audio.setVolume(settings.volume); audio.setMute(settings.mute);
  engine.setQuality(settings.quality); engine.setReduceMotion(settings.reduceMotion);
  store.set(SETTINGS_KEY, JSON.stringify(settings));
}

const audio = createAudio();
const engine = createEngine({ canvas: document.getElementById('world'), quality: settings.quality, reduceMotion: settings.reduceMotion, levels: LEVELS,
  onQualityChange: (q) => { settings.quality = q; store.set(SETTINGS_KEY, JSON.stringify(settings)); } });
const hud = createHud(document.getElementById('ui'));
const ui = { talk, card, timeSkip, phone, emergencyAlert, notebook, inventory, planner, shop, bag, radio, extinguisher, docs, routemap, failCard, chapterCard, ending: endingCard };

let G = null;
const progress = { index: 0 };
const persist = {
  checkpoint(key) {
    checkpoint(G, key);
    ctx.snap = { levelStates: Object.fromEntries(Object.entries(ctx.levelStates).map(([k, v]) => [k, { ...v }])), power: ctx.power, outdoorEnv: ctx.outdoorEnv, indoorEnv: ctx.indoorEnv, lightSource: ctx.lightSource, trail: [...(ctx.trail || [])], seoyeonAsk: ctx.seoyeonAsk };
    store.set(SAVE_KEY, JSON.stringify({ index: progress.index, G: JSON.parse(save(G)) }));
  },
};
const ctx = createRuntime({ engine, hud, ui, audio, settings, persist, getG: () => G, setG: (g) => { G = g; } });

// The 서연 interlude swaps who you are; the engine keeps one controller, so swap the model it drives.
ctx.setPlayer = (preset) => engine.setPlayerPreset?.(preset);

applySettings();
// ?debug exposes the game for automated play-throughs (teleport to a point, read state).
if (new URLSearchParams(location.search).has('debug')) window.__game = { ctx, engine, hud, get G() { return G; } };

// Global keys while roaming.
let flashOn = false, flashT = 0;
engine.onKey(async ({ code }) => {
  if (!G || !engine.controlsEnabled) return;
  if (code === 'KeyF') {
    if (!ctx.lightSource) return void ctx.toast('켤 수 있는 불빛이 없다.');
    flashOn = !flashOn; engine.player.setFlashlight(flashOn);
  } else if (code === 'Tab') await ctx.modal(() => notebook(G.info));
  else if (code === 'KeyI') await ctx.modal(() => inventory({ home: G.items, bag: G.items, catalog: CATALOG, load: bagLoad(G), limit: BAG_LIMIT }));
  else if (code === 'KeyM') await ctx.modal(() => routemap({ mode: 'view', from: nearestNode(), to: 'shelter', options: [], closures: G.know.closureSeen || G.know.closureHeard ? G.cond.closures.dday : [], gates: {}, known: { closures: [] }, playerAt: engine.level?.id === 'town' ? { x: engine.player.x, z: engine.player.z } : null }));
  else if (code === 'Escape') await openPause();
});
engine.onFrame((dt) => {
  if (!flashOn || ctx.lightSource !== 'phone' || !G) return;
  if ((flashT += dt) > 20) { flashT = 0; G.phone.battery = Math.max(0, G.phone.battery - 1); hud.setBattery(G.phone.battery); if (!G.phone.battery) { flashOn = false; engine.player.setFlashlight(false); ctx.toast('휴대폰이 꺼졌다.'); } }
});
function nearestNode() {
  if (engine.level?.id !== 'town') return 'home';
  const p = { x: engine.player.x, z: engine.player.z };
  return Object.entries(NODES).sort((a, b) => dist(a[1], p) - dist(b[1], p))[0][0];
}

async function openPause() {
  const pick = await ctx.modal(() => pause());
  if (pick === 'settings') { Object.assign(settings, await ctx.modal(() => settingsMenu({ ...settings }))); applySettings(); }
  else if (pick === 'save') { if (G?.checkpoint) store.set(SAVE_KEY, JSON.stringify({ index: progress.index, G: JSON.parse(save(G)) })); ctx.toast('마지막 체크포인트를 저장했습니다.'); }
  else if (pick === 'menu') location.reload();
}

async function run(start, resumeKey) {
  for (let i = start; i < CHAPTERS.length; i++) {
    const [id, fn] = CHAPTERS[i];
    progress.index = i;
    ctx.resume = resumeKey ?? null;
    if (!resumeKey) persist.checkpoint(id);
    resumeKey = null;
    for (;;) {
      try { const r = await fn(ctx); if (id === 'ending') return r; break; }
      catch (err) {
        if (!(err instanceof Fail)) throw err;
        engine.setControls(false); audio.siren(false); audio.fire(0); hud.setCinematic(false);
        const d = failDebrief(G, err.type);
        const pick = await failCard({ ...d, sources: (d.sourceIds || []).map(s => SOURCES.find(x => x.id === s)).filter(Boolean) });
        if (pick !== 'retry') return 'menu';
        const key = G.checkpoint.key;
        restore(G);
        const snap = ctx.snap || { levelStates: {} };
        Object.assign(ctx, { ...snap, levelStates: Object.fromEntries(Object.entries(snap.levelStates).map(([k, v]) => [k, { ...v }])) });
        ctx.resume = key === id ? null : key;
        engine.setControls(true);
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
    if (G?.checkpoint) { const key = G.checkpoint.key; restore(G); start = saved.index ?? 0; resume = CHAPTERS[start][0] === key ? null : key; }
    else G = createGame({ seed: (crypto.getRandomValues(new Uint32Array(1))[0]) >>> 1 });
    engine.setView(settings.view);
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
