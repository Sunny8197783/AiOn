import test from 'node:test';
import assert from 'node:assert/strict';
import { produceGeos } from '../dist/levels/produce-geo.js';
import { readFileSync } from 'node:fs';

test('mart produce: seven distinct unit shapes with normals, inside ±1.3, light on the lowest tier', () => {
  for (const seg of [5, 7, 8, 10, 12]) {
    const geos = produceGeos(seg);
    assert.equal(geos.length, 7);
    const counts = geos.map((g) => g.attributes.position.count);
    assert.ok(new Set(geos.map((g) => g.uuid)).size === 7);
    for (const g of geos) {
      assert.ok(g.attributes.normal, 'has normals');
      assert.ok(g.attributes.position.array.every(Number.isFinite) && g.attributes.normal.array.every(Number.isFinite));
      g.computeBoundingBox(); const b = g.boundingBox;
      for (const v of [...b.min.toArray(), ...b.max.toArray()]) assert.ok(Math.abs(v) <= 1.3, `seg ${seg}: bbox ${v}`);
      const ext = b.max.clone().sub(b.min).toArray();
      assert.ok(Math.max(...ext) > 1.6 && Math.min(...ext) > 0.6, `unit-size, not degenerate: ${ext}`);
    }
    if (seg === 5) assert.ok(counts.reduce((a, n) => a + n, 0) / 3 / 7 <= 32, 'ultralow averages near the old 20-triangle balls');
  }
});

test('mart: produce keeps its rng order, the travelator solid covers the clad prism, shoppers start apart', () => {
  const src = readFileSync(new URL('../dist/levels/mart.js', import.meta.url), 'utf8');
  assert.match(src, /z0: -21\.95, z1: -6\.4, id: 'travelator'/);
  assert.match(src, /id: 'travelatorBack'/);
  assert.match(src, /r\(\); r\(\); const cart = i % 3 === 0, x = AISLES\[i % AISLES\.length\]/);
  assert.doesNotMatch(src, /IcosahedronGeometry/);
});
