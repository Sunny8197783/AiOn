import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { TYPES, vehicleGeo, bodyShape } from '../dist/levels/vehicle-geo.js';
import * as traffic from '../dist/levels/town-traffic.js';
import { bagGeo } from '../dist/levels/town-props.js';

const tris = (g) => (g.index ? g.index.count : g.attributes.position.count) / 3;
const box = (g) => { g.computeBoundingBox(); return g.boundingBox; };
const finite = (g) => g.attributes.position.array.every(Number.isFinite) && g.attributes.normal.array.every(Number.isFinite);

test('town traffic re-exports the shared vehicle models (one source for town, shelter and window view)', () => {
  assert.equal(traffic.TYPES, TYPES);
  assert.equal(traffic.vehicleGeo, vehicleGeo);
  assert.deepEqual(Object.keys(TYPES), ['sedan', 'suv', 'truck']);
});

test('every vehicle part is a clean geometry; wheels on the ground, front bumper at -z', () => {
  for (const [k, T] of Object.entries(TYPES)) for (const lo of [false, true]) {
    const g = vehicleGeo(T, lo);
    for (const part of ['body', 'glass', 'dark', 'lamp']) {
      assert.ok(g[part] && finite(g[part]), `${k} ${part} finite`);
      assert.ok(g[part].attributes.uv, `${k} ${part} has uv (instanced with textured materials)`);
    }
    const b = box(g.body);
    assert.ok(Math.abs(box(g.dark).min.y) < 1e-6, `${k}: tyres touch y = 0`);
    assert.ok(Math.abs(b.max.z - b.min.z - T.L) < 0.15, `${k}: body length ~ L`);
    if (lo) assert.ok(tris(g.body) + tris(g.dark) < tris(vehicleGeo(T).body) + tris(vehicleGeo(T).dark), `${k}: lo is lighter`);
  }
});

test('the glass lies on the body (not under its bevel) and never pokes through the roof', () => {
  for (const k of ['sedan', 'suv']) {
    const T = TYPES[k], g = vehicleGeo(T), gb = box(g.glass), bb = box(g.body);
    assert.ok(gb.max.y < bb.max.y, `${k}: glass top ${gb.max.y} under roof ${bb.max.y}`);
    assert.ok(gb.max.x >= bb.max.x - 0.01, `${k}: side glass flush with or outside the doors`);
    // the windscreen: straight down through its middle the glass is met above the body
    const [a, c] = [T.glass[0], T.glass[1]], s = (a[0] + c[0]) / 2, z = s - T.L / 2;
    const ray = new THREE.Raycaster(new THREE.Vector3(0, 3, z), new THREE.Vector3(0, -1, 0));
    const hit = (geo) => ray.intersectObject(new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ side: THREE.DoubleSide })))[0]?.distance ?? Infinity;
    assert.ok(hit(g.glass) < hit(g.body), `${k}: windscreen visible from above (glass ${hit(g.glass)} vs body ${hit(g.body)})`);
  }
  assert.ok(bodyShape(TYPES.sedan).getPoints().length > TYPES.sedan.top.length, 'arches add points to the outline');
});

test('종량제 bag: base on the ground, knot on top, no heavier than the old icosahedron on the lowest tier', () => {
  for (const lv of [0, 1, 2]) {
    const g = bagGeo(lv), b = box(g);
    assert.ok(finite(g), `lv ${lv} finite`);
    assert.ok(Math.abs(b.min.y) < 1e-6 && b.max.y > 0.5 && b.max.y < 0.62, `lv ${lv} height ${b.max.y}`);
    assert.ok(b.max.x - b.min.x < 0.6 && b.max.z - b.min.z < 0.6, `lv ${lv} footprint`);
  }
  assert.ok(tris(bagGeo(0)) <= 80, `lv 0: ${tris(bagGeo(0))} triangles (icosahedron detail 1: 80)`);
  assert.ok(tris(bagGeo(2)) < 400);
});
