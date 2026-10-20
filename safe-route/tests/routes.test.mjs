import test from 'node:test';
import assert from 'node:assert/strict';
import { shortestPath, routeOptions, explain } from '../dist/game/routes.js';
import { graph, CLOSURES } from '../dist/game/townmap.js';
import { createGame, buy, pack, CLOSURE_SETS } from '../dist/game/state.js';

const GATE_SETS = [
  { east: true, stairs: true, ramp: true, access: true },
  { east: false, stairs: true, ramp: true, access: false },
  { east: 'closed', stairs: 'open', ramp: 'open', access: 'open' },
  { east: false, stairs: false, ramp: false, access: true },
];

test('shortestPath finds the shortest way and its parts', () => {
  const adj = graph();
  const r = shortestPath(adj, 'home', 'store');
  assert.deepEqual(r.nodes, ['home', 'gate', 'I4', 'store']);
  assert.deepEqual(r.segments, ['aptPath', 'hanbitMid', 'yeonmuE']);
  assert.deepEqual(r.tags, []);
  assert.ok(r.length > 50 && r.length < 70);
  assert.deepEqual(shortestPath(adj, 'home', 'home'), { nodes: ['home'], length: 0, segments: [], tags: [] });
});

test('shortestPath edge cases: unknown nodes, unreachable, avoidTags, tagCost', () => {
  const adj = graph();
  assert.equal(shortestPath(adj, 'home', 'moon'), null);
  assert.equal(shortestPath(null, 'home', 'store'), null);
  assert.equal(shortestPath(graph({ gates: { east: false, stairs: false, ramp: false, access: false } }), 'home', 'shelter'), null);
  const viaA = shortestPath(adj, 'I3', 'shelter');
  assert.ok(viaA.tags.includes('stairs'));
  const noStairs = shortestPath(adj, 'I3', 'shelter', { avoidTags: ['stairs'] });
  assert.ok(!noStairs.tags.includes('stairs'));
  const pricey = shortestPath(adj, 'I3', 'shelter', { tagCost: { stairs: 100 } });
  assert.ok(!pricey.tags.includes('stairs'));
  assert.equal(shortestPath(adj, 'A', 'shelter', { tagCost: { stairs: Infinity, ramp: Infinity, level: Infinity } })?.tags.includes('stairs') ?? false, false);
});

test('routeOptions gives up to 3 distinct options with time', () => {
  const G = createGame({ seed: 1 });
  const opts = routeOptions(G, 'home', 'shelter', { closures: ['yeonmuCenter'], gates: { east: 'closed', access: 'closed' } });
  assert.ok(opts.length >= 2 && opts.length <= 3);
  assert.equal(new Set(opts.map(o => o.segments.join())).size, opts.length);
  assert.equal(opts[0].kind, 'shortest');
  for (const o of opts) {
    assert.equal(o.seconds, Math.round(o.length / 1.3));
    assert.ok(['stairs', 'ramp'].includes(o.entrance), o.entrance);
    assert.ok(!o.segments.some(s => CLOSURES.yeonmuCenter.segments.includes(s)));
    assert.ok(!o.segments.includes('gateEast') && !o.segments.includes('gateC'));
  }
  assert.ok(opts.every(o => o.length >= opts[0].length));
  assert.deepEqual(routeOptions(G, 'home', 'shelter', { gates: { east: false, stairs: false, ramp: false, access: false } }), []);
  assert.throws(() => routeOptions(G, 'home', 'shelter', { mobility: 'fly' }));
});

test('carrying over 8 kg slows walking; the bag is used when carryingKg is not given', () => {
  const G = createGame({ seed: 1 });
  const light = routeOptions(G, 'home', 'water')[0];
  const heavy = routeOptions(G, 'home', 'water', { carryingKg: 10 })[0];
  assert.equal(heavy.seconds, Math.round(light.length / (1.3 * 0.8)));
  buy(G, { water_2l: 3 }, { water_2l: 1100 });
  pack(G, 'water_2l', 5);
  assert.equal(routeOptions(G, 'home', 'water')[0].seconds, heavy.seconds);
  assert.equal(routeOptions(null, 'home', 'water')[0].seconds, light.seconds);
});

test('walker routes never use stairs or steep ways', () => {
  const sets = [[], ...CLOSURE_SETS.dday, ...CLOSURE_SETS.evac, Object.keys(CLOSURES)];
  let found = 0;
  for (const closures of sets) for (const gates of GATE_SETS) for (const [from, to] of [['cafe', 'shelter'], ['home', 'shelter'], ['home', 'assembly'], ['store', 'water']]) {
    for (const o of routeOptions(null, from, to, { closures, gates, mobility: 'walker' })) {
      found++;
      assert.ok(!o.tags.includes('stairs') && !o.tags.includes('steep'), `${from}->${to} ${o.segments}`);
      assert.equal(o.seconds, Math.round(o.length / 0.9));
    }
  }
  assert.ok(found > 50);
  const seoyeon = routeOptions(null, 'cafe', 'shelter', { gates: { east: false }, mobility: 'walker' })[0];
  assert.equal(seoyeon.entrance, 'access');
  assert.deepEqual(routeOptions(null, 'cafe', 'shelter', { gates: { access: false, east: false }, mobility: 'walker' }), []);
});

test('walking options include a step-free one when it differs', () => {
  const opts = routeOptions(null, 'home', 'shelter', { gates: { east: false } });
  assert.ok(opts.some(o => !o.tags.includes('stairs') && !o.tags.includes('steep')));
});

test('explain describes the route in Korean', () => {
  const o = routeOptions(null, 'home', 'shelter', { gates: { east: false, ramp: false, access: false } })[0];
  const s = explain(o);
  assert.match(s, /^103동 → 단지 정문/);
  assert.match(s, /대피소\(지하\)/);
  assert.match(s, /계단/);
  assert.match(s, /\d+m, 약 \d+분/);
  assert.equal(explain(null), '갈 수 있는 길이 없습니다.');
  assert.match(explain({ nodes: ['home', 'gate'], length: 22, tags: [] }), /103동 → 단지 정문 · 22m, 약 1분$/);
});
