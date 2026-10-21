// The car park as a civil-defence shelter: rows of mats (돗자리·은박매트) with bags, blankets, bottles and shoes
// left at the edge, a reception table with the roster clipboard and a battery lantern, a wheeled notice board,
// the shared radio table, supply pallets, recycling bins, a cloth banner, and the cars still parked in the east.
import { ROOM, GRID, STAIR } from './shelter-shell.js';

export const MAT_COLS = Array.from({ length: 13 }, (_, i) => -20.4 + 2.2 * i);
export const MAT_ROWS = [-3.9, -1.55, 1.55, 3.9];
export const SOUTH_MATS = { cols: [-20.6, -18.4, -16.2, -14, -11.8, -9.6], rows: [12.3, 14.5] };
export const SPOT = { x: MAT_COLS[9], z: MAT_ROWS[3] };          // the free mat the player can take
export const CHAIRS = { reception: [[-0.45, 0.7, 0], [0.5, 0.75, 0.2]], radio: [[-0.9, 0.2, -Math.PI / 2], [0.9, 0.25, Math.PI / 2], [0.1, 0.75, 0]] };   // [dx, dz, ry] from the table
export const RECEPTION = { x: 2.0, z: -8.6 }, BOARD = { x: -3.0, z: -13.6 }, RADIO = { x: -5.5, z: 7.4 }, PALLETS = { x: -19.3, z: -13.2 };

export function buildCamp(kit, T) {
  const { THREE } = kit;
  const g = new THREE.Group(); g.name = 'shelter-camp';
  const live = new THREE.Group(); live.name = 'shelter-camp-live';
  const add = (m, parent = g) => { parent.add(m); return m; };
  const r = kit.rng(1987);
  const C = (hex) => new THREE.Color(hex);
  const M4 = (x, y, z, ry = 0, sx = 1, sy = 1, sz = 1, rx = 0) => new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), new THREE.Quaternion().setFromEuler(new THREE.Euler(rx, ry, 0)), new THREE.Vector3(sx, sy, sz));
  function inst(geo, mat, items, cast = true) { // items: [{ m: Matrix4, c?: Color }]
    const im = new THREE.InstancedMesh(geo, mat, Math.max(1, items.length));
    items.forEach((it, i) => { im.setMatrixAt(i, it.m); if (it.c) im.setColorAt(i, it.c); });
    im.count = items.length; im.castShadow = cast; im.receiveShadow = true;
    im.instanceMatrix.needsUpdate = true; if (im.instanceColor) im.instanceColor.needsUpdate = true;
    im.computeBoundingSphere();
    return add(im);
  }
  const M = {
    woven: new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(128, 128, (c) => { c.fillStyle = '#ffffff'; c.fillRect(0, 0, 128, 128); for (let y = 0; y < 128; y += 4) { c.fillStyle = y % 16 < 8 ? 'rgba(0,0,0,0.12)' : 'rgba(255,255,255,0.1)'; c.fillRect(0, y, 128, 2); } c.fillStyle = 'rgba(0,0,0,0.25)'; c.fillRect(0, 0, 128, 3); c.fillRect(0, 125, 128, 3); }), { repeat: [1, 1] }), roughness: 0.72 }),
    foil: new THREE.MeshStandardMaterial({ color: 0xc8ccd0, metalness: 0.8, roughness: 0.3, normalMap: kit.texture(kit.normalFrom(kit.canvas(128, 128, (c) => { c.fillStyle = '#808080'; c.fillRect(0, 0, 128, 128); for (let i = 0; i < 90; i++) { c.strokeStyle = r() < 0.5 ? '#a0a0a0' : '#5a5a5a'; c.lineWidth = 2 + r() * 3; c.beginPath(); c.moveTo(r() * 128, r() * 128); c.lineTo(r() * 128, r() * 128); c.stroke(); } }), 3), { color: false }) }),
    fabric: new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.95 }), plastic: new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.45 }),
    bottle: new THREE.MeshStandardMaterial({ color: 0xcfe3f0, roughness: 0.15, metalness: 0.1, transparent: true, opacity: 0.75 }),
    tableTop: kit.mat('paint:#ecebe6', { roughness: 0.5 }), legs: kit.mat('steel'), seat: kit.mat('paint:#3a3f46', { roughness: 0.6 }),
    wood: kit.mat('paint:#a07a4c', { roughness: 0.85 }), tyre: kit.mat('rubber'), rim: kit.mat('metal', { roughness: 0.3 }),
    glass: kit.mat('darkGlass'), lens: kit.mat('paint:#dfe6ea', { roughness: 0.1, metalness: 0.4 }), tail: kit.mat('paint:#7c1616', { roughness: 0.25 }),
  };

  // ---- mats and what people brought ----
  const mats = [], seats = [];
  const woven = [], foil = [], bags = [], blankets = [], bottles = [], shoes = [], pillows = [];
  const bagCols = ['#2e3a4f', '#5b2d2d', '#3c4a3a', '#1d1f24', '#7a6a55', '#28516e', '#8a3b52'].map(C);
  const blanketCols = ['#c9b79c', '#8d9bb0', '#b0646a', '#d9d4c7', '#6e7f62', '#e0c6a8'].map(C);
  const matCols = ['#3d6fb0', '#4f8a5c', '#6f86a0', '#b7a37a', '#9e4b45', '#58708a'].map(C);
  const shoeCols = ['#202226', '#e8e6e0', '#5a4636', '#2d3a55', '#8b8f96'].map(C);
  const all = [...MAT_COLS.flatMap((x) => MAT_ROWS.map((z) => ({ x, z }))), ...SOUTH_MATS.cols.flatMap((x) => SOUTH_MATS.rows.map((z) => ({ x, z })))];
  for (const m of all) {
    const isSpot = m.x === SPOT.x && m.z === SPOT.z;
    const kind = r() < 0.3 ? 'foil' : 'woven', ry = (r() - 0.5) * 0.08;
    (kind === 'foil' ? foil : woven).push({ m: M4(m.x, 0.006, m.z, ry), c: kind === 'woven' ? matCols[(r() * matCols.length) | 0] : undefined });
    const used = !isSpot && r() < 0.66;
    mats.push({ ...m, used, spot: isSpot });
    if (!used) continue;
    const edge = m.z < 0 ? (m.z < -2.5 ? -1 : 1) : (m.z > 2.5 ? 1 : -1);   // mat side toward the nearest walkway
    const n = r() < 0.45 ? 2 : 1;
    for (let k = 0; k < n; k++) seats.push({ x: m.x + (n === 2 ? (k ? 0.42 : -0.42) : (r() - 0.5) * 0.3), z: m.z - edge * (0.1 + r() * 0.3), yaw: (edge > 0 ? Math.PI : 0) + (r() - 0.5) * 1.6, mat: mats.length - 1 });
    const cx = m.x + (r() < 0.5 ? -0.62 : 0.62), cz = m.z - edge * 0.72;
    bags.push({ m: M4(cx, 0.012, cz, r() * 6, 1, 0.8 + r() * 0.4, 1), c: bagCols[(r() * bagCols.length) | 0] });
    if (r() < 0.8) blankets.push({ m: M4(m.x - (cx - m.x) * 0.9, 0.012, m.z - edge * 0.6, (r() - 0.5) * 0.4, 1, 0.6 + r() * 0.8, 1), c: blanketCols[(r() * blanketCols.length) | 0] });
    for (let b = 0, nb = 1 + (r() * 3 | 0); b < nb; b++) bottles.push({ m: M4(cx + 0.25 + b * 0.09, 0.012, cz + 0.12, 0, 1, b === 2 ? 0.55 : 1, 1) });
    if (r() < 0.35) pillows.push({ m: M4(m.x + (r() - 0.5) * 0.8, 0.012, m.z - edge * 0.65, r()), c: blanketCols[(r() * blanketCols.length) | 0] });
    for (let k = 0; k < n; k++) { // shoes paired at the walkway edge, toes toward the mat
      const sx = m.x + (k - (n - 1) / 2) * 0.55, sz = m.z + edge * 1.12, col = shoeCols[(r() * shoeCols.length) | 0];
      for (const d of [-0.07, 0.07]) shoes.push({ m: M4(sx + d, 0, sz, (edge > 0 ? Math.PI : 0) + (r() - 0.5) * 0.3), c: col });
    }
  }
  inst(new THREE.BoxGeometry(1.8, 0.012, 2.0), M.woven, woven, false);
  inst(new THREE.BoxGeometry(1.8, 0.008, 2.0), M.foil, foil, false);
  inst(new THREE.BoxGeometry(0.32, 0.42, 0.2).translate(0, 0.21, 0), M.fabric, bags);
  inst(new THREE.BoxGeometry(0.62, 0.1, 0.42).translate(0, 0.05, 0), M.fabric, blankets);
  inst(new THREE.CylinderGeometry(0.034, 0.036, 0.22, 10).translate(0, 0.11, 0), M.bottle, bottles, false);
  inst(new THREE.BoxGeometry(0.42, 0.1, 0.28).translate(0, 0.05, 0), M.fabric, pillows);
  inst(new THREE.BoxGeometry(0.1, 0.08, 0.27).translate(0, 0.04, 0), M.plastic, shoes, false);

  // ---- folding tables and chairs ----
  function table(x, z, ry = 0, w = 1.8, d = 0.6) {
    const t = new THREE.Group(); t.position.set(x, 0, z); t.rotation.y = ry;
    t.add(kit.box(w, 0.04, d, M.tableTop, { y: 0.7 }));
    for (const sx of [-1, 1]) for (const sz of [-1, 1]) t.add(kit.cyl(0.016, 0.016, 0.7, M.legs, { x: sx * (w / 2 - 0.08), z: sz * (d / 2 - 0.06) }, 6));
    t.add(kit.box(w - 0.2, 0.03, 0.03, M.legs, { y: 0.15 }));
    return add(t);
  }
  function chair(x, z, ry = 0) {
    const c = new THREE.Group(); c.position.set(x, 0, z); c.rotation.y = ry;
    c.add(kit.box(0.42, 0.03, 0.4, M.seat, { y: 0.45 })); c.add(kit.box(0.42, 0.32, 0.03, M.seat, { y: 0.62, z: 0.2 }));
    for (const sx of [-0.19, 0.19]) { c.add(kit.box(0.025, 0.95, 0.025, M.legs, { x: sx, z: 0.21, rx: 0.12 })); c.add(kit.box(0.025, 0.5, 0.025, M.legs, { x: sx, z: -0.17, rx: -0.2 })); }
    return add(c);
  }

  // ---- reception: roster clipboard, pens, masks, sanitiser, battery lantern ----
  table(RECEPTION.x, RECEPTION.z);
  for (const [dx, dz, ry] of CHAIRS.reception) chair(RECEPTION.x + dx, RECEPTION.z + dz, ry);   // staff sit south, facing the queue
  const rx = RECEPTION.x, rz = RECEPTION.z;
  add(kit.box(0.24, 0.012, 0.33, M.wood, { x: rx - 0.35, y: 0.74, z: rz - 0.05, ry: 0.1 }));
  const sheet = add(new THREE.Mesh(new THREE.PlaneGeometry(0.21, 0.28), new THREE.MeshStandardMaterial({ map: T.rosterTex(), roughness: 0.8 })));
  sheet.rotation.set(-Math.PI / 2, 0, Math.PI + 0.1); sheet.position.set(rx - 0.35, 0.753, rz - 0.06);
  add(kit.box(0.08, 0.02, 0.03, M.legs, { x: rx - 0.35, y: 0.75, z: rz - 0.2, ry: 0.1 }));
  for (const d of [0, 0.03]) add(kit.cyl(0.005, 0.005, 0.14, kit.mat('paint:#1d3f8f'), { x: rx - 0.1 + d, y: 0.73, z: rz - 0.1, rz: Math.PI / 2, ry: 0.3 }, 6));
  add(kit.box(0.2, 0.1, 0.11, kit.mat('paint:#e9eef3'), { x: rx + 0.25, y: 0.72, z: rz + 0.12 }));
  add(kit.cyl(0.035, 0.035, 0.16, kit.mat('paint:#cfe8f5', { roughness: 0.2 }), { x: rx + 0.5, y: 0.72, z: rz }, 10));
  add(kit.cyl(0.008, 0.008, 0.06, kit.mat('plastic'), { x: rx + 0.5, y: 0.88, z: rz }, 6));
  const lantern = kit.lit('emergency', new THREE.MeshStandardMaterial({ color: 0xfff3d6, emissive: 0xffd9a0, roughness: 0.3 }), 2.4);
  add(kit.cyl(0.06, 0.07, 0.04, M.seat, { x: rx + 0.72, y: 0.72, z: rz - 0.12 }, 12));
  add(kit.cyl(0.055, 0.055, 0.13, lantern, { x: rx + 0.72, y: 0.76, z: rz - 0.12 }, 12));
  add(kit.cyl(0.06, 0.06, 0.03, M.seat, { x: rx + 0.72, y: 0.89, z: rz - 0.12 }, 12));
  const desk = add(kit.sign(['접수 · 대피자 명부 작성'], { w: 0.9, h: 0.22, bg: '#1f4e8c', single: true })); desk.position.set(rx, 0.6, rz - 0.31); desk.rotation.y = Math.PI;
  const af = add(kit.sign(['대피소 접수', '이쪽입니다'], { w: 0.6, h: 0.8, bg: '#f7f5ee', fg: '#1f4e8c', border: '#1f4e8c', single: true }));
  af.position.set(rx - 1.6, 0.75, rz - 0.9); af.rotation.set(-0.12, Math.PI, 0);
  add(kit.box(0.62, 0.02, 0.4, M.legs, { x: rx - 1.6, y: 0.0, z: rz - 0.75 }));
  const lanternLight = new THREE.PointLight(0xffc98a, 1.2, 4, 2); lanternLight.position.set(rx + 0.72, 1.0, rz - 0.12); add(lanternLight, live);

  // ---- wheeled notice board: aluminium frame on castors, sheets drawn from text the director passes ----
  const bx = BOARD.x, bz = BOARD.z;
  const boardMat = new THREE.MeshStandardMaterial({ map: T.drawBoard(null), roughness: 0.55 });
  const face = add(new THREE.Mesh(new THREE.PlaneGeometry(1.8, 1.125), boardMat)); face.position.set(bx, 1.45, bz + 0.03);
  add(kit.box(1.86, 1.19, 0.04, M.rim, { x: bx, y: 0.885, z: bz }));
  for (const sx of [-0.88, 0.88]) { add(kit.box(0.04, 1.5, 0.04, M.rim, { x: bx + sx, y: 0.1, z: bz })); add(kit.box(0.04, 0.03, 0.6, M.rim, { x: bx + sx, y: 0.08, z: bz })); for (const sz of [-0.27, 0.27]) add(kit.cyl(0.035, 0.035, 0.07, M.tyre, { x: bx + sx, y: 0.008, z: bz + sz, rz: Math.PI / 2 }, 8)); }

  // ---- the shared radio on its table, chairs around it ----
  table(RADIO.x, RADIO.z, 0, 1.2, 0.6);
  for (const [dx, dz, ry] of CHAIRS.radio) chair(RADIO.x + dx, RADIO.z + dz, ry);
  const radio = new THREE.Group(); radio.position.set(RADIO.x, 0.72, RADIO.z - 0.05); radio.rotation.y = 0.15; add(radio, live);
  radio.add(kit.rbox(0.34, 0.2, 0.11, 0.02, kit.mat('paint:#3b3e42', { roughness: 0.5 })));
  const grille = new THREE.Mesh(new THREE.CircleGeometry(0.06, 20), kit.mat('paint:#1d1f22', { roughness: 0.9 })); grille.position.set(-0.08, 0.1, -0.056); grille.rotation.y = Math.PI; radio.add(grille);
  const dial = new THREE.MeshStandardMaterial({ color: 0x2a2015, emissive: 0xffb24a, emissiveIntensity: 0, roughness: 0.3 });
  const disp = new THREE.Mesh(new THREE.PlaneGeometry(0.13, 0.04), dial); disp.position.set(0.07, 0.14, -0.056); disp.rotation.y = Math.PI; radio.add(disp);
  for (const dx of [0.04, 0.11]) radio.add(kit.cyl(0.015, 0.015, 0.02, M.legs, { x: dx, y: 0.06, z: -0.065, rx: Math.PI / 2 }, 10));
  const ant = new THREE.Mesh(new THREE.CylinderGeometry(0.003, 0.004, 0.55, 5), M.rim); ant.position.set(0.13, 0.45, 0.02); ant.rotation.z = -0.35; radio.add(ant);
  radio.add(kit.box(0.24, 0.02, 0.025, M.legs, { y: 0.21, z: 0.02 }));
  add(kit.box(0.09, 0.03, 0.06, kit.mat('paint:#c8a640'), { x: RADIO.x + 0.3, y: 0.72, z: RADIO.z + 0.1 }));
  add(kit.box(0.2, 0.015, 0.28, kit.mat('paint:#f2efe6'), { x: RADIO.x - 0.35, y: 0.72, z: RADIO.z + 0.08, ry: -0.2 }));
  add(kit.cyl(0.04, 0.04, 0.26, kit.mat('paint:#7c8a95', { metalness: 0.6, roughness: 0.3 }), { x: RADIO.x + 0.45, y: 0.72, z: RADIO.z - 0.12 }, 12));

  // ---- supplies: 2 L six-packs on pallets, cartons of 500 mL water and blankets ----
  const packMat = new THREE.MeshStandardMaterial({ map: T.waterPackTex(), roughness: 0.3, metalness: 0.05 });
  const cartonMats = ['생수 500mL × 20', '담요', '비상식량'].map((t) => new THREE.MeshStandardMaterial({ map: T.cartonTex(t), roughness: 0.9 }));
  const pallet = (x, z) => { for (const dz of [-0.45, 0, 0.45]) add(kit.box(1.1, 0.1, 0.12, M.wood, { x, z: z + dz })); add(kit.box(1.1, 0.03, 1.1, M.wood, { x, y: 0.1, z })); };
  const packs = [];
  for (const [px, pz, layers] of [[PALLETS.x - 0.7, PALLETS.z - 1.0, 4], [PALLETS.x + 0.6, PALLETS.z - 1.0, 3]]) {
    pallet(px, pz);
    for (let l = 0; l < layers; l++) for (let i = 0; i < 3; i++) for (let j = 0; j < 5; j++) if (!(l === layers - 1 && r() < 0.35)) packs.push({ m: M4(px - 0.35 + i * 0.35, 0.13 + l * 0.31, pz - 0.42 + j * 0.21, l % 2 ? Math.PI : 0) });
  }
  inst(new THREE.BoxGeometry(0.33, 0.31, 0.21).translate(0, 0.155, 0), packMat, packs);
  pallet(PALLETS.x - 0.1, PALLETS.z + 1.4);
  cartonMats.forEach((mat, k) => {
    const items = [];
    for (let l = 0; l < 3 - k; l++) for (let i = 0; i < 2; i++) items.push({ m: M4(PALLETS.x - 0.35 + i * 0.5 + (r() - 0.5) * 0.04, 0.13 + l * 0.3 + (k ? 0 : 0), PALLETS.z + 1.0 + k * 0.38, (r() - 0.5) * 0.08) });
    inst(new THREE.BoxGeometry(0.46, 0.3, 0.34).translate(0, 0.15, 0), mat, items);
  });
  const sup = add(kit.sign(['구호물품 · 배부 시 안내'], { w: 1.2, h: 0.26, bg: '#3a4048', single: true })); sup.position.set(PALLETS.x, 1.9, ROOM.z0 + 0.03);

  // ---- recycling bins ----
  for (const [i, label, col] of [[0, '일반쓰레기', '#5d6168'], [1, '재활용', '#2e66b2']]) {
    const x = 7.4, z = -4.4 + i * 0.75;
    add(kit.rbox(0.55, 0.85, 0.55, 0.05, kit.mat(`paint:${col}`, { roughness: 0.5 }), { x, z }));
    const s = add(kit.sign([label], { w: 0.45, h: 0.14, bg: '#ffffff', fg: '#111', single: true })); s.position.set(x - 0.28, 0.6, z); s.rotation.y = -Math.PI / 2;
  }
  // priority area stand in the south-west bays
  const pr = add(kit.sign(['노약자 · 영유아 동반 구역'], { w: 1.1, h: 0.3, bg: '#f7f5ee', fg: '#1f4e8c', border: '#1f4e8c', single: true })); pr.position.set(-13.8, 1.15, 10.96); pr.rotation.y = Math.PI;
  add(kit.cyl(0.02, 0.02, 1.0, M.legs, { x: -13.8, z: 11.0 }, 6)); add(kit.cyl(0.16, 0.18, 0.03, M.seat, { x: -13.8, z: 11.0 }, 12));

  // ---- banner hung from the drain line on the south wall (sways in the ventilation draught) ----
  const banner = new THREE.Group(); banner.position.set(-11.5, 2.78, ROOM.z1 - 0.12); add(banner, live);
  const cloth = new THREE.Mesh(new THREE.PlaneGeometry(5.6, 0.82, 16, 2), new THREE.MeshStandardMaterial({ map: T.bannerTex('연무 생활문화센터 민방위 대피소'), roughness: 0.9, side: THREE.DoubleSide }));
  cloth.position.y = -0.45; cloth.rotation.y = Math.PI; cloth.castShadow = false; banner.add(cloth);

  // ---- parked cars in the east bays ----
  const paint = (hex) => kit.memo(`sh:carpaint:${hex}`, () => new THREE.MeshPhysicalMaterial({ color: hex, metalness: 0.55, roughness: 0.32, clearcoat: 1, clearcoatRoughness: 0.08 }));
  const plate = kit.sign(['12가 3456'], { w: 0.52, h: 0.11, bg: '#f5f5f2', fg: '#111', single: true });
  const cover = kit.mat('paint:#8c9095', { roughness: 0.95 });
  function car(x, z, ry, color, covered = false) {
    const c = new THREE.Group(); c.position.set(x, 0, z); c.rotation.y = ry;
    const p = paint(color);
    if (covered) { c.add(kit.rbox(1.95, 1.38, 4.7, 0.35, cover, { y: 0.06 })); }
    else {
      c.add(kit.rbox(1.82, 0.66, 4.62, 0.16, p, { y: 0.26 }));
      c.add(kit.rbox(1.6, 0.56, 2.45, 0.2, M.glass, { y: 0.86, z: 0.25 }));
      c.add(kit.rbox(1.5, 0.07, 1.9, 0.03, p, { y: 1.39, z: 0.3 }));
      for (const sx of [-1, 1]) {
        c.add(kit.box(0.4, 0.09, 0.05, M.lens, { x: sx * 0.6, y: 0.68, z: -2.29 }));
        c.add(kit.box(0.35, 0.09, 0.05, M.tail, { x: sx * 0.62, y: 0.74, z: 2.29 }));
        c.add(kit.box(0.08, 0.1, 0.16, p, { x: sx * 0.95, y: 0.96, z: -0.75 }));
      }
      for (const pz of [-2.32, 2.32]) { const pl = plate.clone(); pl.position.set(0, 0.45, pz); pl.rotation.y = pz < 0 ? Math.PI : 0; c.add(pl); }
    }
    for (const sx of [-0.82, 0.82]) for (const sz of [-1.45, 1.4]) {
      c.add(kit.cyl(0.33, 0.33, 0.22, M.tyre, { x: sx, y: 0.22, z: sz, rz: Math.PI / 2 }, 18));
      c.add(kit.cyl(0.2, 0.2, 0.225, M.rim, { x: sx * 1.002, y: 0.2125, z: sz, rz: Math.PI / 2 }, 12));
    }
    return add(c);
  }
  const cars = [[9.7, -13.4, 0, 0xe9e9e6], [12.15, -13.4, 0, 0x1b1c1f], [17.9, -13.4, 0, 0xa7abb0, true], [20.6, -13.4, 0, 0x4a4e54],
    [9.7, -2.6, Math.PI, 0x22304a], [14.6, 2.6, 0, 0xe9e9e6], [17.9, -2.6, Math.PI, 0xa7abb0], [20.6, 2.6, 0, 0x1b1c1f], [17.9, 13.4, Math.PI, 0xe9e9e6]];
  const carSolids = [];
  for (const [x, z, ry, col, cov] of cars) { car(x, z, ry, col, cov); carSolids.push({ x0: x - 0.95, x1: x + 0.95, z0: z - 2.35, z1: z + 2.35, y0: -1, y1: 1.5 }); }
  // wheel stoppers in the car bays
  const stops = [];
  for (const [x, z, ry] of cars) for (const d of [-0.6, 0.6]) stops.push({ m: M4(x + d, 0, z + (Math.abs(z) > 10 ? Math.sign(z) * 2.2 : (ry ? -1 : 1) * 2.35 * Math.sign(z || 1)), 0) });
  inst(new THREE.BoxGeometry(0.5, 0.1, 0.15).translate(0, 0.05, 0), T.hazardMaterial(), stops, false);

  const solids = [
    { x0: RECEPTION.x - 0.95, x1: RECEPTION.x + 0.95, z0: RECEPTION.z - 0.35, z1: RECEPTION.z + 0.35, y0: -1, y1: 0.8 },
    { x0: RADIO.x - 0.65, x1: RADIO.x + 0.65, z0: RADIO.z - 0.35, z1: RADIO.z + 0.35, y0: -1, y1: 0.8 },
    { x0: BOARD.x - 0.95, x1: BOARD.x + 0.95, z0: BOARD.z - 0.32, z1: BOARD.z + 0.32, y0: -1, y1: 2.1 },
    { x0: PALLETS.x - 1.3, x1: PALLETS.x + 1.2, z0: PALLETS.z - 1.6, z1: PALLETS.z + 2.0, y0: -1, y1: 1.5 },
    { x0: 7.1, x1: 7.7, z0: -4.7, z1: -3.3, y0: -1, y1: 0.9 },
    ...carSolids,
  ];
  void GRID; void STAIR;
  return { group: g, live, mats, seats, solids, dial, banner, cloth, boardMat, lanternLight };
}
