// Ports the rest of engine/people-body.js: the head, hair, limb and shoe tables, arms and hands, legs and
// shoes, hair(). JS space.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Gyeongbo.People.PeopleGeo;

namespace Gyeongbo.People
{
    public static partial class PeopleBody
    {
        // Head rows: [f (0 chin .. 1 crown), rx, rz, z] in units of head height.
        static readonly float[][] HEAD = {
            new[] { 0f, 0.1f, 0.1f, -0.24f }, new[] { 0.05f, 0.19f, 0.2f, -0.17f }, new[] { 0.12f, 0.27f, 0.32f, -0.1f }, new[] { 0.17f, 0.29f, 0.36f, -0.075f }, new[] { 0.2f, 0.3f, 0.38f, -0.06f },
            new[] { 0.3f, 0.315f, 0.41f, -0.04f }, new[] { 0.42f, 0.33f, 0.425f, -0.03f }, new[] { 0.46f, 0.332f, 0.428f, -0.028f }, new[] { 0.5f, 0.335f, 0.43f, -0.025f }, new[] { 0.535f, 0.337f, 0.432f, -0.022f },
            new[] { 0.565f, 0.34f, 0.435f, -0.02f }, new[] { 0.6f, 0.34f, 0.435f, -0.017f }, new[] { 0.7f, 0.335f, 0.43f, -0.01f }, new[] { 0.82f, 0.3f, 0.39f, 0f }, new[] { 0.92f, 0.22f, 0.29f, 0.01f }, new[] { 1f, 0.03f, 0.04f, 0.01f } };
        static readonly List<BodyRow> HEAD_ROWS = HEAD.Select(h => new BodyRow { y = h[0], rx = h[1], rz = h[2], z = h[3] }).ToList();

        // Hair: [f, thickness, half arc around the back (PI = full ring)] per style. hang: falls straight below the skull.
        sealed class HairStyle { public float[][] rows; public float[] pbr; public bool hang; }
        const float P_ = Mathf.PI;
        static readonly HairStyle HAIR_PERM = new HairStyle { pbr = PBR["perm"], rows = new[] { new[] { 0.3f, 0.06f, 1.3f }, new[] { 0.42f, 0.1f, 1.9f }, new[] { 0.55f, 0.12f, 2.3f }, new[] { 0.66f, 0.13f, 2.65f }, new[] { 0.72f, 0.13f, P_ }, new[] { 0.84f, 0.13f, P_ }, new[] { 0.94f, 0.11f, P_ }, new[] { 1f, 0.09f, P_ } } };
        static readonly Dictionary<string, HairStyle> HAIR = new Dictionary<string, HairStyle>
        {
            ["short"] = new HairStyle { rows = new[] { new[] { 0.28f, 0.03f, 1.0f }, new[] { 0.38f, 0.035f, 1.55f }, new[] { 0.5f, 0.04f, 2.0f }, new[] { 0.6f, 0.05f, 2.3f }, new[] { 0.68f, 0.06f, 2.6f }, new[] { 0.76f, 0.065f, P_ }, new[] { 0.86f, 0.07f, P_ }, new[] { 0.94f, 0.06f, P_ }, new[] { 1f, 0.05f, P_ } } },
            ["tied"] = new HairStyle { rows = new[] { new[] { 0.22f, 0.03f, 1.2f }, new[] { 0.36f, 0.04f, 1.7f }, new[] { 0.5f, 0.05f, 2.1f }, new[] { 0.6f, 0.055f, 2.4f }, new[] { 0.7f, 0.06f, 2.7f }, new[] { 0.74f, 0.06f, P_ }, new[] { 0.86f, 0.065f, P_ }, new[] { 0.94f, 0.06f, P_ }, new[] { 1f, 0.05f, P_ } } },
            ["perm"] = HAIR_PERM,
            ["bob"] = new HairStyle { hang = true, rows = new[] { new[] { 0.08f, 0.09f, 1.85f }, new[] { 0.2f, 0.1f, 1.95f }, new[] { 0.35f, 0.095f, 2.05f }, new[] { 0.5f, 0.085f, 2.2f }, new[] { 0.6f, 0.08f, 2.45f }, new[] { 0.62f, 0.08f, P_ }, new[] { 0.72f, 0.08f, P_ }, new[] { 0.84f, 0.085f, P_ }, new[] { 0.94f, 0.07f, P_ }, new[] { 1f, 0.06f, P_ } } },
            ["long"] = new HairStyle { hang = true, rows = new[] { new[] { -1.05f, 0.0f, 0.9f }, new[] { -0.95f, 0.0f, 1.2f }, new[] { -0.5f, 0.0f, 1.35f }, new[] { -0.2f, 0.0f, 1.55f }, new[] { 0.08f, 0.09f, 1.85f }, new[] { 0.2f, 0.1f, 1.95f }, new[] { 0.35f, 0.095f, 2.05f }, new[] { 0.5f, 0.085f, 2.2f }, new[] { 0.6f, 0.08f, 2.45f }, new[] { 0.62f, 0.08f, P_ }, new[] { 0.72f, 0.08f, P_ }, new[] { 0.84f, 0.085f, P_ }, new[] { 0.94f, 0.07f, P_ }, new[] { 1f, 0.06f, P_ } } },
        };
        // Below-chin hair rows hug the back: [f] -> { rx, rz, z } in head units.
        static readonly Dictionary<float, float[]> LONG_BACK = new Dictionary<float, float[]> { [-1.05f] = new[] { 0.22f, 0.12f, 0.34f }, [-0.95f] = new[] { 0.3f, 0.17f, 0.33f }, [-0.5f] = new[] { 0.34f, 0.2f, 0.3f }, [-0.2f] = new[] { 0.33f, 0.27f, 0.18f } };

        static readonly float[][] LEG = { new[] { 0.555f, 0.052f }, new[] { 0.5f, 0.05f }, new[] { 0.42f, 0.043f }, new[] { 0.335f, 0.034f }, new[] { 0.285f, 0.031f, -0.002f }, new[] { 0.25f, 0.033f, 0.004f }, new[] { 0.2f, 0.033f, 0.007f }, new[] { 0.13f, 0.025f, 0.003f }, new[] { 0.075f, 0.0195f }, new[] { 0.05f, 0.018f } };
        static readonly float[][] ARM = { new[] { 0.022f, 0.008f }, new[] { 0.015f, 0.021f }, new[] { 0f, 0.028f }, new[] { -0.02f, 0.029f }, new[] { -0.06f, 0.026f }, new[] { -0.12f, 0.0235f } };
        static readonly float[][] HAND = { new[] { 0f, 0.0105f, 0.015f, 0f }, new[] { -0.017f, 0.0125f, 0.0225f, -0.001f }, new[] { -0.046f, 0.0115f, 0.0255f, -0.0015f }, new[] { -0.064f, 0.0092f, 0.0238f, -0.0035f }, new[] { -0.087f, 0.0068f, 0.021f, -0.0093f }, new[] { -0.1f, 0.0045f, 0.016f, -0.015f } };

        // ---- arms and hands, legs and shoes ----
        static void BuildLimbs(MeshBuilder b, BodyDims d, PersonSpec spec, Color skin)
        {
            float H = d.H; Garment top = spec.top, outer = spec.outer, bot = spec.bottom;
            Garment sleeve = outer != null && outer.kind != "vest" && outer.kind != "safety" ? outer : top;
            bool shortSl = !(outer != null && sleeve == outer) && top.sleeves == "short";
            float rf = d.f ? 0.86f : 1;
            foreach (int s in new[] { -1, 1 })
            {
                int a = s < 0 ? B.lArm : B.rArm; float x0 = s * d.shX;
                var pts = ARM.Select(q => new[] { d.shY + q[0] * H, q[1] * H }).Concat(new[] { new[] { d.elY + 0.03f * H, 0.021f * H }, new[] { d.elY, 0.0205f * H }, new[] { d.elY - 0.045f * H, 0.0225f * H }, new[] { d.wrY + 0.03f * H, 0.0165f * H }, new[] { d.wrY - 0.004f * H, 0.0145f * H } });
                var arm = pts.Select(q => new BodyRow { y = q[0], rx = q[1] * rf * d.g, rz = q[1] * rf * d.g * 1.04f, x = x0 + s * 0.009f * H * Ss(d.shY, d.wrY, q[0]), p = 2 }).Reverse().ToList();
                float cuff = shortSl ? d.shY - 0.075f * H : d.wrY + 0.004f * H, st = sleeve == outer ? (outer.kind == "padded" ? 0.012f : 0.007f) * H : 0.003f * H;
                var az = new List<Zone> { new Zone(-1, skin, PBR["skin"], 0), new Zone(cuff, Col(sleeve.c), Pb(sleeve.pbr ?? (sleeve.kind == "padded" ? "quilt" : "fabric")), st) };
                var armChain = new[] { new Seg(d.shY - 0.01f * H, B.chest, a, 0.022f * H), new Seg(d.elY, a, a + 1, 0.022f * H), new Seg(d.wrY, a + 1, a + 2, 0.01f * H) };
                b.Loft(Zoned(arm, az), new LoftOpts { seg = 12, cap = true, skin = (p) => Chain(p.y, armChain) });
                float hx = x0 + s * 0.009f * H;
                var handChain = new[] { new Seg(d.wrY + 0.005f * H, a + 1, a + 2, 0.008f * H) };
                b.Loft(HAND.Select(q => new BodyRow { y = d.wrY + q[0] * H * rf, rx = q[1] * H * rf, rz = q[2] * H * rf, x = hx + s * q[3] * H * rf, p = 2.3f }).Reverse().ToList(),
                    new LoftOpts { seg = 10, cap = true, c = skin, pbr = PBR["skin"], skin = (p) => Chain(p.y, handChain) });
                var thumb = new[] { new Vector3(hx - s * 0.004f * H, d.wrY - 0.012f * H, -0.014f * H), new Vector3(hx - s * 0.009f * H, d.wrY - 0.034f * H, -0.024f * H), new Vector3(hx - s * 0.014f * H, d.wrY - 0.052f * H, -0.026f * H) }
                    .Select(v => new Vector3(v.x, v.y, v.z * rf)).ToList();
                b.Pipe(thumb, new[] { 0.0062f * H * rf, 0.0058f * H * rf, 0.0046f * H * rf }, new LoftOpts { c = skin, pbr = PBR["skin"], bone = a + 2, seg = 7 });
            }

            float[] lf = d.f ? new[] { 1.06f, 0.95f } : new[] { 1f, 1f }; bool trousers = bot.kind != "skirt";
            Color legC = trousers ? Col(bot.c) : Col(bot.legs ?? spec.skin);
            float[] legP = trousers ? Pb(bot.pbr ?? "fabric") : bot.legs != null ? PBR["knit"] : PBR["skin"];
            float straight = (bot.kind == "jeans" ? 0.032f : bot.kind == "slacks" ? 0.035f : 0.037f) * H;
            foreach (int s in new[] { -1, 1 })
            {
                int l = s < 0 ? B.lThigh : B.rThigh;
                var leg = LEG.Select(q =>
                {
                    float yy = q[0] * H, k = yy > d.kneeY ? lf[0] : lf[1], z = q.Length > 2 ? q[2] : 0;
                    float rr = q[1] * H * k * (0.6f + 0.4f * d.g) + (trousers ? 0.005f * H : 0.001f * H);
                    if (trousers && yy < d.kneeY + 0.02f * H) rr = Math.Max(rr, straight - (d.kneeY - yy) * 0.04f);
                    return new BodyRow { y = yy, rx = rr, rz = rr * 1.03f, z = z * H, x = s * d.hipX * (1 - 0.07f * Ss(d.hipY, d.kneeY, yy)) };
                }).Reverse().ToList();
                var legChain = new[] { new Seg(d.hipY + 0.012f * H, B.pelvis, l, 0.03f * H), new Seg(d.kneeY, l, l + 1, 0.028f * H), new Seg(d.ankleY + 0.006f * H, l + 1, l + 2, 0.012f * H) };
                b.Loft(leg, new LoftOpts { seg = 12, c = legC, pbr = legP, skin = (p) => Chain(p.y, legChain) });
                Shoe(b, s * d.hipX * 0.95f, d, spec.shoes ?? new Garment { kind = "sneaker", c = "#333" }, l + 2);
            }
        }

        static void Hair(MeshBuilder b, Garment h, Func<float, BodyRow> hRow, float hh, float y0, Color hairC, Func<Vector3, float[]> hs, bool cap)
        {
            HairStyle st = h.style != null && HAIR.TryGetValue(h.style, out var hst) ? hst : HAIR["short"];
            if (h.style == "bald") return;
            var rows = new List<BodyRow>();
            foreach (var q in st.rows)
            {
                float f = q[0], t = q[1], a = q[2];
                if (cap && f > 0.62f && a >= P_) continue;
                if (f < 0) { var lb = LONG_BACK[f]; rows.Add(new BodyRow { y = y0 + f * hh, rx = lb[0] * hh, rz = lb[1] * hh, z = lb[2] * hh, a = a }); continue; }
                var r = hRow(f);
                float rx = r.rx, rz = r.rz.Value, z = r.z.Value;
                if (st.hang && f < 0.45f) { rx = Math.Max(rx, 0.31f * hh); rz = Math.Max(rz, 0.4f * hh); z = Math.Max(z, 0.01f * hh); }
                rows.Add(new BodyRow { y = r.y, rx = rx + t * hh, rz = rz + t * hh, z = z + (st.hang ? 0.02f * hh : 0), a = a });
            }
            if (!cap) rows.Add(new BodyRow { y = y0 + (1 + 0.05f) * hh + (st == HAIR_PERM ? 0.08f * hh : 0), rx = 0.02f * hh, rz = 0.02f * hh, z = 0.01f * hh, a = P_ });
            b.Loft(rows, new LoftOpts { seg = 22, color = (r, th, i, p) => (Math.Abs(th) > 2.6f ? hairC * 0.9f : hairC), pbr = st.pbr ?? PBR["hair"], skin = hs });
            if (h.style == "tied") // low ponytail
            {
                var tail = new[] { new Vector3(0, y0 + 0.55f * hh, 0.47f * hh), new Vector3(0, y0 + 0.4f * hh, 0.55f * hh), new Vector3(0, y0 + 0.05f * hh, 0.56f * hh), new Vector3(0, y0 - 0.35f * hh, 0.5f * hh) };
                b.Pipe(tail, new[] { 0.07f * hh, 0.085f * hh, 0.065f * hh, 0.03f * hh }, new LoftOpts { c = hairC, pbr = PBR["hair"], skin = hs, seg = 8 });
            }
        }

        // [u along foot, half width, half height]; sole height; length factor.
        sealed class ShoeStyle { public float[][] rows; public float sole, len; public float[] pbr; }
        static readonly Dictionary<string, ShoeStyle> SHOE = new Dictionary<string, ShoeStyle>
        {
            ["sneaker"] = new ShoeStyle { rows = new[] { new[] { 0f, 0.016f, 0.017f }, new[] { 0.05f, 0.021f, 0.026f }, new[] { 0.25f, 0.024f, 0.027f }, new[] { 0.5f, 0.028f, 0.021f }, new[] { 0.72f, 0.029f, 0.018f }, new[] { 0.9f, 0.026f, 0.015f }, new[] { 1f, 0.014f, 0.01f } }, sole = 0.009f, len = 0.155f, pbr = PBR["fabric"] },
            ["dress"] = new ShoeStyle { rows = new[] { new[] { 0f, 0.015f, 0.016f }, new[] { 0.05f, 0.02f, 0.024f }, new[] { 0.25f, 0.022f, 0.025f }, new[] { 0.5f, 0.026f, 0.019f }, new[] { 0.72f, 0.027f, 0.016f }, new[] { 0.92f, 0.021f, 0.012f }, new[] { 1f, 0.01f, 0.008f } }, sole = 0.004f, len = 0.158f, pbr = PBR["leather"] },
            ["flat"] = new ShoeStyle { rows = new[] { new[] { 0f, 0.014f, 0.012f }, new[] { 0.05f, 0.019f, 0.02f }, new[] { 0.25f, 0.021f, 0.02f }, new[] { 0.5f, 0.025f, 0.014f }, new[] { 0.72f, 0.026f, 0.012f }, new[] { 0.9f, 0.022f, 0.01f }, new[] { 1f, 0.012f, 0.007f } }, sole = 0.004f, len = 0.148f, pbr = PBR["leather"] },
            ["boot"] = new ShoeStyle { rows = new[] { new[] { 0f, 0.017f, 0.035f }, new[] { 0.05f, 0.022f, 0.045f }, new[] { 0.25f, 0.025f, 0.05f }, new[] { 0.45f, 0.028f, 0.03f }, new[] { 0.72f, 0.029f, 0.019f }, new[] { 0.9f, 0.026f, 0.016f }, new[] { 1f, 0.014f, 0.011f } }, sole = 0.008f, len = 0.158f, pbr = PBR["leather"] },
        };

        static void Shoe(MeshBuilder b, float x, BodyDims d, Garment sh, int bone)
        {
            ShoeStyle S = sh.kind != null && SHOE.TryGetValue(sh.kind, out var ss) ? ss : SHOE["sneaker"];
            float H = d.H, L = S.len * H * (d.f ? 0.97f : 1), heel = 0.22f * L;
            Color up = Col(sh.c), sole = Col(sh.sole ?? "#1d1d1f"); float so = S.sole * H;
            var rows = S.rows.Select(q => new BodyRow { y = q[0] * L - heel, rx = q[1] * H, rz = q[2] * H, z = q[2] * H, p = 2.6f }).ToList();
            b.Loft(rows, new LoftOpts
            {
                seg = 12, cap = true, m = At(x, 0, 0, -P_ / 2), bone = bone,
                color = (r, th, i, p) => (p.z < so ? sole : up),
                pbrFn = (r, th, i, p) => (p.z < so ? PBR["rubber"] : S.pbr),
            });
        }
    }
}
