// Roofs and small facade parts for town-buildings.js: the apartments' 옥탑 (elevator machine room with the brand
// mark, stair houses with steel doors, an FRP panel water tank on a steel base, antenna, lightning rods, vent
// pipes), rooftop stair houses of the low buildings, the 한결물류 office's mullion fins and rooftop 실외기, and
// sills and 방범창 on the villa faces along the streets. What is built follows detailOf (town-buildings-detail.js).
import * as THREE from 'three';
import { CURB } from './town-plan.js';
import { box, cylG, quad, parts, barsMat, acMat, acBox } from './town-buildings-detail.js';

// FRP water tank panels: 1 m squares, darker bolted seams, a slight bulge per panel (tile = 2 m).
const frpMat = (kit) => kit.memo('town:frp', () => new THREE.MeshStandardMaterial({ roughness: 0.45, map: kit.texture(kit.canvas(256, 256, (g, w) => {
  g.fillStyle = '#5f87b0'; g.fillRect(0, 0, w, w);
  for (const x of [0, 128]) for (const y of [0, 128]) { const gr = g.createRadialGradient(x + 64, y + 64, 4, x + 64, y + 64, 80); gr.addColorStop(0, 'rgba(255,255,255,0.12)'); gr.addColorStop(1, 'rgba(0,0,0,0.06)'); g.fillStyle = gr; g.fillRect(x, y, 128, 128); }
  g.fillStyle = '#4a6c8f'; for (const k of [0, 127]) { g.fillRect(k, 0, 3, w); g.fillRect(0, k, w, 3); }
  g.fillStyle = '#3d5a78'; for (const k of [1, 128]) for (let t = 8; t < w; t += 16) { g.fillRect(k, t, 2, 2); g.fillRect(t, k, 2, 2); }
  g.fillStyle = 'rgba(60,48,30,0.12)'; g.fillRect(0, w - 20, w, 20); // dirt splashed up from the roof
})) }));

// Steel door (with frame and a small canopy and lamp box above it) on a wall whose outward normal is ry.
function door(P, M, x, y, z, ry) {
  const s = Math.sin(ry), c = Math.cos(ry), at = (out, dy, g) => g.translate(x + s * out, y + dy, z + c * out);
  P.add(M.steel, at(0.03, 0, box(0.95, 2.05, 0.05, 0, 0, 0, { ry, drop: 'nz ny' })), false);
  P.add(M.dark, at(0.02, 0, box(1.07, 2.12, 0.04, 0, 0, 0, { ry, drop: 'nz ny' })), false);
  P.add(M.roof, at(0.3, 2.2, box(1.4, 0.08, 0.6, 0, 0, 0, { ry, uvm: 3, drop: 'nz' })));
  P.add(M.wall, at(0.06, 2.3, box(0.22, 0.12, 0.1, 0, 0, 0, { ry }))); // lamp housing
}
function louvre(P, M, x, y, z, ry) {
  const s = Math.sin(ry), c = Math.cos(ry);
  P.add(M.steel, box(1.2, 0.8, 0.05, x + s * 0.03, y, z + c * 0.03, { ry, drop: 'nz' }), false);
  for (let i = 0; i < 5; i++) P.add(M.dark, box(1.1, 0.035, 0.09, x + s * 0.07, y + 0.1 + i * 0.14, z + c * 0.07, { ry, drop: 'nz ny px nx' }), false);
}
function ladder(P, M, x, y, z, ry, hgt) {
  const s = Math.sin(ry), c = Math.cos(ry), lx = c, lz = -s; // along the wall
  for (const k of [-0.22, 0.22]) P.add(M.steel, box(0.04, hgt, 0.04, x + lx * k + s * 0.18, y, z + lz * k + c * 0.18), false);
  for (let t = 0.3; t < hgt; t += 0.3) P.add(M.steel, box(0.44, 0.03, 0.03, x + s * 0.18, y + t, z + c * 0.18, { ry }), false);
}
// Few materials on purpose: each one is a draw call for every roof together (thin parts cast no shadow).
const mats = (kit, roofMat, parapetMat) => ({ steel: kit.mat('steel'), dark: kit.mat('darkMetal'), roof: roofMat, wall: parapetMat });

// The apartment roof: machine room over the entrance core, its stair house, a second stair house, the tank.
export function apartmentRoof(kit, { b, h, core, target, roofMat, parapetMat, gableMat, facePlane, detail }) {
  const P = parts(), M = mats(kit, roofMat, parapetMat), mid = (b.x0 + b.x1) / 2, zc = (b.z0 + b.z1) / 2;
  const mz = b.z0 + 2.9, xs = core < mid ? b.x1 - 10 : b.x0 + 10, tx = (core + xs) / 2, tz = zc + 1.5, side = xs > core ? 1 : -1;
  P.add(M.wall, box(5.2, 3.3, 5.4, core, h, mz)); P.add(M.roof, box(5.6, 0.2, 5.8, core, h + 3.3, mz, { uvm: 3 }));
  const sx = core + side * 4.1; // the stair house leaning on the machine room, door to the south
  P.add(M.wall, box(3.0, 2.7, 3.4, sx, h, b.z0 + 3.9)); P.add(M.roof, box(3.4, 0.15, 3.8, sx, h + 2.7, b.z0 + 3.9, { uvm: 3 }));
  P.add(M.wall, box(3.2, 2.7, 3.6, xs, h, zc + 1)); P.add(M.roof, box(3.6, 0.15, 4.0, xs, h + 2.7, zc + 1, { uvm: 3 }));
  P.add(frpMat(kit), box(4.4, 2.4, 2.8, tx, h + 0.3, tz, { uvm: 2 }));
  for (const dx of [-1.6, 0, 1.6]) P.add(M.steel, box(0.2, 0.3, 3.0, tx + dx, h, tz));
  // brand mark on the machine room, cut from the gable texture (green rule, 한빛 leaf and name)
  target.add(facePlane('n', b.z0 + 0.15, core - 1.45, core + 1.45, h + 0.95, h + 3.15, gableMat, { u0: 0.15, u1: 0.85, v0: 0.8, v1: 0.98 }));
  if (detail) {
    door(P, M, sx, h, b.z0 + 5.6, 0); door(P, M, xs, h, zc + 2.8, 0);
    louvre(P, M, core + side * -2.6, h + 1.6, mz, side * -Math.PI / 2); louvre(P, M, core, h + 1.6, mz + 2.7, 0);
  }
  if (detail > 1) {
    ladder(P, M, core - side * 1.2, h, mz + 2.7, 0, 3.4); ladder(P, M, tx + 2.2, h + 0.3, tz, Math.PI / 2, 2.5);
    P.add(M.steel, box(0.7, 0.12, 0.7, tx - 1, h + 2.7, tz)); // tank hatch
    const ax = xs + side * 3.5, az = b.z0 + 2.2; // TV antenna mast, three Yagi arms, a dish
    P.add(M.steel, cylG(0.035, 0.045, 6, ax, h, az, 6), false); P.add(M.steel, box(0.5, 0.3, 0.5, ax, h, az));
    for (const [y, L] of [[h + 4.4, 1.6], [h + 5.1, 1.3], [h + 5.7, 1.0]]) {
      P.add(M.steel, box(L, 0.025, 0.025, ax, y, az), false);
      for (let t = -L / 2 + 0.1; t < L / 2; t += 0.22) P.add(M.steel, box(0.015, 0.015, 0.5 - Math.abs(t) * 0.3, ax + t, y, az), false);
    }
    const dish = new THREE.CylinderGeometry(0.38, 0.3, 0.06, 14).toNonIndexed().rotateX(Math.PI / 2 - 0.5).translate(ax + 0.4 * side, h + 2.6, az + 0.25);
    P.add(M.wall, dish);
    for (const [x, z] of [[b.x0 + 0.4, b.z0 + 0.4], [b.x1 - 0.4, b.z0 + 0.4], [b.x0 + 0.4, b.z1 - 0.4], [b.x1 - 0.4, b.z1 - 0.4]]) P.add(M.steel, cylG(0.015, 0.02, 1.6, x, h + 0.9, z, 5), false);
    const vr = kit.rng(b.x0 + b.z0 * 3);
    for (let i = 0; i < 6; i++) { const x = b.x0 + 5 + vr() * (b.x1 - b.x0 - 10), z = b.z0 + 6.5 + vr() * 4; if (Math.abs(x - tx) < 3 || Math.abs(x - xs) < 2.5) continue; P.add(M.wall, cylG(0.07, 0.07, 0.5, x, h, z, 8)); P.add(M.steel, box(0.24, 0.06, 0.24, x, h + 0.5, z)); }
  }
  P.flush(target);
}

// Rooftop clutter for low buildings: stair house (door, louvre, lamp), stainless tanks, green urethane coat.
// rr is the caller's random stream: it is read exactly three times here, as before, so the blocks around keep theirs.
export function roofKit(kit, target, { x0, x1, z0, z1, h, rr, green, detail, roofMat, parapetMat, greenRoof }) {
  const P = parts(), M = mats(kit, roofMat, parapetMat), metal = kit.mat('metal');
  const cx = x0 + (x1 - x0) * (0.25 + rr() * 0.5), cz = z0 + (z1 - z0) * (0.25 + rr() * 0.5);
  P.add(M.wall, box(3, 2.6, 3.2, cx, h, cz)); P.add(M.roof, box(3.5, 0.15, 3.7, cx, h + 2.6, cz, { uvm: 3 }));
  for (let i = 0; i < 1 + (rr() * 2 | 0); i++) { P.add(metal, cylG(0.55, 0.55, 1.3, cx + 2.4 + i * 1.3, h + 0.3, cz + 1, 16)); P.add(metal, new THREE.CircleGeometry(0.55, 16).toNonIndexed().rotateX(-Math.PI / 2).translate(cx + 2.4 + i * 1.3, h + 1.6, cz + 1)); P.add(M.steel, box(1.2, 0.3, 1.2, cx + 2.4 + i * 1.3, h, cz + 1)); }
  if (green) P.add(greenRoof, new THREE.PlaneGeometry(x1 - x0 - 0.4, z1 - z0 - 0.4).toNonIndexed().rotateX(-Math.PI / 2).translate((x0 + x1) / 2, h + 0.02, (z0 + z1) / 2), false);
  if (detail) { door(P, M, cx - 1.5, h, cz, -Math.PI / 2); louvre(P, M, cx, h + 1.6, cz + 1.6, 0); }
  if (detail > 1) { const q = kit.rng((cx * 7 + cz) | 0); for (let i = 0; i < 2; i++) { const x = cx - 2.5 - q() * 2, z = cz - 1.5 + q() * 3; P.add(M.wall, cylG(0.06, 0.06, 0.45, x, h, z, 8)); P.add(M.steel, box(0.2, 0.05, 0.2, x, h + 0.45, z)); } }
  P.flush(target);
}

// 한결물류 office: aluminium fins on the window grid from the 2nd floor up, a row of rooftop 실외기 on a frame.
export function officeDetail(kit, target, { b, h, colsL, colsS, fh, detail }) {
  if (!detail) return;
  const P = parts(), metal = kit.mat('metal'), steel = kit.mat('steel'), ys = CURB + fh, H = h - ys;
  for (let i = 1; i < colsL; i++) { const x = b.x0 + (b.x1 - b.x0) * i / colsL; P.add(metal, box(0.12, H, 0.15, x, ys, b.z1 + 0.075, { drop: 'nz' })); P.add(metal, box(0.12, H, 0.15, x, ys, b.z0 - 0.075, { drop: 'pz' })); }
  for (let i = 1; i < colsS; i++) { const z = b.z0 + (b.z1 - b.z0) * i / colsS; P.add(metal, box(0.15, H, 0.12, b.x1 + 0.075, ys, z, { drop: 'nx' })); P.add(metal, box(0.15, H, 0.12, b.x0 - 0.075, ys, z, { drop: 'px' })); }
  P.add(steel, box(6 * 1.45 + 0.4, 0.25, 1.0, b.x0 + 4 + 2.5 * 1.45, h, b.z0 + 2.6));
  for (let i = 0; i < 6; i++) P.add(acMat(kit), acBox(1.3, 1.6, 0.75, b.x0 + 4 + i * 1.45, h + 0.25, b.z0 + 2.6));
  P.flush(target);
}

// Sills and 방범창 on one villa face (kit.facade 'villa': 4 window columns per texture repeat, rows of fhv).
const RY = { s: 0, n: Math.PI, e: Math.PI / 2, w: -Math.PI / 2 };
export function villaFace(kit, target, { side, c, a0, a1, y0, floors, fhv, reps, seed }) {
  const P = parts(), sill = kit.mat('curb'), bars = barsMat(kit), q = kit.rng(seed), ry = RY[side];
  const out = side === 's' || side === 'e' ? 1 : -1, cw = (a1 - a0) / (reps * 4), ww = cw * 0.5, wh = fhv * 0.45;
  const at = (a, o) => side === 's' || side === 'n' ? [a, c + out * o] : [c + out * o, a];
  for (let i = 0; i < reps * 4; i++) for (let f = 0; f < floors; f++) {
    const a = a0 + (i + 0.5) * cw, yb = y0 + f * fhv + fhv * 0.3;
    const [x, z] = at(a, 0.07); P.add(sill, box(ww + 0.16, 0.06, 0.14, x, yb - 0.11, z, { ry, drop: 'nz' }), false);
    if (f <= 1 || q() < 0.25) { const [gx, gz] = at(a, 0.08); P.add(bars, quad(ww + 0.1, wh + 0.1, gx, yb - 0.05, gz, ry, 2.52), false); }
  }
  P.flush(target);
}
