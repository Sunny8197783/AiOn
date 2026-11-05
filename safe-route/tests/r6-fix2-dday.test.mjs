import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, setPocket, load, save } from '../dist/game/state.js';
import { receptionCard, unpackAtShelter } from '../dist/story/dday.js';

const item = (bag, home = 0) => ({ home, bag, bought: 0, checked: false, used: 0 });

test('shelter reception: a contact card in a bag left at home does not count', () => {
  const G = createGame({ seed: 4 });
  G.items.contact_card = item(1);
  G.know.bagOn = false;
  assert.equal(receptionCard(G).card, false);
  assert.match(receptionCard(G).line, /휴대폰/);
  G.know.bagOn = true;
  assert.deepEqual(receptionCard(G), { card: true, line: '(배낭에서 비상연락카드를 꺼내 보고 적었다.)' });
  setPocket(G, 'contact_card', 1);
  assert.match(receptionCard(G).line, /앞주머니/);
});

test('shelter arrival: bag water is drunk (thirst drops), the line names only what was in the bag', () => {
  const G = createGame({ seed: 5 });
  G.know.bagOn = true; G.items.water_500 = item(2); delete G.items.blanket;
  const t0 = G.stats.thirst;
  assert.equal(unpackAtShelter(G), '배낭에서 물을 꺼냈다');
  assert.equal(G.items.water_500.bag, 1); assert.equal(G.items.water_500.used, 1);
  assert.equal(G.stats.thirst, Math.max(0, t0 - 20));
  G.items.blanket = item(1); G.items.water_500 = item(0);
  assert.equal(unpackAtShelter(G), '배낭에서 담요를 꺼냈다');
  G.items.water_2l = item(1);
  assert.equal(unpackAtShelter(G), '배낭에서 물과 담요를 꺼냈다');
  G.know.bagOn = false; assert.equal(unpackAtShelter(G), null);
  assert.deepEqual(load(save(G)), G);
});
