// Post chain: RenderPass → GTAO (high only, half-res) → UnrealBloom → Grade (exposure, saturation, filmic
// contrast, tint, vignette, grain, edge chroma, fade) → Output (ACES + sRGB) → SMAA (high/medium).
// The grade runs in linear HDR (the composer's targets are half-float) so it never clips before tone mapping.
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { GTAOPass } from 'three/addons/postprocessing/GTAOPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { SMAAPass } from 'three/addons/postprocessing/SMAAPass.js';

const GradeShader = {
  uniforms: {
    tDiffuse: { value: null }, uExposure: { value: 0 }, uSat: { value: 1 }, uContrast: { value: 1 },
    uTint: { value: new THREE.Vector3(1, 1, 1) }, uTintAmt: { value: 0 }, uVignette: { value: 0.2 }, uGrain: { value: 0.03 },
    uChroma: { value: 0 }, uFade: { value: 1 }, uTime: { value: 0 }, uRes: { value: new THREE.Vector2(1, 1) },
  },
  vertexShader: /* glsl */`varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
  fragmentShader: /* glsl */`
uniform sampler2D tDiffuse; uniform vec3 uTint; uniform vec2 uRes;
uniform float uExposure, uSat, uContrast, uTintAmt, uVignette, uGrain, uChroma, uFade, uTime;
varying vec2 vUv;
float hash(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
void main() {
  vec2 d = vUv - 0.5; float r2 = dot(d, d);
  vec3 c;
  if (uChroma > 0.0) { vec2 o = d * r2 * uChroma; c = vec3(texture2D(tDiffuse, vUv - o).r, texture2D(tDiffuse, vUv).g, texture2D(tDiffuse, vUv + o).b); }
  else c = texture2D(tDiffuse, vUv).rgb;
  c *= exp2(uExposure);
  float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
  c = max(mix(vec3(l), c, uSat), 0.0);
  c = 0.18 * pow(c / 0.18 + 1e-5, vec3(uContrast));           // filmic contrast around mid grey
  c *= mix(vec3(1.0), uTint, uTintAmt);
  vec2 asp = vec2(uRes.x / uRes.y, 1.0);
  float v = smoothstep(0.3, 1.0, length(d * asp) / length(0.5 * asp));   // 0 centre .. 1 corners
  c *= 1.0 - uVignette * v * v;
  float n = hash(floor(vUv * uRes) + fract(uTime) * 517.0) - 0.5;
  c *= 1.0 + n * uGrain * 2.0; c += n * uGrain * 0.015;      // film grain: multiplicative + a touch in the toe
  gl_FragColor = vec4(max(c, 0.0) * uFade, 1.0);
}`,
};

const KEYS = ['saturation', 'contrast', 'exposure', 'vignette', 'grain', 'tintAmount'];
const DEFAULT_GRADE = { saturation: 1, contrast: 1, exposure: 0, vignette: 0.2, grain: 0.03, tint: [1, 1, 1], tintAmount: 0 };
const ease = (k) => k * k * (3 - 2 * k);

export function createPost({ renderer, scene, camera, quality = 'high', reduceMotion = false }) {
  const composer = new EffectComposer(renderer); // half-float targets keep HDR until the OutputPass
  const renderPass = new RenderPass(scene, camera);
  const gtao = new GTAOPass(scene, camera, 2, 2, undefined, { radius: 0.6, distanceExponent: 1.6, thickness: 1.4, scale: 1.0, samples: 12 });
  gtao.blendIntensity = 0.9;
  // Half-res AO, and keep sky shells, beams and other depthWrite-less effects out of the AO G-buffer.
  const gtaoSize = gtao.setSize.bind(gtao);
  gtao.setSize = (w, h) => gtaoSize(Math.max(1, w >> 1), Math.max(1, h >> 1));
  const hideBase = gtao._overrideVisibility.bind(gtao);
  gtao._overrideVisibility = () => {
    hideBase();
    scene.traverse((o) => {
      if (!o.visible) return;
      if (o.userData.noAO || (o.material && !Array.isArray(o.material) && o.material.transparent && !o.material.depthWrite)) { o.visible = false; gtao._visibilityCache.push(o); }
    });
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
    if (partial.tint) to.tint = [...partial.tint];
    for (const key of KEYS) if (typeof to[key] !== 'number' || !Number.isFinite(to[key])) to[key] = from[key];
    if (!Array.isArray(to.tint) || to.tint.length !== 3) to.tint = from.tint;
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
  function update(dt) {
    if (k < 1) {
      k = Math.min(1, k + dt / dur); const e = ease(k);
      for (const key of KEYS) cur[key] = from[key] + (to[key] - from[key]) * e;
      cur.tint = from.tint.map((v, i) => v + (to.tint[i] - v) * e);
    }
    if (fade !== fadeTarget) { const s = fadeRate * dt; fade = Math.abs(fadeTarget - fade) <= s ? fadeTarget : fade + Math.sign(fadeTarget - fade) * s; }
    if (fade === fadeTarget && waiters.length) waiters.splice(0).forEach((r) => r());
    U.uExposure.value = cur.exposure; U.uSat.value = cur.saturation; U.uContrast.value = cur.contrast;
    U.uTint.value.fromArray(cur.tint); U.uTintAmt.value = cur.tintAmount; U.uVignette.value = cur.vignette;
    U.uGrain.value = cur.grain * (RM ? 0.5 : 1); U.uChroma.value = RM ? 0 : 0.006; U.uFade.value = fade;
    if (!RM) U.uTime.value = (U.uTime.value + dt * 7.31) % 1000;
  }

  function setQuality(q) {
    gtao.enabled = q === 'high';
    smaa.enabled = q !== 'low';
    bloomHalf = q === 'low';
    const s = renderer.getDrawingBufferSize(new THREE.Vector2());
    bloom.setSize(s.x, s.y);
  }
  function setSize(w, h) {
    composer.setPixelRatio(renderer.getPixelRatio());
    composer.setSize(w, h);
    const s = renderer.getDrawingBufferSize(new THREE.Vector2());
    U.uRes.value.set(s.x, s.y);
  }
  function render(dt) {
    info.reset();
    update(dt);
    composer.render(dt);
  }
  setQuality(quality);

  return {
    composer, bloom, gtao, grade, sceneStats, render, setSize, setQuality, setGrade, fadeTo,
    setBloom(strength, radius, threshold) { bloom.strength = strength; if (radius !== undefined) bloom.radius = radius; if (threshold !== undefined) bloom.threshold = threshold; },
    setReduceMotion(v) { RM = !!v; },
  };
}
