// Fences from town-plan FENCES: green welded-mesh panels, brick block walls, box hedges and low park
// railings, each with a blocking solid, plus invisible walls just outside BOUNDS so nobody walks off
// into the filler city.
import * as THREE from 'three';
import { BOUNDS } from '../game/townmap.js';
import { CURB, FENCES } from './town-plan.js';
import { lineSolid } from './town-util.js';

const Y = CURB;

export function buildFences(kit) {
  const group = new THREE.Group(); group.name = 'fences';
  const stat = new THREE.Group(); group.add(stat);
  const solids = [];

  const alphaMat = (key, draw, color, extra = {}) => kit.memo(`town:fence:${key}`, () => new THREE.MeshStandardMaterial({
    map: kit.texture(kit.canvas(256, 256, draw)), alphaTest: 0.5, side: THREE.DoubleSide, color, roughness: 0.5, metalness: 0.5, ...extra,
  }));
  // One texture = one 2.4 m panel: frame, vertical wires every ~5 cm, horizontal every ~20 cm, two V folds.
  const meshMat = alphaMat('mesh', (g, w, h) => {
    g.strokeStyle = '#ffffff'; g.lineWidth = 2;
    for (let x = 3; x < w; x += 6) { g.beginPath(); g.moveTo(x, 0); g.lineTo(x, h); g.stroke(); }
    for (let y = 4; y < h; y += 26) { g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.stroke(); }
    g.lineWidth = 5; for (const y of [h * 0.3, h * 0.72]) { g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.stroke(); }
    g.lineWidth = 10; g.strokeRect(5, 5, w - 10, h - 10);
  }, 0x2f6b49);
  const parkMat = alphaMat('park', (g, w, h) => {
    g.fillStyle = '#ffffff';
    for (let x = 6; x < w; x += 26) g.fillRect(x, h * 0.08, 7, h * 0.86);
    g.fillRect(0, h * 0.06, w, 14); g.fillRect(0, h * 0.82, w, 10);
    for (let x = 19; x < w; x += 52) { g.beginPath(); g.arc(x, h * 0.06, 10, 0, 7); g.fill(); }
  }, 0x2a2e33);
  const hedgeMat = kit.memo('town:hedge', () => {
    const r = kit.rng(31);
    const draw = (g, w, h, shade) => { g.fillStyle = shade ? '#404040' : '#2f4a26'; g.fillRect(0, 0, w, h); for (let i = 0; i < 2600; i++) { const v = r(); g.fillStyle = shade ? `rgb(${80 + v * 140 | 0},${80 + v * 140 | 0},${80 + v * 140 | 0})` : ['#3f6230', '#4d7337', '#36552a', '#5c7f3c', '#6b6a32'][(v * 5) | 0]; g.beginPath(); g.ellipse(r() * w, r() * h, 3 + r() * 4, 1.5 + r() * 2, r() * 3, 0, 7); g.fill(); } };
    return new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(256, 256, (g, w, h) => draw(g, w, h, false))), normalMap: kit.texture(kit.normalFrom(kit.canvas(256, 256, (g, w, h) => draw(g, w, h, true)), 3), { color: false }), roughness: 0.95 });
  });
  const brick = kit.surface('brick', { color: 0xe8ddd2 }), coping = kit.mat('curb'), post = kit.mat('paint:#2c5c40', { roughness: 0.45, metalness: 0.5 }), dark = kit.mat('darkMetal');

  const posts = [], ppost = [];
  // Vertical panel along a fence line; uv.x repeats every `per` metres.
  function panel(x0, z0, x1, z1, h, y0, mat, per) {
    const L = Math.hypot(x1 - x0, z1 - z0), g = new THREE.PlaneGeometry(L, h), uv = g.attributes.uv;
    for (let i = 0; i < uv.count; i++) uv.setX(i, uv.getX(i) * L / per);
    const m = new THREE.Mesh(g, mat);
    m.position.set((x0 + x1) / 2, y0 + h / 2, (z0 + z1) / 2); m.rotation.y = -Math.atan2(z1 - z0, x1 - x0);
    m.castShadow = true; m.receiveShadow = true;
    return m;
  }
  for (const line of FENCES) {
    const [x0, z0, x1, z1, kind] = line, L = Math.hypot(x1 - x0, z1 - z0);
    if (L < 0.05) continue;
    solids.push({ ...lineSolid(line), id: `fence:${kind}` });
    const cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, alongX = Math.abs(x1 - x0) > Math.abs(z1 - z0), ry = alongX ? 0 : Math.PI / 2;
    const steps = (per, list) => { const n = Math.max(1, Math.round(L / per)); for (let i = 0; i <= n; i++) list.push({ x: x0 + (x1 - x0) * i / n, y: Y, z: z0 + (z1 - z0) * i / n }); };
    if (kind === 'mesh') {
      stat.add(panel(x0, z0, x1, z1, 1.7, Y + 0.06, meshMat, 2.4));
      stat.add(kit.box(L, 0.05, 0.05, post, { x: cx, y: Y + 1.74, z: cz, ry }));
      steps(2.4, posts);
    } else if (kind === 'park') {
      stat.add(panel(x0, z0, x1, z1, 1.05, Y, parkMat, 2.0));
      steps(2.0, ppost);
    } else if (kind === 'wall') {
      stat.add(kit.slab(L + 0.2, 1.8, 0.2, brick, { x: cx, y: Y - 0.05, z: cz, ry }));
      stat.add(kit.box(L + 0.3, 0.07, 0.3, coping, { x: cx, y: Y + 1.75, z: cz, ry }));
    } else if (kind === 'hedge') {
      stat.add(kit.slab(L + 0.3, 1.05, 0.75, hedgeMat, { x: cx, y: Y, z: cz, ry }));
      stat.add(kit.slab(L + 0.1, 0.18, 0.62, hedgeMat, { x: cx, y: Y + 1.04, z: cz, ry }));
    } else console.error(`[town] unknown fence kind "${kind}"`);
  }
  if (posts.length) group.add(kit.instanced(new THREE.BoxGeometry(0.06, 1.85, 0.06).translate(0, 0.92, 0), post, posts));
  if (ppost.length) group.add(kit.instanced(new THREE.BoxGeometry(0.07, 1.12, 0.07).translate(0, 0.56, 0), dark, ppost));

  // Invisible edge of the playable town.
  const { x0, x1, z0, z1 } = BOUNDS;
  solids.push({ x0: x0 - 2, x1: x0 + 0.4, z0: z0 - 2, z1: z1 + 2, y0: -5, y1: 20, id: 'bounds' }, { x0: x1 - 0.4, x1: x1 + 2, z0: z0 - 2, z1: z1 + 2, y0: -5, y1: 20, id: 'bounds' },
    { x0: x0 - 2, x1: x1 + 2, z0: z0 - 2, z1: z0 + 0.4, y0: -5, y1: 20, id: 'bounds' }, { x0: x0 - 2, x1: x1 + 2, z0: z1 - 0.4, z1: z1 + 2, y0: -5, y1: 20, id: 'bounds' });

  kit.merge(stat);
  return { group, solids };
}
