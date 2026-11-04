import test from 'node:test';
import assert from 'node:assert/strict';
import { FENCES, CURB, SIDEWALKS, EVAC, ENTRY } from '../dist/levels/town-plan.js';
import { FENCE_SIZE, fenceSolid, sealTops, STAIR_A, stairTreads, zebraZones, busyZebras } from '../dist/levels/town-geo.js';
import { CROSSWALKS, ROADS } from '../dist/game/townmap.js';
import { gridOf } from '../dist/engine/space.js';
import { planVault } from '../dist/engine/vault.js';
import { segBox } from '../dist/engine/geom.js';

const fences = FENCES.filter(([x0, z0, x1, z1]) => Math.hypot(x1 - x0, z1 - z0) >= 0.05).map(l => fenceSolid(l, CURB));
const crosses = (a, b, s) => segBox({ x: a[0], y: 1, z: a[1] }, { x: b[0] - a[0], y: 0, z: b[1] - a[1] }, s, 0) <= 1;

test('fence colliders follow the mesh and are never vaulted or climbed', () => {
  for (const s of fences) {
    const kind = s.id.slice(6), [t, h] = FENCE_SIZE[kind], thin = Math.min(s.x1 - s.x0, s.z1 - s.z0);
    assert.ok(s.noVault && s.noClimb && s.seal, s.id);
    assert.ok(Math.abs(thin - 2 * t) < 1e-9 && Math.abs(s.y1 - (CURB + h)) < 1e-9, `${s.id} ${thin} ${s.y1}`);
  }
  assert.equal(FENCE_SIZE.hedge[0] * 2, 0.75); // the hedge mesh is 0.75 m deep: the body no longer sinks into it
  // Space in front of every fence, from both sides, straight at it: a plain jump (null), never over or onto it.
  const walk = [{ x0: -200, x1: 200, z0: -200, z1: 200, y: CURB }], L = { id: 'town', walk, solids: fences }, grid = gridOf(fences);
  for (const s of fences) {
    const alongX = s.x1 - s.x0 > s.z1 - s.z0, cx = (s.x0 + s.x1) / 2, cz = (s.z0 + s.z1) / 2;
    for (const side of [-1, 1]) {
      const x = alongX ? cx : (side < 0 ? s.x0 : s.x1) + side * 0.45, z = alongX ? (side < 0 ? s.z0 : s.z1) + side * 0.45 : cz;
      const v = planVault({ x, z, feet: CURB, dx: alongX ? 0 : -side, dz: alongX ? -side : 0, L, grid, walk });
      assert.equal(v, null, `${s.id} at ${x},${z}`);
    }
  }
});

test('sealTops keeps a fence top out of jump reach of what stands next to it', () => {
  const box = (x0, x1, z0, z1, y1, extra = {}) => ({ x0, x1, z0, z1, y0: -0.5, y1, ...extra });
  const mesh = () => fenceSolid([0, 0, 20, 0, 'mesh'], CURB), park = () => fenceSolid([0, 0, 20, 0, 'park'], CURB);
  let f = mesh(); sealTops([f, box(5, 7, -5, -0.5, 1.8, { id: 'car' })]);
  assert.ok(f.y1 >= 1.8 + 0.55, 'raised over a car roof (1.8 m + a 0.54 m jump)');
  f = mesh(); sealTops([f, box(5, 7, -12, -8, 1.8, { id: 'car' })]);
  assert.equal(f.y1, CURB + FENCE_SIZE.mesh[1], 'a car 8 m away changes nothing');
  f = park(); sealTops([f, box(5, 6, -1, -0.3, 2, { id: 'vending', noClimb: true })]);
  assert.equal(f.y1, CURB + FENCE_SIZE.park[1], 'nobody gets onto a vending machine that cannot be climbed');
  f = park(); sealTops([f, box(5, 6, -1, -0.3, 2, { id: 'vending', noClimb: true }), box(6.5, 8.5, -6, -1.5, 1.8, { id: 'car' })]);
  assert.ok(f.y1 >= 2.55, 'unless a car roof beside it is a jump below its top');
  f = mesh(); sealTops([f, { x0: 2, x1: 4, z0: -3, z1: -1, y: 1.6 }].slice(0, 1), [{ x0: 2, x1: 4, z0: -3, z1: -1, y: 1.6 }]);
  assert.ok(f.y1 >= 2.2, 'raised walk zones count too');
});

test('stair A: seven equal risers, six 0.28 m treads and a landing in front of the door', () => {
  const t = stairTreads(CURB);
  assert.equal(t.length, STAIR_A.n);
  t.forEach((s, i) => {
    assert.ok(Math.abs(s.top - (CURB - STAIR_A.rise * (i + 1))) < 1e-9, `riser ${i}`);
    assert.ok(Math.abs(s.z0 - (i ? t[i - 1].z1 : STAIR_A.zt)) < 1e-9, `tread ${i} follows the one above`);
    if (i < t.length - 1) assert.ok(Math.abs(s.z1 - s.z0 - STAIR_A.run) < 1e-9);
  });
  const land = t.at(-1);
  assert.ok(land.z1 === STAIR_A.door && land.z1 - land.z0 >= 0.7, `landing ${land.z1 - land.z0} m`);
});

test('cars see people on a zebra, not people on the sidewalk beside it', () => {
  const zones = zebraZones(CROSSWALKS, ROADS), k = CROSSWALKS.findIndex(c => c.road === 'yeonmu' && c.at === 0);
  assert.ok(busyZebras(zones, [{ x: 0.7, z: -4 }]).has(k));
  assert.ok(busyZebras(zones, [{ x: 0.5, z: 6.4 }]).has(k), 'stepping off the curb counts');
  assert.equal(busyZebras(zones, [{ x: 0, z: 8 }, { x: 10, z: 0 }]).size, 0);
});

test('crowd paths never run through a fence, and every sidewalk has an evacuation path on its own side', () => {
  const paths = [...SIDEWALKS, ...EVAC.map(([, p]) => p)];
  for (const p of paths) for (let i = 1; i < p.length; i++) for (const s of fences) assert.ok(!crosses(p[i - 1], p[i], s), `${p[i - 1]} -> ${p[i]} crosses ${s.id}`);
  for (const [g, p] of EVAC) assert.ok(Math.hypot(p.at(-1)[0] - ENTRY[g].x, p.at(-1)[1] - ENTRY[g].z) < 0.01, `${g} path ends at its entrance`);
  // A stroller keeps their place when the siren mode starts and heads for the nearest point of the evacuation paths.
  const nearest = (x, z) => {
    let best = null, bd = Infinity;
    for (const [, p] of EVAC) for (let i = 1; i < p.length; i++) {
      const [ax, az] = p[i - 1], [bx, bz] = p[i], dx = bx - ax, dz = bz - az, t = Math.max(0, Math.min(1, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)));
      const q = [ax + dx * t, az + dz * t], d = Math.hypot(q[0] - x, q[1] - z); if (d < bd) { bd = d; best = q; }
    }
    return best;
  };
  for (const p of SIDEWALKS) for (let i = 1; i < p.length; i++) {
    const [ax, az] = p[i - 1], [bx, bz] = p[i], n = Math.ceil(Math.hypot(bx - ax, bz - az) / 2);
    for (let k = 0; k <= n; k++) {
      const x = ax + (bx - ax) * k / n, z = az + (bz - az) * k / n, q = nearest(x, z);
      for (const s of fences) assert.ok(!crosses([x, z], q, s), `stroller at ${x.toFixed(1)},${z.toFixed(1)} -> ${q.map(v => v.toFixed(1))} crosses ${s.id}`);
    }
  }
});
