// Fully procedural Web Audio (no files): ambiences, the air-raid siren, phone alerts, radio static and a
// muffled voice, footsteps, UI blips, fire, extinguisher spray, crowd murmur.
// Every source runs through master gain -> limiter, so nothing exceeds the master volume; mute = master 0.
// Calls made before unlock() are remembered and applied once the context exists.

const clamp01 = (v) => Math.max(0, Math.min(1, Number(v) || 0));
const rnd = (a, b) => a + Math.random() * (b - a);

export function createAudio() {
  const AC = globalThis.AudioContext || globalThis.webkitAudioContext;
  let ctx = null, master, amb, sfx, uiBus, verb, white, brown;
  let volume = 0.7, muted = false;
  const want = { ambience: 'none', siren: false, radio: null, fire: 0, crowd: 0, spray: false };
  let ambNow = null, sirenNode = null, radioNode = null, fireNode = null, crowdNode = null, sprayNode = null, lastStep = 0;

  // ---------- graph helpers ----------
  const now = () => ctx.currentTime;
  const gain = (v = 0) => { const g = ctx.createGain(); g.gain.value = v; return g; };
  const filt = (type, freq, Q = 0.7) => { const f = ctx.createBiquadFilter(); f.type = type; f.frequency.value = freq; f.Q.value = Q; return f; };
  const pan = (p) => { if (!ctx.createStereoPanner) return gain(1); const n = ctx.createStereoPanner(); n.pan.value = p; return n; };
  const chain = (...n) => { for (let i = 0; i < n.length - 1; i++) n[i].connect(n[i + 1]); return n.at(-1); };
  function noise(buf, loop = true) { const s = ctx.createBufferSource(); s.buffer = buf; s.loop = loop; s.start(now(), loop ? Math.random() * buf.duration : Math.random() * (buf.duration - 0.5)); return s; }
  function osc(type, freq) { const o = ctx.createOscillator(); o.type = type; o.frequency.value = freq; o.start(); return o; }
  function lfo(freq, depth, param) { const o = osc('sine', freq), g = gain(depth); o.connect(g); g.connect(param); return o; }
  const ramp = (param, v, s) => { const t = now(); param.cancelScheduledValues(t); param.setValueAtTime(param.value, t); param.linearRampToValueAtTime(v, t + Math.max(0.01, s)); };
  function makeBuffer(seconds, fill) {
    const len = Math.floor(ctx.sampleRate * seconds), b = ctx.createBuffer(2, len, ctx.sampleRate);
    for (let c = 0; c < 2; c++) fill(b.getChannelData(c), len);
    return b;
  }

  // A group of looping nodes and timers that can be faded and stopped together.
  function group(dest, level = 0) {
    const out = gain(level); out.connect(dest);
    const nodes = [], timers = new Set();
    return {
      out, nodes,
      add(n) { nodes.push(n); return n; },
      every(minS, maxS, fn) { const tick = () => { timers.delete(id); fn(); id = setTimeout(tick, rnd(minS, maxS) * 1000); timers.add(id); }; let id = setTimeout(tick, rnd(0, maxS) * 1000); timers.add(id); },
      stop(fade = 0.3) {
        timers.forEach(clearTimeout); timers.clear();
        ramp(out.gain, 0, fade);
        setTimeout(() => { nodes.forEach((n) => { try { n.stop(); } catch { /* already stopped */ } }); out.disconnect(); }, (fade + 0.1) * 1000);
      },
    };
  }
  const loopNoise = (G, buf, type, freq, vol, Q = 0.7, dest = G.out) => { const s = G.add(noise(buf)), f = filt(type, freq, Q), g = gain(vol); chain(s, f, g, dest); return { s, f, g }; };

  // One-shot noise burst (footsteps, clicks, crackle).
  function burst({ freq = 1000, Q = 1, type = 'bandpass', dur = 0.05, vol = 0.1, p = 0, dest = sfx, at = now(), wet = 0 }) {
    const s = noise(white, false), f = filt(type, freq, Q), g = gain(0), pn = pan(p);
    chain(s, f, g, pn, dest);
    if (wet) { const w = gain(wet); pn.connect(w); w.connect(verb); }
    g.gain.setValueAtTime(0, at); g.gain.linearRampToValueAtTime(vol, at + 0.004); g.gain.exponentialRampToValueAtTime(0.0001, at + dur);
    s.stop(at + dur + 0.05);
  }
  function tone({ freq = 880, to = null, type = 'sine', dur = 0.12, vol = 0.05, at = now(), dest = uiBus, p = 0 }) {
    const o = ctx.createOscillator(), g = gain(0), pn = pan(p);
    o.type = type; o.frequency.setValueAtTime(freq, at); if (to) o.frequency.exponentialRampToValueAtTime(to, at + dur);
    chain(o, g, pn, dest);
    g.gain.setValueAtTime(0, at); g.gain.linearRampToValueAtTime(vol, at + 0.008); g.gain.exponentialRampToValueAtTime(0.0001, at + dur);
    o.start(at); o.stop(at + dur + 0.05);
  }

  function build() {
    master = gain(muted ? 0 : volume);
    const limiter = ctx.createDynamicsCompressor();
    limiter.threshold.value = -8; limiter.knee.value = 4; limiter.ratio.value = 14; limiter.attack.value = 0.003; limiter.release.value = 0.25;
    chain(master, limiter, ctx.destination);
    amb = gain(0.8); sfx = gain(1); uiBus = gain(0.45);
    [amb, sfx, uiBus].forEach((b) => b.connect(master));
    white = makeBuffer(3, (d, n) => { for (let i = 0; i < n; i++) d[i] = Math.random() * 2 - 1; });
    brown = makeBuffer(4, (d, n) => { let v = 0; for (let i = 0; i < n; i++) { v = (v + 0.02 * (Math.random() * 2 - 1)) / 1.02; d[i] = v * 3.5; } });
    verb = ctx.createConvolver();
    verb.buffer = makeBuffer(2.6, (d, n) => { for (let i = 0; i < n; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / n) ** 3; });
    const wet = gain(0.5); chain(verb, wet, master);
  }

  // ---------- ambiences ----------
  function crowdLayer(G, vol, dest = G.out) {
    for (const [f, rate] of [[420, 3.1], [780, 4.3], [1350, 2.6]]) {
      const { g, f: bp } = loopNoise(G, white, 'bandpass', f, vol * 0.5, 1.3, dest);
      G.add(lfo(rate + Math.random(), vol * 0.45, g.gain)); G.add(lfo(0.13 + Math.random() * 0.1, f * 0.15, bp.frequency));
    }
  }
  const cricket = (G, vol) => () => { const t = now(), p = rnd(-0.8, 0.8); for (let i = 0; i < 3; i++) tone({ freq: rnd(4100, 4400), dur: 0.03, vol, at: t + i * 0.055, dest: G.out, p }); };
  const carPass = (G, vol) => () => {
    const s = noise(brown, false), f = filt('bandpass', 300, 0.8), g = gain(0), pn = pan(0), t = now(), d = rnd(2.5, 4.5), dir = Math.random() < 0.5 ? -1 : 1;
    chain(s, f, g, pn, G.out);
    f.frequency.setValueAtTime(260, t); f.frequency.linearRampToValueAtTime(rnd(700, 1000), t + d * 0.5); f.frequency.linearRampToValueAtTime(240, t + d);
    g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(vol, t + d * 0.5); g.gain.linearRampToValueAtTime(0, t + d);
    if (pn.pan) { pn.pan.setValueAtTime(-0.9 * dir, t); pn.pan.linearRampToValueAtTime(0.9 * dir, t + d); }
    s.stop(t + d + 0.1);
  };
  const RECIPES = {
    city(G) {
      loopNoise(G, brown, 'lowpass', 380, 0.45); loopNoise(G, white, 'bandpass', 1800, 0.025, 0.5);
      G.every(3, 8, carPass(G, 0.3));
      G.every(6, 14, () => { const t = now(), p = rnd(-0.7, 0.7); for (let i = 0; i < 3; i++) tone({ freq: rnd(3200, 3900), to: rnd(2400, 2900), dur: 0.08, vol: 0.025, at: t + i * 0.12, dest: G.out, p }); });
    },
    night(G) { loopNoise(G, brown, 'lowpass', 260, 0.3); G.every(0.8, 2, cricket(G, 0.025)); G.every(9, 20, carPass(G, 0.16)); },
    blackout(G) {
      loopNoise(G, brown, 'lowpass', 160, 0.16);
      const w = loopNoise(G, white, 'bandpass', 520, 0.05, 0.4);
      G.add(lfo(0.07, 0.035, w.g.gain)); G.add(lfo(0.05, 180, w.f.frequency));
      G.every(2, 4.5, cricket(G, 0.018));
    },
    indoor(G) { loopNoise(G, brown, 'lowpass', 140, 0.12); const h = G.add(osc('sine', 120)), g = gain(0.012); chain(h, g, G.out); },
    office(G) {
      loopNoise(G, brown, 'lowpass', 200, 0.14); loopNoise(G, white, 'lowpass', 700, 0.045);
      const h = G.add(osc('sine', 120)), g = gain(0.01); chain(h, g, G.out);
      G.every(0.12, 1.4, () => burst({ freq: 3200, type: 'highpass', dur: 0.018, vol: 0.025, p: rnd(-0.6, 0.6), dest: G.out }));
    },
    mart(G) {
      loopNoise(G, white, 'lowpass', 650, 0.05); crowdLayer(G, 0.12);
      G.every(3, 7, () => tone({ freq: 2400, dur: 0.09, vol: 0.025, dest: G.out, p: rnd(-0.5, 0.5) }));
    },
    shelter(G) {
      const wet = gain(0.7); wet.connect(verb);
      loopNoise(G, brown, 'lowpass', 120, 0.24); loopNoise(G, white, 'lowpass', 420, 0.045); crowdLayer(G, 0.09);
      crowdLayer(G, 0.05, wet);
      G.every(4, 9, () => tone({ freq: 1300, to: 650, dur: 0.07, vol: 0.02, dest: wet }));
      G.nodes.push({ stop: () => wet.disconnect() });
    },
    rain(G) {
      const a = loopNoise(G, white, 'highpass', 900, 0.2); const lp = filt('lowpass', 7000); a.g.disconnect(); chain(a.g, lp, G.out);
      loopNoise(G, brown, 'lowpass', 420, 0.14);
      const b = loopNoise(G, white, 'bandpass', 2600, 0.07, 0.8); G.add(lfo(0.3, 0.04, b.g.gain));
    },
  };
  function ambience(name = 'none', fadeS = 1.5) {
    want.ambience = name;
    if (!ctx || ambNow?.name === name) return;
    if (ambNow) ambNow.stop(Math.max(0.05, fadeS));
    ambNow = null;
    if (!RECIPES[name]) { if (name !== 'none') console.warn(`[audio] unknown ambience "${name}"`); return; }
    const G = group(amb, 0); G.name = name;
    try { RECIPES[name](G); } catch (e) { console.error('[audio] ambience failed', name, e); }
    ramp(G.out.gain, 1, Math.max(0.05, fadeS));
    ambNow = G;
  }

  // ---------- siren: air-raid wave tone, 1 minute of 5 s rise + 3 s fall ----------
  function makeSiren() {
    const G = group(sfx, 0), t0 = now() + 0.05, lo = 300, hi = 640;
    const tone1 = G.add(osc('sawtooth', lo)), tone2 = G.add(osc('sawtooth', lo)), tone3 = G.add(osc('triangle', lo * 2));
    tone2.detune.value = 9;
    const lp = filt('lowpass', 1700, 0.5), mix = gain(0.5), m3 = gain(0.3);
    tone1.connect(mix); tone2.connect(mix); tone3.connect(m3); m3.connect(mix); chain(mix, lp, G.out);
    const send = gain(0.9); G.out.connect(send); send.connect(verb);
    const sched = (f, mul) => {
      f.setValueAtTime(lo * mul, t0);
      for (let i = 0; i < 7; i++) { const tc = t0 + i * 8; f.exponentialRampToValueAtTime(hi * mul, tc + 5); f.exponentialRampToValueAtTime(lo * mul, tc + 8); }
      f.exponentialRampToValueAtTime(hi * mul, t0 + 60); f.exponentialRampToValueAtTime(lo * 0.55 * mul, t0 + 63);
    };
    sched(tone1.frequency, 1); sched(tone2.frequency, 1); sched(tone3.frequency, 2);
    const g = G.out.gain;
    g.setValueAtTime(0, t0);
    for (let i = 0; i < 7; i++) { const tc = t0 + i * 8; g.linearRampToValueAtTime(0.1, tc + 0.05); g.linearRampToValueAtTime(0.3, tc + 5); g.linearRampToValueAtTime(0.1, tc + 8); }
    g.linearRampToValueAtTime(0.3, t0 + 60); g.linearRampToValueAtTime(0, t0 + 63);
    G.add(lfo(0.37, 0.03, G.out.gain));
    [tone1, tone2, tone3].forEach((o) => o.stop(t0 + 63.3));
    const end = setTimeout(() => { if (sirenNode === G) { sirenNode = null; want.siren = false; } }, 63500);
    const stop = G.stop; G.stop = (f) => { clearTimeout(end); stop(f); };
    return G;
  }
  function siren(on) {
    want.siren = !!on;
    if (!ctx) return;
    if (on && !sirenNode) sirenNode = makeSiren();
    else if (!on && sirenNode) { sirenNode.stop(1.5); sirenNode = null; }
  }

  // ---------- phone alerts ----------
  function phoneAlert(kind = 'sms') {
    if (!ctx) return;
    const t = now();
    if (kind === 'emergency') {
      // Two-tone attention signal (853 + 960 Hz), pulsed, moderate level.
      for (const [s, d] of [[0, 1.0], [1.2, 0.45], [1.85, 0.45], [2.5, 1.2]]) for (const f of [853, 960]) {
        const o = ctx.createOscillator(), g = gain(0), lp = filt('lowpass', 3200);
        o.type = 'square'; o.frequency.value = f; chain(o, lp, g, sfx);
        g.gain.setValueAtTime(0, t + s); g.gain.linearRampToValueAtTime(0.05, t + s + 0.01); g.gain.setValueAtTime(0.05, t + s + d - 0.02); g.gain.linearRampToValueAtTime(0, t + s + d);
        o.start(t + s); o.stop(t + s + d + 0.05);
      }
    } else if (kind === 'safety') {
      [1046, 1318, 1568].forEach((f, i) => tone({ freq: f, dur: 0.5, vol: 0.09, at: t + i * 0.22, dest: sfx }));
    } else {
      [0, 0.16].forEach((s) => tone({ freq: 1760, dur: 0.18, vol: 0.06, at: t + s, dest: sfx }));
    }
  }

  // ---------- radio: static + a muffled synthetic voice; clarity 0..1 trades one for the other ----------
  function makeRadio() {
    const G = group(sfx, 0);
    const stat = loopNoise(G, white, 'bandpass', 2200, 0.2, 0.6);
    const crack = gain(0); const cs = G.add(noise(white)); chain(cs, filt('highpass', 3800), crack, G.out);
    const voiceOut = gain(0), syll = gain(0), buzz = G.add(osc('sawtooth', 140));
    const formants = [[650, 5, 1], [1150, 6, 0.7], [2500, 7, 0.35]].map(([f, q, v]) => { const bp = filt('bandpass', f, q), g = gain(v); chain(buzz, bp, g, syll); return bp; });
    chain(syll, filt('highpass', 320), filt('lowpass', 2900), voiceOut, G.out);
    let clarity = 0;
    G.every(0.12, 0.26, () => { buzz.frequency.setTargetAtTime(rnd(105, 185), now(), 0.04); formants[0].frequency.setTargetAtTime(rnd(450, 850), now(), 0.05); formants[1].frequency.setTargetAtTime(rnd(950, 1700), now(), 0.05); });
    G.every(0.09, 0.22, () => syll.gain.setTargetAtTime(Math.random() < 0.18 ? 0 : rnd(0.4, 1), now(), 0.03));
    G.every(0.04, 0.2, () => { const k = 1 - clarity; if (Math.random() < 0.3 + k * 0.5) { const t = now(); crack.gain.setValueAtTime(rnd(0.05, 0.25) * k, t); crack.gain.exponentialRampToValueAtTime(0.0001, t + rnd(0.01, 0.04)); } });
    ramp(G.out.gain, 0.9, 0.2);
    G.set = (c) => { clarity = c; stat.g.gain.setTargetAtTime(0.03 + (1 - c) * 0.3, now(), 0.08); voiceOut.gain.setTargetAtTime(c * 0.3, now(), 0.08); };
    return G;
  }
  function radio({ on = true, clarity = 0 } = {}) {
    want.radio = on ? { clarity } : null;
    if (!ctx) return;
    if (!on) { radioNode?.stop(0.25); radioNode = null; return; }
    radioNode ||= makeRadio();
    radioNode.set(clamp01(clarity));
  }

  // ---------- footsteps ----------
  const STEP = { concrete: [950, 1.1, 0.08, 0.09, 0], tile: [2300, 1.4, 0.055, 0.08, 0], wood: [450, 1.8, 0.1, 0.11, 0], stairs: [760, 1, 0.11, 0.11, 0.25] };
  function footstep(surface = 'concrete') {
    if (!ctx || now() - lastStep < 0.16) return;
    lastStep = now();
    const [f, Q, dur, vol, wet] = STEP[surface] || STEP.concrete;
    burst({ freq: f * rnd(0.85, 1.15), Q, dur: dur * rnd(0.85, 1.15), vol: vol * rnd(0.8, 1), p: rnd(-0.15, 0.15), wet });
    if (surface === 'wood' || surface === 'stairs') tone({ freq: rnd(80, 100), to: 50, dur: 0.07, vol: 0.06, dest: sfx });
  }

  // ---------- UI blips ----------
  const UI = {
    open: () => tone({ freq: 660, to: 880, dur: 0.09, vol: 0.035 }),
    close: () => tone({ freq: 820, to: 600, dur: 0.08, vol: 0.025 }),
    click: () => tone({ freq: 1400, dur: 0.035, vol: 0.03 }),
    tick: () => tone({ freq: 1250, dur: 0.03, vol: 0.022 }),
    select: () => tone({ freq: 990, dur: 0.07, vol: 0.04 }),
    confirm: () => { tone({ freq: 784, dur: 0.08, vol: 0.045 }); tone({ freq: 1175, dur: 0.12, vol: 0.045, at: now() + 0.07 }); },
    error: () => tone({ freq: 220, type: 'triangle', dur: 0.14, vol: 0.06 }),
    note: () => { tone({ freq: 1318, dur: 0.1, vol: 0.03 }); tone({ freq: 1760, dur: 0.12, vol: 0.03, at: now() + 0.06 }); },
    payoff: () => [659, 880, 1318].forEach((f, i) => tone({ freq: f, dur: 0.25, vol: 0.04, at: now() + i * 0.09 })),
    alert: () => [0, 0.2, 0.4].forEach((s) => tone({ freq: 1000, type: 'triangle', dur: 0.12, vol: 0.05, at: now() + s })),
  };
  function ui(name = 'click') { if (ctx) (UI[name] || UI.click)(); }
  addEventListener('gyeongbo:ui', (e) => ui(e.detail));

  // ---------- fire, spray, crowd ----------
  function fire(level = 0) {
    want.fire = clamp01(level);
    if (!ctx) return;
    if (want.fire <= 0) { fireNode?.stop(0.8); fireNode = null; return; }
    if (!fireNode) {
      const G = group(sfx, 0); G.level = 0;
      const roar = loopNoise(G, brown, 'lowpass', 480, 0.0), hiss = loopNoise(G, white, 'bandpass', 2500, 0.0, 0.5);
      G.every(0.03, 0.16, () => { if (Math.random() < 0.25 + G.level * 0.6) burst({ freq: rnd(1500, 4000), type: 'highpass', dur: rnd(0.008, 0.03), vol: rnd(0.05, 0.22) * G.level, p: rnd(-0.5, 0.5), dest: G.out }); });
      G.set = (k) => { G.level = k; roar.g.gain.setTargetAtTime(k * 0.4, now(), 0.3); hiss.g.gain.setTargetAtTime(k * 0.06, now(), 0.3); };
      ramp(G.out.gain, 1, 0.5);
      fireNode = G;
    }
    fireNode.set(want.fire);
  }
  function spray(on) {
    want.spray = !!on;
    if (!ctx) return;
    if (!on) { sprayNode?.stop(0.15); sprayNode = null; return; }
    if (sprayNode) return;
    const G = group(sfx, 0);
    const s = loopNoise(G, white, 'highpass', 1800, 0.18);
    G.add(lfo(13, 0.04, s.g.gain));
    ramp(G.out.gain, 1, 0.05);
    sprayNode = G;
  }
  function crowd(level = 0) {
    want.crowd = clamp01(level);
    if (!ctx) return;
    if (want.crowd <= 0) { crowdNode?.stop(1); crowdNode = null; return; }
    if (!crowdNode) { crowdNode = group(sfx, 0); crowdLayer(crowdNode, 0.35); }
    ramp(crowdNode.out.gain, want.crowd, 0.8);
  }

  // ---------- master ----------
  const applyMaster = () => { if (ctx) master.gain.setTargetAtTime(muted ? 0 : volume, now(), 0.05); };
  function unlock() {
    if (!AC) return;
    if (!ctx) {
      try { ctx = new AC(); build(); } catch (e) { console.error('[audio] Web Audio unavailable', e); ctx = null; return; }
      const w = { ...want };
      ambience(w.ambience, 1.5); if (w.siren) siren(true); if (w.radio) radio({ on: true, ...w.radio });
      if (w.fire) fire(w.fire); if (w.crowd) crowd(w.crowd); if (w.spray) spray(true);
    }
    if (ctx.state === 'suspended') ctx.resume().catch(() => {});
  }
  document.addEventListener('visibilitychange', () => {
    if (!ctx) return;
    if (document.hidden) ctx.suspend().catch(() => {}); else ctx.resume().catch(() => {});
  });

  return {
    unlock,
    setVolume(v) { volume = clamp01(v); applyMaster(); },
    setMute(b) { muted = !!b; applyMaster(); },
    ambience, siren, phoneAlert, radio, footstep, ui, fire, spray, crowd,
    get state() { return ctx?.state ?? 'locked'; },
  };
}
