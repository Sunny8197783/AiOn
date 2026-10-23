// Ports engine/people-body.js: one person's body — anatomy rows (fractions of height H), clothing layers, hair,
// face planes and small accessories — lofted into a single mesh skinned to an 18-bone skeleton. Frame: feet at
// origin, facing -z, person's right hand on +x (JS space; MeshBuilder.Build mirrors X). Rest pose: arms hanging.
// Hair and shoes are in PeopleBodyParts.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Gyeongbo.People.PeopleGeo;

namespace Gyeongbo.People
{
    public sealed class BodyDims
    {
        public float H, g, hh, ankleY, kneeY, hipY, pelvisY, waistY, chestY, shY, neckY, headY, elY, wrY, shX, hipX, L1, L2, U1, U2;
        public bool f;
    }

    // buildBody() -> { b, d, J, rows, chestF } so props can be baked in before b.Build().
    public sealed class BodyBuild
    {
        public MeshBuilder b;
        public BodyDims d;
        public float[][] J;
        public List<BodyRow> rows;
        public Func<float, float?, float> chestF;   // (y, t = 0.006 H) -> z just in front of the chest layer
    }

    public static partial class PeopleBody
    {
        // Bone indices (people-body.js B).
        public static class B
        {
            public const int root = 0, pelvis = 1, spine = 2, chest = 3, neck = 4, head = 5, lArm = 6, lFore = 7, lHand = 8, rArm = 9, rFore = 10, rHand = 11, lThigh = 12, lShin = 13, lFoot = 14, rThigh = 15, rShin = 16, rFoot = 17;
        }
        public static readonly string[] BONE_NAMES = { "root", "pelvis", "spine", "chest", "neck", "head", "lArm", "lFore", "lHand", "rArm", "rFore", "rHand", "lThigh", "lShin", "lFoot", "rThigh", "rShin", "rFoot" };
        public static readonly int[] PARENT = { -1, 0, 1, 2, 3, 4, 3, 6, 7, 3, 9, 10, 1, 12, 13, 1, 15, 16 };

        const float PI = Mathf.PI;
        public static float Ss(float a, float b, float x) { float t = Math.Min(1f, Math.Max(0f, (x - a) / (b - a))); return t * t * (3 - 2 * t); }
        static float Gauss(float x, float s) => Mathf.Exp(-((x / s) * (x / s)));

        // Body measurements in metres from a spec { H, sex, girth }.
        public static BodyDims Dims(PersonSpec spec)
        {
            float H = spec.H, g = spec.girth; bool f = spec.sex == "f";
            var d = new BodyDims
            {
                H = H, f = f, g = g, hh = H / 7.5f,
                ankleY = 0.05f * H, kneeY = 0.285f * H, hipY = 0.53f * H, pelvisY = 0.565f * H, waistY = 0.635f * H, chestY = 0.75f * H,
                shY = (f ? 0.815f : 0.818f) * H, neckY = 0.852f * H, headY = 0.9f * H, elY = 0.63f * H, wrY = 0.485f * H,
                shX = (f ? 0.104f : 0.115f) * H * (0.55f + 0.45f * g), hipX = (f ? 0.056f : 0.052f) * H * (0.7f + 0.3f * g),
            };
            d.L1 = d.hipY - d.kneeY; d.L2 = d.kneeY - d.ankleY; d.U1 = d.shY - d.elY; d.U2 = d.elY - d.wrY;
            return d;
        }

        // Rest joint positions [x, y, z] per bone (root space, JS).
        public static float[][] Joints(BodyDims d)
        {
            var J = new float[18][];
            J[B.root] = new[] { 0f, 0, 0 }; J[B.pelvis] = new[] { 0, d.pelvisY, 0 }; J[B.spine] = new[] { 0, d.waistY, 0 }; J[B.chest] = new[] { 0, d.chestY, 0 };
            J[B.neck] = new[] { 0, d.neckY, 0.005f * d.H }; J[B.head] = new[] { 0, d.headY, 0 };
            foreach (int s in new[] { -1, 1 })
            {
                int a = s < 0 ? B.lArm : B.rArm, l = s < 0 ? B.lThigh : B.rThigh;
                J[a] = new[] { s * d.shX, d.shY, 0 }; J[a + 1] = new[] { s * d.shX, d.elY, 0 }; J[a + 2] = new[] { s * d.shX, d.wrY, 0 };
                J[l] = new[] { s * d.hipX, d.hipY, 0 }; J[l + 1] = new[] { s * d.hipX, d.kneeY, 0 }; J[l + 2] = new[] { s * d.hipX, d.ankleY, 0 };
            }
            return J;
        }

        // One joint of a descending chain: blend half-width w around y between the bone above and below.
        struct Seg { public float y, w; public int up, dn; public Seg(float y, int up, int dn, float w) { this.y = y; this.up = up; this.dn = dn; this.w = w; } }

        static float[] Chain(float y, Seg[] segs)
        {
            foreach (var j in segs)
            {
                if (y > j.y + j.w) return new float[] { j.up, 1 };
                if (y > j.y - j.w) { float t = Ss(j.y + j.w, j.y - j.w, y); return new float[] { j.up, 1 - t, j.dn, t }; }
            }
            return new float[] { segs[segs.Length - 1].dn, 1 };
        }

        static Func<Vector3, float[]> TorsoSkin(BodyDims d)
        {
            float H = d.H;
            return (p) =>
            {
                float x = p.x, y = p.y, ax = Math.Abs(x);
                if (y < d.pelvisY + 0.02f * H) return new float[] { B.pelvis, 1 };
                if (y < d.waistY + 0.03f * H) { float t = Ss(d.pelvisY + 0.02f * H, d.waistY + 0.03f * H, y); return new float[] { B.pelvis, 1 - t, B.spine, t }; }
                if (y < d.chestY) { float t = Ss(d.waistY + 0.03f * H, d.chestY, y); return new float[] { B.spine, 1 - t, B.chest, t }; }
                float arm = 0.55f * Ss(d.shX - 0.045f * H, d.shX + 0.005f * H, ax) * Ss(d.shY - 0.07f * H, d.shY - 0.01f * H, y);
                if (arm > 0.01f) return new float[] { B.chest, 1 - arm, x < 0 ? B.lArm : B.rArm, arm };
                if (y > d.neckY - 0.012f * H) return new float[] { B.chest, 0.6f, B.neck, 0.4f };
                return new float[] { B.chest, 1 };
            };
        }

        // Garment parts hanging below the hips follow both thighs (front/back stays between them).
        static Func<Vector3, float[]> SkirtSkin(BodyDims d, float hemY)
        {
            float top = d.hipY + 0.02f * d.H; var ts = TorsoSkin(d);
            return (p) =>
            {
                float k = 0.9f * Math.Min(1, Math.Max(0, (top - p.y) / Math.Max(0.01f, top - hemY)));
                if (k <= 0) return ts(p);
                float wx = Ss(-0.06f * d.H, 0.06f * d.H, p.x);
                return new float[] { B.pelvis, 1 - k, B.lThigh, k * (1 - wx), B.rThigh, k * wx };
            };
        }

        static Seg[] HeadChain(BodyDims d) => new[] { new Seg(d.headY - 0.006f * d.H, B.head, B.neck, 0.016f * d.H), new Seg(d.neckY, B.neck, B.chest, 0.014f * d.H) };

        static BodyRow Row(float y, float rx, float rz, float z = 0) => new BodyRow { y = y, rx = rx, rz = rz, z = z };

        // Torso surface rows (crotch -> neck base), fractions of H.
        static List<BodyRow> TorsoRows(BodyDims d, PersonSpec spec)
        {
            float H = d.H, g = d.g, bl = spec.belly; bool f = d.f;
            float sx = (f ? 0.9f : 1) * g, hx = (f ? 1.09f : 1) * (0.5f + 0.5f * g), wx = (f ? 0.86f : 1) * g;
            BodyRow R(float y, float rx, float rz, float z = 0, float p = 2.2f) => new BodyRow { y = y * H, rx = rx * H, rz = rz * H, z = z * H, p = p };
            return new List<BodyRow>
            {
                R(0.494f, 0.066f * hx, 0.05f, 0.004f), R(0.52f, 0.087f * hx, 0.058f, 0.007f), R(0.553f, 0.099f * hx, 0.062f, 0.007f),
                R(0.59f, 0.095f * hx, 0.06f + bl * 0.3f, 0.004f - bl * 0.3f), R(0.625f, 0.087f * wx, 0.057f + bl * 0.8f, -bl * 0.8f),
                R(0.66f, 0.089f * wx, 0.058f + bl, -0.002f - bl), R(0.70f, 0.093f * sx, 0.062f + bl * 0.5f, -0.003f - bl * 0.5f),
                R(0.74f, 0.099f * sx, f ? 0.069f : 0.066f, f ? -0.011f : -0.006f), R(0.775f, 0.103f * sx, f ? 0.064f : 0.065f, -0.005f),
                R(0.803f, 0.107f * sx, 0.061f, -0.001f, 2.6f), R(0.822f, 0.101f * sx, 0.054f, 0.002f, 2.8f), R(0.840f, 0.07f * sx, 0.044f, 0.004f, 2.4f),
                R(0.852f, f ? 0.036f : 0.04f, 0.036f, 0.005f),
            };
        }

        static readonly Dictionary<string, float> LEN = new Dictionary<string, float> { ["hip"] = 0.515f, ["waist"] = 0.56f, ["thigh"] = 0.45f, ["knee"] = 0.3f, ["calf"] = 0.2f };

        static float[] Pb(string n) => n != null && PBR.TryGetValue(n, out var v) ? v : PBR["fabric"];

        public static BodyBuild BuildBody(PersonSpec spec)
        {
            BodyDims d = Dims(spec); float[][] J = Joints(d); float H = d.H, hh = d.hh; var b = new MeshBuilder(true);
            Color skin = Col(spec.skin), hairC = Col(spec.hair?.c ?? "#1c1814");
            Garment top = spec.top, outer = spec.outer, bot = spec.bottom; Extras ex = spec.extras ?? new Extras();
            var rows = TorsoRows(d, spec); var ts = TorsoSkin(d);
            float Front(float y, float t = 0) { var r = RingAt(rows, y); return r.z.Value - r.rz.Value - t; }

            // ---- torso base layer: bottom garment below the top's hem, optional belt, top above ----
            bool tucked = top.kind == "shirt" || top.kind == "blouse";
            float hemTop = tucked ? 0.603f * H : 0.535f * H;
            var zones = new List<Zone> { new Zone(-1, Col(bot.c), Pb(bot.pbr ?? "fabric"), 0.004f * H) };
            if (tucked) zones.Add(new Zone(0.603f * H, Col(bot.belt ?? "#2a221d"), PBR["leather"], 0.005f * H));
            zones.Add(new Zone(tucked ? 0.618f * H : hemTop, Col(top.c), Pb(top.pbr), 0.004f * H));
            b.Loft(Zoned(rows, zones), new LoftOpts { seg = 24, cap = true, skin = ts });
            if (bot.kind == "skirt") // A-line skirt over the hips
            {
                float hem = (bot.lenF ?? 0.33f) * H;
                var sk = new List<BodyRow> { Row(hem, 0.113f * H, 0.074f * H, 0.004f * H), Row(0.49f * H, 0.106f * H, 0.069f * H, 0.006f * H),
                    Row(0.553f * H, 0.102f * H, 0.066f * H, 0.007f * H), Row(0.61f * H, 0.092f * H * d.g, 0.062f * H, 0.002f * H) };
                foreach (var r in sk) r.p = 2.1f;
                b.Loft(sk, new LoftOpts { seg = 24, c = Col(bot.c), pbr = Pb(bot.pbr ?? "fabric"), skin = SkirtSkin(d, hem) });
            }
            if (top.kind == "shirt" && !(outer != null && outer.kind != "vest" && !outer.open)) // shirt collar
            {
                var r0 = Row(0.842f * H, 0.046f * H, 0.042f * H, 0.004f * H); r0.a = PI - 0.42f;
                var r1 = Row(0.866f * H, 0.042f * H, 0.04f * H, 0.008f * H); r1.a = PI - 0.5f;
                b.Loft(new[] { r0, r1 }, new LoftOpts { seg = 16, c = Col(top.collar ?? top.c), pbr = Pb(top.pbr), skin = ts });
            }
            if (top.kind == "hoodie")
            {
                b.Blob(new Vector3(0, 0.852f * H, 0.05f * H), new Vector3(0.07f * H, 0.032f * H, 0.036f * H), new LoftOpts { c = Col(top.c), pbr = PBR["knit"], skin = ts, seg = 12 });
                foreach (int s in new[] { -1, 1 }) b.Pipe(new[] { new Vector3(s * 0.016f * H, 0.842f * H, Front(0.84f * H, 0.006f * H)), new Vector3(s * 0.017f * H, 0.79f * H, Front(0.79f * H, 0.006f * H)) }, 0.0022f * H, new LoftOpts { c = Col("#e8e4dc"), bone = B.chest, seg = 5 });
            }

            // ---- outer garment ----
            if (outer != null) BuildOuter(b, d, rows, outer, ts);

            // ---- neck, head, face, hair ----
            Func<Vector3, float[]> hs = (p) => Chain(p.y, HeadChain(d));
            b.Loft(new[] { Row(0.836f * H, (d.f ? 0.029f : 0.033f) * H, 0.031f * H, 0.006f * H), Row(0.875f * H, (d.f ? 0.027f : 0.031f) * H, 0.03f * H, 0.004f * H),
                Row(0.91f * H, (d.f ? 0.027f : 0.03f) * H, 0.03f * H, 0) }, new LoftOpts { seg = 14, c = skin, pbr = PBR["skin"], skin = hs });
            float y0 = H - hh;
            BodyRow HRow(float f) { var r = RingAt(HEAD_ROWS, f); return new BodyRow { y = y0 + f * hh, rx = r.rx * hh, rz = r.rz * hh, z = r.z * hh }; }
            Color lipC = skin * new Color(1.06f, 0.78f, 0.76f, 1), browC = Color.LerpUnclamped(skin, hairC, 0.45f);
            b.Loft(HEAD.Select(h => { var r = HRow(h[0]); r.f = h[0]; r.p = h[0] < 0.2f ? 2.3f : 2.05f; return r; }).ToList(), new LoftOpts
            {
                seg = 26, pbr = PBR["skin"], skin = hs,
                color = (r, th, i, p) => // faceless but with soft planes: eye sockets, brow line, lips
                {
                    float fr = -Mathf.Cos(th); if (fr < 0.5f) return skin;
                    float sx = Mathf.Abs(Mathf.Sin(th)), k = Ss(0.5f, 0.85f, fr);
                    Color c = skin * (1 - 0.2f * k * Gauss(r.f - 0.5f, 0.03f) * Gauss(sx - 0.4f, 0.14f));
                    c = Color.LerpUnclamped(c, browC, k * Gauss(r.f - 0.565f, 0.025f) * Gauss(sx - 0.38f, 0.17f));
                    return Color.LerpUnclamped(c, lipC, 0.7f * k * Gauss(r.f - 0.185f, 0.03f) * Gauss(sx, 0.16f));
                },
            });
            if (ex.mask == null) b.Loft(new[] { Row(y0 + 0.29f * hh, 0.04f * hh, 0.03f * hh, -0.45f * hh), Row(y0 + 0.31f * hh, 0.065f * hh, 0.07f * hh, -0.45f * hh),
                Row(y0 + 0.38f * hh, 0.045f * hh, 0.05f * hh, -0.445f * hh), Row(y0 + 0.48f * hh, 0.02f * hh, 0.02f * hh, -0.43f * hh) }, new LoftOpts { seg = 8, cap = true, c = skin, pbr = PBR["skin"], bone = B.head });
            foreach (int s in new[] { -1, 1 }) b.Blob(new Vector3(s * 0.33f * hh, y0 + 0.45f * hh, 0.02f * hh), new Vector3(0.04f * hh, 0.13f * hh, 0.08f * hh), new LoftOpts { c = skin * 0.95f, pbr = PBR["skin"], bone = B.head, seg = 8, rings = 6 });
            Hair(b, spec.hair ?? new Garment { style = "short" }, HRow, hh, y0, hairC, hs, ex.cap != null);
            if (ex.cap != null)
            {
                var capRows = new[] { 0.6f, 0.7f, 0.82f, 0.92f, 1f }.Select(f => { var r = HRow(f); float t = 0.075f * hh; r.rx += t; r.rz += t; return r; }).ToList();
                capRows.Add(Row(H + 0.07f * hh, 0.02f * hh, 0.02f * hh, 0.01f * hh));
                b.Loft(capRows, new LoftOpts { seg = 22, c = Col(ex.cap), pbr = PBR["fabric"], bone = B.head });
                b.RBox(0.78f * hh, 0.05f * hh, 0.36f * hh, 0.15f * hh, new LoftOpts { p = 2.2f, seg = 16, c = Col(ex.cap) * 0.8f, pbr = PBR["fabric"], bone = B.head, m = At(0, y0 + 0.63f * hh, -0.6f * hh, -0.12f) });
            }
            if (ex.mask != null) b.Loft(new[] { 0.08f, 0.15f, 0.25f, 0.33f, 0.42f, 0.47f }.Select(f =>
            {
                var r = HRow(f); float t = (f > 0.25f && f < 0.45f ? 0.07f : 0.035f) * hh;
                r.rx += t; r.rz += t; r.z -= 0.01f * hh; r.a = 1.25f; r.ac = PI; return r;
            }).ToList(), new LoftOpts { seg = 12, c = Col(ex.mask), pbr = PBR["fabric"], bone = B.head });
            if (ex.glasses != null)
            {
                Color gc = Col(ex.glasses); float fz = -0.47f * hh, gy = y0 + 0.5f * hh;
                var go = new LoftOpts { c = gc, pbr = PBR["glossy"], bone = B.head, seg = 4 };
                foreach (int s in new[] { -1, 1 })
                {
                    var ring = new List<Vector3>(); for (int k = 0; k <= 12; k++) { float a = 2 * PI * k / 12; ring.Add(new Vector3(s * 0.14f * hh + Mathf.Cos(a) * 0.1f * hh, gy + Mathf.Sin(a) * 0.07f * hh, fz)); }
                    b.Pipe(ring, 0.006f * hh, go);
                    b.Pipe(new[] { new Vector3(s * 0.24f * hh, gy + 0.03f * hh, fz + 0.01f * hh), new Vector3(s * 0.35f * hh, gy + 0.03f * hh, -0.15f * hh), new Vector3(s * 0.35f * hh, gy, 0.08f * hh) }, 0.006f * hh, go);
                }
                b.Pipe(new[] { new Vector3(-0.04f * hh, gy + 0.02f * hh, fz), new Vector3(0.04f * hh, gy + 0.02f * hh, fz) }, 0.006f * hh, go);
            }

            BuildLimbs(b, d, spec, skin);

            // ---- small accessories baked into the body ----
            float ChestF(float y, float? t = null) => Front(y, (outer != null ? (outer.kind == "padded" ? 0.02f : 0.01f) * H : 0.004f * H) + (t ?? 0.006f * H));
            if (ex.scarf != null)
            {
                Color sc = Col(ex.scarf); float[] sp = PBR["check"];
                b.Loft(new[] { Row(0.83f * H, 0.06f * H, 0.056f * H, 0.002f * H), Row(0.848f * H, 0.062f * H, 0.058f * H, 0.002f * H), Row(0.87f * H, 0.05f * H, 0.047f * H, 0.006f * H) }, new LoftOpts { seg = 18, cap = true, c = sc, pbr = sp, skin = hs });
                b.RBox(0.058f * H, 0.14f * H, 0.012f * H, 0.008f * H, new LoftOpts { p = 4, seg = 10, c = sc, pbr = sp, bone = B.chest, m = At(-0.026f * H, 0.70f * H, ChestF(0.76f * H, 0.012f * H), 0, 0, 0.08f) });
            }
            if (ex.lanyard != null)
            {
                Color lc = Col(ex.lanyard); float cz = ChestF(0.735f * H);
                foreach (int s in new[] { -1, 1 }) b.Pipe(new[] { new Vector3(s * 0.03f * H, 0.858f * H, 0.025f * H), new Vector3(s * 0.046f * H, 0.846f * H, -0.01f * H), new Vector3(s * 0.036f * H, 0.8f * H, ChestF(0.8f * H, 0.004f * H)), new Vector3(s * 0.008f * H, 0.75f * H, cz) }, 0.0025f * H, new LoftOpts { c = lc, bone = B.chest, seg = 4 });
                b.RBox(0.032f * H, 0.046f * H, 0.003f * H, 0.003f * H, new LoftOpts { p = 6, seg = 8, c = Col("#f3f1ec"), pbr = PBR["plastic"], bone = B.chest, m = At(0, 0.703f * H, cz - 0.003f * H) });
            }
            if (ex.nameTag) b.RBox(0.04f * H, 0.014f * H, 0.003f * H, 0.002f * H, new LoftOpts { p = 6, seg = 8, c = Col("#f4f2ec"), pbr = PBR["plastic"], bone = B.chest, m = At(-0.05f * H, 0.75f * H, ChestF(0.757f * H, 0.002f * H)) });
            if (ex.tie != null) b.Loft(new[] { Row(0.69f * H, 0.002f * H, 0.002f * H), Row(0.7f * H, 0.017f * H, 0.003f * H), Row(0.83f * H, 0.008f * H, 0.003f * H), Row(0.842f * H, 0.012f * H, 0.006f * H) }
                .Select(r => { r.z = ChestF(r.y, 0.001f * H); return r; }).ToList(), new LoftOpts { seg = 8, cap = true, c = Col(ex.tie), pbr = PBR["glossy"], bone = B.chest });
            return new BodyBuild { b = b, d = d, J = J, rows = rows, chestF = ChestF };
        }

        static void BuildOuter(MeshBuilder b, BodyDims d, List<BodyRow> rows, Garment outer, Func<Vector3, float[]> ts)
        {
            float H = d.H;
            float hem = (outer.len != null && LEN.TryGetValue(outer.len, out var lv) ? lv : LEN["hip"]) * H, t = (outer.kind == "padded" ? 0.02f : outer.kind == "coat" ? 0.012f : 0.008f) * H;
            var hipR = RingAt(rows, 0.553f * H);
            float lapel = outer.kind == "vest" || outer.kind == "cardigan" ? 0.7f : outer.kind == "safety" ? 0.76f : 0.765f;
            float open = outer.open ? (outer.kind == "cardigan" ? 0.38f : 0.3f) : outer.kind == "coat" ? 0.1f : 0;
            float AAt(float y)
            {
                float a = PI - open;
                if (y > lapel * H) a = Math.Min(a, PI - (outer.kind == "vest" || outer.kind == "cardigan" ? 0.25f : 0.12f) - Ss(lapel * H, 0.85f * H, y) * (outer.kind == "vest" ? 0.75f : 0.55f));
                if (outer.kind == "coat" && y < 0.5f * H) a = Math.Min(a, PI - 0.22f);
                return a;
            }
            var oRows = new List<BodyRow>();
            if (hem < 0.494f * H) // long coat skirt below the crotch
                foreach (var q in new[] { new[] { hem, 0.118f, 0.079f }, new[] { (hem + 0.494f * H) / 2, 0.114f, 0.076f } })
                    oRows.Add(new BodyRow { y = q[0], rx = q[1] * H, rz = q[2] * H, z = 0.006f * H, p = 2.2f });
            float startY = Math.Max(hem, 0.494f * H);
            foreach (var r in new[] { RingAt(rows, startY) }.Concat(rows.Where(r => r.y > startY && r.y < 0.85f * H)))
            {
                bool below = r.y < 0.553f * H;
                var o = r.Clone();
                o.rx = Math.Max(r.rx, below ? hipR.rx * 1.04f : 0) + t; o.rz = Math.Max(r.rz.Value, below ? hipR.rz.Value : 0) + t;
                oRows.Add(o);
            }
            oRows.Add(new BodyRow { y = 0.85f * H, rx = 0.052f * H, rz = 0.046f * H, z = 0.005f * H, p = 2.2f });
            foreach (var r in oRows) r.a = AAt(r.y);
            var oz = new List<Zone> { new Zone(-1, Col(outer.c), Pb(outer.pbr ?? (outer.kind == "padded" ? "quilt" : "fabric")), 0) };
            if (outer.kind == "safety") foreach (float y in new[] { 0.635f, 0.705f })
                { oz.Add(new Zone(y * H, Col("#c9ced3"), PBR["reflect"], 0.0015f * H)); oz.Add(new Zone((y + 0.022f) * H, Col(outer.c), PBR["fluo"], 0)); }
            b.Loft(Zoned(oRows, oz), new LoftOpts { seg = 24, skin = SkirtSkin(d, Math.Min(hem, 0.52f * H)) });
            if (outer.kind == "jacket" || outer.kind == "coat" || outer.kind == "padded") // standing collar
            {
                var r0 = Row(0.838f * H, 0.056f * H, 0.05f * H, 0.004f * H); r0.a = PI - 0.55f;
                var r1 = Row(0.874f * H, 0.05f * H, 0.047f * H, 0.01f * H); r1.a = PI - 0.7f;
                b.Loft(new[] { r0, r1 }, new LoftOpts { seg = 16, c = Col(outer.c), pbr = Pb(outer.pbr ?? "fabric"), skin = ts });
            }
        }
    }
}
