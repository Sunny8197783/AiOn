// 연무마트 1층: sliding entrance doors, carts and baskets, six stocked aisles under hanging section signs, open
// dairy chillers, produce tables, a travelator to the 2nd floor, three checkout lanes and an ATM by the doors.
// Frame: floor x -16..16, z -24..0, entrance on the south wall (z = 0), yaw 0 faces north (-z).
// Sections ('water','food','daily','elec','health') swap to empty shelves with '품절' tags via setState('soldOut').
import * as THREE from 'three';
import { MART_SECTIONS } from '../game/items.js';
import { createGoods, gondola, panels, lamps, windowView, moodFor, npc, solidOf, yawTo, cartsAndBaskets, MOODS } from './commerce-goods.js';

const H = 3.6;
const AISLES = [-14.26, -10.9, -7.5, -4.1, -0.7, 2.7, 6.2];
const FRONT = -7.4, BACK = -21.3;
const GX = [-12.6, -9.2, -5.8, -2.4, 1.0, 4.4];
const LANES = [1.6, 5.0, 8.4];
const SEC = { water: ['#1f6fb2', 0, 'shelfWater'], food: ['#d9682b', 1, 'shelfFood'], daily: ['#2f8a57', 2, 'shelfDaily'], elec: ['#3b3f46', 3, 'shelfElec'], health: ['#1d8a8a', 4, 'shelfHealth'] };
// Gondola faces west (neg) / east (pos): [section, product pool].
const FACES = [
  [['water', ['pack6', 'water5', 'drink', 'can']], ['food', ['ramen', 'cup', 'ramen', 'soup']]],
  [['food', ['rice', 'tuna', 'ham', 'bar', 'cracker']], ['daily', ['tissue', 'wipes', 'towel', 'bags', 'foil']]],
  [['daily', ['stove', 'butane', 'tape', 'rain', 'jug', 'whistle']], ['elec', ['aa', 'flash', 'radio', 'power']]],
  [['elec', ['candle', 'aa', 'power', 'flash', 'radio']], ['health', ['mask', 'aid', 'band', 'sanit']]],
  [['health', ['sanit', 'mask', 'band', 'aid']], ['misc', ['deter', 'soap', 'mix', 'cereal', 'oil']]],
  [['misc', ['chips', 'cookie', 'gim', 'chips']], ['misc', ['cereal', 'mix', 'cookie', 'chips', 'gim']]],
];
const pick = (pool, lvl) => [pool[(lvl * 2) % pool.length], pool[(lvl * 2 + 1) % pool.length], ...(lvl % 2 ? [pool[(lvl + 3) % pool.length]] : [])];

export function buildMart(kit, people, fx) {
  const group = new THREE.Group(); group.name = 'mart';
  const stat = new THREE.Group(), dyn = new THREE.Group(); group.add(stat, dyn);
  const solids = [], r = kit.rng(77);
  const goods = createGoods(kit);
  const M = {
    wall: kit.surface('plaster'), floor: kit.surface('tile', { roughness: 0.22 }), steel: kit.mat('steel'), dark: kit.mat('darkMetal'),
    glass: kit.mat('glass'), white: kit.mat('paint:#ece9e2'), green: kit.mat('paint:#2f8a57'), wood: kit.surface('wood'),
    top: kit.mat('paint:#3a3c40', { roughness: 0.3 }), clad: kit.mat('paint:#b9bcc0', { metalness: 0.6, roughness: 0.35 }), rubber: kit.mat('rubber'),
  };
  const add = (o, solid, pad = 0.04) => { stat.add(o); if (solid) solids.push(solidOf(o, pad)); return o; };
  // Mains power is level-local: real lights sit in `powered`, glowing materials lose their emissive colour.
  // (The env presets still drive the shared 'indoor' group on top of this.)
  const powered = new THREE.Group(), glow = [], BLACK = new THREE.Color(0);
  const onMains = (m) => { glow.push([m, m.emissive.clone()]); return m; };
  const sign = (lines, o, at, lightGroup) => {
    const s = kit.sign(lines, { single: true, ...o }); s.position.set(at.x, at.y, at.z); s.rotation.y = at.ry || 0; s.userData.keep = true;
    if (lightGroup) kit.lit(lightGroup, s.material, o.emissive || 0.6);
    if (lightGroup === 'indoor') onMains(s.material);
    return s;
  };

  // ---- shell: floor, walls with brand stripe, T-bar ceiling with a travelator opening, roof ----
  stat.add(kit.plane(32, 24, M.floor, { x: 0, z: -12 }));
  add(kit.slab(0.3, H, 24.6, M.wall, { x: -16.15, z: -12 }), true); add(kit.slab(0.3, H, 24.6, M.wall, { x: 16.15, z: -12 }), true);
  add(kit.slab(32.6, H, 0.3, M.wall, { x: 0, z: -24.15 }), true);
  for (const [x0, x1] of [[-16, -14], [-4, 16]]) stat.add(kit.slab(x1 - x0, H, 0.3, M.wall, { x: (x0 + x1) / 2, z: 0.15 }));
  stat.add(kit.slab(10, H - 2.6, 0.3, M.wall, { x: -9, y: 2.6, z: 0.15 }));
  solids.push({ x0: -16, x1: 16, y0: 0, y1: H, z0: 0, z1: 0.5, id: 'southWall' });
  for (const [x, z, w, d] of [[-15.99, -12, 0.02, 24], [15.99, -12, 0.02, 24], [0, -23.99, 32, 0.02], [6, -0.01, 20, 0.02]]) stat.add(kit.box(w, 0.25, d, M.green, { x, y: 2.05, z, cast: false }));
  const ceil = new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(256, 256, (g) => { g.fillStyle = '#efeeea'; g.fillRect(0, 0, 256, 256); g.fillStyle = 'rgba(0,0,0,0.04)'; for (let i = 0; i < 900; i++) g.fillRect(r() * 256, r() * 256, 2, 2); g.fillStyle = '#c9c8c3'; for (let i = 0; i < 256; i += 64) { g.fillRect(i, 0, 3, 256); g.fillRect(0, i, 256, 3); } }), { repeat: [1 / 2.4, 1 / 2.4] }), roughness: 0.9 });
  for (const [x0, x1, z0, z1] of [[-16, 13.2, -24, 0], [13.2, 16, -24, -21], [13.2, 16, -14, 0]]) stat.add(kit.slab(x1 - x0, 0.25, z1 - z0, ceil, { x: (x0 + x1) / 2, y: H, z: (z0 + z1) / 2 }));
  stat.add(kit.box(2.8, 0.1, 7, M.dark, { x: 14.6, y: H + 1.6, z: -17.5 }));
  stat.add(kit.slab(33, 0.3, 25, kit.surface('concrete'), { x: 0, y: H + 1.7, z: -12 }));

  // ---- entrance: glazing, sliding doors, mat, outside view, exit sign ----
  for (const [x0, x1] of [[-14, -10.25], [-7.75, -4]]) {
    const pane = kit.box(x1 - x0, 2.6, 0.03, M.glass, { x: (x0 + x1) / 2, z: 0.05, cast: false }); pane.userData.keep = true; stat.add(pane);
    for (const x of [x0, x1]) stat.add(kit.box(0.08, 2.6, 0.12, M.dark, { x, z: 0.05 }));
  }
  stat.add(kit.box(3.2, 0.25, 0.3, M.dark, { x: -9, y: 2.35, z: -0.1 }));
  const leaves = [-1, 1].map((s) => { const m = kit.box(1.3, 2.3, 0.04, M.glass, { x: -9 + s * 0.62, z: -0.05, cast: false }); m.userData.keep = true; dyn.add(m); return m; });
  stat.add(kit.box(2.6, 0.012, 1.8, M.rubber, { x: -9, z: -1.1, cast: false }));
  const view = windowView(kit, { w: 40, h: 14, kind: 'street', seed: 11 });
  view.mesh.position.set(-9, 4.2, 10); view.mesh.rotation.y = Math.PI; dyn.add(view.mesh);
  dyn.add(kit.plane(40, 10, kit.surface('asphalt'), { x: -9, y: -0.02, z: 5 }));
  dyn.add(kit.box(12, 0.15, 3, M.white, { x: -9, y: 3.0, z: 1.6 }));
  dyn.add(sign(['비상구'], { w: 0.6, h: 0.22, bg: '#1f9a4a', fg: '#ffffff', emissive: 1.2 }, { x: -9, y: 2.8, z: -0.3, ry: Math.PI }, 'emergency'));
  dyn.add(sign(['감사합니다 · 안녕히 가세요'], { w: 3, h: 0.32, bg: '#2f8a57', fg: '#ffffff', emissive: 0.4 }, { x: -9, y: 3.25, z: -0.2, ry: Math.PI }, 'indoor'));

  // ---- aisles: west wall water shelving + six double gondolas ----
  const stockRuns = (runs, pools, seed) => runs.forEach((run, i) => {
    const [sec, pool] = pools[run.side < 0 ? 0 : 1] || pools[0];
    goods.stock({ ...run, sec, items: pick(pool, i % 5), seed: seed + i });
  });
  const wall = gondola(kit, { x: -15.97, z: -14.5, len: 15, sides: 'pos', depth: 0.5 });
  add(wall.group, true); stockRuns(wall.runs, [['water', ['pack6', 'pack6', 'water2', 'water2']]], 3);
  GX.forEach((x, i) => { const gd = gondola(kit, { x, z: -14, len: 11 }); add(gd.group, true); stockRuns(gd.runs, FACES[i], 20 + i * 10); });
  const chill = gondola(kit, { x: -4.7, z: -23.95, len: 21.4, axis: 'x', sides: 'pos', depth: 0.62, levels: [0.3, 0.68, 1.04, 1.4], height: 2.0, color: '#f2f2f2' });
  add(chill.group, true); chill.runs.forEach((run, i) => goods.stock({ ...run, sec: 'misc', items: pick(['milk', 'yogurt', 'tofu', 'kimchi', 'soymilk', 'milk'], i), seed: 90 + i }));
  stat.add(kit.box(21.4, 0.14, 0.3, M.white, { x: -4.7, y: 1.98, z: -23.2 }));
  const chillStrip = panels(kit, [{ x: -4.7, y: 1.96, z: -23.08, sx: 21.2 }], { w: 1, d: 0.05, on: 3 }); onMains(chillStrip.material); dyn.add(chillStrip);

  // Pallet of water six-packs at the front with the purchase limit notice.
  add(kit.slab(1.2, 0.14, 1.0, M.wood, { x: -6.6, z: -4.05 }), true);
  goods.stock({ sec: 'water', x: -7.15, z: -3.57, dir: [1, 0], face: [0, 1], len: 1.1, depth: 0.95, y: 0.14, clear: 1.1, items: ['pack6'], rows: 4, stack: 3, fill: 1, edge: false, seed: 5 });
  stat.add(kit.cyl(0.015, 0.015, 1.3, M.steel, { x: -5.85, z: -3.5 }));
  dyn.add(sign(['1인 6병 한정', '생수 2L'], { w: 0.42, h: 0.3, bg: '#ffd23a', fg: '#1d1d1f' }, { x: -5.85, y: 1.45, z: -3.5, ry: -Math.PI / 3 }));

  // ---- checkout lanes ----
  const beltTex = kit.texture(kit.canvas(64, 64, (g) => { g.fillStyle = '#1b1c1e'; g.fillRect(0, 0, 64, 64); g.fillStyle = '#2c2e31'; for (let y = 0; y < 64; y += 8) g.fillRect(0, y, 64, 3); }), { repeat: [2, 4] });
  const beltMat = new THREE.MeshStandardMaterial({ map: beltTex, roughness: 0.75 });
  const cashiers = [];
  LANES.forEach((x, i) => {
    add(kit.rbox(0.7, 0.84, 3.2, 0.03, M.white, { x, z: -3.4 }), true);
    stat.add(kit.box(0.76, 0.03, 3.26, M.top, { x, y: 0.84, z: -3.4 }));
    const belt = kit.plane(0.5, 1.5, beltMat, { x, y: 0.875, z: -4.2 }); dyn.add(belt);
    stat.add(kit.box(0.4, 0.06, 0.35, M.glass, { x, y: 0.87, z: -3.25 }));
    stat.add(kit.cyl(0.02, 0.02, 0.35, M.steel, { x: x + 0.25, y: 0.87, z: -2.9 }));
    const pos = kit.box(0.34, 0.24, 0.04, M.dark, { x: x + 0.25, y: 1.2, z: -2.9, ry: Math.PI / 2 }); stat.add(pos);
    dyn.add(sign([i < 2 ? '계산대' : '마감', `${i + 1}`], { w: 0.22, h: 0.18, bg: '#0d2a3a', fg: '#7fe0c6', emissive: 0.8 }, { x: x + 0.22, y: 1.21, z: -2.9, ry: -Math.PI / 2 }, 'indoor'));
    stat.add(kit.box(0.12, 0.05, 0.2, M.dark, { x: x - 0.3, y: 0.87, z: -2.7 }));
    stat.add(kit.cyl(0.03, 0.03, 2.2, M.steel, { x: x - 0.32, z: -5.05 }));
    dyn.add(sign([`${i + 1}`], { w: 0.34, h: 0.34, bg: i < 2 ? '#2f8a57' : '#6b6e72', fg: '#ffffff', emissive: i < 2 ? 1.1 : 0.15 }, { x: x - 0.32, y: 2.38, z: -5.08, ry: Math.PI }, 'indoor'));
    const rack = gondola(kit, { x: x - 0.55, z: -3.0, len: 0.6, sides: 'neg', depth: 0.18, levels: [0.9, 1.15, 1.4], height: 1.6, color: '#c8202f' });
    add(rack.group, true); rack.runs.forEach((run, k) => goods.stock({ ...run, sec: 'misc', items: [['gum', 'vita'][k % 2]], seed: 140 + i * 3 + k, edge: false }));
    if (i < 2) cashiers.push(npc(people, 'clerk', 41 + i, { x: x + 0.75, z: -3.3, ry: Math.PI / 2, anim: 'idle' }));
  });
  dyn.add(sign(['이 계산대는 마감했습니다', '옆 계산대를 이용해 주세요'], { w: 0.66, h: 0.3, bg: '#ffffff', fg: '#333333', border: '#c8202f' }, { x: 7.62, y: 0.8, z: -5.13, ry: Math.PI }));
  for (const x of [7.25, 7.98]) stat.add(kit.cyl(0.025, 0.03, 0.95, M.steel, { x, z: -5.1 }));
  solids.push({ x0: 7.2, x1: 8.05, y0: 0, y1: 1, z0: -5.2, z1: -5.0 });
  cashiers.forEach((p) => dyn.add(p.group));

  // ---- ATM by the doors ----
  const atm = new THREE.Group();
  atm.add(kit.rbox(0.9, 1.75, 0.75, 0.04, kit.mat('paint:#d7dbe0', { metalness: 0.5, roughness: 0.35 }), { x: -2.6, z: -0.42 }));
  atm.add(kit.box(0.9, 0.3, 0.8, M.green, { x: -2.6, y: 1.75, z: -0.42 }));
  atm.add(kit.box(0.3, 0.12, 0.2, M.dark, { x: -2.6, y: 0.95, z: -0.85, rx: -0.4 }));
  add(atm, true);
  dyn.add(sign(['24시 자동화기기', '현금 인출 · 입금 · 이체', '카드를 넣어 주세요'], { w: 0.5, h: 0.36, bg: '#0f3d6e', fg: '#e8f4ff', emissive: 0.9 }, { x: -2.6, y: 1.3, z: -0.81, ry: Math.PI }, 'indoor'));
  dyn.add(sign(['ATM'], { w: 0.6, h: 0.22, bg: '#2f8a57', fg: '#ffffff', emissive: 0.7 }, { x: -2.6, y: 1.9, z: -0.83, ry: Math.PI }, 'indoor'));

  // ---- carts, baskets ----
  const carts = [];
  for (const x of [-15.3, -14.6]) for (let k = 0; k < 9; k++) carts.push({ x, z: -1.2 - k * 0.3, ry: 0 });
  const baskets = [];
  for (const [x, n] of [[-3.75, 7], [-3.25, 5]]) for (let k = 0; k < n; k++) baskets.push({ x, y: 0.12 + k * 0.06, z: -1.3 });
  add(cartsAndBaskets(kit, carts, baskets));
  solids.push({ x0: -15.7, x1: -14.2, y0: 0, y1: 1.1, z0: -4.4, z1: -0.8 });
  add(kit.box(1.1, 0.12, 0.45, M.steel, { x: -3.5, z: -1.3 }), true);

  // ---- produce tables (east) ----
  const produce = [], col = [];
  const KINDS = [['#b8262a', 0.042], ['#f08a1c', 0.04], ['#c79a5a', 0.038], ['#a07848', 0.036], ['#8fbf5a', 0.09], ['#d8c06a', 0.045], ['#e9e6d8', 0.05]];
  for (const tx of [8.3, 11.5]) for (const tz of [-11.8, -17.2]) {
    add(kit.slab(1.8, 0.72, 4.4, M.wood, { x: tx, z: tz }), true);
    for (let cx = 0; cx < 3; cx++) for (let cz = 0; cz < 8; cz++) {
      const x = tx - 0.6 + cx * 0.6, z = tz - 1.93 + cz * 0.55, [c, s] = KINDS[(r() * KINDS.length) | 0];
      stat.add(kit.slab(0.56, 0.12, 0.5, M.wood, { x, y: 0.72, z }));
      const n = s > 0.08 ? 4 : 14;
      for (let k = 0; k < n; k++) { produce.push({ x: x + (r() - 0.5) * 0.4, y: 0.84 + s * 0.8 + (k > n * 0.6 ? s * 1.2 : 0), z: z + (r() - 0.5) * 0.36, s, sy: s * (c === '#e9e6d8' ? 3 : 0.92), rx: r() * 3 }); col.push(c); }
    }
  }
  const prodMesh = kit.instanced(new THREE.IcosahedronGeometry(1, 1), new THREE.MeshStandardMaterial({ roughness: 0.55 }), produce, { cast: false });
  const tc = new THREE.Color(); col.forEach((c, i) => prodMesh.setColorAt(i, tc.set(c).multiplyScalar(0.85 + r() * 0.3))); dyn.add(prodMesh);

  // ---- travelator to the 2nd floor (east wall) ----
  const run = 13.4, ang = Math.atan2(H, run), L = run / Math.cos(ang) + 1.5;
  const prism = new THREE.Shape([new THREE.Vector2(0, 0), new THREE.Vector2(run + 2, H + 0.54), new THREE.Vector2(run + 2, 0)]);
  const pm = new THREE.Mesh(new THREE.ExtrudeGeometry(prism, { depth: 2.2, bevelEnabled: false }).rotateY(Math.PI / 2), M.clad);
  pm.position.set(13.3, 0, -6.5); pm.castShadow = pm.receiveShadow = true; stat.add(pm);
  const tread = kit.texture(kit.canvas(64, 64, (g) => { g.fillStyle = '#8d9196'; g.fillRect(0, 0, 64, 64); g.fillStyle = '#5d6166'; for (let x = 0; x < 64; x += 4) g.fillRect(x, 0, 2, 64); g.fillStyle = '#3a3d40'; g.fillRect(0, 0, 64, 2); }), { repeat: [2.8, 2.5] });
  const tilt = new THREE.Group(); tilt.position.set(14.4, 0.02, -6.5); tilt.rotation.x = ang; dyn.add(tilt);
  tilt.add(kit.plane(1.05, L, new THREE.MeshStandardMaterial({ map: tread, metalness: 0.7, roughness: 0.4 }), { z: -L / 2 }));
  for (const s of [-1, 1]) {
    tilt.add(kit.box(0.08, 0.06, L, M.rubber, { x: s * 0.62, y: 0.92, z: -L / 2 }));
    const gl = kit.box(0.02, 0.86, L, M.glass, { x: s * 0.62, y: 0.05, z: -L / 2, cast: false }); gl.userData.keep = true; tilt.add(gl);
  }
  stat.add(kit.box(1.4, 0.02, 0.6, kit.mat('paint:#c9a227', { metalness: 0.6 }), { x: 14.4, z: -6.1 }));
  solids.push({ x0: 13.2, x1: 15.6, y0: 0, y1: H, z0: -21, z1: -6.4, id: 'travelator' });
  dyn.add(sign(['2층 · 주방 · 문구 · 의류', '카트는 바퀴를 고정해 주세요'], { w: 1.4, h: 0.5, bg: '#1d1d1f', fg: '#ffffff', emissive: 0.5 }, { x: 14.4, y: 2.5, z: -5.95 }, 'indoor'));

  // ---- signs: hanging section boards, posters ----
  const hang = (lines, color, x, z, w = 1.9) => {
    for (const ry of [0, Math.PI]) dyn.add(sign(lines, { w, h: 0.42, bg: color, fg: '#ffffff', emissive: 0.35 }, { x, y: 2.75, z: z + (ry ? -0.01 : 0.01), ry }, 'indoor'));
    for (const s of [-1, 1]) stat.add(kit.cyl(0.004, 0.004, H - 2.96, M.steel, { x: x + s * w * 0.4, y: 2.96, z, cast: false }));
  };
  for (const [sec, [color, a]] of Object.entries(SEC)) hang([MART_SECTIONS[sec].label], color, AISLES[a], -8.4);
  hang(['세제 · 조미료'], '#6b5b95', AISLES[5], -8.4); hang(['과자 · 간식'], '#c0436e', AISLES[6], -8.4);
  hang(['채소 · 과일'], '#5a8f3c', 9.9, -14.5, 2.4); hang(['신선 · 유제품'], '#2b6cb0', -4.7, -21.9, 2.6);
  const posters = [
    [['이번 주 행사', '라면 · 즉석밥 1+1'], '#c4161c', -15.98, 2.75, -10, Math.PI / 2], [['연무마트 포인트', '적립 2배 데이'], '#2f8a57', -15.98, 2.75, -18, Math.PI / 2],
    [['장바구니를 이용하면', '비닐봉투 값을 아껴요'], '#1f6fb2', 15.98, 2.6, -3.2, -Math.PI / 2], [['오늘의 특가', '제철 사과 1봉 7,900원'], '#e8481c', 3, 2.75, -0.02, Math.PI],
  ];
  for (const [lines, bg, x, y, z, ry] of posters) dyn.add(sign(lines, { w: 1.2, h: 0.85, bg, fg: '#ffffff' }, { x, y, z, ry }));

  // ---- goods, sold-out notices ----
  const built = goods.build(); group.add(built.group);
  const sections = built.sections;
  for (const [sec, [, a]] of Object.entries(SEC)) {
    sections[sec] ||= { full: new THREE.Group(), out: new THREE.Group() };
    for (const gx of [GX[a] ?? GX[0], GX[a - 1]].filter((v) => v !== undefined)) {
      const s = sign(['품절', '다음 입고 일정 미정'], { w: 0.4, h: 0.3, bg: '#ffffff', fg: '#c4161c', border: '#c4161c' }, { x: gx + (gx === GX[a] ? -0.22 : 0.22), y: 1.45, z: -8.45 });
      sections[sec].out.add(s);
    }
  }

  // ---- lights ----
  const fixtures = [];
  for (const x of AISLES.slice(0, 6)) for (const z of [-9.5, -12, -14.5, -17, -19.5]) fixtures.push({ x, y: H - 0.03, z, ry: Math.PI / 2 });
  for (let x = -14; x <= 13; x += 2.4) for (const z of [-2.2, -5.4, -22.2]) fixtures.push({ x, y: H - 0.03, z });
  for (const x of [8.3, 11.5]) for (const z of [-10, -13, -16, -19]) fixtures.push({ x, y: H - 0.03, z });
  const ceilPanels = panels(kit, fixtures); onMains(ceilPanels.material); dyn.add(ceilPanels); dyn.add(powered);
  powered.add(lamps(kit, [-12, -4, 4, 11].flatMap((x) => [-5.5, -16].map((z) => ({ x, y: H - 0.35, z }))), { intensity: 13 }));
  const emerg = [[-9, 3.1, -0.6], [-4.7, 3.1, -23.3], [12, 3.1, -1]].map(([x, y, z]) => {
    const l = new THREE.PointLight(0xf4f7ff, 0, 12, 2); l.position.set(x, y, z); dyn.add(l);
    const m = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xffffff, emissiveIntensity: 0 });
    dyn.add(kit.box(0.35, 0.08, 0.12, m, { x, y: y + 0.1, z, cast: false })); return { l, m };
  });

  // ---- people: shoppers wander the aisles, a queue forms when busy ----
  const q = kit.quality === 'low' ? 0.5 : kit.quality === 'medium' ? 0.75 : 1;
  const nNormal = Math.round(6 * q), nBusy = Math.round(12 * q);
  const shoppers = Array.from({ length: nBusy }, (_, i) => {
    const cart = i % 3 === 0, x = AISLES[(r() * AISLES.length) | 0], z = r() < 0.5 ? FRONT : BACK;
    const p = npc(people, 'crowd', 300 + i, { x, z, ry: r() * 6, props: [cart ? 'cart' : 'basket'] });
    // 'walk' for everyone: the 'walker' anim would auto-show a rollator; the cart prop alone sets the grip.
    const s = { p, cart, walk: 'walk', speed: (cart ? 0.75 : 0.85) + r() * 0.35, off: (r() - 0.5) * 0.6, path: [], wait: r() * 3, blocked: false };
    p.setSpeed(s.speed); p.setAnim('idle'); dyn.add(p.group); return s;
  });
  const queue = [[4.25, -3.8, -Math.PI / 2], [4.25, -5.4, Math.PI], [4.3, -6.0, Math.PI], [4.2, -6.6, Math.PI], [0.85, -2.2, Math.PI], [0.75, -5.8, Math.PI]]
    .slice(0, Math.round(6 * q)).map(([x, z, ry], i) => { const p = npc(people, 'crowd', 400 + i, { x, z, ry, anim: i === 2 ? 'phone' : 'idle', props: ['basket'] }); dyn.add(p.group); return p; });
  function plan(s) {
    const g = s.p.group, atFront = Math.abs(g.position.z - FRONT) < Math.abs(g.position.z - BACK);
    const ax = AISLES[(r() * AISLES.length) | 0] + s.off, z0 = atFront ? FRONT : BACK, z1 = atFront ? BACK : FRONT;
    const zb = -9.5 - r() * 9, side = ax > AISLES[6] - 0.5 ? 1 : r() < 0.5 ? -1 : 1;
    s.path = [{ x: ax, z: z0 }, { x: ax, z: zb, wait: 2 + r() * 5, face: yawTo(side, 0) }, { x: ax, z: z1 }];
  }
  const turn = (a, b) => Math.atan2(Math.sin(b - a), Math.cos(b - a));
  function stepShopper(s, dt, pl) {
    const g = s.p.group;
    if (s.wait > 0) { s.wait -= dt; if (s.wait <= 0) s.p.setAnim(s.walk); else if (s.face !== undefined) g.rotation.y += turn(g.rotation.y, s.face) * Math.min(1, dt * 4); return; }
    if (!s.path.length) plan(s);
    const t = s.path[0], dx = t.x - g.position.x, dz = t.z - g.position.z, d = Math.hypot(dx, dz);
    if (d < 0.05) { s.path.shift(); if (t.wait) { s.wait = t.wait; s.face = t.face; s.p.setAnim('idle'); } else s.face = undefined; return; }
    if (pl) {
      const px = pl.x - g.position.x, pz = pl.z - g.position.z;
      const block = Math.hypot(px, pz) < 1.15 && px * dx + pz * dz > 0;
      if (block !== s.blocked) { s.blocked = block; s.p.setAnim(block ? 'idle' : s.walk); }
      if (block) return;
    }
    const k = Math.min(d, s.speed * dt) / d; g.position.x += dx * k; g.position.z += dz * k;
    g.rotation.y += turn(g.rotation.y, yawTo(dx, dz)) * Math.min(1, dt * 7);
  }

  // ---- state ----
  const state = { crowd: 'normal', soldOut: [], power: 'on', outside: null };
  let doorOpen = 0;
  function applyCrowd() {
    const busy = state.crowd === 'busy';
    shoppers.forEach((s, i) => { s.p.group.visible = i < (busy ? nBusy : nNormal); if (s.p.group.visible && !s.wait && !s.path.length) s.p.setAnim(s.walk); });
    queue.forEach((p) => { p.group.visible = busy; });
  }
  applyCrowd();
  function setState(key, v) {
    if (key === 'crowd') { if (!['normal', 'busy'].includes(v)) return void console.error('[mart] crowd must be normal|busy', v); state.crowd = v; applyCrowd(); }
    else if (key === 'soldOut') {
      const list = Array.isArray(v) ? v : [];
      for (const id of list) if (!SEC[id]) console.error('[mart] unknown soldOut section', id);
      state.soldOut = list.filter((id) => SEC[id]);
      for (const sec of Object.keys(SEC)) { const out = state.soldOut.includes(sec); sections[sec].full.visible = !out; sections[sec].out.visible = out; }
    } else if (key === 'power') {
      if (!['on', 'off'].includes(v)) return void console.error('[mart] power must be on|off', v);
      state.power = v; const on = v === 'on';
      powered.traverse((o) => { if (o.isLight) kit.gateLit(o, on); }); // dim, never hide: light count stays fixed
      for (const [m, c] of glow) m.emissive.copy(on ? c : BLACK);
      for (const e of emerg) { e.l.intensity = on ? 0 : 4; e.m.emissiveIntensity = on ? 0 : 2.5; }
    } else if (key === 'outside') { if (v !== null && !MOODS.includes(v)) return void console.error('[mart] outside must be', MOODS, v); state.outside = v; }
    else console.error('[mart] unknown state', key, v);
  }

  const P = (id, label, x, z, kind, rad = 1.6) => ({ id, label, x, y: 0, z, r: rad, kind });
  const points = [
    P('martExit', '출입구', -9, -1.0, 'door', 1.8), P('checkout', '계산대', 0.75, -3.6, 'counter', 1.8), P('atm', 'ATM', -2.6, -1.4, 'atm', 1.4),
    ...Object.entries(SEC).map(([sec, [, a, id]]) => P(id, `${MART_SECTIONS[sec].label} 진열대`, AISLES[a], -14, 'shelf', 3)),
  ];

  kit.merge(stat);
  return {
    id: 'mart', group, env: 'interior',
    walk: [{ x0: -16, x1: 16, z0: -24, z1: 0, y: 0 }],
    solids, points,
    spawns: { entry: { x: -9, y: 0, z: -2.4, yaw: 0 } },
    npcs: { cashier: cashiers[0], cashier2: cashiers[1] },
    states: { crowd: ['normal', 'busy'], soldOut: [[], ['water'], ['water', 'elec'], ['water', 'food', 'daily', 'elec', 'health']], power: ['on', 'off'], outside: [null, ...MOODS] },
    setState,
    update(dt, t, ctx = {}) {
      const pl = ctx.player, on = state.power === 'on';
      if (on) {
        const near = pl && Math.abs(pl.x + 9) < 2.4 && pl.z > -3.6;
        doorOpen += Math.max(-dt * 1.4, Math.min(dt * 1.4, (near ? 1 : 0) - doorOpen));
        leaves.forEach((m, i) => { m.position.x = -9 + (i ? 1 : -1) * (0.62 + doorOpen * 1.2); });
        beltTex.offset.y -= dt * 0.25; tread.offset.y += dt * 1.2;
      }
      view.set(state.outside || moodFor(ctx.env, 'day'));
      for (const s of shoppers) if (s.p.group.visible) { stepShopper(s, dt, pl); s.p.update(dt); }
      for (const p of queue) if (p.group.visible) p.update(dt);
      for (const p of cashiers) p.update(dt);
    },
    anchors: { overview: { x: 12.5, y: 3.0, z: -2.5 }, entrance: { x: -9, y: 1.6, z: -3.2 }, waterAisle: { x: -14.26, y: 1.6, z: -7.5 }, checkout: { x: 0.75, y: 1.6, z: -6.2 } },
  };
}
