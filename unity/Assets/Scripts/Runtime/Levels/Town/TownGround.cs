// Ports levels/town-ground.js: roads with markings, curbs, 보도블록 sidewalks, tactile paving at crossings,
// lots and lawns. Block tops sit at curb height and are carved around the shelter pits (holes).
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Levels.TownUtil;

namespace Gyeongbo.Levels
{
    public sealed class TownGroundResult
    {
        public GameObject group;
        public List<WalkZone> walk = new List<WalkZone>();
    }

    public static class TownGround
    {
        const float CURB = TownPlan.CURB;

        // Interior surfaces [x0, x1, z0, z1, surface]. They must not overlap each other.
        static readonly (float x0, float x1, float z0, float z1, string s)[] OVERLAYS =
        {
            (-36, 4, -22, -12, "asphalt"), (4.2f, 41.8f, -51.8f, -10.2f, "asphalt"), (58, 130, -60, -10, "asphalt"),
            (12.2f, 41.8f, 22.2f, 51.8f, "asphalt"), (-42, 12, 12, 52, "grass"), (-126, -58.2f, 47, 59.5f, "asphalt"),
            (58.2f, 130, 13, 24.5f, "asphalt"), (58.2f, 130, 45, 52, "grass"), (60, 118, 52, 57, "asphalt"), (58.2f, 130, 73, 80, "grass"),
            (60, 118, 80, 85, "asphalt"), (58.2f, 130, 101, 110, "grass"), (58.2f, 64, 30, 42, "grass"), (112, 116, 30, 42, "grass"),
            (-42, 42, -110, -68, "concrete"), (-42, 42, 68, 110, "concrete"), (-130, -58.2f, -110, -56.5f, "concrete"),
            (-130, -58.2f, 60, 110, "concrete"), (-130, -112, 16.7f, 45.8f, "grass"), (58.2f, 130, -110, -60, "concrete"),
        };

        public static TownGroundResult BuildGround(Kit kit, IList<RectXZ> holes = null)
        {
            holes ??= new List<RectXZ>();
            var group = J.Group("ground");
            var stat = J.Group("static", group);
            Material S(string name, SurfaceOpts o = null) => kit.Surface(name, o);
            var autumnGrass = S("grass", new SurfaceOpts { color = J.Hex(0xe6dcb4) });
            var matOf = new Dictionary<string, Material> { ["asphalt"] = S("asphalt"), ["concrete"] = S("concrete"), ["grass"] = autumnGrass, ["sidewalk"] = S("sidewalk") };
            var walk = new List<WalkZone>();
            var B = TownMap.BOUNDS;
            float BX0 = (float)B.x0, BX1 = (float)B.x1, BZ0 = (float)B.z0, BZ1 = (float)B.z1;

            // ---- far ground and road extensions (outside the playable bounds) ----
            group.Add(Flat(new RectXZ(-900, 900, -900, 900), -0.06f, S("concrete", new SurfaceOpts { color = J.Hex(0x8c8a84) })));
            const float ext = 360;
            var roadRects = new[]
            {
                new RectXZ(-ext, ext, -6, 6),
                new RectXZ(-54, -46, -ext, -6), new RectXZ(-54, -46, 6, ext),
                new RectXZ(46, 54, -ext, -6), new RectXZ(46, 54, 6, ext),
                new RectXZ(-46, 46, -64, -56), new RectXZ(-46, 46, 56, 64),
            };
            foreach (var r in roadRects)
            {
                stat.Add(Flat(r, 0, matOf["asphalt"]));
                walk.Add(new WalkZone(Math.Max(r.x0, BX0), Math.Min(r.x1, BX1), Math.Max(r.z0, BZ0), Math.Min(r.z1, BZ1), 0));
            }
            // A strip of pavement either side of the far roads so they do not float on bare ground.
            foreach (var (x0, x1) in new[] { (-ext, BX0), (BX1, ext) })
                stat.Add(Flat(new RectXZ(x0, x1, -10, -6), CURB * 0.5f, matOf["sidewalk"]), Flat(new RectXZ(x0, x1, 6, 10), CURB * 0.5f, matOf["sidewalk"]));
            foreach (var xs in new[] { (-58f, -54f), (-46f, -42f), (42f, 46f), (54f, 58f) })
                foreach (var zs in new[] { (-ext, BZ0), (BZ1, ext) })
                    stat.Add(Flat(new RectXZ(xs.Item1, xs.Item2, zs.Item1, zs.Item2), CURB * 0.5f, matOf["sidewalk"]));

            // ---- block tops: sidewalk pavers everywhere, interior overlays on top (no overlap), pits carved ----
            var overlayRects = new List<RectXZ>();
            foreach (var o in OVERLAYS) overlayRects.Add(new RectXZ(o.x0, o.x1, o.z0, o.z1));
            var cutAll = new List<RectXZ>(holes); cutAll.AddRange(overlayRects);
            foreach (var b in TownPlan.BLOCKS)
            {
                foreach (var r in Carve(new WalkZone(b.x0, b.x1, b.z0, b.z1), cutAll)) stat.Add(Flat(r, CURB, matOf["sidewalk"]));
                foreach (var r in Carve(new WalkZone(b.x0, b.x1, b.z0, b.z1, CURB), holes)) walk.Add(r);
            }
            foreach (var o in OVERLAYS) foreach (var r in Carve(new WalkZone(o.x0, o.x1, o.z0, o.z1), holes)) stat.Add(Flat(r, CURB, matOf[o.s]));

            // ---- curbs (granite, 15 cm) along every block edge that faces a road ----
            var curbMat = kit.Mat("curb");
            foreach (var b in TownPlan.BLOCKS)
            {
                var edges = new List<(float x, float z, float w, float d)>();
                if (b.x0 > BX0) edges.Add((b.x0 + 0.075f, (b.z0 + b.z1) / 2, 0.15f, b.z1 - b.z0));
                if (b.x1 < BX1) edges.Add((b.x1 - 0.075f, (b.z0 + b.z1) / 2, 0.15f, b.z1 - b.z0));
                if (b.z0 > BZ0) edges.Add(((b.x0 + b.x1) / 2, b.z0 + 0.075f, b.x1 - b.x0, 0.15f));
                if (b.z1 < BZ1) edges.Add(((b.x0 + b.x1) / 2, b.z1 - 0.075f, b.x1 - b.x0, 0.15f));
                foreach (var (x, z, w, d) in edges) stat.Add(kit.Box(w, CURB + 0.03f, d, curbMat, At(x, -0.02f, z, cast: false)));
            }

            // ---- markings: one instanced quad mesh per paint colour ----
            var white = new List<Xform>(); var yellow = new List<Xform>();
            void Quad(List<Xform> list, float x0, float x1, float z0, float z1) => list.Add(new Xform { x = (x0 + x1) / 2, y = 0.008f, z = (z0 + z1) / 2, sx = x1 - x0, sz = z1 - z0 });
            // Intervals along a road minus gaps -> callback per kept interval.
            void Spans(float a, float b, List<(float g0, float g1)> gaps, Action<float, float> fn)
            {
                float cur = a;
                var sorted = new List<(float g0, float g1)>(gaps); sorted.Sort((p, q) => p.g0.CompareTo(q.g0));
                foreach (var (g0, g1) in sorted) { if (g0 > cur) fn(cur, Math.Min(g0, b)); cur = Math.Max(cur, g1); }
                if (cur < b) fn(cur, b);
            }
            List<(float, float)> Cw(string road)
            {
                var o = new List<(float, float)>();
                foreach (var c in TownMap.CROSSWALKS) if (c.road == road) o.Add(((float)c.at - 2.4f, (float)c.at + 2.4f));
                return o;
            }
            // 연무로: double yellow centre, dashed white lane lines, interrupted at crossings and junctions.
            var yGaps = Cw("yeonmu"); yGaps.Add((-54.5f, -45.5f)); yGaps.Add((45.5f, 54.5f));
            Spans(-ext, ext, yGaps, (a, b) =>
            {
                Quad(yellow, a, b, -0.27f, -0.12f); Quad(yellow, a, b, 0.12f, 0.27f);
                foreach (var z in new[] { -3f, 3f }) for (double x = a + 1; x + 3 < b; x += 8) Quad(white, (float)x, (float)x + 3, z - 0.07f, z + 0.07f);
            });
            foreach (var (id, cx) in new[] { ("munhwa", -50f), ("hanbit", 50f) })
            {
                var gaps = Cw(id); gaps.Add((-6.5f, 6.5f)); gaps.Add((-64.5f, -55.5f)); gaps.Add((55.5f, 64.5f));
                Spans(-ext, ext, gaps, (a, b) => { Quad(yellow, cx - 0.27f, cx - 0.12f, a, b); Quad(yellow, cx + 0.12f, cx + 0.27f, a, b); });
            }
            foreach (var (id, cz) in new[] { ("sijang", -60f), ("danji", 60f) }) Spans(-46, 46, Cw(id), (a, b) => Quad(yellow, a, b, cz - 0.08f, cz + 0.08f));
            // Zebra crossings (bars parallel to traffic, 50 cm, 50 cm apart) and tactile paving at both ends.
            var tactile = new List<RectXZ>();
            foreach (var c in TownMap.CROSSWALKS)
            {
                var r = TownMap.ROADS.Find(q => q.id == c.road);
                float at = (float)c.at, rx0 = (float)r.x0, rx1 = (float)r.x1, rz0 = (float)r.z0, rz1 = (float)r.z1;
                if (r.z1 - r.z0 < r.x1 - r.x0) // east-west road
                {
                    for (double z = r.z0 + 0.45; z + 0.5 <= r.z1 - 0.2; z += 1) Quad(white, at - 2, at + 2, (float)z, (float)z + 0.5f);
                    tactile.Add(new RectXZ(at - 2, at + 2, rz0 - 0.75f, rz0 - 0.15f)); tactile.Add(new RectXZ(at - 2, at + 2, rz1 + 0.15f, rz1 + 0.75f));
                }
                else
                {
                    for (double x = r.x0 + 0.45; x + 0.5 <= r.x1 - 0.2; x += 1) Quad(white, (float)x, (float)x + 0.5f, at - 2, at + 2);
                    tactile.Add(new RectXZ(rx0 - 0.75f, rx0 - 0.15f, at - 2, at + 2)); tactile.Add(new RectXZ(rx1 + 0.15f, rx1 + 0.75f, at - 2, at + 2));
                }
            }
            // Stop lines just ahead of where cars wait, across the lane's half of the road.
            var arrows = new List<Xform>();
            foreach (var L in TownPlan.LANES) foreach (var s in L.stops)
            {
                float at = s + L.dir * 0.4f;
                if (L.axis == "x") Quad(white, at - 0.22f, at + 0.22f, L.c - 1.4f, L.c + 1.4f);
                else Quad(white, L.c - 1.4f, L.c + 1.4f, at - 0.22f, at + 0.22f);
                float back = s - L.dir * 14;
                arrows.Add(L.axis == "x" ? new Xform { x = back, y = 0.009f, z = L.c, ry = L.dir > 0 ? -Mathf.PI / 2 : Mathf.PI / 2 } : new Xform { x = L.c, y = 0.009f, z = back, ry = L.dir > 0 ? Mathf.PI : 0 });
            }
            // Parking bays: mart lot, the walled public lot, the logistics yard, the culture centre rear lot, the complex.
            void Bays(float x0, float x1, float zLine, float depth, float step = 2.5f)
            {
                for (double x = x0; x <= x1 + 1e-6; x += step) Quad(white, (float)x - 0.06f, (float)x + 0.06f, Math.Min(zLine, zLine + depth), Math.Max(zLine, zLine + depth));
                Quad(white, x0, x1, zLine - 0.06f, zLine + 0.06f);
            }
            Bays(-35, 3, -22, 5); Bays(6, 40, -51.5f, 5); Bays(6, 40, -10.5f, -5); Bays(6, 40, -31, 5); Bays(6, 40, -31, -5);
            Bays(74.5f, 82, -10.5f, -5.5f); Bays(92, 126, -10.5f, -5.5f); Bays(104.4f, 123.6f, -60, 6, 3.2f); Bays(-124, -90, 59.5f, -5); Bays(60, 126, 13.5f, 5);
            Bays(62, 116, 57, -5); Bays(62, 116, 85, -5); Bays(14, 40, 51.5f, -5); Bays(14, 40, 22.5f, 5);
            var plane = Geo.Plane(1, 1).RotateX(-Mathf.PI / 2);
            Material PaintMat(int hex) => Mat.Lit(new MatSpec { color = J.Hex(hex), roughness = 0.62f, polygonOffset = true, polygonOffsetFactor = -2, polygonOffsetUnits = -2 });
            group.Add(kit.Instanced(plane, PaintMat(0xe9e8e1), white, false));
            group.Add(kit.Instanced(plane, PaintMat(0xe8b21e), yellow, false));
            var arrowShape = new List<Vector2> { new Vector2(-0.15f, -2.4f), new Vector2(0.15f, -2.4f), new Vector2(0.15f, 0.2f), new Vector2(0.45f, 0.2f), new Vector2(0, 1.4f), new Vector2(-0.45f, 0.2f), new Vector2(-0.15f, 0.2f) };
            group.Add(kit.Instanced(Geo.Shape(arrowShape).RotateX(-Mathf.PI / 2), PaintMat(0xe9e8e1), arrows, false));

            // Tactile paving (점형블록): yellow with raised dots.
            var tac = kit.Memo("town:tactile", () =>
            {
                void Dots(Paint g, int w, string fg) { for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++) { g.fillStyle = fg; g.beginPath(); g.arc((x + 0.5f) * w / 6, (y + 0.5f) * w / 6, w / 22f, 0, 7); g.fill(); } }
                var map = kit.Texture(kit.Canvas(128, 128, (g, w, h) => { g.fillStyle = "#e2a91c"; g.fillRect(0, 0, w, w); Dots(g, w, "#f0bd33"); g.strokeStyle = "rgba(0,0,0,0.25)"; g.strokeRect(0, 0, w, w); }));
                var nrm = kit.Texture(kit.NormalFrom(kit.Canvas(128, 128, (g, w, h) => { g.fillStyle = "#404040"; g.fillRect(0, 0, w, w); Dots(g, w, "#e0e0e0"); }), 3), false);
                return Mat.Lit(new MatSpec { map = map, normalMap = nrm, repeat = new Vector2(1 / 0.3f, 1 / 0.3f), roughness = 0.6f, polygonOffset = true, polygonOffsetFactor = -1, polygonOffsetUnits = -1 });
            });
            foreach (var t in tactile) stat.Add(Flat(t, CURB + 0.004f, tac));

            // Manholes and drain grates on the roads.
            var holeMat = kit.Memo("town:manhole", () => Mat.Lit(new MatSpec
            {
                map = kit.Texture(kit.Canvas(128, 128, (g, w, h) =>
                {
                    g.fillStyle = "#2b2c2e"; g.fillRect(0, 0, w, w); g.strokeStyle = "#4a4b4e"; g.lineWidth = 5;
                    g.beginPath(); g.arc(64, 64, 58, 0, 7); g.stroke();
                    for (int i = 16; i < 112; i += 12) { g.beginPath(); g.moveTo(i, 18); g.lineTo(i, 110); g.stroke(); }
                })),
                roughness = 0.45f, metalness = 0.7f, polygonOffset = true, polygonOffsetFactor = -2,
            }));
            var holesList = new List<Xform>();
            for (int x = -122; x < 130; x += 31) holesList.Add(new Xform { x = x, y = 0.01f, z = x % 2 != 0 ? 3 : -3 });
            for (int z = -100; z < 110; z += 34) { holesList.Add(new Xform { x = -51.6f, y = 0.01f, z = z }); holesList.Add(new Xform { x = 51.6f, y = 0.01f, z = z + 9 }); }
            group.Add(kit.Instanced(Geo.Circle(0.36f, 20).RotateX(-Mathf.PI / 2), holeMat, holesList, false));
            var grate = kit.Memo("town:grate", () => kit.Mat("darkMetal", new MatSpec { roughness = 0.6f }));
            var grates = new List<Xform>();
            foreach (var (z, xs) in new[] { (-5.6f, new float[] { -120, -90, -30, 20, 80, 110 }), (5.6f, new float[] { -110, -75, -20, 30, 90, 120 }) })
                foreach (var x in xs) grates.Add(new Xform { x = x, y = 0.012f, z = z, sx = 0.9f, sz = 0.35f });
            group.Add(kit.Instanced(plane, grate, grates, false));

            kit.Merge(stat);
            return new TownGroundResult { group = group, walk = walk };
        }

        // Thin quad from p to q (world xz) of width w at height y: park paths, painted bands.
        public static GameObject Strip(XZ p, XZ q, float w, float y, Material material)
        {
            float dx = q.x - p.x, dz = q.z - p.z, len = Hypot(dx, dz);
            var m = Geo.Obj(FlatGeo(new RectXZ(-len / 2, len / 2, -w / 2, w / 2), 0), material, null, false, true);
            J.SetPos(m, (p.x + q.x) / 2, y, (p.z + q.z) / 2); J.SetYaw(m, -Mathf.Atan2(dz, dx));
            return m;
        }
    }
}
