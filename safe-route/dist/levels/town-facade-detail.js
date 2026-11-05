// Relief for the painted villa and civic faces (kit.facade 'villa' / 'civic' cell plan, in world units), so the
// windows stop reading as wallpaper: white PVC or grey aluminium frames with a mullion, stone sills and slab bands at every
// floor line; on the villa faces along the streets also 방범창, and on the finest tier 실외기 on wall brackets, the
// yellow 가스 riser with a meter per floor and a 선홈통; some villas stand on 필로티 parking (open ground floor:
// columns, beam, a recess with the entrance door, mailboxes and a ceiling lamp; collisions are unchanged, the
// block's own box). Frames follow the painted glass exactly. What is built follows detailOf (town-buildings-detail).
import * as THREE from 'three';
import { box, cylG, quad, parts, barsMat, acMat, acBox } from './town-buildings-detail.js';

const RY = { s: 0, n: Math.PI, e: Math.PI / 2, w: -Math.PI / 2 };
const OUT = { s: 1, e: 1, n: -1, w: -1 };
// Point `out` metres in front of a face at `a` along it, as [x, z].
const onFace = (side, c, a, out) => (side === 's' || side === 'n' ? [a, c + OUT[side] * out] : [c + OUT[side] * out, a]);
// Window rectangles of kit.facade's villa/civic paint: per column a 0.5-wide pane centred in its cell, per floor
// a 0.45-high pane 0.3 of the row above the floor line (the paint counts rows from the top; the cell is symmetric).
function cells(a0, a1, cols, y0, floors, fh) {
  const cw = (a1 - a0) / cols, out = [];
  for (let i = 0; i < cols; i++) for (let f = 0; f < floors; f++) out.push({ i, f, a: a0 + (i + 0.5) * cw, yb: y0 + f * fh + 0.3 * fh, ww: cw * 0.5, wh: fh * 0.45, cw });
  return out;
}
// Frame around one pane (profile t, depth d) with its mullion, and the stone sill under it; backs left out.
function frame(P, M, side, c, w, fmat, t = 0.06, d = 0.06) {
  const ry = RY[side], o = { ry, drop: 'nz' }, [x, z] = onFace(side, c, w.a, d / 2), s = Math.cos(ry), k = -Math.sin(ry); // along-face axis
  const along = (da) => [x + s * da, z + k * da];
  P.add(fmat, box(w.ww + 2 * t, t, d, x, w.yb + w.wh, z, o), false);
  P.add(fmat, box(w.ww + 2 * t, t, d, x, w.yb - t, z, o), false);
  for (const sg of [-1, 1]) { const [px, pz] = along(sg * (w.ww + t) / 2); P.add(fmat, box(t, w.wh, d, px, w.yb, pz, o), false); }
  P.add(fmat, box(0.045, w.wh, d * 0.7, x, w.yb, z, o), false);
  const [sx, sz] = onFace(side, c, w.a, 0.07);
  P.add(M.sill, box(w.ww + 0.16, 0.06, 0.14, sx, w.yb - 0.11, sz, o), false);
}
// Slab band along a face at height y (0.16 m concrete, 5 cm proud).
const band = (P, M, side, c, a0, a1, y) => { const [x, z] = onFace(side, c, (a0 + a1) / 2, 0.025); P.add(M.sill, box(a1 - a0, 0.16, 0.05, x, y - 0.08, z, { ry: RY[side], drop: 'nz' }), false); };
const mats = (kit) => ({
  sill: kit.mat('curb'), white: kit.mat('whiteWall'), steel: kit.mat('steel'), dark: kit.mat('darkMetal'),
  gas: kit.mat('paint:#d6a823', { roughness: 0.6 }), pipe: kit.mat('paint:#9ea3a4', { roughness: 0.7 }),
});

// The 필로티 recess behind an open ground floor (inside the block's collision box): span a0..a1, clear height y0..y1.
function piloti(kit, P, M, side, c, a0, a1, y0, y1, q) {
  const conc = kit.memo('town:pilotiConc', () => kit.surface('concrete', { color: 0x9c9a94 })), ry = RY[side], D = 5;
  const lamp = kit.memo('town:pilotiLamp', () => kit.lit('windows', new THREE.MeshStandardMaterial({ color: 0xf2efe6, emissive: 0xffe6c0, emissiveIntensity: 0.5, roughness: 0.5 }), 0.5));
  const L = a1 - a0, mid = (a0 + a1) / 2, back = onFace(side, c, mid, -D), H = y1 - y0;
  P.add(conc, quad(L, H, back[0], y0, back[1], ry, 3)); // back wall, facing out
  const inA = side === 's' || side === 'n' ? Math.PI / 2 : 0; // a quad turned so it faces +a
  for (const [a, turn] of [[a0, inA], [a1, inA + Math.PI]]) { const [x, z] = onFace(side, c, a, -D / 2); P.add(conc, quad(D, H, x, y0, z, turn, 3)); } // side walls, facing in
  const top = new THREE.PlaneGeometry(L, D).toNonIndexed().rotateX(Math.PI / 2).rotateY(ry), [cx, cz] = onFace(side, c, mid, -D / 2);
  P.add(conc, top.translate(cx, y1 - 0.3, cz)); // soffit under the beam line
  const flo = new THREE.PlaneGeometry(L, D).toNonIndexed().rotateX(-Math.PI / 2).rotateY(ry);
  P.add(conc, flo.translate(cx, y0 + 0.005, cz), false);
  const [bx, bz] = onFace(side, c, mid, 0); P.add(conc, box(L + 0.3, 0.3, 0.3, bx, y1 - 0.3, bz, { ry, uvm: 3 })); // edge beam, 15 cm proud
  const n = Math.max(2, Math.round(L / 5.5) + 1);
  for (let k = 0; k < n; k++) { const [x, z] = onFace(side, c, a0 + 0.25 + (L - 0.5) * k / (n - 1), -0.25); P.add(conc, box(0.45, H - 0.3, 0.45, x, y0, z, { ry, uvm: 3 })); }
  // the 공동현관 at the back: dark glass door in a steel frame, a keypad, a mailbox bank, a lamp over the door
  const da = mid + (q() - 0.5) * L * 0.4, at = (a, o) => onFace(side, c, a, -D + o), o = { ry, drop: 'nz' };
  { const [x, z] = at(da, 0.03); P.add(M.steel, box(1.2, 2.2, 0.06, x, y0, z, o), false); }
  { const [x, z] = at(da, 0.07); P.add(M.dark, box(1.04, 2.08, 0.04, x, y0, z, o), false); }
  { const [x, z] = at(da + 0.85, 0.04); P.add(M.dark, box(0.12, 0.18, 0.05, x, y0 + 1.25, z, o), false); }
  { const [x, z] = at(da - 1.5, 0.08); P.add(M.steel, box(0.9, 0.6, 0.16, x, y0 + 1.1, z, o)); }
  { const [x, z] = at(da, 0.06); P.add(lamp, box(0.3, 0.08, 0.12, x, y0 + 2.35, z, o), false); }
  { const [x, z] = onFace(side, c, mid, -D * 0.45); P.add(lamp, box(0.6, 0.04, 0.12, x, y1 - 0.34, z, { ry }), false); }
}
// Is this villa block on 필로티? (its own random stream: the town's other choices keep theirs)
export const isPiloti = (kit, x0, z0, len) => len >= 8 && kit.rng(Math.round(x0 * 7 + z0 * 13) + 5)() < 0.45;

// One villa face along a street. reps: texture repeats (4 window columns each), fhv: floor height. pil: open
// ground floor (the caller cut the facade plane there).
export function villaFront(kit, target, { side, c, a0, a1, y0, floors, fhv, reps, seed, detail, pil = false }) {
  const P = parts(), M = mats(kit), bars = barsMat(kit), q = kit.rng(seed), w2 = kit.rng(seed + 17), ry = RY[side];
  for (const w of cells(a0, a1, reps * 4, y0, floors, fhv)) {
    if (pil && w.f === 0) continue;
    frame(P, M, side, c, w, w2() < 0.15 ? M.steel : M.white); // some units kept their old grey aluminium sashes
    if (w.f <= 1 || q() < 0.25) { const [gx, gz] = onFace(side, c, w.a, 0.08); P.add(bars, quad(w.ww + 0.1, w.wh + 0.1, gx, w.yb - 0.05, gz, ry, 2.52), false); }
    if (detail > 1 && w.f > 0 && w.i < reps * 4 - 1 && w2() < 0.3) { // 실외기 on a bracket between two windows
      const a = w.a + w.cw / 2, y = w.yb - 0.1, [x, z] = onFace(side, c, a, 0.2), [bx, bz] = onFace(side, c, a, 0.17);
      P.add(acMat(kit), acBox(0.78, 0.56, 0.28, x, y, z, ry));
      for (const s of [-0.28, 0.28]) { const [px, pz] = [bx + Math.cos(ry) * s, bz - Math.sin(ry) * s]; P.add(M.steel, box(0.04, 0.04, 0.34, px, y - 0.04, pz, { ry }), false); }
    }
  }
  for (let f = 1; f < floors; f++) band(P, M, side, c, a0, a1, y0 + f * fhv);
  if (pil) piloti(kit, P, M, side, c, a0 + 0.3, a1 - 0.3, y0, y0 + fhv, q);
  if (detail > 1) {
    const ga = a0 + 0.2, top = y0 + (floors - 1) * fhv + 1.6, [gx, gz] = onFace(side, c, ga, 0.09); // 가스 riser and meters
    P.add(M.gas, cylG(0.028, 0.028, top - y0 - 0.4, gx, y0 + 0.4, gz, 6), false);
    for (let f = 0; f < floors; f++) {
      const y = y0 + f * fhv + 1.35, [mx, mz] = onFace(side, c, ga + 0.25, 0.1), [px, pz] = onFace(side, c, ga + 0.12, 0.09);
      P.add(M.steel, box(0.26, 0.3, 0.16, mx, y - 0.15, mz, { ry, drop: 'nz' }), false);
      P.add(M.gas, box(0.24, 0.05, 0.05, px, y + 0.2, pz, { ry }), false);
    }
    const [dx, dz] = onFace(side, c, a1 - 0.25, 0.08); // 선홈통 from the roof edge
    P.add(M.pipe, cylG(0.05, 0.05, floors * fhv + 0.9, dx, y0, dz, 8));
    P.add(M.pipe, box(0.22, 0.2, 0.22, dx, y0 + floors * fhv + 0.6, dz, { ry }));
  }
  P.flush(target);
}

// The civic buildings (행정복지센터, 생활문화센터, 관리동): aluminium frames, stone sills, slab bands on every face.
// ground: faces whose ground floor gets frames too (the others carry entrances, canopies, shop glass or ramps).
export function civicFaces(kit, target, { b, h, y0, floors, colsL, colsS, ground = '', detail }) {
  const P = parts(), M = mats(kit), fh = (h - y0) / floors;
  for (const [side, c, a0, a1, cols] of [['s', b.z1, b.x0, b.x1, colsL], ['n', b.z0, b.x0, b.x1, colsL], ['e', b.x1, b.z0, b.z1, colsS], ['w', b.x0, b.z0, b.z1, colsS]]) {
    for (const w of cells(a0, a1, cols, y0, floors, fh)) if (w.f > 0 || ground.includes(side)) frame(P, M, side, c, w, M.steel, 0.05, 0.07);
    for (let f = 1; f < floors; f++) band(P, M, side, c, a0, a1, y0 + f * fh);
    if (detail > 1) for (const a of [a0 + 0.3, a1 - 0.3]) { const [x, z] = onFace(side, c, a, 0.08); P.add(M.pipe, cylG(0.055, 0.055, h - 0.2, x, 0, z, 8)); }
  }
  P.flush(target);
}
