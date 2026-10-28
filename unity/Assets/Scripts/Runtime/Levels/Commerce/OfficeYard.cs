// The yard half of levels/office.js: below the ribbon window one storey down — warehouse docks, trucks, pallets of
// parcels (and the return samples by the office wall), a forklift, lamps, trees, the painted sky, the hazard lights.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.CommerceGoods;

namespace Gyeongbo.Levels
{
    sealed class OfficeMats
    {
        public Material wall, lam, frame, dark, black, ped, fabric, chair, white, paper, steel, rubber, dglass, glass, screens;
    }
    delegate GameObject OfficeSign(string[] lines, SignOpts o, float x, float y, float z, float ry, string lightGroup, GameObject parent);

    static class OfficeYard
    {
        const float GY = Office.GY, PI = Mathf.PI;

        public static (WindowView view, Material hazard) Build(Kit kit, GameObject yard, GameObject dyn, List<Solid> solids, Func<float> r, OfficeMats M, OfficeSign sign)
        {
            yard.Add(kit.Plane(130, 80, kit.Surface("asphalt"), A(0, GY, -52)));
            yard.Add(kit.Slab(40, -GY, 0.3f, kit.Surface("concrete"), A(0, GY, -12.45f)));
            var line = kit.Mat("paint:#e8e6df", new MatSpec { roughness = 0.7f });
            for (int x = -48; x <= 48; x += 4) yard.Add(kit.Box(0.12f, 0.01f, 9, line, A(x, GY, -50, cast: false)));
            yard.Add(kit.Box(100, 0.01f, 0.15f, line, A(0, GY, -45.5f, cast: false)));
            Texture2D ribs(bool horiz, string @base) => kit.Texture(kit.Canvas(128, 128, (g, W, H) =>
            {
                g.fillStyle = @base; g.fillRect(0, 0, 128, 128);
                for (int i = 0; i < 128; i += 32)
                {
                    var gr = horiz ? new PaintGradient(0, i, 0, i + 32) : new PaintGradient(i, 0, i + 32, 0);
                    gr.addColorStop(0, "rgba(0,0,0,0.18)"); gr.addColorStop(0.5f, "rgba(255,255,255,0.12)"); gr.addColorStop(1, "rgba(0,0,0,0.18)"); g.fillStyle = gr;
                    if (horiz) g.fillRect(0, i, 128, 32); else g.fillRect(i, 0, 32, 128);
                }
            }));
            var corr = Mat.Lit(new MatSpec { map = ribs(false, "#b7c0c6"), metalness = 0.45f, roughness = 0.5f });
            var shutter = Mat.Lit(new MatSpec { map = ribs(true, "#9aa3a9"), metalness = 0.5f, roughness = 0.45f });
            yard.Add(kit.Slab(110, 11, 34, corr, A(0, GY, -79)));
            yard.Add(kit.Box(110.4f, 0.5f, 34.4f, M.dark, A(0, GY + 11, -79)));
            yard.Add(kit.Slab(110, 1.2f, 2.2f, kit.Surface("concrete"), A(0, GY, -60.9f)));
            sign(new[] { "한결물류 연무센터" }, new SignOpts { w = 26, h = 3.2f, bg = "#18806f", fg = "#ffffff", emissive = 0.7f }, 0, GY + 8.6f, -61.9f, 0, "signs", yard);
            var glowY = Mat.Lit(new MatSpec { color = J.Hex(0x222222), emissive = J.Hex(0xffd9a0), roughness = 0.5f }); kit.Lit("yard", glowY, 2.2f);
            var inside = Mat.Lit(new MatSpec { color = J.Hex(0x302820), emissive = J.Hex(0xffc98a), roughness = 0.8f }); kit.Lit("yard", inside, 0.9f);
            for (int i = 0; i < 8; i++)
            {
                float x = -35 + i * 10, top = GY + 1.2f; bool open = i == 2 || i == 5;
                yard.Add(kit.Slab(3.2f, open ? 1.6f : 3.2f, 0.1f, shutter, A(x, open ? top + 1.6f : top, -61.95f)));
                if (open) yard.Add(kit.Box(3.2f, 1.6f, 0.04f, inside, A(x, top, -61.97f, cast: false)));
                foreach (var s in new[] { -1, 1 }) yard.Add(kit.Box(0.25f, 0.35f, 0.2f, M.rubber, A(x + s * 1.3f, top - 0.45f, -59.75f)));
                yard.Add(kit.Box(0.5f, 0.12f, 0.3f, glowY, A(x, top + 3.5f, -61.75f, cast: false)));
                sign(new[] { (i + 1).ToString("00") }, new SignOpts { w = 0.8f, h = 0.5f, bg = "#ffffff", fg = "#1d1d1f" }, x + 2.1f, top + 2.6f, -61.89f, 0, null, yard);
            }
            var logo = Mat.Lit(new MatSpec
            {
                roughness = 0.4f, metalness = 0.2f, map = kit.Texture(kit.Canvas(1024, 256, (g, W, H) =>
                {
                    g.fillStyle = "#eef0f1"; g.fillRect(0, 0, 1024, 256); g.fillStyle = "rgba(0,0,0,0.08)"; for (int x = 0; x < 1024; x += 64) g.fillRect(x, 0, 2, 256);
                    g.fillStyle = "#18806f"; g.fillRect(0, 186, 1024, 34); g.fillStyle = "#f28a1a"; g.fillRect(0, 222, 1024, 8);
                    g.fillStyle = "#18806f"; OfficeDraw.Font(g, 96, 900); g.fillText("한결물류", 60, 130); OfficeDraw.Font(g, 34, 700); g.fillText("HANGYEOL LOGISTICS", 520, 128);
                })),
            });
            var cabMat = kit.Mat("paint:#f4f4f2", new MatSpec { roughness = 0.35f, metalness = 0.2f });
            void truck(float x, float z, float ry, bool big)
            {
                var t = J.Group("truck", yard); J.SetPos(t, x, GY, z); J.SetYaw(t, ry);
                float L = big ? 7 : 2.9f, W = big ? 2.4f : 1.8f, wr = big ? 0.5f : 0.36f, cabD = big ? 2.0f : 1.5f, cabH = big ? 2.4f : 1.9f, z0 = -(cabD + L) / 2;
                t.Add(kit.Box(W * 0.45f, 0.3f, cabD + L, M.dark, A(0, wr + 0.05f, 0.2f)));
                t.Add(kit.RBox(W - 0.08f, cabH, cabD, 0.12f, cabMat, A(0, wr + 0.25f, z0 + cabD / 2)));
                t.Add(kit.Box(W - 0.3f, cabH * 0.36f, 0.02f, M.dglass, A(0, wr + 0.25f + cabH * 0.56f, z0 - 0.005f)));
                foreach (var s in new[] { -1, 1 })
                {
                    t.Add(kit.Box(0.02f, cabH * 0.32f, cabD * 0.5f, M.dglass, A(s * (W / 2 - 0.035f), wr + 0.25f + cabH * 0.58f, z0 + cabD * 0.35f)));
                    t.Add(kit.Box(0.05f, 0.3f, 0.12f, M.black, A(s * (W / 2 + 0.12f), wr + 0.25f + cabH * 0.6f, z0 + 0.15f)));
                    t.Add(kit.Box(0.24f, 0.12f, 0.02f, kit.Mat("paint:#e9e6da", new MatSpec { roughness = 0.2f }), A(s * (W / 2 - 0.3f), wr + 0.45f, z0 - 0.01f)));
                    foreach (var wz in big ? new[] { z0 + cabD / 2, z0 + cabD + L - 1.9f, z0 + cabD + L - 0.9f } : new[] { z0 + cabD / 2, z0 + cabD + L - 0.8f })
                        t.Add(kit.Cyl(wr, wr, 0.3f, M.rubber, A(s * (W / 2 - 0.2f), wr, wz, rz: PI / 2), 14));
                }
                t.Add(kit.Box(W, 0.28f, 0.18f, M.black, A(0, wr - 0.1f, z0 - 0.05f))); t.Add(kit.Box(W * 0.6f, cabH * 0.2f, 0.02f, M.black, A(0, wr + 0.3f, z0 - 0.01f)));
                t.Add(kit.Box(W + 0.05f, big ? 2.6f : 1.8f, L, logo, A(0, wr + 0.55f, z0 + cabD + 0.15f + L / 2)));
            }
            foreach (var (x, z, ry, big) in new[] { (-25f, -54.4f, PI, true), (-5f, -54.4f, PI, true), (15f, -54.4f, PI, true), (28f, -38f, PI, true), (33f, -38f, PI, true), (38f, -38f, PI, true),
                (-42f, -30f, PI, false), (-38.6f, -30f, PI, false), (-35.2f, -30f, PI, false), (2f, -36f, PI * 0.82f, true) }) truck(x, z, ry, big);
            var box = Geo.Box(1, 1, 1).Translate(0, 0.5f, 0);
            var cardboardTex = kit.Texture(kit.Canvas(256, 256, (g, W, H) =>
            {
                var q = Kit.Rng(12); g.fillStyle = "#b98b5a"; g.fillRect(0, 0, 256, 256); for (int i = 0; i < 2500; i++) { g.fillStyle = q() < 0.5f ? "rgba(90,60,30,0.12)" : "rgba(255,230,190,0.1)"; g.fillRect(q() * 256, q() * 256, 2, 2); }
                g.fillStyle = "rgba(230,200,150,0.75)"; g.fillRect(0, 110, 256, 36); g.fillStyle = "#ffffff"; g.fillRect(150, 160, 84, 60); g.fillStyle = "#1d1d1f"; for (int x = 156; x < 228; x += 4 + (int)(q() * 3)) g.fillRect(x, 186, 2, 28);
            }));
            var parcels = new List<Xform>(); var pallets = new List<Xform>();
            void stack(float x, float z, float ry, int n)
            {
                pallets.Add(X(x, GY, z, ry, sx: 1.2f, sy: 0.14f, sz: 1.0f));
                for (int l = 0; l < n; l++) for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++)
                        {
                            if (l == n - 1 && r() < 0.3f) continue;
                            float lx = (i - 0.5f) * 0.58f, lz = (j - 0.5f) * 0.48f, c = Mathf.Cos(ry), s = Mathf.Sin(ry);
                            float pry = ry + (r() - 0.5f) * 0.1f, psy = 0.38f + r() * 0.02f;
                            parcels.Add(X(x + lx * c + lz * s, GY + 0.14f + l * 0.4f, z - lx * s + lz * c, pry, sx: 0.55f, sy: psy, sz: 0.45f));
                        }
            }
            for (int i = 0; i < 14; i++) stack(-40 + r() * 80, -57.5f + r() * 2.5f, (r() - 0.5f) * 0.3f, 2 + (int)(r() * 3));
            foreach (var (x, z) in new[] { (-6f, -15.5f), (-4.4f, -16f), (-2.6f, -15.4f), (3f, -16.2f), (4.6f, -15.6f), (-4.8f, -17.6f) }) stack(x, z, (r() - 0.5f) * 0.4f, 3 + (int)(r() * 2));
            foreach (var (x, z, y) in new[] { (8.3f, -8.9f, 0f), (8.55f, -8.4f, 0f), (8.4f, -8.65f, 0.36f) }) parcels.Add(X(x, y, z, r() * 0.4f, sx: 0.5f, sy: 0.36f, sz: 0.4f));
            solids.Add(new Solid(8.0f, 8.9f, -9.2f, -8.1f, 0, 0.8f));
            sign(new[] { "반품 샘플" }, new SignOpts { w = 0.3f, h = 0.1f, bg = "#ffffff", fg = "#c4161c" }, 8.03f, 0.25f, -8.65f, -PI / 2, null, dyn);
            var tints = parcels.Select(_ => Scaled(Color.white, Step(0.85f + r() * 0.2f))).ToList();
            dyn.Add(InstancedByColor(kit, box, c => Mat.Lit(new MatSpec { roughness = 0.85f, map = cardboardTex, color = c }), parcels, tints, name: "parcels"));
            dyn.Add(kit.Instanced(box, kit.Surface("wood", new SurfaceOpts { color = J.Hex(0xc8a878) }), pallets));
            // forklift parked by the stacks
            var fork = J.Group("forklift", yard); J.SetPos(fork, 7.5f, GY, -19); J.SetYaw(fork, 0.4f);
            var yel = kit.Mat("paint:#f2b80f", new MatSpec { roughness = 0.5f });
            fork.Add(kit.Box(1.1f, 1.0f, 1.9f, yel, A(0, 0.3f, 0.2f))); fork.Add(kit.Box(1.05f, 0.5f, 0.6f, M.dark, A(0, 0.9f, 0.85f)));
            foreach (var x in new[] { -0.5f, 0.5f }) { fork.Add(kit.Box(0.08f, 2.4f, 0.08f, M.dark, A(x, 0.3f, -0.8f))); fork.Add(kit.Box(0.06f, 1.6f, 0.06f, M.dark, A(x, 1.3f, 0.75f))); fork.Add(kit.Box(0.12f, 0.05f, 1.1f, M.steel, A(x * 0.6f, 0.1f, -1.35f))); }
            fork.Add(kit.Box(1.1f, 0.06f, 1.2f, M.dark, A(0, 2.9f, 0.15f)));
            foreach (var (x, z) in new[] { (-0.55f, -0.4f), (0.55f, -0.4f), (-0.55f, 0.8f), (0.55f, 0.8f) }) fork.Add(kit.Cyl(0.28f, 0.28f, 0.22f, M.rubber, A(x, 0.28f, z, rz: PI / 2), 12));
            foreach (var (x, z) in new[] { (-30f, -27f), (0f, -27f), (30f, -27f), (-46f, -57f), (46f, -57f) })
            {
                yard.Add(kit.Cyl(0.1f, 0.14f, 9, M.dark, A(x, GY, z))); yard.Add(kit.Box(0.2f, 0.15f, 1.4f, M.dark, A(x, GY + 8.9f, z + 0.6f)));
                yard.Add(kit.Box(0.5f, 0.08f, 0.8f, glowY, A(x, GY + 8.84f, z + 1.1f, cast: false)));
            }
            var trees = new List<Xform>(); var crowns = new List<Xform>();
            for (int z = -18; z > -60; z -= 6) foreach (var x in new[] { -57f, 57f })
                {
                    trees.Add(X(x + r() * 2, GY, z, sx: 0.25f, sy: 2.4f, sz: 0.25f));
                    float cx = x + r() * 2, cy = GY + 4 + r(); crowns.Add(X(cx, cy, z, s: 2.2f + r() * 0.8f));
                }
            dyn.Add(kit.Instanced(box, kit.Mat("bark"), trees)); dyn.Add(kit.Instanced(Geo.Icosahedron(1, 1), kit.Mat("foliage"), crowns));
            var view = MakeWindowView(kit, 420, 130, "sky", 5); J.SetPos(view.mesh, 0, 32, -165); dyn.Add(view.mesh);
            // hazard lights blinking on the idle truck at x 33 (front faces the office)
            var hazard = Mat.Lit(new MatSpec { color = J.Hex(0x3a2a10), emissive = J.Hex(0xffa31a), emissiveIntensity = 0, roughness = 0.4f });
            foreach (var s in new[] { -1, 1 }) dyn.Add(kit.Box(0.2f, 0.1f, 0.05f, hazard, A(33 + s * 0.9f, GY + 0.95f, -38 + 4.55f, cast: false)));
            return (view, hazard);
        }
    }
}
