// Graphics tiers: one table every quality consumer reads (renderer, post, sky, controls, kit, fx, crowd and the
// levels' build-time knobs), the Korean labels, saved-setting migration, first-run detection and the frame-rate
// governor that steps down one tier at a time. Pure (no three.js); the engine applies it.
export const TIERS = ['ultralow', 'low', 'medium', 'high', 'ultra'];
// [label, hint] for the settings menu and the dev viewer.
export const TIER_LABELS = {
  ultralow: ['완전 낮음', '아주 오래된 노트북'], low: ['낮음', '가벼운 노트북 · 내장 그래픽'], medium: ['중간', '대부분의 PC'],
  high: ['높음', '그림자 · 주변광'], ultra: ['완전 높음', '고성능 그래픽카드'],
};
const ALIAS = { 'ultra-low': 'ultralow', ultraLow: 'ultralow', verylow: 'ultralow', 'very-low': 'ultralow', minimum: 'ultralow',
  'ultra-high': 'ultra', ultrahigh: 'ultra', veryhigh: 'ultra', 'very-high': 'ultra', max: 'ultra', normal: 'medium', mid: 'medium' };

// A tier id (aliases accepted), or `fallback` when q is not one.
export const normQ = (q, fallback = 'medium') => { const k = typeof q === 'string' ? (ALIAS[q] ?? q) : null; return TIERS.includes(k) ? k : fallback; };
export const rankQ = (q) => TIERS.indexOf(normQ(q, 'medium'));
export const lowerQ = (q) => TIERS[Math.max(0, rankQ(q) - 1)];

// Every knob, per tier. '높음' (high) is the look the game was tuned at; the tiers below give things up in the order
// they cost the least to the night mood: render scale, GTAO, shadow resolution and casters, post extras, crowd.
//   pr: pixel ratio = min(devicePixelRatio, cap) * scale        sun: sun/moon shadow map size (0 = no sun shadow)
//   shadowEvery / darkEvery: redraw the sun shadow every n frames (darkEvery under a dim key light, keyI < 0.3: the
//     moon, a dark room; 0 = no sun shadow then)
//   casterMin: smallest mesh (bounding diameter, m) that still casts shadows (0 = all)
//   lightShadows / lightShadowMap: point/spot lights of a level that may cast shadows, and their largest map
//   flash: flashlight shadow map (0 = none)      gtao: 0 off, 1 half-res 12 samples, 2 full-res 16 samples
//   bloom: 0 off (only on '완전 낮음': emissive windows and lamps still read, without the glow), 1 quarter res, 2 half res
//   adapt: eye adaptation (64x64 + 1x1 passes, ~free; off: the grade holds heldAdapt below instead)
//   blur, sharpen, chroma, smaa: post extras     clouds: cloud fbm octaves
//   indoorKey: key light scale in indoor presets (below 1 without a sun shadow, which lights rooms through ceilings)
//   aniso, tex, atlas, goodsSeg, shelterTex: texture filtering / sizes and goods lathe segments (built per level)
//   crowdCap, crowdScale, crowdShadowM, crowdAnimM, people: crowd members, how many of a requested crowd, shadow
//     and full-rate animation distances (m), body mesh detail (loft segment factor)
//   fx: particle count factor   shop: share of the mart shoppers   shelterPeople: [seated, walking] shelter residents
//   keyLights: town street key lights   pool: apartment emergency light pool   shelterLights: shelter area lights
//   roomLights: below 1, long rows of shop ceiling lamps are merged in pairs (levels/commerce-goods.js lamps)
//   accentLights: small decorative lights (the corridor's green exit glow)   towerLights: the closures' light-tower
//     spotlights (without them the tower head still glows)   parked: share of parked cars kept
//   physicalPaint: clear-coat car paint and full wheel/bevel detail   prewarm: build other levels while idle
export const PROFILES = {
  ultralow: { pr: { cap: 1, scale: 0.75 }, sun: 0, shadowEvery: 2, darkEvery: 0, casterMin: Infinity, lightShadows: 0, lightShadowMap: 0, flash: 0,
    gtao: 0, bloom: 0, adapt: false, blur: false, sharpen: 0, chroma: false, smaa: false, clouds: 3, indoorKey: 0.65,
    aniso: 1, tex: 256, atlas: 1024, goodsSeg: 5, shelterTex: 512,
    crowdCap: 8, crowdScale: 0.5, crowdShadowM: 0, crowdAnimM: 15, people: 0.5, fx: 0.3, shop: 0.35, shelterPeople: [6, 1],
    keyLights: 1, pool: 2, shelterLights: 4, roomLights: 0.5, accentLights: false, towerLights: false, parked: 0.5, physicalPaint: false, prewarm: false },
  low: { pr: { cap: 1, scale: 1 }, sun: 512, shadowEvery: 2, darkEvery: 0, casterMin: 0.35, lightShadows: 0, lightShadowMap: 0, flash: 0,
    gtao: 0, bloom: 1, adapt: true, blur: false, sharpen: 0, chroma: true, smaa: false, clouds: 4, indoorKey: 1,
    aniso: 2, tex: 256, atlas: 1024, goodsSeg: 7, shelterTex: 512,
    crowdCap: 12, crowdScale: 0.7, crowdShadowM: 12, crowdAnimM: 25, people: 0.6, fx: 0.45, shop: 0.5, shelterPeople: [8, 2],
    keyLights: 2, pool: 3, shelterLights: 4, roomLights: 0.5, accentLights: false, towerLights: false, parked: 0.7, physicalPaint: false, prewarm: true },
  medium: { pr: { cap: 1.25, scale: 1 }, sun: 1024, shadowEvery: 1, darkEvery: 0, casterMin: 0.2, lightShadows: 1, lightShadowMap: 512, flash: 0,
    gtao: 0, bloom: 2, adapt: true, blur: true, sharpen: 0.25, chroma: true, smaa: true, clouds: 4, indoorKey: 1,
    aniso: 4, tex: 512, atlas: 2048, goodsSeg: 8, shelterTex: 1024,
    crowdCap: 20, crowdScale: 1, crowdShadowM: 18, crowdAnimM: 35, people: 0.75, fx: 0.7, shop: 0.75, shelterPeople: [16, 3],
    keyLights: 4, pool: 4, shelterLights: 6, roomLights: 1, accentLights: true, towerLights: true, parked: 1, physicalPaint: false, prewarm: true },
  high: { pr: { cap: 1.5, scale: 1 }, sun: 2048, shadowEvery: 1, darkEvery: 1, casterMin: 0, lightShadows: 2, lightShadowMap: 1024, flash: 512,
    gtao: 1, bloom: 2, adapt: true, blur: true, sharpen: 0.25, chroma: true, smaa: true, clouds: 5, indoorKey: 1,
    aniso: 8, tex: 512, atlas: 2048, goodsSeg: 10, shelterTex: 1024,
    crowdCap: 40, crowdScale: 1, crowdShadowM: 25, crowdAnimM: 45, people: 1, fx: 1, shop: 1, shelterPeople: [26, 4],
    keyLights: 7, pool: 5, shelterLights: 6, roomLights: 1, accentLights: true, towerLights: true, parked: 1, physicalPaint: true, prewarm: true },
  ultra: { pr: { cap: 2, scale: 1 }, sun: 4096, shadowEvery: 1, darkEvery: 1, casterMin: 0, lightShadows: 4, lightShadowMap: 1024, flash: 1024,
    gtao: 2, bloom: 2, adapt: true, blur: true, sharpen: 0.25, chroma: true, smaa: true, clouds: 5, indoorKey: 1,
    aniso: 16, tex: 1024, atlas: 2048, goodsSeg: 12, shelterTex: 1024,
    crowdCap: 40, crowdScale: 1.25, crowdShadowM: 35, crowdAnimM: 60, people: 1, fx: 1, shop: 1, shelterPeople: [26, 4],
    keyLights: 7, pool: 5, shelterLights: 6, roomLights: 1, accentLights: true, towerLights: true, parked: 1, physicalPaint: true, prewarm: true },
};
for (const P of Object.values(PROFILES)) Object.freeze(P);
export const profileOf = (q) => PROFILES[normQ(q, 'high')];
export const pixelRatioFor = (q, dpr = 1) => { const { cap, scale } = profileOf(q).pr; return Math.min(dpr || 1, cap) * scale; };
// The eye adaptation a tier without it holds (grade multiplier): what 높음 settles at in each env preset, or in one
// level where that differs (measured in dev.html at the levels' entry spawns).
export const ADAPT_HOLD = {
  day: 1.16, afternoon: 1.16, dusk: 1.34, night: 1.34, blackout: 1.35, interior: 1, interiorNight: 0.85, interiorDark: 1.34, underground: 1,
  'store:interior': 0.63, 'mart:interior': 0.77, 'apartment:interior': 1.06, 'office:interior': 0.97,
  'store:interiorNight': 0.6, 'mart:interiorNight': 0.63, 'apartment:interiorNight': 0.89, 'office:interiorNight': 0.8,
};
export const heldAdapt = (preset, level) => ADAPT_HOLD[`${level}:${preset}`] ?? ADAPT_HOLD[preset] ?? 1;
// How many of a requested `n` people (or lights, cars) a tier keeps: n * factor, at least `min` when n > 0.
export const share = (n, f, min = 1) => (n > 0 ? Math.max(min, Math.round(n * f)) : 0);

// The tier the whole engine is at right now (crowds read distances and caps from it every frame).
export const live = { tier: 'high', P: PROFILES.high };
export function setLive(q) { live.tier = normQ(q, 'high'); live.P = PROFILES[live.tier]; return live.tier; }

// Saved settings -> { quality: tier | 'auto', auto }. Old 'low' | 'medium' | 'high' keep their meaning; a tier the
// game detected itself (qualityAuto) is kept too (the governor may have lowered it); anything else is detected again.
export function savedQuality(saved = {}) {
  const q = normQ(saved?.quality, null);
  return q ? { quality: q, auto: !!saved.qualityAuto } : { quality: 'auto', auto: true };
}

// First-run guess from the GPU name, CPU cores, memory and screen size. 'ultra' is never guessed.
const SOFTWARE = /swiftshader|llvmpipe|softpipe|software|basic render|microsoft basic/;
const STRONG = /rtx|gtx ?(9[78]0|10[6-9]0|16[56]0)|radeon rx|rx ?[5-7]\d{3}|rx ?vega|arc a\d|apple m\d (pro|max|ultra)|quadro|radeon pro/;
const WEAK = /intel|uhd|iris|hd graphics|mali|adreno|powervr|videocore|radeon\(tm\) graphics|radeon graphics|vega \d\b/;
export function autoTier({ gpu = '', cores = 4, memory = 8, pixels = 2e6, maxTexture = 8192 } = {}) {
  const g = String(gpu).toLowerCase();
  if (SOFTWARE.test(g)) return 'ultralow';
  let t = STRONG.test(g) ? 'high' : WEAK.test(g) ? 'low' : 'medium';
  if (cores <= 2 || memory <= 2 || maxTexture < 4096) t = lowerQ(lowerQ(t));
  else if (cores <= 4 || memory <= 4) t = lowerQ(t);
  if (pixels > 5e6) t = lowerQ(t); // 4K-class screens: the fill rate decides
  return t;
}
// What autoTier needs, from a WebGL renderer and the browser (each probe may be missing).
export function gpuInfo(renderer, env = globalThis) {
  let gpu = '', maxTexture = 8192;
  try {
    const gl = renderer.getContext(), ext = gl.getExtension('WEBGL_debug_renderer_info');
    gpu = String(gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER) || '');
    maxTexture = gl.getParameter(gl.MAX_TEXTURE_SIZE) || maxTexture;
  } catch { /* no info: the defaults stand */ }
  const nav = env.navigator || {}, scr = env.screen || {}, dpr = env.devicePixelRatio || 1;
  return { gpu, cores: nav.hardwareConcurrency || 4, memory: nav.deviceMemory || 8, pixels: (scr.width || 1920) * (scr.height || 1080) * dpr * dpr, maxTexture };
}

// Frame-rate governor. sample(raw seconds of the last frame) returns true when the game has stayed below `low` fps
// for `need` seconds of real time (busy stretches and the settle time after a change do not count). Above `high`
// fps the count resets; between the two it drains slowly (hysteresis), so hovering near the line never steps.
// A frame longer than `gap` seconds (a hidden tab, a debugger) resets the window.
export function createGovernor({ low = 30, high = 38, need = 4, settle = 3, after = 6, gap = 2.5 } = {}) {
  let acc = 0, frames = 0, slow = 0, rt = 0, until = settle, fps = 60;
  return {
    get fps() { return fps; },
    get slow() { return slow; },
    // Do not judge for `s` seconds (after a tier change, a level swap, a shader compile).
    hold(s = 3) { until = Math.max(until, rt + s); slow = 0; },
    sample(raw, { busy = false, floor = false } = {}) {
      if (!(raw >= 0) || raw > gap) { acc = frames = 0; return false; }
      rt += raw; acc += raw; frames++;
      if (acc < 0.5) return false;
      const win = acc; fps = frames / acc; acc = frames = 0;
      if (busy || floor || rt < until) { slow = 0; return false; }
      if (fps < low) slow += win; else if (fps > high) slow = 0; else slow = Math.max(0, slow - win * 0.5);
      if (slow < need) return false;
      slow = 0; until = rt + after;
      return true;
    },
  };
}
