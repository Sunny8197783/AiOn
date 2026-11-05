// Post chain: RenderPass → GTAO (high: half-res, ultra: full-res) → UnrealBloom → Grade (eye adaptation, camera motion blur,
// sharpen, exposure, saturation, filmic contrast, split toning, tint, vignette, grain, edge chroma, fade) → Output (ACES + sRGB)
// → SMAA. Which of them run is the tier's (engine/quality.js); '완전 낮음' keeps only the grade and the tone mapping.
// The grade runs in linear HDR (the composer's targets are half-float) so it never clips before tone mapping.
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { GTAOPass } from 'three/addons/postprocessing/GTAOPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { SMAAPass } from 'three/addons/postprocessing/SMAAPass.js';
import { FullScreenQuad } from 'three/addons/postprocessing/Pass.js';
import { profileOf } from './quality.js';

const GradeShader = {
  uniforms: {
    tDiffuse: { value: null }, uExposure: { value: 0 }, uSat: { value: 1 }, uContrast: { value: 1 },
    uTint: { value: new THREE.Vector3(1, 1, 1) }, uTintAmt: { value: 0 }, uVignette: { value: 0.2 }, uGrain: { value: 0.03 },
    uChroma: { value: 0 }, uFade: { value: 1 }, uTime: { value: 0 }, uRes: { value: new THREE.Vector2(1, 1) },
    uShadowTint: { value: new THREE.Vector3(1, 1, 1) }, uHighTint: { value: new THREE.Vector3(1, 1, 1) }, uSplit: { value: 0 },
    uSharpen: { value: 0 }, uBlur: { value: 0 }, uReproj: { value: new THREE.Matrix4() }, tAdapt: { value: null }, uAdapt: { value: 0 },
  },
  vertexShader: /* glsl */`varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
  fragmentShader: /* glsl */`
uniform sampler2D tDiffuse, tAdapt; uniform float uAdapt; uniform vec3 uTint, uShadowTint, uHighTint; uniform vec2 uRes; uniform mat4 uReproj;
uniform float uExposure, uSat, uContrast, uTintAmt, uVignette, uGrain, uChroma, uFade, uTime, uSplit, uSharpen, uBlur;
varying vec2 vUv;
float hash(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
vec3 tap(vec2 uv) { return texture2D(tDiffuse, uv).rgb; }
void main() {
  vec2 d = vUv - 0.5; float r2 = dot(d, d);
  vec3 c;
  // Camera motion blur: where this pixel's view ray pointed a moment ago (rotation only, no depth needed).
  vec2 vel = vec2(0.0);
  if (uBlur > 0.0) { vec4 p = uReproj * vec4(vUv * 2.0 - 1.0, 1.0, 1.0); vel = (vUv * 2.0 - 1.0 - p.xy / p.w) * 0.5 * uBlur; float l = length(vel); if (l > 0.04) vel *= 0.04 / l; }
  if (length(vel * uRes) > 1.5) {
    float j = hash(vUv * uRes + uTime) - 0.5; c = vec3(0.0);
    for (int i = 0; i < 8; i++) c += tap(vUv - vel * ((float(i) + j) / 7.0 - 0.5));
    c /= 8.0;
  } else if (uChroma > 0.0) { vec2 o = d * r2 * uChroma; c = vec3(tap(vUv - o).r, tap(vUv).g, tap(vUv + o).b); }
  else c = tap(vUv);
  if (uSharpen > 0.0) { // contrast-adaptive sharpening, clamped to the neighbourhood (no halos around lamps)
    vec2 px = 1.0 / uRes; vec3 a = tap(vUv + vec2(px.x, 0.0)), b = tap(vUv - vec2(px.x, 0.0)), e = tap(vUv + vec2(0.0, px.y)), f = tap(vUv - vec2(0.0, px.y));
    vec3 mn = min(min(a, b), min(e, f)), mx = max(max(a, b), max(e, f));
    c = clamp(c + (c * 4.0 - a - b - e - f) * uSharpen * 0.25, min(mn, c), max(mx, c));
  }
  c *= exp2(uExposure);
  if (uAdapt > 0.0) c *= mix(1.0, texture2D(tAdapt, vec2(0.5)).r, uAdapt); // eye adaptation
  float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
  c = max(mix(vec3(l), c, uSat), 0.0);
  c = 0.18 * pow(c / 0.18 + 1e-5, vec3(uContrast));           // filmic contrast around mid grey
  float hl = smoothstep(0.02, 0.6, l / (l + 0.18));             // split toning: cool shadows, warm highlights
  c *= mix(vec3(1.0), mix(uShadowTint, uHighTint, hl), uSplit);
  c *= mix(vec3(1.0), uTint, uTintAmt);
  vec2 asp = vec2(uRes.x / uRes.y, 1.0);
  float v = smoothstep(0.3, 1.0, length(d * asp) / length(0.5 * asp));   // 0 centre .. 1 corners
  c *= 1.0 - uVignette * v * v;
  float n = hash(floor(vUv * uRes) + fract(uTime) * 517.0) - 0.5;
  c *= 1.0 + n * uGrain * 2.0; c += n * uGrain * 0.015;      // film grain: multiplicative + a touch in the toe
  gl_FragColor = vec4(max(c, 0.0) * uFade, 1.0);
}`,
};

// Eye adaptation: the scene's centre-weighted log luminance (64x64 metering image, 64 taps into a 1x1 target),
// eased toward over time; the grade multiplies by the resulting exposure (square root of the full correction,
// within 0.6..1.35, so the presets' own night/day moods survive). Brightness steps (a door to the sun) open up over a second or two.
const LUM_FS = `uniform sampler2D tSrc; varying vec2 vUv;
void main() { vec3 c = texture2D(tSrc, vUv).rgb; gl_FragColor = vec4(log(max(dot(c, vec3(0.2126, 0.7152, 0.0722)), 1e-4)), 0.0, 0.0, 1.0); }`;
const ADAPT_FS = `uniform sampler2D tLum, tPrev; uniform float uK, uFirst; varying vec2 vUv;
void main() {
  float s = 0.0, w = 0.0;
  for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) {
    vec2 uv = (vec2(float(x), float(y)) + 0.5) / 8.0; float k = 1.0 - 0.75 * length(uv - 0.5) * 1.41;
    s += texture2D(tLum, uv).r * k; w += k;
  }
  float target = clamp(sqrt(0.16 / exp(s / w)), 0.6, 1.35); // half-strength: moods survive, extremes ease
  float prev = texture2D(tPrev, vec2(0.5)).r;
  gl_FragColor = vec4(uFirst > 0.5 ? target : prev + (target - prev) * uK, 0.0, 0.0, 1.0);
}`;
const VS = 'varying vec2 vUv; void main() { vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }';

const KEYS = ['saturation', 'contrast', 'exposure', 'vignette', 'grain', 'tintAmount', 'split'];
const TINTS = ['tint', 'shadows', 'highlights'];
const DEFAULT_GRADE = { saturation: 1, contrast: 1, exposure: 0, vignette: 0.2, grain: 0.03, tint: [1, 1, 1], tintAmount: 0, shadows: [0.9, 1, 1.08], highlights: [1.06, 1, 0.92], split: 0.5 };
const ease = (k) => k * k * (3 - 2 * k);

export function createPost({ renderer, scene, camera, quality = 'high', reduceMotion = false }) {
  // Half-float targets keep HDR until the OutputPass. They carry depth textures, so GTAO reads the depth the scene
  // render already wrote (normals rebuilt from it) instead of drawing the whole scene a second time into a G-buffer.
  const target = new THREE.WebGLRenderTarget(1, 1, { type: THREE.HalfFloatType, depthTexture: new THREE.DepthTexture(1, 1) });
  target.texture.name = 'EffectComposer.rt1';
  const composer = new EffectComposer(renderer, target); // rt2 is a clone with its own depth texture
  const renderPass = new RenderPass(scene, camera);
  const gtao = new GTAOPass(scene, camera, 2, 2, undefined, { radius: 0.6, distanceExponent: 1.6, thickness: 1.4, scale: 1.0, samples: 12 });
  // Built with its own G-buffer target first (r180's setGBuffer reads normalRenderTarget even when given a depth
  // texture), then pointed at the composer's depth; that unused target is never drawn into, so it never allocates.
  gtao.setGBuffer(target.depthTexture);
  gtao.blendIntensity = 0.9;
  let aoHalf = true, aoSamples = 12;
  const gtaoSize = gtao.setSize.bind(gtao);
  gtao.setSize = (w, h) => gtaoSize(aoHalf ? Math.max(1, w >> 1) : w, aoHalf ? Math.max(1, h >> 1) : h);
  const gtaoRender = gtao.render.bind(gtao);
  gtao.render = (r, write, read, ...rest) => { // the composer swaps its two targets: read the depth of this frame's
    const d = read.depthTexture, U2 = gtao.gtaoMaterial.uniforms;
    if (d && U2.tDepth.value !== d) { gtao.depthTexture = d; U2.tDepth.value = gtao.pdMaterial.uniforms.tDepth.value = d; }
    gtaoRender(r, write, read, ...rest);
  };
  const bloom = new UnrealBloomPass(new THREE.Vector2(256, 256), 0.3, 0.5, 0.85);
  let bloomHalf = false;
  const bloomSize = bloom.setSize.bind(bloom);
  bloom.setSize = (w, h) => bloomSize(bloomHalf ? Math.max(2, w >> 1) : w, bloomHalf ? Math.max(2, h >> 1) : h);
  const grade = new ShaderPass(GradeShader);
  const output = new OutputPass();
  const smaa = new SMAAPass();
  for (const p of [renderPass, gtao, bloom, grade, output, smaa]) composer.addPass(p);

  // Scene stats: the frame's draw calls/triangles right after the scene (and shadow) render, before post.
  const info = renderer.info; info.autoReset = false;
  const sceneStats = { calls: 0, triangles: 0 };
  const rpRender = renderPass.render.bind(renderPass);
  renderPass.render = (...a) => { rpRender(...a); sceneStats.calls = info.render.calls; sceneStats.triangles = info.render.triangles; };

  // Tweened grade.
  const cur = structuredClone(DEFAULT_GRADE); let from = structuredClone(cur), to = structuredClone(cur), k = 1, dur = 0;
  let fade = 1, fadeTarget = 1, fadeRate = 0, RM = reduceMotion;
  const waiters = [];
  function setGrade(partial = {}, seconds = 1) {
    from = structuredClone(cur); to = { ...to, ...partial };
    for (const key of TINTS) { if (partial[key]) to[key] = [...partial[key]]; if (!Array.isArray(to[key]) || to[key].length !== 3) to[key] = from[key]; }
    for (const key of KEYS) if (typeof to[key] !== 'number' || !Number.isFinite(to[key])) to[key] = from[key];
    dur = Math.max(0, seconds); k = dur ? 0 : 1;
    if (!dur) Object.assign(cur, structuredClone(to));
  }
  // Fade to/from black; resolves when done.
  function fadeTo(v, seconds) {
    fadeTarget = v;
    if (!seconds) { fade = v; return Promise.resolve(); }
    fadeRate = Math.abs(v - fade) / seconds;
    return new Promise((res) => { waiters.push(res); });
  }

  const U = grade.uniforms;
  // Eye adaptation targets (see ADAPT_FS); hooked in front of the grade so it meters the frame it grades.
  const rtOpt = { type: THREE.HalfFloatType, depthBuffer: false };
  const lumRT = new THREE.WebGLRenderTarget(64, 64, rtOpt), adaptRT = [0, 1].map(() => new THREE.WebGLRenderTarget(1, 1, { ...rtOpt, type: THREE.FloatType }));
  const lumQ = new FullScreenQuad(new THREE.ShaderMaterial({ uniforms: { tSrc: { value: null } }, vertexShader: VS, fragmentShader: LUM_FS, depthTest: false, depthWrite: false }));
  const adaptQ = new FullScreenQuad(new THREE.ShaderMaterial({ uniforms: { tLum: { value: lumRT.texture }, tPrev: { value: null }, uK: { value: 0 }, uFirst: { value: 1 } }, vertexShader: VS, fragmentShader: ADAPT_FS, depthTest: false, depthWrite: false }));
  let adaptOn = true, ai = 0, adaptDt = 0;
  const gradeRender = grade.render.bind(grade);
  grade.render = (r, write, read, ...rest) => {
    U.uAdapt.value = adaptOn ? 1 : 0;
    if (adaptOn) {
      const prevRT = r.getRenderTarget();
      lumQ.material.uniforms.tSrc.value = read.texture; r.setRenderTarget(lumRT); lumQ.render(r);
      const A = adaptQ.material.uniforms;
      A.tPrev.value = adaptRT[ai].texture; A.uK.value = 1 - Math.exp(-adaptDt * 1.6);
      r.setRenderTarget(adaptRT[1 - ai]); adaptQ.render(r); A.uFirst.value = 0;
      ai = 1 - ai; U.tAdapt.value = adaptRT[ai].texture;
      r.setRenderTarget(prevRT);
    }
    gradeRender(r, write, read, ...rest);
  };
  // Camera motion blur from the camera's turn since the last frame, as if the shutter were open for ~1/60 s
  // (the same length at any frame rate). Off with reduced motion and on the tiers without it.
  let blurOn = true, chromaOn = true;
  const prevQ = new THREE.Quaternion().copy(camera.quaternion), dq = new THREE.Quaternion(), qI = new THREE.Quaternion(), rot = new THREE.Matrix4();
  function motion(dt) {
    dq.copy(prevQ).invert().multiply(camera.quaternion); // current -> previous view, in view space
    prevQ.copy(camera.quaternion);
    const k = dt > 0 ? Math.min(1, (1 / 60) / dt) : 0, ang = 2 * Math.acos(Math.min(1, Math.abs(dq.w)));
    U.uBlur.value = blurOn && !RM && ang * k > 0.002 && ang < 0.6 ? 1 : 0; // a cut is not a turn
    if (!U.uBlur.value) return;
    dq.slerpQuaternions(qI, dq, k).invert();
    rot.makeRotationFromQuaternion(dq);
    U.uReproj.value.copy(camera.projectionMatrix).multiply(rot).multiply(camera.projectionMatrixInverse);
  }
  function update(dt) {
    if (k < 1) {
      k = Math.min(1, k + dt / dur); const e = ease(k);
      for (const key of KEYS) cur[key] = from[key] + (to[key] - from[key]) * e;
      for (const key of TINTS) cur[key] = from[key].map((v, i) => v + (to[key][i] - v) * e);
    }
    if (fade !== fadeTarget) { const s = fadeRate * dt; fade = Math.abs(fadeTarget - fade) <= s ? fadeTarget : fade + Math.sign(fadeTarget - fade) * s; }
    if (fade === fadeTarget && waiters.length) waiters.splice(0).forEach((r) => r());
    U.uExposure.value = cur.exposure; U.uSat.value = cur.saturation; U.uContrast.value = cur.contrast;
    U.uTint.value.fromArray(cur.tint); U.uTintAmt.value = cur.tintAmount; U.uVignette.value = cur.vignette;
    U.uShadowTint.value.fromArray(cur.shadows); U.uHighTint.value.fromArray(cur.highlights); U.uSplit.value = cur.split;
    U.uGrain.value = cur.grain * (RM ? 0.5 : 1); U.uChroma.value = RM || !chromaOn ? 0 : 0.006; U.uFade.value = fade;
    if (!RM) U.uTime.value = (U.uTime.value + dt * 7.31) % 1000;
  }

  const _s = new THREE.Vector2();
  function setQuality(q) {
    const P = profileOf(q);
    gtao.enabled = P.gtao > 0; aoHalf = P.gtao < 2;
    const n = P.gtao >= 2 ? 16 : 12;
    if (n !== aoSamples) { aoSamples = n; gtao.updateGtaoMaterial({ samples: n }); }
    adaptOn = P.adapt; blurOn = P.blur; chromaOn = P.chroma; U.uSharpen.value = P.sharpen;
    smaa.enabled = P.smaa;
    bloom.enabled = !!P.bloom; bloomHalf = P.bloom === 1;
    const s = renderer.getDrawingBufferSize(_s);
    bloom.setSize(s.x, s.y); gtao.setSize(s.x, s.y);
  }
  function setSize(w, h) {
    composer.setPixelRatio(renderer.getPixelRatio());
    composer.setSize(w, h);
    const s = renderer.getDrawingBufferSize(_s);
    U.uRes.value.set(s.x, s.y);
  }
  function render(dt) {
    info.reset();
    update(dt); motion(dt); adaptDt = dt;
    composer.render(dt);
  }
  setQuality(quality);

  return {
    composer, bloom, gtao, grade, sceneStats, render, setSize, setQuality, setGrade, fadeTo,
    setBloom(strength, radius, threshold) { bloom.strength = strength; if (radius !== undefined) bloom.radius = radius; if (threshold !== undefined) bloom.threshold = threshold; },
    setReduceMotion(v) { RM = !!v; },
    // Jump the eye adaptation straight to the current frame (after a fade or a level swap).
    get grade() { return structuredClone(to); }, // the grade being eased toward
    snapAdapt() { adaptQ.material.uniforms.uFirst.value = 1; },
    // The current adapted exposure multiplier (debug/tests; reads back one pixel).
    adaptValue() { const b = new Float32Array(4); renderer.readRenderTargetPixels(adaptRT[ai], 0, 0, 1, 1, b); return b[0]; },
  };
}
