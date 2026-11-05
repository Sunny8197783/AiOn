// 한빛아파트 101-103동 facades and relief for town-buildings.js. South: living-room balcony sashes over a
// painted parapet or 확장형 full-height glass behind a 난간, room windows with 실외기 on the ledge, varied
// curtains, blinds, laundry and plants. North: the 복도식 corridor (parapet bands, corridor glass with unit
// doors, meter boxes, kitchen grilles and the 소화전함 behind it) and the stair core over the entrance. One
// texture (one draw call) for both faces: a 24 m x 15-floor south tile, the north tile and the core strip side by
// side, shared by the three blocks; each half face shifts its tile by whole floors, so no two halves repeat. Geometry reads the same plan as the paint, so a railing or 실외기 always
// stands where the texture has one. The graphics tier sets what is built (detailOf).
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { CURB } from './town-plan.js';

// 0: textures only (완전 낮음, 낮음), 1: relief (bands, parapets, railings, 방범창, roof doors), 2: fine parts
// (실외기 and cages, ladders, antennas, vents). From the tier table's mesh-detail knob (engine/quality.js).
export const detailOf = (kit) => { const s = kit.Q?.goodsSeg ?? 10; return s >= 10 ? 2 : s >= 8 ? 1 : 0; };

// Geometry batches by material, one merged mesh each (kit.merge then folds them into the group's own).
export function parts() {
  const by = new Map();
  return {
    add(mat, g, cast = true) { const k = mat.uuid + cast; if (!by.has(k)) by.set(k, { mat, cast, list: [] }); by.get(k).list.push(g); },
    flush(group) {
      for (const { mat, cast, list } of by.values()) { const m = new THREE.Mesh(mergeGeometries(list, false), mat); m.castShadow = cast; m.receiveShadow = true; group.add(m); }
      by.clear();
    },
  };
}
// Box w x h x d with its bottom at y, turned ry, non-indexed. drop: faces left out ('px nx py ny pz nz', before ry);
// uvm: metres per texture repeat (world-size UVs for tiled surfaces).
const FACES = ['px', 'nx', 'py', 'ny', 'pz', 'nz'];
export function box(w, h, d, x, y, z, { ry = 0, drop = '', uvm = 0 } = {}) {
  let g = new THREE.BoxGeometry(w, h, d).toNonIndexed();
  if (uvm) { const uv = g.attributes.uv; for (let i = 0; i < uv.count; i++) { const f = (i / 6) | 0, [su, sv] = f < 2 ? [d, h] : f < 4 ? [w, d] : [w, h]; uv.setXY(i, uv.getX(i) * su / uvm, uv.getY(i) * sv / uvm); } }
  if (drop) {
    const out = new THREE.BufferGeometry();
    for (const [name, a] of Object.entries(g.attributes)) {
      const n = a.itemSize, arr = [];
      FACES.forEach((f, i) => { if (!drop.includes(f)) for (let j = i * 6 * n; j < (i + 1) * 6 * n; j++) arr.push(a.array[j]); });
      out.setAttribute(name, new THREE.Float32BufferAttribute(arr, n));
    }
    g = out;
  }
  if (ry) g.rotateY(ry);
  return g.translate(x, y + h / 2, z);
}
export const cylG = (r0, r1, h, x, y, z, seg = 8) => new THREE.CylinderGeometry(r0, r1, h, seg, 1, true).toNonIndexed().translate(x, y + h / 2, z);
// Vertical quad facing +z (turn with ry), bottom at y; uv x scaled by w / uw so a texture keeps its metre size.
export function quad(w, h, x, y, z, ry = 0, uw = 0) {
  const g = new THREE.PlaneGeometry(w, h).toNonIndexed(), uv = g.attributes.uv;
  if (uw) for (let i = 0; i < uv.count; i++) uv.setX(i, uv.getX(i) * w / uw);
  if (ry) g.rotateY(ry);
  return g.translate(x, y + h / 2, z);
}

// Steel bars on clear: 난간, 방범창 and 실외기 cages (alpha-tested, one draw call for all of them).
export const barsMat = (kit) => kit.memo('town:bars', () => new THREE.MeshStandardMaterial({
  map: kit.texture(kit.canvas(128, 128, (g, w, h) => {
    g.fillStyle = '#ffffff'; g.fillRect(0, 0, w, 9); g.fillRect(0, h - 6, w, 6);
    for (let x = 2; x < w; x += 128 / 24) g.fillRect(x, 0, 2.2, h);
  }), { color: false }), color: 0x8d9398, alphaTest: 0.5, side: THREE.DoubleSide, metalness: 0.6, roughness: 0.45,
}));
// 실외기: the front (u 0..0.5) has the fan grille, the other faces (u 0.5..1) plain louvred casing.
export const acMat = (kit) => kit.memo('town:acAtlas', () => new THREE.MeshStandardMaterial({ roughness: 0.5, map: kit.texture(kit.canvas(256, 96, (g, w, h) => {
  g.fillStyle = '#e4e3de'; g.fillRect(0, 0, w, h);
  g.fillStyle = '#9da09f'; g.beginPath(); g.arc(w * 0.19, h / 2, h * 0.38, 0, 7); g.fill();
  g.strokeStyle = '#6a6d6c'; g.lineWidth = 2; for (let i = 1; i < 7; i++) { g.beginPath(); g.arc(w * 0.19, h / 2, h * 0.06 * i, 0, 7); g.stroke(); }
  g.fillStyle = '#c9c8c2'; for (let y = 8; y < h - 8; y += 6) { g.fillRect(w * 0.37, y, w * 0.1, 2); g.fillRect(w * 0.55, y, w * 0.4, 1.5); }
  g.fillStyle = 'rgba(90,80,64,0.25)'; g.fillRect(0, h - 6, w, 6); // grime along the base
})) }));
export function acBox(w, h, d, x, y, z, ry = 0) {
  const g = box(w, h, d, 0, 0, 0), uv = g.attributes.uv;
  for (let i = 0; i < uv.count; i++) uv.setX(i, (i >= 24 && i < 30 ? 0 : 0.5) + uv.getX(i) * 0.5); // vertices 24..29: the +z face
  if (ry) g.rotateY(ry);
  return g.translate(x, y, z);
}

// ---------- the paint ----------
const TILE = 24, CORE = 4;
const CURTAIN = ['#ebe4d3', '#d9ccb4', '#c8d1d6', '#efe8db', '#b8a58b', '#d6c3c0'];
const LAUNDRY = ['#f4f4f0', '#dfe6ec', '#9fb3c8', '#2f3b52', '#d9a5a5', '#e9dfc9', '#7d8a6a', '#c9c9c9'];
const LEAF = ['#4f6b35', '#5b7a3c', '#3f5a2a', '#6a7f3e'];
const pick = (r, a) => a[(r() * a.length) | 0];

// What each south cell (row 0..14 of the tile, unit 0..3 of 6 m) holds; paint and geometry both read it.
function southPlan(kit) {
  return kit.memo('town:aptPlan', () => {
    const r = kit.rng(4421);
    // glow as kit.facade's windows: mostly warm rooms below the bloom threshold, a few cool (TV/LED), one in 14 bright
    const tone = (t) => (t < 0.07 ? '255,224,176' : t > 0.86 ? '190,208,232' : r() < 0.5 ? '255,198,128' : '255,180,104');
    const win = () => {
      const lit = r() < 0.6, t = r(), glow = tone(t), a = t < 0.07 ? 0.9 + r() * 0.1 : 0.42 + r() * 0.32;
      return { lit, glow, a, cover: pick(r, ['none', 'none', 'curtain', 'curtain', 'sheer', 'blind']), cl: 0.12 + r() * 0.3, cr: 0.12 + r() * 0.3, bf: 0.25 + r() * 0.75, col: pick(r, CURTAIN) };
    };
    return Array.from({ length: 15 }, () => Array.from({ length: 4 }, () => {
      const ext = r() < 0.3, ac = r() < 0.72;
      return { ext, ac, cage: ac && r() < 0.3, frame: r() < 0.12 ? '#6b5a4a' : r() < 0.15 ? '#b9bec2' : '#e6e4de', big: win(), small: win(), laundry: !ext && r() < 0.24, plants: r() < 0.3, boxes: !ext && r() < 0.12 };
    }));
  });
}

function pane(c, e, ro, x0, y0, x1, y1, w, frame, panels, q, extra) {
  const W = x1 - x0, H = y1 - y0;
  c.fillStyle = w.lit ? '#3c4650' : '#26313b'; c.fillRect(x0, y0, W, H);
  c.fillStyle = 'rgba(255,255,255,0.05)'; c.fillRect(x0, y1 - H * 0.12, W, H * 0.12); // the ceiling inside
  if (w.lit) { const g = e.createLinearGradient(0, y1, 0, y0); g.addColorStop(0, `rgba(${w.glow},${w.a})`); g.addColorStop(1, `rgba(${w.glow},${w.a * 0.6})`); e.fillStyle = g; e.fillRect(x0, y0, W, H); } // lamp deeper in: brighter at the top
  const dim = (x, y, ww, hh, k) => { if (!w.lit) return; e.globalAlpha = k; e.fillStyle = '#000'; e.fillRect(x, y, ww, hh); };
  extra?.(dim);
  if (w.cover === 'curtain') {
    for (const [a, b] of [[x0, x0 + W * w.cl], [x1 - W * w.cr, x1]]) {
      c.fillStyle = w.col; c.fillRect(a, y0, b - a, H);
      c.fillStyle = 'rgba(0,0,0,0.09)'; for (let x = a + 0.06; x < b; x += 0.13) c.fillRect(x, y0, 0.05, H);
      dim(a, y0, b - a, H, 0.5); if (w.lit) { e.globalAlpha = w.a * 0.2; e.fillStyle = w.col; e.fillRect(a, y0, b - a, H); }
    }
  } else if (w.cover === 'sheer') { c.fillStyle = 'rgba(235,232,224,0.42)'; c.fillRect(x0, y0, W, H); dim(x0, y0, W, H, 0.22); }
  else if (w.cover === 'blind') {
    const yb = y1 - H * w.bf;
    c.fillStyle = '#dcdad3'; c.fillRect(x0, yb, W, y1 - yb); c.fillStyle = 'rgba(0,0,0,0.12)'; for (let y = yb; y < y1; y += 0.05) c.fillRect(x0, y, W, 0.012);
    dim(x0, yb, W, y1 - yb, 0.5); if (w.lit) { e.globalAlpha = 0.3; for (let y = yb; y < y1; y += 0.05) e.fillRect(x0, y, W, 0.015); }
  }
  const gr = c.createLinearGradient(x0, y1, x0 + W * 0.7, y0); // sky in the glass, strongest at the top left
  gr.addColorStop(0, 'rgba(190,212,228,0.2)'); gr.addColorStop(1, 'rgba(190,212,228,0)'); c.fillStyle = gr; c.fillRect(x0, y0, W, H);
  c.strokeStyle = frame; c.lineWidth = 0.05; c.strokeRect(x0, y0, W, H);
  e.globalAlpha = 1; e.strokeStyle = '#000'; e.lineWidth = 0.06; e.strokeRect(x0, y0, W, H);
  for (let i = 1; i < panels; i++) { const x = x0 + W * i / panels; for (const g of [c, e]) { g.beginPath(); g.moveTo(x, y0); g.lineTo(x, y1); g.stroke(); } }
  ro.fillStyle = '#4d4d4d'; ro.fillRect(x0, y0, W, H); // glass: smooth, picks up the sky
}
function acPaint(c, ro, x, y, cage) {
  c.fillStyle = '#e4e3de'; c.fillRect(x, y, 0.84, 0.6); c.fillStyle = '#9da09f'; c.beginPath(); c.arc(x + 0.3, y + 0.3, 0.23, 0, 7); c.fill();
  c.strokeStyle = '#6a6d6c'; c.lineWidth = 0.015; for (let i = 1; i < 6; i++) { c.beginPath(); c.arc(x + 0.3, y + 0.3, 0.04 * i, 0, 7); c.stroke(); }
  c.fillStyle = '#c9c8c2'; for (let yy = y + 0.08; yy < y + 0.55; yy += 0.05) c.fillRect(x + 0.6, yy, 0.18, 0.015);
  c.fillStyle = 'rgba(0,0,0,0.18)'; c.fillRect(x, y, 0.84, 0.03);
  if (cage) { c.strokeStyle = 'rgba(150,156,160,0.9)'; c.lineWidth = 0.012; for (let xx = x - 0.04; xx < x + 0.9; xx += 0.06) { c.beginPath(); c.moveTo(xx, y); c.lineTo(xx, y + 0.68); c.stroke(); } }
  ro.fillStyle = '#909090'; ro.fillRect(x, y, 0.84, 0.6);
}
// Dirt washed down from a sill corner, kept inside the floor (the tile wraps by whole floors).
const streak = (c, x, y, len) => { const g = c.createLinearGradient(x, y, x, y - len); g.addColorStop(0, 'rgba(78,70,58,0.09)'); g.addColorStop(1, 'rgba(78,70,58,0)'); c.fillStyle = g; c.fillRect(x - 0.04, y - len, 0.08, len); };

function paintSouth(c, e, ro, plan, fh, q, detail) {
  c.fillStyle = '#e8e3d8'; c.fillRect(0, 0, TILE, 15 * fh);
  for (let r = 0; r < 15; r++) {
    const y0 = r * fh;
    c.fillStyle = 'rgba(0,0,0,0.08)'; c.fillRect(0, y0, TILE, 0.06);
    c.fillStyle = 'rgba(70,64,54,0.05)'; c.fillRect(0, y0 + fh - 0.2, TILE, 0.2); // under the slab above
    for (let k = 0; k < 4; k++) {
      const s = plan[r][k], x0 = k * 6, bx0 = x0 + 0.24, bx1 = x0 + 2.76, by0 = y0 + (s.ext ? 0.22 : 1.0), by1 = y0 + 2.42;
      if (!s.ext) { c.fillStyle = '#f2efe8'; c.fillRect(bx0 - 0.03, y0 + 0.16, bx1 - bx0 + 0.06, 0.84); c.fillStyle = '#d9d4c8'; c.fillRect(bx0 - 0.05, y0 + 0.96, bx1 - bx0 + 0.1, 0.04); }
      c.fillStyle = '#c9c3b6'; c.fillRect(bx0 - 0.06, by0 - 0.06, bx1 - bx0 + 0.12, by1 - by0 + 0.12);
      pane(c, e, ro, bx0, by0, bx1, by1, s.big, s.frame, 4, q, (dim) => {
        if (s.laundry) { // a drying rack inside the balcony
          c.strokeStyle = '#9aa0a4'; c.lineWidth = 0.02; c.beginPath(); c.moveTo(bx0 + 0.1, by1 - 0.2); c.lineTo(bx1 - 0.1, by1 - 0.2); c.stroke();
          for (let x = bx0 + 0.15 + q() * 0.3; x < bx1 - 0.3; x += 0.3 + q() * 0.25) { const w = 0.2 + q() * 0.25, h = 0.3 + q() * 0.45; c.fillStyle = pick(q, LAUNDRY); c.fillRect(x, by1 - 0.2 - h, w, h); dim(x, by1 - 0.2 - h, w, h, 0.85); }
        }
        if (s.boxes) for (let x = bx0 + 0.2 + q() * 1.2, n = 1 + (q() * 3 | 0); n-- > 0; x += 0.42) { const h = 0.2 + q() * 0.25; c.fillStyle = q() < 0.5 ? '#c9b48f' : '#d8d2c4'; c.fillRect(x, by0, 0.38, h); dim(x, by0, 0.38, h, 0.8); }
        if (s.plants) {
          const px = bx0 + 0.2 + q() * (bx1 - bx0 - 0.7), top = s.ext ? 0.75 : 0.35;
          if (s.ext) { c.fillStyle = q() < 0.5 ? '#a5603f' : '#e9e6df'; c.fillRect(px + 0.08, by0, 0.3, 0.25); dim(px + 0.08, by0, 0.3, 0.25, 0.85); }
          for (let i = 0; i < 18; i++) {
            const lx = px + q() * 0.46, ly = by0 + (s.ext ? 0.2 : 0) + q() * top;
            c.fillStyle = pick(q, LEAF); c.beginPath(); c.ellipse(lx, ly, 0.06 + q() * 0.05, 0.025, q() * 3, 0, 7); c.fill();
            if (s.big.lit) { e.globalAlpha = 0.8; e.fillStyle = '#000'; e.beginPath(); e.ellipse(lx, ly, 0.07, 0.03, 0, 0, 7); e.fill(); }
          }
        }
      });
      if (s.ext && !detail) { // the painted railing, only on the tiers without the real one (no double image)
        c.strokeStyle = 'rgba(80,86,90,0.35)'; c.lineWidth = 0.015;
        for (let x = bx0; x <= bx1; x += 0.105) { c.beginPath(); c.moveTo(x, y0 + 0.1); c.lineTo(x, y0 + 1.1); c.stroke(); }
        c.lineWidth = 0.04; c.beginPath(); c.moveTo(bx0, y0 + 1.1); c.lineTo(bx1, y0 + 1.1); c.stroke();
      } else if (!s.ext) { streak(c, bx0, y0 + 0.96, 0.7); streak(c, bx1, y0 + 0.96, 0.55 + q() * 0.25); }
      const sx0 = x0 + 3.24, sx1 = x0 + 4.74, sy0 = y0 + 1.05, sy1 = y0 + 2.33;
      c.fillStyle = '#c9c3b6'; c.fillRect(sx0 - 0.06, sy0 - 0.06, sx1 - sx0 + 0.12, sy1 - sy0 + 0.12);
      pane(c, e, ro, sx0, sy0, sx1, sy1, s.small, s.frame, 2, q);
      c.fillStyle = '#d6d0c3'; c.fillRect(sx0 - 0.1, sy0 - 0.1, sx1 - sx0 + 0.2, 0.05); // sill
      streak(c, sx0 - 0.05, sy0 - 0.1, 0.6 + q() * 0.3); streak(c, sx1 + 0.05, sy0 - 0.1, 0.5);
      if (s.ac) acPaint(c, ro, x0 + 4.88, y0 + 0.09, s.cage);
    }
  }
}

function paintNorth(c, e, ro, fh, q) {
  c.fillStyle = '#e3ded3'; c.fillRect(0, 0, TILE + CORE, 15 * fh);
  const DOOR = ['#4a4039', '#5b6064', '#3e4757', '#6a5a4a', '#585148', '#4e5a52'];
  for (let r = 0; r < 15; r++) {
    const y0 = r * fh, wy0 = y0 + 1.02, wy1 = y0 + 2.2;
    c.fillStyle = '#d9d3c7'; c.fillRect(0, y0, TILE, 0.22); c.fillStyle = 'rgba(0,0,0,0.12)'; c.fillRect(0, y0, TILE, 0.03);
    c.fillStyle = '#cfc8bb'; c.fillRect(0, y0 + 0.97, TILE, 0.05);
    for (let k = 0; k < 6; k++) { // corridor glass, 3.2 m between 0.8 m piers
      const x0 = k * 4 + 0.4, x1 = k * 4 + 3.6;
      c.fillStyle = '#8e8a82'; c.fillRect(x0, wy0, 3.2, wy1 - wy0); // the corridor's inner wall in shade
      c.fillStyle = '#9b988f'; c.fillRect(x0, wy1 - 0.1, 3.2, 0.1);
    }
    for (let u = 0; u < 2; u++) { // a unit every 12 m: steel door, meter box, kitchen window with its grille
      const dx = u * 12 + 1.6, kx = u * 12 + 6.2, lamp = q() < 0.5, cook = q() < 0.3;
      c.fillStyle = pick(q, DOOR); c.fillRect(dx, wy0, 0.95, 1.1); c.fillStyle = '#2f2b29'; c.fillRect(dx - 0.05, wy0, 0.05, 1.12); c.fillRect(dx + 0.95, wy0, 0.05, 1.12); c.fillRect(dx - 0.05, wy0 + 1.08, 1.05, 0.05);
      c.fillStyle = '#1e1e1e'; c.fillRect(dx + 0.78, wy0 + 0.02, 0.07, 0.14); // digital lock
      c.fillStyle = '#9aa0a4'; c.fillRect(dx + 1.12, y0 + 1.45, 0.4, 0.45); c.fillStyle = 'rgba(0,0,0,0.25)'; c.fillRect(dx + 1.12, y0 + 1.66, 0.4, 0.02);
      c.fillStyle = '#7f878d'; c.fillRect(kx, y0 + 1.3, 1.3, 0.65); c.strokeStyle = '#4b5055'; c.lineWidth = 0.02; for (let x = kx; x <= kx + 1.3; x += 0.1) { c.beginPath(); c.moveTo(x, y0 + 1.3); c.lineTo(x, y0 + 1.95); c.stroke(); }
      c.fillStyle = '#d9dcd8'; c.fillRect(dx + 0.25, wy1 - 0.07, 0.4, 0.04); // ceiling lamp by the door
      if (lamp) { e.globalAlpha = 0.12; e.fillStyle = '#cdd6cc'; e.fillRect(Math.floor(dx / 4) * 4 + 0.4, wy0, 3.2, wy1 - wy0); e.globalAlpha = 0.42; e.fillRect(dx - 0.3, wy1 - 0.6, 1.55, 0.6); }
      if (cook) { e.globalAlpha = 0.35; e.fillStyle = '#ffcf8a'; e.fillRect(kx, y0 + 1.3, 1.3, 0.65); e.globalAlpha = 0.7; e.strokeStyle = '#000'; e.lineWidth = 0.025; for (let x = kx; x <= kx + 1.3; x += 0.1) { e.beginPath(); e.moveTo(x, y0 + 1.3); e.lineTo(x, y0 + 1.95); e.stroke(); } }
    }
    c.fillStyle = '#b5302a'; c.fillRect(21.0, wy0, 0.75, 0.9); c.fillStyle = '#f0e6e0'; c.fillRect(21.08, wy0 + 0.62, 0.59, 0.08); // 소화전함
    for (let k = 0; k < 6; k++) { // corridor clutter that shows over the parapet: tall plants, a bicycle's handlebars
      const v = q(), x = k * 4 + 0.7 + q() * 2.4;
      if (v < 0.14) for (let i = 0; i < 14; i++) { c.fillStyle = pick(q, LEAF); c.beginPath(); c.ellipse(x + q() * 0.4, wy0 + q() * 0.55, 0.08, 0.03, q() * 3, 0, 7); c.fill(); }
      else if (v < 0.22) { c.strokeStyle = '#2a2d30'; c.lineWidth = 0.025; c.beginPath(); c.moveTo(x, wy0 + 0.05); c.lineTo(x + 0.1, wy0 + 0.22); c.lineTo(x + 0.55, wy0 + 0.22); c.stroke(); }
    }
    for (let k = 0; k < 6; k++) {
      const x0 = k * 4 + 0.4, open = q() < 0.25, side = q() < 0.5 ? 0 : 1.6;
      c.fillStyle = 'rgba(36,48,56,0.42)'; c.fillRect(open ? x0 + side : x0, wy0, open ? 1.6 : 3.2, wy1 - wy0); // an open pane shows the corridor untinted
      const gr = c.createLinearGradient(0, wy1, 0, wy0); gr.addColorStop(0, 'rgba(196,214,228,0.24)'); gr.addColorStop(1, 'rgba(196,214,228,0.04)'); c.fillStyle = gr; c.fillRect(x0, wy0, 3.2, wy1 - wy0);
      c.strokeStyle = '#c4c8ca'; c.lineWidth = 0.05; c.strokeRect(x0, wy0, 3.2, wy1 - wy0);
      e.globalAlpha = 1; e.strokeStyle = '#000'; e.lineWidth = 0.06; e.strokeRect(x0, wy0, 3.2, wy1 - wy0);
      const mids = open ? [x0 + 1.6 - 0.04, x0 + 1.6 + 0.04] : [x0 + 1.6];
      for (const x of mids) for (const g of [c, e]) { g.beginPath(); g.moveTo(x, wy0); g.lineTo(x, wy1); g.stroke(); }
      ro.fillStyle = '#4a4a4a'; ro.fillRect(x0, wy0, 3.2, wy1 - wy0);
      streak(c, x0 + 0.05, y0 + 0.97, 0.6 + q() * 0.2); streak(c, x0 + 3.15, y0 + 0.97, 0.5 + q() * 0.3);
    }
    // the stair core strip (x 24..28): its landing window sits half a floor up
    c.fillStyle = '#d8d1c5'; c.fillRect(TILE, y0, CORE, fh); c.fillStyle = 'rgba(0,0,0,0.1)'; c.fillRect(TILE, y0, 0.06, fh); c.fillRect(TILE + CORE - 0.06, y0, 0.06, fh);
    const sy = y0 + fh * 0.5 + 0.3;
    c.fillStyle = '#c4c8ca'; c.fillRect(TILE + 1.35, sy - 0.05, 1.3, 1.0); c.fillStyle = '#2c353c'; c.fillRect(TILE + 1.4, sy, 1.2, 0.9);
    c.strokeStyle = 'rgba(160,166,170,0.5)'; c.lineWidth = 0.03; c.beginPath(); c.moveTo(TILE + 1.4, sy + 0.2); c.lineTo(TILE + 2.6, sy + 0.6); c.stroke(); // the handrail behind
    ro.fillStyle = '#4a4a4a'; ro.fillRect(TILE + 1.4, sy, 1.2, 0.9);
    if (q() < 0.45) { e.globalAlpha = 0.26; e.fillStyle = '#d8d6c4'; e.fillRect(TILE + 1.4, sy, 1.2, 0.9); } // motion-sensor landing lamp
    e.globalAlpha = 1;
  }
}

function facadeMat(kit, key, Wm, Hm, paint, detail) {
  const S = (kit.Q?.tex ?? 512) >= 512 ? 1024 : 512; // texture size of the graphics tier
  return kit.memo(`town:aptFac:${key}:${S}:${detail}:${Hm.toFixed(3)}`, () => {
    const color = kit.canvas(S * 2, S * 2), glow = kit.canvas(S * 2, S * 2), rough = kit.canvas(S, S); // ~40 px per metre
    const [c, e, ro] = [color, glow, rough].map((cv) => cv.getContext('2d'));
    for (const [g, cv] of [[c, color], [e, glow], [ro, rough]]) g.setTransform(cv.width / Wm, 0, 0, -cv.height / Hm, 0, cv.height); // metres, y up
    e.fillStyle = '#000'; e.fillRect(0, 0, Wm, Hm); ro.fillStyle = '#dcdcdc'; ro.fillRect(0, 0, Wm, Hm);
    paint(c, e, ro);
    const m = new THREE.MeshStandardMaterial({ map: kit.texture(color), emissiveMap: kit.texture(glow), roughnessMap: kit.texture(rough, { color: false }), emissive: 0xffffff, emissiveIntensity: 0, roughness: 1, metalness: 0.05 });
    kit.lit('windows', m, 1.1);
    return m;
  });
}

// Faces, relief and the entrance core of one 아파트 동. b: the BUILDINGS entry, h: roof height, core: entrance x,
// k: block index (texture shifts), target: the static group, facePlane: town-buildings' face builder.
const SHIFT_S = [[0, 7], [4, 11], [9, 2]], SHIFT_N = [[0, 5], [8, 13], [3, 10]];
const WT = 2 * TILE + CORE, US = TILE / WT, UN = 2 * TILE / WT; // texture u: south tile | north tile | core strip
export function apartmentFaces(kit, { b, h, core, k, target, facePlane, parapetMat, detail }) {
  const fh = (h - CURB) / 15, plan = southPlan(kit);
  const fac = facadeMat(kit, 'SN', WT, 15 * fh, (c, e, ro) => {
    paintSouth(c, e, ro, plan, fh, kit.rng(77), detail);
    for (const g of [c, e, ro]) g.translate(TILE, 0);
    paintNorth(c, e, ro, fh, kit.rng(78));
  }, detail);
  const mid = (b.x0 + b.x1) / 2, P = parts(), bars = barsMat(kit), steel = kit.mat('steel');
  for (const half of [0, 1]) {
    const a0 = half ? mid : b.x0, a1 = half ? b.x1 : mid, ss = SHIFT_S[k % 3][half], sn = SHIFT_N[k % 3][half];
    target.add(facePlane('s', b.z1, a0, a1, CURB, h, fac, { u1: US, v0: ss / 15, v1: 1 + ss / 15 }));
    target.add(facePlane('n', b.z0, a0, a1, CURB, h, fac, { u0: US, u1: UN, v0: sn / 15, v1: 1 + sn / 15 }));
    if (!detail) continue;
    // south: 난간 on 확장형 bays, parapet faces on the others, 방범창 low down, 실외기 on the ledges
    for (let F = 0; F < 15; F++) for (let u = 0; u < 4; u++) {
      const s = plan[(F + ss) % 15][u], x = a0 + u * 6, y = CURB + F * fh, top = y + 0.09;
      if (F <= 1) P.add(bars, quad(1.56, 1.34, x + 3.99, y + 1.02, b.z1 + 0.07, 0, 0.105 * 24), false);
      if (!F) continue;
      if (s.ext) { P.add(bars, quad(2.52, 1.0, x + 1.5, top, b.z1 + 0.3, 0, 2.52), false); P.add(steel, box(2.56, 0.05, 0.06, x + 1.5, top + 1.0, b.z1 + 0.3, { drop: 'nz ny' })); }
      else { P.add(parapetMat, box(2.58, 0.89, 0.08, x + 1.5, top, b.z1 + 0.04, { drop: 'nz ny py' })); P.add(parapetMat, box(2.64, 0.04, 0.16, x + 1.5, top + 0.89, b.z1 + 0.08, { drop: 'nz ny' })); }
      if (s.ac && detail > 1) {
        P.add(acMat(kit), acBox(0.84, 0.6, 0.3, x + 5.3, top, b.z1 + 0.18));
        if (s.cage) P.add(bars, box(0.94, 0.7, 0.36, x + 5.3, top, b.z1 + 0.2, { drop: 'ny nz' }), false);
      }
    }
  }
  if (detail) {
    // north: corridor slab edge + parapet as one band per floor (the glass sits 0.3 m back), stopped at the core
    const segs = [[b.x0, core - 2.1], [core + 2.1, b.x1]];
    for (let F = 1; F <= 15; F++) {
      const y = CURB + (F - 1) * fh + 2.2, top = F < 15 ? CURB + F * fh + 1.02 : h;
      for (const [s0, s1] of segs) {
        P.add(parapetMat, box(s1 - s0, top - y, 0.3, (s0 + s1) / 2, y, b.z0 - 0.15, { drop: 'pz' }));
        if (F < 15) P.add(kit.mat('curb'), box(s1 - s0, 0.04, 0.36, (s0 + s1) / 2, top, b.z0 - 0.16, { drop: 'pz ny' }), false);
      }
    }
    for (const s of [-1, 1]) P.add(parapetMat, box(0.3, h - CURB - fh, 0.34, core + s * 2.15, CURB + fh, b.z0 - 0.17, { drop: 'pz' })); // core edges
    for (const x of [b.x0 + 0.45, b.x1 - 0.45, core + 2.6]) P.add(parapetMat, cylG(0.06, 0.06, h - CURB, x, CURB, b.z0 - 0.4)); // 우수관
  }
  // the stair core face over the entrance (rows 1..14 of the strip)
  target.add(facePlane('n', b.z0 - 0.05, core - 2, core + 2, CURB + fh, h, fac, { u0: UN, u1: 1, v0: 1 / 15, v1: 1 }));
  P.flush(target);
  return fh;
}
