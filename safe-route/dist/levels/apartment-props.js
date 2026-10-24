// 302호 props that follow the director's states: the battery radio on the living-room table (dial glows when on),
// a candle that flickers and lights the room (fire-risk prop), the emergency backpack by the door (empty / packed)
// and the wall planner board (계획판) the 1장 slots are chosen on.
import * as THREE from 'three';
import { FL } from './apartment-parts.js';
import { HOME } from './apartment-home.js';
import { FONT } from '../engine/kit.js';

export function buildProps(B, g, dyn) {
  const { kit, M } = B, T = HOME.top;
  const P = (hex, roughness = 0.6, metalness = 0) => kit.mat('paint:' + hex, { roughness, metalness });

  // ---- radio (휴대용 라디오): body, speaker grille, lit dial window, knobs, handle, extended antenna ----
  const radio = new THREE.Group(); radio.position.set(-2.2, T, 5.88); radio.rotation.y = Math.PI + 0.35; dyn.add(radio);
  const dial = new THREE.MeshStandardMaterial({ color: 0x1a1408, map: B.tex('radioDial', 256, 112, drawDial), emissive: 0xffb04a, emissiveMap: B.tex('radioDial', 256, 112, drawDial), emissiveIntensity: 0, roughness: 0.25 });
  const led = new THREE.MeshStandardMaterial({ color: 0x0b1a0e, emissive: 0x3cff6a, emissiveIntensity: 0, roughness: 0.4 });
  radio.add(kit.rbox(0.26, 0.15, 0.075, 0.016, P('#3a3d40', 0.45, 0.2), {}));
  const face = (w, h, m, x, y) => { const p = new THREE.Mesh(new THREE.PlaneGeometry(w, h), m); p.position.set(x, y, 0.0385); radio.add(p); return p; };
  face(0.11, 0.1, new THREE.MeshStandardMaterial({ map: B.tex('radioGrille', 128, 128, drawGrille), roughness: 0.6, metalness: 0.3 }), -0.06, 0.075);
  face(0.1, 0.044, dial, 0.065, 0.1);
  radio.add(kit.cyl(0.017, 0.017, 0.02, M.black, { x: 0.065, y: 0.025, z: 0.045, rx: Math.PI / 2 }, 18), kit.cyl(0.011, 0.011, 0.016, M.black, { x: 0.105, y: 0.03, z: 0.043, rx: Math.PI / 2 }, 14));
  radio.add(kit.box(0.008, 0.008, 0.004, led, { x: 0.02, y: 0.045, z: 0.038 }));
  radio.add(kit.box(0.2, 0.014, 0.022, M.black, { y: 0.17 }), kit.box(0.014, 0.03, 0.02, M.black, { x: -0.093, y: 0.145 }), kit.box(0.014, 0.03, 0.02, M.black, { x: 0.093, y: 0.145 }));
  B.rod(radio, [0.11, 0.15, -0.02], [0.2, 0.5, -0.09], 0.0035, M.metal); kit.merge(radio);
  const radioPt = B.point('homeRadio', '라디오', -2.0, FL, 5.95, 'radio', 1.7);
  function setRadio(v) {
    radio.visible = v !== 'none';
    dial.emissiveIntensity = v === 'on' ? 1.7 : 0; led.emissiveIntensity = v === 'on' ? 2.2 : 0;
    radioPt.label = v === 'none' ? '거실 탁자' : '라디오';
  }

  // ---- candle on a saucer: additive flame sprite + halo + a warm flickering light (created once: no recompiles) ----
  const candle = new THREE.Group(); candle.position.set(-1.62, T, 6.12); dyn.add(candle);
  candle.add(kit.cyl(0.055, 0.06, 0.012, P('#f1ede4', 0.3), {}, 20), kit.cyl(0.027, 0.027, 0.1, P('#f6f1e2', 0.7), { y: 0.012 }, 18), kit.cyl(0.0018, 0.0018, 0.012, M.black, { y: 0.112 }, 6));
  const sprite = (tex, color, sx, sy, y) => {
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, color, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true }));
    s.scale.set(sx, sy, 1); s.position.y = y; candle.add(s); return s;
  };
  const flame = sprite(B.tex('candleFlame', 64, 128, drawFlame), new THREE.Color(2.6, 1.9, 1.1), 0.026, 0.06, 0.148);
  const halo = sprite(B.tex('candleHalo', 64, 64, drawHalo), new THREE.Color(0.55, 0.32, 0.12), 0.34, 0.34, 0.14); kit.merge(candle);
  const light = new THREE.PointLight(0xff9a40, 0, 4.5, 2); light.position.set(-1.62, T + 0.2, 6.12); dyn.add(light);
  let lit = false, wasLit = false;
  function setCandle(v) { lit = v === 'lit'; wasLit ||= lit; candle.visible = lit || wasLit; flame.visible = halo.visible = lit; if (!lit) light.intensity = 0; }

  // ---- emergency backpack by the door: packed (full, bottle in the side pocket) or empty (slumped) ----
  const bag = new THREE.Group(); bag.position.set(-2.25, FL, 1.78); bag.rotation.y = Math.PI / 2; dyn.add(bag);
  const fab = P('#2f3642', 0.95), fab2 = P('#3d4656', 0.95);
  bag.add(kit.rbox(0.32, 0.46, 0.22, 0.07, fab, {}), kit.rbox(0.26, 0.22, 0.07, 0.03, fab2, { y: 0.04, z: 0.12 }));
  bag.add(kit.box(0.22, 0.012, 0.004, P('#c9ccd0', 0.2, 0.6), { y: 0.3, z: 0.111 }), kit.box(0.16, 0.018, 0.004, P('#e8e8e2', 0.3), { y: 0.2, z: 0.157 }));
  for (const x of [-0.08, 0.08]) bag.add(kit.box(0.05, 0.4, 0.02, P('#1b1f26', 0.9), { x, y: 0.04, z: -0.12 }));
  B.rod(bag, [-0.04, 0.46, 0], [0.04, 0.46, 0], 0.008, P('#1b1f26', 0.9));
  const bottle = kit.cyl(0.033, 0.033, 0.2, P('#bfe3f2', 0.12), { x: 0.185, y: 0.05 }, 14); bottle.userData.keep = true; bag.add(bottle); kit.merge(bag);
  const bagPt = B.point('homeBag', '비상가방', -2.0, FL, 1.8, 'bag', 1.4);
  function setBag(v) {
    bag.visible = v !== 'none'; bottle.visible = v === 'packed';
    if (v === 'packed') bag.scale.set(1, 1, 1); else bag.scale.set(1, 0.7, 0.42);
    bag.position.x = v === 'packed' ? -2.25 : -2.31;
    bagPt.label = v === 'none' ? '가방 둘 자리' : v === 'packed' ? '비상가방 (꾸림)' : '비상가방 (비어 있음)';
  }

  // ---- wallet with the ID card on the living-room table (picked up = gone from the table and from E) ----
  const wallet = new THREE.Group(); wallet.position.set(-1.98, T, 6.13); wallet.rotation.y = 0.4; dyn.add(wallet);
  wallet.add(kit.rbox(0.11, 0.022, 0.09, 0.008, P('#4a2e1e', 0.7), {}));
  wallet.add(kit.box(0.085, 0.003, 0.054, P('#dfe7ee', 0.4), { x: 0.035, y: 0.022, z: 0.02, ry: 0.3 })); // card peeking out
  const walletPt = B.point('homeWallet', '지갑 · 신분증', -1.98, FL, 6.13, 'pickup', 1.4);
  function setWallet(v) { wallet.visible = v !== 'taken'; walletPt.enabled = v !== 'taken'; }

  // ---- 계획판: whiteboard with this week's slots, sticky notes and magnets, on the bathroom wall facing the living room ----
  const pz = 3.6, px = -0.4, py = FL + 1.45;
  g.add(kit.box(1.0, 0.72, 0.025, M.alu, { x: px, y: py - 0.36, z: pz + 0.0125 }));
  const board = new THREE.Mesh(new THREE.PlaneGeometry(0.96, 0.68), new THREE.MeshStandardMaterial({ map: B.tex('planner', 512, 364, drawPlanner), roughness: 0.35 }));
  board.position.set(px, py, pz + 0.026); g.add(board);
  g.add(kit.box(0.6, 0.025, 0.05, M.alu, { x: px, y: py - 0.38, z: pz + 0.03 }), kit.cyl(0.008, 0.008, 0.12, P('#1f5fbf', 0.4), { x: px - 0.1, y: py - 0.407, z: pz + 0.04, rz: Math.PI / 2 }, 8));
  B.point('homePlanner', '달력 (계획판)', px, FL, pz + 0.55, 'planner', 1.5);

  function update(dt, t) {
    if (!lit) return;
    const k = 1 + 0.07 * Math.sin(t * 13.1) + 0.05 * Math.sin(t * 7.3 + 1.3) + 0.03 * Math.sin(t * 23.7);
    light.intensity = 2.2 * k;
    flame.scale.y = 0.06 * (0.94 + 0.07 * Math.sin(t * 11.3)); flame.position.x = 0.0015 * Math.sin(t * 5.1);
    halo.material.opacity = 0.85 + 0.12 * Math.sin(t * 9.7);
  }
  setRadio('none'); setCandle('none'); setBag('empty');
  return { setRadio, setCandle, setBag, setWallet, update };
}

function drawDial(c, w, h) {
  c.fillStyle = '#e9c98a'; c.fillRect(0, 0, w, h); c.fillStyle = '#3a2a10'; c.font = `600 15px ${FONT}`; c.textAlign = 'center';
  ['88', '92', '96', '100', '104', '108'].forEach((f, i) => { const x = 18 + i * 44; c.fillText(f, x, 30); c.fillRect(x - 1, 40, 2, 18); });
  for (let x = 14; x < w - 10; x += 8.8) c.fillRect(x, 48, 1, 10);
  c.fillText('FM  MHz', w / 2, 92); c.fillStyle = '#c4161c'; c.fillRect(165, 36, 3, 40);
}
function drawGrille(c, w, h) {
  c.fillStyle = '#2b2e31'; c.fillRect(0, 0, w, h); c.fillStyle = '#121416';
  for (let y = 6; y < h; y += 9) for (let x = 6 + (y % 18 ? 4 : 0); x < w; x += 9) { c.beginPath(); c.arc(x, y, 2.6, 0, 7); c.fill(); }
}
function drawFlame(c, w, h) {
  const gr = c.createRadialGradient(w / 2, h * 0.68, 2, w / 2, h * 0.6, h * 0.45);
  gr.addColorStop(0, 'rgba(255,255,240,1)'); gr.addColorStop(0.25, 'rgba(255,214,120,0.95)'); gr.addColorStop(0.6, 'rgba(255,130,40,0.5)'); gr.addColorStop(1, 'rgba(255,80,10,0)');
  c.fillStyle = gr; c.beginPath(); c.moveTo(w / 2, 2); c.bezierCurveTo(w * 0.95, h * 0.5, w * 0.85, h * 0.95, w / 2, h - 2); c.bezierCurveTo(w * 0.15, h * 0.95, w * 0.05, h * 0.5, w / 2, 2); c.fill();
}
function drawHalo(c, w, h) {
  const gr = c.createRadialGradient(w / 2, h / 2, 0, w / 2, h / 2, w / 2); gr.addColorStop(0, 'rgba(255,200,140,0.9)'); gr.addColorStop(1, 'rgba(255,160,80,0)');
  c.fillStyle = gr; c.fillRect(0, 0, w, h);
}
function drawPlanner(c, w, h) {
  c.fillStyle = '#fbfbf8'; c.fillRect(0, 0, w, h);
  c.fillStyle = '#2a2f36'; c.font = `800 26px ${FONT}`; c.textAlign = 'left'; c.fillText('이번 주 계획', 18, 36);
  const days = ['월', '화', '수', '목', '금', '토', '일'], cw = 54, x0 = 18, y0 = 54;
  days.forEach((d, i) => {
    const x = x0 + i * cw;
    c.fillStyle = i === 2 || i === 3 ? '#fff3d6' : '#f1f2f0'; c.fillRect(x, y0, cw - 4, 270);
    c.fillStyle = i >= 5 ? '#c4161c' : '#3a3f46'; c.font = `700 18px ${FONT}`; c.textAlign = 'center'; c.fillText(d, x + cw / 2 - 2, y0 + 22);
  });
  const tile = (x, y) => { c.strokeStyle = '#c99a2e'; c.lineWidth = 2; c.setLineDash([5, 4]); c.strokeRect(x, y, cw - 14, 34); c.setLineDash([]); };
  tile(x0 + 2 * cw + 5, y0 + 226);
  for (let i = 0; i < 5; i++) tile(x0 + 3 * cw + 5, y0 + 40 + i * 46);
  c.fillStyle = '#8a8f96'; c.font = `500 13px ${FONT}`; c.textAlign = 'left'; c.fillText('저녁 1칸 · 내일 5칸', x0 + 2 * cw, y0 + 290);
  const note = (x, y, r, bg, lines) => { c.save(); c.translate(x, y); c.rotate(r); c.fillStyle = bg; c.fillRect(0, 0, 112, 92); c.fillStyle = '#3a3320'; c.font = `600 15px ${FONT}`; lines.forEach((l, i) => c.fillText(l, 10, 28 + i * 22)); c.restore(); };
  note(396, 60, 0.04, '#fff07a', ['관리비', '25일까지']);
  note(392, 170, -0.05, '#ffc3d0', ['분리수거', '화 · 금']);
  c.fillStyle = '#d33'; c.beginPath(); c.arc(452, 64, 6, 0, 7); c.fill(); c.fillStyle = '#36c'; c.beginPath(); c.arc(448, 174, 6, 0, 7); c.fill();
}
