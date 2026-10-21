// Story set dressing that switches on and off: road closures (water-filled barriers, cones, tape,
// a portable light tower, a flashing lamp, a guide with a light baton and solids that truly block),
// the emergency water point (tank trailer, taps, table, tent, queue lane) and the evacuation buses
// with the assembly-point reception.
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { CLOSURES, PLACES } from '../game/townmap.js';
import { CURB, BARRICADES, CLOSURE_GUIDES } from './town-plan.js';
import { heightAt, yawTo } from './town-util.js';

const Y = CURB;
const NG = (g) => (g.index ? g.toNonIndexed() : g);
export const WATER = { tapZ: -19.6, queue: { x: -78.6, z: -18.4 } };
export const BUS = [{ x: -69.5, z: -40.5 }, { x: -65, z: -40.5 }];
export const ASSEMBLY = { queue: { x: -62.6, z: -44.6 }, area: { x0: -61.8, x1: -58.6, z0: -49, z1: -40 } };

export function buildEvents(kit, people, walk) {
  const group = new THREE.Group(); group.name = 'events';
  const solids = [], hy = (x, z) => heightAt(walk, x, z);
  const steel = kit.mat('steel'), dark = kit.mat('darkMetal'), white = kit.mat('paint:#f4f4f0', { roughness: 0.5 });
  const person = (preset, seed) => { try { const p = people.createPerson({ preset, seed }); p.group.visible = false; group.add(p.group); return p; } catch (err) { console.error(`[town] createPerson(${preset}) failed`, err); return null; } };

  // ================= closures =================
  const barrierGeo = NG(new RoundedBoxGeometry(1.45, 0.9, 0.5, 2, 0.08));
  const coneMat = kit.memo('town:cone', () => new THREE.MeshStandardMaterial({ roughness: 0.5, map: kit.texture(kit.canvas(16, 128, (g, w, h) => { g.fillStyle = '#ee5a1c'; g.fillRect(0, 0, w, h); g.fillStyle = '#f4f4f4'; g.fillRect(0, h * 0.18, w, h * 0.14); g.fillRect(0, h * 0.45, w, h * 0.1); }), { repeat: [1, 1] }) }));
  const coneGeo = mergeGeometries([new THREE.CylinderGeometry(0.035, 0.16, 0.7, 14).translate(0, 0.39, 0), new THREE.BoxGeometry(0.38, 0.04, 0.38).translate(0, 0.02, 0)].map(NG));
  const tapeMat = kit.memo('town:tape', () => new THREE.MeshStandardMaterial({ side: THREE.DoubleSide, roughness: 0.6, map: kit.texture(kit.canvas(512, 32, (g, w, h) => { g.fillStyle = '#f2c230'; g.fillRect(0, 0, w, h); g.fillStyle = '#111'; g.font = '800 22px sans-serif'; g.textBaseline = 'middle'; for (let x = 4; x < w; x += 128) g.fillText('통제선 출입금지', x, h / 2); }), { repeat: [1, 1] }) }));
  const towerHead = kit.memo('town:towerHead', () => kit.lit('emergency', new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xf6fbff, emissiveIntensity: 6, roughness: 0.3 }), 6));
  const flashMat = new THREE.MeshStandardMaterial({ color: 0x5a3a00, emissive: 0xffa21a, emissiveIntensity: 0, roughness: 0.4 });
  const closures = {};
  for (const [id, rects] of Object.entries(BARRICADES)) {
    const g = new THREE.Group(); g.name = `closure:${id}`; g.visible = false; group.add(g);
    const units = [], cones = [], col = [], sol = [];
    const gp = CLOSURE_GUIDES[id];
    for (const r of rects) {
      const alongZ = r.z1 - r.z0 > r.x1 - r.x0, L = alongZ ? r.z1 - r.z0 : r.x1 - r.x0, n = Math.floor(L / 1.5);
      const cx = (r.x0 + r.x1) / 2, cz = (r.z0 + r.z1) / 2;
      const W = alongZ ? r.x1 - r.x0 : r.z1 - r.z0, rows = W >= 3 ? [-W / 2 + 0.6, W / 2 - 0.6] : [0];
      for (const o of rows) for (let k = 0; k < n; k++) {
        const a = -L / 2 + 0.75 + k * (L - 1.5) / Math.max(1, n - 1), x = alongZ ? cx + o : cx + a, z = alongZ ? cz + a : cz + o;
        units.push({ x, y: hy(x, z), z, ry: alongZ ? Math.PI / 2 : 0 }); col.push(k % 2 ? '#f2f2ee' : '#e4572a');
      }
      // cones in a taper on the approach side (towards the guide), tape on posts above the barriers
      const side = gp ? Math.sign(alongZ ? gp.x - cx : gp.z - cz) || 1 : 1;
      for (let k = 0; k < 6; k++) { const a = -L / 2 + 1 + k * (L - 2) / 5, off = 1.3 + (k % 2) * 0.4, x = alongZ ? cx + side * off : cx + a, z = alongZ ? cz + a : cz + side * off; cones.push({ x, y: hy(x, z), z, ry: k }); }
      const tape = new THREE.Mesh(new THREE.PlaneGeometry(L, 0.06), tapeMat);
      tape.position.set(cx, Y + 1.15, cz); tape.rotation.y = alongZ ? Math.PI / 2 : 0; g.add(tape);
      for (const s of [-1, 1]) g.add(kit.cyl(0.025, 0.025, 1.2, steel, { x: alongZ ? cx : cx + s * L / 2, y: Y, z: alongZ ? cz + s * L / 2 : cz }, 6));
      const sg = kit.sign(['차량 · 보행 통제', CLOSURES[id].label], { w: 1.4, h: 0.7, bg: '#c42b25', fg: '#ffffff', border: '#ffffff', single: true });
      const edge = (alongZ ? r.x1 - r.x0 : r.z1 - r.z0) / 2 + 0.45, sx = alongZ ? cx + side * edge : cx, sz = alongZ ? cz : cz + side * edge;
      sg.position.set(sx, hy(sx, sz) + 1.0, sz); sg.rotation.y = alongZ ? (side > 0 ? Math.PI / 2 : -Math.PI / 2) : (side > 0 ? 0 : Math.PI); g.add(sg, kit.box(0.06, 0.7, 0.06, dark, { x: sx, y: hy(sx, sz), z: sz }));
      const lamp = kit.box(0.16, 0.12, 0.16, flashMat, { x: alongZ ? cx : cx + L / 2 - 0.75, y: hy(cx, cz) + 0.9, z: alongZ ? cz + L / 2 - 0.75 : cz, cast: false }); g.add(lamp);
      sol.push({ x0: r.x0, x1: r.x1, z0: r.z0, z1: r.z1, y0: -1, y1: 3, id: `closure:${id}`, off: true });
    }
    if (gp) { // portable light tower beside the guide
      const tx = gp.x + gp.face[1] * 1.6, tz = gp.z - gp.face[0] * 1.6;
      g.add(kit.cyl(0.04, 0.05, 3.2, steel, { x: tx, y: hy(tx, tz), z: tz }, 8), kit.box(0.9, 0.5, 0.5, kit.mat('paint:#f2c230'), { x: tx, y: hy(tx, tz), z: tz }));
      const head = kit.box(0.7, 0.35, 0.12, towerHead, { x: tx, y: hy(tx, tz) + 3.1, z: tz, ry: yawTo(-gp.face[0], -gp.face[1]), cast: false }); g.add(head);
      const L = new THREE.SpotLight(0xf4f8ff, 0, 26, 0.7, 0.6, 1.6); L.position.set(tx, hy(tx, tz) + 3.2, tz); L.target.position.set(tx - gp.face[0] * 6, 0, tz - gp.face[1] * 6);
      kit.lit('emergency', L, 120); g.add(L, L.target);
      sol.push({ x0: tx - 0.5, x1: tx + 0.5, z0: tz - 0.3, z1: tz + 0.3, y0: -1, y1: 3, id: `closure:${id}`, off: true });
    }
    const um = kit.instanced(barrierGeo, kit.mat('paint:#ffffff', { roughness: 0.55 }), units); const c = new THREE.Color();
    col.forEach((h, i) => um.setColorAt(i, c.set(h))); um.instanceColor.needsUpdate = true;
    g.add(um, kit.instanced(coneGeo, coneMat, cones));
    solids.push(...sol);
    kit.merge(g);
    closures[id] = { group: g, solids: sol };
  }
  const guides = [person('guide', 71)];
  let active = [];
  function setClosures(ids = []) {
    const list = [].concat(ids || []).filter(id => { if (!closures[id]) { console.error(`[town] unknown closure "${id}"; known: ${Object.keys(CLOSURES).join(', ')}`); return false; } return true; });
    active = list;
    for (const [id, c] of Object.entries(closures)) { const on = list.includes(id); c.group.visible = on; for (const s of c.solids) s.off = !on; }
    list.forEach((id, k) => { guides[k] ||= person('guide', 72 + k); });
    guides.forEach((p, k) => {
      if (!p) return;
      const id = list[k]; p.group.visible = !!id;
      if (!id) return;
      const gp = CLOSURE_GUIDES[id];
      p.group.position.set(gp.x, hy(gp.x, gp.z), gp.z); p.group.rotation.y = yawTo(gp.face[0], gp.face[1]);
      p.setProp?.('baton', true); p.setAnim?.('idle');
    });
    return list;
  }

  // ================= water point =================
  const water = new THREE.Group(); water.name = 'waterPoint'; water.visible = false; group.add(water);
  {
    const px = PLACES.water.x, tz = WATER.tapZ, y = hy(px, tz);
    const tank = new THREE.Mesh(new THREE.CylinderGeometry(0.8, 0.8, 3.4, 24).rotateZ(Math.PI / 2), kit.mat('metal', { roughness: 0.22 })); tank.position.set(px, y + 1.55, tz - 1.4); tank.castShadow = true; water.add(tank);
    for (const s of [-1, 1]) { const cap = new THREE.Mesh(new THREE.CylinderGeometry(0.81, 0.81, 0.06, 24).rotateZ(Math.PI / 2), steel); cap.position.set(px + s * 1.7, y + 1.55, tz - 1.4); water.add(cap); }
    water.add(kit.box(3.8, 0.12, 1.4, dark, { x: px, y: y + 0.62, z: tz - 1.4 }), kit.box(0.12, 0.12, 1.8, dark, { x: px + 2.4, y: y + 0.5, z: tz - 1.4, ry: Math.PI / 2 }));
    for (const s of [-1, 1]) { const w = new THREE.Mesh(new THREE.CylinderGeometry(0.36, 0.36, 0.22, 16).rotateZ(Math.PI / 2), kit.mat('rubber')); w.position.set(px, y + 0.36, tz - 1.4 + s * 0.8); w.rotation.y = Math.PI / 2; water.add(w); }
    const label = kit.sign(['비상급수', '음용수 · 연무동 행정복지센터'], { w: 2.2, h: 0.55, bg: '#1f5fa8', single: true }); label.position.set(px, y + 1.6, tz - 0.58); water.add(label);
    water.add(new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 3.2, 10).rotateZ(Math.PI / 2), kit.mat('metal')));
    water.children[water.children.length - 1].position.set(px, y + 0.9, tz - 0.3);
    for (let k = 0; k < 4; k++) { const x = px - 1.2 + k * 0.8; water.add(kit.cyl(0.025, 0.025, 0.25, kit.mat('metal'), { x, y: y + 0.65, z: tz - 0.18 }, 8), kit.box(0.08, 0.06, 0.12, kit.mat('paint:#1f5fa8'), { x, y: y + 0.92, z: tz - 0.25 })); }
    water.add(kit.box(3.4, 0.1, 0.6, kit.mat('paint:#5a6a74'), { x: px, y: y + 0.25, z: tz - 0.2 }), kit.box(3.4, 0.25, 0.08, steel, { x: px, y: y, z: tz + 0.08 }));
    // tent (몽골텐트) over the taps, valance text
    const T = 5, tx = px, tzc = tz - 0.8;
    for (const [dx, dz] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) water.add(kit.box(0.06, 2.4, 0.06, kit.mat('metal'), { x: tx + dx * T / 2, y, z: tzc + dz * T / 2 }));
    const roof = new THREE.Mesh(new THREE.ConeGeometry(T * 0.72, 1.3, 4, 1, true), kit.mat('paint:#f4f4ee', { roughness: 0.8, side: THREE.DoubleSide })); roof.rotation.y = Math.PI / 4; roof.position.set(tx, y + 3.05, tzc); roof.castShadow = true; water.add(roof);
    const val = kit.sign(['비상급수 · 연무동'], { w: T, h: 0.32, bg: '#f4f4ee', fg: '#1f5fa8', single: true });
    for (const [ry, dx, dz] of [[Math.PI, 0, -1], [0, 0, 1], [Math.PI / 2, 1, 0], [-Math.PI / 2, -1, 0]]) { const v = val.clone(); v.position.set(tx + dx * T / 2 * 1.002, y + 2.24, tzc + dz * T / 2 * 1.002); v.rotation.y = ry; water.add(v); }
    // table with bottles and bags, a notice
    const tbx = px - 4.2, tbz = tz + 0.4;
    water.add(kit.box(1.8, 0.05, 0.7, white, { x: tbx, y: y + 0.72, z: tbz }), ...[-0.8, 0.8].map(dx => kit.box(0.05, 0.72, 0.6, dark, { x: tbx + dx, y, z: tbz })));
    const bottles = []; for (let k = 0; k < 14; k++) bottles.push({ x: tbx - 0.75 + (k % 7) * 0.24, y: y + 0.77, z: tbz - 0.12 + Math.floor(k / 7) * 0.24 });
    water.add(kit.instanced(new THREE.CylinderGeometry(0.055, 0.06, 0.32, 10).translate(0, 0.16, 0), kit.mat('paint:#9fd0ea', { roughness: 0.15, transparent: true, opacity: 0.75 }), bottles, { cast: false }));
    const nt = kit.sign(['비상급수 운영', '09:00 – 17:00', '현장 안내에 따라 주세요'], { w: 0.7, h: 0.9, bg: '#ffffff', fg: '#1f4e8c', single: true }); nt.position.set(tbx + 1.4, y + 1.2, tbz + 0.02); water.add(nt, kit.box(0.05, 0.75, 0.05, dark, { x: tbx + 1.4, y, z: tbz - 0.02 }));
    // queue lane: floor marks every metre, stanchions with belts both sides
    const marks = []; for (let k = 0; k < 10; k++) marks.push({ x: WATER.queue.x + k * 0.9, y: y + 0.006, z: WATER.queue.z });
    water.add(kit.instanced(new THREE.PlaneGeometry(0.5, 0.12).rotateX(-Math.PI / 2), kit.mat('paint:#f2c230', { polygonOffset: true, polygonOffsetFactor: -2 }), marks, { cast: false }));
    const belt = kit.mat('paint:#1f5fa8', { roughness: 0.6, side: THREE.DoubleSide });
    for (const z of [WATER.queue.z - 0.75, WATER.queue.z + 0.75]) {
      for (let x = WATER.queue.x + 0.6; x <= WATER.queue.x + 8.7; x += 2.7) water.add(kit.cyl(0.03, 0.03, 0.95, kit.mat('metal'), { x, y, z }, 8), kit.cyl(0.14, 0.14, 0.03, dark, { x, y, z }, 12));
      const b = new THREE.Mesh(new THREE.PlaneGeometry(8.1, 0.06), belt); b.position.set(WATER.queue.x + 4.65, y + 0.9, z); water.add(b);
    }
  }
  const waterSolids = [
    { x0: PLACES.water.x - 2.6, x1: PLACES.water.x + 2.6, z0: WATER.tapZ - 2.3, z1: WATER.tapZ + 0.15, y0: -1, y1: 3, id: 'waterTank', off: true },
    { x0: PLACES.water.x - 5.2, x1: PLACES.water.x - 3.2, z0: WATER.tapZ + 0.02, z1: WATER.tapZ + 0.8, y0: -1, y1: 1, id: 'waterTable', off: true },
    { x0: WATER.queue.x + 0.5, x1: WATER.queue.x + 8.8, z0: WATER.queue.z - 0.8, z1: WATER.queue.z - 0.7, y0: -1, y1: 1.2, id: 'queueBelt', off: true },
    { x0: WATER.queue.x + 0.5, x1: WATER.queue.x + 8.8, z0: WATER.queue.z + 0.7, z1: WATER.queue.z + 0.8, y0: -1, y1: 1.2, id: 'queueBelt', off: true },
  ];
  solids.push(...waterSolids);
  kit.merge(water);
  const setWater = (on) => { water.visible = on; for (const s of waterSolids) s.off = !on; };

  // ================= buses + assembly reception =================
  const buses = new THREE.Group(); buses.name = 'buses'; buses.visible = false; group.add(buses);
  const busSolids = [];
  const dest = kit.sign(['임시 대피 버스'], { w: 1.9, h: 0.28, bg: '#120b04', fg: '#ffae2a', single: true, emissive: 1 });
  kit.lit('signs', dest.material, 1.6);
  const side = kit.sign(['임시 대피 버스', '연무동 → 지정 대피 시설'], { w: 1.6, h: 0.5, bg: '#ffffff', fg: '#c42b25', single: true });
  for (const [i, b] of BUS.entries()) {
    const y = hy(b.x, b.z), g = new THREE.Group(); g.position.set(b.x, y, b.z); buses.add(g);
    const body = kit.mat(i ? 'paint:#2f8a4a' : 'paint:#1f5fa8', { roughness: 0.35, metalness: 0.3 });
    g.add(kit.rbox(2.5, 1.3, 11, 0.12, white, { y: 0.35 }), kit.box(2.46, 1.06, 10.7, kit.mat('darkGlass'), { y: 1.62 }), kit.rbox(2.5, 0.48, 11, 0.15, white, { y: 2.62 }));
    g.add(kit.box(2.52, 0.2, 11.02, body, { y: 1.32 }), kit.box(2.52, 0.12, 11.02, body, { y: 2.6 }));
    g.add(kit.box(0.04, 2.2, 1.1, kit.mat('paint:#202426'), { x: 1.25, y: 0.4, z: -4.3 }));
    for (const z of [-3.6, 3.4]) for (const s of [-1, 1]) { const w = new THREE.Mesh(new THREE.CylinderGeometry(0.5, 0.5, 0.3, 18).rotateZ(Math.PI / 2), kit.mat('rubber')); w.position.set(s * 1.1, 0.5, z); w.castShadow = true; g.add(w); }
    const fd = dest.clone(); fd.position.set(0, 2.88, -5.51); fd.rotation.y = Math.PI; g.add(fd);
    const sd = side.clone(); sd.position.set(1.27, 2.0, -2.2); sd.rotation.y = Math.PI / 2; g.add(sd);
    for (const s of [-0.85, 0.85]) g.add(kit.box(0.3, 0.12, 0.04, kit.mat('emit:#fff3d6'), { x: s, y: 0.75, z: -5.52, cast: false }));
    busSolids.push({ x0: b.x - 1.35, x1: b.x + 1.35, z0: b.z - 5.6, z1: b.z + 5.6, y0: -1, y1: 3.3, id: 'bus', off: true });
  }
  {
    const ax = -59, az = -28.6;
    const st = kit.sign(['임시 집결지', '주민 이동 지원 접수'], { w: 1.3, h: 0.7, bg: '#1f4e8c', single: false }); st.position.set(ax, Y + 2.2, az); st.rotation.y = Math.PI / 2; buses.add(st, kit.cyl(0.04, 0.04, 2.6, steel, { x: ax, y: Y, z: az + 0.05 }, 8));
    const tx = -59.8, tz = -33;
    for (const [dx, dz] of [[-1.5, -1.5], [1.5, -1.5], [1.5, 1.5], [-1.5, 1.5]]) buses.add(kit.box(0.05, 2.3, 0.05, kit.mat('metal'), { x: tx + dx, y: Y, z: tz + dz }));
    const roof = new THREE.Mesh(new THREE.ConeGeometry(2.15, 0.9, 4, 1, true), kit.mat('paint:#f4f4ee', { roughness: 0.8, side: THREE.DoubleSide })); roof.rotation.y = Math.PI / 4; roof.position.set(tx, Y + 2.75, tz); buses.add(roof);
    buses.add(kit.box(0.7, 0.05, 1.8, white, { x: tx - 0.4, y: Y + 0.72, z: tz }), ...[-0.8, 0.8].map(dz => kit.box(0.6, 0.72, 0.05, dark, { x: tx - 0.4, y: Y, z: tz + dz })));
    busSolids.push({ x0: ax - 0.12, x1: ax + 0.12, z0: az - 0.12, z1: az + 0.12, y0: -1, y1: 3, id: 'assemblySign', off: true }, { x0: tx - 0.8, x1: tx, z0: tz - 0.95, z1: tz + 0.95, y0: -1, y1: 1, id: 'assemblyTable', off: true });
  }
  solids.push(...busSolids);
  kit.merge(buses);
  const setBuses = (on) => { buses.visible = on; for (const s of busSolids) s.off = !on; };

  // ================= per-frame =================
  let gt = 0;
  function update(dt, t, ctx = {}) {
    const blink = Math.floor(t * 1.6) % 2 === 0;
    flashMat.emissiveIntensity = active.length && blink ? 4 : 0;
    gt += dt;
    const pl = ctx.player;
    guides.forEach((p, k) => {
      if (!p?.group.visible) return;
      const dx = pl ? pl.x - p.group.position.x : 99, dz = pl ? pl.z - p.group.position.z : 99, near = dx * dx + dz * dz < 64;
      p.lookAt?.(near ? pl.x : null, near ? pl.z : null);
      const phase = (gt + k * 2.3) % 9;
      p.setAnim?.(near ? (phase < 3 ? 'point' : 'talk') : phase < 3 ? 'wave' : 'idle');
      if (dx * dx + dz * dz < 6400) p.update?.(dt);
    });
  }

  const closurePoints = Object.keys(CLOSURES).map(id => {
    const gp = CLOSURE_GUIDES[id];
    return { id: `closure_${id}`, label: `안내 요원 · ${CLOSURES[id].label}`, x: gp.x, y: hy(gp.x, gp.z), z: gp.z, r: 2.4, kind: 'npc', enabled: false };
  });
  return { group, solids, setClosures, setWater, setBuses, update, closurePoints, guides, get active() { return active; } };
}
