// 바로편의점: a small Korean convenience store. Glass storefront with a pull door and plastic tables outside, a
// window bar with stools, a ramen station (hot-water dispenser, microwave, sorted bins), two low gondolas, a bank
// of glass-door drink fridges, an ice-cream chest, the counter with 이수아 (NPC 'clerk'), an impulse battery rack,
// a self-serve coffee machine and an ATM by the door.
// Frame: floor x -5..5, z -9..0, storefront on the south wall (z = 0), yaw 0 faces north (-z).
// power 'off': ceiling LEDs, fridge glow, screens and real lights die (lights sit in one `powered` group, glowing
// materials lose their emissive colour) and two battery emergency units light the room.
import * as THREE from 'three';
import { createGoods, gondola, panels, lamps, windowView, moodFor, npc, solidOf, MOODS } from './commerce-goods.js';

const H = 2.9, DOOR = 0.6, FX0 = -4.92, FDOOR = 0.76, FN = 8;
const SOLD = ['water', 'batteries', 'food'];
const BLACK = new THREE.Color(0);

export function buildStore(kit, people) {
  const group = new THREE.Group(); group.name = 'store';
  const stat = new THREE.Group(), dyn = new THREE.Group(), powered = new THREE.Group();
  group.add(stat, dyn); dyn.add(powered);
  const solids = [], r = kit.rng(19), goods = createGoods(kit), glow = [];
  const M = {
    wall: kit.surface('plaster'), floor: kit.surface('tile', { roughness: 0.2, color: 0xf2f0ea }), steel: kit.mat('steel'), metal: kit.mat('metal'),
    dark: kit.mat('darkMetal'), white: kit.mat('paint:#f1efe9', { roughness: 0.4 }), teal: kit.mat('paint:#18a58f'), orange: kit.mat('paint:#f28a1a'),
    top: kit.mat('paint:#3a3c40', { roughness: 0.3 }), rubber: kit.mat('rubber'), wood: kit.surface('wood'), plastic: kit.mat('plastic'),
    green: kit.mat('paint:#2f8a57', { roughness: 0.5 }), dglass: kit.mat('darkGlass'),
    glass: new THREE.MeshStandardMaterial({ color: 0xd6e6ee, roughness: 0.04, metalness: 0.1, transparent: true, opacity: 0.16, depthWrite: false }),
  };
  const add = (o, solid, pad = 0.03) => { stat.add(o); if (solid) solids.push(solidOf(o, pad)); return o; };
  const keep = (o, parent = dyn) => { o.userData.keep = true; parent.add(o); return o; };
  // Glowing material: follows the env's 'indoor' group and goes dark with the store's power.
  const lampMat = (hex, on, extra = {}) => {
    const m = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: new THREE.Color(hex), roughness: 0.4, ...extra });
    kit.lit('indoor', m, on); glow.push([m, m.emissive.clone()]); return m;
  };
  const sign = (lines, o, at, lightGroup) => {
    const s = kit.sign(lines, { single: true, ...o }); s.position.set(at.x, at.y, at.z); s.rotation.y = at.ry || 0; s.userData.keep = true;
    if (lightGroup) kit.lit(lightGroup, s.material, o.emissive || 0.6);
    if (lightGroup === 'indoor') glow.push([s.material, s.material.emissive.clone()]);
    dyn.add(s); return s;
  };

  // ---- shell: floor, walls with the brand band, ceiling, skirting ----
  stat.add(kit.plane(10, 9, M.floor, { x: 0, z: -4.5 }));
  add(kit.slab(0.25, H, 9.5, M.wall, { x: -5.125, z: -4.5 }), true); add(kit.slab(0.25, H, 9.5, M.wall, { x: 5.125, z: -4.5 }), true);
  add(kit.slab(10.5, H, 0.25, M.wall, { x: 0, z: -9.125 }), true);
  stat.add(kit.slab(10.5, 0.12, 9.5, kit.mat('paint:#f3f2ee', { roughness: 0.9 }), { x: 0, y: H, z: -4.5 }));
  for (const [x, z, w, d] of [[-4.99, -4.5, 0.02, 9], [4.99, -4.5, 0.02, 9], [0, -8.99, 10, 0.02]]) {
    stat.add(kit.box(w, 0.22, d, M.teal, { x, y: 2.46, z, cast: false })); stat.add(kit.box(w, 0.04, d, M.orange, { x, y: 2.42, z, cast: false }));
    stat.add(kit.box(w + 0.01, 0.1, d + 0.01, M.dark, { x, z, cast: false }));
  }
  const dome = new THREE.Mesh(new THREE.SphereGeometry(0.07, 12, 8, 0, Math.PI * 2, 0, Math.PI / 2), M.dglass); dome.rotation.x = Math.PI; dome.position.set(1.8, H, -4.4); stat.add(dome);

  // ---- storefront: glazing, mullions, pull door, stickers ----
  stat.add(kit.box(10, 0.3, 0.12, M.dark, { x: 0, z: -0.06 })); stat.add(kit.box(10, 0.4, 0.2, M.dark, { x: 0, y: H - 0.4, z: -0.1 }));
  for (const x of [-5, -3.75, -2.5, -1.25, 0.1, 1.1, 2.5, 3.75, 5]) stat.add(kit.box(0.06, H - 0.7, 0.1, M.metal, { x, y: 0.3, z: -0.05 }));
  for (const [x0, x1] of [[-5, 0.1], [1.1, 5]]) keep(kit.box(x1 - x0, H - 0.7, 0.02, M.glass, { x: (x0 + x1) / 2, y: 0.3, z: -0.05, cast: false }));
  solids.push({ x0: -5, x1: 5, y0: 0, y1: H, z0: -0.15, z1: 0.4, id: 'storefront' });
  const leaf = new THREE.Group(); leaf.position.set(0.1, 0, -0.05); dyn.add(leaf);
  keep(kit.box(0.98, 2.2, 0.02, M.glass, { x: 0.49, y: 0.02, cast: false }), leaf);
  for (const [w, h, x, y] of [[0.98, 0.05, 0.49, 0.02], [0.98, 0.05, 0.49, 2.17], [0.05, 2.2, 0.025, 0.02], [0.05, 2.2, 0.955, 0.02]]) leaf.add(kit.box(w, h, 0.05, M.metal, { x, y }));
  leaf.add(kit.box(0.03, 0.5, 0.03, M.steel, { x: 0.86, y: 0.8, z: -0.07 }));
  const sticker = (lines, w, h, bg, fg, x, y, parent = dyn, z = -0.075) => { const s = kit.sign(lines, { w, h, bg, fg, single: true }); s.position.set(x, y, z); s.rotation.y = Math.PI; return keep(s, parent); };
  sticker(['당기세요', 'PULL'], 0.22, 0.12, '#18a58f', '#ffffff', 0.86, 1.42, leaf, -0.02);
  sticker(['24시간 영업'], 0.5, 0.16, '#ffffff', '#18a58f', -0.6, 1.6);
  sticker(['2+1 행사', '음료 · 생수'], 0.7, 0.5, '#f28a1a', '#ffffff', -3.1, 1.75);
  sticker(['1+1', '컵라면 골라 담기'], 0.7, 0.5, '#c4161c', '#ffffff', 3.1, 1.75);
  sticker(['택배 접수', '교통카드 충전'], 0.6, 0.3, '#ffffff', '#1f4f8c', 1.8, 1.9);

  // ---- outside: sidewalk, curb, road, canopy, plastic tables, a street lamp, the painted street ----
  dyn.add(kit.plane(14, 4, kit.surface('sidewalk'), { x: 0, z: 2.2 }));
  dyn.add(kit.box(14, 0.14, 0.18, kit.mat('curb'), { x: 0, y: -0.12, z: 4.25 }));
  dyn.add(kit.plane(40, 12, kit.surface('asphalt'), { x: 0, y: -0.12, z: 10.3 }));
  dyn.add(kit.box(10.4, 0.16, 1.5, M.teal, { x: 0, y: H + 0.02, z: 0.75 }));
  const edge = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xffe3b0, roughness: 0.4 }); kit.lit('shops', edge, 1.6);
  dyn.add(kit.box(10.4, 0.03, 0.04, edge, { x: 0, y: H, z: 1.48, cast: false }));
  const tbl = new THREE.Group();
  for (const tx of [-3.3, -1.3]) {
    tbl.add(kit.cyl(0.42, 0.42, 0.03, M.plastic, { x: tx, y: 0.7, z: 1.9 }, 20)); tbl.add(kit.cyl(0.03, 0.03, 0.7, M.metal, { x: tx, z: 1.9 }));
    for (const a of [0, Math.PI / 2, Math.PI, 1.5 * Math.PI]) {
      const cx = tx + Math.sin(a + 0.3) * 0.62, cz = 1.9 + Math.cos(a + 0.3) * 0.62;
      tbl.add(kit.box(0.42, 0.04, 0.42, M.green, { x: cx, y: 0.42, z: cz, ry: a + 0.3 }));
      tbl.add(kit.box(0.42, 0.38, 0.04, M.green, { x: cx + Math.sin(a + 0.3) * 0.2, y: 0.46, z: cz + Math.cos(a + 0.3) * 0.2, ry: a + 0.3 }));
      for (const s of [-1, 1]) tbl.add(kit.box(0.03, 0.42, 0.03, M.green, { x: cx + Math.cos(a + 0.3) * 0.18 * s, z: cz - Math.sin(a + 0.3) * 0.18 * s }));
    }
  }
  dyn.add(kit.merge(tbl));
  dyn.add(kit.cyl(0.07, 0.09, 6.2, M.dark, { x: 4.4, z: 3.8 }));
  const head = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xffd9a0, roughness: 0.4 }); kit.lit('street', head, 3);
  dyn.add(kit.box(0.9, 0.1, 0.3, M.dark, { x: 4.0, y: 6.1, z: 3.8 })); dyn.add(kit.box(0.6, 0.03, 0.2, head, { x: 3.85, y: 6.08, z: 3.8, cast: false }));
  const view = windowView(kit, { w: 34, h: 12, kind: 'street', seed: 23 });
  view.mesh.position.set(0, 3.04, 14.5); view.mesh.rotation.y = Math.PI; dyn.add(view.mesh);

  // ---- window bar with stools ----
  add(kit.box(4.55, 0.04, 0.42, M.wood, { x: -2.48, y: 1.0, z: -0.42 }));
  for (const x of [-4.6, -2.48, -0.35]) stat.add(kit.box(0.04, 1.0, 0.04, M.steel, { x, z: -0.55 }));
  for (const x of [-4.3, -3.4, -2.5, -1.6, -0.75]) {
    stat.add(kit.cyl(0.17, 0.16, 0.06, M.dark, { x, y: 0.7, z: -0.98 }, 18)); stat.add(kit.cyl(0.025, 0.025, 0.7, M.metal, { x, z: -0.98 }));
    stat.add(kit.cyl(0.2, 0.22, 0.03, M.metal, { x, z: -0.98 }, 18));
  }
  solids.push({ x0: -4.75, x1: -0.2, y0: 0, y1: 1.1, z0: -1.18, z1: -0.15, id: 'windowBar' });
  stat.add(kit.box(0.12, 0.14, 0.1, M.plastic, { x: -1.0, y: 1.04, z: -0.42 }));

  // ---- ramen station: hot water, microwave, bins ----
  add(kit.box(0.55, 0.88, 1.2, M.white, { x: -4.725, z: -2.15 }), true);
  stat.add(kit.box(0.58, 0.03, 1.24, M.steel, { x: -4.72, y: 0.88, z: -2.15 }));
  stat.add(kit.box(0.36, 0.5, 0.34, M.metal, { x: -4.75, y: 0.91, z: -2.48 }));
  stat.add(kit.box(0.04, 0.06, 0.05, kit.mat('paint:#c4161c'), { x: -4.55, y: 1.06, z: -2.56 })); stat.add(kit.box(0.04, 0.06, 0.05, kit.mat('paint:#1f6fb2'), { x: -4.55, y: 1.06, z: -2.4 }));
  stat.add(kit.box(0.2, 0.02, 0.26, M.dark, { x: -4.62, y: 0.91, z: -2.48 }));
  dyn.add(kit.box(0.01, 0.05, 0.12, lampMat('#ff5a3a', 1.4), { x: -4.565, y: 1.26, z: -2.48, cast: false }));
  stat.add(kit.box(0.38, 0.28, 0.48, M.white, { x: -4.76, y: 0.91, z: -1.84 }));
  stat.add(kit.box(0.01, 0.2, 0.32, M.dglass, { x: -4.565, y: 0.95, z: -1.9 }));
  dyn.add(kit.box(0.01, 0.03, 0.08, lampMat('#5aff9a', 1.2), { x: -4.565, y: 1.1, z: -1.64, cast: false }));
  stat.add(kit.cyl(0.04, 0.04, 0.12, M.plastic, { x: -4.8, y: 0.91, z: -2.75 }));
  for (let k = 0; k < 6; k++) stat.add(kit.cyl(0.04, 0.035, 0.1, M.white, { x: -4.86, y: 0.91 + k * 0.02, z: -1.56 }));
  sign(['라면 조리대', '뜨거운 물 조심 · 국물은 오른쪽 통에'], { w: 1.0, h: 0.32, bg: '#ffffff', fg: '#c4161c', border: '#c4161c' }, { x: -4.98, y: 1.75, z: -2.15, ry: Math.PI / 2 });
  [['일반', '#5b6770'], ['캔·페트', '#1f6fb2'], ['국물', '#c4161c']].forEach(([lbl, c], i) => {
    const z = -2.96 - i * 0.32;
    stat.add(kit.box(0.36, 0.78, 0.3, kit.mat('paint:' + c, { roughness: 0.5 }), { x: -4.8, z }));
    stat.add(kit.box(0.3, 0.02, 0.24, M.dark, { x: -4.8, y: 0.78, z }));
    sign([lbl], { w: 0.26, h: 0.1, bg: '#ffffff', fg: c }, { x: -4.615, y: 0.62, z, ry: Math.PI / 2 });
  });
  solids.push({ x0: -5, x1: -4.58, y0: 0, y1: 0.9, z0: -3.8, z1: -2.75 });

  // ---- shelving: west wall + two low gondolas ----
  const wall = gondola(kit, { x: -4.98, z: -5.6, len: 3.4, sides: 'pos', depth: 0.42, height: 1.9, levels: [0.1, 0.46, 0.82, 1.18, 1.54] });
  add(wall.group, true);
  const WALL = [['tissue', 'towel'], ['wipes', 'bags', 'tape'], ['soap', 'deter'], ['rain', 'foil', 'candle'], ['mask', 'band', 'sanit']];
  wall.runs.forEach((run, i) => goods.stock({ ...run, sec: 'misc', items: WALL[i], seed: 10 + i }));
  const LOW = { levels: [0.1, 0.46, 0.82, 1.18], height: 1.45, len: 4.2, z: -4.6 };
  const gA = gondola(kit, { x: -2.5, ...LOW }), gB = gondola(kit, { x: 0.3, ...LOW });
  add(gA.group, true); add(gB.group, true);
  const A = { neg: [['misc', ['chips', 'chips']], ['misc', ['cookie', 'cracker']], ['food', ['bar', 'cracker']], ['misc', ['cookie', 'chips']]],
    pos: [['food', ['ramen', 'ramen']], ['food', ['ramen', 'ramen']], ['food', ['cup', 'cup']], ['food', ['cup', 'cup']]] };
  const B = { neg: [['food', ['rice', 'rice']], ['food', ['tuna', 'ham']], ['food', ['soup', 'gim']], ['food', ['bar', 'cracker']]],
    pos: [['misc', ['mask', 'wipes']], ['misc', ['band', 'sanit']], ['batteries', ['aa', 'aa', 'power']], ['batteries', ['flash', 'aa', 'candle']]] };
  for (let [gd, plan, seed] of [[gA, A, 30], [gB, B, 50]]) {
    const idx = { neg: 0, pos: 0 };
    gd.runs.forEach((run) => { const side = run.side < 0 ? 'neg' : 'pos', [sec, items] = plan[side][idx[side]++]; goods.stock({ ...run, sec, items, seed: seed++ }); });
  }
  for (const [x, lines, bg] of [[-2.5, ['2+1', '과자'], '#f28a1a'], [0.3, ['1+1', '즉석밥'], '#c4161c']]) {
    stat.add(kit.cyl(0.008, 0.008, 0.3, M.steel, { x, y: 1.45, z: -2.48, cast: false }));
    sign(lines, { w: 0.34, h: 0.26, bg, fg: '#ffffff' }, { x, y: 1.86, z: -2.47 });
  }

  // ---- drink fridges (north wall): cabinet, lit back, shelves, glass doors with LED mullions ----
  const flen = FN * FDOOR, fcx = FX0 + flen / 2;
  add(kit.box(flen, 0.14, 0.8, M.dark, { x: fcx, z: -8.6 }));
  add(kit.box(flen, 0.32, 0.82, M.dark, { x: fcx, y: 2.02, z: -8.6 }));
  for (const x of [FX0 - 0.025, FX0 + flen + 0.025]) add(kit.box(0.05, 2.34, 0.82, M.dark, { x, z: -8.6 }));
  solids.push({ x0: FX0 - 0.05, x1: FX0 + flen + 0.05, y0: 0, y1: 2.4, z0: -9, z1: -8.12, id: 'fridges' });
  const back = new THREE.Mesh(new THREE.PlaneGeometry(flen, 1.88), lampMat('#eaf2ff', 0.45, { color: 0xe8eef4 })); back.position.set(fcx, 1.08, -8.95); stat.add(back);
  const FL = [0.16, 0.54, 0.92, 1.3, 1.66];
  for (const y of FL) stat.add(kit.box(flen, 0.02, 0.62, M.steel, { x: fcx, y: y - 0.02, z: -8.55, cast: false }));
  const led = lampMat('#f4f8ff', 2.4);
  for (let i = 0; i <= FN; i++) {
    const x = FX0 + i * FDOOR;
    stat.add(kit.box(0.05, 1.88, 0.05, M.dark, { x, y: 0.14, z: -8.16 }));
    stat.add(kit.box(0.012, 1.8, 0.012, led, { x, y: 0.18, z: -8.2, cast: false }));
    if (i < FN) {
      for (const y of [0.14, 1.97]) stat.add(kit.box(FDOOR, 0.05, 0.04, M.dark, { x: x + FDOOR / 2, y, z: -8.14 }));
      stat.add(kit.cyl(0.012, 0.012, 0.9, M.metal, { x: x + FDOOR - 0.09, y: 0.62, z: -8.08 }, 8));
    }
  }
  keep(kit.box(flen, 1.88, 0.01, M.glass, { x: fcx, y: 0.14, z: -8.14, cast: false }));
  const DOORS = [['water', ['water2', 'water2'], ['water5', 'water5']], ['misc', ['drink', 'drink', 'drink'], ['drink', 'can', 'drink']], ['misc', ['can', 'vita'], ['can', 'can']], ['misc', ['milk', 'soymilk'], ['yogurt', 'milk']]];
  const spans = [[0, 2], [2, 3], [5, 2], [7, 1]];
  spans.forEach(([d0, n], k) => {
    const [sec, low, up] = DOORS[k];
    FL.forEach((y, i) => goods.stock({ sec, x: FX0 + d0 * FDOOR + 0.03, z: -8.24, dir: [1, 0], face: [0, 1], len: n * FDOOR - 0.06, depth: 0.58, y, clear: (FL[i + 1] ?? 2.0) - y - 0.04, items: (i === 0 ? low : up).slice(0, n * 2), rows: 3, seed: 70 + k * 7 + i }));
  });
  sign(['시원한 음료 · 생수 · 유제품'], { w: 3.2, h: 0.22, bg: '#18a58f', fg: '#ffffff', emissive: 0.9 }, { x: fcx, y: 2.18, z: -8.185 }, 'indoor');
  const fglow = new THREE.PointLight(0xeaf2ff, 3, 6, 2); fglow.position.set(fcx, 1.5, -7.5); kit.lit('indoor', fglow, 3); powered.add(fglow);

  // ---- ice-cream chest freezer and the stock corner (water six-packs, crates, staff door) ----
  for (const [w, d, x, z] of [[1.5, 0.05, 2.2, -8.23], [1.5, 0.05, 2.2, -8.93], [0.05, 0.65, 1.475, -8.58], [0.05, 0.65, 2.925, -8.58]]) stat.add(kit.box(w, 0.85, d, M.white, { x, z }));
  stat.add(kit.box(1.4, 0.6, 0.65, M.white, { x: 2.2, z: -8.58 }));
  solids.push({ x0: 1.42, x1: 2.98, y0: 0, y1: 0.9, z0: -8.98, z1: -8.18, id: 'freezer' });
  const ice = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 0.65), lampMat('#cfe6ff', 0.35, { color: 0xdfe9f2 })); ice.rotation.x = -Math.PI / 2; ice.position.set(2.2, 0.605, -8.58); stat.add(ice);
  const pops = [], pc = [];
  for (let i = 0; i < 44; i++) { pops.push({ x: 1.6 + r() * 1.2, y: 0.61, z: -8.82 + r() * 0.48, ry: r() * 3, sx: 0.16, sy: 0.03 + r() * 0.04, sz: 0.07 }); pc.push(['#f28a1a', '#c4161c', '#1f6fb2', '#f2c230', '#7a3b12', '#2f9a3c', '#f7c7d8'][(r() * 7) | 0]); }
  const popMesh = kit.instanced(new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0), new THREE.MeshStandardMaterial({ roughness: 0.5 }), pops, { cast: false });
  pc.forEach((c, i) => popMesh.setColorAt(i, new THREE.Color(c))); dyn.add(popMesh);
  keep(kit.box(1.44, 0.01, 0.7, M.glass, { x: 2.2, y: 0.85, z: -8.58, cast: false }));
  sign(['아이스크림', '반값 행사'], { w: 0.5, h: 0.24, bg: '#1f6fb2', fg: '#ffffff' }, { x: 2.2, y: 0.55, z: -8.195 });
  goods.stock({ sec: 'water', x: 3.35, z: -8.0, dir: [1, 0], face: [0, 1], len: 1.2, depth: 0.95, y: 0, clear: 1.05, items: ['pack6', 'pack6'], rows: 3, stack: 3, fill: 1, edge: false, seed: 9 });
  solids.push({ x0: 3.3, x1: 4.6, y0: 0, y1: 1.05, z0: -9, z1: -7.95, id: 'waterStack' });
  const crates = [];
  for (let k = 0; k < 4; k++) crates.push({ x: 4.62, y: k * 0.3, z: -5.85, ry: (r() - 0.5) * 0.1, sx: 0.55, sy: 0.29, sz: 0.38 });
  dyn.add(kit.instanced(new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0), kit.mat('paint:#2f8a57', { roughness: 0.6 }), crates));
  solids.push({ x0: 4.3, x1: 5, y0: 0, y1: 1.2, z0: -6.1, z1: -5.6 });
  add(kit.box(0.06, 2.1, 0.95, kit.mat('paint:#c9ccd1', { metalness: 0.5, roughness: 0.4 }), { x: 4.97, z: -6.95 }));
  sign(['관계자 외 출입금지'], { w: 0.5, h: 0.14, bg: '#c4161c', fg: '#ffffff' }, { x: 4.93, y: 1.6, z: -6.95, ry: -Math.PI / 2 });

  // ---- counter, POS, steamer, impulse rack, back shelf, coffee machine, ATM ----
  add(kit.rbox(0.6, 0.92, 3.0, 0.02, M.white, { x: 2.95, z: -3.4 }), true);
  stat.add(kit.box(0.7, 0.04, 3.08, M.top, { x: 2.95, y: 0.92, z: -3.4 }));
  stat.add(kit.box(0.02, 0.22, 3.0, M.teal, { x: 2.64, y: 0.55, z: -3.4 })); stat.add(kit.box(0.02, 0.03, 3.0, M.orange, { x: 2.64, y: 0.5, z: -3.4 }));
  const screen = (w, h, hex, at) => { const m = kit.box(0.01, h, w, lampMat(hex, 0.9, { color: 0x111418 }), { ...at, cast: false }); dyn.add(m); return m; };
  stat.add(kit.box(0.2, 0.05, 0.24, M.dark, { x: 3.1, y: 0.96, z: -3.7 })); stat.add(kit.box(0.04, 0.18, 0.04, M.dark, { x: 3.1, y: 1.0, z: -3.7 }));
  stat.add(kit.box(0.05, 0.28, 0.38, M.dark, { x: 3.12, y: 1.14, z: -3.7, rz: 0.15 })); screen(0.34, 0.24, '#7fb8ff', { x: 3.155, y: 1.16, z: -3.7, rz: 0.15 });
  stat.add(kit.box(0.05, 0.14, 0.22, M.dark, { x: 2.8, y: 1.08, z: -3.7 })); screen(0.18, 0.1, '#7fe0c6', { x: 2.772, y: 1.1, z: -3.7 });
  stat.add(kit.box(0.09, 0.05, 0.16, M.dark, { x: 2.78, y: 0.96, z: -3.15, rz: -0.3 })); screen(0.07, 0.05, '#9ad0ff', { x: 2.78, y: 1.0, z: -3.13, rz: -0.3 });
  stat.add(kit.box(0.08, 0.1, 0.06, M.dark, { x: 3.0, y: 0.96, z: -3.25 }));
  // 찐빵 steamer: glass case with warm light and buns
  stat.add(kit.box(0.4, 0.04, 0.36, M.steel, { x: 2.95, y: 0.96, z: -2.25 })); stat.add(kit.box(0.4, 0.04, 0.36, M.steel, { x: 2.95, y: 1.38, z: -2.25 }));
  keep(kit.box(0.38, 0.38, 0.34, M.glass, { x: 2.95, y: 1.0, z: -2.25, cast: false }));
  dyn.add(kit.box(0.36, 0.01, 0.3, lampMat('#ffb35a', 1.6), { x: 2.95, y: 1.37, z: -2.25, cast: false }));
  const buns = []; for (let i = 0; i < 6; i++) buns.push({ x: 2.83 + (i % 3) * 0.12, y: 1.05, z: -2.33 + Math.floor(i / 3) * 0.16, s: 0.05, sy: 0.035 });
  dyn.add(kit.instanced(new THREE.SphereGeometry(1, 10, 8), kit.mat('paint:#f4ede0', { roughness: 0.8 }), buns, { cast: false }));
  const rack = gondola(kit, { x: 2.64, z: -4.3, len: 0.55, sides: 'neg', depth: 0.16, levels: [0.3, 0.55, 0.8], height: 0.92, color: '#18a58f' });
  add(rack.group, true); rack.runs.forEach((run, k) => goods.stock({ ...run, sec: k < 2 ? 'batteries' : 'misc', items: [k < 2 ? 'aa' : 'gum'], seed: 140 + k, edge: false }));
  add(kit.box(0.45, 0.9, 3.0, M.white, { x: 4.77, z: -3.4 }), true);
  const backShelf = gondola(kit, { x: 4.99, z: -3.4, len: 3.0, sides: 'neg', depth: 0.32, levels: [0.98, 1.28, 1.58, 1.88], height: 2.1, color: '#f2f2ef' });
  add(backShelf.group, true);
  const BACKS = [['batteries', ['aa', 'aa', 'power']], ['batteries', ['power', 'flash', 'aa']], ['misc', ['mask', 'band', 'mask']], ['misc', ['vita', 'can', 'vita']]];
  backShelf.runs.forEach((run, k) => goods.stock({ ...run, sec: BACKS[k][0], items: BACKS[k][1], seed: 160 + k }));
  sign(['안전상비의약품 · 건전지 · 충전기'], { w: 1.6, h: 0.2, bg: '#1f4f8c', fg: '#ffffff', emissive: 0.6 }, { x: 4.97, y: 2.35, z: -3.4, ry: -Math.PI / 2 }, 'indoor');
  add(kit.box(1.75, 0.92, 0.4, M.white, { x: 4.125, z: -1.7 }), true);
  add(kit.box(1.75, 0.92, 0.36, M.white, { x: 4.125, z: -5.1 }), true);
  stat.add(kit.box(0.42, 0.58, 0.42, M.dark, { x: 4.4, y: 0.92, z: -1.72 }));
  sign(['원두커피', '셀프'], { w: 0.3, h: 0.2, bg: '#3b2416', fg: '#f2dcc0', emissive: 0.7 }, { x: 4.4, y: 1.3, z: -1.505 }, 'indoor');
  for (let k = 0; k < 5; k++) stat.add(kit.cyl(0.045, 0.035, 0.1, M.white, { x: 3.75, y: 0.92 + k * 0.03, z: -1.7 }));
  const hang = sign(['계산대 · 택배 접수 · 교통카드 충전'], { w: 1.9, h: 0.3, bg: '#18a58f', fg: '#ffffff', emissive: 0.6 }, { x: 2.5, y: 2.35, z: -3.4, ry: -Math.PI / 2 }, 'indoor');
  hang.material.side = THREE.DoubleSide;
  for (const s of [-1, 1]) stat.add(kit.cyl(0.004, 0.004, H - 2.5, M.steel, { x: 2.5, y: 2.5, z: -3.4 + s * 0.8, cast: false }));
  const atm = new THREE.Group();
  atm.add(kit.rbox(0.75, 1.75, 0.9, 0.04, kit.mat('paint:#d7dbe0', { metalness: 0.5, roughness: 0.35 }), { x: 4.62, z: -0.62 }));
  atm.add(kit.box(0.78, 0.28, 0.92, M.green, { x: 4.62, y: 1.75, z: -0.62 }));
  atm.add(kit.box(0.18, 0.1, 0.3, M.dark, { x: 4.2, y: 0.95, z: -0.62, rz: 0.4 }));
  add(atm, true);
  sign(['24시 자동화기기', '현금 인출 · 입금 · 이체'], { w: 0.44, h: 0.32, bg: '#0f3d6e', fg: '#e8f4ff', emissive: 0.9 }, { x: 4.235, y: 1.32, z: -0.62, ry: -Math.PI / 2 }, 'indoor');
  sign(['ATM'], { w: 0.5, h: 0.2, bg: '#2f8a57', fg: '#ffffff', emissive: 0.7 }, { x: 4.225, y: 1.89, z: -0.62, ry: -Math.PI / 2 }, 'indoor');
  stat.add(kit.box(1.4, 0.012, 0.9, M.rubber, { x: DOOR, z: -0.7, cast: false }));

  // ---- goods and sold-out cards ----
  const built = goods.build(); group.add(built.group);
  const sections = built.sections;
  const cards = { water: [FX0 + 0.76, 1.3, -8.125, 0], batteries: [0.8, 1.08, -3.6, Math.PI / 2], food: [-2.0, 1.08, -4.6, Math.PI / 2] };
  for (const sec of SOLD) {
    sections[sec] ||= { full: new THREE.Group(), out: new THREE.Group() };
    const [x, y, z, ry] = cards[sec];
    const s = kit.sign(['품절', '입고 일정 미정'], { w: 0.36, h: 0.26, bg: '#ffffff', fg: '#c4161c', border: '#c4161c', single: true });
    s.position.set(x, y, z); s.rotation.y = ry; sections[sec].out.add(s);
  }

  // ---- lights: ceiling LED lines, real lamps (powered), battery emergency units, exit sign ----
  const fixtures = [];
  for (const z of [-1.3, -3.0, -4.7, -6.4, -7.9]) for (const x of [-3.4, -0.6, 2.2]) fixtures.push({ x, y: H - 0.03, z });
  const pm = panels(kit, fixtures, { w: 2.4, d: 0.1, color: '#f4f8ff', on: 2.6 }); glow.push([pm.material, pm.material.emissive.clone()]); dyn.add(pm);
  powered.add(lamps(kit, [[-2.6, -2.4], [1.6, -2.6], [-2.6, -6.2], [1.6, -6.4]].map(([x, z]) => ({ x, y: H - 0.3, z })), { intensity: 9, distance: 9, color: 0xf6f8ff }));
  const emerg = [[DOOR, 2.62, -0.32, Math.PI], [-2.0, 2.55, -8.92, 0], [4.92, 2.55, -3.4, -Math.PI / 2]].map(([x, y, z, ry]) => {
    const u = new THREE.Group(); u.position.set(x, y, z); u.rotation.y = ry; dyn.add(u);
    u.add(kit.box(0.34, 0.12, 0.08, M.white, { y: 0 }));
    const m = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xfff2dc, emissiveIntensity: 0, roughness: 0.3 });
    for (const s of [-1, 1]) u.add(kit.cyl(0.035, 0.045, 0.06, m, { x: s * 0.11, y: -0.05, z: 0.06, rx: 0.6, cast: false }, 10));
    const l = new THREE.PointLight(0xfff2dc, 0, 9, 2); l.position.set(x + Math.sin(ry) * 0.35, y - 0.12, z + Math.cos(ry) * 0.35); dyn.add(l);
    return { l, m };
  });
  sign(['비상구'], { w: 0.5, h: 0.18, bg: '#1f9a4a', fg: '#ffffff', emissive: 1.2 }, { x: DOOR, y: 2.42, z: -0.22, ry: Math.PI }, 'emergency');
  const clock = kit.sign(['17:30'], { w: 0.34, h: 0.14, bg: '#101214', fg: '#ff5a3a', single: true, emissive: 0.8 }); clock.position.set(-4.98, 2.2, -5.3); clock.rotation.y = Math.PI / 2;
  kit.lit('indoor', clock.material, 0.8); glow.push([clock.material, clock.material.emissive.clone()]); keep(clock);

  // ---- people ----
  const clerk = npc(people, 'clerk', 7, { x: 3.9, z: -3.3, ry: Math.PI / 2, anim: 'idle' });
  dyn.add(clerk.group);
  const sitter = npc(people, 'crowd', 211, { x: -2.5, z: -0.98, ry: Math.PI, anim: 'sitChair' }); sitter.seat = 0.76;
  const browser = npc(people, 'crowd', 212, { x: -3.6, z: -7.6, ry: 0, anim: 'idle' });
  const extras = [sitter, browser]; for (const p of extras) dyn.add(p.group);
  stat.add(kit.cyl(0.055, 0.045, 0.1, kit.mat('paint:#d8261c'), { x: -2.5, y: 1.04, z: -0.42 }, 14));
  stat.add(kit.cyl(0.057, 0.057, 0.006, M.white, { x: -2.5, y: 1.14, z: -0.42 }, 14));

  // ---- state ----
  const state = { soldOut: [], power: 'on', outside: null, clerk: 'in' };
  function applyPower(on) {
    powered.traverse((o) => { if (o.isLight) kit.gateLit(o, on); }); // dim, never hide: light count stays fixed
    for (const [m, c] of glow) m.emissive.copy(on ? c : BLACK);
    for (const e of emerg) { e.l.intensity = on ? 0 : 2.6; e.m.emissiveIntensity = on ? 0 : 2.6; }
    for (const p of extras) p.group.visible = on;
    clerk.setProp('flashlight', !on);
  }
  function setState(key, v) {
    if (key === 'soldOut') {
      const list = Array.isArray(v) ? v : [];
      for (const id of list) if (!SOLD.includes(id)) console.error('[store] unknown soldOut section', id, '; known:', SOLD);
      state.soldOut = list.filter((id) => SOLD.includes(id));
      for (const sec of SOLD) { const out = state.soldOut.includes(sec); sections[sec].full.visible = !out; sections[sec].out.visible = out; }
    } else if (key === 'power') {
      if (!['on', 'off'].includes(v)) return void console.error('[store] power must be on|off', v);
      state.power = v; applyPower(v === 'on');
    } else if (key === 'clerk') { // 'out': 이수아 has left town; nobody behind the counter
      if (!['in', 'out'].includes(v)) return void console.error('[store] clerk must be in|out', v);
      state.clerk = v; clerk.group.visible = v === 'in';
      const c = points.find((p) => p.id === 'storeCounter'); c.label = v === 'in' ? '계산대 · 이수아' : '계산대 (아무도 없다)';
    } else if (key === 'outside') { if (v !== null && !MOODS.includes(v)) return void console.error('[store] outside must be null or', MOODS, v); state.outside = v; }
    else console.error('[store] unknown state', key, v);
  }

  const P = (id, label, x, z, kind, rad = 1.6, desc) => ({ id, label, x, y: 0, z, r: rad, kind, ...(desc && { desc }) });
  // desc: what E shows when no chapter handles the point (story/world.js). A function gets the run context.
  const dark = (ctx) => ctx.power === false;
  const points = [
    P('storeExit', '밖으로 나가기', DOOR, -0.75, 'door', 1.4), P('storeCounter', '계산대 · 이수아', 2.1, -3.3, 'npc', 1.7),
    P('storeShelf', '진열대 살펴보기', -1.1, -4.6, 'shelf', 2.4), P('storeFridge', '음료 냉장고', -1.6, -7.5, 'shelf', 2.2),
    P('storeAtm', 'ATM (자동화기기)', 4.05, -0.62, 'atm', 1.2),
    P('storeCoffee', '셀프 커피머신', 4.0, -1.75, 'look', 1.0, (c) => dark(c) ? '전원이 꺼져 있다. 버튼을 눌러도 아무 반응이 없다.' : '원두커피 셀프 머신. 컵은 계산대에서 받는다.'),
    P('storeSteamer', '찐빵 찜기', 2.5, -2.25, 'look', 0.9, (c) => dark(c) ? '찜기가 식어 있다. 안의 찐빵도 차갑다.' : '김이 서린 유리 너머로 찐빵이 따뜻하게 데워지고 있다.'),
    P('storeRack', '건전지 진열대', 2.4, -4.3, 'look', 0.9, ['AA·AAA 건전지와 휴대폰 충전 케이블이 걸려 있다.', '라디오와 손전등은 건전지가 들어 있어야 쓸 수 있다.']),
    P('storeRamen', '라면 조리대', -4.35, -2.15, 'look', 1.2, (c) => dark(c) ? '온수기가 꺼져 있다. 정전에는 컵라면도 끓는 물이 없으면 못 먹는다.' : '온수기, 전자레인지, 분리수거함. 컵라면 국물은 여기 버린다.'),
    P('storeIce', '아이스크림 냉동고', 2.2, -7.9, 'look', 1.2, (c) => dark(c) ? '냉동고가 멈췄다. 뚜껑 안쪽에 물방울이 맺혀 있다. 열지 않는 게 낫겠다.' : '하드와 콘이 가득하다. 가을인데도 잘 팔린다고 한다.'),
    P('storeWindowBar', '창가 자리', -2.5, -1.4, 'look', 1.4, ['창밖으로 단지 앞 도로가 보인다. 플라스틱 의자가 몇 개 놓여 있다.']),
  ];

  kit.merge(stat);
  let door = 0;
  return {
    id: 'store', group, env: 'interior',
    walk: [{ x0: -5, x1: 5, z0: -9, z1: 0, y: 0 }],
    solids, points,
    spawns: { entry: { x: DOOR, y: 0, z: -1.35, yaw: 0 }, counter: { x: 2.0, y: 0, z: -3.3, yaw: -Math.PI / 2 } },
    npcs: { clerk },
    states: { soldOut: [[], ['water'], ['batteries'], ['water', 'batteries'], ['water', 'batteries', 'food']], power: ['on', 'off'], outside: [null, ...MOODS] },
    setState,
    update(dt, t, ctx = {}) {
      const pl = ctx.player, near = pl && Math.abs(pl.x - DOOR) < 1.3 && pl.z > -1.9;
      door += Math.max(-dt * 2, Math.min(dt * 2, (near ? 1 : 0) - door));
      leaf.rotation.y = door * 1.25;
      view.set(state.outside || moodFor(ctx.env, ctx.env === 'interiorNight' ? 'night' : 'dusk'));
      clerk.update(dt);
      for (const p of extras) if (p.group.visible) p.update(dt);
    },
    anchors: { overview: { x: -4.3, y: 2.3, z: -0.8 }, counter: { x: 1.6, y: 1.6, z: -3.3 }, fridge: { x: -1.6, y: 1.6, z: -6.6 } },
  };
}
