import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { buildPeople } from '../dist/levels/shelter-people.js';

// Shelter evacuees share the WAYS polylines and the reception desk: nobody walks through anybody (stub people).
const seats = [];
for (const z of [-4.2, -1.8, 3.4, 5.8]) for (const x of [-18.2, -16, -13.8, -11.6, -2.8, -0.6, 3.8, 6.0]) seats.push({ x, z, yaw: 0 });
const onSeat = (g) => seats.some(s => Math.hypot(s.x - g.position.x, s.z - g.position.z) < 0.05);

function run(mode, off) {
  const rng = (seed) => { let s = (seed + off * 7919) >>> 0; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); };
  const P = buildPeople({ THREE, rng, Q: { shelterPeople: [32, 4] } }, null, { seats, walk: [] }); // the top tier's walkers
  P.setCrowd(mode); P.setPhase('alert');
  const evs = P.group.children.slice(0, 32), seated0 = evs.filter(g => g.visible && onSeat(g)).length;
  let min = Infinity, jump = 0;
  const was = new Map(evs.map(g => [g, { v: g.visible, x: g.position.x, z: g.position.z }]));
  for (let t = 0, dt = 1 / 30; t < 240; t += dt) {
    P.update(dt, t, null, null);
    const m = evs.filter(g => g.visible && !onSeat(g));
    for (let i = 0; i < m.length; i++) for (let j = i + 1; j < m.length; j++) min = Math.min(min, Math.hypot(m[i].position.x - m[j].position.x, m[i].position.z - m[j].position.z));
    for (const g of m) { const w = was.get(g); if (w.v && !onSeat(g)) jump = Math.max(jump, Math.hypot(g.position.x - w.x, g.position.z - w.z)); }
    for (const g of evs) Object.assign(was.get(g), { v: g.visible && !onSeat(g), x: g.position.x, z: g.position.z });
  }
  return { min, jump, seated: evs.filter(g => g.visible && onSeat(g)).length, seated0 };
}

test('shelter walkers keep apart at the reception desk, at merges and in file', () => {
  for (const mode of ['arriving', 'leaving', 'settled']) for (const off of [0, 1, 2, 3]) {
    const r = run(mode, off);
    assert.ok(r.min > 0.3, `${mode}/${off}: two walkers came within ${r.min.toFixed(2)} m`);
    assert.ok(r.jump < 0.12, `${mode}/${off}: a walker jumped ${r.jump.toFixed(2)} m in one frame`);
    if (mode === 'arriving') assert.ok(r.seated > r.seated0, 'arrivals still reach their seats (no deadlock)');
    if (mode === 'leaving') assert.ok(r.seated < r.seated0, 'leavers still get out (no deadlock)');
  }
});
