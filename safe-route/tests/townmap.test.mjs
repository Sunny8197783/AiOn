import test from 'node:test';
import assert from 'node:assert/strict';
import { NODES, EDGES, CLOSURES, SHELTER_ENTRANCES, PLACES, SPAWNS, graph } from '../dist/game/townmap.js';
import { conditions, CLOSURE_SETS } from '../dist/game/state.js';

const segs = new Set(EDGES.map(e => e[2]));

function reachable(adj, from, to) {
  const seen = new Set([from]), todo = [from];
  while (todo.length) for (const e of adj[todo.pop()]) if (!seen.has(e.to)) { seen.add(e.to); todo.push(e.to); }
  return seen.has(to);
}

// Worst case on any night: east gate closed, entrance C still shut (opens 20:16).
const WORST_GATES = { east: false, stairs: true, ramp: true, access: false };

test('every edge endpoint is a node and segment ids are unique', () => {
  for (const [a, b, seg, tags] of EDGES) {
    assert.ok(NODES[a], `edge ${seg}: unknown node ${a}`);
    assert.ok(NODES[b], `edge ${seg}: unknown node ${b}`);
    assert.ok(Array.isArray(tags));
  }
  assert.equal(segs.size, EDGES.length);
});

test('every closure segment exists, every shelter entrance is wired', () => {
  for (const [id, c] of Object.entries(CLOSURES)) {
    assert.ok(c.segments.length, id);
    for (const s of c.segments) assert.ok(segs.has(s), `closure ${id}: unknown segment ${s}`);
  }
  for (const [k, e] of Object.entries(SHELTER_ENTRANCES)) {
    assert.ok(NODES[e.node], k);
    assert.ok(PLACES[e.place], k);
    const edge = EDGES.find(x => x[2] === e.segment);
    assert.ok(edge && edge.includes('shelter'), `${k} segment must lead into the shelter`);
  }
  for (const id of ['home', 'shelter']) assert.ok(SPAWNS[id], id);
});

test('graph() is symmetric and drops closed segments and gates', () => {
  const open = graph();
  for (const [a, es] of Object.entries(open)) for (const e of es) assert.ok(open[e.to].some(x => x.to === a && x.seg === e.seg));
  const g = graph({ closures: ['yeonmuCenter'], gates: WORST_GATES });
  const all = Object.values(g).flat().map(e => e.seg);
  for (const s of ['yeonmuC1', 'yeonmuC2', 'gateEast', 'gateC']) assert.ok(!all.includes(s), s);
  assert.ok(all.includes('gateA'));
});

test('conditions only ever produce closure sets from CLOSURE_SETS', () => {
  const key = (l) => [...l].sort().join('+');
  for (let seed = 1; seed <= 500; seed++) {
    const c = conditions(seed).closures;
    assert.ok(CLOSURE_SETS.dday.some(l => key(l) === key(c.dday)), `seed ${seed} dday`);
    assert.ok(CLOSURE_SETS.evac.some(l => key(l) === key(c.evac)), `seed ${seed} evac`);
  }
});

test('home -> shelter stays reachable on foot for every closure set (seeds 1..500)', () => {
  for (let seed = 1; seed <= 500; seed++) {
    const c = conditions(seed).closures;
    for (const set of [c.dday, c.evac]) {
      assert.ok(reachable(graph({ closures: set, gates: WORST_GATES }), 'home', 'shelter'), `seed ${seed}: ${set}`);
    }
  }
  for (const set of [...CLOSURE_SETS.dday, ...CLOSURE_SETS.evac]) {
    assert.ok(reachable(graph({ closures: set, gates: WORST_GATES }), 'home', 'shelter'), String(set));
  }
});

test('home -> assembly point stays reachable under every evac closure set', () => {
  for (const set of CLOSURE_SETS.evac) assert.ok(reachable(graph({ closures: set }), 'home', 'assembly'), String(set));
});

test('the reachability check can fail (sanity)', () => {
  const g = graph({ closures: ['yeonmuCenter', 'danji', 'sijang'], gates: WORST_GATES });
  assert.equal(reachable(g, 'home', 'shelter'), false);
});
