import test from 'node:test';
import assert from 'node:assert/strict';
import { segBox, ceilAt, collide } from '../dist/engine/geom.js';
import { gridOf, segHit, headroomAt, camTop } from '../dist/engine/space.js';
import { planVault } from '../dist/engine/vault.js';
import { createPicker, hidden, reachOf } from '../dist/engine/reach.js';
import { pushOut } from '../dist/engine/bodies.js';

// The array-based segBox it replaced, kept here as the reference.
function segBoxRef(o, d, s, pad) {
  let t0 = 0, t1 = 1;
  const lo = [s.x0 - pad, s.y0 - pad, s.z0 - pad], hi = [s.x1 + pad, s.y1 + pad, s.z1 + pad], O = [o.x, o.y, o.z], D = [d.x, d.y, d.z];
  if (O[0] > lo[0] && O[0] < hi[0] && O[1] > lo[1] && O[1] < hi[1] && O[2] > lo[2] && O[2] < hi[2]) return Infinity;
  for (let i = 0; i < 3; i++) {
    if (Math.abs(D[i]) < 1e-9) { if (O[i] < lo[i] || O[i] > hi[i]) return Infinity; continue; }
    let a = (lo[i] - O[i]) / D[i], b = (hi[i] - O[i]) / D[i];
    if (a > b) [a, b] = [b, a];
    t0 = Math.max(t0, a); t1 = Math.min(t1, b);
    if (t0 > t1) return Infinity;
  }
  return t0;
}
function rnd(seed) { let a = seed >>> 0; return () => { a = (a + 0x6D2B79F5) >>> 0; let t = Math.imul(a ^ (a >>> 15), a | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }
const box = (x0, x1, z0, z1, y1 = 2.9, y0 = 0, extra = {}) => ({ x0, x1, z0, z1, y0, y1, ...extra });

test('scalar segBox matches the array version', () => {
  const r = rnd(7);
  for (let i = 0; i < 20000; i++) {
    const s = box(r() * 4 - 2, 0, r() * 4 - 2, 0, r() * 3, r() - 0.5); s.x1 = s.x0 + r() * 2; s.z1 = s.z0 + r() * 2;
    const o = { x: r() * 8 - 4, y: r() * 3, z: r() * 8 - 4 };
    const d = { x: r() < 0.1 ? 0 : r() * 8 - 4, y: r() < 0.3 ? 0 : r() * 2 - 1, z: r() < 0.1 ? 0 : r() * 8 - 4 }, pad = r() < 0.3 ? 0 : r() * 0.3;
    assert.equal(segBox(o, d, s, pad), segBoxRef(o, d, s, pad));
  }
});

test('grid queries match a brute-force filter, follow moving solids and switched gates', () => {
  const r = rnd(3), solids = [];
  for (let i = 0; i < 400; i++) { const x = r() * 200 - 100, z = r() * 200 - 100; solids.push(box(x, x + r() * 6, z, z + r() * 6, 2, 0, { off: r() < 0.1 })); }
  solids.push(box(-150, 150, -150, -149)); // a long wall: in many cells
  const g = gridOf(solids);
  const brute = (x, z, R) => solids.filter((s) => s && !s.off && s.x0 - R < x && s.x1 + R > x && s.z0 - R < z && s.z1 + R > z);
  for (let i = 0; i < 300; i++) {
    const x = r() * 220 - 110, z = r() * 220 - 110;
    assert.deepEqual(g.nearby(x, z, 3), brute(x, z, 3));
    const q = g.query(x - 2, x + 2, z - 2, z + 2), want = solids.filter((s) => !s.off && s.x0 <= x + 2 && s.x1 >= x - 2 && s.z0 <= z + 2 && s.z1 >= z - 2);
    assert.deepEqual(q, want);
  }
  // A car drives in, a gate closes: the cached cell list still sees both.
  const car = box(0, 0, 0, 0, 1.8, -0.5, { id: 'movingCar' }); solids.push(car);
  const gate = solids[5]; gate.off = true;
  gridOf(solids); g.nearby(gate.x0, gate.z0, 3);
  gate.off = false; Object.assign(car, { x0: 50, x1: 52, z0: 50, z1: 55 });
  for (let i = 0; i < 4; i++) gridOf(solids); // each call re-checks a quarter of the solids
  assert.ok(gridOf(solids).nearby(51, 52, 3).includes(car));
  assert.ok(gridOf(solids).nearby(gate.x0, gate.z0, 3).includes(gate));
  assert.equal(segHit(gridOf(solids), { x: 51, y: 1, z: 40 }, { x: 0, y: 0, z: 20 }, 0) < 1, true);
});

test('ceilings: lowest one above, indoor default, camera kept under it', () => {
  const ceilings = [{ x0: 0, x1: 10, z0: 0, z1: 10, y: 2.3 }, { x0: 0, x1: 10, z0: 0, z1: 10, y0: 5.1 }];
  assert.equal(ceilAt(ceilings, 5, 5, 0), 2.3);
  assert.equal(ceilAt(ceilings, 5, 5, 2.8), 5.1);
  assert.equal(ceilAt(ceilings, 20, 5, 0), Infinity);
  const flat = { id: 'store', walk: [], solids: [] };
  assert.equal(headroomAt(flat, 1, 1, 0), 2.3); // indoors without data: the lowest interior room
  assert.equal(headroomAt({ id: 'town' }, 1, 1, 0), Infinity);
  assert.equal(headroomAt({ id: 'mart', ceilings: [{ x0: -9, x1: 9, z0: -9, z1: 9, y: 3.6 }] }, 1, 1, 0), 3.6);
  const over = [box(-1, 1, -1, 1, 4, 2.6)]; // a landing above the head
  assert.ok(Math.abs(camTop({ id: 'town' }, gridOf(over), 0, 0, 0, 1.7) - 2.45) < 1e-9);
  assert.ok(Math.abs(camTop({ id: 'apartment', ceilings }, null, 5, 5, 0, 1.7) - 2.15) < 1e-9);
});

const room = (extra = {}) => ({ id: 'store', walk: [{ x0: -10, x1: 10, z0: -10, z1: 10, y: 0 }], solids: [], ...extra });
const plan = (L, x, z, yaw, feet = 0) => planVault({ x, z, feet, dx: -Math.sin(yaw), dz: -Math.cos(yaw), L, grid: gridOf(L.solids) });

test('vault: low fence is crossed straight, tall or thick things are not', () => {
  const town = { id: 'town', walk: [{ x0: -10, x1: 10, z0: -10, z1: 10, y: 0 }], solids: [box(-5, 5, -0.05, 0.05, 1.0)] };
  const v = plan(town, 0.3, 0.7, Math.PI / 5); // oblique approach from +z: crosses along z
  assert.equal(v.kind, 'over'); assert.ok(v.z1 < -0.4 && Math.abs(v.x1 - 0.3) < 1e-9);
  town.solids[0].y1 = 1.7; // a tall fence: neither over nor onto (thin)
  assert.equal(plan(town, 0.3, 0.7, 0), null);
  const car = { id: 'town', walk: town.walk, solids: [box(-1, 1, -2.2, 2.2, 1.5)] }; // a parked car from the side: onto the roof
  const c = plan(car, 1.6, 0, Math.PI / 2);
  assert.equal(c.kind, 'onto'); assert.equal(c.y1, 1.5);
});

test('vault: never through or onto a wall', () => {
  // A 0.31 m wall hit at 45 degrees from a 1.0 m cabinet top: the slanted ray reads 0.44 m, the wall is thinner.
  const L = room({ solids: [box(8.97, 9.28, -10, 10, 2.75), box(7.9, 8.6, 0, 0.6, 1.0)] });
  assert.equal(plan(L, 8.6, 0.3, -Math.PI / 4, 1.0), null);
  assert.equal(plan(L, 8.6, 0.3, -Math.PI / 2, 1.0), null);
  // Store front wall 0.55 m thick, 1.8 m above a 1.1 m counter: no standing on it.
  const S = room({ solids: [box(-5, 5, -0.15, 0.4, 2.9), box(0.2, 1.4, -1.6, -0.7, 1.1)] });
  assert.equal(plan(S, 0.6, -1.1, 0, 1.1), null);
  // A low cabinet in front of a wall: no vault through the wall behind it.
  const W = room({ solids: [box(-5, 5, 0.6, 0.84, 2.3), box(-1, 1, 0, 0.5, 0.9)] });
  const w = plan(W, 0, -0.5, Math.PI);
  assert.ok(!w || w.kind === 'onto'); // the cabinet top at most (headroom permitting), never past z 0.84
  if (w) assert.ok(w.z1 < 0.6);
});

test('vault: onto furniture needs 1.8 m of headroom', () => {
  const ceil = [{ x0: -10, x1: 10, z0: -10, z1: 10, y: 2.3 }];
  const L = room({ id: 'apartment', ceilings: ceil, solids: [box(-0.6, 0.6, -2.0, -1.0, 1.85)] }); // a fridge
  assert.equal(plan(L, 0, -0.5, 0), null);
  const bed = room({ id: 'apartment', ceilings: [{ x0: -10, x1: 10, z0: -10, z1: 10, y: 3.0 }], solids: [box(-1, 1, -3, -1, 0.5)] });
  assert.equal(plan(bed, 0, -0.5, 0).kind, 'onto');
  const town = { id: 'town', walk: room().walk, solids: [box(-0.6, 0.6, -2.0, -1.0, 1.85), box(-1, 1, -2, -1, 4, 3.0)] }; // a canopy over it
  assert.equal(plan(town, 0, -0.5, 0), null);
});

test('vault: landing must be on a walk zone at about the same level', () => {
  const L = { id: 'town', walk: [{ x0: -10, x1: 10, z0: 0, z1: 10, y: 5 }, { x0: -10, x1: 10, z0: -10, z1: 0, y: 0 }], solids: [box(-5, 5, -0.1, 0.1, 6.0, 0)] };
  assert.equal(plan(L, 0, 0.6, 0, 5), null); // a parapet over a 5 m drop: not a vault
});

test('prompts: reach at least 1.6 m, wide view cone, walls hide, own solid does not', () => {
  const pk = createPicker(), pos = { x: 0, y: 0, z: 0 };
  const p = { id: 'coffee', x: 0, y: 0, z: -1.5, r: 1.0 };
  assert.equal(reachOf(p), 1.6);
  const L = { solids: [box(-1, 1, -2, -1.2, 1.1)] }; // the counter holding it
  assert.equal(pk.pick([p], pos, [0, 0], gridOf(L.solids)), p);
  assert.equal(pk.pick([p], pos, [Math.PI * 0.95, Math.PI * 0.95], gridOf(L.solids)), null); // turned away
  pos.z = -0.4; assert.equal(pk.pick([p], pos, [Math.PI, Math.PI], gridOf(L.solids)), p); // close: any facing
  pos.z = 0;
  const wall = { solids: [box(-3, 3, -0.8, -0.6, 2.9)] };
  assert.equal(hidden(p, pos, gridOf(wall.solids)), true);
  const gate = { solids: [box(-3, 3, -1.4, -1.0, 3, -3, { id: 'gateEast' })] }; // the barrier the prompt belongs to
  assert.equal(hidden(p, pos, gridOf(gate.solids)), false); // abuts the point (0.1 m away)
  const far = { x: 0, y: 0, z: -2.4, r: 2.6 }, bar = [box(-3, 3, -1.4, -1.0, 3, -3, { id: 'gateEast' })];
  assert.equal(hidden(far, pos, gridOf(bar)), true);
  assert.equal(hidden({ ...far, host: 'gateEast' }, pos, gridOf(bar)), false);
  const low = { solids: [box(-3, 3, -0.8, -0.6, 0.9)] }; // a low shelf alone does not hide
  assert.equal(hidden(p, pos, gridOf(low.solids)), false);
});

test('prompts: neighbours do not flicker, a clearly better one wins', () => {
  const pk = createPicker(), g = gridOf([]);
  const a = { id: 'a', x: -0.3, y: 0, z: -1 }, b = { id: 'b', x: 0.3, y: 0, z: -1 };
  const pos = { x: -0.05, y: 0, z: 0 };
  assert.equal(pk.pick([a, b], pos, [0, 0], g), a);
  pos.x = 0.05; assert.equal(pk.pick([a, b], pos, [0, 0], g), a); // b is a bit better now: keep a
  pos.x = 0.6; assert.equal(pk.pick([a, b], pos, [0, 0], g), b);
  b.enabled = false; assert.equal(pk.pick([a, b], pos, [0, 0], g), a);
});

test('people push the player out and the push is idempotent', () => {
  const [x, z, pushed] = pushOut(0.1, 0, 0.32, [{ x: 0, z: 0, r: 0.24 }]);
  assert.ok(pushed); assert.ok(Math.abs(Math.hypot(x, z) - 0.56) < 1e-9);
  assert.deepEqual(pushOut(x + 1e-6, z, 0.32, [{ x: 0, z: 0, r: 0.24 }]).slice(2), [false]);
  const [cx] = collide(0, 0, 0, [box(-0.1, 0.1, -0.1, 0.1, 1)], 0.32); assert.ok(Math.abs(cx) >= 0.42 - 1e-9);
});
