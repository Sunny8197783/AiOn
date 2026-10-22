// War in the streets: while the player roams the town after the war has started, shells fall (a falling
// whistle first, then the blast, debris, a crater and a fire), damaged buildings shed rubble onto the
// sidewalk, and desperate people demand food. Crouching (C) and cover between you and a blast cut the damage.
// How much of each depends on the chapter (INTENSITY). Rubble piles are solids you can climb over.
// createDanger({ ctx, actions, hurt }) -> { onHit(person, ko) }
import * as THREE from 'three';
import { BUILDINGS } from '../game/townmap.js';
import { groundAt } from '../engine/geom.js';

// shell / collapse / looter: chance weights 0..1 per chapter (null = peace).
const INTENSITY = {
  dday: (G) => (G.know.strikeSeen ? { shell: 0.8, collapse: 0.5, looter: 0 } : null),
  endure: () => ({ shell: 0.3, collapse: 0.3, looter: 0.7 }),
  crossroads: () => ({ shell: 0.6, collapse: 0.5, looter: 0.5 }),
  evac: () => ({ shell: 0.6, collapse: 0.5, looter: 0.6 }),
};
const FOOD = ['water_2l', 'water_500', 'canned', 'bars', 'rice_instant', 'ramen'];
const rnd = (a, b) => a + Math.random() * (b - a);
const LOOTER_LINES = [['야, 거기. 가방 내려놔.', '물이랑 먹을 거 있지? 다 알아. 순순히 내놔.'], ['애들이 사흘째 굶었어요…', '먹을 거 좀 나눠 줘요. 안 주면… 나도 어쩔 수 없어.']];

export function createDanger({ ctx, actions, hurt }) {
  const { engine, audio } = ctx;
  const scene = engine.scene;
  // One flash light, in the scene from the start so it never forces a shader recompile mid-play.
  const flash = new THREE.PointLight(0xffb070, 0, 40, 2); flash.position.set(0, -500, 0); scene.add(flash);
  const craterMat = new THREE.MeshBasicMaterial({ color: 0x0b0a09, transparent: true, opacity: 0.75, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2 });
  const craterGeo = new THREE.CircleGeometry(1, 20).rotateX(-Math.PI / 2);
  const rubbleMat = engine.kit.surface?.('concrete', { color: 0x8d8780 }) ?? new THREE.MeshStandardMaterial({ color: 0x8a847c, roughness: 1 }), box = new THREE.BoxGeometry(1, 1, 1);
  // The fireball: one additive sphere, reused, also in the scene from the start.
  const ball = new THREE.Mesh(new THREE.SphereGeometry(1, 16, 12), new THREE.MeshBasicMaterial({ color: 0xffa040, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, fog: false }));
  ball.position.set(0, -500, 0); scene.add(ball);
  // The shock ring: dust thrown out along the ground.
  const ring = new THREE.Mesh(new THREE.RingGeometry(0.7, 1, 40).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0xcdbca4, transparent: true, opacity: 0, depthWrite: false, side: THREE.DoubleSide }));
  ring.position.set(0, -500, 0); scene.add(ring);
  // Smoke puffs: a pool of soft grey sprites that billow up from a blast or a collapse.
  const puffTex = (() => {
    const c = document.createElement('canvas'); c.width = c.height = 64; const g = c.getContext('2d'), r = g.createRadialGradient(32, 32, 2, 32, 32, 31);
    r.addColorStop(0, 'rgba(255,255,255,0.9)'); r.addColorStop(0.5, 'rgba(255,255,255,0.45)'); r.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = r; g.fillRect(0, 0, 64, 64); return new THREE.CanvasTexture(c);
  })();
  const puffs = Array.from({ length: 48 }, () => {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: puffTex, color: 0x3a3532, transparent: true, opacity: 0, depthWrite: false }));
    sp.position.set(0, -500, 0); scene.add(sp); return { sp, life: 0, age: 0, vx: 0, vy: 0, vz: 0, s0: 1, s1: 1, o: 0 };
  });
  let puffI = 0;
  function billow(x, y, z, n, { color = 0x2e2a28, up = 1.5, size = 8, life = 20 } = {}) {
    for (let i = 0; i < n; i++) {
      const p = puffs[puffI++ % puffs.length];
      Object.assign(p, { life: life * rnd(0.6, 1), age: 0, vx: rnd(-0.8, 0.8), vy: up * rnd(0.5, 1.2), vz: rnd(-0.8, 0.8), s0: rnd(1.5, 3), s1: size * rnd(0.7, 1.3), o: rnd(0.55, 0.85) });
      p.sp.material.color.setHex(color); p.sp.position.set(x + rnd(-1, 1), y + rnd(0.3, 2), z + rnd(-1, 1));
    }
  }
  const burning = []; // { fire, smoke, x, z, t }
  const pending = []; // { t, run }
  let shellT = 20, collapseT = 60, looterT = 45, looter = null, flashK = 0;

  const level = () => engine.level;
  const intensity = () => { const G = ctx.G; return G && INTENSITY[G.chapter]?.(G) || null; };
  const active = () => ctx.roaming && engine.controlsEnabled && level()?.id === 'town';
  const later = (t, run) => pending.push({ t, run });
  const dist = (x, z) => Math.hypot(engine.player.x - x, engine.player.z - z);

  // Something solid between the blast and the body (sampled at 1 m height) takes most of it.
  function covered(x, z) {
    const P = engine.player, n = Math.ceil(dist(x, z) / 0.5);
    for (let i = 1; i < n; i++) {
      const k = i / n, sx = x + (P.x - x) * k, sz = z + (P.z - z) * k, sy = P.y + 1;
      if ((level().solids || []).some((s) => s && !s.off && sx > s.x0 && sx < s.x1 && sz > s.z0 && sz < s.z1 && sy > s.y0 && sy < s.y1)) return true;
    }
    return false;
  }
  function damage(x, z, radii, why) {
    const d = dist(x, z);
    const base = d < radii[0] ? 85 : d < radii[1] ? 40 : d < radii[2] ? 15 : 0;
    if (!base) return 0;
    const n = Math.round(base * (engine.crouching ? 0.4 : 1) * (covered(x, z) ? 0.3 : 1));
    hurt(n, why);
    return n;
  }

  function ignite(x, y, z, seconds) {
    const fx = engine.fx;
    try {
      const fire = fx.fire({ x, y, z, scale: 1.6, light: false }), smoke = fx.smoke({ x, y, z, ceiling: 22, spread: 7, radius: 0.9, count: 90, density: 1, color: 0x2a2725 });
      level().group.add(fire.group, smoke.group);
      burning.push({ fire, smoke, x, z, t: seconds, L: level() });
    } catch (err) { console.error('[danger] fire fx failed', err); }
  }

  // ---- shells ----
  function shell(I) {
    const P = engine.player, close = Math.random() < 0.18 * I.shell;
    // mostly in front of the camera, so it is seen; yaw 0 looks toward -z
    const a = (Math.random() < 0.75 ? P.yaw + rnd(-0.9, 0.9) : Math.random() * Math.PI * 2), r = close ? rnd(3, 8) : rnd(9, 32);
    const x = P.x - Math.sin(a) * r, z = P.z - Math.cos(a) * r;
    audio.hit('whistle', 2.3);
    ctx.toast(close ? '휘이이익— 바로 머리 위다! 엎드려! (C)' : '휘이익— 포탄 낙하음!', 'site', 2600);
    later(2.3, () => blast(x, z));
  }
  function blast(x, z) {
    const L = level(); if (!L || L.id !== 'town') return;
    const g = groundAt(L.walk, x, z, 0.5, true) ?? { y: 0 }, y = g.y;
    const d = dist(x, z);
    audio.boom(Math.min(1, Math.max(0.3, d / 40)));
    engine.shake(Math.max(0.04, 0.6 - d * 0.02), 1.2);
    flash.position.set(x, y + 2, z); flashK = 1; ball.position.set(x, y + 1.2, z); ring.position.set(x, y + 0.15, z);
    const c = new THREE.Mesh(craterGeo, craterMat); c.scale.setScalar(rnd(1.4, 2.2)); c.position.set(x, y + 0.03, z); L.group.add(c);
    for (let i = 0; i < 8; i++) { const a = Math.random() * 6.28, v = rnd(3, 8); actions.spawn(Math.random() < 0.5 ? 'brick' : 'rock', x, y + 0.4, z, Math.cos(a) * v, rnd(4, 9), Math.sin(a) * v); }
    billow(x, y, z, 14);
    if (Math.random() < 0.6) ignite(x, y, z, rnd(25, 50)); // often a fire that keeps burning
    const n = damage(x, z, [4, 9, 16], 'blast');
    if (n >= 30) ctx.toast('폭풍에 날아가 바닥을 굴렀다. 귀가 먹먹하다.', 'self', 4200);
    else if (n > 0) ctx.toast('파편이 스쳤다. 피가 흐른다.', 'self', 3600);
    ctx.stat('tension', 8);
    ctx.event('blast', { near: Math.round(d), dmg: n });
  }

  // ---- collapse: the nearest damaged facade sheds a rubble pile onto its sidewalk ----
  function collapse() {
    const P = engine.player;
    const b = BUILDINGS.filter((b) => b.floors >= 2).map((b) => {
      const x = Math.min(Math.max(P.x, b.x0 + 3), b.x1 - 3), z = Math.min(Math.max(P.z, b.z0 + 3), b.z1 - 3);
      return { b, x, z, d: Math.hypot(P.x - x, P.z - z) };
    }).filter((o) => o.d > 5 && o.d < 35).sort((a, c) => a.d - c.d)[0];
    if (!b) return;
    const { b: B } = b;
    // the face toward the player
    const faces = [['n', B.z0 - P.z], ['s', P.z - B.z1], ['w', B.x0 - P.x], ['e', P.x - B.x1]].sort((a, c) => c[1] - a[1]);
    const f = faces[0][0], along = f === 'n' || f === 's';
    let cx = along ? Math.min(Math.max(P.x, B.x0 + 3), B.x1 - 3) : f === 'w' ? B.x0 - 2 : B.x1 + 2;
    let cz = along ? (f === 'n' ? B.z0 - 2 : B.z1 + 2) : Math.min(Math.max(P.z, B.z0 + 3), B.z1 - 3);
    // never on a door or anything else you interact with: slide along the face, or give up
    const blocks = (x, z) => (level().points || []).some((q) => Math.hypot(q.x - x, q.z - z) < 6);
    for (const off of [0, 8, -8, 14, -14]) {
      const tx = along ? cx + off : cx, tz = along ? cz : cz + off;
      if (!blocks(tx, tz) && (along ? tx > B.x0 + 2 && tx < B.x1 - 2 : tz > B.z0 + 2 && tz < B.z1 - 2)) { cx = tx; cz = tz; break; }
      if (off === -14) return;
    }
    audio.hit('collapse');
    ctx.toast('우르르— 금 간 건물 벽에서 콘크리트가 떨어지기 시작한다! 벽에서 떨어져!', 'site', 3400);
    engine.shake(0.08, 2.5);
    later(3, () => {
      const L = level(); if (!L || L.id !== 'town') return;
      const W = along ? 6 : 4, D = along ? 4 : 6, h = 1.4;
      const n = 60, pile = new THREE.InstancedMesh(box, rubbleMat, n), m = new THREE.Object3D();
      for (let i = 0; i < n; i++) { // a heap: bigger, higher chunks toward the middle
        const u = rnd(-1, 1), v = rnd(-1, 1), k = 1 - Math.max(Math.abs(u), Math.abs(v)), s = rnd(0.4, 1.2);
        m.position.set(cx + (u * W) / 2, k * h * rnd(0.5, 1), cz + (v * D) / 2); m.rotation.set(rnd(0, 3), rnd(0, 3), rnd(0, 3));
        m.scale.set(s * rnd(0.8, 1.6), s * 0.55, s); m.updateMatrix(); pile.setMatrixAt(i, m.matrix);
      }
      pile.castShadow = pile.receiveShadow = true;
      L.group.add(pile);
      // ponytail: runtime-only rubble (not in the save); put it in levelStates if it should outlive a reload
      L.solids.push({ x0: cx - W / 2, x1: cx + W / 2, z0: cz - D / 2, z1: cz + D / 2, y0: -0.5, y1: h, id: 'rubble' });
      audio.boom(0.5); engine.shake(0.25, 1);
      billow(cx, 0, cz, 12, { color: 0xb3a998, up: 1.2, size: 9, life: 12 }); // the dust cloud
      const P2 = engine.player;
      if (Math.abs(P2.x - cx) < W / 2 + 0.4 && Math.abs(P2.z - cz) < D / 2 + 0.4) {
        hurt(engine.crouching ? 30 : 55, 'debris'); P2.teleport({ x: P2.x, z: cz + (P2.z > cz ? 1 : -1) * (D / 2 + 0.6), yaw: P2.yaw });
        ctx.toast('쏟아진 잔해에 깔릴 뻔했다. 온몸이 욱신거린다.', 'self', 4200);
      }
      ctx.event('collapse', { x: Math.round(cx), z: Math.round(cz) });
    });
  }

  // ---- a looter: walks up, asks; give, refuse (a fight) or run ----
  function spawnLooter() {
    const P = engine.player, L = level();
    const a = P.yaw + Math.PI + rnd(-0.8, 0.8), x = P.x - Math.sin(a) * 14, z = P.z - Math.cos(a) * 14;
    const g = groundAt(L.walk, x, z, 0.5, true); if (!g) return;
    const p = engine.people.createPerson({ preset: 'crowd', seed: 700 + Math.floor(Math.random() * 99) });
    p.group.position.set(x, g.y, z); L.group.add(p.group); p.hostile = true;
    looter = { p, L, hp: 4, state: 'approach', hitT: 0, talk: Math.floor(Math.random() * LOOTER_LINES.length), down: false };
    p.onHit = (fx, fz, ko) => { // the player hits back: actions.js calls this instead of its own reaction
      if (!looter || looter.p !== p || looter.down) return;
      looter.hp -= ko ? 2 : 1; looter.state = 'fight'; p.setAnim('hurt'); looter.hitT = 0.9;
      if (looter.hp <= 0) { looter.down = true; looter.state = 'down'; p.setAnim('down'); dropLoot(p); }
    };
    p.setAnim('walk');
  }
  function dropLoot(p) {
    const w = p.group.position;
    actions.spawn('can', w.x, w.y + 0.5, w.z, rnd(-1, 1), 2, rnd(-1, 1));
    const G = ctx.G; (G.items.canned ||= { home: 0, bag: 0, bought: 0, checked: false, used: 0 }).bag += 1;
    ctx.toast('상대가 쓰러졌다. 떨어뜨린 통조림 하나를 챙겼다.', 'self', 4200);
    ctx.event('looter', { result: 'fought' });
  }
  async function confront() {
    looter.state = 'talking';
    const G = ctx.G, have = FOOD.filter((id) => (G.items[id]?.bag ?? 0) > 0);
    looter.p.setAnim('talk'); looter.p.lookAt?.(engine.player.x, engine.player.z);
    const pick = await ctx.say('낯선 사람', LOOTER_LINES[looter.talk], [
      ...(have.length ? [{ id: 'give', label: '가방에서 먹을 것을 내준다' }] : []),
      { id: 'refuse', label: '거절한다' }, { id: 'run', label: '등을 돌려 뛴다' }], '눈빛이 심상치 않다');
    if (!looter) return;
    if (pick === 'give') {
      for (const id of have.slice(0, 2)) G.items[id].bag -= 1;
      ctx.toast('먹을 것을 건넸다. 그는 말없이 골목으로 사라졌다.', 'self', 4200);
      ctx.event('looter', { result: 'gave' }); G.karma = (G.karma ?? 0) + 1;
      looter.state = 'leave';
    } else { looter.state = pick === 'run' ? 'chase' : 'fight'; if (pick === 'refuse') ctx.toast('“좋게 말할 때 내놨어야지.” 그가 주먹을 쥔다. (클릭/Q로 맞서기, 멀리 뛰면 포기한다)', 'site', 5000); }
  }
  function looterTick(dt) {
    const { p, L } = looter, P = engine.player, w = p.group.position;
    if (level() !== L) return void dropLooter();
    const dx = P.x - w.x, dz = P.z - w.z, d = Math.hypot(dx, dz);
    const step = (v, away = false) => {
      const k = (away ? -1 : 1) * v * dt / (d || 1); w.x += dx * k; w.z += dz * k;
      const g = groundAt(L.walk, w.x, w.z, w.y + 0.3, false, 2); if (g) w.y = g.y;
      p.group.rotation.y = Math.atan2(away ? dx : -dx, away ? dz : -dz); p.setSpeed?.(v);
    };
    looter.hitT = Math.max(0, looter.hitT - dt);
    if (looter.state === 'down') { if ((looter.gone = (looter.gone ?? 15) - dt) <= 0) dropLooter(); return; }
    if (looter.state === 'leave') { if (p.anim !== 'walk') p.setAnim('walk'); step(1.4, true); if (d > 30) dropLooter(); return; }
    if (looter.state === 'approach') { if (d < 2.6) confront(); else { if (p.anim !== 'walk') p.setAnim('walk'); step(1.6); } return; }
    if (looter.state === 'fight' || looter.state === 'chase') {
      if (d > 22) { ctx.toast('뒤쫓던 사람이 포기하고 멈춰 섰다.', 'self'); looter.state = 'leave'; ctx.event('looter', { result: 'escaped' }); return; }
      if (looter.hitT > 0) return;
      if (d > 1.1) { if (p.anim !== 'run') p.setAnim('run'); step(looter.state === 'chase' ? 4.4 : 3.4); return; }
      p.setAnim('punch'); p.lookAt?.(P.x, P.z); looter.hitT = 1.3; audio.hit('punch'); engine.act('hurt', 0.35); engine.shake(0.1, 0.3);
      hurt(Math.round(rnd(7, 12)), 'fight');
    }
  }
  function dropLooter() { looter?.p.group.removeFromParent(); looter = null; }

  engine.onFrame((dt) => {
    // effects keep running (fires burn down) even while a menu is open
    if (flashK > 0) { flashK = Math.max(0, flashK - dt * 2); flash.intensity = 900 * flashK * flashK; ball.scale.setScalar(2 + (1 - flashK) * 7); ball.material.opacity = Math.min(1, flashK * flashK * 1.6); ring.scale.setScalar(1 + (1 - flashK) * 16); ring.material.opacity = 0.55 * flashK; }
    for (const p of puffs) {
      if (p.age >= p.life) continue;
      p.age += dt; const k = p.age / p.life, w = p.sp.position;
      w.x += p.vx * dt; w.y += p.vy * dt * (1 - k * 0.7); w.z += p.vz * dt;
      p.sp.scale.setScalar(p.s0 + (p.s1 - p.s0) * Math.sqrt(k)); p.sp.material.opacity = p.o * Math.min(1, p.age * 3) * (1 - k);
      if (p.age >= p.life) { p.sp.material.opacity = 0; w.y = -500; }
    }
    for (let i = burning.length - 1; i >= 0; i--) {
      const b = burning[i]; b.t -= dt;
      try { b.fire.setLevel(Math.min(1, b.t / 10)); b.smoke.setDensity(Math.min(1, Math.max(0, b.t + 12) / 15)); b.fire.update(dt, engine.camera); b.smoke.update(dt, engine.camera); } catch { /* fx stand-in */ }
      if (b.t <= -12) { b.fire.dispose?.(); b.smoke.dispose?.(); burning.splice(i, 1); continue; }
      if (b.t > 3 && b.L === level() && active() && dist(b.x, b.z) < 1.3) { b.burnT = (b.burnT ?? 0) - dt; if (b.burnT <= 0) { b.burnT = 0.5; hurt(4, 'fire'); ctx.toast('뜨겁다! 불길에서 떨어져!', 'self', 1500); } }
    }
    if (!active()) return;
    for (let i = pending.length - 1; i >= 0; i--) if ((pending[i].t -= dt) <= 0) pending.splice(i, 1)[0].run();
    if (looter) looterTick(dt);
    const I = intensity(); if (!I) return;
    if (I.shell && (shellT -= dt * I.shell) <= 0) { shellT = rnd(25, 60); shell(I); }
    if (I.collapse && (collapseT -= dt * I.collapse) <= 0) { collapseT = rnd(70, 140); collapse(); }
    if (I.looter && !looter && (looterT -= dt * I.looter) <= 0) { looterT = rnd(90, 180); spawnLooter(); }
  });

  return {
    // engine/actions.js hit someone: the looter's own onHit handles it; tells the app not to count it as violence.
    isHostile: (p) => !!p?.hostile,
    // force an event now (dev panel / tests)
    trigger(kind, at) { const I = intensity() ?? { shell: 1 }; if (kind === 'blast') blast(at.x, at.z); else if (kind === 'shell') shell(I); else if (kind === 'collapse') collapse(); else if (kind === 'looter' && !looter) spawnLooter(); },
    reset() { dropLooter(); pending.length = 0; shellT = 20; collapseT = 60; looterT = 45; },
  };
}
