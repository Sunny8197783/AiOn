// Canvas textures for the basement car-park shelter: green epoxy floor, pillar zone labels, painted floor
// markings, exit signs (피난구·통로 유도등), anti-slip ramp concrete, tactile paving, notice sheets, roster, banner.
import { FONT } from '../engine/kit.js';

export const ZONES = [
  { letter: 'A', color: '#2f8f5b' },  // north pillar row
  { letter: 'B', color: '#d99a1e' },  // middle row
  { letter: 'C', color: '#2f6db5' },  // south row
];

export function shelterTextures(kit) {
  const { THREE, canvas, texture, normalFrom, memo, rng } = kit;
  const S = kit.quality === 'low' ? 512 : 1024;

  function scribble(g, r, x, y, w, color = 'rgba(40,44,52,0.55)', lw = 2) { // handwriting-like line
    g.strokeStyle = color; g.lineWidth = lw; g.beginPath(); g.moveTo(x, y);
    for (let px = x; px < x + w; px += 6 + r() * 6) g.lineTo(px, y + (r() - 0.5) * lw * 2.5);
    g.stroke();
  }

  // ---- green epoxy floor: mottled coat, trowel arcs, worn lanes, tyre marks, oil stains (4 m tile) ----
  function floorMaterial() {
    return memo('sh:floor', () => {
      const r = rng(4021);
      const color = canvas(S, S, (g, w, h) => {
        g.fillStyle = '#5b7867'; g.fillRect(0, 0, w, h);
        for (let i = 0; i < 26; i++) { g.fillStyle = `rgba(${r() < 0.5 ? '40,60,50' : '150,175,160'},${0.04 + r() * 0.06})`; g.beginPath(); g.ellipse(r() * w, r() * h, 40 + r() * 160, 30 + r() * 110, r() * 3, 0, 7); g.fill(); }
        g.strokeStyle = 'rgba(255,255,255,0.025)'; g.lineWidth = 6;
        for (let i = 0; i < 40; i++) { g.beginPath(); g.arc(r() * w, r() * h, 30 + r() * 90, r() * 6, r() * 6 + 1.2); g.stroke(); }
        for (let i = 0; i < S * 14; i++) { g.fillStyle = r() < 0.5 ? 'rgba(20,30,25,0.18)' : 'rgba(220,235,225,0.12)'; g.fillRect(r() * w, r() * h, 1.2, 1.2); }
        for (let i = 0; i < 7; i++) { // tyre marks: paired dark arcs
          const x = r() * w, y = r() * h, rad = 200 + r() * 500, a0 = r() * 6;
          g.strokeStyle = `rgba(18,20,20,${0.05 + r() * 0.06})`; g.lineWidth = 12 + r() * 8;
          for (const off of [0, 46]) { g.beginPath(); g.arc(x, y, rad + off, a0, a0 + 0.6 + r() * 0.5); g.stroke(); }
        }
        for (let i = 0; i < 5; i++) { // oil drips
          g.fillStyle = `rgba(25,22,18,${0.12 + r() * 0.12})`; g.beginPath(); g.ellipse(r() * w, r() * h, 8 + r() * 24, 6 + r() * 16, r() * 3, 0, 7); g.fill();
        }
        for (let i = 0; i < 18; i++) { g.fillStyle = 'rgba(230,240,232,0.08)'; g.fillRect(r() * w, r() * h, 2 + r() * 40, 1 + r() * 2); } // scuffs
      });
      const height = canvas(S / 2, S / 2, (g, w, h) => { g.fillStyle = '#808080'; g.fillRect(0, 0, w, h); for (let i = 0; i < w * 20; i++) { g.fillStyle = r() < 0.5 ? '#868686' : '#7a7a7a'; g.fillRect(r() * w, r() * h, 1.5, 1.5); } });
      const rough = canvas(S / 2, S / 2, (g, w, h) => {
        g.fillStyle = '#6a6a6a'; g.fillRect(0, 0, w, h);
        for (let i = 0; i < 30; i++) { g.fillStyle = `rgba(${r() < 0.6 ? '200,200,200' : '70,70,70'},${0.1 + r() * 0.25})`; g.beginPath(); g.ellipse(r() * w, r() * h, 10 + r() * 60, 6 + r() * 30, r() * 3, 0, 7); g.fill(); }
      });
      const rep = [0.25, 0.25];
      return new THREE.MeshStandardMaterial({
        map: texture(color, { repeat: rep }), normalMap: texture(normalFrom(height, 1.2), { repeat: rep, color: false }),
        roughnessMap: texture(rough, { repeat: rep, color: false }), roughness: 0.75, metalness: 0, normalScale: new THREE.Vector2(0.35, 0.35),
      });
    });
  }

  // ---- concrete ramp with the round anti-slip grooves Korean car ramps have (1 m tile) ----
  function grooveMaterial() {
    return memo('sh:groove', () => {
      const n = 256, r = rng(77);
      const color = canvas(n, n, (g) => {
        g.fillStyle = '#8f8f8a'; g.fillRect(0, 0, n, n);
        for (let i = 0; i < n * 30; i++) { g.fillStyle = r() < 0.5 ? 'rgba(0,0,0,0.08)' : 'rgba(255,255,255,0.06)'; g.fillRect(r() * n, r() * n, 1.3, 1.3); }
        g.strokeStyle = 'rgba(40,40,38,0.55)'; g.lineWidth = 5;
        for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) { g.beginPath(); g.arc(32 + x * 64, 32 + y * 64, 22, 0, 7); g.stroke(); }
      });
      const height = canvas(n, n, (g) => {
        g.fillStyle = '#909090'; g.fillRect(0, 0, n, n); g.strokeStyle = '#303030'; g.lineWidth = 5;
        for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) { g.beginPath(); g.arc(32 + x * 64, 32 + y * 64, 22, 0, 7); g.stroke(); }
      });
      return new THREE.MeshStandardMaterial({ map: texture(color), normalMap: texture(normalFrom(height, 3), { color: false }), roughness: 0.92 });
    });
  }

  // ---- yellow tactile warning blocks (점형블록), 30 cm tiles with 6x6 domes ----
  function tactileMaterial() {
    return memo('sh:tactile', () => {
      const n = 128;
      const color = canvas(n, n, (g) => { g.fillStyle = '#e8b818'; g.fillRect(0, 0, n, n); g.fillStyle = 'rgba(0,0,0,0.25)'; g.fillRect(0, 0, n, 2); g.fillRect(0, 0, 2, n); for (let y = 0; y < 6; y++) for (let x = 0; x < 6; x++) { g.fillStyle = 'rgba(255,240,170,0.5)'; g.beginPath(); g.arc(11 + x * 21, 11 + y * 21, 6, 0, 7); g.fill(); } });
      const height = canvas(n, n, (g) => { g.fillStyle = '#404040'; g.fillRect(0, 0, n, n); g.fillStyle = '#e0e0e0'; for (let y = 0; y < 6; y++) for (let x = 0; x < 6; x++) { g.beginPath(); g.arc(11 + x * 21, 11 + y * 21, 6.5, 0, 7); g.fill(); } });
      return new THREE.MeshStandardMaterial({ map: texture(color), normalMap: texture(normalFrom(height, 5), { color: false }), roughness: 0.6 });
    });
  }

  // ---- yellow/black chevrons for ramp walls, speed bumps and height bars (1 m tile) ----
  function hazardMaterial() {
    return memo('sh:hazard', () => new THREE.MeshStandardMaterial({ map: texture(canvas(128, 128, (g) => {
      g.fillStyle = '#e9b21c'; g.fillRect(0, 0, 128, 128); g.fillStyle = '#1d1d1d';
      for (let i = -2; i < 4; i++) { g.beginPath(); g.moveTo(i * 64, 128); g.lineTo(i * 64 + 32, 128); g.lineTo(i * 64 + 160, 0); g.lineTo(i * 64 + 128, 0); g.closePath(); g.fill(); }
    })), roughness: 0.55 }));
  }

  // ---- pillar labels: one atlas, 5 columns x 3 zone rows; each cell 'A1' + '지하1층' ----
  function pillarAtlas() {
    return memo('sh:pillars', () => {
      const cw = 160, ch = 200;
      const c = canvas(cw * 5, ch * 3, (g) => {
        ZONES.forEach((z, row) => { for (let col = 0; col < 5; col++) {
          const x = col * cw, y = row * ch;
          g.fillStyle = '#f4f2ec'; g.fillRect(x, y, cw, ch);
          g.fillStyle = z.color; g.fillRect(x + 8, y + 8, cw - 16, ch - 58);
          g.fillStyle = '#ffffff'; g.textAlign = 'center'; g.textBaseline = 'middle';
          g.font = `800 92px ${FONT}`; g.fillText(`${z.letter}${col + 1}`, x + cw / 2, y + (ch - 50) / 2 + 4);
          g.fillStyle = '#333'; g.font = `700 30px ${FONT}`; g.fillText('지하 1층', x + cw / 2, y + ch - 26);
        } });
      });
      const map = texture(c);
      return { material: new THREE.MeshStandardMaterial({ map, roughness: 0.6 }), cell: (row, col) => [col / 5, 1 - (row + 1) / 3, (col + 1) / 5, 1 - row / 3] };
    });
  }

  // ---- painted floor markings atlas (transparent): 0 arrow, 1 wheelchair, 2 '서행', 3 'B1', 4 '경차', 5 hatch ----
  function markAtlas() {
    return memo('sh:marks', () => {
      const n = 256;
      const c = canvas(n * 6, n, (g) => {
        g.clearRect(0, 0, n * 6, n);
        g.fillStyle = '#f1efe6'; // arrow
        g.beginPath(); g.moveTo(128, 14); g.lineTo(206, 110); g.lineTo(156, 110); g.lineTo(156, 242); g.lineTo(100, 242); g.lineTo(100, 110); g.lineTo(50, 110); g.closePath(); g.fill();
        g.fillStyle = '#2a64b8'; g.fillRect(n + 24, 24, 208, 208); // wheelchair bay symbol
        g.strokeStyle = '#ffffff'; g.fillStyle = '#ffffff'; g.lineWidth = 16; g.lineCap = 'round';
        g.beginPath(); g.arc(n + 120, 62, 16, 0, 7); g.fill();
        g.beginPath(); g.moveTo(n + 118, 86); g.lineTo(n + 118, 150); g.lineTo(n + 176, 150); g.lineTo(n + 196, 196); g.stroke();
        g.beginPath(); g.moveTo(n + 118, 112); g.lineTo(n + 160, 112); g.stroke();
        g.lineWidth = 12; g.beginPath(); g.arc(n + 114, 168, 44, -1.1, 3.9); g.stroke();
        g.fillStyle = '#f1efe6'; g.textAlign = 'center'; g.textBaseline = 'middle';
        g.font = `900 110px ${FONT}`; g.fillText('서행', n * 2 + 128, 128);
        g.lineWidth = 12; g.strokeStyle = '#f1efe6'; g.beginPath(); g.arc(n * 3 + 128, 128, 104, 0, 7); g.stroke();
        g.font = `900 120px ${FONT}`; g.fillText('B1', n * 3 + 128, 134);
        g.fillStyle = '#4c8fd6'; g.font = `900 104px ${FONT}`; g.fillText('경차', n * 4 + 128, 128);
        g.save(); g.beginPath(); g.rect(n * 5, 0, n, n); g.clip(); g.fillStyle = '#e9c22a';
        for (let i = -4; i < 8; i++) { g.beginPath(); g.moveTo(n * 5 + i * 48, n); g.lineTo(n * 5 + i * 48 + 18, n); g.lineTo(n * 5 + i * 48 + 18 + n, 0); g.lineTo(n * 5 + i * 48 + n, 0); g.closePath(); g.fill(); }
        g.restore();
      });
      const map = texture(c);
      const material = new THREE.MeshStandardMaterial({ map, transparent: true, roughness: 0.5, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
      return { material, cell: (i) => [i / 6, 0, (i + 1) / 6, 1] };
    });
  }

  // Running figure for exit pictograms. dir 1 = runs right.
  function runner(g, x, y, s, color, dir = 1) {
    g.strokeStyle = color; g.fillStyle = color; g.lineWidth = 11 * s; g.lineCap = 'round'; g.lineJoin = 'round';
    const P = (px, py) => [x + px * s * dir, y + py * s];
    const line = (...pts) => { g.beginPath(); g.moveTo(...P(...pts[0])); for (const p of pts.slice(1)) g.lineTo(...P(...p)); g.stroke(); };
    g.beginPath(); g.arc(...P(14, -58), 11 * s, 0, 7); g.fill();
    line([6, -40], [-6, 6]); line([-6, 6], [18, 30], [12, 58]); line([-6, 6], [-26, 30], [-46, 30]);
    line([-26, -22], [4, -34], [26, -12]);
  }
  // 피난구유도등: green field, white figure running out through a door. 'face' toggles text strip.
  function exitTex() {
    return memo('sh:exit', () => texture(canvas(512, 256, (g) => {
      g.fillStyle = '#0d8a43'; g.fillRect(0, 0, 512, 256);
      g.fillStyle = '#f5fff6'; g.fillRect(300, 44, 150, 176); g.fillStyle = '#0d8a43'; g.fillRect(318, 60, 114, 160);
      runner(g, 220, 140, 1.45, '#f5fff6', 1);
      g.fillStyle = '#f5fff6'; g.font = `800 34px ${FONT}`; g.textAlign = 'center'; g.fillText('비상구', 376, 150);
    }), { repeat: [1, 1] }));
  }
  // 통로유도등: white field, green arrow and figure. dir 1 = right, -1 = left.
  function aisleTex(dir = 1) {
    return memo(`sh:aisle:${dir}`, () => texture(canvas(512, 192, (g) => {
      g.fillStyle = '#f3f8f1'; g.fillRect(0, 0, 512, 192);
      g.strokeStyle = '#0d8a43'; g.lineWidth = 8; g.strokeRect(6, 6, 500, 180);
      runner(g, 256 - dir * 60, 104, 1.05, '#0d8a43', dir);
      g.fillStyle = '#0d8a43'; g.save(); g.translate(256 + dir * 120, 96); g.scale(dir, 1);
      g.beginPath(); g.moveTo(-70, -18); g.lineTo(10, -18); g.lineTo(10, -44); g.lineTo(70, 0); g.lineTo(10, 44); g.lineTo(10, 18); g.lineTo(-70, 18); g.closePath(); g.fill(); g.restore();
    }), { repeat: [1, 1] }));
  }

  // ---- notice board: header, pinned sheets; director text on the first sheet (no invented advice) ----
  function boardCanvas() { return memo('sh:boardCanvas', () => canvas(1024, 640)); }
  function drawBoard(lines) {
    const c = boardCanvas(), g = c.getContext('2d'), r = rng(19);
    g.fillStyle = '#f6f6f2'; g.fillRect(0, 0, 1024, 640);
    g.fillStyle = '#1f4e8c'; g.fillRect(0, 0, 1024, 92);
    g.fillStyle = '#ffffff'; g.font = `800 52px ${FONT}`; g.textAlign = 'left'; g.textBaseline = 'middle'; g.fillText('대피소 알림', 34, 48);
    g.font = `600 26px ${FONT}`; g.textAlign = 'right'; g.fillText('연무 생활문화센터 지하주차장', 990, 48);
    const sheet = (x, y, w, h, rot, head, body) => {
      g.save(); g.translate(x + w / 2, y + h / 2); g.rotate(rot); g.translate(-w / 2, -h / 2);
      g.fillStyle = 'rgba(0,0,0,0.12)'; g.fillRect(5, 6, w, h); g.fillStyle = '#ffffff'; g.fillRect(0, 0, w, h);
      g.fillStyle = '#d7262d'; g.beginPath(); g.arc(w / 2, 12, 8, 0, 7); g.fill();
      g.fillStyle = '#20242a'; g.textAlign = 'left'; g.textBaseline = 'top';
      if (head) { g.font = `800 30px ${FONT}`; g.fillText(head, 22, 30); }
      let ty = head ? 80 : 34;
      for (const line of body) {
        if (typeof line === 'string') {
          let size = 26; g.font = `600 ${size}px ${FONT}`;
          while (g.measureText(line).width > w - 44 && size > 14) { size -= 2; g.font = `600 ${size}px ${FONT}`; }
          g.fillText(line, 22, ty); ty += size + 14;
        } else { scribble(g, r, 22, ty + 10, line, 'rgba(60,64,72,0.45)', 3); ty += 28; }
      }
      g.restore();
    };
    const main = lines?.length ? lines.slice(0, 9) : [180, 260, 220, 240];
    sheet(40, 120, 520, 480, -0.01, lines?.length ? null : '안내', main);
    sheet(600, 120, 380, 230, 0.02, '대피자 명부', [300, 260, 280]);
    sheet(610, 380, 360, 220, -0.015, '구호물품', [220, 280, 180]);
    const t = memo('sh:boardTex', () => texture(c, { repeat: [1, 1] }));
    t.needsUpdate = true;
    return t;
  }

  // ---- clipboard roster sheet (명부): ruled table with handwritten rows ----
  function rosterTex() {
    return memo('sh:roster', () => texture(canvas(256, 340, (g) => {
      const r = rng(5);
      g.fillStyle = '#fbfbf7'; g.fillRect(0, 0, 256, 340);
      g.fillStyle = '#111'; g.font = `800 24px ${FONT}`; g.textAlign = 'center'; g.fillText('대피자 명부', 128, 30);
      g.strokeStyle = 'rgba(30,30,30,0.5)'; g.lineWidth = 1;
      for (let i = 0; i < 13; i++) { g.beginPath(); g.moveTo(12, 48 + i * 22); g.lineTo(244, 48 + i * 22); g.stroke(); }
      for (const x of [12, 44, 120, 190, 244]) { g.beginPath(); g.moveTo(x, 48); g.lineTo(x, 312); g.stroke(); }
      for (let i = 0; i < 8; i++) { scribble(g, r, 50, 59 + i * 22, 50 + r() * 20, 'rgba(20,40,120,0.75)', 1.6); scribble(g, r, 126, 59 + i * 22, 40 + r() * 20, 'rgba(20,40,120,0.75)', 1.6); }
    }), { repeat: [1, 1] }));
  }

  // ---- cloth banner ----
  function bannerTex(text) {
    return memo(`sh:banner:${text}`, () => texture(canvas(1536, 224, (g) => {
      g.fillStyle = '#f7f5ee'; g.fillRect(0, 0, 1536, 224);
      g.fillStyle = '#1f4e8c'; g.fillRect(0, 0, 1536, 18); g.fillRect(0, 206, 1536, 18);
      g.fillStyle = '#d7262d'; g.fillRect(40, 44, 110, 140);
      g.fillStyle = '#fff'; g.beginPath(); g.arc(95, 100, 38, 0, 7); g.fill(); g.fillStyle = '#f39200'; g.beginPath(); g.arc(95, 100, 26, 0, 7); g.fill();
      g.fillStyle = '#1d4fa0'; g.beginPath(); g.moveTo(95, 82); g.lineTo(112, 112); g.lineTo(78, 112); g.closePath(); g.fill();
      g.fillStyle = '#16233a'; g.font = `900 104px ${FONT}`; g.textAlign = 'left'; g.textBaseline = 'middle';
      let size = 104; while (g.measureText(text).width > 1300 && size > 40) { size -= 4; g.font = `900 ${size}px ${FONT}`; }
      g.fillText(text, 190, 118);
    }), { repeat: [1, 1] }));
  }

  // ---- hydrant cabinet face (옥내소화전함) ----
  function hydrantTex() {
    return memo('sh:hydrant', () => texture(canvas(256, 400, (g) => {
      g.fillStyle = '#c8261f'; g.fillRect(0, 0, 256, 400);
      g.strokeStyle = 'rgba(0,0,0,0.35)'; g.lineWidth = 6; g.strokeRect(14, 14, 228, 372);
      g.fillStyle = '#ffffff'; g.textAlign = 'center'; g.font = `900 60px ${FONT}`; g.fillText('소화전', 128, 120);
      g.font = `700 26px ${FONT}`; g.fillText('HYDRANT', 128, 160);
      g.fillStyle = 'rgba(255,255,255,0.85)'; g.fillRect(40, 220, 176, 70); g.fillStyle = '#c8261f'; g.font = `800 28px ${FONT}`; g.fillText('발신기', 128, 266);
      g.fillStyle = '#222'; g.fillRect(214, 190, 14, 60);
    }), { repeat: [1, 1] }));
  }

  // ---- six-pack of 2 L bottles seen from the side (shrink wrap + labels) ----
  function waterPackTex() {
    return memo('sh:waterpack', () => texture(canvas(256, 128, (g) => {
      g.fillStyle = '#a9c9de'; g.fillRect(0, 0, 256, 128);
      for (let i = 0; i < 3; i++) {
        const x = i * 85;
        g.fillStyle = 'rgba(255,255,255,0.35)'; g.fillRect(x + 10, 0, 18, 128);
        g.fillStyle = '#1e5fb0'; g.fillRect(x + 4, 52, 78, 38);
        g.fillStyle = '#ffffff'; g.font = `800 20px ${FONT}`; g.textAlign = 'center'; g.fillText('생수 2L', x + 43, 77);
        g.fillStyle = '#2b7bd0'; g.fillRect(x + 30, 0, 26, 8);
      }
    }), { repeat: [1, 1] }));
  }
  function cartonTex(label) {
    return memo(`sh:carton:${label}`, () => texture(canvas(256, 160, (g) => {
      g.fillStyle = '#b48a5a'; g.fillRect(0, 0, 256, 160);
      g.fillStyle = 'rgba(0,0,0,0.08)'; g.fillRect(0, 70, 256, 18);
      g.fillStyle = '#2b2b2b'; g.font = `800 34px ${FONT}`; g.textAlign = 'center'; g.fillText(label, 128, 62);
      g.font = `600 20px ${FONT}`; g.fillText('구호물품 · 행정복지센터', 128, 130);
    }), { repeat: [1, 1] }));
  }

  return { floorMaterial, grooveMaterial, tactileMaterial, hazardMaterial, pillarAtlas, markAtlas, exitTex, aisleTex, drawBoard, rosterTex, bannerTex, hydrantTex, waterPackTex, cartonTex };
}

// A plane whose UVs cover one atlas cell [u0, v0, u1, v1].
export function cellPlane(THREE, w, h, cell) {
  const geo = new THREE.PlaneGeometry(w, h), uv = geo.attributes.uv;
  for (let i = 0; i < uv.count; i++) uv.setXY(i, cell[0] + uv.getX(i) * (cell[2] - cell[0]), cell[1] + uv.getY(i) * (cell[3] - cell[1]));
  return geo;
}
