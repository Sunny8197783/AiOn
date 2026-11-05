// Where the solids are: a uniform 4 m grid over a level's solids, built once per solids array, so the player,
// the camera boom, prompts, crowds and thrown things only test the few solids near them. The grid keeps in step
// with a solids array that grows or swaps entries (rebuild) and with solids that move (traffic, door leaves:
// they are tested every query). Also the headroom rules the camera and the vault share. Pure, no three.js.
import { segBox, ceilAt, groundAt } from './geom.js';

const CELL = 4, BIG = 24; // a solid spanning more than BIG cells per side is tested every query
const cache = new WeakMap();
const keyOf = (cx, cz) => (cx + 32768) * 65536 + (cz + 32768);
const cell = (v) => Math.floor(v / CELL);

function build(solids) {
  const n = solids.length, snap = new Float64Array(n * 4), ref = solids.slice(), buckets = new Map(), big = [];
  const moving = new Uint8Array(n), dyn = [];
  for (let i = 0; i < n; i++) {
    const s = solids[i]; if (!s) continue;
    snap[i * 4] = s.x0; snap[i * 4 + 1] = s.x1; snap[i * 4 + 2] = s.z0; snap[i * 4 + 3] = s.z1;
    const a = cell(s.x0), b = cell(s.x1), c = cell(s.z0), d = cell(s.z1);
    if (!(b - a < BIG && d - c < BIG)) { big.push(i); continue; } // also NaN bounds
    for (let x = a; x <= b; x++) for (let z = c; z <= d; z++) { const k = keyOf(x, z); let l = buckets.get(k); if (!l) buckets.set(k, l = []); l.push(i); }
  }
  return { n, snap, ref, buckets, big, moving, dyn, stamp: new Uint32Array(n), q: 0 };
}

// The grid of a solids array (cached). Call once per frame or per batch of queries; it re-checks what moved.
export function gridOf(solids) {
  let g = cache.get(solids);
  if (!g) { g = makeGrid(solids); cache.set(solids, g); }
  g.sync();
  return g;
}

function makeGrid(solids) {
  let B = build(solids), version = 1;
  const idx = [], near = { key: NaN, ver: 0, list: [] };
  // A quarter of the solids per call (every one within 4 calls): a solid that starts to move is caught within
  // a few frames, then tested on every query.
  let cur = 0;
  function sync() {
    if (B.n !== solids.length) { B = build(solids); version++; return; }
    const { snap, ref, moving, dyn } = B, per = Math.ceil(B.n / 4);
    for (let k = 0; k < per; k++) {
      const i = cur; cur = cur + 1 >= B.n ? 0 : cur + 1;
      const s = solids[i];
      if (s !== ref[i]) { B = build(solids); version++; return; }
      if (!s || moving[i]) continue;
      const j = i * 4;
      if (s.x0 !== snap[j] || s.x1 !== snap[j + 1] || s.z0 !== snap[j + 2] || s.z1 !== snap[j + 3]) { moving[i] = 1; dyn.push(i); }
    }
  }
  // Indices of the solids (not off, unless `all`) whose footprint overlaps [x0,x1]x[z0,z1], in array order (in `idx`).
  function collect(x0, x1, z0, z1, all = false) {
    idx.length = 0;
    const { buckets, big, dyn, stamp } = B;
    if (++B.q > 4e9) { stamp.fill(0); B.q = 1; }
    const q = B.q;
    const take = (i) => {
      if (stamp[i] === q) return; stamp[i] = q;
      const s = solids[i];
      if (s && (all || !s.off) && s.x0 <= x1 && s.x1 >= x0 && s.z0 <= z1 && s.z1 >= z0) idx.push(i);
    };
    const a = cell(x0), b = cell(x1), c = cell(z0), d = cell(z1);
    if ((b - a + 1) * (d - c + 1) > 400) { for (let i = 0; i < B.n; i++) take(i); }
    else for (let x = a; x <= b; x++) for (let z = c; z <= d; z++) { const l = buckets.get(keyOf(x, z)); if (l) for (const i of l) take(i); }
    for (const i of big) take(i);
    for (const i of dyn) take(i);
    if (idx.length > 1) idx.sort((p, r) => p - r);
    return idx;
  }
  // Solids (not off) whose footprint overlaps [x0,x1]x[z0,z1], in array order; `out` is reused when given.
  function query(x0, x1, z0, z1, out = []) {
    out.length = 0;
    for (const i of collect(x0, x1, z0, z1)) out.push(solids[i]);
    return out;
  }
  // Solids within R of (x, z) (footprint distance, as the old per-frame filter), in array order. The candidates
  // that never move are cached by 1 m cell, so walking inside a cell costs one exact pass over a handful of them.
  function nearby(x, z, R, out = []) {
    const cx = Math.floor(x), cz = Math.floor(z), key = keyOf(cx, cz) * 64 + Math.round(R * 4);
    if (near.key !== key || near.ver !== version) {
      collect(cx - R - 0.01, cx + 1 + R + 0.01, cz - R - 0.01, cz + 1 + R + 0.01, true); // off ones too: gates switch
      near.list.length = 0; for (const i of idx) near.list.push(i);
      near.key = key; near.ver = version;
    }
    out.length = 0; idx.length = 0;
    const ok = (s) => s && !s.off && s.x0 - R < x && s.x1 + R > x && s.z0 - R < z && s.z1 + R > z;
    for (const i of near.list) if (!B.moving[i] && ok(solids[i])) idx.push(i);
    for (const i of B.dyn) if (ok(solids[i])) idx.push(i);
    if (B.dyn.length) idx.sort((p, r) => p - r);
    for (const i of idx) out.push(solids[i]);
    return out;
  }
  return { sync, query, nearby, get size() { return B.n; } };
}

// First hit t (0..1) of the segment o→o+d against the solids near it (pad grows each box), or Infinity.
// skip(s) leaves a solid out.
const hitList = [];
export function segHit(grid, o, d, pad, skip = null) {
  const x0 = Math.min(o.x, o.x + d.x) - pad, x1 = Math.max(o.x, o.x + d.x) + pad;
  const z0 = Math.min(o.z, o.z + d.z) - pad, z1 = Math.max(o.z, o.z + d.z) + pad;
  let t = Infinity;
  for (const s of grid.query(x0, x1, z0, z1, hitList)) {
    if (skip && skip(s)) continue;
    const k = segBox(o, d, s, pad); if (k < t) t = k;
  }
  return t;
}

// The level the player is in (set by engine/controls.js every frame): a crowd built without its level's solids
// collides with these, since only the current level's people are updated.
let active = null;
export const setActiveLevel = (L) => { active = L; };
export const activeLevel = () => active;

// Indoors (every level but the town, unless it says level.outdoor) a room without ceiling data is taken to be
// this high above the feet: the lowest ceiling of all the interiors (apartment home, 2.3 m).
export const ROOM_H = 2.3;
export const indoor = (L) => !!L && L.id !== 'town' && !L.outdoor;
// Ceiling height over (x, z) of the room whose floor is at feetY: the level's ceilings, else the indoor default.
export function headroomAt(L, x, z, feetY) {
  const c = ceilAt(L?.ceilings, x, z, feetY + 0.5);
  if (c < Infinity) return c;
  return indoor(L) ? feetY + ROOM_H : Infinity;
}

// Highest a third-person camera may sit over (x, z): under the ceiling (0.15 m margin, the near plane is 0.08),
// under any solid whose underside is above the head (stair flights, landings, overhangs), and indoors without
// ceiling data no higher than feet + 2.15. A ceiling plane under a floor at (x, z) that is below the head is not a
// lid: it is the underside of a landing the camera boom stands above (walking down a stair, the landing just left;
// the boom's walk-zone test keeps it over that floor). headroomAt (vault, jump) still looks from feet + 0.5.
export function camTop(L, grid, x, z, feetY, headY) {
  let top = Infinity;
  const f = groundAt(L?.walk, x, z, headY - 0.45, false, 50); // the highest floor at (x, z) under the head
  const c = ceilAt(L?.ceilings, x, z, Math.max(feetY + 0.5, f ? f.y : -Infinity));
  if (c < Infinity) top = c - 0.15;
  else if (indoor(L)) top = feetY + ROOM_H - 0.15;
  if (grid) for (const s of grid.query(x - 0.15, x + 0.15, z - 0.15, z + 0.15, hitList)) {
    if (s.y0 > headY && s.y0 - 0.15 < top) top = s.y0 - 0.15; // the underside of something overhead
  }
  return top;
}
