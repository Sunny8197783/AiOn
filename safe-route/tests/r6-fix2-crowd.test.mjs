import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { live, setLive } from '../dist/engine/quality.js';
import { createCrowd } from '../dist/engine/crowd.js';

const people = { createPerson: () => ({ group: new THREE.Group(), setAnim() {}, setSpeed() {}, setProp() {}, update() {} }) };
// Two crowds of one level: standers idle in an area, strollers walk straight through it.
function run(peers) {
  const prev = live.tier; setLive('high');
  const stand = createCrowd({ count: 12, seed: 3, people, solids: [], peers });
  const walk = createCrowd({ count: 8, seed: 9, people, solids: [], peers });
  stand.setMode('idle', { area: { x0: -3, x1: 3, z0: -3, z1: 3 }, paths: [], instant: true });
  walk.setPaths([[{ x: 0, z: -25 }, { x: 0, z: 25 }], [{ x: 1, z: 25 }, { x: 1, z: -25 }]]); walk.setMode('stroll');
  const a = [], b = []; let close = 0;
  for (let i = 0; i < 1200; i++) {
    stand.update(0.05, {}); walk.update(0.05, {});
    if (i < 40 || i % 4) continue;
    for (const p of stand.positions(a)) for (const q of walk.positions(b)) if (Math.hypot(p.x - q.x, p.z - q.z) < 0.3) close++;
  }
  // a hidden crowd's members are not in anyone's way
  walk.group.visible = false;
  const n = peers?.length;
  stand.dispose(); walk.dispose(); setLive(prev);
  return { close, n, left: peers?.length };
}

test('crowds sharing peers separate from each other; dispose takes its members out of the list', () => {
  const alone = run(null), shared = run([]);
  assert.equal(shared.n, 20);
  assert.equal(shared.left, 0);
  assert.ok(shared.close <= 2, `shared: ${shared.close} close samples (separate crowds: ${alone.close})`);
  assert.ok(alone.close > shared.close, `without peers the crowds overlap (${alone.close})`);
});
