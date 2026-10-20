import * as THREE from 'three';
import { FONT } from './engine/kit.js';

const AMBER = '#f6bb6e';
const TEAL = '#85c7bb';
const ease = t => t * t * (3 - 2 * t);
const clip = (text, max) => (text.length > max ? `${text.slice(0, max - 1)}…` : text);

// 3D pieces of the "다시 읽기" scene: scripted camera, 준's path, and labelled pins.
// The step order and every sentence live in story/seoyeon.js; this file only draws.
export function createReread(world, { start, reduceMotion = false }) {
  const group = new THREE.Group();
  world.scene.add(group);
  const owned = [];
  const own = item => { owned.push(item); return item; };
  const tickers = new Set();
  let cameraFn = null;
  world.setCameraOverride((camera, dt) => {
    cameraFn?.(camera, dt);
    tickers.forEach(tick => tick(dt));
  });

  // First person at 서연's start, easing the gaze down so the walker handles fill the lower frame.
  function eye(pitch) {
    let current = -0.35;
    cameraFn = (camera, dt) => {
      current += (pitch - current) * (reduceMotion ? 1 : 1 - Math.exp(-dt * 2.5));
      camera.position.set(start.x, 1.5, start.z);
      camera.lookAt(start.x - Math.sin(start.yaw) * Math.cos(current), 1.5 + Math.sin(current), start.z - Math.cos(start.yaw) * Math.cos(current));
    };
  }

  // Rise above the neighbourhood so both people's starts and all shelter entrances are in frame.
  // Nearly top-down so the buildings never hide entrance C.
  function crane(to = new THREE.Vector3(-25, 190, 95), target = new THREE.Vector3(-25, 0, 12)) {
    const from = new THREE.Vector3();
    const lookFrom = new THREE.Vector3();
    const look = new THREE.Vector3();
    let t = -1;
    cameraFn = (camera, dt) => {
      if (t < 0) { from.copy(camera.position); camera.getWorldDirection(lookFrom).multiplyScalar(8).add(from); t = 0; }
      t = reduceMotion ? 1 : Math.min(1, t + dt / 1.6);
      camera.position.lerpVectors(from, to, ease(t));
      camera.lookAt(look.lerpVectors(lookFrom, target, ease(t)));
    };
  }

  // 준's recorded steps as amber dots, with a translucent figure walking them once.
  function showPath(points) {
    const every = Math.max(1, Math.ceil(points.length / 120));
    const pts = points.filter((_, i) => i % every === 0);
    if (!pts.length) return;
    const material = own(new THREE.MeshBasicMaterial({ color: AMBER, transparent: true, opacity: 0.85, depthWrite: false }));
    const discs = own(new THREE.InstancedMesh(own(new THREE.CircleGeometry(1.1, 14)), material, pts.length));
    discs.frustumCulled = false; // bounds would be cached while only the first dot is shown
    const matrix = new THREE.Matrix4();
    const flat = new THREE.Quaternion().setFromEuler(new THREE.Euler(-Math.PI / 2, 0, 0));
    const one = new THREE.Vector3(1, 1, 1);
    pts.forEach((p, i) => discs.setMatrixAt(i, matrix.compose(new THREE.Vector3(p.x, 0.1, p.z), flat, one)));
    discs.count = reduceMotion ? pts.length : 0;
    group.add(discs);
    const ghost = new THREE.Mesh(own(new THREE.CylinderGeometry(0.6, 0.6, 2, 12)), own(new THREE.MeshBasicMaterial({ color: AMBER, transparent: true, opacity: 0.45, depthWrite: false })));
    group.add(ghost);
    let t = reduceMotion ? 1 : 0;
    const tick = dt => {
      t = Math.min(1, t + dt / 2.5);
      const i = Math.min(pts.length - 1, Math.floor(t * (pts.length - 1)));
      discs.count = i + 1;
      ghost.position.set(pts[i].x, 0.9, pts[i].z);
      if (t >= 1) tickers.delete(tick);
    };
    tickers.add(tick);
    tick(0);
  }

  function label(n, was, now) {
    const canvas = document.createElement('canvas');
    canvas.width = 1024;
    canvas.height = 300;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = 'rgba(16,28,30,.9)';
    ctx.fillRect(0, 0, 1024, 300);
    ctx.fillStyle = AMBER;
    ctx.beginPath();
    ctx.arc(70, 150, 44, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#16282a';
    ctx.font = `700 56px ${FONT}`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(String(n), 70, 152);
    ctx.textAlign = 'left';
    ctx.font = `700 46px ${FONT}`;
    ctx.fillStyle = AMBER;
    ctx.fillText(clip(was, 26), 140, 100, 860);
    ctx.fillStyle = TEAL;
    ctx.fillText(clip(now, 26), 140, 200, 860);
    const texture = own(new THREE.CanvasTexture(canvas));
    texture.colorSpace = THREE.SRGBColorSpace;
    const sprite = new THREE.Sprite(own(new THREE.SpriteMaterial({ map: texture, depthTest: false, fog: false })));
    sprite.renderOrder = 10;
    return sprite;
  }

  // A pole at the place the reason belongs, topped by "what the note said" over "what is true for 서연".
  function pin(n, at, was, now, scale = 2.6) {
    const sprite = label(n, was, now);
    sprite.position.set(at.x, 6.5 * scale, at.z);
    if (at.x < -95) sprite.center.x = 0.85; // C's label grows west so it clears A's
    sprite.scale.set(13 * scale, 3.8 * scale, 1);
    const pole = new THREE.Mesh(own(new THREE.CylinderGeometry(0.08, 0.08, 6 * scale, 6)), own(new THREE.MeshBasicMaterial({ color: AMBER })));
    pole.position.set(at.x, 3 * scale, at.z);
    group.add(sprite, pole);
  }

  // A flat amber strip on the ground between two places (e.g. the two starting points).
  function link(a, b) {
    const length = Math.hypot(b.x - a.x, b.z - a.z);
    const strip = new THREE.Mesh(own(new THREE.PlaneGeometry(0.35, length)), own(new THREE.MeshBasicMaterial({ color: AMBER, transparent: true, opacity: 0.7, depthWrite: false, side: THREE.DoubleSide })));
    strip.rotation.set(-Math.PI / 2, 0, Math.atan2(b.x - a.x, b.z - a.z));
    strip.position.set((a.x + b.x) / 2, 0.12, (a.z + b.z) / 2);
    group.add(strip);
  }

  function dispose() {
    world.setCameraOverride(null);
    tickers.clear();
    group.removeFromParent();
    owned.forEach(item => item.dispose());
  }

  return { eye, crane, showPath, pin, link, dispose };
}
