// Engine core: renderer, scene, camera, shared kit, post chain, sky + environment presets, lazy levels,
// the player rig and the frame loop. People (engine/people.js, engine/crowd.js) and fx (engine/fx.js) are
// imported dynamically; when one is missing the engine keeps running with simple stand-ins.
import * as THREE from 'three';
import { createKit } from './kit.js';
import { createSky } from './sky.js';
import { createPost } from './post.js';
import { createControls } from './controls.js';
import { PRESETS, envState, gradeFor, mixState } from './presets.js';
import { normQ, rankQ, lowerQ, profileOf, pixelRatioFor, setLive, autoTier, gpuInfo, createGovernor, heldAdapt } from './quality.js';
import { applyTier, disposeLevel } from './tier-apply.js';

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

// quality: a tier id (engine/quality.js TIERS; 'low' | 'medium' | 'high' as before) or 'auto' (detect one from the GPU,
// cores, memory and screen). onQualityChange(tier, { auto: true, from }) fires when the frame-rate governor steps down.
// onWait(true | false): a level swap has been waiting on its shaders behind the black screen for over a second, or is done.
export function createEngine({ canvas, quality = 'auto', reduceMotion = false, levels = {}, onQualityChange = null, onWait = null } = {}) {
  if (!canvas) throw new Error('[engine] createEngine needs a canvas');
  let RM = !!reduceMotion;

  const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, powerPreference: 'high-performance' });
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFSoftShadowMap; // fixed: a type change recompiles everything
  renderer.localClippingEnabled = true; // the first-person body cuts its own head (engine/controls.js)
  const asked = normQ(quality, null);
  if (!asked && quality !== 'auto') console.warn(`[engine] unknown quality "${quality}"; detecting one`);
  quality = asked ?? autoTier(gpuInfo(renderer));
  const detected = !asked;
  setLive(quality);
  const pixelRatio = () => pixelRatioFor(quality, globalThis.devicePixelRatio || 1);
  const gov = createGovernor();
  renderer.setPixelRatio(pixelRatio());
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
  const fxReady = import('./fx.js').then((m) => { fx = m.createFx(THREE, kit, { reduceMotion: RM, renderer }); })
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
  // The tiers without a sun shadow dim the key light indoors (it would light every room through the ceilings), eased
  // with the preset blend; the tiers without eye adaptation hold the multiplier 높음 settles at (engine/quality.js
  // heldAdapt), as grade exposure stops, so they are not brighter than 높음.
  let keyFrom = 1, keyTo = 1, held = 0, envNow = null; // envNow: the blended state before the key scale
  const keyScale = (preset) => (PRESETS[preset]?.outdoor === false ? profileOf(quality).indoorKey : 1);
  const heldStops = () => (profileOf(quality).adapt ? 0 : Math.log2(heldAdapt(envName, level?.id)));
  function syncHeld() { // re-aim the held exposure after a tier change or a level swap (the grade tween keeps going)
    const s = heldStops(); if (s === held) return;
    post.setGrade({ exposure: post.grade.exposure + s - held }, 0); held = s;
  }
  // Grid power off (blackout): street lamps, lit windows, shop fronts and signs stay dark whatever the sky
  // preset says; battery-backed 'emergency' and 'indoor' (interiors pick their own preset) are untouched.
  const GRID = new Set(['street', 'windows', 'shops', 'signs']);
  const litTarget = (preset) => Object.fromEntries(Object.entries(PRESETS[preset].lit).map(([g, v]) => [g, !power && GRID.has(g) ? 0 : v]));
  function stepEnv(dt) {
    envK = envDur > 0 ? Math.min(1, envK + dt / envDur) : 1;
    const e = ease(envK), S = mixState(envFrom, envTo, e);
    const ks = keyFrom + (keyTo - keyFrom) * e;
    envNow = S; sky.apply(ks === 1 ? S : { ...S, keyI: S.keyI * ks });
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
    envFrom = envNow ? { ...envNow } : envTo;
    keyFrom += (keyTo - keyFrom) * ease(envK); keyTo = keyScale(preset); // from: the scale shown now
    envDur = Math.max(0, Number(seconds) || 0); envK = 0;
    litFrom = { ...litNow }; litTo = { ...litNow, ...litTarget(preset) };
    envName = preset;
    try { scene.environment = sky.environment(preset, rain); } catch (err) { console.error('[engine] PMREM environment failed', err); }
    const g = gradeFor(preset, rain); held = heldStops(); g.exposure += held;
    post.setGrade(g, envDur);
    if (outdoorRain && !rainFx) { try { rainFx = fx.rain(scene, camera); rainFx.setDensity?.(profileOf(quality).fx); } catch (err) { console.error('[engine] rain fx failed', err); } }
    rainFx?.setOn(outdoorRain);
    if (!envDur) stepEnv(0);
  }
  const reapplyLit = () => { for (const [g, v] of Object.entries(litNow)) kit.setLit(g, v > 0.0005, v); };

  // Wet look while it rains: asphalt / sidewalk / concrete surface materials (matched by their shared kit
  // texture) and any material with userData.wet = true get lower roughness, a darker albedo and more
  // reflection. The dry values are kept in material.userData.dry and restored when the rain stops.
  let wetList = null, wetNow = 0;
  function collectWet(root) {
    const srcs = new Set(['asphalt', 'sidewalk', 'concrete'].flatMap((n) => { kit.surface(n); return kit.surfaceMaps(n); })); // every tier's texture
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
  const built = new Map(), done = new Set(), liveL = new Map(); // liveL: id -> the built level of `built`
  // What a dropped build must not free: the scene (current level, sky, player) and the other builds and player models.
  const keepRoots = () => [scene, ...[...liveL.values()].map((L) => L.group), ...Object.values(models).map((m) => m.group)];
  // Shader compiles in flight: renderer.compileAsync polls its materials until they are ready, and one disposed meanwhile
  // (a dropped build being prewarmed) throws there, so a dropped build is freed once those have settled.
  const compiles = new Set();
  // Program keys follow the bound target (tone mapping, output colour space). The scene draws into the composer's
  // half-float buffer, not the screen, so a precompile binds that buffer too (compileAsync collects synchronously);
  // keyed for the screen it compiled programs that were never drawn, and every real one compiled at its first draw.
  function compileAsync(...a) {
    const rt = renderer.getRenderTarget(); renderer.setRenderTarget(post.composer.readBuffer);
    let p; try { p = renderer.compileAsync(...a); } finally { renderer.setRenderTarget(rt); }
    p = p.finally(() => compiles.delete(p)); compiles.add(p); return p;
  }
  function drop(L) { // a build left the cache: free it once nothing shows or compiles it
    if (liveL.get(L.id) === L) liveL.delete(L.id);
    Promise.allSettled([...compiles]).then(() => disposeLevel(L, persons, { kit, keep: keepRoots() }));
  }
  function getLevel(id) {
    if (!built.has(id)) {
      const make = levels[id];
      if (typeof make !== 'function') return Promise.reject(new Error(`[engine] unknown level "${id}"; known: ${Object.keys(levels).join(', ')}`));
      const p = (async () => { await ready; return normalizeLevel(await make(kit, people, fx), id); })();
      built.set(id, p);
      // a failed build may be retried; a build dropped meanwhile (tier change, rebuildOthers) touches nothing
      p.then((L) => { if (built.get(id) === p) { done.add(id); liveL.set(id, L); } }, () => { if (built.get(id) === p) built.delete(id); });
    }
    return built.get(id);
  }
  let queue = Promise.resolve(), busy = false, entering = null, veil = false; // veil: the scene is not drawn (see settle)
  let scripted = false; // a camera override (cutscene, 3D picker) drives the camera
  const stale = new Set(); // built levels still shown at an older tier: dropped once the player leaves them
  // Fade out, swap level.group into the scene, place the player, run prepare(level) (level states, setEnv:
  // the level is already current), precompile shaders for that final setup, fade in.
  function enter(levelId, spawn, { prepare } = {}) {
    const run = queue.catch(() => {}).then(async () => {
      busy = true; entering = levelId;
      const fadeS = RM ? 0 : done.has(levelId) ? 0.1 : 0.35; // a level already built swaps in a blink
      try {
        await post.fadeTo(0, fadeS);
        let L;
        // one retry: a cold first load can drop a module fetch; a failed build is not cached, so the retry refetches
        try { L = await getLevel(levelId).catch(() => getLevel(levelId)); }
        catch (err) { console.error(`[engine] level "${levelId}" failed to load`, err); throw err; }
        if (L !== level) {
          if (wetList) { setWet(wetList, 0); wetList = null; }
          if (level) scene.remove(level.group);
          if (level && stale.delete(level.id) && built.get(level.id) !== undefined) { // built at an older tier: rebuild next time
            const old = level; built.delete(old.id); done.delete(old.id); tried.delete(old.id); drop(old);
          }
          scene.add(L.group); level = L; reapplyLit(); applyWet(wetNow); syncHeld();
        }
        const names = Object.keys(L.spawns);
        const want = spawn ?? (L.spawns.entry ? 'entry' : names[0]);
        if (!controls.player.teleport(want) && names.length) controls.player.teleport(names[0]);
        controls.snap(); controls.update(0);
        if (prepare) { try { await prepare(L); } catch (err) { console.error(`[engine] prepare for level "${levelId}" failed (continuing)`, err); } }
        applyTier(L.group, quality); // shadow casters and shadowed lights for this tier, before the shaders compile
        await settle(L);
        prewarm();
        return L;
      } finally {
        await post.fadeTo(1, fadeS);
        busy = false; entering = null; gov.hold(3); // the first frames after a swap upload textures: do not judge them
      }
    });
    queue = run;
    return run;
  }
  // With the screen black: draw nothing until the scene's programs are compiled. A draw with a program still compiling
  // waits for it on the main thread (getProgramInfoLog), so drawing meanwhile froze the page for tens of seconds on
  // D3D11 (UHD 620); undrawn, they compile in the GPU process while the page stays live. ANGLE still finishes a program
  // at its first draw (and shadow depth programs are not precompiled at all), so a level build's first showing, or any
  // new program, is then drawn once here, every object unculled, with the loading note up: that wait lands behind the
  // black screen instead of in the first seconds of play or of a cutscene.
  const drawn = new WeakSet(); // level builds already drawn once
  async function settle(L) {
    veil = true;
    const progs = renderer.info.programs.length, slow = setTimeout(() => onWait?.(true), 1200);
    try {
      await Promise.race([compileAsync(scene, camera), new Promise((r) => setTimeout(r, 60000))]);
      if (!drawn.has(L) || renderer.info.programs.length !== progs) { clearTimeout(slow); onWait?.(true); await painted(); drawAll(); drawn.add(L); }
    }
    catch (err) { console.warn('[engine] shader precompile failed (continuing)', err); }
    finally { clearTimeout(slow); veil = false; onWait?.(false); }
  }
  const painted = () => new Promise((r) => requestAnimationFrame(() => setTimeout(r, 0))); // the DOM change is on screen
  // One full frame (scene, shadow maps, post) with frustum culling off, so every visible object's programs are used once.
  function drawAll() {
    const culled = [];
    scene.traverse((o) => { if (o.frustumCulled) { o.frustumCulled = false; culled.push(o); } });
    try { post.render(0); } finally { for (const o of culled) o.frustumCulled = true; }
  }

  // Build and precompile the other levels while the player is busy elsewhere, so a door never waits on a
  // build. Idle callbacks keep each build between frames; one level at a time.
  // Off on the lowest tier (memory and long build frames): levels then build at the door.
  let warming = false, building = false; const tried = new Set(); // building: a prewarm build is between frames
  function prewarm() {
    if (warming || !profileOf(quality).prewarm) return; warming = true;
    const idle = (fn) => (globalThis.requestIdleCallback ? requestIdleCallback(fn, { timeout: 8000 }) : setTimeout(fn, 1500));
    const next = () => {
      const id = profileOf(quality).prewarm && Object.keys(levels).find((k) => !built.has(k) && !tried.has(k));
      if (!id) { warming = false; return; }
      tried.add(id);
      idle(async () => {
        // A build is one long main-thread task (1-3 s on a weak laptop) and its compiles queue ahead of the scene's: build
        // only while the player cannot move (a dialog, a menu, the title screen), never in a cutscene or a swap.
        if (scripted || busy || controls.enabled) { tried.delete(id); setTimeout(next, 1500); return; }
        building = true;
        try { const L = await getLevel(id); await compileAsync(L.group, camera, scene); }
        catch (err) { console.warn(`[engine] prewarm of "${id}" failed (it loads at the door instead)`, err); }
        building = false; gov.hold(1.5); // a build's long frames are not the game running slow
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
  // A tier change applies the runtime knobs (pixel ratio, post, shadows, crowd distances and caps, rain) behind a short
  // black fade, then compiles and draws what they re-keyed (settle), so the recompile is not a freeze in view. A tier
  // picked in the settings also drops the other built levels, so they rebuild with this tier's build-time knobs
  // (lights, people, textures) at the next door; the level the player is in keeps its build until the player leaves
  // it. The governor's own steps (auto) keep them: a rebuild is seconds at the next door or cutscene, what it fights.
  function setQuality(q, { auto = false } = {}) {
    const t = normQ(q, null);
    if (!t) { console.error(`[engine] unknown quality "${q}"`); return Promise.resolve(); }
    if (t === quality) { applyQuality(t); return Promise.resolve(); }
    const run = queue.catch(() => {}).then(async () => {
      busy = true;
      try { await post.fadeTo(0, RM ? 0 : 0.2); applyQuality(t); if (!auto) rebuildOthers(); if (level) await settle(level); }
      finally { await post.fadeTo(1, RM ? 0 : 0.2); busy = false; gov.hold(4); }
    });
    queue = run;
    return run;
  }
  function applyQuality(t) {
    quality = t; setLive(t); kit.setQuality(t);
    renderer.setPixelRatio(pixelRatio());
    sky.setQuality(t); post.setQuality(t); controls.setQuality(t);
    rainFx?.setDensity?.(profileOf(t).fx);
    if (level) applyTier(level.group, t);
    const ks = keyScale(envName);
    if (envName && ks !== keyTo) { keyFrom = keyTo = ks; stepEnv(0); }
    syncHeld();
    resize();
  }
  function rebuildOthers() {
    for (const [id, p] of [...built]) {
      if (id === entering || (level && id === level.id)) { stale.add(id); continue; }
      built.delete(id); done.delete(id); tried.delete(id);
      p.then((L) => { if (L !== level) drop(L); }, () => {});
    }
    if (level) prewarm();
  }
  function setReduceMotion(v) { RM = !!v; post.setReduceMotion(RM); controls.setReduceMotion(RM); fx.setReduceMotion?.(RM); }
  if (typeof ResizeObserver === 'function') new ResizeObserver(resize).observe(canvas); else addEventListener('resize', resize);
  resize();

  // ---- frame loop ----
  // Frame listeners run from a snapshot rebuilt only when the set changed: one added during a frame starts next
  // frame, one removed during a frame stops at once (the has() check).
  const frameFns = new Set(), logged = new Set(); let frameList = [], frameDirty = false;
  const logOnce = (key, err) => { if (!logged.has(key)) { logged.add(key); console.error(`[engine] ${key}`, err); } };
  let timeScale = 1; // slow motion (cutscenes); 1 = real time
  let t = 0, last = performance.now();
  // Frame-rate governor (gov, created at the top): below 30 fps for 4 s of real time steps down one tier, then waits
  // 6 s before judging again; never below the lowest tier, never while a level loads, the tab is hidden or a cutscene
  // (a scripted camera) plays, and not for 2 s after a frame that made new shader programs (a compile is not the game
  // running slow; judged as one, it stepped down mid-cutscene and the step recompiled everything).
  const gopt = { busy: false, floor: false }; // reused every frame
  let progN = 0;
  function measure(raw) {
    const n = renderer.info.programs.length; if (n !== progN) { progN = n; gov.hold(2); }
    gopt.busy = busy || building || scripted || globalThis.document?.visibilityState === 'hidden'; gopt.floor = rankQ(quality) === 0;
    if (!gov.sample(raw, gopt)) return;
    const from = quality, q = lowerQ(quality);
    console.warn(`[engine] fps ${gov.fps.toFixed(1)} < 30 for 4 s; quality ${from} -> ${q}`);
    setQuality(q, { auto: true }).then(() => onQualityChange?.(q, { auto: true, from }))
      .catch((err) => console.error('[engine] onQualityChange failed', err));
  }
  const fctx = { camera, player: controls.player, env: 'day', power: true };
  function frame(now) {
    const raw = Math.max(0, (now - last) / 1000); last = now;
    const dt = Math.min(raw, 0.1) * timeScale; t += dt;
    try { controls.update(dt); } catch (err) { logOnce('player update failed', err); }
    if (envK < 1) stepEnv(dt);
    fctx.env = envName; fctx.power = power; // one object reused every frame (levels spread it when they add to it)
    if (level?.update) { try { level.update(dt, t, fctx); } catch (err) { logOnce(`level "${level.id}" update failed`, err); } }
    sky.update(dt, camera, controls.player.position, RM);
    if (rainFx) { try { rainFx.update(dt, camera); } catch (err) { logOnce('rain update failed', err); } }
    if (frameDirty) { frameList = [...frameFns]; frameDirty = false; }
    for (const fn of frameList) if (frameFns.has(fn)) { try { fn(dt, t); } catch (err) { logOnce('onFrame listener failed', err); } }
    if (!veil) post.render(dt);
    measure(raw);
  }
  setEnv('day', { seconds: 0 });
  renderer.setAnimationLoop(frame);

  return {
    THREE, renderer, scene, camera, kit, people, persons, ready, post,
    get fx() { return fx; },
    get quality() { return quality; },
    get qualityDetected() { return detected; }, // the tier was guessed (createEngine quality 'auto')
    get level() { return level; },
    get player() { return controls.player; },
    get view() { return controls.view; },
    get nearest() { return controls.nearest; },
    get controlsEnabled() { return controls.enabled; },
    get env() { return envName; },
    // The grade exposure setEnv gives the current preset (its grade plus a held eye adaptation): what a cutscene returns to.
    get gradeExposure() { return envName ? gradeFor(envName).exposure + held : 0; },
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
    setCameraOverride: (fn) => { scripted = typeof fn === 'function'; controls.setOverride(fn); },
    onInteract: (fn) => controls.onInteract(fn),
    onNearest: (fn) => controls.onNearest(fn),
    onKey: (fn) => controls.onKey(fn),
    onFrame: (fn) => { frameFns.add(fn); frameDirty = true; return () => { const had = frameFns.delete(fn); frameDirty ||= had; return had; }; },
    setQuality, setReduceMotion,
    get timeScale() { return timeScale; }, set timeScale(v) { timeScale = Math.min(2, Math.max(0, Number(v) || 0)); },
    // { adapt: true } also reads back the eye adaptation (a GPU stall: tests only, not every frame).
    stats: ({ adapt = false } = {}) => ({ fps: Math.round(gov.fps), drawCalls: post.sceneStats.calls, triangles: post.sceneStats.triangles, ...(adapt ? { adapt: +post.adaptValue().toFixed(2) } : {}) }),
    dispose() { renderer.setAnimationLoop(null); controls.dispose(); renderer.dispose(); },
  };
}
