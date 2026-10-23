// Cutscenes: letterboxed camera moves over the live world, with people walking through them, timed events
// (explosions, lights going out, sounds) and captions. Space / Enter / Esc skips the rest.
// createCine(ctx) -> { play(shots, { title }), actor(preset, opts), missile(from, to, seconds), title(lines) }
// A shot: { dur, cam, lines, at, env, slow, fade, setup, exposure }
//   cam: { p: [[x,y,z], ...], t: [[x,y,z], ...], fov: [a, b], ease: 'inout' | 'linear' | 'out', hand: metres }
//        positions / look-at targets eased along a Catmull-Rom curve over the shot; hand = handheld sway.
//        Or a function (k, s) -> { p: [x,y,z], t: [x,y,z], fov } for anything else (s = seconds into the shot).
//   lines: [[at, text, speaker?, ms?]] captions; at: [[seconds, fn]] events; env: [preset, seconds] at the start;
//   slow: world time scale for the shot; fade: fade in from black; setup(): returns an optional cleanup;
//   exposure: stops added to the grade for the shot (a night scene the eye could still read);
//   fill: { at: [x,y,z] | () => [x,y,z], i, color } a soft light that stays on the subject.
import * as THREE from 'three';
import { groundAt } from '../engine/geom.js';

const SKIP = new Set(['Space', 'Enter', 'NumpadEnter', 'Escape']);
const ease = { inout: (k) => k * k * (3 - 2 * k), linear: (k) => k, out: (k) => 1 - (1 - k) ** 2, in: (k) => k * k };
const V = (a) => new THREE.Vector3(...a);

function overlay() {
  const el = document.createElement('div'); el.className = 'cine'; el.hidden = true;
  el.innerHTML = '<div class="cine-bar top"></div><div class="cine-bar bottom"></div><div class="cine-title"></div><div class="cine-skip">Space · 건너뛰기</div>';
  document.body.append(el);
  return { el, title: el.querySelector('.cine-title') };
}

export function createCine(ctx) {
  const { engine, hud } = ctx;
  const ui = overlay(), live = new Set(); // actors and effects to remove when the cutscene ends
  // A fill light for dark shots (shot.fill), in the scene from the start so it never forces a shader recompile.
  const fill = new THREE.PointLight(0xdfe6ff, 0, 14, 1.6); fill.position.set(0, -500, 0); engine.scene.add(fill);
  let busy = false, baseExp = 0;

  // Typed-out title lines in the corner (place, time), the way a film opens on a location.
  let titleT = 0;
  function title(lines) {
    clearTimeout(titleT); ui.title.classList.remove('out');
    ui.title.replaceChildren(); if (!lines?.length) return;
    titleT = setTimeout(() => ui.title.classList.add('out'), 6500); // read, then out of the way
    lines.forEach((text, i) => {
      const p = document.createElement('p'); p.className = i ? 'sub' : ''; ui.title.append(p);
      [...text].forEach((ch, j) => setTimeout(() => { p.textContent += ch; }, 400 + i * 700 + j * 45));
    });
  }

  function pose(cam, k, s) {
    if (typeof cam === 'function') return cam(k, s);
    const e = (ease[cam.ease] ?? ease.inout)(k);
    const at = (pts) => (pts.length === 1 ? V(pts[0]) : pts.length === 2 ? V(pts[0]).lerp(V(pts[1]), e) : new THREE.CatmullRomCurve3(pts.map(V)).getPoint(e));
    return { p: at(cam.p).toArray(), t: at(cam.t).toArray(), fov: cam.fov ? cam.fov[0] + (cam.fov[1] - cam.fov[0]) * e : null };
  }

  function runShot(shot, skipped) {
    return new Promise((resolve) => {
      const cleanup = shot.setup?.();
      if (shot.env) engine.setEnv(shot.env[0], { seconds: shot.env[1] ?? 0 });
      engine.timeScale = shot.slow ?? 1;
      engine.post.setGrade({ exposure: baseExp + (shot.exposure ?? 0) }, shot.exposure ? 0.4 : 0.2);
      if (shot.fade) { engine.post.fadeTo(0, 0); engine.post.fadeTo(1, 0.6); }
      const events = [...(shot.at ?? [])];
      for (const [at, text, speaker, ms] of shot.lines ?? []) events.push([at, () => ctx.caption(text, speaker, ms)]);
      events.sort((a, b) => a[0] - b[0]);
      const look = new THREE.Vector3(), hand = shot.cam?.hand ?? 0.04, RM = engine.reduceMotion;
      let s = 0, done = false;
      const t0 = performance.now();
      const finish = () => { if (done) return; done = true; off(); fill.intensity = 0; try { cleanup?.(); } catch (err) { console.error('[cine] shot cleanup failed', err); } resolve(); };
      engine.setCameraOverride((camera) => {
        s = (performance.now() - t0) / 1000; // real seconds, whatever the world's time scale or frame rate
        while (events.length && events[0][0] <= s) { try { events.shift()[1](); } catch (err) { console.error('[cine] event failed', err); } }
        const P = pose(shot.cam, Math.min(1, s / shot.dur), s);
        camera.position.set(...P.p);
        look.set(...P.t);
        if (!RM && hand) look.add({ x: hand * (Math.sin(s * 1.3) + 0.5 * Math.sin(s * 3.1 + 1)), y: hand * 0.7 * Math.sin(s * 1.7 + 2), z: hand * Math.sin(s * 0.9 + 4) });
        camera.lookAt(look);
        if (P.fov && Math.abs(camera.fov - P.fov) > 0.01) { camera.fov = P.fov; camera.updateProjectionMatrix(); }
        if (shot.fill) { const f = shot.fill, at = typeof f.at === 'function' ? f.at() : f.at; if (at) fill.position.set(...at); fill.intensity = f.i ?? 6; if (f.color) fill.color.set(f.color); }
        if (s >= shot.dur || skipped()) finish();
      });
      const off = engine.onFrame(() => { // ends a shot even when the camera override is not running
        if (skipped()) finish();
        else if ((performance.now() - t0) / 1000 > shot.dur + 2) { console.warn(`[cine] shot overran (camera override lost?) after ${s.toFixed(2)} of ${shot.dur} s`); finish(); }
      });
    });
  }

  // Play shots in order; resolves 'done' or 'skipped'. Controls are off and the HUD is hidden meanwhile.
  async function play(shots, { title: lines } = {}) {
    if (busy) return 'busy';
    busy = true;
    let skipped = false;
    const onKey = (e) => { if (SKIP.has(e.code) && !e.repeat) { e.preventDefault(); e.stopPropagation(); skipped = true; } };
    addEventListener('keydown', onKey, true);
    engine.setControls(false); hud.setCinematic(true); ui.el.hidden = false; void ui.el.offsetWidth; ui.el.classList.add('on');
    engine.setModelHidden(true);
    baseExp = engine.post.grade.exposure;
    title(lines);
    try { for (const shot of shots) { if (skipped) break; await runShot(shot, () => skipped); } }
    finally {
      removeEventListener('keydown', onKey, true);
      engine.setCameraOverride(null); engine.timeScale = 1; engine.post.fadeTo(1, 0); engine.post.setGrade({ exposure: baseExp }, 0.6);
      for (const o of live) o.dispose(); live.clear();
      ui.el.classList.remove('on'); setTimeout(() => { if (!busy) ui.el.hidden = true; }, 700);
      title(null); hud.setCinematic(false); engine.setModelHidden(false); engine.setControls(true);
      busy = false;
    }
    return skipped ? 'skipped' : 'done';
  }

  // Someone in the shot: stands at `at` or walks `path` ([[x, z], ...]) at `speed`, in the current level.
  let seed = 900;
  function actor(preset, { at, path, speed = 1.3, anim = 'idle', yaw = 0, delay = 0, props = [] } = {}) {
    const L = engine.level, p = engine.people.createPerson({ preset, seed: seed++ }); // crowd extras each look different
    engine.persons.delete(p); // a cutscene extra, not someone to punch
    for (const n of props) p.setProp?.(n, true);
    const y = (x, z) => groundAt(L.walk, x, z, 0.5, true)?.y ?? 0;
    const start = path ? path[0] : at;
    p.group.position.set(start[0], y(start[0], start[1]), start[1]); p.group.rotation.y = yaw;
    p.setAnim(anim); L.group.add(p.group);
    let i = 1, wait = delay;
    const off = engine.onFrame((dt) => {
      if (path && i < path.length && (wait -= dt) <= 0) {
        const g = p.group.position, [tx, tz] = path[i], dx = tx - g.x, dz = tz - g.z, d = Math.hypot(dx, dz);
        if (d < 0.2) { if (++i >= path.length) { p.setAnim('idle'); p.setSpeed?.(0); } }
        else {
          const st = Math.min(d, speed * dt); g.x += (dx / d) * st; g.z += (dz / d) * st; g.y = y(g.x, g.z);
          p.group.rotation.y += Math.atan2(Math.sin(Math.atan2(-dx, -dz) - p.group.rotation.y), Math.cos(Math.atan2(-dx, -dz) - p.group.rotation.y)) * Math.min(1, dt * 8);
          if (p.anim !== 'walk') p.setAnim('walk'); p.setSpeed?.(speed);
        }
      }
      p.update(dt);
    });
    const o = { person: p, dispose() { off(); p.dispose?.(); p.group.removeFromParent(); } };
    live.add(o);
    return p;
  }

  // A missile coming down: a white-hot head with a long smoke-and-fire trail, from `from` to `to` in `seconds`.
  function missile(from, to, seconds = 1) {
    const A = V(from), Bv = V(to), dir = Bv.clone().sub(A).normalize(), L = engine.level;
    const g = new THREE.Group();
    const trail = new THREE.Mesh(new THREE.CylinderGeometry(0.25, 0.9, 40, 8, 1, true).translate(0, -20, 0).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: new THREE.Color(2.2, 1.4, 0.8), transparent: true, opacity: 0.55, blending: THREE.AdditiveBlending, depthWrite: false, fog: false }));
    g.add(trail);
    const head = new THREE.Mesh(new THREE.SphereGeometry(0.6, 10, 8), new THREE.MeshBasicMaterial({ color: new THREE.Color(8, 6, 4), fog: false }));
    g.add(head);
    g.position.copy(A); g.lookAt(A.clone().sub(dir)); L.group.add(g); // +z (the trail) points back up the flight path
    let t = 0;
    const off = engine.onFrame((dt) => { t += dt; const k = Math.min(1, t / seconds); g.position.lerpVectors(A, Bv, k * k); g.visible = k < 1; });
    const o = { dispose() { off(); g.removeFromParent(); trail.geometry.dispose(); head.geometry.dispose(); } };
    live.add(o);
    return o;
  }

  return { play, actor, missile, title, get busy() { return busy; } };
}
