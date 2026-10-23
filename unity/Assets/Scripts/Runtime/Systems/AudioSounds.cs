// audio.js sounds, on the graph helpers in GameAudio.cs: ambience recipes, the air-raid siren, phone alerts, the
// radio, footsteps, UI blips, fire / spray / crowd, the distant boom and body/object hits. Numbers are audio.js's.
using System;
using System.Collections.Generic;
using UnityEngine;
using static Gyeongbo.Systems.SysUtil;

namespace Gyeongbo.Systems
{
    public sealed partial class GameAudio
    {
        Dictionary<string, Action<Grp>> RECIPES;
        Dictionary<string, float[]> STEP;
        Dictionary<string, Action> UI;
        Dictionary<string, Action<float?>> SFX;

        void InitTables()
        {
            RECIPES = new Dictionary<string, Action<Grp>>
            {
                ["city"] = G =>
                {
                    LoopNoise(G, brown, "lowpass", 380, 0.45f); LoopNoise(G, white, "bandpass", 1800, 0.025f, 0.5f);
                    Every(G, 3, 8, CarPass(G, 0.3f));
                    Every(G, 6, 14, () => { double t = Now(); float p = Rnd(-0.7f, 0.7f); for (int i = 0; i < 3; i++) Tone(freq: Rnd(3200, 3900), to: Rnd(2400, 2900), dur: 0.08f, vol: 0.025f, at: t + i * 0.12, dest: G.@out, p: p); });
                },
                ["night"] = G => { LoopNoise(G, brown, "lowpass", 260, 0.3f); Every(G, 0.8f, 2, Cricket(G, 0.025f)); Every(G, 9, 20, CarPass(G, 0.16f)); },
                ["blackout"] = G =>
                {
                    LoopNoise(G, brown, "lowpass", 160, 0.16f);
                    var w = LoopNoise(G, white, "bandpass", 520, 0.05f, 0.4f);
                    Add(G, Lfo(0.07f, 0.035f, w.g.gain)); Add(G, Lfo(0.05f, 180, w.f.frequency));
                    Every(G, 2, 4.5f, Cricket(G, 0.018f));
                },
                ["indoor"] = G => { LoopNoise(G, brown, "lowpass", 140, 0.12f); var h = Add(G, Osc("sine", 120)); var g = Gain(0.012f); Chain(h, g, G.@out); },
                ["office"] = G =>
                {
                    LoopNoise(G, brown, "lowpass", 200, 0.14f); LoopNoise(G, white, "lowpass", 700, 0.045f);
                    var h = Add(G, Osc("sine", 120)); var g = Gain(0.01f); Chain(h, g, G.@out);
                    Every(G, 0.12f, 1.4f, () => Burst(freq: 3200, type: "highpass", dur: 0.018f, vol: 0.025f, p: Rnd(-0.6f, 0.6f), dest: G.@out));
                },
                ["mart"] = G =>
                {
                    LoopNoise(G, white, "lowpass", 650, 0.05f); CrowdLayer(G, 0.12f);
                    Every(G, 3, 7, () => Tone(freq: 2400, dur: 0.09f, vol: 0.025f, dest: G.@out, p: Rnd(-0.5f, 0.5f)));
                },
                ["shelter"] = G =>
                {
                    var wet = Gain(0.7f); wet.Connect(verb);
                    LoopNoise(G, brown, "lowpass", 120, 0.24f); LoopNoise(G, white, "lowpass", 420, 0.045f); CrowdLayer(G, 0.09f);
                    CrowdLayer(G, 0.05f, wet);
                    Every(G, 4, 9, () => Tone(freq: 1300, to: 650, dur: 0.07f, vol: 0.02f, dest: wet));
                    G.extra.Add(() => wet.Disconnect());
                },
                ["rain"] = G =>
                {
                    var a = LoopNoise(G, white, "highpass", 900, 0.2f); var lp = Filt("lowpass", 7000); a.g.Disconnect(); Chain(a.g, lp, G.@out);
                    LoopNoise(G, brown, "lowpass", 420, 0.14f);
                    var b = LoopNoise(G, white, "bandpass", 2600, 0.07f, 0.8f); Add(G, Lfo(0.3f, 0.04f, b.g.gain));
                },
            };
            // [freq, Q, dur, vol, wet]
            STEP = new Dictionary<string, float[]>
            {
                ["concrete"] = new[] { 950, 1.1f, 0.08f, 0.09f, 0 }, ["tile"] = new[] { 2300, 1.4f, 0.055f, 0.08f, 0 },
                ["wood"] = new[] { 450, 1.8f, 0.1f, 0.11f, 0 }, ["stairs"] = new[] { 760, 1, 0.11f, 0.11f, 0.25f },
            };
            UI = new Dictionary<string, Action>
            {
                ["open"] = () => Tone(freq: 660, to: 880, dur: 0.09f, vol: 0.035f),
                ["close"] = () => Tone(freq: 820, to: 600, dur: 0.08f, vol: 0.025f),
                ["click"] = () => Tone(freq: 1400, dur: 0.035f, vol: 0.03f),
                ["tick"] = () => Tone(freq: 1250, dur: 0.03f, vol: 0.022f),
                ["select"] = () => Tone(freq: 990, dur: 0.07f, vol: 0.04f),
                ["confirm"] = () => { Tone(freq: 784, dur: 0.08f, vol: 0.045f); Tone(freq: 1175, dur: 0.12f, vol: 0.045f, at: Now() + 0.07); },
                ["error"] = () => Tone(freq: 220, type: "triangle", dur: 0.14f, vol: 0.06f),
                ["note"] = () => { Tone(freq: 1318, dur: 0.1f, vol: 0.03f); Tone(freq: 1760, dur: 0.12f, vol: 0.03f, at: Now() + 0.06); },
                ["payoff"] = () => { var f = new float[] { 659, 880, 1318 }; for (int i = 0; i < f.Length; i++) Tone(freq: f[i], dur: 0.25f, vol: 0.04f, at: Now() + i * 0.09); },
                ["alert"] = () => { foreach (var s in new[] { 0, 0.2, 0.4 }) Tone(freq: 1000, type: "triangle", dur: 0.12f, vol: 0.05f, at: Now() + s); },
            };
            // Body and object hits: punches, thrown things landing, breaking glass, a swing through the air.
            SFX = new Dictionary<string, Action<float?>>
            {
                ["punch"] = _ => { Burst(freq: 900, Q: 0.8f, dur: 0.06f, vol: 0.5f); Tone(freq: 120, to: 60, dur: 0.12f, vol: 0.25f, dest: sfx); },
                ["thud"] = _ => { Burst(freq: 400, Q: 1, dur: 0.09f, vol: 0.35f); Tone(freq: 90, to: 50, dur: 0.1f, vol: 0.18f, dest: sfx); },
                ["land"] = _ => { Burst(freq: 300, Q: 0.7f, type: "lowpass", dur: 0.18f, vol: 0.7f); Tone(freq: 70, to: 40, dur: 0.2f, vol: 0.35f, dest: sfx); },
                ["glass"] = _ =>
                {
                    for (int i = 0; i < 6; i++) Tone(freq: Rnd(2500, 5200), dur: Rnd(0.05f, 0.25f), vol: 0.05f, at: Now() + i * Rnd(0.01f, 0.04f), dest: sfx, type: "triangle");
                    Burst(freq: 5000, Q: 0.5f, type: "highpass", dur: 0.2f, vol: 0.25f);
                },
                ["swing"] = _ => Burst(freq: 700, Q: 0.5f, dur: 0.16f, vol: 0.12f),
                ["pick"] = _ => Tone(freq: 500, to: 700, dur: 0.06f, vol: 0.05f, dest: sfx),
                // An incoming shell: a falling whistle over `s` seconds (the blast itself is Boom()).
                ["whistle"] = s => Tone(freq: 2400, to: 500, type: "sine", dur: s ?? 2.2f, vol: 0.12f, dest: sfx),
                ["collapse"] = _ => { double t = Now(); for (int i = 0; i < 14; i++) Burst(freq: Rnd(150, 700), Q: 0.6f, type: "lowpass", dur: Rnd(0.2f, 0.6f), vol: Rnd(0.3f, 0.8f), at: t + i * Rnd(0.05f, 0.2f), wet: 0.4f); },
            };
        }

        // ---------- ambiences ----------
        void CrowdLayer(Grp G, float vol, ANode dest = null)
        {
            foreach (var fr in new[] { (420f, 3.1f), (780f, 4.3f), (1350f, 2.6f) })
            {
                float f = fr.Item1, rate = fr.Item2;
                var ln = LoopNoise(G, white, "bandpass", f, vol * 0.5f, 1.3f, dest ?? G.@out);
                Add(G, Lfo(rate + Rand(), vol * 0.45f, ln.g.gain)); Add(G, Lfo(0.13f + Rand() * 0.1f, f * 0.15f, ln.f.frequency));
            }
        }
        Action Cricket(Grp G, float vol) => () => { double t = Now(); float p = Rnd(-0.8f, 0.8f); for (int i = 0; i < 3; i++) Tone(freq: Rnd(4100, 4400), dur: 0.03f, vol: vol, at: t + i * 0.055, dest: G.@out, p: p); };
        Action CarPass(Grp G, float vol) => () =>
        {
            var s = Noise(brown, false); var f = Filt("bandpass", 300, 0.8f); var g = Gain(0); var pn = Pan(0); double t = Now(); float d = Rnd(2.5f, 4.5f), dir = Rand() < 0.5f ? -1 : 1;
            Chain(s, f, g, pn, G.@out);
            f.frequency.SetValueAtTime(260, t); f.frequency.LinearRampToValueAtTime(Rnd(700, 1000), t + d * 0.5); f.frequency.LinearRampToValueAtTime(240, t + d);
            g.gain.SetValueAtTime(0, t); g.gain.LinearRampToValueAtTime(vol, t + d * 0.5); g.gain.LinearRampToValueAtTime(0, t + d);
            pn.pan.SetValueAtTime(-0.9f * dir, t); pn.pan.LinearRampToValueAtTime(0.9f * dir, t + d);
            s.Stop(t + d + 0.1);
        };

        public void Ambience(string name = "none", float fadeS = 1.5f)
        {
            name ??= "none";
            wantAmbience = name;
            if (ctx == null || ambNow?.name == name) return;
            if (ambNow != null) ambNow.stop(Mathf.Max(0.05f, fadeS));
            ambNow = null;
            if (!RECIPES.TryGetValue(name, out var recipe)) { if (name != "none") Debug.LogWarning("[audio] unknown ambience \"" + name + "\""); return; }
            var G = Group(amb, 0); G.name = name;
            try { recipe(G); } catch (Exception e) { Debug.LogError("[audio] ambience failed " + name + " " + e); }
            Ramp(G.@out.gain, 1, Mathf.Max(0.05f, fadeS));
            ambNow = G;
        }

        // ---------- siren: air-raid wave tone, 1 minute of 5 s rise + 3 s fall ----------
        Grp MakeSiren()
        {
            var G = Group(sfx, 0); double t0 = Now() + 0.05; const float lo = 300, hi = 640;
            var tone1 = Add(G, Osc("sawtooth", lo)); var tone2 = Add(G, Osc("sawtooth", lo)); var tone3 = Add(G, Osc("triangle", lo * 2));
            tone2.detune.value = 9;
            var lp = Filt("lowpass", 1700, 0.5f); var mix = Gain(0.5f); var m3 = Gain(0.3f);
            tone1.Connect(mix); tone2.Connect(mix); tone3.Connect(m3); m3.Connect(mix); Chain(mix, lp, G.@out);
            var send = Gain(0.9f); G.@out.Connect(send); send.Connect(verb);
            void Sched(AParam f, float mul)
            {
                f.SetValueAtTime(lo * mul, t0);
                for (int i = 0; i < 7; i++) { double tc = t0 + i * 8; f.ExponentialRampToValueAtTime(hi * mul, tc + 5); f.ExponentialRampToValueAtTime(lo * mul, tc + 8); }
                f.ExponentialRampToValueAtTime(hi * mul, t0 + 60); f.ExponentialRampToValueAtTime(lo * 0.55f * mul, t0 + 63);
            }
            Sched(tone1.frequency, 1); Sched(tone2.frequency, 1); Sched(tone3.frequency, 2);
            var g = G.@out.gain;
            g.SetValueAtTime(0, t0);
            for (int i = 0; i < 7; i++) { double tc = t0 + i * 8; g.LinearRampToValueAtTime(0.1f, tc + 0.05); g.LinearRampToValueAtTime(0.3f, tc + 5); g.LinearRampToValueAtTime(0.1f, tc + 8); }
            g.LinearRampToValueAtTime(0.3f, t0 + 60); g.LinearRampToValueAtTime(0, t0 + 63);
            Add(G, Lfo(0.37f, 0.03f, G.@out.gain));
            foreach (var o in new[] { tone1, tone2, tone3 }) o.Stop(t0 + 63.3);
            var stop = G.stop;
            var end = SetTimeout(() => { if (sirenNode == G) { sirenNode = null; wantSiren = false; } stop(0.05f); }, 63.5); // also frees the LFO and reverb send
            G.stop = f => { ClearTimeout(end); stop(f); };
            return G;
        }
        void SirenImpl(bool on)
        {
            wantSiren = on;
            if (ctx == null) return;
            if (on && sirenNode == null) sirenNode = MakeSiren();
            else if (!on && sirenNode != null) { sirenNode.stop(1.5f); sirenNode = null; }
        }
        public void Siren(bool on) { SirenImpl(on); Raise("siren", on); }

        // ---------- phone alerts ----------
        public void PhoneAlert(string kind = "sms")
        {
            kind ??= "sms";
            if (ctx != null)
            {
                double t = Now();
                if (kind == "emergency")
                {
                    // Two-tone attention signal (853 + 960 Hz), pulsed, moderate level.
                    foreach (var sd in new[] { (0.0, 1.0), (1.2, 0.45), (1.85, 0.45), (2.5, 1.2) })
                        foreach (var f in new[] { 853f, 960f })
                        {
                            double s = sd.Item1, d = sd.Item2;
                            var o = new AOsc(ctx) { type = "square" }; var g = Gain(0); var lp = Filt("lowpass", 3200);
                            o.frequency.value = f; Chain(o, lp, g, sfx);
                            g.gain.SetValueAtTime(0, t + s); g.gain.LinearRampToValueAtTime(0.05f, t + s + 0.01); g.gain.SetValueAtTime(0.05f, t + s + d - 0.02); g.gain.LinearRampToValueAtTime(0, t + s + d);
                            o.Start(t + s); o.Stop(t + s + d + 0.05);
                        }
                }
                else if (kind == "safety")
                {
                    var f = new float[] { 1046, 1318, 1568 };
                    for (int i = 0; i < f.Length; i++) Tone(freq: f[i], dur: 0.5f, vol: 0.09f, at: t + i * 0.22, dest: sfx);
                }
                else foreach (var s in new[] { 0, 0.16 }) Tone(freq: 1760, dur: 0.18f, vol: 0.06f, at: t + s, dest: sfx);
            }
            Raise("phoneAlert", kind);
        }

        // ---------- radio: static + a muffled synthetic voice; clarity 0..1 trades one for the other ----------
        Grp MakeRadio()
        {
            var G = Group(sfx, 0);
            var stat = LoopNoise(G, white, "bandpass", 2200, 0.2f, 0.6f);
            var crack = Gain(0); var cs = Add(G, Noise(white)); Chain(cs, Filt("highpass", 3800), crack, G.@out);
            var voiceOut = Gain(0); var syll = Gain(0); var buzz = Add(G, Osc("sawtooth", 140));
            var formants = new List<ABiquad>();
            foreach (var fqv in new[] { (650f, 5f, 1f), (1150f, 6f, 0.7f), (2500f, 7f, 0.35f) })
            {
                var bp = Filt("bandpass", fqv.Item1, fqv.Item2); var g = Gain(fqv.Item3); Chain(buzz, bp, g, syll); formants.Add(bp);
            }
            Chain(syll, Filt("highpass", 320), Filt("lowpass", 2900), voiceOut, G.@out);
            float clarity = 0;
            Every(G, 0.12f, 0.26f, () => { buzz.frequency.SetTargetAtTime(Rnd(105, 185), Now(), 0.04f); formants[0].frequency.SetTargetAtTime(Rnd(450, 850), Now(), 0.05f); formants[1].frequency.SetTargetAtTime(Rnd(950, 1700), Now(), 0.05f); });
            Every(G, 0.09f, 0.22f, () => syll.gain.SetTargetAtTime(Rand() < 0.18f ? 0 : Rnd(0.4f, 1), Now(), 0.03f));
            Every(G, 0.04f, 0.2f, () => { float k = 1 - clarity; if (Rand() < 0.3f + k * 0.5f) { double t = Now(); crack.gain.SetValueAtTime(Rnd(0.05f, 0.25f) * k, t); crack.gain.ExponentialRampToValueAtTime(0.0001f, t + Rnd(0.01f, 0.04f)); } });
            Ramp(G.@out.gain, 0.9f, 0.2f);
            G.set = c => { clarity = c; stat.g.gain.SetTargetAtTime(0.03f + (1 - c) * 0.3f, Now(), 0.08f); voiceOut.gain.SetTargetAtTime(c * 0.3f, Now(), 0.08f); };
            return G;
        }
        public void Radio(bool on = true, float clarity = 0)
        {
            wantRadio = on ? clarity : (float?)null;
            if (ctx == null) return;
            if (!on) { radioNode?.stop(0.25f); radioNode = null; return; }
            radioNode ??= MakeRadio();
            radioNode.set(Clamp01(clarity));
        }

        // ---------- footsteps ----------
        public void Footstep(string surface = "concrete")
        {
            if (ctx == null || Now() - lastStep < 0.16) return;
            lastStep = Now();
            if (surface == null || !STEP.TryGetValue(surface, out var st)) st = STEP["concrete"];
            float f = st[0], Q = st[1], dur = st[2], vol = st[3], wet = st[4];
            Burst(freq: f * Rnd(0.85f, 1.15f), Q: Q, dur: dur * Rnd(0.85f, 1.15f), vol: vol * Rnd(0.8f, 1), p: Rnd(-0.15f, 0.15f), wet: wet);
            if (surface == "wood" || surface == "stairs") Tone(freq: Rnd(80, 100), to: 50, dur: 0.07f, vol: 0.06f, dest: sfx);
        }

        // ---------- UI blips ----------
        public void Ui(string name = "click")
        {
            if (ctx == null) return;
            if (name == null || !UI.TryGetValue(name, out var fn)) fn = UI["click"];
            fn();
        }

        // ---------- fire, spray, crowd ----------
        public void Fire(float level = 0)
        {
            wantFire = Clamp01(level);
            if (ctx == null) return;
            if (wantFire <= 0) { fireNode?.stop(0.8f); fireNode = null; return; }
            if (fireNode == null)
            {
                var G = Group(sfx, 0); G.level = 0;
                var roar = LoopNoise(G, brown, "lowpass", 480, 0.0f); var hiss = LoopNoise(G, white, "bandpass", 2500, 0.0f, 0.5f);
                Every(G, 0.03f, 0.16f, () => { if (Rand() < 0.25f + G.level * 0.6f) Burst(freq: Rnd(1500, 4000), type: "highpass", dur: Rnd(0.008f, 0.03f), vol: Rnd(0.05f, 0.22f) * G.level, p: Rnd(-0.5f, 0.5f), dest: G.@out); });
                G.set = k => { G.level = k; roar.g.gain.SetTargetAtTime(k * 0.4f, Now(), 0.3f); hiss.g.gain.SetTargetAtTime(k * 0.06f, Now(), 0.3f); };
                Ramp(G.@out.gain, 1, 0.5f);
                fireNode = G;
            }
            fireNode.set(wantFire);
        }

        public void Spray(bool on)
        {
            wantSpray = on;
            if (ctx == null) return;
            if (!on) { sprayNode?.stop(0.15f); sprayNode = null; return; }
            if (sprayNode != null) return;
            var G = Group(sfx, 0);
            var s = LoopNoise(G, white, "highpass", 1800, 0.18f);
            Add(G, Lfo(13, 0.04f, s.g.gain));
            Ramp(G.@out.gain, 1, 0.05f);
            sprayNode = G;
        }

        public void Crowd(float level = 0)
        {
            wantCrowd = Clamp01(level);
            if (ctx == null) return;
            if (wantCrowd <= 0) { crowdNode?.stop(1); crowdNode = null; return; }
            if (crowdNode == null) { crowdNode = Group(sfx, 0); CrowdLayer(crowdNode, 0.35f); }
            Ramp(crowdNode.@out.gain, wantCrowd, 0.8f);
        }

        // A distant explosion: a sharp crack, then a long low rumble with the room/street reverb.
        public void Boom(float distance = 1)
        {
            if (ctx != null)
            {
                double t = Now(); float far = Mathf.Max(0.3f, Mathf.Min(1, distance));
                Burst(freq: 1800 - far * 900, Q: 0.6f, type: "lowpass", dur: 0.25f, vol: 0.9f / far, wet: 0.6f);
                var s = Noise(brown, false); var f = Filt("lowpass", 160 - far * 60, 0.8f); var g = Gain(0);
                Chain(s, f, g, sfx); var w = Gain(0.7f); g.Connect(w); w.Connect(verb);
                g.gain.SetValueAtTime(0, t); g.gain.LinearRampToValueAtTime(1.6f / far, t + 0.05); g.gain.ExponentialRampToValueAtTime(0.0001f, t + 3.8);
                s.Stop(t + 4);
            }
            Raise("boom", distance);
        }

        // punch | thud | land | glass | swing | pick | whistle(seconds) | collapse; anything else thuds.
        public void Hit(string name, float? arg = null)
        {
            if (ctx == null) return;
            if (name == null || !SFX.TryGetValue(name, out var fn)) fn = SFX["thud"];
            fn(arg);
        }
    }
}
