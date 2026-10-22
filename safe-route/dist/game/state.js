// Game state for 〈경보 이전〉: one plain JSON-safe object G plus the functions that change it.
// Pure logic: no DOM, no three.js. Thrown errors are for developers; player text lives in the UI/story.
import { CATALOG, START_HOME, BAG_LIMIT } from './items.js';

const STATS = ['health', 'thirst', 'fatigue', 'tension'];
const LABELS = ['official', 'site', 'neighbor', 'rumor', 'self'];
const TOOLS = ['radio', 'bag', 'route', 'extinguisher', 'docs'];
const PEOPLE = ['manager', 'clerk', 'seoyeon'];

// mulberry32, same as engine/kit.js rng (copied: game/* must not import three.js through kit).
function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6D2B79F5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function toSeed(seed) {
  if (typeof seed !== 'number' || !Number.isFinite(seed)) throw new TypeError(`seed must be a number, got ${seed}`);
  return Math.trunc(seed) >>> 0;
}

// Every closure set conditions() may pick. Each keeps home -> shelter reachable on foot with only
// the stairs and steep gates open, and home -> assembly reachable (tests/townmap.test.mjs proves it).
export const CLOSURE_SETS = {
  dday: [['yeonmuCenter'], ['yeonmuCenter', 'danji']],
  evac: [['yeonmuCenter', 'sijang'], ['yeonmuCenter', 'munhwaNorth'], ['danji', 'munhwaNorth'], ['sijang', 'munhwaSouth'], ['danji', 'sijang']],
};

// Situations that vary per seed. They change where and when, never what the safe action is.
export function conditions(seed) {
  const r = rng(toSeed(seed));
  const pick = (list) => list[Math.floor(r() * list.length)];
  return {
    closures: { dday: [...pick(CLOSURE_SETS.dday)], evac: [...pick(CLOSURE_SETS.evac)] },
    eastGateClosed: true,
    accessOpensAt: '20:16',
    commsWeakAt: 19 * 60 + 44 + Math.floor(r() * 12), // D-day, after the 19:40 blackout
    soldOut: { early: r() < 0.5 ? [] : ['water'], late: r() < 0.5 ? ['water', 'elec'] : ['water', 'elec', 'food'] }, // MART_SECTIONS ids
    rumorTrue: false,
    waterAt: 9 * 60 + 30 * Math.floor(r() * 4), // D+1
    evacNoticeAt: 7 * 60 + 15 * Math.floor(r() * 5), // D+12
    seoyeonAt: 'cafe',
  };
}

const newItem = (home = 0) => ({ home, bag: 0, bought: 0, checked: false, used: 0 });

export function createGame({ seed = Math.floor(Math.random() * 2 ** 31) } = {}) {
  seed = toSeed(seed);
  return {
    v: 2, seed, chapter: 'prologue', day: -3, minute: 17 * 60 + 30,
    slots: { total: 6, used: 0, done: [] },
    money: 150000,
    stats: { health: 100, thirst: 10, fatigue: 20, tension: 25 },
    phone: { battery: 64, signal: 'ok' },
    items: Object.fromEntries(Object.entries(START_HOME).map(([id, n]) => [id, newItem(n)])),
    know: {
      shelterLocated: false, entrancesChecked: false, routeChecked: false, extinguisherLocated: false, radioChecked: false,
      contactsCard: false, docsCopied: false, meetPoint: null, waterPoint: false, gateClosedSeen: false,
    },
    rel: Object.fromEntries(PEOPLE.map(p => [p, { trust: 0, contact: null }])),
    skill: Object.fromEntries(TOOLS.map(t => [t, []])),
    info: [],
    cond: conditions(seed),
    events: [],
    checkpoint: null,
  };
}

const stamp = (G) => ({ chapter: G.chapter, day: G.day, minute: G.minute });

// Events carry `chapter` too, so chapter summaries do not depend on day ranges.
export function recordEvent(G, type, details = {}) {
  if (typeof type !== 'string' || !type) throw new TypeError('event type must be a non-empty string');
  const e = { seq: (G.events.at(-1)?.seq ?? 0) + 1, type, ...stamp(G), details };
  G.events.push(e);
  return e;
}

export const hhmm = (m) => `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`;

export function fmtClock(G) {
  const d = G.day === 0 ? 'D-day' : G.day < 0 ? `D${G.day}` : `D+${G.day}`;
  return `${d} ${hhmm(G.minute)}`;
}

export function adjust(G, stat, delta) {
  if (!STATS.includes(stat)) throw new Error(`unknown stat: ${stat}`);
  if (typeof delta !== 'number' || !Number.isFinite(delta)) throw new TypeError(`delta must be a number, got ${delta}`);
  return (G.stats[stat] = Math.min(100, Math.max(0, G.stats[stat] + delta)));
}

// Moves the clock. Drift counts boundaries crossed on an absolute clock, so ten 2-minute steps
// add the same thirst as one 20-minute step.
export function advance(G, minutes, { awake = true } = {}) {
  if (typeof minutes !== 'number' || !Number.isFinite(minutes) || minutes < 0) throw new RangeError(`minutes must be >= 0, got ${minutes}`);
  const t0 = G.day * 1440 + G.minute, t1 = t0 + Math.round(minutes);
  const crossed = (step) => Math.floor(t1 / step) - Math.floor(t0 / step);
  adjust(G, 'thirst', crossed(20));
  if (awake) adjust(G, 'fatigue', crossed(30));
  G.day = Math.floor(t1 / 1440);
  G.minute = t1 - G.day * 1440;
  return G;
}

export function spendSlot(G, actionId, cost = 1) {
  if (!Number.isInteger(cost) || cost < 1) throw new RangeError(`slot cost must be a positive integer, got ${cost}`);
  const left = G.slots.total - G.slots.used;
  if (cost > left) throw new Error(`not enough slots for ${actionId}: ${left} left, ${cost} needed`);
  G.slots.used += cost;
  G.slots.done.push(actionId);
  recordEvent(G, 'slot', { actionId, cost });
  return left - cost;
}

function qty(n) {
  if (!Number.isInteger(n) || n < 1) throw new RangeError(`quantity must be a positive integer, got ${n}`);
  return n;
}

function entry(G, id) {
  if (!CATALOG[id]) throw new Error(`unknown item: ${id}`);
  return (G.items[id] ??= newItem());
}

// cart { id: qty }, prices { id: won } = what this shop has in stock (sold-out ids are left out).
// 'cash' at an ATM is a purchase like any other: its price is withdrawn from money.
export function buy(G, cart, prices) {
  const lines = Object.entries(cart ?? {}).filter(([, n]) => n !== 0);
  let total = 0;
  for (const [id, n] of lines) {
    if (!CATALOG[id]) throw new Error(`unknown item: ${id}`);
    qty(n);
    const p = prices?.[id];
    if (typeof p !== 'number' || !Number.isFinite(p) || p < 0) throw new Error(`${id} is not in stock here`);
    total += p * n;
  }
  if (total > G.money) throw new Error(`not enough money: ${total} > ${G.money}`);
  G.money -= total;
  for (const [id, n] of lines) { const e = entry(G, id); e.home += n; e.bought += n; }
  if (lines.length) recordEvent(G, 'buy', { cart: Object.fromEntries(lines), total });
  return total;
}

function move(G, id, n, from, to) {
  qty(n);
  const e = entry(G, id);
  if (e[from] < n) throw new Error(`only ${e[from]} ${id} at ${from}, asked to move ${n}`);
  e[from] -= n;
  e[to] += n;
  return e;
}
export const pack = (G, id, n = 1) => move(G, id, n, 'home', 'bag');
export const unpack = (G, id, n = 1) => move(G, id, n, 'bag', 'home');

const round2 = (x) => Math.round(x * 100) / 100;

export function bagLoad(G) {
  let w = 0, v = 0;
  for (const [id, e] of Object.entries(G.items)) { w += e.bag * CATALOG[id].weightKg; v += e.bag * CATALOG[id].volumeL; }
  const weightKg = round2(w), volumeL = round2(v);
  return { weightKg, volumeL, over: { weight: weightKg > BAG_LIMIT.weightKg, volume: volumeL > BAG_LIMIT.volumeL } };
}

const have = (G, id) => { const e = G.items[id]; return !!e && e.home + e.bag > 0; };

// Owned is not usable: a flashlight without batteries does not count.
export function usable(G, id) {
  if (!have(G, id)) return false;
  return CATALOG[id]?.needsCheck ? G.items[id].checked : true;
}

// Turns an owned item into a usable one. 'batteries' uses up one AA pack; 'charge' needs grid
// power, which the director gates (before the blackout). Returns { ok, reason? } (reason is Korean).
export function check(G, id) {
  if (!CATALOG[id]) throw new Error(`unknown item: ${id}`);
  if (!have(G, id)) return { ok: false, reason: '가지고 있지 않습니다.' };
  const e = G.items[id];
  if (e.checked) return { ok: true };
  if (CATALOG[id].needsCheck === 'batteries') {
    if (!have(G, 'batteries_aa')) return { ok: false, reason: '넣을 건전지가 없습니다.' };
    consume(G, 'batteries_aa', 1, G.items.batteries_aa.home > 0 ? 'home' : 'bag');
  }
  e.checked = true;
  recordEvent(G, 'check', { id });
  return { ok: true };
}

export function consume(G, id, n = 1, from = 'home') {
  if (from !== 'home' && from !== 'bag') throw new Error(`consume from must be 'home' or 'bag', got ${from}`);
  qty(n);
  const e = entry(G, id);
  if (e[from] < n) throw new Error(`only ${e[from]} ${id} at ${from}, asked to consume ${n}`);
  e[from] -= n;
  e.used += n;
  return e;
}

// Durable items (radio, flashlight, water_jug...) are used without being used up.
export function use(G, id) {
  const e = entry(G, id);
  if (!usable(G, id)) throw new Error(`${id} is not usable`);
  e.used += 1;
  return e;
}

export function note(G, text, label, place = null) {
  if (typeof text !== 'string' || !text.trim()) throw new TypeError('note text must be a non-empty string');
  if (!LABELS.includes(label)) throw new Error(`unknown info label: ${label}`);
  const t = text.trim();
  const dup = G.info.find(i => i.text === t && i.label === label);
  if (dup) return dup;
  const n = { ...stamp(G), text: t, label, place };
  G.info.push(n);
  return n;
}

// Walking and running speed multiplier 0.6..1 from fatigue (above 50) and, once the bag is on the back,
// its weight against BAG_LIMIT (up to -12 % at the limit, steeply more above it).
export function speedMul(G) {
  const weightKg = G.know?.bagOn ? bagLoad(G).weightKg : 0, lim = BAG_LIMIT.weightKg;
  const tired = Math.max(0, G.stats.fatigue - 50) / 50 * 0.25;
  const load = weightKg <= lim ? 0.12 * weightKg / lim : 0.12 + (weightKg - lim) / lim * 0.4;
  const leg = G.injury?.includes('leg') ? 0.7 : 1; // a broken leg from a fall stays for the rest of the run
  return round2(Math.min(1, Math.max(0.6, 1 - tired - load)) * leg);
}

export const save = (G) => JSON.stringify(G);

const isObj = (o) => o !== null && typeof o === 'object' && !Array.isArray(o);
const num = (n, lo = -Infinity, hi = Infinity) => typeof n === 'number' && Number.isFinite(n) && n >= lo && n <= hi;
const fail = (why) => { throw new Error(`invalid save: ${why}`); };

function validate(G, nested) {
  if (!isObj(G) || G.v !== 2) fail('not a v2 game');
  if (!num(G.seed, 0) || typeof G.chapter !== 'string' || !Number.isInteger(G.day) || !Number.isInteger(G.minute) || !num(G.minute, 0, 1439)) fail('clock');
  const s = G.slots;
  if (!isObj(s) || !Number.isInteger(s.total) || !num(s.used, 0, s.total) || !Array.isArray(s.done)) fail('slots');
  if (!num(G.money, 0)) fail('money');
  if (!isObj(G.stats) || !STATS.every(k => num(G.stats[k], 0, 100))) fail('stats');
  if (!isObj(G.phone) || !num(G.phone.battery, 0, 100)) fail('phone');
  if (!isObj(G.items)) fail('items');
  for (const [id, e] of Object.entries(G.items)) {
    if (!CATALOG[id]) { delete G.items[id]; continue; } // removed from the catalogue since this save
    if (!isObj(e) || !['home', 'bag', 'bought', 'used'].every(k => Number.isInteger(e[k]) && e[k] >= 0) || typeof e.checked !== 'boolean') fail(`item ${id}`);
  }
  if (!isObj(G.know) || !isObj(G.cond) || !isObj(G.rel) || !PEOPLE.every(p => isObj(G.rel[p]))) fail('know/cond/rel');
  if (!isObj(G.skill) || !TOOLS.every(t => Array.isArray(G.skill[t]))) fail('skill');
  if (!Array.isArray(G.info) || !G.info.every(i => isObj(i) && typeof i.text === 'string' && LABELS.includes(i.label))) fail('info');
  if (!Array.isArray(G.events) || !G.events.every(e => isObj(e) && Number.isInteger(e.seq) && typeof e.type === 'string')) fail('events');
  if (G.checkpoint !== null) {
    if (nested || !isObj(G.checkpoint) || typeof G.checkpoint.key !== 'string') fail('checkpoint');
    validate(G.checkpoint.state, true);
  }
}

export function load(json) {
  let G;
  try { G = typeof json === 'string' ? JSON.parse(json) : structuredClone(json); } catch { fail('not JSON'); }
  validate(G, false);
  return G;
}

export function checkpoint(G, key) {
  if (typeof key !== 'string' || !key) throw new TypeError('checkpoint key must be a non-empty string');
  const { checkpoint: _old, ...rest } = G;
  G.checkpoint = { key, state: structuredClone({ ...rest, checkpoint: null }) };
  return G.checkpoint;
}

// Rolls G back in place (callers keep their reference); the checkpoint stays for another retry.
export function restore(G) {
  const cp = G.checkpoint;
  if (!cp) throw new Error('no checkpoint to restore');
  for (const k of Object.keys(G)) delete G[k];
  Object.assign(G, structuredClone(cp.state), { checkpoint: cp });
  return G;
}
