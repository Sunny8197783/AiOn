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
export function segBox(o, d, s, pad) {
  let t0 = 0, t1 = 1;
  const lo = [s.x0 - pad, s.y0 - pad, s.z0 - pad], hi = [s.x1 + pad, s.y1 + pad, s.z1 + pad], O = [o.x, o.y, o.z], D = [d.x, d.y, d.z];
  if (O[0] > lo[0] && O[0] < hi[0] && O[1] > lo[1] && O[1] < hi[1] && O[2] > lo[2] && O[2] < hi[2]) return Infinity;
  for (let i = 0; i < 3; i++) {
    if (Math.abs(D[i]) < 1e-9) { if (O[i] < lo[i] || O[i] > hi[i]) return Infinity; continue; }
    let a = (lo[i] - O[i]) / D[i], b = (hi[i] - O[i]) / D[i];
    if (a > b) [a, b] = [b, a];
    t0 = Math.max(t0, a); t1 = Math.min(t1, b);
    if (t0 > t1) return Infinity;
  }
  return t0;
}
