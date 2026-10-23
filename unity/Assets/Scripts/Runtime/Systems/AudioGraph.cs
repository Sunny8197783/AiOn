// The slice of Web Audio that audio.js uses, rebuilt in C#: AudioParam automation (set / linear / exponential /
// target / cancel, a-rate inputs for LFOs), gain, biquad (Web Audio formulas), stereo panner, oscillators,
// buffer sources, a convolver stand-in and the master limiter. Pull-based, 128-frame render quanta, all on the
// main thread (AudioPump renders ahead into a looping AudioClip). Finished one-shot chains prune themselves.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Systems
{
    internal sealed class ABuffer
    {
        public readonly float[][] ch;
        public readonly int length, sampleRate;
        public ABuffer(int channels, int length, int sampleRate) { this.length = length; this.sampleRate = sampleRate; ch = new float[channels][]; for (int c = 0; c < channels; c++) ch[c] = new float[length]; }
        public float duration => (float)length / sampleRate;
    }

    internal sealed class AParam
    {
        enum K { Set, Lin, Exp, Target }
        struct Ev { public K k; public double t; public float v, tau; }

        readonly ACtx ctx;
        readonly List<Ev> ev = new List<Ev>();
        internal readonly List<ANode> ins = new List<ANode>();
        internal readonly float[] buf = new float[ACtx.QUANTUM];
        internal bool flat;              // this quantum's values are all buf[0] (no automation, no inputs)
        float v, lastV, tgt, tau, v0;
        double lastT = double.NaN, t0;
        bool tgtOn;

        public AParam(ACtx ctx, float v) { this.ctx = ctx; this.v = v; }

        public float value
        {
            get => v;
            set { if (ev.Count == 0) { v = value; tgtOn = false; } else SetValueAtTime(value, ctx.currentTime); }
        }

        void Add(Ev e) { int i = ev.Count; while (i > 0 && ev[i - 1].t > e.t) i--; ev.Insert(i, e); }
        void Anchor() { if (ev.Count == 0 && double.IsNaN(lastT)) { lastT = ctx.currentTime; lastV = v; } }
        public void SetValueAtTime(float value, double time) => Add(new Ev { k = K.Set, v = value, t = time });
        public void LinearRampToValueAtTime(float value, double time) { Anchor(); Add(new Ev { k = K.Lin, v = value, t = time }); }
        public void ExponentialRampToValueAtTime(float value, double time) { Anchor(); Add(new Ev { k = K.Exp, v = value, t = time }); }
        public void SetTargetAtTime(float target, double time, float timeConstant) => Add(new Ev { k = K.Target, v = target, t = time, tau = timeConstant });
        public void CancelScheduledValues(double time) { for (int i = ev.Count - 1; i >= 0; i--) if (ev[i].t >= time) ev.RemoveAt(i); }

        // Automation value at time t (t only moves forward). A ramp runs from the previous event to its own time.
        float Eval(double t)
        {
            while (ev.Count > 0)
            {
                var e = ev[0];
                if (t < e.t)
                {
                    if (e.k != K.Lin && e.k != K.Exp) break;
                    if (double.IsNaN(lastT)) { lastT = t; lastV = v; }
                    double span = e.t - lastT;
                    float k = span <= 0 ? 1 : (float)((t - lastT) / span);
                    v = e.k == K.Lin ? lastV + (e.v - lastV) * k
                        : lastV == 0 || lastV * e.v < 0 ? lastV : lastV * Mathf.Pow(e.v / lastV, k);
                    return v;
                }
                ev.RemoveAt(0);
                if (e.k == K.Target) { tgtOn = true; tgt = e.v; tau = e.tau; t0 = e.t; v0 = v; lastV = v; }
                else { tgtOn = false; v = e.v; lastV = e.v; }
                lastT = e.t;
            }
            if (tgtOn) v = tau <= 0 ? tgt : tgt + (v0 - tgt) * (float)Math.Exp(-(t - t0) / tau);
            return v;
        }

        // No event falls inside this quantum: the same curve as Eval, without a pow / exp per sample.
        void Steady(int n)
        {
            double t = ctx.t0, dt = ctx.dt;
            if (ev.Count > 0 && (ev[0].k == K.Lin || ev[0].k == K.Exp))   // a ramp on its way to ev[0]
            {
                var e = ev[0];
                if (double.IsNaN(lastT)) { lastT = t; lastV = v; }
                double span = e.t - lastT;
                if (span <= 0) { for (int i = 0; i < n; i++) buf[i] = e.v; }
                else if (e.k == K.Lin) { double k = (t - lastT) / span, dk = dt / span; for (int i = 0; i < n; i++) buf[i] = lastV + (e.v - lastV) * (float)(k + i * dk); }
                else if (lastV == 0 || lastV * e.v < 0) { for (int i = 0; i < n; i++) buf[i] = lastV; }
                else
                {
                    double r = (double)e.v / lastV, x = lastV * Math.Pow(r, (t - lastT) / span), q = Math.Pow(r, dt / span);
                    for (int i = 0; i < n; i++) { buf[i] = (float)x; x *= q; }
                }
            }
            else if (tgtOn)
            {
                if (tau <= 0) { for (int i = 0; i < n; i++) buf[i] = tgt; }
                else
                {
                    double x = (v0 - tgt) * Math.Exp(-(t - t0) / tau), f = Math.Exp(-dt / tau);
                    for (int i = 0; i < n; i++) { buf[i] = tgt + (float)x; x *= f; }
                }
            }
            else for (int i = 0; i < n; i++) buf[i] = v;   // a set waiting in the future: hold
            v = buf[n - 1];
        }

        // Per-sample values of this quantum: automation plus connected inputs (LFOs), down-mixed to mono.
        internal void Fill(int n)
        {
            // A target curve that has arrived (nothing queued after it) stops costing an exp per sample.
            if (tgtOn && ev.Count == 0 && Mathf.Abs(v - tgt) <= 1e-6f * Mathf.Max(1, Mathf.Abs(tgt))) { tgtOn = false; v = tgt; }
            flat = ev.Count == 0 && !tgtOn;
            if (flat) { for (int i = 0; i < n; i++) buf[i] = v; }
            else if (ev.Count == 0 || ev[0].t > ctx.t0 + (n - 1) * ctx.dt) Steady(n);
            else for (int i = 0; i < n; i++) buf[i] = Eval(ctx.t0 + i * ctx.dt);
            if (ins.Count > 0) flat = false;
            for (int j = ins.Count - 1; j >= 0; j--)
            {
                var s = ins[j]; s.Pull(n);
                if (s.stereo) for (int i = 0; i < n; i++) buf[i] += (s.L[i] + s.R[i]) * 0.5f;
                else for (int i = 0; i < n; i++) buf[i] += s.L[i];
                if (s.Ended) { ins.RemoveAt(j); s.Unlink(this); }
            }
        }
    }

    internal abstract class ANode
    {
        protected readonly ACtx ctx;
        internal readonly List<ANode> ins = new List<ANode>();
        readonly List<object> outs = new List<object>();
        internal readonly float[] L = new float[ACtx.QUANTUM], R = new float[ACtx.QUANTUM];
        internal bool stereo, keep;   // keep: a bus or group output, never pruned (disconnected explicitly)
        bool hadIn;
        int stamp = -1;

        protected ANode(ACtx ctx) { this.ctx = ctx; }

        // Nothing will come out of it again: a stopped source, or a chain node whose inputs all ended.
        internal virtual bool Ended => !keep && hadIn && ins.Count == 0;

        public ANode Connect(ANode n) { n.ins.Add(this); n.hadIn = true; outs.Add(n); return n; }
        public void Connect(AParam p) { p.ins.Add(this); outs.Add(p); }
        public void Disconnect()
        {
            foreach (var o in outs) { if (o is ANode n) n.ins.Remove(this); else ((AParam)o).ins.Remove(this); }
            outs.Clear();
        }
        internal void Unlink(object to) => outs.Remove(to);

        internal void Pull(int n)
        {
            if (stamp == ctx.block) return;
            stamp = ctx.block;
            Array.Clear(L, 0, n); Array.Clear(R, 0, n); stereo = false;
            for (int j = ins.Count - 1; j >= 0; j--)
            {
                var s = ins[j]; s.Pull(n);
                for (int i = 0; i < n; i++) { L[i] += s.L[i]; R[i] += s.R[i]; }   // mono nodes keep R = L
                stereo |= s.stereo;
                if (s.Ended) { ins.RemoveAt(j); s.Unlink(this); }
            }
            Process(n);
        }

        protected virtual void Process(int n) { }
    }

    internal sealed class AGain : ANode
    {
        public readonly AParam gain;
        public AGain(ACtx c, float v) : base(c) { gain = new AParam(c, v); }
        protected override void Process(int n)
        {
            gain.Fill(n); var g = gain.buf;
            for (int i = 0; i < n; i++) { L[i] *= g[i]; R[i] *= g[i]; }
        }
    }

    internal sealed class ABiquad : ANode
    {
        public string type;
        public readonly AParam frequency, Q;
        float b0, b1, b2, a1, a2, lx1, lx2, ly1, ly2, rx1, rx2, ry1, ry2;
        public ABiquad(ACtx c, string type, float freq, float q) : base(c) { this.type = type; frequency = new AParam(c, freq); Q = new AParam(c, q); }

        void Coefs(float f, float q)
        {
            float sr = ctx.sampleRate, f0 = Mathf.Clamp(f, 0, sr / 2), w0 = 2 * Mathf.PI * f0 / sr, cw = Mathf.Cos(w0), sw = Mathf.Sin(w0);
            float c0, c1, c2, d0, d1, d2;
            if (type == "bandpass")
            {
                float alpha = sw / (2 * Mathf.Max(q, 1e-4f));
                c0 = alpha; c1 = 0; c2 = -alpha; d0 = 1 + alpha; d1 = -2 * cw; d2 = 1 - alpha;
            }
            else
            {
                float alpha = sw / (2 * Mathf.Pow(10, q / 20));   // lowpass / highpass: Q in dB
                bool hp = type == "highpass";
                c0 = hp ? (1 + cw) / 2 : (1 - cw) / 2; c1 = hp ? -(1 + cw) : 1 - cw; c2 = c0;
                d0 = 1 + alpha; d1 = -2 * cw; d2 = 1 - alpha;
            }
            b0 = c0 / d0; b1 = c1 / d0; b2 = c2 / d0; a1 = d1 / d0; a2 = d2 / d0;
        }

        protected override void Process(int n)
        {
            frequency.Fill(n); Q.Fill(n);
            Coefs(frequency.buf[0], Q.buf[0]);   // coefficients per quantum
            for (int i = 0; i < n; i++)
            {
                float x = L[i], y = b0 * x + b1 * lx1 + b2 * lx2 - a1 * ly1 - a2 * ly2;
                if (Mathf.Abs(y) < 1e-20f) y = 0;
                lx2 = lx1; lx1 = x; ly2 = ly1; ly1 = y; L[i] = y;
                x = R[i]; y = b0 * x + b1 * rx1 + b2 * rx2 - a1 * ry1 - a2 * ry2;
                if (Mathf.Abs(y) < 1e-20f) y = 0;
                rx2 = rx1; rx1 = x; ry2 = ry1; ry1 = y; R[i] = y;
            }
        }
    }

    internal sealed class APanner : ANode
    {
        public readonly AParam pan;
        public APanner(ACtx c, float p) : base(c) { pan = new AParam(c, p); }
        protected override void Process(int n)
        {
            pan.Fill(n);
            float gc = 0, gs = 0, pf = 0;
            for (int i = 0; i < n; i++)
            {
                float l = L[i], r = R[i];
                if (i == 0 || !pan.flat)
                {
                    pf = Mathf.Clamp(pan.buf[i], -1, 1);
                    float x = !stereo ? (pf + 1) / 2 : pf <= 0 ? pf + 1 : pf;
                    gc = Mathf.Cos(x * Mathf.PI / 2); gs = Mathf.Sin(x * Mathf.PI / 2);
                }
                if (!stereo) { L[i] = l * gc; R[i] = l * gs; }
                else if (pf <= 0) { L[i] = l + r * gc; R[i] = r * gs; }
                else { L[i] = l * gc; R[i] = r + l * gs; }
            }
            stereo = true;
        }
    }

    internal abstract class ASource : ANode
    {
        protected double startAt = double.PositiveInfinity, stopAt = double.PositiveInfinity;
        protected bool done;
        protected ASource(ACtx c) : base(c) { }
        internal override bool Ended => done || ctx.t1 >= stopAt;
        public void Stop(double when) { if (!double.IsInfinity(startAt)) stopAt = Math.Max(when, ctx.currentTime); }
        public void Stop() => Stop(ctx.currentTime);
        protected bool Live(double t) => t >= startAt && t < stopAt;
    }

    internal sealed class AOsc : ASource
    {
        public string type = "sine";
        public readonly AParam frequency, detune;
        double phase;
        public AOsc(ACtx c) : base(c) { frequency = new AParam(c, 440); detune = new AParam(c, 0); }
        public void Start(double when) => startAt = Math.Max(when, 0);

        static readonly float[] SIN = MakeSin();
        static float[] MakeSin() { var t = new float[4097]; for (int i = 0; i <= 4096; i++) t[i] = (float)Math.Sin(2 * Math.PI * i / 4096); return t; }
        static float Sine(float ph) { float x = ph * 4096; int i = (int)x; if (i < 0) i = 0; else if (i > 4095) i = 4095; return SIN[i] + (SIN[i + 1] - SIN[i]) * (x - i); }

        static float Blep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1; }
            if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
            return 0;
        }

        protected override void Process(int n)
        {
            frequency.Fill(n); detune.Fill(n);
            float dm = detune.flat ? (detune.buf[0] == 0 ? 1 : Mathf.Pow(2, detune.buf[0] / 1200)) : 1;
            for (int i = 0; i < n; i++)
            {
                double t = ctx.t0 + i * ctx.dt;
                if (!Live(t)) { L[i] = R[i] = 0; continue; }
                float d = detune.buf[i], f = frequency.buf[i] * (detune.flat ? dm : d == 0 ? 1 : Mathf.Pow(2, d / 1200)), inc = Mathf.Clamp(f / ctx.sampleRate, -0.5f, 0.5f);
                float ph = (float)phase, a = Mathf.Abs(inc), s;
                switch (type)
                {
                    case "square": s = (ph < 0.5f ? 1 : -1) + Blep(ph, a) - Blep((ph + 0.5f) % 1, a); break;
                    case "sawtooth": { float q = (ph + 0.5f) % 1; s = 2 * q - 1 - Blep(q, a); break; }   // 0 at phase 0, rising
                    case "triangle": s = ph < 0.25f ? 4 * ph : ph < 0.75f ? 2 - 4 * ph : 4 * ph - 4; break;
                    default: s = Sine(ph); break;
                }
                L[i] = R[i] = s;
                phase += inc; phase -= Math.Floor(phase);
            }
        }
    }

    internal sealed class ABufferSource : ASource
    {
        public ABuffer buffer;
        public bool loop;
        double offset;
        public ABufferSource(ACtx c) : base(c) { }
        public void Start(double when, double offsetSeconds) { startAt = Math.Max(when, 0); offset = Math.Max(0, offsetSeconds); }

        protected override void Process(int n)
        {
            stereo = true;
            int len = buffer?.length ?? 0, sr = ctx.sampleRate;
            long idx = -1;
            float[] cl = len > 0 ? buffer.ch[0] : null, cr = len > 0 ? buffer.ch[buffer.ch.Length > 1 ? 1 : 0] : null;
            for (int i = 0; i < n; i++)
            {
                double t = ctx.t0 + i * ctx.dt;
                if (len == 0 || done || !Live(t)) { L[i] = R[i] = 0; idx = -1; continue; }
                if (idx < 0) { idx = (long)Math.Floor((offset + (t - startAt)) * sr + 0.5); if (loop) idx %= len; }
                else if (++idx >= len && loop) idx = 0;
                if (idx >= len) { done = true; L[i] = R[i] = 0; continue; }
                L[i] = cl[idx]; R[i] = cr[idx];
            }
        }
    }

    // ponytail: Schroeder reverb (4 combs + 2 allpasses per channel) tuned to the 2.6 s noise impulse audio.js builds
    // (RT60 and the Web Audio convolver's normalised energy); a real ConvolverNode port needs FFT convolution.
    internal sealed class AConvolver : ANode
    {
        static readonly int[] COMB = { 1557, 1617, 1491, 1422 }, AP = { 556, 225 };
        readonly float[][] cl, cr, al, ar;
        readonly int[] ci = new int[8], ai = new int[4];
        readonly float[] g = new float[4];
        readonly float scale;

        public AConvolver(ACtx c, float seconds) : base(c)
        {
            keep = true;
            float k = c.sampleRate / 44100f, rt60 = seconds * 0.9f;
            cl = new float[4][]; cr = new float[4][]; al = new float[2][]; ar = new float[2][];
            float sumG = 0;
            for (int i = 0; i < 4; i++)
            {
                int d = Mathf.Max(1, Mathf.RoundToInt(COMB[i] * k));
                cl[i] = new float[d]; cr[i] = new float[d + Mathf.RoundToInt(23 * k)];
                g[i] = Mathf.Pow(10, -3f * d / (rt60 * c.sampleRate)); sumG += 1 - g[i] * g[i];
            }
            for (int i = 0; i < 2; i++) { int d = Mathf.Max(1, Mathf.RoundToInt(AP[i] * k)); al[i] = new float[d]; ar[i] = new float[d + Mathf.RoundToInt(23 * k)]; }
            // Web Audio normalises the impulse: energy gain = (0.00125 * 44100 / sr)^2 * length.
            float cal = 0.00125f * 44100f / c.sampleRate, energy = cal * cal * Mathf.Floor(seconds * c.sampleRate);
            scale = Mathf.Sqrt(energy * (sumG / 4) / 4);
        }

        float Run(float x, float[][] combs, float[][] aps, int o)
        {
            float y = 0;
            for (int i = 0; i < 4; i++)
            {
                var b = combs[i]; int j = ci[o + i]; float d = b[j];
                b[j] = x + g[i] * d; y += d; ci[o + i] = j + 1 == b.Length ? 0 : j + 1;
            }
            for (int i = 0; i < 2; i++)
            {
                var b = aps[i]; int j = ai[o / 2 + i]; float d = b[j], w = y + 0.5f * d;
                b[j] = w; y = d - 0.5f * w; ai[o / 2 + i] = j + 1 == b.Length ? 0 : j + 1;
            }
            return y;
        }

        protected override void Process(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float l = L[i], r = stereo ? R[i] : L[i];
                L[i] = Run(l, cl, al, 0) * scale; R[i] = Run(r, cr, ar, 4) * scale;
            }
            stereo = true;
        }
    }

    // DynamicsCompressorNode as audio.js sets it (a limiter): soft-knee curve, attack/release on the gain
    // reduction and the browsers' automatic make-up gain ((1 / gain at 0 dB)^0.6).
    internal sealed class ALimiter : ANode
    {
        readonly float threshold, knee, ratio, aA, aR, makeup, under;
        float env;
        public ALimiter(ACtx c, float threshold, float knee, float ratio, float attack, float release) : base(c)
        {
            keep = true; this.threshold = threshold; this.knee = knee; this.ratio = ratio;
            aA = Mathf.Exp(-1 / (attack * c.sampleRate)); aR = Mathf.Exp(-1 / (release * c.sampleRate));
            makeup = Mathf.Pow(10, -Curve(0) * 0.6f / 20);
            under = Mathf.Pow(10, (threshold - knee / 2) / 20);   // below this level the curve is 0 dB
        }

        float Curve(float x)   // gain change in dB for an input level x dB
        {
            float over = x - threshold;
            if (2 * over < -knee) return 0;
            if (2 * Mathf.Abs(over) <= knee) { float a = over + knee / 2; return (1 / ratio - 1) * a * a / (2 * knee); }
            return threshold + over / ratio - x;
        }

        protected override void Process(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float det = Mathf.Max(Mathf.Abs(L[i]), Mathf.Abs(R[i]));
                float gr = det < under ? 0 : Curve(det > 1e-6f ? 20 * Mathf.Log10(det) : -120);
                env = gr < env ? aA * env + (1 - aA) * gr : aR * env + (1 - aR) * gr;
                if (env > -1e-5f) env = 0;
                float k = env == 0 ? makeup : Mathf.Pow(10, env / 20) * makeup;
                L[i] *= k; R[i] *= k;
            }
        }
    }

    internal sealed class ACtx
    {
        public const int QUANTUM = 128;
        public readonly int sampleRate;
        public readonly ANode destination;
        public string state = "running";
        public Action onQuantum;   // setTimeout stand-in: runs before each render quantum, on the audio clock
        long frames;
        internal int block;
        internal double t0, t1, dt;

        sealed class Dest : ANode { public Dest(ACtx c) : base(c) { keep = true; } }

        public ACtx(int sampleRate)
        {
            if (sampleRate <= 0) throw new ArgumentException("no audio output");
            this.sampleRate = sampleRate; dt = 1.0 / sampleRate;
            destination = new Dest(this);
        }

        public double currentTime => frames / (double)sampleRate;

        // Interleaved stereo, clamped to [-1, 1].
        public void Render(float[] o, int n)
        {
            int w = 0;
            while (n > 0)
            {
                int k = Math.Min(QUANTUM, n);
                onQuantum?.Invoke();
                block++; t0 = currentTime; t1 = (frames + k) / (double)sampleRate;
                destination.Pull(k);
                for (int i = 0; i < k; i++) { o[w++] = Mathf.Clamp(destination.L[i], -1, 1); o[w++] = Mathf.Clamp(destination.R[i], -1, 1); }
                frames += k; n -= k;
            }
        }

        // Time lost to a stall: the clock moves on without rendering (events past it are consumed when next read).
        public void Skip(long n) { if (n > 0) frames += n; }

        public ABuffer CreateBuffer(int channels, int length) => new ABuffer(channels, length, sampleRate);
    }
}
