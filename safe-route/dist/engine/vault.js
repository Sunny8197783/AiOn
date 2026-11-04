// Vault / climb planning for Space in front of a low solid. Pure, no three.js.
// planVault({ x, z, feet, dx, dz, L, grid, walk, R, H }) -> { x1, z1, y1, arc, h, kind: 'over'|'onto' } or null
// (null = nothing to get over here: the caller jumps instead). The rules, so a wall is never a way through:
//  - the first solid in front at knee height decides; a solid taller than 2.3 m (a wall) stops it all;
//  - thickness is the box's size across the move axis (the face you meet), not the slanted ray through it,
//    and the move goes straight across that face;
//  - over: up to 1.5 m tall and 1.0 m thick (low walls, railings, cars, low fences), landing on a walk zone at
//    about the same level, with a free body there and a clear line from here to the landing at the height the
//    body passes (nothing hidden behind the obstacle, no wall just past it), and headroom over the arc;
//  - onto: a top at least 0.4 m deep, and 0.6 m if it is more than 1.2 m up (no standing on a structural wall),
//    with 1.8 m of headroom over the landing (level.ceilings or solids above; indoors without ceiling data the
//    room is taken to be 2.3 m high) and a clear line to it;
//  - solids marked noVault / noClimb / npc are never used.
import { groundAt, collide, segBox, groundNear } from './geom.js';
import { headroomAt } from './space.js';

export const VAULT = { over: 1.5, overThick: 1.0, onto: 2.3, thin: 0.4, wall: 0.6, wallH: 1.2, head: 1.8, drop: 1.2, reach: 1.0 };
const near = [], o = { x: 0, y: 0, z: 0 }, d = { x: 0, y: 0, z: 0 };

// The first solid the knee-height ray (radius-padded) meets within `reach`, and where.
function firstHit(x, z, feet, dx, dz, list, R) {
  o.x = x; o.y = feet + 0.25; o.z = z; d.x = dx * VAULT.reach; d.y = 0; d.z = dz * VAULT.reach;
  let best = null, bt = Infinity;
  for (const s of list) {
    if (s.npc || s.y1 <= feet + 0.3 || s.y0 > feet + 0.3) continue;
    const t = segBox(o, d, s, R * 0.6); if (t < bt) { bt = t; best = s; }
  }
  return best;
}

// Is the line from (ax, ay, az) to (bx, by, bz) clear of every solid but `skip`?
function clear(list, skip, ax, ay, az, bx, by, bz) {
  o.x = ax; o.y = ay; o.z = az; d.x = bx - ax; d.y = by - ay; d.z = bz - az;
  for (const s of list) if (s !== skip && !s.npc && segBox(o, d, s, 0.05) <= 1) return false;
  return true;
}

export function planVault({ x, z, feet, dx, dz, L, grid, walk = L?.walk || [], R = 0.32, H = 1.72 }) {
  if (!grid) return null;
  const list = grid.query(x - 3, x + 3, z - 3, z + 3, near);
  const best = firstHit(x, z, feet, dx, dz, list, R);
  if (!best) return null;
  const h = best.y1 - feet;
  if (h > VAULT.onto) return null; // a wall: just a jump
  // The face met: the axis the player is outside the box on (the larger gap at a corner), crossed straight.
  const gx = x < best.x0 ? best.x0 - x : x > best.x1 ? x - best.x1 : -1, gz = z < best.z0 ? best.z0 - z : z > best.z1 ? z - best.z1 : -1;
  if (gx < 0 && gz < 0) return null; // already over it
  const ax = gx >= gz ? 'x' : 'z', sgn = ax === 'x' ? (x < best.x0 ? 1 : -1) : (z < best.z0 ? 1 : -1);
  const lo = ax === 'x' ? best.x0 : best.z0, hi = ax === 'x' ? best.x1 : best.z1, thick = hi - lo;
  const width = ax === 'x' ? best.z1 - best.z0 : best.x1 - best.x0;
  const near0 = sgn > 0 ? lo : hi, far = sgn > 0 ? hi : lo;
  // Along the face the move keeps the player's line, kept on the box so the body really goes across it.
  const cross = ax === 'x' ? z : x, cLo = ax === 'x' ? best.z0 : best.x0, cHi = ax === 'x' ? best.z1 : best.x1;
  const along = Math.min(Math.max(cross, cLo + Math.min(0.2, width / 2)), cHi - Math.min(0.2, width / 2));
  const at = (u) => (ax === 'x' ? [u, along] : [along, u]);
  const free = (px, pz, py) => { const [cx, cz] = collide(px, pz, py, list, R, VAULT.head); return Math.hypot(cx - px, cz - pz) < 0.05; };
  const chest = feet + 1.4;

  if (h <= VAULT.over && thick <= VAULT.overThick && !best.noVault) { // over it
    const [lx, lz] = at(far + sgn * (R + 0.18));
    const gl = groundAt(walk, lx, lz, feet, false, VAULT.drop) || groundNear(walk, lx, lz, feet);
    const body = Math.max(feet + 0.45, best.y1 + 0.25);
    if (gl && Math.abs(gl.y - feet) <= VAULT.drop && free(lx, lz, gl.y)
      && headroomAt(L, (x + lx) / 2, (z + lz) / 2, feet) - best.y1 >= 1.0 && headroomAt(L, lx, lz, gl.y) - gl.y >= VAULT.head
      && clear(list, best, x, body, z, lx, Math.max(gl.y + 0.45, best.y1 + 0.25), lz) && clear(list, best, x, Math.max(chest, best.y1 + 0.6), z, lx, gl.y + 1.4, lz)) {
      const y1 = Math.max(gl.y, feet - 0.4);
      return { kind: 'over', x1: lx, z1: lz, y1, h, arc: best.y1 + 0.2 - (feet + y1) / 2, solid: best };
    }
  }
  const minW = Math.min(thick, width);
  if (best.noClimb || minW < VAULT.thin || (h > VAULT.wallH && minW < VAULT.wall)) return null; // onto it
  const [lx, lz] = at(near0 + sgn * Math.min(0.45, thick / 2)); // at least 0.2 m in from both faces
  if (!free(lx, lz, best.y1) || headroomAt(L, lx, lz, feet) - best.y1 < VAULT.head) return null; // the room's ceiling
  if (!clear(list, best, x, Math.max(chest, best.y1 + 0.3), z, lx, best.y1 + 1.0, lz)) return null;
  return { kind: 'onto', x1: lx, z1: lz, y1: best.y1, h, arc: 0.2, solid: best };
}
