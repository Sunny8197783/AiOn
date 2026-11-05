// Where the 3D picker (ui/grab3d.js) stands its items: shelf heights, items per row and the spacing across.
// Pure, so tests can check that tags never overlap and stay on screen.
// shelfLayout({ kind: 'shop'|'bag', n, fov (deg), aspect, dist }) -> { rows: [y], perRow, sp, scale }
//   rows: the heights in use (one per row, above the floor), perRow: items per row, sp: metres between
//   neighbours (<= SP, capped so a full row of tags, TAG_NEAR in front of the shelf, spans at most WIDTH of
//   the visible width), scale = sp / SP (items and tags shrink with the spacing).
export const SP = 0.36, TAG_W = 0.26, TAG_NEAR = 0.12, WIDTH = 0.78; // WIDTH leaves room for the downward look (rows below the eye spread wider)
const SHELF = { shop: [1.42, 1.02, 0.62], bag: [0.98, 0.66, 0.34] }, BAG2 = [0.82, 0.5]; // two-row bags keep their old look

export function shelfLayout({ kind = 'bag', n = 0, fov = 70, aspect = 16 / 9, dist = 1.1 } = {}) {
  const count = Math.max(1, n | 0), nRows = count > 10 ? 3 : count > 5 ? 2 : 1, perRow = Math.ceil(count / nRows);
  const all = kind === 'shop' ? SHELF.shop : nRows <= 2 ? BAG2 : SHELF.bag;
  const rows = all.slice(0, Math.ceil(count / perRow));
  const halfW = Math.tan((fov * Math.PI) / 360) * aspect * (dist - TAG_NEAR) * WIDTH; // at the tags' plane
  const sp = kind === 'shop' ? SP : Math.min(SP, halfW / ((perRow - 1) / 2 + TAG_W / (2 * SP)));
  return { rows, perRow, sp, scale: sp / SP };
}

// x offset (metres along the shelf) of item i: rows are centred, the last row may hold fewer.
export function slotX(L, i, n) {
  const r = Math.floor(i / L.perRow), k = i % L.perRow, cols = Math.min(L.perRow, n - r * L.perRow);
  return { row: r, x: (k - (cols - 1) / 2) * L.sp };
}
