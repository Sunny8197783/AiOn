// Ports levels/office.js: 한결물류 연무센터 운영팀 — a 2nd-floor open office at dusk. Two desk islands with dual
// monitors, the player's desk with the director-drawn dashboard, 윤 팀장 (NPC 'coworker', seated), a 2x2 video wall with
// the route map, whiteboard, cooler, printer, and a ribbon window over the yard (OfficeYard).
// Frame: floor x -9..9, z -12..0 at y 0; the yard lies north (-z) one storey down (y = -4.2). Door on the south wall.
// States: alerts (0..3 rows red) dashboard (a Paint the prologue draws; never saved in the snapshot) outside (null | mood).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.CommerceGoods;

namespace Gyeongbo.Levels
{
    public static class Office
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("office", (kit, people, fx) => BuildOffice(kit, people));

        internal const float H = 2.75f, GY = -4.2f, DOOR = 6.8f, PI = Mathf.PI;
        static readonly float[] ISL = { -4.6f, -9.2f }, DX = { -5.8f, -4.4f, -3.0f, -1.6f };
        static readonly (float x, float z) ME = (-1.6f, ISL[0]), LEAD = (7.55f, -7.5f);

        public static Task<Level> BuildOffice(Kit kit, IPeople people)
        {
            try { return Task.FromResult(Build(kit, people)); }
            catch (Exception e) { return Task.FromException<Level>(e); }
        }

        static Material ScreenMat(Texture map, float k = 0.85f) => Mat.Lit(new MatSpec { color = J.Hex(0x1a1d20), map = map, emissiveMap = map, emissive = J.Hex(0xffffff), emissiveIntensity = k, roughness = 0.25f });

        static Level Build(Kit kit, IPeople people)
        {
            var group = J.Group("office");
            var stat = J.Group("stat", group); var yard = J.Group("yard", group); var dyn = J.Group("dyn", group);
            var solids = new List<Solid>(); var r = Kit.Rng(31);
            var M = new OfficeMats
            {
                wall = kit.Surface("plaster"), lam = kit.Mat("paint:#d9d2c4", new MatSpec { roughness = 0.45f }), frame = kit.Mat("paint:#8e959c", new MatSpec { metalness = 0.6f, roughness = 0.35f }),
                dark = kit.Mat("darkMetal"), black = kit.Mat("paint:#1c1e21", new MatSpec { roughness = 0.4f }), ped = kit.Mat("paint:#b4b8bc", new MatSpec { metalness = 0.4f, roughness = 0.45f }),
                fabric = kit.Mat("paint:#5f7378", new MatSpec { roughness = 0.98f }), chair = kit.Mat("paint:#2a2e33", new MatSpec { roughness = 0.9f }), white = kit.Mat("paint:#f1f0ec", new MatSpec { roughness = 0.5f }),
                paper = kit.Mat("paint:#fafaf6", new MatSpec { roughness = 0.9f }), steel = kit.Mat("steel"), rubber = kit.Mat("rubber"), dglass = kit.Mat("darkGlass"),
                glass = Mat.Lit(new MatSpec { color = J.Hex(0xd6e6ee), roughness = 0.04f, metalness = 0.1f, transparent = true, opacity = 0.14f, depthWrite = false }),
                screens = ScreenMat(kit.Texture(kit.Canvas(1024, 512, (g, W, Hh) => OfficeDraw.DrawAtlas(g, Kit.Rng(8)))), 0.7f),
            };
            GameObject add(GameObject o, bool solid = false, float pad = 0.03f) { stat.Add(o); if (solid) solids.Add(SolidOf(o, pad)); return o; }
            GameObject keep(GameObject o) { Kit.Keep(o); dyn.Add(o); return o; }
            GameObject sign(string[] lines, SignOpts o, float x, float y, float z, float ry, string lightGroup, GameObject parent)
            {
                var s = SignAt(kit, lines, o, x, y, z, ry);
                if (lightGroup != null) kit.Lit(lightGroup, MatOf(s), o.emissive != 0 ? o.emissive : 0.6f);
                (parent ?? dyn).Add(s); return s;
            }
            GameObject flat(GeoData geo, Material mat, float x, float y, float z, float ry = 0) { var m = Geo.Obj(geo, mat); J.SetPos(m, x, y, z); J.SetYaw(m, ry); return m; }

            // ---- shell: carpet tiles, walls, ribbon window, T-bar ceiling ----
            var carpetC = kit.Canvas(512, 512, (g, W, Hh) =>
            {
                var g2 = Kit.Rng(4);
                for (int ty = 0; ty < 4; ty++) for (int tx = 0; tx < 4; tx++)
                    {
                        float v = 86 + g2() * 10; g.fillStyle = $"rgb({(int)(v - 8)},{(int)v},{(int)(v + 14)})"; g.fillRect(tx * 128, ty * 128, 128, 128);
                        g.strokeStyle = "rgba(30,36,44,0.35)"; g.lineWidth = 1;
                        for (int k = 0; k < 40; k++) { float o = g2() * 128; g.beginPath(); if ((tx + ty) % 2 == 1) { g.moveTo(tx * 128 + o, ty * 128); g.lineTo(tx * 128 + o, ty * 128 + 128); } else { g.moveTo(tx * 128, ty * 128 + o); g.lineTo(tx * 128 + 128, ty * 128 + o); } g.stroke(); }
                    }
                for (int i = 0; i < 9000; i++) { g.fillStyle = g2() < 0.5f ? "rgba(0,0,0,0.08)" : "rgba(255,255,255,0.05)"; g.fillRect(g2() * 512, g2() * 512, 1.5f, 1.5f); }
                g.fillStyle = "rgba(20,24,30,0.5)"; for (int i = 0; i <= 4; i++) { g.fillRect(i * 128 - 1, 0, 2, 512); g.fillRect(0, i * 128 - 1, 512, 2); }
            });
            var carpet = Mat.Lit(new MatSpec { map = kit.Texture(carpetC), normalMap = kit.Texture(kit.NormalFrom(carpetC, 1.5f), false), repeat = new Vector2(0.5f, 0.5f), roughness = 0.97f });
            stat.Add(kit.Plane(18, 12, carpet, A(z: -6)));
            add(kit.Slab(0.25f, H, 12.5f, M.wall, A(-9.125f, 0, -6)), true); add(kit.Slab(0.25f, H, 12.5f, M.wall, A(9.125f, 0, -6)), true);
            foreach (var (x0, x1) in new[] { (-9f, DOOR - 0.5f), (DOOR + 0.5f, 9f) }) add(kit.Slab(x1 - x0, H, 0.25f, M.wall, A((x0 + x1) / 2, 0, 0.125f)), true);
            stat.Add(kit.Slab(1.0f, H - 2.1f, 0.25f, M.wall, A(DOOR, 2.1f, 0.125f)));
            solids.Add(new Solid(DOOR - 0.5f, DOOR + 0.5f, 0.02f, 0.6f, 0, H, "door"));
            add(kit.Slab(18.5f, 0.8f, 0.3f, M.wall, A(0, 0, -12.15f)), true);
            stat.Add(kit.Slab(18.5f, H - 2.45f, 0.3f, M.wall, A(0, 2.45f, -12.15f)));
            stat.Add(kit.Box(18, 0.04f, 0.32f, M.white, A(0, 0.8f, -11.97f)));
            for (int i = 0; i <= 12; i++) stat.Add(kit.Box(0.06f, 1.65f, 0.12f, M.frame, A(-9 + i * 1.5f, 0.8f, -12.1f)));
            keep(kit.Box(18, 1.65f, 0.02f, M.glass, A(0, 0.8f, -12.08f, cast: false)));
            solids.Add(new Solid(-9, 9, -12.4f, -11.82f, 0, H, "windowWall"));
            var slats = Mat.Lit(new MatSpec { map = kit.Texture(kit.Canvas(64, 64, (g, W, Hh) => { g.fillStyle = "#e4e2dc"; g.fillRect(0, 0, 64, 64); g.fillStyle = "#b9b6ae"; for (int y = 0; y < 64; y += 8) g.fillRect(0, y, 64, 2); })), repeat = new Vector2(1, 8), roughness = 0.7f });
            for (int i = 0; i < 12; i++)
            {
                float x = -8.25f + i * 1.5f, low = i >= 9 ? 0.75f : i == 4 ? 0.3f : 0;
                stat.Add(kit.Box(1.42f, 0.1f, 0.08f, M.white, A(x, 2.33f, -11.92f, cast: false)));
                if (low != 0) stat.Add(kit.Box(1.42f, low, 0.01f, slats, A(x, 2.33f - low, -11.9f)));
            }
            stat.Add(kit.Slab(18.5f, 0.1f, 12.5f, Mat.Lit(new MatSpec
            {
                map = kit.Texture(kit.Canvas(256, 256, (g, W, Hh) =>
                {
                    var q = Kit.Rng(6); g.fillStyle = "#efeeea"; g.fillRect(0, 0, 256, 256); g.fillStyle = "rgba(0,0,0,0.04)"; for (int i = 0; i < 900; i++) g.fillRect(q() * 256, q() * 256, 2, 2);
                    g.fillStyle = "#c9c8c3"; for (int i = 0; i < 256; i += 64) { g.fillRect(i, 0, 3, 256); g.fillRect(0, i, 256, 3); }
                })), repeat = new Vector2(1 / 2.4f, 1 / 2.4f), roughness = 0.9f,
            }), A(0, H, -6)));
            foreach (var (x, z, w, d) in new[] { (-8.99f, -6f, 0.02f, 12f), (8.99f, -6f, 0.02f, 12f), (0f, -0.01f, 18f, 0.02f) }) stat.Add(kit.Box(w, 0.1f, d, M.black, A(x, 0, z, cast: false)));

            // ---- desk islands: desks, partitions, dual monitors, clutter; chairs are instanced ----
            var chairs = new List<Xform>(); var mugs = new[] { "#ffffff", "#1f4f8c", "#c4161c", "#2f8a57", "#f2c230", "#6b5b95" };
            void monitor(float x, float z, float ry, float w, float h, int? cell, float y = 0.98f)
            {
                float f0 = Mathf.Sin(ry), f1 = Mathf.Cos(ry);
                stat.Add(kit.Box(0.22f, 0.015f, 0.18f, M.black, A(x - f0 * 0.08f, 0.715f, z - f1 * 0.08f, ry)));
                stat.Add(kit.Box(0.04f, y - 0.72f, 0.03f, M.black, A(x - f0 * 0.1f, 0.72f, z - f1 * 0.1f, ry)));
                stat.Add(kit.Box(w + 0.02f, h + 0.02f, 0.025f, M.black, A(x, y - h / 2 - 0.01f, z, ry)));
                if (cell.HasValue) stat.Add(flat(OfficeDraw.CellPlane(w, h, cell.Value), M.screens, x + f0 * 0.0135f, y, z + f1 * 0.0135f, ry));
            }
            void desk(float cx, float zc, int s, bool mine)
            {
                float zf = zc + s * 0.35f, ry = s > 0 ? 0 : PI;
                At at(float dx, float dz, float y = 0, float ary = 0) => A(cx + dx, y, zc + s * dz, ary);
                stat.Add(kit.Box(1.38f, 0.03f, 0.7f, M.lam, A(cx, 0.69f, zf)));
                foreach (var dx in new[] { -0.66f, 0.66f }) stat.Add(kit.Box(0.04f, 0.69f, 0.6f, M.frame, A(cx + dx, 0, zf)));
                stat.Add(kit.Box(0.42f, 0.56f, 0.5f, M.ped, at(0.4f, 0.34f)));
                if (!mine) { monitor(cx - 0.29f, zc + s * 0.16f, ry, 0.53f, 0.3f, (int)(r() * 4)); monitor(cx + 0.29f, zc + s * 0.16f, ry, 0.53f, 0.3f, (int)(r() * 4)); }
                stat.Add(kit.Box(0.44f, 0.02f, 0.14f, M.black, at(0, 0.5f, 0.72f))); stat.Add(kit.Box(0.06f, 0.025f, 0.1f, M.black, at(0.32f, 0.5f, 0.72f)));
                stat.Add(kit.Box(0.18f, 0.06f, 0.2f, M.black, at(-0.52f, 0.42f, 0.72f, s * 0.3f)));
                stat.Add(kit.Cyl(0.04f, 0.04f, 0.1f, kit.Mat("paint:" + mugs[(int)(r() * mugs.Length)], new MatSpec { roughness = 0.3f }), at(0.5f, 0.25f, 0.72f), 12));
                for (int k = 0; k < 1 + r() * 3; k++) { float dx = -0.15f + r() * 0.3f, dz = 0.42f + r() * 0.1f; stat.Add(kit.Box(0.21f, 0.004f, 0.297f, M.paper, at(dx, dz, 0.72f + k * 0.004f, (r() - 0.5f) * 0.6f))); }
                if (r() < 0.5f) for (int k = 0; k < 3; k++) stat.Add(kit.Box(0.06f, 0.3f, 0.24f, kit.Mat("paint:" + new[] { "#1f4f8c", "#c4161c", "#2f8a57", "#3b3f46" }[(int)(r() * 4)]), at(-0.62f + k * 0.065f, 0.2f, 0.72f)));
                else if (r() < 0.5f) { stat.Add(kit.Cyl(0.06f, 0.05f, 0.1f, M.white, at(0.55f, 0.15f, 0.72f), 10)); stat.Add(kit.Cyl(0.002f, 0.11f, 0.18f, kit.Mat("foliage"), at(0.55f, 0.15f, 0.82f), 8)); }
                if (mine) chairs.Add(X(cx + 0.35f, 0, zc + s * 1.45f, ry + 0.5f));
                else { float x = cx + (r() - 0.5f) * 0.2f, z = zc + s * (1.05f + r() * 0.25f); chairs.Add(X(x, 0, z, ry + (r() - 0.5f) * 0.7f)); }
            }
            foreach (var zc in ISL)
            {
                foreach (var s in new[] { -1, 1 }) foreach (var cx in DX) desk(cx, zc, s, zc == ME.z && s > 0 && cx == ME.x);
                stat.Add(kit.Box(5.66f, 0.44f, 0.04f, M.fabric, A(-3.7f, 0.7f, zc))); stat.Add(kit.Box(5.66f, 0.025f, 0.06f, M.frame, A(-3.7f, 1.14f, zc)));
                solids.Add(new Solid(-6.55f, -0.85f, zc - 0.72f, zc + 0.72f, 0, 1.2f, "island"));
            }
            // The player's desk: dashboard monitor (director canvas), messenger, a sticky note and a name plate.
            monitor(ME.x - 0.26f, ME.z + 0.17f, 0, 0.62f, 0.39f, null, 1.04f);
            monitor(ME.x + 0.36f, ME.z + 0.17f, -0.25f, 0.53f, 0.3f, 3);
            var dashPaint = kit.Canvas(1024, 640, (g, W, Hh) => { g.fillStyle = "#0d1418"; g.fillRect(0, 0, 1024, 640); g.fillStyle = "#7fd1c4"; OfficeDraw.Font(g, 40); g.fillText("운영 대시보드", 40, 80); g.fillStyle = "#8fa3ab"; OfficeDraw.Font(g, 28, 500); g.fillText("데이터를 불러오는 중…", 40, 150); });
            var dashTex = kit.Texture(dashPaint); int dashW = dashPaint.width, dashH = dashPaint.height;
            var dash = keep(flat(Geo.Plane(0.62f, 0.39f), ScreenMat(dashTex, 0.9f), ME.x - 0.26f, 1.04f, ME.z + 0.184f));
            sign(new[] { "서해안 노선", "내일 확인!" }, new SignOpts { w = 0.075f, h = 0.075f, bg = "#ffe38a", fg = "#7a3b12" }, ME.x - 0.585f, 1.15f, ME.z + 0.187f, 0.05f, null, null);
            sign(new[] { "운영팀 · 정민" }, new SignOpts { w = 0.24f, h = 0.06f, bg = "#1f4f8c", fg = "#ffffff" }, ME.x - 0.45f, 0.755f, ME.z + 0.69f, 0, null, null);

            // ---- team lead desk (윤 팀장), wall screen, whiteboard, cooler, printer, cabinets, door ----
            stat.Add(kit.Box(0.8f, 0.03f, 1.8f, M.lam, A(6.7f, 0.69f, LEAD.z)));
            stat.Add(kit.Box(0.04f, 0.69f, 1.8f, M.lam, A(6.32f, 0, LEAD.z))); stat.Add(kit.Box(0.5f, 0.56f, 0.42f, M.ped, A(6.85f, 0, LEAD.z + 0.6f)));
            solids.Add(new Solid(6.28f, 7.12f, LEAD.z - 0.92f, LEAD.z + 0.92f, 0, 1.2f, "leadDesk"));
            monitor(6.5f, LEAD.z - 0.3f, PI / 2, 0.53f, 0.3f, 1); monitor(6.5f, LEAD.z + 0.3f, PI / 2, 0.53f, 0.3f, 0);
            stat.Add(kit.Box(0.2f, 0.06f, 0.18f, M.black, A(6.8f, 0.72f, LEAD.z - 0.7f)));
            for (int k = 0; k < 6; k++) stat.Add(kit.Box(0.297f, 0.004f, 0.21f, M.paper, A(6.85f, 0.72f + k * 0.006f, LEAD.z + 0.25f, (r() - 0.5f) * 0.3f)));
            sign(new[] { "운영팀장 윤" }, new SignOpts { w = 0.26f, h = 0.07f, bg = "#3b3f46", fg = "#ffffff" }, 6.29f, 0.55f, LEAD.z, -PI / 2, null, null);
            chairs.Add(X(LEAD.x, 0, LEAD.z, PI / 2));
            var wallPaint = kit.Canvas(1280, 720); var wallTex = kit.Texture(wallPaint);
            int wallAlerts = 0; bool blink = false;
            void redrawWall() { OfficeDraw.DrawWall(wallPaint, 1280, 720, wallAlerts, blink); wallPaint.Upload(wallTex); }
            redrawWall();
            stat.Add(kit.Box(0.06f, 1.48f, 2.56f, M.black, A(-8.97f, 0.96f, -6.5f)));
            keep(flat(Geo.Plane(2.5f, 1.4f), ScreenMat(wallTex, 0.95f), -8.935f, 1.7f, -6.5f, PI / 2));
            add(kit.Box(0.42f, 0.72f, 2.6f, M.white, A(-8.79f, 0, -6.5f)), true);
            stat.Add(kit.Box(0.3f, 0.05f, 0.22f, M.black, A(-8.8f, 0.72f, -7.4f)));
            var bk = new[] { "#1f4f8c", "#2f8a57", "#c4161c", "#f2c230", "#3b3f46" };
            for (int k = 0; k < 5; k++) stat.Add(kit.Box(0.24f, 0.3f, 0.05f, kit.Mat("paint:" + bk[k]), A(-8.8f, 0.72f, -5.6f - k * 0.06f)));
            var boardTex = kit.Texture(kit.Canvas(1024, 512, (g, w, h) => OfficeDraw.DrawBoard(g, w, h)));
            stat.Add(kit.Box(0.03f, 1.26f, 2.46f, M.frame, A(8.985f, 0.87f, -3.6f)));
            keep(flat(Geo.Plane(2.4f, 1.2f), Mat.Lit(new MatSpec { map = boardTex, roughness = 0.25f }), 8.965f, 1.5f, -3.6f, -PI / 2));
            stat.Add(kit.Box(0.08f, 0.03f, 1.2f, M.frame, A(8.93f, 0.86f, -3.6f)));
            foreach (var (z, c) in new[] { (-3.2f, "#c4161c"), (-3.1f, "#1f4f8c"), (-3.0f, "#2b2b2b") }) stat.Add(kit.Box(0.02f, 0.02f, 0.12f, kit.Mat("paint:" + c), A(8.93f, 0.89f, z, cast: false)));
            // water cooler
            add(kit.Box(0.34f, 1.0f, 0.34f, M.white, A(8.62f, 0, -1.5f)), true);
            stat.Add(kit.Box(0.02f, 0.05f, 0.04f, kit.Mat("paint:#c4161c"), A(8.44f, 0.82f, -1.57f))); stat.Add(kit.Box(0.02f, 0.05f, 0.04f, kit.Mat("paint:#1f6fb2"), A(8.44f, 0.82f, -1.43f)));
            stat.Add(kit.Box(0.1f, 0.02f, 0.24f, M.dark, A(8.47f, 0.62f, -1.5f))); stat.Add(kit.Cyl(0.04f, 0.04f, 0.4f, M.white, A(8.62f, 0.5f, -1.24f), 10));
            keep(kit.Cyl(0.135f, 0.135f, 0.42f, Mat.Lit(new MatSpec { color = J.Hex(0x7fb6e8), roughness = 0.08f, transparent = true, opacity = 0.55f, depthWrite = false }), A(8.62f, 1.0f, -1.5f, cast: false), 18));
            // printer, paper, cabinets, plants
            add(kit.Box(0.62f, 1.0f, 0.62f, kit.Mat("paint:#d7d9db", new MatSpec { roughness = 0.5f }), A(8.5f, 0, -10.8f)), true);
            stat.Add(kit.Box(0.6f, 0.08f, 0.5f, M.black, A(8.5f, 1.0f, -10.8f)));
            stat.Add(flat(OfficeDraw.CellPlane(0.16f, 0.09f, 1), M.screens, 8.18f, 0.95f, -10.8f, -PI / 2));
            for (int k = 0; k < 4; k++) stat.Add(kit.Box(0.3f, 0.05f, 0.21f, M.paper, A(8.55f, k * 0.05f, -10.2f, (r() - 0.5f) * 0.2f)));
            var doorsTex = kit.Texture(kit.Canvas(256, 128, (g, W, Hh) => { g.fillStyle = "#c3c7cb"; g.fillRect(0, 0, 256, 128); g.fillStyle = "#9aa0a6"; foreach (var x in new[] { 0, 127, 254 }) g.fillRect(x, 0, 2, 128); g.fillStyle = "#5d6368"; foreach (var x in new[] { 112, 140 }) g.fillRect(x, 50, 4, 28); }));
            var cab = Mat.Lit(new MatSpec { map = doorsTex, metalness = 0.4f, roughness = 0.45f });
            var binders = new[] { "#1f4f8c", "#c4161c", "#2f8a57", "#3b3f46", "#f2c230" };
            foreach (var x in new[] { -7.6f, -5.8f, -4.0f })
            {
                add(kit.Box(1.78f, 1.05f, 0.45f, cab, A(x, 0, -0.35f)), true);
                for (int k = 0; k < 6; k++) stat.Add(kit.Box(0.06f, 0.3f, 0.26f, kit.Mat("paint:" + binders[(int)(r() * 5)]), A(x - 0.6f + k * 0.07f, 1.05f, -0.35f)));
            }
            foreach (var (x, z) in new[] { (-8.6f, -11.4f), (8.5f, -11.4f), (-2.7f, -0.4f) })
            {
                add(kit.Cyl(0.2f, 0.16f, 0.42f, M.white, A(x, 0, z), 14), true);
                var plant = Geo.Obj(Geo.Icosahedron(0.36f, 1), kit.Mat("foliage")); J.SetPos(plant, x, 0.85f, z); stat.Add(plant);
            }
            // door: glass leaf, card reader, exit sign, a corridor behind it
            keep(kit.Box(0.96f, 2.08f, 0.02f, M.glass, A(DOOR, 0, 0.1f, cast: false)));
            foreach (var (w, h, x, y) in new[] { (1.0f, 0.05f, DOOR, 0f), (1.0f, 0.05f, DOOR, 2.05f), (0.05f, 2.1f, DOOR - 0.48f, 0f), (0.05f, 2.1f, DOOR + 0.48f, 0f) }) stat.Add(kit.Box(w, h, 0.05f, M.frame, A(x, y, 0.1f)));
            stat.Add(kit.Box(0.03f, 0.5f, 0.03f, M.steel, A(DOOR + 0.36f, 0.8f, 0.05f)));
            stat.Add(kit.Box(0.08f, 0.12f, 0.03f, M.black, A(DOOR + 0.75f, 1.2f, -0.015f)));
            sign(new[] { "●" }, new SignOpts { w = 0.02f, h = 0.02f, bg = "#000000", fg = "#5aff9a", emissive = 1.5f }, DOOR + 0.75f, 1.24f, -0.032f, PI, "indoor", null);
            sign(new[] { "비상구" }, new SignOpts { w = 0.5f, h = 0.18f, bg = "#1f9a4a", fg = "#ffffff", emissive = 1.2f }, DOOR, 2.42f, -0.01f, PI, "emergency", null);
            stat.Add(kit.Plane(3, 3, kit.Surface("tile"), A(DOOR, 0, 1.6f))); stat.Add(kit.Slab(3, H, 0.1f, M.wall, A(DOOR, 0, 3.1f)));
            sign(new[] { "한결물류 HANGYEOL", "연무센터 운영팀" }, new SignOpts { w = 1.7f, h = 0.48f, bg = "#ffffff", fg = "#18806f", border = "#18806f" }, 3.0f, 1.75f, -0.01f, PI, null, null);
            keep(flat(Geo.Plane(0.34f, 0.34f), Mat.Lit(new MatSpec { map = kit.Texture(kit.Canvas(256, 256, (g, w, h) => OfficeDraw.DrawClock(g, w))), roughness = 0.4f, transparent = true }), 0, 2.2f, -0.012f, PI));

            var (view, hazard) = OfficeYard.Build(kit, yard, dyn, solids, r, M, sign);

            // ---- chairs (instanced), lights ----
            GeoData T(GeoData g, float x, float y, float z, float rx = 0, float ry = 0, float rz = 0) => g.RotateX(rx).RotateY(ry).RotateZ(rz).Translate(x, y, z);
            var legs = Enumerable.Range(0, 5).Select(i => { float a = i / 5f * PI * 2; return T(Geo.Box(0.05f, 0.03f, 0.3f), 0, 0.06f, 0, 0, a).Translate(Mathf.Sin(a) * 0.15f, 0, Mathf.Cos(a) * 0.15f); }).ToList();
            var chairMetal = Geo.Merge(legs.Concat(new[] { T(Geo.Cylinder(0.03f, 0.03f, 0.36f, 8), 0, 0.26f, 0), T(Geo.Box(0.04f, 0.22f, 0.04f), -0.25f, 0.56f, 0.02f), T(Geo.Box(0.04f, 0.22f, 0.04f), 0.25f, 0.56f, 0.02f),
                T(Geo.Box(0.06f, 0.03f, 0.26f), -0.25f, 0.68f, 0.02f), T(Geo.Box(0.06f, 0.03f, 0.26f), 0.25f, 0.68f, 0.02f), T(Geo.Box(0.04f, 0.3f, 0.03f), 0, 0.5f, 0.25f, -0.15f) }).ToList());
            var chairSoft = Geo.Merge(new[] { T(Geo.Box(0.48f, 0.08f, 0.46f), 0, 0.46f, 0), T(Geo.Box(0.46f, 0.52f, 0.06f), 0, 0.88f, 0.27f, -0.12f) });
            dyn.Add(kit.Instanced(chairMetal, M.black, chairs)); dyn.Add(kit.Instanced(chairSoft, M.chair, chairs));
            var fixtures = new List<Xform>();
            for (int i = 0; -7.8f + i * 2.4f <= 7.9f; i++) for (int j = 0; -1.5f - j * 1.8f >= -10.6f; j++) fixtures.Add(X(-7.8f + i * 2.4f, H - 0.03f, -1.5f - j * 1.8f));
            dyn.Add(Panels(kit, fixtures, w: 0.6f, d: 0.6f, color: "#f4f1ea", on: 2.0f).obj);
            dyn.Add(Lamps(kit, new[] { (-6f, -3.5f), (-1.5f, -3.5f), (-3.5f, -9.5f), (4.5f, -5.5f), (4.5f, -10f) }.Select(p => X(p.Item1, H - 0.35f, p.Item2)).ToList(), J.Hex(0xfff4e6), 9, 11));

            // ---- people ----
            var coworker = Npc(people, "coworker", 3, new NpcAt { x = LEAD.x, z = LEAD.z, ry = PI / 2, anim = "sitChair" });
            var staff1 = Npc(people, "crowd", 61, new NpcAt { x = -4.4f, z = ISL[1] + 1.15f, ry = 0, anim = "sitChair" });
            var staff2 = Npc(people, "crowd", 62, new NpcAt { x = 4.6f, z = -11.35f, ry = 0.2f, anim = "phone" });
            // As in the JS this runs after the chairs were instanced, so it moves nothing on screen.
            foreach (var c in chairs) if (Mathf.Abs(c.x + 4.4f) < 0.2f && Mathf.Abs(c.z - ISL[1] - 1.15f) < 0.3f) { c.x = -4.4f; c.z = ISL[1] + 1.15f; c.ry = 0; }
            var worker = Npc(people, "crowd", 63, new NpcAt { x = -7, y = GY, z = -18.6f, ry = -PI / 2, anim = "walk" }); worker.SetSpeed(1.1f);
            var driver = Npc(people, "crowd", 64, new NpcAt { x = 35.4f, y = GY, z = -32.8f, ry = 2.6f, anim = "phone" });
            var crew = new[] { coworker, staff1, staff2, worker, driver };
            foreach (var p in crew) dyn.Add(p.Group);
            float pace = 1;

            // ---- state ----
            int alerts = 0; Paint dashboard = null; string outside = null;
            var dashRend = dash.GetComponent<Renderer>();
            void setState(string key, object value)
            {
                if (key == "dashboard")
                {
                    var v = value as Paint;
                    if (v == null || !(v.width > 0) || !(v.height > 0)) { Debug.LogError($"[office] dashboard must be a canvas with a size {value}"); return; }
                    dashboard = v;
                    if (v.width == dashW && v.height == dashH) v.Upload(dashTex);
                    else { var old = dashTex; dashTex = kit.Texture(v); dashW = v.width; dashH = v.height; if (dashRend != null) dashRend.sharedMaterial = ScreenMat(dashTex, 0.9f); UnityEngine.Object.Destroy(old); }
                }
                else if (key == "alerts")
                {
                    float f = value == null ? 0 : Sv.Num(value, float.NaN); int n = float.IsNaN(f) ? -1 : Round(f); // JS Number(null) = 0
                    if (!(n >= 0 && n <= 3)) { Debug.LogError($"[office] alerts must be 0..3 {value}"); return; }
                    alerts = wallAlerts = n; redrawWall();
                }
                else if (key == "outside") { var v = Sv.Str(value); if (v != null && !MOODS.Contains(v)) { Debug.LogError($"[office] outside must be null or {string.Join(",", MOODS)} {v}"); return; } outside = v; }
                else Debug.LogError($"[office] unknown state {key} {value}");
            }

            var points = new List<Point>
            {
                Pt("desk", "내 자리 · 대시보드 확인", ME.x, ME.z + 1.3f, "desk", 1.5f), Pt("coworker", "윤 팀장", 5.75f, LEAD.z, "npc", 1.8f),
                Pt("window", "창밖 보기 · 물류센터 마당", 0, -11.2f, "look", 2.6f), Pt("officeExit", "퇴근하기 · 출입문", DOOR, -0.7f, "door", 1.4f),
            };

            kit.Merge(stat); kit.Merge(yard);
            float acc = 0;
            return new Level
            {
                id = "office", group = group, env = "interior",
                walk = new List<WalkZone> { new WalkZone(-9, 9, -12, 0, 0) },
                solids = solids, points = points,
                spawns = new Dictionary<string, Spawn> { ["entry"] = new Spawn(DOOR, 0, -1.3f, 0), ["desk"] = new Spawn(ME.x, 0, ME.z + 1.25f, 0) },
                npcs = new Dictionary<string, IPerson> { ["coworker"] = coworker, ["staff1"] = staff1, ["staff2"] = staff2, ["worker"] = worker, ["driver"] = driver },
                states = new Dictionary<string, object> { ["alerts"] = new[] { 0, 1, 2, 3 }, ["dashboard"] = new[] { "(canvas)" }, ["outside"] = new[] { null, "day", "dusk", "night", "blackout" } },
                setState = (key, v) => { if (key == "dashboard" && v is string s && s == "(canvas)") return; setState(key, v); },
                update = (dt, t, ctx) =>
                {
                    view.Set(outside ?? MoodFor(ctx?.env, ctx?.env == "interiorNight" ? "night" : "dusk"));
                    // JS polls the reduce-motion class once a second; the frame context carries the same flag.
                    bool reduced = ctx != null && ctx.reduced;
                    Mat.SetEmissiveIntensity(hazard, reduced ? 2 : Mathf.Sin(t * PI * 2.6f) > 0 ? 3 : 0); // reduced motion: steady lamp, rows stop blinking
                    acc += dt; if (acc > 0.6f) { acc = 0; bool b = wallAlerts != 0 && !reduced ? !blink : false; if (b != blink) { blink = b; redrawWall(); } }
                    var wp = J.Pos(worker.Group); wp.x += pace * 1.1f * dt; // paces between the pallet stacks below the window
                    if (wp.x > 6 || wp.x < -8) { pace = -pace; wp.x = Mathf.Max(-8, Mathf.Min(6, wp.x)); }
                    J.SetPos(worker.Group, wp); J.SetYaw(worker.Group, pace > 0 ? -PI / 2 : PI / 2);
                    foreach (var p in crew) p.Update(dt);
                },
                anchors = new Dictionary<string, Anchor>
                {
                    ["deskCam"] = new Anchor { x = ME.x, y = 1.2f, z = ME.z + 1.15f, look = new Vector3(ME.x - 0.2f, 1.0f, ME.z + 0.18f) },
                    ["wallScreen"] = new Anchor { x = -6.2f, y = 1.6f, z = -6.5f, look = new Vector3(-8.9f, 1.7f, -6.5f) },
                    ["window"] = new Anchor { x = 0, y = 1.6f, z = -11.3f, look = new Vector3(0, GY, -40) },
                    ["lead"] = new Anchor { x = 5.6f, y = 1.6f, z = LEAD.z, look = new Vector3(LEAD.x, 1.1f, LEAD.z) },
                },
            };
        }
    }
}
