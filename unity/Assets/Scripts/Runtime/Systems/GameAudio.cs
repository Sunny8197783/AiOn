// Ports audio.js: everything synthesised (PORTING.md "Save, settings, audio": AudioClip.Create PCM + AudioSources)
// — ambience beds per place, the air-raid siren, the radio voice, phone alerts, hits, footsteps, UI blips,
// fire, extinguisher spray and crowd murmur, with master volume, mute and three buses.
// The Web Audio graph audio.js builds is rebuilt node for node on AudioGraph (gain, biquad, panner, oscillators,
// buffer sources, convolver, limiter); AudioPump plays it through one looping clip. Every source runs through
// master gain -> limiter; mute = master 0. Calls made before Unlock() are remembered and applied once it runs.
// This file: graph helpers, groups and timers, build, unlock and the master/bus controls. AudioSounds.cs: the sounds.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;
using static Gyeongbo.Systems.SysUtil;

namespace Gyeongbo.Systems
{
    public sealed partial class GameAudio : IAudio
    {
        public static GameAudio Create() => new GameAudio();

        public event Action<string, object> Played;
        void Raise(string name, object arg) { try { Played?.Invoke(name, arg); } catch (Exception e) { Debug.LogException(e); } }

        static float Clamp01(float v) => float.IsNaN(v) ? 0 : Mathf.Max(0, Mathf.Min(1, v));

        ACtx ctx;
        AudioPump pump;
        AGain master, amb, sfx, uiBus;
        AConvolver verb;
        ABuffer white, brown;
        float volume = 0.7f;
        bool muted;
        // Per-bus volume (settings): ambience, effects (siren, fire, radio, steps), ui blips. 0..1 on top of the master.
        static readonly Dictionary<string, float> BUS_BASE = new Dictionary<string, float> { { "ambience", 0.8f }, { "effects", 1 }, { "ui", 0.45f } };
        readonly Dictionary<string, float> busVol = new Dictionary<string, float> { { "ambience", 1 }, { "effects", 1 }, { "ui", 1 } };
        AGain BusNode(string name) => name == "ambience" ? amb : name == "effects" ? sfx : name == "ui" ? uiBus : null;
        // want: what the game asked for, replayed by Unlock().
        string wantAmbience = "none";
        bool wantSiren, wantSpray;
        float? wantRadio;                 // clarity while on, null = off
        float wantFire, wantCrowd;
        Grp ambNow, sirenNode, radioNode, fireNode, crowdNode, sprayNode;
        double lastStep;

        GameAudio() { InitTables(); }

        // ---------- timers (setTimeout on the audio clock, run between render quanta) ----------
        sealed class ATimer { public double due; public Action fn; }
        readonly List<ATimer> timers = new List<ATimer>();

        ATimer SetTimeout(Action fn, double seconds)
        {
            var t = new ATimer { due = Now() + Math.Max(0, seconds), fn = fn };
            timers.Add(t);
            return t;
        }
        void ClearTimeout(ATimer t) { if (t != null) timers.Remove(t); }

        void RunTimers()
        {
            double now = ctx.currentTime;
            while (true)
            {
                ATimer next = null;
                foreach (var t in timers) if (t.due <= now && (next == null || t.due < next.due)) next = t;
                if (next == null) return;
                timers.Remove(next);
                try { next.fn(); } catch (Exception e) { Debug.LogError("[audio] timer failed " + e); }
            }
        }

        // ---------- graph helpers ----------
        double Now() => ctx.currentTime;
        AGain Gain(float v = 0) => new AGain(ctx, v);
        ABiquad Filt(string type, float freq, float Q = 0.7f) => new ABiquad(ctx, type, freq, Q);
        APanner Pan(float p) => new APanner(ctx, p);
        static ANode Chain(params ANode[] n) { for (int i = 0; i < n.Length - 1; i++) n[i].Connect(n[i + 1]); return n[n.Length - 1]; }

        ABufferSource Noise(ABuffer buf, bool loop = true)
        {
            var s = new ABufferSource(ctx) { buffer = buf, loop = loop };
            s.Start(Now(), loop ? Rand() * buf.duration : Rand() * (buf.duration - 0.5f));
            return s;
        }
        AOsc Osc(string type, float freq) { var o = new AOsc(ctx) { type = type }; o.frequency.value = freq; o.Start(Now()); return o; }
        AOsc Lfo(float freq, float depth, AParam param) { var o = Osc("sine", freq); var g = Gain(depth); o.Connect(g); g.Connect(param); return o; }
        void Ramp(AParam param, float v, float s)
        {
            double t = Now();
            param.CancelScheduledValues(t); param.SetValueAtTime(param.value, t); param.LinearRampToValueAtTime(v, t + Math.Max(0.01f, s));
        }
        ABuffer MakeBuffer(float seconds, Action<float[], int> fill)
        {
            int len = (int)Math.Floor(ctx.sampleRate * (double)seconds);
            var b = ctx.CreateBuffer(2, len);
            for (int c = 0; c < 2; c++) fill(b.ch[c], len);
            return b;
        }

        // A group of looping nodes and timers that can be faded and stopped together.
        sealed class Grp
        {
            public AGain @out;
            public readonly List<ASource> nodes = new List<ASource>();
            public readonly List<Action> extra = new List<Action>();      // { stop: () => ... } entries
            public readonly HashSet<ATimer> timers = new HashSet<ATimer>();
            public string name;
            public float level;
            public Action<float> set, stop;
        }

        Grp Group(ANode dest, float level = 0)
        {
            var G = new Grp { @out = Gain(level) };
            G.@out.keep = true;
            G.@out.Connect(dest);
            G.stop = fade =>
            {
                foreach (var t in G.timers) ClearTimeout(t);
                G.timers.Clear();
                Ramp(G.@out.gain, 0, fade);
                SetTimeout(() =>
                {
                    foreach (var n in G.nodes) { try { n.Stop(); } catch { /* already stopped */ } }
                    foreach (var a in G.extra) { try { a(); } catch { /* already stopped */ } }
                    G.@out.Disconnect();
                }, fade + 0.1f);
            };
            return G;
        }
        static T Add<T>(Grp G, T n) where T : ASource { G.nodes.Add(n); return n; }
        void Every(Grp G, float minS, float maxS, Action fn)
        {
            ATimer id = null;
            Action tick = null;
            tick = () => { G.timers.Remove(id); fn(); id = SetTimeout(tick, Rnd(minS, maxS)); G.timers.Add(id); };
            id = SetTimeout(tick, Rnd(0, maxS));
            G.timers.Add(id);
        }

        sealed class Ln { public ABufferSource s; public ABiquad f; public AGain g; }
        Ln LoopNoise(Grp G, ABuffer buf, string type, float freq, float vol, float Q = 0.7f, ANode dest = null)
        {
            var s = Add(G, Noise(buf)); var f = Filt(type, freq, Q); var g = Gain(vol);
            Chain(s, f, g, dest ?? G.@out);
            return new Ln { s = s, f = f, g = g };
        }

        // One-shot noise burst (footsteps, clicks, crackle).
        void Burst(float freq = 1000, float Q = 1, string type = "bandpass", float dur = 0.05f, float vol = 0.1f, float p = 0, ANode dest = null, double? at = null, float wet = 0)
        {
            double t = at ?? Now();
            var s = Noise(white, false); var f = Filt(type, freq, Q); var g = Gain(0); var pn = Pan(p);
            Chain(s, f, g, pn, dest ?? sfx);
            if (wet != 0) { var w = Gain(wet); pn.Connect(w); w.Connect(verb); }
            g.gain.SetValueAtTime(0, t); g.gain.LinearRampToValueAtTime(vol, t + 0.004); g.gain.ExponentialRampToValueAtTime(0.0001f, t + dur);
            s.Stop(t + dur + 0.05);
        }
        void Tone(float freq = 880, float? to = null, string type = "sine", float dur = 0.12f, float vol = 0.05f, double? at = null, ANode dest = null, float p = 0)
        {
            double t = at ?? Now();
            var o = new AOsc(ctx) { type = type }; var g = Gain(0); var pn = Pan(p);
            o.frequency.SetValueAtTime(freq, t); if (to.HasValue && to.Value != 0) o.frequency.ExponentialRampToValueAtTime(to.Value, t + dur);
            Chain(o, g, pn, dest ?? uiBus);
            g.gain.SetValueAtTime(0, t); g.gain.LinearRampToValueAtTime(vol, t + 0.008); g.gain.ExponentialRampToValueAtTime(0.0001f, t + dur);
            o.Start(t); o.Stop(t + dur + 0.05);
        }

        void Build()
        {
            master = Gain(muted ? 0 : volume); master.keep = true;
            var limiter = new ALimiter(ctx, -8, 4, 14, 0.003f, 0.25f);
            Chain(master, limiter, ctx.destination);
            amb = Gain(BUS_BASE["ambience"] * busVol["ambience"]); sfx = Gain(BUS_BASE["effects"] * busVol["effects"]); uiBus = Gain(BUS_BASE["ui"] * busVol["ui"]);
            foreach (var b in new[] { amb, sfx, uiBus }) { b.keep = true; b.Connect(master); }
            white = MakeBuffer(3, (d, n) => { for (int i = 0; i < n; i++) d[i] = Rand() * 2 - 1; });
            brown = MakeBuffer(4, (d, n) => { float v = 0; for (int i = 0; i < n; i++) { v = (v + 0.02f * (Rand() * 2 - 1)) / 1.02f; d[i] = v * 3.5f; } });
            verb = new AConvolver(ctx, 2.6f);   // audio.js: a 2.6 s noise impulse with a cubic decay
            var wet = Gain(0.5f); wet.keep = true; Chain(verb, wet, master);
        }

        // Reload (pause -> 메인 메뉴): stop this graph's output; the next GameApp builds its own.
        public void Dispose()
        {
            if (pump != null) UnityEngine.Object.Destroy(pump.gameObject);
            pump = null; ctx = null;
        }

        public void Unlock()
        {
            if (ctx == null)
            {
                try
                {
                    ctx = new ACtx(AudioSettings.outputSampleRate);
                    ctx.onQuantum = RunTimers;
                    Build();
                    pump = AudioPump.Create(ctx);
                }
                catch (Exception e) { Debug.LogError("[audio] audio output unavailable " + e); ctx = null; return; }
                string ambience = wantAmbience; bool siren = wantSiren, spray = wantSpray; float? radio = wantRadio; float fire = wantFire, crowd = wantCrowd;
                Ambience(ambience, 1.5f); if (siren) SirenImpl(true); if (radio.HasValue) Radio(true, radio.Value);
                if (fire != 0) Fire(fire); if (crowd != 0) Crowd(crowd); if (spray) Spray(true);
            }
            if (ctx.state == "suspended" && Application.isFocused) pump?.Suspend(false);
        }

        // ---------- master ----------
        void ApplyMaster() { if (ctx != null) master.gain.SetTargetAtTime(muted ? 0 : volume, Now(), 0.05f); }
        public void SetVolume(float v) { volume = Clamp01(v); ApplyMaster(); }
        public void SetMute(bool on) { muted = on; ApplyMaster(); }
        public void SetBus(string bus, float v)
        {
            if (bus == null || !busVol.ContainsKey(bus)) return;
            busVol[bus] = Clamp01(v);
            var n = ctx != null ? BusNode(bus) : null;
            if (n != null) Ramp(n.gain, BUS_BASE[bus] * busVol[bus], 0.1f);
        }

        public string State => ctx?.state ?? "locked";
    }
}
