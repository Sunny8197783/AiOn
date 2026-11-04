// People as bodies the player bumps into. Sources register lists of people (engine/actions.js: every person the
// engine made; engine/crowd.js: its members); the controller adds the level's npcs. A person counts while it is
// shown in the scene and not lying down. The player is pushed out of them; they are never moved by it.
const sources = new Set();
// src: an iterable of people ({ group, anim? }) or a function returning one. Returns the unregister function.
export function addBodies(src) { sources.add(src); return () => sources.delete(src); }

export const BODY_R = 0.24;
const seen = new Set();
const shown = (g) => { for (let x = g; x; x = x.parent) { if (!x.visible) return false; if (x.isScene) return true; } return false; };

// Circles { x, z, r } of the people within `R` of (x, z) whose feet are within 1.2 m of feetY; `skip` is the
// player's own model. `out` is reused; extra lists (level.npcs values) are passed in `more`.
export function bodiesNear(x, z, feetY, R, skip, out, more) {
  out.length = 0; seen.clear();
  const add = (p) => {
    const g = p?.group; if (!g || p === skip || seen.has(p)) return;
    seen.add(p);
    const e = g.matrixWorld?.elements; if (!e) return;
    const px = e[12], py = e[13], pz = e[14];
    if (Math.abs(px - x) > R || Math.abs(pz - z) > R || Math.abs(py - feetY) > 1.2) return;
    if (p.anim === 'down' || !shown(g)) return;
    out.push({ x: px, z: pz, r: p.anim === 'sit' ? BODY_R + 0.08 : BODY_R });
  };
  const each = (list) => { if (list) for (const p of list) add(p); };
  for (const s of sources) { try { each(typeof s === 'function' ? s() : s); } catch (err) { console.error('[engine/bodies] source failed', err); sources.delete(s); } }
  if (more) each(more);
  return out;
}

// Push a circle (radius r) at (px, pz) out of the body circles. Returns [x, z, pushed].
export function pushOut(px, pz, r, bodies) {
  let pushed = false;
  for (const b of bodies) {
    const dx = px - b.x, dz = pz - b.z, m = r + b.r, d2 = dx * dx + dz * dz;
    if (d2 >= m * m) continue;
    const d = Math.sqrt(d2);
    if (d > 1e-6) { px = b.x + (dx / d) * m; pz = b.z + (dz / d) * m; } else px = b.x + m;
    pushed = true;
  }
  return [px, pz, pushed];
}
