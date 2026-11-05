// Shop and civic window interiors for town-fronts.js: one atlas of painted rooms (2 x 4 slots). Shelves are stocked
// from the commerce-goods label palette in runs of one product (2-5 facings, as a real shelf is faced up), with
// price rails, shelf shadows and sold-out gaps; the store's drinks fridge has glass doors. On the tiers with fine
// mesh detail (detailOf 2: 높음, 완전 높음) the glass plane becomes a box room 2.4 m deep (interior mapping in the
// material's shader: no extra geometry or draw calls); below that the room stays a flat picture.
import { FONT, rng } from '../engine/kit.js';
import { LABELS } from './commerce-goods.js';

export const INTERIORS = ['store', 'pharmacy', 'snack', 'mart', 'cafe', 'lobby', 'office', 'civic'];
export const ROOM = { cols: 2, rows: 4, depth: 2.4 };
// The window each shelf slot is painted for (metres), so products keep their real size whatever the slot aspect.
const SIZE = { store: [11.2, 2.75], pharmacy: [5.6, 2.7], mart: [8.5, 2.7] };
// Sections (one per gondola run), from the commerce-goods labels; the pharmacy adds its own OTC boxes.
export const SECTIONS = {
  store: [['water5', 'water5b', 'ion', 'tea', 'cola', 'cider'], ['ramenR', 'ramenB', 'ramenW', 'cupR', 'cupU'], ['chips', 'chips2', 'cookie', 'choco', 'bar', 'cracker'], ['tissue', 'wipes', 'mask', 'band', 'aa', 'gum']],
  mart: [['pack6', 'pack6b', 'water2a', 'water2b'], ['ramenR', 'ramenB', 'ramenW', 'rice', 'tuna', 'ham'], ['soy', 'oil', 'kimchi', 'gim', 'mix', 'cereal'], ['tissue', 'deter', 'soap', 'towel', 'wipes']],
  pharmacy: [['med1', 'med2', 'med3', 'band', 'mask'], ['med4', 'med5', 'aid', 'sanit'], ['med2', 'med6', 'med1', 'mask', 'band']],
};
const MEDS = { // OTC cartons and a brown tonic bottle (fictional)
  med1: ['box', '#c8463a', '#fff', '#f4efe6'], med2: ['box', '#2f6fb2', '#fff', '#e9eef4'], med3: ['box', '#f3f1ea', '#2f8a3c', '#2f8a3c'],
  med4: ['pet', '#e8c23a', '#1f3d7a', '#c9a227', '', '', '#5a2a10'], med5: ['box', '#e39a36', '#fff', '#fbf3e3'], med6: ['box', '#5f9447', '#fff', '#eef2e6'],
};
export const LAB = (k) => LABELS[k] || MEDS[k];
// Facing size in metres by package style: [width, height]; a run of one product is 2-5 facings wide.
export const FACING = { pet: [0.075, 0.22], pet2: [0.11, 0.31], can: [0.066, 0.12], bag: [0.21, 0.23], box: [0.15, 0.2], cup: [0.11, 0.1], tin: [0.09, 0.05], carton: [0.075, 0.24], pack: [0.33, 0.31], blister: [0.1, 0.15] };

function rrect(g, x, y, w, h, r) { r = Math.min(r, w / 2, h / 2); g.beginPath(); g.moveTo(x + r, y); g.arcTo(x + w, y, x + w, y + h, r); g.arcTo(x + w, y + h, x, y + h, r); g.arcTo(x, y + h, x, y, r); g.arcTo(x, y, x + w, y, r); g.closePath(); g.fill(); }
// One package, standing on y (metres, canvas y down), x..x+w.
function facing(g, L, x, y, w, h) {
  const [style, bg, , accent, , , liquid] = L, top = y - h;
  if (style === 'pet') {
    g.fillStyle = liquid || '#bcd3df'; rrect(g, x + w * 0.06, top + h * 0.2, w * 0.88, h * 0.8, w * 0.25); g.fillRect(x + w * 0.3, top + h * 0.06, w * 0.4, h * 0.2);
    g.fillStyle = accent; g.fillRect(x + w * 0.32, top, w * 0.36, h * 0.07); // cap
    g.fillStyle = bg; g.fillRect(x + w * 0.06, top + h * 0.42, w * 0.88, h * 0.26); // label
    g.fillStyle = 'rgba(255,255,255,0.18)'; g.fillRect(x + w * 0.18, top + h * 0.24, w * 0.1, h * 0.7);
  } else if (style === 'can' || style === 'tin') {
    g.fillStyle = bg; g.fillRect(x, top, w, h);
    g.fillStyle = '#a9adb0'; g.fillRect(x, top, w, h * 0.08); g.fillRect(x, y - h * 0.06, w, h * 0.06);
    g.fillStyle = accent; g.fillRect(x, top + h * 0.55, w, h * 0.12);
  } else if (style === 'bag') {
    g.fillStyle = bg; rrect(g, x, top + h * 0.04, w, h * 0.96, w * 0.12);
    g.fillStyle = accent; g.beginPath(); g.ellipse(x + w * 0.5, top + h * 0.58, w * 0.3, h * 0.2, 0, 0, 7); g.fill();
    g.fillStyle = 'rgba(0,0,0,0.18)'; g.fillRect(x + w * 0.04, top + h * 0.04, w * 0.92, h * 0.06); // crimped seal
  } else if (style === 'cup') {
    g.fillStyle = bg; g.beginPath(); g.moveTo(x, top); g.lineTo(x + w, top); g.lineTo(x + w * 0.88, y); g.lineTo(x + w * 0.12, y); g.closePath(); g.fill();
    g.fillStyle = accent; g.fillRect(x - w * 0.02, top - h * 0.06, w * 1.04, h * 0.08);
  } else if (style === 'carton') {
    g.fillStyle = bg; g.fillRect(x, top + h * 0.16, w, h * 0.84);
    g.fillStyle = accent; g.beginPath(); g.moveTo(x, top + h * 0.16); g.lineTo(x + w * 0.5, top); g.lineTo(x + w, top + h * 0.16); g.closePath(); g.fill();
  } else { // box, pack, blister
    g.fillStyle = bg; g.fillRect(x, top, w, h);
    g.fillStyle = accent; g.fillRect(x, top + h * (style === 'pack' ? 0.3 : 0.62), w, h * 0.18);
    g.fillStyle = 'rgba(255,255,255,0.14)'; g.fillRect(x, top, w * 0.12, h);
  }
}
// A gondola run x0..x1 between y0 and y1 (metres): rows, stock, rails, shadows. r: the slot's random stream.
function gondola(g, r, x0, x1, y0, y1, rows, section) {
  const rh = (y1 - y0) / rows, rail = 0.035;
  g.fillStyle = '#b6bbb9'; g.fillRect(x0, y0, x1 - x0, y1 - y0);
  g.fillStyle = '#8f9492'; g.fillRect(x0 - 0.04, y0 - 0.06, 0.04, y1 - y0 + 0.06); g.fillRect(x1, y0 - 0.06, 0.04, y1 - y0 + 0.06);
  for (let row = 0; row < rows; row++) {
    const base = y0 + (row + 1) * rh - rail;
    let k = (r() * section.length) | 0, x = x0 + 0.02;
    while (x < x1 - 0.06) {
      const key = section[k++ % section.length], L = LAB(key), st = L[0] === 'pet' && /2L/.test(L[5] || '') ? 'pet2' : L[0];
      const [fw, fh0] = FACING[st] || FACING.box, big = fw > 0.2, n = big ? 1 + ((r() * 2) | 0) : 2 + ((r() * 4) | 0);
      if (r() < 0.08) { x += fw * (1 + ((r() * 2) | 0)) + 0.01; continue; } // sold out: the bare shelf shows
      const fh = Math.min(fh0, rh - rail - 0.03), stack = st === 'tin' ? 3 : st === 'cup' || st === 'can' ? 2 : 1;
      for (let i = 0; i < n && x + fw < x1 - 0.01; i++, x += fw + 0.006) {
        const hj = fh * (1 + (r() - 0.5) * 0.04);
        for (let s = 0; s < stack && (s + 1) * hj < rh - rail - 0.02; s++) facing(g, L, x + (r() - 0.5) * 0.004, base - s * hj, fw, hj);
      }
      x += 0.02;
    }
    const sh = g.createLinearGradient(0, base - rh + rail, 0, base); // the shelf above shades the tops, the bay stays dim
    sh.addColorStop(0, 'rgba(20,24,26,0.4)'); sh.addColorStop(1, 'rgba(20,24,26,0.1)'); g.fillStyle = sh; g.fillRect(x0, base - rh + rail, x1 - x0, rh - rail);
    g.fillStyle = '#e6e8e4'; g.fillRect(x0, base, x1 - x0, rail); // price rail
    g.fillStyle = '#5e6361'; g.fillRect(x0, base + rail, x1 - x0, 0.01);
    for (let t = x0 + 0.05 + r() * 0.1; t < x1 - 0.08; t += 0.22 + r() * 0.16) { g.fillStyle = r() < 0.18 ? '#f2c94a' : '#fbfbf8'; g.fillRect(t, base + 0.005, 0.06, rail - 0.01); g.fillStyle = '#2b2b2b'; g.fillRect(t + 0.012, base + 0.016, 0.036, 0.006); }
  }
}
// The store's drinks fridge: three glass doors, wire shelves of bottles and cans, a cool back panel.
function fridge(g, r, x0, x1, y0, y1) {
  g.fillStyle = '#9aa4aa'; g.fillRect(x0, y0, x1 - x0, y1 - y0);
  const dw = (x1 - x0 - 0.08) / 3, drinks = SECTIONS.store[0].concat(['coffeeCan', 'juiceCan', 'soda', 'vita', 'milk']);
  for (let d = 0; d < 3; d++) {
    const a = x0 + 0.04 + d * dw, b = a + dw - 0.04;
    g.fillStyle = '#d9e6ee'; g.fillRect(a, y0 + 0.08, b - a, y1 - y0 - 0.12);
    for (let s = 0; s < 5; s++) {
      const base = y0 + 0.08 + (s + 1) * (y1 - y0 - 0.12) / 5 - 0.012;
      let x = a + 0.02, k = (r() * drinks.length) | 0;
      while (x < b - 0.08) { const L = LAB(drinks[k++ % drinks.length]), st = L[0] === 'pet' || L[0] === 'carton' ? L[0] : 'can', [fw, fh] = FACING[st]; for (let i = 0; i < 3 && x + fw < b - 0.01; i++, x += fw + 0.008) if (r() > 0.06) facing(g, L, x, base, fw, fh); x += 0.015; }
      g.fillStyle = '#7f898e'; g.fillRect(a, base, b - a, 0.012);
    }
    g.fillStyle = '#6f797e'; g.fillRect(b - 0.035, y0 + 0.35, 0.02, 0.5); // door handle
  }
  g.fillStyle = '#0f8a6c'; g.fillRect(x0, y0, x1 - x0, 0.08);
}

// Paints slot i (w x h px). deep: the slot is seen through the room shader, which lays its own depth over it.
export function paintInterior(g, i, w, h, deep) {
  const kind = INTERIORS[i], r = (s => () => ((s = (s * 16807) % 2147483647) / 2147483647))(i * 97 + 13);
  const warm = kind === 'cafe' || kind === 'snack' || kind === 'lobby';
  g.fillStyle = warm ? '#5a4030' : kind === 'civic' || kind === 'office' ? '#c9ccc8' : '#e8ece9'; g.fillRect(0, 0, w, h);
  g.fillStyle = warm ? '#3a2a20' : '#b8bcb8'; g.fillRect(0, h * 0.78, w, h * 0.22);
  for (let x = 20; x < w; x += 70) { g.fillStyle = warm ? '#ffd9a0' : '#ffffff'; if (kind === 'cafe') { g.beginPath(); g.arc(x + 20, h * 0.2, 9, 0, 7); g.fill(); g.fillRect(x + 19, 0, 2, h * 0.2); } else g.fillRect(x, h * 0.04, 50, 6); }
  if (SIZE[kind]) {
    const [Wm, Hm] = SIZE[kind], q = rng(i * 131 + 7), sec = SECTIONS[kind];
    g.save(); g.scale(w / Wm, h / Hm); // metres from the top-left
    const end = kind === 'store' ? Wm * 0.7 : kind === 'pharmacy' ? Wm * 0.82 : Wm - 0.3, units = Math.max(1, Math.round((end - 0.3) / 1.85)), uw = (end - 0.3) / units;
    for (let u = 0; u < units; u++) gondola(g, q, 0.3 + u * uw + 0.06, 0.3 + (u + 1) * uw - 0.06, Hm * 0.22, Hm * 0.78, 5, sec[u % sec.length]);
    if (kind === 'store') { fridge(g, q, Wm * 0.72, Wm * 0.97, Hm * 0.16, Hm * 0.78); g.fillStyle = '#0f8a6c'; g.fillRect(0, Hm * 0.12, Wm * 0.71, 0.08); }
    if (kind === 'pharmacy') { g.fillStyle = '#1f7a3a'; g.fillRect(Wm * 0.86, Hm * 0.3, 0.42, 0.13); g.fillRect(Wm * 0.86 + 0.145, Hm * 0.3 - 0.145, 0.13, 0.42); }
    g.restore();
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
  if (deep) return; // the glass in front carries the reflection
  const glare = g.createLinearGradient(0, 0, w, h); glare.addColorStop(0, 'rgba(255,255,255,0.18)'); glare.addColorStop(0.5, 'rgba(255,255,255,0)'); g.fillStyle = glare; g.fillRect(0, 0, w, h);
}

// Interior mapping on a MeshStandardMaterial whose map (and emissiveMap) is the room atlas. The window's size in
// metres and its axes come from screen-space derivatives of the view position and the UVs (the planes are flat and
// their UVs unstretched, so no extra attributes survive kit.merge); a ray from the eye is cut against a box room of
// ROOM.depth behind the glass: back wall = the painted slot, side walls / floor / ceiling = the slot's edge pixels
// stretched into depth and shaded darker. Nothing is brighter than the flat picture (shades <= 1).
export function roomShader(material) {
  material.onBeforeCompile = (sh) => {
    sh.uniforms.uRoomDepth = { value: ROOM.depth };
    sh.fragmentShader = sh.fragmentShader
      .replace('#include <common>', `#include <common>\nuniform float uRoomDepth;\n#define ROOM_GRID vec2(${ROOM.cols.toFixed(1)}, ${ROOM.rows.toFixed(1)})`)
      .replace('#include <map_fragment>', `vec4 roomCol = vec4(1.0);
#ifdef USE_MAP
{
  vec3 rp = -vViewPosition, q0 = dFdx(rp), q1 = dFdy(rp);
  vec2 s0 = dFdx(vMapUv), s1 = dFdy(vMapUv);
  vec3 rn = normalize(cross(q0, q1)), q1p = cross(q1, rn), q0p = cross(rn, q0);
  float A = dot(q0, q1p); A = A < 0.0 ? min(A, -1e-10) : max(A, 1e-10);
  vec3 gu = (q1p * s0.x + q0p * s1.x) / A, gv = (q1p * s0.y + q0p * s1.y) / A; // d(uv)/d(view position)
  float lu = max(length(gu), 1e-6), lv = max(length(gv), 1e-6);
  vec2 room = 1.0 / (ROOM_GRID * vec2(lu, lv)); // the window, metres
  vec2 cell = floor(vMapUv * ROOM_GRID), loc = vMapUv * ROOM_GRID - cell;
  vec3 rd = normalize(rp), d = vec3(dot(rd, gu / lu), dot(rd, gv / lv), dot(rd, rn));
  vec3 ad = max(abs(d), vec3(1e-4)), s = vec3(loc * room, 0.0);
  float tx = (d.x > 0.0 ? room.x - s.x : s.x) / ad.x, ty = (d.y > 0.0 ? room.y - s.y : s.y) / ad.y, tz = uRoomDepth / ad.z;
  float t = min(min(tx, ty), tz);
  vec3 hp = s + d * t;
  float f = clamp(-hp.z / uRoomDepth, 0.0, 1.0), shade = 1.0;
  vec2 st = hp.xy / room;
  if (t < tz) {
    if (tx < ty) { st = vec2(d.x > 0.0 ? 0.94 : 0.06, st.y); shade = 0.74 - 0.2 * f; } // side wall: the shelf rows run on along it
    else if (d.y < 0.0) { st = vec2(st.x, 0.01); shade = 0.84 - 0.22 * f; } // floor
    else { st = vec2(st.x, 0.99); shade = 0.8 - 0.25 * f; } // ceiling
  }
  roomCol = textureGrad(map, (cell + clamp(st, 0.01, 0.99)) / ROOM_GRID, s0, s1);
  roomCol.rgb *= shade;
  diffuseColor *= roomCol;
}
#endif`)
      .replace('#include <emissivemap_fragment>', '#ifdef USE_EMISSIVEMAP\n\ttotalEmissiveRadiance *= roomCol.rgb;\n#endif');
  };
  material.customProgramCacheKey = () => 'town-room';
  return material;
}
