// Sky and light rig: physical Sky shader (day/dusk), night dome with city glow, drifting clouds, stars,
// moon, a distant city ring (light group 'windows') with blinking aviation lights, layered hills,
// the sun/moon shadow light that follows the player, and cached PMREM environments per preset.
import * as THREE from 'three';
import { Sky } from 'three/addons/objects/Sky.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { PRESETS, MOON, envState } from './presets.js';

const D2R = Math.PI / 180;
const UP = new THREE.Vector3(0, 1, 0);
export const dirFrom = (az, el, v = new THREE.Vector3()) =>
  v.set(Math.sin(az * D2R) * Math.cos(el * D2R), Math.sin(el * D2R), -Math.cos(az * D2R) * Math.cos(el * D2R));

// Camera-centred shells drawn at the far plane (z = w) so they never clip and always sit behind geometry.
const FAR_VS = /* glsl */`varying vec3 vDir;
void main() { vDir = position; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); gl_Position.z = gl_Position.w; }`;

const DOME_FS = /* glsl */`uniform vec3 uZenith, uHorizon, uGlowColor; uniform float uGlow, uNight; varying vec3 vDir;
void main() {
  vec3 d = normalize(vDir); float h = max(d.y, 0.0);
  vec3 c = mix(uHorizon, uZenith, pow(h, 0.45)) + uGlowColor * uGlow * exp(-h * 9.0) * 0.08;
  if (d.y < 0.0) c = uHorizon * 0.6 + uGlowColor * uGlow * 0.05;
  gl_FragColor = vec4(c, uNight);
}`;

const CLOUD_FS = /* glsl */`uniform float uCover, uOpacity, uTime; uniform vec3 uLit, uShade, uSunDir; varying vec3 vDir;
float h2(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h2(i), h2(i + vec2(1.0, 0.0)), f.x), mix(h2(i + vec2(0.0, 1.0)), h2(i + vec2(1.0, 1.0)), f.x), f.y); }
float fbm(vec2 p) { float s = 0.0, a = 0.5; for (int i = 0; i < OCT; i++) { s += a * vn(p); p = p * 2.07 + vec2(1.7, 9.2); a *= 0.5; } return s; }
void main() {
  vec3 d = normalize(vDir); if (d.y <= 0.0) discard;
  vec2 uv = d.xz / (d.y + 0.1) * 1.6 + vec2(uTime * 0.012, uTime * 0.004);
  float n = fbm(uv), edge = 1.0 - uCover;
  float a = smoothstep(edge, edge + 0.22, n), thick = smoothstep(edge, edge + 0.5, n);
  float sun = pow(max(dot(d, uSunDir), 0.0), 8.0);
  vec3 c = mix(uLit, uShade, thick * 0.75) + uLit * sun * (1.0 - thick) * 0.8;
  gl_FragColor = vec4(c, a * smoothstep(0.0, 0.2, d.y) * uOpacity);
}`;

function makeSky() { // Sky addon with an extra brightness uniform so presets can balance it against the scene
  const sky = new Sky();
  const m = sky.material;
  m.uniforms.skyGain = { value: 1 };
  const fs = m.fragmentShader.replace('uniform vec3 up;', 'uniform vec3 up;\nuniform float skyGain;')
    .replace('gl_FragColor = vec4( retColor, 1.0 );', 'gl_FragColor = vec4( retColor * skyGain, 1.0 );');
  if (fs.split('skyGain').length === 3) m.fragmentShader = fs;
  else console.warn('[engine/sky] Sky shader patch did not apply; sky brightness is fixed');
  sky.userData.noAO = true;
  return sky;
}

const shell = (r, fs, uniforms, order, defines) => {
  const m = new THREE.Mesh(new THREE.SphereGeometry(r, 48, 24), new THREE.ShaderMaterial({
    uniforms, vertexShader: FAR_VS, fragmentShader: fs, defines, side: THREE.BackSide, transparent: true, depthWrite: false, fog: false,
  }));
  m.renderOrder = order; m.frustumCulled = false; m.userData.noAO = true;
  return m;
};

function makeStars(kit) {
  const r = kit.rng(77), n = 2400, pos = new Float32Array(n * 3), col = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) {
    const y = 0.02 + r() * 0.98, th = r() * Math.PI * 2, s = Math.sqrt(1 - y * y), b = 0.25 + 1.8 * r() ** 7, t = r();
    pos.set([Math.cos(th) * s * 1800, y * 1800, Math.sin(th) * s * 1800], i * 3);
    col.set(t < 0.15 ? [b, b * 0.85, b * 0.7] : t < 0.35 ? [b * 0.8, b * 0.9, b] : [b, b, b], i * 3);
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3)); g.setAttribute('color', new THREE.BufferAttribute(col, 3));
  const dot = new THREE.CanvasTexture(kit.canvas(32, 32, (c) => {
    const gr = c.createRadialGradient(16, 16, 0, 16, 16, 16); gr.addColorStop(0, '#fff'); gr.addColorStop(0.35, 'rgba(255,255,255,0.6)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
    c.fillStyle = gr; c.fillRect(0, 0, 32, 32);
  }));
  const pts = new THREE.Points(g, new THREE.PointsMaterial({ size: 2.2, sizeAttenuation: false, map: dot, vertexColors: true, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, fog: false }));
  pts.renderOrder = -3; pts.frustumCulled = false;
  return pts;
}

function makeMoon(kit) {
  const r = kit.rng(9);
  const map = new THREE.CanvasTexture(kit.canvas(256, 256, (g) => {
    const halo = g.createRadialGradient(128, 128, 24, 128, 128, 128);
    halo.addColorStop(0, 'rgba(200,215,255,0.35)'); halo.addColorStop(0.35, 'rgba(160,180,230,0.08)'); halo.addColorStop(1, 'rgba(0,0,0,0)');
    g.fillStyle = halo; g.fillRect(0, 0, 256, 256);
    g.save(); g.beginPath(); g.arc(128, 128, 30, 0, 7); g.clip();
    g.fillStyle = '#e8ecf2'; g.fillRect(90, 90, 80, 80);
    for (let i = 0; i < 10; i++) { g.fillStyle = `rgba(110,118,130,${0.15 + r() * 0.2})`; g.beginPath(); g.arc(128 + (r() - 0.5) * 40, 128 + (r() - 0.5) * 40, 4 + r() * 9, 0, 7); g.fill(); }
    g.restore();
  }));
  map.colorSpace = THREE.SRGBColorSpace;
  const s = new THREE.Sprite(new THREE.SpriteMaterial({ map, transparent: true, depthWrite: false, fog: false, blending: THREE.AdditiveBlending }));
  s.material.color.setScalar(2.5); s.scale.setScalar(145); s.renderOrder = -3; s.userData.noAO = true;
  return s;
}

// Distant city: one merged mesh of boxes with a tiled window texture (8 floors x 8 bays per 32 x 24 m tile).
function makeSkyline(kit) {
  const r = kit.rng(4242), cells = [];
  const map = kit.texture(kit.canvas(512, 512, (g) => {
    g.fillStyle = '#b9b4aa'; g.fillRect(0, 0, 512, 512);
    for (let y = 0; y < 8; y++) for (let x = 0; x < 8; x++) {
      g.fillStyle = 'rgba(0,0,0,0.07)'; g.fillRect(x * 64, y * 64 + 58, 64, 3);
      g.fillStyle = '#2b3440'; g.fillRect(x * 64 + 8, y * 64 + 14, 48, 34);
      g.fillStyle = 'rgba(170,195,215,0.18)'; g.fillRect(x * 64 + 8, y * 64 + 14, 20, 34);
      cells.push([x * 64 + 8, y * 64 + 14]);
    }
  }));
  const emissiveMap = kit.texture(kit.canvas(512, 512, (g) => {
    g.fillStyle = '#000'; g.fillRect(0, 0, 512, 512);
    for (const [x, y] of cells) if (r() < 0.42) { g.fillStyle = r() < 0.75 ? '#ffcf8a' : '#d8ecff'; g.globalAlpha = 0.5 + r() * 0.5; g.fillRect(x, y, 48, 34); }
  }));
  const geoms = [], tops = [], placed = [];
  for (let tries = 0; geoms.length < 160 && tries < 1500; tries++) {
    const ang = r() * Math.PI * 2, dist = 330 + r() ** 0.7 * 300, x = Math.cos(ang) * dist, z = Math.sin(ang) * dist, k = r();
    let w, d, h, ry;
    if (k < 0.05) { w = d = 24 + r() * 8; h = 95 + r() * 45; ry = r() * Math.PI; } // landmark tower
    else if (k < 0.6) { w = 48 + r() * 30; d = 13 + r() * 4; h = 39 + Math.floor(r() * 10) * 3; ry = (r() - 0.5) * 0.3; } // apartment slab facing south
    else { w = 18 + r() * 26; d = 14 + r() * 18; h = 9 + Math.floor(r() * 8) * 3; ry = r() * Math.PI; } // low-rise
    const rad = Math.max(w, d) * 0.6;
    if (placed.some(p => Math.hypot(p.x - x, p.z - z) < p.rad + rad)) continue;
    placed.push({ x, z, rad });
    const g = new THREE.BoxGeometry(w, h, d), uv = g.attributes.uv, n = g.attributes.normal, off = Math.floor(r() * 8) / 8;
    for (let i = 0; i < uv.count; i++) {
      if (Math.abs(n.getY(i)) > 0.5) { uv.setXY(i, 0.004, 0.004); continue; } // roof: a plain wall texel
      uv.setXY(i, uv.getX(i) * (Math.abs(n.getX(i)) > 0.5 ? d : w) / 32 + off, uv.getY(i) * h / 24);
    }
    g.rotateY(ry); g.translate(x, h / 2 - 2, z); geoms.push(g);
    if (h > 90) tops.push(x, h - 1, z);
  }
  const mat = new THREE.MeshStandardMaterial({ map, emissiveMap, emissive: 0xffffff, emissiveIntensity: 1, roughness: 0.9, metalness: 0 });
  kit.lit('windows', mat, 1.3);
  const city = new THREE.Mesh(mergeGeometries(geoms), mat);
  city.castShadow = city.receiveShadow = false;
  const beacons = new THREE.InstancedMesh(new THREE.SphereGeometry(1.2, 8, 6), new THREE.MeshBasicMaterial({ color: new THREE.Color(9, 0.5, 0.3), fog: false }), tops.length / 3);
  for (let i = 0; i < tops.length / 3; i++) beacons.setMatrixAt(i, new THREE.Matrix4().makeTranslation(tops[i * 3], tops[i * 3 + 1], tops[i * 3 + 2]));
  beacons.castShadow = false;
  return { city, beacons };
}

// A ring of hills (unlit silhouette, colour set per preset). peaks: [angle rad, width rad, extra height 0..1].
function hillRing(kit, R, base, amp, seed, peaks = []) {
  const r = kit.rng(seed), seg = 220, ph = [r(), r(), r(), r()].map(v => v * 6.28), pos = [], col = [], idx = [];
  for (let i = 0; i <= seg; i++) {
    const a = (i / seg) * Math.PI * 2;
    let h = 0.5 + 0.25 * Math.sin(a * 3 + ph[0]) + 0.15 * Math.sin(a * 7 + ph[1]) + 0.08 * Math.sin(a * 13 + ph[2]) + 0.05 * Math.sin(a * 29 + ph[3]);
    for (const [pa, pw, pk] of peaks) { const da = Math.atan2(Math.sin(a - pa), Math.cos(a - pa)); h += pk * Math.exp(-(da * da) / (pw * pw)); }
    const x = Math.cos(a) * R, z = Math.sin(a) * R;
    pos.push(x, -30, z, x, base + amp * Math.max(0, h), z); col.push(0.6, 0.6, 0.6, 1, 1, 1);
    if (i < seg) { const k = i * 2; idx.push(k, k + 1, k + 2, k + 1, k + 3, k + 2); }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3)); g.setIndex(idx);
  const m = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ vertexColors: true, fog: false, side: THREE.DoubleSide }));
  m.castShadow = m.receiveShadow = false;
  return m;
}

export function createSky({ renderer, scene, kit, quality = 'high' }) {
  const keyDir = new THREE.Vector3(0, 1, 0), sunDir = new THREE.Vector3(), moonDir = dirFrom(MOON[0], MOON[1]);
  scene.fog = new THREE.FogExp2(0xc4d2df, 0.002);
  scene.background = new THREE.Color(0xc4d2df);

  const sun = new THREE.DirectionalLight(0xffffff, 3);
  sun.castShadow = true; sun.shadow.bias = -0.0004; sun.shadow.normalBias = 0.03;
  sun.shadow.camera.near = 1; sun.shadow.camera.far = 260;
  const hemi = new THREE.HemisphereLight(0xffffff, 0x444444, 0.5);
  scene.add(sun, sun.target, hemi);

  // Camera-following sky shells.
  const group = new THREE.Group(); group.name = 'sky';
  const skyMesh = makeSky(); skyMesh.scale.setScalar(2000); skyMesh.renderOrder = -5;
  const domeU = { uZenith: { value: new THREE.Color() }, uHorizon: { value: new THREE.Color() }, uGlowColor: { value: new THREE.Color() }, uGlow: { value: 0 }, uNight: { value: 0 } };
  const dome = shell(1900, DOME_FS, domeU, -4);
  const cloudU = { uCover: { value: 0.3 }, uOpacity: { value: 1 }, uTime: { value: 0 }, uLit: { value: new THREE.Color() }, uShade: { value: new THREE.Color() }, uSunDir: { value: new THREE.Vector3(0, 1, 0) } };
  const clouds = shell(1700, CLOUD_FS, cloudU, -2, { OCT: quality === 'high' ? 5 : 4 });
  const stars = makeStars(kit), moon = makeMoon(kit);
  group.add(skyMesh, dome, stars, moon, clouds);
  // Fixed distant world: city ring, aviation beacons, near and far hills (광교산 rises to the north).
  const world = new THREE.Group(); world.name = 'skyline';
  const { city, beacons } = makeSkyline(kit);
  const hillsNear = hillRing(kit, 760, 25, 110, 5, [[Math.PI * 1.5, 0.35, 0.9], [0.4, 0.25, 0.4]]);
  const hillsFar = hillRing(kit, 1100, 50, 150, 11, [[Math.PI * 1.35, 0.5, 0.6], [2.6, 0.4, 0.5]]);
  world.add(city, beacons, hillsNear, hillsFar);
  scene.add(group, world);

  let shadowBox = 70, cur = null;
  function apply(S) {
    cur = S;
    dirFrom(S.keyAz, S.keyEl, keyDir); dirFrom(S.sunAz, S.sunEl, sunDir);
    sun.color.fromArray(S.keyColor); sun.intensity = S.keyI; sun.visible = S.keyI > 0.01;
    hemi.color.fromArray(S.hemiSky); hemi.groundColor.fromArray(S.hemiGround); hemi.intensity = S.hemiI;
    scene.fog.color.fromArray(S.fogColor); scene.fog.density = S.fogDensity; scene.background.copy(scene.fog.color);
    scene.environmentIntensity = S.envI; renderer.toneMappingExposure = S.exposure;
    const su = skyMesh.material.uniforms;
    su.sunPosition.value.copy(sunDir); su.turbidity.value = S.turbidity; su.rayleigh.value = S.rayleigh;
    su.mieCoefficient.value = S.mie; su.mieDirectionalG.value = S.mieG; su.skyGain.value = S.skyGain;
    domeU.uZenith.value.fromArray(S.zenith); domeU.uHorizon.value.fromArray(S.horizon); domeU.uGlowColor.value.fromArray(S.glowColor);
    domeU.uGlow.value = S.glow; domeU.uNight.value = S.night; dome.visible = S.night > 0.002;
    stars.material.opacity = S.stars; stars.visible = S.stars > 0.005;
    moon.material.opacity = S.moon; moon.visible = S.moon > 0.005;
    cloudU.uCover.value = S.cloudCover; cloudU.uOpacity.value = S.cloudOpacity; clouds.visible = S.cloudOpacity > 0.01;
    cloudU.uLit.value.fromArray(S.cloudLit); cloudU.uShade.value.fromArray(S.cloudShade); cloudU.uSunDir.value.copy(S.sunEl > -4 ? sunDir : keyDir);
    hillsNear.material.color.fromArray(S.hills); hillsFar.material.color.fromArray(S.hills).lerp(scene.fog.color, 0.45);
    group.visible = world.visible = S.skyVis > 0.5;
    shadowBox = S.shadowBox;
  }

  const fwd = new THREE.Vector3(), right = new THREE.Vector3(), up2 = new THREE.Vector3(), snapped = new THREE.Vector3();
  let time = 0;
  // Per frame: shells follow the camera, clouds drift, beacons blink, the shadow box follows the player
  // snapped to whole shadow texels in light space (no shimmering while walking).
  function update(dt, camera, focus, reduceMotion) {
    group.position.copy(camera.position);
    moon.position.copy(moonDir).multiplyScalar(1500);
    if (!reduceMotion) time += dt;
    cloudU.uTime.value = time;
    beacons.visible = kit.isLit('windows') && (reduceMotion || (time % 1.6) < 0.8);
    if (!sun.visible) return;
    const half = shadowBox / 2, cam = sun.shadow.camera;
    if (cam.right !== half) { cam.left = cam.bottom = -half; cam.right = cam.top = half; cam.updateProjectionMatrix(); }
    const texel = (2 * half) / sun.shadow.mapSize.x;
    fwd.copy(keyDir).negate(); right.crossVectors(UP, fwd);
    if (right.lengthSq() < 1e-6) right.set(1, 0, 0);
    right.normalize(); up2.crossVectors(fwd, right);
    snapped.copy(right).multiplyScalar(Math.round(focus.dot(right) / texel) * texel)
      .addScaledVector(up2, Math.round(focus.dot(up2) / texel) * texel).addScaledVector(fwd, focus.dot(fwd));
    sun.target.position.copy(snapped); sun.position.copy(snapped).addScaledVector(keyDir, 100);
  }

  // PMREM environments, generated once per (preset, rain) and cached; indoor presets share RoomEnvironment.
  const pmrem = new THREE.PMREMGenerator(renderer), cache = new Map();
  let envScene = null;
  function environment(name, rain) {
    const p = PRESETS[name], key = p.env === 'room' ? 'room' : `${name}:${rain && p.outdoor ? 1 : 0}`;
    if (cache.has(key)) return cache.get(key);
    let tex;
    if (p.env === 'room') { const room = new RoomEnvironment(); tex = pmrem.fromScene(room, 0.04).texture; room.dispose(); }
    else {
      if (!envScene) {
        envScene = new THREE.Scene();
        const s = makeSky(); s.scale.setScalar(50);
        const d = new THREE.Mesh(new THREE.SphereGeometry(40, 32, 16), dome.material.clone());
        const ground = new THREE.Mesh(new THREE.CircleGeometry(60, 32).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial());
        ground.position.y = -1.5;
        envScene.add(s, d, ground); envScene.userData = { s, d, ground };
      }
      const S = envState(THREE, name, rain), { s, d, ground } = envScene.userData, u = s.material.uniforms, du = d.material.uniforms;
      u.sunPosition.value.copy(dirFrom(S.sunAz, S.sunEl)); u.turbidity.value = S.turbidity; u.rayleigh.value = S.rayleigh;
      u.mieCoefficient.value = S.mie; u.mieDirectionalG.value = S.mieG; u.skyGain.value = S.skyGain;
      du.uZenith.value.fromArray(S.zenith); du.uHorizon.value.fromArray(S.horizon); du.uGlowColor.value.fromArray(S.glowColor);
      du.uGlow.value = S.glow; du.uNight.value = S.night; d.visible = S.night > 0.002;
      ground.material.color.fromArray(S.hemiGround).multiplyScalar(0.5 * S.hemiI + 0.05);
      tex = pmrem.fromScene(envScene, 0.02).texture;
    }
    cache.set(key, tex);
    return tex;
  }

  function setQuality(q) {
    const n = q === 'high' ? 2048 : q === 'medium' ? 1024 : 512;
    if (sun.shadow.mapSize.x !== n) { sun.shadow.mapSize.set(n, n); sun.shadow.map?.dispose(); sun.shadow.map = null; }
    const oct = q === 'high' ? 5 : 4;
    if (clouds.material.defines.OCT !== oct) { clouds.material.defines.OCT = oct; clouds.material.needsUpdate = true; }
  }
  setQuality(quality);

  return { sun, hemi, group, world, keyDir, apply, update, environment, setQuality, get state() { return cur; } };
}
