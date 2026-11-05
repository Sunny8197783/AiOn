import test from 'node:test';
import assert from 'node:assert/strict';
import { doDocs, doEquip } from '../dist/story/prep-actions.js';
import { smokeEscape } from '../dist/story/escape.js';
import { createGame, buy, pack, bagLoad, bagCells } from '../dist/game/state.js';
import { evalBag } from '../dist/game/drills.js';
import { ripples } from '../dist/game/ripples.js';
import { CATALOG } from '../dist/game/items.js';

function mockCtx(G, { docs = null, engine = null } = {}) {
  const toasts = [], payoffs = [];
  const ctx = {
    G, toasts, payoffs, engine, hud: {},
    objective() {}, toast: (t) => toasts.push(t), event: (type, d) => G.events.push({ type, details: d, seq: G.events.length, day: G.day ?? 0, minute: G.minute ?? 0 }),
    modal: (fn) => fn(), roam: async ({ on }) => on.homeDesk?.(), ui: { docs: async () => docs, talk: async () => null },
    payoff: (prep, moment) => { payoffs.push(prep); ctx.event('payoff', { prep, moment }); }, stat() {},
  };
  return ctx;
}

test('an empty contact card (nothing ticked, or Esc) is not a written card', async () => {
  for (const res of [null, { card: {}, where: 'bag' }]) {
    const G = createGame({ seed: 3 }), ctx = mockCtx(G, { docs: res });
    await doDocs(ctx);
    assert.ok(!G.know.contactsCard);
    assert.ok(!G.items.contact_card || G.items.contact_card.home + G.items.contact_card.bag === 0, 'no card item');
    assert.ok(!G.events.some(e => e.type === 'docs_done'));
    assert.ok(ctx.toasts.includes('아무것도 적지 않았다.'));
  }
  const G = createGame({ seed: 3 }), ctx = mockCtx(G, { docs: { card: { family: '부모님' }, where: 'bag' } });
  await doDocs(ctx);
  assert.equal(G.know.contactsCard, true);
  assert.equal(G.items.contact_card.bag, 1);
});

test('the bag room limit is the cell grid: over.volume fires when the grid overflows', () => {
  const G = createGame({ seed: 3 }); G.money = 1e7;
  buy(G, { water_2l: 5 }, Object.fromEntries(Object.entries(CATALOG).map(([id, c]) => [id, c.price])));
  pack(G, 'water_2l', 6); // 6 x (3x2 cells) = 36 of 50 cells, and no room for a 3x2 piece left
  assert.deepEqual(bagCells(G), { used: 36, total: 50, fits: true });
  assert.equal(bagLoad(G).over.volume, false);
  pack(G, 'water_2l', 1); // a seventh does not fit, though 7 x 2.2 L is far under 25 L
  assert.equal(bagCells(G).fits, false);
  assert.ok(bagLoad(G).volumeL < 25);
  assert.equal(bagLoad(G).over.volume, true);
  assert.equal(evalBag(G, 'alert').ok, false);
});

test('gas valve check and the wet towel pay off in the smoke escape', async () => {
  globalThis.document ??= { getElementById: () => null };
  const G = createGame({ seed: 3 });
  const equip = mockCtx(G);
  equip.roam = async ({ on }) => { await on.homeGas(); };
  await doEquip(equip);
  assert.equal(G.know.gasValve, true);
  G.know.tubFilled = true; equip.event('tub_filled', {});
  const player = { y: 5.6 }, ctx = mockCtx(G, { engine: { player, level: { id: 'apartment' } } });
  ctx.roam = async ({ tick, on }) => { await on.homeSink(); player.y = 0.5; return tick(0.1); };
  const res = await smokeEscape(ctx);
  assert.equal(res.towel, true);
  assert.deepEqual(ctx.payoffs, ['gas', 'tub', 'towel']);
  const rows = ripples(G).map(r => r.prep);
  assert.ok(rows.includes('gas') && rows.includes('towel'), rows.join());
});
