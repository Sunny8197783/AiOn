// Bag grid: where each packed thing sits in the 25 L backpack, plus the 0.5 L front pocket. Pure logic.
//
// packGrid(list, { cols = 10, rows = 5 } = {}) -> { placed, pocket, overflow, used, free, pocketL, fits }
//   list: [{ id, n, pocket = 0 }] (n units in the bag, `pocket` of them wanted in the front pocket).
//   placed: [{ id, x, y, w, h, rotated }] cells from the top-left, x across, y down.
//   pocket: [{ id, n }] what is in the front pocket (CATALOG pocket items only, up to POCKET_L litres;
//     the rest falls back to the main grid). overflow: [{ id, n }] units that found no room.
// Deterministic first-fit decreasing: biggest pieces first (area, then longest side, then id), each one
// at the first free spot in reading order, upright before rotated. Same list -> same layout.
// bagList(G) builds the list from G.items; canAdd(list, id) says whether one more unit still fits.
import { CATALOG, BAG_GRID, POCKET_L } from './items.js';

const round2 = (x) => Math.round(x * 100) / 100;

function units(list) {
  if (!Array.isArray(list)) throw new TypeError('packGrid list must be an array of { id, n, pocket }');
  const grid = [], pocket = [], overflow = {};
  let pocketL = 0;
  for (const { id, n = 1, pocket: want = 0 } of list) {
    const c = CATALOG[id];
    if (!c) throw new Error(`unknown item: ${id}`);
    if (!Number.isInteger(n) || n < 0 || !Number.isInteger(want) || want < 0 || want > n) throw new RangeError(`bad counts for ${id}: n ${n}, pocket ${want}`);
    for (let i = 0; i < n; i++) {
      if (i < want && c.pocket && round2(pocketL + c.volumeL) <= POCKET_L) { pocketL = round2(pocketL + c.volumeL); pocket.push(id); continue; }
      if (!c.cells) { overflow[id] = (overflow[id] ?? 0) + 1; continue; } // fixtures never go in a bag
      grid.push({ id, w: c.cells[0], h: c.cells[1] });
    }
  }
  return { grid, pocket, pocketL, overflow };
}

const count = (ids) => Object.entries(ids.reduce((m, id) => ((m[id] = (m[id] ?? 0) + 1), m), {})).map(([id, n]) => ({ id, n }));

export function packGrid(list, { cols = BAG_GRID.cols, rows = BAG_GRID.rows } = {}) {
  if (!Number.isInteger(cols) || !Number.isInteger(rows) || cols < 1 || rows < 1) throw new RangeError(`grid must be at least 1x1, got ${cols}x${rows}`);
  const { grid, pocket, pocketL, overflow } = units(list);
  grid.sort((a, b) => b.w * b.h - a.w * a.h || Math.max(b.w, b.h) - Math.max(a.w, a.h) || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  const taken = Array.from({ length: rows }, () => new Array(cols).fill(false));
  const free = (x, y, w, h) => {
    if (x + w > cols || y + h > rows) return false;
    for (let j = y; j < y + h; j++) for (let i = x; i < x + w; i++) if (taken[j][i]) return false;
    return true;
  };
  const spot = (w, h) => { for (let y = 0; y < rows; y++) for (let x = 0; x < cols; x++) if (free(x, y, w, h)) return { x, y }; return null; };
  const placed = [];
  let used = 0;
  for (const u of grid) {
    let at = spot(u.w, u.h), w = u.w, h = u.h, rotated = false;
    if (!at && u.w !== u.h && (at = spot(u.h, u.w))) { [w, h, rotated] = [u.h, u.w, true]; }
    if (!at) { overflow[u.id] = (overflow[u.id] ?? 0) + 1; continue; }
    for (let j = at.y; j < at.y + h; j++) for (let i = at.x; i < at.x + w; i++) taken[j][i] = true;
    placed.push({ id: u.id, x: at.x, y: at.y, w, h, rotated });
    used += w * h;
  }
  const over = Object.entries(overflow).map(([id, n]) => ({ id, n }));
  return { placed, pocket: count(pocket), overflow: over, used, free: cols * rows - used, pocketL, fits: over.length === 0 };
}

export const bagList = (G) => Object.entries(G.items).filter(([id, e]) => CATALOG[id] && e.bag > 0).map(([id, e]) => ({ id, n: e.bag, pocket: e.pocket ?? 0 }));

// Would one more `id` still find room? (The bag UI's take rule.)
export function canAdd(list, id, opts) {
  const next = list.some(l => l.id === id) ? list.map(l => (l.id === id ? { ...l, n: (l.n ?? 1) + 1 } : l)) : [...list, { id, n: 1 }];
  return packGrid(next, opts).fits;
}
