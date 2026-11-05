import test from 'node:test';
import assert from 'node:assert/strict';
import { townTour, rippleMontage, elderScene } from '../dist/story/cutscenes.js';

// A stand-in story ctx: cine.play records the shots and answers with `answer` (or a queue of answers).
function fakeCtx({ reduceMotion = false, level = 'apartment', answers = ['done'] } = {}) {
  const log = { plays: [], gotos: [], states: [], envs: [], controls: [] };
  const engine = { reduceMotion, level: { id: level }, controlsEnabled: true, setEnv: (e) => log.envs.push(e), setControls: (on) => { engine.controlsEnabled = on; log.controls.push(on); } };
  const ctx = {
    G: { know: {} }, engine, outdoorEnv: 'night', log,
    goto: async (lv, spawn, env) => { log.gotos.push([lv, spawn, env]); engine.level = { id: lv }; },
    setLevelState: (lv, k, v) => log.states.push([lv, k, v]),
    cine: { title: () => {}, play: async (shots, opts) => { log.plays.push({ shots, opts }); return answers.length > 1 ? answers.shift() : answers[0]; } },
  };
  return ctx;
}
const rows = (n) => Array.from({ length: n }, (_, i) => ({ prep: `p${i}`, label: `준비${i}`, cause: { when: 'D-1 오전', text: `원인${i}` }, effect: { when: 'D+3 오전', text: `결과${i}` } }));

test('C0 tour: four labelled places in town, once (know.toured)', async () => {
  const ctx = fakeCtx();
  assert.equal(await townTour(ctx), 'done');
  assert.deepEqual(ctx.log.gotos, [['town', 'home', 'night']]);
  const { shots } = ctx.log.plays[0];
  assert.equal(shots.length, 4);
  assert.ok(shots.every(s => s.at?.length && s.cam.p.length === 2), 'each shot names its place and moves');
  assert.equal(ctx.G.know.toured, true);
});

test('reduced motion: every shot holds its last framing, no sway', async () => {
  const ctx = fakeCtx({ reduceMotion: true });
  await townTour(ctx);
  for (const s of ctx.log.plays[0].shots) { assert.equal(s.cam.p.length, 1); assert.equal(s.cam.t.length, 1); assert.equal(s.cam.hand, 0); assert.equal(s.cam.fov[0], s.cam.fov[1]); }
});

test('C1 keeps the world still when it was asked from inside a modal', async () => {
  const ctx = fakeCtx(); ctx.engine.controlsEnabled = false;
  assert.equal(await elderScene(ctx), 'done');
  assert.equal(ctx.log.plays[0].shots.length, 2);
  assert.match(ctx.log.plays[0].shots[0].lines[0][1], /304호 어르신이 계단 손잡이를 붙들고 서 있다/);
  assert.equal(ctx.engine.controlsEnabled, false);
});

test('C2 montage: at most three ripples over the town, then the closing shot of the path', async () => {
  const ctx = fakeCtx({ level: 'town' });
  assert.equal(await rippleMontage(ctx, rows(5), 'bus'), 'done');
  assert.equal(ctx.log.plays.length, 2);
  assert.equal(ctx.log.plays[0].shots[0].lines.length, 3);
  assert.deepEqual(ctx.log.states, [['town', 'buses', 'on']]);
  // No ripples: straight to the closing shot (it fades in itself); an unknown path falls back to the bus.
  const quiet = fakeCtx({ level: 'town' });
  await rippleMontage(quiet, [], 'nowhere');
  assert.equal(quiet.log.plays.length, 1);
  assert.equal(quiet.log.plays[0].shots[0].fade, true);
  // 303호 앞 with 서연: the closing shot moves to the apartment corridor.
  const sy = fakeCtx({ level: 'town' });
  await rippleMontage(sy, rows(1), 'seoyeon');
  assert.deepEqual(sy.log.gotos.at(-1), ['apartment', 'corridor3F', 'interiorDark']);
  assert.deepEqual(sy.log.states, [['apartment', 'seoyeonDoor', 'open']], '서연 stands at her open door');
});

test('C2 montage: skipping the crane skips the rest', async () => {
  const ctx = fakeCtx({ level: 'town', answers: ['skipped', 'done'] });
  assert.equal(await rippleMontage(ctx, rows(2), 'stay'), 'skipped');
  assert.equal(ctx.log.plays.length, 1);
  assert.deepEqual(ctx.log.states, []);
});
