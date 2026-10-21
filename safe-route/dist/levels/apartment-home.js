// 302호, 정민's flat (103동 3F, 복도식): 현관 with tiles, the step up, 신발장 and the 분전반 (두꺼비집); a hall with the
// 월패드 and boiler thermostat; an open kitchen (sink under the corridor window, fridge, built-in hob with the yellow gas
// pipe and 중간밸브, cabinets, dining table, recycling bins); the living room (sofa, low table, wall TV, stand air
// conditioner, curtains); a small bedroom (bed, wardrobe, desk with a laptop) and the south 베란다 (washing machine,
// drying poles, 경량칸막이). Stateful props: apartment-props.js (radio, candle, bag, planner), apartment-stock.js.
// Unit box x [-7.6, 0.3] (east wall against the lift shaft), z [0, 8.0]; balcony z [8.0, 9.4].
import * as THREE from 'three';
import { F3, FL } from './apartment-parts.js';
import { FONT } from '../engine/kit.js';

export const HOME = { x0: -7.6, x1: 0.3, zb: 8.0, zo: 9.4, ch: FL + 2.3, bf: FL - 0.08, top: FL + 0.42 };

export function buildHome(B, g, H) {
  const { kit, M } = B, { x0: X0, x1: X1, zb: ZB, zo: ZO, ch: CH, bf: BF } = HOME;
  const P = (hex, roughness = 0.6, metalness = 0) => kit.mat('paint:' + hex, { roughness, metalness });
  const box = (w, h, d, m, x, y, z, o = {}) => { const b = kit.box(w, h, d, m, { x, y, z, ...o }); g.add(b); return b; };
  const rbox = (w, h, d, r, m, x, y, z, o = {}) => { const b = kit.rbox(w, h, d, r, m, { x, y, z, ...o }); g.add(b); return b; };
  const cyl = (rt, rb, h, m, x, y, z, o = {}, seg = 16) => { const c = kit.cyl(rt, rb, h, m, { x, y, z, ...o }, seg); g.add(c); return c; };
  // shared power-switched glow materials (one draw call each after merging)
  const GLOW = { lamp: ['#fff3e4', 2.2], shade: ['#ffd8a4', 0.75], red: ['#ff3a22', 1.5], amber: ['#ffa64a', 1.25], green: ['#3cff8a', 1.3], cyan: ['#8fd8ff', 1.1] }, glows = {};
  const glow = (kind) => (glows[kind] ||= B.onPower(B.glowMat(...GLOW[kind])));
  const W = {
    cabinet: P('#f2f0eb', 0.45), counter: P('#d9d4ca', 0.22), gap: P('#9a968f', 0.8), door: P('#efece6', 0.5),
    fridge: P('#c8cbce', 0.32, 0.55), hob: P('#0d0e10', 0.08, 0.3), sofa: P('#8e8a82', 0.97), cushion: P('#9b978e', 0.97),
    pillow: P('#c9b79a', 0.95), rug: P('#b5aa98', 1), duvet: P('#a9b4bf', 0.95), linen: P('#d9d0bf', 1), skirt: P('#e6e2da', 0.5),
    screen: P('#07090b', 0.12, 0.2), paper: P('#f4f1e8', 0.9), plate: P('#f6f4ef', 0.25), green: kit.mat('foliage'),
    pot: P('#b9a48a', 0.8), tile: kit.surface('tile', { color: 0xf1efe9, roughness: 0.18, repeat: [1 / 0.6, 1 / 0.6] }),
  };

  // ---- floors, ceiling ----
  g.add(kit.slab(2.7, 0.2, 1.3, M.entryTile, { x: -1.05, y: F3 - 0.2, z: 0.65 }));
  g.add(kit.slab(5.2, 0.35, 8.0, M.wood, { x: -5.0, y: F3 - 0.2, z: 4.0 }));
  g.add(kit.slab(2.7, 0.35, 6.7, M.wood, { x: -1.05, y: F3 - 0.2, z: 4.65 }));
  g.add(kit.slab(7.9, 0.3, 1.4, M.balconyTile, { x: -3.65, y: BF - 0.3, z: 8.7 }));
  g.add(kit.slab(7.9, 0.35, 9.4, M.ceiling, { x: -3.65, y: CH, z: 4.7 }));
  box(1.3, 0.152, 0.06, M.stone, -1.75, F3, 1.3); // 현관 디딤판 edge
  B.floor(-2.4, X1, 0, 1.3, F3); B.floor(X0, -2.4, 0, ZB, FL); B.floor(-2.4, X1, 1.3, ZB, FL); B.floor(X0, X1, ZB, ZO, BF);

  // ---- walls: corridor-side lining, party walls, room walls, the balcony line and the outer balcony windows ----
  const hDoor = { a: -1.75, b: -0.8, y0: F3, y1: F3 + 2.1 }, hKitchen = { a: -6.2, b: -4.9, y0: F3 + 1.1, y1: F3 + 1.9 };
  B.wallZ(g, 0, X0, X1, F3, CH, M.wallpaper, { t: 0.02, side: 1, holes: [hDoor, hKitchen], solid: false });
  B.wallX(g, X1, 0, ZO, F3 - 0.2, CH, M.wallpaper, { side: 1 });
  B.wallX(g, X0, 0, ZO, F3 - 0.2, CH, M.wallpaper, { side: -1 });
  B.wallZ(g, 3.6, X0, -4.0, FL, CH, M.wallpaper, { side: 1 });
  B.wallX(g, -4.0, 3.6, ZB, FL, CH, M.wallpaper, { side: -1, holes: [{ a: 4.0, b: 4.85, y0: FL, y1: FL + 2.05, door: true }] });
  B.wallX(g, -2.4, 0, 2.6, F3, CH, M.wallpaper, { side: -1 });
  B.wallX(g, -1.1, 1.3, 3.6, FL, CH, M.wallpaper, { side: 1, holes: [{ a: 2.2, b: 2.95, y0: FL, y1: FL + 2.05 }] });
  B.wallZ(g, 1.3, -1.1, X1, F3, CH, M.wallpaper, { side: 1 });
  B.wallZ(g, 3.6, -1.1, X1, FL, CH, M.wallpaper, { side: -1 });
  B.wallZ(g, ZB, X0, X1, F3, CH, M.wallpaper, { side: -1, holes: [{ a: -7.0, b: -4.6, y0: FL + 0.8, y1: FL + 2.1 }, { a: -3.75, b: -1.0, y0: FL, y1: FL + 2.2 }, { a: -1.0, b: -0.1, y0: FL, y1: FL + 2.2, door: true }] });
  B.window(g, 'z', ZB - 0.05, -7.0, -4.6, FL + 0.8, FL + 2.1, { mat: M.frame, split: 2 });
  const panel = (a, b, z) => { // one sliding glass leaf of the living-room door
    const w = b - a, h = 2.16;
    box(w, 0.05, 0.045, M.frame, (a + b) / 2, FL + 0.01, z); box(w, 0.05, 0.045, M.frame, (a + b) / 2, FL + h - 0.04, z);
    box(0.05, h, 0.045, M.frame, a + 0.025, FL + 0.01, z); box(0.05, h, 0.045, M.frame, b - 0.025, FL + 0.01, z);
    box(w - 0.1, h - 0.1, 0.01, M.glass, (a + b) / 2, FL + 0.06, z, { cast: false });
  };
  panel(-3.75, -2.8, ZB - 0.08); panel(-2.85, -1.9, ZB - 0.025); panel(-1.95, -1.0, ZB - 0.08); panel(-1.95, -1.0, ZB - 0.025);
  box(2.75, 0.02, 0.12, M.alu, -2.375, FL, ZB - 0.05, { cast: false }); box(0.9, 0.012, 0.12, M.alu, -0.55, FL, ZB - 0.05, { cast: false });
  B.wallZ(g, ZO, X0, X1, BF - 0.3, CH, M.exterior, { t: 0.18, side: 1, holes: [{ a: X0 + 0.08, b: X1 - 0.08, y0: BF + 0.5, y1: CH - 0.12 }] });
  B.window(g, 'z', ZO + 0.06, X0 + 0.08, X1 - 0.08, BF + 0.5, CH - 0.12, { mat: M.frame, split: 4, depth: 0.08 });
  box(7.74, 0.03, 0.24, M.stone, -3.65, BF + 0.47, ZO + 0.09);
  for (let x = X0 + 0.15; x <= X1 - 0.1; x += 1.29) box(0.025, 0.6, 0.025, M.alu, x, BF + 0.5, ZO + 0.27);
  B.rod(g, [X0 + 0.12, BF + 1.1, ZO + 0.27], [X1 - 0.12, BF + 1.1, ZO + 0.27], 0.02, M.alu);
  // skirting boards: [ax, az, bx, bz, s] along a wall face, s = which side of the face the room is on
  const skirts = [[X0, 3.7, -4.1, 3.7, 1], [X0, 7.9, -4.1, 7.9, -1], [-4.0, 4.85, -4.0, 7.9, 1], [X1, 3.6, X1, 5.0, -1], [X1, 7.0, X1, 7.38, -1], [-2.4, 1.3, -2.4, 2.6, 1], [X0, 0.65, X0, 3.6, 1], [-1.1, 3.6, X1, 3.6, 1]];
  for (const [ax, az, bx, bz, s] of skirts) { const alongX = az === bz;
    box(alongX ? bx - ax : 0.012, 0.07, alongX ? 0.012 : bz - az, W.skirt, alongX ? (ax + bx) / 2 : ax + s * 0.006, FL, alongX ? az + s * 0.006 : (az + bz) / 2, { cast: false }); }

  // ---- kitchen: base run with sink + hob, upper cabinets, fridge, gas pipe and 중간밸브, dining table, bins ----
  const KX0 = -7.5, KX1 = -3.5, CT = FL + 0.85;
  box(KX1 - KX0, 0.1, 0.5, P('#3b3936', 0.7), (KX0 + KX1) / 2, FL, 0.28);
  for (const [a, b, top] of [[KX0, -5.95, 0.82], [-5.95, -5.15, 0.6], [-5.15, KX1, 0.82]]) box(b - a, top - 0.1, 0.6, W.cabinet, (a + b) / 2, FL + 0.1, 0.32);
  box(-5.92 - KX0 + 0.01, 0.03, 0.64, W.counter, (KX0 - 0.01 - 5.92) / 2, CT - 0.03, 0.33);
  box(KX1 + 0.01 + 5.18, 0.03, 0.64, W.counter, (-5.18 + KX1 + 0.01) / 2, CT - 0.03, 0.33);
  box(0.74, 0.03, 0.09, W.counter, -5.55, CT - 0.03, 0.055); box(0.74, 0.03, 0.1, W.counter, -5.55, CT - 0.03, 0.6);
  box(0.74, 0.2, 0.45, kit.mat('metal', { color: 0xd4d8db, roughness: 0.28, side: THREE.BackSide }), -5.55, CT - 0.2, 0.325, { cast: false }); // basin
  for (let x = KX0 + 0.5; x < KX1 - 0.05; x += 0.5) box(0.005, 0.68, 0.004, W.gap, x, FL + 0.12, 0.622, { cast: false });
  for (let x = KX0 + 0.25; x < KX1; x += 0.5) box(0.11, 0.012, 0.02, M.metal, x, FL + 0.74, 0.635);
  cyl(0.024, 0.03, 0.04, M.metal, -5.55, CT, 0.1);
  B.rod(g, [-5.55, CT + 0.03, 0.1], [-5.55, CT + 0.33, 0.1], 0.012, M.metal); B.rod(g, [-5.55, CT + 0.33, 0.1], [-5.55, CT + 0.33, 0.29], 0.012, M.metal);
  B.rod(g, [-5.55, CT + 0.33, 0.29], [-5.55, CT + 0.25, 0.31], 0.011, M.metal); box(0.02, 0.012, 0.1, M.metal, -5.47, CT + 0.07, 0.15);
  box(0.42, 0.015, 0.3, M.metal, -4.82, CT, 0.3); // dish rack + plates
  for (let i = 0; i < 4; i++) cyl(0.1, 0.1, 0.01, W.plate, -4.95 + i * 0.05, CT + 0.11, 0.3, { rz: Math.PI / 2 }, 20);
  cyl(0.035, 0.03, 0.09, P('#4f7fa8', 0.3), -4.67, CT + 0.015, 0.22);
  box(0.62, 0.008, 0.48, W.hob, -4.07, CT, 0.32, { cast: false }); // built-in hob
  for (const [dx, dz, r] of [[-0.16, 0.1, 0.085], [0.16, 0.1, 0.07], [0, -0.12, 0.06]]) {
    cyl(r, r, 0.012, M.darkMetal, -4.07 + dx, CT + 0.008, 0.32 + dz, {}, 20); cyl(r * 0.4, r * 0.4, 0.022, M.metal, -4.07 + dx, CT + 0.008, 0.32 + dz);
    box(r * 2.4, 0.014, 0.014, M.black, -4.07 + dx, CT + 0.02, 0.32 + dz); box(0.014, 0.014, r * 2.4, M.black, -4.07 + dx, CT + 0.02, 0.32 + dz);
  }
  for (let i = 0; i < 3; i++) cyl(0.017, 0.017, 0.024, M.black, -4.27 + i * 0.2, FL + 0.7, 0.64, { rx: Math.PI / 2 });
  cyl(0.1, 0.092, 0.12, M.metal, -4.23, CT + 0.035, 0.42, {}, 20); cyl(0.102, 0.102, 0.012, M.metal, -4.23, CT + 0.155, 0.42, {}, 20); // pot on the burner
  B.rod(g, [-3.62, CH, 0.05], [-3.62, FL + 0.95, 0.05], 0.013, M.yellowPipe); B.rod(g, [-3.62, FL + 0.95, 0.05], [-3.86, FL + 0.95, 0.05], 0.013, M.yellowPipe);
  cyl(0.022, 0.022, 0.07, M.metal, -3.62, FL + 1.09, 0.05); box(0.018, 0.1, 0.02, P('#e0b21c', 0.4), -3.62, FL + 1.08, 0.09);
  B.plate(g, '가스', { x: -3.62, y: FL + 1.27, z: 0.07, w: 0.07, h: 0.035, bg: '#e7b400', fg: '#3a2a00' });
  for (const [a, b, y, h] of [[KX0, -6.3, FL + 1.45, 0.75], [-4.8, -3.7, FL + 1.45, 0.75], [-6.3, -4.8, FL + 1.85, 0.35]]) {
    box(b - a, h, 0.34, W.cabinet, (a + b) / 2, y, 0.19);
    for (let x = a + 0.4; x < b - 0.1; x += 0.4) box(0.005, h - 0.02, 0.004, W.gap, x, y + 0.01, 0.362, { cast: false });
  }
  box(0.66, 0.1, 0.46, M.metal, -4.07, FL + 1.35, 0.25); box(0.22, 0.005, 0.06, glow('lamp'), -4.07, FL + 1.345, 0.42, { cast: false });
  B.wallZ(g, 0.02, KX0, KX1, CT, FL + 1.45, W.tile, { t: 0.012, side: 1, holes: [hKitchen], solid: false });
  box(1.3, 0.03, 0.12, M.stone, -5.55, F3 + 1.07, 0.08);
  cyl(0.13, 0.12, 0.24, M.white, -6.9, CT, 0.32, {}, 20); box(0.06, 0.025, 0.005, glow('amber'), -6.9, CT + 0.15, 0.452, { cast: false }); // rice cooker
  cyl(0.075, 0.085, 0.22, M.steel, -6.45, CT, 0.3, {}, 18); box(0.3, 0.012, 0.22, M.lightWood, -5.0, CT, 0.48, { ry: 0.12 }); // kettle, board
  B.solid(X0, KX1, 0, 0.66, FL, FL + 0.9);
  rbox(0.85, 1.82, 0.72, 0.03, W.fridge, -2.925, FL, 0.4);
  box(0.004, 1.2, 0.01, W.gap, -2.925, FL + 0.6, 0.762, { cast: false }); box(0.84, 0.004, 0.01, W.gap, -2.925, FL + 0.6, 0.762, { cast: false });
  for (const x of [-2.96, -2.89]) B.rod(g, [x, FL + 0.95, 0.79], [x, FL + 1.45, 0.79], 0.01, M.metal);
  B.rod(g, [-3.15, FL + 0.48, 0.79], [-2.7, FL + 0.48, 0.79], 0.01, M.metal);
  box(0.07, 0.1, 0.005, glow('cyan'), -2.72, FL + 1.3, 0.763, { cast: false }); box(0.1, 0.14, 0.003, W.paper, -3.18, FL + 1.2, 0.763, { rz: 0.06 });
  B.solid(-3.36, -2.49, 0, 0.8, FL, FL + 1.85);
  box(1.1, 0.035, 0.72, M.lightWood, -5.6, FL + 0.7, 2.2);
  for (const [dx, dz] of [[-0.5, -0.31], [0.5, -0.31], [-0.5, 0.31], [0.5, 0.31]]) box(0.04, 0.7, 0.04, M.lightWood, -5.6 + dx, FL, 2.2 + dz);
  const chair = (x, z, ry) => { const c = new THREE.Group(); c.position.set(x, FL, z); c.rotation.y = ry; g.add(c);
    c.add(kit.box(0.42, 0.03, 0.42, M.lightWood, { y: 0.44 }), kit.box(0.4, 0.36, 0.025, M.lightWood, { y: 0.5, z: 0.2 }));
    for (const [dx, dz] of [[-0.18, -0.18], [0.18, -0.18], [-0.18, 0.18], [0.18, 0.18]]) c.add(kit.box(0.03, 0.44, 0.03, M.lightWood, { x: dx, z: dz })); };
  chair(-5.85, 1.55, Math.PI); chair(-5.35, 2.95, 0.35);
  cyl(0.13, 0.08, 0.07, W.plate, -5.75, FL + 0.735, 2.15, {}, 20);
  for (const [dx, dz, c] of [[-0.03, 0, '#c0392b'], [0.04, 0.03, '#d35400'], [0, -0.05, '#c0392b']]) { const s = new THREE.Mesh(new THREE.SphereGeometry(0.035, 12, 8), P(c, 0.5)); s.position.set(-5.75 + dx, FL + 0.8, 2.15 + dz); s.castShadow = true; g.add(s); }
  box(0.24, 0.012, 0.18, W.paper, -5.3, FL + 0.735, 2.3, { ry: -0.2 }); box(0.22, 0.012, 0.16, P('#e9e1cf', 0.9), -5.31, FL + 0.747, 2.29, { ry: 0.1 });
  B.solid(-6.16, -5.04, 1.83, 2.57, FL, FL + 0.75);
  [['#3d78c2', '플라스틱'], ['#8d939a', '캔·병'], ['#c49a52', '종이']].forEach(([c, label], i) => {
    box(0.32, 0.46, 0.34, P(c, 0.5), -7.38, FL, 2.4 + i * 0.36);
    B.plate(g, label, { x: -7.21, y: FL + 0.36, z: 2.4 + i * 0.36, ry: Math.PI / 2, w: 0.2, h: 0.07, bg: '#ffffff', fg: '#333333' });
  });
  B.solid(X0, -7.2, 2.2, 3.4, FL, FL + 0.5);
  cyl(0.22, 0.22, 0.035, glow('lamp'), -5.5, CH - 0.035, 1.8, { cast: false }, 24);
  B.fixtures.push({ x: -5.5, y: CH - 0.3, z: 1.8, color: 0xfff0dc, intensity: 3.5, distance: 8 });

  // ---- 현관: 신발장, mirror, shoes, umbrellas, 분전반 (두꺼비집), the inside of the door, sensor light ----
  box(0.38, 1.05, 1.2, W.cabinet, 0.11, F3, 0.66);
  for (const z of [0.36, 0.66, 0.96]) { box(0.004, 1.0, 0.004, W.gap, -0.082, F3 + 0.03, z, { cast: false }); box(0.02, 0.1, 0.012, M.metal, -0.09, F3 + 0.75, z - 0.06); }
  box(0.01, 0.8, 0.6, M.mirror, 0.294, F3 + 1.3, 0.66, { cast: false });
  box(0.2, 0.015, 0.12, M.darkWood, 0.1, F3 + 1.05, 0.4); cyl(0.018, 0.018, 0.16, M.black, 0.05, F3 + 1.07, 0.95, { rz: Math.PI / 2 }); // key tray, flashlight
  B.solid(-0.1, X1, 0.04, 1.28, F3, F3 + 1.1);
  const shoes = [[-1.05, 0.95, 0.2], [-0.93, 0.95, -0.1], [-0.6, 0.35, 1.4], [-0.48, 0.38, 1.6], [-1.9, 1.62, 0], [-1.75, 1.62, 0.1]];
  const sh = kit.instanced(new THREE.BoxGeometry(0.1, 0.07, 0.26), P('#ffffff', 0.7), shoes.map(([x, z, ry], i) => ({ x, y: (i >= 4 ? FL : F3) + 0.035, z, ry })), { cast: false });
  shoes.forEach((_, i) => sh.setColorAt(i, new THREE.Color(['#e9e6df', '#e9e6df', '#2a2a2a', '#2a2a2a', '#8a6d55', '#8a6d55'][i]))); g.add(sh);
  cyl(0.1, 0.09, 0.42, M.steel, -2.22, F3, 0.3, {}, 18);
  for (const [dx, c] of [[-0.03, '#1f2d45'], [0.035, '#7a1f2b']]) { B.rod(g, [-2.22 + dx, F3 + 0.1, 0.3], [-2.2 + dx, F3 + 0.92, 0.32], 0.012, M.black); cyl(0.035, 0.012, 0.5, P(c, 0.8), -2.22 + dx, F3 + 0.2, 0.3); }
  const brk = B.tex('breaker', 256, 320, drawBreaker);
  box(0.36, 0.46, 0.08, P('#d9d4c8', 0.5, 0.3), -2.1, F3 + 1.72, 0.06);
  const face = new THREE.Mesh(new THREE.PlaneGeometry(0.34, 0.44), new THREE.MeshStandardMaterial({ map: brk, roughness: 0.45, metalness: 0.2 }));
  face.position.set(-2.1, F3 + 1.95, 0.1012); g.add(face);
  if (H.door302) { // inner face of the 302 door: lock body, door closer, latch guard
    const pv = H.door302.pivot;
    pv.add(kit.box(0.08, 0.28, 0.05, M.black, { x: 0.83, y: 0.88, z: 0.05 }), kit.box(0.3, 0.05, 0.07, M.alu, { x: 0.3, y: 1.98, z: 0.06 }), kit.box(0.04, 0.06, 0.03, M.metal, { x: 0.86, y: 1.45, z: 0.04 }));
  }
  B.sensors.push({ x: -1.25, y: CH - 0.02, z: 0.65, kind: 'sensor', floor: 3 });

  // ---- hall: 월패드, boiler thermostat, switches, downlight; the closed bathroom door ----
  box(0.02, 0.2, 0.28, M.white, -2.39, FL + 1.32, 2.0); box(0.004, 0.14, 0.2, glow('cyan'), -2.378, FL + 1.35, 1.98, { cast: false });
  box(0.02, 0.12, 0.09, M.white, -2.39, FL + 1.34, 2.38); box(0.004, 0.03, 0.05, glow('amber'), -2.378, FL + 1.41, 2.38, { cast: false });
  for (const [x, z, ry] of [[-2.39, 1.55, Math.PI / 2], [-3.99, 5.0, Math.PI / 2], [-2.51, 2.3, Math.PI / 2], [-1.108, 3.2, Math.PI / 2]]) box(0.07, 0.12, 0.012, M.white, x, FL + 1.1, z, { ry });
  cyl(0.06, 0.06, 0.02, glow('lamp'), -1.75, CH - 0.02, 2.5, { cast: false });
  B.door(g, 'x', -1.05, 2.2, 2.95, FL, 2.05, W.door, { hinge: 'b', dir: -1, open: 0, solid: false, frameMat: M.frame });

  // ---- living room: sofa, rug, low table, wall TV on a stand, stand air conditioner, lamp, plant, picture, curtains ----
  for (const [dx, dz] of [[-0.4, 5.0], [0.38, 5.0], [-0.4, 7.0], [0.38, 7.0]]) cyl(0.025, 0.025, 0.06, M.darkWood, -3.5 + dx, FL, dz);
  rbox(0.92, 0.36, 2.2, 0.05, W.sofa, -3.5, FL + 0.06, 6.0); rbox(0.24, 0.5, 2.2, 0.07, W.sofa, -3.84, FL + 0.42, 6.0);
  for (const z of [4.98, 7.02]) rbox(0.92, 0.22, 0.16, 0.06, W.sofa, -3.5, FL + 0.42, z);
  for (const z of [5.47, 6.53]) rbox(0.66, 0.12, 1.02, 0.05, W.cushion, -3.38, FL + 0.42, z);
  rbox(0.13, 0.36, 0.38, 0.06, W.pillow, -3.62, FL + 0.52, 5.3, { rz: -0.28, ry: 0.15 }); rbox(0.13, 0.34, 0.36, 0.06, P('#7c8a86', 0.95), -3.62, FL + 0.52, 6.72, { rz: -0.3, ry: -0.2 });
  rbox(0.5, 0.06, 0.4, 0.03, P('#c7b9a4', 1), -3.35, FL + 0.54, 6.25, { ry: 0.2 });
  B.solid(-4.0, -3.02, 4.88, 7.12, FL, FL + 0.9);
  box(2.2, 0.01, 1.7, W.rug, -1.9, FL, 6.0, { cast: false });
  box(1.0, 0.04, 0.55, M.lightWood, -1.95, FL + 0.38, 6.0);
  for (const [dx, dz] of [[-0.46, -0.24], [0.46, -0.24], [-0.46, 0.24], [0.46, 0.24]]) box(0.04, 0.38, 0.04, M.lightWood, -1.95 + dx, FL, 6.0 + dz);
  box(0.05, 0.016, 0.17, M.black, -1.75, HOME.top, 5.82, { ry: 0.3 }); cyl(0.04, 0.036, 0.09, P('#e8e2d6', 0.3), -1.48, HOME.top, 5.86); box(0.22, 0.008, 0.3, P('#4a6b8a', 0.7), -2.3, HOME.top, 6.15, { ry: -0.15 });
  B.solid(-2.47, -1.43, 5.71, 6.29, FL, FL + 0.45);
  box(0.42, 0.4, 1.9, P('#e8e3da', 0.45), 0.09, FL, 6.0);
  for (const z of [5.37, 6.0, 6.63]) box(0.004, 0.36, 0.004, W.gap, -0.121, FL + 0.02, z, { cast: false });
  box(0.08, 0.06, 0.8, M.darkMetal, 0.0, FL + 0.4, 6.0); box(0.2, 0.04, 0.2, M.black, 0.12, FL + 0.4, 6.75); box(0.004, 0.006, 0.02, glow('green'), 0.019, FL + 0.42, 6.75, { cast: false });
  box(0.05, 0.72, 1.26, M.black, 0.255, FL + 0.8, 6.0); box(0.004, 0.68, 1.22, W.screen, 0.228, FL + 0.82, 6.0, { cast: false });
  box(0.004, 0.008, 0.02, glow('red'), 0.2255, FL + 0.806, 6.45, { cast: false });
  B.solid(-0.13, X1, 5.04, 6.96, FL, FL + 1.55);
  rbox(0.36, 1.75, 0.3, 0.05, M.white, 0.08, FL, 7.55); box(0.004, 0.5, 0.22, M.darkMetal, -0.101, FL + 1.05, 7.55); box(0.004, 0.04, 0.08, glow('cyan'), -0.102, FL + 1.32, 7.55, { cast: false });
  B.solid(-0.12, 0.28, 7.38, 7.72, FL, FL + 1.75);
  cyl(0.14, 0.16, 0.03, M.black, -3.62, FL, 7.5); B.rod(g, [-3.62, FL + 0.03, 7.5], [-3.62, FL + 1.45, 7.5], 0.012, M.black);
  cyl(0.15, 0.2, 0.26, glow('shade'), -3.62, FL + 1.42, 7.5, { cast: false }, 20); B.solid(-3.78, -3.46, 7.34, 7.66, FL, FL + 1.7);
  cyl(0.17, 0.13, 0.34, W.pot, 0.02, FL, 4.62);
  for (const [dx, dy, dz, s] of [[0, 0.6, 0, 0.3], [-0.12, 0.85, 0.05, 0.22], [0.08, 0.8, -0.08, 0.24]]) { const f = new THREE.Mesh(new THREE.IcosahedronGeometry(s, 1), W.green); f.position.set(0.02 + dx, FL + dy, 4.62 + dz); f.castShadow = true; g.add(f); }
  B.solid(-0.2, 0.25, 4.4, 4.86, FL, FL + 1.0);
  box(0.03, 0.58, 0.84, M.darkWood, -3.985, FL + 1.25, 6.0);
  const art = new THREE.Mesh(new THREE.PlaneGeometry(0.76, 0.5), new THREE.MeshStandardMaterial({ map: B.tex('art302', 256, 168, drawArt), roughness: 0.8 }));
  art.position.set(-3.968, FL + 1.54, 6.0); art.rotation.y = Math.PI / 2; g.add(art);
  cyl(0.16, 0.16, 0.03, new THREE.MeshStandardMaterial({ map: B.tex('clock', 256, 256, drawClock), roughness: 0.4 }), X1 - 0.016, FL + 1.85, 4.3, { rz: Math.PI / 2 }, 28);
  const curtain = (a, b, z) => { // pleated panel: alternating tilted strips
    const n = Math.max(4, Math.round((b - a) / 0.09)), w = (b - a) / n;
    for (let i = 0; i < n; i++) box(w * 1.15, CH - FL - 0.08, 0.02, W.linen, a + (i + 0.5) * w, FL + 0.02, z + (i % 2 ? 0.025 : -0.025), { ry: i % 2 ? 0.5 : -0.5 });
  };
  box(4.2, 0.03, 0.05, M.alu, -1.85, CH - 0.04, ZB - 0.17); curtain(-4.0, -3.45, ZB - 0.17); curtain(-0.12, 0.28, ZB - 0.17);
  box(0.62, 0.05, 0.62, M.white, -1.85, CH - 0.05, 5.9); box(0.58, 0.004, 0.58, glow('lamp'), -1.85, CH - 0.054, 5.9, { cast: false });
  B.fixtures.push({ x: -1.85, y: CH - 0.35, z: 5.9, color: 0xfff2e0, intensity: 5, distance: 9 });

  // ---- bedroom: wardrobe, bed, nightstand + lamp, desk with laptop and lamp, office chair, curtain, ceiling light ----
  box(0.6, 2.2, 1.7, W.cabinet, -7.27, FL, 4.6);
  for (const z of [4.17, 4.6, 5.03]) box(0.004, 2.14, 0.004, W.gap, -6.968, FL + 0.03, z, { cast: false });
  for (const z of [4.52, 4.68]) box(0.02, 0.3, 0.015, M.metal, -6.96, FL + 1.0, z);
  B.solid(X0, -6.95, 3.7, 5.47, FL, FL + 2.2);
  box(2.05, 0.28, 1.6, M.lightWood, -6.53, FL, 6.4); rbox(1.98, 0.22, 1.52, 0.06, M.white, -6.53, FL + 0.28, 6.4);
  rbox(1.45, 0.1, 1.58, 0.05, W.duvet, -6.25, FL + 0.47, 6.4, { rz: 0.02 }); rbox(0.5, 0.06, 1.6, 0.03, P('#8c99a6', 0.95), -5.85, FL + 0.55, 6.4);
  for (const z of [6.02, 6.78]) rbox(0.38, 0.12, 0.62, 0.05, M.white, -7.25, FL + 0.5, z, { ry: z > 6.4 ? 0.08 : -0.05 });
  box(0.06, 0.95, 1.62, M.lightWood, -7.54, FL, 6.4);
  B.solid(X0, -5.48, 5.58, 7.22, FL, FL + 0.62);
  box(0.42, 0.48, 0.38, M.lightWood, -7.34, FL, 7.5); cyl(0.06, 0.07, 0.05, M.white, -7.34, FL + 0.48, 7.5);
  cyl(0.1, 0.13, 0.17, glow('shade'), -7.34, FL + 0.66, 7.5, { cast: false }, 18); B.rod(g, [-7.34, FL + 0.53, 7.5], [-7.34, FL + 0.66, 7.5], 0.008, M.metal);
  B.solid(X0, -7.1, 7.3, 7.72, FL, FL + 0.6);
  box(1.3, 0.03, 0.6, M.lightWood, -6.2, FL + 0.7, 4.0);
  for (const dx of [-0.62, 0.62]) box(0.03, 0.7, 0.56, M.lightWood, -6.2 + dx, FL, 4.0);
  box(0.33, 0.014, 0.23, M.alu, -6.2, FL + 0.73, 4.08);
  const lid = new THREE.Group(); lid.position.set(-6.2, FL + 0.744, 3.965); lid.rotation.x = -0.28; g.add(lid);
  lid.add(kit.box(0.33, 0.22, 0.008, M.alu, { z: -0.004 }));
  const scr = new THREE.Mesh(new THREE.PlaneGeometry(0.3, 0.19), B.onPower(new THREE.MeshStandardMaterial({ color: 0x050505, emissive: 0xffffff, emissiveMap: B.tex('laptop', 320, 200, drawLaptop), emissiveIntensity: 1.1, roughness: 0.2 })));
  scr.position.set(0, 0.11, 0.0005); lid.add(scr);
  box(0.21, 0.01, 0.15, P('#e8d7a8', 0.9), -5.8, FL + 0.73, 4.12, { ry: 0.18 }); B.rod(g, [-5.7, FL + 0.742, 4.07], [-5.66, FL + 0.742, 4.2], 0.004, M.black);
  cyl(0.04, 0.036, 0.09, P('#5f7f6a', 0.35), -6.68, FL + 0.73, 4.12);
  cyl(0.07, 0.08, 0.02, M.black, -5.72, FL + 0.73, 3.85); B.rod(g, [-5.72, FL + 0.75, 3.85], [-5.78, FL + 1.12, 3.95], 0.008, M.black); B.rod(g, [-5.78, FL + 1.12, 3.95], [-5.95, FL + 1.06, 4.05], 0.008, M.black);
  cyl(0.035, 0.06, 0.07, glow('lamp'), -5.97, FL + 0.99, 4.06, { cast: false });
  box(0.3, 0.04, 0.06, M.white, -6.7, FL, 3.8); box(0.01, 0.006, 0.01, glow('red'), -6.6, FL + 0.04, 3.83, { cast: false }); // 멀티탭
  B.solid(-6.87, -5.53, 3.7, 4.32, FL, FL + 0.75);
  const ch = new THREE.Group(); ch.position.set(-6.15, FL, 4.78); ch.rotation.y = 0.25; g.add(ch);
  for (let i = 0; i < 5; i++) { const a = i * Math.PI * 0.4; ch.add(kit.box(0.04, 0.03, 0.3, M.black, { x: Math.sin(a) * 0.15, y: 0.04, z: Math.cos(a) * 0.15, ry: a })); }
  ch.add(kit.cyl(0.025, 0.025, 0.36, M.steel, { y: 0.06 }), kit.rbox(0.48, 0.08, 0.46, 0.03, P('#33373d', 0.9), { y: 0.42 }), kit.rbox(0.44, 0.5, 0.06, 0.03, P('#33373d', 0.9), { y: 0.58, z: 0.22, rx: -0.12 }));
  B.solid(-6.42, -5.88, 4.52, 5.04, FL, FL + 0.95);
  box(2.6, 0.03, 0.05, M.alu, -5.8, CH - 0.04, ZB - 0.17); curtain(-7.1, -6.55, ZB - 0.17); curtain(-5.05, -4.5, ZB - 0.17);
  cyl(0.2, 0.2, 0.035, glow('lamp'), -5.8, CH - 0.035, 5.8, { cast: false }, 24);
  B.fixtures.push({ x: -5.8, y: CH - 0.3, z: 5.8, color: 0xfff0dc, intensity: 3.5, distance: 7 });
  B.door(g, 'x', -4.05, 4.0, 4.85, FL, 2.05, W.door, { hinge: 'a', dir: -1, open: 0.85, solid: false, frameMat: M.frame });

  // ---- 베란다: washing machine, laundry, drying poles with clothes, plants, drain, 경량칸막이 panel, bulb ----
  rbox(0.62, 0.86, 0.62, 0.04, M.white, -7.25, BF, 9.03);
  cyl(0.2, 0.2, 0.03, kit.mat('darkGlass'), -6.93, BF + 0.48, 9.03, { rz: Math.PI / 2 }, 24); cyl(0.235, 0.235, 0.02, M.metal, -6.94, BF + 0.48, 9.03, { rz: Math.PI / 2 }, 24);
  box(0.004, 0.07, 0.5, M.black, -6.937, BF + 0.75, 9.03); box(0.004, 0.02, 0.05, glow('green'), -6.942, BF + 0.765, 8.86, { cast: false });
  B.rod(g, [-7.3, BF + 1.2, 9.36], [-7.3, BF + 0.9, 9.3], 0.012, M.metal); box(0.06, 0.06, 0.05, M.metal, -7.3, BF + 1.18, 9.34);
  B.solid(X0, -6.92, 8.7, 9.36, BF, BF + 0.9);
  cyl(0.2, 0.17, 0.34, P('#d8cdb5', 0.9), -6.55, BF, 8.35, {}, 18);
  for (const z of [8.45, 8.95]) { B.rod(g, [-6.3, CH - 0.38, z], [-1.6, CH - 0.38, z], 0.012, M.alu); for (const x of [-6.0, -1.9]) B.rod(g, [x, CH, z], [x, CH - 0.38, z], 0.008, M.alu); }
  [[-5.6, 8.45, '#e8e4dc', 0.5, 0.62], [-4.9, 8.45, '#5d7fa3', 0.55, 0.5], [-4.2, 8.95, '#f2efe6', 0.4, 0.7], [-3.5, 8.45, '#8a3a3a', 0.45, 0.55], [-2.7, 8.95, '#3f4a56', 0.6, 0.62]]
    .forEach(([x, z, c, w, h], i) => { box(w, h, 0.012, P(c, 0.98), x, CH - 0.38 - h, z, { ry: (i % 3 - 1) * 0.08 }); });
  for (const [x, s] of [[-2.2, 0.22], [-1.75, 0.16]]) { cyl(s * 0.75, s * 0.6, s * 1.3, W.pot, x, BF, 9.15); const f = new THREE.Mesh(new THREE.IcosahedronGeometry(s, 1), W.green); f.position.set(x, BF + s * 1.3 + s * 0.6, 9.15); f.castShadow = true; g.add(f); }
  box(0.12, 0.003, 0.12, M.steel, -5.0, BF, 9.2, { cast: false });
  for (const [w, h, x, y, z] of [[0.012, 2.0, X0 + 0.006, BF + 0.05, 8.12], [0.012, 2.0, X0 + 0.006, BF + 0.05, 8.68], [0.012, 0.02, X0 + 0.006, BF + 2.05, 8.4]]) box(w, h, z === 8.4 ? 0.58 : 0.02, P('#9e988c', 0.6), x, y, z, { cast: false });
  B.plate(g, '경량칸막이', { x: X0 + 0.014, y: BF + 1.55, z: 8.4, ry: Math.PI / 2, w: 0.32, h: 0.09, bg: '#fff4d6', fg: '#8a1c1c', border: '#8a1c1c' });
  cyl(0.06, 0.06, 0.03, glow('lamp'), -4.0, CH - 0.03, 8.7, { cast: false });

  // ---- points, spawns, anchors ----
  B.point('homeExitDoor', '현관문 (복도로)', -1.275, F3, 0.5, 'door', 1.3);
  B.point('homeBreaker', '분전반 (두꺼비집)', -2.1, F3, 0.45, 'inspect', 1.3);
  B.point('homeFridge', '냉장고', -2.92, FL, 1.2, 'inspect', 1.4);
  B.point('homeSink', '싱크대', -5.55, FL, 1.0, 'inspect', 1.4);
  B.point('homeGas', '가스 중간밸브', -3.85, FL, 1.0, 'inspect', 1.4);
  B.point('homeDesk', '책상 (노트북)', -6.2, FL, 4.75, 'inspect', 1.5);
  B.point('homeBed', '침대', -6.0, FL, 6.4, 'bed', 1.9);
  B.point('homeWindow', '거실 창밖 보기', -2.4, FL, 7.45, 'window', 1.6);
  B.spawns.home = { x: -1.6, y: FL, z: 4.5, yaw: Math.PI };
  B.spawns.homeEntry = { x: -1.275, y: F3, z: 0.75, yaw: Math.PI };
  B.anchors.homeLiving = { x: -0.2, y: FL + 1.9, z: 4.0, target: { x: -2.6, y: FL + 0.6, z: 7.2 } };
  B.anchors.homeKitchen = { x: -2.8, y: FL + 1.8, z: 3.2, target: { x: -5.4, y: FL + 0.8, z: 0.4 } };
  B.anchors.homeBalcony = { x: -0.7, y: FL + 1.6, z: 8.5, target: { x: -4.0, y: FL + 0.9, z: 9.0 } };
}

// 분전반 face: steel door with a smoked window over the main ELB (yellow test button) and the branch breakers.
function drawBreaker(c, w, h) {
  c.fillStyle = '#d6d1c4'; c.fillRect(0, 0, w, h);
  c.fillStyle = '#2f4e6e'; c.fillRect(0, 0, w, 40); c.fillStyle = '#fff'; c.font = `700 24px ${FONT}`; c.textAlign = 'center'; c.fillText('분전반', w / 2, 29);
  c.fillStyle = '#3c4146'; c.fillRect(22, 60, w - 44, 200);
  c.fillStyle = '#20242a'; c.fillRect(34, 76, 56, 120); c.fillStyle = '#f2c230'; c.fillRect(48, 92, 28, 16); c.fillStyle = '#e8e8e8'; c.fillRect(52, 130, 20, 44);
  for (let i = 0; i < 6; i++) { const x = 104 + i * 22; c.fillStyle = '#ecebe6'; c.fillRect(x, 84, 18, 104); c.fillStyle = '#555'; c.fillRect(x + 5, 104, 8, 34); }
  c.fillStyle = 'rgba(170,200,220,0.18)'; c.fillRect(22, 60, (w - 44) * 0.45, 200);
  c.fillStyle = '#222'; c.font = `600 15px ${FONT}`; c.textAlign = 'left';
  c.fillText('누전차단기', 30, 222); c.fillText('전등 · 전열 · 주방 · 에어컨', 30, 246);
  c.fillStyle = '#9a958a'; c.fillRect(w - 40, h - 46, 18, 26);
}
function drawArt(c, w, h) {
  const gr = c.createLinearGradient(0, 0, 0, h); gr.addColorStop(0, '#d9c7a6'); gr.addColorStop(0.6, '#e9dcc3'); gr.addColorStop(1, '#a9b3a0'); c.fillStyle = gr; c.fillRect(0, 0, w, h);
  c.fillStyle = '#7d8d86'; c.beginPath(); c.moveTo(0, h * 0.7); c.quadraticCurveTo(w * 0.3, h * 0.35, w * 0.55, h * 0.62); c.quadraticCurveTo(w * 0.8, h * 0.45, w, h * 0.66); c.lineTo(w, h); c.lineTo(0, h); c.fill();
  c.fillStyle = '#c96f4a'; c.beginPath(); c.arc(w * 0.72, h * 0.3, 18, 0, 7); c.fill();
}
function drawClock(c, w, h) {
  c.fillStyle = '#f7f5f0'; c.fillRect(0, 0, w, h); c.strokeStyle = '#333'; c.lineCap = 'round';
  for (let i = 0; i < 12; i++) { const a = i * Math.PI / 6; c.lineWidth = i % 3 ? 4 : 8; c.beginPath(); c.moveTo(w / 2 + Math.sin(a) * 100, h / 2 - Math.cos(a) * 100); c.lineTo(w / 2 + Math.sin(a) * 118, h / 2 - Math.cos(a) * 118); c.stroke(); }
  c.lineWidth = 9; c.beginPath(); c.moveTo(w / 2, h / 2); c.lineTo(w / 2 - 44, h / 2 - 40); c.stroke();
  c.lineWidth = 6; c.beginPath(); c.moveTo(w / 2, h / 2); c.lineTo(w / 2 + 70, h / 2 - 50); c.stroke();
}
function drawLaptop(c, w, h) {
  const gr = c.createLinearGradient(0, 0, w, h); gr.addColorStop(0, '#2b4d6e'); gr.addColorStop(1, '#5b7f95'); c.fillStyle = gr; c.fillRect(0, 0, w, h);
  c.fillStyle = '#f4f6f8'; c.fillRect(40, 30, 200, 130); c.fillStyle = '#dfe5ea'; c.fillRect(40, 30, 200, 16);
  c.fillStyle = '#c8d6c0'; c.fillRect(48, 54, 120, 98); c.strokeStyle = '#ffffff'; c.lineWidth = 5; c.beginPath(); c.moveTo(48, 120); c.lineTo(110, 96); c.lineTo(168, 108); c.stroke();
  c.fillStyle = '#333'; c.font = `600 11px ${FONT}`; c.fillText('지도', 176, 66); c.fillText('메모', 176, 86);
  c.fillStyle = 'rgba(0,0,0,0.45)'; c.fillRect(0, h - 18, w, 18);
}
