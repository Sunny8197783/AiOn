// Story wiring that can run without a browser: the runtime's level defaults and handler order (fake engine),
// the body-strain numbers, the elder follower, and the evening neighbours with scripted answers.
import test from 'node:test';
import assert from 'node:assert/strict';
import { createRuntime, Fail, LEVEL_DEFAULTS, staleKeys, idleLine } from '../dist/story/runtime.js';
import { carryStep, smokeStep, CARRY, SMOKE } from '../dist/story/strain.js';
import { followTarget, elderSafeEarly } from '../dist/story/elder.js';
import { storeRumor, barter, knock, offerFor } from '../dist/story/neighbors.js';
import { createGame, recordEvent } from '../dist/game/state.js';
import { remember, deeds } from '../dist/game/deeds.js';

function fakeEngine() {
  const frames = new Set(), inter = new Set(), built = {};
  const level = (id) => ({ id, points: [], npcs: {}, state: {}, setState(k, v) { this.state[k] = v; } });
  const engine = {
    level: null, controlsEnabled: true, crouching: false,
    player: { x: 0, y: 0, z: 0, speedMul: 1, staminaUse: 1 },
    onNearest() {}, onFrame(f) { frames.add(f); return () => frames.delete(f); }, onInteract(f) { inter.add(f); return () => inter.delete(f); },
    setPower() {}, setEnv() {}, setControls(v) { engine.controlsEnabled = v; },
    async enter(id, spawn, { prepare }) { engine.level = built[id] ||= level(id); await prepare(engine.level); return engine.level; },
    frame: (dt = 1 / 30) => { for (const f of [...frames]) f(dt); },
    press: (p) => Promise.all([...inter].map(f => f(p))),
    built,
  };
  return engine;
}
function rig() {
  let G = createGame({ seed: 7 });
  const engine = fakeEngine(), toasts = [], talks = [];
  const hud = { prompt() {}, setClock() {}, setStats() {}, setBattery() {}, setBag() {}, setTension() {}, setObjective() {}, toast: (t) => toasts.push(t), caption: async () => {} };
  const ui = { talk: async (o) => { talks.push(o); return o.choices?.[0]?.id; }, card: async () => 'ok' };
  const ctx = createRuntime({ engine, hud, ui, audio: { ambience() {} }, settings: {}, persist: { checkpoint() {} }, getG: () => G, setG: (g) => { G = g; } });
  return { ctx, engine, toasts, talks, get G() { return G; } };
}
const tick = () => new Promise(r => setTimeout(r, 0));

test('a state set in one run goes back to the level default after levelStates is reset (F1)', async () => {
  const { ctx, engine } = rig();
  ctx.setLevelState('town', 'strike', 'hit');
  ctx.setLevelState('town', 'closures', ['sijang']);
  await ctx.goto('town', 'home');
  assert.equal(engine.level.state.strike, 'hit');
  ctx.levelStates = {}; // what resetRun / a checkpoint retry leave behind
  await ctx.goto('apartment', 'home');
  await ctx.goto('town', 'home');
  assert.equal(engine.level.state.strike, 'none');
  assert.deepEqual(engine.level.state.closures, []);
  // a key the run still holds is applied as it is; keys never touched are not re-sent
  ctx.setLevelState('town', 'buses', 'on'); delete engine.built.town.state.traffic;
  await ctx.goto('apartment', 'home'); await ctx.goto('town', 'home');
  assert.equal(engine.level.state.buses, 'on');
  assert.equal(engine.level.state.traffic, undefined);
  assert.deepEqual(staleKeys(new Set(['a', 'b', 'c']), { b: 1 }, { a: 0, b: 0 }), ['a']);
  for (const [id, d] of Object.entries(LEVEL_DEFAULTS)) assert.ok(Object.keys(d).length, id);
});

test('shelter 서연 is a runtime state the next run hides again', async () => {
  const { ctx, engine } = rig();
  await ctx.goto('shelter', 'spot');
  engine.level.npcs.seoyeon = { group: { visible: false } };
  ctx.setLevelState('shelter', 'seoyeon', 'shown');
  assert.equal(engine.level.npcs.seoyeon.group.visible, true);
  assert.equal(engine.level.state.seoyeon, undefined, 'not sent to the level');
  ctx.levelStates = {};
  await ctx.goto('shelter', 'spot');
  assert.equal(engine.level.npcs.seoyeon.group.visible, false);
});

test("roam: a chapter's '*' runs before the everyday text, falls through on undefined, and E always answers (F8, F12)", async () => {
  const { ctx, engine, toasts, talks } = rig();
  await ctx.goto('town', 'home');
  let star = 0;
  const done = ctx.roam({ on: { '*': async (p) => { star++; if (p.kind === 'notice') return ctx.handled; if (p.id === 'stop') return 'stopped'; } } });
  await engine.press({ id: 'aptNotice', kind: 'notice', label: '단지 게시판' });
  assert.equal(star, 1); assert.equal(talks.length, 0, 'the peacetime board text did not run');
  await engine.press({ id: 'center', kind: 'place', label: '행정복지센터' });
  assert.equal(star, 2); assert.equal(talks.at(-1).speaker, '행정복지센터', 'falls through to the description');
  await engine.press({ id: 'shelterA', kind: 'shelter', label: '출입구 A' });
  assert.equal(toasts.at(-1), idleLine({ kind: 'shelter' }));
  await engine.press({ id: 'stop', kind: 'npc', label: 'x' });
  assert.equal(await done, 'stopped');
  assert.match(idleLine({ kind: 'npc', label: '304호 어르신' }), /304호 어르신: 지금은 할 일이 없다/);
});

test('roam deadline type may be decided at the moment it passes (lostWay vs late)', async () => {
  const { ctx, engine, G } = rig();
  await ctx.goto('town', 'home');
  G.day = 0; G.minute = 20 * 60 + 31;
  const p = ctx.roam({ deadline: { day: 0, minute: 20 * 60 + 30, type: () => 'lostWay' } });
  engine.frame();
  await assert.rejects(p, (e) => e instanceof Fail && e.type === 'lostWay');
});

test('payoff toast carries the cause stamp from ripples (M1)', () => {
  const { ctx, toasts, G } = rig();
  G.day = -1; G.minute = 10 * 60; recordEvent(G, 'bag_packed', {});
  G.day = 0; G.minute = 20 * 60 + 6; ctx.payoff('bag', '바로 나섰다');
  assert.equal(toasts.at(-1), '준비가 도움이 됐습니다 · 바로 나섰다 (D-1 오전 · 비상가방을 꾸려 현관에 두었다)');
  ctx.payoff('judgement', '판단했다');
  assert.equal(toasts.at(-1), '준비가 도움이 됐습니다 · 판단했다');
});

test('carrying water tires only while walking; resting recovers (F3)', () => {
  assert.ok(carryStep(1, 0, 20).fatigue < 0, 'standing still rests');
  assert.ok(carryStep(1, 3.2, 20).fatigue > carryStep(1, 1.8, 20).fatigue, 'running costs more');
  assert.ok(carryStep(1, 1.8, 20).fatigue > carryStep(1, 1.8, 4).fatigue, '20 L costs more than 4 L');
  // 165 m home at the heavy pace (0.62 x 3 m/s): about 11 fatigue, so only an already worn-out body gives out.
  let f = 0; for (let t = 0; t < 165 / 1.86; t += 0.1) f += carryStep(0.1, 1.86, 20).fatigue;
  assert.ok(f > 8 && f < 15, String(f));
  assert.ok(CARRY.warn < 100);
});

test('smoke: crouching with a wet towel stays safe, standing without one blurs but is survivable on a direct path (M7)', () => {
  const run = (sec, o) => { const s = {}; let hurt = 0, r; for (let t = 0; t < sec; t += 1 / 30) { r = smokeStep(s, 1 / 30, o); hurt += r.hurt; } return { ...r, hurt }; };
  const low = run(40, { smoke: 0.7, crouch: true, towel: true });
  assert.ok(low.hurt <= 12, `crouch + towel: ${low.hurt}`);
  const direct = run(8, { smoke: 0.7 });
  assert.ok(direct.exposure > SMOKE.safe && direct.level > 0.5, 'visibly blurred');
  assert.ok(direct.hurt < 15, `direct path: ${direct.hurt}`);
  assert.ok(run(60, { smoke: 0.7 }).hurt > 100, 'lingering in thick smoke is deadly');
  assert.equal(run(10, { smoke: 0 }).hurt, 0);
});

test('the elder follows the trail a step behind; helped or told counts as brought down early', () => {
  const crumbs = [{ x: 0, z: 0 }, { x: 0.5, z: 0 }, { x: 1, z: 0 }, { x: 1.5, z: 0 }, { x: 2, z: 0 }];
  assert.deepEqual(followTarget(crumbs, 1.1), { x: 0.5, z: 0 });
  assert.deepEqual(followTarget(crumbs.slice(0, 1)), { x: 0, z: 0 });
  assert.equal(followTarget([]), null);
  assert.ok(elderSafeEarly({ elderPick: 'help' }) && elderSafeEarly({ elderPick: 'manager', elderTold: true }));
  assert.ok(!elderSafeEarly({ elderPick: 'manager' }) && !elderSafeEarly({ elderPick: 'go' }) && !elderSafeEarly({}));
});

// A ctx for one scene: talk() answers from a script (by choice id), everything else is recorded.
function sceneCtx(G, answers) {
  const said = [], toasts = [];
  return { said, toasts, ctx: {
    G, engine: { level: { id: 'store' } }, audio: { radio() {} },
    ui: { talk: async (o) => { said.push(o); if (!o.choices) return; const id = answers.shift(); const c = o.choices.find(x => x.id === id); assert.ok(c, `choice ${id} offered`); assert.ok(!c.disabled, `choice ${id} enabled`); return id; } },
    note() {}, toast: (t) => toasts.push(t), caption: async () => {}, refresh() {}, pass() {}, remember: (id, d) => remember(G, id, d),
  } };
}
const n = (G, id) => (G.items[id]?.home ?? 0) + (G.items[id]?.bag ?? 0);

test('barter: testing the pack first shows it is a used one, and the price drops to one snack (M6)', async (t) => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  const G = createGame({ seed: 3 }); G.items.bars = { home: 3, bag: 0, bought: 3, checked: false, used: 0 };
  assert.deepEqual(offerFor(G), { bars: 3 });
  const { ctx, said } = sceneCtx(G, ['test', 'small']);
  await barter(ctx);
  assert.equal(n(G, 'bars'), 2); assert.equal(n(G, 'batteries_old'), 1);
  assert.ok(said.some(o => o.lines.some(l => l.includes('30%'))));
  const tr = G.events.find(e => e.type === 'trade');
  assert.equal(tr.details.from, 'trader');
});

test('barter: old bars are read and politely declined; taking without a test gets the used pack', async () => {
  const G = createGame({ seed: 3 }); G.items.bars = { home: 3, bag: 0, bought: 3, checked: false, used: 0 };
  const { ctx } = sceneCtx(G, ['old', 'take']);
  await barter(ctx);
  assert.ok(G.events.some(e => e.type === 'barter_old_bars'));
  assert.equal(n(G, 'old_bars'), 2, 'the expired bars stay');
  assert.equal(n(G, 'bars'), 0); assert.equal(n(G, 'batteries_old'), 1);
  const H = createGame({ seed: 3 });
  const s2 = sceneCtx(H, ['no']); await barter(s2.ctx);
  assert.ok(!H.events.some(e => e.type === 'trade'));
});

test('knock: water, information, or no answer; only the first two are help (M6)', async () => {
  for (const [pick, help] of [['give', true], ['info', true], ['none', false]]) {
    const G = createGame({ seed: 5 }), before = n(G, 'water_2l');
    G.day = 1; G.minute = 21 * 60;
    const { ctx } = sceneCtx(G, [pick]);
    await knock(ctx);
    assert.equal(G.events.some(e => e.type === 'help' && e.details.who === 'stranger'), help, pick);
    assert.equal(n(G, 'water_2l'), pick === 'give' ? before - 1 : before);
  }
});

test('shelter rumour is judged by source and sorted into the info ladder (M5)', async (t) => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  const G = createGame({ seed: 9 });
  G.chapter = 'endure';
  const a = sceneCtx(G, ['rumor', 'check', 'tell']);
  await storeRumor(a.ctx);
  assert.equal(G.skill.info.at(-1).result, 'sorted');
  assert.ok(G.events.some(e => e.type === 'rumor_checked'));
  assert.deepEqual(deeds(G).map(d => [d.id, d.details.after]), [['rumor_checked', undefined], ['rumor_corrected', 'check']]);
  const H = createGame({ seed: 9 });
  const b = sceneCtx(H, ['official', 'spread', 'fix']);
  await storeRumor(b.ctx);
  assert.equal(H.skill.info.at(-1).result, 'misled');
  assert.ok(H.events.some(e => e.type === 'rumor_spread') && H.events.some(e => e.type === 'rumor_believed'));
  assert.equal(H.know.misled ?? null, null, 'corrected the same evening');
  assert.deepEqual(deeds(H).map(d => [d.id, d.details.after]), [['rumor_spread', undefined], ['rumor_corrected', 'spread']], 'told the neighbour again');
});
