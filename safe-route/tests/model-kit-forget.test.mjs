import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { createKit } from '../dist/engine/kit.js';

// kit.lit / setLit / gateLit / forget work on plain objects: no canvas or WebGL needed.
const emissive = (v = 1) => new THREE.MeshStandardMaterial({ emissive: 0xffffff, emissiveIntensity: v });

test('lit registers an object once, setLit and gateLit behave as before', () => {
  const kit = createKit(null), m = emissive(2);
  kit.lit('shops', m, 1.5);
  kit.lit('shops', m, 9); kit.lit('signs', m, 9); // handed in again: ignored, base stays 1.5
  kit.setLit('shops', true, 0.5); assert.equal(m.emissiveIntensity, 0.75);
  kit.setLit('signs', false); assert.equal(m.emissiveIntensity, 0.75); // never joined 'signs'
  kit.gateLit(m, false); assert.equal(m.emissiveIntensity, 0);
  kit.gateLit(m, true); assert.equal(m.emissiveIntensity, 0.75);
  kit.setLit('shops', false); assert.equal(m.emissiveIntensity, 0);
  const L = new THREE.PointLight(0xffffff, 6);
  kit.lit('street', L); kit.setLit('street', true, 0.5); assert.equal(L.intensity, 3);
});

test('forget drops a build\'s lights and materials but keeps memoized ones', () => {
  const kit = createKit(null);
  const shared = kit.memo('test:shared', () => kit.lit('windows', emissive(), 1.1));
  const build = () => {
    const root = new THREE.Group(), own = kit.lit('windows', emissive(), 1.1), L = kit.lit('street', new THREE.PointLight(0xffffff, 4), 4);
    const inner = new THREE.Group(); inner.add(L, new THREE.Mesh(new THREE.BoxGeometry(), [own, shared]));
    root.add(inner, new THREE.Mesh(new THREE.BoxGeometry(), kit.memo('test:shared', () => null)));
    return { root, own, L };
  };
  const a = build(), b = build();
  assert.equal(kit.forget(a.root), 2); // a's lamp and material; the memoized one stays
  assert.equal(kit.forget(a.root), 0);
  kit.setLit('windows', true, 0.5); kit.setLit('street', true, 0.5);
  assert.equal(shared.emissiveIntensity, 0.55); assert.equal(b.own.emissiveIntensity, 0.55); assert.equal(b.L.intensity, 2);
  a.own.emissiveIntensity = 7; a.L.intensity = 7; kit.setLit('windows', false); kit.setLit('street', false);
  assert.equal(a.own.emissiveIntensity, 7); assert.equal(a.L.intensity, 7); // no longer driven
  assert.equal(b.L.intensity, 0); assert.equal(shared.emissiveIntensity, 0);
  kit.gateLit(a.L, false); assert.equal(a.L.intensity, 7); // gateLit on a forgotten object: no-op
  kit.lit('street', a.L, 4); kit.setLit('street', true, 1); assert.equal(a.L.intensity, 4); // can be registered again
  assert.equal(kit.forget([b.root, a.root]), 3); // a list of roots
  assert.equal(kit.forget(null), 0);
});
