// Pure town geometry shared by the builders and the tests (no three.js): fence colliders, the rule that keeps a
// sealing fence's top out of jump reach, stair A's treads, and the zebra crossings cars wait at.

// [half-thickness, height above the curb] of each fence kind's collider. Footprint and height follow the mesh:
// mesh panel + posts 0.16 m / 1.85 m, block wall with its 0.3 m coping 1.82 m, box hedge 0.75 m deep. The low park
// railing (1.12 m) and hedge (1.22 m) keep a 1.88 m collider: every solid top is standable in the engine, so a
// lower one could be jumped onto from a bollard or hydrant top and walked along into a sealed block.
export const FENCE_SIZE = { mesh: [0.08, 1.85], park: [0.08, 1.88], wall: [0.15, 1.82], hedge: [0.375, 1.88] };

// Fence line -> blocking AABB (half-thickness t, default 0.4 m thick and 2 m tall), ends padded so corners close.
export function lineSolid([x0, z0, x1, z1], t = 0.2, y0 = -0.5, y1 = 2, extra = {}) {
  return { x0: Math.min(x0, x1) - t, x1: Math.max(x0, x1) + t, z0: Math.min(z0, z1) - t, z1: Math.max(z0, z1) + t, y0, y1, ...extra };
}
// Things the player can stand on (walk zones above the curb, and any solid top up to `reach`: the engine lands
// on every top, even a bollard's) within `r` of a sealing solid (seal: true) must stay lower than its top minus a
// jump (0.54 m apex): a sealing fence's top is raised where needed so nobody hops from a car roof onto it.
// A noClimb top counts only once something reachable within `r` is less than a jump below it (the ground too).
export function sealTops(solids, walk = [], { r = 3, jump = 0.6, reach = 2.45, curb = 0.12 } = {}) {
  const near = (a, b, d) => a.x0 < b.x1 + d && a.x1 > b.x0 - d && a.z0 < b.z1 + d && a.z1 > b.z0 - d;
  const all = solids.filter(s => !s.seal && !s.npc && s.id !== 'movingCar' && s.y1 <= reach);
  const stands = [...all.filter(s => !s.noClimb || s.y1 <= curb + jump), ...walk.filter(w => !w.ramp && (w.y ?? 0) > curb + 0.05).map(w => ({ ...w, y1: w.y }))];
  for (let more = true; more;) { // tops you can only jump onto
    more = false;
    for (const s of all) if (s.noClimb && !stands.includes(s) && stands.some(q => q.y1 + jump >= s.y1 && near(q, s, r))) { stands.push(s); more = true; }
  }
  const raised = [];
  for (const f of solids) {
    if (!f.seal) continue;
    let top = f.y1;
    for (const s of stands) if (s.x0 < f.x1 + r && s.x1 > f.x0 - r && s.z0 < f.z1 + r && s.z1 > f.z0 - r) top = Math.max(top, s.y1 + jump);
    if (top > f.y1 + 1e-6) { raised.push({ id: f.id, from: f.y1, to: +top.toFixed(2) }); f.y1 = top; }
  }
  return raised;
}
// A town-plan FENCES line -> its collider. Every line seals a block, so none can be vaulted or climbed.
export function fenceSolid(line, curb = 0.12) {
  const [t, h] = FENCE_SIZE[line[4]] || FENCE_SIZE.mesh;
  return lineSolid(line, t, -0.5, curb + h, { id: `fence:${line[4]}`, noVault: true, noClimb: true, seal: true });
}

// Stair A (출입구 A): 7 risers of 0.17 m from the curb; 6 treads of 0.28 m, the 7th riser lands on the landing in
// front of the fire door (z 16, the culture centre's north face). Treads: { z0, z1, top }, the last is the landing.
export const STAIR_A = { x0: -87.3, x1: -84.7, zt: 13.6, door: 16, n: 7, rise: 0.17, run: 0.28 };
export function stairTreads(curb = 0.12, S = STAIR_A) {
  const out = [];
  for (let i = 0; i < S.n; i++) out.push({ z0: S.zt + S.run * i, z1: i < S.n - 1 ? S.zt + S.run * (i + 1) : S.door, top: curb - S.rise * (i + 1) });
  return out;
}

// Zebra crossings as boxes (the 4 m stripes plus 0.6 m, across the road plus a step onto it).
export function zebraZones(crosswalks, roads) {
  return crosswalks.map(c => {
    const r = roads.find(q => q.id === c.road), ew = r.z1 - r.z0 < r.x1 - r.x0;
    return ew ? { x0: c.at - 2.6, x1: c.at + 2.6, z0: r.z0 - 0.6, z1: r.z1 + 0.6 } : { x0: r.x0 - 0.6, x1: r.x1 + 0.6, z0: c.at - 2.6, z1: c.at + 2.6 };
  });
}
// Indices of the zones someone ({x, z}) stands in, into the Set `out`.
export function busyZebras(zones, people, out = new Set()) {
  out.clear();
  zones.forEach((z, k) => { for (const p of people) if (p && p.x > z.x0 && p.x < z.x1 && p.z > z.z0 && p.z < z.z1) { out.add(k); break; } });
  return out;
}
