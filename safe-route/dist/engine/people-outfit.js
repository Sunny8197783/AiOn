// Character specs: the named cast and a seeded generator for believable Korean neighbourhood crowds.
// A spec = { H, sex, girth, belly, stoop, skin, hair, top, outer, bottom, shoes, extras, bake, props }.
import { rng } from './kit.js';

export const PRESETS = {
  jeongmin: { H: 1.74, sex: 'm', girth: 1, skin: '#dcb391', hair: { style: 'short', c: '#17130f' },
    top: { kind: 'tee', c: '#d9d7d0', pbr: 'knit' }, outer: { kind: 'jacket', c: '#1e2840', open: true, len: 'hip' },
    bottom: { kind: 'trousers', c: '#5c5f65' }, shoes: { kind: 'sneaker', c: '#25272b', sole: '#e9e6df' } },
  seoyeon: { H: 1.62, sex: 'f', girth: 0.95, skin: '#ecc9aa', hair: { style: 'bob', c: '#2b1d15' },
    top: { kind: 'knit', c: '#ece3d2', pbr: 'knit' }, outer: { kind: 'coat', c: '#a87d52', len: 'knee' },
    bottom: { kind: 'slacks', c: '#2a2a2f' }, shoes: { kind: 'flat', c: '#3b2b22' }, extras: { scarf: '#7a2e35' }, props: ['walker'] },
  manager: { H: 1.68, sex: 'm', girth: 1.07, belly: 0.012, stoop: 0.08, skin: '#cc9f7c', hair: { style: 'short', c: '#9b9893' },
    top: { kind: 'shirt', c: '#a9bfd6', pbr: 'check' }, outer: { kind: 'vest', c: '#1e2a46', len: 'waist' },
    bottom: { kind: 'slacks', c: '#3a3b40' }, shoes: { kind: 'dress', c: '#3b2a1f' }, extras: { lanyard: '#1f4e8c' } },
  clerk: { H: 1.63, sex: 'f', girth: 0.96, skin: '#eecbab', hair: { style: 'tied', c: '#3a281d' },
    top: { kind: 'hoodie', c: '#8b9097', pbr: 'knit' }, outer: { kind: 'vest', c: '#2e8b4f', open: true, len: 'waist' },
    bottom: { kind: 'jeans', c: '#3d5a80', pbr: 'denim' }, shoes: { kind: 'sneaker', c: '#efeee8', sole: '#efeee8' }, extras: { nameTag: true } },
  guide: { H: 1.75, sex: 'm', girth: 1.04, skin: '#d2a582', hair: { style: 'short', c: '#151210' },
    top: { kind: 'windbreaker', c: '#202634', pbr: 'nylon' }, outer: { kind: 'safety', c: '#d8f23a', open: true, len: 'waist' },
    bottom: { kind: 'trousers', c: '#23252a' }, shoes: { kind: 'boot', c: '#1c1c1e' }, extras: { cap: '#1f2433' }, props: ['baton'] },
  coworker: { H: 1.64, sex: 'f', girth: 0.97, skin: '#e8c4a2', hair: { style: 'long', c: '#241912' },
    top: { kind: 'blouse', c: '#f1eee8' }, outer: { kind: 'cardigan', c: '#b9a58a', pbr: 'knit', open: true, len: 'hip' },
    bottom: { kind: 'slacks', c: '#30323a' }, shoes: { kind: 'flat', c: '#1f1f22' }, extras: { lanyard: '#c23b3b', glasses: '#2a2420' } },
  elder: { H: 1.53, sex: 'f', girth: 1.06, belly: 0.008, stoop: 0.22, skin: '#c99c78', hair: { style: 'perm', c: '#bdbab5' },
    top: { kind: 'knit', c: '#7d4655', pbr: 'knit' }, outer: { kind: 'vest', c: '#545a48', pbr: 'quilt', len: 'waist' },
    bottom: { kind: 'trousers', c: '#4c4640' }, shoes: { kind: 'sneaker', c: '#5c5650', sole: '#d9d4cb' } },
};

const SKIN = ['#f0d2b8', '#e9c4a3', '#ddb391', '#d2a481', '#c69672'];
const HAIR_DARK = ['#15110e', '#1f1712', '#2b1e16', '#3e2b1f', '#5a3e2a'];
const HAIR_GREY = ['#77736e', '#9b9893', '#bdbab5', '#d7d4cf'];
const OUTER_C = ['#1c1d21', '#2a2f3a', '#3b3f45', '#56514b', '#8b7d6b', '#a68a64', '#5b6150', '#6e5a4a', '#2e3b4e', '#7c7f84', '#c8bca6'];
const HIKE_C = ['#b8352f', '#cf6a2a', '#2a7f8a', '#5b3f8f', '#2f6db0', '#3d8b45', '#a33f6b'];
const TOP_C = ['#e9e6df', '#d8d3c9', '#2b2b2e', '#7a8088', '#b9c3cf', '#c7b9a3', '#8e3b46', '#3f5a7a', '#5d6b4f', '#f2efe6'];
const BOT_C = ['#1f2024', '#2c2f36', '#4a4c52', '#6b625a', '#9c8f7d', '#3b3a36'];
const JEANS_C = ['#3d5a80', '#2b3b55', '#56708f', '#1e2533'];
const BAG_C = ['#2b3242', '#1c1d21', '#4b5a3c', '#6d2f33', '#3a4f6b', '#8a7a62', '#2f2f2f'];

// Random crowd member. Age group drives height, posture, clothing and hair.
export function crowdSpec(seed) {
  const r = rng((seed * 2654435761) >>> 0), pick = (a) => a[(r() * a.length) | 0], p = (x) => r() < x;
  const u = r(), age = u < 0.3 ? 'young' : u < 0.65 ? 'adult' : u < 0.87 ? 'middle' : 'elder';
  const f = p(0.52), old = age === 'elder';
  const H = (f ? 1.555 + r() * 0.12 : 1.665 + r() * 0.13) - (old ? 0.05 : age === 'middle' ? 0.015 : 0);
  const s = { H, sex: f ? 'f' : 'm', girth: 0.92 + r() * (old || age === 'middle' ? 0.22 : 0.16), skin: pick(SKIN), extras: {}, bake: {} };
  if (!f && (age === 'middle' || old)) s.belly = r() * 0.014;
  s.stoop = old ? 0.12 + r() * 0.14 : age === 'middle' ? r() * 0.05 : 0;
  const greyHair = old || (age === 'middle' && p(0.45));
  const hc = greyHair ? pick(HAIR_GREY) : pick(HAIR_DARK);
  s.hair = { c: hc, style: f ? (old || (age === 'middle' && p(0.55)) ? 'perm' : pick(['bob', 'long', 'tied', 'long'])) : 'short' };
  // clothing
  const topKind = f ? pick(['knit', 'blouse', 'tee', 'knit', 'hoodie']) : pick(['tee', 'shirt', 'knit', 'hoodie', 'shirt']);
  s.top = { kind: topKind, c: pick(TOP_C), pbr: topKind === 'knit' || topKind === 'hoodie' ? 'knit' : topKind === 'shirt' && p(0.3) ? 'check' : 'fabric' };
  const o = r();
  if (age === 'middle' && o < 0.4 || old && o < 0.3) s.outer = { kind: 'jacket', c: pick(HIKE_C), pbr: 'nylon', len: 'hip', open: p(0.3) };
  else if (old && o < 0.75) s.outer = { kind: 'vest', c: pick(OUTER_C), pbr: 'quilt', len: 'waist' };
  else if (o < 0.6) s.outer = { kind: 'jacket', c: pick(OUTER_C), open: p(0.55), len: 'hip', pbr: p(0.3) ? 'nylon' : 'fabric' };
  else if (o < 0.72) s.outer = { kind: 'coat', c: pick(['#c8b08a', '#1c1d21', '#56514b', '#a68a64', '#3b3f45']), len: p(0.5) ? 'knee' : 'thigh' };
  else if (o < 0.8) s.outer = { kind: 'padded', c: pick(OUTER_C), len: 'hip' };
  else if (o < 0.88 && f) s.outer = { kind: 'cardigan', c: pick(['#b9a58a', '#8a8f96', '#d8cfc0', '#6d5a50']), pbr: 'knit', open: true, len: 'hip' };
  if (s.top.kind === 'shirt' && !f && age === 'adult' && s.outer?.kind === 'jacket' && !s.outer.open) s.extras.tie = pick(['#2a3550', '#5a2a30', '#3b3f45']);
  const bk = f && !old && p(0.3) ? 'skirt' : p(0.4) && !old ? 'jeans' : p(0.5) ? 'slacks' : 'trousers';
  s.bottom = { kind: bk, c: bk === 'jeans' ? pick(JEANS_C) : pick(BOT_C), pbr: bk === 'jeans' ? 'denim' : 'fabric' };
  if (bk === 'skirt') { s.bottom.len = 0.27 + r() * 0.08; if (p(0.6)) s.bottom.legs = pick(['#1d1d20', '#3a3532', '#2a2a30']); }
  if (s.outer?.kind === 'jacket' && s.outer.pbr === 'nylon' && (age === 'middle' || old)) s.bottom = { kind: 'trousers', c: pick(['#2c2f36', '#4a4c52', '#3b3a36']), pbr: 'nylon' };
  s.shoes = f && !old && p(0.35) ? { kind: 'flat', c: pick(['#1f1f22', '#3b2b22', '#8a6a52']) }
    : p(0.65) ? { kind: 'sneaker', c: pick(['#f0eee8', '#25272b', '#7c7f84', '#2e3b4e', '#d8d3c9']), sole: p(0.7) ? '#ece9e2' : '#1d1d1f' }
      : p(0.6) ? { kind: 'dress', c: pick(['#1c1c1e', '#3b2a1f']) } : { kind: 'boot', c: pick(['#3b2a1f', '#1c1c1e', '#6b5440']) };
  // accessories
  if (p(old ? 0.25 : 0.1)) s.extras.mask = p(0.75) ? '#f2f2f0' : '#202124';
  if (p(age === 'young' ? 0.18 : 0.3)) s.extras.glasses = pick(['#1b1b1d', '#4a3a2e', '#8a8f96']);
  if (!f && p(old ? 0.3 : 0.08)) s.extras.cap = pick(['#2a2f3a', '#56514b', '#1c1d21', '#b9a58a']);
  if (s.outer?.kind !== 'coat' && !old && p(age === 'young' ? 0.45 : age === 'adult' ? 0.28 : 0.2)) s.bake.backpack = pick(BAG_C);
  else if (p(0.22)) s.bake.crossbody = pick(['#3a2e28', '#1c1d21', '#6b5440', '#8a3a3a']);
  if (p(0.15) && !s.extras.mask) s.extras.scarf = pick(['#7a2e35', '#c8bca6', '#3b3f45', '#5b6150']);
  return s;
}

// Spec for a preset name (unknown names fall back to a crowd member and report it).
export function presetSpec(preset, seed) {
  if (preset === 'crowd') return crowdSpec(seed ?? 1);
  const p = PRESETS[preset];
  if (!p) { console.error(`people: unknown preset "${preset}", using crowd`); return crowdSpec(seed ?? 1); }
  return structuredClone(p);
}
