// Ports engine/fx.js: particle effects — fire, smoke, rain, dust motes, extinguisher powder — with procedural
// textures and camera-facing billboards. Implements Core.IFx; the engine creates it with Fx.Create.
// This file: textures, materials, fire and smoke. FxMore.cs: rain, dust and the extinguisher spray.
// Particle positions are JS space (local to the effect's group); vertex colours are linear like the JS iCol.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Systems.SysUtil;

namespace Gyeongbo.Systems
{
    public sealed partial class Fx : IFx
    {
        bool RM;
        readonly float Q;
        readonly Kit kit;
        readonly Dictionary<string, object> local = new Dictionary<string, object>();

        Fx(Kit kit, bool reduceMotion)
        {
            this.kit = kit; RM = reduceMotion;
            var q = kit?.Quality;
            Q = q == "high" ? 1 : q == "medium" ? 0.7f : q == "low" ? 0.45f : 1;
        }

        public static Fx Create(Kit kit, bool reduceMotion = false) => new Fx(kit, reduceMotion);

        public bool Stub => false;
        public bool ReduceMotion => RM;
        public void SetReduceMotion(bool v) => RM = v;

        T Memo<T>(string k, Func<T> make) where T : class
        {
            if (kit != null) return kit.Memo("fx:" + k, make);
            if (!local.TryGetValue(k, out var v)) local[k] = v = make();
            return (T)v;
        }

        static float Clamp01(float v) => Mathf.Min(1, Mathf.Max(0, v));
        static float Smooth(float a, float b, float v) { float t = Clamp01((v - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float Dt(float dt) => float.IsNaN(dt) ? 0 : Mathf.Min(dt, 0.05f);

        // ---- procedural textures ----
        static float Hash(float x, float y) { double s = Math.Sin(x * 127.1 + y * 311.7) * 43758.5453; return (float)(s - Math.Floor(s)); }
        internal static float Vnoise(float x, float y)
        {
            float xi = Mathf.Floor(x), yi = Mathf.Floor(y), xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }
        static float Fbm(float x, float y) => Vnoise(x, y) * 0.55f + Vnoise(x * 2.03f, y * 2.03f) * 0.3f + Vnoise(x * 4.11f, y * 4.11f) * 0.15f;
        static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);   // Uint8ClampedArray rounding

        // shade(u -1..1, v 0..1 up, cell) -> rgba 0..255; cell row 0 = bottom (texture flipY), like the canvas port.
        internal static Texture2D Pixels(int size, int cells, Func<float, float, int, Vector4> shade)
        {
            var c = new Paint(size, size); int n = size / cells;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int cell = ((cells - 1 - y / n) * cells) + x / n;
                var px = shade(((x % n) + 0.5f) / n * 2 - 1, 1 - ((y % n) + 0.5f) / n, cell);
                c.Pixels[y * size + x] = new Color32(B(px.x), B(px.y), B(px.z), B(px.w));
            }
            var t = c.ToTexture(true, true);
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        static readonly float[][] RAMP = { new[] { 0f, 90, 14, 4 }, new[] { 0.3f, 214, 62, 10 }, new[] { 0.6f, 255, 150, 44 }, new[] { 0.85f, 255, 222, 150 }, new[] { 1f, 255, 248, 228 } };
        static Vector3 Ramp(float h)
        {
            for (int i = 1; i < RAMP.Length; i++) if (h <= RAMP[i][0])
            {
                float[] a = RAMP[i - 1], b = RAMP[i]; float k = (h - a[0]) / (b[0] - a[0]);
                return new Vector3(a[1] + (b[1] - a[1]) * k, a[2] + (b[2] - a[2]) * k, a[3] + (b[3] - a[3]) * k);
            }
            var l = RAMP[RAMP.Length - 1];
            return new Vector3(l[1], l[2], l[3]);
        }

        // Four flame tongues: teardrop silhouettes torn by noise, hot white-yellow core fading to red edges.
        Texture2D FlameTex() => Memo("flame", () => Pixels(256, 2, (u, v, cell) =>
        {
            float s = cell * 17.31f, t = Clamp01((v - 0.04f) / 0.92f);
            float n = Fbm(u * 2.2f + s, v * 3.1f - s);
            float width = 1.48f * Mathf.Pow(t, 0.35f) * Mathf.Pow(1 - t, 0.75f);
            float d = Mathf.Abs(u + (n - 0.5f) * 0.6f * t) / Mathf.Max(width, 1e-3f);
            float a = Smooth(1, 0.15f, d) * Clamp01(1.2f - t * 0.95f + (n - 0.5f) * 0.9f) * Smooth(0, 0.1f, v);
            a = Clamp01(a);
            var rgb = Ramp(Clamp01(a * (1.05f - t * 0.6f)));
            return new Vector4(rgb.x, rgb.y, rgb.z, a * 255);
        }));
        // Four soft puffs with ragged, noisy edges (white; tinted per particle).
        internal Texture2D PuffTex() => Memo("puff", () => Pixels(256, 2, (u, v, cell) =>
        {
            float y = v * 2 - 1, s = cell * 9.7f, n = Fbm(u * 2.6f + s, y * 2.6f - s);
            float d = Hypot(u, y) * (1 + (n - 0.5f) * 0.7f);
            float a = Smooth(0.95f, 0.25f, d) * (0.45f + 0.55f * Fbm(u * 5 - s, y * 5 + s));
            return new Vector4(255, 255, 255, Clamp01(a) * 255);
        }));
        Texture2D GlowTex() => Memo("glow", () => Pixels(64, 1, (u, v, cell) =>
        {
            float d = Hypot(u, v * 2 - 1);
            return new Vector4(255, 255, 255, Mathf.Exp(-d * d * 5) * Smooth(1, 0.8f, d) * 255);
        }));

        // ---- billboard materials (patch(): transparent, no depth write, per-instance colour) ----
        // ponytail: needs Mat.Unlit/Lit to honour MatSpec.vertexColors (URP Particles shaders); see the report.
        Material FlameMat() => Memo("flameMat", () => Mat.Unlit(new MatSpec { map = FlameTex(), blending = "additive", transparent = true, depthWrite = false, vertexColors = true }));
        Material GlowMat() => Memo("glowMat", () => Mat.Unlit(new MatSpec { map = GlowTex(), blending = "additive", transparent = true, depthWrite = false, vertexColors = true }));
        internal Material SmokeMat() => Memo("smokeMat", () => Mat.Lit(new MatSpec { map = PuffTex(), transparent = true, depthWrite = false, vertexColors = true, roughness = 1, metalness = 0 }));

        FxBillboards Billboards(int max, Material m, int cells, GameObject parent, string name)
        {
            var b = new FxBillboards(max, m, cells, name);
            parent.Add(b.obj);
            return b;
        }

        static GameObject MakeGroup(string name, float x, float y, float z) { var g = J.Group(name); J.SetPos(g, x, y, z); return g; }

        // ---- fire: flame tongues + base glow + embers + flickering warm light ----
        public IFire Fire(FireOptions o) => new FireFx(this, o ?? new FireOptions());

        sealed class Flame { public float x, y, z, vy, age, life, s0, rot, spin, ph; public int frame; }
        sealed class Ember { public float x, y, z, vx, vy, vz, age, life, s, ph; }

        sealed class FireFx : IFire
        {
            readonly Fx fx;
            readonly float scale, radius, lightMul, unit;
            readonly int fmax, emax;
            readonly FxBillboards flames, embers, glow;
            readonly List<Flame> P = new List<Flame>();
            readonly List<Ember> E = new List<Ember>();
            readonly Vector3 bas;
            float level, shown, acc, eacc, t;
            public GameObject Group { get; }
            public Light Light { get; }
            public float Level => level;
            public void SetLevel(float v) => level = Clamp01(v);

            public FireFx(Fx fx, FireOptions o)
            {
                this.fx = fx;
                scale = o.scale; radius = o.radius * scale;
                Group = MakeGroup("fx-fire", o.x, o.y, o.z);
                fmax = Mathf.Max(1, JsRoundI(o.count * fx.Q)); emax = Mathf.Max(1, JsRoundI(26 * fx.Q));
                flames = fx.Billboards(fmax, fx.FlameMat(), 2, Group, "fx-flames");
                embers = fx.Billboards(emax, fx.GlowMat(), 1, Group, "fx-embers");
                glow = fx.Billboards(2, fx.GlowMat(), 1, Group, "fx-glow");
                flames.RenderOrder = 2; embers.RenderOrder = 3;
                if (o.light != 0 && fx.kit != null)
                {
                    Light = PointLight(fx.kit, J.Hex(0xff8a3c), 10 * scale, 2, out unit);
                    Group.Add(Light);
                    J.SetPos(Light.gameObject, 0, 0.45f * scale, 0);
                    // ponytail: three.js shadow.bias -0.002 has no Unity equivalent unit; default bias kept
                    Light.shadows = o.shadow ? LightShadows.Soft : LightShadows.None;
                }
                lightMul = o.light;
                bas = new Vector3(0, 0.1f * scale, 0);
                level = Clamp01(o.level); t = Rand() * 10;
                Update(0);
            }

            Flame Spawn(bool rm)
            {
                float a = Rand() * Mathf.PI * 2, d = Mathf.Sqrt(Rand()) * radius * (0.45f + 0.55f * shown);
                float sp = rm ? 0.6f : 1;
                return new Flame
                {
                    x = Mathf.Cos(a) * d, y = 0.02f * scale, z = Mathf.Sin(a) * d, vy = (0.85f + Rand() * 0.75f) * Mathf.Sqrt(scale) * (0.55f + 0.6f * shown) * sp,
                    age = 0, life = (0.5f + Rand() * 0.45f) / sp, s0 = (0.26f + Rand() * 0.22f) * scale * (0.55f + 0.6f * shown),
                    rot = (Rand() - 0.5f) * 0.5f, spin = rm ? 0 : (Rand() - 0.5f) * 0.9f, frame = (int)(Rand() * 4), ph = Rand() * 9,
                };
            }

            public void Update(float dt, Camera camera = null)
            {
                dt = Dt(dt); t += dt;
                bool rm = fx.RM;
                shown += (level - shown) * Mathf.Min(1, dt * 1.6f);
                acc += shown * fmax / 0.72f * (rm ? 0.55f : 1) * dt;
                while (acc >= 1) { acc--; if (P.Count < fmax) P.Add(Spawn(rm)); }
                flames.Begin();
                for (int i = P.Count - 1; i >= 0; i--)
                {
                    var p = P[i]; p.age += dt; float k = p.age / p.life;
                    if (k >= 1) { P.RemoveAt(i); continue; }
                    p.y += p.vy * dt; p.x *= 1 - dt * 1.3f; p.z *= 1 - dt * 1.3f; // tongues lean in as they rise
                    if (!rm) { p.x += Mathf.Sin(t * 6.1f + p.ph) * 0.12f * scale * dt; p.z += Mathf.Cos(t * 5.3f + p.ph) * 0.12f * scale * dt; }
                    p.rot += p.spin * dt;
                    float size = p.s0 * (0.75f + 0.7f * Mathf.Sin(Mathf.Min(k * 1.25f, 1) * Mathf.PI * 0.5f) - 0.45f * k * k);
                    float heat = 1 - k, a = Smooth(0, 0.1f, k) * Mathf.Pow(1 - k, 1.1f) * Mathf.Min(1, shown * 1.6f);
                    flames.Push(new Vector3(p.x, p.y, p.z), size, p.rot, p.frame, 1.7f * (0.7f + 0.3f * heat), 1.7f * (0.32f + 0.5f * heat * heat), 1.7f * (0.1f + 0.35f * heat * heat * heat), a); // under bloom, so tongues keep their shape
                }
                flames.End();
                glow.Begin();
                glow.Push(bas, radius * 4.2f, 0, 0, 1.0f, 0.42f, 0.14f, 0.38f * shown);
                glow.End();
                eacc += (shown > 0.25f ? (rm ? 2 : 7) * shown * scale : 0) * dt;
                while (eacc >= 1)
                {
                    eacc--;
                    if (E.Count < emax) E.Add(new Ember
                    {
                        x = (Rand() - 0.5f) * radius, y = 0.35f * scale, z = (Rand() - 0.5f) * radius,
                        vx = (Rand() - 0.5f) * 0.4f, vy = (1.1f + Rand()) * (rm ? 0.6f : 1), vz = (Rand() - 0.5f) * 0.4f, age = 0, life = 1.2f + Rand() * 1.4f, s = 0.022f + Rand() * 0.022f, ph = Rand() * 9,
                    });
                }
                embers.Begin();
                for (int i = E.Count - 1; i >= 0; i--)
                {
                    var e = E[i]; e.age += dt; float k = e.age / e.life;
                    if (k >= 1) { E.RemoveAt(i); continue; }
                    if (!rm) { e.vx += Mathf.Sin(t * 3 + e.ph) * 0.6f * dt; e.vz += Mathf.Cos(t * 2.6f + e.ph) * 0.6f * dt; }
                    e.vy *= 1 - 0.35f * dt; e.x += e.vx * dt; e.y += e.vy * dt; e.z += e.vz * dt;
                    float tw = rm ? 1 : 0.75f + 0.25f * Mathf.Sin(t * 17 + e.ph);
                    embers.Push(new Vector3(e.x, e.y, e.z), e.s, 0, 0, 3, 1.5f, 0.45f, (1 - k) * tw);
                }
                embers.End();
                if (Light != null)
                {
                    float f = rm ? 0.95f + 0.04f * Mathf.Sin(t * 1.7f)
                        : 0.8f + 0.09f * Mathf.Sin(t * 10.7f + Mathf.Sin(t * 3.1f) * 2) + 0.07f * Mathf.Sin(t * 23.3f) + 0.06f * (Vnoise(t * 9, 3.7f) - 0.5f) * 2;
                    // Intensity only: the light count stays the same.
                    Light.intensity = shown > 0.01f ? 60 * lightMul * scale * scale * shown * f * unit : 0;
                    if (!rm) J.SetPos(Light.gameObject, Mathf.Sin(t * 7.3f) * 0.03f * scale, 0.45f * scale + Mathf.Sin(t * 5.1f) * 0.04f * scale, Mathf.Cos(t * 6.7f) * 0.03f * scale);
                }
            }

            public void Dispose()
            {
                if (Group != null) UnityEngine.Object.Destroy(Group);
                flames.Dispose(); embers.Dispose(); glow.Dispose();
            }
        }

        // ---- smoke: buoyant puffs that rise, hit the ceiling and spread into a layer that thickens downwards ----
        public ISmoke Smoke(SmokeOptions o) => new SmokeFx(this, o ?? new SmokeOptions());

        sealed class Puff { public float x, y, z, vx, vy, vz, age, life, s0, grow, rot, spin, shade, layer, rise, size; public int frame; }

        sealed class SmokeFx : ISmoke
        {
            readonly Fx fx;
            readonly int max;
            readonly FxBillboards sys;
            readonly float ceiling, spread, radius;
            readonly RectXZ b;
            readonly Color col;
            readonly List<Puff> P = new List<Puff>();
            float density, shown, acc;
            public GameObject Group { get; }
            public float Density => density;
            public void SetDensity(float v) => density = Clamp01(v);

            public SmokeFx(Fx fx, SmokeOptions o)
            {
                this.fx = fx;
                Group = MakeGroup("fx-smoke", o.x, o.y, o.z);
                max = JsRoundI(o.count * fx.Q);
                sys = fx.Billboards(max, fx.SmokeMat(), 2, Group, "fx-smoke-puffs");
                sys.RenderOrder = 4; sys.sort = true; sys.lit = true;
                ceiling = o.ceiling; spread = o.spread; radius = o.radius; b = o.bounds;
                col = (o.color ?? J.Hex(0x34302d)).linear;   // THREE.Color(hex) is linear
                density = Clamp01(o.density);
                Update(0);
            }

            public void Update(float dt, Camera camera = null)
            {
                dt = Dt(dt);
                bool rm = fx.RM; float sp = rm ? 0.6f : 1;
                shown += (density - shown) * Mathf.Min(1, dt * 0.8f);
                float layerLow = ceiling - (0.25f + 1.4f * shown);
                acc += shown * max / 8.5f * (rm ? 0.6f : 1) * dt;
                while (acc >= 1)
                {
                    acc--;
                    if (P.Count >= max) break;
                    float a = Rand() * Mathf.PI * 2, d = Mathf.Sqrt(Rand()) * radius;
                    P.Add(new Puff
                    {
                        x = Mathf.Cos(a) * d, y = 0.15f, z = Mathf.Sin(a) * d, vx = (Rand() - 0.5f) * 0.12f, vy = 0.3f, vz = (Rand() - 0.5f) * 0.12f, age = 0,
                        life = (6 + Rand() * 4) / sp, s0 = 0.35f + Rand() * 0.3f, grow = 0.16f + Rand() * 0.12f, rot = Rand() * 6.3f, spin = rm ? 0 : (Rand() - 0.5f) * 0.3f,
                        frame = (int)(Rand() * 4), shade = 0.8f + Rand() * 0.3f, layer = layerLow + Rand() * (ceiling - layerLow), rise = (0.55f + Rand() * 0.35f) * sp,
                    });
                }
                for (int i = P.Count - 1; i >= 0; i--)
                {
                    var p = P[i]; p.age += dt;
                    if (p.age >= p.life) { P.RemoveAt(i); continue; }
                    float size = Mathf.Min(p.s0 + p.age * p.grow * sp, 2.4f);
                    if (p.y < ceiling - 0.35f) // plume: rise and widen
                    {
                        p.vy += (p.rise - p.vy) * dt * 1.5f;
                        p.vx += (Rand() - 0.5f) * 0.25f * dt * sp; p.vz += (Rand() - 0.5f) * 0.25f * dt * sp;
                    }
                    else // ceiling jet: stop rising, run outwards, sink slowly into the layer
                    {
                        p.vy *= Mathf.Exp(-3 * dt);
                        float r = Hypot(p.x, p.z); if (r == 0) r = 1e-3f;
                        float push = 0.45f * sp * Mathf.Max(0, 1 - r / spread);
                        p.vx += (p.x / r * push - p.vx) * dt; p.vz += (p.z / r * push - p.vz) * dt;
                        p.y += (p.layer - p.y) * dt * 0.25f;
                    }
                    p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt;
                    p.y = Mathf.Min(p.y, ceiling - size * 0.22f);
                    if (b != null) { p.x = Mathf.Min(b.x1, Mathf.Max(b.x0, p.x)); p.z = Mathf.Min(b.z1, Mathf.Max(b.z0, p.z)); }
                    p.rot += p.spin * dt; p.size = size;
                }
                sys.Begin();   // back to front happens when the quads are built for the camera
                foreach (var p in P)
                {
                    float k = p.age / p.life, a = Smooth(0, 0.12f, k) * Smooth(1, 0.55f, k) * (0.3f + 0.4f * shown);
                    sys.Push(new Vector3(p.x, p.y, p.z), p.size, p.rot, p.frame, col.r * p.shade, col.g * p.shade, col.b * p.shade, a);
                }
                sys.End();
            }

            public void Dispose()
            {
                if (Group != null) UnityEngine.Object.Destroy(Group);
                sys.Dispose();
            }
        }
    }
}
