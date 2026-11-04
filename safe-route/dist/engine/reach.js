// Interaction picking: which level point (E prompt) the player is at. Pure, no three.js.
// A point {x, y, z, r?, reach?, host?, enabled} is in reach within its own radius (never under 1.6 m: most points
// sit on furniture that keeps the body at arm's length), measured flat from the player to the point, while it
// lies within 100° of where the camera or the body faces (anywhere once closer than 1.2 m), from 1.8 m below
// to 2.2 m above the feet, and not behind a wall. Clustered points go to the best score (distance plus how far
// off the view it is); the current one is kept unless another is clearly better, so the prompt never flickers.
import { segBox } from './geom.js';
import { segHit } from './space.js';

export const REACH = { min: 1.6, cone: 100, free: 1.2, below: 1.8, above: 2.2, turn: 1.0, keep: 0.3 };
const D2R = Math.PI / 180;
const wrap = (a) => Math.atan2(Math.sin(a), Math.cos(a));
export const reachOf = (p) => p.reach ?? Math.max(p.r ?? REACH.min, REACH.min);

// The point's own solid never hides it: one that holds or abuts it (within 0.25 m: an ATM point sits just in
// front of the ATM), or the ones named by point.host (an id or a list of ids, e.g. the gate barrier it is on).
function own(p, s) {
  if (p.host != null && s.id != null && (Array.isArray(p.host) ? p.host.includes(s.id) : p.host === s.id)) return true;
  return p.x >= s.x0 - 0.25 && p.x <= s.x1 + 0.25 && p.z >= s.z0 - 0.25 && p.z <= s.z1 + 0.25;
}

// Behind a wall: one solid (not the point's own) blocks the line to it both at 0.3 m and at 1.5 m (low furniture
// or a high sign alone does not hide it). The last 0.25 m before the point is ignored.
const o = { x: 0, y: 0, z: 0 }, d = { x: 0, y: 0, z: 0 }, o2 = { x: 0, y: 0, z: 0 };
export function hidden(p, pos, grid) {
  const dx = p.x - pos.x, dz = p.z - pos.z, dist = Math.hypot(dx, dz);
  if (dist < 0.4 || !grid) return false;
  const k = (dist - 0.25) / dist, py = p.y ?? pos.y;
  o.x = o2.x = pos.x; o.z = o2.z = pos.z; o.y = pos.y + 0.3; o2.y = pos.y + 1.5;
  d.x = dx * k; d.z = dz * k; d.y = (py - pos.y) * k;
  // Both heights share the horizontal run, so the low segment's candidates cover the high one too.
  return segHit(grid, o, d, 0, (s) => own(p, s) || !(segBox(o2, d, s, 0) <= 1)) <= 1;
}

// Score of a point in reach and in view (distance plus how far off the view it is), or Infinity. Visibility is
// tested separately: it is the costly part.
export function rawScore(p, pos, yaws) {
  if (!p || p.enabled === false) return Infinity;
  const dx = p.x - pos.x, dz = p.z - pos.z, dist = Math.hypot(dx, dz), dy = (p.y ?? pos.y) - pos.y;
  if (dist > reachOf(p) || dy > REACH.above || dy < -REACH.below) return Infinity;
  if (dist < 0.05) return dist;
  const a = Math.atan2(-dx, -dz);
  let off = Infinity; for (const y of yaws) off = Math.min(off, Math.abs(wrap(a - y)));
  if (off > REACH.cone * D2R && dist > REACH.free) return Infinity;
  return dist + off * REACH.turn;
}

// A picker remembers the current point for the hysteresis. pick(points, pos {x,y,z}, yaws [camera, body], grid).
export function createPicker() {
  let cur = null;
  const cand = [];
  function pick(points, pos, yaws, grid) {
    cand.length = 0;
    for (const p of points || []) { const s = rawScore(p, pos, yaws); if (s < Infinity) cand.push({ p, s }); }
    cand.sort((a, b) => a.s - b.s);
    let best = null, bs = Infinity, cs = Infinity;
    for (const c of cand) if (!hidden(c.p, pos, grid)) { best = c.p; bs = c.s; break; }
    if (cur && best && cur !== best) {
      const c = cand.find((k) => k.p === cur);
      if (c && c.s <= bs + REACH.keep && !hidden(cur, pos, grid)) cs = c.s;
      if (cs < Infinity) best = cur;
    }
    return (cur = best);
  }
  return { pick, reset() { cur = null; }, get current() { return cur; } };
}
