// Pure geometry for the player rig and thrown things: walk-zone ground height, circle-vs-AABB push-out,
// segment-vs-AABB entry. No three.js.
const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);

// Ground under (x, z): the highest walk surface within `below` m below / 0.45 m above feetY, or null.
export function groundAt(walk, x, z, feetY, any = false, below = 0.6) {
  let best = null, slope = 0;
  for (const w of walk || []) {
    if (x < w.x0 - 0.02 || x > w.x1 + 0.02 || z < w.z0 - 0.02 || z > w.z1 + 0.02) continue;
    let y = w.y ?? 0, s = 0;
    if (w.ramp) {
      const r = w.ramp, span = r.axis === 'x' ? w.x1 - w.x0 : w.z1 - w.z0;
      const k = clamp(((r.axis === 'x' ? x - w.x0 : z - w.z0) / (span || 1)), 0, 1);
      y = r.y0 + (r.y1 - r.y0) * k; s = Math.abs(r.y1 - r.y0) / (span || 1);
    }
    if (!any && (y > feetY + 0.45 || y < feetY - below)) continue;
    if (best === null || y > best) { best = y; slope = s; }
  }
  return best === null ? null : { y: best, slope };
}

// Tops of solids you can stand on (cars, walls, fences, crates) join the walk zones as flat surfaces.
export const topsOf = (solids) => solids.map((s) => ({ x0: s.x0, x1: s.x1, z0: s.z0, z1: s.z1, y: s.y1 }));
// Ground with a little footprint: a seam narrower than the body between two walk zones is not a hole.
export function groundNear(walk, x, z, feetY) {
  for (const [ox, oz] of [[0, 0], [0.25, 0], [-0.25, 0], [0, 0.25], [0, -0.25]]) { const g = groundAt(walk, x + ox, z + oz, feetY); if (g) return g; }
  return null;
}

// Push a circle (radius r) out of every solid that overlaps the vertical span [feetY, feetY + height].
export function collide(px, pz, feetY, solids, r = 0.32, h = 1.72) {
  for (let pass = 0; pass < 3; pass++) {
    let moved = false;
    for (const s of solids) {
      if (s.off || s.y1 <= feetY + 0.05 || s.y0 >= feetY + h) continue;
      const cx = clamp(px, s.x0, s.x1), cz = clamp(pz, s.z0, s.z1), dx = px - cx, dz = pz - cz, d2 = dx * dx + dz * dz;
      if (d2 >= r * r) continue;
      if (d2 > 1e-10) { const d = Math.sqrt(d2); px += (dx / d) * (r - d); pz += (dz / d) * (r - d); }
      else { // centre inside the box: leave by the shallowest side
        const l = px - s.x0, rr = s.x1 - px, f = pz - s.z0, b = s.z1 - pz, m = Math.min(l, rr, f, b);
        if (m === l) px = s.x0 - r; else if (m === rr) px = s.x1 + r; else if (m === f) pz = s.z0 - r; else pz = s.z1 + r;
      }
      moved = true;
    }
    if (!moved) break;
  }
  return [px, pz];
}

// Segment o→o+d (t in 0..1) against an AABB grown by pad; returns entry t, or Infinity (also when o is inside).
// Scalar slab test: no allocation, it runs for every solid near the camera boom, a prompt or a thrown thing.
export function segBox(o, d, s, pad) {
  const x0 = s.x0 - pad, x1 = s.x1 + pad, y0 = s.y0 - pad, y1 = s.y1 + pad, z0 = s.z0 - pad, z1 = s.z1 + pad;
  if (o.x > x0 && o.x < x1 && o.y > y0 && o.y < y1 && o.z > z0 && o.z < z1) return Infinity;
  let t0 = 0, t1 = 1, a, b, k;
  if (Math.abs(d.x) < 1e-9) { if (o.x < x0 || o.x > x1) return Infinity; }
  else { a = (x0 - o.x) / d.x; b = (x1 - o.x) / d.x; if (a > b) { k = a; a = b; b = k; } if (a > t0) t0 = a; if (b < t1) t1 = b; if (t0 > t1) return Infinity; }
  if (Math.abs(d.y) < 1e-9) { if (o.y < y0 || o.y > y1) return Infinity; }
  else { a = (y0 - o.y) / d.y; b = (y1 - o.y) / d.y; if (a > b) { k = a; a = b; b = k; } if (a > t0) t0 = a; if (b < t1) t1 = b; if (t0 > t1) return Infinity; }
  if (Math.abs(d.z) < 1e-9) { if (o.z < z0 || o.z > z1) return Infinity; }
  else { a = (z0 - o.z) / d.z; b = (z1 - o.z) / d.z; if (a > b) { k = a; a = b; b = k; } if (a > t0) t0 = a; if (b < t1) t1 = b; if (t0 > t1) return Infinity; }
  return t0;
}

// Ceilings: optional level.ceilings [{x0,x1,z0,z1,y}] (y0 is read when y is missing). The lowest ceiling over
// (x, z) above height y, or Infinity.
export function ceilAt(ceilings, x, z, y) {
  let c = Infinity;
  if (ceilings) for (const k of ceilings) {
    const cy = k.y ?? k.y0;
    if (x >= k.x0 && x <= k.x1 && z >= k.z0 && z <= k.z1 && cy > y && cy < c) c = cy;
  }
  return c;
}
