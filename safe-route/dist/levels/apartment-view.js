// What the windows of 103동 look out on, driven only by setView('day'|'dusk'|'night'|'blackout') so the view matches
// the story time whatever interior preset is active: the parking lot and planting in front of 103 with parked cars,
// autumn trees and street lamps (light pools at night), the corridor faces of 102·104·105동 (corridor lamps and
// kitchen windows lit at night, dark with green exit dots and a few torches in a blackout), and a far ring of city
// and hills with stars and the moon. Everything is unlit (MeshBasic, no fog) and tinted per view; nothing is merged.
import * as THREE from 'three';

const hash = (x, y) => { const s = Math.sin(x * 12.9898 + y * 78.233) * 43758.5453; return s - Math.floor(s); };
const VIEWS = {
  day: { tint: 0.95, far: 1, fac: 1, lamps: 0, cap: '#4f86cf', sky: ['#4f86cf', '#9cc2e6', '#dde8ee'], hill: ['#9aabb9', '#b2bec7'], bFar: '#c3c9cd', bNear: '#adb3b6', ground: '#8d9188',
    win: 0, dayWin: '#7f8f9a', stars: 0, moon: 0, glow: 0, beacon: 0,
    wall: '#e4dfd4', recess: '#9b978f', parapet: '#e9e4d9', rail: '#7d7a73', core: '#d9d4c9', door: '#6a5d52', winC: '#73828d', kitchen: 0, corridor: 0, stair: 0, exit: 0, dots: 0 },
  dusk: { tint: 0.5, far: 1.1, fac: 1.15, lamps: 1, cap: '#1f2550', sky: ['#1f2550', '#7a5878', '#f0955a'], hill: ['#3b3150', '#4e3d58'], bFar: '#55485e', bNear: '#3f3648', ground: '#2c2630',
    win: 0.35, lit: '#ffc98a', lit2: '#dbe6ff', stars: 0.04, moon: 0, glow: 0.12, beacon: 1,
    wall: '#5a5258', recess: '#2e2a30', parapet: '#605860', rail: '#2a272b', core: '#544d54', door: '#241f22', winC: '#2a2e38', litWin: '#ffcf96', kitchen: 0.4, corridor: 0.9, lamp: '#fff3d6', stair: 0.3, exit: 0, dots: 0 },
  night: { tint: 0.15, far: 1.45, fac: 1.6, lamps: 1, cap: '#03050c', sky: ['#03050c', '#0b1020', '#2a2430'], hill: ['#07090f', '#0b0d14'], bFar: '#10131c', bNear: '#0b0d14', ground: '#07080b',
    win: 0.6, lit: '#ffd9a0', lit2: '#dbe8ff', stars: 0.15, moon: 1, glow: 0.22, beacon: 1,
    wall: '#15161b', recess: '#0b0b0e', parapet: '#1c1c20', rail: '#0a0a0c', core: '#131418', door: '#0a090a', winC: '#0c0e12', litWin: '#ffd39a', kitchen: 0.55, corridor: 1, lamp: '#fff6e2', stair: 0.25, exit: 0, dots: 0 },
  blackout: { tint: 0.07, far: 1.3, fac: 1.4, lamps: 0, cap: '#010208', sky: ['#010208', '#050a18', '#0b1324'], hill: ['#04060c', '#060912'], bFar: '#080b14', bNear: '#05070d', ground: '#030406',
    win: 0.012, lit: '#ff9a4a', lit2: '#e8eeff', stars: 1, moon: 1, glow: 0, beacon: 0,
    wall: '#0c0f16', recess: '#06070a', parapet: '#10131b', rail: '#050609', core: '#0b0d13', door: '#050506', winC: '#07080b', litWin: '#ff9a4a', kitchen: 0.02, corridor: 0, stair: 0, exit: 1, dots: 0.05 },
};

export function buildView(B) {
  const { kit } = B, r = kit.rng(1033), group = new THREE.Group(); group.name = 'apt-view';
  const tinted = []; // { mat, base: Color }
  const basic = (opts, base = 0xffffff) => { const m = new THREE.MeshBasicMaterial({ fog: false, ...opts }); tinted.push({ mat: m, base: new THREE.Color(base) }); return m; };
  const L = new THREE.Vector3(0.3, 0.85, 0.45).normalize();
  const shade = (geo) => { // bake simple sun shading into vertex colours (MeshBasic has no lighting)
    const n = geo.attributes.normal, c = new Float32Array(n.count * 3);
    for (let i = 0; i < n.count; i++) { const k = 0.5 + 0.5 * Math.max(0, n.getX(i) * L.x + n.getY(i) * L.y + n.getZ(i) * L.z); c.fill(k, i * 3, i * 3 + 3); }
    geo.setAttribute('color', new THREE.BufferAttribute(c, 3)); return geo;
  };
  const add = (m, cast = false) => { m.castShadow = cast; m.receiveShadow = false; group.add(m); return m; };

  // ---- far ring (city + hills + sky), sky cap ----
  const far = kit.canvas(2048, 512), farTex = kit.texture(far, { repeat: [1, 1] }); farTex.wrapT = THREE.ClampToEdgeWrapping;
  const farMat = basic({ map: farTex, side: THREE.BackSide });
  const ring = add(new THREE.Mesh(new THREE.CylinderGeometry(150, 150, 120, 72, 1, true, Math.PI), farMat)); ring.position.set(2, 58, 2);
  const capMat = new THREE.MeshBasicMaterial({ fog: false, side: THREE.DoubleSide });
  const cap = add(new THREE.Mesh(new THREE.CircleGeometry(151, 48), capMat)); cap.rotation.x = Math.PI / 2; cap.position.set(2, 117.9, 2);
  const layout = {
    stars: Array.from({ length: 900 }, () => ({ x: r() * 2048, y: r() * 380, s: r() < 0.08 ? 2 : 1, a: 0.3 + r() * 0.7 })),
    ridges: [0, 1].map(k => Array.from({ length: 257 }, (_, i) => (k ? 34 : 62) + Math.sin(i * 0.05 + k * 2) * (k ? 14 : 26) + Math.sin(i * 0.17 + k) * 9 + Math.sin(i * 0.41) * 4)),
    bldgs: [], crosses: [],
  };
  for (let i = 0; i < 260; i++) {
    const tall = r() < 0.12, farL = r() < 0.5;
    layout.bldgs.push({ x: r() * 2048, w: 8 + r() * (tall ? 18 : 34), h: (tall ? 70 + r() * 60 : 10 + r() * 46) * (farL ? 0.7 : 1), far: farL });
  }
  layout.bldgs.sort((a, b) => b.far - a.far);
  for (let i = 0; i < 4; i++) layout.crosses.push(layout.bldgs.find((b, k) => !b.far && b.h < 40 && k > i * 40));

  // ---- the opposite blocks: corridor faces with a per-view facade canvas; plain tinted end walls and roofs ----
  const fac = kit.canvas(1024, 720), facTex = kit.texture(fac, { repeat: [1, 1] });
  const facMat = basic({ map: facTex }), sideMat = basic({}, 0xd8d3c8), roofMat = basic({}, 0x8a8780);
  const block = (x0, x1, z0, z1) => { const m = add(new THREE.Mesh(new THREE.BoxGeometry(x1 - x0, 44.8, z1 - z0), [sideMat, sideMat, roofMat, roofMat, facMat, facMat])); m.position.set((x0 + x1) / 2, 22.4 - 0.05, (z0 + z1) / 2); return m; };
  block(6, 62, 50, 62); block(-74, -22, 58, 70); block(-30, 26, -62, -50);

  // ---- ground: grass base, planting strips, sidewalks, the lot (with bay lines), inner roads ----
  const surf = (name) => kit.surface(name).map;
  const grass = basic({ map: surf('grass') }, 0xc8d4b8), walk = basic({ map: surf('sidewalk') }), asph = basic({ map: surf('asphalt') });
  const plane = (w, d, m, x, z, y) => add(kit.plane(w, d, m, { x, y, z }));
  plane(320, 320, grass, 2, 2, -0.06);
  plane(90, 2.0, walk, 0, 13.5, -0.04); plane(90, 2.0, walk, 0, 33, -0.04); plane(90, 2, walk, 0, 45, -0.04);
  plane(70, 17.5, asph, -2, 23.25, -0.035); plane(110, 7, asph, 0, 40.5, -0.035); plane(80, 6.5, asph, 0, -16.8, -0.035);
  const lines = kit.canvas(1024, 256, (c, w, h) => { c.clearRect(0, 0, w, h); c.fillStyle = 'rgba(245,245,240,0.9)';
    for (let x = 0; x <= w; x += w / 28) { c.fillRect(x, 6, 3, 66); c.fillRect(x, h - 72, 3, 66); }
    c.fillRect(0, 70, w, 3); c.fillRect(0, h - 73, w, 3); c.fillStyle = 'rgba(242,194,48,0.9)'; c.fillRect(0, h / 2 - 2, w, 4); });
  const lineMat = basic({ map: kit.texture(lines, { repeat: [1 / 70, 1 / 17.5] }), transparent: true, depthWrite: false }); // kit.plane UVs are in metres
  plane(70, 17.5, lineMat, -2, 23.25, -0.02);

  // ---- trees (autumn mix), hedge, parked cars, street lamps with light pools ----
  const spots = [];
  for (let x = -42; x < 50; x += 7) spots.push([x + (r() - 0.5) * 2, 35.6 + (r() - 0.5)]);
  for (let x = -38; x < 40; x += 9) spots.push([x + (r() - 0.5) * 2, 11.2]);
  for (let x = -30; x < 30; x += 8) spots.push([x + r() * 2, -23 + (r() - 0.5) * 2]);
  const crowns = spots.map(([x, z]) => { const s = 2.2 + r() * 1.2; return { x, y: 2.6 + s * 0.9, z, sx: s, sy: s * 1.15, sz: s, ry: r() * 6 }; });
  const crownMesh = add(kit.instanced(shade(new THREE.IcosahedronGeometry(1, 1)), basic({ vertexColors: true }), crowns, { cast: false, receive: false }));
  crowns.forEach((_, i) => crownMesh.setColorAt(i, new THREE.Color(['#5a7a3a', '#4f6e34', '#c9a23a', '#c46a2a', '#a8442e', '#6b8a40'][(r() * 6) | 0])));
  add(kit.instanced(shade(new THREE.CylinderGeometry(0.16, 0.22, 3, 6).translate(0, 1.5, 0)), basic({ vertexColors: true }, 0x4b3a2c), spots.map(([x, z]) => ({ x, z })), { cast: false, receive: false }));
  const hedge = [];
  for (let x = -40; x < 40; x += 1.6) if (x < -8 || x > 1) hedge.push({ x, y: 0.3, z: 10.2, sx: 1.0, sy: 0.55, sz: 0.8 });
  add(kit.instanced(shade(new THREE.IcosahedronGeometry(1, 0)), basic({ vertexColors: true }, 0x3f5a2c), hedge, { cast: false, receive: false }));
  const cars = [];
  for (const zc of [17.6, 28.9]) for (let x = -35.5; x < 31; x += 2.5) if (r() < 0.68) cars.push({ x, y: 0.28, z: zc + (r() - 0.5) * 0.3, ry: (r() - 0.5) * 0.04 });
  const body = add(kit.instanced(shade(new THREE.BoxGeometry(1.84, 0.78, 4.6).translate(0, 0.39, 0)), basic({ vertexColors: true }), cars, { cast: false, receive: false }));
  cars.forEach((_, i) => { const k = r(); body.setColorAt(i, new THREE.Color(k < 0.36 ? '#ecebe7' : k < 0.56 ? '#1c1d20' : k < 0.76 ? '#b8bcc0' : k < 0.9 ? '#6e7378' : k < 0.95 ? '#26324a' : '#8a2a2a')); });
  add(kit.instanced(shade(new THREE.BoxGeometry(1.6, 0.56, 2.5).translate(0, 1.06, -0.2)), basic({ vertexColors: true }, 0x2a3038), cars, { cast: false, receive: false }));
  const posts = [];
  for (const z of [14.7, 32.2]) for (let x = -34; x < 34; x += 13) posts.push({ x, z });
  add(kit.instanced(shade(new THREE.CylinderGeometry(0.07, 0.09, 6.2, 8).translate(0, 3.1, 0)), basic({ vertexColors: true }, 0x5d6368), posts, { cast: false, receive: false }));
  const headMat = new THREE.MeshBasicMaterial({ fog: false });
  add(kit.instanced(new THREE.BoxGeometry(0.55, 0.12, 0.28), headMat, posts.map(p => ({ x: p.x, y: 6.15, z: p.z })), { cast: false, receive: false }));
  const poolTex = kit.texture(kit.canvas(128, 128, (c, w, h) => { const gr = c.createRadialGradient(w / 2, h / 2, 0, w / 2, h / 2, w / 2); gr.addColorStop(0, 'rgba(255,214,150,0.55)'); gr.addColorStop(1, 'rgba(255,190,120,0)'); c.fillStyle = gr; c.fillRect(0, 0, w, h); }), { repeat: [1, 1] });
  const poolMat = new THREE.MeshBasicMaterial({ map: poolTex, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, fog: false });
  const pools = add(kit.instanced(new THREE.PlaneGeometry(12, 12).rotateX(-Math.PI / 2), poolMat, posts.map(p => ({ x: p.x, y: 0.02, z: p.z })), { cast: false, receive: false }));

  let current = null;
  function setView(name) {
    const V = VIEWS[name];
    if (!V) { console.error(`[apartment] unknown windowView "${name}"; use ${Object.keys(VIEWS).join(' | ')}`); return; }
    if (current === name) return;
    current = name;
    drawFar(far.getContext('2d'), far.width, far.height, V, layout); farTex.needsUpdate = true;
    drawFacade(fac.getContext('2d'), fac.width, fac.height, V); facTex.needsUpdate = true;
    for (const t of tinted) t.mat.color.copy(t.base).multiplyScalar(V.tint);
    farMat.color.setScalar(V.far); facMat.color.setScalar(V.fac); capMat.color.set(V.cap);
    headMat.color.set(V.lamps ? new THREE.Color(5, 4.2, 3) : new THREE.Color(0xb9bcc0).multiplyScalar(V.tint));
    pools.visible = !!V.lamps;
  }
  setView('day');
  return { group, setView, views: Object.keys(VIEWS) };
}

function drawFar(c, w, h, V, L) {
  const H0 = 470;
  const gr = c.createLinearGradient(0, 0, 0, H0); gr.addColorStop(0, V.sky[0]); gr.addColorStop(0.62, V.sky[1]); gr.addColorStop(1, V.sky[2]);
  c.fillStyle = gr; c.fillRect(0, 0, w, h);
  const n = Math.round(L.stars.length * V.stars);
  for (let i = 0; i < n; i++) { const s = L.stars[i]; c.fillStyle = `rgba(235,240,255,${s.a})`; c.fillRect(s.x, s.y, s.s, s.s); }
  if (V.moon) {
    const mx = 1150, my = 300, g = c.createRadialGradient(mx, my, 4, mx, my, 60); g.addColorStop(0, 'rgba(220,230,255,0.35)'); g.addColorStop(1, 'rgba(220,230,255,0)');
    c.fillStyle = g; c.fillRect(mx - 60, my - 60, 120, 120); c.fillStyle = '#f2f0e6'; c.beginPath(); c.arc(mx, my, 9, 0, 7); c.fill();
  }
  L.ridges.forEach((ridge, i) => { c.fillStyle = V.hill[i]; c.beginPath(); c.moveTo(0, H0); ridge.forEach((y, x) => c.lineTo(x * 8, H0 - y)); c.lineTo(w, H0); c.closePath(); c.fill(); });
  if (V.glow) { const g = c.createLinearGradient(0, H0 - 90, 0, H0); g.addColorStop(0, 'rgba(255,150,80,0)'); g.addColorStop(1, `rgba(255,150,80,${V.glow})`); c.fillStyle = g; c.fillRect(0, H0 - 90, w, 90); }
  for (const b of L.bldgs) {
    const top = H0 - b.h;
    c.fillStyle = b.far ? V.bFar : V.bNear; c.fillRect(b.x, top, b.w, b.h + 4);
    for (let y = top + 4; y < H0 - 3; y += 6) for (let x = b.x + 2; x < b.x + b.w - 2; x += 4) {
      const k = hash(x, y);
      if (k < V.win) { c.fillStyle = k < V.win * 0.2 ? V.lit2 : V.lit; c.fillRect(x, y, 2, 3); }
      else if (V.dayWin && k > 0.4) { c.fillStyle = V.dayWin; c.fillRect(x, y, 2, 3); }
    }
    if (V.beacon && b.h > 80) { c.fillStyle = '#ff3a2a'; c.fillRect(b.x + b.w / 2 - 1.5, top - 3, 3, 3); }
  }
  if (V.beacon) for (const b of L.crosses) if (b) { // red neon crosses on low buildings
    const x = b.x + b.w / 2, y = H0 - b.h - 16; c.fillStyle = 'rgba(255,40,40,0.25)'; c.fillRect(x - 7, y - 4, 14, 22); c.fillStyle = '#ff4a3a'; c.fillRect(x - 1, y, 2, 14); c.fillRect(x - 4, y + 4, 8, 2);
  }
  c.fillStyle = V.ground; c.fillRect(0, H0, w, h - H0);
}

// Corridor face of a 15-floor 복도식 block (64 m x 44.8 m at 16 px/m): parapets, doors and kitchen windows, two stair cores.
function drawFacade(c, w, h, V) {
  const fh = 45, cores = [256, 768];
  c.fillStyle = V.wall; c.fillRect(0, 0, w, h);
  c.fillStyle = V.parapet; c.fillRect(0, fh * 0.55, w, fh * 0.45);
  for (let f = 0; f < 15; f++) {
    const y = h - (f + 1) * fh;
    c.fillStyle = V.recess; c.fillRect(0, y, w, fh * 0.62);
    for (let x = 14; x < w - 40; x += 64) {
      if (cores.some(cx => Math.abs(cx - (x + 22)) < 46)) continue;
      c.fillStyle = V.door; c.fillRect(x, y + 5, 12, fh * 0.57);
      c.fillStyle = hash(x, f) < V.kitchen ? V.litWin : V.winC; c.fillRect(x + 22, y + 9, 24, 10);
      if (V.dots && hash(x + 5, f + 3) < V.dots) { c.fillStyle = hash(x, f + 11) < 0.5 ? '#ffb060' : '#f4f6ff'; c.fillRect(x + 30, y + 12, 5, 4); }
    }
    if (V.corridor) for (let x = 60; x < w; x += 128) if (hash(x * 3, f * 7) < V.corridor) {
      const g = c.createRadialGradient(x, y + 3, 1, x, y + 14, 34); g.addColorStop(0, 'rgba(255,244,220,0.55)'); g.addColorStop(1, 'rgba(255,244,220,0)');
      c.fillStyle = g; c.fillRect(x - 36, y, 72, fh); c.fillStyle = V.lamp; c.fillRect(x - 6, y + 1, 12, 3);
    }
    c.fillStyle = V.parapet; c.fillRect(0, y + fh * 0.62, w, fh * 0.38); c.fillStyle = V.rail; c.fillRect(0, y + fh * 0.62, w, 2);
    for (const cx of cores) {
      c.fillStyle = V.core; c.fillRect(cx - 24, y, 48, fh);
      c.fillStyle = hash(cx, f) < V.stair ? V.litWin : V.winC; c.fillRect(cx - 12, y + 10, 24, 18);
      if (V.exit) { c.fillStyle = '#2bff7a'; c.fillRect(cx + 15, y + 6, 6, 3); }
    }
  }
  c.fillStyle = V.recess; c.fillRect(0, h - fh, w, fh * 0.7); // ground-floor pilotis
  if (V.beacon) for (const cx of cores) { c.fillStyle = '#ff3a2a'; c.fillRect(cx - 2, 2, 5, 4); }
}
