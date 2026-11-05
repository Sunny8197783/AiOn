// Lived-in detail for 302호 (apartment-home.js), by graphics tier (soft-goods.js detailOf; '완전 낮음' keeps the
// plain shapes and adds nothing): real potted plants (인도고무나무 by the TV, 산세베리아 and 스킨답서스 on the
// 베란다), sneakers on soles and 실내화 at the step, a wire 식기건조대 with plates, bowls and a 수저통, pink
// 고무장갑 over the sink edge, a 행주 on a cabinet handle, the 행정복지센터 wall calendar by the fridge, a knit
// throw over the sofa back and soft cushions, the TV stand's 공유기, photo frame, reed diffuser and cable, a puffy
// duvet hanging over the bed, the nightstand's phone and book, a standing 빨래건조대 with towels and socks (its own
// solid), detergent on the washing machine and washing heaped in the basket. Static meshes on shared materials:
// kit.merge folds them into the room's draw calls.
import * as THREE from 'three';
import { FONT } from '../engine/kit.js';
import { detailOf, join, pillowGeo, drapeGeo, ribbonGeo, shoeGeo, soleGeo, slipperGeo, plantGeo, potGeo, twoSided } from './soft-goods.js';

export function homeDetail(B, g, { W, P, glow, F3, FL, BF, CT }) {
  const { kit, M } = B, lv = detailOf(kit.Q), r = kit.rng(3020);
  const mesh = (geo, mat, x = 0, y = 0, z = 0, o = {}) => {
    const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.rotation.set(o.rx || 0, o.ry || 0, o.rz || 0);
    m.castShadow = o.cast ?? true; m.receiveShadow = true; g.add(m); return m;
  };
  const box = (w, h, d, m, x, y, z, o) => g.add(kit.box(w, h, d, m, { x, y, z, ...o }));
  const cyl = (rt, rb, h, m, x, y, z, o, seg = 12) => g.add(kit.cyl(rt, rb, h, m, { x, y, z, ...o }, seg));
  const leafMat = kit.mat('paint:#3f5f2e', { roughness: 0.55, side: THREE.DoubleSide }), soil = P('#3a2d22', 1);
  // Along-z cloth: ribbonGeo's profile is [[z, y]] (world), width across x, then moved to x.
  const hangZ = (path, width, x, mat, o = {}) => mesh(ribbonGeo(path, width, o).rotateY(-Math.PI / 2), mat, x, 0, 0, { cast: o.cast ?? false });

  // ---- potted plants: kind 'rubber' | 'snake' | 'pothos' in a lathe pot of rt x h ----
  function plant(kind, x, y, z, rt, h, potMat, s = 1) { // '완전 낮음': fewer leaves on the old foliage and pot materials
    const { pot, soil: dirt } = potGeo(rt, h, lv);
    mesh(pot, lv ? potMat : W.pot, x, y, z); if (lv) mesh(dirt, soil, x, y, z, { cast: false });
    const p = plantGeo(kind, r, lv, s);
    mesh(lv ? p.leaves : twoSided(p.leaves), lv ? leafMat : kit.mat('foliage'), x, y + h - 0.045, z, { ry: r() * 6.28 });
    if (p.stems) mesh(p.stems, M.darkWood, x, y + h - 0.045, z);
  }
  // ---- shoes at the 현관 ([x, z, ry] per shoe, colours); the last pair, on the hall floor, are 실내화 ----
  function shoes(list, cols) {
    const at = (i) => ({ x: list[i][0], y: (i >= 4 ? FL : F3), z: list[i][1], ry: list[i][2] });
    if (!lv) {
      const sh = kit.instanced(new THREE.BoxGeometry(0.1, 0.07, 0.26), P('#ffffff', 0.7), list.map((_, i) => ({ ...at(i), y: at(i).y + 0.035 })), { cast: false });
      list.forEach((_, i) => sh.setColorAt(i, new THREE.Color(cols[i]))); g.add(sh); return;
    }
    const four = [0, 1, 2, 3].map(at), up = kit.instanced(shoeGeo(), P('#ffffff', 0.7), four, { cast: false }), sole = kit.instanced(soleGeo(0.022, 0.28), P('#ffffff', 0.7), four, { cast: false });
    four.forEach((_, i) => { up.setColorAt(i, new THREE.Color(cols[i])); sole.setColorAt(i, new THREE.Color(i < 2 ? '#f4f2ec' : '#1c1c1d')); });
    g.add(up, sole);
    for (const i of [4, 5]) { const a = at(i); mesh(slipperGeo(lv), P('#7c8a86', 0.95), a.x, a.y, a.z, { ry: a.ry }); } // grey-green felt, as the cushion
  }
  // ---- 식기건조대 on the counter left of the hob: tray, wire posts and rails, plates on edge, bowls, cups, 수저통 ----
  function dishRack(x, z) {
    const plates = (n) => { for (let i = 0; i < n; i++) cyl(0.1, 0.1, 0.01, W.plate, x - 0.13 + i * 0.05, CT + 0.11, z, { rz: Math.PI / 2 }, 20); };
    if (!lv) { box(0.42, 0.015, 0.3, M.metal, x, CT, z); plates(4); cyl(0.035, 0.03, 0.09, P('#4f7fa8', 0.3), x + 0.15, CT + 0.015, z - 0.08, {}, 16); return; }
    box(0.44, 0.02, 0.31, W.cabinet, x, CT, z);
    const wire = [];
    for (const [dx, dz] of [[-0.21, -0.145], [0.21, -0.145], [-0.21, 0.145], [0.21, 0.145]]) wire.push(new THREE.CylinderGeometry(0.004, 0.004, 0.17, 4).translate(x + dx, CT + 0.105, z + dz));
    for (const dz of [-0.145, 0.145]) wire.push(new THREE.CylinderGeometry(0.004, 0.004, 0.42, 4).rotateZ(Math.PI / 2).translate(x, CT + 0.19, z + dz));
    for (let i = 0; i < 7; i++) for (const dz of [-0.09, 0.09]) wire.push(new THREE.CylinderGeometry(0.003, 0.003, 0.09, 4).translate(x - 0.17 + i * 0.035, CT + 0.065, z + dz));
    mesh(join(wire), M.metal);
    plates(4);
    const bowl = new THREE.LatheGeometry([[0, 0], [0.04, 0], [0.042, 0.008], [0.07, 0.05], [0.066, 0.052], [0.036, 0.012], [0, 0.012]].map(([a, b]) => new THREE.Vector2(a, b)), 16);
    mesh(bowl.rotateX(Math.PI).translate(0, 0.052, 0), W.plate, x + 0.1, CT + 0.025, z - 0.07, { rz: 0.45 }); // upside down, leaning on the rail
    cyl(0.035, 0.03, 0.09, P('#4f7fa8', 0.3), x + 0.13, CT + 0.02, z + 0.05, { rx: Math.PI });
    cyl(0.03, 0.028, 0.11, M.metal, x + 0.18, CT + 0.02, z + 0.11, {}, 10); // 수저통 with spoons and chopsticks
    for (let i = 0; i < 5; i++) B.rod(g, [x + 0.175 + (i % 3) * 0.008, CT + 0.06, z + 0.105 + (i >> 1) * 0.008], [x + 0.17 + (i - 2) * 0.012, CT + 0.2 + (i % 2) * 0.02, z + 0.1 + (i - 2) * 0.01], 0.0025, M.metal, 4);
  }
  // ---- sofa: soft back cushions (pillows on edge) and, from '낮음' up, the knit throw ----
  function sofa(cushions) { // [x, y, z, ry, rz, mat, w, h, d] as the old rounded boxes (w along x is the thickness)
    for (const [x, y, z, ry, rz, mat, w, h, d] of cushions) {
      if (!lv) { g.add(kit.rbox(w, h, d, 0.06, mat, { x, y, z, ry, rz })); continue; }
      mesh(pillowGeo(h, w, d, lv).translate(0, -w / 2, 0).rotateZ(-Math.PI / 2), mat, x, y + h / 2, z, { ry, rz }); // the full side faces the room (+x)
    }
    if (!lv) { g.add(kit.rbox(0.5, 0.06, 0.4, 0.03, P('#c7b9a4', 1), { x: -3.35, y: FL + 0.54, z: 6.25, ry: 0.2 })); return; }
    const knit = kit.memo('apt:knit', () => new THREE.MeshStandardMaterial({ roughness: 0.98, map: kit.texture(kit.canvas(64, 64, (c) => {
      c.fillStyle = '#b39762'; c.fillRect(0, 0, 64, 64);
      for (let x = 0; x < 64; x += 8) { c.fillStyle = 'rgba(70,50,20,0.22)'; c.fillRect(x, 0, 2, 64); c.fillStyle = 'rgba(255,240,210,0.12)'; c.fillRect(x + 3, 0, 3, 64); }
      for (let y = 0; y < 64; y += 4) { c.fillStyle = 'rgba(60,40,15,0.08)'; c.fillRect(0, y, 64, 1); }
    }), { repeat: [7, 9] }) }));
    const Y = (v) => FL + v, path = [[-3.986, Y(0.6)], [-3.978, Y(0.84)], [-3.94, Y(0.93)], [-3.84, Y(0.947)], [-3.745, Y(0.93)], [-3.705, Y(0.85)], [-3.7, Y(0.62)], [-3.66, Y(0.556)], [-3.48, Y(0.552)], [-3.24, Y(0.553)]];
    mesh(ribbonGeo(path, 0.64, { n: 22, m: 8, amp: 0.013, seed: 3 }), knit, 0, 0, 6.2, { cast: true });
  }
  // ---- bed: puffy duvet hanging over the foot and both sides, soft pillows ----
  function bed() {
    if (!lv) {
      g.add(kit.rbox(1.45, 0.1, 1.58, 0.05, W.duvet, { x: -6.25, y: FL + 0.47, z: 6.4, rz: 0.02 }));
      for (const z of [6.02, 6.78]) g.add(kit.rbox(0.38, 0.12, 0.62, 0.05, M.white, { x: -7.25, y: FL + 0.5, z, ry: z > 6.4 ? 0.08 : -0.05 }));
      return FL + 0.55;
    }
    mesh(drapeGeo({ w: 1.47, d: 1.54, top: 0.515, hang: [0, 0.19, 0.18, 0.18], puff: 0.045, amp: 0.011, seed: 9, nx: lv > 1 ? 22 : 12, nz: lv > 1 ? 20 : 10 }), W.duvet, -6.25, FL, 6.4);
    for (const z of [6.02, 6.78]) mesh(pillowGeo(0.38, 0.13, 0.62, lv), M.white, -7.25, FL + 0.5, z, { ry: z > 6.4 ? 0.08 : -0.05 });
    return FL + 0.58; // the folded runner at the foot sits on the duvet
  }

  // ---- the rest: clutter that only exists from '낮음' up ----
  function furnish() {
    if (!lv) return;
    // TV stand top (x -0.12..0.3, z 5.05..6.95, FL + 0.4): 공유기 with antennas and LEDs, a photo frame, a reed diffuser, the TV cable
    const ST = FL + 0.4;
    box(0.12, 0.03, 0.19, M.white, 0.07, ST, 5.22);
    for (const [dz, t] of [[-0.07, 0.12], [0, -0.06], [0.07, 0.2]]) box(0.012, 0.15, 0.008, M.white, 0.12, ST + 0.03, 5.22 + dz, { rx: t * 0.6, rz: -0.1 });
    for (const dz of [-0.04, -0.01, 0.02]) box(0.003, 0.004, 0.01, glow('green'), 0.0085, ST + 0.012, 5.22 + dz, { cast: false });
    box(0.015, 0.19, 0.15, M.darkWood, 0.17, ST, 5.48, { rz: -0.12 });
    const paper = kit.memo('apt:paper302', () => new THREE.MeshStandardMaterial({ roughness: 0.8, map: B.tex('paper302', 256, 512, (c) => { drawCalendar(c, 256, 370); c.translate(0, 384); drawPhoto(c, 96, 128); }) }));
    const sheet = (w, h, u1, v0, v1) => { const q = new THREE.PlaneGeometry(w, h), uv = q.attributes.uv; for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * u1, v0 + uv.getY(i) * (v1 - v0)); return q; };
    const photo = new THREE.Mesh(sheet(0.12, 0.16, 0.375, 0, 0.25), paper); // atlas: calendar on top, the photo below
    photo.position.set(0.17 - 0.0085, ST + 0.095, 5.48); photo.rotation.set(0, -Math.PI / 2, 0); photo.rotateX(-0.12); g.add(photo);
    cyl(0.028, 0.034, 0.09, P('#e8e2d6', 0.3), 0.03, ST, 6.53, {}, 14);
    for (let i = 0; i < 5; i++) { const a = i * 1.26; B.rod(g, [0.03, ST + 0.06, 6.53], [0.03 + Math.cos(a) * 0.06, ST + 0.3 + (i % 2) * 0.03, 6.53 + Math.sin(a) * 0.06], 0.0022, M.darkWood, 4); }
    B.rod(g, [0.24, FL + 0.81, 6.3], [0.24, ST + 0.01, 6.34], 0.004, M.black, 5);
    // kitchen: 고무장갑 over the sink's front edge, a 행주 on a door handle, the wall calendar beside the fridge
    const pink = P('#df6f86', 0.35);
    hangZ([[0.47, CT + 0.008], [0.6, CT + 0.012], [0.655, CT - 0.008], [0.662, CT - 0.12]], 0.1, -5.32, pink, { n: 10, m: 3, amp: 0.006, seed: 4 });
    const fingers = [];
    for (let i = 0; i < 4; i++) fingers.push(new THREE.CapsuleGeometry(0.009, 0.045, 2, 6).translate(-0.036 + i * 0.024, -0.03 - (i === 0 || i === 3 ? 0 : 0.01), 0));
    mesh(join(fingers), pink, -5.32, CT - 0.12, 0.663, { cast: false });
    hangZ([[0.632, FL + 0.752], [0.648, FL + 0.763], [0.657, FL + 0.74], [0.66, FL + 0.48]], 0.2, -4.75, P('#f2efe6', 0.98), { n: 10, m: 4, amp: 0.008, seed: 6, cast: true });
    const cal = new THREE.Mesh(sheet(0.36, 0.52, 1, 1 - 370 / 512, 1), paper);
    cal.position.set(-2.503, FL + 1.55, 1.15); cal.rotation.set(0, -Math.PI / 2, 0.012); g.add(cal);
    box(0.012, 0.012, 0.012, M.metal, -2.506, FL + 1.81, 1.15);
    // 현관: a long 구두주걱 on its hook beside the 신발장, an air freshener on top
    box(0.012, 0.012, 0.03, M.metal, -0.24, F3 + 0.96, 1.285); box(0.045, 0.43, 0.006, M.darkWood, -0.24, F3 + 0.53, 1.294, { rz: 0.03 });
    cyl(0.03, 0.03, 0.08, P('#e8e2d6', 0.3), 0.2, F3 + 1.05, 1.05, {}, 12); cyl(0.012, 0.016, 0.03, M.white, 0.2, F3 + 1.13, 1.05, {}, 8);
    // bedroom nightstand (top FL + 0.48): phone face down by the lamp, a paperback
    box(0.075, 0.009, 0.155, M.black, -7.46, FL + 0.48, 7.36, { ry: 0.3 });
    box(0.13, 0.025, 0.19, M.darkWood, -7.22, FL + 0.48, 7.63, { ry: -0.2 });
    // 베란다: standing 빨래건조대 with towels and socks (x -4.8..-3.4 against the outer glass), bottles on the washer, a heaped basket
    const RY = BF + 0.95, z0 = 8.9, rods = [];
    for (let k = 0; k < 6; k++) rods.push(new THREE.CylinderGeometry(0.006, 0.006, 1.4, 5).rotateZ(Math.PI / 2).translate(-4.1, RY, z0 + k * 0.07));
    for (const x of [-4.8, -3.4]) {
      rods.push(new THREE.CylinderGeometry(0.008, 0.008, 0.36, 5).rotateX(Math.PI / 2).translate(x, RY, z0 + 0.175));
      for (const [za, zb] of [[8.86, 9.3], [9.3, 8.86]]) { const d = new THREE.Vector3(0, RY - BF, zb - za), L = d.length(); rods.push(new THREE.CylinderGeometry(0.009, 0.009, L, 5).translate(0, L / 2, 0).applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.normalize())).translate(x, BF, za)); }
    }
    mesh(join(rods), M.alu);
    const towel = (x, zr, w, h, c, seed) => hangZ([[zr - 0.014, RY - h], [zr - 0.013, RY - 0.03], [zr, RY + 0.009], [zr + 0.013, RY - 0.03], [zr + 0.015, RY - h * 0.92]], w, x, P(c, 0.98), { n: 12, m: 5, amp: 0.012, seed, cast: true });
    towel(-4.45, z0 + 0.07, 0.42, 0.4, '#e8e4dc', 2); towel(-3.95, z0 + 0.21, 0.36, 0.34, '#5d7fa3', 3); towel(-3.62, z0 + 0.35, 0.3, 0.28, '#f2efe6', 5);
    for (const [x, c] of [[-4.72, '#3f4a56'], [-4.64, '#3f4a56'], [-3.48, '#8a3a3a']]) towel(x, z0 + 0.28, 0.07, 0.17, c, 7);
    mesh(ribbonGeo([[-3.43, RY - 0.36], [-3.415, RY - 0.03], [-3.4, RY + 0.01], [-3.385, RY - 0.03], [-3.37, RY - 0.4]], 0.3, { n: 12, m: 5, amp: 0.012, seed: 8 }), P('#8a3a3a', 0.98), 0, 0, z0 + 0.17); // a towel over the end bar
    const rack = B.solid(-4.86, -3.34, 8.82, 9.34, BF, BF + 1.0); rack.noVault = rack.noClimb = true;
    box(0.12, 0.2, 0.17, P('#4f7fa8', 0.3), -7.4, BF + 0.86, 9.17, { ry: 0.2 }); box(0.04, 0.03, 0.04, M.white, -7.42, BF + 1.06, 9.13);
    const bottle = new THREE.LatheGeometry([[0, 0], [0.045, 0], [0.05, 0.03], [0.05, 0.17], [0.03, 0.21], [0.018, 0.23], [0.018, 0.25], [0, 0.25]].map(([a, b]) => new THREE.Vector2(a, b)), 14);
    mesh(bottle, P('#e8e2d6', 0.3), -7.17, BF + 0.86, 9.2);
    for (const [dx, dz, c, ry] of [[-0.04, 0.02, '#e8e4dc', 0.3], [0.05, -0.03, '#5d7fa3', -0.5], [0, 0.05, '#3f4a56', 1.1]])
      mesh(pillowGeo(0.24, 0.07, 0.2, 1), P(c, 0.98), -6.55 + dx, BF + 0.3 + (c === '#3f4a56' ? 0.04 : 0), 8.35 + dz, { ry, rz: (r() - 0.5) * 0.3 });
  }
  return { lv, plant, shoes, dishRack, sofa, bed, furnish };
}

function drawPhoto(c, w, h) { // a family snapshot at the beach: sky, sea, sand, three figures
  c.fillStyle = '#f4f1ea'; c.fillRect(0, 0, w, h);
  const g = c.createLinearGradient(0, 8, 0, 70); g.addColorStop(0, '#9cc3dc'); g.addColorStop(1, '#d9e8ef'); c.fillStyle = g; c.fillRect(8, 8, w - 16, 62);
  c.fillStyle = '#4f86a8'; c.fillRect(8, 62, w - 16, 18); c.fillStyle = '#d9c7a0'; c.fillRect(8, 80, w - 16, h - 88);
  for (const [x, hh, col] of [[30, 40, '#2d3b55'], [48, 46, '#8a3a3a'], [64, 30, '#e0b23a']]) { c.fillStyle = col; c.fillRect(x - 6, 104 - hh, 12, hh - 8); c.fillStyle = '#e2b99a'; c.beginPath(); c.arc(x, 104 - hh - 2, 5, 0, 7); c.fill(); }
}
function drawCalendar(c, w, h) { // 행정복지센터 freebie: a mountain photo, the month grid with red Sundays, one day circled
  c.fillStyle = '#fbfaf6'; c.fillRect(0, 0, w, h);
  const g = c.createLinearGradient(0, 0, 0, 150); g.addColorStop(0, '#b9cfdc'); g.addColorStop(1, '#e6dccb'); c.fillStyle = g; c.fillRect(10, 10, w - 20, 140);
  c.fillStyle = '#6f8471'; c.beginPath(); c.moveTo(10, 130); c.lineTo(80, 60); c.lineTo(130, 105); c.lineTo(180, 70); c.lineTo(w - 10, 120); c.lineTo(w - 10, 150); c.lineTo(10, 150); c.fill();
  c.fillStyle = '#c4161c'; c.font = `800 38px ${FONT}`; c.textAlign = 'left'; c.fillText('11', 14, 192);
  c.fillStyle = '#333'; c.font = `600 13px ${FONT}`; c.fillText('NOVEMBER', 62, 186);
  const days = ['일', '월', '화', '수', '목', '금', '토'], cw = (w - 20) / 7;
  c.font = `700 12px ${FONT}`; c.textAlign = 'center';
  days.forEach((d, i) => { c.fillStyle = i === 0 ? '#c4161c' : i === 6 ? '#1f4e8c' : '#333'; c.fillText(d, 10 + cw * (i + 0.5), 212); });
  c.font = `500 13px ${FONT}`;
  for (let d = 1; d <= 30; d++) { const k = d + 5, col = k % 7, row = (k / 7) | 0; c.fillStyle = col === 0 ? '#c4161c' : col === 6 ? '#1f4e8c' : '#333'; c.fillText(String(d), 10 + cw * (col + 0.5), 234 + row * 22); }
  c.strokeStyle = '#d0302a'; c.lineWidth = 2; c.beginPath(); c.arc(10 + cw * 3.5, 230 + 2 * 22, 10, 0, 7); c.stroke();
  c.fillStyle = '#d0302a'; c.font = `500 10px ${FONT}`; c.fillText('훈련 2시', 10 + cw * 3.5, 252 + 2 * 22);
  c.fillStyle = '#555'; c.font = `600 12px ${FONT}`; c.fillText('연무동 행정복지센터', w / 2, h - 12);
}
