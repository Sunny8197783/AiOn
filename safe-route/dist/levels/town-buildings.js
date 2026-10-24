// Town buildings from BUILDINGS: facade planes (kit.facade, night windows), parapets, rooftop plant,
// balcony slabs, apartment gable numbers, the logistics warehouse and a ring of filler blocks outside
// the bounds. Big background blocks are split into several villas with alleys so they read as streets.
import * as THREE from 'three';
import { BUILDINGS, BOUNDS } from '../game/townmap.js';
import { FONT } from '../engine/kit.js';
import { CURB } from './town-plan.js';

const FH = { apartment: 2.8, villa: 2.9, shop: 3.5, mart: 4.6, office: 3.7, civic: 3.6 };
export const heightOf = (b) => b.h ?? (b.floors * (FH[b.kind] ?? 3) + (b.kind === 'apartment' ? 0.8 : 0));
const BACKGROUND = new Set(['n1', 'n2', 'nw1', 's1', 's2', 'sw1']);

// Vertical plane for one building face, front side outward. side: 'n'|'s'|'e'|'w'. a0..a1 along the
// face (world x for n/s, world z for e/w), y0..y1 height; u/v ranges into the texture. Texture u runs
// left->right as seen from outside, so sub-ranges match world a0->a1 only on 's' and 'w' faces.
export function facePlane(side, c, a0, a1, y0, y1, material, { u0 = 0, u1 = 1, v0 = 0, v1 = 1, cast = true } = {}) {
  const g = new THREE.PlaneGeometry(a1 - a0, y1 - y0), uv = g.attributes.uv;
  for (let i = 0; i < uv.count; i++) uv.setXY(i, u0 + uv.getX(i) * (u1 - u0), v0 + uv.getY(i) * (v1 - v0));
  const m = new THREE.Mesh(g, material), mid = (a0 + a1) / 2, ym = (y0 + y1) / 2;
  if (side === 's' || side === 'n') m.position.set(mid, ym, c); else m.position.set(c, ym, mid);
  m.rotation.y = { s: 0, n: Math.PI, e: Math.PI / 2, w: -Math.PI / 2 }[side];
  m.castShadow = cast; m.receiveShadow = true;
  return m;
}

export function buildBuildings(kit, { holes = {} } = {}) {
  const group = new THREE.Group(); group.name = 'buildings';
  let stat = new THREE.Group(); // swapped briefly while the strike villa is built into its own group
  const far = new THREE.Group(); group.add(stat, far);
  // The villa the D-day missile destroys (town-strike.js): built unmerged so it can be swapped for a ruin.
  const strike = { group: new THREE.Group(), box: null }; strike.group.name = 'strikeVilla'; group.add(strike.group);
  const solids = [];

  const roofMat = kit.memo('town:roof', () => kit.surface('concrete', { color: 0xb5b2aa }));
  const greenRoof = kit.mat('paint:#5d8a6b', { roughness: 0.75 });
  const parapetMat = kit.mat('whiteWall');
  const metal = kit.mat('metal'), steel = kit.mat('steel');

  // Facade material with optional colour tint (clones share textures; registered for night windows).
  const fac = (kind, floors, cols, seed, tint) => kit.memo(`town:fac:${kind}:${floors}:${cols}:${seed}:${tint || ''}`, () => {
    const base = kit.facade(kind, { floors, cols, seed, lit: kind === 'office' ? 0.45 : 0.6 });
    if (!tint) return base;
    const m = base.clone(); m.color.set(tint); kit.lit('windows', m, 1.1); return m;
  });

  // A box building from facade planes + roof + parapet. mats: { long, short, roof } ; long faces are n/s.
  function shell(x0, x1, z0, z1, h, mats, o = {}) {
    const W = x1 - x0, D = z1 - z0, rep = (w, per) => Math.max(1, Math.round(w / per));
    const uL = o.perL ? rep(W, o.perL) : 1, uS = o.perS ? rep(D, o.perS) : 1;
    const y0 = o.y0 ?? CURB;
    const hole = o.hole; // { side, a0, a1, y1 } opening at ground level
    for (const [side, c, a0, a1, mat, u1] of [['s', z1, x0, x1, mats.long, uL], ['n', z0, x0, x1, mats.long, uL], ['e', x1, z0, z1, mats.short, uS], ['w', x0, z0, z1, mats.short, uS]]) {
      const target = o.far ? far : stat;
      if (hole && hole.side === side) {
        const L = a1 - a0, U = (a) => (a - a0) / L * u1, V = (y) => (y - y0) / (h - y0);
        target.add(facePlane(side, c, a0, hole.a0, y0, h, mat, { u1: U(hole.a0) }));
        target.add(facePlane(side, c, hole.a1, a1, y0, h, mat, { u0: U(hole.a1), u1 }));
        target.add(facePlane(side, c, hole.a0, hole.a1, hole.y1, h, mat, { u0: U(hole.a0), u1: U(hole.a1), v0: V(hole.y1) }));
      } else target.add(facePlane(side, c, a0, a1, y0, h, mat, { u1, cast: !o.far }));
    }
    const top = new THREE.Mesh(new THREE.PlaneGeometry(W, D).rotateX(-Math.PI / 2), mats.roof || roofMat);
    top.position.set((x0 + x1) / 2, h, (z0 + z1) / 2); top.receiveShadow = true;
    (o.far ? far : stat).add(top);
    if (!o.far) { // parapet
      const p = 0.9, t = 0.18, pm = mats.parapet || parapetMat;
      stat.add(kit.box(W, p, t, pm, { x: (x0 + x1) / 2, y: h, z: z0 + t / 2 }), kit.box(W, p, t, pm, { x: (x0 + x1) / 2, y: h, z: z1 - t / 2 }),
        kit.box(t, p, D - 2 * t, pm, { x: x0 + t / 2, y: h, z: (z0 + z1) / 2 }), kit.box(t, p, D - 2 * t, pm, { x: x1 - t / 2, y: h, z: (z0 + z1) / 2 }));
    }
  }
  // Rooftop clutter for low buildings: stair house, stainless tanks, AC units.
  function roofKit(x0, x1, z0, z1, h, rr, green) {
    const cx = x0 + (x1 - x0) * (0.25 + rr() * 0.5), cz = z0 + (z1 - z0) * (0.25 + rr() * 0.5);
    stat.add(kit.box(3, 2.6, 3.2, parapetMat, { x: cx, y: h, z: cz }), kit.box(3.3, 0.15, 3.5, roofMat, { x: cx, y: h + 2.6, z: cz }));
    for (let i = 0; i < 1 + (rr() * 2 | 0); i++) stat.add(kit.cyl(0.55, 0.55, 1.3, metal, { x: cx + 2.4 + i * 1.3, y: h + 0.3, z: cz + 1 }), kit.box(1.2, 0.3, 1.2, steel, { x: cx + 2.4 + i * 1.3, y: h, z: cz + 1 }));
    if (green) stat.add(new THREE.Mesh(new THREE.PlaneGeometry(x1 - x0 - 0.4, z1 - z0 - 0.4).rotateX(-Math.PI / 2).translate((x0 + x1) / 2, h + 0.02, (z0 + z1) / 2), greenRoof));
  }

  // Apartment gable: plain wall, brand mark and the big 동 number.
  const gable = (label) => kit.memo(`town:gable:${label}`, () => {
    const map = kit.texture(kit.canvas(512, 1536, (g, w, h) => {
      g.fillStyle = '#ebe6db'; g.fillRect(0, 0, w, h);
      const gr = kit.rng(label.length * 7 + 1); for (let i = 0; i < 1500; i++) { g.fillStyle = `rgba(0,0,0,${gr() * 0.035})`; g.fillRect(gr() * w, gr() * h, 3, 3); }
      g.fillStyle = '#c9c2b2'; g.fillRect(0, 0, w * 0.1, h); g.fillRect(w * 0.9, 0, w * 0.1, h);
      g.fillStyle = '#3f7d5c'; g.fillRect(w * 0.1, h * 0.035, w * 0.8, h * 0.012);
      g.fillStyle = '#e7a23a'; g.beginPath(); g.ellipse(w * 0.5, h * 0.09, w * 0.07, w * 0.11, 0.6, 0, 7); g.fill();
      g.fillStyle = '#3f7d5c'; g.font = `800 ${w * 0.14}px ${FONT}`; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText('한빛', w * 0.5, h * 0.155);
      g.fillStyle = '#4b5560'; g.font = `800 ${w * 0.36}px ${FONT}`; g.fillText(label, w * 0.5, h * 0.27);
      g.fillStyle = '#5a6672'; for (let f = 0; f < 15; f++) { const y = h * 0.36 + f * h * 0.042; g.fillRect(w * 0.44, y, w * 0.12, h * 0.02); }
    }));
    return new THREE.MeshStandardMaterial({ map, roughness: 0.85 });
  });

  for (const b of BUILDINGS) {
    const h = heightOf(b), seed = [...b.id].reduce((s, ch) => (s * 31 + ch.charCodeAt(0)) % 997, 7);
    solids.push({ x0: b.x0, x1: b.x1, z0: b.z0, z1: b.z1, y0: -3, y1: h + 1, id: b.id });
    if (BACKGROUND.has(b.id)) { background(b); continue; }
    if (b.kind === 'apartment') {
      const long = fac('apartment', 15, 16, seed);
      shell(b.x0, b.x1, b.z0, b.z1, h, { long, short: gable(b.label) });
      // balcony slabs + fins on the south face, machine rooms and tank on the roof
      for (let f = 1; f < 15; f++) stat.add(kit.box(b.x1 - b.x0, 0.14, 0.35, parapetMat, { x: (b.x0 + b.x1) / 2, y: CURB + f * 2.8 - 0.05, z: b.z1 + 0.17 }));
      for (let x = b.x0 + 6; x < b.x1; x += 12) stat.add(kit.box(0.3, h - 0.4, 0.45, parapetMat, { x, y: CURB, z: b.z1 + 0.22 }));
      for (const x of [b.x0 + 14, b.x1 - 14]) stat.add(kit.box(5, 3.2, 4.5, parapetMat, { x, y: h, z: (b.z0 + b.z1) / 2 }), kit.box(5.4, 0.2, 4.9, roofMat, { x, y: h + 3.2, z: (b.z0 + b.z1) / 2 }));
      stat.add(kit.box(4, 2.2, 2.6, kit.mat('paint:#5f87b0', { roughness: 0.45 }), { x: (b.x0 + b.x1) / 2, y: h + 0.4, z: (b.z0 + b.z1) / 2 }));
      stat.add(kit.cyl(0.04, 0.04, 6, metal, { x: b.x1 - 6, y: h, z: b.z0 + 2 }, 6));
      continue;
    }
    if (b.kind === 'warehouse') { warehouse(b, h); continue; }
    const kind = b.kind === 'mart' ? 'shop' : b.kind;
    const W = b.x1 - b.x0, D = b.z1 - b.z0;
    const colsL = Math.max(3, Math.round(W / 3.6)), colsS = Math.max(2, Math.round(D / 3.6));
    const mats = { long: fac(kind, b.floors, colsL, seed), short: fac(kind, b.floors, colsS, seed + 1) };
    if (b.id === 'culture') mats.parapet = kit.mat('paint:#8a8f94', { roughness: 0.6 });
    shell(b.x0, b.x1, b.z0, b.z1, h, mats, { hole: holes[b.id], y0: b.id === 'culture' ? -0.9 : CURB });
    if (b.kind !== 'shop' || b.floors > 1) roofKit(b.x0, b.x1, b.z0, b.z1, h, kit.rng(seed), false);
  }

  function background(b) {
    const W = b.x1 - b.x0, D = b.z1 - b.z0, nx = Math.max(1, Math.round(W / 17)), nz = Math.max(1, Math.round(D / 17)), gap = 3;
    const cw = (W - gap * (nx - 1)) / nx, cd = (D - gap * (nz - 1)) / nz, rr = kit.rng(b.x0 * 13 + b.z0);
    for (let i = 0; i < nx; i++) for (let j = 0; j < nz; j++) {
      const x0 = b.x0 + i * (cw + gap), z0 = b.z0 + j * (cd + gap), floors = Math.max(3, b.floors + [-1, 0, 0, 1][(rr() * 4) | 0]);
      const kind = b.kind === 'shop' || rr() < 0.3 ? 'shop' : 'villa', v = (rr() * 3) | 0;
      const tint = kind === 'villa' ? ['#ffffff', '#d9c7b8', '#f2d5c0'][v] : ['#ffffff', '#e6e0d2', '#cfd6dc'][v];
      const fh = FH[kind], h = CURB + floors * fh, hit = b.id === 's2' && i === 0 && j === 0;
      const keep = stat; if (hit) { stat = strike.group; strike.box = { x0, x1: x0 + cw, z0, z1: z0 + cd, h }; }
      shell(x0, x0 + cw, z0, z0 + cd, h, { long: fac(kind, floors, 4, v + 1, tint), short: fac(kind, floors, 4, v + 1, tint) }, { perL: 14, perS: 14 });
      roofKit(x0, x0 + cw, z0, z0 + cd, h, rr, kind === 'villa' && rr() < 0.7);
      stat = keep;
    }
  }

  function warehouse(b, h) {
    const ribs = kit.memo('town:corrugated', () => {
      const c = kit.canvas(256, 256, (g, w) => { for (let x = 0; x < w; x += 8) { const k = Math.sin(x / 8 * Math.PI) > 0 ? 1 : 0; g.fillStyle = k ? '#a9b3b9' : '#8e989e'; g.fillRect(x, 0, 8, w); } g.fillStyle = 'rgba(80,60,40,0.08)'; g.fillRect(0, w * 0.85, w, w * 0.15); });
      const map = kit.texture(c, { repeat: [1 / 4, 1 / 10] });
      const nrm = kit.texture(kit.normalFrom(kit.canvas(256, 256, (g, w) => { for (let x = 0; x < w; x++) { const v = 128 + 100 * Math.sin(x / 8 * Math.PI); g.fillStyle = `rgb(${v},${v},${v})`; g.fillRect(x, 0, 1, w); } }), 3), { repeat: [1 / 4, 1 / 10], color: false });
      return new THREE.MeshStandardMaterial({ map, normalMap: nrm, roughness: 0.5, metalness: 0.6 });
    });
    const W = b.x1 - b.x0, D = b.z1 - b.z0;
    // world-metre UVs so ribs keep their size: u range = width in metres, v = height in metres
    for (const [side, c, a0, a1] of [['s', b.z1, b.x0, b.x1], ['n', b.z0, b.x0, b.x1], ['e', b.x1, b.z0, b.z1], ['w', b.x0, b.z0, b.z1]]) {
      stat.add(facePlane(side, c, a0, a1, CURB, h, ribs, { u1: a1 - a0, v1: h - CURB }));
    }
    stat.add(new THREE.Mesh(new THREE.PlaneGeometry(W, D).rotateX(-Math.PI / 2).translate((b.x0 + b.x1) / 2, h, (b.z0 + b.z1) / 2), kit.mat('paint:#7d878d', { roughness: 0.5, metalness: 0.5 })));
    // loading docks on the yard side (south): dock pit edge, bumpers, roll-up doors, canopy
    const dark = kit.mat('paint:#16191c', { roughness: 0.9 }), shutter = kit.memo('town:dockShutter', () => new THREE.MeshStandardMaterial({
      map: kit.texture(kit.canvas(128, 256, (g, w, hh) => { g.fillStyle = '#c3c7c9'; g.fillRect(0, 0, w, hh); g.fillStyle = '#9ea3a6'; for (let y = 0; y < hh; y += 10) g.fillRect(0, y, w, 3); })), roughness: 0.5, metalness: 0.5,
    }));
    for (let i = 0; i < 6; i++) {
      const x = 106 + i * 3.2, open = i === 1 || i === 4;
      stat.add(facePlane('s', b.z1 + 0.02, x - 1.3, x + 1.3, CURB + 1.1, CURB + 4.1, dark, { cast: false }));
      stat.add(facePlane('s', b.z1 + 0.06, x - 1.3, x + 1.3, open ? CURB + 3.3 : CURB + 1.1, CURB + 4.1, shutter, { cast: false }));
      stat.add(kit.box(0.25, 0.5, 0.2, kit.mat('rubber'), { x: x - 1.1, y: CURB + 0.7, z: b.z1 + 0.1 }), kit.box(0.25, 0.5, 0.2, kit.mat('rubber'), { x: x + 1.1, y: CURB + 0.7, z: b.z1 + 0.1 }));
      stat.add(kit.box(0.5, 0.06, 0.06, kit.mat('hazard'), { x, y: CURB + 4.15, z: b.z1 + 0.05 }));
    }
    stat.add(kit.box(19.6, 1.0, 0.9, kit.surface('concrete'), { x: 114, y: CURB, z: b.z1 + 0.45 }));
    stat.add(kit.box(19.6, 0.04, 0.12, kit.mat('hazard'), { x: 114, y: CURB + 1.0, z: b.z1 + 0.85, cast: false }));
    stat.add(kit.box(20.5, 0.3, 4, steel, { x: 114, y: CURB + 4.6, z: b.z1 + 2 }));
    solids.push({ x0: 104.2, x1: 123.8, z0: b.z1, z1: b.z1 + 0.9, y0: -1, y1: CURB + 1, id: 'dock' });
  }

  // Filler blocks outside the bounds so the street ends in a city, not a plain (no shadows, merged).
  const ffac = fac('apartment', 18, 8, 77), fvil = fac('villa', 5, 4, 78, '#e8d2c0'), fshop = fac('shop', 4, 4, 79, '#e0dccf');
  const fr = kit.rng(99);
  for (let x = -300; x <= 300; x += 36) for (let z = -290; z <= 290; z += 34) {
    const ax = Math.abs(x), az = Math.abs(z);
    if (ax < BOUNDS.x1 + 14 && az < BOUNDS.z1 + 14) continue;
    if (az < 20 || Math.abs(Math.abs(x) - 50) < 16) continue; // keep 연무로 / 문화로 / 한빛로 corridors open
    if (fr() < 0.18) continue;
    const big = fr() < 0.35, w = big ? 34 + fr() * 10 : 16 + fr() * 12, d = big ? 12 + fr() * 2 : 12 + fr() * 10;
    const x0 = x - w / 2 + (fr() - 0.5) * 6, z0 = z - d / 2 + (fr() - 0.5) * 6;
    const floors = big ? 15 + (fr() * 8 | 0) : 3 + (fr() * 3 | 0);
    const h = floors * (big ? 2.8 : 3);
    const long = big ? ffac : fr() < 0.5 ? fvil : fshop;
    shell(x0, x0 + w, z0, z0 + d, h, { long, short: long }, { far: true, perL: big ? 24 : 14, perS: big ? 24 : 14, y0: 0 });
  }
  far.traverse(o => { if (o.isMesh) { o.castShadow = false; o.receiveShadow = false; } });

  kit.merge(stat); kit.merge(far); kit.merge(strike.group);
  return { group, solids, strike };
}
