// r6 fixer 1 (fails:F1): E on a walk-through door must not leave a stale "armed" door behind, so arriving next to
// the door you came back through never walks you straight out again.
import test from 'node:test';
import assert from 'node:assert/strict';
import { createRuntime } from '../dist/story/runtime.js';
import { createGame } from '../dist/game/state.js';

function rig() {
  const frames = new Set(), inter = new Set();
  const mk = (id, points) => ({ id, points, npcs: {}, state: {}, setState(k, v) { this.state[k] = v; } });
  const built = {
    apartment: mk('apartment', [{ id: 'lobbyExit', kind: 'door', label: '현관', x: 4, z: -6 }]),
    town: mk('town', [{ id: 'home', kind: 'door', label: '아파트', x: 78, z: 28.5 }]),
  };
  const spawns = { 'apartment:lobby': { x: 4, z: -5.4 }, 'town:home': { x: 78, z: 26 } };
  const engine = {
    level: null, controlsEnabled: true, crouching: false, player: { x: 0, y: 0, z: 0, speedMul: 1, staminaUse: 1 },
    onNearest() {}, onFrame(f) { frames.add(f); return () => frames.delete(f); }, onInteract(f) { inter.add(f); return () => inter.delete(f); },
    setPower() {}, setEnv() {}, setControls(v) { engine.controlsEnabled = v; },
    async enter(id, spawn, { prepare }) { engine.level = built[id]; Object.assign(engine.player, spawns[`${id}:${spawn}`] || {}); await prepare(engine.level); return engine.level; },
  };
  const frame = () => { for (const f of [...frames]) f(1 / 30); };
  const press = (p) => Promise.all([...inter].map(f => f(p)));
  let G = createGame({ seed: 7 });
  const hud = { prompt() {}, setClock() {}, setStats() {}, setBattery() {}, setBag() {}, setTension() {}, setObjective() {}, toast() {}, caption: async () => {} };
  const ctx = createRuntime({ engine, hud, ui: { talk: async () => null, card: async () => 'ok' }, audio: { ambience() {} }, settings: {}, persist: { checkpoint() {} }, getG: () => G, setG: (g) => { G = g; } });
  return { ctx, engine, built, frame, press };
}
const tick = () => new Promise(r => setTimeout(r, 0));

test('E out through the lobby and E back in: the lobby spawn (0.6 m from the exit) does not bounce you out', async () => {
  const { ctx, engine, built, frame, press } = rig();
  await ctx.goto('apartment', 'lobby');
  ctx.roam({ on: {} });
  engine.player.x = 4; engine.player.z = -2.5; frame(); // > 1.6 m from lobbyExit: it arms
  await press(built.apartment.points[0]); await tick();
  assert.equal(engine.level.id, 'town');
  for (let i = 0; i < 5; i++) frame();
  await press(built.town.points[0]); await tick();
  assert.equal(engine.level.id, 'apartment');
  for (let i = 0; i < 10; i++) { frame(); await tick(); }
  assert.equal(engine.level.id, 'apartment', 'still in the lobby, not walked back out');
  engine.player.z = -2.5; frame(); engine.player.z = -5.9; frame(); await tick(); await tick();
  assert.equal(engine.level.id, 'town', 'walking away and back into the door still goes through it');
});
