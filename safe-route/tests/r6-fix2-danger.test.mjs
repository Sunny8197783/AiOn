import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { shellAim } from '../dist/story/danger.js';

test('a close shell never lands within 4 m of a player who stands, walks or runs straight on', () => {
  for (const v of [0, 3, 6]) for (const yaw of [0, 1.3, -2.2]) {
    let near9 = 0;
    for (let i = 0; i < 4000; i++) {
      const P = { x: 10, z: -20, yaw }, { x, z } = shellAim(P, true);
      // 2.3 s later the walker has moved v * 2.3 m along the facing (yaw 0 looks toward -z)
      const px = P.x - Math.sin(yaw) * v * 2.3, pz = P.z - Math.cos(yaw) * v * 2.3, d = Math.hypot(x - px, z - pz);
      assert.ok(d >= 4, `v ${v} yaw ${yaw}: ${d.toFixed(2)} m`);
      if (d < 9) near9++;
    }
    if (v === 0) assert.equal(near9, 4000, 'standing still a close shell still hurts (inside 9 m)');
  }
  for (let i = 0; i < 2000; i++) { const { x, z } = shellAim({ x: 0, z: 0, yaw: 0 }, false), d = Math.hypot(x, z); assert.ok(d >= 9 - 1e-9 && d <= 32 + 1e-9); }
});

test('looters: the give goes through consume(bag) and only while the bag is on; loot drops where the bag is', () => {
  const src = readFileSync(new URL('../dist/story/danger.js', import.meta.url), 'utf8');
  assert.match(src, /consume\(G, id, 1, 'bag'\)/);
  assert.doesNotMatch(src, /\.bag -= 1/);
  assert.match(src, /have = G\.know\.bagOn \? FOOD\.filter/);
  assert.match(src, /\[G\.know\.bagOn \? 'bag' : 'home'\] \+= 1/);
});
