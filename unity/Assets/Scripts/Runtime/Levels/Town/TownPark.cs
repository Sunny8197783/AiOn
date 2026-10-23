// Ports levels/town-park.js: 연무공원 (paths, playground, 정자, benches, outdoor exercise frames, park lamps) and
// every tree in town (street ginkgos in pits, park zelkovas and maples, complex rows) instanced, plus shrubs from
// TownProps. TreeSpots() is pure so props can avoid trees. Trees and leaves are planted in TownParkTrees.cs.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Levels.TownUtil;

namespace Gyeongbo.Levels
{
    // A tree or shrub: { x, z, type, s, ry, tint }; type ginkgo | zelkova | maple | shrub.
    public sealed class TreeSpot
    {
        public float x, z, s, ry, tint;
        public string type;
    }

    public sealed class TownParkResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public List<WalkZone> walk = new List<WalkZone>();
        public List<TreeSpot> spots = new List<TreeSpot>();
    }

    public static class TownPark
    {
        const float Y = TownPlan.CURB;
        const float PI = Mathf.PI;

        public static readonly List<XZ> PARK_PATH = new List<XZ> { new XZ(0, 12), new XZ(0, 14), new XZ(-15, 22), new XZ(-15, 40), new XZ(-30, 44), new XZ(-38, 30.5f), new XZ(-42, 30.5f) };
        static readonly List<XZ>[] BRANCHES = { new List<XZ> { new XZ(-15, 32), new XZ(-3.6f, 37.2f) }, new List<XZ> { new XZ(-15, 25.5f), new XZ(-8.5f, 24.5f) } };
        static readonly RectXZ PLAY = new RectXZ(-37, -23, 14.5f, 26), GYM = new RectXZ(-10, -1, 20.5f, 28.5f);
        const float PAV_X = -1, PAV_Z = 38, PAV_S = 4.8f;

        static float SegDist(XZ p, XZ a, XZ b)
        {
            float dx = b.x - a.x, dz = b.z - a.z, t = Math.Max(0, Math.Min(1, ((p.x - a.x) * dx + (p.z - a.z) * dz) / (dx * dx + dz * dz)));
            return Hypot(p.x - a.x - dx * t, p.z - a.z - dz * t);
        }
        static bool NearPath(XZ p, float d)
        {
            foreach (var L in Paths()) for (int i = 1; i < L.Count; i++) if (SegDist(p, L[i - 1], L[i]) < d) return true;
            return false;
        }
        static IEnumerable<List<XZ>> Paths() { yield return PARK_PATH; foreach (var b in BRANCHES) yield return b; }
        static bool InRect(XZ p, RectXZ r, float m = 0) => p.x > r.x0 - m && p.x < r.x1 + m && p.z > r.z0 - m && p.z < r.z1 + m;

        // Every tree in town: [{ x, z, type, s, ry, tint }]. Street trees stand in pits 1.4 m behind the curb.
        public static List<TreeSpot> TreeSpots(Func<float> rand)
        {
            var output = new List<TreeSpot>();
            void Add(float x, float z, string type) => output.Add(new TreeSpot { x = x, z = z, type = type, s = 0.85f + rand() * 0.3f, ry = rand() * 6.28f, tint = rand() });
            bool Near(float v, IEnumerable<float> list, float d) { foreach (var a in list) if (Math.Abs(v - a) < d) return true; return false; }
            var cwY = new List<float>();
            foreach (var c in TownMap.CROSSWALKS) if (c.road == "yeonmu") cwY.Add((float)c.at);
            var cwJ = new List<float>(cwY) { -50, 50 };
            foreach (var z in new[] { -6.9f, 6.9f }) for (int x = -124; x <= 124; x += 9)
            {
                if (Near(x, cwJ, 5.5f)) continue;
                if (z < 0 && ((x > -21 && x < -10) || (x > 60 && x < 74) || (x > 82 && x < 92) || (x > 7 && x < 17))) continue;
                if (z > 0 && Near(x, new float[] { -86, -104, 36, 78, -94 }, 3.6f)) continue;
                Add(x, z, "ginkgo");
            }
            var cols = new (float x, (float a, float b)[] skip)[]
            {
                (-54.9f, new[] { (-52f, -25f), (24f, 37f), (43f, 63f) }), (-45.1f, new[] { (25f, 36f) }), (45.1f, new (float, float)[0]), (54.9f, new[] { (13f, 27f) }),
            };
            foreach (var (x, skip) in cols)
                for (int z = -104; z <= 104; z += 10)
                {
                    bool skipped = false; foreach (var (a, b) in skip) if (z > a && z < b) skipped = true;
                    if (Math.Abs(z) < 18 || Math.Abs(z + 60) < 7 || Math.Abs(z - 60) < 7 || skipped) continue;
                    Add(x, z, "ginkgo");
                }
            for (int x = -39; x <= 9; x += 6) for (int z = 15; z <= 49; z += 6)
            {
                var p = new XZ(x + (rand() - 0.5f) * 3, z + (rand() - 0.5f) * 3);
                if (rand() < 0.35f || NearPath(p, 2.8f) || InRect(p, PLAY, 1.5f) || InRect(p, GYM, 1) || Hypot(p.x - PAV_X, p.z - PAV_Z) < 5.5f) continue;
                if (p.x < -40 || p.x > 10.5f || p.z < 14 || p.z > 50.5f) continue;
                Add(p.x, p.z, rand() < 0.6f ? "zelkova" : "maple");
            }
            // Rows between the apartment blocks keep a zelkova crown (up to ~5.7 m wide) clear of the south balconies.
            foreach (var z in new[] { 49.5f, 77.5f, 105f }) for (float x = 64; x <= 124; x += 7.5f) Add(x + (rand() - 0.5f), z, (int)(x / 7.5f) % 3 == 0 ? "maple" : "zelkova");
            for (int x = 63; x <= 125; x += 9) Add(x, 11.6f, "zelkova");
            // By 103동's west gable only small maples fit (a zelkova crown went through the balconies); none in the
            // 4 m gap between 103동 and the management building.
            foreach (var (x, z) in new[] { (60f, 32f), (60f, 37f) }) Add(x, z, "maple");
            foreach (var (x, z) in new[] { (-127f, 20f), (-122f, 27f), (-127f, 34f), (-118f, 41f), (-121f, 44f) }) Add(x, z, "zelkova");
            return output;
        }

        // spots: all trees and shrubs to plant (Town passes TreeSpots + TownProps.ShrubSpots).
        public static TownParkResult BuildPark(Kit kit, List<TreeSpot> spots)
        {
            var group = J.Group("park");
            var stat = J.Group("static", group);
            var solids = new List<Solid>(); var walk = new List<WalkZone>();
            var rand = Kit.Rng(4242);
            spots ??= TreeSpots(Kit.Rng(77));
            TownParkTrees.Plant(kit, group, solids, spots, rand);

            // ---- 연무공원: paths ----
            var pathMat = kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0xd8c39d), roughness = 0.96f });
            void Disc(XZ p, float w) { var m = Obj(Geo.Circle(w / 2, 20).RotateX(-PI / 2), pathMat, false, true); J.SetPos(m, p.x, Y + 0.012f, p.z); stat.Add(m); }
            foreach (var L in Paths())
            {
                float w = L == PARK_PATH ? 2.6f : 1.8f;
                for (int i = 1; i < L.Count; i++) stat.Add(TownGround.Strip(L[i - 1], L[i], w, Y + 0.012f, pathMat));
                for (int i = 1; i < L.Count; i++) Disc(L[i], w);
            }
            // Curb stones along the main path.
            var edge = kit.Mat("curb", new MatSpec { roughness = 0.85f });
            for (int i = 2; i < PARK_PATH.Count; i++)
            {
                XZ a = PARK_PATH[i - 1], b = PARK_PATH[i]; float dx = b.x - a.x, dz = b.z - a.z, len = Hypot(dx, dz), nx = -dz / len, nz = dx / len;
                foreach (var s in new[] { -1, 1 }) stat.Add(kit.Box(len, 0.08f, 0.1f, edge, At((a.x + b.x) / 2 + nx * 1.35f * s, Y, (a.z + b.z) / 2 + nz * 1.35f * s, -Mathf.Atan2(dz, dx), false)));
            }

            // ---- playground (놀이터) ----
            var epdm = kit.Memo("town:epdm", () =>
            {
                var r = Kit.Rng(9); string[] cs = { "#4a8a5e", "#346b45", "#5a9a6a", "#2e5f3e" };
                var c = kit.Canvas(256, 256, (g, w, h) => { g.fillStyle = "#3f7a52"; g.fillRect(0, 0, w, w); for (int i = 0; i < 9000; i++) { g.fillStyle = cs[Idx(r(), 4)]; g.fillRect(r() * w, r() * w, 1.5f, 1.5f); } });
                return Mat.Lit(new MatSpec { map = kit.Texture(c), repeat = new Vector2(1 / 2f, 1 / 2f), roughness = 0.95f });
            });
            stat.Add(kit.Plane(PLAY.x1 - PLAY.x0, PLAY.z1 - PLAY.z0, epdm, At((PLAY.x0 + PLAY.x1) / 2, Y + 0.02f, (PLAY.z0 + PLAY.z1) / 2)));
            stat.Add(kit.Box(PLAY.x1 - PLAY.x0 + 0.3f, 0.12f, 0.15f, edge, At((PLAY.x0 + PLAY.x1) / 2, Y, PLAY.z0)), kit.Box(PLAY.x1 - PLAY.x0 + 0.3f, 0.12f, 0.15f, edge, At((PLAY.x0 + PLAY.x1) / 2, Y, PLAY.z1)));
            Material red = kit.Mat("paint:#c8463a", new MatSpec { roughness = 0.45f }), blue = kit.Mat("paint:#2f6fb5", new MatSpec { roughness = 0.45f }), yel = kit.Mat("paint:#f2c230", new MatSpec { roughness = 0.4f }),
                steel = kit.Mat("steel"), green = kit.Mat("paint:#3f9a5a", new MatSpec { roughness = 0.45f });
            void Tower(float x, float z, Material roof)
            {
                foreach (var (dx, dz) in new[] { (-0.8f, -0.8f), (0.8f, -0.8f), (0.8f, 0.8f), (-0.8f, 0.8f) }) stat.Add(kit.Box(0.12f, 2.5f, 0.12f, steel, At(x + dx, Y, z + dz)));
                stat.Add(kit.Box(1.75f, 0.08f, 1.75f, kit.Surface("wood", new SurfaceOpts { color = J.Hex(0xc8a070) }), At(x, Y + 1.2f, z)));
                var cone = Obj(Geo.Cone(1.45f, 1.0f, 4), roof, true, false); J.SetYaw(cone, PI / 4); J.SetPos(cone, x, Y + 3.0f, z); stat.Add(cone);
                foreach (var s in new[] { -1, 1 }) stat.Add(kit.Box(1.7f, 0.06f, 0.05f, roof, At(x, Y + 2.0f, z + 0.85f * s)));
            }
            Tower(-33, 18.6f, red); Tower(-28.4f, 18.6f, blue);
            stat.Add(kit.Box(2.9f, 0.07f, 0.9f, green, At(-30.7f, Y + 1.2f, 18.6f)));
            foreach (var s in new[] { -1, 1 }) stat.Add(kit.Box(2.9f, 0.05f, 0.05f, yel, At(-30.7f, Y + 1.95f, 18.6f + 0.45f * s)));
            stat.Add(kit.Box(3.2f, 0.06f, 0.6f, yel, At(-26.0f, Y + 0.68f, 18.6f, rz: -0.34f)));
            foreach (var s in new[] { -1, 1 }) stat.Add(kit.Box(3.2f, 0.22f, 0.04f, yel, At(-26.0f, Y + 0.72f, 18.6f + 0.3f * s, rz: -0.34f)));
            for (int i = 0; i < 5; i++) stat.Add(kit.Box(0.05f, 0.05f, 0.6f, steel, At(-34.0f, Y + 0.25f + i * 0.24f, 18.6f)));
            // Swing: two A-frames and a top bar; seats on chains.
            foreach (var x in new[] { -32.6f, -27.4f }) foreach (var s in new[] { -1, 1 }) stat.Add(kit.Cyl(0.05f, 0.05f, 2.5f, steel, At(x, Y, 23.6f + s * 0.3f, rx: -s * 0.24f), 8));
            var bar = kit.Cyl(0.055f, 0.055f, 5.3f, steel, At(-30, Y + 2.4f, 23.6f, rz: PI / 2), 8); var bp = J.Pos(bar); J.SetPos(bar, bp.x, Y + 2.42f, bp.z); stat.Add(bar);
            foreach (var x in new[] { -31.2f, -28.8f })
            {
                foreach (var dx in new[] { -0.22f, 0.22f }) stat.Add(kit.Cyl(0.012f, 0.012f, 1.95f, steel, At(x + dx, Y + 0.47f, 23.6f), 4));
                stat.Add(kit.Box(0.5f, 0.04f, 0.2f, kit.Mat("rubber"), At(x, Y + 0.44f, 23.6f)));
            }
            foreach (var (x, z, m) in new[] { (-24.8f, 23.2f, red), (-24.8f, 21.4f, blue) }) stat.Add(kit.Cyl(0.08f, 0.08f, 0.35f, steel, At(x, Y, z), 8), kit.RBox(0.7f, 0.4f, 0.3f, 0.12f, m, At(x, Y + 0.35f, z)));
            solids.Add(new Solid(-34.3f, -24.2f, 17.6f, 19.6f, -0.5f, 3, "play")); solids.Add(new Solid(-32.8f, -27.2f, 22.9f, 24.3f, -0.5f, 3, "swing"));

            // ---- 정자 pavilion: raised wooden floor you can step onto, round columns, hipped tile roof ----
            Material wood = kit.Surface("wood", new SurfaceOpts { color = J.Hex(0xc49a6c) }), col = kit.Mat("paint:#7a3b2a", new MatSpec { roughness = 0.7f });
            var tile = kit.Memo("town:giwa", () =>
            {
                var hc = kit.Canvas(128, 128, (g, w, h) => { for (int y = 0; y < w; y++) { var v = F(128 + 110 * Math.Sin((double)y / w * Math.PI * 12)); g.fillStyle = $"rgb({v},{v},{v})"; g.fillRect(0, y, w, 1); } });
                return Mat.Lit(new MatSpec { color = J.Hex(0x4b5157), normalMap = kit.Texture(kit.NormalFrom(hc, 4), false), repeat = new Vector2(3, 3), roughness = 0.7f, metalness = 0.1f, flatShading = false });
            });
            float half = PAV_S / 2;
            stat.Add(kit.Slab(PAV_S, 0.4f, PAV_S, wood, At(PAV_X, Y, PAV_Z)));
            foreach (var (dx, dz) in new[] { (-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0) })
            {
                stat.Add(kit.Cyl(0.13f, 0.14f, 2.6f, col, At(PAV_X + dx * (half - 0.2f), Y + 0.4f, PAV_Z + dz * (half - 0.2f)), 12));
                if (dx != 0 && dz != 0) solids.Add(new Solid(PAV_X + dx * (half - 0.2f) - 0.18f, PAV_X + dx * (half - 0.2f) + 0.18f, PAV_Z + dz * (half - 0.2f) - 0.18f, PAV_Z + dz * (half - 0.2f) + 0.18f, -0.5f, 3.5f, "pavilion"));
            }
            foreach (var s in new[] { -1, 1 }) stat.Add(kit.Box(PAV_S, 0.22f, 0.2f, col, At(PAV_X, Y + 2.9f, PAV_Z + s * (half - 0.2f))), kit.Box(0.2f, 0.22f, PAV_S, col, At(PAV_X + s * (half - 0.2f), Y + 2.9f, PAV_Z)));
            var roof = Obj(Geo.Cone(PAV_S * 0.98f, 1.6f, 4, 1, true), tile, true, true); J.SetYaw(roof, PI / 4); J.SetPos(roof, PAV_X, Y + 3.9f, PAV_Z);
            var under = Obj(Geo.Cone(PAV_S * 0.97f, 1.55f, 4, 1, true), kit.Mat("paint:#8a5a3c", new MatSpec { roughness = 0.8f, side = "back" })); J.SetYaw(under, PI / 4); J.SetPos(under, PAV_X, Y + 3.9f - 0.05f, PAV_Z);
            stat.Add(roof, under);
            walk.Add(new WalkZone(PAV_X - half, PAV_X + half, PAV_Z - half, PAV_Z + half, Y + 0.4f));

            // ---- benches (instanced wood + cast legs), facing the path ----
            var woodParts = new List<GeoData>();
            for (int i = 0; i < 3; i++) woodParts.Add(Geo.Box(1.7f, 0.035f, 0.11f).Translate(0, 0.43f, -0.16f + i * 0.13f));
            for (int i = 0; i < 2; i++) woodParts.Add(Geo.Box(1.7f, 0.1f, 0.03f).RotateX(-0.2f).Translate(0, 0.62f + i * 0.14f, 0.17f + i * 0.03f));
            var benchWood = Geo.Merge(woodParts);
            var benchLeg = Geo.Merge(new[] { Geo.Box(0.06f, 0.85f, 0.48f).Translate(-0.7f, 0.42f, 0.02f), Geo.Box(0.06f, 0.85f, 0.48f).Translate(0.7f, 0.42f, 0.02f) });
            var benches = new List<Xform>();
            foreach (var L in Paths()) for (int i = 2; i < L.Count; i++)
            {
                XZ a = L[i - 1], b = L[i]; float dx = b.x - a.x, dz = b.z - a.z, len = Hypot(dx, dz);
                if (len < 7) continue;
                foreach (var k in new[] { 0.33f, 0.7f })
                {
                    float s = benches.Count % 2 != 0 ? 1 : -1, nx = -dz / len * s, nz = dx / len * s;
                    float x = a.x + dx * k + nx * 2.0f, z = a.z + dz * k + nz * 2.0f;
                    if (InRect(new XZ(x, z), PLAY, 0.5f)) continue;
                    benches.Add(new Xform { x = x, y = Y, z = z, ry = YawTo(-nx, -nz) });
                    solids.Add(new Solid(x - 0.6f, x + 0.6f, z - 0.6f, z + 0.6f, -0.5f, 0.9f, "bench"));
                }
            }
            group.Add(kit.Instanced(benchWood, kit.Surface("wood", new SurfaceOpts { color = J.Hex(0xb07a4c), repeat = new Vector2(1, 1) }), benches), kit.Instanced(benchLeg, kit.Mat("darkMetal"), benches));

            // ---- outdoor exercise frames (야외 운동기구) ----
            float gx = (GYM.x0 + GYM.x1) / 2, gz = (GYM.z0 + GYM.z1) / 2;
            foreach (var (x, z) in new[] { (gx - 3, gz - 2), (gx, gz - 2), (gx + 3, gz - 2) })
            {
                stat.Add(kit.Cyl(0.07f, 0.07f, 2.2f, blue, At(x, Y, z), 10), kit.Cyl(0.05f, 0.05f, 1.2f, yel, At(x, Y + 1.5f, z, rz: PI / 2), 8));
                stat.Add(kit.Cyl(0.05f, 0.05f, 0.9f, yel, At(x + 0.45f, Y + 0.3f, z, rx: 0.3f), 8), kit.Box(0.35f, 0.05f, 0.28f, kit.Mat("rubber"), At(x + 0.45f, Y + 0.32f, z + 0.15f)));
                solids.Add(new Solid(x - 0.7f, x + 0.8f, z - 0.5f, z + 0.5f, -0.5f, 2.3f, "gym"));
            }
            foreach (var (x, z) in new[] { (gx - 1.5f, gz + 2), (gx + 1.5f, gz + 2) })
            {
                stat.Add(kit.Cyl(0.08f, 0.08f, 1.1f, yel, At(x, Y, z), 10), kit.Cyl(0.4f, 0.4f, 0.06f, blue, At(x, Y + 0.18f, z), 18));
                solids.Add(new Solid(x - 0.45f, x + 0.45f, z - 0.45f, z + 0.45f, -0.5f, 1.2f, "gym"));
            }
            var gymSign = kit.Sign(new[] { "야외 운동기구", "바른 자세로 천천히 이용하세요" }, new SignOpts { w = 0.9f, h = 0.5f, bg = "#2f6fb5", single = true });
            J.SetPos(gymSign, gx + 4.3f, Y + 1.25f, gz + 3.2f); J.SetYaw(gymSign, -PI / 4); stat.Add(gymSign, kit.Box(0.06f, 1.0f, 0.06f, steel, At(gx + 4.3f, Y, gz + 3.24f)));

            // ---- park lamps (short, globe heads, light group 'street'), bins, fountain, the gate stone ----
            var lampMat = kit.Memo("town:parkLamp", () => kit.Lit("street", Mat.Lit(new MatSpec { color = J.Hex(0xfff3dc), emissive = J.Hex(0xffe2b0), emissiveIntensity = 3, roughness = 0.3f }), 3));
            var lamps = new List<Xform>();
            foreach (var (x, z) in new[] { (-2f, 16f), (-13f, 23f), (-13.4f, 34f), (-22f, 43.6f), (-35f, 36f), (-40f, 32.4f), (3f, 30f), (-19f, 16f) }) lamps.Add(new Xform { x = x, y = Y, z = z });
            group.Add(kit.Instanced(Geo.Cylinder(0.05f, 0.07f, 3.6f, 8).Translate(0, 1.8f, 0), kit.Mat("darkMetal"), lamps));
            group.Add(kit.Instanced(Geo.Sphere(0.2f, 14, 10).Translate(0, 3.75f, 0), lampMat, lamps, false));
            foreach (var p in lamps) solids.Add(new Solid(p.x - 0.15f, p.x + 0.15f, p.z - 0.15f, p.z + 0.15f, -0.5f, 3, "lamp"));
            foreach (var (x, z) in new[] { (-12.8f, 27.6f), (-16.8f, 39f), (-31f, 41.5f), (2f, 15f) })
                stat.Add(kit.Cyl(0.24f, 0.22f, 0.85f, kit.Mat("paint:#3f6b4a", new MatSpec { roughness = 0.5f }), At(x, Y, z), 14), kit.Cyl(0.26f, 0.26f, 0.06f, kit.Mat("darkMetal"), At(x, Y + 0.85f, z), 14));
            stat.Add(kit.Cyl(0.22f, 0.26f, 0.9f, kit.Mat("curb"), At(-16.8f, Y, 20.5f), 10), kit.Cyl(0.03f, 0.03f, 0.12f, kit.Mat("metal"), At(-16.8f, Y + 0.9f, 20.5f), 6));
            stat.Add(kit.RBox(1.7f, 0.8f, 0.45f, 0.06f, kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0xb9b2a5) }), At(5.2f, Y, 13.4f)));
            var name = kit.Sign(new[] { "연무공원" }, new SignOpts { w = 1.4f, h = 0.36f, bg = "#6c665c", fg = "#f4efe4", single = true }); J.SetPos(name, 5.2f, Y + 0.5f, 13.17f); J.SetYaw(name, PI); stat.Add(name);
            solids.Add(new Solid(4.3f, 6.1f, 13.1f, 13.7f, -0.5f, 1, "parkStone"));

            kit.Merge(stat);
            return new TownParkResult { group = group, solids = solids, walk = walk, spots = spots };
        }
    }
}
