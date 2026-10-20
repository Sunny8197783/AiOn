// Story runtime: the glue chapters use to roam, talk, keep the clock, change levels and fail softly.
import { PLACES, SPAWNS } from '../game/townmap.js';
import { recordEvent, note, advance, adjust, fmtClock, speedMul, bagLoad, load } from '../game/state.js';
import { BAG_LIMIT } from '../game/items.js';
import { cite } from '../game/facts.js';

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

export function createRuntime({ engine, hud, ui, audio, getG, setG, persist, settings }) {
  const ctx = {
    engine, hud, ui, audio, settings,
    get G() { return getG(); },
    resume: null,          // checkpoint key a retry resumes from
    rate: 0,               // game seconds per real second while roaming (0 = clock stopped)
    outdoorEnv: 'day', indoorEnv: 'interior', power: true,
    levelStates: {},       // levelId -> { key: value } re-applied on every enter
  };
  let acc = 0;

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
    engine.player.speedMul = speedMul(G);
    hud.setTension(Math.max(0, (G.stats.tension - 40) / 60));
  }
  ctx.refresh = refresh;

  ctx.setLevelState = (levelId, key, value) => {
    (ctx.levelStates[levelId] ||= {})[key] = value;
    if (engine.level?.id === levelId) engine.level.setState(key, value);
  };

  // Enter a level and re-apply remembered states and the right lighting.
  ctx.goto = async (levelId, spawn, env) => {
    const level = await engine.enter(levelId, spawn);
    for (const [k, v] of Object.entries(ctx.levelStates[levelId] || {})) level.setState(k, v);
    engine.setEnv(env || (OUTDOOR.has(levelId) ? ctx.outdoorEnv : levelId === 'shelter' ? 'underground' : ctx.power ? ctx.indoorEnv : 'interiorDark'), { seconds: 0 });
    audio.ambience(ambienceFor(levelId), 1.2);
    return level;
  };
  function ambienceFor(id) {
    if (id === 'town') return ctx.outdoorEnv === 'blackout' ? 'blackout' : ctx.outdoorEnv === 'night' || ctx.outdoorEnv === 'dusk' ? 'night' : 'city';
    return { office: 'office', mart: 'mart', store: 'mart', shelter: 'shelter' }[id] || 'indoor';
  }

  ctx.objective = (title, body = '', steps = []) => hud.setObjective(title, body, steps);
  ctx.toast = (text, label = 'self', ms = 3800) => hud.toast(text, { label, ms });
  ctx.caption = (text, speaker, ms) => hud.caption(text, { speaker, ms });
  ctx.event = (type, details = {}) => recordEvent(ctx.G, type, details);
  ctx.note = (text, label = 'self', place = engine.level?.id) => { note(ctx.G, text, label, place); ctx.toast(`메모: ${text}`, label); };
  ctx.payoff = (prep, moment) => { recordEvent(ctx.G, 'payoff', { prep, moment }); ctx.toast(`준비가 도움이 됐습니다 · ${moment}`, 'self', 4600); };
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
  // deadline: { minute, day, type } -> Fail(type) when the clock passes it
  ctx.roam = ({ on = {}, doors = true, deadline = null, rate = ctx.rate, tick = null, shelter = null } = {}) => new Promise((resolve, reject) => {
    const prevRate = ctx.rate; ctx.rate = rate;
    let busy = false, done = false;
    const finish = (fnRes, value) => { if (done) return; done = true; offI(); offF(); ctx.rate = prevRate; fnRes(value); };
    const offI = engine.onInteract(async (point) => {
      if (busy || done) return;
      busy = true;
      try {
        const key = `${engine.level.id}:${point.id}`;
        let value;
        if (on[point.id]) value = await modal(() => on[point.id](point));
        else if (on[key]) value = await modal(() => on[key](point));
        else if (shelter && point.kind === 'shelter' && engine.level.id === 'town') value = await modal(() => shelter(point));
        else if (doors && DOORS[key]) { const [lv, sp] = DOORS[key]; await ctx.goto(lv, sp); }
        else if (on['*']) value = await modal(() => on['*'](point));
        if (value !== undefined) finish(resolve, value);
      } catch (err) { finish(reject, err); } finally { busy = false; }
    });
    const offF = engine.onFrame((dt) => {
      if (done) return;
      try {
        if (tick) { const v = tick(dt); if (v !== undefined) finish(resolve, v); }
        const G = ctx.G;
        if (deadline && (G.day > deadline.day || (G.day === deadline.day && G.minute >= deadline.minute))) finish(reject, new Fail(deadline.type));
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
