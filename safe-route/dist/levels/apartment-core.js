// 103동 ground floor and stairwell: lobby (glass entrance, mailboxes, notice board, 관리사무소 window,
// parcel lockers, elevator, fire door), and the switchback stairwell from 1F to the 4F landing.
// The stairs are walkable to the 3F landing; the flight above 3F is roped off for wet cleaning.
import * as THREE from 'three';
import { FH } from './apartment-parts.js';
import { FONT } from '../engine/kit.js';

const yOf = (n) => FH * (n - 1);
export const STAIR = { x0: 3.4, x1: 6.6, laneA: [5.1, 6.6], laneB: [3.4, 4.9], zLand: 1.4, zMid: 3.6, zEnd: 5.0, top: 11.2 };

export function buildLobby(B, g) {
  const { kit, M } = B;
  g.add(kit.slab(10, 0.2, 6.6, M.lobbyFloor, { x: 4, y: -0.2, z: -3.3 }));
  B.floor(-1, 9, -6.5, 0, 0);
  g.add(kit.slab(10.2, 0.08, 6.7, M.ceiling, { x: 4, y: 2.6, z: -3.3 }));
  g.add(kit.slab(10.6, 0.45, 7.0, M.concrete, { x: 4, y: 2.68, z: -3.45 })); // roof of the entrance volume
  B.wallX(g, -1, -6.5, 0, 0, 2.6, M.lobbyWall, { side: 1 });
  const win = [{ a: -5.2, b: -3.0, y0: 0.95, y1: 2.0 }];
  B.wallX(g, 9, -6.5, 0, 0, 2.6, M.lobbyWall, { side: -1, holes: win });
  B.wallZ(g, 0, -1, 9, 0, 2.6, M.lobbyWall, { side: -1, holes: [{ a: 0.6, b: 1.8, y0: 0, y1: 2.1 }, { a: 4.55, b: 5.45, y0: 0, y1: 2.1, door: true }] });
  for (const [x0, x1, z] of [[-0.9, 8.9, -0.1], [-0.9, 8.9, -6.4]]) g.add(kit.box(x1 - x0, 0.09, 0.015, M.stone, { x: (x0 + x1) / 2, z, cast: false }));

  // North glass front with the double entrance doors (closed; 'lobbyExit' leads out).
  for (const x of [-0.95, 1, 3, 5, 7, 8.95]) g.add(kit.box(0.1, 2.6, 0.14, M.alu, { x, z: -6.5 }));
  g.add(kit.box(10, 0.35, 0.12, M.alu, { x: 4, y: 2.25, z: -6.5 }));
  g.add(kit.box(10, 0.1, 0.12, M.alu, { x: 4, z: -6.5 }));
  for (const [a, b] of [[-0.9, 0.95], [1.05, 2.95], [5.05, 6.95], [7.05, 8.9]]) g.add(kit.box(b - a, 2.15, 0.012, M.glass, { x: (a + b) / 2, y: 0.1, z: -6.5, cast: false }));
  for (const x of [3.5, 4.5]) { // door leaves: aluminium frame, glass, long pull bar
    g.add(kit.box(0.96, 2.15, 0.012, M.glass, { x, y: 0.1, z: -6.5, cast: false }));
    for (const dx of [-0.46, 0.46]) g.add(kit.box(0.05, 2.15, 0.05, M.alu, { x: x + dx, y: 0.1, z: -6.5 }));
    g.add(kit.box(0.96, 0.12, 0.05, M.alu, { x, y: 0.1, z: -6.5 }));
    B.rod(g, [x + (x < 4 ? 0.36 : -0.36), 0.75, -6.42], [x + (x < 4 ? 0.36 : -0.36), 1.45, -6.42], 0.014, M.metal);
  }
  B.solid(-1, 9, -6.62, -6.38, 0, 2.6);
  B.exitSign(g, 4, 2.42, -6.38, 0, 'exit');
  g.add(kit.box(2.6, 0.015, 1.3, M.rubber, { x: 4, z: -5.75, cast: false })); // entrance mat

  // Mailboxes (60 units, 15 floors x 4 lines) and the notice board on the west wall.
  const mail = B.tex('mailboxes', 768, 384, (c, w, h) => {
    const gr = c.createLinearGradient(0, 0, w, h); gr.addColorStop(0, '#b8bcbf'); gr.addColorStop(1, '#8f9497'); c.fillStyle = gr; c.fillRect(0, 0, w, h);
    const cols = 12, rows = 5, cw = w / cols, rh = h / rows;
    c.font = `600 ${rh * 0.16}px ${FONT}`; c.textAlign = 'center';
    for (let r = 0; r < rows; r++) for (let k = 0; k < cols; k++) {
      const i = r * cols + k, fl = 15 - Math.floor(i / 4), unit = (i % 4) + 1;
      c.strokeStyle = '#6d7275'; c.lineWidth = 2; c.strokeRect(k * cw + 3, r * rh + 3, cw - 6, rh - 6);
      c.fillStyle = '#3d4245'; c.fillRect(k * cw + cw * 0.2, r * rh + rh * 0.3, cw * 0.6, rh * 0.06);
      c.fillStyle = '#2a2d2f'; c.fillText(`${fl}0${unit}`, k * cw + cw / 2, r * rh + rh * 0.78);
    }
  });
  g.add(kit.box(0.26, 1.4, 2.8, new THREE.MeshStandardMaterial({ map: mail, roughness: 0.35, metalness: 0.6 }), { x: -0.77, y: 0.5, z: -4.5 }));
  B.solid(-0.9, -0.62, -5.9, -3.1, 0, 1.9);
  const notice = B.tex('lobbyNotice', 600, 400, (c, w, h) => {
    c.fillStyle = '#8a6a48'; c.fillRect(0, 0, w, h); c.fillStyle = '#c9a77c'; c.fillRect(14, 54, w - 28, h - 68);
    c.fillStyle = '#ffffff'; c.font = `800 34px ${FONT}`; c.textAlign = 'center'; c.fillText('103동 게시판', w / 2, 40);
    const notes = [['관리비 고지서 배부', '이번 달 납부 마감 25일'], ['엘리베이터 정기 점검', '목요일 10:00 ~ 12:00'], ['분리수거 요일', '화 · 금 19시 이후'], ['주차 등록 안내', '관리사무소 방문']];
    notes.forEach(([t, s], i) => {
      const x = 30 + (i % 2) * 285, y = 70 + Math.floor(i / 2) * 160;
      c.fillStyle = i === 1 ? '#fff7d6' : '#ffffff'; c.fillRect(x, y, 255, 140);
      c.fillStyle = '#c4161c'; c.beginPath(); c.arc(x + 127, y + 8, 6, 0, 7); c.fill();
      c.fillStyle = '#222'; c.font = `700 25px ${FONT}`; c.fillText(t, x + 127, y + 52); c.font = `500 20px ${FONT}`; c.fillStyle = '#555'; c.fillText(s, x + 127, y + 92);
    });
  });
  const nb = new THREE.Mesh(new THREE.PlaneGeometry(1.5, 1.0), new THREE.MeshStandardMaterial({ map: notice, roughness: 0.8 }));
  nb.position.set(-0.88, 1.45, -1.6); nb.rotation.y = Math.PI / 2; g.add(nb);
  B.point('lobbyNotice', '게시판 보기', -0.4, 0, -1.6, 'notice');

  // Parcel lockers (무인택배함) with a small screen.
  const lockers = B.tex('lockers', 512, 512, (c, w, h) => {
    c.fillStyle = '#d7dadc'; c.fillRect(0, 0, w, h);
    for (let r = 0; r < 6; r++) for (let k = 0; k < 4; k++) { c.strokeStyle = '#8b9093'; c.lineWidth = 4; c.strokeRect(k * 128 + 6, r * 85 + 6, 116, 73); c.fillStyle = '#5c6164'; c.fillRect(k * 128 + 98, r * 85 + 36, 10, 14); }
    c.fillStyle = '#1f6fb2'; c.fillRect(0, 0, w, 22); c.fillStyle = '#fff'; c.font = `700 16px ${FONT}`; c.textAlign = 'center'; c.fillText('무인택배함', w / 2, 17);
  });
  g.add(kit.box(2.0, 1.9, 0.5, new THREE.MeshStandardMaterial({ map: lockers, roughness: 0.45, metalness: 0.3 }), { x: 7.8, z: -0.35 }));
  g.add(kit.box(0.3, 0.2, 0.01, B.onPower(B.glowMat('#7fc4ff', 1.4)), { x: 7.2, y: 1.15, z: -0.605, cast: false }));
  B.solid(6.8, 8.8, -0.6, -0.1, 0, 1.9);

  // Fire alarm set (bell + always-on red location lamp) by the stair door, potted plants, CCTV dome.
  g.add(kit.box(0.32, 0.5, 0.1, M.red, { x: 6.2, y: 1.0, z: -0.15 }));
  g.add(kit.cyl(0.07, 0.07, 0.04, M.metal, { x: 6.2, y: 1.35, z: -0.2, rx: Math.PI / 2 }));
  g.add(kit.cyl(0.035, 0.035, 0.03, B.redLamp ||= kit.lit('emergency', B.glowMat('#ff2a1a', 3), 3), { x: 6.2, y: 1.52, z: -0.2 }));
  for (const x of [0, 8]) { g.add(kit.cyl(0.24, 0.19, 0.5, M.stone, { x, z: -5.9 })); const f = new THREE.Mesh(new THREE.IcosahedronGeometry(0.42, 1), kit.mat('foliage')); f.position.set(x, 0.95, -5.9); f.scale.set(1, 1.25, 1); f.castShadow = true; g.add(f); }
  const dome = new THREE.Mesh(new THREE.SphereGeometry(0.07, 12, 8, 0, Math.PI * 2, 0, Math.PI / 2), M.darkMetal); dome.rotation.x = Math.PI; dome.position.set(8.5, 2.6, -6.0); g.add(dome);
  for (const [x, z] of [[1.5, -2], [4, -2], [6.5, -2], [1.5, -4.6], [4, -4.6], [6.5, -4.6]]) g.add(kit.cyl(0.11, 0.11, 0.015, B.lobbyLamp ||= B.onPower(B.glowMat('#fff3e2', 3.2)), { x, y: 2.585, z, cast: false }));
  B.exitSign(g, 5.0, 2.42, -0.12, Math.PI, 'exit'); // above the stair door, lobby side (faces north)

  // 관리사무소: window with counter, desk, CCTV monitor, binders; 박 소장 sits behind it.
  const office = new THREE.Group(); g.add(office);
  office.add(kit.slab(3.5, 0.2, 5, M.wood, { x: 10.75, y: -0.2, z: -4 }));
  office.add(kit.slab(3.5, 0.08, 5, M.ceiling, { x: 10.75, y: 2.6, z: -4 }));
  B.wallX(office, 9, -6.5, -1.5, 0, 2.6, M.paintWhite, { side: 1, holes: win, solid: false });
  B.wallX(office, 12.5, -6.5, -1.5, 0, 2.6, M.paintWhite, { side: -1, solid: false });
  B.wallZ(office, -6.5, 9, 12.5, 0, 2.6, M.paintWhite, { side: 1, solid: false });
  B.wallZ(office, -1.5, 9, 12.5, 0, 2.6, M.paintWhite, { side: -1, solid: false });
  B.window(office, 'x', 9, -5.2, -3.0, 0.95, 2.0, { mat: M.alu, split: 2 });
  office.add(kit.box(0.5, 0.04, 2.3, M.lightWood, { x: 9.0, y: 0.93, z: -4.1 }));
  office.add(kit.box(0.7, 0.74, 2.0, M.lightWood, { x: 9.5, z: -4.1 }));
  const cctv = B.tex('cctv', 256, 160, (c, w, h) => {
    for (let i = 0; i < 4; i++) { const x = (i % 2) * w / 2, y = Math.floor(i / 2) * h / 2; const gr = c.createLinearGradient(x, y, x, y + h / 2); gr.addColorStop(0, '#5d6668'); gr.addColorStop(1, '#2d3335'); c.fillStyle = gr; c.fillRect(x + 2, y + 2, w / 2 - 4, h / 2 - 4); c.fillStyle = '#9aa3a5'; c.fillRect(x + 20 + i * 9, y + 40, 40, 6); c.fillStyle = '#fff'; c.font = `600 11px ${FONT}`; c.fillText(['현관', '주차장', '엘리베이터', '놀이터'][i], x + 8, y + 16); }
  });
  office.add(kit.box(0.05, 0.36, 0.55, M.black, { x: 9.45, y: 0.82, z: -4.6 }));
  office.add(kit.box(0.01, 0.32, 0.5, B.onPower(new THREE.MeshStandardMaterial({ color: 0x050505, emissive: 0xffffff, emissiveMap: cctv, emissiveIntensity: 1.3 })), { x: 9.48, y: 0.84, z: -4.6, cast: false }));
  office.add(kit.box(0.5, 0.45, 0.5, M.darkMetal, { x: 10.4, z: -4.1 })); // chair seat block
  office.add(kit.box(0.08, 0.6, 0.5, M.darkMetal, { x: 10.68, y: 0.45, z: -4.1 }));
  const bind = [];
  for (let i = 0; i < 28; i++) bind.push({ x: 12.3, y: 0.3 + Math.floor(i / 14) * 0.7 + 0.15, z: -5.8 + (i % 14) * 0.08, sy: 0.9 + (i % 3) * 0.1 });
  office.add(kit.box(0.35, 1.9, 1.3, M.lightWood, { x: 12.3, z: -5.35 }));
  const bm = kit.instanced(new THREE.BoxGeometry(0.28, 0.3, 0.07), kit.mat('paint:#2f5d8c', { roughness: 0.6 }), bind, { cast: false });
  bind.forEach((_, i) => bm.setColorAt(i, new THREE.Color(['#2f5d8c', '#8c2f3a', '#3d7a4a', '#d8b23a'][i % 4])));
  office.add(bm);
  office.add(kit.box(1.1, 0.05, 1.1, B.onPower(B.glowMat('#fff3e2', 2.5)), { x: 10.75, y: 2.55, z: -4, cast: false }));
  B.plate(g, '관리사무소', { x: 8.88, y: 2.28, z: -4.1, ry: -Math.PI / 2, w: 0.9, h: 0.22, bg: '#2f4e6e', fg: '#ffffff' });
  B.point('managerWindow', '관리사무소 창구', 8.4, 0, -4.1, 'npc');

  // Outside: the 103동 entrance canopy over a short slice of paving.
  g.add(kit.slab(6.2, 0.25, 3.2, M.exterior, { x: 4, y: 2.85, z: -8.1 }));
  g.add(kit.plane(16, 7, M.paving, { x: 4, y: 0.005, z: -10 }));
  for (const x of [2.5, 5.5]) g.add(kit.cyl(0.12, 0.12, 0.02, B.lobbyLamp, { x, y: 2.835, z: -8.2, cast: false }));
  B.plate(g, '103동', { x: 4, y: 2.97, z: -9.71, ry: Math.PI, w: 1.1, h: 0.22, bg: '#e6e1d6', fg: '#2f4e6e' });
  g.add(kit.box(0.14, 1.2, 0.1, M.darkMetal, { x: 5.5, z: -6.75 })); // 공동현관 keypad post
  g.add(kit.box(0.1, 0.06, 0.012, B.onPower(B.glowMat('#62c1ff', 1.5)), { x: 5.5, y: 1.12, z: -6.81, cast: false }));
  B.point('lobbyExit', '밖으로 나가기', 4, 0, -6.0, 'door');
  B.point('elevator', '엘리베이터', 1.2, 0, -0.6, 'elevator');
  B.point('stairs1F', '계단', 5.0, 0, -0.7, 'stairs');
}

// Elevator front at floor level y: brushed doors, surround, floor indicator (shared canvas), call buttons.
export function buildElevator(B, g, y, H) {
  const { kit, M } = B;
  for (const x of [0.525, 1.875]) g.add(kit.box(0.15, 2.35, 0.06, M.metal, { x, y, z: -0.12 }));
  g.add(kit.box(1.5, 0.25, 0.06, M.metal, { x: 1.2, y: y + 2.1, z: -0.12 }));
  g.add(kit.box(1.2, 2.1, 0.02, M.darkMetal, { x: 1.2, y, z: -0.09, cast: false }));
  for (const x of [0.9, 1.5]) g.add(kit.box(0.595, 2.09, 0.03, kit.mat('metal', { roughness: 0.28, color: 0xb9bec2 }), { x, y, z: -0.115 }));
  g.add(kit.box(1.2, 0.012, 0.12, M.alu, { x: 1.2, y, z: -0.16, cast: false }));
  if (!H.elevMat) {
    const c = document.createElement('canvas'); c.width = 128; c.height = 64;
    H.elevCanvas = c; H.elevTex = B.kit.texture(c, { repeat: [1, 1] });
    H.elevMat = B.onPower(new THREE.MeshStandardMaterial({ color: 0x050505, emissive: 0xffffff, emissiveMap: H.elevTex, emissiveIntensity: 1.8, roughness: 0.2 }));
    H.elev = { floor: 7, target: 1, wait: 0, step: 0 };
    drawElevator(H);
    B.ticks.push((dt) => tickElevator(H, dt));
  }
  g.add(kit.box(0.4, 0.17, 0.03, M.black, { x: 1.2, y: y + 2.14, z: -0.165 }));
  g.add(kit.box(0.36, 0.13, 0.005, H.elevMat, { x: 1.2, y: y + 2.16, z: -0.183, cast: false }));
  g.add(kit.box(0.1, 0.24, 0.02, M.metal, { x: 2.1, y: y + 0.95, z: -0.11 }));
  const btn = B.onPower(B.glowMat('#ffb347', 1.2));
  for (const dy of [0.05, 0.15]) g.add(kit.cyl(0.022, 0.022, 0.012, btn, { x: 2.1, y: y + 0.95 + dy, z: -0.12, rx: Math.PI / 2, cast: false }));
  B.solid(0.6, 1.8, -0.2, 0.1, y, y + 2.2);
}
function drawElevator(H) {
  const c = H.elevCanvas.getContext('2d'), e = H.elev;
  c.fillStyle = '#000'; c.fillRect(0, 0, 128, 64);
  c.fillStyle = '#ff7a1a'; c.font = `700 44px ${FONT}`; c.textAlign = 'center'; c.textBaseline = 'middle';
  c.fillText(String(e.floor), 80, 34);
  if (e.target !== e.floor) { const up = e.target > e.floor; c.beginPath(); c.moveTo(30, up ? 16 : 48); c.lineTo(44, up ? 36 : 28); c.lineTo(16, up ? 36 : 28); c.closePath(); c.fill(); }
  H.elevTex.needsUpdate = true;
}
function tickElevator(H, dt) { // the car wanders between floors; one floor per 1.6 s, then waits
  const e = H.elev;
  if (e.floor === e.target) { e.wait -= dt; if (e.wait <= 0) { e.target = 1 + Math.floor(Math.random() * 15); e.wait = 4 + Math.random() * 6; drawElevator(H); } return; }
  e.step += dt;
  if (e.step >= 1.6) { e.step = 0; e.floor += Math.sign(e.target - e.floor); drawElevator(H); }
}

// The stairwell: walls, landings, flights (8 risers x 0.175 m, treads 0.275 m), balustrades, painted floor
// numbers, motion-sensor lamps under each landing and battery emergency lamps on the walls.
export function buildStairs(B, g, dyn) {
  const { kit, M } = B, S = STAIR;
  for (const n of [1, 2, 3, 4]) { // fire doors: 1F opens toward the lobby, 3F into the stairwell; 2F/4F stay shut
    const auto = n === 1 || n === 3;
    const D = B.door(auto ? dyn : g, 'z', 0, 4.55, 5.45, yOf(n), 2.1, M.fireDoor, { hinge: 'a', dir: n === 1 ? -1 : 1 }); // 2F/4F keep a solid: the wall's door gap runs full height
    if (auto) B.autoDoor(D);
  }
  B.wallX(g, 3.3, 0, 5.1, -0.2, S.top, M.stairWall, { side: 1 });
  B.wallX(g, 6.7, 0, 5.1, -0.2, S.top, M.stairWall, { side: -1 });
  B.wallZ(g, 5.1, 3.3, 6.7, -0.2, S.top, M.stairWall, { side: -1, holes: [1, 2, 3].map(n => ({ a: 4.3, b: 5.7, y0: yOf(n) + 2.35, y1: yOf(n) + 3.45 })) });
  B.wallZ(g, 0, 3.3, 6.7, -0.2, S.top, M.stairWall, { side: 1, holes: [1, 2, 3, 4].map(n => ({ a: 4.55, b: 5.45, y0: yOf(n), y1: yOf(n) + 2.1, door: n === 1 || n === 3 })) });
  g.add(kit.slab(3.4, 0.2, 5.1, M.ceiling, { x: 5, y: S.top, z: 2.55 }));
  for (const n of [1, 2, 3]) B.window(g, 'z', 5.05, 4.3, 5.7, yOf(n) + 2.35, yOf(n) + 3.45, { mat: M.alu });

  const balusters = [], A = S.laneA, Bl = S.laneB;
  const flight = (y0, lane) => {
    const [xa, xb] = lane === 'A' ? A : Bl, w = xb - xa, cx = (xa + xb) / 2;
    for (let i = 0; i < 8; i++) {
      const top = y0 + (i + 1) * 0.175, z0 = lane === 'A' ? S.zLand + i * 0.275 : S.zMid - (i + 1) * 0.275;
      g.add(kit.slab(w, 0.375, 0.275, M.step, { x: cx, y: top - 0.375, z: z0 + 0.1375 }));
      g.add(kit.box(w - 0.12, 0.006, 0.05, M.nosing, { x: cx, y: top, z: lane === 'A' ? z0 + 0.03 : z0 + 0.245, cast: false }));
      const bx = lane === 'A' ? xa + 0.04 : xb - 0.04;
      balusters.push({ x: bx, y: top + 0.45, z: z0 + 0.1375, sy: 0.9 });
    }
    const L = Math.hypot(2.2, 1.4), ang = Math.atan2(1.4, 2.2);
    const soffit = new THREE.Mesh(new THREE.BoxGeometry(w, 0.16, L + 0.12), M.step);
    soffit.position.set(cx, y0 + 0.7 - 0.27, (S.zLand + S.zMid) / 2); soffit.rotation.x = lane === 'A' ? -ang : ang;
    soffit.castShadow = soffit.receiveShadow = true; g.add(soffit);
    const rx = lane === 'A' ? xa + 0.04 : xb - 0.04, wx = lane === 'A' ? xb - 0.06 : xa + 0.06;
    const lo = lane === 'A' ? [S.zLand, y0] : [S.zMid, y0 + 1.4], hi = lane === 'A' ? [S.zMid, y0 + 1.4] : [S.zLand, y0 + 2.8];
    B.rod(g, [rx, lo[1] + 0.9, lo[0]], [rx, hi[1] + 0.9, hi[0]], 0.028, M.darkWood);
    B.rod(g, [wx, lo[1] + 0.85, lo[0]], [wx, hi[1] + 0.85, hi[0]], 0.022, M.rail);
  };
  for (let n = 1; n <= 4; n++) {
    const y = yOf(n);
    g.add(kit.slab(3.2, 0.2, 1.3, M.step, { x: 5, y: y - 0.2, z: 0.75 }));
    g.add(kit.box(3.2, 0.1, 0.012, M.stairBand, { x: 5, y, z: 0.107, cast: false }));
    for (const x of [3.407, 6.593]) g.add(kit.box(0.012, 1.0, 1.3, M.stairBand, { x, y, z: 0.75, cast: false }));
    B.plate(g, `${n}F`, { x: 3.95, y: y + 1.6, z: 0.106, w: 0.62, h: 0.44, bg: '#e9e5d8', fg: '#2f5a3c' });
    B.sensors.push({ x: 5.0, y: y + 2.58, z: 0.75, kind: 'sensor', floor: n });
    emergencyLamp(B, g, 3.41, y + 2.25, 0.75);
    if (n <= 3) B.floor(3.4, 6.6, 0.1, 1.6, y);
    if (n === 4) continue;
    g.add(kit.slab(3.2, 0.2, 1.4, M.step, { x: 5, y: y + 1.2, z: 4.3 }));
    for (const x of [3.407, 6.593]) g.add(kit.box(0.012, 0.9, 1.4, M.stairBand, { x, y: y + 1.4, z: 4.3, cast: false }));
    g.add(kit.box(3.2, 0.9, 0.012, M.stairBand, { x: 5, y: y + 1.4, z: 4.993, cast: false }));
    B.sensors.push({ x: 5.0, y: Math.min(y + 3.98, S.top - 0.02), z: 4.3, kind: 'sensor', floor: n + 0.5 });
    emergencyLamp(B, g, 3.41, y + 3.65, 4.3);
    flight(y, 'A'); flight(y, 'B');
    B.rod(g, [A[0] + 0.04, y + 2.3, S.zMid], [Bl[1] - 0.04, y + 2.3, S.zMid], 0.028, M.darkWood); // well guard at the mid landing
    if (n < 3) { B.ramp(A[0], A[1], 1.2625, 3.4625, 'z', y, y + 1.4); B.floor(3.4, 6.6, 3.4, 5.0, y + 1.4); B.ramp(Bl[0], Bl[1], 1.5375, 3.7375, 'z', y + 2.8, y + 1.4); }
  }
  for (const n of [1, 2, 3]) for (const zz of [S.zLand, S.zMid]) balusters.push({ x: 5.0, y: yOf(n) + (zz === S.zLand ? 0 : 1.4) + 0.45, z: zz, sy: 0.9 });
  g.add(kit.instanced(new THREE.BoxGeometry(0.022, 1, 0.022), M.rail, balusters, { cast: true }));

  // Lane divider between the flights, closet under the first flight, and the cleaning sign blocking 3F -> 4F.
  B.solid(4.9, 5.1, 1.4, 3.6, 0, S.top);
  g.add(kit.slab(1.5, 2.35, 0.06, M.stairWall, { x: 4.15, y: 0, z: 1.43 }));
  g.add(kit.box(0.7, 1.9, 0.02, M.fireDoor, { x: 4.15, y: 0.02, z: 1.465 }));
  B.solid(3.4, 4.9, 1.4, 3.6, 0, 1.3);
  const wet = new THREE.Group(); wet.position.set(5.85, yOf(3), 1.15); g.add(wet);
  const wetTex = B.tex('wetSign', 256, 384, (c, w, h) => { c.fillStyle = '#f2c230'; c.fillRect(0, 0, w, h); c.fillStyle = '#1b1b1b'; c.font = `800 46px ${FONT}`; c.textAlign = 'center'; c.fillText('물청소 중', w / 2, 120); c.font = `600 30px ${FONT}`; c.fillText('계단 이용 주의', w / 2, 180); c.beginPath(); c.moveTo(w / 2, 230); c.lineTo(w / 2 + 60, 340); c.lineTo(w / 2 - 60, 340); c.closePath(); c.lineWidth = 10; c.stroke(); });
  for (const s of [-1, 1]) { const p = new THREE.Mesh(new THREE.PlaneGeometry(0.34, 0.6), new THREE.MeshStandardMaterial({ map: wetTex, roughness: 0.5, side: THREE.DoubleSide })); p.position.set(0, 0.3, s * 0.1); p.rotation.x = s * 0.17; p.castShadow = true; wet.add(p); }
  B.solid(5.1, 6.6, 1.25, 1.75, yOf(3), yOf(4));
  B.exitSign(g, 5.0, 2.42, 0.12, 0, 'exit'); // above the 1F door, stair side
}

// 비상조명등: wall box with two lamp heads that light up on battery when the power fails.
function emergencyLamp(B, g, x, y, z) {
  const { kit, M } = B;
  B.emLampMat ||= B.onBackup(B.glowMat('#eaf2ff', 4), 4);
  g.add(kit.box(0.08, 0.12, 0.32, M.white, { x: x + 0.04, y, z }));
  for (const dz of [-0.1, 0.1]) g.add(kit.cyl(0.035, 0.03, 0.05, B.emLampMat, { x: x + 0.1, y: y - 0.02, z: z + dz, cast: false }));
  B.sensors.push({ x: x + 0.3, y: y - 0.05, z, kind: 'backup' });
}
