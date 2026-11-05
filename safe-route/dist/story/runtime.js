// Story runtime: the glue chapters use to roam, talk, keep the clock, change levels and fail softly.
import { PLACES, SPAWNS } from '../game/townmap.js';
import { recordEvent, note, advance, adjust, fmtClock, speedMul, bagLoad, load } from '../game/state.js';
import { BAG_LIMIT } from '../game/items.js';
import { cite } from '../game/facts.js';
import { causeOf } from '../game/ripples.js';
import { isCommon, common } from './world.js';

export class Fail extends Error {
  constructor(type, detail = {}) { super(`fail:${type}`); this.type = type; this.detail = detail; }
}

// Default door wiring between levels. [level, point] -> [level, spawn]
const DOORS = {
  'town:home': ['apartment', 'lobby'], 'apartment:lobbyExit': ['town', 'home'],
  'town:mart': ['mart', 'entry'], 'mart:martExit': ['town', 'mart'],
  'town:store': ['store', 'entry'], 'store:storeExit': ['town', 'store'],
  'town:office': ['office', 'entry'], 'office:officeExit': ['town', 'office'],
  'shelter:shelterExit': ['town', 'shelter'],
};
// Which env a level uses for the current story time unless a chapter overrides it.
const OUTDOOR = new Set(['town']);
// What each level looks like when it is first built. Levels are built once per page and cached, so a key a
// chapter set and a retry or a new run no longer lists goes back to this value on the next enter.
// A level may publish its own `defaults`, which win. (stock and noticeText are re-set by their chapters.)
export const LEVEL_DEFAULTS = {
  apartment: { wallet: 'home', power: 'on', stock: {}, bag: 'empty', radio: 'none', hallFire: 'none', smoke: 0, haze: 'follow', seoyeonDoor: 'closed', candles: 'none', windowView: 'day', elder304: 'off', curtain: 'none' },
  town: { traffic: 'normal', crowd: 'normal', closures: [], gates: { east: 'open', stairs: 'open', ramp: 'open', access: 'closed' }, waterPoint: 'off', buses: 'off', notices: 'none', strike: 'none' },
  shelter: { crowd: 'settled', phase: 'calm', power: 'grid', fire: 'none', ways: ['stairs', 'ramp', 'access', 'east'], elder: 'off', seoyeon: 'hidden' },
  store: { soldOut: [], power: 'on', clerk: 'in' },
  mart: { crowd: 'normal', soldOut: [], power: 'on' },
  office: { alerts: 0 },
};
// States the runtime keeps for a level that has no setState key for them: [levelId][key](level, value).
const VIRTUAL = {
  shelter: { seoyeon: (L, v) => { const n = L.npcs?.seoyeon; if (n) n.group.visible = v === 'shown'; } },
};
function applyState(level, key, value) {
  const v = VIRTUAL[level.id]?.[key];
  if (v && !level.states?.[key]) v(level, value); else level.setState(key, value);
}
// Pure: the keys to put back to their default before a level's remembered states are applied.
export function staleKeys(applied, want, defaults) {
  return [...applied].filter(k => !(k in want) && k in defaults);
}
// A prompt with no handler in this chapter still answers, so E never silently does nothing.
export function idleLine(point) {
  return point.kind === 'door' ? '지금은 이 문으로 나갈 때가 아니다.' : point.kind === 'shelter' ? '지금은 대피소에 들어갈 일이 없다.' : `${point.label}: 지금은 할 일이 없다.`;
}

export function createRuntime({ engine, hud, ui, audio, getG, setG, persist, settings }) {
  const ctx = {
    engine, hud, ui, audio, settings,
    get G() { return getG(); },
    resume: null,          // checkpoint key a retry resumes from
    rate: 0,               // game seconds per real second while roaming (0 = clock stopped)
    outdoorEnv: 'day', indoorEnv: 'interior',
    // Grid power. The shelter runs on its own generator, so the engine sees power there either way.
    get power() { return power; },
    set power(v) { power = !!v; engine.setPower(power || engine.level?.id === 'shelter'); },
    levelStates: {},       // levelId -> { key: value } re-applied on every enter
  };
  let power = true;
  let acc = 0;
  const applied = {}; // levelId -> keys this page has set on the live (cached) level object
  // An on['*'] handler returns this when it handled the point but the roam goes on.
  ctx.handled = Symbol('handled');

  engine.onNearest((p) => hud.prompt(p ? p.label : null));
  engine.onFrame((dt) => {
    if (!ctx.rate || !engine.controlsEnabled) return;
    acc += dt * ctx.rate;
    if (acc >= 60) { const m = Math.floor(acc / 60); acc -= m * 60; advance(ctx.G, m); refresh(); }
  });

  function refresh() {
    const G = ctx.G;
    hud.setClock(fmtClock(G));
    hud.setStats(G.stats);
    hud.setBattery(G.phone.battery);
    const load = bagLoad(G);
    hud.setBag(G.know.bagOn ? { weightKg: load.weightKg, limitKg: BAG_LIMIT.weightKg } : null);
    // Experience mode (settings.profile) slows on top of the story's speed; a heavy bag on the back tires faster.
    engine.player.speedMul = (ctx.speedOverride ?? speedMul(G)) * (ctx.profileSpeed ?? 1);
    engine.player.staminaUse = 1 + (G.know.bagOn ? load.weightKg / BAG_LIMIT.weightKg : 0);
    hud.setTension(Math.max(0, (G.stats.tension - 40) / 60));
  }
  ctx.refresh = refresh;

  ctx.setLevelState = (levelId, key, value) => {
    (ctx.levelStates[levelId] ||= {})[key] = value;
    if (engine.level?.id === levelId) { applyState(engine.level, key, value); (applied[levelId] ||= new Set()).add(key); }
  };

  // Enter a level and re-apply remembered states and the right lighting. prepare runs before the engine
  // precompiles shaders, so it compiles the lights the scene will really have.
  ctx.goto = async (levelId, spawn, env) => {
    let prepared = false;
    const prepare = (level) => {
      prepared = true;
      engine.setPower(ctx.power || levelId === 'shelter');
      const want = ctx.levelStates[levelId] || {}, seen = (applied[levelId] ||= new Set());
      const defaults = { ...(LEVEL_DEFAULTS[levelId] || {}), ...(level.defaults || {}) };
      for (const k of staleKeys(seen, want, defaults)) applyState(level, k, defaults[k]);
      seen.clear();
      for (const [k, v] of Object.entries(want)) { applyState(level, k, v); seen.add(k); }
      engine.setEnv(env || (OUTDOOR.has(levelId) ? ctx.outdoorEnv : levelId === 'shelter' ? 'underground' : ctx.power ? ctx.indoorEnv : 'interiorDark'), { seconds: 0 });
    };
    const level = await engine.enter(levelId, spawn, { prepare });
    if (!prepared) prepare(level);
    audio.ambience(ambienceFor(levelId), 1.2);
    return level;
  };
  function ambienceFor(id) {
    if (id === 'town') return ctx.outdoorEnv === 'blackout' ? 'blackout' : ctx.outdoorEnv === 'night' || ctx.outdoorEnv === 'dusk' ? 'night' : 'city';
    return { office: 'office', mart: 'mart', store: 'mart', shelter: 'shelter' }[id] || 'indoor';
  }

  ctx.objective = (title, body = '', steps = []) => hud.setObjective(title, body, steps);
  ctx.toast = (text, label = 'self', ms = 3800) => hud.toast(text, { label, ms });
  // Captions off: same timing, nothing drawn, so story pacing does not change.
  ctx.caption = (text, speaker, ms) => (settings.captions === false && settings.profile !== 'deaf' ? new Promise(r => setTimeout(r, ms ?? Math.min(7000, 1500 + text.length * 60))) : hud.caption(text, { speaker, ms }));
  ctx.event = (type, details = {}) => recordEvent(ctx.G, type, details);
  ctx.note = (text, label = 'self', place = engine.level?.id) => { note(ctx.G, text, label, place); ctx.toast(`메모: ${text}`, label); };
  // The toast names the earlier moment that set it up (game/ripples.js), e.g. "(D-1 오전 · 비상가방을 꾸려 현관에 두었다)".
  ctx.payoff = (prep, moment) => {
    const ev = recordEvent(ctx.G, 'payoff', { prep, moment }), c = causeOf(ctx.G, prep, ev?.seq ?? Infinity);
    ctx.toast(`준비가 도움이 됐습니다 · ${moment}${c ? ` (${c.when} · ${c.text})` : ''}`, 'self', c ? 6000 : 4600);
  };
  ctx.stat = (stat, delta) => { adjust(ctx.G, stat, delta); refresh(); };
  ctx.pass = (minutes) => { advance(ctx.G, minutes); refresh(); };
  ctx.factLine = (id) => { const c = cite(id); return c ? `${c.text}\n— ${c.source}` : ''; };

  // Modal helpers: controls off while a UI is open.
  async function modal(fn) {
    engine.setControls(false);
    try { return await fn(); } finally { engine.setControls(true); refresh(); }
  }
  ctx.modal = modal;
  ctx.say = (speaker, lines, choices, role) => modal(() => ui.talk({ speaker, role, lines: [].concat(lines), choices }));
  ctx.card = (opts) => modal(() => ui.card(opts));
  ctx.skip = async (from, to, text) => { engine.setControls(false); await ui.timeSkip({ from, to, text }); engine.setControls(true); };

  ctx.checkpoint = (key) => { persist.checkpoint(key); };
  ctx.fail = (type, detail) => { throw new Fail(type, detail); };

  // Free roam until a handler returns something other than undefined.
  // on: { pointId: async (point) => value }   doors: follow DOORS unless overridden
  // deadline: { minute, day, type } -> Fail(type) when the clock passes it (type may be a function of the moment)
  ctx.roam = ({ on = {}, doors = true, deadline = null, rate = ctx.rate, tick = null, shelter = null } = {}) => new Promise((resolve, reject) => {
    const prevRate = ctx.rate; ctx.rate = rate; ctx.roaming = true;
    let busy = false, done = false, armed = new Set(), lastL = null;
    const finish = (fnRes, value) => { if (done) return; done = true; offI(); offF(); ctx.rate = prevRate; ctx.roaming = false; fnRes(value); };
    // Walk-through doors: stepping into a plain door (no handler of the chapter's own) goes through it, no key.
    // A door arms only once you have been more than 1.6 m from it in the current level, so arriving next to one
    // never bounces you back (armed resets on every level change: E on a door, or a handler's own ctx.goto).
    const walkDoor = () => {
      const L = engine.level; if (!doors || busy || !L) return;
      if (L !== lastL) { lastL = L; armed = new Set(); }
      for (const p of L.points || []) {
        const key = `${L.id}:${p.id}`;
        if (!DOORS[key] || on[p.id] || on[key] || p.enabled === false) continue;
        const d = Math.hypot(engine.player.x - p.x, engine.player.z - p.z);
        if (d > 1.6) armed.add(key); else if (d < 0.75 && armed.has(key)) { armed = new Set(); interact(p); return; }
      }
    };
    const interact = async (point) => {
      if (busy || done) return;
      busy = true;
      try {
        const key = `${engine.level.id}:${point.id}`;
        let value;
        if (on[point.id]) value = await modal(() => on[point.id](point));
        else if (on[key]) value = await modal(() => on[key](point));
        else if (shelter && point.kind === 'shelter' && engine.level.id === 'town') value = await modal(() => shelter(point));
        else if (doors && DOORS[key]) { const [lv, sp] = DOORS[key]; armed = new Set(); await ctx.goto(lv, sp); }
        // The chapter's '*' sees every other point first; undefined means "not mine" and the common text runs.
        else if (on['*'] && (value = await modal(() => on['*'](point))) !== undefined) { /* handled */ }
        else if (isCommon(ctx, point)) await modal(() => common(ctx, point));
        else ctx.toast(idleLine(point), 'self', 2200);
        if (value === ctx.handled) value = undefined;
        if (value !== undefined) finish(resolve, value);
      } catch (err) { finish(reject, err); } finally { busy = false; }
    };
    const offI = engine.onInteract(interact);
    const offF = engine.onFrame((dt) => {
      if (done || !engine.controlsEnabled) return; // a menu or dialog pauses timers and deadlines too
      try {
        walkDoor();
        if (tick) { const v = tick(dt); if (v !== undefined) finish(resolve, v); }
        const G = ctx.G;
        if (G.stats.health <= 0) return void finish(reject, new Fail('dead', { why: ctx.deathCause ?? 'hurt' }));
        if (deadline && (G.day > deadline.day || (G.day === deadline.day && G.minute >= deadline.minute))) finish(reject, new Fail(typeof deadline.type === 'function' ? deadline.type() : deadline.type));
      } catch (err) { finish(reject, err); }
    });
  });

  // Walk-to helper: resolves when the player is within r metres of (x, z) in the given level.
  ctx.near = (levelId, x, z, r = 3) => () => engine.level?.id === levelId && Math.hypot(engine.player.x - x, engine.player.z - z) < r ? true : undefined;
  ctx.place = (id) => PLACES[id];
  ctx.spawn = (id) => SPAWNS[id];

  ctx.load = (json) => { setG(load(json)); refresh(); };
  return ctx;
}
