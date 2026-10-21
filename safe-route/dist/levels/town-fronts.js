// Shop fronts and civic entrances: Hangul signboards from two atlases (one draw call each, light group
// 'shops'), fake lit interiors behind glass, awnings, canopies, 돌출간판, the convenience-store terrace,
// cafe deck, mart carts, flags (태극기) and the three notice boards whose text follows `notices`.
import * as THREE from 'three';
import { FONT } from '../engine/kit.js';
import { BUILDINGS, PLACES } from '../game/townmap.js';
import { CURB } from './town-plan.js';
import { board, sway } from './town-util.js';
import { heightOf } from './town-buildings.js';

const Y = CURB;
const B = Object.fromEntries(BUILDINGS.map(b => [b.id, b]));

// Horizontal signboards: [text, bg, fg, mark]. mark: 'B' brand disc, '약' pharmacy disc, 'line' accent bar.
const SIGNS = [
  ['바로편의점', '#0f8a6c', '#ffffff', 'B'], ['연무약국', '#1f7a3a', '#ffffff', '약'], ['연무분식', '#e8a32a', '#3a1d0e'], ['연무마트', '#c8312a', '#ffffff', 'line'],
  ['한결물류', '#ffffff', '#1d4f8c', 'line'], ['연무동 행정복지센터', '#2b4c7e', '#ffffff'], ['연무 생활문화센터', '#3e5a6e', '#ffffff'], ['카페 쉼표', '#2d2a26', '#f3e3c3'],
  ['관리사무소', '#3f6b4a', '#ffffff'], ['한빛아파트 103동', '#f4f0e6', '#3f7d5c'], ['수학 · 영어 학원', '#1d3f8f', '#ffffff'], ['치킨 · 호프', '#e7c44a', '#5a2a10'],
  ['헤어살롱 연', '#7a3a6a', '#ffffff'], ['크린세탁', '#2f7fb5', '#ffffff'], ['한빛공인중개사', '#c84a2a', '#ffffff'], ['할매국밥', '#3b2a1e', '#f2d28a'],
  ['PC방', '#141c3a', '#5fd0ff'], ['연무태권도', '#b52a2a', '#ffffff'], ['꽃집 봄', '#f3d6dc', '#8a2a4a'], ['연무정육점', '#a8262a', '#ffffff'],
  ['엄마손반찬', '#f2e6c8', '#4a3a20'], ['연무철물', '#2f3a44', '#f2c230'], ['한빛아파트 102동', '#f4f0e6', '#3f7d5c'], ['한빛아파트 101동', '#f4f0e6', '#3f7d5c'],
];
const VSIGNS = [['약', '#1f7a3a'], ['학원', '#1d3f8f'], ['치킨', '#c8312a'], ['국밥', '#3b2a1e'], ['PC', '#141c3a'], ['미용실', '#7a3a6a'], ['노래방', '#5a2a8a'], ['세탁', '#2f7fb5']];
const INTERIORS = ['store', 'pharmacy', 'snack', 'mart', 'cafe', 'lobby', 'office', 'civic'];

// Atlas of equal slots; plane(i, w, h) returns a PlaneGeometry whose UVs cover slot i.
function atlas(kit, key, W, H, cols, rows, paint) {
  const sw = W / cols, sh = H / rows;
  const tex = kit.memo(`town:atlas:${key}`, () => kit.texture(kit.canvas(W, H, (g) => {
    for (let i = 0; i < cols * rows; i++) { g.save(); g.translate((i % cols) * sw, Math.floor(i / cols) * sh); g.beginPath(); g.rect(0, 0, sw, sh); g.clip(); paint(g, i, sw, sh); g.restore(); }
  }), { repeat: [1, 1] }));
  function plane(i, w, h) {
    const g = new THREE.PlaneGeometry(w, h), uv = g.attributes.uv, pad = 2;
    const u0 = ((i % cols) * sw + pad) / W, u1 = ((i % cols + 1) * sw - pad) / W;
    const v1 = 1 - (Math.floor(i / cols) * sh + pad) / H, v0 = 1 - ((Math.floor(i / cols) + 1) * sh - pad) / H;
    for (let k = 0; k < uv.count; k++) uv.setXY(k, u0 + uv.getX(k) * (u1 - u0), v0 + uv.getY(k) * (v1 - v0));
    return g;
  }
  return { tex, plane };
}

function paintSign(g, i, w, h) {
  const [text, bg, fg, mark] = SIGNS[i] || ['', '#333', '#fff'];
  g.fillStyle = bg; g.fillRect(0, 0, w, h);
  g.fillStyle = 'rgba(255,255,255,0.12)'; g.fillRect(0, 0, w, h * 0.08);
  let x0 = w * 0.06;
  if (mark === 'B' || mark === '약') {
    g.fillStyle = mark === 'B' ? '#f39a1e' : '#ffffff'; g.beginPath(); g.arc(h * 0.62, h / 2, h * 0.36, 0, 7); g.fill();
    g.fillStyle = mark === 'B' ? '#ffffff' : '#c8312a'; g.font = `900 ${h * 0.48}px ${FONT}`; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(mark, h * 0.62, h * 0.53);
    x0 = h * 1.15;
  }
  if (mark === 'line') { g.fillStyle = fg; g.fillRect(0, h * 0.86, w, h * 0.06); }
  let size = h * 0.6; g.font = `800 ${size}px ${FONT}`;
  while (g.measureText(text).width > w - x0 - w * 0.05 && size > 10) { size -= 2; g.font = `800 ${size}px ${FONT}`; }
  g.fillStyle = fg; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(text, (x0 + w * 0.95) / 2, h * 0.53);
}
function paintVSign(g, i, w, h) {
  const [text, bg] = VSIGNS[i] || ['', '#333'];
  g.fillStyle = bg; g.fillRect(0, 0, w, h); g.strokeStyle = '#ffffff'; g.lineWidth = 4; g.strokeRect(6, 6, w - 12, h - 12);
  const chars = [...text], step = Math.min(w * 0.9, (h - 30) / chars.length);
  g.fillStyle = '#ffffff'; g.font = `800 ${step * 0.78}px ${FONT}`; g.textAlign = 'center'; g.textBaseline = 'middle';
  chars.forEach((c, k) => g.fillText(c, w / 2, 15 + step * (k + 0.5)));
}
function paintInterior(g, i, w, h) {
  const kind = INTERIORS[i], r = (s => () => ((s = (s * 16807) % 2147483647) / 2147483647))(i * 97 + 13);
  const warm = kind === 'cafe' || kind === 'snack' || kind === 'lobby';
  g.fillStyle = warm ? '#5a4030' : kind === 'civic' || kind === 'office' ? '#c9ccc8' : '#e8ece9'; g.fillRect(0, 0, w, h);
  g.fillStyle = warm ? '#3a2a20' : '#b8bcb8'; g.fillRect(0, h * 0.78, w, h * 0.22);
  for (let x = 20; x < w; x += 70) { g.fillStyle = warm ? '#ffd9a0' : '#ffffff'; if (kind === 'cafe') { g.beginPath(); g.arc(x + 20, h * 0.2, 9, 0, 7); g.fill(); g.fillRect(x + 19, 0, 2, h * 0.2); } else g.fillRect(x, h * 0.04, 50, 6); }
  if (kind === 'store' || kind === 'mart' || kind === 'pharmacy') {
    const n = kind === 'pharmacy' ? 3 : 5;
    for (let s = 0; s < n; s++) {
      const x = 14 + s * (w - 28) / n, sw = (w - 28) / n - 14;
      g.fillStyle = '#d0d4d2'; g.fillRect(x, h * 0.22, sw, h * 0.56);
      for (let row = 0; row < 4; row++) for (let k = 0; k < 7; k++) { g.fillStyle = `hsl(${r() * 360 | 0},${50 + r() * 30}%,${45 + r() * 20}%)`; g.fillRect(x + 4 + k * sw / 7, h * 0.25 + row * h * 0.13, sw / 7 - 3, h * 0.1); }
    }
    if (kind === 'store') { g.fillStyle = '#cfe8ff'; g.fillRect(w * 0.72, h * 0.18, w * 0.25, h * 0.6); g.fillStyle = '#0f8a6c'; g.fillRect(0, h * 0.12, w, 8); }
    if (kind === 'pharmacy') { g.fillStyle = '#1f7a3a'; g.fillRect(w * 0.8, h * 0.3, 40, 12); g.fillRect(w * 0.8 + 14, h * 0.3 - 14, 12, 40); }
  } else if (kind === 'snack') {
    g.fillStyle = '#c8312a'; g.fillRect(w * 0.05, h * 0.12, w * 0.5, h * 0.2); g.fillStyle = '#fff'; g.font = `700 ${h * 0.1}px ${FONT}`; g.fillText('떡볶이 · 김밥 · 순대', w * 0.08, h * 0.25);
    g.fillStyle = '#8a8f94'; g.fillRect(w * 0.6, h * 0.45, w * 0.35, h * 0.33); for (let k = 0; k < 4; k++) { g.fillStyle = '#c8312a'; g.fillRect(w * 0.08 + k * 60, h * 0.6, 30, h * 0.18); }
  } else if (kind === 'cafe') {
    for (let k = 0; k < 5; k++) { g.fillStyle = '#8a5a3a'; g.fillRect(30 + k * 95, h * 0.58, 60, 8); g.fillRect(56 + k * 95, h * 0.58, 8, h * 0.2); }
    g.fillStyle = '#2d2a26'; g.fillRect(w * 0.7, h * 0.35, w * 0.28, h * 0.43); g.fillStyle = '#f3e3c3'; g.fillRect(w * 0.72, h * 0.12, w * 0.24, h * 0.16);
  } else if (kind === 'lobby') {
    g.fillStyle = '#9a8f80'; g.fillRect(w * 0.08, h * 0.25, w * 0.35, h * 0.4); for (let y = 0; y < 5; y++) for (let x = 0; x < 6; x++) { g.fillStyle = '#c9c2b4'; g.fillRect(w * 0.09 + x * w * 0.057, h * 0.27 + y * h * 0.075, w * 0.05, h * 0.06); }
    g.fillStyle = '#6a7a8a'; g.fillRect(w * 0.6, h * 0.2, w * 0.18, h * 0.58);
  } else {
    g.fillStyle = '#7a8a94'; g.fillRect(w * 0.1, h * 0.45, w * 0.5, h * 0.33); g.fillStyle = '#2b4c7e'; g.fillRect(w * 0.15, h * 0.15, w * 0.3, h * 0.12);
    for (let k = 0; k < 6; k++) { g.fillStyle = '#4a5560'; g.fillRect(w * 0.65 + k * 25, h * 0.55, 14, h * 0.23); }
  }
  const glare = g.createLinearGradient(0, 0, w, h); glare.addColorStop(0, 'rgba(255,255,255,0.18)'); glare.addColorStop(0.5, 'rgba(255,255,255,0)'); g.fillStyle = glare; g.fillRect(0, 0, w, h);
}

export function buildFronts(kit) {
  const group = new THREE.Group(); group.name = 'fronts';
  const stat = new THREE.Group(); group.add(stat);
  const solids = [];
  const block = (x0, x1, z0, z1, y1 = 2.5, id = 'front') => solids.push({ x0, x1, z0, z1, y0: -0.5, y1, id });
  const steel = kit.mat('steel'), dark = kit.mat('darkMetal'), glass = kit.mat('glass', { opacity: 0.35 }), white = kit.mat('whiteWall');

  const hs = atlas(kit, 'signs', 1024, 1020, 2, 12, paintSign);
  const vs = atlas(kit, 'vsigns', 1024, 512, 8, 1, paintVSign);
  const ins = atlas(kit, 'interiors', 1024, 1024, 2, 4, paintInterior);
  const glow = (map, group, v) => kit.lit(group, new THREE.MeshStandardMaterial({ map, emissiveMap: map, emissive: 0xffffff, emissiveIntensity: v, roughness: 0.45 }), v);
  const signMat = kit.memo('town:signMat', () => glow(hs.tex, 'shops', 0.9)), vMat = kit.memo('town:vsignMat', () => glow(vs.tex, 'shops', 1.1));
  const shopIn = kit.memo('town:shopInterior', () => glow(ins.tex, 'shops', 0.75)), civIn = kit.memo('town:civInterior', () => glow(ins.tex, 'indoor', 0.6));

  // Flat panel on a face. side: n|s|e|w (outward normal), a = along-face centre, y = centre height.
  const onFace = (geo, mat, side, c, a, y, out = 0.06) => {
    const m = new THREE.Mesh(geo, mat), o = side === 'n' || side === 'w' ? -out : out;
    if (side === 'n' || side === 's') m.position.set(a, y, c + o); else m.position.set(c + o, y, a);
    m.rotation.y = { s: 0, n: Math.PI, e: Math.PI / 2, w: -Math.PI / 2 }[side];
    stat.add(m); return m;
  };
  const faceOf = (b, side) => ({ n: b.z0, s: b.z1, e: b.x1, w: b.x0 }[side]);
  const sign = (i, b, side, a, y, w, h, depth = 0.18) => {
    const c = faceOf(b, side);
    onFace(new THREE.BoxGeometry(w + 0.1, h + 0.1, depth), dark, side, c, a, y, depth / 2);
    onFace(hs.plane(i, w, h), signMat, side, c, a, y, depth + 0.01);
  };
  const shopWindow = (k, b, side, a, y0, w, h, civic = false) => {
    const c = faceOf(b, side);
    onFace(ins.plane(INTERIORS.indexOf(k), w, h), civic ? civIn : shopIn, side, c, a, y0 + h / 2, 0.05);
    onFace(new THREE.PlaneGeometry(w, h), glass, side, c, a, y0 + h / 2, 0.08).userData.keep = true;
    for (const s of [-1, 1]) onFace(new THREE.BoxGeometry(0.08, h, 0.12), dark, side, c, a + s * w / 2, y0 + h / 2, 0.1);
    onFace(new THREE.BoxGeometry(w, 0.1, 0.14), dark, side, c, a, y0 + h, 0.1);
    for (let x = -w / 2 + 2.4; x < w / 2 - 0.5; x += 2.4) onFace(new THREE.BoxGeometry(0.05, h, 0.08), dark, side, c, a + x, y0 + h / 2, 0.1);
  };
  const vsign = (i, x, y, z, ry) => {
    const nx = Math.sin(ry) * 0.056, nz = Math.cos(ry) * 0.056;
    for (const s of [1, -1]) { const m = new THREE.Mesh(vs.plane(i, 0.55, 2.4), vMat); m.position.set(x + nx * s, y, z + nz * s); m.rotation.y = s > 0 ? ry : ry + Math.PI; stat.add(m); }
    stat.add(kit.box(0.6, 2.5, 0.1, dark, { x, y: y - 1.25, z, ry }));
  };

  // ---- 바로편의점 (north face) ----
  const st = B.store;
  shopWindow('store', st, 'n', 36, Y + 0.1, 11.2, 2.75);
  sign(0, st, 'n', 36, Y + 3.35, 11.6, 0.75);
  for (const [x, z] of [[33, 9.6], [40.5, 9.6]]) {
    stat.add(kit.cyl(0.42, 0.42, 0.04, white, { x, y: Y + 0.7, z }, 20), kit.cyl(0.03, 0.03, 0.7, steel, { x, y: Y, z }, 8), kit.cyl(0.025, 0.025, 2.2, steel, { x, y: Y + 0.7, z }, 6));
    const para = new THREE.Mesh(new THREE.ConeGeometry(1.15, 0.38, 8, 1, true), kit.memo('town:parasol', () => new THREE.MeshStandardMaterial({ side: THREE.DoubleSide, roughness: 0.8, map: kit.texture(kit.canvas(256, 64, (g, w, h) => { for (let k = 0; k < 8; k++) { g.fillStyle = k % 2 ? '#ffffff' : '#0f8a6c'; g.fillRect(k * w / 8, 0, w / 8, h); } })) })));
    para.position.set(x, Y + 2.7, z); para.castShadow = true; stat.add(para);
    for (let k = 0; k < 4; k++) { const a = k * Math.PI / 2 + 0.4, cx = x + Math.cos(a) * 0.68, cz = z + Math.sin(a) * 0.68; stat.add(kit.box(0.42, 0.04, 0.42, kit.mat('paint:#e9e9e4'), { x: cx, y: Y + 0.44, z: cz, ry: -a }), kit.box(0.42, 0.42, 0.04, kit.mat('paint:#e9e9e4'), { x: cx + Math.cos(a) * 0.2, y: Y + 0.46, z: cz + Math.sin(a) * 0.2, ry: -a + Math.PI / 2 })); }
    block(x - 0.95, x + 0.95, z - 0.95, z + 0.95, 1.2, 'storeTable');
  }
  const pack = kit.memo('town:waterPack', () => new THREE.MeshStandardMaterial({ roughness: 0.25, map: kit.texture(kit.canvas(128, 64, (g, w, h) => { g.fillStyle = '#cfe6f2'; g.fillRect(0, 0, w, h); for (let k = 0; k < 3; k++) { g.fillStyle = '#8fc3e0'; g.fillRect(6 + k * 41, 4, 34, h - 8); g.fillStyle = '#1f5fa8'; g.fillRect(6 + k * 41, h * 0.4, 34, 10); } })) }));
  for (let k = 0; k < 6; k++) stat.add(kit.box(0.38, 0.33, 0.26, pack, { x: 41.2 - (k % 2) * 0.4, y: Y + Math.floor(k / 2) * 0.33, z: 11.5 }));
  stat.add(kit.box(1.2, 0.8, 0.6, white, { x: 30.9, y: Y, z: 11.55 }), kit.box(1.15, 0.02, 0.55, glass, { x: 30.9, y: Y + 0.8, z: 11.55 }));
  block(40.7, 41.7, 11.3, 12, 1.2, 'storeStock'); block(30.2, 31.6, 11.2, 12, 1, 'freezer');

  // ---- 상가: pharmacy + snack bar, 2F academy, awning, protruding signs ----
  const sh = B.shopsS;
  shopWindow('pharmacy', sh, 'n', 19.4, Y + 0.1, 5.6, 2.7); shopWindow('snack', sh, 'n', 25.8, Y + 0.1, 5.4, 2.7);
  sign(1, sh, 'n', 19.4, Y + 3.2, 5.8, 0.7); sign(2, sh, 'n', 25.8, Y + 3.2, 5.6, 0.7); sign(10, sh, 'n', 22.5, Y + 5.4, 8, 0.8);
  const awning = kit.memo('town:awning', () => new THREE.MeshStandardMaterial({ side: THREE.DoubleSide, roughness: 0.85, map: kit.texture(kit.canvas(256, 64, (g, w, h) => { for (let k = 0; k < 12; k++) { g.fillStyle = k % 2 ? '#f2efe6' : '#c8312a'; g.fillRect(k * w / 12, 0, w / 12, h); } })) }));
  const aw = new THREE.Mesh(new THREE.PlaneGeometry(5.4, 1.3), awning); aw.position.set(25.8, Y + 2.62, 11.45); aw.rotation.x = -Math.PI / 2 + 0.42; aw.castShadow = true; stat.add(aw);
  vsign(0, 16.6, Y + 5.6, 11.3, Math.PI / 2); vsign(1, 28.6, Y + 5.6, 11.3, Math.PI / 2);

  // ---- 연무마트 (south face): big sign, entrance canopy, glass, X-banners, cart bay ----
  const mt = B.mart, mh = heightOf(mt);
  sign(3, mt, 's', -16, mh - 2.0, 14, 1.8, 0.3);
  shopWindow('mart', mt, 's', -16, Y + 0.1, 8, 3.0); shopWindow('mart', mt, 's', -29, Y + 0.6, 9, 2.4); shopWindow('mart', mt, 's', -3, Y + 0.6, 9, 2.4);
  stat.add(kit.box(9, 0.25, 3, kit.mat('paint:#d8d4ca', { roughness: 0.6 }), { x: -16, y: Y + 3.4, z: -22.5 }));
  for (const s of [-1, 1]) { stat.add(kit.box(0.2, 3.4, 0.2, steel, { x: -16 + s * 4.3, y: Y, z: -21.2 })); block(-16 + s * 4.3 - 0.15, -16 + s * 4.3 + 0.15, -21.35, -21.05, 3, 'martCanopy'); }
  for (const [x, t] of [[-21.6, ['가을 정기 세일', '생활용품 최대 30%']], [-10.4, ['신선 코너', '오늘의 과일']]]) {
    const ban = kit.sign(t, { w: 0.6, h: 1.6, bg: '#c8312a', single: false }); ban.position.set(x, Y + 1.1, -21.6); stat.add(ban, kit.box(0.5, 0.05, 0.4, dark, { x, y: Y, z: -21.6 }));
  }
  const cartM = kit.memo('town:cartWire', () => new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(64, 64, (g, w) => { g.strokeStyle = '#d0d4d8'; g.lineWidth = 3; for (let k = 2; k < w; k += 9) { g.beginPath(); g.moveTo(k, 0); g.lineTo(k, w); g.stroke(); g.beginPath(); g.moveTo(0, k); g.lineTo(w, k); g.stroke(); } })), alphaTest: 0.5, side: THREE.DoubleSide, metalness: 0.8, roughness: 0.3 }));
  for (let k = 0; k < 5; k++) { const x = -8.2 + k * 0.32; stat.add(kit.box(0.55, 0.45, 0.9, cartM, { x, y: Y + 0.45, z: -21.6, cast: false }), kit.box(0.5, 0.04, 0.1, kit.mat('paint:#c8312a'), { x, y: Y + 1.0, z: -21.15 })); }
  block(-8.6, -6.4, -22.1, -21.1, 1.2, 'carts');

  // ---- 한결물류 (south face) ----
  const of = B.office, oh = heightOf(of);
  sign(4, of, 's', 87, oh - 1.5, 12, 1.6, 0.25);
  shopWindow('office', of, 's', 87, Y + 0.1, 6, 2.8, true);
  stat.add(kit.box(7, 0.22, 2.6, kit.mat('glass', { opacity: 0.5 }), { x: 87, y: Y + 3.1, z: -20.7, cast: false }), kit.box(7.2, 0.12, 0.12, steel, { x: 87, y: Y + 3.1, z: -19.45 }));

  // ---- 행정복지센터 (south face): sign, canopy, doors, flags, notice board ----
  const ce = B.center, chh = heightOf(ce);
  sign(5, ce, 's', -89, chh - 1.6, 12, 1.2, 0.25);
  shopWindow('civic', ce, 's', -89, Y + 0.1, 5, 2.8, true); shopWindow('civic', ce, 's', -98, Y + 0.9, 7, 2.0, true); shopWindow('civic', ce, 's', -80, Y + 0.9, 7, 2.0, true);
  stat.add(kit.slab(6.4, 0.3, 3.2, kit.surface('concrete'), { x: -89, y: Y + 3.3, z: -26.4 }));
  for (const s of [-1, 1]) { stat.add(kit.box(0.35, 3.3, 0.35, white, { x: -89 + s * 2.9, y: Y, z: -25.1 })); block(-89 + s * 2.9 - 0.2, -89 + s * 2.9 + 0.2, -25.3, -24.9, 3.3, 'centerCanopy'); }
  const flags = [];
  [-102, -100.4, -98.8].forEach((x, k) => {
    stat.add(kit.cyl(0.04, 0.06, 7, metal(kit), { x, y: Y, z: -26.2 }, 10), kit.box(0.3, 0.3, 0.3, kit.surface('concrete'), { x, y: Y, z: -26.2 }));
    block(x - 0.2, x + 0.2, -26.4, -26, 3, 'flagpole');
    flags.push(flag(kit, k, x, Y + 6.9, -26.2));
  });
  for (const f of flags) group.add(f);

  // ---- 카페 쉼표 (south face): sign, glass, awning, wooden deck with tables, menu board ----
  const cf = B.cafe;
  sign(7, cf, 's', -118, Y + 3.45, 6, 0.8);
  shopWindow('cafe', cf, 's', -118, Y + 0.1, 13, 2.9);
  const awc = new THREE.Mesh(new THREE.PlaneGeometry(14, 1.6), kit.mat('paint:#2f4a3a', { roughness: 0.9, side: THREE.DoubleSide })); awc.position.set(-118, Y + 2.85, -15.35); awc.rotation.x = -Math.PI / 2 + 0.35; awc.castShadow = true; stat.add(awc);
  stat.add(kit.slab(15, 0.08, 2.6, kit.surface('wood', { color: 0xa47a52 }), { x: -118, y: Y, z: -14.7 }));
  for (const x of [-123.4, -121.2, -114.6, -112.4]) {
    stat.add(kit.cyl(0.32, 0.32, 0.03, kit.mat('paint:#3a2a20'), { x, y: Y + 0.74, z: -14.6 }, 16), kit.cyl(0.03, 0.03, 0.74, dark, { x, y: Y, z: -14.6 }, 6));
    for (const s of [-1, 1]) stat.add(kit.box(0.38, 0.04, 0.38, kit.surface('wood'), { x: x + s * 0.55, y: Y + 0.45, z: -14.6 }), kit.box(0.04, 0.45, 0.04, dark, { x: x + s * 0.55, y: Y, z: -14.6 }));
    block(x - 0.85, x + 0.85, -15.1, -14.1, 1, 'cafeTable');
  }
  const menu = kit.sign(['오늘의 커피', '아메리카노 2,500', '라떼 3,200'], { w: 0.55, h: 0.75, bg: '#1e2420', fg: '#f3e3c3', single: false });
  menu.position.set(-110.9, Y + 0.55, -13.3); menu.rotation.x = -0.15; stat.add(menu); block(-111.3, -110.5, -13.6, -13, 1, 'menuBoard');

  // ---- culture centre name signs, 관리사무소 ----
  const cu = B.culture, cuh = heightOf(cu);
  sign(6, cu, 'e', 31, cuh - 2.2, 10, 1.1, 0.25); sign(6, cu, 'n', -96, cuh - 2.0, 10, 1.1, 0.25);
  sign(8, B.aptOffice, 'n', 121, Y + 2.6, 4, 0.6);

  // ---- apartment entrances: canopy, columns, lobby glass, 동 sign ----
  for (const [id, x, si] of [['apt103', 78, 9], ['apt102', 88, 22], ['apt101', 88, 23]]) {
    const b = B[id], z = b.z0;
    stat.add(kit.slab(4.4, 0.25, 2.6, kit.surface('concrete', { color: 0xd9d4c8 }), { x, y: Y + 2.75, z: z - 1.3 }));
    for (const s of [-1, 1]) { stat.add(kit.box(0.3, 2.75, 0.3, white, { x: x + s * 2.0, y: Y, z: z - 2.4 })); block(x + s * 2 - 0.18, x + s * 2 + 0.18, z - 2.58, z - 2.22, 3, 'canopy'); }
    shopWindow('lobby', b, 'n', x, Y + 0.05, 2.6, 2.4, true);
    sign(si, b, 'n', x, Y + 3.45, 3.4, 0.5, 0.12);
    stat.add(kit.box(0.25, 0.4, 0.06, dark, { x: x + 1.6, y: Y + 1.2, z: z - 0.06 }));
  }

  // ---- background streets: shop signs and 돌출간판 on n1 (faces 시장길), villa name plates ----
  const n1 = B.n1, cells = [];
  { const W = n1.x1 - n1.x0, nx = Math.max(1, Math.round(W / 17)), cw = (W - 3 * (nx - 1)) / nx; for (let i = 0; i < nx; i++) cells.push([n1.x0 + i * (cw + 3), n1.x0 + i * (cw + 3) + cw]); }
  let gi = 0;
  for (const [a0, a1] of cells) for (const f of [0, 1]) {
    const w = (a1 - a0) / 2 - 0.6;
    for (const k of [0, 1]) sign(11 + (gi++ % 11), n1, 's', a0 + w / 2 + 0.4 + k * (w + 0.4), Y + 4.1 + f * 3.5, w, 0.75, 0.14);
  }
  cells.forEach(([a0], k) => vsign((2 + k * 2) % VSIGNS.length, a0 + 0.5, Y + 6.2, n1.z1 + 0.4, Math.PI / 2));
  vsign(3, cells[0][1] - 0.5, Y + 9.2, n1.z1 + 0.4, Math.PI / 2);

  // ---- notice boards (text follows the notices state) ----
  const boards = {};
  // back: +1 = board stands south of its point and faces north (-z); -1 = stands north, faces south.
  for (const [id, back] of [['shelterNotice', 1], ['aptNotice', 1], ['centerNotice', -1]]) {
    const p = PLACES[id], bd = board(kit, { w: 512, h: 384 }), b = back;
    const m = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 1.05), new THREE.MeshStandardMaterial({ map: bd.texture, roughness: 0.6 }));
    m.position.set(p.x, Y + 1.45, p.z + 0.25 * b); m.rotation.y = b > 0 ? Math.PI : 0; group.add(m);
    stat.add(kit.box(1.55, 1.2, 0.06, kit.mat('paint:#8a9198', { metalness: 0.6, roughness: 0.4 }), { x: p.x, y: Y + 0.85, z: p.z + 0.3 * b }), kit.box(1.7, 0.06, 0.32, dark, { x: p.x, y: Y + 2.05, z: p.z + 0.25 * b }));
    for (const s of [-1, 1]) stat.add(kit.box(0.07, 2.05, 0.07, dark, { x: p.x + s * 0.72, y: Y, z: p.z + 0.3 * b }));
    block(p.x - 0.85, p.x + 0.85, p.z + Math.min(0.15 * b, 0.45 * b), p.z + Math.max(0.15 * b, 0.45 * b), 2.2, 'noticeBoard');
    boards[id] = bd;
  }

  kit.merge(stat);
  const walk = [{ x0: -125.5, x1: -110.5, z0: -16, z1: -13.4, y: Y + 0.08 }];
  return { group, solids, walk, boards };
}

const metal = (kit) => kit.mat('metal');

// Flag on a pole top, sways in the wind. k: 0 태극기, 1 city flag, 2 연무동.
function flag(kit, k, x, y, z) {
  const map = kit.memo(`town:flag:${k}`, () => kit.texture(kit.canvas(256, 384, (g, CW, CH) => {
    const W = 3, H = 2; g.setTransform(0, -CH / W, CW / H, 0, 0, CH); // flag coords (W x H, hoist at fx 0) -> rotated canvas
    g.fillStyle = k === 0 ? '#ffffff' : k === 1 ? '#1d4fa0' : '#2f7a4a'; g.fillRect(0, 0, W, H);
    if (k === 0) {
      const cx = 1.5, cy = 1, r = 0.5;
      g.fillStyle = '#cd2e3a'; g.beginPath(); g.arc(cx, cy, r, Math.PI, 0); g.arc(cx + r / 2, cy, r / 2, 0, Math.PI, true); g.arc(cx - r / 2, cy, r / 2, 0, Math.PI); g.fill();
      g.fillStyle = '#0047a0'; g.beginPath(); g.arc(cx, cy, r, 0, Math.PI); g.arc(cx - r / 2, cy, r / 2, Math.PI, 0, true); g.arc(cx + r / 2, cy, r / 2, Math.PI, 0); g.fill();
      g.fillStyle = '#000000';
      const tri = (tx, ty, ang, pat) => { g.save(); g.translate(tx, ty); g.rotate(ang); pat.forEach((bar, i) => { const yy = -0.12 + i * 0.12; if (bar) g.fillRect(-0.25, yy, 0.5, 0.08); else { g.fillRect(-0.25, yy, 0.21, 0.08); g.fillRect(0.04, yy, 0.21, 0.08); } }); g.restore(); };
      tri(0.62, 0.42, -Math.PI / 3.3, [1, 1, 1]); tri(2.38, 1.58, -Math.PI / 3.3, [0, 0, 0]); tri(2.38, 0.42, Math.PI / 3.3, [0, 1, 0]); tri(0.62, 1.58, Math.PI / 3.3, [1, 0, 1]);
    } else {
      g.fillStyle = '#ffffff'; g.beginPath(); g.arc(1.5, 1, 0.42, 0, 7); g.fill();
      g.fillStyle = k === 1 ? '#1d4fa0' : '#2f7a4a'; g.font = `800 0.3px ${FONT}`; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(k === 1 ? '시' : '연무', 1.5, 1.02);
    }
  }), { repeat: [1, 1] }));
  const m = new THREE.MeshStandardMaterial({ map, side: THREE.DoubleSide, roughness: 0.9 });
  sway(m, { amp: 0.09, freq: 3.1, ref: 0 });
  const geo = new THREE.PlaneGeometry(0.9, 1.35, 4, 10).translate(0, 0.675, 0);
  const mesh = new THREE.Mesh(geo, m); mesh.rotation.z = -Math.PI / 2; mesh.position.set(x + 0.05, y - 0.45, z); mesh.castShadow = true;
  return mesh;
}
