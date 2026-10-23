// Ports engine/people-props.js: prop geometry for people, all in the shared vertex-PBR material (one draw call
// per prop). Hand props are built in hand space (origin at the wrist, rest orientation: fingers -y, palm toward
// the body, thumb forward). Body props are built in body rest space. The walker is skinned so its wheels can
// spin. Built in JS space; MeshBuilder.Build mirrors X.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using static Gyeongbo.People.PeopleGeo;

namespace Gyeongbo.People
{
    // Body props in rest space: P = { d, front(y) -> z, back(y) -> z, c, skin, skinChest, skinPelvis, m }.
    public sealed class BagOpts
    {
        public BodyDims d;
        public Func<float, float> front, back;
        public string c;
        public int skin, skinChest, skinPelvis;
        public Matrix4x4? m;
    }

    public static class PeopleProps
    {
        const float PI = Mathf.PI;
        static readonly float[] GLOW_RED = { 0.3f, 0, 7, 0 }, LENS = { 0.2f, 0, 5, 0 }, SCREEN = { 0.2f, 0, 1.6f, 0 }, CHROME = { 0.28f, 0.9f, 0, 8 };
        static readonly Dictionary<string, Mesh> memo = new Dictionary<string, Mesh>();
        static Mesh Once(string key, Func<Mesh> make)
        {
            if (!memo.TryGetValue(key, out var m) || m == null) memo[key] = m = make();
            return m;
        }
        static string F2(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

        // Point along a direction from the grip.
        static Vector3 Along(Vector3 g, Vector3 v, float t) => g + v * t;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // Wheel centred on the origin, axle along x. Hub has 5 light spokes so spinning reads.
        static void Wheel(MeshBuilder b, Matrix4x4 m, float r, float w, int skin, string hubC = "#8d9196")
        {
            Color tire = Col("#141416"), hub = Col(hubC), hubD = Col(hubC, 0.45f); float h = w / 2;
            var rows = new List<BodyRow>();
            foreach (var q in new[] { new[] { -h, 0.2f }, new[] { -h, 0.45f }, new[] { -h, 0.66f }, new[] { -h, 0.88f }, new[] { -h * 0.63f, 1 }, new[] { h * 0.63f, 1 }, new[] { h, 0.88f }, new[] { h, 0.66f }, new[] { h, 0.45f }, new[] { h, 0.2f } })
                rows.Add(new BodyRow { y = q[0], rx = r * q[1], k = q[1] });
            b.Loft(rows, new LoftOpts
            {
                seg = 20, cap = true, bone = skin, m = m * At(0, 0, 0, 0, 0, PI / 2),
                color = (ring, th, i, p) => (ring.k > 0.7f ? tire : Mathf.Cos(5 * th) > 0.15f && ring.k > 0.3f ? hub : hubD),
                pbrFn = (ring, th, i, p) => (ring.k > 0.7f ? PBR["rubber"] : PBR["plastic"]),
            });
        }

        // ---- hand props (s = +1 right hand, -1 left; k = hand scale) ----
        static Vector3 Grip(int s, float k) => V(-s * 0.012f * k, -0.072f * k, 0);

        public static Mesh PhoneGeo(int s, float k) => Once($"phone:{s}:{F2(k)}", () =>
        {
            var b = new MeshBuilder(false); float palm = 0.02f * k, cx = -s * (palm + 0.0045f), cy = -0.078f * k;
            b.RBox(0.0085f, 0.155f, 0.074f, 0.006f, new LoftOpts { p = 5, seg = 12, c = Col("#1c1d21"), pbr = PBR["glossy"], m = At(cx, cy - 0.0775f, -0.006f) });
            b.RBox(0.0012f, 0.145f, 0.066f, 0.004f, new LoftOpts { p = 6, seg = 10, c = Col("#a9ccff"), pbr = SCREEN, m = At(cx - s * 0.0043f, cy - 0.0725f, -0.006f) });
            return b.Build("phone");
        });

        public static Mesh BatonGeo(int s, float k) => Once($"baton:{s}:{F2(k)}", () => // traffic light baton (경광봉): black grip, glowing red tube
        {
            var b = new MeshBuilder(false); Vector3 g = Grip(s, k), v = V(0, -0.5f, -0.87f).normalized;
            b.Pipe(new[] { Along(g, v, -0.06f), Along(g, v, 0.12f) }, 0.016f, new LoftOpts { c = Col("#1a1a1c"), pbr = PBR["rubber"], seg = 10 });
            b.Pipe(new[] { Along(g, v, 0.12f), Along(g, v, 0.125f) }, 0.02f, new LoftOpts { c = Col("#d9d9d9"), pbr = PBR["plastic"], seg = 10 });
            b.Pipe(new[] { Along(g, v, 0.125f), Along(g, v, 0.5f) }, new[] { 0.018f, 0.017f }, new LoftOpts { c = Col("#ff2b1c"), pbr = GLOW_RED, seg = 10 });
            return b.Build("baton");
        });

        public static Mesh FlashlightGeo(int s, float k) => Once($"flash:{s}:{F2(k)}", () =>
        {
            var b = new MeshBuilder(false); Vector3 g = Grip(s, k), v = V(0, -0.3f, -0.95f).normalized;
            b.Pipe(new[] { Along(g, v, -0.05f), Along(g, v, 0.09f) }, 0.015f, new LoftOpts { c = Col("#2b2d31"), pbr = PBR["metal"], seg = 10 });
            b.Pipe(new[] { Along(g, v, 0.09f), Along(g, v, 0.11f), Along(g, v, 0.15f) }, new[] { 0.017f, 0.022f, 0.023f }, new LoftOpts { c = Col("#34373c"), pbr = PBR["metal"], seg = 12 });
            b.Pipe(new[] { Along(g, v, 0.15f), Along(g, v, 0.153f) }, 0.019f, new LoftOpts { c = Col("#fff1d2"), pbr = LENS, seg = 12 });
            return b.Build("flashlight");
        });

        // Hanging props: origin at the grip, built hanging straight down (Person keeps them vertical).
        public static Mesh WaterJugGeo() => Once("jug", () => // 10 L water container, about 70 % full
        {
            var b = new MeshBuilder(false); Color body = Col("#dfe8ec"), water = Col("#9fbccb");
            b.Pipe(new[] { V(0, -0.055f, 0.085f), V(0, -0.015f, 0.07f), V(0, 0, 0.035f), V(0, 0, -0.035f), V(0, -0.015f, -0.07f), V(0, -0.055f, -0.085f) }, 0.011f, new LoftOpts { c = body, pbr = PBR["plastic"], seg = 6 });
            b.RBox(0.14f, 0.3f, 0.24f, 0.03f, new LoftOpts { p = 6, seg = 14, m = At(0, -0.36f, 0), color = (r, th, i, p) => (p.y < 0.2f ? water : body), pbr = PBR["plastic"] });
            b.Loft(new[] { new BodyRow { y = -0.065f, rx = 0.022f, z = -0.085f }, new BodyRow { y = -0.035f, rx = 0.022f, z = -0.085f } }, new LoftOpts { seg = 12, cap = true, c = Col("#2f6db0"), pbr = PBR["plastic"] });
            return b.Build("waterJug");
        });

        public static Mesh BasketGeo(string c = "#c22f38") => Once($"basket:{c}", () => // mart hand basket with a few groceries
        {
            var b = new MeshBuilder(false); Color red = Col(c); float y0 = -0.38f, y1 = -0.17f;
            void Walls(float k, bool flip) => b.Loft(new[] { new BodyRow { y = y0, rx = 0.125f * k, rz = 0.19f * k, p = 7 }, new BodyRow { y = y1, rx = 0.135f * k, rz = 0.21f * k, p = 7 } }, new LoftOpts { seg = 24, c = red, pbr = PBR["grid"], flip = flip });
            Walls(1, false); Walls(0.95f, true);
            b.RBox(0.25f, 0.012f, 0.38f, 0.03f, new LoftOpts { p = 7, seg = 16, c = Col(c, 0.7f), pbr = PBR["plastic"], m = At(0, y0, 0) });
            var rim = new List<Vector3>();
            for (int i = 0; i <= 24; i++) { float a = 2 * PI * i / 24, s = Mathf.Sin(a), cc = Mathf.Cos(a); rim.Add(V(0.132f * Math.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / 7), y1, 0.207f * Math.Sign(cc) * Mathf.Pow(Mathf.Abs(cc), 2f / 7))); }
            b.Pipe(rim, 0.008f, new LoftOpts { c = Col(c, 0.85f), pbr = PBR["plastic"], seg = 5 });
            foreach (int s in new[] { -1, 1 }) b.Pipe(new[] { V(s * 0.135f, y1 + 0.01f, -0.07f), V(s * 0.09f, -0.06f, -0.06f), V(s * 0.02f, -0.012f, -0.05f), V(s * 0.012f, 0, -0.03f), V(s * 0.012f, 0, 0.03f), V(s * 0.02f, -0.012f, 0.05f), V(s * 0.09f, -0.06f, 0.06f), V(s * 0.135f, y1 + 0.01f, 0.07f) }, 0.008f, new LoftOpts { c = Col(c, 0.75f), pbr = PBR["plastic"], seg = 5 });
            b.RBox(0.16f, 0.05f, 0.11f, 0.01f, new LoftOpts { c = Col("#e8b33a"), pbr = PBR["paper"], m = At(0.03f, y0 + 0.012f, 0.08f) });
            foreach (float z in new[] { -0.12f, -0.03f })
            {
                var bottle = new[] { new BodyRow { y = y0 + 0.012f, rx = 0.048f }, new BodyRow { y = y0 + 0.24f, rx = 0.048f }, new BodyRow { y = y0 + 0.27f, rx = 0.018f }, new BodyRow { y = y0 + 0.29f, rx = 0.016f } };
                foreach (var r in bottle) { r.x = -0.06f; r.z = z; }
                b.Loft(bottle, new LoftOpts { seg = 12, cap = true, pbr = PBR["glossy"], color = (r, th, i, p) => (r.y > y0 + 0.25f ? Col("#2f6db0") : r.y > y0 + 0.1f && r.y < y0 + 0.16f ? Col("#3b8fd6") : Col("#d3e4ee")) });
            }
            return b.Build("basket");
        });

        // ---- root props (origin at the feet, forward -z) ----
        public static Mesh CartGeo() => Once("cart", () => // mart shopping cart, handle 1.0 m high, 0.4 m ahead
        {
            var b = new MeshBuilder(false); Color chrome = Col("#b9bdc3"), red = Col("#c9343c");
            var tilt = At(0, 0, 0, -PI / 2, 0, 0);
            void Shell(float k, bool flip) => b.Loft(new[] { new BodyRow { y = 0.45f, rx = 0.27f * k, rz = 0.2f * k, z = 0.78f, p = 7, a = 2.25f, ac = PI }, new BodyRow { y = 1.2f, rx = 0.22f * k, rz = 0.16f * k, z = 0.82f, p = 7, a = 2.25f, ac = PI } },
                new LoftOpts { seg = 24, c = chrome, pbr = CHROME, flip = flip, m = tilt });
            Shell(1, false); Shell(0.96f, true);
            foreach (var q in new[] { new[] { -0.45f, 0.54f, 0.4f, 0.58f }, new[] { -1.2f, 0.44f, 0.32f, 0.66f } }) b.RBox(q[1], q[2], 0.012f, 0.01f, new LoftOpts { p = 7, c = chrome, pbr = CHROME, m = At(0, q[3], q[0]) });
            foreach (int s in new[] { -1, 1 })
            {
                b.Pipe(new[] { V(s * 0.21f, 0.15f, -0.45f), V(s * 0.16f, 0.13f, -1.18f) }, 0.012f, new LoftOpts { c = chrome, pbr = PBR["metal"], seg = 6 });
                b.Pipe(new[] { V(s * 0.24f, 0.15f, -0.46f), V(s * 0.26f, 0.6f, -0.44f), V(s * 0.27f, 0.99f, -0.4f) }, 0.012f, new LoftOpts { c = chrome, pbr = PBR["metal"], seg = 6 });
                foreach (var xz in new[] { new[] { s * 0.2f, -0.47f }, new[] { s * 0.15f, -1.15f } })
                {
                    float x = xz[0], z = xz[1];
                    b.Pipe(new[] { V(x, 0.14f, z), V(x, 0.07f, z + 0.02f) }, 0.008f, new LoftOpts { c = Col("#555"), pbr = PBR["metal"], seg = 5 });
                    Wheel(b, At(x, 0.055f, z + 0.025f), 0.055f, 0.026f, 0, "#9b2b30");
                }
            }
            b.RBox(0.4f, 0.012f, 0.66f, 0.01f, new LoftOpts { p = 7, c = chrome, pbr = CHROME, m = At(0, 0.17f, -0.82f) });
            b.Pipe(new[] { V(-0.29f, 1.0f, -0.38f), V(0.29f, 1.0f, -0.38f) }, 0.017f, new LoftOpts { c = red, pbr = PBR["plastic"], seg = 10 });
            b.RBox(0.3f, 0.2f, 0.22f, 0.02f, new LoftOpts { c = Col("#d9e6ee"), pbr = PBR["glossy"], m = At(-0.08f, 0.5f, -0.7f) });
            b.RBox(0.22f, 0.12f, 0.3f, 0.02f, new LoftOpts { c = Col("#f2f0ea"), pbr = PBR["fabric"], m = At(0.12f, 0.5f, -0.95f) });
            b.RBox(0.2f, 0.08f, 0.14f, 0.01f, new LoftOpts { c = Col("#d8452e"), pbr = PBR["paper"], m = At(0.1f, 0.5f, -0.6f) });
            return b.Build("cart");
        });

        public static readonly float[][] WALKER_WHEELS = { new[] { -0.27f, 0.1f, -0.86f }, new[] { 0.27f, 0.1f, -0.86f }, new[] { -0.27f, 0.1f, -0.3f }, new[] { 0.27f, 0.1f, -0.3f } };
        public const float WALKER_R = 0.1f;

        // 4-wheel rollator, skinned to bones [frame, FL, FR, RL, RR]; handle grips at height hy, about 0.27 m ahead.
        // Its bindposes are the rest bones (frame at the origin, wheels at WALKER_WHEELS), Unity space.
        public static Mesh WalkerGeo(float hy) => Once($"walker:{F2(hy)}", () =>
        {
            var b = new MeshBuilder(true); Color paint = Col("#2c3e66"), dark = Col("#1c1d21"); float[] pp = { 0.35f, 0.6f, 0, 0 };
            var o = new LoftOpts { c = paint, pbr = pp, seg = 8, bone = 0 };
            LoftOpts With(Color c, float[] pbr) { var w = o.Copy(); w.c = c; w.pbr = pbr; return w; }
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * 0.26f;
                b.Pipe(new[] { V(x, 0.13f, -0.3f), V(x, hy - 0.06f, -0.33f), V(x, hy, -0.35f) }, 0.013f, o);
                b.Pipe(new[] { V(x, hy, -0.35f), V(x, hy + 0.004f, -0.21f) }, 0.018f, With(dark, PBR["rubber"]));
                b.Pipe(new[] { V(x, hy - 0.025f, -0.37f), V(x, 0.5f, -0.63f), V(x, 0.23f, -0.84f) }, 0.013f, o);
                b.Pipe(new[] { V(x, 0.23f, -0.84f), V(x, 0.1f, -0.86f) }, 0.01f, With(dark, PBR["metal"]));
                b.Pipe(new[] { V(x, 0.55f, -0.37f), V(x, 0.55f, -0.68f) }, 0.011f, o);
                b.Pipe(new[] { V(x, 0.3f, -0.31f), V(x, 0.33f, -0.74f) }, 0.01f, o);
                b.Pipe(new[] { V(x, hy - 0.015f, -0.345f), V(x, hy - 0.035f, -0.26f), V(x, hy - 0.028f, -0.22f) }, 0.006f, With(Col("#9aa0a6"), PBR["metal"]));
                b.Pipe(new[] { V(x, hy - 0.02f, -0.34f), V(x + s * 0.025f, hy - 0.15f, -0.32f), V(x + s * 0.02f, 0.22f, -0.3f) }, 0.003f, With(dark, PBR["rubber"]));
            }
            foreach (float z in new[] { -0.38f, -0.69f }) b.Pipe(new[] { V(-0.26f, 0.52f, z), V(0.26f, 0.52f, z) }, 0.011f, o);
            b.Pipe(new[] { V(-0.26f, 0.33f, -0.55f), V(0.26f, 0.5f, -0.55f) }, 0.009f, o); b.Pipe(new[] { V(-0.26f, 0.5f, -0.55f), V(0.26f, 0.33f, -0.55f) }, 0.009f, o);
            b.RBox(0.5f, 0.045f, 0.3f, 0.015f, new LoftOpts { p = 6, seg = 16, c = Col("#22232a"), pbr = PBR["fabric"], bone = 0, m = At(0, 0.555f, -0.53f) });
            b.RBox(0.52f, 0.075f, 0.03f, 0.012f, new LoftOpts { p = 6, seg = 16, c = Col("#22232a"), pbr = PBR["fabric"], bone = 0, m = At(0, hy - 0.17f, -0.335f) });
            b.RBox(0.36f, 0.17f, 0.26f, 0.02f, new LoftOpts { p = 7, seg = 16, c = Col("#3a3d44"), pbr = PBR["grid"], bone = 0, m = At(0, 0.31f, -0.53f) });
            for (int i = 0; i < WALKER_WHEELS.Length; i++) { var p = WALKER_WHEELS[i]; Wheel(b, At(p[0], p[1], p[2]), WALKER_R, 0.035f, i + 1); }
            var mesh = b.Build("walker");
            var bind = new Matrix4x4[1 + WALKER_WHEELS.Length];
            bind[0] = Matrix4x4.identity;
            for (int i = 0; i < WALKER_WHEELS.Length; i++) { var p = WALKER_WHEELS[i]; bind[i + 1] = Matrix4x4.Translate(new Vector3(p[0], -p[1], -p[2])); } // inverse of J.V(p)
            mesh.bindposes = bind;
            return mesh;
        });

        // ---- body props in rest space ----
        public static void Backpack(MeshBuilder b, BagOpts P)
        {
            BodyDims d = P.d; float H = d.H; Matrix4x4 m = P.m ?? Matrix4x4.identity; int sk = P.skin; Color main = Col(P.c ?? "#2b3242");
            float yb = d.waistY - 0.05f, bz = Math.Max(Math.Max(P.back(yb + 0.12f), P.back(d.chestY)), P.back(d.shY - 0.05f)) + 0.004f;
            b.RBox(0.29f, 0.42f, 0.14f, 0.045f, new LoftOpts { p = 3, seg = 16, c = main, pbr = PBR["nylon"], bone = sk, m = m * At(0, yb, bz + 0.07f) });
            b.RBox(0.23f, 0.19f, 0.05f, 0.03f, new LoftOpts { p = 3, seg = 12, c = Col(P.c ?? "#2b3242", 0.8f), pbr = PBR["nylon"], bone = sk, m = m * At(0, yb + 0.03f, bz + 0.155f) });
            b.Pipe(new[] { V(-0.1f, yb + 0.205f, bz + 0.17f), V(0.1f, yb + 0.205f, bz + 0.17f) }, 0.004f, new LoftOpts { c = Col("#111"), bone = sk, m = m, seg = 4 });
            b.Pipe(new[] { V(-0.04f, yb + 0.415f, bz + 0.07f), V(-0.02f, yb + 0.45f, bz + 0.07f), V(0.02f, yb + 0.45f, bz + 0.07f), V(0.04f, yb + 0.415f, bz + 0.07f) }, 0.007f, new LoftOpts { c = Col("#1d1e21"), bone = sk, m = m, seg = 5 });
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * 0.55f * d.shX;
                float f(float y, float e = 0.008f) => P.front(y) - e;
                b.Pipe(new[] { V(s * 0.08f, yb + 0.39f, bz + 0.01f), V(x, 0.836f * H + 0.012f, P.back(0.83f * H) * 0.6f), V(x * 1.04f, 0.846f * H + 0.01f, 0.004f * H), V(x * 1.06f, 0.82f * H, f(0.82f * H)),
                    V(x * 1.08f, 0.76f * H, f(0.76f * H)), V(x * 1.12f, 0.70f * H, f(0.70f * H, 0.006f)), V(s * 0.16f * H / 1.72f, 0.665f * H, f(0.665f * H) * 0.3f), V(s * 0.14f, yb + 0.03f, bz + 0.01f) }, 0.011f, new LoftOpts { c = Col("#1d1e21"), pbr = PBR["nylon"], bone = sk, m = m, seg = 6 });
            }
        }

        public static void Crossbody(MeshBuilder b, BagOpts P) // small bag on a strap from the right shoulder to the left hip
        {
            BodyDims d = P.d; float H = d.H; Color c = Col(P.c ?? "#3a2e28");
            float f(float y, float e = 0.006f) => P.front(y) - e;
            b.Pipe(new[] { V(0.1f, 0.846f * H + 0.01f, 0.0f), V(0.08f, 0.81f * H, f(0.81f * H)), V(0.0f, 0.72f * H, f(0.72f * H)), V(-0.08f, 0.63f * H, f(0.63f * H)), V(-0.12f, 0.575f * H, f(0.575f * H, 0.02f)) }, 0.006f, new LoftOpts { c = c, pbr = PBR["leather"], bone = P.skinChest, seg = 5 });
            b.RBox(0.19f, 0.14f, 0.055f, 0.02f, new LoftOpts { p = 4, seg = 12, c = c, pbr = PBR["leather"], bone = P.skinPelvis, m = At(-0.12f, 0.49f * H, f(0.53f * H, 0.03f)) });
        }
    }
}
