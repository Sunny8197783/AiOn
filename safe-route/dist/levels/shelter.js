// 〈경보 이전〉 shelter level: the 연무 생활문화센터 basement car park (B1) in use as a civil-defence shelter.
// Built from shelter-shell (structure, the four ways in), shelter-services (lights, pipes, safety kit),
// shelter-camp (mats, reception, board, radio, supplies, parked cars) and shelter-people (evacuees, staff, 서연).
// Local frame: x east, z south, floor y = 0, yaw 0 faces north (-z). Town footprint: local = town + (91, 0, -31).
// States: crowd 'arriving'|'settled'|'leaving', phase 'alert'|'calm', power 'grid'|'emergency' (emergency: circuit B
// tubes and area lights off, battery emergency lights on, jet fans spin down, lift display dark),
// fire 'none'|'large' (a camping-burner fire on stacked cartons in the north-west corner: flames reach the cable
// tray and sprinkler main, a smoke layer spreads under the slab, nearby evacuees have moved away, a solid blocks it),
// boardText string[]|null (lines the director pins on the notice board; no text is invented here).
import { shelterTextures } from './shelter-tex.js';
import { buildShell, ROOM } from './shelter-shell.js';
import { buildServices } from './shelter-services.js';
import { buildCamp, SPOT, RECEPTION, BOARD, RADIO } from './shelter-camp.js';
import { buildPeople } from './shelter-people.js';

const FIRE = { x: -18.6, z: -9.2 };
const STATES = {
  crowd: ['arriving', 'settled', 'leaving'], phase: ['alert', 'calm'], power: ['grid', 'emergency'], fire: ['none', 'large'],
  boardText: [null, ['연무 생활문화센터 지하주차장 · 민방위 대피소']],
};
// Faulty tube: steady, with a short starter stutter every 9 s (steady when reduceMotion).
const flicker = (t) => (t % 9 < 1.1 ? (Math.sin(t * 71) * Math.sin(t * 23) > 0 ? 0.95 : 0.08) : 0.9);

function sootTex(kit) {
  return kit.memo('sh:soot', () => kit.texture(kit.canvas(256, 256, (g) => {
    const r = kit.rng(91);
    for (let i = 0; i < 14; i++) {
      const x = 128 + (r() - 0.5) * 60, y = 128 + (r() - 0.5) * 60, rad = 40 + r() * 45;
      const gr = g.createRadialGradient(x, y, 0, x, y, rad);
      gr.addColorStop(0, 'rgba(14,11,9,0.5)'); gr.addColorStop(1, 'rgba(14,11,9,0)');
      g.fillStyle = gr; g.fillRect(0, 0, 256, 256);
    }
  }), { repeat: [1, 1] }));
}

export function buildShelter(kit, people, fx) {
  const { THREE } = kit;
  const T = shelterTextures(kit);
  const group = new THREE.Group(); group.name = 'level-shelter';
  const shell = buildShell(kit, T), services = buildServices(kit, T, shell), camp = buildCamp(kit, T);
  for (const g of [shell.group, services.group, camp.group]) group.add(kit.merge(g));
  group.add(services.live, camp.live);
  const crowd = buildPeople(kit, people, { seats: camp.seats, walk: shell.walk });
  group.add(crowd.group);

  // ---- fire corner: cartons stacked beside a camping burner with a pot; only shown while the fire burns ----
  const fireProps = new THREE.Group(); fireProps.name = 'shelter-fire-props'; fireProps.visible = false; group.add(fireProps);
  const ember = new THREE.MeshStandardMaterial({ color: 0x2a1d14, roughness: 1, emissive: 0xff4a12, emissiveIntensity: 0 });
  const char = kit.mat('paint:#1c1814', { roughness: 1 }), card = kit.mat('paint:#a7835a', { roughness: 0.95 });
  for (const [dx, dz, y, w, h, d, ry, m] of [[0, 0, 0, 0.55, 0.42, 0.42, 0.1, ember], [0.05, 0.02, 0.42, 0.48, 0.36, 0.38, -0.15, ember],
    [-0.55, -0.1, 0, 0.5, 0.4, 0.4, 0.3, char], [0.2, -0.55, 0, 0.46, 0.34, 0.36, -0.4, card], [-0.4, 0.45, 0, 0.42, 0.3, 0.34, 0.6, ember]]) {
    fireProps.add(kit.box(w, h, d, m, { x: FIRE.x + dx, y, z: FIRE.z + dz, ry }));
  }
  const bx = FIRE.x + 0.75, bz = FIRE.z + 0.55;
  fireProps.add(kit.box(0.34, 0.1, 0.27, kit.mat('paint:#2d3034', { roughness: 0.5, metalness: 0.4 }), { x: bx, z: bz, ry: 0.4 }));
  fireProps.add(kit.cyl(0.1, 0.09, 0.09, kit.mat('darkMetal'), { x: bx, y: 0.1, z: bz }, 16));
  fireProps.add(kit.box(0.15, 0.025, 0.12, kit.mat('paint:#c8321f'), { x: bx + 0.3, z: bz + 0.2, ry: 1.1 }));   // ramen packet
  const soot = new THREE.MeshBasicMaterial({ map: sootTex(kit), transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2 });
  const ceilSoot = new THREE.Mesh(new THREE.PlaneGeometry(4.5, 4.5), soot); ceilSoot.rotation.x = Math.PI / 2; ceilSoot.position.set(FIRE.x, ROOM.ceil - 0.005, FIRE.z);
  const floorSoot = new THREE.Mesh(new THREE.PlaneGeometry(2.6, 2.6), soot); floorSoot.rotation.x = -Math.PI / 2; floorSoot.position.set(FIRE.x, 0.008, FIRE.z);
  fireProps.add(ceilSoot, floorSoot);

  // ---- fx: flames on the stack (warm flickering light), a low lick at its base, ceiling smoke, dust motes ----
  const flames = fx.fire({ x: FIRE.x, y: 0.7, z: FIRE.z, scale: 2.1, radius: 0.36, level: 0 });
  const lick = fx.fire({ x: FIRE.x - 0.45, y: 0.02, z: FIRE.z + 0.4, scale: 0.85, light: false, level: 0 });
  const smoke = fx.smoke({ x: FIRE.x, y: 0.6, z: FIRE.z, ceiling: ROOM.ceil - 0.75, spread: 12, radius: 0.45, count: 120, density: 0, color: 0x2b2724,
    bounds: { x0: ROOM.x0 + 0.4 - FIRE.x, x1: ROOM.x1 - 0.4 - FIRE.x, z0: ROOM.z0 + 0.4 - FIRE.z, z1: ROOM.z1 - 0.4 - FIRE.z } });
  const motes = fx.dust({ box: { x0: -21.5, x1: 21.5, y0: 0.2, y1: 2.8, z0: -15.5, z1: 15.5 }, count: 420, opacity: 0.22 });
  group.add(flames.group, lick.group, smoke.group, motes.group);
  const fireSolid = { id: 'fire', x0: FIRE.x - 1.0, x1: FIRE.x + 1.2, z0: FIRE.z - 0.9, z1: FIRE.z + 1.0, y0: -1, y1: 3, off: true };
  const solids = [...shell.solids, ...camp.solids, fireSolid];

  const P = (id, label, x, z, kind, r = 1.6) => ({ id, label, x, y: 0, z, r, kind });
  const points = [
    P('fromStairs', '출입구 A · 계단', 2.9, -15.2, 'entrance', 1.3),
    P('shelterExit', '계단으로 나가기 · 지상', 5, -16.7, 'door', 1.4),
    P('fromRamp', '출입구 B · 차량 진출입로', 11, 15.0, 'entrance', 1.8),
    P('fromAccess', '출입구 C · 경사로', -13, -15.2, 'entrance', 1.6),
    P('reception', '접수대 · 대피자 명부', RECEPTION.x, RECEPTION.z - 0.85, 'counter', 1.7),
    P('shelterBoard', '대피소 게시판', BOARD.x, BOARD.z + 0.9, 'notice', 1.7),
    P('shelterRadio', '공용 라디오', RADIO.x, RADIO.z - 0.75, 'radio', 1.6),
    P('spot', '빈 자리 · 돗자리', SPOT.x, SPOT.z, 'seat', 1.6),
  ];
  const spawns = {
    stairs: { x: 5, y: 0, z: -14.6, yaw: Math.PI },        // stair A foot, facing into the hall
    ramp: { x: 11, y: 0, z: 14.4, yaw: 0 },                // bottom of car ramp B
    access: { x: -13, y: 0, z: -14.6, yaw: Math.PI },      // bottom of accessible ramp C
    east: { x: 23.6, y: 0, z: 0, yaw: Math.PI / 2 },       // lift hall behind the east fire doors
    spot: { x: SPOT.x, y: 0, z: SPOT.z + 1.4, yaw: 0.45 }, // at the free mat, the fire corner in view
  };
  const anchors = {
    overview: { x: 19.5, y: 2.45, z: 9.5 }, reception: { x: 4.5, y: 1.65, z: -11.8 }, stairFoot: { x: 5, y: 1.6, z: -13.5 },
    spot: { x: SPOT.x + 2.6, y: 1.5, z: SPOT.z + 3.2 }, fire: { x: -11.5, y: 1.7, z: -5.6 },
  };

  // ---- states ----
  const state = { crowd: 'settled', phase: 'calm', power: 'grid', fire: 'none', boardText: null };
  function setState(key, v) {
    if (key === 'boardText') { state.boardText = Array.isArray(v) ? v.map(String) : null; camp.boardMat.map = T.drawBoard(state.boardText); return; }
    if (!STATES[key]) return void console.error('[shelter] unknown state', key, v);
    if (!STATES[key].includes(v)) return void console.error(`[shelter] ${key} must be ${STATES[key].join('|')}`, v);
    state[key] = v;
    if (key === 'crowd') crowd.setCrowd(v);
    else if (key === 'phase') crowd.setPhase(v);
    else if (key === 'fire') {
      const on = v === 'large';
      flames.setLevel(on ? 1 : 0); lick.setLevel(on ? 0.8 : 0); smoke.setDensity(on ? 0.9 : 0);   // smoke thins out over seconds
      fireProps.visible = on; fireSolid.off = !on; crowd.setFire(on ? FIRE : null);
    }
  }

  let fan = 14;
  function update(dt, t, ctx = {}) {
    const grid = state.power === 'grid', rm = !!fx?.reduceMotion, tubes = services.tubes;
    const s = tubes.A.emissiveIntensity / 3.2;   // the env preset's 'indoor' level (0..1); circuit B and lights follow it
    tubes.B.emissiveIntensity = grid ? s * 3.2 : 0;
    tubes.F.emissiveIntensity = s * 3.2 * (rm ? 0.55 : flicker(t));
    for (const L of services.lights) L.light.intensity = L.base * s * (grid || L.circuit === 'A' ? 1 : 0);
    services.emLamp.emissiveIntensity = grid ? 0 : 2.4;
    for (const l of services.emLights) l.intensity = grid ? 0 : 14;
    for (const m of shell.dyn.lift) m.emissiveIntensity = grid ? 1.2 : 0;
    fan += ((grid ? 14 : 0) - fan) * Math.min(1, dt * 0.35);
    for (const f of services.fans) f.rotor.rotation.x += fan * dt;
    services.cctvLed.emissiveIntensity = t % 1.4 < 0.18 ? 2.6 : 0.25;
    camp.dial.emissiveIntensity = state.phase === 'alert' ? 1.6 : 1.1;
    camp.banner.rotation.x = rm ? 0 : Math.sin(t * 0.55) * 0.02 * (0.3 + fan / 14);   // ventilation draught
    ember.emissiveIntensity = state.fire === 'large' ? 0.9 + (rm ? 0 : 0.35 * Math.sin(t * 9.1) * Math.sin(t * 3.7)) : 0;
    flames.update(dt); lick.update(dt); smoke.update(dt, ctx.camera); motes.update(dt, ctx.camera);
    crowd.update(dt, t, ctx.player);
  }
  crowd.setPhase(state.phase);

  return {
    id: 'shelter', group, env: 'underground', walk: shell.walk, solids, points, spawns,
    npcs: crowd.npcs, states: STATES, setState, update, anchors,
  };
}
