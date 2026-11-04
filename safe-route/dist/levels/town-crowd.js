// Crowd wiring for the town: which people.createCrowd groups exist for each `crowd` state, the
// sidewalk and evacuation paths they use (cut by active closures), queue lines at the water point and
// the assembly bay. Crowds are created lazily and every call is guarded, so a missing or failing
// crowd module only empties the streets.
import { SIDEWALKS, BARRICADES, EVAC, ENTRY, clipPaths } from './town-plan.js';
import { heightAt } from './town-util.js';
import { WATER, ASSEMBLY } from './town-events.js';

const SIZES = { main: 26, extra: 14, few: 8, line: 14 };
export const CROWD_MODES = ['none', 'normal', 'busy', 'evacuating', 'sparse', 'queue', 'assembly'];
const toPts = (paths) => paths.map(p => p.map(([x, z]) => ({ x, z })));

export function createTownCrowd({ people, group, walk, solids = null, quality = 'high' }) {
  const crowds = {}, failed = new Set(), tmp = [];
  let mode = 'none', closures = [], gates = { east: 'open', stairs: 'open', ramp: 'open', access: 'closed' };
  const heightFn = (x, z) => heightAt(walk, x, z);
  const guard = (what, fn) => { try { return fn(); } catch (err) { if (!failed.has(what)) { failed.add(what); console.error(`[town] crowd ${what} failed`, err); } return undefined; } };

  function get(name) {
    if (name in crowds) return crowds[name];
    crowds[name] = guard(`create ${name}`, () => {
      const c = people.createCrowd({ count: SIZES[name], seed: 900 + Object.keys(crowds).length * 17, people, heightAt: heightFn, quality, solids });
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
    update(dt, ctx) { for (const [n, c] of Object.entries(crowds)) if (c?.group.visible) guard(`${n}.update`, () => c.update(dt, ctx)); },
    // Where the shown crowd members are ({x, z}), for cars that give way (town-traffic). `out` is reused.
    positions(out = []) {
      out.length = 0;
      for (const c of Object.values(crowds)) if (c?.group.visible && c.positions) for (const p of c.positions(tmp)) out.push(p);
      return out;
    },
    get mode() { return mode; },
  };
}
