import test from 'node:test';
import assert from 'node:assert/strict';
import { createGame, recordEvent, checkpoint, restore } from '../dist/game/state.js';
import { recordEncounter } from '../dist/game/drills.js';
import { FACTS } from '../dist/game/facts.js';
import { RAW } from './deed-fixtures.mjs';
import { AXES, CH, DEEDS, NEW_DEEDS, NEIGHBOURS, remember, deeds, axes, promises, newDeeds, cueText } from '../dist/game/deeds.js';

const factIds = new Set(FACTS.map((f) => f.id));
const ids = (G) => deeds(G).map((d) => d.id);
const fresh = () => createGame({ seed: 4 });

test('table: 64 deeds in order, known chapters, axes and facts, Korean text', () => {
  const all = Object.entries(DEEDS);
  assert.equal(all.length, 64);
  all.forEach(([id, d], i) => {
    assert.equal(d.no, i + 1, id);
    assert.ok(d.ch in CH, id);
    assert.ok(Object.keys(d.w).every((k) => AXES.includes(k) && Number.isInteger(d.w[k])), id);
    for (const s of [d.short, d.line]) { assert.match(s, /[가-힣]/, id); assert.doesNotMatch(s, /[A-Za-z]/, id); }
    assert.ok(d.short.length <= 22, `${id}: ${d.short}`);
    assert.ok(d.facts.every((f) => factIds.has(f)), id);
    assert.ok(d.cue === null || /[은는]$/.test(d.cue), id);
    assert.ok(['event', 'new'].includes(d.src));
  });
  assert.equal(NEW_DEEDS.length, 27);
  assert.deepEqual(Object.keys(NEIGHBOURS), ['seoyeon', 'manager', 'clerk', 'elder']);
  assert.equal(cueText('stairs_asked'), '서연은 이 일을 기억할 것이다.');
  assert.equal(cueText('hoard'), null);
});

test('a fresh game has no deeds and every axis at 0', () => {
  const G = fresh();
  assert.deepEqual(deeds(G), []);
  assert.deepEqual(axes(G, { final: true }), Object.fromEntries(AXES.map((k) => [k, 0])));
});

test('remember: once per id, only new-choice ids, rolled back by restore', () => {
  const G = fresh();
  const e = remember(G, 'driver_text');
  assert.equal(e.type, 'deed'); assert.equal(e.details.id, 'driver_text');
  assert.equal(remember(G, 'driver_text'), null);
  assert.throws(() => remember(G, 'nope'));
  assert.throws(() => remember(G, 'hoard'));
  assert.equal(remember(G, 'rumor_corrected', { after: 'check', id: 'x' }).details.id, 'rumor_corrected');
  checkpoint(G, 'c');
  remember(G, 'mart_share');
  assert.ok(ids(G).includes('mart_share'));
  restore(G);
  assert.deepEqual(ids(G), ['driver_text', 'rumor_corrected']);
  assert.equal(deeds(G)[1].details.after, 'check');
});


test('every event-derived deed comes from its event alone', () => {
  const eventIds = Object.keys(DEEDS).filter((id) => DEEDS[id].src === 'event' && id !== 'signals_clear');
  assert.deepEqual(eventIds.sort(), Object.keys(RAW).sort());
  for (const [id, evs] of Object.entries(RAW)) {
    const G = fresh();
    for (const [type, d, ch] of evs) { if (ch) G.chapter = ch; recordEvent(G, type, d); }
    assert.deepEqual(ids(G), [id], id);
  }
});

test('untagged help events fall back to their old text; other helpers do not count', () => {
  const G = fresh();
  recordEvent(G, 'help', { who: 'seoyeon', what: '어떻게 도울지 먼저 묻고, 계단에서 보행기를 옆에서 들어 주었다' });
  recordEvent(G, 'help', { who: 'seoyeon', what: '라디오 메모와 급수소 소식을 서로 나누기로 했다' });
  recordEvent(G, 'help', { who: 'seoyeon', what: '떠나기 전 서로의 연락처를 다시 확인했다' });
  recordEvent(G, 'help', { who: 'manager', what: '마지막까지 건물을 지키며 길을 안내했다' });
  recordEvent(G, 'help', { who: 'clerk', what: '가게를 닫으며 쪽지와 현금 통을 남겨 이웃이 필요한 물건을 가져가게 했다' });
  assert.deepEqual(ids(G), ['stairs_asked', 'notes_exchange', 'seoyeon_card_photo']);
  const C = fresh();
  recordEvent(C, 'help', { who: 'seoyeon', what: '계단에서 보행기를 옆에서 들어 주었다' });
  assert.deepEqual(ids(C), ['stairs_carried']);
  const T = fresh(); // a tag wins over the text
  recordEvent(T, 'help', { who: 'seoyeon', kind: 'water', what: '어떻게 도울지 먼저 묻고' });
  assert.deepEqual(ids(T), []);
});

test('signals_clear needs four correct sorts; its seq is the 4th', () => {
  const G = fresh();
  for (let i = 0; i < 3; i++) recordEvent(G, 'signal_sorted', { id: `s${i}`, ok: true });
  recordEvent(G, 'signal_sorted', { id: 'x', ok: false });
  assert.ok(!ids(G).includes('signals_clear'));
  const e = recordEvent(G, 'signal_sorted', { id: 's3', ok: true });
  recordEvent(G, 'signal_sorted', { id: 's4', ok: true });
  const d = deeds(G).find((x) => x.id === 'signals_clear');
  assert.equal(d.seq, e.seq);
  assert.equal(axes(G).info, 2);
});

test('violence: two hits and a knock-out are harm 5; self-defence is no harm', () => {
  const G = fresh();
  recordEvent(G, 'violence', { how: 'punch', ko: false });
  recordEvent(G, 'violence', { how: 'kick', ko: false });
  recordEvent(G, 'violence', { how: 'punch', ko: true });
  recordEvent(G, 'looter', { result: 'fought' });
  assert.equal(axes(G).harm, 5);
  assert.equal(deeds(G).find((d) => d.id === 'violence').n, 3);
});

test('withdrawal weighs the same as the riskier choice', () => {
  const fire = (fn) => { const G = fresh(); G.chapter = 'endure'; fn(G); return axes(G); };
  const out = fire((G) => recordEncounter(G, 'extinguisher', { result: 'out' }));
  assert.deepEqual(fire((G) => recordEncounter(G, 'extinguisher', 'retreat-ok')), out);
  assert.deepEqual(fire((G) => recordEvent(G, 'fire_retreat', { size: 'large' })), out);
  assert.equal(out.manager, 1);
  assert.deepEqual(fire((G) => recordEncounter(G, 'extinguisher', 'escalated')).manager, 0);
  for (const id of ['looter_escaped', 'water_declined', 'shelter_water_received']) assert.deepEqual(DEEDS[id].w, {});
  const sum = (list) => list.reduce((s, id) => s + Object.values(DEEDS[id].w).reduce((a, b) => a + b, 0), 0);
  assert.equal(sum(['smoke_judged']), sum(['shelter_fire_judged', 'fire_report']));
  assert.equal(sum(['rounds_with_manager']), sum(['sort_supplies']));
});

test('no new-choice deed tied to a safety-first option weighs less than zero', () => {
  const temptations = new Set(['store_took']);
  for (const id of NEW_DEEDS) if (!temptations.has(id)) assert.ok(Object.values(DEEDS[id].w).every((v) => v >= 0), id);
  // harm comes only from unprovoked violence, hoarding, taking and spreading rumours
  assert.deepEqual(Object.keys(DEEDS).filter((id) => DEEDS[id].w.harm).sort(), ['hoard', 'rumor_spread', 'store_took', 'violence']);
});

test('promises: kept, open until the run ends, then broken and the bond drops', () => {
  const G = fresh();
  remember(G, 'promise_note');
  assert.deepEqual(promises(G), [{ id: 'note_for_seoyeon', who: 'seoyeon', state: 'open' }]);
  assert.equal(promises(G, { final: true })[0].state, 'broken');
  assert.equal(axes(G).seoyeon, 1);
  assert.equal(axes(G, { final: true }).seoyeon, 0);
  recordEvent(G, 'note_left', {});
  assert.equal(promises(G, { final: true })[0].state, 'kept');
  remember(G, 'promise_store');
  remember(G, 'store_shuttered');
  assert.equal(promises(G, { final: true }).find((p) => p.id === 'store_watch').state, 'kept');
});

test('newDeeds lists only what came after a seq', () => {
  const G = fresh();
  remember(G, 'driver_text');
  const mid = recordEvent(G, 'blackout', {}).seq;
  remember(G, 'mart_share');
  recordEvent(G, 'hoard', {});
  assert.deepEqual(newDeeds(G, mid).map((d) => d.id), ['mart_share', 'hoard']);
  assert.deepEqual(newDeeds(G, 0).length, 3);
  assert.deepEqual(newDeeds(G, G.events.at(-1).seq), []);
});

test('final-build events: the 304 dilemma, the night knock and only the shelter rumour count', () => {
  const G = fresh();
  recordEvent(G, 'elder_choice', { pick: 'go' });
  recordEvent(G, 'knock', { pick: 'none' });
  recordEvent(G, 'rumor_checked', { id: 'camp_food' });
  recordEvent(G, 'rumor_spread', { id: 'evac_shortcut' });
  recordEvent(G, 'help', { who: 'manager', what: '박 소장과 함께 남아 건물을 지키기로 했다' });
  assert.deepEqual(ids(G), []);
  recordEvent(G, 'help', { who: 'elder', what: '경보 속 캄캄한 계단에서 304호 어르신을 부축해 1층까지 함께 내려갔다' });
  recordEvent(G, 'knock', { pick: 'info' });
  recordEvent(G, 'rumor_checked', {}); // an old save: no id means the shelter rumour
  assert.deepEqual(ids(G), ['elder_alert_knock', 'knock_helped', 'rumor_checked']);
});
