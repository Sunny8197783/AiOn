// 베란다 수납장: a white open shelving cabinet against the east partition of the 302 balcony, plus a floor row for
// extra 2 L water. Every stock kind is one InstancedMesh whose drawn count follows setStock({ itemId: count }), so the
// shelf visibly fills up or empties as the player buys, packs and uses things.
import * as THREE from 'three';
import { HOME } from './apartment-home.js';
import { FONT } from '../engine/kit.js';

const BF = HOME.bf, ZA = 8.12, ZZ = 9.28, XF = -0.13, XB = 0.26; // inner box: front x, back x
const SHELF = [0.08, 0.48, 0.88, 1.28, 1.66].map(h => BF + h);

export function buildStock(B, g, dyn) {
  const { kit, M } = B, r = kit.rng(302);
  const white = kit.mat('paint:#f1efe9', { roughness: 0.5 });
  // carcass: sides, back, top, plinth, shelves; a cardboard box and a toolbox on top
  g.add(kit.box(0.44, 2.0, 0.02, white, { x: 0.07, y: BF, z: ZA - 0.01 }), kit.box(0.44, 2.0, 0.02, white, { x: 0.07, y: BF, z: ZZ + 0.01 }));
  g.add(kit.box(0.02, 2.0, 1.2, white, { x: XB + 0.02, y: BF, z: (ZA + ZZ) / 2 }), kit.box(0.44, 0.02, 1.2, white, { x: 0.07, y: BF + 1.98, z: (ZA + ZZ) / 2 }));
  for (const y of SHELF) g.add(kit.box(0.42, 0.02, 1.16, white, { x: 0.065, y: y - 0.02, z: (ZA + ZZ) / 2 }));
  g.add(kit.box(0.42, 0.06, 1.16, kit.mat('paint:#cfcbc2', { roughness: 0.6 }), { x: 0.065, y: BF, z: (ZA + ZZ) / 2 }));
  g.add(kit.box(0.36, 0.22, 0.46, M.cardboard, { x: 0.06, y: BF + 2.0, z: 8.45, ry: 0.05 }), kit.box(0.2, 0.14, 0.4, kit.mat('paint:#b8322a', { roughness: 0.45, metalness: 0.3 }), { x: 0.05, y: BF + 2.0, z: 9.0 }));
  B.solid(XF - 0.04, HOME.x1, ZA - 0.03, ZZ + 0.03, BF, BF + 2.25);

  // ---- item geometries (base at y = 0) and materials ----
  const vc = (geo, colorAt) => { // per-vertex colours from (y, normal)
    const p = geo.attributes.position, n = geo.attributes.normal, c = new Float32Array(p.count * 3), col = new THREE.Color();
    for (let i = 0; i < p.count; i++) { col.set(colorAt(p.getY(i), n.getY(i))); c.set([col.r, col.g, col.b], i * 3); }
    geo.setAttribute('color', new THREE.BufferAttribute(c, 3)); return geo;
  };
  const prof = [[0, 0], [0.044, 0], [0.051, 0.008], [0.051, 0.109], [0.0512, 0.11], [0.0512, 0.19], [0.051, 0.191], [0.051, 0.2], [0.049, 0.215], [0.03, 0.258], [0.016, 0.283], [0.016, 0.296], [0.0172, 0.297], [0.0172, 0.315], [0, 0.315]];
  const bottle = vc(new THREE.LatheGeometry(prof.map(([x, y]) => new THREE.Vector2(x, y)), 14), (y) => y > 0.2965 ? '#1f5fbf' : y > 0.1095 && y < 0.1905 ? '#2c7fd1' : '#d4ecf6');
  const can = vc(new THREE.CylinderGeometry(0.043, 0.043, 0.046, 18).translate(0, 0.023, 0), (y, ny) => Math.abs(ny) > 0.5 ? '#c9cdd1' : '#d9a62c');
  const butane = vc(new THREE.CylinderGeometry(0.033, 0.033, 0.2, 14, 10).translate(0, 0.1, 0), (y, ny) => ny > 0.5 || y > 0.185 ? '#c9cdd1' : y > 0.08 && y < 0.125 ? '#f4f1ea' : '#e0661a');
  const roll = new THREE.CylinderGeometry(0.055, 0.055, 0.1, 16).translate(0, 0.05, 0);
  const cube = (sx, sy, sz) => new THREE.BoxGeometry(sx, sy, sz).translate(0, sy / 2, 0);
  const vmat = (rough, metal = 0) => new THREE.MeshStandardMaterial({ vertexColors: true, roughness: rough, metalness: metal });
  const print = (key, bg, fg, lines, art) => new THREE.MeshStandardMaterial({ roughness: 0.55, map: B.tex('stock:' + key, 256, 128, (c, w, h) => {
    c.fillStyle = bg; c.fillRect(0, 0, w, h); art?.(c, w, h);
    c.fillStyle = fg; c.textAlign = 'center'; c.textBaseline = 'middle';
    lines.forEach((l, i) => { c.font = `${i ? 600 : 800} ${i ? 22 : 36}px ${FONT}`; c.fillText(l, w / 2, h * (lines.length === 1 ? 0.5 : 0.36 + i * 0.36)); });
  }) });
  const cross = (c, w, h) => { c.fillStyle = '#d7262d'; c.fillRect(w * 0.08, h * 0.3, 40, 14); c.fillRect(w * 0.08 + 13, h * 0.3 - 13, 14, 40); };
  const bowl = (c, w, h) => { c.fillStyle = '#ffd34d'; c.beginPath(); c.arc(w * 0.82, h * 0.62, 26, 0, Math.PI); c.fill(); };
  const foil = (c, w, h) => { const gr = c.createLinearGradient(0, 0, w, h); gr.addColorStop(0, '#d7b65a'); gr.addColorStop(0.5, '#f4e3a0'); gr.addColorStop(1, '#b8943c'); c.fillStyle = gr; c.fillRect(0, 0, w, h); };

  // ---- kinds: [id, geometry, material, per unit, slot blocks]. Block: { z0, z1, nx, nz, ny, y, h, s } ----
  const KINDS = [
    ['water_2l', bottle, vmat(0.1), 1, [{ z0: 8.14, z1: 9.0, nx: 2, nz: 8, ny: 1, y: SHELF[0] }, { floor: true }]],
    ['water_500', bottle, vmat(0.1), 1, [{ z0: 9.02, z1: 9.26, nx: 5, nz: 3, ny: 1, y: SHELF[0], s: [0.64, 0.66, 0.64] }]],
    ['ramen', cube(0.14, 0.055, 0.19), print('ramen', '#d8312a', '#fff3c4', ['얼큰라면', '5개입'], bowl), 1, [{ z0: 8.14, z1: 8.74, nx: 2, nz: 3, ny: 4, y: SHELF[1], h: 0.056 }]],
    ['rice_instant', cube(0.115, 0.04, 0.13), print('rice', '#f7f6f2', '#c4161c', ['즉석밥', '210g']), 1, [{ z0: 8.78, z1: 9.26, nx: 3, nz: 3, ny: 4, y: SHELF[1], h: 0.041 }]],
    ['canned', can, vmat(0.35, 0.4), 1, [{ z0: 8.14, z1: 8.6, nx: 3, nz: 5, ny: 3, y: SHELF[2], h: 0.047 }]],
    ['batteries_aa', cube(0.02, 0.16, 0.11), print('aa', '#1d1d1f', '#f2c230', ['AA 알카라인', '4입']), 1, [{ z0: 8.62, z1: 8.84, nx: 6, nz: 2, ny: 1, y: SHELF[2] }]],
    ['candles', cube(0.07, 0.11, 0.2), print('candle', '#f6f0dc', '#8a5a1c', ['양초', '6입']), 1, [{ z0: 8.86, z1: 9.26, nx: 3, nz: 2, ny: 2, y: SHELF[2], h: 0.112 }]],
    ['gas_stove', cube(0.27, 0.1, 0.34), print('stove', '#2a2c30', '#f4f4f1', ['휴대용 가스레인지']), 1, [{ z0: 8.14, z1: 8.5, nx: 1, nz: 1, ny: 3, y: SHELF[3], h: 0.102 }]],
    ['butane', butane, vmat(0.35, 0.5), 4, [{ z0: 8.52, z1: 8.92, nx: 4, nz: 5, ny: 1, y: SHELF[3] }]],
    ['first_aid', cube(0.14, 0.1, 0.2), print('aid', '#f8f8f6', '#d7262d', ['구급함'], cross), 1, [{ z0: 8.95, z1: 9.26, nx: 2, nz: 1, ny: 2, y: SHELF[3], h: 0.102 }]],
    ['tissue', roll, kit.mat('paint:#f7f7f4', { roughness: 0.95 }), 1, [{ z0: 8.14, z1: 8.5, nx: 3, nz: 3, ny: 2, y: SHELF[4], h: 0.1 }]],
    ['blanket', cube(0.1, 0.03, 0.16), print('foil', '#d7b65a', '#5a4410', ['은박 담요'], foil), 1, [{ z0: 8.52, z1: 8.76, nx: 1, nz: 1, ny: 6, y: SHELF[4], h: 0.031 }]],
    ['wipes', cube(0.11, 0.07, 0.2), print('wipes', '#e9f5ec', '#2f8a4a', ['물티슈', '100매']), 1, [{ z0: 8.78, z1: 9.0, nx: 1, nz: 1, ny: 3, y: SHELF[4], h: 0.071 }]],
    ['water_jug', cube(0.18, 0.06, 0.22), print('jug', '#2f6fb2', '#ffffff', ['접이식 물통', '10L']), 1, [{ z0: 9.02, z1: 9.26, nx: 1, nz: 1, ny: 2, y: SHELF[4], h: 0.061 }]],
  ];
  const kinds = KINDS.map(([id, geo, mat, per, blocks]) => {
    const slots = [];
    for (const b of blocks) {
      if (b.floor) { for (let row = 0; row < 2; row++) for (let i = 0; i < 12; i++) slots.push({ x: -0.32 - i * 0.108, y: BF, z: 9.25 - row * 0.108, ry: r() * 6.28 }); continue; }
      const { z0, z1, nx, nz, ny, y, h = 0, s } = b, dx = (XB - XF) / nx, dz = (z1 - z0) / nz;
      for (let k = 0; k < ny; k++) for (let i = 0; i < nx; i++) for (let j = 0; j < nz; j++) slots.push({
        x: XF + (i + 0.5) * dx + (r() - 0.5) * 0.008, y: y + k * h, z: z0 + (j + 0.5) * dz + (r() - 0.5) * 0.008,
        ry: (r() - 0.5) * (geo === bottle || geo === can || geo === butane ? 6 : 0.12), sx: s?.[0], sy: s?.[1], sz: s?.[2] });
    }
    const mesh = kit.instanced(geo, mat, slots, { cast: true }); mesh.count = 0; dyn.add(mesh);
    return { id, per, mesh, cap: slots.length };
  });
  B.point('homeStock', '베란다 수납장', -0.75, BF, 8.7, 'stock', 1.6);

  // counts: { itemId: n } (missing ids -> 0). Shown units = n * per, capped by the slots.
  function setStock(counts = {}) {
    for (const k of kinds) k.mesh.count = Math.max(0, Math.min(k.cap, Math.round((Number(counts[k.id]) || 0) * k.per)));
  }
  return { setStock, ids: kinds.map(k => k.id) };
}
