// Structure of the 생활문화센터 basement car park (B1): epoxy floor, walls, 8.1 m pillar grid with zone labels,
// flat slab with drop panels, bay lines, and the four ways in: stair A (north), car ramp B (south),
// accessible ramp C (north-west), and the east core (lift hall from the main lobby).
// Local frame: x east, z south, floor y = 0. Town footprint maps as local = town + (91, 0, -31).
import { ZONES, cellPlane } from './shelter-tex.js';

export const ROOM = { x0: -22, x1: 22, z0: -16, z1: 16, ceil: 3.0, drop: 2.72 };
export const GRID = { xs: [-16.2, -8.1, 0, 8.1, 16.2], zs: [-10.5, 0, 10.5] };
export const STAIR = { x0: 3.6, x1: 6.4 }, ACCESS = { x0: -14.2, x1: -11.8 }, RAMP = { x0: 8, x1: 14 }, EAST = { z0: -1.1, z1: 1.1 };

// Cylinder from a to b (THREE.Vector3-like {x,y,z}).
export function rod(THREE, a, b, r, mat, seg = 10) {
  const dir = new THREE.Vector3(b.x - a.x, b.y - a.y, b.z - a.z), len = dir.length();
  const m = new THREE.Mesh(new THREE.CylinderGeometry(r, r, len, seg), mat);
  m.position.set((a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2);
  m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir.normalize());
  m.castShadow = m.receiveShadow = true;
  return m;
}

export function buildShell(kit, T) {
  const { THREE } = kit;
  const g = new THREE.Group(); g.name = 'shelter-shell';
  const walk = [], solids = [], fixtures = [], lights = [], dyn = {};
  const add = (m, parent = g) => { parent.add(m); return m; };
  const solid = (x0, x1, z0, z1, y0 = -1, y1 = 6.5) => solids.push({ x0, x1, z0, z1, y0, y1 });

  const M = {
    floor: T.floorMaterial(), wall: kit.surface('plaster', { color: 0xdcdad3 }), ceil: kit.surface('concrete', { color: 0xbdbcb6 }),
    pillar: kit.surface('plaster', { color: 0xefeee9 }), band: kit.mat('paint:#3f6a57', { roughness: 0.45 }), stripe: kit.mat('paint:#e2b324', { roughness: 0.5 }),
    line: kit.mat('paint:#ecebe3', { roughness: 0.5, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1 }),
    blue: kit.mat('paint:#2a64b8', { roughness: 0.5, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1 }),
    concrete: kit.surface('concrete'), step: kit.surface('concrete', { color: 0xc9c6bd }), steel: kit.mat('steel'), metal: kit.mat('metal'),
    dark: kit.mat('darkMetal'), hazard: T.hazardMaterial(), groove: T.grooveMaterial(), tactile: T.tactileMaterial(),
    rubber: kit.mat('paint:#59656e', { roughness: 0.92 }), door: kit.mat('paint:#b9b6aa', { roughness: 0.5, metalness: 0.4 }), tile: kit.surface('tile'),
    gyp: kit.mat('whiteWall'), stainless: kit.mat('metal', { roughness: 0.22 }),
  };

  // Sloped slabs. A is the lower-z end (z increases from A to B); top surface runs through both points.
  function slopeZ(x0, x1, zA, yA, zB, yB, th, mat, parent) {
    const dz = zB - zA, dy = yB - yA, L = Math.hypot(dz, dy), a = Math.atan2(-dy, dz);
    const m = kit.slab(x1 - x0, th, L, mat, { rx: a });
    m.position.set((x0 + x1) / 2, (yA + yB) / 2 - Math.cos(a) * th / 2, (zA + zB) / 2 - Math.sin(a) * th / 2);
    return add(m, parent);
  }
  function slopeX(xA, yA, xB, yB, z0, z1, th, mat) {
    const dx = xB - xA, dy = yB - yA, L = Math.hypot(dx, dy), a = Math.atan2(dy, dx);
    const m = kit.slab(L, th, z1 - z0, mat, { rz: a });
    m.position.set((xA + xB) / 2 + Math.sin(a) * th / 2, (yA + yB) / 2 - Math.cos(a) * th / 2, (z0 + z1) / 2);
    return add(m);
  }

  // ---- floor, ceiling, perimeter walls with openings ----
  add(kit.plane(44, 32, M.floor, { x: 0, y: 0, z: 0 }));
  add(kit.slab(44.5, 0.3, 32.5, M.ceil, { y: ROOM.ceil }));
  walk.push({ x0: ROOM.x0, x1: ROOM.x1, z0: ROOM.z0, z1: ROOM.z1, y: 0 });

  // Wall along x at z (side -1: thickness toward -z). openings: [a, b, top, lintelTop]
  function wallX(z, x0, x1, side, openings = []) {
    let cur = x0;
    const zc = z + side * 0.125, inner = z - side * 0.012;
    const seg = (a, b) => {
      add(kit.slab(b - a, ROOM.ceil, 0.25, M.wall, { x: (a + b) / 2, z: zc }));
      add(kit.box(b - a, 0.9, 0.02, M.band, { x: (a + b) / 2, z: inner }));
      add(kit.box(b - a, 0.07, 0.025, M.stripe, { x: (a + b) / 2, y: 0.9, z: inner }));
      add(kit.box(b - a, 0.006, 0.24, M.dark, { x: (a + b) / 2, z: z - side * 0.14 }));  // drain grating
      solid(a, b, Math.min(z, z + side * 0.25), Math.max(z, z + side * 0.25));
    };
    for (const [a, b, top, lt = ROOM.ceil] of openings) {
      if (a > cur) seg(cur, a);
      add(kit.slab(b - a, lt - top, 0.25, M.wall, { x: (a + b) / 2, y: top, z: zc }));
      solids.push({ x0: a, x1: b, z0: Math.min(z, z + side * 0.25), z1: Math.max(z, z + side * 0.25), y0: top, y1: lt });
      cur = b;
    }
    if (cur < x1) seg(cur, x1);
  }
  function wallZ(x, z0, z1, side, openings = []) {
    let cur = z0;
    const xc = x + side * 0.125, inner = x - side * 0.012;
    const seg = (a, b) => {
      add(kit.slab(0.25, ROOM.ceil, b - a, M.wall, { x: xc, z: (a + b) / 2 }));
      add(kit.box(0.02, 0.9, b - a, M.band, { x: inner, z: (a + b) / 2 }));
      add(kit.box(0.025, 0.07, b - a, M.stripe, { x: inner, y: 0.9, z: (a + b) / 2 }));
      add(kit.box(0.24, 0.006, b - a, M.dark, { x: x - side * 0.14, z: (a + b) / 2 }));
      solid(Math.min(x, x + side * 0.25), Math.max(x, x + side * 0.25), a, b);
    };
    for (const [a, b, top] of openings) {
      if (a > cur) seg(cur, a);
      add(kit.slab(0.25, ROOM.ceil - top, b - a, M.wall, { x: xc, y: top, z: (a + b) / 2 }));
      cur = b;
    }
    if (cur < z1) seg(cur, z1);
  }
  wallX(ROOM.z0, ROOM.x0 - 0.25, ROOM.x1 + 0.25, -1, [[ACCESS.x0, ACCESS.x1, 2.6], [STAIR.x0, STAIR.x1, 2.2, 6.2]]);
  wallX(ROOM.z1, ROOM.x0 - 0.25, ROOM.x1 + 0.25, 1, [[RAMP.x0, RAMP.x1, 2.5]]);
  wallZ(ROOM.x0, ROOM.z0, ROOM.z1, -1);
  wallZ(ROOM.x1, ROOM.z0, ROOM.z1, 1, [[EAST.z0, EAST.z1, 2.2]]);

  // ---- pillars: zone colour band, corner guards, labels on four faces, drop panel ----
  const atlas = T.pillarAtlas();
  GRID.zs.forEach((pz, row) => GRID.xs.forEach((px, col) => {
    const zone = ZONES[row];
    add(kit.slab(0.7, ROOM.drop, 0.7, M.pillar, { x: px, z: pz }));
    add(kit.box(0.72, 1.05, 0.72, kit.mat(`paint:${zone.color}`, { roughness: 0.5 }), { x: px, z: pz }));
    for (const [cx, cz] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) add(kit.box(0.09, 1.2, 0.09, M.hazard, { x: px + cx * 0.335, z: pz + cz * 0.335 }));
    for (let f = 0; f < 4; f++) {
      const a = f * Math.PI / 2, lbl = new THREE.Mesh(cellPlane(THREE, 0.5, 0.62, atlas.cell(row, col)), atlas.material);
      lbl.position.set(px + Math.sin(a) * 0.356, 1.95, pz + Math.cos(a) * 0.356); lbl.rotation.y = a;
      add(lbl);
    }
    add(kit.slab(2.4, ROOM.ceil - ROOM.drop, 2.4, M.ceil, { x: px, y: ROOM.drop, z: pz }));
    solid(px - 0.37, px + 0.37, pz - 0.37, pz + 0.37);
  }));

  // ---- bay lines: north bays, middle double row, south bays (2.5 m bays between pillar columns) ----
  const spans = [ROOM.x0, ...GRID.xs, ROOM.x1];
  const line = (x0, x1, z0, z1, mat = M.line) => add(kit.plane(x1 - x0, z1 - z0, mat, { x: (x0 + x1) / 2, y: 0.004, z: (z0 + z1) / 2 }));
  const bays = [];
  for (let i = 0; i < spans.length - 1; i++) {
    const a = spans[i] + (i ? 0.4 : 0.1), b = spans[i + 1] - (i < spans.length - 2 ? 0.4 : 0.1), n = Math.round((b - a) / 2.5), w = (b - a) / n;
    for (let k = 0; k < n; k++) bays.push([a + k * w, a + (k + 1) * w]);
  }
  const blockedN = (x0, x1) => x1 > ACCESS.x0 - 1.8 && x0 < ACCESS.x1 + 0.6 || x1 > STAIR.x0 - 0.4 && x0 < STAIR.x1 + 0.4;
  const blockedS = (x0, x1) => x1 > RAMP.x0 - 0.3 && x0 < RAMP.x1 + 0.3;
  let accessBay = null;
  for (const [x0, x1] of bays) {
    const lm = !accessBay && x0 > ACCESS.x1 && x1 < -8 ? (accessBay = [x0, x1], M.blue) : M.line;
    if (!blockedN(x0, x1)) { line(x0 - 0.06, x0 + 0.06, -16, -10.8, lm); line(x1 - 0.06, x1 + 0.06, -16, -10.8, lm); line(x0, x1, -10.86, -10.74, lm); }
    line(x0 - 0.06, x0 + 0.06, -5.2, 5.2); line(x1 - 0.06, x1 + 0.06, -5.2, 5.2);
    if (!blockedS(x0, x1)) { line(x0 - 0.06, x0 + 0.06, 10.8, 16); line(x1 - 0.06, x1 + 0.06, 10.8, 16); line(x0, x1, 10.74, 10.86); }
  }
  line(ROOM.x0, ROOM.x1, -5.26, -5.14); line(ROOM.x0, ROOM.x1, 5.14, 5.26); line(ROOM.x0, ROOM.x1, -0.06, 0.06);

  // painted symbols and hatches from the marking atlas
  const marks = T.markAtlas();
  const mark = (i, x, z, w, d, ry = 0) => { const m = new THREE.Mesh(cellPlane(THREE, w, d, marks.cell(i)), marks.material); m.rotation.set(-Math.PI / 2, 0, ry); m.position.set(x, 0.006, z); m.receiveShadow = true; return add(m); };
  for (const x of [-16, -4, 12]) mark(0, x, -8, 1.1, 1.6, Math.PI / 2);   // north aisle runs west
  for (const x of [-12, 0, 18]) mark(0, x, 8, 1.1, 1.6, -Math.PI / 2);    // south aisle runs east
  if (accessBay) mark(1, (accessBay[0] + accessBay[1]) / 2, -13.4, 1.6, 1.6);
  mark(2, 11, 13.2, 2.2, 2.2); mark(3, 19.6, -8, 1.8, 1.8, Math.PI / 2); mark(4, 18.5, -13.2, 1.4, 1.4);
  mark(5, (STAIR.x0 + STAIR.x1) / 2, -14.75, 3.2, 2.5); mark(5, -13.4, -14.5, 3.6, 3); mark(5, 20.7, 0, 2.6, 2.6);

  // tactile warning strips at the stair foot and the accessible ramp
  const tactile = (x0, x1, z0, z1, y = 0.008) => add(kit.plane(x1 - x0, z1 - z0, M.tactile, { x: (x0 + x1) / 2, y, z: (z0 + z1) / 2 }));
  tactile(STAIR.x0, STAIR.x1, -16.2, -15.6); tactile(ACCESS.x0, ACCESS.x1, -15.9, -15.3);

  // ---- stair A: switchback core north of the opening; flight 1 rises north, flight 2 returns south ----
  const sx0 = STAIR.x0, sx1 = STAIR.x1, sm = 4.95, sm1 = 5.05, zFoot = -17.45, zLand = -19.97, zEnd = -21.37, R = 0.17, TR = 0.28;
  const core = (x0, x1, z0, z1, y0 = 0, y1 = 6.2) => { add(kit.slab(x1 - x0, y1 - y0, z1 - z0, M.wall, { x: (x0 + x1) / 2, y: y0, z: (z0 + z1) / 2 })); solid(x0, x1, z0, z1, y0 - 1, y1); };
  core(sx0 - 0.25, sx0, zEnd - 0.25, ROOM.z0); core(sx1, sx1 + 0.25, zEnd - 0.25, ROOM.z0); core(sx0 - 0.25, sx1 + 0.25, zEnd - 0.25, zEnd);
  core(sm, sm1, zLand, zFoot);                                                      // wall between the flights
  add(kit.plane(sx1 - sx0, -16.25 - zEnd, M.step, { x: 5, y: 0.002, z: (zEnd - 16.25) / 2 }));
  add(kit.slab(sx1 - sx0 + 0.5, 0.25, -16 - zEnd + 0.25, M.ceil, { x: 5, y: 6.2, z: (zEnd - 16) / 2 }));
  for (let k = 1; k <= 9; k++) {
    const z1 = zFoot - TR * (k - 1), z2 = zLand + TR * (k - 1);
    add(kit.box(sm - sx0, R * k, TR, M.step, { x: (sx0 + sm) / 2, z: z1 - TR / 2 }));
    add(kit.box(sm - sx0, 0.025, 0.05, M.dark, { x: (sx0 + sm) / 2, y: R * k, z: z1 - 0.03 }));
    add(kit.box(sx1 - sm1, R + 0.2, TR, M.step, { x: (sm1 + sx1) / 2, y: 1.7 + R * k - R - 0.2, z: z2 + TR / 2 }));
    add(kit.box(sx1 - sm1, 0.025, 0.05, M.dark, { x: (sm1 + sx1) / 2, y: 1.7 + R * k, z: z2 + 0.03 }));
  }
  add(kit.box(sx1 - sx0, 1.7, zLand - zEnd, M.step, { x: 5, z: (zLand + zEnd) / 2 }));               // landing
  add(kit.box(sx1 - sx0, 0.22, -16.25 - zFoot, M.step, { x: 5, y: 3.4 - 0.22, z: (zFoot - 16.25) / 2 })); // top landing
  add(kit.box(sx1 - sm1, 3.0, 0.08, M.wall, { x: (sm1 + sx1) / 2, z: zFoot + 0.04 }));                // closes the space under flight 2
  solids.push({ x0: sm1, x1: sx1, z0: zLand, z1: zFoot + 0.1, y0: -1, y1: 1.6 });
  for (const y of [3.9, 4.5]) add(rod(THREE, { x: sx0, y, z: zFoot - 0.04 }, { x: sm, y, z: zFoot - 0.04 }, 0.022, M.stainless)); // top landing guard over flight 1
  solid(sx0, sm1, zFoot - 0.12, zFoot, 3.2, 4.6);
  for (const [x, ya, za, yb, zb] of [[sx0 + 0.06, 0.9, zFoot, 2.6, zLand], [sm - 0.06, 0.9, zFoot, 2.6, zLand], [sx1 - 0.06, 2.6, zLand, 4.3, zFoot], [sm1 + 0.06, 2.6, zLand, 4.3, zFoot]]) {
    if (ya < 1) add(rod(THREE, { x, y: ya, z: za + 0.3 }, { x, y: ya, z: za }, 0.022, M.stainless));
    add(rod(THREE, { x, y: ya, z: za }, { x, y: yb, z: zb }, 0.022, M.stainless));
  }
  add(kit.box(0.06, 2.1, 0.95, M.door, { x: sx1 - 0.03, y: 3.4, z: -16.85 }));                        // exit door at the top
  add(kit.box(0.05, 0.05, 0.7, M.metal, { x: sx1 - 0.09, y: 4.45, z: -16.85 }));
  walk.push({ x0: sx0, x1: sx1, z0: zFoot, z1: ROOM.z0, y: 0 },
    { x0: sx0, x1: sm, z0: zLand, z1: zFoot, ramp: { axis: 'z', y0: 1.7, y1: 0 } },
    { x0: sx0, x1: sx1, z0: zEnd, z1: zLand, y: 1.7 },
    { x0: sm1, x1: sx1, z0: zLand, z1: zFoot, ramp: { axis: 'z', y0: 1.7, y1: 3.4 } },
    { x0: sx0, x1: sx1, z0: zFoot, z1: -16.25, y: 3.4 });
  add(kit.box(sx1 - sx0 + 0.5, 0.3, 0.32, M.dark, { x: 5, y: 2.2, z: -15.84 }));                      // rolled-up fire shutter box
  for (const x of [sx0 - 0.08, sx1 + 0.08]) add(kit.box(0.08, 2.2, 0.1, M.steel, { x, z: -15.95 }));
  fixtures.push({ x: 5, y: 6.05, z: -18.7, ry: Math.PI / 2 }, { x: 5, y: 6.05, z: -16.9, ry: Math.PI / 2 });
  lights.push({ pos: [5, 5.2, -18.6], color: 0xe8f2ff, base: 9, dist: 9, circuit: 'A' });

  // ---- ramp B: car ramp rising south to the street, anti-slip rings, chevron walls, barrier at the top ----
  const rz0 = 16.25, rz1 = 36, rTop = 3.4, rIn = 0.0;
  const ry = (z) => rIn + (z - rz0) / (rz1 - rz0) * rTop;
  for (const [x0, x1] of [[RAMP.x0 - 0.25, RAMP.x0], [RAMP.x1, RAMP.x1 + 0.25]]) { add(kit.slab(x1 - x0, 6.6, rz1 - ROOM.z1, M.wall, { x: (x0 + x1) / 2, z: (ROOM.z1 + rz1) / 2 })); solid(x0, x1, ROOM.z1, rz1); }
  slopeZ(RAMP.x0, RAMP.x1, rz0, 0, rz1, rTop, 0.3, M.groove);
  slopeZ(RAMP.x0 - 0.25, RAMP.x1 + 0.25, rz0, 2.75, rz1, rTop + 2.75, 0.3, M.ceil);
  for (const x of [RAMP.x0 + 0.012, RAMP.x1 - 0.012]) { const s = slopeZ(x - 0.01, x + 0.01, rz0, 0.55, rz1, rTop + 0.55, 0.5, M.hazard); s.castShadow = false; }
  add(kit.box(RAMP.x1 - RAMP.x0, 0.07, 0.4, M.hazard, { x: 11, z: 15.2 }));                            // speed bump
  add(rod(THREE, { x: RAMP.x0 + 0.2, y: 2.32, z: 15.75 }, { x: RAMP.x1 - 0.2, y: 2.32, z: 15.75 }, 0.07, M.hazard, 12)); // height bar
  for (const x of [RAMP.x0 + 0.5, RAMP.x1 - 0.5]) add(rod(THREE, { x, y: 2.38, z: 15.75 }, { x, y: 2.5, z: 15.75 }, 0.012, M.steel));
  const hb = add(kit.sign(['높이제한 2.3m'], { w: 1.2, h: 0.3, bg: '#e9b21c', fg: '#151515', single: true })); hb.position.set(11, 2.32, 15.66); hb.rotation.y = Math.PI;
  const mirror = add(new THREE.Mesh(new THREE.CylinderGeometry(0.42, 0.42, 0.06, 28), new THREE.MeshStandardMaterial({ color: 0xdfe6ea, metalness: 1, roughness: 0.04 })));
  mirror.position.set(RAMP.x1 - 0.3, 2.2, 15.5); mirror.rotation.set(Math.PI / 2, 0, 0.6);
  add(new THREE.Mesh(new THREE.TorusGeometry(0.43, 0.035, 8, 28), kit.mat('paint:#f07a1a'))).position.copy(mirror.position).add(new THREE.Vector3(0, 0, 0.02));
  // barrier gate at the top (pedestrians stop here in this level)
  const bz = 31, by = ry(bz);
  add(kit.box(0.4, 1.05, 0.32, kit.mat('paint:#e9e6dc'), { x: RAMP.x1 - 0.45, y: by, z: bz }));
  const arm = add(rod(THREE, { x: RAMP.x1 - 0.5, y: by + 0.92, z: bz }, { x: RAMP.x0 + 0.3, y: by + 0.92, z: bz }, 0.045, kit.mat('paint:#d23a33')));
  for (let i = 0; i < 5; i++) add(kit.box(0.3, 0.1, 0.1, kit.mat('paint:#f4f2ec'), { x: RAMP.x1 - 1.2 - i * 1.0, y: by + 0.87, z: bz }));
  const inSign = add(kit.sign(['입차'], { w: 0.5, h: 0.25, bg: '#05140a', fg: '#56f27a', emissive: 1.4, single: true }));
  inSign.position.set(RAMP.x1 - 0.45, by + 1.75, bz - 0.17); inSign.rotation.y = Math.PI; kit.lit('signs', inSign.material, 1.4);
  add(rod(THREE, { x: RAMP.x1 - 0.45, y: by, z: bz + 0.25 }, { x: RAMP.x1 - 0.45, y: by + 1.6, z: bz + 0.25 }, 0.04, M.steel));
  solid(RAMP.x0, RAMP.x1, bz - 0.2, bz + 0.2);
  walk.push({ x0: RAMP.x0, x1: RAMP.x1, z0: ROOM.z1, z1: bz, ramp: { axis: 'z', y0: 0, y1: ry(bz) } });
  // night outside the top opening: follows the street lights (dark in a blackout)
  const night = new THREE.MeshStandardMaterial({ color: 0x000000, emissive: 0x2a3c52, emissiveIntensity: 0.5, roughness: 1 });
  kit.lit('street', night, 0.5);
  const sky = add(new THREE.Mesh(new THREE.PlaneGeometry(RAMP.x1 - RAMP.x0, 3), night)); sky.position.set(11, rTop + 1.5, rz1 - 0.05); sky.rotation.y = Math.PI;
  for (const z of [20, 26]) fixtures.push({ x: 11, y: ry(z) + 2.6, z, ry: 0, rx: Math.atan2(rTop, rz1 - rz0) });

  // ---- ramp C: accessible ramp 1:12 north, landing every 0.75 m rise, handrails both sides, turn east at the top ----
  const ax0 = ACCESS.x0, ax1 = ACCESS.x1, az = [-16.25, -25, -26.5, -35.5, -38.5];
  const aw = (x0, x1, z0, z1, y1 = 5.2) => { add(kit.slab(x1 - x0, y1, z1 - z0, M.wall, { x: (x0 + x1) / 2, z: (z0 + z1) / 2 })); solid(x0, x1, z0, z1); };
  aw(ax0 - 0.25, ax0, az[4] - 0.25, ROOM.z0); aw(ax1, ax1 + 0.25, az[3], ROOM.z0); aw(ax0 - 0.25, ax1 + 0.25, az[4] - 0.25, az[4]);
  slopeZ(ax0, ax1, az[1], 0.75, az[0], 0, 0.3, M.rubber);
  add(kit.box(ax1 - ax0, 0.75, az[1] - az[2], M.rubber, { x: (ax0 + ax1) / 2, z: (az[1] + az[2]) / 2 }));
  slopeZ(ax0, ax1, az[3], 1.5, az[2], 0.75, 0.3, M.rubber);
  add(kit.box(ax1 - ax0, 1.5, az[3] - az[4], M.rubber, { x: (ax0 + ax1) / 2, z: (az[3] + az[4]) / 2 }));
  for (const [z0, z1, y] of [[az[1], az[0], 3.0], [az[3], az[1], 3.8], [az[4] - 0.25, az[3], 4.2]]) add(kit.slab(ax1 - ax0 + 0.5, 0.25, z1 - z0, M.ceil, { x: (ax0 + ax1) / 2, y, z: (z0 + z1) / 2 }));
  for (const [z, y0, y1] of [[az[1], 3.0, 3.8], [az[3], 3.8, 4.2]]) add(kit.box(ax1 - ax0, y1 - y0, 0.2, M.ceil, { x: (ax0 + ax1) / 2, y: y0, z }));
  tactile(ax0, ax1, az[1] - 0.6, az[1], 0.758); tactile(ax0, ax1, az[2], az[2] + 0.3, 0.758); tactile(ax0, ax1, az[3] - 0.6, az[3], 1.508);
  const railPts = [[az[0] + 0.3, 0], [az[0], 0], [az[1], 0.75], [az[2], 0.75], [az[3], 1.5], [az[4] + 0.2, 1.5]];
  for (const x of [ax0 + 0.07, ax1 - 0.07]) for (const h of [0.85, 0.65]) for (let i = 0; i < railPts.length - 1; i++) {
    if (x > ax1 - 0.1 && railPts[i + 1][0] < az[3] - 0.1) continue;   // east side opens at the turn
    add(rod(THREE, { x, y: railPts[i][1] + h, z: railPts[i][0] }, { x, y: railPts[i + 1][1] + h, z: railPts[i + 1][0] }, 0.02, M.stainless, 8));
  }
  // the turn east at the top landing and the start of the next ramp
  aw(ax1, -7.3, az[4] - 0.25, az[4]); aw(ax1, -7.3, az[3], az[3] + 0.25); aw(-7.55, -7.3, az[4], az[3]);
  slopeX(ax1, 1.5, -7.55, 1.83, az[4], az[3], 0.3, M.rubber);
  add(kit.slab(-7.3 - ax1, 0.25, 3.5, M.ceil, { x: (ax1 - 7.3) / 2, y: 4.3, z: (az[3] + az[4]) / 2 }));
  for (const z of [az[4] + 0.4, az[3] - 0.4]) add(kit.cyl(0.03, 0.03, 0.95, M.stainless, { x: ax1 + 0.15, y: 1.5, z }));
  add(rod(THREE, { x: ax1 + 0.15, y: 2.38, z: az[4] + 0.4 }, { x: ax1 + 0.15, y: 2.38, z: az[3] - 0.4 }, 0.025, kit.mat('paint:#c8261f'), 6));
  solids.push({ x0: ax1, x1: ax1 + 0.4, z0: az[4], z1: az[3], y0: 0, y1: 5 });
  walk.push({ x0: ax0, x1: ax1, z0: az[1], z1: ROOM.z0, ramp: { axis: 'z', y0: 0.75, y1: 0 } },
    { x0: ax0, x1: ax1, z0: az[2], z1: az[1], y: 0.75 },
    { x0: ax0, x1: ax1, z0: az[3], z1: az[2], ramp: { axis: 'z', y0: 1.5, y1: 0.75 } },
    { x0: ax0, x1: ax1, z0: az[4], z1: az[3], y: 1.5 });
  fixtures.push({ x: -13, y: 2.92, z: -20.5, ry: Math.PI / 2 }, { x: -13, y: 3.72, z: -31, ry: Math.PI / 2 }, { x: -9.6, y: 4.22, z: -37, ry: 0 });
  lights.push({ pos: [-13, 2.9, -27], color: 0xf0f4ff, base: 8, dist: 14, circuit: 'A' });

  // ---- east core: lift hall behind the open fire doors, stairs up to the lobby, toilets ----
  const ex0 = ROOM.x1 + 0.25, ex1 = 27;
  add(kit.plane(ex1 - ex0, 8, M.tile, { x: (ex0 + ex1) / 2, y: 0.003, z: 0 }));
  add(kit.plane(0.25, EAST.z1 - EAST.z0, M.step, { x: ROOM.x1 + 0.125, y: 0.003, z: 0 }));
  for (const [x0, x1, z0, z1] of [[ex0, ex1 + 0.25, -4.25, -4], [ex0, ex1 + 0.25, 4, 4.25], [ex1, ex1 + 0.25, -4, 4]]) { add(kit.slab(x1 - x0, 2.9, z1 - z0, M.gyp, { x: (x0 + x1) / 2, z: (z0 + z1) / 2 })); solid(x0, x1, z0, z1); }
  add(kit.slab(ex1 - ex0 + 0.25, 0.2, 8.5, M.gyp, { x: (ex0 + ex1) / 2, y: 2.6, z: 0 }));
  walk.push({ x0: ROOM.x1, x1: ex1, z0: -4, z1: 4, y: 0 });
  for (const s of [-1, 1]) { // fire-door leaves held open against the hall walls
    add(kit.box(0.05, 2.1, 1.08, M.door, { x: ex0 + 0.04, z: s * (EAST.z1 + 0.56) }));
    add(kit.box(0.04, 0.04, 0.3, M.metal, { x: ex0 + 0.09, y: 1.05, z: s * (EAST.z1 + 0.85) }));
  }
  add(kit.box(0.06, 2.35, 1.5, M.stainless, { x: ex1 - 0.03, z: -1.6 }));                            // lift frame + doors
  for (const dz of [-0.28, 0.28]) add(kit.box(0.04, 2.1, 0.54, kit.mat('metal', { roughness: 0.3, color: 0xb8bcc0 }), { x: ex1 - 0.06, z: -1.6 + dz }));
  const disp = new THREE.MeshStandardMaterial({ map: kit.signTexture(['▼ B1'], { w: 256, h: 96, bg: '#0b0b0b', fg: '#ff5a36' }), emissive: 0xffffff, emissiveMap: kit.signTexture(['▼ B1'], { w: 256, h: 96, bg: '#0b0b0b', fg: '#ff5a36' }), emissiveIntensity: 1.2 });
  const dispM = add(new THREE.Mesh(new THREE.PlaneGeometry(0.42, 0.16), disp)); dispM.position.set(ex1 - 0.07, 2.48, -1.6); dispM.rotation.y = -Math.PI / 2;
  const btn = new THREE.MeshStandardMaterial({ color: 0x222222, emissive: 0x9fd4ff, emissiveIntensity: 1.1 });
  add(kit.box(0.03, 0.22, 0.1, M.stainless, { x: ex1 - 0.05, y: 1.0, z: -0.55 })); add(kit.cyl(0.025, 0.025, 0.02, btn, { x: ex1 - 0.07, y: 1.1, z: -0.55, rz: Math.PI / 2 }));
  dyn.lift = [disp, btn];
  const up = add(kit.sign(['계단 · 1층 로비'], { w: 1.1, h: 0.26, bg: '#1f4e8c', single: true })); up.position.set(24.6, 2.32, -3.98);
  const wc = add(kit.sign(['화장실'], { w: 0.7, h: 0.26, bg: '#3a4048', single: true })); wc.position.set(24.6, 2.32, 3.98); wc.rotation.y = Math.PI;
  add(kit.box(1.0, 2.1, 0.05, M.door, { x: 24.6, z: -3.97 })); add(kit.box(1.0, 2.1, 0.05, kit.mat('paint:#cfd6da'), { x: 24.6, z: 3.97 }));
  fixtures.push({ x: 24.6, y: 2.58, z: 0, ry: Math.PI / 2, panel: true });
  lights.push({ pos: [24.6, 2.3, 0], color: 0xfff4e6, base: 5, dist: 8, circuit: 'A' });

  return { group: g, walk, solids, fixtures, lights, dyn, M };
}
