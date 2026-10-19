import * as THREE from './vendor/three.module.js';

export const LOCATIONS = [
  { id: 'notice', label: '현장 안내', x: 0, z: 18 },
  { id: 'gate', label: '동쪽 출입구', x: 12, z: 2 },
  { id: 'stairs', label: '계단 출입구', x: -13, z: -20 },
  { id: 'ramp', label: '경사로 출입구', x: 14, z: -20 },
];

export const MAP_BUILDINGS = [
  { x: -24, z: 25, w: 18, d: 19 },
  { x: 25, z: 25, w: 18, d: 19 },
  { x: -26, z: -11, w: 20, d: 15 },
  { x: 27, z: -10, w: 18, d: 13 },
  { x: 0, z: -34, w: 42, d: 16 },
];

export function createWorld(canvas, { onUpdate } = {}) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.6));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.18;
  const scene = new THREE.Scene();
  scene.background = new THREE.Color('#9badbb');
  scene.fog = new THREE.Fog('#9badbb', 42, 105);
  const camera = new THREE.PerspectiveCamera(54, 1, 0.15, 160);
  const clock = new THREE.Clock();
  const world = new THREE.Group();
  scene.add(world);
  const colliders = [];
  const cameraSolids = [];
  const materials = new Map();
  const geometries = new Map();
  const textures = [];
  let disposed = false;
  let active = false;
  let chapter = 0;
  let moving = false;
  let yaw = 0;
  let pitch = 0.37;
  let view = 'third';
  let lookPitch = -0.08;
  const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let frame = 0;
  let lastReport = -1;
  let stride = 0;
  let pointer = null;
  let targetId = null;
  const keys = new Set();
  const visited = new Set();
  const player = new THREE.Vector3(0, 0, 32);

  function mat(color, options = {}) {
    const key = `${color}:${JSON.stringify(options)}`;
    if (!materials.has(key)) materials.set(key, new THREE.MeshStandardMaterial({ color, roughness: 0.86, ...options }));
    return materials.get(key);
  }

  function box(w, h, d, color, x, y, z, parent = world, options = {}) {
    const key = `box:${w}:${h}:${d}`;
    if (!geometries.has(key)) geometries.set(key, new THREE.BoxGeometry(w, h, d));
    const mesh = new THREE.Mesh(geometries.get(key), typeof color === 'string' || typeof color === 'number' ? mat(color, options) : color);
    mesh.position.set(x, y, z);
    mesh.castShadow = h > 0.12;
    mesh.receiveShadow = true;
    parent.add(mesh);
    return mesh;
  }

  function cylinder(radius, height, color, x, y, z, parent = world, top = radius, segments = 8) {
    const key = `cylinder:${top}:${radius}:${height}:${segments}`;
    if (!geometries.has(key)) geometries.set(key, new THREE.CylinderGeometry(top, radius, height, segments));
    const mesh = new THREE.Mesh(geometries.get(key), mat(color));
    mesh.position.set(x, y, z);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    parent.add(mesh);
    return mesh;
  }

  function solid(x, z, w, d, mesh) {
    colliders.push({ x, z, w, d });
    if (mesh) cameraSolids.push(mesh);
  }

  function sign(lines, x, y, z, width, height, { background = '#173e46', color = '#e6fff2', rotation = 0, parent = world } = {}) {
    const surface = document.createElement('canvas');
    surface.width = 768;
    surface.height = Math.round(768 * height / width);
    const context = surface.getContext('2d');
    context.fillStyle = background;
    context.fillRect(0, 0, surface.width, surface.height);
    context.strokeStyle = `${color}66`;
    context.lineWidth = 3;
    context.strokeRect(12, 12, surface.width - 24, surface.height - 24);
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    const texts = Array.isArray(lines) ? lines : [lines];
    const lineHeight = surface.height / (texts.length + 0.55);
    texts.forEach((text, i) => {
      context.font = `700 ${Math.min(lineHeight * 0.62, 98)}px "Malgun Gothic", "Noto Sans KR", sans-serif`;
      context.fillStyle = color;
      context.fillText(text, surface.width / 2, lineHeight * (i + 0.78), surface.width * 0.91);
    });
    const texture = new THREE.CanvasTexture(surface);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.anisotropy = Math.min(4, renderer.capabilities.getMaxAnisotropy());
    textures.push(texture);
    const material = new THREE.MeshBasicMaterial({ map: texture, side: THREE.DoubleSide });
    const geometry = new THREE.PlaneGeometry(width, height);
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(x, y, z);
    mesh.rotation.y = rotation;
    parent.add(mesh);
    return mesh;
  }

  scene.add(new THREE.HemisphereLight('#f4e9d4', '#546979', 2.25));
  const sun = new THREE.DirectionalLight('#ffddb1', 3.2);
  sun.position.set(-25, 42, 18);
  sun.target.position.set(0, 0, -3);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  Object.assign(sun.shadow.camera, { left: -43, right: 43, top: 44, bottom: -44, near: 1, far: 110 });
  sun.shadow.bias = -0.0004;
  sun.shadow.normalBias = 0.035;
  scene.add(sun, sun.target);

  box(110, 0.35, 110, '#818e86', 0, -0.24, 0);
  box(15, 0.09, 87, '#46515a', 0, -0.025, -1.5);
  box(82, 0.095, 11, '#48545c', 0, -0.02, 2);
  box(67, 0.11, 9, '#617473', 0, -0.01, -21);
  [-1, 1].forEach(side => {
    box(5.2, 0.1, 83, '#b0b3a5', side * 10.1, 0, -1.5);
    box(0.22, 0.13, 82, '#d0caba', side * 7.59, 0.025, -1);
    for (let z = -40; z < 41; z += 3) box(4.9, 0.012, 0.025, '#959e94', side * 10.1, 0.055, z);
  });
  for (let z = -38; z <= 41; z += 5.5) {
    if (z > -5 && z < 9) continue;
    box(0.14, 0.012, 2.7, '#ddc889', -0.17, 0.029, z);
    box(0.14, 0.012, 2.7, '#ddc889', 0.17, 0.029, z);
  }
  [-7.4, 11.8].forEach(z => {
    for (let x = -5.7; x < 7; x += 1.5) box(0.85, 0.02, 2.4, '#d8d5bf', x, 0.03, z);
  });
  for (let x = -33; x < 35; x += 4) {
    if (Math.abs(x) < 11) continue;
    box(2.1, 0.01, 0.12, '#d1c694', x, 0.039, 2);
  }
  [17, 28, -13, -29].forEach(z => {
    box(0.95, 0.02, 0.56, '#36464b', 6.8, 0.04, z);
    for (let a = 0; a < 5; a++) box(0.045, 0.02, 0.53, '#748384', 6.46 + a * 0.17, 0.053, z);
  });

  function building(data, index) {
    const { x, z, w, d } = data;
    const height = [14, 18, 10, 12, 8][index];
    const colors = ['#bfb1a0', '#9da7a6', '#c9b69e', '#adb7ad', '#879b98'];
    const body = box(w, height, d, colors[index], x, height / 2, z);
    solid(x, z, w, d, body);
    box(w + 0.6, 0.35, d + 0.6, '#5b6a69', x, height + 0.12, z);
    box(w + 0.1, 0.27, d + 0.1, '#d5c8b0', x, 3.4, z);
    box(w, 0.6, d + 0.15, '#758480', x, 0.31, z);
    if (index === 4) return;
    const face = x < 0 ? 1 : -1;
    const faceX = x + face * (w / 2 + 0.055);
    for (let floor = 4.8; floor < height - 0.8; floor += 3.1) {
      for (let offset = -d / 2 + 2.2; offset < d / 2 - 1; offset += 3.5) {
        const glowing = Math.sin(floor * 2 + offset * 7 + index) > -0.13;
        box(0.15, 1.65, 1.7, '#667b7b', faceX, floor, z + offset);
        box(0.16, 1.35, 1.35, glowing ? '#e2bc7c' : '#506774', faceX + face * 0.03, floor, z + offset, world,
          glowing ? { emissive: '#b08748', emissiveIntensity: 0.3 } : {});
        box(0.2, 0.09, 1.5, '#b4b7a6', faceX + face * 0.1, floor, z + offset);
      }
      for (let offset = -w / 2 + 2.3; offset < w / 2 - 1; offset += 3.4) {
        box(1.8, 1.65, 0.15, '#6f8181', x + offset, floor, z + d / 2 + 0.07);
        box(1.48, 1.35, 0.17, '#b9bda2', x + offset, floor, z + d / 2 + 0.1);
        box(0.07, 1.4, 0.2, '#6c7d7c', x + offset, floor, z + d / 2 + 0.12);
      }
    }
    box(2.5, 2, 2.5, '#bbc4b7', x + 3, height + 1.2, z);
    box(3, 0.3, 3, '#71847c', x + 3, height + 2.3, z);
    cylinder(0.05, 4.4, '#5b6a65', x - 4, height + 2.2, z - 3);
    box(2.3, 0.045, 0.045, '#5b6a65', x - 4, height + 3.3, z - 3);
    const shopColor = ['#a9604b', '#405d69', '#627b67', '#b3915d'][index];
    const name = ['우리동네 문구', '느린 오후', '한빛 세탁', '동네 슈퍼'][index];
    sign(name, faceX + face * 0.08, 2.9, z, 6.5, 1.1, { background: shopColor, rotation: face * Math.PI / 2 });
    for (const offset of [-2.2, 0, 2.2]) box(0.16, 2.15, 1.9, '#345866', faceX, 1.35, z + offset);
    box(1.65, 0.14, 7.3, shopColor, faceX + face * 0.62, 2.15, z);
    box(0.15, 0.4, 7.3, shopColor, faceX + face * 1.4, 2, z);
  }
  MAP_BUILDINGS.forEach(building);

  const shelterFront = -25.92;
  sign(['한빛 생활문화센터', '대피 시설 · SHELTER'], 0, 5.85, shelterFront + 0.09, 15.2, 2.4);
  box(42.5, 0.23, 2.4, '#416a69', 0, 3.65, -25.4);
  for (const x of [-13, 14]) {
    box(4.6, 3.1, 0.15, '#23474d', x, 1.57, shelterFront);
    box(0.08, 3, 0.21, '#afbfb3', x, 1.57, shelterFront + 0.03);
    sign(x < 0 ? ['출입구 A', '계단'] : ['출입구 B', '경사로'], x, 4.03, -24.1, 4.1, 1.35);
  }
  for (let i = 0; i < 4; i++) box(4.7, 0.13 * (i + 1), 0.8, '#c6c8b8', -13, 0.065 * (i + 1), -22.7 - i * 0.8);
  const ramp = box(5.3, 0.14, 5.9, '#b9c2b0', 14, 0.3, -23.1);
  ramp.rotation.x = -0.082;
  for (const x of [11.2, 16.8]) {
    for (const z of [-21, -23, -25]) cylinder(0.04, 0.85, '#b8d0c0', x, 0.65, z);
    box(0.06, 0.06, 4.2, '#b8d0c0', x, 1.03, -23);
  }
  sign('B  →', 14, 0.07, -19.7, 2, 0.9).rotation.x = -Math.PI / 2;
  for (const x of [-4, 4]) {
    box(3.6, 2, 0.1, '#78979a', x, 1.8, shelterFront + 0.05);
    box(0.08, 2, 0.16, '#c0c6af', x, 1.8, shelterFront + 0.1);
  }

  function tree(x, z, size = 1) {
    const group = new THREE.Group();
    group.position.set(x, 0, z);
    world.add(group);
    box(1.55, 0.48, 1.55, '#b9b29b', 0, 0.24, 0, group);
    box(1.3, 0.05, 1.3, '#58674c', 0, 0.5, 0, group);
    cylinder(0.13, 2.6 * size, '#756a54', 0, 1.5 * size, 0, group, 0.09);
    const geometry = new THREE.IcosahedronGeometry(1, 1);
    for (const [cx, cy, cz, radius, color] of [[0, 3.3, 0, 1.35, '#5d7d5d'], [0.65, 2.9, 0.1, 1, '#6d8963'], [-0.5, 3.1, 0.45, 1.1, '#758c61']]) {
      const leaf = new THREE.Mesh(geometry, mat(color));
      leaf.position.set(cx * size, cy * size, cz * size);
      leaf.scale.setScalar(radius * size);
      leaf.castShadow = true;
      group.add(leaf);
    }
    solid(x, z, 1.6, 1.6);
  }
  [[-10.6, 34, 1.1], [10.6, 34, 1.25], [-10.6, 23, 0.92], [10.6, 15, 1], [-11, -11, 1.1], [12, -9, 1.05], [-22, -22, 1.1], [24, -22, 1.2], [-35, 8, 1.2], [35, 8, 1]].forEach(args => tree(...args));

  function lamp(x, z, side) {
    cylinder(0.075, 5.8, '#475d60', x, 2.9, z);
    cylinder(0.19, 0.3, '#52666a', x, 0.15, z);
    box(1.8, 0.07, 0.09, '#475d60', x + side * 0.8, 5.78, z);
    box(0.75, 0.17, 0.36, '#4b5c5c', x + side * 1.5, 5.71, z);
    box(0.62, 0.03, 0.28, '#ffe6b2', x + side * 1.5, 5.6, z, world, { emissive: '#ffc873', emissiveIntensity: 1.2 });
    solid(x, z, 0.35, 0.35);
  }
  [[-8.6, 29, 1], [8.6, 20, -1], [-8.6, -9, 1], [8.6, -14, -1], [-28, 9, 1]].forEach(args => lamp(...args));

  function car(x, z, color, angle = 0) {
    const group = new THREE.Group();
    group.position.set(x, 0, z);
    group.rotation.y = angle;
    world.add(group);
    box(1.9, 0.58, 4.15, color, 0, 0.65, 0, group);
    box(1.72, 0.68, 2.35, color, 0, 1.17, -0.12, group);
    box(1.52, 0.48, 0.025, '#4c6971', 0, 1.23, -1.31, group).rotation.x = -0.19;
    box(1.52, 0.43, 0.03, '#4c6971', 0, 1.24, 1.08, group).rotation.x = 0.17;
    for (const side of [-1, 1]) {
      box(0.025, 0.44, 1.87, '#4c6971', side * 0.87, 1.25, -0.12, group);
      box(0.045, 0.51, 0.09, color, side * 0.9, 1.24, -0.08, group);
      for (const cz of [-1.3, 1.27]) {
        const wheel = cylinder(0.37, 0.2, '#26383b', side * 0.91, 0.4, cz, group, 0.37, 12);
        wheel.rotation.z = Math.PI / 2;
      }
      box(0.48, 0.18, 0.045, '#f0dfa3', side * 0.6, 0.69, -2.09, group);
      box(0.45, 0.17, 0.045, '#bd7560', side * 0.6, 0.69, 2.09, group);
    }
    box(1.63, 0.15, 0.1, '#607477', 0, 0.4, -2.12, group);
    solid(x, z, angle ? 4.3 : 2.05, angle ? 2.05 : 4.3, group);
  }
  car(-5.4, 26, '#d6cfb8');
  car(5.5, -11.7, '#677f88');
  car(27, 5.6, '#b69271', Math.PI / 2);

  function bench(x, z, rotation = 0) {
    const group = new THREE.Group();
    group.position.set(x, 0, z);
    group.rotation.y = rotation;
    world.add(group);
    for (let i = 0; i < 3; i++) box(2.6, 0.08, 0.15, '#ac8e68', 0, 0.56, i * 0.2, group);
    for (const h of [0.9, 1.13]) box(2.6, 0.17, 0.08, '#ac8e68', 0, h, 0.46, group);
    for (const bx of [-1, 1]) box(0.1, 0.55, 0.52, '#465e5c', bx, 0.27, 0.2, group);
    solid(x, z, rotation ? 0.8 : 2.8, rotation ? 2.8 : 0.8);
  }
  bench(-10.7, 14, Math.PI / 2);
  bench(7, -23);
  bench(-5.5, -23);
  for (const [x, z] of [[-10, 16.8], [10.2, 26.5], [20, -21.6]]) {
    cylinder(0.35, 0.95, '#3e625b', x, 0.49, z, world);
    cylinder(0.39, 0.07, '#678075', x, 1, z);
    solid(x, z, 0.75, 0.75);
  }

  for (const side of [-1, 1]) {
    for (const z of [-14, 14, 38]) {
      cylinder(0.105, 9.2, '#68716b', side * 13.8, 4.6, z);
      box(1.9, 0.12, 0.15, '#697771', side * 13.8, 8.2, z);
    }
    for (const xOffset of [-0.7, 0, 0.7]) {
      const points = [];
      for (let z = -14; z <= 38; z += 0.5) {
        const start = z < 14 ? -14 : 14;
        const end = z < 14 ? 14 : 38;
        const phase = (z - start) / (end - start);
        points.push(new THREE.Vector3(side * 13.8 + xOffset, 8.28 - Math.sin(phase * Math.PI) * 0.7, z));
      }
      const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints(points), new THREE.LineBasicMaterial({ color: '#66747a' }));
      world.add(line);
    }
  }

  // The entrance obstruction is visible from the street; inspecting it never requires entering the debris.
  for (const z of [-1, 1.8, 4.6]) {
    box(0.28, 1.1, 2.1, '#d7a569', 17.5, 0.58, z).rotation.y = 0.08;
    for (const dz of [-0.7, 0.2]) box(0.32, 0.24, 0.4, '#454f4d', 17.5, 0.85, z + dz);
    box(1, 0.12, 0.35, '#596563', 17.5, 0.07, z);
  }
  solid(17.5, 1.8, 0.65, 8);
  sign(['동쪽 출입구', '현장 상태 확인'], 16.8, 2.2, 2, 3.1, 1.5, { background: '#735442', color: '#fff0c6', rotation: -Math.PI / 2 });
  for (const [x, z, angle] of [[21, 0, 0.3], [22, 3, 0.6], [24, 1, -0.2], [23, -1, 0.7]]) {
    const rubble = box(1.5, 0.45, 1, '#929789', x, 0.25, z);
    rubble.rotation.y = angle;
    solid(x, z, 1.6, 1.3);
  }

  box(3.1, 2.4, 0.18, '#617879', 0, 2.35, 16.6);
  for (const x of [-1.3, 1.3]) box(0.1, 2.5, 0.11, '#4b6669', x, 1.3, 16.6);
  sign(['현장 안내', '확인한 정보부터', '안내 기록  ·  14:10'], 0, 2.38, 16.71, 2.86, 2.12);
  box(3.4, 0.12, 0.6, '#425e60', 0, 3.62, 16.6);
  solid(0, 16.6, 3.1, 0.3);

  // Silhouettes extend the neighborhood without adding unreachable mission areas.
  for (let i = 0; i < 13; i++) {
    const x = -58 + i * 9.5;
    const height = 13 + (i % 4) * 5;
    box(7.5, height, 9, ['#879a9e', '#97a6a7', '#85999b'][i % 3], x, height / 2, -61 - i % 3 * 7);
  }
  for (const x of [-46, 46]) {
    for (const z of [-34, -13, 22, 42]) box(12, 15 + ((z + 34) % 3) * 3, 14, '#9aa5a0', x, 7.5, z);
  }

  const markerGeometry = new THREE.RingGeometry(0.8, 0.96, 48);
  const markers = LOCATIONS.map(location => {
    const group = new THREE.Group();
    group.position.set(location.x, 0.075, location.z);
    const ring = new THREE.Mesh(markerGeometry, new THREE.MeshBasicMaterial({ color: '#aee7d6', transparent: true, opacity: 0.68, side: THREE.DoubleSide, depthWrite: false }));
    ring.rotation.x = -Math.PI / 2;
    group.add(ring);
    const diamond = new THREE.Mesh(new THREE.OctahedronGeometry(0.2), new THREE.MeshBasicMaterial({ color: '#b9fbe7' }));
    diamond.position.y = 2.9;
    group.add(diamond);
    world.add(group);
    return { id: location.id, group, ring, diamond };
  });
  const targetRing = new THREE.Mesh(new THREE.RingGeometry(1.24, 1.35, 64), new THREE.MeshBasicMaterial({ color: '#f9bf70', transparent: true, opacity: 0.85, side: THREE.DoubleSide, depthWrite: false }));
  targetRing.rotation.x = -Math.PI / 2;
  targetRing.visible = false;
  world.add(targetRing);

  const avatar = new THREE.Group();
  scene.add(avatar);
  const clothes = new THREE.MeshStandardMaterial({ color: '#448582', roughness: 0.92 });
  const hairMaterial = new THREE.MeshStandardMaterial({ color: '#343d3a', roughness: 1 });
  const body = new THREE.Group();
  avatar.add(body);
  const skin = '#dbb18c';
  cylinder(0.26, 0.62, clothes.color.getHex(), 0, 1.35, 0, body, 0.34, 7).material = clothes;
  box(0.56, 0.26, 0.37, '#374b57', 0, 0.94, 0, body);
  cylinder(0.1, 0.15, skin, 0, 1.75, 0, body);
  const head = box(0.37, 0.43, 0.36, skin, 0, 2, -0.025, body);
  const hair = box(0.4, 0.16, 0.38, hairMaterial, 0, 2.18, 0, body);
  box(0.39, 0.25, 0.1, hairMaterial, 0, 2.04, 0.14, body);
  box(0.1, 0.09, 0.08, skin, 0, 1.99, -0.24, body);
  for (const x of [-0.22, 0.22]) box(0.06, 0.12, 0.1, skin, x, 1.99, 0, body);
  box(0.49, 0.57, 0.24, '#c99e62', 0, 1.36, 0.28, body);
  box(0.37, 0.21, 0.08, '#ac7f50', 0, 1.22, 0.42, body);
  for (const x of [-0.21, 0.21]) box(0.055, 0.5, 0.05, '#c4ad7f', x, 1.46, -0.22, body);
  const arms = [];
  const legs = [];
  for (const side of [-1, 1]) {
    const arm = new THREE.Group();
    arm.position.set(side * 0.34, 1.59, 0);
    arm.rotation.z = side * 0.08;
    body.add(arm);
    cylinder(0.09, 0.43, '#448582', 0, -0.17, 0, arm).material = clothes;
    cylinder(0.074, 0.31, skin, 0, -0.52, 0, arm);
    box(0.14, 0.15, 0.13, skin, 0, -0.73, 0, arm);
    arms.push(arm);
    const leg = new THREE.Group();
    leg.position.set(side * 0.15, 0.95, 0);
    body.add(leg);
    cylinder(0.12, 0.78, '#3e515c', 0, -0.38, 0, leg, 0.14);
    box(0.23, 0.16, 0.38, '#d3d0ba', 0, -0.83, -0.075, leg);
    box(0.24, 0.045, 0.39, '#4b605f', 0, -0.925, -0.075, leg);
    legs.push(leg);
  }
  // Wheeled walker (보행 보조기): front legs on wheels, handles at hip height, open at the back.
  const walker = new THREE.Group();
  walker.position.set(0, 0, -0.5);
  for (const side of [-1, 1]) {
    for (const z of [-0.2, 0.2]) cylinder(0.02, 0.86, '#c6d3ca', side * 0.29, 0.47, z, walker, 0.02);
    box(0.045, 0.045, 0.46, '#c6d3ca', side * 0.29, 0.89, 0, walker);
    box(0.06, 0.07, 0.2, '#3d5554', side * 0.29, 0.92, 0.12, walker);
    const wheel = cylinder(0.065, 0.04, '#2f4243', side * 0.29, 0.065, -0.2, walker, 0.065, 12);
    wheel.rotation.z = Math.PI / 2;
  }
  box(0.6, 0.035, 0.035, '#c6d3ca', 0, 0.62, -0.2, walker);
  walker.visible = false;
  avatar.add(walker);
  const shadow = new THREE.Mesh(new THREE.CircleGeometry(0.64, 32), new THREE.MeshBasicMaterial({ color: '#263d40', transparent: true, opacity: 0.16, depthWrite: false }));
  shadow.rotation.x = -Math.PI / 2;
  shadow.position.y = 0.055;
  avatar.add(shadow);
  const cameraLook = new THREE.Vector3(0, 1.25, 32);
  const desiredCamera = new THREE.Vector3();
  const direction = new THREE.Vector3();
  const raycaster = new THREE.Raycaster();
  camera.position.set(0, 5.1, 41.5);
  camera.lookAt(cameraLook);

  function isBlocked(x, z, radius = 0.36) {
    if (x < -39 || x > 39 || z < -43 || z > 41) return true;
    return colliders.some(c => x + radius > c.x - c.w / 2 && x - radius < c.x + c.w / 2 && z + radius > c.z - c.d / 2 && z - radius < c.z + c.d / 2);
  }

  for (const location of LOCATIONS) {
    if (isBlocked(location.x, location.z)) throw new Error(`이동 지점이 장애물과 겹칩니다: ${location.id}`);
  }

  function getSnapshot() {
    let nearby = null;
    for (const location of LOCATIONS) {
      const distance = Math.hypot(player.x - location.x, player.z - location.z);
      if (distance <= 4 && (!nearby || distance < nearby.distance)) nearby = { id: location.id, label: location.label, distance };
    }
    return { x: player.x, z: player.z, heading: avatar.rotation.y, moving, nearby, view };
  }

  function setActive(value) {
    active = Boolean(value);
    if (!active) {
      keys.clear();
      moving = false;
      pointer = null;
    }
  }

  function setView(value) {
    view = value === 'first' ? 'first' : 'third';
    if (view === 'first') {
      avatar.rotation.y = yaw;
      lookPitch = -0.08;
    }
    body.visible = shadow.visible = view === 'third';
    return view;
  }

  function setChapter(value) {
    chapter = value === 1 ? 1 : 0;
    keys.clear();
    player.set(0, 0, 32);
    avatar.position.copy(player);
    avatar.rotation.y = 0;
    yaw = 0;
    pitch = 0.37;
    moving = false;
    stride = 0;
    clothes.color.set(chapter ? '#aa7659' : '#448582');
    hairMaterial.color.set(chapter ? '#a5afa7' : '#343d3a');
    walker.visible = chapter === 1;
    lookPitch = -0.08;
    cameraLook.set(0, 1.25, 32);
    camera.position.set(0, 5.1, 41.5);
    onUpdate?.(getSnapshot());
  }

  function setTarget(id) {
    targetId = LOCATIONS.some(p => p.id === id) ? id : null;
    const location = LOCATIONS.find(p => p.id === targetId);
    targetRing.visible = Boolean(location);
    if (location) targetRing.position.set(location.x, 0.087, location.z);
  }

  function setVisited(ids) {
    visited.clear();
    if (Array.isArray(ids)) ids.forEach(id => visited.add(id));
    markers.forEach(marker => {
      marker.ring.material.opacity = visited.has(marker.id) ? 0.24 : 0.68;
      marker.diamond.visible = !visited.has(marker.id);
    });
  }

  const movementKeys = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'ShiftLeft', 'ShiftRight', 'KeyJ', 'KeyL']);
  const onKeyDown = event => {
    if (!active || event.ctrlKey || event.metaKey || event.altKey || /INPUT|TEXTAREA|SELECT/.test(event.target?.tagName)) return;
    if (movementKeys.has(event.code)) {
      keys.add(event.code);
      event.preventDefault();
    }
  };
  const onKeyUp = event => keys.delete(event.code);
  const onBlur = () => { keys.clear(); pointer = null; moving = false; };
  function look(dx, dy, sensitivity) {
    yaw -= dx * sensitivity;
    if (view === 'first') lookPitch = THREE.MathUtils.clamp(lookPitch - dy * sensitivity, -1.2, 1.15);
    else pitch = THREE.MathUtils.clamp(pitch + dy * sensitivity * 0.6, 0.12, 0.8);
  }
  const onPointerDown = event => {
    if (!active || event.button !== 0) return;
    canvas.focus({ preventScroll: true });
    pointer = { id: event.pointerId, x: event.clientX, y: event.clientY };
    canvas.setPointerCapture(event.pointerId);
  };
  const onPointerMove = event => {
    if (!active || !pointer || event.pointerId !== pointer.id) return;
    look(event.clientX - pointer.x, event.clientY - pointer.y, 0.005);
    pointer.x = event.clientX;
    pointer.y = event.clientY;
  };
  const onPointerUp = event => {
    if (pointer?.id !== event.pointerId) return;
    if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
    pointer = null;
  };
  const onVisibility = () => { if (document.hidden) onBlur(); };
  window.addEventListener('keydown', onKeyDown);
  window.addEventListener('keyup', onKeyUp);
  window.addEventListener('blur', onBlur);
  document.addEventListener('visibilitychange', onVisibility);
  canvas.addEventListener('pointerdown', onPointerDown);
  canvas.addEventListener('pointermove', onPointerMove);
  canvas.addEventListener('pointerup', onPointerUp);
  canvas.addEventListener('pointercancel', onPointerUp);
  canvas.addEventListener('lostpointercapture', onPointerUp);
  const resize = () => {
    const width = Math.max(1, canvas.clientWidth);
    const height = Math.max(1, canvas.clientHeight);
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
  };
  const observer = new ResizeObserver(resize);
  observer.observe(canvas);
  resize();

  function animate() {
    if (disposed) return;
    frame = requestAnimationFrame(animate);
    const dt = Math.min(clock.getDelta(), 0.05);
    const elapsed = clock.elapsedTime;
    let moveX = 0;
    let moveZ = 0;
    if (active) {
      if (keys.has('KeyJ')) yaw += dt * 1.7;
      if (keys.has('KeyL')) yaw -= dt * 1.7;
      const forward = Number(keys.has('KeyW') || keys.has('ArrowUp')) - Number(keys.has('KeyS') || keys.has('ArrowDown'));
      const right = Number(keys.has('KeyD') || keys.has('ArrowRight')) - Number(keys.has('KeyA') || keys.has('ArrowLeft'));
      const length = Math.hypot(forward, right) || 1;
      const running = chapter === 0 && (keys.has('ShiftLeft') || keys.has('ShiftRight'));
      const speed = chapter === 1 ? 3.3 : running ? 7.1 : 4.8;
      moveX = (-Math.sin(yaw) * forward + Math.cos(yaw) * right) / length * speed * dt;
      moveZ = (-Math.cos(yaw) * forward - Math.sin(yaw) * right) / length * speed * dt;
      const beforeX = player.x;
      const beforeZ = player.z;
      if (!isBlocked(player.x + moveX, player.z)) player.x += moveX;
      if (!isBlocked(player.x, player.z + moveZ)) player.z += moveZ;
      moving = Math.hypot(player.x - beforeX, player.z - beforeZ) > 0.0001;
      if (moving) {
        const facing = Math.atan2(-moveX, -moveZ);
        if (view === 'third') avatar.rotation.y += Math.atan2(Math.sin(facing - avatar.rotation.y), Math.cos(facing - avatar.rotation.y)) * Math.min(1, dt * 14);
        stride += dt * (chapter ? 6.5 : running ? 12 : 9);
      }
    } else moving = false;
    const amplitude = moving ? chapter ? 0.32 : 0.56 : 0;
    legs[0].rotation.x = THREE.MathUtils.damp(legs[0].rotation.x, Math.sin(stride) * amplitude, 15, dt);
    legs[1].rotation.x = THREE.MathUtils.damp(legs[1].rotation.x, -Math.sin(stride) * amplitude, 15, dt);
    // With the walker both hands stay on the handles.
    arms[0].rotation.x = chapter ? 0.72 : -legs[0].rotation.x * 0.8;
    arms[1].rotation.x = chapter ? 0.72 : -legs[1].rotation.x * 0.8;
    body.position.y = moving ? Math.abs(Math.sin(stride)) * 0.035 : Math.sin(elapsed * 1.8) * 0.009;
    avatar.position.copy(player);
    if (view === 'first') {
      // The body follows the view so the walker stays in front of the eyes.
      avatar.rotation.y = yaw;
      const bob = moving && !reduceMotion ? Math.sin(stride * 2) * 0.03 : 0;
      camera.position.set(player.x, (chapter ? 1.5 : 1.62) + bob, player.z);
      cameraLook.set(
        player.x - Math.sin(yaw) * Math.cos(lookPitch),
        camera.position.y + Math.sin(lookPitch),
        player.z - Math.cos(yaw) * Math.cos(lookPitch),
      );
      camera.lookAt(cameraLook);
    } else {
      cameraLook.lerp(direction.set(player.x, 1.32, player.z), 1 - Math.exp(-dt * 12));
      const radius = 9.4;
      desiredCamera.set(player.x + Math.sin(yaw) * Math.cos(pitch) * radius, 1.4 + Math.sin(pitch) * radius, player.z + Math.cos(yaw) * Math.cos(pitch) * radius);
      direction.subVectors(desiredCamera, cameraLook);
      const cameraDistance = direction.length();
      raycaster.set(cameraLook, direction.normalize());
      raycaster.far = cameraDistance;
      const obstruction = raycaster.intersectObjects(cameraSolids, true)[0];
      if (obstruction) desiredCamera.copy(cameraLook).addScaledVector(direction, Math.max(1.1, obstruction.distance - 0.5));
      // Retract immediately on collision, then ease out as the space opens up.
      camera.position.lerp(desiredCamera, obstruction ? 1 : 1 - Math.exp(-dt * 9));
      camera.lookAt(cameraLook);
    }
    markers.forEach(marker => {
      marker.diamond.rotation.y = elapsed * 0.8;
      marker.diamond.position.y = 2.65 + Math.sin(elapsed * 2 + marker.group.position.x) * 0.1;
      marker.ring.scale.setScalar(marker.id === targetId ? 1.05 + Math.sin(elapsed * 2) * 0.04 : 1);
    });
    targetRing.scale.setScalar(1 + Math.sin(elapsed * 2) * 0.04);
    renderer.render(scene, camera);
    if (elapsed - lastReport > 0.1) {
      lastReport = elapsed;
      onUpdate?.(getSnapshot());
    }
  }

  function dispose() {
    disposed = true;
    cancelAnimationFrame(frame);
    observer.disconnect();
    window.removeEventListener('keydown', onKeyDown);
    window.removeEventListener('keyup', onKeyUp);
    window.removeEventListener('blur', onBlur);
    document.removeEventListener('visibilitychange', onVisibility);
    canvas.removeEventListener('pointerdown', onPointerDown);
    canvas.removeEventListener('pointermove', onPointerMove);
    canvas.removeEventListener('pointerup', onPointerUp);
    canvas.removeEventListener('pointercancel', onPointerUp);
    canvas.removeEventListener('lostpointercapture', onPointerUp);
    const disposedGeometries = new Set();
    const disposedMaterials = new Set();
    scene.traverse(object => {
      if (object.geometry && !disposedGeometries.has(object.geometry)) {
        object.geometry.dispose();
        disposedGeometries.add(object.geometry);
      }
      const list = Array.isArray(object.material) ? object.material : [object.material];
      list.filter(Boolean).forEach(material => {
        if (!disposedMaterials.has(material)) {
          material.dispose();
          disposedMaterials.add(material);
        }
      });
    });
    textures.forEach(texture => texture.dispose());
    renderer.dispose();
  }

  frame = requestAnimationFrame(animate);
  return { setActive, setChapter, setTarget, setVisited, setView, getSnapshot, dispose };
}
