import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { TIERS, TIER_LABELS, PROFILES, normQ, rankQ, lowerQ, profileOf, pixelRatioFor, savedQuality, autoTier, createGovernor, share, live, setLive } from '../dist/engine/quality.js';
import { createCrowd } from '../dist/engine/crowd.js';

test('five tiers with the Korean labels, every profile has every knob', () => {
  assert.deepEqual(TIERS, ['ultralow', 'low', 'medium', 'high', 'ultra']);
  assert.deepEqual(TIERS.map(q => TIER_LABELS[q][0]), ['완전 낮음', '낮음', '중간', '높음', '완전 높음']);
  const keys = Object.keys(PROFILES.high).sort();
  for (const q of TIERS) {
    assert.deepEqual(Object.keys(PROFILES[q]).sort(), keys, q);
    for (const k of keys) assert.notEqual(PROFILES[q][k], undefined, `${q}.${k}`);
    assert.ok(Object.isFrozen(PROFILES[q]));
  }
});

test('tiers only add cost going up', () => {
  const up = ['sun', 'flash', 'gtao', 'bloom', 'clouds', 'aniso', 'tex', 'atlas', 'goodsSeg', 'shelterTex', 'crowdCap', 'crowdScale', 'crowdShadowM', 'crowdAnimM', 'people', 'fx', 'shop', 'keyLights', 'pool', 'shelterLights', 'roomLights', 'parked', 'lightShadows', 'lightShadowMap'];
  for (let i = 1; i < TIERS.length; i++) {
    const a = PROFILES[TIERS[i - 1]], b = PROFILES[TIERS[i]];
    for (const k of up) assert.ok(b[k] >= a[k], `${k}: ${TIERS[i - 1]} ${a[k]} > ${TIERS[i]} ${b[k]}`);
    const every = (n) => n || Infinity; // 0 = no sun shadow under a dim key light
    assert.ok(b.casterMin <= a.casterMin && b.shadowEvery <= a.shadowEvery && every(b.darkEvery) <= every(a.darkEvery), TIERS[i]);
    assert.ok(b.shelterPeople[0] >= a.shelterPeople[0] && b.shelterPeople[1] >= a.shelterPeople[1]);
    for (const dpr of [1, 1.25, 2, 3]) assert.ok(pixelRatioFor(TIERS[i], dpr) >= pixelRatioFor(TIERS[i - 1], dpr));
  }
  // 완전 낮음 is light: no shadows, no AO, no bloom or SMAA, below native resolution, few people and particles
  const U = PROFILES.ultralow;
  assert.ok(U.sun === 0 && U.flash === 0 && U.lightShadows === 0 && U.gtao === 0 && U.bloom === 0 && !U.smaa && !U.adapt && !U.blur);
  assert.ok(pixelRatioFor('ultralow', 1) < 1 && U.crowdCap <= 8 && U.fx <= 0.3 && !U.prewarm);
  assert.ok(U.keyLights >= 1, 'the home street keeps one real light');
});

test('높음 stays the look the game was tuned at', () => {
  const H = PROFILES.high;
  assert.deepEqual([H.sun, H.flash, H.gtao, H.bloom, H.clouds, H.tex, H.atlas, H.goodsSeg, H.shelterTex], [2048, 512, 1, 2, 5, 512, 2048, 10, 1024]);
  assert.deepEqual([H.keyLights, H.pool, H.shelterLights, H.crowdShadowM, H.crowdAnimM, H.people, H.fx, H.shop], [7, 5, 6, 25, 45, 1, 1, 1]);
  assert.deepEqual(H.shelterPeople, [26, 4]);
  assert.ok(H.adapt && H.blur && H.smaa && H.chroma && H.sharpen === 0.25 && H.physicalPaint && H.casterMin === 0);
  assert.equal(pixelRatioFor('high', 1), 1);
});

test('ids, aliases and saved-setting migration', () => {
  for (const q of TIERS) assert.equal(normQ(q), q);
  assert.equal(normQ('ultra-low'), 'ultralow'); assert.equal(normQ('ultra-high'), 'ultra');
  assert.equal(normQ('potato'), 'medium'); assert.equal(normQ('potato', null), null); assert.equal(normQ(undefined, null), null); assert.equal(normQ(3, null), null);
  assert.equal(rankQ('ultralow'), 0); assert.equal(rankQ('ultra'), 4);
  assert.equal(profileOf('nonsense'), PROFILES.high);
  // old saves keep their meaning; a missing or unknown tier is detected again
  for (const q of ['low', 'medium', 'high']) assert.deepEqual(savedQuality({ quality: q }), { quality: q, auto: false });
  assert.deepEqual(savedQuality({}), { quality: 'auto', auto: true });
  assert.deepEqual(savedQuality(null), { quality: 'auto', auto: true });
  assert.deepEqual(savedQuality({ quality: 'turbo' }), { quality: 'auto', auto: true });
  assert.deepEqual(savedQuality({ quality: 'low', qualityAuto: true }), { quality: 'low', auto: true });
  assert.equal(share(26, 1.25), 33); assert.equal(share(26, 0.5), 13); assert.equal(share(1, 0.1), 1); assert.equal(share(0, 1), 0);
});

test('one step down at a time, never below 완전 낮음', () => {
  const seen = ['ultra']; let q = 'ultra';
  for (let i = 0; i < 6; i++) { q = lowerQ(q); seen.push(q); }
  assert.deepEqual(seen, ['ultra', 'high', 'medium', 'low', 'ultralow', 'ultralow', 'ultralow']);
});

test('first-run detection', () => {
  assert.equal(autoTier({ gpu: 'ANGLE (Google, Vulkan 1.3.0 (SwiftShader Device (Subzero)), SwiftShader driver)' }), 'ultralow');
  assert.equal(autoTier({ gpu: 'llvmpipe (LLVM 15.0.7, 256 bits)', cores: 16 }), 'ultralow');
  assert.equal(autoTier({ gpu: 'ANGLE (NVIDIA, NVIDIA GeForce RTX 3060 Direct3D11 vs_5_0 ps_5_0)', cores: 12, memory: 8 }), 'high');
  assert.equal(autoTier({ gpu: 'ANGLE (Intel, Intel(R) UHD Graphics 620 Direct3D11 vs_5_0 ps_5_0)', cores: 8, memory: 8 }), 'low');
  assert.equal(autoTier({ gpu: 'ANGLE (Intel, Intel(R) UHD Graphics 600 Direct3D11)', cores: 2, memory: 4 }), 'ultralow');
  assert.equal(autoTier({ gpu: 'Some Future GPU', cores: 8, memory: 8 }), 'medium');
  assert.equal(autoTier({ gpu: 'Some Future GPU', cores: 4, memory: 8 }), 'low');
  assert.equal(autoTier({ gpu: 'ANGLE (NVIDIA GeForce RTX 4070)', cores: 16, memory: 8, pixels: 3840 * 2160 }), 'medium');
  assert.equal(autoTier({ gpu: 'Apple M2', cores: 8, memory: 8 }), 'medium');
  assert.notEqual(autoTier({ gpu: 'NVIDIA GeForce RTX 4090', cores: 32, memory: 8 }), 'ultra'); // never guessed
});

test('the governor steps down after 4 s below 30 fps, then settles', () => {
  const g = createGovernor();
  const run = (fps, seconds, o) => { let n = 0; for (let t = 0; t < seconds; t += 1 / fps) if (g.sample(1 / fps, o)) n++; return n; };
  assert.equal(run(60, 5), 0);
  assert.equal(run(20, 3.4), 0, 'not before 4 s');
  assert.equal(run(20, 1.2), 1, 'steps once at 4 s');
  assert.equal(run(20, 5.5), 0, 'settles 6 s after a step');
  assert.equal(run(20, 5), 1, 'and steps again when it stays slow');
  // hysteresis: hovering around the line (28 / 34 fps) drains slowly instead of stepping; a smooth stretch resets
  const g2 = createGovernor({ settle: 0 });
  let n2 = 0;
  for (let i = 0; i < 10; i++) { for (let t = 0; t < 0.5; t += 1 / 28) n2 += g2.sample(1 / 28) ? 1 : 0; for (let t = 0; t < 0.5; t += 1 / 34) n2 += g2.sample(1 / 34) ? 1 : 0; }
  assert.equal(n2, 0, '28/34 fps alternating never steps');
  const g3 = createGovernor({ settle: 0 });
  let n3 = 0; for (let i = 0; i < 6; i++) { for (let t = 0; t < 3; t += 1 / 20) n3 += g3.sample(1 / 20) ? 1 : 0; for (let t = 0; t < 0.6; t += 1 / 60) n3 += g3.sample(1 / 60) ? 1 : 0; }
  assert.equal(n3, 0, 'a smooth half second resets the count');
  // busy (a level loading), the floor tier and long gaps (hidden tab) never step
  const g4 = createGovernor({ settle: 0 });
  let n4 = 0; for (let t = 0; t < 10; t += 0.1) n4 += g4.sample(0.1, { busy: true }) ? 1 : 0;
  for (let t = 0; t < 10; t += 0.1) n4 += g4.sample(0.1, { floor: true }) ? 1 : 0;
  for (let i = 0; i < 10; i++) n4 += g4.sample(3) ? 1 : 0;
  assert.equal(n4, 0);
  // a very slow machine (1 fps) is still judged: each long frame counts
  const g5 = createGovernor({ settle: 0 });
  let n5 = 0; for (let i = 0; i < 5; i++) n5 += g5.sample(1) ? 1 : 0;
  assert.equal(n5, 1);
});

test('auto-downgrade chain from 완전 높음 to 완전 낮음, then it stops', () => {
  const g = createGovernor(); let q = 'ultra', t = 0; const log = [];
  while (t < 120) {
    t += 0.1;
    if (g.sample(0.1, { floor: rankQ(q) === 0 }) && rankQ(q) > 0) { q = lowerQ(q); log.push([q, +t.toFixed(1)]); g.hold(4); }
  }
  assert.deepEqual(log.map(([x]) => x), ['high', 'medium', 'low', 'ultralow']);
  assert.ok(log[0][1] >= 6.4 && log[0][1] < 8, `first step at ${log[0][1]} s`); // 3 s settle + 4 s slow, in 0.5 s windows
  for (let i = 1; i < log.length; i++) assert.ok(log[i][1] - log[i - 1][1] >= 9.4, 'one step at a time with a settle (6 s + 4 s)');
});

test('crowd: strollers wait at a closed zebra and cross once it opens', () => {
  const people = { createPerson: () => ({ group: new THREE.Group(), setAnim() {}, setSpeed() {}, setProp() {}, update() {} }) };
  const hold = { x0: -2.6, x1: 2.6, z0: -6.6, z1: 6.6, open: false };
  const prev = live.tier; setLive('high');
  const c = createCrowd({ count: 10, seed: 3, people, solids: [], holds: [hold] });
  c.setPaths([[{ x: 0, z: -20 }, { x: 0, z: -8 }, { x: 0, z: 8 }, { x: 0, z: 20 }]]);
  c.setMode('stroll');
  const inside = (p) => p.x > hold.x0 && p.x < hold.x1 && p.z > hold.z0 && p.z < hold.z1;
  const left = new Set(), out = [];
  let entered = 0;
  for (let i = 0; i < 600; i++) { // 60 s closed: whoever is outside never steps in
    c.update(0.1, {});
    c.positions(out).forEach((p, k) => { if (!inside(p)) left.add(k); else if (left.has(k)) entered++; });
  }
  assert.equal(entered, 0);
  assert.ok(left.size === out.length);
  hold.open = true;
  let crossed = 0;
  for (let i = 0; i < 400; i++) { c.update(0.1, {}); c.positions(out).forEach((p) => { if (inside(p)) crossed++; }); }
  assert.ok(crossed > 0, 'they cross on the green');
  // a lower tier at run time shows fewer of the members built
  setLive('ultralow'); c.update(0.1, {});
  assert.equal(c.active, PROFILES.ultralow.crowdCap);
  setLive(prev); c.dispose();
});

test('every tier knob the engine and levels read exists in the table', async () => {
  const { readdirSync, readFileSync } = await import('node:fs');
  const dirs = ['dist/engine', 'dist/levels'].map(d => new URL(`../${d}/`, import.meta.url));
  const used = new Set();
  for (const d of dirs) for (const f of readdirSync(d)) {
    if (!f.endsWith('.js')) continue;
    const src = readFileSync(new URL(f, d), 'utf8');
    for (const m of src.matchAll(/(?:kit\.Q\??\.|live\.P\.|P0\.|profileOf\([^)]*\)\.)([a-zA-Z]+)/g)) used.add(m[1]);
  }
  assert.ok(used.size >= 15, `found ${used.size} knobs`);
  for (const k of used) assert.ok(k in PROFILES.high, `unknown tier knob "${k}"`);
});

test('pixel ratio per tier: below native on 완전 낮음, up to 2 on 완전 높음', () => {
  assert.deepEqual(TIERS.map(q => pixelRatioFor(q, 1)), [0.75, 1, 1, 1, 1]);
  assert.deepEqual(TIERS.map(q => pixelRatioFor(q, 2)), [0.75, 1, 1.25, 1.5, 2]);
  assert.equal(pixelRatioFor('high', 0), 1); // no devicePixelRatio: 1
});

test('applyTier: small casters and extra shadowed lights drop on the lower tiers and come back above', async () => {
  const { applyTier } = await import('../dist/engine/tier-apply.js');
  const root = new THREE.Group();
  const big = new THREE.Mesh(new THREE.BoxGeometry(2, 2, 2)), cup = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.1, 0.08));
  const decal = new THREE.Mesh(new THREE.PlaneGeometry(1, 1)); // the level chose no shadow: never turned on
  big.castShadow = cup.castShadow = true; decal.castShadow = false;
  const lights = [0, 1, 2].map(() => { const l = new THREE.SpotLight(); l.castShadow = true; l.shadow.mapSize.set(1024, 1024); root.add(l); return l; });
  const plain = new THREE.PointLight(); root.add(big, cup, decal, plain);
  applyTier(root, 'ultralow');
  assert.deepEqual([big.castShadow, cup.castShadow, decal.castShadow], [false, false, false]);
  assert.deepEqual(lights.map(l => l.castShadow), [false, false, false]);
  applyTier(root, 'medium');
  assert.deepEqual([big.castShadow, cup.castShadow], [true, false]);
  assert.deepEqual(lights.map(l => l.castShadow), [true, false, false]);
  assert.equal(lights[0].shadow.mapSize.x, 512);
  applyTier(root, 'high');
  assert.deepEqual([big.castShadow, cup.castShadow, decal.castShadow, plain.castShadow], [true, true, false, false]);
  assert.deepEqual(lights.map(l => l.castShadow), [true, true, false]);
  assert.equal(lights[0].shadow.mapSize.x, 1024);
  applyTier(root, 'ultra');
  assert.deepEqual(lights.map(l => l.castShadow), [true, true, true]);
});

test('crowd: a car standing on the walk is walked round, never through', () => {
  const people = { createPerson: () => ({ group: new THREE.Group(), setAnim() {}, setSpeed() {}, setProp() {}, update() {} }) };
  const car = { x0: -1, x1: 1, z0: -2.3, z1: 2.3, y0: -0.5, y1: 1.8, id: 'movingCar', off: false };
  const prev = live.tier; setLive('high');
  const c = createCrowd({ count: 8, seed: 5, people, solids: [car] });
  c.setPaths([[{ x: 0, z: -20 }, { x: 0, z: 20 }]]); c.setMode('stroll');
  const out = []; let inside = 0;
  for (let i = 0; i < 900; i++) { c.update(0.1, {}); for (const p of c.positions(out)) if (p.x > car.x0 + 0.05 && p.x < car.x1 - 0.05 && p.z > car.z0 + 0.05 && p.z < car.z1 - 0.05) inside++; }
  assert.equal(inside, 0);
  setLive(prev); c.dispose();
});
