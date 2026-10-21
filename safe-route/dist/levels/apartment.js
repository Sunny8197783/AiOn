// 한빛아파트 103동 level: ground-floor lobby with 관리사무소 (박 소장), the stairwell, the 3rd-floor corridor (301-304,
// 옥내소화전 + 분말소화기, the hallway-fire spot near 304), 302호 (정민's flat) and the view out of the windows.
// buildApartment(kit, people, fx) -> Level (see spec 5.2). Lighting: room lamps, motion-sensor lamps (센서등) and
// battery emergency lamps share a small pool of PointLights given to the fixtures nearest the player, so the light
// count never changes (no shader recompiles). Green exit signs are in the kit's 'emergency' group and stay lit.
import * as THREE from 'three';
import { createBuilder, F3, FL } from './apartment-parts.js';
import { buildLobby, buildElevator, buildStairs } from './apartment-core.js';
import { buildCorridor, FIRE_SPOT } from './apartment-corridor.js';
import { buildHome } from './apartment-home.js';
import { buildProps } from './apartment-props.js';
import { buildStock } from './apartment-stock.js';
import { buildView } from './apartment-view.js';

const STATES = {
  power: ['on', 'off'],
  stock: { water_2l: [0, 2, 6, 12, 24, 40], water_500: [0, 4, 12], rice_instant: [0, 2, 6, 12], canned: [0, 1, 4, 12], ramen: [0, 5, 10, 20],
    batteries_aa: [0, 1, 3], candles: [0, 1, 2], gas_stove: [0, 1], butane: [0, 1, 3], first_aid: [0, 1], tissue: [0, 2, 6], blanket: [0, 2], wipes: [0, 1, 2], water_jug: [0, 1] },
  bag: ['none', 'empty', 'packed'],
  radio: ['none', 'off', 'on'],
  hallFire: ['none', 'small', 'large', 'out'],
  smoke: [0, 0.25, 0.5, 0.75, 1],
  seoyeonDoor: ['closed', 'open'],
  candles: ['none', 'lit'],
  windowView: ['day', 'dusk', 'night', 'blackout'],
};

export async function buildApartment(kit, people, fx) {
  const B = createBuilder(kit), H = {};
  B.fixtures = []; // room lamps { x, y, z, color, intensity, distance } (on with power)
  const group = new THREE.Group(); group.name = 'level-apartment';
  const dyn = new THREE.Group(); dyn.name = 'apt-dynamic';
  const areas = ['lobby', 'stairs', 'corridor', 'home'].map((n) => { const a = new THREE.Group(); a.name = 'apt-' + n; group.add(a); return a; });
  group.add(dyn);
  buildLobby(B, areas[0]); buildElevator(B, areas[0], 0, H);
  buildStairs(B, areas[1], dyn);
  buildCorridor(B, areas[2], dyn, H);
  buildHome(B, areas[3], H);
  const props = buildProps(B, areas[3], dyn), stock = buildStock(B, areas[3], dyn), view = buildView(B);
  group.add(view.group);
  B.fixtures.push({ x: 4, y: 2.3, z: -3.3, color: 0xfff3e2, intensity: 4.5, distance: 11 }, { x: 10.75, y: 2.25, z: -4, color: 0xfff3e2, intensity: 2.5, distance: 6 });
  for (const a of areas) kit.merge(a);

  // ---- fixtures + light pool ----
  const fixtures = [
    ...B.fixtures.map((f) => ({ ...f, kind: 'room' })),
    ...B.sensors.map((s) => s.kind === 'backup'
      ? { x: s.x, y: s.y, z: s.z, color: 0xe6eeff, intensity: 2.4, distance: 7, kind: 'backup' }
      : { x: s.x, y: s.y - 0.25, z: s.z, color: 0xfff1dc, intensity: 3.0, distance: 6.5, kind: 'sensor', hold: 0, ceil: s.y }),
  ];
  for (const f of fixtures) { f.w = 0; f.on = false; }
  const POOL = kit.quality === 'low' ? 3 : 5;
  const pool = Array.from({ length: POOL }, () => { const l = new THREE.PointLight(0xffffff, 0, 8, 2); l.castShadow = false; dyn.add(l); return l; });
  // sensor lamp diffusers: an "off" plastic dome for every lamp, a glowing dome shown over it while lit
  const sensors = fixtures.filter((f) => f.kind === 'sensor');
  const dome = new THREE.SphereGeometry(0.12, 16, 6, 0, Math.PI * 2, 0, Math.PI / 2).scale(1, 0.45, 1).rotateX(Math.PI);
  const at = sensors.map((f) => ({ x: f.x, y: f.ceil, z: f.z }));
  dyn.add(kit.instanced(dome, kit.mat('paint:#f3f2ee', { roughness: 0.35 }), at, { cast: false }));
  const glowDomes = kit.instanced(dome, new THREE.MeshBasicMaterial({ color: new THREE.Color(3.2, 3.0, 2.6) }), at.map((p) => ({ ...p, s: 1.03 })), { cast: false, receive: false });
  dyn.add(glowDomes);
  const mOn = sensors.map((_, i) => { const m = new THREE.Matrix4(); glowDomes.getMatrixAt(i, m); return m; }), mOff = new THREE.Matrix4().makeScale(0, 0, 0);
  sensors.forEach((_, i) => glowDomes.setMatrixAt(i, mOff)); glowDomes.instanceMatrix.needsUpdate = true;

  let power = true;
  const setI = (o, v) => { if (o.isLight) o.intensity = v; else o.emissiveIntensity = v; };
  function setPower(v) {
    power = v !== 'off';
    for (const { obj, base } of B.powered) setI(obj, power ? base : 0);
    for (const { obj, base } of B.backup) setI(obj, power ? 0 : base);
  }
  function stepLights(dt, p) {
    const head = p.y + 1.6;
    let changed = false;
    sensors.forEach((f, i) => {
      if (power && Math.hypot(p.x - f.x, p.z - f.z) < 3.4 && Math.abs(head - f.y) < 1.8) f.hold = 9;
      f.hold = Math.max(0, f.hold - dt);
      const on = power && f.hold > 0;
      if (on !== f.on) { f.on = on; glowDomes.setMatrixAt(i, on ? mOn[i] : mOff); changed = true; }
    });
    if (changed) glowDomes.instanceMatrix.needsUpdate = true;
    for (const f of fixtures) {
      if (f.kind !== 'sensor') f.on = f.kind === 'backup' ? !power : power;
      f.d = f.on ? Math.hypot(p.x - f.x, (head - f.y) * 1.6, p.z - f.z) : Infinity;
    }
    const best = fixtures.filter((f) => f.on && f.d < 20).sort((a, b) => a.d - b.d).slice(0, POOL - 1);
    for (const f of fixtures) f.w += ((best.includes(f) ? 1 : 0) - f.w) * Math.min(1, dt * 4);
    const lit = fixtures.filter((f) => f.w > 0.01).sort((a, b) => b.w - a.w);
    pool.forEach((l, i) => {
      const f = lit[i];
      if (!f) { l.intensity = 0; return; }
      l.position.set(f.x, f.y, f.z); l.color.setHex(f.color); l.distance = f.distance; l.intensity = f.intensity * f.w;
    });
  }

  // ---- fire + smoke (created now so the scene's light count stays fixed) ----
  const fire = fx.fire({ x: FIRE_SPOT.x - 0.05, y: F3 + 0.02, z: FIRE_SPOT.z + 0.05, scale: 1.2, level: 0 });
  const sz = FIRE_SPOT.z - 0.4;
  const smoke = fx.smoke({ x: FIRE_SPOT.x, y: F3 + 0.5, z: sz, ceiling: 1.88, spread: 14, density: 0, bounds: { x0: -15.7 - FIRE_SPOT.x, x1: 21.7 - FIRE_SPOT.x, z0: -1.85 - sz, z1: -0.15 - sz } });
  const haze = fx.smoke({ x: -1.4, y: FL + 0.2, z: 1.6, ceiling: 2.08, spread: 5, density: 0, count: 40, color: 0x57524d, bounds: { x0: -6.1, x1: 1.6, z0: -1.5, z1: 6.3 } });
  dyn.add(fire.group, smoke.group, haze.group);
  function setFire(v) {
    const F = H.fire;
    fire.setLevel(v === 'small' ? 0.4 : v === 'large' ? 1 : 0);
    F.normal.visible = v === 'none' || v === 'small'; F.scorched.visible = v === 'large' || v === 'out';
    F.soot.visible = v !== 'none'; F.powder.visible = v === 'out';
    H.firePoint.label = v === 'none' ? '304호 앞 복도' : v === 'out' ? '꺼진 불 자리' : '304호 앞 불';
  }

  // ---- NPCs ----
  const manager = people.createPerson({ preset: 'manager', seed: 31 });
  manager.group.position.set(10.4, 0, -4.1); manager.group.rotation.y = Math.PI / 2; manager.setAnim('sitChair'); dyn.add(manager.group);
  const seoyeon = people.createPerson({ preset: 'seoyeon', seed: 7 });
  seoyeon.group.position.set(H.seoyeonSpot.x, H.seoyeonSpot.y, H.seoyeonSpot.z); seoyeon.group.rotation.y = H.seoyeonSpot.yaw;
  seoyeon.setAnim('idle'); seoyeon.group.visible = false; dyn.add(seoyeon.group);
  let door303 = 0;

  // ---- spawns, anchors ----
  Object.assign(B.spawns, {
    lobby: { x: 4, y: 0, z: -5.4, yaw: Math.PI }, corridor3F: { x: -1.275, y: F3, z: -1.0, yaw: -Math.PI / 2 },
    stairs3F: { x: 5.0, y: F3, z: 0.9, yaw: Math.PI }, stairs1F: { x: 5.0, y: 0, z: 0.9, yaw: Math.PI },
  });
  Object.assign(B.anchors, {
    lobbyView: { x: 8.4, y: 2.2, z: -6.1, target: { x: 1.0, y: 1.0, z: -1.0 } },
    corridorView: { x: -14.5, y: F3 + 2.0, z: -1.6, target: { x: 10, y: F3 + 0.9, z: -0.4 } },
    fireView: { x: 14.5, y: F3 + 1.7, z: -1.6, target: { x: FIRE_SPOT.x, y: F3 + 0.4, z: FIRE_SPOT.z } },
    stairWell: { x: 5.0, y: F3 + 2.2, z: 4.6, target: { x: 5.0, y: 1.0, z: 1.0 } },
  });

  const S = { power: 'on', stock: {}, bag: 'empty', radio: 'none', hallFire: 'none', smoke: 0, seoyeonDoor: 'closed', candles: 'none', windowView: 'day' };
  const apply = {
    power: setPower, stock: (v) => stock.setStock(v), bag: props.setBag, radio: props.setRadio, hallFire: setFire,
    smoke: (v) => { smoke.setDensity(v); haze.setDensity(v * 0.5); }, seoyeonDoor: (v) => { seoyeon.group.visible = v === 'open'; },
    candles: props.setCandle, windowView: view.setView,
  };
  function setState(key, value) {
    if (!apply[key]) { console.warn(`[apartment] unknown state "${key}"; known: ${Object.keys(apply).join(', ')}`); return; }
    if (key === 'smoke') value = Math.min(1, Math.max(0, Number(value) || 0));
    else if (key === 'stock') { if (!value || typeof value !== 'object') { console.error('[apartment] stock needs { itemId: count }', value); return; } }
    else if (!STATES[key].includes(value)) { console.error(`[apartment] bad value for ${key}:`, value, 'allowed:', STATES[key]); return; }
    S[key] = value; apply[key](value);
  }
  for (const [k, v] of Object.entries(S)) apply[k](v);

  function update(dt, t, ctx = {}) {
    const p = ctx.player || { x: 0, y: 0, z: 0 }, cam = ctx.camera;
    for (const D of B.doors) { // auto doors: open while the player is close on the same floor
      const want = Math.hypot(p.x - D.x, p.z - D.z) < D.auto && Math.abs(p.y - D.y) < 1.2 ? 1 : 0;
      if (D.k !== want) D.set(D.k + Math.sign(want - D.k) * Math.min(Math.abs(want - D.k), dt * 1.8));
    }
    const want303 = S.seoyeonDoor === 'open' ? 0.72 : 0;
    if (door303 !== want303) { door303 += Math.sign(want303 - door303) * Math.min(Math.abs(want303 - door303), dt * 1.2); H.door303.set(door303); }
    stepLights(dt, p);
    if (power) for (const tick of B.ticks) tick(dt, t, S);
    else if (H.candleWindow) H.candleWindow.emissiveIntensity = 0.1 + 0.03 * Math.sin(t * 7.1) + 0.02 * Math.sin(t * 12.7); // a neighbour's candle in 301
    props.update(dt, t);
    fire.update(dt, cam); smoke.update(dt, cam); haze.update(dt, cam);
    if (p.y < 2 && Math.hypot(p.x - 10.4, p.z + 4.1) < 25) { // 박 소장 looks up when the player comes to the window
      manager.lookAt?.(...(Math.hypot(p.x - 10.4, p.z + 4.1) < 4.5 ? [p.x, p.z] : [null, null])); manager.update(dt);
    }
    if (seoyeon.group.visible) { seoyeon.lookAt?.(...(Math.hypot(p.x - H.seoyeonSpot.x, p.z - H.seoyeonSpot.z) < 4 && Math.abs(p.y - F3) < 1 ? [p.x, p.z] : [null, null])); seoyeon.update(dt); }
  }

  return {
    id: 'apartment', group, env: 'interior',
    walk: B.walk, solids: B.solids, points: B.points, spawns: B.spawns,
    npcs: { manager, seoyeon }, states: STATES, setState, update, anchors: B.anchors,
  };
}
