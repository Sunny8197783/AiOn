// Where things sit in the backpack while packing by hand (ui/grab3d.js, design M8). Pure: no DOM, no three.js.
// A layout keeps each piece where it was put: a new unit takes the first free spot in reading order (upright,
// then turned) and taking one out leaves a hole, so a bag packed bit by bit can run out of room that a tidy one
// still has. tidyLayout() re-packs it from scratch with game/pack.js (biggest first, deterministic).
//
// layout = { cols, rows, pieces: [{ id, x, y, w, h, rotated }], pocket: { id: n } }
// createLayout(list) -> { layout, overflow: [{ id, n }] }   list = [{ id, n, pocket }] (bagList shape)
// addUnit(L, id)       -> 'grid' | 'pocket' (main part full, went in the front pocket) | 'tidy' (fits once tidied) | 'full'
// removeUnit(L, id)    -> 'grid' | 'pocket' | null (from the main part first, so a pocket choice stays)
// togglePocket(L, id)  -> { to: 'pocket'|'grid', n } | { refused: 'pocketFull'|'tidy'|'full'|'notPocket' }
// tidyLayout(L) -> a new layout | null; sameLayout(a, b); listOf(L); pocketLoad(L) litres; usedCells(L)
import { CATALOG, BAG_GRID, POCKET_L } from '../game/items.js';
import { packGrid, canAdd } from '../game/pack.js';

const round2 = (x) => Math.round(x * 100) / 100;
const vol = (id) => CATALOG[id]?.volumeL ?? 0;

export const pocketLoad = (L) => round2(Object.entries(L.pocket).reduce((s, [id, n]) => s + vol(id) * n, 0));
export const usedCells = (L) => L.pieces.reduce((s, p) => s + p.w * p.h, 0);
const gridCount = (L, id) => L.pieces.filter(p => p.id === id).length;

// [{ id, n, pocket }] in first-seen order, as game/pack.js takes it.
export function listOf(L) {
  const rows = new Map();
  const row = (id) => { if (!rows.has(id)) rows.set(id, { id, n: 0, pocket: 0 }); return rows.get(id); };
  for (const p of L.pieces) row(p.id).n++;
  for (const [id, n] of Object.entries(L.pocket)) if (n > 0) { const r = row(id); r.n += n; r.pocket += n; }
  return [...rows.values()];
}

function spot(L, w, h) {
  const taken = Array.from({ length: L.rows }, () => new Array(L.cols).fill(false));
  for (const p of L.pieces) for (let j = p.y; j < p.y + p.h; j++) for (let i = p.x; i < p.x + p.w; i++) taken[j][i] = true;
  const free = (x, y, ww, hh) => {
    if (x + ww > L.cols || y + hh > L.rows) return false;
    for (let j = y; j < y + hh; j++) for (let i = x; i < x + ww; i++) if (taken[j][i]) return false;
    return true;
  };
  for (const [ww, hh, rotated] of w === h ? [[w, h, false]] : [[w, h, false], [h, w, true]]) {
    for (let y = 0; y < L.rows; y++) for (let x = 0; x < L.cols; x++) if (free(x, y, ww, hh)) return { x, y, w: ww, h: hh, rotated };
  }
  return null;
}
const putGrid = (L, id) => { const c = CATALOG[id]?.cells, at = c && spot(L, c[0], c[1]); if (!at) return false; L.pieces.push({ id, ...at }); return true; };
const pocketRoom = (L, id) => !!CATALOG[id]?.pocket && round2(pocketLoad(L) + vol(id)) <= POCKET_L;
// Reopening the bag re-packs it with packGrid (biggest first), which can need more room than a hand-packed order:
// a unit only stays in the main part when the re-pack still holds everything, so reopening never moves one home.
const repacks = (L) => packGrid(listOf(L), { cols: L.cols, rows: L.rows }).fits;

export function createLayout(list, { cols = BAG_GRID.cols, rows = BAG_GRID.rows } = {}) {
  const r = packGrid(list, { cols, rows });
  return {
    layout: { cols, rows, pieces: r.placed.map(p => ({ ...p })), pocket: Object.fromEntries(r.pocket.map(p => [p.id, p.n])) },
    overflow: r.overflow,
  };
}

export function addUnit(L, id) {
  if (!CATALOG[id]?.cells) return 'full'; // a fixture (the curtain) never goes in a bag
  if (putGrid(L, id)) { if (repacks(L)) return 'grid'; L.pieces.pop(); }
  if (pocketRoom(L, id)) { L.pocket[id] = (L.pocket[id] ?? 0) + 1; return 'pocket'; }
  return canAdd(listOf(L), id, { cols: L.cols, rows: L.rows }) ? 'tidy' : 'full';
}

export function removeUnit(L, id) {
  for (let i = L.pieces.length - 1; i >= 0; i--) if (L.pieces[i].id === id) { L.pieces.splice(i, 1); return 'grid'; }
  if (L.pocket[id] > 0) { if (--L.pocket[id] === 0) delete L.pocket[id]; return 'pocket'; }
  return null;
}

// A held chip pressed: units in the main part move to the pocket while it has room; if they are all in the
// pocket already, they go back to the main part (all or nothing).
export function togglePocket(L, id) {
  if (!CATALOG[id]?.pocket) return { refused: 'notPocket' };
  if (gridCount(L, id) > 0) {
    let n = 0;
    while (gridCount(L, id) > 0 && pocketRoom(L, id)) { removeUnit(L, id); L.pocket[id] = (L.pocket[id] ?? 0) + 1; n++; }
    return n ? { to: 'pocket', n } : { refused: 'pocketFull' };
  }
  const n = L.pocket[id] ?? 0;
  if (!n) return { refused: 'notPocket' };
  const before = L.pieces.length;
  let put = 0; while (put < n && putGrid(L, id)) put++;
  if (put === n) { const pocket = { ...L.pocket }; delete pocket[id]; if (repacks({ ...L, pocket })) { delete L.pocket[id]; return { to: 'grid', n }; } }
  L.pieces.length = before; // undo: stays in the pocket
  const out = { ...L, pieces: [...L.pieces], pocket: { ...L.pocket } }; delete out.pocket[id];
  return { refused: packGrid(listOf(out).concat({ id, n }), { cols: L.cols, rows: L.rows }).fits ? 'tidy' : 'full' };
}

// null when the tidy order would not hold everything (first-fit decreasing is not always best): keep L then.
export function tidyLayout(L) {
  const r = createLayout(listOf(L), { cols: L.cols, rows: L.rows });
  return r.overflow.length ? null : r.layout;
}

const key = (L) => L.pieces.map(p => `${p.id}@${p.x},${p.y},${p.w}x${p.h}`).sort().join('|');
export const sameLayout = (a, b) => key(a) === key(b);
