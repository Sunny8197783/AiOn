// Part of the levels/town-props.js port (same order as the JS buildProps): parcels at 한결물류, AC outdoor units
// (실외기), 현수막 between street trees, vending machines and the 분리수거장 by 103동.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Levels.TownUtil;

namespace Gyeongbo.Levels
{
    internal static class TownPropsMore
    {
        const float Y = TownProps.Y;
        const float PI = Mathf.PI;

        // ---- parcels at 한결물류 (pallets, cardboard stacks, roll cages) ----
        internal static void Parcels(TownProps.Ctx c)
        {
            var kit = c.kit; var rand = c.rand;
            var card = kit.Memo("town:cardboard", () =>
            {
                var cv = kit.Canvas(128, 128, (g, w, h) =>
                {
                    g.fillStyle = "#b48a5a"; g.fillRect(0, 0, w, w); g.fillStyle = "#c9a777"; g.fillRect(0, w * 0.44f, w, w * 0.12f); g.fillStyle = "#f2efe6"; g.fillRect(w * 0.58f, w * 0.62f, w * 0.3f, w * 0.2f);
                    g.fillStyle = "#333"; for (int i = 0; i < 6; i++) g.fillRect(w * 0.6f + i * 5, w * 0.66f, 2, w * 0.1f);
                });
                return Mat.Lit(new MatSpec { map = kit.Texture(cv), roughness = 0.88f });
            });
            var parcels = new List<Xform>(); var pallets = new List<Xform>();
            foreach (var (px, pz) in new[] { (97f, -18.6f), (98.4f, -18.6f), (99.8f, -18.6f), (97.6f, -16.9f), (99.2f, -16.9f), (102.2f, -18.4f) })
            {
                pallets.Add(new Xform { x = px, y = Y, z = pz });
                for (int lay = 0; lay < 2 + (int)(rand() * 3); lay++) foreach (var (dx, dz) in new[] { (-0.27f, -0.27f), (0.27f, -0.27f), (-0.27f, 0.27f), (0.27f, 0.27f) })
                {
                    if (rand() < 0.15f) continue;
                    float s = 0.42f + rand() * 0.1f, h = 0.3f + rand() * 0.15f;
                    parcels.Add(new Xform { x = px + dx, y = Y + 0.15f + lay * 0.46f + h / 2, z = pz + dz, ry = (rand() - 0.5f) * 0.2f, sx = s, sy = h, sz = s });
                }
            }
            for (int i = 0; i < 9; i++) parcels.Add(new Xform { x = 89 + rand() * 3, y = Y + 0.15f, z = -21.4f + rand() * 0.8f, ry = rand(), sx = 0.4f, sy = 0.3f, sz = 0.35f });
            c.group.Add(kit.Instanced(Geo.Box(1, 1, 1), card, parcels));
            var pal = new List<GeoData> { Geo.Box(1.1f, 0.04f, 1.1f).Translate(0, 0.13f, 0) };
            foreach (var x in new[] { -0.45f, 0, 0.45f }) pal.Add(Geo.Box(0.1f, 0.1f, 1.1f).Translate(x, 0.06f, 0));
            c.group.Add(kit.Instanced(Geo.Merge(pal), kit.Surface("wood", new SurfaceOpts { color = J.Hex(0xa8865c) }), pallets));
            c.Box(96.3f, 103, -19.3f, -16.2f, 2.2f, "parcels");
            var cage = kit.Memo("town:cageMesh", () => Mat.Lit(new MatSpec
            {
                map = kit.Texture(kit.Canvas(64, 128, (g, w, h) =>
                {
                    g.strokeStyle = "#c9ced2"; g.lineWidth = 3;
                    for (int x = 2; x < w; x += 12) { g.beginPath(); g.moveTo(x, 0); g.lineTo(x, h); g.stroke(); }
                    for (int y = 2; y < h; y += 14) { g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.stroke(); }
                })),
                alphaTest = 0.5f, side = "double", metalness = 0.7f, roughness = 0.4f,
            }));
            foreach (var (x, z) in new[] { (93.2f, -20.6f), (94.2f, -20.6f), (95.2f, -20.6f) })
            {
                c.stat.Add(kit.Box(0.8f, 1.7f, 0.62f, cage, At(x, Y + 0.12f, z, cast: false)), kit.Box(0.82f, 0.1f, 0.64f, c.steel, At(x, Y + 0.02f, z)));
                if (parcels.Count > 0) c.stat.Add(kit.Box(0.6f, 0.5f, 0.45f, card, At(x, Y + 0.22f, z)));
            }
            c.Box(92.7f, 95.7f, -21, -20.2f, 2, "cages");
        }

        // ---- AC outdoor units (실외기) on shop/villa/civic walls ----
        internal static void Acs(TownProps.Ctx c)
        {
            var kit = c.kit;
            var acs = new List<Xform>();
            foreach (var b in TownMap.BUILDINGS)
            {
                if (b.kind == "apartment" || b.kind == "warehouse" || b.id == "culture") continue;
                float fh = b.kind == "villa" ? 2.9f : 3.5f; int floors = Math.Min(b.floors, 4); var r = Kit.Rng(Seed(b.x0 * 7 + b.z0));
                float bx0 = (float)b.x0, bx1 = (float)b.x1, bz0 = (float)b.z0, bz1 = (float)b.z1;
                foreach (var (side, len) in new[] { ("s", bx1 - bx0), ("e", bz1 - bz0), ("w", bz1 - bz0), ("n", bx1 - bx0) })
                {
                    if (side == b.door || (b.kind == "shop" && b.floors <= 2 && side == "n")) continue;
                    for (float a = 2.5f; a < len - 2; a += 4 + r() * 4) for (int f = 0; f < floors; f++)
                    {
                        if (r() < (f != 0 ? 0.55f : 0.4f)) continue;
                        float y = Y + f * fh + (f != 0 ? 0.15f : 0), along = side == "s" || side == "n" ? bx0 + a : bz0 + a;
                        float tx, tz, tr;
                        if (side == "s") { tx = along; tz = bz1 + 0.18f; tr = 0; }
                        else if (side == "n") { tx = along; tz = bz0 - 0.18f; tr = PI; }
                        else if (side == "e") { tx = bx1 + 0.18f; tz = along; tr = -PI / 2; }
                        else { tx = bx0 - 0.18f; tz = along; tr = PI / 2; }
                        acs.Add(new Xform { x = tx, y = y, z = tz, ry = tr });
                        if (f == 0) c.Box(tx - 0.45f, tx + 0.45f, tz - 0.45f, tz + 0.45f, 1, "ac");
                    }
                }
            }
            var acMat = kit.Memo("town:ac", () => Mat.Lit(new MatSpec
            {
                roughness = 0.5f,
                map = kit.Texture(kit.Canvas(128, 96, (g, w, h) =>
                {
                    g.fillStyle = "#e4e3de"; g.fillRect(0, 0, w, h); g.fillStyle = "#9da09f"; g.beginPath(); g.arc(w * 0.38f, h / 2f, h * 0.38f, 0, 7); g.fill();
                    g.strokeStyle = "#6a6d6c"; g.lineWidth = 2; for (int i = 0; i < 7; i++) { g.beginPath(); g.arc(w * 0.38f, h / 2f, h * 0.06f * i, 0, 7); g.stroke(); }
                    g.fillStyle = "#c9c8c2"; for (int y = 8; y < h - 8; y += 6) g.fillRect(w * 0.74f, y, w * 0.2f, 2);
                })),
            }));
            c.group.Add(kit.Instanced(Geo.Merge(new[] { Geo.Box(0.85f, 0.62f, 0.3f).Translate(0, 0.31f, 0), Geo.Box(0.04f, 0.04f, 0.4f).Translate(-0.35f, 0, -0.05f), Geo.Box(0.04f, 0.04f, 0.4f).Translate(0.35f, 0, -0.05f) }), acMat, acs));
        }

        // ---- 현수막 between street trees (cloth sways a little) ----
        static readonly (string[] text, string bg, string fg, float want, float z)[] BANNERS =
        {
            (new[] { "연무동 가을 문화축제", "10월 31일(토) 연무공원 · 주민 누구나" }, "#fff6e0", "#a33a1e", 12, 6.9f),
            (new[] { "연무시장 김장철 큰 장터", "11월 첫째 주말 · 시장길" }, "#1f5fa8", "#ffffff", -30, -6.9f),
            (new[] { "불법 주정차 집중 단속 구역", "연무동 행정복지센터" }, "#ffffff", "#c42b25", 66, 6.9f),
            (new[] { "한빛아파트 가을 대청소", "10월 25일(일) 오전 10시" }, "#2f7a4a", "#ffffff", 100, 6.9f),
        };

        internal static void Banners(TownProps.Ctx c)
        {
            var kit = c.kit;
            var trees = c.avoid.FindAll(t => t.type == "ginkgo");
            foreach (var (text, bg, fg, want, z) in BANNERS)
            {
                var row = trees.FindAll(t => Math.Abs(t.z - z) < 0.3f);
                row = StableSortByX(row);
                (float, float)? pair = null;
                for (int i = 1; i < row.Count; i++)
                    if (row[i].x - row[i - 1].x < 10.5f && (pair == null || Math.Abs((row[i].x + row[i - 1].x) / 2 - want) < Math.Abs((pair.Value.Item1 + pair.Value.Item2) / 2 - want))) pair = (row[i - 1].x, row[i].x);
                var (a, b) = pair ?? (want - 4.5f, want + 4.5f);
                float x = (a + b) / 2, zz = z + Math.Sign(z) * 0.2f, ry = z > 0 ? PI : 0;
                var m = kit.Sign(text, new SignOpts { w = 5.2f, h = 0.9f, bg = bg, fg = fg, single = true });
                var backMat = Mat.Lit(new MatSpec { color = J.Css(bg), roughness = 0.9f });
                var backing = Obj(Geo.Plane(5.2f, 0.9f), backMat); J.SetYaw(backing, PI);
                foreach (var mm in new[] { MatOf(m), backMat }) Sway(mm, 0.05f, 2.3f, 0.5f);
                var cloth = J.Group("banner"); cloth.Add(m, backing); J.SetPos(cloth, x, Y + 2.75f, zz); J.SetYaw(cloth, ry); c.group.Add(cloth);
                CastShadow(m, true); Kit.Keep(m); Kit.Keep(backing);
                foreach (var (from, to) in new[] { (a + 0.2f, x - 2.6f), (x + 2.6f, b - 0.2f) }) foreach (var dy in new[] { 0.4f, -0.4f })
                {
                    var rope = Obj(Geo.Cylinder(0.007f, 0.007f, to - from, 4).RotateZ(PI / 2), c.dark); J.SetPos(rope, (from + to) / 2, Y + 2.75f + dy, zz); c.stat.Add(rope);
                }
                if (pair == null) foreach (var px in new[] { a, b }) { c.stat.Add(kit.Cyl(0.05f, 0.05f, 3.3f, c.steel, At(px, Y, zz), 8)); c.Box(px - 0.1f, px + 0.1f, zz - 0.1f, zz + 0.1f, 3, "bannerPost"); }
            }
        }

        // Array.prototype.sort is stable; List.Sort is not.
        static List<TreeSpot> StableSortByX(List<TreeSpot> list)
        {
            var idx = new List<int>(); for (int i = 0; i < list.Count; i++) idx.Add(i);
            idx.Sort((p, q) => { int k = list[p].x.CompareTo(list[q].x); return k != 0 ? k : p.CompareTo(q); });
            return idx.ConvertAll(i => list[i]);
        }

        // ---- vending machines (light group 'shops') ----
        internal static void Vending(TownProps.Ctx c)
        {
            var kit = c.kit;
            var vend = kit.Memo("town:vending", () =>
            {
                var map = kit.Texture(kit.Canvas(256, 512, (g, w, h) =>
                {
                    g.fillStyle = "#d8262e"; g.fillRect(0, 0, w, h); g.fillStyle = "#141414"; g.fillRect(12, 40, w - 24, h * 0.58f);
                    string[] cols = { "#2f7dd1", "#e94e3c", "#f2c230", "#3ca55c", "#f08a2c", "#8a5bd1", "#e0e0e0" };
                    for (int r = 0; r < 5; r++) for (int k = 0; k < 6; k++)
                    {
                        int x = 22 + k * 37, y = 52 + r * 58; g.fillStyle = cols[(r * 3 + k) % cols.Length]; g.fillRect(x, y, 24, 40); g.fillStyle = "rgba(255,255,255,0.5)"; g.fillRect(x + 4, y + 4, 4, 30);
                        g.fillStyle = "#fff"; g.font = "700 11px sans-serif"; g.fillText($"{(r + 1) * 200 + 700}", x - 2, y + 52);
                    }
                    g.fillStyle = "#ffffff"; g.font = "800 26px sans-serif"; g.textAlign = "center"; g.fillText("시원한 음료", w / 2f, 30);
                    g.fillStyle = "#222"; g.fillRect(w * 0.6f, h * 0.72f, w * 0.3f, h * 0.12f); g.fillStyle = "#111"; g.fillRect(30, h * 0.88f, w - 60, h * 0.07f);
                }));
                // unpowered: the backlit front goes dark, not just unlit (userData.unlitColor)
                var m = Mat.Lit(new MatSpec { map = map, emissiveMap = map, emissive = Color.white, emissiveIntensity = 0.9f, roughness = 0.35f, unlitColor = 0.22f });
                return kit.Lit("shops", m, 0.9f);
            });
            foreach (var (x, z, ry) in new[] { (-60.6f, 37.6f, -PI / 2), (43.4f, 15.2f, -PI / 2), (7.2f, 11.3f, 0f) })
            {
                c.stat.Add(kit.Box(0.95f, 1.83f, 0.72f, kit.Mat("paint:#b82028", new MatSpec { roughness = 0.4f }), At(x, Y, z, ry)));
                var face = Obj(Geo.Plane(0.9f, 1.78f), vend); J.SetPos(face, x - Mathf.Sin(ry) * 0.365f, Y + 0.93f, z - Mathf.Cos(ry) * 0.365f); J.SetYaw(face, ry + PI); c.stat.Add(face);
                float hx = Math.Abs(Mathf.Sin(ry)) * 0.36f + Math.Abs(Mathf.Cos(ry)) * 0.48f, hz = Math.Abs(Mathf.Cos(ry)) * 0.36f + Math.Abs(Mathf.Sin(ry)) * 0.48f;
                c.Box(x - hx, x + hx, z - hz, z + hz, 2, "vending");
            }
        }

        // ---- 분리수거장 (recycling shed) by 103동 ----
        internal static void Recycling(TownProps.Ctx c)
        {
            var kit = c.kit; var stat = c.stat;
            const float shx = 117.5f, shz = 43.6f;
            stat.Add(kit.Box(5.4f, 0.08f, 2.2f, kit.Mat("paint:#4f7a5a", new MatSpec { roughness = 0.5f, metalness = 0.4f }), At(shx, Y + 2.3f, shz)));
            foreach (var dx in new[] { -2.6f, 2.6f }) foreach (var dz in new[] { -1, 1 }) stat.Add(kit.Box(0.08f, 2.3f, 0.08f, c.steel, At(shx + dx, Y, shz + dz)));
            stat.Add(kit.Box(5.3f, 1.6f, 0.04f, kit.Mat("paint:#d8d4ca", new MatSpec { roughness = 0.8f }), At(shx, Y, shz - 1.02f)));
            var bins = new[] { ("종이", "#2f6fb5"), ("캔·고철", "#e0a52a"), ("플라스틱", "#e06a2a"), ("유리병", "#3a8a4e"), ("비닐", "#8a8f94") };
            for (int i = 0; i < bins.Length; i++) stat.Add(kit.RBox(0.9f, 1.05f, 0.8f, 0.05f, kit.Mat($"paint:{bins[i].Item2}", new MatSpec { roughness = 0.5f }), At(shx - 2.1f + i * 1.05f, Y, shz - 0.4f)));
            var labels = new List<string>(); foreach (var (l, _) in bins) labels.Add(l);
            var tags = kit.Sign(new[] { string.Join("     ", labels) }, new SignOpts { w = 5.2f, h = 0.24f, bg = "#ffffff", fg = "#2f3a44", single = true }); J.SetPos(tags, shx, Y + 1.32f, shz - 0.99f); stat.Add(tags);
            var shedSign = kit.Sign(new[] { "분리수거장", "수요일 · 일요일 배출" }, new SignOpts { w = 1.6f, h = 0.45f, bg = "#2f5a3e", single = true }); J.SetPos(shedSign, shx, Y + 1.95f, shz - 0.99f); stat.Add(shedSign);
            c.Box(shx - 2.7f, shx + 2.7f, shz - 1.1f, shz + 0.05f, 2.4f, "recycling");
        }
    }
}
