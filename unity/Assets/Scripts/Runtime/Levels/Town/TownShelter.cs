// Ports levels/town-shelter.js: the 연무 생활문화센터 shelter site above ground. A: stair kiosk with canopy, stairs
// going down to a fire door (walk stops at the top). B: steep striped car ramp descending under the building
// (walkable, boom barrier). C: gentle ramp along the north wall with handrails both sides, tactile strips and a
// roll-down shutter. East: glass double doors under a canopy. Official shelter signs at each entrance, guide signs
// 10-50 m away on 2 m posts. setGates() switches doors, shutter, boom and the matching solids.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownShelterResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public List<WalkZone> walk = new List<WalkZone>();
        // gates { east|stairs|ramp|access: open|closed }
        public Action<Dictionary<string, string>> setGates;
    }

    public static class TownShelter
    {
        const float Y = TownPlan.CURB;
        // Ground rects cut out for the stair and ramp pits.
        public static readonly List<RectXZ> SHELTER_HOLES = new List<RectXZ>
        {
            new RectXZ(-87.3f, -84.7f, 13.6f, 16),     // A stair pit
            new RectXZ(-84.6f, -75.4f, 46, 54),        // B ramp
            new RectXZ(-112, -101.8f, 13.6f, 16),      // C ramp trench
        };
        public static readonly FacadeHole FACADE_HOLE = new FacadeHole { side = "s", a0 = -84.5f, a1 = -75.5f, y1 = 2.0f };
        public static readonly Dictionary<string, GuideSpot> GUIDE_SPOTS = new Dictionary<string, GuideSpot>
        {
            ["guideEast"] = new GuideSpot { x = -66.4f, z = 28.9f, face = new XZ(1, 0) },
            ["guideC"] = new GuideSpot { x = -100.4f, z = 12.7f, face = new XZ(0.4f, -1) },
        };
        // B: y at z0 .. y at z1
        const float B_Y0 = -0.9f, B_Y1 = Y, B_Z0 = 46, B_Z1 = 54;
        static float RampYB(float z) => B_Y0 + (B_Y1 - B_Y0) * (z - B_Z0) / (B_Z1 - B_Z0);

        // Quad from four corners (counter-clockwise seen from the front); UVs in metres along p0->p1 / p0->p3.
        static GameObject Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Material mat, float s = 1)
        {
            Vector3 e1 = p1 - p0, e2 = p3 - p0, n = Vector3.Cross(e1, e2).normalized;
            float L1 = e1.magnitude / s, L2 = e2.magnitude / s;
            var g = new GeoData();
            g.pos.AddRange(new[] { p0, p1, p2, p3 });
            g.normal.AddRange(new[] { n, n, n, n });
            g.uv.AddRange(new[] { new Vector2(0, 0), new Vector2(L1, 0), new Vector2(L1, L2), new Vector2(0, L2) });
            g.index.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            return TownBKit.Mesh(g, mat, cast: false, receive: true);
        }
        static Vector3 P(float x, float y, float z) => new Vector3(x, y, z);   // JS-space point

        public static TownShelterResult BuildShelterSite(Kit kit)
        {
            var group = J.Group("shelterSite");
            var stat = J.Group("stat", group);
            var dyn = J.Group("dyn", group);
            var solids = new List<Solid>(); var walk = new List<WalkZone>();
            Solid solid(float x0, float x1, float z0, float z1, string id, float y0 = -3, float y1 = 3) { var s = new Solid(x0, x1, z0, z1, y0, y1, id); solids.Add(s); return s; }
            var conc = kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0xb3b0a8) }); var darkConc = kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0x55585a) });
            var steel = kit.Mat("steel"); var dark = kit.Mat("darkMetal"); var glass = kit.Mat("glass", new MatSpec { opacity = 0.4f }); var yellow = kit.Mat("paint:#f2c230", new MatSpec { roughness = 0.5f });
            Texture2D tex(string key, int w, int h, Action<Paint, int, int> draw) => kit.Memo($"town:sh:{key}", () => kit.Texture(kit.Canvas(w, h, draw)));
            var hazard = kit.Memo("town:hazardMat", () => Mat.Lit(new MatSpec
            {
                roughness = 0.6f,
                map = tex("hazard", 128, 32, (g, w, h) => { g.fillStyle = "#f2c230"; g.fillRect(0, 0, w, h); g.fillStyle = "#1a1a1a"; for (int x = -h; x < w; x += 32) { g.beginPath(); g.moveTo(x, h); g.lineTo(x + 16, h); g.lineTo(x + 16 + h, 0); g.lineTo(x + h, 0); g.fill(); } }),
            }));
            var tactile = kit.Memo("town:shTactile", () => Mat.Lit(new MatSpec
            {
                roughness = 0.6f, polygonOffset = true, polygonOffsetFactor = -1,
                map = tex("tactile", 64, 64, (g, w, _) => { g.fillStyle = "#e2a91c"; g.fillRect(0, 0, w, w); g.fillStyle = "#f3c23a"; for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) { g.beginPath(); g.arc((x + 0.5f) * 16, (y + 0.5f) * 16, 4.5f, 0, 7); g.fill(); } }),
            }));
            var exitMat = kit.Memo("town:exitSign", () =>
            {
                var map = tex("exit", 256, 128, (g, w, h) =>
                {
                    g.fillStyle = "#12a150"; g.fillRect(0, 0, w, h); g.fillStyle = "#ffffff"; g.fillRect(w * 0.08f, h * 0.15f, w * 0.22f, h * 0.7f); g.fillStyle = "#12a150"; g.fillRect(w * 0.12f, h * 0.22f, w * 0.14f, h * 0.63f);
                    g.fillStyle = "#ffffff"; g.beginPath(); g.arc(w * 0.45f, h * 0.28f, 11, 0, 7); g.fill(); g.lineWidth = 9; g.strokeStyle = "#fff"; g.lineCap = "round";
                    g.beginPath(); g.moveTo(w * 0.44f, h * 0.4f); g.lineTo(w * 0.4f, h * 0.62f); g.lineTo(w * 0.33f, h * 0.8f); g.moveTo(w * 0.4f, h * 0.62f); g.lineTo(w * 0.5f, h * 0.82f); g.moveTo(w * 0.36f, h * 0.48f); g.lineTo(w * 0.52f, h * 0.5f); g.stroke();
                    g.font = "800 40px sans-serif"; g.fillText("비상구", w * 0.58f, h * 0.65f);
                });
                return kit.Lit("emergency", Mat.Lit(new MatSpec { map = map, emissiveMap = map, emissive = J.Hex(0xffffff), emissiveIntensity = 1.6f, roughness = 0.4f }), 1.6f);
            });
            var emLamp = kit.Memo("town:emLamp", () => kit.Lit("emergency", Mat.Lit(new MatSpec { color = J.Hex(0xffffff), emissive = J.Hex(0xf4f8ff), emissiveIntensity = 4, roughness = 0.3f }), 4));
            var tube = kit.Memo("town:tubeLamp", () => kit.Lit("indoor", Mat.Lit(new MatSpec { color = J.Hex(0xffffff), emissive = J.Hex(0xeef6ff), emissiveIntensity = 3, roughness = 0.3f }), 3));
            void rail(Vector3 a, Vector3 b, float r = 0.024f)
            {
                var d = b - a;
                var geo = Geo.Cylinder(r, r, d.magnitude, 8).ApplyMatrix(Matrix4x4.TRS((a + b) / 2, Quaternion.FromToRotation(Vector3.up, d.normalized), Vector3.one));
                stat.Add(TownBKit.Mesh(geo, kit.Mat("metal"), cast: true));
            }
            var signProto = new Dictionary<float, GameObject>();
            void shelterSign(float x, float y, float z, float ry, float w = 0.45f)
            {
                if (!signProto.TryGetValue(w, out var proto)) signProto[w] = proto = kit.ShelterSign(w);
                var s = TownBKit.Clone(proto); J.SetPos(s, x, y, z); J.SetYaw(s, ry); stat.Add(s);
                stat.Add(kit.Box(w + 0.04f, w * 4 / 3 + 0.04f, 0.03f, dark, new At { x = x - Mathf.Sin(ry) * 0.02f, y = y - w * 2 / 3 - 0.02f, z = z - Mathf.Cos(ry) * 0.02f, ry = ry }));
            }
            var gates = new Dictionary<string, Action<bool>>();

            // ================= A: stair kiosk =================
            {
                float x0 = -87.3f, x1 = -84.7f, cx = (x0 + x1) / 2, zt = 13.6f, rise = 0.17f, run = 0.28f; int n = 8;
                for (int i = 0; i < n; i++)
                {
                    float top = Y - rise * (i + 1), z = zt + run * (i + 0.5f);
                    stat.Add(kit.Slab(x1 - x0, top + 1.6f, run, conc, new At { x = cx, y = -1.6f, z = z }));
                    stat.Add(kit.Box(x1 - x0 - 0.2f, 0.012f, 0.05f, yellow, new At { x = cx, y = top, z = z - run / 2 + 0.03f, cast = false }));
                }
                float bottom = Y - rise * n;
                stat.Add(kit.Box(x1 - x0, 0.22f, 0.04f, conc, new At { x = cx, y = Y - 0.21f, z = zt + 0.02f })); // nosing of the top riser
                stat.Add(kit.Slab(x1 - x0, 0.1f, 16 - (zt + run * n), conc, new At { x = cx, y = bottom - 0.1f, z = (zt + run * n + 16) / 2 }));
                foreach (var x in new[] { x0 - 0.15f, x1 + 0.15f })
                {
                    stat.Add(kit.Slab(0.3f, Y + 1.6f + 1.0f, 16 - 12.8f, conc, new At { x = x, y = -1.6f, z = (12.8f + 16) / 2 }));
                    var gp = kit.Box(0.02f, 1.3f, 3.1f, glass, new At { x = x, y = Y + 1.0f, z = (12.9f + 16) / 2, cast = false }); Kit.Keep(gp); stat.Add(gp);
                    stat.Add(kit.Box(0.08f, 2.5f, 0.08f, steel, new At { x = x, y = Y, z = 12.85f }));
                    float rx = x + (x < cx ? 0.2f : -0.2f);
                    rail(P(rx, Y + 0.85f, zt), P(rx, bottom + 0.85f, zt + run * n));
                }
                stat.Add(kit.Box(3.5f, 0.12f, 3.6f, kit.Mat("paint:#5a6670", new MatSpec { roughness = 0.4f, metalness = 0.6f }), new At { x = cx, y = Y + 2.5f, z = 14.3f, rx = 0.04f }));
                var roofGlass = kit.Box(3.3f, 0.03f, 3.4f, glass, new At { x = cx, y = Y + 2.64f, z = 14.3f, rx = 0.04f, cast = false }); Kit.Keep(roofGlass); stat.Add(roofGlass);
                var lintel = kit.Sign(new[] { "민방위 대피시설 · 출입구 A" }, new SignOpts { w = 2.9f, h = 0.32f, bg = "#1f4e8c", single = true, emissive = 0.3f });
                kit.Lit("signs", TownBKit.MatOf(lintel), 0.5f); J.SetPos(lintel, cx, Y + 2.3f, 12.8f); J.SetYaw(lintel, Mathf.PI); stat.Add(lintel);
                shelterSign(x0 - 0.15f, Y + 1.85f, 12.78f, Mathf.PI);
                // fire door at the bottom with exit sign and a battery emergency light (works in a blackout)
                stat.Add(kit.Box(2.2f, 2.15f, 0.06f, kit.Mat("paint:#6f7a72", new MatSpec { roughness = 0.5f, metalness = 0.4f }), new At { x = cx, y = bottom, z = 15.95f }));
                stat.Add(kit.Box(0.02f, 2.0f, 0.08f, dark, new At { x = cx, y = bottom + 0.05f, z = 15.9f }), kit.Box(0.25f, 0.4f, 0.02f, glass, new At { x = cx - 0.5f, y = bottom + 1.3f, z = 15.91f }));
                var ex = TownBKit.Mesh(Geo.Plane(0.6f, 0.3f), exitMat); J.SetPos(ex, cx, bottom + 2.4f, 15.9f); J.SetYaw(ex, Mathf.PI); stat.Add(ex);
                stat.Add(kit.Box(0.4f, 0.1f, 0.12f, emLamp, new At { x = cx, y = Y + 2.35f, z = 15.4f, cast = false }));
                var L = kit.PointLight(J.Hex(0xeef4ff), 22, 9, 2); group.Add(L); J.SetPos(L.gameObject, cx, Y + 2.1f, 14.6f); kit.Lit("emergency", L, 22);
                solid(x0 - 0.35f, x0, 12.75f, 16, "kioskA"); solid(x1, x1 + 0.35f, 12.75f, 16, "kioskA");
                solid(x0, x1, zt + 0.1f, 16, "stairsTop");
                // closed: folding steel gate across the top
                var lattice = kit.Memo("town:lattice", () => Mat.Lit(new MatSpec
                {
                    map = tex("lattice", 128, 128, (g, w, _) => { g.strokeStyle = "#d0d3d6"; g.lineWidth = 5; for (int k = -w; k < w * 2; k += 22) { g.beginPath(); g.moveTo(k, 0); g.lineTo(k + w, w); g.stroke(); g.beginPath(); g.moveTo(k + w, 0); g.lineTo(k, w); g.stroke(); } }),
                    alphaTest = 0.5f, side = "double", metalness = 0.7f, roughness = 0.4f,
                }));
                var fold = J.Group("fold");
                var lat = kit.Box(x1 - x0, 2.1f, 0.04f, lattice, new At { x = cx, y = Y, z = zt + 0.02f, cast = false }); Mat.SetTiling(lattice, new Vector2(2.5f, 2), Vector2.zero);
                var no = kit.Sign(new[] { "출입 통제", "다른 출입구를 이용하세요" }, new SignOpts { w = 1.2f, h = 0.45f, bg = "#c42b25", single = true }); J.SetPos(no, cx, Y + 1.3f, zt - 0.03f); J.SetYaw(no, Mathf.PI);
                fold.Add(lat, no); dyn.Add(fold); kit.Merge(fold);
                gates["stairs"] = open => fold.SetActive(!open);
            }

            // ================= B: car ramp under the building =================
            {
                float x0 = -84.3f, x1 = -75.7f, cx = (x0 + x1) / 2;
                var rings = kit.Memo("town:rampRings", () =>
                {
                    void circles(Paint g) { foreach (var (x, y) in new[] { (32, 32), (96, 32), (32, 96), (96, 96) }) { g.beginPath(); g.arc(x, y, 22, 0, 7); g.stroke(); } }
                    var h = kit.Canvas(128, 128, (g, w, _) => { g.fillStyle = "#c0c0c0"; g.fillRect(0, 0, w, w); g.strokeStyle = "#303030"; g.lineWidth = 6; circles(g); });
                    return Mat.Lit(new MatSpec
                    {
                        map = kit.Texture(kit.Canvas(128, 128, (g, w, _) => { g.fillStyle = "#9d9b96"; g.fillRect(0, 0, w, w); g.strokeStyle = "#7a7873"; g.lineWidth = 6; circles(g); })),
                        normalMap = kit.Texture(kit.NormalFrom(h, 3), false), roughness = 0.85f,
                    });
                });
                walk.Add(new WalkZone { x0 = x0, x1 = x1, z0 = B_Z0, z1 = B_Z1, ramp = new Ramp("z", B_Y0, B_Y1) });
                stat.Add(Quad(P(x0, B_Y1, 54), P(x1, B_Y1, 54), P(x1, B_Y0, 46), P(x0, B_Y0, 46), rings, 1));
                stat.Add(Quad(P(x0, B_Y0, 46), P(x1, B_Y0, 46), P(x1, -2.9f, 37), P(x0, -2.9f, 37), rings, 1));
                for (int k = 0; k < 3; k++) { float z = 53.2f - k * 2.6f, y = RampYB(z) + 0.012f; stat.Add(Quad(P(x0 + 0.3f, y, z + 0.25f), P(x1 - 0.3f, y, z + 0.25f), P(x1 - 0.3f, y - 0.064f, z - 0.25f), P(x0 + 0.3f, y - 0.064f, z - 0.25f), hazard, 0.5f)); }
                foreach (var (x, s) in new[] { (x0 - 0.15f, -1), (x1 + 0.15f, 1) })
                {
                    stat.Add(kit.Slab(0.3f, Y + 0.9f + 3, 8, conc, new At { x = x, y = -3, z = 50 }));
                    stat.Add(kit.Box(0.32f, 0.12f, 8, hazard, new At { x = x, y = Y + 0.9f, z = 50 }));
                    for (double z = 46.6; z < 54; z += 0.9) stat.Add(kit.Box(0.02f, 0.5f, 0.45f, hazard, new At { x = x - s * 0.16f, y = RampYB((float)z) + 0.05f, z = (float)z, cast = false }));
                    stat.Add(kit.Slab(0.3f, 5.5f, 9, darkConc, new At { x = x - s * 0.15f, y = -3, z = 41.5f }));
                }
                stat.Add(kit.Slab(x1 - x0 + 0.6f, 0.3f, 9, darkConc, new At { x = cx, y = 2.0f, z = 41.5f }), kit.Slab(x1 - x0, 5, 0.3f, darkConc, new At { x = cx, y = -3, z = 37.2f }));
                foreach (var z in new[] { 44.5f, 41 }) stat.Add(kit.Box(1.4f, 0.06f, 0.14f, tube, new At { x = cx, y = 1.93f, z = z, cast = false }));
                var ex = TownBKit.Mesh(Geo.Plane(0.7f, 0.35f), exitMat); J.SetPos(ex, cx, 0.3f, 37.4f); stat.Add(ex);
                var pk = kit.Sign(new[] { "P  지하주차장 입구" }, new SignOpts { w = 3.2f, h = 0.5f, bg = "#1f5fa8", single = true, emissive = 0.3f }); kit.Lit("signs", TownBKit.MatOf(pk), 0.6f); J.SetPos(pk, cx, 2.6f, 46.06f); stat.Add(pk);
                shelterSign(x0 - 1.2f, Y + 1.9f, 46.04f, 0);
                var plaque = kit.Sign(new[] { "출입구 B · 지하주차장 진출입로", "경사 급함 · 보행 주의" }, new SignOpts { w = 1.6f, h = 0.5f, bg = "#ffffff", fg = "#1f4e8c", single = true }); J.SetPos(plaque, x1 + 1.3f, Y + 1.5f, 46.04f); stat.Add(plaque);
                foreach (var x in new[] { x0 - 0.5f, x1 + 0.5f }) stat.Add(kit.Box(0.12f, 2.6f, 0.12f, steel, new At { x = x, y = Y, z = 54.6f }));
                stat.Add(kit.Box(x1 - x0 + 1.2f, 0.18f, 0.12f, hazard, new At { x = cx, y = Y + 2.35f, z = 54.6f }));
                var hl = kit.Sign(new[] { "높이제한 2.3m" }, new SignOpts { w = 1.4f, h = 0.3f, bg = "#ffffff", fg = "#c42b25", single = false }); J.SetPos(hl, cx, Y + 2.15f, 54.6f); stat.Add(hl);
                solid(x0 - 0.65f, x0, 46, 54.2f, "rampWallB"); solid(x1, x1 + 0.65f, 46, 54.2f, "rampWallB");
                solid(x0 - 0.62f, x0 - 0.38f, 54.48f, 54.72f, "heightBar"); solid(x1 + 0.38f, x1 + 0.62f, 54.48f, 54.72f, "heightBar");
                // boom barrier (차단기) on the ramp, pivot at the cabinet by the west wall
                float zb = 48.5f, yb = RampYB(zb);
                stat.Add(kit.Box(0.4f, 1.0f, 0.35f, yellow, new At { x = x0 + 0.3f, y = yb, z = zb }));
                var pivot = J.Group("boomPivot", dyn); J.SetPos(pivot, x0 + 0.3f, yb + 0.92f, zb);
                var armMat = kit.Memo("town:boomArm", () => Mat.Lit(new MatSpec { roughness = 0.5f, map = tex("boom", 256, 16, (g, w, h) => { for (int k = 0; k < 16; k++) { g.fillStyle = k % 2 != 0 ? "#ffffff" : "#d42a24"; g.fillRect(k * w / 16f, 0, w / 16f, h); } }) }));
                pivot.Add(kit.Box(8.1f, 0.09f, 0.07f, armMat, new At { x = 4.05f, y = -0.045f, z = 0 }));
                var boomSolid = solid(x0, x1, zb - 0.22f, zb + 0.22f, "boomB");
                gates["ramp"] = open => { J.SetRot(pivot, 0, 0, open ? Mathf.PI / 2 - 0.05f : 0); boomSolid.off = open; };
            }

            BuildAccess(kit, stat, dyn, walk, solid, tactile, exitMat, emLamp, conc, steel, shelterSign, rail, gates);
            BuildEast(kit, stat, dyn, solid, glass, dark, shelterSign, gates);
            BuildGuideSigns(kit, stat, steel, solid);

            kit.Merge(stat);
            foreach (var p in signProto.Values) TownBKit.Drop(p);
            void setGates(Dictionary<string, string> g)
            {
                if (g == null) return;
                foreach (var k in new[] { "east", "stairs", "ramp", "access" }) if (g.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v)) gates[k](v == "open");
            }
            return new TownShelterResult { group = group, solids = solids, walk = walk, setGates = setGates };
        }

        delegate Solid SolidFn(float x0, float x1, float z0, float z1, string id, float y0 = -3, float y1 = 3);
        delegate void SignFn(float x, float y, float z, float ry, float w = 0.45f);
        delegate void RailFn(Vector3 a, Vector3 b, float r = 0.024f);
    }
}
