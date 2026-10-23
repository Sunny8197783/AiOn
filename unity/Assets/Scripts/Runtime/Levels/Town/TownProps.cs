// Ports levels/town-props.js: street furniture — street lights (instanced heads in light group 'street', ground
// glow, a few real lights), 전봇대 with sagging wires and service drops, the bus stop, bollards, hydrants, planters,
// trash bags at collection points, bicycles, parcels at the logistics centre, AC units, 현수막, vending machines
// and the apartment recycling shed (the last five in TownPropsMore.cs). Fences live in TownFences.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Levels.TownUtil;

namespace Gyeongbo.Levels
{
    public sealed class TownPropsResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public List<Light> lights = new List<Light>();
    }

    public static class TownProps
    {
        internal const float Y = TownPlan.CURB;
        const float PI = Mathf.PI;

        // [x, z, ry] planter boxes; pure so the tree builder can add the shrubs to its instancing.
        public static readonly List<float[]> PLANTERS = new List<float[]>
        {
            new float[] { -30, -23.3f, 0 }, new float[] { -4, -23.3f, 0 }, new float[] { -98, -26.6f, 0 }, new float[] { -80, -26.6f, 0 }, new float[] { 74, -20.8f, 0 }, new float[] { 100, -20.8f, 0 },
            new float[] { -66, 21, PI / 2 }, new float[] { -66, 41, PI / 2 }, new float[] { 69.5f, 29, 0 }, new float[] { 88, 29, 0 }, new float[] { -123, -14.6f, 0 }, new float[] { 18, 10.9f, 0 },
        };

        public static List<TreeSpot> ShrubSpots(Func<float> rand)
        {
            var output = new List<TreeSpot>();
            foreach (var p in PLANTERS) foreach (var k in new[] { -0.75f, 0, 0.75f })
                output.Add(new TreeSpot { x = p[0] + Mathf.Cos(p[2]) * k, z = p[1] - Mathf.Sin(p[2]) * k, type = "shrub", s = 0.55f + rand() * 0.2f, ry = rand() * 6.28f, tint = rand() });
            for (double x = 66; x <= 110; x += 2.2) output.Add(new TreeSpot { x = (float)x, z = 42.9f, type = "shrub", s = 0.7f + rand() * 0.3f, ry = rand() * 6.28f, tint = rand() });
            for (double z = 18; z <= 44; z += 2.4) output.Add(new TreeSpot { x = -113.2f, z = (float)z, type = "shrub", s = 0.8f, ry = rand() * 6.28f, tint = rand() });
            return output;
        }

        // What every section of buildProps shares.
        internal sealed class Ctx
        {
            public Kit kit; public GameObject group, stat; public List<Solid> solids; public Func<float> rand; public List<TreeSpot> avoid;
            public Material steel, dark, metal;
            public void Box(float x0, float x1, float z0, float z1, float y1 = 2.5f, string id = "prop") => solids.Add(new Solid(x0, x1, z0, z1, -0.5f, y1, id));
            public bool Clear(float x, float z, float d = 2.4f) { foreach (var a in avoid) if (Hypot(a.x - x, a.z - z) < d) return false; return true; }
        }

        // avoid: tree/shrub spots props must keep clear of.
        public static TownPropsResult BuildProps(Kit kit, List<TreeSpot> avoid = null)
        {
            var group = J.Group("props");
            var c = new Ctx { kit = kit, group = group, stat = J.Group("static", group), solids = new List<Solid>(), rand = Kit.Rng(2024), avoid = avoid ?? new List<TreeSpot>(),
                steel = kit.Mat("steel"), dark = kit.Mat("darkMetal"), metal = kit.Mat("metal") };
            var lights = StreetLights(c);
            Poles(c); BusStop(c); Bollards(c); Planters(c); Bags(c); Bikes(c);
            TownPropsMore.Parcels(c); TownPropsMore.Acs(c); TownPropsMore.Banners(c); TownPropsMore.Vending(c); TownPropsMore.Recycling(c);
            kit.Merge(c.stat);
            // ponytail: JS returns the street-light spots as `lights` (unused by town.js); the frozen type holds Lights,
            // so this carries the real point lights at the story stops.
            return new TownPropsResult { group = group, solids = c.solids, lights = lights };
        }

        // ---- street lights ----
        static List<Light> StreetLights(Ctx c)
        {
            var kit = c.kit;
            List<float> Cw(string road) { var o = new List<float>(); foreach (var w in TownMap.CROSSWALKS) if (w.road == road) o.Add((float)w.at); return o; }
            var lights = new List<Xform>();
            void AddLight(float x, float z, float ry) { if (c.Clear(x, z)) lights.Add(new Xform { x = x, y = Y, z = z, ry = ry }); }
            var cwY = Cw("yeonmu");
            for (int x = -122; x <= 122; x += 26)
                foreach (var (z, ry, off) in new[] { (6.55f, 0f, 0), (-6.55f, PI, 13) })
                {
                    int xx = x + off;
                    if (xx > 126 || cwY.Exists(a => Math.Abs(a - xx) < 4.6f) || Math.Abs(Math.Abs(xx) - 50) < 5.2f) continue;
                    AddLight(xx, z, ry);
                }
            foreach (var (x, ry, road) in new[] { (-54.55f, -PI / 2, "munhwa"), (-45.45f, PI / 2, "munhwa"), (45.45f, -PI / 2, "hanbit"), (54.55f, PI / 2, "hanbit") })
            {
                var cw = Cw(road);
                for (int z = -102 + (x > 0 ? 13 : 0); z <= 104; z += 26)
                {
                    if (Math.Abs(z) < 9 || Math.Abs(Math.Abs(z) - 60) < 5.5f || cw.Exists(a => Math.Abs(a - z) < 4.6f)) continue;
                    AddLight(x, z, ry);
                }
            }
            for (int x = -38; x <= 40; x += 26) { AddLight(x, -64.55f, PI); AddLight(x + 13, 64.55f, 0); }
            var poleGeo = Geo.Merge(new[]
            {
                Geo.Cylinder(0.075f, 0.115f, 8.6f, 10).Translate(0, 4.3f, 0),
                Geo.Box(0.32f, 0.55f, 0.26f).Translate(0, 0.28f, 0),
                Geo.Cylinder(0.045f, 0.06f, 1.0f, 8).RotateX(-0.9f).Translate(0, 8.75f, -0.38f),
                Geo.Cylinder(0.045f, 0.045f, 1.5f, 8).RotateX(PI / 2).Translate(0, 9.05f, -1.5f),
                Geo.Box(0.34f, 0.14f, 0.72f).Translate(0, 9.0f, -2.45f),
            });
            var lensMat = kit.Memo("town:streetLens", () => kit.Lit("street", Mat.Lit(new MatSpec { color = J.Hex(0xfff6e8), emissive = J.Hex(0xfff0d8), emissiveIntensity = 6, roughness = 0.3f }), 6));
            c.group.Add(kit.Instanced(poleGeo, kit.Mat("paint:#7d848a", new MatSpec { roughness = 0.45f, metalness = 0.6f }), lights));
            c.group.Add(kit.Instanced(Geo.Box(0.26f, 0.03f, 0.6f).Translate(0, 8.92f, -2.45f), lensMat, lights, false));
            foreach (var p in lights) c.Box(p.x - 0.18f, p.x + 0.18f, p.z - 0.18f, p.z + 0.18f, 4, "streetlight");
            // Light pools on the ground under each head (additive, follows the street group).
            var glowTex = kit.Memo("town:glowTex", () => kit.Texture(kit.Canvas(128, 128, (g, w, h) =>
            {
                var r = new PaintGradient(w / 2f, w / 2f, 0, w / 2f, w / 2f, w / 2f);
                r.addColorStop(0, "rgba(255,255,255,1)"); r.addColorStop(0.45f, "rgba(255,255,255,0.45)"); r.addColorStop(1, "rgba(255,255,255,0)");
                g.fillStyle = r; g.fillRect(0, 0, w, w);
            })));
            var poolMat = kit.Memo("town:pool", () => kit.Lit("street", Mat.Lit(new MatSpec
            {
                color = Color.black, emissive = J.Hex(0xffdcae), emissiveMap = glowTex, emissiveIntensity = 0.55f, transparent = true, depthWrite = false, blending = "additive", polygonOffset = true, polygonOffsetFactor = -4,
            }), 0.55f));
            var pools = lights.ConvertAll(p => new Xform { x = p.x - Mathf.Sin(p.ry) * 2.45f, y = 0.02f, z = p.z - Mathf.Cos(p.ry) * 2.45f, s = 1 });
            c.group.Add(kit.Instanced(Geo.Plane(10, 10).RotateX(-PI / 2), poolMat, pools, false, false));
            // A few real lights at the places the story stops (others glow only).
            var keyLights = new[] { (78f, 25.5f), (-95f, 10.5f), (36f, 8.4f), (-80f, -15f), (-63f, 31f), (-61f, -34f), (0f, 9f) };
            int n = kit.Quality == "low" ? 2 : kit.Quality == "medium" ? 4 : keyLights.Length;
            var real = new List<Light>();
            for (int i = 0; i < n && i < keyLights.Length; i++)
            {
                var L = kit.PointLight(J.Hex(0xffd9a8), 260, 30, 2); L.transform.localPosition = J.V(keyLights[i].Item1, 7.6f, keyLights[i].Item2);
                kit.Lit("street", L, 260); c.group.Add(L); real.Add(L);
            }
            return real;
        }

        // ---- 전봇대 (concrete utility poles), sagging power and telecom lines, service drops ----
        static void Poles(Ctx c)
        {
            var kit = c.kit;
            var lines = new List<(List<(float x, float z)> pts, int ux, int uz)>
            {
                (Row(new float[] { -40, -20, 0, 20, 40 }, x => (x, -52.6f)), 0, 1),
                (Row(new float[] { -40, -20, 0, 20, 40 }, x => (x, 52.6f)), 0, 1),
                (Row(new float[] { -104, -88, -72 }, z => (-57.6f, z)), 1, 0), (Row(new float[] { 70, 87, 104 }, z => (-57.6f, z)), 1, 0),
                (Row(new float[] { -104, -87, -70 }, z => (57.4f, z)), 1, 0), (Row(new float[] { 72, 88, 104 }, z => (57.4f, z)), 1, 0),
            };
            var conc = kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0xbab6ad), repeat = new Vector2(1, 3) });
            var upoles = new List<Xform>();
            foreach (var L in lines) foreach (var (x, z) in L.pts) upoles.Add(new Xform { x = x, y = Y, z = z, ry = L.ux != 0 ? PI / 2 : 0 });
            c.group.Add(kit.Instanced(Geo.Cylinder(0.12f, 0.19f, 11, 12).Translate(0, 5.5f, 0), conc, upoles));
            var arms = new List<GeoData> { Geo.Box(2.2f, 0.1f, 0.1f).Translate(0, 10.3f, 0), Geo.Box(1.6f, 0.09f, 0.09f).Translate(0, 9.5f, 0) };
            foreach (var x in new[] { -0.95f, 0, 0.95f }) arms.Add(Geo.Cylinder(0.05f, 0.06f, 0.18f, 8).Translate(x, 10.44f, 0));
            arms.Add(Geo.Box(0.34f, 0.12f, 0.34f).Translate(0, 6.1f, 0)); arms.Add(Geo.Box(0.3f, 0.1f, 0.3f).Translate(0, 5.6f, 0));
            for (int i = 0; i < 6; i++) arms.Add(Geo.Box(0.03f, 0.03f, 0.3f).Translate(0, 2.4f + i * 0.45f, 0).RotateY(i * 1.57f));
            c.group.Add(kit.Instanced(Geo.Merge(arms), c.steel, upoles));
            var trafos = new List<Xform>(); for (int i = 0; i < upoles.Count; i++) if (i % 3 == 1) trafos.Add(upoles[i]);
            c.group.Add(kit.Instanced(Geo.Cylinder(0.32f, 0.32f, 1.05f, 14).Translate(0.42f, 8.1f, 0), kit.Mat("paint:#8c9297", new MatSpec { roughness = 0.5f, metalness = 0.4f }), trafos));
            foreach (var p in upoles) c.Box(p.x - 0.22f, p.x + 0.22f, p.z - 0.22f, p.z + 0.22f, 4, "utilityPole");
            var wire = new List<Vector3>();
            void Sag(Vector3 a, Vector3 b, float s, int n = 14)
            {
                for (int i = 0; i < n; i++) { float t0 = (float)i / n, t1 = (float)(i + 1) / n; foreach (var t in new[] { t0, t1 }) wire.Add(new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t - 4 * s * t * (1 - t), a.z + (b.z - a.z) * t)); }
            }
            foreach (var L in lines) for (int i = 1; i < L.pts.Count; i++)
            {
                var (ax, az) = L.pts[i - 1]; var (bx, bz) = L.pts[i]; int ux = L.ux, uz = L.uz;
                foreach (var o in new[] { -0.95f, 0, 0.95f }) Sag(new Vector3(ax + ux * o, Y + 10.5f, az + uz * o), new Vector3(bx + ux * o, Y + 10.5f, bz + uz * o), 0.42f);
                foreach (var (o, h, s) in new[] { (-0.12f, 6.15f, 0.7f), (0.1f, 6.05f, 0.85f), (0f, 5.62f, 0.95f), (0.15f, 5.55f, 1.1f) }) Sag(new Vector3(ax + ux * o, Y + h, az + uz * o), new Vector3(bx + ux * o, Y + h, bz + uz * o), s);
            }
            foreach (var L in lines) foreach (var (x, z) in L.pts) // service drops to the nearest building
            {
                float bd = float.PositiveInfinity, bcx = 0, bcz = 0;
                foreach (var q in TownMap.BUILDINGS)
                {
                    float cx = Math.Max((float)q.x0, Math.Min(x, (float)q.x1)), cz = Math.Max((float)q.z0, Math.Min(z, (float)q.z1)), d = Hypot(cx - x, cz - z);
                    if (d < bd) { bd = d; bcx = cx; bcz = cz; }
                }
                if (bd < 16) foreach (var dz in new[] { -0.6f, 0.5f }) Sag(new Vector3(x, Y + 6, z), new Vector3(bcx + (L.ux != 0 ? 0 : dz), Y + 5.2f, bcz + (L.ux != 0 ? dz : 0)), 0.5f, 8);
            }
            var verts = wire.ConvertAll(v => J.V(v)); var idx = new int[verts.Count]; for (int i = 0; i < idx.Length; i++) idx[i] = i;
            var wm = new Mesh { name = "wires", indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            wm.SetVertices(verts); wm.SetIndices(idx, MeshTopology.Lines, 0); wm.RecalculateBounds();
            c.group.Add(Geo.Obj(wm, Mat.Unlit(new MatSpec { color = J.Hex(0x17181a) }), "wires", false, false));
        }

        static List<(float x, float z)> Row(float[] v, Func<float, (float, float)> f) { var o = new List<(float x, float z)>(); foreach (var a in v) o.Add(f(a)); return o; }

        // ---- bus stop (연무로 north side, westbound) ----
        static void BusStop(Ctx c)
        {
            var kit = c.kit; var stat = c.stat;
            Material glass = kit.Mat("glass"), roofMat = kit.Mat("paint:#3a4a5a", new MatSpec { roughness = 0.5f, metalness = 0.5f });
            const float bx = 11.5f, bz = -9.0f;
            stat.Add(kit.Box(4.2f, 0.1f, 1.8f, roofMat, At(bx, Y + 2.55f, bz + 0.1f)));
            foreach (var dx in new[] { -2f, 2f }) stat.Add(kit.Box(0.09f, 2.55f, 0.09f, c.steel, At(bx + dx, Y, bz - 0.62f)), kit.Box(0.09f, 2.55f, 0.09f, c.steel, At(bx + dx, Y, bz + 0.6f)));
            var back = kit.Box(4.0f, 2.1f, 0.02f, glass, At(bx, Y + 0.3f, bz - 0.64f, cast: false)); Kit.Keep(back); stat.Add(back);
            stat.Add(kit.Box(2.8f, 0.05f, 0.4f, kit.Surface("wood", new SurfaceOpts { color = J.Hex(0xb88a5c) }), At(bx - 0.4f, Y + 0.44f, bz - 0.35f)), kit.Box(2.8f, 0.44f, 0.06f, c.dark, At(bx - 0.4f, Y, bz - 0.35f)));
            var mapPanel = kit.Sign(new[] { "연무동 행정복지센터 · 12-345", "마을 3  ·  720  ·  1115", "다음: 연무마트 → 한빛아파트" }, new SignOpts { w = 1.4f, h = 1.9f, bg = "#eef2f5", fg = "#16324f", single = true, emissive = 0.15f });
            kit.Lit("shops", MatOf(mapPanel), 0.6f);
            J.SetPos(mapPanel, bx + 2.0f, Y + 1.35f, bz - 0.01f); J.SetYaw(mapPanel, -PI / 2); stat.Add(mapPanel);
            var bis = kit.Sign(new[] { "마을 3  곧 도착", "720  6분" }, new SignOpts { w = 1.2f, h = 0.32f, bg = "#120c08", fg = "#ffb340", single = true, emissive = 1 });
            kit.Lit("shops", MatOf(bis), 1.6f); J.SetPos(bis, bx - 1.2f, Y + 2.3f, bz + 0.98f); J.SetYaw(bis, 0); stat.Add(bis);
            stat.Add(kit.Cyl(0.05f, 0.05f, 2.9f, c.steel, At(bx + 3.2f, Y, -6.9f), 8));
            var stopSign = kit.Sign(new[] { "버스", "연무동 행정복지센터" }, new SignOpts { w = 0.7f, h = 0.6f, bg = "#1f5fa8", single = false }); J.SetPos(stopSign, bx + 3.2f, Y + 2.6f, -6.9f); J.SetYaw(stopSign, PI / 2); stat.Add(stopSign);
            c.Box(bx - 2.1f, bx + 2.1f, bz - 0.72f, bz - 0.56f, 2.6f, "busStop"); c.Box(bx + 1.9f, bx + 2.1f, bz - 0.7f, bz + 0.7f, 2.6f, "busStop");
        }

        // ---- bollards at the crossings, hydrants ----
        static void Bollards(Ctx c)
        {
            var kit = c.kit;
            var bol = new List<Xform>();
            foreach (var cw in TownMap.CROSSWALKS)
            {
                var r = TownMap.ROADS.Find(q => q.id == cw.road); bool ew = r.z1 - r.z0 < r.x1 - r.x0;
                foreach (var side in new[] { -1, 1 }) foreach (var k in new[] { -2.6f, 2.6f })
                {
                    float edge = side < 0 ? (float)(ew ? r.z0 : r.x0) - 1.05f : (float)(ew ? r.z1 : r.x1) + 1.05f;
                    float x = ew ? (float)cw.at + k : edge, z = ew ? edge : (float)cw.at + k;
                    if (c.Clear(x, z, 0.8f)) bol.Add(new Xform { x = x, y = Y, z = z });
                }
            }
            foreach (var x in new[] { 31f, 33.5f, 38.5f, 41f }) bol.Add(new Xform { x = x, y = Y, z = 10.4f });
            c.group.Add(kit.Instanced(Geo.Cylinder(0.1f, 0.11f, 0.85f, 14).Translate(0, 0.425f, 0), kit.Mat("metal", new MatSpec { roughness = 0.25f }), bol));
            c.group.Add(kit.Instanced(Geo.Cylinder(0.112f, 0.112f, 0.07f, 14).Translate(0, 0.7f, 0), kit.Mat("paint:#f4f4f0", new MatSpec { roughness = 0.2f, emissive = J.Hex(0x222222) }), bol, false));
            foreach (var p in bol) c.Box(p.x - 0.13f, p.x + 0.13f, p.z - 0.13f, p.z + 0.13f, 1, "bollard");
            var hyd = new List<Xform>();
            foreach (var (x, z) in new[] { (-70f, -8.8f), (-20f, 8.8f), (26f, -8.8f), (62f, 8.8f), (-44.4f, 40f), (44.4f, -40f), (-56.8f, -84f), (56.8f, 84f), (-120f, 8.8f), (100f, -8.8f) })
                if (c.Clear(x, z, 1.2f)) hyd.Add(new Xform { x = x, y = Y, z = z, ry = c.rand() * 6.28f });
            var red = kit.Mat("paint:#c42b25", new MatSpec { roughness = 0.45f });
            c.group.Add(kit.Instanced(Geo.Merge(new[] { Geo.Cylinder(0.16f, 0.2f, 0.62f, 14).Translate(0, 0.31f, 0), Geo.Sphere(0.17f, 14, 8, 0, 6.29f, 0, 1.6f).Translate(0, 0.62f, 0), Geo.Cylinder(0.07f, 0.07f, 0.5f, 10).RotateZ(PI / 2).Translate(0, 0.42f, 0), Geo.Cylinder(0.06f, 0.06f, 0.12f, 6).Translate(0, 0.8f, 0) }), red, hyd));
            foreach (var p in hyd) c.Box(p.x - 0.25f, p.x + 0.25f, p.z - 0.25f, p.z + 0.25f, 1, "hydrant");
        }

        // ---- planter boxes (shrubs come from ShrubSpots) ----
        static void Planters(Ctx c)
        {
            var kit = c.kit;
            Material plMat = kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0xa9a59c) }), soil = kit.Mat("paint:#3b2e22", new MatSpec { roughness = 1 });
            foreach (var p in PLANTERS)
            {
                float x = p[0], z = p[1], ry = p[2];
                c.stat.Add(kit.Slab(2.6f, 0.5f, 0.95f, plMat, At(x, Y, z, ry)), kit.Box(2.45f, 0.02f, 0.8f, soil, At(x, Y + 0.46f, z, ry, false)));
                float ax = Math.Abs(Mathf.Cos(ry)) * 1.3f + Math.Abs(Mathf.Sin(ry)) * 0.48f, az = Math.Abs(Mathf.Sin(ry)) * 1.3f + Math.Abs(Mathf.Cos(ry)) * 0.48f;
                c.Box(x - ax, x + ax, z - az, z + az, 0.9f, "planter");
            }
        }

        // ---- 종량제 봉투 at collection points ----
        static void Bags(Ctx c)
        {
            var kit = c.kit; var rand = c.rand;
            var bags = new List<Xform>(); var bagCol = new List<int>();
            string[] cols = { "#ecead8", "#f1e6a2", "#e9e3cc", "#cfdff0", "#f6f2e4" };
            foreach (var (cx, cz, n) in new[] { (-20f, -53.4f, 9), (20f, 53.4f, 8), (40f, -53.4f, 6), (-57.1f, 88f, 7), (57.0f, -88f, 6), (43.2f, 23f, 5) })
            {
                for (int i = 0; i < n; i++)
                {
                    float a = rand() * 6.28f, d = rand() * 0.8f, s = 0.75f + rand() * 0.5f;
                    float ry = rand() * 6.28f, sy = s * (0.75f + rand() * 0.35f);
                    bags.Add(new Xform { x = cx + Mathf.Cos(a) * d, y = Y + 0.24f * s + (i > n - 3 ? 0.35f : 0), z = cz + Mathf.Sin(a) * d * 0.6f, ry = ry, sx = s, sy = sy, sz = s });
                    bagCol.Add(Idx(rand(), 5));
                }
                c.Box(cx - 0.9f, cx + 0.9f, cz - 0.6f, cz + 0.6f, 1, "trash");
            }
            var bagGeo = Geo.Icosahedron(0.3f, 1); var bagMat = kit.Mat("paint:#ffffff", new MatSpec { roughness = 0.32f, metalness = 0 });
            for (int k = 0; k < cols.Length; k++)
            {
                var sub = new List<Xform>(); for (int i = 0; i < bags.Count; i++) if (bagCol[i] == k) sub.Add(bags[i]);
                if (sub.Count > 0) c.group.Add(kit.Instanced(bagGeo, Tinted(kit, "town:bag", bagMat, cols[k]), sub));
            }
        }

        // ---- bicycles (frame tinted per instance) ----
        static void Bikes(Ctx c)
        {
            var kit = c.kit; var rand = c.rand;
            var bikes = new List<Xform>();
            foreach (var (x, z, ry, n) in new[] { (30.6f, 11.2f, 0f, 3), (-5.5f, 11.0f, 0f, 2), (64.5f, 27.4f, PI / 2, 4), (-62f, 13.4f, 0f, 2), (15.8f, -9.4f, 0f, 2) })
            {
                bool turned = ry != 0;
                for (int i = 0; i < n; i++) bikes.Add(new Xform { x = x + (turned ? 0 : i * 0.75f), y = Y, z = z + (turned ? i * 0.75f : 0), ry = ry + (rand() - 0.5f) * 0.15f });
                float ex = turned ? 0.6f : (n - 1) * 0.75f + 0.3f, ez = turned ? (n - 1) * 0.75f + 0.6f : 0.6f;
                c.Box(x - (turned ? ex : 0.3f), x + ex, z - (turned ? 0.6f : ez), z + ez, 1.1f, "bikes");
            }
            GeoData WheelL(float x) => Geo.Torus(0.33f, 0.022f, 6, 22).RotateY(PI / 2).Translate(0, 0.35f, x);
            GeoData Tube(Vector3 a, Vector3 b, float r = 0.018f)
            {
                var d = b - a; var g = Geo.Cylinder(r, r, d.magnitude, 6);
                g.ApplyMatrix(Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, d.normalized))); // setFromUnitVectors (JS-space vectors)
                return g.Translate((a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2);
            }
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
            var frame = Geo.Merge(new[]
            {
                Tube(V(0, 0.35f, 0.52f), V(0, 0.42f, -0.05f)), Tube(V(0, 0.42f, -0.05f), V(0, 0.82f, 0.38f)), Tube(V(0, 0.42f, -0.05f), V(0, 0.86f, -0.12f)), Tube(V(0, 0.86f, -0.12f), V(0, 0.82f, 0.38f)),
                Tube(V(0, 0.35f, 0.52f), V(0, 0.86f, -0.12f)), Tube(V(0, 0.82f, 0.38f), V(0, 0.35f, 0.58f)), Tube(V(0, 0.82f, 0.38f), V(0, 1.0f, 0.42f)), Tube(V(-0.28f, 1.0f, 0.42f), V(0.28f, 1.0f, 0.42f), 0.016f),
                Geo.Box(0.14f, 0.05f, 0.26f).Translate(0, 0.98f, -0.16f),
            });
            string[] cols = { "#2a5b8f", "#c8c8c8", "#a12c2c", "#2f2f33", "#6b8f3a", "#e0e0d8" };
            var frameMat = kit.Mat("paint:#ffffff", new MatSpec { roughness = 0.4f, metalness = 0.5f });
            var colOf = new List<int>(); foreach (var _ in bikes) colOf.Add(Idx(rand(), cols.Length));
            for (int k = 0; k < cols.Length; k++)
            {
                var sub = new List<Xform>(); for (int i = 0; i < bikes.Count; i++) if (colOf[i] == k) sub.Add(bikes[i]);
                if (sub.Count > 0) c.group.Add(kit.Instanced(frame, Tinted(kit, "town:bikeFrame", frameMat, cols[k]), sub));
            }
            c.group.Add(kit.Instanced(Geo.Merge(new[] { WheelL(-0.55f), WheelL(0.55f) }), kit.Mat("rubber"), bikes));
            c.stat.Add(kit.Box(2.4f, 0.05f, 0.05f, c.metal, At(31.4f, Y + 0.6f, 11.6f)));
        }
    }
}
