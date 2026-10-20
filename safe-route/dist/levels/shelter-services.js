// Ceiling services and safety equipment of the basement car park: twin-tube fluorescent battens on two
// circuits (A stays on the emergency supply, B drops out), area lights, battery emergency lights, exit signs
// (피난구·통로 유도등), red sprinkler mains with heads, cable trays, a grey drain line, the west supply duct,
// two jet fans, hydrant cabinets (옥내소화전), extinguishers at pillars and CCTV domes.
import { ROOM, GRID, STAIR, ACCESS, RAMP, rod } from './shelter-shell.js';

export function buildServices(kit, T, shell) {
  const { THREE } = kit;
  const g = new THREE.Group(); g.name = 'shelter-services';
  const live = new THREE.Group(); live.name = 'shelter-services-live';   // animated, never merged
  const add = (m, parent = g) => { parent.add(m); return m; };
  const V = (x, y, z) => new THREE.Vector3(x, y, z);
  const T4 = (x, y, z, rx = 0, ry = 0, rz = 0, s = 1) => new THREE.Matrix4().compose(V(x, y, z), new THREE.Quaternion().setFromEuler(new THREE.Euler(rx, ry, rz)), V(s, s, s));
  function inst(geo, mat, mats, cast = false) {
    const im = new THREE.InstancedMesh(geo, mat, Math.max(1, mats.length));
    mats.forEach((m, i) => im.setMatrixAt(i, m)); im.count = mats.length;
    im.castShadow = cast; im.receiveShadow = false; im.instanceMatrix.needsUpdate = true; im.computeBoundingSphere();
    return add(im);
  }
  const low = kit.quality === 'low';
  const M = {
    housing: kit.mat('paint:#e7e7e2', { roughness: 0.4, metalness: 0.3 }), red: kit.mat('paint:#c4302b', { roughness: 0.45, metalness: 0.2 }),
    galv: kit.mat('metal', { color: 0xb9bdc0, roughness: 0.5 }), grey: kit.mat('paint:#9da2a6', { roughness: 0.6 }), dark: kit.mat('darkMetal'),
    cable: kit.mat('rubber'), brass: kit.mat('metal', { color: 0xc8a560, roughness: 0.35 }), white: kit.mat('plastic'),
  };
  const tube = (c) => new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: c, emissiveIntensity: 3.2, roughness: 0.3 });
  const tubes = { A: tube(0xeaf3ff), B: tube(0xeaf3ff), F: tube(0xe4f0ff) };
  for (const m of Object.values(tubes)) kit.lit('indoor', m, 3.2);

  // ---- fluorescent battens: grid along the aisles and over the bays, plus the shell's extra spots ----
  const spots = [];
  for (const z of [-13.2, -8, -2.6, 2.6, 8, 13.2]) for (let i = 0; i < 14; i++) spots.push({ x: -19.5 + 3 * i, y: 2.62, z, ry: 0 });
  for (const f of shell.fixtures) spots.push(f);
  const faulty = 33;
  const parts = { housing: [], A: [], B: [], F: [], rods: [], panels: [] };
  spots.forEach((f, i) => {
    const F = T4(f.x, f.y, f.z, f.rx || 0, f.ry || 0, 0);
    if (f.panel) { parts.panels.push(F); return; }
    parts.housing.push(F);
    const key = i === faulty ? 'F' : i % 2 ? 'B' : 'A';
    for (const dz of [-0.04, 0.04]) parts[key].push(F.clone().multiply(T4(0, -0.035, dz, 0, 0, Math.PI / 2)));
    for (const dx of [-0.45, 0.45]) parts.rods.push(F.clone().multiply(T4(dx, 0.2, 0)));
  });
  inst(new THREE.BoxGeometry(1.25, 0.05, 0.17), M.housing, parts.housing);
  const tubeGeo = new THREE.CylinderGeometry(0.014, 0.014, 1.18, 8);
  for (const k of ['A', 'B', 'F']) inst(tubeGeo, tubes[k], parts[k]);
  inst(new THREE.CylinderGeometry(0.006, 0.006, 0.36, 5), M.dark, parts.rods);
  if (parts.panels.length) inst(new THREE.BoxGeometry(0.6, 0.04, 0.6), tubes.A, parts.panels);

  // ---- real lights: area points on alternating circuits, one shadowed spot over the mats ----
  const lights = [];
  const point = (pos, color, base, dist, circuit) => {
    const l = new THREE.PointLight(color, 0, dist, 2); l.position.set(...pos); l.castShadow = false; add(l, live);
    lights.push({ light: l, base, circuit });
  };
  const area = low ? [[-12, -8, 'A'], [6, -8, 'B'], [-12, 8, 'B'], [6, 8, 'A']] : [[-14, -8, 'A'], [-2, -8, 'B'], [10, -8, 'A'], [-14, 8, 'B'], [-2, 8, 'A'], [10, 8, 'B']];
  for (const [x, z, c] of area) point([x, 2.5, z], 0xe6f0ff, low ? 22 : 16, 18, c);
  for (const l of shell.lights) point(l.pos, l.color, l.base, l.dist, l.circuit);
  const spot = new THREE.SpotLight(0xedf3ff, 0, 15, 1.15, 0.75, 2);
  spot.position.set(-7, 2.85, 0); spot.target.position.set(-7, 0, 0.4);
  spot.castShadow = !low; spot.shadow.mapSize.set(kit.quality === 'high' ? 1024 : 512, kit.quality === 'high' ? 1024 : 512);
  spot.shadow.camera.near = 0.5; spot.shadow.camera.far = 15; spot.shadow.bias = -0.0005; spot.shadow.normalBias = 0.02;
  add(spot, live); add(spot.target, live);
  lights.push({ light: spot, base: 34, circuit: 'A' });

  // ---- battery emergency lights (비상조명등): twin heads, dark until the power state says 'emergency' ----
  const emLamp = new THREE.MeshStandardMaterial({ color: 0xf2f6ff, emissive: 0xdde9ff, emissiveIntensity: 0, roughness: 0.2 });
  const emLed = new THREE.MeshStandardMaterial({ color: 0x220000, emissive: 0xff2a1a, emissiveIntensity: 1.5 });
  const emUnits = [[-4, -15.9, 0], [12, -15.9, 0], [-16, 15.9, Math.PI], [2, 15.9, Math.PI], [18, 15.9, Math.PI], [-21.9, -8, Math.PI / 2], [-21.9, 8, Math.PI / 2], [21.9, -8, -Math.PI / 2], [21.9, 8, -Math.PI / 2]];
  const body = [], heads = [], leds = [];
  for (const [x, z, ry] of emUnits) {
    const F = T4(x, 2.42, z, 0, ry, 0);
    body.push(F.clone().multiply(T4(0, 0, 0.06)));
    for (const dx of [-0.1, 0.1]) heads.push(F.clone().multiply(T4(dx, -0.07, 0.12, 0.7, 0, 0)));
    leds.push(F.clone().multiply(T4(0.12, 0.04, 0.115)));
  }
  inst(new THREE.BoxGeometry(0.34, 0.14, 0.12), M.white, body, true);
  inst(new THREE.CylinderGeometry(0.045, 0.035, 0.07, 12), emLamp, heads);
  inst(new THREE.SphereGeometry(0.008, 6, 4), emLed, leds);
  const emLights = [];
  for (const [p, t] of [[[-4, 2.4, -15.6], [-4, 0, -6]], [[2, 2.4, 15.6], [2, 0, 6]]]) {
    const s = new THREE.SpotLight(0xdfe9ff, 0, 16, 0.95, 0.85, 2); s.position.set(...p); s.target.position.set(...t);
    add(s, live); add(s.target, live); emLights.push(s);
  }

  // ---- exit signs (emergency group: lit in every power state) ----
  const exitMat = new THREE.MeshStandardMaterial({ map: T.exitTex(), emissive: 0xffffff, emissiveMap: T.exitTex(), roughness: 0.4 });
  kit.lit('emergency', exitMat, 1.6);
  const exitBox = (x, y, z, ry) => {
    add(kit.box(0.44, 0.23, 0.07, M.white, { x: x - Math.sin(ry) * 0.04, y: y - 0.115, z: z - Math.cos(ry) * 0.04, ry }));
    const p = add(new THREE.Mesh(new THREE.PlaneGeometry(0.4, 0.2), exitMat)); p.position.set(x, y, z); p.rotation.y = ry;
  };
  exitBox((STAIR.x0 + STAIR.x1) / 2, 2.66, -15.62, 0); exitBox(21.9, 2.45, 0, -Math.PI / 2);
  exitBox((ACCESS.x0 + ACCESS.x1) / 2, 2.8, -15.9, 0); exitBox(STAIR.x1 - 0.1, 5.7, -16.85, -Math.PI / 2);
  // aisle guidance on the pillar faces that look into the aisles, arrows toward the nearest pedestrian exit
  const aisleMats = {};
  const aisleMat = (dir) => aisleMats[dir] ||= kit.lit('emergency', new THREE.MeshStandardMaterial({ map: T.aisleTex(dir), emissive: 0xffffff, emissiveMap: T.aisleTex(dir), roughness: 0.4 }), 1.3);
  GRID.xs.forEach((px) => {
    const tA = px < -10 ? (ACCESS.x0 + ACCESS.x1) / 2 : px < 12 ? (STAIR.x0 + STAIR.x1) / 2 : 22;
    const tC = px < 5 ? (STAIR.x0 + STAIR.x1) / 2 : 22;
    for (const [pz, face, tx] of [[GRID.zs[0], 1, tA], [GRID.zs[2], -1, tC]]) {
      const dir = Math.sign(tx - px || 1) * face, ry = face > 0 ? 0 : Math.PI;
      const p = add(new THREE.Mesh(new THREE.PlaneGeometry(0.36, 0.135), aisleMat(dir))); p.position.set(px, 1.0, pz + face * 0.378); p.rotation.y = ry;
      add(kit.box(0.38, 0.15, 0.03, M.white, { x: px, y: 0.925, z: pz + face * 0.36 }));
    }
  });
  // hanging direction boards over the aisles
  const hang = (lines, x, z, ry, w = 1.7) => {
    const s = add(kit.sign(lines, { w, h: 0.3, bg: '#0d8a43', fg: '#ffffff', single: true, emissive: 0.6 })); s.position.set(x, 2.38, z); s.rotation.y = ry;
    kit.lit('signs', s.material, 0.6);
    add(kit.box(w + 0.04, 0.32, 0.03, M.dark, { x: x - Math.sin(ry) * 0.02, y: 2.22, z: z - Math.cos(ry) * 0.02, ry }));
    for (const d of [-w / 2 + 0.1, w / 2 - 0.1]) add(rod(THREE, V(x + Math.cos(ry) * d, 2.54, z - Math.sin(ry) * d), V(x + Math.cos(ry) * d, 3.0, z - Math.sin(ry) * d), 0.006, M.dark, 4));
  };
  hang(['출입구 A · 계단 ↑'], 5, -10.0, 0); hang(['출입구 C · 경사로 ↑'], -13, -10.0, 0);
  hang(['동쪽 출입구 · 1층 로비 ↑'], 19.6, 0, -Math.PI / 2, 2.0); hang(['출입구 B · 차량 진출입로 ↑'], 11, 11.2, Math.PI, 2.0);

  // ---- sprinkler mains (red), branches and heads; hangers ----
  for (const z of [-12.4, -5.2, 5.2, 12.4]) add(rod(THREE, V(ROOM.x0 + 0.2, 2.84, z), V(ROOM.x1 - 0.2, 2.84, z), 0.055, M.red, 12));
  const br = [], hd = [], hang2 = [];
  for (const z of [-12.4, -5.2, 5.2, 12.4]) for (let i = 0; i < 14; i++) {
    const x = -18 + 3 * i;
    // outer mains only branch toward the wall, clear of the pillar drop panels
    for (const dz of Math.abs(z) > 10 ? [Math.sign(z) * 1.3] : [-1.3, 1.3]) {
      br.push(T4(x, 2.82, z + dz / 2, Math.PI / 2, 0, 0));
      hd.push(T4(x, 2.76, z + dz)); hd.push(T4(x, 2.73, z + dz, 0, 0, 0, 0.6));
    }
    hang2.push(T4(x - 1.5, 2.92, z));
  }
  inst(new THREE.CylinderGeometry(0.022, 0.022, 1.3, 8), M.red, br);
  inst(new THREE.CylinderGeometry(0.012, 0.03, 0.05, 8), M.brass, hd);
  inst(new THREE.CylinderGeometry(0.008, 0.008, 0.16, 4), M.dark, hang2);
  // grey drain line along the south wall with a downpipe at the corner
  add(rod(THREE, V(ROOM.x0 + 0.3, 2.88, 15.6), V(ROOM.x1 - 0.3, 2.88, 15.6), 0.075, M.grey, 12));
  add(rod(THREE, V(ROOM.x0 + 0.3, 0, 15.6), V(ROOM.x0 + 0.3, 2.95, 15.6), 0.075, M.grey, 12));
  // cable trays (ladder type) with cables
  const rungs = [], th = [];
  for (const z of [-8.9, 8.9]) {
    for (const dz of [-0.2, 0.2]) add(kit.box(ROOM.x1 - ROOM.x0 - 0.4, 0.08, 0.02, M.galv, { x: 0, y: 2.82, z: z + dz }));
    for (let x = ROOM.x0 + 0.4; x < ROOM.x1 - 0.3; x += 0.3) rungs.push(T4(x, 2.83, z));
    for (let x = ROOM.x0 + 1; x < ROOM.x1; x += 1.5) for (const dz of [-0.22, 0.22]) th.push(T4(x, 2.94, z + dz));
    [[-0.12, 0.02, 0.018], [-0.06, 0.02, 0.014], [0, 0.02, 0.02], [0.07, 0.02, 0.012], [0.12, 0.02, 0.016]].forEach(([dz, , r]) => add(rod(THREE, V(ROOM.x0 + 0.3, 2.86 + r, z + dz), V(ROOM.x1 - 0.3, 2.86 + r, z + dz), r, M.cable, 6)));
  }
  inst(new THREE.BoxGeometry(0.03, 0.02, 0.4), M.galv, rungs);
  inst(new THREE.CylinderGeometry(0.006, 0.006, 0.12, 4), M.dark, th);
  // supply duct along the west wall with grilles
  add(kit.box(0.8, 0.42, ROOM.z1 - ROOM.z0 - 0.4, M.galv, { x: ROOM.x0 + 0.65, y: 2.5, z: 0 }));
  for (let z = -13; z <= 13; z += 4.33) add(kit.box(0.02, 0.24, 0.6, M.dark, { x: ROOM.x0 + 1.06, y: 2.59, z }));

  // ---- jet fans: galvanised drum with grilles, rotor turning while the grid supply is on ----
  const fans = [];
  for (const [x, z] of [[-3, -8], [12, 8]]) {
    const drum = new THREE.Mesh(new THREE.CylinderGeometry(0.33, 0.33, 1.6, 20, 1, true), kit.mat('metal', { color: 0xb9bdc0, roughness: 0.5, side: THREE.DoubleSide }));
    drum.rotation.z = Math.PI / 2; drum.position.set(x, 2.55, z); add(drum);
    for (const dx of [-0.8, 0.8]) { const ring = add(new THREE.Mesh(new THREE.TorusGeometry(0.33, 0.025, 6, 20), M.galv)); ring.position.set(x + dx, 2.55, z); ring.rotation.y = Math.PI / 2; }
    for (const dx of [-0.5, 0.5]) add(kit.box(0.06, 0.12, 0.5, M.dark, { x: x + dx, y: 2.88, z }));
    const rotor = new THREE.Group(); rotor.position.set(x, 2.55, z); rotor.userData.keep = true;
    const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 0.18, 12), M.dark); hub.rotation.z = Math.PI / 2; rotor.add(hub);
    for (let b = 0; b < 6; b++) { const bl = new THREE.Mesh(new THREE.BoxGeometry(0.03, 0.24, 0.09), M.galv); bl.position.set(0, Math.cos(b * Math.PI / 3) * 0.16, Math.sin(b * Math.PI / 3) * 0.16); bl.rotation.set(b * Math.PI / 3 + 0.5, 0, 0); rotor.add(bl); }
    add(rotor, live); fans.push({ rotor, speed: 0 });
  }

  // ---- hydrant cabinets with their always-on red lamp ----
  const hydrantFace = new THREE.MeshStandardMaterial({ map: T.hydrantTex(), roughness: 0.45, metalness: 0.2 });
  const hydLamp = kit.lit('emergency', new THREE.MeshStandardMaterial({ color: 0x400000, emissive: 0xff2010, roughness: 0.3 }), 2.2);
  for (const [x, z, ry] of [[-21.9, -2, Math.PI / 2], [-6, -15.9, 0], [-2, 15.9, Math.PI], [21.9, -9, -Math.PI / 2]]) {
    const sx = Math.sin(ry), cz = Math.cos(ry);
    add(kit.box(0.75, 1.3, 0.2, M.red, { x: x + sx * 0.1, y: 0.5, z: z + cz * 0.1, ry }));
    const f = add(new THREE.Mesh(new THREE.PlaneGeometry(0.7, 1.24), hydrantFace)); f.position.set(x + sx * 0.205, 1.15, z + cz * 0.205); f.rotation.y = ry;
    const l = add(new THREE.Mesh(new THREE.SphereGeometry(0.05, 10, 6), hydLamp)); l.position.set(x + sx * 0.1, 1.86, z + cz * 0.1);
  }

  // ---- extinguishers beside pillars, with the red '소화기' plate (one shared material) ----
  const plate = kit.sign(['소화기'], { w: 0.32, h: 0.14, bg: '#d7262d', fg: '#ffffff', single: true });
  const ext = [[GRID.xs[0], GRID.zs[0], 1], [GRID.xs[2], GRID.zs[0], 1], [GRID.xs[4], GRID.zs[0], 1], [GRID.xs[1], GRID.zs[1], -1], [GRID.xs[3], GRID.zs[1], 1], [GRID.xs[1], GRID.zs[2], -1], [GRID.xs[3], GRID.zs[2], -1]];
  for (const [px, pz, face] of ext) {
    const z = pz + face * 0.5, x = px + 0.22;
    add(kit.box(0.26, 0.04, 0.26, M.dark, { x, z }));
    add(kit.cyl(0.085, 0.085, 0.46, M.red, { x, y: 0.04, z }, 16));
    add(kit.cyl(0.03, 0.05, 0.08, M.dark, { x, y: 0.5, z }, 8));
    add(rod(THREE, V(x + 0.03, 0.55, z), V(x + 0.1, 0.2, z + face * 0.05), 0.012, M.cable, 5));
    const p = plate.clone(); p.position.set(px, 1.35, pz + face * 0.358); p.rotation.y = face > 0 ? 0 : Math.PI; add(p);
  }

  // ---- CCTV domes with a blinking record LED; electrical panel by stair A ----
  const cctvLed = new THREE.MeshStandardMaterial({ color: 0x300000, emissive: 0xff1a1a, emissiveIntensity: 2 });
  for (const [x, z] of [[2, -11.6], [-12, -4.4], [14, 4.4], [19, -12]]) {
    add(kit.cyl(0.09, 0.09, 0.04, M.white, { x, y: 2.96, z }, 14));
    const dome = add(new THREE.Mesh(new THREE.SphereGeometry(0.075, 14, 8, 0, Math.PI * 2, Math.PI / 2, Math.PI / 2), kit.mat('darkGlass'))); dome.position.set(x, 2.96, z);
    const led = add(new THREE.Mesh(new THREE.SphereGeometry(0.01, 6, 4), cctvLed)); led.position.set(x + 0.08, 2.95, z);
  }
  const cc = add(kit.sign(['CCTV 녹화 중'], { w: 0.5, h: 0.2, bg: '#f4f2ec', fg: '#c8261f', single: true })); cc.position.set(0.4, 1.75, -15.97);
  add(kit.box(0.8, 1.6, 0.25, kit.mat('paint:#a9adb0', { roughness: 0.5, metalness: 0.3 }), { x: -1.5, y: 0.3, z: -15.86 }));
  const ep = add(kit.sign(['분전반 · 관계자 외 조작 금지'], { w: 0.7, h: 0.12, bg: '#f2c230', fg: '#111', single: true })); ep.position.set(-1.5, 1.6, -15.73);
  for (const [x, z, ry] of [[21.97, -5, -Math.PI / 2], [-21.97, 5, Math.PI / 2]]) {
    const b1 = add(kit.sign(['B1'], { w: 1.4, h: 1.0, bg: '#dcdad3', fg: '#2f8f5b', single: true })); b1.position.set(x, 1.75, z); b1.rotation.y = ry;
  }
  const ramps = add(kit.sign(['출입구 C · 경사로', '휠체어 · 유모차 · 보행보조기'], { w: 1.7, h: 0.46, bg: '#1d4fa0', single: true }));
  ramps.position.set(ACCESS.x0 - 1.5, 2.0, -15.97);
  const stairs = add(kit.sign(['출입구 A · 계단'], { w: 1.3, h: 0.28, bg: '#1d4fa0', single: true })); stairs.position.set(STAIR.x0 - 0.8, 2.0, -15.97);
  const rampB = add(kit.sign(['지하주차장 출입구 B'], { w: 1.6, h: 0.28, bg: '#1d4fa0', single: true })); rampB.position.set(RAMP.x0 - 1.0, 2.0, 15.97); rampB.rotation.y = Math.PI;
  for (const [x, z, ry] of [[ACCESS.x1 + 0.5, -15.97, 0], [STAIR.x1 + 0.45, -15.97, 0], [21.97, 2.0, -Math.PI / 2]]) {
    const s = add(kit.shelterSign({ w: 0.36 })); s.position.set(x, 1.85, z); s.rotation.y = ry;
  }

  return { group: g, live, tubes, lights, emLamp, emLed, emLights, fans, cctvLed, faulty };
}
