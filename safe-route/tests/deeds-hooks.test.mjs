// Every new-choice deed is written somewhere in the story: story/*.js must call remember('<id>' for each.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { NEW_DEEDS } from '../dist/game/deeds.js';

const dir = new URL('../dist/story/', import.meta.url);
const src = readdirSync(dir).filter((f) => f.endsWith('.js')).map((f) => readFileSync(new URL(f, dir), 'utf8')).join('\n');

test('every NEW_DEEDS id has a remember call in story/', () => {
  const missing = NEW_DEEDS.filter((id) => !src.includes(`remember('${id}'`));
  assert.deepEqual(missing, []);
});

test('story never remembers an id that is not a new-choice deed', () => {
  const used = [...src.matchAll(/remember\('([a-z_]+)'/g)].map((m) => m[1]);
  assert.deepEqual(used.filter((id) => !NEW_DEEDS.includes(id)), []);
});

test('help events the ledger reads carry a kind', () => {
  for (const kind of ['ask', 'carry', 'water', 'powerbank', 'memo', 'card', 'wait']) assert.ok(src.includes(`kind: '${kind}'`), kind);
});
