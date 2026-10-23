// Ports levels/apartment-home.js: 302호, 정민's flat (103동 3F, 복도식): 현관 with tiles, the step up, 신발장 and the
// 분전반 (두꺼비집); a hall with the 월패드 and boiler thermostat; an open kitchen (sink under the corridor window, fridge,
// built-in hob with the yellow gas pipe and 중간밸브, cabinets, dining table, recycling bins); the living room (sofa, low
// table, wall TV, stand air conditioner, curtains); a small bedroom (bed, wardrobe, desk with a laptop) and the south
// 베란다 (washing machine, drying poles, 경량칸막이). Stateful props: ApartmentProps, ApartmentStock.
// Unit box x [-7.6, 0.3] (east wall against the lift shaft), z [0, 8.0]; balcony z [8.0, 9.4].
// This file: shell, kitchen, 현관 and hall; ApartmentHomeRooms.cs: living room, bedroom, balcony, points, drawings.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ApartmentParts;

namespace Gyeongbo.Levels
{
    public sealed class HomeDims
    {
        public double x0 = -7.6, x1 = 0.3, zb = 8.0, zo = 9.4;
        public double ch = ApartmentParts.FL + 2.3, bf = ApartmentParts.FL - 0.08, top = ApartmentParts.FL + 0.42;
    }

    // W in the JS: the flat's own paint materials.
    sealed class HomeMats
    {
        public Material cabinet, counter, gap, door, fridge, hob, sofa, cushion, pillow, rug, duvet, linen, skirt, screen, paper, plate, green, pot, tile;
    }

    // The JS buildHome closure: builder, group, materials and the box/rbox/cyl/glow helpers.
    sealed class HomeKit
    {
        public readonly AptBuilder B; public readonly GameObject g; public readonly Kit kit; public readonly AptMats M; public readonly HomeMats W;
        // shared power-switched glow materials (one draw call each after merging)
        static readonly Dictionary<string, (string hex, double k)> GLOW = new Dictionary<string, (string, double)>
        {
            ["lamp"] = ("#fff3e4", 2.2), ["shade"] = ("#ffd8a4", 0.75), ["red"] = ("#ff3a22", 1.5), ["amber"] = ("#ffa64a", 1.25), ["green"] = ("#3cff8a", 1.3), ["cyan"] = ("#8fd8ff", 1.1),
        };
        readonly Dictionary<string, Material> glows = new Dictionary<string, Material>();

        public HomeKit(AptBuilder B, GameObject g)
        {
            this.B = B; this.g = g; kit = B.kit; M = B.M;
            W = new HomeMats
            {
                cabinet = P("#f2f0eb", 0.45), counter = P("#d9d4ca", 0.22), gap = P("#9a968f", 0.8), door = P("#efece6", 0.5),
                fridge = P("#c8cbce", 0.32, 0.55), hob = P("#0d0e10", 0.08, 0.3), sofa = P("#8e8a82", 0.97), cushion = P("#9b978e", 0.97),
                pillow = P("#c9b79a", 0.95), rug = P("#b5aa98", 1), duvet = P("#a9b4bf", 0.95), linen = P("#d9d0bf", 1), skirt = P("#e6e2da", 0.5),
                screen = P("#07090b", 0.12, 0.2), paper = P("#f4f1e8", 0.9), plate = P("#f6f4ef", 0.25), green = kit.Mat("foliage"),
                pot = P("#b9a48a", 0.8), tile = kit.Surface("tile", new SurfaceOpts { color = J.Hex(0xf1efe9), roughness = 0.18f, repeat = new Vector2((float)(1 / 0.6), (float)(1 / 0.6)) }),
            };
        }

        public Material P(string hex, double roughness = 0.6, double metalness = 0) => kit.Mat("paint:" + hex, new MatSpec { roughness = (float)roughness, metalness = (float)metalness });
        public Material glow(string kind)
        {
            if (!glows.TryGetValue(kind, out var m)) { var (hex, k) = GLOW[kind]; m = glows[kind] = B.OnPower(B.GlowMat(hex, k)); }
            return m;
        }
        public GameObject box(double w, double h, double d, Material m, double x, double y, double z, double rx = 0, double ry = 0, double rz = 0, bool cast = true)
        { var b = B.Box(w, h, d, m, A(x, y, z, rx, ry, rz, cast)); g.Add(b); return b; }
        public GameObject rbox(double w, double h, double d, double r, Material m, double x, double y, double z, double rx = 0, double ry = 0, double rz = 0, bool cast = true)
        { var b = B.RBox(w, h, d, r, m, A(x, y, z, rx, ry, rz, cast)); g.Add(b); return b; }
        public GameObject cyl(double rt, double rb, double h, Material m, double x, double y, double z, double rx = 0, double ry = 0, double rz = 0, bool cast = true, int seg = 16)
        { var c = B.Cyl(rt, rb, h, m, A(x, y, z, rx, ry, rz, cast), seg); g.Add(c); return c; }
    }

    public static partial class ApartmentHome
    {
        public static readonly HomeDims HOME = new HomeDims();

        public static void BuildHome(AptBuilder B, GameObject g, AptHandles H)
        {
            var k = new HomeKit(B, g);
            BuildShell(k); BuildKitchen(k); BuildEntry(k, H); BuildHall(k);
            BuildLiving(k); BuildBedroom(k); BuildBalcony(k); BuildPoints(B);
        }

        // ---- floors, ceiling; walls: corridor-side lining, party walls, room walls, the balcony line and the outer balcony windows ----
        static void BuildShell(HomeKit k)
        {
            var B = k.B; var g = k.g; var M = k.M;
            double X0 = HOME.x0, X1 = HOME.x1, ZB = HOME.zb, ZO = HOME.zo, CH = HOME.ch, BF = HOME.bf;
            g.Add(B.Slab(2.7, 0.2, 1.3, M.entryTile, A(x: -1.05, y: F3 - 0.2, z: 0.65)));
            g.Add(B.Slab(5.2, 0.35, 8.0, M.wood, A(x: -5.0, y: F3 - 0.2, z: 4.0)));
            g.Add(B.Slab(2.7, 0.35, 6.7, M.wood, A(x: -1.05, y: F3 - 0.2, z: 4.65)));
            g.Add(B.Slab(7.9, 0.3, 1.4, M.balconyTile, A(x: -3.65, y: BF - 0.3, z: 8.7)));
            g.Add(B.Slab(7.9, 0.35, 9.4, M.ceiling, A(x: -3.65, y: CH, z: 4.7)));
            k.box(1.3, 0.152, 0.06, M.stone, -1.75, F3, 1.3); // 현관 디딤판 edge
            B.Floor(-2.4, X1, 0, 1.3, F3); B.Floor(X0, -2.4, 0, ZB, FL); B.Floor(-2.4, X1, 1.3, ZB, FL); B.Floor(X0, X1, ZB, ZO, BF);

            var hDoor = new WallHole(-1.75, -0.8, F3, F3 + 2.1); var hKitchen = new WallHole(-6.2, -4.9, F3 + 1.1, F3 + 1.9);
            B.WallZ(g, 0, X0, X1, F3, CH, M.wallpaper, new WallOpts { t = 0.02, side = 1, holes = { hDoor, hKitchen }, solid = false });
            B.WallX(g, X1, 0, ZO, F3 - 0.2, CH, M.wallpaper, new WallOpts { side = 1 });
            B.WallX(g, X0, 0, ZO, F3 - 0.2, CH, M.wallpaper, new WallOpts { side = -1 });
            B.WallZ(g, 3.6, X0, -4.0, FL, CH, M.wallpaper, new WallOpts { side = 1 });
            B.WallX(g, -4.0, 3.6, ZB, FL, CH, M.wallpaper, new WallOpts { side = -1, holes = { new WallHole(4.0, 4.85, FL, FL + 2.05, true) } });
            B.WallX(g, -2.4, 0, 2.6, F3, CH, M.wallpaper, new WallOpts { side = -1 });
            B.WallX(g, -1.1, 1.3, 3.6, FL, CH, M.wallpaper, new WallOpts { side = 1, holes = { new WallHole(2.2, 2.95, FL, FL + 2.05) } });
            B.WallZ(g, 1.3, -1.1, X1, F3, CH, M.wallpaper, new WallOpts { side = 1 });
            B.WallZ(g, 3.6, -1.1, X1, FL, CH, M.wallpaper, new WallOpts { side = -1 });
            B.WallZ(g, ZB, X0, X1, F3, CH, M.wallpaper, new WallOpts { side = -1, holes = { new WallHole(-7.0, -4.6, FL + 0.8, FL + 2.1), new WallHole(-3.75, -1.0, FL, FL + 2.2), new WallHole(-1.0, -0.1, FL, FL + 2.2, true) } });
            B.Window(g, "z", ZB - 0.05, -7.0, -4.6, FL + 0.8, FL + 2.1, mat: M.frame, split: 2);
            void panel(double a, double b, double z) // one sliding glass leaf of the living-room door
            {
                double w = b - a, h = 2.16;
                k.box(w, 0.05, 0.045, M.frame, (a + b) / 2, FL + 0.01, z); k.box(w, 0.05, 0.045, M.frame, (a + b) / 2, FL + h - 0.04, z);
                k.box(0.05, h, 0.045, M.frame, a + 0.025, FL + 0.01, z); k.box(0.05, h, 0.045, M.frame, b - 0.025, FL + 0.01, z);
                k.box(w - 0.1, h - 0.1, 0.01, M.glass, (a + b) / 2, FL + 0.06, z, cast: false);
            }
            panel(-3.75, -2.8, ZB - 0.08); panel(-2.85, -1.9, ZB - 0.025); panel(-1.95, -1.0, ZB - 0.08); panel(-1.95, -1.0, ZB - 0.025);
            k.box(2.75, 0.02, 0.12, M.alu, -2.375, FL, ZB - 0.05, cast: false); k.box(0.9, 0.012, 0.12, M.alu, -0.55, FL, ZB - 0.05, cast: false);
            B.WallZ(g, ZO, X0, X1, BF - 0.3, CH, M.exterior, new WallOpts { t = 0.18, side = 1, holes = { new WallHole(X0 + 0.08, X1 - 0.08, BF + 0.5, CH - 0.12) } });
            B.Window(g, "z", ZO + 0.06, X0 + 0.08, X1 - 0.08, BF + 0.5, CH - 0.12, mat: M.frame, split: 4, depth: 0.08);
            k.box(7.74, 0.03, 0.24, M.stone, -3.65, BF + 0.47, ZO + 0.09);
            for (double x = X0 + 0.15; x <= X1 - 0.1; x += 1.29) k.box(0.025, 0.6, 0.025, M.alu, x, BF + 0.5, ZO + 0.27);
            B.Rod(g, V3(X0 + 0.12, BF + 1.1, ZO + 0.27), V3(X1 - 0.12, BF + 1.1, ZO + 0.27), 0.02, M.alu);
            // skirting boards: [ax, az, bx, bz, s] along a wall face, s = which side of the face the room is on
            var skirts = new[] { new[] { X0, 3.7, -4.1, 3.7, 1 }, new[] { X0, 7.9, -4.1, 7.9, -1 }, new[] { -4.0, 4.85, -4.0, 7.9, 1 }, new[] { X1, 3.6, X1, 5.0, -1 }, new[] { X1, 7.0, X1, 7.38, -1 }, new[] { -2.4, 1.3, -2.4, 2.6, 1 }, new[] { X0, 0.65, X0, 3.6, 1 }, new[] { -1.1, 3.6, X1, 3.6, 1 } };
            foreach (var s in skirts)
            {
                double ax = s[0], az = s[1], bx = s[2], bz = s[3], sd = s[4]; bool alongX = az == bz;
                k.box(alongX ? bx - ax : 0.012, 0.07, alongX ? 0.012 : bz - az, k.W.skirt, alongX ? (ax + bx) / 2 : ax + sd * 0.006, FL, alongX ? az + sd * 0.006 : (az + bz) / 2, cast: false);
            }
        }

        // ---- kitchen: base run with sink + hob, upper cabinets, fridge, gas pipe and 중간밸브, dining table, bins ----
        static void BuildKitchen(HomeKit k)
        {
            var B = k.B; var g = k.g; var kit = k.kit; var M = k.M; var W = k.W;
            double X0 = HOME.x0, CH = HOME.ch;
            const double KX0 = -7.5, KX1 = -3.5; double CT = FL + 0.85;
            k.box(KX1 - KX0, 0.1, 0.5, k.P("#3b3936", 0.7), (KX0 + KX1) / 2, FL, 0.28);
            foreach (var (a, b, top) in new[] { (KX0, -5.95, 0.82), (-5.95, -5.15, 0.6), (-5.15, KX1, 0.82) }) k.box(b - a, top - 0.1, 0.6, W.cabinet, (a + b) / 2, FL + 0.1, 0.32);
            k.box(-5.92 - KX0 + 0.01, 0.03, 0.64, W.counter, (KX0 - 0.01 - 5.92) / 2, CT - 0.03, 0.33);
            k.box(KX1 + 0.01 + 5.18, 0.03, 0.64, W.counter, (-5.18 + KX1 + 0.01) / 2, CT - 0.03, 0.33);
            k.box(0.74, 0.03, 0.09, W.counter, -5.55, CT - 0.03, 0.055); k.box(0.74, 0.03, 0.1, W.counter, -5.55, CT - 0.03, 0.6);
            k.box(0.74, 0.2, 0.45, kit.Mat("metal", new MatSpec { color = J.Hex(0xd4d8db), roughness = 0.28f, side = "back" }), -5.55, CT - 0.2, 0.325, cast: false); // basin
            for (double x = KX0 + 0.5; x < KX1 - 0.05; x += 0.5) k.box(0.005, 0.68, 0.004, W.gap, x, FL + 0.12, 0.622, cast: false);
            for (double x = KX0 + 0.25; x < KX1; x += 0.5) k.box(0.11, 0.012, 0.02, M.metal, x, FL + 0.74, 0.635);
            k.cyl(0.024, 0.03, 0.04, M.metal, -5.55, CT, 0.1);
            B.Rod(g, V3(-5.55, CT + 0.03, 0.1), V3(-5.55, CT + 0.33, 0.1), 0.012, M.metal); B.Rod(g, V3(-5.55, CT + 0.33, 0.1), V3(-5.55, CT + 0.33, 0.29), 0.012, M.metal);
            B.Rod(g, V3(-5.55, CT + 0.33, 0.29), V3(-5.55, CT + 0.25, 0.31), 0.011, M.metal); k.box(0.02, 0.012, 0.1, M.metal, -5.47, CT + 0.07, 0.15);
            k.box(0.42, 0.015, 0.3, M.metal, -4.82, CT, 0.3); // dish rack + plates
            for (int i = 0; i < 4; i++) k.cyl(0.1, 0.1, 0.01, W.plate, -4.95 + i * 0.05, CT + 0.11, 0.3, rz: Math.PI / 2, seg: 20);
            k.cyl(0.035, 0.03, 0.09, k.P("#4f7fa8", 0.3), -4.67, CT + 0.015, 0.22);
            k.box(0.62, 0.008, 0.48, W.hob, -4.07, CT, 0.32, cast: false); // built-in hob
            foreach (var (dx, dz, r) in new[] { (-0.16, 0.1, 0.085), (0.16, 0.1, 0.07), (0.0, -0.12, 0.06) })
            {
                k.cyl(r, r, 0.012, M.darkMetal, -4.07 + dx, CT + 0.008, 0.32 + dz, seg: 20); k.cyl(r * 0.4, r * 0.4, 0.022, M.metal, -4.07 + dx, CT + 0.008, 0.32 + dz);
                k.box(r * 2.4, 0.014, 0.014, M.black, -4.07 + dx, CT + 0.02, 0.32 + dz); k.box(0.014, 0.014, r * 2.4, M.black, -4.07 + dx, CT + 0.02, 0.32 + dz);
            }
            for (int i = 0; i < 3; i++) k.cyl(0.017, 0.017, 0.024, M.black, -4.27 + i * 0.2, FL + 0.7, 0.64, rx: Math.PI / 2);
            k.cyl(0.1, 0.092, 0.12, M.metal, -4.23, CT + 0.035, 0.42, seg: 20); k.cyl(0.102, 0.102, 0.012, M.metal, -4.23, CT + 0.155, 0.42, seg: 20); // pot on the burner
            B.Rod(g, V3(-3.62, CH, 0.05), V3(-3.62, FL + 0.95, 0.05), 0.013, M.yellowPipe); B.Rod(g, V3(-3.62, FL + 0.95, 0.05), V3(-3.86, FL + 0.95, 0.05), 0.013, M.yellowPipe);
            k.cyl(0.022, 0.022, 0.07, M.metal, -3.62, FL + 1.09, 0.05); k.box(0.018, 0.1, 0.02, k.P("#e0b21c", 0.4), -3.62, FL + 1.08, 0.09);
            B.Plate(g, "가스", -3.62, FL + 1.27, 0.07, w: 0.07, h: 0.035, bg: "#e7b400", fg: "#3a2a00");
            foreach (var (a, b, y, h) in new[] { (KX0, -6.3, FL + 1.45, 0.75), (-4.8, -3.7, FL + 1.45, 0.75), (-6.3, -4.8, FL + 1.85, 0.35) })
            {
                k.box(b - a, h, 0.34, W.cabinet, (a + b) / 2, y, 0.19);
                for (double x = a + 0.4; x < b - 0.1; x += 0.4) k.box(0.005, h - 0.02, 0.004, W.gap, x, y + 0.01, 0.362, cast: false);
            }
            k.box(0.66, 0.1, 0.46, M.metal, -4.07, FL + 1.35, 0.25); k.box(0.22, 0.005, 0.06, k.glow("lamp"), -4.07, FL + 1.345, 0.42, cast: false);
            B.WallZ(g, 0.02, KX0, KX1, CT, FL + 1.45, W.tile, new WallOpts { t = 0.012, side = 1, holes = { new WallHole(-6.2, -4.9, F3 + 1.1, F3 + 1.9) }, solid = false });
            k.box(1.3, 0.03, 0.12, M.stone, -5.55, F3 + 1.07, 0.08);
            k.cyl(0.13, 0.12, 0.24, M.white, -6.9, CT, 0.32, seg: 20); k.box(0.06, 0.025, 0.005, k.glow("amber"), -6.9, CT + 0.15, 0.452, cast: false); // rice cooker
            k.cyl(0.075, 0.085, 0.22, M.steel, -6.45, CT, 0.3, seg: 18); k.box(0.3, 0.012, 0.22, M.lightWood, -5.0, CT, 0.48, ry: 0.12); // kettle, board
            B.Solid(X0, KX1, 0, 0.66, FL, FL + 0.9);
            k.rbox(0.85, 1.82, 0.72, 0.03, W.fridge, -2.925, FL, 0.4);
            k.box(0.004, 1.2, 0.01, W.gap, -2.925, FL + 0.6, 0.762, cast: false); k.box(0.84, 0.004, 0.01, W.gap, -2.925, FL + 0.6, 0.762, cast: false);
            foreach (var x in new[] { -2.96, -2.89 }) B.Rod(g, V3(x, FL + 0.95, 0.79), V3(x, FL + 1.45, 0.79), 0.01, M.metal);
            B.Rod(g, V3(-3.15, FL + 0.48, 0.79), V3(-2.7, FL + 0.48, 0.79), 0.01, M.metal);
            k.box(0.07, 0.1, 0.005, k.glow("cyan"), -2.72, FL + 1.3, 0.763, cast: false); k.box(0.1, 0.14, 0.003, W.paper, -3.18, FL + 1.2, 0.763, rz: 0.06);
            B.Solid(-3.36, -2.49, 0, 0.8, FL, FL + 1.85);
            k.box(1.1, 0.035, 0.72, M.lightWood, -5.6, FL + 0.7, 2.2);
            foreach (var (dx, dz) in new[] { (-0.5, -0.31), (0.5, -0.31), (-0.5, 0.31), (0.5, 0.31) }) k.box(0.04, 0.7, 0.04, M.lightWood, -5.6 + dx, FL, 2.2 + dz);
            void chair(double x, double z, double ry)
            {
                var c = J.Group("chair"); SetPos(c, x, FL, z); SetYaw(c, ry); g.Add(c);
                c.Add(B.Box(0.42, 0.03, 0.42, M.lightWood, A(y: 0.44)), B.Box(0.4, 0.36, 0.025, M.lightWood, A(y: 0.5, z: 0.2)));
                foreach (var (dx, dz) in new[] { (-0.18, -0.18), (0.18, -0.18), (-0.18, 0.18), (0.18, 0.18) }) c.Add(B.Box(0.03, 0.44, 0.03, M.lightWood, A(x: dx, z: dz)));
            }
            chair(-5.85, 1.55, Math.PI); chair(-5.35, 2.95, 0.35);
            k.cyl(0.13, 0.08, 0.07, W.plate, -5.75, FL + 0.735, 2.15, seg: 20);
            foreach (var (dx, dz, c) in new[] { (-0.03, 0.0, "#c0392b"), (0.04, 0.03, "#d35400"), (0.0, -0.05, "#c0392b") })
            { var s = Obj(Geo.Sphere(0.035f, 12, 8), k.P(c, 0.5), cast: true); SetPos(s, -5.75 + dx, FL + 0.8, 2.15 + dz); g.Add(s); }
            k.box(0.24, 0.012, 0.18, W.paper, -5.3, FL + 0.735, 2.3, ry: -0.2); k.box(0.22, 0.012, 0.16, k.P("#e9e1cf", 0.9), -5.31, FL + 0.747, 2.29, ry: 0.1);
            B.Solid(-6.16, -5.04, 1.83, 2.57, FL, FL + 0.75);
            var bins = new[] { ("#3d78c2", "플라스틱"), ("#8d939a", "캔·병"), ("#c49a52", "종이") };
            for (int i = 0; i < bins.Length; i++)
            {
                var (c, label) = bins[i];
                k.box(0.32, 0.46, 0.34, k.P(c, 0.5), -7.38, FL, 2.4 + i * 0.36);
                B.Plate(g, label, -7.21, FL + 0.36, 2.4 + i * 0.36, ry: Math.PI / 2, w: 0.2, h: 0.07, bg: "#ffffff", fg: "#333333");
            }
            B.Solid(X0, -7.2, 2.2, 3.4, FL, FL + 0.5);
            k.cyl(0.22, 0.22, 0.035, k.glow("lamp"), -5.5, CH - 0.035, 1.8, cast: false, seg: 24);
            B.fixtures.Add(new Fixture { x = -5.5f, y = (float)(CH - 0.3), z = 1.8f, color = 0xfff0dc, intensity = 3.5f, distance = 8 });
        }

        // ---- 현관: 신발장, mirror, shoes, umbrellas, 분전반 (두꺼비집), the inside of the door, sensor light ----
        static void BuildEntry(HomeKit k, AptHandles H)
        {
            var B = k.B; var g = k.g; var M = k.M; var W = k.W; double X1 = HOME.x1, CH = HOME.ch;
            k.box(0.38, 1.05, 1.2, W.cabinet, 0.11, F3, 0.66);
            foreach (var z in new[] { 0.36, 0.66, 0.96 }) { k.box(0.004, 1.0, 0.004, W.gap, -0.082, F3 + 0.03, z, cast: false); k.box(0.02, 0.1, 0.012, M.metal, -0.09, F3 + 0.75, z - 0.06); }
            k.box(0.01, 0.8, 0.6, M.mirror, 0.294, F3 + 1.3, 0.66, cast: false);
            k.box(0.2, 0.015, 0.12, M.darkWood, 0.1, F3 + 1.05, 0.4); k.cyl(0.018, 0.018, 0.16, M.black, 0.05, F3 + 1.07, 0.95, rz: Math.PI / 2); // key tray, flashlight
            B.Solid(-0.1, X1, 0.04, 1.28, F3, F3 + 1.1);
            var shoes = new[] { (-1.05, 0.95, 0.2), (-0.93, 0.95, -0.1), (-0.6, 0.35, 1.4), (-0.48, 0.38, 1.6), (-1.9, 1.62, 0.0), (-1.75, 1.62, 0.1) };
            var list = shoes.Select((s, i) => X(x: s.Item1, y: (i >= 4 ? FL : F3) + 0.035, z: s.Item2, ry: s.Item3)).ToList();
            g.Add(B.InstancedPaint(Geo.Box(0.1f, 0.07f, 0.26f), "#ffffff", 0.7, list, new[] { "#e9e6df", "#e9e6df", "#2a2a2a", "#2a2a2a", "#8a6d55", "#8a6d55" }, cast: false));
            k.cyl(0.1, 0.09, 0.42, M.steel, -2.22, F3, 0.3, seg: 18);
            foreach (var (dx, c) in new[] { (-0.03, "#1f2d45"), (0.035, "#7a1f2b") })
            { B.Rod(g, V3(-2.22 + dx, F3 + 0.1, 0.3), V3(-2.2 + dx, F3 + 0.92, 0.32), 0.012, M.black); k.cyl(0.035, 0.012, 0.5, k.P(c, 0.8), -2.22 + dx, F3 + 0.2, 0.3); }
            var brk = B.Tex("breaker", 256, 320, DrawBreaker);
            k.box(0.36, 0.46, 0.08, k.P("#d9d4c8", 0.5, 0.3), -2.1, F3 + 1.72, 0.06);
            var face = Obj(Geo.Plane(0.34f, 0.44f), Mat.Lit(new MatSpec { map = brk, roughness = 0.45f, metalness = 0.2f }));
            SetPos(face, -2.1, F3 + 1.95, 0.1012); g.Add(face);
            if (H.door302 != null) // inner face of the 302 door: lock body, door closer, latch guard
            {
                var pv = H.door302.pivot;
                pv.Add(B.Box(0.08, 0.28, 0.05, M.black, A(x: 0.83, y: 0.88, z: 0.05)), B.Box(0.3, 0.05, 0.07, M.alu, A(x: 0.3, y: 1.98, z: 0.06)), B.Box(0.04, 0.06, 0.03, M.metal, A(x: 0.86, y: 1.45, z: 0.04)));
            }
            B.sensors.Add(new Sensor { x = -1.25f, y = (float)(CH - 0.02), z = 0.65f, kind = "sensor", floor = 3 });
        }

        // ---- hall: 월패드, boiler thermostat, switches, downlight; the closed bathroom door ----
        static void BuildHall(HomeKit k)
        {
            var B = k.B; var g = k.g; var M = k.M; var W = k.W; double CH = HOME.ch;
            k.box(0.02, 0.2, 0.28, M.white, -2.39, FL + 1.32, 2.0); k.box(0.004, 0.14, 0.2, k.glow("cyan"), -2.378, FL + 1.35, 1.98, cast: false);
            k.box(0.02, 0.12, 0.09, M.white, -2.39, FL + 1.34, 2.38); k.box(0.004, 0.03, 0.05, k.glow("amber"), -2.378, FL + 1.41, 2.38, cast: false);
            foreach (var (x, z, ry) in new[] { (-2.39, 1.55, Math.PI / 2), (-3.99, 5.0, Math.PI / 2), (-2.51, 2.3, Math.PI / 2), (-1.108, 3.2, Math.PI / 2) }) k.box(0.07, 0.12, 0.012, M.white, x, FL + 1.1, z, ry: ry);
            k.cyl(0.06, 0.06, 0.02, k.glow("lamp"), -1.75, CH - 0.02, 2.5, cast: false);
            B.Door(g, "x", -1.05, 2.2, 2.95, FL, 2.05, W.door, hinge: "b", dir: -1, open: 0, solid: false, frameMat: M.frame);
        }
    }
}
