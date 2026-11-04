// Chapter 4–5 wiring and the prep/home helpers, run without a browser on a fake engine with scripted answers:
// the rumour judgement (M5), the lostWay / missed ends of the evacuation, the day ledger (M4b), the
// '집 안 대비' action (M4a), last year's snack and the album (M3), and the bag list that leaves the curtain out.
import test from 'node:test';
import assert from 'node:assert/strict';
import { createRuntime, Fail } from '../dist/story/runtime.js';
import { hearClaim, takesAsTrue, believes, LABEL_CHOICES } from '../dist/story/rumor-scene.js';
import { depart, missedCaption } from '../dist/story/evac.js';
import { dayLedger, eatOldBars, stockLook, lookAlbum, radioScene, RADIO_DRAIN } from '../dist/story/common.js';
import { bagItems, doHome } from '../dist/story/prep-actions.js';
import { ACTIONS } from '../dist/story/prep.js';
import { createGame, check } from '../dist/game/state.js';
import { rumor, misled } from '../dist/game/rumors.js';

function fakeEngine() {
  const frames = new Set(), inter = new Set(), built = {};
  const level = (id) => ({ id, points: [], npcs: {}, state: {}, setState(k, v) { this.state[k] = v; } });
  const engine = {
    level: null, controlsEnabled: true,
    player: { x: 0, y: 0, z: 0, yaw: 0, speedMul: 1, staminaUse: 1 },
    onNearest() {}, onFrame(f) { frames.add(f); return () => frames.delete(f); }, onInteract(f) { inter.add(f); return () => inter.delete(f); },
    setPower() {}, setEnv() {}, setControls(v) { engine.controlsEnabled = v; },
    async enter(id, spawn, { prepare }) { engine.level = built[id] ||= level(id); await prepare(engine.level); return engine.level; },
    frame: (dt = 1 / 30) => { for (const f of [...frames]) f(dt); },
    press: (p) => Promise.all([...inter].map(f => f(p))),
  };
  return engine;
}
// answer(o) picks for each talk; the default is the first choice.
function rig(answer = (o) => o.choices?.[0]?.id) {
  let G = createGame({ seed: 11 });
  const engine = fakeEngine(), toasts = [], talks = [], objectives = [];
  const hud = { prompt() {}, setClock() {}, setStats() {}, setBattery() {}, setBag() {}, setTension() {}, setObjective: (...a) => objectives.push(a), toast: (t) => toasts.push(t), caption: async () => {} };
  const ui = { talk: async (o) => { talks.push(o); return answer(o); }, card: async () => 'ok' };
  const ctx = createRuntime({ engine, hud, ui, audio: { ambience() {}, radio() {} }, settings: {}, persist: { checkpoint() {} }, getG: () => G, setG: (g) => { G = g; } });
  return { ctx, engine, toasts, talks, objectives, get G() { return G; } };
}
const tick = () => new Promise(r => setTimeout(r, 0));
const isLabels = (o) => o.choices === LABEL_CHOICES;

test('a claim labelled anything but rumour and acted on is believed; a rumour label sets it aside', async () => {
  assert.equal(takesAsTrue('rumor'), false); assert.equal(takesAsTrue('site'), true);
  assert.equal(believes('official', 'go'), true); assert.equal(believes('official', 'check'), false); assert.equal(believes('rumor', 'go'), false);
  const a = rig((o) => (isLabels(o) ? 'official' : 'go'));
  const r = await hearClaim(a.ctx, 'evac_shortcut');
  assert.equal(r.taken, true); assert.equal(misled(a.G), 'evac_shortcut');
  assert.equal(a.G.skill.info.at(-1).result, 'misled');
  const b = rig((o) => (isLabels(o) ? 'rumor' : 'go'));
  const s = await hearClaim(b.ctx, 'evac_shortcut');
  assert.equal(s.taken, false); assert.equal(misled(b.G), null); assert.equal(b.G.skill.info.at(-1).result, 'sorted');
  assert.match(b.talks[0].lines.join(' '), /풀림/);
});

async function runDepart(answer) {
  const r = rig(answer), G = r.G;
  G.chapter = 'evac'; G.day = 12; G.minute = 13 * 60 + 15;
  const done = depart(r.ctx);
  for (let i = 0; i < 5; i++) await tick();
  return { ...r, G, done, road: rumor('evac_shortcut').road(G) };
}

test('believe the shortcut, walk to its closure, let 14:00 pass: lostWay via rumour (F2, M5)', async () => {
  const { engine, G, done, road, objectives } = await runDepart((o) => (isLabels(o) ? 'neighbor' : 'go'));
  assert.match(objectives.at(-1)[1], /지름길/, 'the objective names the road the post points to');
  await engine.press({ id: `closure_${road}`, kind: 'npc', label: 'guide' });
  assert.ok(G.events.some(e => e.type === 'rumor_followed' && e.details.id === 'evac_shortcut'));
  G.minute = 14 * 60;
  engine.frame();
  await assert.rejects(done, (e) => e instanceof Fail && e.type === 'lostWay' && e.detail.via === 'rumor');
});

test('believed but never followed, or corrected by 박 소장: 14:00 is the plain missed bus', async () => {
  const a = await runDepart((o) => (isLabels(o) ? 'official' : 'go'));
  a.G.minute = 14 * 60; a.engine.frame();
  assert.equal(await a.done, undefined); assert.equal(a.G.know.path, 'missed');
  const b = await runDepart((o) => (isLabels(o) ? 'official' : 'go'));
  await b.engine.press({ id: 'managerWindow', kind: 'npc', label: '관리사무소' });
  assert.equal(misled(b.G), null, 'the site information from the manager corrects the post');
  assert.ok(!b.G.events.some(e => e.type === 'help' && e.details.who === 'manager'), 'talking is not helping (F7)');
  await b.engine.press({ id: `closure_${b.road}`, kind: 'npc', label: 'guide' });
  b.G.minute = 14 * 60; b.engine.frame();
  await b.done; assert.equal(b.G.know.path, 'missed');
});

test('303호 can be asked again after a first answer, and staying with the manager is the one help logged (F6, F7)', async () => {
  let n = 0;
  const a = await runDepart((o) => (isLabels(o) ? 'rumor' : o.speaker === '오서연' ? (n++ ? 'wait' : 'go') : o.choices?.[0]?.id));
  await a.engine.press({ id: 'seoyeonDoor', kind: 'door', label: '303호' });
  await a.engine.press({ id: 'seoyeonDoor', kind: 'door', label: '303호' });
  await a.done; assert.equal(a.G.know.path, 'seoyeon');
  const b = await runDepart((o) => (isLabels(o) ? 'rumor' : o.speaker === '박영수 소장' ? 'stay' : 'go'));
  await b.engine.press({ id: 'managerWindow', kind: 'npc', label: '관리사무소' });
  await b.done; assert.equal(b.G.know.path, 'stay');
  assert.equal(b.G.events.filter(e => e.type === 'help' && e.details.who === 'manager').length, 1);
});

test('missed caption: an empty bay only at the assembly point (F11)', () => {
  const { ctx, engine } = rig();
  engine.level = { id: 'town', points: [{ id: 'assembly', x: -60, z: -36 }] };
  engine.player.x = -58; engine.player.z = -30;
  assert.match(missedCaption(ctx), /집결지/);
  engine.player.x = 40; engine.player.z = 60;
  assert.doesNotMatch(missedCaption(ctx), /집결지/);
  engine.level = { id: 'apartment', points: [] };
  assert.match(missedCaption(ctx), /2시가 넘었다/);
});

test('day ledger: three picks play the days out and give the time-skip lines (M4b)', async () => {
  const { ctx, G, talks } = rig((o) => o.choices.at(1).id); // 2 L, two meals, powerbank
  G.day = 3;
  const lines = await dayLedger(ctx, 3, 'home');
  assert.equal(talks.length, 3);
  assert.ok(lines.length >= 1 && lines.every(l => /[가-힣]/.test(l)));
  assert.equal(G.ledger.length, 3); assert.deepEqual(G.ledger.map(r => r.day), [3, 4, 5]);
  assert.ok(Object.values(G.items).every(e => e.home >= 0 && e.bag >= 0));
});

test('old bars: the stock look warns, eating one costs a little health and calm, never a fail (M3)', async () => {
  const { ctx, G, talks, toasts } = rig((o) => o.choices?.[0]?.id);
  await stockLook(ctx);
  assert.match(talks[0].lines[0], /작년 가방에서 나온 것 · 기한 확인 필요/);
  assert.equal(G.items.old_bars.home, 1, 'the first choice eats one');
  assert.equal(G.stats.health, 96); assert.ok(toasts.at(-1).includes('기한'));
  G.items.old_bars.home = 0; assert.equal(eatOldBars(ctx), false);
  const t0 = G.stats.tension;
  await lookAlbum(ctx); await lookAlbum(ctx);
  assert.equal(G.stats.tension, Math.max(0, t0 - 5), 'the album calms once');
});

test("'집 안 대비': curtain hung and taped, water put by, dates read; the curtain never reaches the bag (M4a)", async () => {
  assert.ok(ACTIONS.some(a => a.id === 'home' && a.cost === 1));
  const { ctx, engine, G } = rig((o) => o.choices?.[0]?.id);
  G.items.blackout_curtain = { home: 1, bag: 0, bought: 1, checked: false, used: 0 };
  G.items.tape = { home: 1, bag: 0, bought: 1, checked: false, used: 0 };
  assert.ok(!bagItems(G).some(i => i.id === 'blackout_curtain'));
  await ctx.goto('apartment', 'home');
  const done = doHome(ctx);
  await tick();
  for (const id of ['homeWindow', 'homeSink', 'homeStock']) await engine.press({ id, kind: 'prop', label: id });
  await done;
  assert.equal(G.know.curtain, true); assert.equal(G.know.curtainSealed, true);
  assert.equal(ctx.levelStates.apartment.curtain, 'sealed'); assert.equal(engine.level.state.curtain, 'sealed');
  assert.ok(G.events.some(e => e.type === 'home_ready' && e.details.sealed));
  assert.equal(G.know.tubFilled, true); assert.equal(G.know.expiryChecked, true);
  assert.equal(G.items.old_bars.home, 0, 'the first choice tosses the expired snack');
});

test('each radio listen runs the batteries down; a flat radio takes a spare pack (M3)', async () => {
  const { ctx, G } = rig();
  ctx.ui.radio = async () => ({ official: true, noted: [] });
  G.items.radio = { home: 1, bag: 0, bought: 1, checked: false, used: 0 };
  G.items.batteries_aa = { home: 1, bag: 0, bought: 1, checked: false, used: 0 };
  check(G, 'radio');
  await radioScene(ctx, 'normal');
  assert.equal(G.items.radio.charge, 100 - RADIO_DRAIN);
  G.items.radio.charge = RADIO_DRAIN;
  await radioScene(ctx, 'normal');
  assert.equal(G.items.radio.checked, false); assert.ok(G.events.some(e => e.type === 'battery_dead'));
  G.items.batteries_old = { home: 1, bag: 0, bought: 0, checked: false, used: 0 };
  assert.ok(await radioScene(ctx, 'normal'), 'a used pack brings it back');
  assert.equal(G.items.radio.charge, 30 - RADIO_DRAIN);
});
