// 연무 생활문화센터 shelter entrances. A: stair kiosk with canopy, stairs going down to a fire door
// (walk stops at the top). B: steep striped car ramp descending under the building (walkable, boom
// barrier). C: gentle ramp along the north wall with handrails both sides, tactile strips and a roll-down
// shutter. East: glass double doors under a canopy. Official shelter signs at each entrance, guide signs
// 10-50 m away on 2 m posts. setGates() switches doors, shutter, boom and the matching solids.
import * as THREE from 'three';
import { PLACES } from '../game/townmap.js';
import { CURB } from './town-plan.js';

const Y = CURB;
export const SHELTER_HOLES = [
  { x0: -87.3, x1: -84.7, z0: 13.6, z1: 16 },     // A stair pit
  { x0: -84.6, x1: -75.4, z0: 46, z1: 54 },       // B ramp
  { x0: -112, x1: -101.8, z0: 13.6, z1: 16 },     // C ramp trench
];
export const FACADE_HOLE = { side: 's', a0: -84.5, a1: -75.5, y1: 2.0 };
export const GUIDE_SPOTS = { guideEast: { x: -66.4, z: 28.9, face: [1, 0] }, guideC: { x: -100.4, z: 12.7, face: [0.4, -1] } };
const B_RAMP = { y0: -0.9, y1: Y, z0: 46, z1: 54 }; // B: y at z0 .. y at z1
const rampYB = (z) => B_RAMP.y0 + (B_RAMP.y1 - B_RAMP.y0) * (z - B_RAMP.z0) / (B_RAMP.z1 - B_RAMP.z0);

// Quad from four corners (counter-clockwise seen from the front); UVs in metres along p0->p1 / p0->p3.
function quad(p0, p1, p2, p3, mat, s = 1) {
  const g = new THREE.BufferGeometry(), v = (a, b) => new THREE.Vector3(b[0] - a[0], b[1] - a[1], b[2] - a[2]);
  const e1 = v(p0, p1), e2 = v(p0, p3), n = new THREE.Vector3().crossVectors(e1, e2).normalize(), L1 = e1.length() / s, L2 = e2.length() / s;
  g.setAttribute('position', new THREE.Float32BufferAttribute([...p0, ...p1, ...p2, ...p3], 3));
  g.setAttribute('normal', new THREE.Float32BufferAttribute([...n.toArray(), ...n.toArray(), ...n.toArray(), ...n.toArray()], 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute([0, 0, L1, 0, L1, L2, 0, L2], 2));
  g.setIndex([0, 1, 2, 0, 2, 3]);
  const m = new THREE.Mesh(g, mat); m.receiveShadow = true; m.castShadow = false;
  return m;
}

export function buildShelterSite(kit) {
  const group = new THREE.Group(); group.name = 'shelterSite';
  const stat = new THREE.Group(); group.add(stat);
  const dyn = new THREE.Group(); group.add(dyn);
  const solids = [], walk = [];
  const solid = (x0, x1, z0, z1, id, y0 = -3, y1 = 3) => { const s = { x0, x1, z0, z1, y0, y1, id }; solids.push(s); return s; };
  const conc = kit.surface('concrete', { color: 0xb3b0a8 }), darkConc = kit.surface('concrete', { color: 0x55585a });
  const steel = kit.mat('steel'), dark = kit.mat('darkMetal'), glass = kit.mat('glass', { opacity: 0.4 }), yellow = kit.mat('paint:#f2c230', { roughness: 0.5 });
  const tex = (key, w, h, draw, o) => kit.memo(`town:sh:${key}`, () => kit.texture(kit.canvas(w, h, draw), o));
  const hazard = kit.memo('town:hazardMat', () => new THREE.MeshStandardMaterial({ roughness: 0.6, map: tex('hazard', 128, 32, (g, w, h) => { g.fillStyle = '#f2c230'; g.fillRect(0, 0, w, h); g.fillStyle = '#1a1a1a'; for (let x = -h; x < w; x += 32) { g.beginPath(); g.moveTo(x, h); g.lineTo(x + 16, h); g.lineTo(x + 16 + h, 0); g.lineTo(x + h, 0); g.fill(); } }) }));
  const tactile = kit.memo('town:shTactile', () => new THREE.MeshStandardMaterial({ roughness: 0.6, polygonOffset: true, polygonOffsetFactor: -1, map: tex('tactile', 64, 64, (g, w) => { g.fillStyle = '#e2a91c'; g.fillRect(0, 0, w, w); g.fillStyle = '#f3c23a'; for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) { g.beginPath(); g.arc((x + 0.5) * 16, (y + 0.5) * 16, 4.5, 0, 7); g.fill(); } }) }));
  const exitMat = kit.memo('town:exitSign', () => {
    const map = tex('exit', 256, 128, (g, w, h) => { g.fillStyle = '#12a150'; g.fillRect(0, 0, w, h); g.fillStyle = '#ffffff'; g.fillRect(w * 0.08, h * 0.15, w * 0.22, h * 0.7); g.fillStyle = '#12a150'; g.fillRect(w * 0.12, h * 0.22, w * 0.14, h * 0.63); g.fillStyle = '#ffffff'; g.beginPath(); g.arc(w * 0.45, h * 0.28, 11, 0, 7); g.fill(); g.lineWidth = 9; g.strokeStyle = '#fff'; g.lineCap = 'round'; g.beginPath(); g.moveTo(w * 0.44, h * 0.4); g.lineTo(w * 0.4, h * 0.62); g.lineTo(w * 0.33, h * 0.8); g.moveTo(w * 0.4, h * 0.62); g.lineTo(w * 0.5, h * 0.82); g.moveTo(w * 0.36, h * 0.48); g.lineTo(w * 0.52, h * 0.5); g.stroke(); g.font = `800 40px sans-serif`; g.fillText('비상구', w * 0.58, h * 0.65); }, { repeat: [1, 1] });
    return kit.lit('emergency', new THREE.MeshStandardMaterial({ map, emissiveMap: map, emissive: 0xffffff, emissiveIntensity: 1.6, roughness: 0.4 }), 1.6);
  });
  const emLamp = kit.memo('town:emLamp', () => kit.lit('emergency', new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xf4f8ff, emissiveIntensity: 4, roughness: 0.3 }), 4));
  const tube = kit.memo('town:tubeLamp', () => kit.lit('indoor', new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xeef6ff, emissiveIntensity: 3, roughness: 0.3 }), 3));
  const rail = (a, b, r = 0.024) => { const d = new THREE.Vector3(b[0] - a[0], b[1] - a[1], b[2] - a[2]), m = new THREE.Mesh(new THREE.CylinderGeometry(r, r, d.length(), 8), kit.mat('metal')); m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.clone().normalize()); m.position.set((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2); m.castShadow = true; stat.add(m); };
  const shelterSign = (x, y, z, ry, w = 0.45) => { const s = kit.shelterSign({ w }); s.position.set(x, y, z); s.rotation.y = ry; stat.add(s); const back = kit.box(w + 0.04, w * 4 / 3 + 0.04, 0.03, dark, { x: x - Math.sin(ry) * 0.02, y: y - w * 2 / 3 - 0.02, z: z - Math.cos(ry) * 0.02 }); back.rotation.y = ry; stat.add(back); };
  const gates = {};

  // ================= A: stair kiosk =================
  {
    const x0 = -87.3, x1 = -84.7, cx = (x0 + x1) / 2, zt = 13.6, n = 8, rise = 0.17, run = 0.28;
    for (let i = 0; i < n; i++) {
      const top = Y - rise * (i + 1), z = zt + run * (i + 0.5);
      stat.add(kit.slab(x1 - x0, top + 1.6, run, conc, { x: cx, y: -1.6, z }));
      stat.add(kit.box(x1 - x0 - 0.2, 0.012, 0.05, yellow, { x: cx, y: top, z: z - run / 2 + 0.03, cast: false }));
    }
    const bottom = Y - rise * n;
    stat.add(kit.slab(x1 - x0, 0.1, 16 - (zt + run * n), conc, { x: cx, y: bottom - 0.1, z: (zt + run * n + 16) / 2 }));
    for (const x of [x0 - 0.15, x1 + 0.15]) {
      stat.add(kit.slab(0.3, Y + 1.6 + 1.0, 16 - 12.8, conc, { x, y: -1.6, z: (12.8 + 16) / 2 }));
      const gp = kit.box(0.02, 1.3, 3.1, glass, { x, y: Y + 1.0, z: (12.9 + 16) / 2, cast: false }); gp.userData.keep = true; stat.add(gp);
      stat.add(kit.box(0.08, 2.5, 0.08, steel, { x, y: Y, z: 12.85 }));
      rail([x + (x < cx ? 0.2 : -0.2), Y + 0.85, zt], [x + (x < cx ? 0.2 : -0.2), bottom + 0.85, zt + run * n]);
    }
    stat.add(kit.box(3.5, 0.12, 3.6, kit.mat('paint:#5a6670', { roughness: 0.4, metalness: 0.6 }), { x: cx, y: Y + 2.5, z: 14.3, rx: 0.04 }));
    const roofGlass = kit.box(3.3, 0.03, 3.4, glass, { x: cx, y: Y + 2.64, z: 14.3, rx: 0.04, cast: false }); roofGlass.userData.keep = true; stat.add(roofGlass);
    const lintel = kit.sign(['민방위 대피시설 · 출입구 A'], { w: 2.9, h: 0.32, bg: '#1f4e8c', single: true, emissive: 0.3 });
    kit.lit('signs', lintel.material, 0.5); lintel.position.set(cx, Y + 2.3, 12.8); lintel.rotation.y = Math.PI; stat.add(lintel);
    shelterSign(x0 - 0.15, Y + 1.85, 12.78, Math.PI);
    // fire door at the bottom with exit sign and a battery emergency light (works in a blackout)
    stat.add(kit.box(2.2, 2.15, 0.06, kit.mat('paint:#6f7a72', { roughness: 0.5, metalness: 0.4 }), { x: cx, y: bottom, z: 15.95 }));
    stat.add(kit.box(0.02, 2.0, 0.08, dark, { x: cx, y: bottom + 0.05, z: 15.9 }), kit.box(0.25, 0.4, 0.02, glass, { x: cx - 0.5, y: bottom + 1.3, z: 15.91 }));
    const ex = new THREE.Mesh(new THREE.PlaneGeometry(0.6, 0.3), exitMat); ex.position.set(cx, bottom + 2.4, 15.9); ex.rotation.y = Math.PI; stat.add(ex);
    stat.add(kit.box(0.4, 0.1, 0.12, emLamp, { x: cx, y: Y + 2.35, z: 15.4, cast: false }));
    const L = new THREE.PointLight(0xeef4ff, 22, 9, 2); L.position.set(cx, Y + 2.1, 14.6); kit.lit('emergency', L, 22); group.add(L);
    solid(x0 - 0.35, x0, 12.75, 16, 'kioskA'); solid(x1, x1 + 0.35, 12.75, 16, 'kioskA');
    solid(x0, x1, zt + 0.1, 16, 'stairsTop');
    // closed: folding steel gate across the top
    const lattice = kit.memo('town:lattice', () => new THREE.MeshStandardMaterial({ map: tex('lattice', 128, 128, (g, w) => { g.strokeStyle = '#d0d3d6'; g.lineWidth = 5; for (let k = -w; k < w * 2; k += 22) { g.beginPath(); g.moveTo(k, 0); g.lineTo(k + w, w); g.stroke(); g.beginPath(); g.moveTo(k + w, 0); g.lineTo(k, w); g.stroke(); } }), alphaTest: 0.5, side: THREE.DoubleSide, metalness: 0.7, roughness: 0.4 }));
    const fold = new THREE.Group();
    const lat = kit.box(x1 - x0, 2.1, 0.04, lattice, { x: cx, y: Y, z: zt + 0.02, cast: false }); lat.material.map.repeat.set(2.5, 2);
    const no = kit.sign(['출입 통제', '다른 출입구를 이용하세요'], { w: 1.2, h: 0.45, bg: '#c42b25', single: true }); no.position.set(cx, Y + 1.3, zt - 0.03); no.rotation.y = Math.PI;
    fold.add(lat, no); dyn.add(fold);
    gates.stairs = (open) => { fold.visible = !open; };
  }

  // ================= B: car ramp under the building =================
  {
    const x0 = -84.3, x1 = -75.7, cx = (x0 + x1) / 2;
    const rings = kit.memo('town:rampRings', () => {
      const h = kit.canvas(128, 128, (g, w) => { g.fillStyle = '#c0c0c0'; g.fillRect(0, 0, w, w); g.strokeStyle = '#303030'; g.lineWidth = 6; for (const [x, y] of [[32, 32], [96, 32], [32, 96], [96, 96]]) { g.beginPath(); g.arc(x, y, 22, 0, 7); g.stroke(); } });
      return new THREE.MeshStandardMaterial({ map: kit.texture(kit.canvas(128, 128, (g, w) => { g.fillStyle = '#9d9b96'; g.fillRect(0, 0, w, w); g.strokeStyle = '#7a7873'; g.lineWidth = 6; for (const [x, y] of [[32, 32], [96, 32], [32, 96], [96, 96]]) { g.beginPath(); g.arc(x, y, 22, 0, 7); g.stroke(); } })), normalMap: kit.texture(kit.normalFrom(h, 3), { color: false }), roughness: 0.85 });
    });
    walk.push({ x0, x1, z0: B_RAMP.z0, z1: B_RAMP.z1, ramp: { axis: 'z', y0: B_RAMP.y0, y1: B_RAMP.y1 } });
    stat.add(quad([x0, B_RAMP.y1, 54], [x1, B_RAMP.y1, 54], [x1, B_RAMP.y0, 46], [x0, B_RAMP.y0, 46], rings, 1));
    stat.add(quad([x0, B_RAMP.y0, 46], [x1, B_RAMP.y0, 46], [x1, -2.9, 37], [x0, -2.9, 37], rings, 1));
    for (let k = 0; k < 3; k++) { const z = 53.2 - k * 2.6, y = rampYB(z) + 0.012; stat.add(quad([x0 + 0.3, y, z + 0.25], [x1 - 0.3, y, z + 0.25], [x1 - 0.3, y - 0.064, z - 0.25], [x0 + 0.3, y - 0.064, z - 0.25], hazard, 0.5)); }
    for (const [x, s] of [[x0 - 0.15, -1], [x1 + 0.15, 1]]) {
      stat.add(kit.slab(0.3, Y + 0.9 + 3, 8, conc, { x, y: -3, z: 50 }));
      stat.add(kit.box(0.32, 0.12, 8, hazard, { x, y: Y + 0.9, z: 50 }));
      for (let z = 46.6; z < 54; z += 0.9) stat.add(kit.box(0.02, 0.5, 0.45, hazard, { x: x - s * 0.16, y: rampYB(z) + 0.05, z, cast: false }));
      stat.add(kit.slab(0.3, 5.5, 9, darkConc, { x: x - s * 0.15, y: -3, z: 41.5 }));
    }
    stat.add(kit.slab(x1 - x0 + 0.6, 0.3, 9, darkConc, { x: cx, y: 2.0, z: 41.5 }), kit.slab(x1 - x0, 5, 0.3, darkConc, { x: cx, y: -3, z: 37.2 }));
    for (const z of [44.5, 41]) stat.add(kit.box(1.4, 0.06, 0.14, tube, { x: cx, y: 1.93, z, cast: false }));
    const ex = new THREE.Mesh(new THREE.PlaneGeometry(0.7, 0.35), exitMat); ex.position.set(cx, 0.3, 37.4); stat.add(ex);
    const pk = kit.sign(['P  지하주차장 입구'], { w: 3.2, h: 0.5, bg: '#1f5fa8', single: true, emissive: 0.3 }); kit.lit('signs', pk.material, 0.6); pk.position.set(cx, 2.6, 46.06); stat.add(pk);
    shelterSign(x0 - 1.2, Y + 1.9, 46.04, 0);
    const plaque = kit.sign(['출입구 B · 지하주차장 진출입로', '경사 급함 · 보행 주의'], { w: 1.6, h: 0.5, bg: '#ffffff', fg: '#1f4e8c', single: true }); plaque.position.set(x1 + 1.3, Y + 1.5, 46.04); stat.add(plaque);
    for (const x of [x0 - 0.5, x1 + 0.5]) stat.add(kit.box(0.12, 2.6, 0.12, steel, { x, y: Y, z: 54.6 }));
    stat.add(kit.box(x1 - x0 + 1.2, 0.18, 0.12, hazard, { x: cx, y: Y + 2.35, z: 54.6 }));
    const hl = kit.sign(['높이제한 2.3m'], { w: 1.4, h: 0.3, bg: '#ffffff', fg: '#c42b25', single: false }); hl.position.set(cx, Y + 2.15, 54.6); stat.add(hl);
    solid(x0 - 0.65, x0, 46, 54.2, 'rampWallB'); solid(x1, x1 + 0.65, 46, 54.2, 'rampWallB');
    solid(x0 - 0.62, x0 - 0.38, 54.48, 54.72, 'heightBar'); solid(x1 + 0.38, x1 + 0.62, 54.48, 54.72, 'heightBar');
    // boom barrier (차단기) on the ramp, pivot at the cabinet by the west wall
    const zb = 48.5, yb = rampYB(zb);
    stat.add(kit.box(0.4, 1.0, 0.35, yellow, { x: x0 + 0.3, y: yb, z: zb }));
    const pivot = new THREE.Group(); pivot.position.set(x0 + 0.3, yb + 0.92, zb); dyn.add(pivot);
    const armMat = kit.memo('town:boomArm', () => new THREE.MeshStandardMaterial({ roughness: 0.5, map: tex('boom', 256, 16, (g, w, h) => { for (let k = 0; k < 16; k++) { g.fillStyle = k % 2 ? '#ffffff' : '#d42a24'; g.fillRect(k * w / 16, 0, w / 16, h); } }) }));
    const arm = kit.box(8.1, 0.09, 0.07, armMat, { x: 4.05, y: -0.045, z: 0 }); pivot.add(arm);
    const boomSolid = solid(x0, x1, zb - 0.22, zb + 0.22, 'boomB');
    gates.ramp = (open) => { pivot.rotation.z = open ? Math.PI / 2 - 0.05 : 0; boomSolid.off = open; };
  }

  // ================= C: gentle ramp with handrails, tactile strips, roll-down shutter =================
  {
    const xl = -112, xm = -109.6, xt = -101.8, z0 = 13.75, z1 = 16, yb = -0.5;
    walk.push({ x0: xm, x1: xt, z0, z1, ramp: { axis: 'x', y0: yb, y1: Y } }, { x0: xl, x1: xm, z0, z1, y: yb });
    const floor = kit.surface('concrete', { color: 0xc9c5bc });
    stat.add(quad([xm, yb, z1], [xt, Y, z1], [xt, Y, z0], [xm, yb, z0], floor, 1));
    stat.add(quad([xl, yb, z1], [xm, yb, z1], [xm, yb, z0], [xl, yb, z0], floor, 1));
    stat.add(kit.slab(xt - xl, Y + 1.2, 0.15, conc, { x: (xl + xt) / 2, y: -1.1, z: 13.6 }), kit.slab(0.3, Y + 1.2, 2.4, conc, { x: xl - 0.15, y: -1.1, z: 14.8 }));
    stat.add(quad([xt, Y + 0.004, z1], [xt + 0.6, Y + 0.004, z1], [xt + 0.6, Y + 0.004, z0], [xt, Y + 0.004, z0], tactile, 0.3));
    stat.add(quad([-111.6, yb + 0.004, 15.92], [-109.8, yb + 0.004, 15.92], [-109.8, yb + 0.004, 15.3], [-111.6, yb + 0.004, 15.3], tactile, 0.3));
    for (const h of [0.65, 0.85]) for (const z of [13.85, 15.9]) {
      rail([xt + 0.3, Y + h, z], [xm, yb + h, z]); rail([xm, yb + h, z], [xl + 0.15, yb + h, z]);
    }
    for (let x = xl + 0.3; x <= xt + 0.3; x += 1.5) { const y = x > xm ? yb + (Y - yb) * (x - xm) / (xt - xm) : yb; stat.add(kit.cyl(0.025, 0.025, 0.85, kit.mat('metal'), { x, y, z: 13.85 }, 6)); }
    shelterSign(-108.4, yb + 1.95, 15.94, Math.PI);
    stat.add(kit.cyl(0.04, 0.04, 2.6, steel, { x: xt + 0.7, y: Y, z: 13.2 }, 8)); shelterSign(xt + 0.7, Y + 2.55, 13.15, Math.PI, 0.4);
    const cs = kit.sign(['출입구 C · 경사로', '휠체어 · 유모차 · 보행 보조기'], { w: 1.5, h: 0.42, bg: '#ffffff', fg: '#1f4e8c', single: true }); cs.position.set(-106, Y + 1.45, 15.94); cs.rotation.y = Math.PI; stat.add(cs);
    // shutter box, slats, opening behind
    stat.add(kit.box(2.0, 0.35, 0.3, kit.mat('paint:#9aa1a6', { metalness: 0.6, roughness: 0.4 }), { x: -110.7, y: yb + 2.4, z: 15.82 }));
    const slats = kit.memo('town:shutter', () => {
      const h = kit.canvas(64, 128, (g, w, hh) => { for (let y = 0; y < hh; y++) { const v = 128 + 90 * Math.sin(y / 8 * Math.PI); g.fillStyle = `rgb(${v},${v},${v})`; g.fillRect(0, y, w, 1); } });
      return new THREE.MeshStandardMaterial({ color: 0xb9bec2, metalness: 0.7, roughness: 0.35, normalMap: kit.texture(kit.normalFrom(h, 3), { repeat: [1, 6], color: false }) });
    });
    const shutter = new THREE.Group();
    const sl = new THREE.Mesh(new THREE.PlaneGeometry(1.8, 2.4), slats); sl.position.set(-110.7, yb + 1.2, 15.93); sl.rotation.y = Math.PI; sl.castShadow = true;
    const paper = kit.sign(['공사 중', '10월 말 개방 예정', '비상시 현장 안내에 따라 개방'], { w: 0.6, h: 0.5, bg: '#fdfbf2', fg: '#222', single: true }); paper.position.set(-110.7, yb + 1.45, 15.915); paper.rotation.y = Math.PI;
    shutter.add(sl, paper); dyn.add(shutter);
    const opening = new THREE.Group();
    const hole = new THREE.Mesh(new THREE.PlaneGeometry(1.8, 2.4), kit.mat('paint:#141618', { roughness: 1 })); hole.position.set(-110.7, yb + 1.2, 15.94); hole.rotation.y = Math.PI;
    const ex = new THREE.Mesh(new THREE.PlaneGeometry(0.5, 0.25), exitMat); ex.position.set(-110.7, yb + 2.0, 15.92); ex.rotation.y = Math.PI;
    const inner = kit.box(1.0, 0.06, 0.1, emLamp, { x: -110.7, y: yb + 2.15, z: 15.9, cast: false });
    const cl = new THREE.PointLight(0xf2f6ff, 14, 7, 2); cl.position.set(-110.7, yb + 1.9, 15.1); kit.lit('emergency', cl, 14);
    opening.add(hole, ex, inner, cl); dyn.add(opening);
    solid(xl, xt, 13.45, z0, 'railC', -3, 2); solid(xl - 0.3, xl, 13.45, z1, 'railC', -3, 2);
    gates.access = (open) => { shutter.visible = !open; opening.visible = open; };
  }

  // ================= East: glass double doors, canopy, closure barrier and notice =================
  {
    const x = -69.95, zc = 31;
    stat.add(kit.slab(2.6, 0.28, 5.4, kit.surface('concrete', { color: 0xc7c3ba }), { x: -68.7, y: Y + 2.95, z: zc }));
    for (const z of [28.6, 33.4]) { stat.add(kit.box(0.24, 2.95, 0.24, kit.mat('whiteWall'), { x: -67.55, y: Y, z })); solid(-67.7, -67.4, z - 0.15, z + 0.15, 'canopyEast'); }
    const glow = kit.memo('town:lobbyGlow', () => kit.lit('indoor', new THREE.MeshStandardMaterial({ color: 0x2a2a28, emissive: 0xffe6c4, emissiveIntensity: 0.6, roughness: 0.8 }), 0.6));
    const back = new THREE.Mesh(new THREE.PlaneGeometry(3.2, 2.5), glow); back.position.set(x - 0.01, Y + 1.25, zc); back.rotation.y = Math.PI / 2; stat.add(back);
    for (const dz of [-1.65, 1.65]) stat.add(kit.box(0.1, 2.6, 0.1, dark, { x: x + 0.03, y: Y, z: zc + dz }));
    stat.add(kit.box(0.12, 0.14, 3.4, dark, { x: x + 0.03, y: Y + 2.5, z: zc }));
    const leaves = [-1, 1].map(s => { const g = new THREE.Group(); const p = kit.box(0.03, 2.45, 1.55, glass, { x: 0, y: 0, z: 0, cast: false }); p.userData.keep = true; g.add(p, kit.box(0.05, 2.45, 0.05, dark, { x: 0, y: 0, z: s * 0.75 }), kit.box(0.06, 0.4, 0.03, kit.mat('metal'), { x: 0.04, y: 0.95, z: -s * 0.65 })); g.position.set(x + 0.06, Y, zc + s * 0.78); dyn.add(g); return { g, s }; });
    shelterSign(x + 0.03, Y + 1.95, 33.9, Math.PI / 2);
    const plq = kit.sign(['민방위 대피시설', '연무 생활문화센터 지하주차장', '수용 인원 1,200명'], { w: 0.9, h: 0.6, bg: '#1f4e8c', single: true }); plq.position.set(x + 0.03, Y + 1.5, 28.0); plq.rotation.y = Math.PI / 2; stat.add(plq);
    // closure: stanchions with belts, standing notice
    const closed = new THREE.Group();
    const posts = [28.7, 30.1, 31.9, 33.3];
    for (const z of posts) closed.add(kit.cyl(0.03, 0.03, 0.95, kit.mat('metal'), { x: -67.8, y: Y, z }, 8), kit.cyl(0.15, 0.15, 0.04, dark, { x: -67.8, y: Y, z }, 14));
    const belt = kit.memo('town:belt', () => new THREE.MeshStandardMaterial({ roughness: 0.6, side: THREE.DoubleSide, map: tex('belt', 128, 16, (g, w, h) => { for (let k = 0; k < 8; k++) { g.fillStyle = k % 2 ? '#ffffff' : '#c42b25'; g.fillRect(k * w / 8, 0, w / 8, h); } }) }));
    for (let i = 1; i < posts.length; i++) { const m = new THREE.Mesh(new THREE.PlaneGeometry(posts[i] - posts[i - 1], 0.06), belt); m.position.set(-67.8, Y + 0.88, (posts[i] + posts[i - 1]) / 2); m.rotation.y = Math.PI / 2; closed.add(m); }
    const note = kit.sign(['동쪽 출입구 폐쇄', 'A 계단 · B 진출입로 이용'], { w: 0.9, h: 0.6, bg: '#ffffff', fg: '#c42b25', border: '#c42b25', single: true });
    note.position.set(-66.9, Y + 1.25, 34.5); note.rotation.y = Math.PI / 2; closed.add(note, kit.box(0.05, 0.95, 0.05, dark, { x: -66.92, y: Y, z: 34.5 }), kit.box(0.4, 0.04, 0.4, dark, { x: -66.92, y: Y, z: 34.5 }));
    dyn.add(closed);
    const barrier = solid(-68.0, -67.55, 28.4, 33.6, 'gateEast');
    gates.east = (open) => { closed.visible = !open; barrier.off = open; for (const { g, s } of leaves) g.position.z = zc + s * (open ? 2.25 : 0.78); };
  }

  // ================= guide signs (유도표지판) on 2 m posts =================
  const ENT = { A: PLACES.shelterA, B: PLACES.shelterB, C: PLACES.shelterC, east: PLACES.shelterEast };
  for (const [x, z, nx, nz, to] of [[-58.6, 9.4, 1, 0, 'A'], [-75, 9.4, 1, 0, 'A'], [-118, 9.4, -1, 0, 'C'], [-57, 26, 0, -1, 'east'], [-57, 58, 0, 1, 'B'], [-61.5, -8.8, 0, -1, 'A'], [-66, -8.8, 1, 0, 'A'], [-44.6, 26.6, 1, 0, 'east']]) {
    const t = ENT[to], dx = t.x - x, dz = t.z - z, d = Math.hypot(dx, dz), fx = -nx, fz = -nz;
    const ahead = (dx * fx + dz * fz) / d, side = (dx * -fz + dz * fx) / d;
    const arrow = ahead > 0.7 ? 'up' : side > 0 ? 'right' : 'left', meters = Math.max(10, Math.round(d / 5) * 5);
    const sgn = kit.guideSign({ arrow, meters, w: 0.5 }); sgn.material.side = THREE.FrontSide;
    const ry = Math.atan2(nx, nz);
    sgn.position.set(x + nx * 0.05, Y + 2.125, z + nz * 0.05); sgn.rotation.y = ry; stat.add(sgn);
    const bp = new THREE.Mesh(new THREE.PlaneGeometry(0.52, 0.27), kit.mat('paint:#a8adb2', { metalness: 0.6 })); bp.position.set(x + nx * 0.03, Y + 2.125, z + nz * 0.03); bp.rotation.y = ry + Math.PI; stat.add(bp);
    stat.add(kit.cyl(0.035, 0.035, 2.3, steel, { x, y: Y, z }, 8));
    solid(x - 0.12, x + 0.12, z - 0.12, z + 0.12, 'guidePost', -0.5, 2.5);
  }

  kit.merge(stat);
  function setGates(g = {}) {
    for (const k of ['east', 'stairs', 'ramp', 'access']) if (g[k]) gates[k](g[k] === 'open');
  }
  return { group, solids, walk, setGates };
}
