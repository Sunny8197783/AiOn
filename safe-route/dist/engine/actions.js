// Hands: punch (left click / Q), pick up and throw loose things (G, or click while holding one).
// Loose things (bricks, bottles, cans...) are scattered on each level's walk zones the first time it is
// entered; thrown ones fly with gravity, bounce off solids and floors and knock people back.
// People are engine.persons (every createPerson); a crowd member answers p.onHit (flee / knocked down),
// anyone else is pushed back and may hit back.
// createActions(engine, { onHit(person, how, ko), onHurt(damage, why), onSound(name), onNear(label|null) })
import * as THREE from 'three';
import { groundAt, collide } from './geom.js';

const KINDS = {
  brick: { name: '벽돌', geo: () => new THREE.BoxGeometry(0.21, 0.065, 0.1), color: 0x9a4a32, rough: 0.95, r: 0.06, power: 2 },
  rock: { name: '돌멩이', geo: () => new THREE.DodecahedronGeometry(0.07), color: 0x77736c, rough: 1, r: 0.07, power: 2 },
  bottle: { name: '빈 병', geo: () => new THREE.CylinderGeometry(0.035, 0.04, 0.26, 10), color: 0x2f6b3a, rough: 0.15, r: 0.05, power: 1 },
  can: { name: '빈 캔', geo: () => new THREE.CylinderGeometry(0.033, 0.033, 0.12, 10), color: 0xb8c0c8, rough: 0.35, r: 0.04, power: 0.5 },
  box: { name: '빈 상자', geo: () => new THREE.BoxGeometry(0.3, 0.22, 0.24), color: 0xb08a5a, rough: 0.9, r: 0.12, power: 0.5 },
};
const SCATTER = { town: [40, ['brick', 'rock', 'rock', 'bottle', 'can']], default: [8, ['bottle', 'can', 'box']] };
const G = 12, PICK_R = 1.5, REACH = 1.4;

function rng(seed) { let a = seed >>> 0; return () => { a = (a + 0x6D2B79F5) >>> 0; let t = Math.imul(a ^ (a >>> 15), a | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }
const inScene = (o, scene) => { for (let x = o; x; x = x.parent) { if (!x.visible) return false; if (x === scene) return true; } return false; };

export function createActions(engine, { onHit = () => {}, onHurt = () => {}, onSound = () => {}, onNear = () => {} } = {}) {
  const { scene, camera } = engine;
  const geos = {}, mats = {};
  const meshOf = (kind) => {
    const k = KINDS[kind];
    const m = new THREE.Mesh(geos[kind] ||= k.geo(), mats[kind] ||= new THREE.MeshStandardMaterial({ color: k.color, roughness: k.rough, transparent: kind === 'bottle', opacity: kind === 'bottle' ? 0.85 : 1 }));
    m.castShadow = true; return m;
  };
  const byLevel = new Map(); // level -> items [{ kind, mesh, v, flying }]
  let held = null, near = null, punchT = 0, enabled = true;
  const reactions = new Map(); // person -> { t, vx, vz, back }

  function scatter(L) {
    const [n, kinds] = SCATTER[L.id] || SCATTER.default, r = rng(L.id.length * 7919 + 13), zones = (L.walk || []).filter((w) => !w.ramp && (w.x1 - w.x0) * (w.z1 - w.z0) > 4);
    const items = [];
    for (let i = 0, tries = 0; i < n && zones.length && tries < n * 6; tries++) {
      const w = zones[Math.floor(r() * zones.length)], x = w.x0 + 0.5 + r() * (w.x1 - w.x0 - 1), z = w.z0 + 0.5 + r() * (w.z1 - w.z0 - 1);
      const [cx, cz] = collide(x, z, w.y ?? 0, L.solids || [], 0.3, 1);
      if (Math.hypot(cx - x, cz - z) > 0.01) continue;
      const kind = kinds[Math.floor(r() * kinds.length)], mesh = meshOf(kind);
      mesh.position.set(x, (w.y ?? 0) + KINDS[kind].r, z); mesh.rotation.y = r() * 6.28;
      L.group.add(mesh); items.push({ kind, mesh, v: new THREE.Vector3(), flying: false }); i++;
    }
    byLevel.set(L, items);
    return items;
  }
  const itemsOf = (L) => byLevel.get(L) ?? scatter(L);

  // People the player can reach: in the scene, visible, not the player's own model.
  function people() {
    const out = [], me = engine.player.model;
    for (const p of engine.persons) if (p !== me && inScene(p.group, scene)) out.push(p);
    return out;
  }
  const wp = new THREE.Vector3();
  function hit(p, fromX, fromZ, how, power) {
    p.group.getWorldPosition(wp);
    const dx = wp.x - fromX, dz = wp.z - fromZ, d = Math.hypot(dx, dz) || 1;
    const rec = reactions.get(p) ?? { hp: 3 };
    rec.hp -= power; const ko = rec.hp <= 0;
    onSound(how === 'punch' ? 'punch' : 'thud');
    if (p.onHit) p.onHit(engine.player.x, engine.player.z, ko); // crowd member: flinch, flee or go down
    else { // a placed person: pushed back, may hit back
      Object.assign(rec, { t: ko ? 12 : 0.45, push: 0.45, vx: (dx / d) * 1.6, vz: (dz / d) * 1.6, ko, back: !ko && Math.random() < 0.55 ? 0.6 : 0, anim: p.anim });
      p.setAnim?.(ko ? 'down' : 'hurt');
    }
    if (ko) rec.hp = 3;
    reactions.set(p, rec);
    onHit(p, how, ko);
  }

  function punch() {
    if (punchT > 0 || engine.player.busy) return;
    punchT = 0.5; engine.act('punch'); setTimeout(() => onSound('swing'), 90);
    setTimeout(() => { // the fist arrives ~0.19 s in (people-clips.js punch)
      const P = engine.player, yaw = P.model?.group?.rotation.y ?? P.yaw, fx = -Math.sin(yaw), fz = -Math.cos(yaw);
      let best = null, bd = REACH;
      for (const p of people()) {
        p.group.getWorldPosition(wp);
        const dx = wp.x - P.x, dz = wp.z - P.z, d = Math.hypot(dx, dz);
        if (d < bd && Math.abs(wp.y - P.y) < 1 && (dx * fx + dz * fz) / (d || 1) > 0.55) { bd = d; best = p; }
      }
      if (best) hit(best, P.x, P.z, 'punch', 1);
    }, 190);
  }

  // Bend down for it; the hand closes on it at the bottom of the reach (~0.4 s into people-clips.js pickup)
  // and it comes up held ready in front of the chest.
  let busyT = 0;
  function pick(it) {
    held = it; it.flying = false; busyT = 0.8;
    engine.act('pickup'); onNear(null);
    setTimeout(() => {
      if (held !== it) return;
      const hand = engine.player.model?.bones?.rHand;
      if (hand) { hand.add(it.mesh); it.mesh.position.set(0, -0.08, -0.02); it.mesh.rotation.set(0, 0, 0); it.mesh.scale.setScalar(1); }
      else scene.add(it.mesh);
      onSound('pick'); engine.setHold(true);
    }, 400);
  }
  // Wind up and let go at the top of the whip (~0.3 s into people-clips.js throw), along the line of sight.
  function toss() {
    if (busyT > 0) return;
    const it = held; busyT = 0.6;
    engine.act('throw'); engine.setHold(false);
    setTimeout(() => {
      held = null;
      const L = engine.level; if (!L) return;
      it.mesh.getWorldPosition(wp); L.group.add(it.mesh); it.mesh.position.copy(wp);
      const dir = new THREE.Vector3(); camera.getWorldDirection(dir);
      it.v.copy(dir).multiplyScalar(14).add(new THREE.Vector3(0, 2.2, 0)); it.flying = true; it.byPlayer = true; it.from = { x: engine.player.x, z: engine.player.z };
      onSound('swing');
    }, 300);
  }

  engine.onKey(({ code }) => {
    if (!enabled || !engine.controlsEnabled) return;
    if (code === 'KeyG') { if (held) toss(); else if (near) pick(near); }
    else if (code === 'Mouse0' || code === 'KeyQ') { if (held) toss(); else punch(); }
  });

  const tmp = new THREE.Vector3();
  function fly(it, L, dt) {
    const k = KINDS[it.kind], m = it.mesh, v = it.v;
    v.y -= G * dt;
    m.position.addScaledVector(v, dt); m.rotation.x += dt * 9; m.rotation.z += dt * 5;
    const p = m.position;
    for (const s of L.solids || []) { // bounce off the face it went through least
      if (!s || s.off || p.x < s.x0 - k.r || p.x > s.x1 + k.r || p.z < s.z0 - k.r || p.z > s.z1 + k.r || p.y < s.y0 || p.y > s.y1 + k.r) continue;
      const pen = [[p.x - (s.x0 - k.r), 'x', -1], [s.x1 + k.r - p.x, 'x', 1], [p.z - (s.z0 - k.r), 'z', -1], [s.z1 + k.r - p.z, 'z', 1], [s.y1 + k.r - p.y, 'y', 1]].sort((a, b) => a[0] - b[0])[0];
      p[pen[1]] += pen[2] * pen[0]; v[pen[1]] *= -0.35; v.multiplyScalar(0.7);
      if (v.length() > 2) onSound('thud');
    }
    const g = groundAt(L.walk, p.x, p.z, p.y + 0.3, false, 50) ?? { y: 0 };
    if (p.y <= g.y + k.r) {
      p.y = g.y + k.r;
      if (Math.abs(v.y) > 2) onSound(it.kind === 'bottle' ? 'glass' : 'thud');
      v.y *= -0.3; v.x *= 0.55; v.z *= 0.55;
      if (v.length() < 0.5) { it.flying = false; m.rotation.x = it.kind === 'bottle' || it.kind === 'can' ? Math.PI / 2 : 0; m.rotation.z = 0; }
    }
    if (v.length() > 3) for (const per of people()) {
      per.group.getWorldPosition(tmp);
      if (Math.hypot(tmp.x - p.x, tmp.z - p.z) < 0.45 && p.y > tmp.y && p.y < tmp.y + 1.8) { hit(per, it.from.x, it.from.z, it.byPlayer ? 'throw' : 'debris', k.power); v.multiplyScalar(-0.2); break; }
    }
  }

  engine.onFrame((dt) => {
    punchT = Math.max(0, punchT - dt); busyT = Math.max(0, busyT - dt);
    const L = engine.level; if (!L) return;
    const items = itemsOf(L), P = engine.player;
    for (const it of items) if (it.flying && it !== held) fly(it, L, dt);
    // reactions of placed people (crowds handle their own)
    for (const [p, r] of reactions) {
      if (!(r.t > 0)) continue;
      r.t -= dt;
      if (r.push > 0) { const k = r.push / 0.45; r.push -= dt; p.group.position.x += r.vx * dt * k * 2; p.group.position.z += r.vz * dt * k * 2; }
      if (r.back > 0 && (r.back -= dt) <= 0 && Math.hypot(P.x - p.group.position.x, P.z - p.group.position.z) < 1.8) {
        p.setAnim?.('punch'); p.lookAt?.(P.x, P.z); onSound('punch'); engine.act('hurt', 0.4); engine.shake(0.12, 0.35);
        onHurt(8 + Math.round(Math.random() * 6), 'fight');
        r.t = Math.max(r.t, 0.4);
      }
      if (r.t <= 0) p.setAnim?.(r.anim && r.anim !== 'hurt' && r.anim !== 'down' && r.anim !== 'punch' ? r.anim : 'idle');
    }
    // the nearest loose thing within reach, for the G prompt
    let best = null, bd = PICK_R;
    if (!held && engine.controlsEnabled) for (const it of items) {
      if (it.flying) continue;
      const d = Math.hypot(it.mesh.position.x - P.x, it.mesh.position.z - P.z);
      if (d < bd && Math.abs(it.mesh.position.y - P.y) < 1.4) { bd = d; best = it; }
    }
    if (best !== near) { near = best; onNear(near ? `G · ${KINDS[near.kind].name} 줍기` : null); }
  });

  return {
    // A loose thing flung from (x, y, z) with velocity (vx, vy, vz): blast debris, a looter's drop.
    spawn(kind, x, y, z, vx = 0, vy = 0, vz = 0) {
      const L = engine.level; if (!L || !KINDS[kind]) return;
      const mesh = meshOf(kind); mesh.position.set(x, y, z); L.group.add(mesh);
      itemsOf(L).push({ kind, mesh, v: new THREE.Vector3(vx, vy, vz), flying: true, from: { x, z } });
    },
    get held() { return held ? KINDS[held.kind].name : null; },
    setEnabled(on) { enabled = !!on; },
    // Leaving a level with something in hand: it is dropped where you stand.
    drop() { if (!held) return; const it = held; held = null; engine.setHold(false); it.mesh.getWorldPosition(wp); (engine.level?.group ?? scene).add(it.mesh); it.mesh.position.copy(wp); it.v.set(0, 0, 0); it.flying = true; it.from = { x: wp.x, z: wp.z }; },
  };
}
