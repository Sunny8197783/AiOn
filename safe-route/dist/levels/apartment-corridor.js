// 3rd-floor corridor (복도식): doors 301-304 (numbers only), kitchen windows with security grilles, gas pipe and
// meters, the elevator, the stair fire door, the 옥내소화전 cabinet with a 분말소화기, everyday clutter, and the spot
// near 304 where the hallway fire can appear. 303's entry is modelled behind its door so 서연 can stand in it.
import * as THREE from 'three';
import { F3 } from './apartment-parts.js';
import { buildElevator } from './apartment-core.js';
import { FONT } from '../engine/kit.js';

export const DOORS = { 301: [-12.6, -11.65], 302: [-1.75, -0.8], 303: [9.0, 9.95], 304: [16.0, 16.95] };
const KITCHEN = { 301: [-10.4, -9.1], 302: [-6.2, -4.9], 303: [11.0, 12.3], 304: [19.9, 21.2] };
export const FIRE_SPOT = { x: 19.2, y: F3, z: -0.42 };

export function buildCorridor(B, g, dyn, H) {
  const { kit, M } = B, Y = F3, CY = Y + 2.4;
  g.add(kit.slab(38, 0.2, 2.1, M.corridorFloor, { x: 3, y: Y - 0.2, z: -0.95 }));
  B.floor(-16, 22, -2, 0, Y);
  g.add(kit.slab(38, 0.1, 2.1, M.ceiling, { x: 3, y: CY, z: -0.95 }));
  g.add(kit.slab(38.4, 0.3, 2.5, M.concrete, { x: 3, y: CY + 0.1, z: -1.05 }));

  const wins = [];
  for (let x = -15.6; x < 21; x += 4) wins.push({ a: x, b: Math.min(x + 3.2, 21.6), y0: Y + 0.95, y1: Y + 2.15 });
  B.wallZ(g, -1.9, -16, 22, Y - 0.2, CY + 0.1, M.corridorWall, { t: 0.25, side: -1, holes: wins });
  for (const w of wins) {
    B.window(g, 'z', -2.02, w.a, w.b, w.y0, w.y1, { mat: M.alu, split: 2 });
    g.add(kit.box(w.b - w.a, 0.03, 0.12, M.stone, { x: (w.a + w.b) / 2, y: Y + 0.92, z: -1.86 }));
  }
  const holes = [{ a: 0.6, b: 1.8, y0: Y, y1: Y + 2.1 }, { a: 4.55, b: 5.45, y0: Y, y1: Y + 2.1, door: true }];
  for (const [id, [a, b]] of Object.entries(DOORS)) holes.push({ a, b, y0: Y, y1: Y + 2.1, door: id === '302' || id === '303' });
  for (const [a, b] of Object.values(KITCHEN)) holes.push({ a, b, y0: Y + 1.1, y1: Y + 1.9 });
  B.wallZ(g, 0, -16, 22, Y - 0.2, CY + 0.1, M.corridorWall, { side: -1, holes });
  B.wallX(g, -16, -2, 0, Y - 0.2, CY + 0.1, M.corridorWall, { side: 1 });
  B.wallX(g, 22, -2, 0, Y - 0.2, CY + 0.1, M.corridorWall, { side: -1 });
  for (const z of [-0.106, -1.894]) g.add(kit.box(38, 0.09, 0.012, M.skirting, { x: 3, y: Y, z, cast: false }));

  // Unit doors (steel, opening outward), number plates, digital locks, delivery boxes.
  for (const [id, [a, b]] of Object.entries(DOORS)) {
    const live = id === '302' || id === '303';
    const D = B.door(live ? dyn : g, 'z', 0, a, b, Y, 2.1, M.unitDoor, { hinge: id === '303' ? 'b' : 'a', dir: -1, lock: true, solid: live });
    if (id === '302') H.door302 = B.autoDoor(D, 1.4);
    if (id === '303') H.door303 = D;
    B.plate(g, id, { x: (a + b) / 2, y: Y + 2.3, z: -0.106, ry: Math.PI, w: 0.26, h: 0.12, bg: '#efe9dc', fg: '#33302b', border: '#8a8378' });
    g.add(kit.box(0.24, 0.3, 0.05, M.metal, { x: b + 0.32, y: Y + 1.0, z: -0.125 }));
  }
  // Kitchen windows onto the corridor: frosted, lit from inside while there is power; 방범창 grilles; gas meters.
  const meter = kit.mat('paint:#cfd2d0', { roughness: 0.5 });
  for (const [id, [a, b]] of Object.entries(KITCHEN)) {
    const pane = B.onPower(new THREE.MeshStandardMaterial({ color: 0xdfe4e2, emissive: 0xffd6a0, emissiveIntensity: id === '302' ? 0.15 : 0.3 + (id % 3) * 0.12, roughness: 0.4, transparent: true, opacity: 0.9 }));
    if (id === '301') H.candleWindow = pane;
    B.window(g, 'z', 0, a, b, Y + 1.1, Y + 1.9, { mat: M.alu, glass: pane, split: 2, depth: 0.08 });
    for (let x = a + 0.12; x < b - 0.05; x += 0.13) g.add(kit.box(0.016, 0.82, 0.016, M.darkMetal, { x, y: Y + 1.09, z: -0.17 }));
    for (const yy of [Y + 1.11, Y + 1.86]) g.add(kit.box(b - a + 0.06, 0.03, 0.03, M.darkMetal, { x: (a + b) / 2, y: yy, z: -0.17 }));
    g.add(kit.box(0.24, 0.3, 0.13, meter, { x: b + 0.35, y: Y + 1.55, z: -0.165 }));
    B.rod(g, [b + 0.35, Y + 1.85, -0.07], [b + 0.35, Y + 2.25, -0.07], 0.014, M.yellowPipe);
  }
  B.rod(g, [-15.9, Y + 2.25, -0.07], [21.9, Y + 2.25, -0.07], 0.016, M.yellowPipe);

  buildElevator(B, g, Y, H);
  B.point('elevator', '엘리베이터', 1.2, Y, -0.6, 'elevator');
  B.point('stairs3F', '계단', 5.0, Y, -0.7, 'stairs');
  B.exitSign(g, 5.0, Y + 2.25, -0.13, Math.PI, 'exit');
  B.exitSign(g, -6.5, Y + 0.9, -0.12, Math.PI, 'pass', 'left');
  B.exitSign(g, 14.5, Y + 0.9, -0.12, Math.PI, 'pass', 'right');
  if (kit.quality !== 'low') { const gl = kit.lit('emergency', new THREE.PointLight(0x35ff7a, 1.2, 5, 2), 1.2); gl.position.set(5.0, Y + 2.0, -0.5); dyn.add(gl); }
  for (const x of [-14, -10, -6, -2, 2, 6, 10, 14, 18]) B.sensors.push({ x, y: CY - 0.02, z: -0.95, kind: 'sensor', floor: 3 });

  // 옥내소화전 cabinet (with bell and the always-lit red location lamp) and a 분말소화기 beside it.
  const hyd = B.tex('hydrant', 256, 448, (c, w, h) => {
    c.fillStyle = '#c4161c'; c.fillRect(0, 0, w, h); c.strokeStyle = '#7d0d10'; c.lineWidth = 6; c.strokeRect(10, 10, w - 20, h - 20);
    c.fillStyle = '#fff'; c.textAlign = 'center'; c.font = `800 64px ${FONT}`; c.fillText('소화전', w / 2, 120); c.font = `600 26px ${FONT}`; c.fillText('옥내소화전', w / 2, 168);
    c.strokeStyle = '#fff'; c.lineWidth = 8; c.beginPath(); c.arc(w / 2, 290, 70, 0, 7); c.stroke(); c.beginPath(); c.arc(w / 2, 290, 28, 0, 7); c.stroke();
    c.fillStyle = '#e8e8e8'; c.fillRect(w * 0.68, 382, 44, 16);
  });
  g.add(kit.box(0.85, 1.5, 0.18, M.red, { x: 6.125, y: Y + 0.25, z: -0.19 }));
  const face = new THREE.Mesh(new THREE.PlaneGeometry(0.8, 1.42), new THREE.MeshStandardMaterial({ map: hyd, roughness: 0.4, metalness: 0.2 }));
  face.position.set(6.125, Y + 1.0, -0.282); face.rotation.y = Math.PI; g.add(face);
  g.add(kit.cyl(0.08, 0.08, 0.05, M.metal, { x: 5.95, y: Y + 1.86, z: -0.2, rx: Math.PI / 2 }));
  g.add(kit.cyl(0.04, 0.04, 0.05, B.redLamp ||= kit.lit('emergency', B.glowMat('#ff2a1a', 3), 3), { x: 6.3, y: Y + 1.82, z: -0.2 }));
  B.solid(5.7, 6.55, -0.32, 0, Y, Y + 1.8);
  const ext = new THREE.Group(); ext.position.set(6.95, Y, -0.24); g.add(ext);
  const band = B.tex('extLabel', 256, 64, (c, w, h) => { c.fillStyle = '#f5f1e6'; c.fillRect(0, 0, w, h); c.fillStyle = '#b01318'; c.font = `800 30px ${FONT}`; c.textAlign = 'center'; c.fillText('분말소화기', w / 2, 44); });
  ext.add(kit.cyl(0.075, 0.075, 0.44, M.red, {}, 20), kit.cyl(0.05, 0.075, 0.04, M.red, { y: 0.44 }, 20), kit.cyl(0.02, 0.02, 0.06, M.darkMetal, { y: 0.48 }));
  ext.add(kit.cyl(0.0765, 0.0765, 0.14, new THREE.MeshStandardMaterial({ map: band, roughness: 0.5 }), { y: 0.16 }, 20));
  ext.add(kit.box(0.15, 0.018, 0.03, M.metal, { x: 0.03, y: 0.54 }), kit.box(0.12, 0.012, 0.03, M.metal, { x: 0.03, y: 0.51 }));
  ext.add(kit.cyl(0.022, 0.022, 0.012, M.white, { x: -0.035, y: 0.49, z: -0.03, rx: Math.PI / 2 }));
  B.rod(ext, [0.045, 0.49, 0], [0.09, 0.12, -0.03], 0.009, M.rubber);
  B.plate(g, '소화기', { x: 6.95, y: Y + 1.55, z: -0.106, ry: Math.PI, w: 0.3, h: 0.14, bg: '#c4161c', fg: '#ffffff' });
  B.solid(6.85, 7.05, -0.34, -0.12, Y, Y + 0.6);
  B.point('extinguisher', '소화기·소화전', 6.6, Y, -0.65, 'extinguisher');

  // Clutter: parcels at 301, a doormat and plant at 303, a shoe rack at 304.
  g.add(kit.box(0.5, 0.34, 0.4, M.cardboard, { x: -13.2, y: Y, z: -0.32, ry: 0.08 }), kit.box(0.36, 0.22, 0.3, M.cardboard, { x: -13.18, y: Y + 0.34, z: -0.3, ry: -0.1 }));
  g.add(kit.box(0.14, 0.004, 0.09, M.white, { x: -13.2, y: Y + 0.56, z: -0.3, cast: false }));
  B.solid(-13.5, -12.9, -0.55, -0.1, Y, Y + 0.6);
  g.add(kit.box(0.8, 0.012, 0.5, M.rubber, { x: 9.475, y: Y, z: -0.4, cast: false }));
  g.add(kit.cyl(0.16, 0.12, 0.34, kit.mat('paint:#d8d2c6'), { x: 10.75, y: Y, z: -0.3 }));
  const leaf = new THREE.Mesh(new THREE.IcosahedronGeometry(0.26, 1), kit.mat('foliage')); leaf.position.set(10.75, Y + 0.62, -0.3); leaf.scale.set(1, 1.3, 1); leaf.castShadow = true; g.add(leaf);
  for (let i = 0; i < 3; i++) g.add(kit.box(1.0, 0.02, 0.3, M.darkMetal, { x: 18.1, y: Y + 0.05 + i * 0.25, z: -0.25 }));
  for (const x of [17.62, 18.58]) g.add(kit.box(0.02, 0.6, 0.3, M.darkMetal, { x, y: Y, z: -0.25 }));
  const shoes = [];
  for (let i = 0; i < 7; i++) shoes.push({ x: 17.75 + (i % 4) * 0.24, y: Y + 0.11 + Math.floor(i / 4) * 0.25, z: -0.25, ry: (i % 2) * 0.2 });
  const sh = kit.instanced(new THREE.BoxGeometry(0.11, 0.08, 0.27), kit.mat('paint:#ffffff', { roughness: 0.7 }), shoes, { cast: false });
  shoes.forEach((_, i) => sh.setColorAt(i, new THREE.Color(['#2b2b2b', '#e8e4dc', '#7a4b2c', '#3d5a80', '#c94f4f', '#e8e4dc', '#2b2b2b'][i])));
  g.add(sh);
  B.solid(17.6, 18.6, -0.42, -0.1, Y, Y + 0.6);

  // The hallway-fire spot: recycling boxes and a newspaper bundle (normal / scorched), soot and extinguisher powder.
  const fp = new THREE.Group(); fp.position.set(FIRE_SPOT.x, Y, FIRE_SPOT.z); dyn.add(fp);
  const normal = new THREE.Group(), scorched = new THREE.Group(); fp.add(normal, scorched); scorched.visible = false;
  const char = kit.mat('paint:#2a211b', { roughness: 1 }), paper = kit.mat('paint:#d9d6cc', { roughness: 1 }), ash = kit.mat('paint:#4a4541', { roughness: 1 });
  const boxes = [[0.55, 0.4, 0.42, -0.1, 0, 0.02, 0.1], [0.4, 0.3, 0.34, -0.12, 0.4, 0.04, -0.2], [0.42, 0.18, 0.3, 0.38, 0, 0, 0.3]];
  boxes.forEach(([w, h, d, x, y, z, ry], i) => {
    normal.add(kit.box(w, h, d, i === 2 ? paper : M.cardboard, { x, y, z, ry }));
    scorched.add(kit.box(w * 0.96, h * (i === 1 ? 0.5 : 0.85), d * 0.96, i === 2 ? ash : char, { x, y, z, ry: ry + 0.05 }));
  });
  const soot = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 1.6), new THREE.MeshStandardMaterial({ map: B.tex('soot', 128, 128, blot('rgba(18,14,12,')), transparent: true, depthWrite: false, roughness: 1 }));
  soot.position.set(0, 0.85, 0.31); soot.rotation.y = Math.PI; soot.visible = false; fp.add(soot);
  const powder = new THREE.Mesh(new THREE.PlaneGeometry(2.4, 1.7), new THREE.MeshStandardMaterial({ map: B.tex('powder', 256, 256, blot('rgba(250,248,240,')), transparent: true, depthWrite: false, roughness: 1 }));
  powder.rotation.x = -Math.PI / 2; powder.position.set(-0.2, 0.006, -0.45); powder.visible = false; fp.add(powder);
  H.fire = { group: fp, normal, scorched, soot, powder };
  B.solid(FIRE_SPOT.x - 0.45, FIRE_SPOT.x + 0.65, -0.7, 0, Y, Y + 0.7);
  H.firePoint = B.point('hallFire', '304호 앞 복도', FIRE_SPOT.x - 0.4, Y, -1.0, 'fire', 3.0);

  // 303 entry behind its door (seen when 서연 opens it): tiles, step, shoe cabinet, a warm lamp.
  const e = new THREE.Group(); g.add(e);
  e.add(kit.slab(1.6, 0.2, 1.7, M.entryTile, { x: 9.5, y: Y - 0.2, z: 0.85 }), kit.slab(1.6, 0.15, 0.8, M.wood, { x: 9.5, y: Y, z: 1.3 }));
  B.wallX(e, 8.7, 0, 1.7, Y, CY, M.wallpaper, { side: 1, solid: false });
  B.wallX(e, 10.3, 0, 1.7, Y, CY, M.wallpaper, { side: -1, solid: false });
  B.wallZ(e, 1.7, 8.7, 10.3, Y, CY, M.wallpaper, { side: -1, solid: false });
  e.add(kit.slab(1.6, 0.1, 1.7, M.ceiling, { x: 9.5, y: CY - 0.1, z: 0.85 }), kit.box(0.35, 2.0, 1.1, M.white, { x: 10.1, y: Y, z: 0.8 }));
  e.add(kit.box(0.5, 0.4, 0.03, M.lightWood, { x: 9.3, y: Y + 1.3, z: 1.66 }), kit.cyl(0.15, 0.15, 0.02, B.onPower(B.glowMat('#ffdcae', 2.5)), { x: 9.5, y: CY - 0.12, z: 0.8, cast: false }));
  B.solid(9.0, 9.95, 0.05, 0.4, Y, Y + 2.1);
  H.seoyeonSpot = { x: 9.475, y: Y, z: 0.62, yaw: 0 };
  B.point('seoyeonDoor', '303호', 9.475, Y, -0.65, 'door');
  B.point('homeDoor', '302호 (집)', -1.275, Y, -0.6, 'door');
}

// Soft irregular blotch texture (alpha falls off from the centre), for soot and extinguisher powder.
function blot(rgba) {
  return (c, w, h) => {
    for (let i = 0; i < 70; i++) {
      const a = Math.random() * 6.28, r = Math.random() * w * 0.3, x = w / 2 + Math.cos(a) * r, y = h / 2 + Math.sin(a) * r * 0.8, s = w * (0.08 + Math.random() * 0.14);
      const gr = c.createRadialGradient(x, y, 0, x, y, s); gr.addColorStop(0, rgba + '0.22)'); gr.addColorStop(1, rgba + '0)');
      c.fillStyle = gr; c.fillRect(0, 0, w, h);
    }
  };
}
