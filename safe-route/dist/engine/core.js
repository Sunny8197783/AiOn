// Engine core: renderer, scene, camera, shared kit, post chain, sky + environment presets, lazy levels,
// the player rig and the frame loop. People (engine/people.js, engine/crowd.js) and fx (engine/fx.js) are
// imported dynamically; when one is missing the engine keeps running with simple stand-ins.
import * as THREE from 'three';
import { createKit } from './kit.js';
import { createSky } from './sky.js';
import { createPost } from './post.js';
import { createControls } from './controls.js';
import { PRESETS, envState, gradeFor, mixState } from './presets.js';

const QUALITIES = ['low', 'medium', 'high'];
const PIXEL_RATIO = { high: () => Math.min(globalThis.devicePixelRatio || 1, 2), medium: () => Math.min(globalThis.devicePixelRatio || 1, 1.25), low: () => 1 };
const ease = (k) => k * k * (3 - 2 * k);
const clamp = (v, a, b) => Math.min(b, Math.max(a, v));

// Stand-ins used when people.js / crowd.js / fx.js fail to load.
const PERSON_COLORS = { jeongmin: 0x2b3a55, seoyeon: 0xa98457, manager: 0x2a3550, clerk: 0x3d7a4a, guide: 0xd6e33a, coworker: 0x55606e, elder: 0x7a6a5a };
function capsulePerson({ preset = 'crowd' } = {}) {
  const group = new THREE.Group(); group.name = `person-${preset}`;
  const body = new THREE.Mesh(new THREE.CapsuleGeometry(0.24, 1.24, 6, 14), new THREE.MeshStandardMaterial({ color: PERSON_COLORS[preset] ?? 0x6b6f78, roughness: 0.92 }));
  body.position.y = 0.86; body.castShadow = body.receiveShadow = true;
  group.add(body);
  return { group, anim: 'idle', height: 1.72, setAnim(n) { this.anim = n; }, setSpeed() {}, setProp() {}, lookAt() {}, update() {} };
}
const emptyCrowd = () => ({ group: new THREE.Group(), update() {}, setMode() {}, setPaths() {}, setArea() {} });
function stubFx() {
  const fxObj = () => ({ group: new THREE.Group(), setLevel() {}, setDensity() {}, setOn() {}, aim() {}, update() {}, dispose() {} });
  return { stub: true, fire: fxObj, smoke: fxObj, rain: fxObj, dust: fxObj, extinguisherSpray: fxObj, setReduceMotion() {} };
}

function normalizeLevel(L, id) {
  if (!L || !L.group?.isObject3D) throw new Error(`[engine] level "${id}" did not return a Level with a THREE.Group "group"`);
  L.id ??= id; L.walk ??= []; L.solids ??= []; L.points ??= []; L.spawns ??= {}; L.npcs ??= {}; L.states ??= {}; L.anchors ??= {};
  if (typeof L.setState !== 'function') L.setState = (k) => console.warn(`[engine] level "${id}" has no setState (key ${k})`);
  if (!L.walk.length) console.warn(`[engine] level "${id}" has no walk zones; the player keeps the spawn height`);
  if (!Object.keys(L.spawns).length) console.warn(`[engine] level "${id}" has no spawns`);
  return L;
}

export function createEngine({ canvas, quality = 'high', reduceMotion = false, levels = {}, onQualityChange = null } = {}) {
  if (!canvas) throw new Error('[engine] createEngine needs a canvas');
  if (!QUALITIES.includes(quality)) { console.warn(`[engine] unknown quality "${quality}", using high`); quality = 'high'; }
  let RM = !!reduceMotion;

  const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, powerPreference: 'high-performance' });
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.localClippingEnabled = true; // the first-person body cuts its own head (engine/controls.js)
  renderer.setPixelRatio(PIXEL_RATIO[quality]());
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(70, 1, 0.08, 3000);
  const kit = createKit(renderer, { quality });
  const sky = createSky({ renderer, scene, kit, quality });
  const post = createPost({ renderer, scene, camera, quality, reduceMotion: RM });
  let level = null;
  const controls = createControls({ camera, canvas, scene, getLevel: () => level, reduceMotion: RM, quality });

  // ---- people + fx (dynamic, optional) ----
  const people = { createPerson: capsulePerson, createCrowd: emptyCrowd, fallback: true }, persons = new Set();
  const peopleReady = Promise.allSettled([import('./people.js'), import('./crowd.js')]).then(([p, c]) => {
    // Every person made from here on is known to engine/actions.js (punches, thrown things); the player models too,
    // which actions skips.
    if (p.status === 'fulfilled' && typeof p.value.createPerson === 'function') {
      Object.assign(people, p.value); people.fallback = false;
      const make = people.createPerson; people.createPerson = (o) => { const m = make(o); if (m?.group) persons.add(m); return m; };
    }
    else console.error('[engine] engine/people.js unavailable; using capsule people', p.reason ?? 'no createPerson export');
    if (c.status === 'fulfilled' && typeof c.value.createCrowd === 'function') people.createCrowd = c.value.createCrowd;
    else console.error('[engine] engine/crowd.js unavailable; crowds stay empty', c.reason ?? 'no createCrowd export');
    controls.setModel(playerModel('jeongmin'));
  }).catch((err) => console.error('[engine] people setup failed', err)); // never blocks level loading
  let fx = stubFx();
  const fxReady = import('./fx.js').then((m) => { fx = m.createFx(THREE, kit, { reduceMotion: RM }); })
    .catch((err) => console.error('[engine] engine/fx.js unavailable; fire, smoke and rain are disabled', err));
  const ready = Promise.all([peopleReady, fxReady]);
  // One cached model per preset; the 서연 interlude swaps the model the same controller drives.
  const models = {};
  function playerModel(preset) {
    if (models[preset]) return models[preset];
    let m;
    try { m = people.createPerson({ preset, seed: 1 }); if (!m?.group) throw new Error('createPerson returned no group'); }
    catch (err) { console.error(`[engine] player model "${preset}" failed; using a capsule`, err); m = capsulePerson({ preset }); }
    return (models[preset] = m);
  }
  const setPlayerPreset = (preset) => ready.then(() => controls.setModel(playerModel(preset)));

  // ---- environment presets (tweened) ----
  let envName = null, envFrom = null, envTo = null, envK = 1, envDur = 0, rainFx = null, beam = 1;
  let litFrom = {}, litTo = {}, power = true;
  const litNow = {};
  // Grid power off (blackout): street lamps, lit windows, shop fronts and signs stay dark whatever the sky
  // preset says; battery-backed 'emergency' and 'indoor' (interiors pick their own preset) are untouched.
  const GRID = new Set(['street', 'windows', 'shops', 'signs']);
  const litTarget = (preset) => Object.fromEntries(Object.entries(PRESETS[preset].lit).map(([g, v]) => [g, !power && GRID.has(g) ? 0 : v]));
  function stepEnv(dt) {
    envK = envDur > 0 ? Math.min(1, envK + dt / envDur) : 1;
    const e = ease(envK), S = mixState(envFrom, envTo, e);
    sky.apply(S);
    post.setBloom(S.bloom, S.bloomRadius, S.bloomThreshold);
    beam = clamp(1.25 - S.keyI * 0.45 - S.hemiI * 1.2, 0.08, 1);
    controls.setBeamStrength(beam);
    applyWet(S.wet);
    for (const g of Object.keys(litTo)) {
      const a = litFrom[g] ?? (kit.isLit(g) ? 1 : 0), v = a + (litTo[g] - a) * e;
      litNow[g] = v; kit.setLit(g, v > 0.0005, v);
    }
  }
  function setEnv(preset, { rain = false, seconds = 1.5 } = {}) {
    if (!PRESETS[preset]) { console.error(`[engine] unknown env preset "${preset}"; known: ${Object.keys(PRESETS).join(', ')}`); return; }
    const outdoorRain = !!rain && PRESETS[preset].outdoor;
    envTo = envState(THREE, preset, rain);
    envFrom = sky.state ? { ...sky.state } : envTo;
    envDur = Math.max(0, Number(seconds) || 0); envK = 0;
    litFrom = { ...litNow }; litTo = { ...litNow, ...litTarget(preset) };
    envName = preset;
    try { scene.environment = sky.environment(preset, rain); } catch (err) { console.error('[engine] PMREM environment failed', err); }
    post.setGrade(gradeFor(preset, rain), envDur);
    if (outdoorRain && !rainFx) { try { rainFx = fx.rain(scene, camera); } catch (err) { console.error('[engine] rain fx failed', err); } }
    rainFx?.setOn(outdoorRain);
    if (!envDur) stepEnv(0);
  }
  const reapplyLit = () => { for (const [g, v] of Object.entries(litNow)) kit.setLit(g, v > 0.0005, v); };

  // Wet look while it rains: asphalt / sidewalk / concrete surface materials (matched by their shared kit
  // texture) and any material with userData.wet = true get lower roughness, a darker albedo and more
  // reflection. The dry values are kept in material.userData.dry and restored when the rain stops.
  let wetList = null, wetNow = 0;
  function collectWet(root) {
    const srcs = new Set(['asphalt', 'sidewalk', 'concrete'].map((n) => kit.surface(n).map?.source));
    const out = new Set();
    root.traverse((o) => { for (const m of [].concat(o.material || [])) if (m?.isMeshStandardMaterial && (m.userData.wet || srcs.has(m.map?.source))) out.add(m); });
    for (const m of out) m.userData.dry ??= { roughness: m.roughness, color: m.color.clone(), env: m.envMapIntensity };
    return [...out];
  }
  function setWet(list, w) {
    for (const m of list) {
      const d = m.userData.dry;
      m.roughness = Math.max(0.08, d.roughness * (1 - 0.65 * w)); m.color.copy(d.color).multiplyScalar(1 - 0.28 * w); m.envMapIntensity = d.env * (1 + 0.8 * w);
    }
  }
  function applyWet(w) {
    let from = wetNow;
    if (w > 0.001 && !wetList && level) { wetList = collectWet(level.group); from = 0; } // freshly collected = dry
    if (wetList && w !== from) setWet(wetList, w);
    wetNow = w;
  }

  // ---- levels ----
  const built = new Map(), done = new Set();
  function getLevel(id) {
    if (!built.has(id)) {
      const make = levels[id];
      if (typeof make !== 'function') return Promise.reject(new Error(`[engine] unknown level "${id}"; known: ${Object.keys(levels).join(', ')}`));
      const p = (async () => { await ready; return normalizeLevel(await make(kit, people, fx), id); })();
      built.set(id, p);
      p.then(() => done.add(id), () => built.delete(id)); // a failed build may be retried
    }
    return built.get(id);
  }
  let queue = Promise.resolve(), busy = false;
  // Fade out, swap level.group into the scene, place the player, run prepare(level) (level states, setEnv:
  // the level is already current), precompile shaders for that final setup, fade in.
  function enter(levelId, spawn, { prepare } = {}) {
    const run = queue.catch(() => {}).then(async () => {
      busy = true;
      const fadeS = RM ? 0 : done.has(levelId) ? 0.1 : 0.35; // a level already built swaps in a blink
      try {
        await post.fadeTo(0, fadeS);
        let L;
        try { L = await getLevel(levelId); }
        catch (err) { console.error(`[engine] level "${levelId}" failed to load`, err); throw err; }
        if (L !== level) {
          if (wetList) { setWet(wetList, 0); wetList = null; }
          if (level) scene.remove(level.group);
          scene.add(L.group); level = L; reapplyLit(); applyWet(wetNow);
        }
        const names = Object.keys(L.spawns);
        const want = spawn ?? (L.spawns.entry ? 'entry' : names[0]);
        if (!controls.player.teleport(want) && names.length) controls.player.teleport(names[0]);
        controls.snap(); controls.update(0);
        if (prepare) { try { await prepare(L); } catch (err) { console.error(`[engine] prepare for level "${levelId}" failed (continuing)`, err); } }
        try { await Promise.race([renderer.compileAsync(scene, camera), new Promise((r) => setTimeout(r, 4000))]); }
        catch (err) { console.warn('[engine] shader precompile failed (continuing)', err); }
        prewarm();
        return L;
      } finally {
        await post.fadeTo(1, fadeS);
        busy = false;
      }
    });
    queue = run;
    return run;
  }

  // Build and precompile the other levels while the player is busy elsewhere, so a door never waits on a
  // build. Idle callbacks keep each build between frames; one level at a time.
  let warming = false; const tried = new Set();
  function prewarm() {
    if (warming) return; warming = true;
    const idle = (fn) => (globalThis.requestIdleCallback ? requestIdleCallback(fn, { timeout: 8000 }) : setTimeout(fn, 1500));
    const next = () => {
      const id = Object.keys(levels).find((k) => !built.has(k) && !tried.has(k));
      if (!id) return;
      tried.add(id);
      idle(async () => {
        try { const L = await getLevel(id); await renderer.compileAsync(L.group, camera, scene); }
        catch (err) { console.warn(`[engine] prewarm of "${id}" failed (it loads at the door instead)`, err); }
        setTimeout(next, 800);
      });
    };
    setTimeout(next, 4000);
  }

  // ---- quality, motion, size ----
  function resize() {
    const w = canvas.clientWidth || innerWidth, h = canvas.clientHeight || innerHeight;
    if (!w || !h) return;
    renderer.setSize(w, h, false);
    camera.aspect = w / h; camera.updateProjectionMatrix();
    post.setSize(w, h);
  }
  function setQuality(q) {
    if (!QUALITIES.includes(q)) { console.error(`[engine] unknown quality "${q}"`); return; }
    quality = q;
    renderer.setPixelRatio(PIXEL_RATIO[q]());
    sky.setQuality(q); post.setQuality(q); controls.setQuality(q);
    resize();
  }
  function setReduceMotion(v) { RM = !!v; post.setReduceMotion(RM); controls.setReduceMotion(RM); fx.setReduceMotion?.(RM); }
  if (typeof ResizeObserver === 'function') new ResizeObserver(resize).observe(canvas); else addEventListener('resize', resize);
  resize();

  // ---- frame loop ----
  const frameFns = new Set(), logged = new Set();
  const logOnce = (key, err) => { if (!logged.has(key)) { logged.add(key); console.error(`[engine] ${key}`, err); } };
  let timeScale = 1; // slow motion (cutscenes); 1 = real time
  let t = 0, last = performance.now(), fps = 60, acc = 0, frames = 0, slow = 0, downgraded = false;
  function measure(raw) {
    if (raw > 0.5) { acc = 0; frames = 0; return; } // tab was hidden
    acc += raw; frames++;
    if (acc < 0.5) return;
    fps = frames / acc; acc = 0; frames = 0;
    if (downgraded || busy || quality === 'low' || t < 3) { slow = 0; return; }
    slow = fps < 30 ? slow + 0.5 : 0;
    if (slow >= 4) {
      downgraded = true;
      const q = quality === 'high' ? 'medium' : 'low';
      console.warn(`[engine] fps ${fps.toFixed(1)} < 30 for 4 s; quality ${quality} -> ${q}`);
      setQuality(q);
      try { onQualityChange?.(q); } catch (err) { console.error('[engine] onQualityChange failed', err); }
    }
  }
  function frame(now) {
    const raw = Math.max(0, (now - last) / 1000); last = now;
    const dt = Math.min(raw, 0.1) * timeScale; t += dt;
    try { controls.update(dt); } catch (err) { logOnce('player update failed', err); }
    if (envK < 1) stepEnv(dt);
    if (level?.update) { try { level.update(dt, t, { camera, player: controls.player, env: envName, power }); } catch (err) { logOnce(`level "${level.id}" update failed`, err); } }
    sky.update(dt, camera, controls.player.position, RM);
    if (rainFx) { try { rainFx.update(dt, camera); } catch (err) { logOnce('rain update failed', err); } }
    for (const fn of [...frameFns]) { try { fn(dt, t); } catch (err) { logOnce('onFrame listener failed', err); } }
    post.render(dt);
    measure(raw);
  }
  setEnv('day', { seconds: 0 });
  renderer.setAnimationLoop(frame);

  return {
    THREE, renderer, scene, camera, kit, people, persons, ready, post,
    get fx() { return fx; },
    get quality() { return quality; },
    get level() { return level; },
    get player() { return controls.player; },
    get view() { return controls.view; },
    get nearest() { return controls.nearest; },
    get controlsEnabled() { return controls.enabled; },
    get env() { return envName; },
    get reduceMotion() { return RM; },
    enter, setEnv, setPlayerPreset,
    get power() { return power; },
    // Grid power for the current scene; re-applies the lit groups of the current preset at once.
    setPower(on) {
      power = !!on;
      if (!envName) return;
      litTo = { ...litTo, ...litTarget(envName) };
      if (envK >= 1) { for (const [g, v] of Object.entries(litTo)) { litNow[g] = v; kit.setLit(g, v > 0.0005, v); } }
    },
    setProfile: (p) => controls.setProfile(p),
    shake: (a, s) => controls.shake(a, s),
    setSensitivity: (v) => controls.setSensitivity(v),
    onBlocked: (fn) => controls.onBlocked(fn),
    onLand: (fn) => controls.onLand(fn),
    act: (name, s) => controls.act(name, s),
    setHold: (on) => controls.setHold(on),
    setModelHidden: (v) => controls.setModelHidden(v),
    get crouching() { return controls.crouching; },
    get pointerLocked() { return controls.locked; },
    setGrade: (grade, seconds = 1) => post.setGrade(grade, seconds),
    setView: (v) => controls.setView(v),
    setControls: (on) => controls.setEnabled(on),
    setCameraOverride: (fn) => controls.setOverride(fn),
    onInteract: (fn) => controls.onInteract(fn),
    onNearest: (fn) => controls.onNearest(fn),
    onKey: (fn) => controls.onKey(fn),
    onFrame: (fn) => { frameFns.add(fn); return () => frameFns.delete(fn); },
    setQuality, setReduceMotion,
    get timeScale() { return timeScale; }, set timeScale(v) { timeScale = Math.min(2, Math.max(0, Number(v) || 0)); },
    stats: () => ({ fps: Math.round(fps), drawCalls: post.sceneStats.calls, triangles: post.sceneStats.triangles, adapt: +post.adaptValue().toFixed(2) }),
    dispose() { renderer.setAnimationLoop(null); controls.dispose(); renderer.dispose(); },
  };
}
