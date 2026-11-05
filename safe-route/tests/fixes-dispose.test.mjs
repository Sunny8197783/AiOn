import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { disposeLevel } from '../dist/engine/tier-apply.js';
import { PROFILES, TIERS, heldAdapt, ADAPT_HOLD } from '../dist/engine/quality.js';
import { PRESETS } from '../dist/engine/presets.js';
import { signalAt } from '../dist/levels/town-plan.js';
import { createTownCrowd } from '../dist/levels/town-crowd.js';

const disposed = (x) => { let n = 0; x.addEventListener('dispose', () => n++); return () => n; };

test('disposeLevel frees what only the dropped build uses and keeps what live roots share', () => {
  const shared = new THREE.MeshStandardMaterial({ map: new THREE.Texture({ width: 1, height: 1 }) });
  const own = new THREE.MeshStandardMaterial({ map: new THREE.Texture({ width: 1, height: 1 }) });
  const rt = new THREE.WebGLRenderTarget(4, 4); own.envMap = rt.texture; // an environment map is never freed here
  const group = new THREE.Group(), live = new THREE.Group();
  const a = new THREE.Mesh(new THREE.BoxGeometry(), shared), b = new THREE.Mesh(new THREE.BoxGeometry(), own);
  const inst = new THREE.InstancedMesh(new THREE.BoxGeometry(), own, 3), light = new THREE.SpotLight();
  const bones = [new THREE.Bone()], skin = new THREE.SkinnedMesh(new THREE.BoxGeometry(), own); skin.add(bones[0]); skin.bind(new THREE.Skeleton(bones));
  let skelFreed = 0; skin.skeleton.dispose = () => { skelFreed++; };
  let lightFreed = 0; light.dispose = () => { lightFreed++; };
  const person = { group: new THREE.Group() }; group.add(a, b, inst, light, skin, person.group);
  live.add(new THREE.Mesh(new THREE.BoxGeometry(), shared));
  const n = { shared: disposed(shared), own: disposed(own), sharedMap: disposed(shared.map), ownMap: disposed(own.map), env: disposed(rt.texture), inst: disposed(inst), geo: disposed(b.geometry) };
  const order = [], persons = new Set([person, { group: new THREE.Group() }]);
  const kit = { forget: (root) => { order.push('forget'); assert.equal(root, group); } };
  const L = { id: 't', group, dispose() { order.push('dispose'); person.group.removeFromParent(); } }; // a crowd detaching its people
  disposeLevel(L, persons, { kit, keep: [live, group] });
  assert.deepEqual(order, ['forget', 'dispose']);
  assert.deepEqual([n.own(), n.ownMap(), n.inst(), n.geo(), skelFreed, lightFreed], [1, 1, 1, 1, 1, 1]);
  assert.deepEqual([n.shared(), n.sharedMap(), n.env()], [0, 0, 0], 'shared material, its texture and the render target stay');
  assert.equal(persons.size, 1, 'people detached by L.dispose still leave the persons set');
  assert.doesNotThrow(() => disposeLevel({ id: 'bare', group: new THREE.Group() }, null), 'no kit, no keep, no dispose');
});

test('tier knobs: low adapts, 완전 낮음 holds 높음\'s settled adaptation and dims the indoor key', () => {
  assert.ok(PROFILES.low.adapt && !PROFILES.ultralow.adapt);
  for (const q of TIERS) assert.ok(PROFILES[q].indoorKey > 0 && PROFILES[q].indoorKey <= 1, q);
  for (const q of TIERS) if (PROFILES[q].sun) assert.equal(PROFILES[q].indoorKey, 1, `${q} has a sun shadow: full key`);
  for (const p of Object.keys(PRESETS)) assert.ok(ADAPT_HOLD[p] >= 0.6 && ADAPT_HOLD[p] <= 1.35, p); // the adaptation range
  for (const [k, v] of Object.entries(ADAPT_HOLD)) { assert.ok(PRESETS[k.split(':').at(-1)], k); assert.ok(v >= 0.6 && v <= 1.35, k); }
  assert.equal(heldAdapt('interior', 'mart'), ADAPT_HOLD['mart:interior']);
  assert.equal(heldAdapt('interior', 'nowhere'), ADAPT_HOLD.interior);
  assert.equal(heldAdapt('nonsense'), 1);
});

test('town crowd: walk signals match signalAt, and dispose reaches every crowd', () => {
  let holds = null; const made = [];
  const people = { createCrowd(o) { holds = o.holds; const c = { group: new THREE.Group(), n: 0, setPaths() {}, setMode() {}, update() {}, positions: (out) => out, dispose() { c.n++; } }; made.push(c); return c; } };
  const crowd = createTownCrowd({ people, group: new THREE.Group(), walk: [] });
  crowd.setMode('busy');
  assert.ok(holds.length > 0 && made.length === 2);
  for (let t = -40; t < 120; t += 0.37) for (const on of [true, false]) {
    crowd.setSignals(t, on);
    const s = on ? signalAt(t) : null;
    for (const h of holds) assert.equal(h.open, !s || (s[h.group] === 'green' && s.left > 5), `t ${t.toFixed(2)} ${h.group} ${on}`);
  }
  crowd.dispose();
  assert.deepEqual(made.map((c) => c.n), [1, 1]);
});
