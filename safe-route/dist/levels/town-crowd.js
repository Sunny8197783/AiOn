// Crowd wiring for the town: which people.createCrowd groups exist for each `crowd` state, the
// sidewalk and evacuation paths they use (cut by active closures), queue lines at the water point and
// the assembly bay, and the walk signals strollers wait for at the signalled zebras. Crowds are created lazily
// and every call is guarded, so a missing or failing crowd module only empties the streets.
import { SIDEWALKS, BARRICADES, EVAC, ENTRY, clipPaths, CYCLE } from './town-plan.js';
import { heightAt } from './town-util.js';
import { WATER, ASSEMBLY } from './town-events.js';
import { zebraZones } from './town-geo.js';
import { CROSSWALKS, ROADS } from '../game/townmap.js';

const SIZES = { main: 26, extra: 14, few: 8, line: 14 };
export const CROWD_MODES = ['none', 'normal', 'busy', 'evacuating', 'sparse', 'queue', 'assembly'];
// Seconds of steady green left for group 'EW' | 'NS' at time t (town-plan signalAt without its per-call objects).
const CYCLE_S = CYCLE.reduce((s, c) => s + c[2], 0);
function greenLeft(t, grp) {
  let u = ((t % CYCLE_S) + CYCLE_S) % CYCLE_S;
  for (const [g, c, d] of CYCLE) { if (u < d) return g === grp && c === 'green' ? d - u : 0; u -= d; }
  return 0;
}
const toPts = (paths) => paths.map(p => p.map(([x, z]) => ({ x, z })));
// The carriageway of every zebra with pedestrian signals (town-traffic draws them on these three roads); crossing an
// east-west road is allowed while north-south traffic has green, and the other way round.
const SIGNALLED = ['yeonmu', 'munhwa', 'hanbit'];
function signalHolds() {
  return zebraZones(CROSSWALKS, ROADS).map((z, k) => {
    const r = ROADS.find(q => q.id === CROSSWALKS[k].road);
    return SIGNALLED.includes(r.id) ? { ...z, group: r.z1 - r.z0 < r.x1 - r.x0 ? 'NS' : 'EW', open: true } : null;
  }).filter(Boolean);
}

export function createTownCrowd({ people, group, walk, solids = null, quality = 'high' }) {
  const crowds = {}, failed = new Set(), tmp = [], holds = signalHolds();
  let mode = 'none', closures = [], gates = { east: 'open', stairs: 'open', ramp: 'open', access: 'closed' };
  const heightFn = (x, z) => heightAt(walk, x, z);
  const guard = (what, fn) => { try { return fn(); } catch (err) { if (!failed.has(what)) { failed.add(what); console.error(`[town] crowd ${what} failed`, err); } return undefined; } };

  function get(name) {
    if (name in crowds) return crowds[name];
    crowds[name] = guard(`create ${name}`, () => {
      const c = people.createCrowd({ count: SIZES[name], seed: 900 + Object.keys(crowds).length * 17, people, heightAt: heightFn, quality, solids, holds });
      if (!c?.group) throw new Error('createCrowd returned no group');
      c.group.name = `crowd:${name}`; group.add(c.group); return c;
    }) || null;
    return crowds[name];
  }
  const rects = () => closures.flatMap(id => BARRICADES[id] || []);
  const stroll = () => toPts(clipPaths(SIDEWALKS, rects()));
  function evac() {
    const open = EVAC.filter(([g]) => gates[g] === 'open');
    const use = open.length ? open : EVAC.filter(([g]) => g === 'stairs');
    return { paths: toPts(clipPaths(use.map(([, p]) => p), rects())), targets: [...new Set(use.map(([g]) => g))].map(g => ENTRY[g]) };
  }

  function run(name, fn) { const c = get(name); if (!c) return; c.group.visible = true; guard(`${name}.${mode}`, () => fn(c)); }
  function apply() {
    for (const c of Object.values(crowds)) if (c) c.group.visible = false;
    const walkers = stroll();
    switch (mode) {
      case 'none': return;
      case 'normal': run('main', c => { c.setPaths(walkers); c.setMode('stroll', { speed: 1.25 }); }); return;
      case 'busy':
        for (const n of ['main', 'extra']) run(n, c => { c.setPaths(walkers); c.setMode('stroll', { speed: 1.3 }); });
        return;
      case 'sparse': run('few', c => { c.setPaths(walkers); c.setMode('stroll', { speed: 1.15 }); }); return;
      case 'evacuating': {
        const { paths, targets } = evac();
        for (const n of ['main', 'extra']) run(n, c => { c.setPaths(paths); c.setMode('brisk', { targets, then: 'despawn' }); });
        return;
      }
      case 'queue':
        run('line', c => c.setMode('queue', { point: WATER.queue, at: WATER.queue, dir: { x: 1, z: 0 }, spacing: 0.9, face: { x: -1, z: 0 }, leave: { x: -1, z: 0 } })); // served leave west through the yard, not across 연무로
        run('few', c => { c.setPaths(walkers); c.setMode('stroll', { speed: 1.1 }); });
        return;
      case 'assembly':
        run('main', c => c.setMode('idle', { area: ASSEMBLY.area, paths: [], instant: true }));
        run('line', c => c.setMode('queue', { point: ASSEMBLY.queue, at: ASSEMBLY.queue, dir: { x: 0, z: 1 }, spacing: 0.85, face: { x: 0, z: -1 }, leave: { x: 1, z: 0 } })); // served leave east toward 문화로, clear of the buses
        return;
      default: console.error(`[town] crowd "${mode}" unknown; use ${CROWD_MODES.join(' | ')}`);
    }
  }
  return {
    setMode(m) { if (!CROWD_MODES.includes(m)) { console.error(`[town] crowd "${m}" unknown; use ${CROWD_MODES.join(' | ')}`); return mode; } mode = m; apply(); return mode; },
    setContext(o = {}) { if (o.closures) closures = o.closures; if (o.gates) gates = { ...gates, ...o.gates }; if (mode === 'normal' || mode === 'busy' || mode === 'sparse' || mode === 'evacuating' || mode === 'queue') apply(); },
    update(dt, ctx) { for (const n in crowds) { const c = crowds[n]; if (c?.group.visible) guard(`${n}.update`, () => c.update(dt, ctx)); } },
    // Walk signals at time t (town-plan CYCLE); on = the signals have power and run. A steady green man opens a
    // crossing; it closes again when the green starts to blink (5 s left), so nobody starts across too late.
    setSignals(t, on) {
      for (const h of holds) h.open = !on || greenLeft(t, h.group) > 5;
    },
    // Where the shown crowd members are ({x, z}), for cars that give way (town-traffic). `out` is reused.
    positions(out = []) {
      out.length = 0;
      for (const n in crowds) { const c = crowds[n]; if (c?.group.visible && c.positions) for (const p of c.positions(tmp)) out.push(p); }
      return out;
    },
    get mode() { return mode; },
    // The level build is dropped (engine/tier-apply.js): each crowd's members and its body registration go.
    dispose() { for (const n in crowds) guard(`${n}.dispose`, () => crowds[n]?.dispose?.()); },
  };
}
