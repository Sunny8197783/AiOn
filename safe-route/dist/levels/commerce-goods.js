// Shared pieces for the commerce interiors (mart, store, office): a procedural label atlas, instanced shelf
// stock grouped by section (so a section can be swapped for an empty, '품절'-tagged shelf), gondola shelving,
// ceiling light panels, painted window views and NPC creation with a visible fallback.
// Products are InstancedMeshes that share one atlas; a per-instance aCell attribute picks the label.
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { FONT, rng } from '../engine/kit.js';

const GRID = 8;
// name: [style, label bg, text, accent/cap, title, sub, liquid]. Brands are fictional.
const LABELS = {
  water2a: ['pet', '#1f6fb2', '#fff', '#1f6fb2', '맑은샘', '먹는샘물 2L'], water2b: ['pet', '#2f9e7a', '#fff', '#2a7d5f', '한빛수', '먹는샘물 2L'],
  water5: ['pet', '#1f6fb2', '#fff', '#1f6fb2', '맑은샘', '먹는샘물 500mL'], water5b: ['pet', '#eef5fb', '#1f4f8c', '#7fb2d8', '산골물', '먹는샘물 500mL'],
  cola: ['pet', '#c8202f', '#fff', '#c8202f', '톡톡콜라', '500mL', '#3a1a0e'], cider: ['pet', '#2f9a3c', '#fff', '#2f9a3c', '맑은사이다', '500mL', '#d6f0d2'],
  ion: ['pet', '#2a6cc2', '#fff', '#e8eef5', '이온워터', '620mL', '#d8ecf8'], tea: ['pet', '#8a5a2b', '#fff3df', '#6b3f1a', '보리차', '무카페인 500mL', '#b07a3a'],
  coffeeCan: ['can', '#3b2416', '#f2dcc0', '#c99a62', '진한커피', '175mL'], juiceCan: ['can', '#f28a1a', '#fff', '#ffd36a', '오렌지', '과즙 100%'],
  soda: ['can', '#1f8fd0', '#fff', '#bfe8ff', '탄산수', '355mL'], vita: ['can', '#6b3a1f', '#ffd200', '#f2c230', '비타음료', '100mL'],
  ramenR: ['bag', '#c4161c', '#fff', '#ffcf33', '얼큰라면', '5개입'], ramenB: ['bag', '#1d1d1f', '#ffd200', '#e8481c', '짜장라면', '4개입'],
  ramenW: ['bag', '#f2efe6', '#c4161c', '#e94e1b', '사골라면', '5개입'], cupR: ['cup', '#d8261c', '#fff', '#f2f2f2', '얼큰 컵라면', '큰컵'],
  cupU: ['cup', '#f4e9cf', '#2f6b3a', '#f2f2f2', '튀김우동', '컵'], rice: ['box', '#fbfbf8', '#2b2b2b', '#d64534', '즉석밥', '210g × 3'],
  tuna: ['tin', '#f2c12e', '#123a7a', '#123a7a', '살코기 참치', '150g'], ham: ['tin', '#1f4fa8', '#ffd200', '#e2e6ee', '통햄', '200g'],
  bar: ['box', '#ffb81c', '#4a2a00', '#e8481c', '에너지바', '5개입'], cracker: ['bag', '#b9853f', '#fff', '#7a4f1c', '건빵', '별사탕 포함'],
  chips: ['bag', '#f2c230', '#b3171f', '#e94e1b', '감자칩', '오리지널'], chips2: ['bag', '#2a7d3c', '#fff', '#9ed36a', '양파링', '대용량'],
  cookie: ['box', '#6b3a1f', '#fff', '#f2c230', '초코쿠키', '12개입'], choco: ['box', '#5a2a14', '#ffd200', '#c4161c', '초코과자', '12개입'],
  tissue: ['box', '#f6f2f4', '#c0436e', '#f5b8cc', '도톰 화장지', '30롤'], wipes: ['bag', '#e8f5ec', '#1f7a4a', '#6cc08f', '순한 물티슈', '100매'],
  bags: ['box', '#2f5d9c', '#fff', '#9cc4f2', '비닐봉투', '50매'], tape: ['box', '#1f5fb4', '#fff', '#123a7a', '청테이프', '48mm'],
  rain: ['bag', '#f4f6f8', '#2b6cb0', '#ffd200', '일회용 우의', '성인용'], foil: ['bag', '#c9ccd2', '#333', '#e5b84a', '은박 담요', '비상용'],
  jug: ['box', '#5fa8d3', '#fff', '#1f4f8c', '접이식 물통', '10L'], stove: ['box', '#2b2b2b', '#fff', '#e8481c', '휴대용 가스레인지', '캠핑·가정용'],
  butane: ['tin', '#f28a1a', '#fff', '#ffffff', '부탄가스', '220g'], whistle: ['blister', '#ff7a00', '#fff', '#ff7a00', '호루라기', ''],
  radio: ['box', '#3b3f46', '#fff', '#f2c230', '휴대용 라디오', 'FM · AM'], aa: ['blister', '#1d1d1f', '#f2c230', '#d4a017', '알카라인 AA', '4개입'],
  aaa: ['blister', '#7a3b12', '#fff', '#d98b3a', '알카라인 AAA', '4개입'], flash: ['box', '#1d1d1f', '#fff', '#5fd0ff', 'LED 손전등', '건전지 별매'],
  power: ['box', '#f4f6f8', '#1d1d1f', '#2b6cb0', '보조배터리', '10000mAh'], candle: ['box', '#f6efe2', '#7a3b12', '#e8a33a', '양초', '6개입'],
  aid: ['box', '#f6f6f6', '#1f7a4a', '#1f7a4a', '구급함', '가정용'], mask: ['box', '#ffffff', '#1f4f8c', '#4a90d9', '보건용 마스크', '10매'],
  sanit: ['pet', '#ffffff', '#1f6fb2', '#1f6fb2', '손소독제', '500mL', '#e8f4ff'], band: ['box', '#f2d4b8', '#7a3b12', '#e8481c', '밴드', '30매'],
  soap: ['pet', '#7cc242', '#fff', '#2f8a3c', '주방세제', '1L', '#c8f08a'], deter: ['box', '#2b8fd8', '#fff', '#ffd200', '세탁세제', '2.5kg'],
  towel: ['box', '#ffffff', '#5a8f3c', '#a8d08d', '키친타올', '4롤'], milk: ['carton', '#2b6cb0', '#fff', '#ffffff', '우유', '900mL'],
  yogurt: ['cup', '#f7c7d8', '#9b2a5a', '#ffffff', '딸기 요구르트', ''], tofu: ['box', '#ffffff', '#1f7a4a', '#ffe08a', '두부', '300g'],
  mix: ['box', '#f2c230', '#6b3a1f', '#c4161c', '커피믹스', '100개입'], soup: ['bag', '#f6efe2', '#7a3b12', '#c4161c', '즉석 미역국', '1인분'],
  cereal: ['box', '#e8481c', '#fff', '#f2c230', '콘시리얼', '가족용'], oil: ['pet', '#f2e6b0', '#6b4a10', '#d4a017', '참기름', '350mL', '#d9a426'],
  soy: ['pet', '#c4161c', '#fff', '#1d1d1f', '진간장', '900mL', '#2a120a'], kimchi: ['bag', '#f6efe2', '#c4161c', '#2f8a3c', '포기김치', '500g'],
  gim: ['bag', '#1f3d24', '#fff', '#2f8a3c', '도시락김', '8봉'], soymilk: ['carton', '#c9a66b', '#fff', '#f6efe2', '두유', '190mL'],
  gum: ['blister', '#2fae66', '#fff', '#9ee8b8', '자일리톨 껌', ''],
  pack6: ['pack', '#1f6fb2', '#fff', '#1f6fb2', '맑은샘 2L × 6', '먹는샘물'], pack6b: ['pack', '#2f9e7a', '#fff', '#2a7d5f', '한빛수 2L × 6', '먹는샘물'],
};
const CELL = Object.fromEntries(Object.keys(LABELS).map((k, i) => [k, i]));

// name: [shape, w, h, d, labels, stack]. For fixed-size shapes (bottles, cans, cups) w/h/d are the real sizes.
export const PRODUCTS = {
  water2: ['bottle2', 0.11, 0.32, 0.11, ['water2a', 'water2b']], water5: ['bottle5', 0.068, 0.21, 0.068, ['water5', 'water5b']],
  drink: ['bottle5', 0.068, 0.21, 0.068, ['cola', 'cider', 'ion', 'tea']], can: ['can', 0.068, 0.123, 0.068, ['coffeeCan', 'juiceCan', 'soda', 'vita'], 2],
  ramen: ['pouch', 0.3, 0.1, 0.22, ['ramenR', 'ramenB', 'ramenW'], 3], cup: ['cup', 0.11, 0.1, 0.11, ['cupR', 'cupU'], 3],
  rice: ['box', 0.15, 0.12, 0.2, ['rice'], 2], tuna: ['tin', 0.09, 0.046, 0.09, ['tuna'], 4], ham: ['mbox', 0.1, 0.055, 0.06, ['ham'], 4],
  bar: ['box', 0.15, 0.2, 0.05, ['bar']], cracker: ['pouch', 0.16, 0.22, 0.06, ['cracker']], chips: ['pouch', 0.2, 0.28, 0.08, ['chips', 'chips2']],
  cookie: ['box', 0.2, 0.08, 0.13, ['cookie', 'choco'], 3], tissue: ['box', 0.45, 0.32, 0.24, ['tissue'], 2], wipes: ['pouch', 0.2, 0.07, 0.12, ['wipes'], 4],
  bags: ['box', 0.16, 0.1, 0.08, ['bags'], 3], tape: ['box', 0.1, 0.1, 0.05, ['tape'], 2], rain: ['pouch', 0.16, 0.22, 0.03, ['rain']],
  foil: ['pouch', 0.12, 0.17, 0.02, ['foil']], jug: ['box', 0.25, 0.25, 0.08, ['jug']], stove: ['box', 0.36, 0.12, 0.3, ['stove'], 3],
  butane: ['butane', 0.068, 0.19, 0.068, ['butane']], whistle: ['box', 0.08, 0.15, 0.02, ['whistle']], radio: ['box', 0.2, 0.14, 0.08, ['radio'], 2],
  aa: ['box', 0.1, 0.16, 0.02, ['aa', 'aaa']], flash: ['box', 0.08, 0.22, 0.05, ['flash']], power: ['box', 0.1, 0.16, 0.04, ['power']],
  candle: ['box', 0.12, 0.2, 0.06, ['candle']], aid: ['box', 0.26, 0.18, 0.1, ['aid'], 2], mask: ['box', 0.2, 0.12, 0.1, ['mask'], 3],
  sanit: ['bottle5', 0.068, 0.21, 0.068, ['sanit', 'soap']], band: ['box', 0.1, 0.08, 0.03, ['band'], 3], deter: ['box', 0.25, 0.3, 0.12, ['deter']],
  towel: ['box', 0.24, 0.25, 0.12, ['towel']], milk: ['box', 0.075, 0.2, 0.075, ['milk']], yogurt: ['cupS', 0.066, 0.06, 0.066, ['yogurt'], 2],
  tofu: ['box', 0.12, 0.05, 0.1, ['tofu'], 2], mix: ['box', 0.3, 0.1, 0.1, ['mix'], 2], soup: ['pouch', 0.14, 0.19, 0.02, ['soup']],
  cereal: ['box', 0.2, 0.28, 0.07, ['cereal']], oil: ['bottle5', 0.068, 0.21, 0.068, ['oil', 'soy']], kimchi: ['pouch', 0.16, 0.2, 0.06, ['kimchi']],
  pack6: ['box', 0.34, 0.33, 0.23, ['pack6', 'pack6b']],
  gim: ['pouch', 0.22, 0.05, 0.15, ['gim'], 3], soymilk: ['box', 0.06, 0.11, 0.04, ['soymilk'], 2], gum: ['box', 0.06, 0.1, 0.02, ['gum']],
};

function fitText(g, text, size, weight, maxW) {
  let s = size; g.font = `${weight} ${s}px ${FONT}`;
  while (g.measureText(text).width > maxW && s > 7) { s -= 1; g.font = `${weight} ${s}px ${FONT}`; }
}
function drawLabel(g, c, [style, bg, fg, ac, title, sub, liquid = '#cfe8f2']) {
  const txt = (t, x, y, size, color, weight = 800, maxW = c * 0.82, stroke) => {
    if (!t) return; fitText(g, t, size, weight, maxW); g.textAlign = 'center'; g.textBaseline = 'middle';
    if (stroke) { g.lineWidth = size * 0.16; g.strokeStyle = stroke; g.strokeText(t, x, y); }
    g.fillStyle = color; g.fillText(t, x, y);
  };
  const vgrad = (a, b, y0 = 0, y1 = c) => { const gr = g.createLinearGradient(0, y0, 0, y1); gr.addColorStop(0, a); gr.addColorStop(1, b); return gr; };
  const hsheen = (alpha) => { const gr = g.createLinearGradient(0, 0, c, 0); gr.addColorStop(0, `rgba(0,0,0,${alpha})`); gr.addColorStop(0.42, `rgba(255,255,255,${alpha})`); gr.addColorStop(0.58, 'rgba(255,255,255,0)'); gr.addColorStop(1, `rgba(0,0,0,${alpha})`); g.fillStyle = gr; g.fillRect(0, 0, c, c); };
  if (style === 'pet') { // clear PET bottle: cap, neck, liquid, label band
    g.fillStyle = liquid; g.fillRect(0, 0, c, c);
    g.fillStyle = vgrad('rgba(255,255,255,0.35)', 'rgba(0,0,0,0.12)'); g.fillRect(0, 0, c, c);
    g.fillStyle = 'rgba(255,255,255,0.45)'; g.fillRect(c * 0.44, 0, c * 0.05, c); g.fillRect(c * 0.6, 0, c * 0.02, c);
    g.fillStyle = ac; g.fillRect(0, 0, c, c * 0.075);
    g.fillStyle = bg; g.fillRect(0, c * 0.4, c, c * 0.3);
    g.fillStyle = 'rgba(255,255,255,0.8)'; g.fillRect(0, c * 0.4, c, c * 0.02); g.fillRect(0, c * 0.68, c, c * 0.02);
    txt(title, c / 2, c * 0.51, c * 0.12, fg, 800, c * 0.46); txt(sub, c / 2, c * 0.62, c * 0.055, fg, 600, c * 0.46);
  } else if (style === 'can' || style === 'tin') {
    g.fillStyle = bg; g.fillRect(0, 0, c, c); hsheen(0.22);
    if (style === 'can') { g.fillStyle = '#c9ccd1'; g.fillRect(0, 0, c, c * 0.06); g.fillRect(0, c * 0.95, c, c * 0.05); }
    g.fillStyle = ac; g.fillRect(0, c * 0.66, c, c * 0.08);
    txt(title, c / 2, c * 0.42, c * 0.13, fg, 800, c * 0.48); txt(sub, c / 2, c * 0.82, c * 0.07, fg, 600, c * 0.48);
  } else if (style === 'bag') { // pillow pack: burst, product photo, bold title
    g.fillStyle = vgrad(bg, shade(bg, 0.72)); g.fillRect(0, 0, c, c);
    g.fillStyle = ac; g.beginPath();
    for (let i = 0; i < 24; i++) { const a = i / 24 * Math.PI * 2, r = i % 2 ? c * 0.1 : c * 0.16; g.lineTo(c * 0.78 + Math.cos(a) * r, c * 0.2 + Math.sin(a) * r); }
    g.fill();
    g.fillStyle = 'rgba(255,255,255,0.85)'; g.beginPath(); g.ellipse(c * 0.36, c * 0.7, c * 0.24, c * 0.12, 0, 0, 7); g.fill();
    g.fillStyle = shade(ac, 0.8); g.beginPath(); g.ellipse(c * 0.36, c * 0.68, c * 0.19, c * 0.07, 0, 0, 7); g.fill();
    txt(title, c / 2, c * 0.42, c * 0.17, fg, 900, c * 0.86, 'rgba(0,0,0,0.35)'); txt(sub, c * 0.78, c * 0.86, c * 0.07, fg, 700, c * 0.36);
    g.fillStyle = 'rgba(255,255,255,0.18)'; g.fillRect(0, c * 0.06, c, c * 0.04);
  } else if (style === 'blister') { // hanging card with a clear window
    g.fillStyle = bg; g.fillRect(0, 0, c, c);
    g.fillStyle = '#ffffff'; g.beginPath(); g.ellipse(c / 2, c * 0.07, c * 0.1, c * 0.03, 0, 0, 7); g.fill();
    g.fillStyle = 'rgba(230,236,240,0.9)'; g.fillRect(c * 0.18, c * 0.16, c * 0.64, c * 0.5);
    g.fillStyle = ac; for (let i = 0; i < 4; i++) g.fillRect(c * (0.23 + i * 0.145), c * 0.2, c * 0.1, c * 0.42);
    g.fillStyle = 'rgba(255,255,255,0.4)'; for (let i = 0; i < 4; i++) g.fillRect(c * (0.245 + i * 0.145), c * 0.2, c * 0.025, c * 0.42);
    txt(title, c / 2, c * 0.77, c * 0.1, fg, 800); txt(sub, c / 2, c * 0.9, c * 0.07, fg, 600);
  } else if (style === 'pack') { // shrink-wrapped six-pack: bottles seen through film, brand band, carry handle
    g.fillStyle = vgrad('#e4f1f8', '#a9cfe0'); g.fillRect(0, 0, c, c);
    for (let i = 0; i < 3; i++) {
      const bx = c * (0.08 + i * 0.3);
      g.fillStyle = 'rgba(120,170,200,0.55)'; g.fillRect(bx, c * 0.16, c * 0.24, c * 0.84);
      g.fillStyle = ac; g.fillRect(bx + c * 0.08, c * 0.04, c * 0.08, c * 0.08);
      g.fillStyle = 'rgba(255,255,255,0.5)'; g.fillRect(bx + c * 0.04, c * 0.2, c * 0.03, c * 0.75);
    }
    g.fillStyle = bg; g.fillRect(0, c * 0.46, c, c * 0.26);
    txt(title, c / 2, c * 0.55, c * 0.1, fg, 800, c * 0.9); txt(sub, c / 2, c * 0.66, c * 0.06, fg, 600);
    g.fillStyle = 'rgba(255,255,255,0.6)'; g.fillRect(c * 0.3, 0, c * 0.4, c * 0.03);
  } else if (style === 'cup') {
    g.fillStyle = bg; g.fillRect(0, 0, c, c); hsheen(0.12);
    g.fillStyle = ac; g.fillRect(0, 0, c, c * 0.12);
    g.fillStyle = 'rgba(0,0,0,0.15)'; g.fillRect(0, c * 0.12, c, c * 0.015);
    txt(title, c / 2, c * 0.5, c * 0.12, fg, 900, c * 0.5); txt(sub, c / 2, c * 0.7, c * 0.07, fg, 700, c * 0.5);
  } else { // box / carton: printed carton with a top band and a 'photo'
    g.fillStyle = bg; g.fillRect(0, 0, c, c);
    g.fillStyle = ac; g.fillRect(0, 0, c, c * (style === 'carton' ? 0.3 : 0.16));
    g.fillStyle = shade(ac, 1.1); g.beginPath(); g.arc(c * 0.5, c * 0.72, c * 0.13, 0, 7); g.fill();
    if (title === '구급함') { g.fillStyle = ac; g.fillRect(c * 0.44, c * 0.6, c * 0.12, c * 0.24); g.fillRect(c * 0.38, c * 0.66, c * 0.24, c * 0.12); }
    txt(title, c / 2, c * 0.4, c * 0.14, fg, 900); txt(sub, c / 2, c * 0.92, c * 0.07, fg, 700);
    g.strokeStyle = 'rgba(0,0,0,0.18)'; g.lineWidth = c * 0.01; g.strokeRect(0, 0, c, c);
  }
}
function shade(hex, k) {
  const n = parseInt(hex.replace('#', '').padEnd(6, hex.slice(-1)), 16);
  const ch = (v) => Math.max(0, Math.min(255, Math.round(v * k)));
  return `rgb(${ch(n >> 16)},${ch((n >> 8) & 255)},${ch(n & 255)})`;
}

// Bottom-centred unit geometries; lathe UV v follows real height so caps and label bands land right.
function lathe(profile, seg) {
  const H = profile[profile.length - 1][1];
  const g = new THREE.LatheGeometry(profile.map(([r, y]) => new THREE.Vector2(r, y)), seg);
  const p = g.attributes.position, uv = g.attributes.uv;
  for (let i = 0; i < uv.count; i++) uv.setY(i, p.getY(i) / H);
  return g.rotateY(Math.PI);
}
const cylG = (rt, rb, h, seg) => new THREE.CylinderGeometry(rt, rb, h, seg).translate(0, h / 2, 0).rotateY(Math.PI);
function makeGeoms(hi) {
  const s = hi ? 10 : 7;
  return {
    bottle2: lathe([[0, 0], [0.047, 0.003], [0.054, 0.03], [0.053, 0.08], [0.054, 0.15], [0.054, 0.2], [0.05, 0.235], [0.03, 0.27], [0.016, 0.288], [0.017, 0.3], [0.017, 0.32], [0, 0.32]], s),
    bottle5: lathe([[0, 0], [0.03, 0.003], [0.033, 0.02], [0.032, 0.07], [0.033, 0.13], [0.028, 0.16], [0.014, 0.185], [0.014, 0.21], [0, 0.21]], s),
    can: cylG(0.033, 0.033, 0.123, s), tin: cylG(0.045, 0.045, 0.046, s + 2), butane: cylG(0.033, 0.033, 0.19, s),
    cup: cylG(0.055, 0.044, 0.1, s + 2), cupS: cylG(0.033, 0.027, 0.06, s),
    box: new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0), mbox: new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0),
    pouch: new RoundedBoxGeometry(1, 1, 1, 2, 0.14).translate(0, 0.5, 0),
  };
}
const FIXED = new Set(['bottle2', 'bottle5', 'can', 'tin', 'butane', 'cup', 'cupS']);

// Atlas + materials, built once per kit.
function shared(kit) {
  return kit.memo('commerce:goods', () => {
    const hi = kit.quality !== 'low', S = hi ? 2048 : 1024, c = S / GRID, pad = 3 / S;
    const atlas = kit.texture(kit.canvas(S, S, (g) => {
      Object.values(LABELS).forEach((L, i) => {
        g.save(); g.translate((i % GRID) * c, Math.floor(i / GRID) * c);
        g.beginPath(); g.rect(0, 0, c, c); g.clip(); drawLabel(g, c, L); g.restore();
      });
    }));
    const scale = (1 / GRID - 2 * pad).toFixed(6);
    const mk = (o) => {
      const m = new THREE.MeshStandardMaterial({ map: atlas, ...o });
      m.onBeforeCompile = (sh) => {
        sh.vertexShader = sh.vertexShader.replace('#include <common>', '#include <common>\nattribute vec2 aCell;')
          .replace('#include <uv_vertex>', `#include <uv_vertex>\nvMapUv = vMapUv * ${scale} + aCell;`);
      };
      m.customProgramCacheKey = () => 'commerce-atlas';
      return m;
    };
    const paper = mk({ roughness: 0.62 }), film = mk({ roughness: 0.32 }), pet = mk({ roughness: 0.08, envMapIntensity: 1.6 }), tin = mk({ roughness: 0.3, metalness: 0.6 });
    const mats = { bottle2: pet, bottle5: pet, can: tin, tin, butane: tin, mbox: tin, cup: paper, cupS: paper, box: paper, pouch: film };
    const cellUV = (i) => [(i % GRID) / GRID + pad, 1 - (Math.floor(i / GRID) + 1) / GRID + pad];
    // Shelf-edge price strip (1 m per repeat) and the red '품절' tag.
    const strip = kit.texture(kit.canvas(512, 32, (g) => {
      const r = rng(5); g.fillStyle = '#f4f4f0'; g.fillRect(0, 0, 512, 32);
      for (let x = 6; x < 500; x += 64 + ((r() * 30) | 0)) {
        const promo = r() < 0.2; g.fillStyle = promo ? '#ffd23a' : '#ffffff'; g.fillRect(x, 3, 52, 26);
        g.strokeStyle = '#c9c9c4'; g.strokeRect(x, 3, 52, 26);
        g.fillStyle = promo ? '#c4161c' : '#1d1d1f'; g.font = `800 15px ${FONT}`; g.textAlign = 'center';
        g.fillText(`${(1 + ((r() * 18) | 0)) * 500 - 10}`.replace(/\B(?=(\d{3})+$)/g, ','), x + 26, 23);
      }
    }));
    const tagMat = new THREE.MeshStandardMaterial({ map: kit.signTexture(['품절'], { w: 256, h: 128, bg: '#d7262d', fg: '#ffffff', border: '#ffffff' }), roughness: 0.5, emissive: 0x330000 });
    return { geoms: makeGeoms(hi), mats, cellUV, stripMat: new THREE.MeshStandardMaterial({ map: strip, roughness: 0.4 }), tagMat, deep: hi ? 2 : 1 };
  });
}

// Per-level goods collector. stock() fills shelf runs; build() returns { group, sections: { id: { full, out } } }.
export function createGoods(kit) {
  const S = shared(kit), buckets = new Map(), tags = new Map(), strips = [];
  const push = (key, item) => { if (!buckets.has(key)) buckets.set(key, []); buckets.get(key).push(item); };
  // run: { sec, x, z (front edge at run start), dir: [ax, az], face: [ox, oz], len, depth, y, clear, items, seed, fill, tags }
  // rows/stack override the automatic depth rows (bottles: 1) and stack height.
  function stock({ sec = 'misc', x, z, dir, face, len, depth = 0.42, y, clear = 0.32, items, seed = 1, fill = 0.92, edge = true, rows: rowsSet, stack: stackSet }) {
    const r = rng(seed * 131 + 7), [ax, az] = dir, [ox, oz] = face, ry = Math.atan2(ox, oz), block = len / items.length;
    items.forEach((id, bi) => {
      const P = PRODUCTS[id]; if (!P) return void console.error('[commerce] unknown product', id);
      const [shape, w, h, d, labels, stackMax = 1] = P;
      const fw = w + 0.012, n = Math.max(1, Math.floor(block / fw)), padA = (block - n * fw) / 2;
      const rows = Math.max(1, Math.min(rowsSet ?? (shape.startsWith('bottle') ? 1 : S.deep), Math.floor(depth / (d + 0.01))));
      const stack = Math.max(1, Math.min(stackSet ?? stackMax, Math.floor(clear / (h + 0.004))));
      const cell = CELL[labels[(bi + seed) % labels.length]];
      let kept = 0;
      for (let j = 0; j < n; j++) {
        if (r() > 0.985) continue; // a sold-through facing
        const a = bi * block + padA + fw * (j + 0.5);
        for (let k = 0; k < rows; k++) {
          if (k === 0 && r() > fill) continue;
          const back = 0.025 + d * (k + 0.5) + 0.01 * k + (r() - 0.5) * 0.012;
          for (let m = 0; m < stack; m++) {
            const t = { x: x + ax * a - ox * back, y: y + m * (h + 0.003), z: z + az * a - oz * back, ry: ry + (r() - 0.5) * 0.08, cell, tint: 0.9 + r() * 0.12 };
            if (!FIXED.has(shape)) { t.sx = w; t.sy = h; t.sz = d; }
            push(`${sec}|${shape}`, t);
          }
        }
        // a few leftovers stay behind on a sold-out shelf
        if (kept < 2 && r() < 1.4 / n) { kept++; push(`${sec}:out|${shape}`, { x: x + ax * a - ox * (0.06 + r() * 0.12), y, z: z + az * a - oz * (0.06 + r() * 0.12), ry: ry + (r() - 0.5) * 1.2, cell, tint: 0.85, ...(FIXED.has(shape) ? {} : { sx: w, sy: h, sz: d }) }); }
      }
    });
    if (edge) {
      const g = new THREE.PlaneGeometry(len, 0.04); const uv = g.attributes.uv;
      for (let i = 0; i < uv.count; i++) uv.setX(i, uv.getX(i) * len);
      g.rotateY(ry).translate(x + ax * len / 2 + ox * 0.012, y - 0.022, z + az * len / 2 + oz * 0.012); strips.push(g);
      if (!tags.has(sec)) tags.set(sec, []);
      for (let a = 0.35; a < len; a += 0.9) tags.get(sec).push({ x: x + ax * a + ox * 0.016, y: y - 0.02, z: z + az * a + oz * 0.016, ry });
    }
  }
  function build() {
    const group = new THREE.Group(); group.name = 'goods';
    const sections = {};
    const sect = (sec) => (sections[sec] ||= { full: add(new THREE.Group()), out: add(Object.assign(new THREE.Group(), { visible: false })) });
    const add = (o) => { group.add(o); return o; };
    const color = new THREE.Color();
    for (const [key, list] of buckets) {
      const [secKey, shape] = key.split('|'), [sec, out] = secKey.split(':');
      const geom = S.geoms[shape].clone(), cells = new Float32Array(list.length * 2);
      list.forEach((t, i) => cells.set(S.cellUV(t.cell), i * 2));
      geom.setAttribute('aCell', new THREE.InstancedBufferAttribute(cells, 2));
      const im = kit.instanced(geom, S.mats[shape], list, { cast: false, receive: true });
      list.forEach((t, i) => im.setColorAt(i, color.setScalar(t.tint)));
      im.instanceColor.needsUpdate = true;
      sect(sec)[out ? 'out' : 'full'].add(im);
    }
    for (const [sec, list] of tags) sect(sec).out.add(kit.instanced(new THREE.PlaneGeometry(0.11, 0.055), S.tagMat, list, { cast: false, receive: false }));
    if (strips.length) { const m = new THREE.Mesh(mergeGeometries(strips), S.stripMat); m.receiveShadow = true; group.add(m); }
    return { group, sections };
  }
  return { stock, build };
}

// Wire shopping carts (nested in a row) and plastic baskets (stacked): a few instanced parts each.
// carts/baskets: [{ x, y, z, ry }]. Returns a Group.
export function cartsAndBaskets(kit, carts = [], baskets = []) {
  const P = kit.memo('commerce:carts', () => {
    const grid = (color, line, cell) => kit.texture(kit.canvas(64, 64, (g) => { g.strokeStyle = color; g.lineWidth = line; for (let i = 0; i <= 64; i += cell) { g.beginPath(); g.moveTo(i, 0); g.lineTo(i, 64); g.stroke(); g.beginPath(); g.moveTo(0, i); g.lineTo(64, i); g.stroke(); } }));
    const wire = new THREE.MeshStandardMaterial({ map: grid('#d8dde2', 3, 8), alphaTest: 0.5, metalness: 0.8, roughness: 0.35, side: THREE.DoubleSide });
    const red = new THREE.MeshStandardMaterial({ map: grid('#c8202f', 5, 11), alphaTest: 0.5, roughness: 0.5, side: THREE.DoubleSide, color: 0xffffff });
    const T = (geo, x, y, z, rx = 0, ry = 0, rz = 0) => geo.rotateX(rx).rotateY(ry).rotateZ(rz).translate(x, y, z);
    const box = (w, h, d, x, y, z) => T(new THREE.BoxGeometry(w, h, d), x, y + h / 2, z);
    const pl = (w, h, x, y, z, rx, ry) => T(new THREE.PlaneGeometry(w, h), x, y, z, rx, ry);
    const cart = {
      wire: mergeGeometries([pl(0.52, 0.86, 0, 0.5, 0, -Math.PI / 2), pl(0.86, 0.42, -0.27, 0.72, 0, 0, Math.PI / 2), pl(0.86, 0.42, 0.27, 0.72, 0, 0, Math.PI / 2),
        pl(0.56, 0.42, 0, 0.72, -0.43), pl(0.5, 0.36, 0, 0.7, 0.43), pl(0.5, 0.3, 0, 0.22, 0.05, -Math.PI / 2)]),
      metal: mergeGeometries([box(0.03, 0.03, 0.9, -0.22, 0.12, 0), box(0.03, 0.03, 0.9, 0.22, 0.12, 0), box(0.03, 0.85, 0.03, -0.27, 0.12, 0.45), box(0.03, 0.85, 0.03, 0.27, 0.12, 0.45),
        box(0.025, 0.4, 0.025, -0.22, 0.12, -0.42), box(0.025, 0.4, 0.025, 0.22, 0.12, -0.42)]),
      red: mergeGeometries([box(0.58, 0.05, 0.05, 0, 0.98, 0.5), box(0.5, 0.12, 0.02, 0, 0.82, -0.44)]),
      black: mergeGeometries([[-0.22, -0.4], [0.22, -0.4], [-0.22, 0.4], [0.22, 0.4]].map(([x, z]) => T(new THREE.CylinderGeometry(0.06, 0.06, 0.04, 10), x, 0.06, z, 0, 0, Math.PI / 2))),
    };
    const basket = { red: mergeGeometries([pl(0.44, 0.3, 0, 0.005, 0, -Math.PI / 2), pl(0.44, 0.24, 0, 0.12, 0.15), pl(0.44, 0.24, 0, 0.12, -0.15), pl(0.3, 0.24, 0.22, 0.12, 0, 0, Math.PI / 2), pl(0.3, 0.24, -0.22, 0.12, 0, 0, Math.PI / 2)]) };
    return { cart, basket, mats: { wire, red, metal: kit.mat('metal'), black: kit.mat('rubber') }, redSolid: kit.mat('paint:#c8202f') };
  });
  const g = new THREE.Group(); g.name = 'carts';
  if (carts.length) for (const k of ['wire', 'metal', 'red', 'black']) g.add(kit.instanced(P.cart[k], k === 'red' ? P.redSolid : P.mats[k], carts));
  if (baskets.length) g.add(kit.instanced(P.basket.red, P.mats.red, baskets));
  return g;
}

// Gondola shelving. axis 'z' runs north-south centred on x; 'x' runs east-west centred on z.
// sides: 'both' | 'neg' | 'pos' (face toward -x/-z or +x/+z). Returns { group, runs: [{ side, y, clear, x, z, dir, face, len }] }.
export function gondola(kit, { x, z, len, axis = 'z', sides = 'both', levels = [0.12, 0.5, 0.86, 1.22, 1.58], height = 1.86, depth = 0.45, color = '#e9e9e5' }) {
  const g = new THREE.Group(), steel = kit.mat('paint:' + color, { metalness: 0.4, roughness: 0.45 }), dark = kit.mat('darkMetal');
  const along = axis === 'z', wide = sides === 'both' ? depth * 2 + 0.05 : depth + 0.03;
  const off = sides === 'both' ? 0 : (sides === 'pos' ? 1 : -1) * (depth / 2);
  const P = (a, b) => (along ? { x: x + b, z: z + a } : { x: x + a, z: z + b });
  const B = (lA, h, lB, o) => kit.box(along ? lB : lA, h, along ? lA : lB, o.m || steel, { ...P(o.a || 0, o.b || 0), y: o.y || 0 });
  g.add(B(len, height, 0.04, { b: sides === 'both' ? 0 : off - (sides === 'pos' ? 1 : -1) * depth / 2, m: kit.mat('paint:#d9dad6', { roughness: 0.7 }) }));
  for (const e of [-1, 1]) g.add(B(0.03, height, wide, { a: e * (len / 2 + 0.015), b: off }));
  g.add(B(len, 0.1, wide - 0.06, { b: off, m: dark }));
  const runs = [];
  for (const sgn of sides === 'both' ? [-1, 1] : [sides === 'pos' ? 1 : -1]) {
    const bOff = sides === 'both' ? sgn * (depth / 2 + 0.025) : off;
    levels.forEach((ly, i) => {
      g.add(B(len, 0.025, depth, { b: bOff, y: ly - 0.025 }));
      const clear = (levels[i + 1] ?? height + 0.25) - ly - 0.04;
      const front = sides === 'both' ? sgn * (depth + 0.025) : off + sgn * depth / 2;
      const s = P(-len / 2, front);
      runs.push({ side: sgn, y: ly, clear, x: s.x, z: s.z, dir: along ? [0, 1] : [1, 0], face: along ? [sgn, 0] : [0, sgn], len, depth });
    });
  }
  return { group: g, runs };
}

// Emissive ceiling fixtures (instanced) registered in a light group, plus real point lights.
export function panels(kit, list, { w = 1.2, d = 0.3, color = '#f3f6ff', on = 2.4, group = 'indoor' } = {}) {
  const m = new THREE.MeshStandardMaterial({ color: 0xf6f6f6, emissive: new THREE.Color(color), roughness: 0.4 });
  kit.lit(group, m, on);
  return kit.instanced(new THREE.BoxGeometry(w, 0.035, d), m, list, { cast: false, receive: false });
}
export function lamps(kit, list, { color = 0xf2f4ff, intensity = 14, distance = 16, group = 'indoor' } = {}) {
  const g = new THREE.Group();
  for (const p of list) { const l = new THREE.PointLight(color, intensity, distance, 2); l.position.set(p.x, p.y, p.z); kit.lit(group, l, intensity); g.add(l); }
  return g;
}

// Painted view outside a window. kind 'street' (shops, road) or 'sky' (horizon only). set(mood) swaps the picture.
export const MOODS = ['day', 'dusk', 'night', 'blackout'];
export function windowView(kit, { w, h, kind = 'street', seed = 3 }) {
  const paint = (mood) => kit.memo(`commerce:view:${kind}:${mood}:${seed}`, () => kit.texture(kit.canvas(1024, 512, (g, W, H) => {
    const r = rng(seed), dark = mood === 'night' || mood === 'blackout';
    const sky = { day: ['#8fb8e0', '#dfe9f0'], dusk: ['#3b3a6b', '#f29a5c'], night: ['#0c1424', '#2a3046'], blackout: ['#05070d', '#0d1220'] }[mood];
    const gr = g.createLinearGradient(0, 0, 0, H * 0.75); gr.addColorStop(0, sky[0]); gr.addColorStop(1, sky[1]); g.fillStyle = gr; g.fillRect(0, 0, W, H);
    if (mood === 'blackout') { g.fillStyle = '#fff'; for (let i = 0; i < 160; i++) g.fillRect(r() * W, r() * H * 0.45, 1.5, 1.5); }
    const lit = { day: 0, dusk: 0.3, night: 0.55, blackout: 0.02 }[mood], base = kind === 'street' ? H * 0.72 : H * 0.8;
    for (let x = -20; x < W;) {
      const bw = 60 + r() * 140, bh = (kind === 'street' ? 140 : 40) + r() * (kind === 'street' ? 200 : 110), tone = 150 + r() * 70;
      const k = mood === 'day' ? 1 : mood === 'dusk' ? 0.45 : 0.16;
      g.fillStyle = `rgb(${tone * k | 0},${(tone - 8) * k | 0},${(tone - 16) * k | 0})`; g.fillRect(x, base - bh, bw, bh);
      for (let wy = base - bh + 12; wy < base - 30; wy += kind === 'street' ? 26 : 12) for (let wx = x + 8; wx < x + bw - 12; wx += kind === 'street' ? 22 : 10) {
        const on = r() < lit; g.fillStyle = on ? (r() < 0.75 ? '#ffd08a' : '#dff0ff') : mood === 'day' ? '#5d7184' : '#141a24';
        g.fillRect(wx, wy, kind === 'street' ? 12 : 5, kind === 'street' ? 14 : 6);
      }
      if (kind === 'street') { // ground-floor shop sign
        const sg = ['약국', '세탁', '분식', '부동산', '미용실', '정육점', '철물점'][(r() * 7) | 0];
        g.fillStyle = mood === 'blackout' ? '#1a1d22' : ['#1f6fb2', '#c4161c', '#2f8a57', '#e8a33a'][(r() * 4) | 0]; g.fillRect(x + 6, base - 48, bw - 12, 22);
        g.fillStyle = mood === 'blackout' ? '#3a3d42' : '#fff'; g.font = `700 16px ${FONT}`; g.textAlign = 'center'; g.fillText(sg, x + bw / 2, base - 31);
      } else if (r() < 0.3) { g.fillStyle = mood === 'blackout' ? '#300' : '#ff3b30'; g.fillRect(x + bw / 2, base - bh - 3, 3, 3); }
      x += bw + r() * 12;
    }
    if (kind === 'street') {
      g.fillStyle = mood === 'day' ? '#8f8a84' : dark ? '#1b1c1f' : '#4a4644'; g.fillRect(0, base, W, H * 0.08);
      g.fillStyle = mood === 'day' ? '#3a3c3f' : '#101113'; g.fillRect(0, base + H * 0.08, W, H);
      g.fillStyle = mood === 'blackout' ? '#2a2a2a' : '#e8e8e0'; for (let x = 0; x < W; x += 90) g.fillRect(x, H * 0.92, 50, 4);
      for (let x = 80; x < W; x += 300) {
        g.fillStyle = '#2a2d31'; g.fillRect(x, base - 170, 5, 170 + H * 0.08);
        if (mood === 'dusk' || mood === 'night') { const lg = g.createRadialGradient(x + 2, base - 170, 2, x + 2, base - 170, 60); lg.addColorStop(0, 'rgba(255,230,180,1)'); lg.addColorStop(1, 'rgba(255,200,120,0)'); g.fillStyle = lg; g.fillRect(x - 60, base - 230, 124, 124); }
      }
    }
  })));
  const m = new THREE.MeshBasicMaterial({ map: paint('day'), fog: false });
  const mesh = new THREE.Mesh(new THREE.PlaneGeometry(w, h), m);
  let cur = 'day';
  return { mesh, get mood() { return cur; }, set(mood) { if (mood === cur || !MOODS.includes(mood)) return; cur = mood; m.map = paint(mood); m.color.setScalar(mood === 'day' ? 0.95 : 1); m.needsUpdate = true; } };
}
// Map an engine env preset to an outside mood (interiors default to `fallback`).
export const moodFor = (env, fallback) => ({ day: 'day', afternoon: 'day', dusk: 'dusk', night: 'night', blackout: 'blackout', interiorDark: 'blackout' }[env] || fallback);

// A Person from people.createPerson, or an invisible stand-in that logs once, so levels never crash.
let warned = false;
export function npc(people, preset, seed, at = {}) {
  let p = null;
  try { p = people?.createPerson?.({ preset, seed }) ?? null; } catch (e) { console.error('[commerce] createPerson failed', preset, e); }
  if (!p) {
    if (!warned) { warned = true; console.error('[commerce] people.createPerson unavailable; NPCs are invisible stand-ins'); }
    p = { group: new THREE.Group(), anim: 'idle', height: 1.7, setAnim(a) { this.anim = a; }, setSpeed() {}, setProp() {}, lookAt() {}, update() {} };
  }
  p.group.position.set(at.x || 0, at.y || 0, at.z || 0); p.group.rotation.y = at.ry || 0;
  if (at.anim) p.setAnim(at.anim);
  for (const pr of at.props || []) p.setProp(pr, true);
  return p;
}

// Axis-aligned solid from an object's world bounds (call before kit.merge removes the meshes).
const _b = new THREE.Box3();
export function solidOf(obj, pad = 0, id) {
  _b.setFromObject(obj);
  const s = { x0: _b.min.x - pad, x1: _b.max.x + pad, y0: _b.min.y, y1: _b.max.y, z0: _b.min.z - pad, z1: _b.max.z + pad };
  if (id) s.id = id;
  return s;
}
// Facing a direction: rotation.y for a model that faces -z.
export const yawTo = (dx, dz) => Math.atan2(-dx, -dz);
