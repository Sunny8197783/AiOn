// 한결물류 연무센터 운영팀: a 2nd-floor open office at dusk. Two desk islands with dual monitors, the player's
// desk with the director-drawn dashboard (setState('dashboard', canvas)), the team lead's desk with 윤 팀장 (NPC
// 'coworker', seated), a 2x2 video wall with the route map (setState('alerts', 0..3) turns rows red), a whiteboard
// of delivery schedules, a water cooler, a printer, and a ribbon window over the yard: docks, idle trucks, parcels.
// Frame: floor x -9..9, z -12..0 at y 0; the yard lies north (-z) one storey down (y = -4.2). Door on the south wall.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { FONT } from '../engine/kit.js';
import { panels, lamps, windowView, moodFor, npc, solidOf, MOODS } from './commerce-goods.js';

const H = 2.75, GY = -4.2, DOOR = 6.8;
const ISL = [-4.6, -9.2], DX = [-5.8, -4.4, -3.0, -1.6], ME = { x: -1.6, z: ISL[0] };
const LEAD = { x: 7.55, z: -7.5 };
const ROUTES = [['서해안 노선', '운송 지연 · 내일부터'], ['거래처 휴업 알림', '3곳'], ['기사 결근 연락', '9명'], ['수도권 간선', '정상 운행'], ['중부 노선', '정상 운행'], ['영동 노선', '정상 운행']];
const NODES = [['연무센터', 390, 400, 26], ['수도권 허브', 210, 190, 18], ['서해안', 130, 540, 18], ['중부', 580, 300, 18], ['남부', 480, 620, 18], ['영동', 680, 160, 18]];

const font = (g, size, weight = 700) => { g.font = `${weight} ${size}px ${FONT}`; };
function drawWall(g, W, Hh, alerts, blink) {
  g.fillStyle = '#0b1216'; g.fillRect(0, 0, W, Hh);
  g.fillStyle = '#14232b'; g.fillRect(0, 0, W, 64);
  g.fillStyle = '#e8eef0'; font(g, 30); g.textBaseline = 'middle'; g.fillText('배송 노선 현황 · 연무센터', 28, 33);
  g.fillStyle = '#7fd1c4'; font(g, 22, 500); g.fillText('17:25 기준 · 실시간', 1040, 33);
  g.strokeStyle = '#1b2a31'; g.lineWidth = 1; for (let x = 24; x < 760; x += 40) { g.beginPath(); g.moveTo(x, 84); g.lineTo(x, 700); g.stroke(); }
  for (let y = 84; y < 700; y += 40) { g.beginPath(); g.moveTo(24, y); g.lineTo(760, y); g.stroke(); }
  const [, hx, hy] = NODES[0];
  NODES.slice(1).forEach(([name, x, y], i) => {
    const hot = i === 1 && alerts >= 1;
    g.strokeStyle = hot ? '#e5484d' : '#3cc6b5'; g.lineWidth = 7; g.setLineDash(hot ? [18, 12] : []); g.lineDashOffset = blink ? 15 : 0;
    g.beginPath(); g.moveTo(hx, hy); g.quadraticCurveTo((hx + x) / 2 + (y - hy) * 0.15, (hy + y) / 2 - (x - hx) * 0.15, x, y); g.stroke();
    if (hot) { g.setLineDash([]); g.fillStyle = '#e5484d'; font(g, 24); g.fillText('▲ 지연', (hx + x) / 2 - 70, (hy + y) / 2 - 10); }
  });
  g.setLineDash([]);
  for (const [name, x, y, rr] of NODES) {
    g.fillStyle = '#0b1216'; g.beginPath(); g.arc(x, y, rr + 6, 0, 7); g.fill();
    g.fillStyle = rr > 20 ? '#f2b33d' : '#3cc6b5'; g.beginPath(); g.arc(x, y, rr, 0, 7); g.fill();
    g.fillStyle = '#e8eef0'; font(g, 22, 600); g.textAlign = 'center'; g.fillText(name, x, y + rr + 24); g.textAlign = 'left';
  }
  g.fillStyle = '#8fa3ab'; font(g, 18, 500); g.fillText('━ 정상   ╍ 지연·보류 (▲ 표시)', 36, 684);
  g.fillStyle = '#cfd8dc'; font(g, 24); g.fillText('오늘의 알림', 800, 100);
  ROUTES.forEach(([name, detail], i) => {
    const y = 124 + i * 94, hot = i < alerts;
    g.fillStyle = hot ? '#3a1d20' : '#16232a'; g.fillRect(796, y, 460, 80);
    g.fillStyle = hot ? (blink ? '#ff6b6b' : '#e5484d') : i < 3 ? '#5b6770' : '#3cc6b5';
    g.beginPath(); g.arc(828, y + 40, 16, 0, 7); g.fill();
    g.fillStyle = '#0b1216'; font(g, 22, 900); g.textAlign = 'center'; g.fillText(hot ? '!' : '✓', 828, y + 41); g.textAlign = 'left';
    g.fillStyle = hot ? '#ffb4a8' : '#e8eef0'; font(g, 25); g.fillText(name, 858, y + 28);
    g.fillStyle = '#8fa3ab'; font(g, 20, 500); g.fillText(detail, 858, y + 58);
    g.fillStyle = hot ? '#e5484d' : '#24343c'; g.fillRect(1160, y + 24, 82, 32);
    g.fillStyle = '#ffffff'; font(g, 19); g.textAlign = 'center'; g.fillText(hot ? '주의' : i < 3 ? '집계 중' : '정상', 1201, y + 41); g.textAlign = 'left';
  });
  g.fillStyle = '#05080a'; g.fillRect(W / 2 - 3, 0, 6, Hh); g.fillRect(0, Hh / 2 - 3, W, 6); // video-wall seams
}
// Four generic work screens in one atlas (2 x 2 cells of 512 x 256).
function drawAtlas(g, r) {
  const cell = (i, fn) => { g.save(); g.translate((i % 2) * 512, Math.floor(i / 2) * 256); g.beginPath(); g.rect(0, 0, 512, 256); g.clip(); fn(); g.restore(); };
  const bars = (x, y, w, c) => { g.fillStyle = c; g.fillRect(x, y, w * (0.4 + r() * 0.6), 6); };
  cell(0, () => { // spreadsheet
    g.fillStyle = '#f7f8fa'; g.fillRect(0, 0, 512, 256); g.fillStyle = '#1f7a6a'; g.fillRect(0, 0, 512, 26);
    g.fillStyle = '#fff'; font(g, 15); g.fillText('주문 현황.xlsx', 10, 18);
    g.strokeStyle = '#d5d9de'; for (let y = 44; y < 256; y += 18) { g.beginPath(); g.moveTo(0, y); g.lineTo(512, y); g.stroke(); }
    for (const x of [70, 200, 300, 400]) { g.beginPath(); g.moveTo(x, 26); g.lineTo(x, 256); g.stroke(); }
    for (let y = 32; y < 250; y += 18) for (const x of [6, 76, 206, 306, 406]) { if (r() < 0.1) { g.fillStyle = r() < 0.5 ? '#ffe38a' : '#ffc9c9'; g.fillRect(x - 4, y - 5, 92, 17); } bars(x, y, 56, '#6b7480'); }
  });
  cell(1, () => { // ERP order list
    g.fillStyle = '#14202a'; g.fillRect(0, 0, 512, 256); g.fillStyle = '#0e171e'; g.fillRect(0, 0, 90, 256);
    for (let y = 20; y < 240; y += 26) bars(12, y, 60, '#3a4a55');
    g.fillStyle = '#e8eef0'; font(g, 15); g.fillText('배송 관리 · 주문 목록', 104, 22);
    for (let y = 40; y < 250; y += 24) {
      g.fillStyle = '#1b2a33'; g.fillRect(100, y, 404, 20); bars(108, y + 7, 160, '#8fa3ab');
      const k = r(); g.fillStyle = k < 0.6 ? '#3cc6b5' : k < 0.85 ? '#f2b33d' : '#e5484d'; g.fillRect(430, y + 3, 64, 14);
      g.fillStyle = '#0b1216'; font(g, 11); g.fillText(k < 0.6 ? '배송 완료' : k < 0.85 ? '지연' : '취소', 440, y + 14);
    }
  });
  cell(2, () => { // route map
    g.fillStyle = '#0f1a20'; g.fillRect(0, 0, 512, 256);
    g.strokeStyle = '#2b3d47'; g.lineWidth = 3;
    for (let i = 0; i < 14; i++) { g.beginPath(); let x = r() * 512, y = r() * 256; g.moveTo(x, y); for (let k = 0; k < 4; k++) { x += (r() - 0.5) * 200; y += (r() - 0.5) * 120; g.lineTo(x, y); } g.stroke(); }
    g.strokeStyle = '#3cc6b5'; g.lineWidth = 5; g.beginPath(); g.moveTo(40, 220); g.lineTo(160, 150); g.lineTo(300, 170); g.lineTo(460, 40); g.stroke();
    for (const [x, y] of [[40, 220], [160, 150], [300, 170], [460, 40]]) { g.fillStyle = '#f2b33d'; g.beginPath(); g.arc(x, y, 7, 0, 7); g.fill(); }
    g.fillStyle = 'rgba(20,35,43,0.9)'; g.fillRect(330, 180, 170, 66); g.fillStyle = '#e8eef0'; font(g, 14); g.fillText('2호차 · 도착 예정', 342, 204); g.fillText('18:10 (+25분)', 342, 230);
  });
  cell(3, () => { // messenger
    g.fillStyle = '#eef1f4'; g.fillRect(0, 0, 512, 256); g.fillStyle = '#ffffff'; g.fillRect(0, 0, 150, 256);
    g.fillStyle = '#1f4f8c'; g.fillRect(0, 0, 512, 24); g.fillStyle = '#fff'; font(g, 14); g.fillText('사내 메신저', 10, 17);
    for (let y = 36; y < 250; y += 36) { g.fillStyle = ['#f2b33d', '#3cc6b5', '#c0436e', '#6b5b95'][(y / 36) % 4 | 0]; g.beginPath(); g.arc(20, y + 10, 10, 0, 7); g.fill(); bars(38, y + 4, 100, '#8a929b'); }
    for (let y = 40, k = 0; y < 240; y += 40, k++) { const me = k % 2; g.fillStyle = me ? '#ffe38a' : '#ffffff'; const w = 120 + r() * 160; g.fillRect(me ? 500 - w : 162, y, w, 26); bars(me ? 508 - w : 170, y + 10, w - 20, '#6b7480'); }
  });
}
function drawBoard(g, W, Hh) {
  g.fillStyle = '#fbfbf8'; g.fillRect(0, 0, W, Hh);
  g.fillStyle = 'rgba(80,90,120,0.05)'; for (let i = 0; i < 14; i++) g.fillRect(40 + i * 70, 60 + (i % 3) * 120, 160, 30);
  g.fillStyle = '#1f4f8c'; font(g, 38, 800); g.fillText('이번 주 배송 일정', 36, 54);
  const cols = ['요일', '수도권', '서해안', '중부', '비고'], cx = [36, 156, 356, 556, 736], rows = ['월', '화', '수', '목', '금'];
  g.strokeStyle = '#2b2b2b'; g.lineWidth = 2;
  for (let i = 0; i <= 6; i++) { g.beginPath(); g.moveTo(30, 80 + i * 56); g.lineTo(990, 80 + i * 56); g.stroke(); }
  for (const x of [...cx.slice(1).map((v) => v - 12), 990]) { g.beginPath(); g.moveTo(x, 80); g.lineTo(x, 416); g.stroke(); }
  g.fillStyle = '#2b2b2b'; font(g, 26, 700); cols.forEach((c, i) => g.fillText(c, cx[i], 118));
  const cellTxt = [['06:30 ×4', '07:00 ×3', '06:40 ×2', ''], ['06:30 ×4', '07:00 ×3', '06:40 ×2', '반품 회수 많음'], ['06:30 ×4', '보류?', '06:40 ×2', 'B물류 공지 확인'], ['06:30 ×4', '', '06:40 ×2', ''], ['06:30 ×3', '', '06:40 ×2', '주말 근무 조정']];
  rows.forEach((d, i) => {
    const y = 174 + i * 56; g.fillStyle = '#2b2b2b'; font(g, 26, 700); g.fillText(d, cx[0], y);
    cellTxt[i].forEach((t, k) => { g.fillStyle = t === '보류?' ? '#c4161c' : '#2b2b2b'; font(g, 24, 600); g.fillText(t, cx[k + 1], y); });
  });
  g.strokeStyle = '#c4161c'; g.lineWidth = 4; g.beginPath(); g.ellipse(410, 278, 90, 26, -0.05, 0, 7); g.stroke();
  g.fillStyle = '#c4161c'; font(g, 24, 800); g.fillText('서해안: 내일부터 지연 공지 (B물류 메일)', 36, 456);
  g.fillStyle = '#1f4f8c'; g.fillText('취소 급증 — 원인 확인 중 · 고객에게 추측해서 말하지 않기', 36, 494);
  for (const [x, y, c] of [[980, 30, '#e5484d'], [940, 30, '#3cc6b5'], [900, 30, '#f2b33d']]) { g.fillStyle = c; g.beginPath(); g.arc(x, y, 12, 0, 7); g.fill(); }
}
function drawClock(g, W) {
  const c = W / 2; g.fillStyle = '#ffffff'; g.beginPath(); g.arc(c, c, c - 4, 0, 7); g.fill();
  g.strokeStyle = '#2b2b2b'; g.lineWidth = 8; g.stroke();
  for (let i = 0; i < 12; i++) { const a = i / 12 * Math.PI * 2; g.lineWidth = i % 3 ? 3 : 6; g.beginPath(); g.moveTo(c + Math.sin(a) * c * 0.78, c - Math.cos(a) * c * 0.78); g.lineTo(c + Math.sin(a) * c * 0.9, c - Math.cos(a) * c * 0.9); g.stroke(); }
  const hand = (a, len, w) => { g.lineWidth = w; g.lineCap = 'round'; g.beginPath(); g.moveTo(c, c); g.lineTo(c + Math.sin(a) * len, c - Math.cos(a) * len); g.stroke(); };
  hand((5.5 / 12) * Math.PI * 2, c * 0.5, 9); hand(Math.PI, c * 0.75, 6); // 17:30
}
// Plane whose UVs point at one cell of a 2 x 2 atlas.
function cellPlane(w, h, cell) {
  const g = new THREE.PlaneGeometry(w, h), uv = g.attributes.uv, col = cell % 2, row = Math.floor(cell / 2);
  for (let i = 0; i < uv.count; i++) uv.setXY(i, (col + uv.getX(i)) / 2, (1 - row + uv.getY(i)) / 2); // canvas row 0 = top = v 0.5..1
  return g;
}

export function buildOffice(kit, people) {
  const group = new THREE.Group(); group.name = 'office';
  const stat = new THREE.Group(), yard = new THREE.Group(), dyn = new THREE.Group();
  group.add(stat, yard, dyn);
  const solids = [], r = kit.rng(31);
  const screenMat = (map, k = 0.85) => new THREE.MeshStandardMaterial({ color: 0x1a1d20, map, emissiveMap: map, emissive: 0xffffff, emissiveIntensity: k, roughness: 0.25 });
  const M = {
    wall: kit.surface('plaster'), lam: kit.mat('paint:#d9d2c4', { roughness: 0.45 }), frame: kit.mat('paint:#8e959c', { metalness: 0.6, roughness: 0.35 }),
    dark: kit.mat('darkMetal'), black: kit.mat('paint:#1c1e21', { roughness: 0.4 }), ped: kit.mat('paint:#b4b8bc', { metalness: 0.4, roughness: 0.45 }),
    fabric: kit.mat('paint:#5f7378', { roughness: 0.98 }), chair: kit.mat('paint:#2a2e33', { roughness: 0.9 }), white: kit.mat('paint:#f1f0ec', { roughness: 0.5 }),
    paper: kit.mat('paint:#fafaf6', { roughness: 0.9 }), steel: kit.mat('steel'), rubber: kit.mat('rubber'), dglass: kit.mat('darkGlass'),
    glass: new THREE.MeshStandardMaterial({ color: 0xd6e6ee, roughness: 0.04, metalness: 0.1, transparent: true, opacity: 0.14, depthWrite: false }),
    screens: screenMat(kit.texture(kit.canvas(1024, 512, (g) => drawAtlas(g, kit.rng(8)))), 0.7),
  };
  const add = (o, solid, pad = 0.03, parent = stat) => { parent.add(o); if (solid) solids.push(solidOf(o, pad)); return o; };
  const keep = (o) => { o.userData.keep = true; dyn.add(o); return o; };
  const sign = (lines, o, at, lightGroup, parent = dyn) => {
    const s = kit.sign(lines, { single: true, ...o }); s.position.set(at.x, at.y, at.z); s.rotation.y = at.ry || 0; s.userData.keep = true;
    if (lightGroup) kit.lit(lightGroup, s.material, o.emissive || 0.6);
    parent.add(s); return s;
  };
  const flat = (w, h, mat, at) => { const m = new THREE.Mesh(w.isBufferGeometry ? w : new THREE.PlaneGeometry(w, h), mat); m.position.set(at.x, at.y, at.z); m.rotation.y = at.ry || 0; return m; };

  // ---- shell: carpet tiles, walls, ribbon window, T-bar ceiling ----
  const carpet = (() => {
    const g2 = kit.rng(4), c = kit.canvas(512, 512, (g) => {
      for (let ty = 0; ty < 4; ty++) for (let tx = 0; tx < 4; tx++) {
        const v = 86 + g2() * 10; g.fillStyle = `rgb(${v - 8 | 0},${v | 0},${v + 14 | 0})`; g.fillRect(tx * 128, ty * 128, 128, 128);
        g.strokeStyle = 'rgba(30,36,44,0.35)'; g.lineWidth = 1;
        for (let k = 0; k < 40; k++) { const o = g2() * 128; g.beginPath(); if ((tx + ty) % 2) { g.moveTo(tx * 128 + o, ty * 128); g.lineTo(tx * 128 + o, ty * 128 + 128); } else { g.moveTo(tx * 128, ty * 128 + o); g.lineTo(tx * 128 + 128, ty * 128 + o); } g.stroke(); }
      }
      for (let i = 0; i < 9000; i++) { g.fillStyle = g2() < 0.5 ? 'rgba(0,0,0,0.08)' : 'rgba(255,255,255,0.05)'; g.fillRect(g2() * 512, g2() * 512, 1.5, 1.5); }
      g.fillStyle = 'rgba(20,24,30,0.5)'; for (let i = 0; i <= 4; i++) { g.fillRect(i * 128 - 1, 0, 2, 512); g.fillRect(0, i * 128 - 1, 512, 2); }
    });
    return new THREE.MeshStandardMaterial({ map: kit.texture(c, { repeat: [0.5, 0.5] }), normalMap: kit.texture(kit.normalFrom(c, 1.5), { color: false, repeat: [0.5, 0.5] }), roughness: 0.97 });
  })();
  stat.add(kit.plane(18, 12, carpet, { x: 0, z: -6 }));
  add(kit.slab(0.25, H, 12.5, M.wall, { x: -9.125, z: -6 }), true); add(kit.slab(0.25, H, 12.5, M.wall, { x: 9.125, z: -6 }), true);
  for (const [x0, x1] of [[-9, DOOR - 0.5], [DOOR + 0.5, 9]]) add(kit.slab(x1 - x0, H, 0.25, M.wall, { x: (x0 + x1) / 2, z: 0.125 }), true);
  stat.add(kit.slab(1.0, H - 2.1, 0.25, M.wall, { x: DOOR, y: 2.1, z: 0.125 }));
  solids.push({ x0: DOOR - 0.5, x1: DOOR + 0.5, y0: 0, y1: H, z0: 0.02, z1: 0.6, id: 'door' });
  add(kit.slab(18.5, 0.8, 0.3, M.wall, { x: 0, z: -12.15 }), true);
  stat.add(kit.slab(18.5, H - 2.45, 0.3, M.wall, { x: 0, y: 2.45, z: -12.15 }));
  stat.add(kit.box(18, 0.04, 0.32, M.white, { x: 0, y: 0.8, z: -11.97 }));
  for (let x = -9; x <= 9.01; x += 1.5) stat.add(kit.box(0.06, 1.65, 0.12, M.frame, { x, y: 0.8, z: -12.1 }));
  keep(kit.box(18, 1.65, 0.02, M.glass, { x: 0, y: 0.8, z: -12.08, cast: false }));
  solids.push({ x0: -9, x1: 9, y0: 0, y1: H, z0: -12.4, z1: -11.82, id: 'windowWall' });
  const slats = new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(64, 64, (g) => { g.fillStyle = '#e4e2dc'; g.fillRect(0, 0, 64, 64); g.fillStyle = '#b9b6ae'; for (let y = 0; y < 64; y += 8) g.fillRect(0, y, 64, 2); }), { repeat: [1, 8] }), roughness: 0.7 });
  for (let i = 0; i < 12; i++) {
    const x = -8.25 + i * 1.5, low = i >= 9 ? 0.75 : i === 4 ? 0.3 : 0;
    stat.add(kit.box(1.42, 0.1, 0.08, M.white, { x, y: 2.33, z: -11.92, cast: false }));
    if (low) stat.add(kit.box(1.42, low, 0.01, slats, { x, y: 2.33 - low, z: -11.9 }));
  }
  stat.add(kit.slab(18.5, 0.1, 12.5, new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(256, 256, (g) => {
    const q = kit.rng(6); g.fillStyle = '#efeeea'; g.fillRect(0, 0, 256, 256); g.fillStyle = 'rgba(0,0,0,0.04)'; for (let i = 0; i < 900; i++) g.fillRect(q() * 256, q() * 256, 2, 2);
    g.fillStyle = '#c9c8c3'; for (let i = 0; i < 256; i += 64) { g.fillRect(i, 0, 3, 256); g.fillRect(0, i, 256, 3); }
  }), { repeat: [1 / 2.4, 1 / 2.4] }), roughness: 0.9 }), { x: 0, y: H, z: -6 }));
  for (const [x, z, w, d] of [[-8.99, -6, 0.02, 12], [8.99, -6, 0.02, 12], [0, -0.01, 18, 0.02]]) stat.add(kit.box(w, 0.1, d, M.black, { x, z, cast: false }));

  // ---- desk islands: desks, partitions, dual monitors, clutter; chairs are instanced ----
  const chairs = [], mugs = ['#ffffff', '#1f4f8c', '#c4161c', '#2f8a57', '#f2c230', '#6b5b95'];
  function monitor(x, z, ry, w, h, cell, y = 0.98) {
    const f = [Math.sin(ry), Math.cos(ry)];
    stat.add(kit.box(0.22, 0.015, 0.18, M.black, { x: x - f[0] * 0.08, y: 0.715, z: z - f[1] * 0.08, ry }));
    stat.add(kit.box(0.04, y - 0.72, 0.03, M.black, { x: x - f[0] * 0.1, y: 0.72, z: z - f[1] * 0.1, ry }));
    stat.add(kit.box(w + 0.02, h + 0.02, 0.025, M.black, { x, y: y - h / 2 - 0.01, z, ry }));
    if (cell !== null) stat.add(flat(cellPlane(w, h, cell), 0, M.screens, { x: x + f[0] * 0.0135, y, z: z + f[1] * 0.0135, ry }));
  }
  function desk(cx, zc, s, mine) {
    const zf = zc + s * 0.35, ry = s > 0 ? 0 : Math.PI, at = (dx, dz, o = {}) => ({ x: cx + dx, z: zc + s * dz, ...o });
    stat.add(kit.box(1.38, 0.03, 0.7, M.lam, { x: cx, y: 0.69, z: zf }));
    for (const dx of [-0.66, 0.66]) stat.add(kit.box(0.04, 0.69, 0.6, M.frame, { x: cx + dx, z: zf }));
    stat.add(kit.box(0.42, 0.56, 0.5, M.ped, at(0.4, 0.34)));
    if (!mine) { monitor(cx - 0.29, zc + s * 0.16, ry, 0.53, 0.3, (r() * 4) | 0); monitor(cx + 0.29, zc + s * 0.16, ry, 0.53, 0.3, (r() * 4) | 0); }
    stat.add(kit.box(0.44, 0.02, 0.14, M.black, at(0, 0.5, { y: 0.72 }))); stat.add(kit.box(0.06, 0.025, 0.1, M.black, at(0.32, 0.5, { y: 0.72 })));
    stat.add(kit.box(0.18, 0.06, 0.2, M.black, at(-0.52, 0.42, { y: 0.72, ry: s * 0.3 })));
    stat.add(kit.cyl(0.04, 0.04, 0.1, kit.mat('paint:' + mugs[(r() * mugs.length) | 0], { roughness: 0.3 }), at(0.5, 0.25, { y: 0.72 }), 12));
    for (let k = 0; k < 1 + r() * 3; k++) stat.add(kit.box(0.21, 0.004, 0.297, M.paper, at(-0.15 + r() * 0.3, 0.42 + r() * 0.1, { y: 0.72 + k * 0.004, ry: (r() - 0.5) * 0.6 })));
    if (r() < 0.5) for (let k = 0; k < 3; k++) stat.add(kit.box(0.06, 0.3, 0.24, kit.mat('paint:' + ['#1f4f8c', '#c4161c', '#2f8a57', '#3b3f46'][(r() * 4) | 0]), at(-0.62 + k * 0.065, 0.2, { y: 0.72 })));
    else if (r() < 0.5) { stat.add(kit.cyl(0.06, 0.05, 0.1, M.white, at(0.55, 0.15, { y: 0.72 }), 10)); stat.add(kit.cyl(0.002, 0.11, 0.18, kit.mat('foliage'), at(0.55, 0.15, { y: 0.82 }), 8)); }
    chairs.push(mine ? { x: cx + 0.35, z: zc + s * 1.45, ry: ry + 0.5 } : { x: cx + (r() - 0.5) * 0.2, z: zc + s * (1.05 + r() * 0.25), ry: ry + (r() - 0.5) * 0.7 });
  }
  for (const zc of ISL) {
    for (const s of [-1, 1]) for (const cx of DX) desk(cx, zc, s, zc === ME.z && s > 0 && cx === ME.x);
    stat.add(kit.box(5.66, 0.44, 0.04, M.fabric, { x: -3.7, y: 0.7, z: zc })); stat.add(kit.box(5.66, 0.025, 0.06, M.frame, { x: -3.7, y: 1.14, z: zc }));
    solids.push({ x0: -6.55, x1: -0.85, y0: 0, y1: 1.2, z0: zc - 0.72, z1: zc + 0.72, id: 'island' });
  }
  // The player's desk: dashboard monitor (director canvas), messenger, a sticky note and a name plate.
  monitor(ME.x - 0.26, ME.z + 0.17, 0, 0.62, 0.39, null, 1.04);
  monitor(ME.x + 0.36, ME.z + 0.17, -0.25, 0.53, 0.3, 3);
  let dashTex = kit.texture(kit.canvas(1024, 640, (g) => { g.fillStyle = '#0d1418'; g.fillRect(0, 0, 1024, 640); g.fillStyle = '#7fd1c4'; font(g, 40); g.fillText('운영 대시보드', 40, 80); g.fillStyle = '#8fa3ab'; font(g, 28, 500); g.fillText('데이터를 불러오는 중…', 40, 150); }));
  const dashMat = screenMat(dashTex, 0.9);
  const dash = keep(flat(0.62, 0.39, dashMat, { x: ME.x - 0.26, y: 1.04, z: ME.z + 0.184 }));
  sign(['서해안 노선', '내일 확인!'], { w: 0.075, h: 0.075, bg: '#ffe38a', fg: '#7a3b12' }, { x: ME.x - 0.585, y: 1.15, z: ME.z + 0.187, ry: 0.05 });
  sign(['운영팀 · 정민'], { w: 0.24, h: 0.06, bg: '#1f4f8c', fg: '#ffffff' }, { x: ME.x - 0.45, y: 0.755, z: ME.z + 0.69, ry: 0 });

  // ---- team lead desk (윤 팀장), wall screen, whiteboard, cooler, printer, cabinets, door ----
  stat.add(kit.box(0.8, 0.03, 1.8, M.lam, { x: 6.7, y: 0.69, z: LEAD.z }));
  stat.add(kit.box(0.04, 0.69, 1.8, M.lam, { x: 6.32, z: LEAD.z })); stat.add(kit.box(0.5, 0.56, 0.42, M.ped, { x: 6.85, z: LEAD.z + 0.6 }));
  solids.push({ x0: 6.28, x1: 7.12, y0: 0, y1: 1.2, z0: LEAD.z - 0.92, z1: LEAD.z + 0.92, id: 'leadDesk' });
  monitor(6.5, LEAD.z - 0.3, Math.PI / 2, 0.53, 0.3, 1); monitor(6.5, LEAD.z + 0.3, Math.PI / 2, 0.53, 0.3, 0);
  stat.add(kit.box(0.2, 0.06, 0.18, M.black, { x: 6.8, y: 0.72, z: LEAD.z - 0.7 }));
  for (let k = 0; k < 6; k++) stat.add(kit.box(0.297, 0.004, 0.21, M.paper, { x: 6.85, y: 0.72 + k * 0.006, z: LEAD.z + 0.25, ry: (r() - 0.5) * 0.3 }));
  sign(['운영팀장 윤'], { w: 0.26, h: 0.07, bg: '#3b3f46', fg: '#ffffff' }, { x: 6.29, y: 0.55, z: LEAD.z, ry: -Math.PI / 2 });
  chairs.push({ x: LEAD.x, z: LEAD.z, ry: Math.PI / 2 });
  const wallTex = kit.texture(kit.canvas(1280, 720)), wallG = wallTex.image.getContext('2d');
  let wallAlerts = 0, blink = false;
  const redrawWall = () => { drawWall(wallG, 1280, 720, wallAlerts, blink); wallTex.needsUpdate = true; };
  redrawWall();
  stat.add(kit.box(0.06, 1.48, 2.56, M.black, { x: -8.97, y: 0.96, z: -6.5 }));
  keep(flat(2.5, 1.4, screenMat(wallTex, 0.95), { x: -8.935, y: 1.7, z: -6.5, ry: Math.PI / 2 }));
  add(kit.box(0.42, 0.72, 2.6, M.white, { x: -8.79, z: -6.5 }), true);
  stat.add(kit.box(0.3, 0.05, 0.22, M.black, { x: -8.8, y: 0.72, z: -7.4 }));
  for (let k = 0; k < 5; k++) stat.add(kit.box(0.24, 0.3, 0.05, kit.mat('paint:' + ['#1f4f8c', '#2f8a57', '#c4161c', '#f2c230', '#3b3f46'][k]), { x: -8.8, y: 0.72, z: -5.6 - k * 0.06 }));
  const boardTex = kit.texture(kit.canvas(1024, 512, (g, w, h) => drawBoard(g, w, h)));
  stat.add(kit.box(0.03, 1.26, 2.46, M.frame, { x: 8.985, y: 0.87, z: -3.6 }));
  keep(flat(2.4, 1.2, new THREE.MeshStandardMaterial({ map: boardTex, roughness: 0.25 }), { x: 8.965, y: 1.5, z: -3.6, ry: -Math.PI / 2 }));
  stat.add(kit.box(0.08, 0.03, 1.2, M.frame, { x: 8.93, y: 0.86, z: -3.6 }));
  for (const [z, c] of [[-3.2, '#c4161c'], [-3.1, '#1f4f8c'], [-3.0, '#2b2b2b']]) stat.add(kit.box(0.02, 0.02, 0.12, kit.mat('paint:' + c), { x: 8.93, y: 0.89, z, cast: false }));
  // water cooler
  add(kit.box(0.34, 1.0, 0.34, M.white, { x: 8.62, z: -1.5 }), true);
  stat.add(kit.box(0.02, 0.05, 0.04, kit.mat('paint:#c4161c'), { x: 8.44, y: 0.82, z: -1.57 })); stat.add(kit.box(0.02, 0.05, 0.04, kit.mat('paint:#1f6fb2'), { x: 8.44, y: 0.82, z: -1.43 }));
  stat.add(kit.box(0.1, 0.02, 0.24, M.dark, { x: 8.47, y: 0.62, z: -1.5 })); stat.add(kit.cyl(0.04, 0.04, 0.4, M.white, { x: 8.62, y: 0.5, z: -1.24 }, 10));
  keep(kit.cyl(0.135, 0.135, 0.42, new THREE.MeshStandardMaterial({ color: 0x7fb6e8, roughness: 0.08, transparent: true, opacity: 0.55, depthWrite: false }), { x: 8.62, y: 1.0, z: -1.5, cast: false }, 18));
  // printer, paper, cabinets, plants
  add(kit.box(0.62, 1.0, 0.62, kit.mat('paint:#d7d9db', { roughness: 0.5 }), { x: 8.5, z: -10.8 }), true);
  stat.add(kit.box(0.6, 0.08, 0.5, M.black, { x: 8.5, y: 1.0, z: -10.8 }));
  stat.add(flat(cellPlane(0.16, 0.09, 1), 0, M.screens, { x: 8.18, y: 0.95, z: -10.8, ry: -Math.PI / 2 }));
  for (let k = 0; k < 4; k++) stat.add(kit.box(0.3, 0.05, 0.21, M.paper, { x: 8.55, y: k * 0.05, z: -10.2, ry: (r() - 0.5) * 0.2 }));
  const doorsTex = kit.texture(kit.canvas(256, 128, (g) => { g.fillStyle = '#c3c7cb'; g.fillRect(0, 0, 256, 128); g.fillStyle = '#9aa0a6'; for (const x of [0, 127, 254]) g.fillRect(x, 0, 2, 128); g.fillStyle = '#5d6368'; for (const x of [112, 140]) g.fillRect(x, 50, 4, 28); }));
  const cab = new THREE.MeshStandardMaterial({ map: doorsTex, metalness: 0.4, roughness: 0.45 });
  for (const x of [-7.6, -5.8, -4.0]) {
    add(kit.box(1.78, 1.05, 0.45, cab, { x, z: -0.35 }), true);
    for (let k = 0; k < 6; k++) stat.add(kit.box(0.06, 0.3, 0.26, kit.mat('paint:' + ['#1f4f8c', '#c4161c', '#2f8a57', '#3b3f46', '#f2c230'][(r() * 5) | 0]), { x: x - 0.6 + k * 0.07, y: 1.05, z: -0.35 }));
  }
  for (const [x, z] of [[-8.6, -11.4], [8.5, -11.4], [-2.7, -0.4]]) {
    add(kit.cyl(0.2, 0.16, 0.42, M.white, { x, z }, 14), true);
    stat.add(new THREE.Mesh(new THREE.IcosahedronGeometry(0.36, 1), kit.mat('foliage')).translateX(x).translateY(0.85).translateZ(z));
  }
  // door: glass leaf, card reader, exit sign, a corridor behind it
  keep(kit.box(0.96, 2.08, 0.02, M.glass, { x: DOOR, z: 0.1, cast: false }));
  for (const [w, h, x, y] of [[1.0, 0.05, DOOR, 0], [1.0, 0.05, DOOR, 2.05], [0.05, 2.1, DOOR - 0.48, 0], [0.05, 2.1, DOOR + 0.48, 0]]) stat.add(kit.box(w, h, 0.05, M.frame, { x, y, z: 0.1 }));
  stat.add(kit.box(0.03, 0.5, 0.03, M.steel, { x: DOOR + 0.36, y: 0.8, z: 0.05 }));
  stat.add(kit.box(0.08, 0.12, 0.03, M.black, { x: DOOR + 0.75, y: 1.2, z: -0.015 }));
  sign(['●'], { w: 0.02, h: 0.02, bg: '#000000', fg: '#5aff9a', emissive: 1.5 }, { x: DOOR + 0.75, y: 1.24, z: -0.032, ry: Math.PI }, 'indoor');
  sign(['비상구'], { w: 0.5, h: 0.18, bg: '#1f9a4a', fg: '#ffffff', emissive: 1.2 }, { x: DOOR, y: 2.42, z: -0.01, ry: Math.PI }, 'emergency');
  stat.add(kit.plane(3, 3, kit.surface('tile'), { x: DOOR, z: 1.6 })); stat.add(kit.slab(3, H, 0.1, M.wall, { x: DOOR, z: 3.1 }));
  sign(['한결물류 HANGYEOL', '연무센터 운영팀'], { w: 1.7, h: 0.48, bg: '#ffffff', fg: '#18806f', border: '#18806f' }, { x: 3.0, y: 1.75, z: -0.01, ry: Math.PI });
  const clock = flat(0.34, 0.34, new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(256, 256, (g, w) => drawClock(g, w))), roughness: 0.4, transparent: true }), { x: 0, y: 2.2, z: -0.012, ry: Math.PI });
  keep(clock);

  // ---- the yard below the window: warehouse docks, trucks, pallets of parcels, lamps, trees, sky ----
  yard.add(kit.plane(130, 80, kit.surface('asphalt'), { x: 0, y: GY, z: -52 }));
  yard.add(kit.slab(40, -GY, 0.3, kit.surface('concrete'), { x: 0, y: GY, z: -12.45 }));
  const line = kit.mat('paint:#e8e6df', { roughness: 0.7 });
  for (let x = -48; x <= 48; x += 4) yard.add(kit.box(0.12, 0.01, 9, line, { x, y: GY, z: -50, cast: false }));
  yard.add(kit.box(100, 0.01, 0.15, line, { x: 0, y: GY, z: -45.5, cast: false }));
  const ribs = (w, h, horiz, base) => kit.texture(kit.canvas(128, 128, (g) => {
    g.fillStyle = base; g.fillRect(0, 0, 128, 128);
    for (let i = 0; i < 128; i += 32) { const gr = horiz ? g.createLinearGradient(0, i, 0, i + 32) : g.createLinearGradient(i, 0, i + 32, 0); gr.addColorStop(0, 'rgba(0,0,0,0.18)'); gr.addColorStop(0.5, 'rgba(255,255,255,0.12)'); gr.addColorStop(1, 'rgba(0,0,0,0.18)'); g.fillStyle = gr; horiz ? g.fillRect(0, i, 128, 32) : g.fillRect(i, 0, 32, 128); }
  }));
  const corr = new THREE.MeshStandardMaterial({ map: ribs(0, 0, false, '#b7c0c6'), metalness: 0.45, roughness: 0.5 });
  const shutter = new THREE.MeshStandardMaterial({ map: ribs(0, 0, true, '#9aa3a9'), metalness: 0.5, roughness: 0.45 });
  yard.add(kit.slab(110, 11, 34, corr, { x: 0, y: GY, z: -79 }));
  yard.add(kit.box(110.4, 0.5, 34.4, M.dark, { x: 0, y: GY + 11, z: -79 }));
  yard.add(kit.slab(110, 1.2, 2.2, kit.surface('concrete'), { x: 0, y: GY, z: -60.9 }));
  sign(['한결물류 연무센터'], { w: 26, h: 3.2, bg: '#18806f', fg: '#ffffff', emissive: 0.7 }, { x: 0, y: GY + 8.6, z: -61.9 }, 'signs', yard);
  const glowY = new THREE.MeshStandardMaterial({ color: 0x222222, emissive: 0xffd9a0, roughness: 0.5 }); kit.lit('yard', glowY, 2.2);
  const inside = new THREE.MeshStandardMaterial({ color: 0x302820, emissive: 0xffc98a, roughness: 0.8 }); kit.lit('yard', inside, 0.9);
  for (let i = 0; i < 8; i++) {
    const x = -35 + i * 10, open = i === 2 || i === 5, top = GY + 1.2;
    yard.add(kit.slab(3.2, open ? 1.6 : 3.2, 0.1, shutter, { x, y: open ? top + 1.6 : top, z: -61.95 }));
    if (open) yard.add(kit.box(3.2, 1.6, 0.04, inside, { x, y: top, z: -61.97, cast: false }));
    for (const s of [-1, 1]) yard.add(kit.box(0.25, 0.35, 0.2, M.rubber, { x: x + s * 1.3, y: top - 0.45, z: -59.75 }));
    yard.add(kit.box(0.5, 0.12, 0.3, glowY, { x, y: top + 3.5, z: -61.75, cast: false }));
    sign([`${String(i + 1).padStart(2, '0')}`], { w: 0.8, h: 0.5, bg: '#ffffff', fg: '#1d1d1f' }, { x: x + 2.1, y: top + 2.6, z: -61.89 }, null, yard);
  }
  const logo = new THREE.MeshStandardMaterial({ roughness: 0.4, metalness: 0.2, map: kit.texture(kit.canvas(1024, 256, (g) => {
    g.fillStyle = '#eef0f1'; g.fillRect(0, 0, 1024, 256); g.fillStyle = 'rgba(0,0,0,0.08)'; for (let x = 0; x < 1024; x += 64) g.fillRect(x, 0, 2, 256);
    g.fillStyle = '#18806f'; g.fillRect(0, 186, 1024, 34); g.fillStyle = '#f28a1a'; g.fillRect(0, 222, 1024, 8);
    g.fillStyle = '#18806f'; font(g, 96, 900); g.fillText('한결물류', 60, 130); font(g, 34, 700); g.fillText('HANGYEOL LOGISTICS', 520, 128);
  })) });
  const cabMat = kit.mat('paint:#f4f4f2', { roughness: 0.35, metalness: 0.2 });
  function truck(x, z, ry, big) {
    const t = new THREE.Group(); t.position.set(x, GY, z); t.rotation.y = ry; yard.add(t);
    const L = big ? 7 : 2.9, W = big ? 2.4 : 1.8, wr = big ? 0.5 : 0.36, cabD = big ? 2.0 : 1.5, cabH = big ? 2.4 : 1.9, z0 = -(cabD + L) / 2;
    t.add(kit.box(W * 0.45, 0.3, cabD + L, M.dark, { y: wr + 0.05, z: 0.2 }));
    t.add(kit.rbox(W - 0.08, cabH, cabD, 0.12, cabMat, { y: wr + 0.25, z: z0 + cabD / 2 }));
    t.add(kit.box(W - 0.3, cabH * 0.36, 0.02, M.dglass, { y: wr + 0.25 + cabH * 0.56, z: z0 - 0.005 }));
    for (const s of [-1, 1]) {
      t.add(kit.box(0.02, cabH * 0.32, cabD * 0.5, M.dglass, { x: s * (W / 2 - 0.035), y: wr + 0.25 + cabH * 0.58, z: z0 + cabD * 0.35 }));
      t.add(kit.box(0.05, 0.3, 0.12, M.black, { x: s * (W / 2 + 0.12), y: wr + 0.25 + cabH * 0.6, z: z0 + 0.15 }));
      t.add(kit.box(0.24, 0.12, 0.02, kit.mat('paint:#e9e6da', { roughness: 0.2 }), { x: s * (W / 2 - 0.3), y: wr + 0.45, z: z0 - 0.01 }));
      for (const wz of big ? [z0 + cabD / 2, z0 + cabD + L - 1.9, z0 + cabD + L - 0.9] : [z0 + cabD / 2, z0 + cabD + L - 0.8]) t.add(kit.cyl(wr, wr, 0.3, M.rubber, { x: s * (W / 2 - 0.2), y: wr, z: wz, rz: Math.PI / 2 }, 14));
    }
    t.add(kit.box(W, 0.28, 0.18, M.black, { y: wr - 0.1, z: z0 - 0.05 })); t.add(kit.box(W * 0.6, cabH * 0.2, 0.02, M.black, { y: wr + 0.3, z: z0 - 0.01 }));
    t.add(kit.box(W + 0.05, big ? 2.6 : 1.8, L, logo, { y: wr + 0.55, z: z0 + cabD + 0.15 + L / 2 }));
    return t;
  }
  [[-25, -54.4, Math.PI, true], [-5, -54.4, Math.PI, true], [15, -54.4, Math.PI, true], [28, -38, Math.PI, true], [33, -38, Math.PI, true], [38, -38, Math.PI, true],
    [-42, -30, Math.PI, false], [-38.6, -30, Math.PI, false], [-35.2, -30, Math.PI, false], [2, -36, Math.PI * 0.82, true]].forEach(([x, z, ry, big]) => truck(x, z, ry, big));
  const box = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);
  const cardboard = new THREE.MeshStandardMaterial({ roughness: 0.85, map: kit.texture(kit.canvas(256, 256, (g) => {
    const q = kit.rng(12); g.fillStyle = '#b98b5a'; g.fillRect(0, 0, 256, 256); for (let i = 0; i < 2500; i++) { g.fillStyle = q() < 0.5 ? 'rgba(90,60,30,0.12)' : 'rgba(255,230,190,0.1)'; g.fillRect(q() * 256, q() * 256, 2, 2); }
    g.fillStyle = 'rgba(230,200,150,0.75)'; g.fillRect(0, 110, 256, 36); g.fillStyle = '#ffffff'; g.fillRect(150, 160, 84, 60); g.fillStyle = '#1d1d1f'; for (let x = 156; x < 228; x += 4 + ((q() * 3) | 0)) g.fillRect(x, 186, 2, 28);
  })) });
  const parcels = [], pallets = [];
  const stack = (x, z, ry, n) => {
    pallets.push({ x, y: GY, z, ry, sx: 1.2, sy: 0.14, sz: 1.0 });
    for (let l = 0; l < n; l++) for (let i = 0; i < 2; i++) for (let j = 0; j < 2; j++) {
      if (l === n - 1 && r() < 0.3) continue;
      const lx = (i - 0.5) * 0.58, lz = (j - 0.5) * 0.48, c = Math.cos(ry), s = Math.sin(ry);
      parcels.push({ x: x + lx * c + lz * s, y: GY + 0.14 + l * 0.4, z: z - lx * s + lz * c, ry: ry + (r() - 0.5) * 0.1, sx: 0.55, sy: 0.38 + r() * 0.02, sz: 0.45 });
    }
  };
  for (let i = 0; i < 14; i++) stack(-40 + r() * 80, -57.5 + r() * 2.5, (r() - 0.5) * 0.3, 2 + ((r() * 3) | 0));
  for (const [x, z] of [[-6, -15.5], [-4.4, -16], [-2.6, -15.4], [3, -16.2], [4.6, -15.6], [-4.8, -17.6]]) stack(x, z, (r() - 0.5) * 0.4, 3 + ((r() * 2) | 0));
  for (const [x, z, y] of [[8.3, -8.9, 0], [8.55, -8.4, 0], [8.4, -8.65, 0.36]]) parcels.push({ x, y, z, ry: r() * 0.4, sx: 0.5, sy: 0.36, sz: 0.4 });
  solids.push({ x0: 8.0, x1: 8.9, y0: 0, y1: 0.8, z0: -9.2, z1: -8.1 });
  sign(['반품 샘플'], { w: 0.3, h: 0.1, bg: '#ffffff', fg: '#c4161c' }, { x: 8.03, y: 0.25, z: -8.65, ry: -Math.PI / 2 });
  const pm = kit.instanced(box, cardboard, parcels); const tc = new THREE.Color();
  parcels.forEach((_, i) => pm.setColorAt(i, tc.setScalar(0.85 + r() * 0.2))); dyn.add(pm);
  dyn.add(kit.instanced(box, kit.surface('wood', { color: 0xc8a878 }), pallets));
  // forklift parked by the stacks
  const fork = new THREE.Group(); fork.position.set(7.5, GY, -19); fork.rotation.y = 0.4; yard.add(fork);
  const yel = kit.mat('paint:#f2b80f', { roughness: 0.5 });
  fork.add(kit.box(1.1, 1.0, 1.9, yel, { y: 0.3, z: 0.2 })); fork.add(kit.box(1.05, 0.5, 0.6, M.dark, { y: 0.9, z: 0.85 }));
  for (const x of [-0.5, 0.5]) { fork.add(kit.box(0.08, 2.4, 0.08, M.dark, { x, y: 0.3, z: -0.8 })); fork.add(kit.box(0.06, 1.6, 0.06, M.dark, { x, y: 1.3, z: 0.75 })); fork.add(kit.box(0.12, 0.05, 1.1, M.steel, { x: x * 0.6, y: 0.1, z: -1.35 })); }
  fork.add(kit.box(1.1, 0.06, 1.2, M.dark, { y: 2.9, z: 0.15 }));
  for (const [x, z] of [[-0.55, -0.4], [0.55, -0.4], [-0.55, 0.8], [0.55, 0.8]]) fork.add(kit.cyl(0.28, 0.28, 0.22, M.rubber, { x, y: 0.28, z, rz: Math.PI / 2 }, 12));
  for (const [x, z] of [[-30, -27], [0, -27], [30, -27], [-46, -57], [46, -57]]) {
    yard.add(kit.cyl(0.1, 0.14, 9, M.dark, { x, y: GY, z })); yard.add(kit.box(0.2, 0.15, 1.4, M.dark, { x, y: GY + 8.9, z: z + 0.6 }));
    yard.add(kit.box(0.5, 0.08, 0.8, glowY, { x, y: GY + 8.84, z: z + 1.1, cast: false }));
  }
  const trees = [], crowns = [];
  for (let z = -18; z > -60; z -= 6) for (const x of [-57, 57]) { trees.push({ x: x + r() * 2, y: GY, z, sx: 0.25, sy: 2.4, sz: 0.25 }); crowns.push({ x: x + r() * 2, y: GY + 4 + r(), z, s: 2.2 + r() * 0.8 }); }
  dyn.add(kit.instanced(box, kit.mat('bark'), trees)); dyn.add(kit.instanced(new THREE.IcosahedronGeometry(1, 1), kit.mat('foliage'), crowns));
  const view = windowView(kit, { w: 420, h: 130, kind: 'sky', seed: 5 }); view.mesh.position.set(0, 32, -165); dyn.add(view.mesh);
  // hazard lights blinking on the idle truck at x 33 (front faces the office)
  const hazard = new THREE.MeshStandardMaterial({ color: 0x3a2a10, emissive: 0xffa31a, emissiveIntensity: 0, roughness: 0.4 });
  for (const s of [-1, 1]) dyn.add(kit.box(0.2, 0.1, 0.05, hazard, { x: 33 + s * 0.9, y: GY + 0.95, z: -38 + 4.55, cast: false }));

  // ---- chairs (instanced), lights ----
  const T = (g, x, y, z, rx = 0, ry = 0, rz = 0) => g.rotateX(rx).rotateY(ry).rotateZ(rz).translate(x, y, z);
  const legs = Array.from({ length: 5 }, (_, i) => T(new THREE.BoxGeometry(0.05, 0.03, 0.3), 0, 0.06, 0, 0, i / 5 * Math.PI * 2).translate(0, 0, 0));
  legs.forEach((g, i) => { const a = i / 5 * Math.PI * 2; g.translate(Math.sin(a) * 0.15, 0, Math.cos(a) * 0.15); });
  const chairMetal = mergeGeometries([...legs, T(new THREE.CylinderGeometry(0.03, 0.03, 0.36, 8), 0, 0.26, 0), T(new THREE.BoxGeometry(0.04, 0.22, 0.04), -0.25, 0.56, 0.02), T(new THREE.BoxGeometry(0.04, 0.22, 0.04), 0.25, 0.56, 0.02),
    T(new THREE.BoxGeometry(0.06, 0.03, 0.26), -0.25, 0.68, 0.02), T(new THREE.BoxGeometry(0.06, 0.03, 0.26), 0.25, 0.68, 0.02), T(new THREE.BoxGeometry(0.04, 0.3, 0.03), 0, 0.5, 0.25, -0.15)]);
  const chairSoft = mergeGeometries([T(new THREE.BoxGeometry(0.48, 0.08, 0.46), 0, 0.46, 0), T(new THREE.BoxGeometry(0.46, 0.52, 0.06), 0, 0.88, 0.27, -0.12)]);
  dyn.add(kit.instanced(chairMetal, M.black, chairs)); dyn.add(kit.instanced(chairSoft, M.chair, chairs));
  const fixtures = [];
  for (let x = -7.8; x <= 7.9; x += 2.4) for (let z = -1.5; z >= -10.6; z -= 1.8) fixtures.push({ x, y: H - 0.03, z });
  dyn.add(panels(kit, fixtures, { w: 0.6, d: 0.6, color: '#f4f1ea', on: 2.0 }));
  dyn.add(lamps(kit, [[-6, -3.5], [-1.5, -3.5], [-3.5, -9.5], [4.5, -5.5], [4.5, -10]].map(([x, z]) => ({ x, y: H - 0.35, z })), { intensity: 9, distance: 11, color: 0xfff4e6 }));

  // ---- people ----
  const coworker = npc(people, 'coworker', 3, { x: LEAD.x, z: LEAD.z, ry: Math.PI / 2, anim: 'sitChair' });
  const staff1 = npc(people, 'crowd', 61, { x: -4.4, z: ISL[1] + 1.15, ry: 0, anim: 'sitChair' });
  const staff2 = npc(people, 'crowd', 62, { x: 4.6, z: -11.35, ry: 0.2, anim: 'phone' });
  chairs.forEach((c) => { if (Math.abs(c.x + 4.4) < 0.2 && Math.abs(c.z - ISL[1] - 1.15) < 0.3) { c.x = -4.4; c.z = ISL[1] + 1.15; c.ry = 0; } });
  const worker = npc(people, 'crowd', 63, { x: -7, y: GY, z: -18.6, ry: -Math.PI / 2, anim: 'walk' }); worker.setSpeed(1.1);
  const driver = npc(people, 'crowd', 64, { x: 35.4, y: GY, z: -32.8, ry: 2.6, anim: 'phone' });
  const crew = [coworker, staff1, staff2, worker, driver];
  for (const p of crew) dyn.add(p.group);
  let pace = 1;

  // ---- state ----
  const state = { alerts: 0, dashboard: null, outside: null };
  function setState(key, v) {
    if (key === 'dashboard') {
      if (!v || !(v.width > 0) || !(v.height > 0)) return void console.error('[office] dashboard must be a canvas with a size', v);
      state.dashboard = v;
      if (dashTex.image === v || (dashTex.image.width === v.width && dashTex.image.height === v.height)) { dashTex.image = v; dashTex.needsUpdate = true; }
      else { const old = dashTex; dashTex = kit.texture(v); dashMat.map = dashMat.emissiveMap = dashTex; dashMat.needsUpdate = true; old.dispose(); }
    } else if (key === 'alerts') {
      const n = Math.round(Number(v));
      if (!(n >= 0 && n <= 3)) return void console.error('[office] alerts must be 0..3', v);
      state.alerts = wallAlerts = n; redrawWall();
    } else if (key === 'outside') { if (v !== null && !MOODS.includes(v)) return void console.error('[office] outside must be null or', MOODS, v); state.outside = v; }
    else console.error('[office] unknown state', key, v);
  }

  const P = (id, label, x, z, kind, rad = 1.6) => ({ id, label, x, y: 0, z, r: rad, kind });
  const points = [
    P('desk', '내 자리 · 대시보드 확인', ME.x, ME.z + 1.3, 'desk', 1.5), P('coworker', '윤 팀장', 5.75, LEAD.z, 'npc', 1.8),
    P('window', '창밖 보기 · 물류센터 마당', 0, -11.2, 'look', 2.6), P('officeExit', '퇴근하기 · 출입문', DOOR, -0.7, 'door', 1.4),
  ];

  kit.merge(stat); kit.merge(yard);
  let acc = 0, rmT = 0, reduced = false;
  return {
    id: 'office', group, env: 'interior',
    walk: [{ x0: -9, x1: 9, z0: -12, z1: 0, y: 0 }],
    solids, points,
    spawns: { entry: { x: DOOR, y: 0, z: -1.3, yaw: 0 }, desk: { x: ME.x, y: 0, z: ME.z + 1.25, yaw: 0 } },
    npcs: { coworker, staff1, staff2, worker, driver },
    states: { alerts: [0, 1, 2, 3], dashboard: ['(canvas)'], outside: [null, ...MOODS] },
    setState(key, v) { if (key === 'dashboard' && v === '(canvas)') return; setState(key, v); },
    update(dt, t, ctx = {}) {
      view.set(state.outside || moodFor(ctx.env, ctx.env === 'interiorNight' ? 'night' : 'dusk'));
      if ((rmT -= dt) <= 0) { rmT = 1; reduced = !!(globalThis.document?.documentElement?.classList?.contains('reduce-motion') || globalThis.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches); }
      hazard.emissiveIntensity = reduced ? 2 : Math.sin(t * Math.PI * 2.6) > 0 ? 3 : 0;   // reduced motion: steady lamp, rows stop blinking
      acc += dt; if (acc > 0.6) { acc = 0; const b = wallAlerts && !reduced ? !blink : false; if (b !== blink) { blink = b; redrawWall(); } }
      const wg = worker.group; wg.position.x += pace * 1.1 * dt; // paces between the pallet stacks below the window
      if (wg.position.x > 6 || wg.position.x < -8) { pace = -pace; wg.position.x = Math.max(-8, Math.min(6, wg.position.x)); }
      wg.rotation.y = pace > 0 ? -Math.PI / 2 : Math.PI / 2;
      for (const p of crew) p.update(dt);
    },
    anchors: {
      deskCam: { x: ME.x, y: 1.2, z: ME.z + 1.15, look: { x: ME.x - 0.2, y: 1.0, z: ME.z + 0.18 } },
      wallScreen: { x: -6.2, y: 1.6, z: -6.5, look: { x: -8.9, y: 1.7, z: -6.5 } },
      window: { x: 0, y: 1.6, z: -11.3, look: { x: 0, y: GY, z: -40 } },
      lead: { x: 5.6, y: 1.6, z: LEAD.z, look: { x: LEAD.x, y: 1.1, z: LEAD.z } },
    },
  };
}
