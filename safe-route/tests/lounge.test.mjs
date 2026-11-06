import test from 'node:test';
import assert from 'node:assert/strict';
import { PHRASES, isName, newName, phraseBody, runBody, readMessage, readHistory } from '../dist/game/lounge.js';

const msg = (message, extra = {}) => ({ id: 'a1', time: 1762050000, event: 'message', message, ...extra });

test('names are neighbour labels; new ones always pass the check', () => {
  for (const r of [0, 0.5, 0.9999]) assert.ok(isName(newName(() => r)));
  for (const bad of ['', '107동 이웃 1234', '103동 이웃 12', '103동 이웃 1234<b>', null, 7]) assert.equal(isName(bad), false);
});

test('fixed phrases and runs round-trip; anything else is dropped', () => {
  assert.deepEqual(readMessage(msg(phraseBody('103동 이웃 4821', 'radio'))), { id: 'a1', time: 1762050000, who: '103동 이웃 4821', text: PHRASES.radio, run: false });
  assert.equal(readMessage(msg(runBody('101동 이웃 1000', { ending: 'commons', n: 7 }))).text, '엔딩 7에 닿았어요 · 도전 과제 7/13');
  for (const body of ['hi', '{}', JSON.stringify({ v: 1, who: '103동 이웃 4821', p: 'free text' }), JSON.stringify({ v: 1, who: '103동 이웃 4821', p: 'toString' }),
    JSON.stringify({ v: 1, who: 'evil<script>', p: 'radio' }), JSON.stringify({ v: 2, who: '103동 이웃 4821', p: 'radio' }),
    JSON.stringify({ v: 1, who: '103동 이웃 4821', run: { e: 'nope', c: 1 } }), JSON.stringify({ v: 1, who: '103동 이웃 4821', run: { e: 'bus', c: 99 } })])
    assert.equal(readMessage(msg(body)), null, body);
  assert.equal(readMessage(msg(phraseBody('103동 이웃 4821', 'radio'), { event: 'open' })), null);
  const lines = [JSON.stringify(msg(phraseBody('102동 이웃 2000', 'hello'))), 'garbage', JSON.stringify({ event: 'keepalive' }), ''].join('\n');
  assert.deepEqual(readHistory(lines).map((m) => m.text), [PHRASES.hello]);
});
