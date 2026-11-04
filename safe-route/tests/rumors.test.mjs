import test from 'node:test';
import assert from 'node:assert/strict';
import { RUMORS, rumor, posts, judge, believe, verify, misled, lostByRumor, seenRumors, rumorFacts } from '../dist/game/rumors.js';
import { createGame, conditions, recordEvent } from '../dist/game/state.js';
import { recordEncounter } from '../dist/game/drills.js';
import { fact } from '../dist/game/facts.js';
import { CLOSURES } from '../dist/game/townmap.js';

const LABELS = ['official', 'site', 'neighbor', 'rumor', 'self'];

test('every rumor set is complete and cites real facts', () => {
  assert.deepEqual(RUMORS.map(r => r.id), ['shelter_closed', 'camp_food', 'evac_shortcut']);
  const G = createGame({ seed: 4 });
  for (const r of RUMORS) {
    assert.ok(r.facts.length);
    for (const f of r.facts) assert.ok(fact(f), `${r.id}: ${f}`);
    assert.equal(rumorFacts(r.id).length, r.facts.length);
    assert.equal(r.items[0].key, 'claim');
    assert.equal(r.items[0].truth, false, 'the claim is false');
    for (const it of r.items) {
      assert.ok(it.from && it.meta && typeof it.truth === 'boolean' && LABELS.includes(it.label), `${r.id}.${it.key}`);
      assert.match(it.text(G), /[가-힣]/);
    }
    assert.ok(r.items.some(it => it.truth && it.label !== 'official'), `${r.id} has a true non-official item`);
    assert.ok(r.items.some(it => it.truth && it.label === 'official'), `${r.id} has the official line`);
  }
  assert.throws(() => rumor('nope'));
});

test('evac_shortcut always names a road that is closed, for every seed', () => {
  for (let s = 1; s <= 500; s++) {
    const G = createGame({ seed: s }), c = conditions(s);
    const id = rumor('evac_shortcut').road(G);
    assert.ok(c.closures.evac.includes(id), `seed ${s}: ${id}`);
    const [claim, manager] = posts(G, 'evac_shortcut');
    assert.ok(claim.text.includes(CLOSURES[id].label), claim.text);
    assert.ok(manager.text.includes(CLOSURES[id].label), 'the site line names the same road');
  }
});

test('judge records the pick and gives a ladder result', () => {
  const G = createGame({ seed: 1 });
  let r = judge(G, 'camp_food', 'rumor');
  assert.deepEqual(r, { ok: true, label: 'rumor', truth: false, result: 'sorted', facts: ['info_scope', 'info_rumor'] });
  r = judge(G, 'camp_food', 'official');
  assert.equal(r.ok, false); assert.equal(r.result, 'misled');
  assert.equal(judge(G, 'camp_food', 'neighbor', { item: 'seoyeon' }).ok, true);
  assert.equal(judge(G, 'camp_food', 'rumor', { item: 'seoyeon' }).ok, false, 'a true neighbor memo is not a rumor');
  const evs = G.events.filter(e => e.type === 'rumor_judged');
  assert.equal(evs.length, 4);
  assert.deepEqual(evs[0].details, { id: 'camp_food', item: 'claim', pick: 'rumor', ok: true });
  recordEncounter(G, 'info', r.result);
  assert.equal(G.skill.info.length, 1);
  assert.throws(() => judge(G, 'camp_food', 'gossip'));
  assert.throws(() => judge(G, 'camp_food', 'rumor', { item: 'nope' }));
});

test('believing a false claim misleads until it is checked; following it leads to lostWay', () => {
  const G = createGame({ seed: 1 });
  assert.equal(misled(G), null);
  assert.equal(believe(G, 'evac_shortcut'), true);
  assert.equal(misled(G), 'evac_shortcut');
  assert.equal(lostByRumor(G), null, 'not lost until the closed road is reached');
  recordEvent(G, 'rumor_followed', { id: 'evac_shortcut' });
  assert.equal(lostByRumor(G), 'evac_shortcut');
  const truths = verify(G, 'evac_shortcut');
  assert.ok(truths.length >= 2 && truths.every(p => p.truth));
  assert.equal(misled(G), null);
  assert.equal(lostByRumor(G), null);
  assert.deepEqual(seenRumors(G).map(s => s.id), ['evac_shortcut']);
  assert.equal(seenRumors(G)[0].claim.key, 'claim');
});
