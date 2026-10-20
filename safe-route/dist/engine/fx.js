// Particle effects: fire, smoke, rain, dust motes and extinguisher powder. Procedural textures, no assets.
// Billboards are instanced quads expanded to face the camera in the vertex shader: one draw call per system.
// Smoke and powder use Lambert shading, so they pick up the fire glow, the flashlight and the room lights.

export function createFx(THREE, kit, { reduceMotion } = {}) {
  let RM = reduceMotion ?? (typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches);
  const Q = { high: 1, medium: 0.7, low: 0.45 }[kit?.quality] ?? 1;
  const local = new Map();
  const memo = (k, make) => (kit?.memo ? kit.memo('fx:' + k, make) : (local.has(k) || local.set(k, make()), local.get(k)));
  const rand = Math.random;
  const clamp01 = (v) => Math.min(1, Math.max(0, v));
  const smooth = (a, b, v) => { const t = clamp01((v - a) / (b - a)); return t * t * (3 - 2 * t); };
  const tmpV = new THREE.Vector3(), tmpW = new THREE.Vector3();

  // ---- procedural textures ----
  const hash = (x, y) => { const s = Math.sin(x * 127.1 + y * 311.7) * 43758.5453; return s - Math.floor(s); };
  function vnoise(x, y) {
    const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
    const u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
    const a = hash(xi, yi), b = hash(xi + 1, yi), c = hash(xi, yi + 1), d = hash(xi + 1, yi + 1);
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
  }
  const fbm = (x, y) => vnoise(x, y) * 0.55 + vnoise(x * 2.03, y * 2.03) * 0.3 + vnoise(x * 4.11, y * 4.11) * 0.15;
  function pixels(size, cells, shade) { // shade(u -1..1, v 0..1 up, cell) -> [r,g,b,a] 0..255
    const c = document.createElement('canvas'); c.width = c.height = size;
    const g = c.getContext('2d'), img = g.createImageData(size, size), n = size / cells;
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
      const cell = ((cells - 1 - Math.floor(y / n)) * cells) + Math.floor(x / n); // row 0 = bottom (texture flipY)
      const px = shade(((x % n) + 0.5) / n * 2 - 1, 1 - ((y % n) + 0.5) / n, cell), i = (y * size + x) * 4;
      img.data[i] = px[0]; img.data[i + 1] = px[1]; img.data[i + 2] = px[2]; img.data[i + 3] = px[3];
    }
    g.putImageData(img, 0, 0);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
    return t;
  }
  const RAMP = [[0, 90, 14, 4], [0.3, 214, 62, 10], [0.6, 255, 150, 44], [0.85, 255, 222, 150], [1, 255, 248, 228]];
  function ramp(h) {
    for (let i = 1; i < RAMP.length; i++) if (h <= RAMP[i][0]) {
      const a = RAMP[i - 1], b = RAMP[i], k = (h - a[0]) / (b[0] - a[0]);
      return [a[1] + (b[1] - a[1]) * k, a[2] + (b[2] - a[2]) * k, a[3] + (b[3] - a[3]) * k];
    }
    return RAMP[RAMP.length - 1].slice(1);
  }
  // Four flame tongues: teardrop silhouettes torn by noise, hot white-yellow core fading to red edges.
  const flameTex = () => memo('flame', () => pixels(256, 2, (u, v, cell) => {
    const s = cell * 17.31, t = clamp01((v - 0.04) / 0.92);
    const n = fbm(u * 2.2 + s, v * 3.1 - s);
    const width = 1.48 * Math.pow(t, 0.35) * Math.pow(1 - t, 0.75);
    const d = Math.abs(u + (n - 0.5) * 0.6 * t) / Math.max(width, 1e-3);
    let a = smooth(1, 0.15, d) * clamp01(1.2 - t * 0.95 + (n - 0.5) * 0.9) * smooth(0, 0.1, v);
    a = clamp01(a);
    const [r, g, b] = ramp(clamp01(a * (1.05 - t * 0.6)));
    return [r, g, b, a * 255];
  }));
  // Four soft puffs with ragged, noisy edges (white; tinted per particle).
  const puffTex = () => memo('puff', () => pixels(256, 2, (u, v, cell) => {
    const y = v * 2 - 1, s = cell * 9.7, n = fbm(u * 2.6 + s, y * 2.6 - s);
    const d = Math.hypot(u, y) * (1 + (n - 0.5) * 0.7);
    const a = smooth(0.95, 0.25, d) * (0.45 + 0.55 * fbm(u * 5 - s, y * 5 + s));
    return [255, 255, 255, clamp01(a) * 255];
  }));
  const glowTex = () => memo('glow', () => pixels(64, 1, (u, v) => {
    const d = Math.hypot(u, v * 2 - 1);
    return [255, 255, 255, Math.exp(-d * d * 5) * smooth(1, 0.8, d) * 255];
  }));

  // ---- billboard systems ----
  function patch(material, cells) {
    material.transparent = true; material.depthWrite = false;
    material.onBeforeCompile = (s) => {
      s.vertexShader = 'attribute vec3 iPos;\nattribute vec4 iCol;\nattribute vec3 iSRF;\nvarying vec4 vFx;\n' + s.vertexShader
        .replace('#include <uv_vertex>', `#include <uv_vertex>\n#ifdef USE_MAP\nvMapUv = (vec2(mod(iSRF.z, ${cells}.0), floor(iSRF.z / ${cells}.0)) + uv) / ${cells}.0;\n#endif`)
        .replace('#include <normal_vertex>', 'transformedNormal = vec3(0.0, 0.0, 1.0);\n#include <normal_vertex>')
        .replace('#include <project_vertex>', [
          'vFx = iCol;',
          'vec4 mvPosition = modelViewMatrix * vec4(iPos, 1.0);',
          'float bbC = cos(iSRF.y), bbS = sin(iSRF.y);',
          'vec2 bbQ = position.xy * iSRF.x;',
          'mvPosition.xy += vec2(bbC * bbQ.x - bbS * bbQ.y, bbS * bbQ.x + bbC * bbQ.y);',
          'gl_Position = projectionMatrix * mvPosition;'].join('\n'));
      s.fragmentShader = 'varying vec4 vFx;\n' + s.fragmentShader.replace('#include <color_fragment>', '#include <color_fragment>\ndiffuseColor *= vFx;');
    };
    material.customProgramCacheKey = () => `fx-billboard-${cells}`;
    return material;
  }
  const flameMat = () => memo('flameMat', () => patch(new THREE.MeshBasicMaterial({ map: flameTex(), blending: THREE.AdditiveBlending }), 2));
  const glowMat = () => memo('glowMat', () => patch(new THREE.MeshBasicMaterial({ map: glowTex(), blending: THREE.AdditiveBlending }), 1));
  const smokeMat = () => memo('smokeMat', () => patch(new THREE.MeshLambertMaterial({ map: puffTex(), reflectivity: 0 }), 2));

  function billboards(max, material) {
    const g = new THREE.InstancedBufferGeometry();
    g.setIndex([0, 1, 2, 0, 2, 3]);
    g.setAttribute('position', new THREE.Float32BufferAttribute([-0.5, -0.5, 0, 0.5, -0.5, 0, 0.5, 0.5, 0, -0.5, 0.5, 0], 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute([0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1], 3));
    g.setAttribute('uv', new THREE.Float32BufferAttribute([0, 0, 1, 0, 1, 1, 0, 1], 2));
    const attr = (k) => new THREE.InstancedBufferAttribute(new Float32Array(Math.max(1, max) * k), k).setUsage(THREE.DynamicDrawUsage);
    const iPos = attr(3), iCol = attr(4), iSRF = attr(3);
    g.setAttribute('iPos', iPos); g.setAttribute('iCol', iCol); g.setAttribute('iSRF', iSRF);
    g.instanceCount = 0;
    const mesh = new THREE.Mesh(g, material);
    mesh.frustumCulled = false; mesh.castShadow = mesh.receiveShadow = false;
    let n = 0;
    return {
      mesh,
      begin() { n = 0; },
      push(p, size, rot, frame, r, gg, b, a) {
        if (n >= max || a <= 0.003) return;
        iPos.setXYZ(n, p.x, p.y, p.z); iSRF.setXYZ(n, size, rot, frame); iCol.setXYZW(n, r, gg, b, a); n++;
      },
      end() { g.instanceCount = n; iPos.needsUpdate = iCol.needsUpdate = iSRF.needsUpdate = true; },
      dispose() { g.dispose(); },
    };
  }
  // Back-to-front for normally blended particles; camera optional.
  function sortFor(list, obj, camera) {
    if (!camera || list.length < 2) return list;
    const c = obj.worldToLocal(camera.getWorldPosition(tmpV));
    for (const p of list) p.d = (p.x - c.x) ** 2 + (p.y - c.y) ** 2 + (p.z - c.z) ** 2;
    return list.sort((a, b) => b.d - a.d);
  }
  function makeGroup(name, o) { const g = new THREE.Group(); g.name = name; g.position.set(o.x || 0, o.y || 0, o.z || 0); return g; }
  function disposer(group, systems, light) {
    return () => { group.removeFromParent(); systems.forEach(s => s.dispose()); light?.dispose?.(); };
  }

  // ---- fire: flame tongues + base glow + embers + flickering warm light. opts: x,y,z, scale, radius, level, light (multiplier|false), shadow ----
  function fire(opts = {}) {
    const scale = opts.scale ?? 1, radius = (opts.radius ?? 0.28) * scale;
    const group = makeGroup('fx-fire', opts);
    const fmax = Math.max(1, Math.round((opts.count ?? 46) * Q)), emax = Math.max(1, Math.round(26 * Q));
    const flames = billboards(fmax, flameMat());
    const embers = billboards(emax, glowMat());
    const glow = billboards(2, glowMat());
    flames.mesh.renderOrder = 2; embers.mesh.renderOrder = 3;
    group.add(glow.mesh, flames.mesh, embers.mesh);
    const light = opts.light === false ? null : new THREE.PointLight(0xff8a3c, 0, 10 * scale, 2);
    if (light) { light.position.y = 0.45 * scale; light.castShadow = !!opts.shadow; light.shadow.bias = -0.002; group.add(light); }
    const lightMul = typeof opts.light === 'number' ? opts.light : 1;
    const P = [], E = [], base = new THREE.Vector3(0, 0.1 * scale, 0);
    let level = clamp01(opts.level ?? 1), shown = 0, acc = 0, eacc = 0, t = rand() * 10;

    function spawnFlame(rm) {
      const a = rand() * Math.PI * 2, d = Math.sqrt(rand()) * radius * (0.45 + 0.55 * shown);
      const sp = rm ? 0.6 : 1;
      return { x: Math.cos(a) * d, y: 0.02 * scale, z: Math.sin(a) * d, vy: (0.85 + rand() * 0.75) * Math.sqrt(scale) * (0.55 + 0.6 * shown) * sp,
        age: 0, life: (0.5 + rand() * 0.45) / sp, s0: (0.26 + rand() * 0.22) * scale * (0.55 + 0.6 * shown),
        rot: (rand() - 0.5) * 0.5, spin: rm ? 0 : (rand() - 0.5) * 0.9, frame: (rand() * 4) | 0, ph: rand() * 9 };
    }
    function update(dt) {
      dt = Math.min(dt || 0, 0.05); t += dt;
      const rm = opts.reduceMotion ?? RM;
      shown += (level - shown) * Math.min(1, dt * 1.6);
      acc += shown * fmax / 0.72 * (rm ? 0.55 : 1) * dt;
      while (acc >= 1) { acc--; if (P.length < fmax) P.push(spawnFlame(rm)); }
      flames.begin();
      for (let i = P.length - 1; i >= 0; i--) {
        const p = P[i]; p.age += dt; const k = p.age / p.life;
        if (k >= 1) { P.splice(i, 1); continue; }
        p.y += p.vy * dt; p.x *= 1 - dt * 1.3; p.z *= 1 - dt * 1.3; // tongues lean in as they rise
        if (!rm) { p.x += Math.sin(t * 6.1 + p.ph) * 0.12 * scale * dt; p.z += Math.cos(t * 5.3 + p.ph) * 0.12 * scale * dt; }
        p.rot += p.spin * dt;
        const size = p.s0 * (0.75 + 0.7 * Math.sin(Math.min(k * 1.25, 1) * Math.PI * 0.5) - 0.45 * k * k);
        const heat = 1 - k, a = smooth(0, 0.1, k) * Math.pow(1 - k, 1.1) * Math.min(1, shown * 1.6);
        flames.push(p, size, p.rot, p.frame, 2.4 * (0.7 + 0.3 * heat), 2.4 * (0.32 + 0.5 * heat * heat), 2.4 * (0.1 + 0.35 * heat * heat * heat), a);
      }
      flames.end();
      glow.begin();
      glow.push(base, radius * 4.2, 0, 0, 1.0, 0.42, 0.14, 0.55 * shown);
      glow.end();
      eacc += (shown > 0.25 ? (rm ? 2 : 7) * shown * scale : 0) * dt;
      while (eacc >= 1) {
        eacc--;
        if (E.length < emax) E.push({ x: (rand() - 0.5) * radius, y: 0.35 * scale, z: (rand() - 0.5) * radius,
          vx: (rand() - 0.5) * 0.4, vy: (1.1 + rand()) * (rm ? 0.6 : 1), vz: (rand() - 0.5) * 0.4, age: 0, life: 1.2 + rand() * 1.4, s: 0.022 + rand() * 0.022, ph: rand() * 9 });
      }
      embers.begin();
      for (let i = E.length - 1; i >= 0; i--) {
        const e = E[i]; e.age += dt; const k = e.age / e.life;
        if (k >= 1) { E.splice(i, 1); continue; }
        if (!rm) { e.vx += Math.sin(t * 3 + e.ph) * 0.6 * dt; e.vz += Math.cos(t * 2.6 + e.ph) * 0.6 * dt; }
        e.vy *= 1 - 0.35 * dt; e.x += e.vx * dt; e.y += e.vy * dt; e.z += e.vz * dt;
        const tw = rm ? 1 : 0.75 + 0.25 * Math.sin(t * 17 + e.ph);
        embers.push(e, e.s, 0, 0, 3, 1.5, 0.45, (1 - k) * tw);
      }
      embers.end();
      if (light) {
        const f = rm ? 0.95 + 0.04 * Math.sin(t * 1.7)
          : 0.8 + 0.09 * Math.sin(t * 10.7 + Math.sin(t * 3.1) * 2) + 0.07 * Math.sin(t * 23.3) + 0.06 * (vnoise(t * 9, 3.7) - 0.5) * 2;
        // Intensity only: toggling visible would change the scene's light count and recompile every material.
        light.intensity = shown > 0.01 ? 60 * lightMul * scale * scale * shown * f : 0;
        if (!rm) light.position.set(Math.sin(t * 7.3) * 0.03 * scale, 0.45 * scale + Math.sin(t * 5.1) * 0.04 * scale, Math.cos(t * 6.7) * 0.03 * scale);
      }
    }
    update(0);
    return { group, light, setLevel(v) { level = clamp01(v); }, get level() { return level; }, update, dispose: disposer(group, [flames, embers, glow], light) };
  }

  // ---- smoke: buoyant puffs that rise, hit the ceiling and spread into a layer that thickens downwards ----
  // opts: x,y,z (source), ceiling (local height of the ceiling above the source), spread (m), radius, color, count, density, bounds {x0,x1,z0,z1} (local)
  function smoke(opts = {}) {
    const group = makeGroup('fx-smoke', opts);
    const max = Math.round((opts.count ?? 72) * Q);
    const sys = billboards(max, smokeMat());
    sys.mesh.renderOrder = 4;
    group.add(sys.mesh);
    const ceiling = opts.ceiling ?? 2.4, spread = opts.spread ?? 4, radius = opts.radius ?? 0.25, b = opts.bounds;
    const col = new THREE.Color(opts.color ?? 0x34302d);
    const P = [];
    let density = clamp01(opts.density ?? 0.5), shown = 0, acc = 0;
    function update(dt, camera) {
      dt = Math.min(dt || 0, 0.05);
      const rm = opts.reduceMotion ?? RM, sp = rm ? 0.6 : 1;
      shown += (density - shown) * Math.min(1, dt * 0.8);
      const layerLow = ceiling - (0.25 + 1.4 * shown);
      acc += shown * max / 8.5 * (rm ? 0.6 : 1) * dt;
      while (acc >= 1) {
        acc--;
        if (P.length >= max) break;
        const a = rand() * Math.PI * 2, d = Math.sqrt(rand()) * radius;
        P.push({ x: Math.cos(a) * d, y: 0.15, z: Math.sin(a) * d, vx: (rand() - 0.5) * 0.12, vy: 0.3, vz: (rand() - 0.5) * 0.12, age: 0,
          life: (6 + rand() * 4) / sp, s0: 0.35 + rand() * 0.3, grow: 0.16 + rand() * 0.12, rot: rand() * 6.3, spin: rm ? 0 : (rand() - 0.5) * 0.3,
          frame: (rand() * 4) | 0, shade: 0.8 + rand() * 0.3, layer: layerLow + rand() * (ceiling - layerLow), rise: (0.55 + rand() * 0.35) * sp });
      }
      for (let i = P.length - 1; i >= 0; i--) {
        const p = P[i]; p.age += dt;
        if (p.age >= p.life) { P.splice(i, 1); continue; }
        const size = Math.min(p.s0 + p.age * p.grow * sp, 2.4);
        if (p.y < ceiling - 0.35) { // plume: rise and widen
          p.vy += (p.rise - p.vy) * dt * 1.5;
          p.vx += (rand() - 0.5) * 0.25 * dt * sp; p.vz += (rand() - 0.5) * 0.25 * dt * sp;
        } else { // ceiling jet: stop rising, run outwards, sink slowly into the layer
          p.vy *= Math.exp(-3 * dt);
          const r = Math.hypot(p.x, p.z) || 1e-3, push = 0.45 * sp * Math.max(0, 1 - r / spread);
          p.vx += (p.x / r * push - p.vx) * dt; p.vz += (p.z / r * push - p.vz) * dt;
          p.y += (p.layer - p.y) * dt * 0.25;
        }
        p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt;
        p.y = Math.min(p.y, ceiling - size * 0.22);
        if (b) { p.x = Math.min(b.x1, Math.max(b.x0, p.x)); p.z = Math.min(b.z1, Math.max(b.z0, p.z)); }
        p.rot += p.spin * dt; p.size = size;
      }
      sys.begin();
      for (const p of sortFor(P, group, camera)) {
        const k = p.age / p.life, a = smooth(0, 0.12, k) * smooth(1, 0.55, k) * (0.3 + 0.4 * shown);
        sys.push(p, p.size, p.rot, p.frame, col.r * p.shade, col.g * p.shade, col.b * p.shade, a);
      }
      sys.end();
    }
    update(0);
    return { group, setDensity(v) { density = clamp01(v); }, get density() { return density; }, update, dispose: disposer(group, [sys]) };
  }

  // ---- rain: world-anchored streaks wrapped around the camera + splash rings on the ground (all GPU) ----
  const FOG_V = '#include <fog_pars_vertex>\n', FOG_F = '#include <fog_pars_fragment>\n';
  const OUT_F = '#include <tonemapping_fragment>\n#include <colorspace_fragment>\n#include <fog_fragment>\n';
  function rain(scene, camera, opts = {}) {
    const group = new THREE.Group(); group.name = 'fx-rain'; group.visible = false;
    const n = Math.round((opts.count ?? 9000) * Q), S = 36, H = 22;
    const seeds = new Float32Array(n * 8), zeros = new Float32Array(n * 6);
    for (let i = 0; i < n; i++) { const a = [rand(), rand(), rand()]; seeds.set([...a, 0, ...a, 1], i * 8); }
    const lg = new THREE.BufferGeometry();
    lg.setAttribute('position', new THREE.BufferAttribute(zeros, 3));
    lg.setAttribute('aSeed', new THREE.BufferAttribute(seeds, 4));
    const uniforms = THREE.UniformsUtils.merge([THREE.UniformsLib.fog, {
      uTime: { value: 0 }, uCam: { value: new THREE.Vector3() }, uWind: { value: new THREE.Vector2(1.2, 0.5) }, uSpeed: { value: 9 },
      uLen: { value: 0.045 }, uColor: { value: new THREE.Color(0.7, 0.74, 0.8) }, uOpacity: { value: 0 }, uGround: { value: opts.groundY ?? 0 },
      uScale: { value: 900 } }]);
    const lines = new THREE.LineSegments(lg, new THREE.ShaderMaterial({
      uniforms, transparent: true, depthWrite: false, fog: true,
      vertexShader: `attribute vec4 aSeed; uniform float uTime, uSpeed, uLen, uGround; uniform vec3 uCam; uniform vec2 uWind; varying float vA;\n${FOG_V}
        void main() {
          float spd = uSpeed * (0.85 + 0.3 * fract(aSeed.x * 91.7));
          float y = uCam.y - ${H * 0.35} + mod(aSeed.y * ${H}.0 - uTime * spd - uCam.y + ${H * 0.35}, ${H}.0);
          vec2 xz = aSeed.xz * ${S}.0 + uWind * uTime * (spd / uSpeed);
          xz = uCam.xz + mod(xz - uCam.xz + ${S / 2}.0, ${S}.0) - ${S / 2}.0;
          vec3 p = vec3(xz.x, y, xz.y) - vec3(uWind.x, -spd, uWind.y) * uLen * aSeed.w;
          vA = (1.0 - aSeed.w * 0.85) * step(uGround, y) * (1.0 - smoothstep(${S * 0.3}, ${S / 2}.0, length(xz - uCam.xz)));
          vec4 mvPosition = modelViewMatrix * vec4(p, 1.0);
          gl_Position = projectionMatrix * mvPosition;
          #include <fog_vertex>
        }`,
      fragmentShader: `uniform vec3 uColor; uniform float uOpacity; varying float vA;\n${FOG_F}
        void main() { gl_FragColor = vec4(uColor, vA * uOpacity * 0.42);\n${OUT_F} }`,
    }));
    const m = Math.round(700 * Q), R = 16, sp = new Float32Array(m * 4);
    for (let i = 0; i < m; i++) sp.set([rand(), rand(), rand(), rand()], i * 4);
    const pg = new THREE.BufferGeometry();
    pg.setAttribute('position', new THREE.BufferAttribute(new Float32Array(m * 3), 3));
    pg.setAttribute('aSeed', new THREE.BufferAttribute(sp, 4));
    const splashes = new THREE.Points(pg, new THREE.ShaderMaterial({
      uniforms, transparent: true, depthWrite: false, fog: true,
      vertexShader: `attribute vec4 aSeed; uniform float uTime, uGround, uScale; uniform vec3 uCam; varying float vA, vPh;\n${FOG_V}
        void main() {
          float cyc = uTime * (2.2 + aSeed.w) + aSeed.z * 7.0, k = floor(cyc); vPh = fract(cyc);
          vec2 h = fract(sin(vec2(k * 12.9898 + aSeed.x * 78.233, k * 39.3468 + aSeed.y * 11.135)) * 43758.5453);
          vec2 xz = (aSeed.xy + h) * ${R * 2}.0;
          xz = uCam.xz + mod(xz - uCam.xz + ${R}.0, ${R * 2}.0) - ${R}.0;
          vec4 mvPosition = modelViewMatrix * vec4(xz.x, uGround + 0.015, xz.y, 1.0);
          gl_PointSize = (0.05 + vPh * 0.16) * uScale / max(0.5, -mvPosition.z);
          vA = (1.0 - vPh) * (1.0 - smoothstep(${R * 0.6}, ${R}.0, length(xz - uCam.xz))) * step(uGround - 3.0, uCam.y) * step(uCam.y, uGround + 40.0);
          gl_Position = projectionMatrix * mvPosition;
          #include <fog_vertex>
        }`,
      fragmentShader: `uniform vec3 uColor; uniform float uOpacity; varying float vA, vPh;\n${FOG_F}
        void main() {
          vec2 q = gl_PointCoord * 2.0 - 1.0; q.y *= 2.2; float d = length(q);
          float ring = smoothstep(0.55, 0.85, d) * (1.0 - smoothstep(0.85, 1.0, d)) + (1.0 - smoothstep(0.0, 0.3, d)) * step(vPh, 0.3);
          if (ring < 0.02) discard;
          gl_FragColor = vec4(uColor * 1.2, ring * vA * uOpacity * 0.5);\n${OUT_F} }`,
    }));
    lines.frustumCulled = splashes.frustumCulled = false;
    group.add(lines, splashes);
    scene?.add(group);
    let on = false, shown = 0;
    function update(dt, cam = camera) {
      dt = Math.min(dt || 0, 0.05);
      const rm = opts.reduceMotion ?? RM;
      shown += ((on ? 1 : 0) - shown) * Math.min(1, dt * 0.8);
      group.visible = shown > 0.01;
      if (!group.visible) return;
      const u = uniforms;
      u.uTime.value += dt * (rm ? 0.65 : 1);
      u.uOpacity.value = shown;
      if (cam) {
        cam.getWorldPosition(u.uCam.value);
        if (cam.isPerspectiveCamera && typeof innerHeight === 'number') u.uScale.value = innerHeight * Math.min(devicePixelRatio || 1, 2) / (2 * Math.tan(cam.fov * Math.PI / 360));
      }
      const fc = scene?.fog?.color;
      if (fc) u.uColor.value.setRGB(fc.r * 0.85 + 0.12, fc.g * 0.85 + 0.13, fc.b * 0.85 + 0.15);
      lg.setDrawRange(0, (rm ? Math.floor(n / 2) : n) * 2);
      pg.setDrawRange(0, rm ? Math.floor(m / 2) : m);
    }
    return {
      group, setOn(v) { on = !!v; }, get on() { return on; }, update,
      dispose() { group.removeFromParent(); lg.dispose(); pg.dispose(); lines.material.dispose(); splashes.material.dispose(); },
    };
  }

  // ---- dust: slow motes drifting in a box (basements, light shafts). opts: box {x0,x1,y0,y1,z0,z1}, count, color, opacity, size ----
  function dust(opts = {}) {
    const bx = { x0: -4, x1: 4, y0: 0.2, y1: 2.6, z0: -4, z1: 4, ...(opts.box || {}) };
    const n = Math.round((opts.count ?? 260) * Q), pos = new Float32Array(n * 3), seed = new Float32Array(n * 4);
    for (let i = 0; i < n; i++) {
      pos.set([bx.x0 + rand() * (bx.x1 - bx.x0), bx.y0 + rand() * (bx.y1 - bx.y0), bx.z0 + rand() * (bx.z1 - bx.z0)], i * 3);
      seed.set([rand(), rand(), rand(), rand()], i * 4);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.setAttribute('aSeed', new THREE.BufferAttribute(seed, 4));
    const uniforms = { uTime: { value: rand() * 100 }, uScale: { value: 900 }, uSize: { value: opts.size ?? 0.012 }, uOpacity: { value: opts.opacity ?? 0.35 },
      uColor: { value: new THREE.Color(opts.color ?? 0xfff1d6) }, uY: { value: new THREE.Vector2(bx.y0, bx.y1 - bx.y0) } };
    const points = new THREE.Points(g, new THREE.ShaderMaterial({
      uniforms, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
      vertexShader: `attribute vec4 aSeed; uniform float uTime, uScale, uSize; uniform vec2 uY; varying float vA;
        void main() {
          vec3 p = position + vec3(sin(uTime * (0.05 + aSeed.x * 0.08) + aSeed.w * 6.28), 0.0, cos(uTime * (0.04 + aSeed.y * 0.07) + aSeed.z * 6.28)) * 0.35;
          p.y = uY.x + mod(position.y - uY.x + uTime * (0.01 + aSeed.z * 0.025), uY.y);
          vec4 mvPosition = modelViewMatrix * vec4(p, 1.0);
          gl_PointSize = clamp(uSize * (0.6 + aSeed.x) * uScale / max(0.2, -mvPosition.z), 1.0, 5.0);
          vA = (0.55 + 0.45 * sin(uTime * (0.3 + aSeed.y * 0.5) + aSeed.x * 20.0)) * smoothstep(0.0, 0.08, (p.y - uY.x) / uY.y) * (1.0 - smoothstep(0.85, 1.0, (p.y - uY.x) / uY.y));
          gl_Position = projectionMatrix * mvPosition;
        }`,
      fragmentShader: `uniform vec3 uColor; uniform float uOpacity; varying float vA;
        void main() { float d = length(gl_PointCoord - 0.5) * 2.0; gl_FragColor = vec4(uColor, (1.0 - smoothstep(0.2, 1.0, d)) * vA * uOpacity);\n#include <colorspace_fragment>\n}`,
    }));
    points.frustumCulled = false;
    const group = makeGroup('fx-dust', opts); group.add(points);
    let on = true, density = 1;
    return {
      group,
      setOn(v) { on = !!v; group.visible = on; },
      setDensity(v) { density = clamp01(v); g.setDrawRange(0, Math.round(n * density)); },
      update(dt, camera) {
        if (!on) return;
        uniforms.uTime.value += Math.min(dt || 0, 0.05) * ((opts.reduceMotion ?? RM) ? 0.3 : 1);
        if (camera?.isPerspectiveCamera && typeof innerHeight === 'number') uniforms.uScale.value = innerHeight * Math.min(devicePixelRatio || 1, 2) / (2 * Math.tan(camera.fov * Math.PI / 360));
      },
      dispose() { group.removeFromParent(); g.dispose(); points.material.dispose(); },
    };
  }

  // ---- extinguisher spray: dry-chemical powder jet from the group's origin (the nozzle) toward aim(). ----
  // Particles live in world space, so the group can be parented to a hand. opts: count, floorY, speed
  function extinguisherSpray(opts = {}) {
    const group = new THREE.Group(); group.name = 'fx-spray';
    const max = Math.round((opts.count ?? 170) * Q);
    const sys = billboards(max, smokeMat());
    sys.mesh.matrixAutoUpdate = false; sys.mesh.matrixWorldAutoUpdate = false; // identity world matrix: world-space particles
    sys.mesh.renderOrder = 5;
    group.add(sys.mesh);
    const target = new THREE.Vector3(), dir = new THREE.Vector3(), origin = new THREE.Vector3(), up = new THREE.Vector3(0, 1, 0), side = new THREE.Vector3();
    const P = [], floorY = opts.floorY ?? -Infinity;
    let on = false, aimed = false, acc = 0;
    const world = { worldToLocal: (v) => v }; // sortFor in world space
    function update(dt, camera) {
      dt = Math.min(dt || 0, 0.05);
      const rm = opts.reduceMotion ?? RM, sp = rm ? 0.7 : 1;
      group.getWorldPosition(origin);
      if (!aimed) target.copy(group.localToWorld(tmpW.set(0, 0, -3)));
      dir.subVectors(target, origin).normalize();
      side.crossVectors(dir, up).normalize(); if (!side.lengthSq()) side.set(1, 0, 0);
      const up2 = tmpV.crossVectors(side, dir);
      if (on) acc += (rm ? 70 : 140) * Q * dt;
      while (acc >= 1) {
        acc--;
        if (P.length >= max) { acc = 0; break; }
        const a = rand() * Math.PI * 2, c = Math.sqrt(rand()) * 0.13, v = (opts.speed ?? 8) * (0.75 + rand() * 0.35) * sp;
        const vx = (dir.x + (side.x * Math.cos(a) + up2.x * Math.sin(a)) * c) * v, vy = (dir.y + (side.y * Math.cos(a) + up2.y * Math.sin(a)) * c) * v, vz = (dir.z + (side.z * Math.cos(a) + up2.z * Math.sin(a)) * c) * v;
        P.push({ x: origin.x, y: origin.y, z: origin.z, vx, vy, vz, age: 0, life: 1.3 + rand() * 0.9, rot: rand() * 6.3, spin: rm ? 0 : (rand() - 0.5) * 1.2, frame: (rand() * 4) | 0, shade: 0.9 + rand() * 0.1 });
      }
      for (let i = P.length - 1; i >= 0; i--) {
        const p = P[i]; p.age += dt;
        if (p.age >= p.life) { P.splice(i, 1); continue; }
        const drag = Math.exp(-dt * 2.8);
        p.vx *= drag; p.vz *= drag; p.vy = p.vy * drag - 0.6 * dt;
        p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt;
        p.size = Math.min(0.05 + p.age * 1.15, 1.5);
        if (p.y < floorY + p.size * 0.3) { p.y = floorY + p.size * 0.3; p.vy = 0; }
        p.rot += p.spin * dt;
      }
      sys.begin();
      for (const p of sortFor(P, world, camera)) {
        const k = p.age / p.life, a = smooth(0, 0.04, p.age) * Math.pow(1 - k, 1.5) * 0.6;
        sys.push(p, p.size, p.rot, p.frame, 0.96 * p.shade, 0.96 * p.shade, 0.93 * p.shade, a);
      }
      sys.end();
    }
    return {
      group, update,
      setOn(v) { on = !!v; }, get on() { return on; },
      aim(v) { target.copy(v); aimed = true; },
      dispose: disposer(group, [sys]),
    };
  }

  return {
    fire, smoke, rain, dust, extinguisherSpray,
    get reduceMotion() { return RM; },
    setReduceMotion(v) { RM = !!v; },
  };
}
