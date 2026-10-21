// Environment presets: sun/moon, sky, fog, exposure, bloom, clouds, grade and light groups per time of day.
// Angles are degrees: azimuth from north (-z) clockwise toward east (+x), elevation above the horizon.
// lit: light group -> scale (0 = off). Groups not listed are left as they are.
// bloom: [strength, radius, threshold] (threshold in linear HDR before tone mapping; lower at night so lamps glow).

export const MOON = [140, 34];

const NIGHT_OFF = { amount: 0, zenith: '#0a1430', horizon: '#2a3550', glow: 0, glowColor: '#ff9a55', stars: 0, moon: 0 };

export const PRESETS = {
  day: { // clear autumn 13:00
    outdoor: true, env: 'sky', shadowBox: 70,
    sun: [195, 50], key: [195, 50], keyColor: '#fff2df', keyI: 3.3,
    hemi: ['#cddcf5', '#7a6c58', 0.6], envI: 0.85,
    fog: ['#c4d2df', 0.0022], exposure: 0.95, bloom: [0.22, 0.35, 0.95],
    sky: { turbidity: 2.2, rayleigh: 1.0, mie: 0.004, mieG: 0.8, gain: 1.0 },
    night: NIGHT_OFF,
    clouds: { cover: 0.32, opacity: 0.95, lit: '#ffffff', shade: '#a3adbd', gain: 0.95 },
    hills: '#8c9fae',
    grade: { saturation: 1.06, contrast: 1.05, exposure: 0, vignette: 0.18, grain: 0.025, tint: [1, 0.98, 0.95], tintAmount: 0.1 },
    lit: { street: 0, windows: 0, shops: 0.5, signs: 0.4, indoor: 1, emergency: 1 },
  },
  afternoon: { // 17:30, low warm sun from the west-southwest, long shadows
    outdoor: true, env: 'sky', shadowBox: 70,
    sun: [248, 8], key: [248, 8], keyColor: '#ffb36a', keyI: 3.0,
    hemi: ['#b9c6dd', '#8a5a3a', 0.45], envI: 0.7,
    fog: ['#e3bf9f', 0.0028], exposure: 1.0, bloom: [0.35, 0.5, 0.9],
    sky: { turbidity: 4.5, rayleigh: 2.0, mie: 0.006, mieG: 0.86, gain: 1.0 },
    night: NIGHT_OFF,
    clouds: { cover: 0.38, opacity: 0.95, lit: '#ffd2a1', shade: '#8a7f8f', gain: 1.0 },
    hills: '#a99088',
    grade: { saturation: 1.1, contrast: 1.08, exposure: 0, vignette: 0.24, grain: 0.03, tint: [1, 0.92, 0.82], tintAmount: 0.25 },
    lit: { street: 0, windows: 0.25, shops: 0.8, signs: 0.6, indoor: 1, emergency: 1 },
  },
  dusk: { // 18:40, sun just below the horizon: orange band, purple zenith, street lights on
    outdoor: true, env: 'sky', shadowBox: 70,
    sun: [258, -3], key: [255, 6], keyColor: '#ff8c5a', keyI: 0.7,
    hemi: ['#6a6fa8', '#3a2c2e', 0.55], envI: 0.6,
    fog: ['#7d6a86', 0.0026], exposure: 1.15, bloom: [0.6, 0.55, 0.8],
    sky: { turbidity: 6, rayleigh: 3.2, mie: 0.009, mieG: 0.9, gain: 1.1 },
    night: { amount: 0.35, zenith: '#12183a', horizon: '#3a2a4a', glow: 0.2, glowColor: '#ff9a55', stars: 0.05, moon: 0.6 },
    clouds: { cover: 0.45, opacity: 0.95, lit: '#ff9a62', shade: '#4a3a5c', gain: 0.6 },
    hills: '#3b3048',
    grade: { saturation: 1.08, contrast: 1.06, exposure: 0.1, vignette: 0.3, grain: 0.035, tint: [1, 0.88, 0.95], tintAmount: 0.2 },
    lit: { street: 1, windows: 0.7, shops: 1, signs: 1, indoor: 1, emergency: 1 },
  },
  night: { // city lit: sodium/LED glow on the horizon, few stars
    outdoor: true, env: 'sky', shadowBox: 70,
    sun: [270, -25], key: MOON, keyColor: '#9fb4e0', keyI: 0.25,
    hemi: ['#1c2640', '#1a1612', 0.35], envI: 0.35,
    fog: ['#232634', 0.0028], exposure: 1.25, bloom: [0.9, 0.6, 0.7],
    sky: { turbidity: 2, rayleigh: 1, mie: 0.005, mieG: 0.8, gain: 1.0 },
    night: { amount: 1, zenith: '#04070f', horizon: '#1a1c28', glow: 0.55, glowColor: '#ff9a55', stars: 0.15, moon: 1 },
    clouds: { cover: 0.35, opacity: 0.9, lit: '#6b5a52', shade: '#1a1c26', gain: 0.12 },
    hills: '#0b0d14',
    grade: { saturation: 0.95, contrast: 1.1, exposure: 0.15, vignette: 0.32, grain: 0.045, tint: [0.92, 0.95, 1.08], tintAmount: 0.15 },
    lit: { street: 1, windows: 1, shops: 1, signs: 1, indoor: 1, emergency: 1 },
  },
  blackout: { // grid down: no city glow, moonlight and a full star field, cold grade
    outdoor: true, env: 'sky', shadowBox: 70,
    sun: [270, -25], key: MOON, keyColor: '#a9c2ff', keyI: 0.45,
    hemi: ['#141c30', '#07080a', 0.22], envI: 0.25,
    fog: ['#0b0f18', 0.0032], exposure: 1.35, bloom: [1.1, 0.65, 0.7],
    sky: { turbidity: 2, rayleigh: 1, mie: 0.005, mieG: 0.8, gain: 1.0 },
    night: { amount: 1, zenith: '#02040a', horizon: '#0b1020', glow: 0, glowColor: '#ff9a55', stars: 1, moon: 1 },
    clouds: { cover: 0.28, opacity: 0.9, lit: '#3a4258', shade: '#07090f', gain: 0.05 },
    hills: '#05070b',
    grade: { saturation: 0.65, contrast: 1.12, exposure: 0.1, vignette: 0.42, grain: 0.06, tint: [0.82, 0.92, 1.12], tintAmount: 0.35 },
    lit: { street: 0, windows: 0, shops: 0, signs: 0.8, indoor: 0, emergency: 1 },
  },
  interior: { // warm room light, daylight through windows
    outdoor: false, env: 'room', shadowBox: 24,
    sun: [210, 35], key: [210, 35], keyColor: '#fff0dc', keyI: 1.6,
    hemi: ['#fff1e0', '#6b5848', 0.35], envI: 0.4,
    fog: ['#d8d0c4', 0.006], exposure: 1.0, bloom: [0.35, 0.45, 0.9],
    sky: { turbidity: 3, rayleigh: 1.2, mie: 0.005, mieG: 0.8, gain: 1.0 },
    night: NIGHT_OFF,
    clouds: { cover: 0.32, opacity: 0.95, lit: '#ffffff', shade: '#a3adbd', gain: 0.95 },
    hills: '#8c9fae',
    grade: { saturation: 1.04, contrast: 1.04, exposure: 0, vignette: 0.25, grain: 0.03, tint: [1, 0.95, 0.88], tintAmount: 0.2 },
    lit: { indoor: 1, shops: 1, signs: 1, emergency: 1, street: 0, windows: 0 },
  },
  interiorNight: { // extra (not in spec 4.2): indoor lights on at night, lit city outside the windows
    outdoor: false, env: 'room', shadowBox: 24,
    sun: [270, -25], key: MOON, keyColor: '#9fb4e0', keyI: 0.15,
    hemi: ['#3a342e', '#16130f', 0.22], envI: 0.38,
    fog: ['#141821', 0.006], exposure: 1.1, bloom: [0.5, 0.5, 0.8],
    sky: { turbidity: 2, rayleigh: 1, mie: 0.005, mieG: 0.8, gain: 1.0 },
    night: { amount: 1, zenith: '#04070f', horizon: '#1a1c28', glow: 0.55, glowColor: '#ff9a55', stars: 0.15, moon: 1 },
    clouds: { cover: 0.35, opacity: 0.9, lit: '#6b5a52', shade: '#1a1c26', gain: 0.12 },
    hills: '#0b0d14',
    grade: { saturation: 1.0, contrast: 1.06, exposure: 0.05, vignette: 0.3, grain: 0.04, tint: [1, 0.93, 0.84], tintAmount: 0.18 },
    lit: { indoor: 1, windows: 1, street: 1, shops: 1, signs: 1, emergency: 1 },
  },
  interiorDark: { // indoor lights off, cold moonlight through the windows
    outdoor: false, env: 'room', shadowBox: 24,
    sun: [270, -25], key: MOON, keyColor: '#9fb6ea', keyI: 0.5,
    hemi: ['#1a2238', '#0a0a0c', 0.12], envI: 0.06,
    fog: ['#0a0d14', 0.01], exposure: 1.4, bloom: [0.9, 0.6, 0.75],
    sky: { turbidity: 2, rayleigh: 1, mie: 0.005, mieG: 0.8, gain: 1.0 },
    night: { amount: 1, zenith: '#02040a', horizon: '#0b1020', glow: 0.1, glowColor: '#ff9a55', stars: 0.6, moon: 1 },
    clouds: { cover: 0.28, opacity: 0.9, lit: '#3a4258', shade: '#07090f', gain: 0.05 },
    hills: '#05070b',
    grade: { saturation: 0.7, contrast: 1.1, exposure: 0.15, vignette: 0.45, grain: 0.06, tint: [0.85, 0.92, 1.1], tintAmount: 0.3 },
    lit: { indoor: 0, windows: 0, street: 0, shops: 0, signs: 0.8, emergency: 1 },
  },
  underground: { // cool fluorescent car park, slight haze, no sky
    outdoor: false, env: 'room', shadowBox: 30, noSky: true,
    sun: [270, -25], key: [200, 60], keyColor: '#ffffff', keyI: 0,
    hemi: ['#cfe3ea', '#3a3f40', 0.3], envI: 0.3,
    fog: ['#2a3236', 0.02], exposure: 1.1, bloom: [0.55, 0.5, 0.85],
    sky: { turbidity: 2, rayleigh: 1, mie: 0.005, mieG: 0.8, gain: 1.0 },
    night: NIGHT_OFF,
    clouds: { cover: 0, opacity: 0, lit: '#000000', shade: '#000000', gain: 0 },
    hills: '#1a1f22',
    grade: { saturation: 0.85, contrast: 1.06, exposure: 0, vignette: 0.35, grain: 0.05, tint: [0.9, 1.02, 1.04], tintAmount: 0.25 },
    lit: { indoor: 1, emergency: 1 },
  },
};

const lerpArr = (a, b, k) => a.map((v, i) => v + (b[i] - v) * k);
const grey = (c) => { const l = c[0] * 0.2126 + c[1] * 0.7152 + c[2] * 0.0722; return [l, l, l]; };

// Flat tweenable state (numbers and linear-RGB arrays) for a preset.
export function envState(THREE, name, rain = false) {
  const p = PRESETS[name];
  if (!p) throw new Error(`[engine] unknown env preset "${name}"`);
  const C = (hex, k = 1) => new THREE.Color(hex).multiplyScalar(k).toArray();
  const s = {
    keyAz: p.key[0], keyEl: p.key[1], keyColor: C(p.keyColor), keyI: p.keyI,
    sunAz: p.sun[0], sunEl: p.sun[1],
    hemiSky: C(p.hemi[0]), hemiGround: C(p.hemi[1]), hemiI: p.hemi[2], envI: p.envI,
    fogColor: C(p.fog[0]), fogDensity: p.fog[1], exposure: p.exposure, bloom: p.bloom[0], bloomRadius: p.bloom[1], bloomThreshold: p.bloom[2] ?? 0.85,
    turbidity: p.sky.turbidity, rayleigh: p.sky.rayleigh, mie: p.sky.mie, mieG: p.sky.mieG, skyGain: p.sky.gain,
    night: p.night.amount, zenith: C(p.night.zenith), horizon: C(p.night.horizon), glow: p.night.glow, glowColor: C(p.night.glowColor),
    stars: p.night.stars, moon: p.night.moon,
    cloudCover: p.clouds.cover, cloudOpacity: p.clouds.opacity, cloudLit: C(p.clouds.lit, p.clouds.gain), cloudShade: C(p.clouds.shade, p.clouds.gain),
    hills: C(p.hills), skyVis: p.noSky ? 0 : 1, shadowBox: p.shadowBox, wet: 0,
  };
  if (rain && p.outdoor) { // overcast, hazier, darker, wet ground
    s.fogDensity *= 1.8; s.fogColor = lerpArr(s.fogColor, grey(s.fogColor), 0.65);
    s.keyI *= 0.3; s.turbidity = Math.max(s.turbidity, 10); s.skyGain *= 0.55; s.stars = 0; s.moon *= 0.15;
    s.cloudCover = 0.97; s.cloudOpacity = 1; s.cloudLit = grey(s.cloudLit).map(v => v * 0.55); s.cloudShade = grey(s.cloudShade).map(v => v * 0.5);
    s.hemiI *= 1.15; s.envI *= 0.85; s.bloom += 0.1; s.wet = 1; s.hills = lerpArr(s.hills, s.fogColor, 0.6);
  }
  return s;
}

export function gradeFor(name, rain = false) {
  const g = { ...PRESETS[name].grade };
  if (rain && PRESETS[name].outdoor) { g.saturation *= 0.85; g.contrast *= 0.97; }
  return g;
}

// Linear blend of two states (numbers and arrays).
export function mixState(a, b, k) {
  const out = {};
  for (const key in b) out[key] = Array.isArray(b[key]) ? lerpArr(a[key], b[key], k) : a[key] + (b[key] - a[key]) * k;
  return out;
}
