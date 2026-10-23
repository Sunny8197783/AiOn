// Ports levels/shelter-shell.js: structure of the 생활문화센터 basement car park (B1) — epoxy floor, walls, 8.1 m
// pillar grid with zone labels, flat slab with drop panels, bay lines, and the four ways in: stair A (north),
// car ramp B (south), accessible ramp C (north-west) and the east core (lift hall from the main lobby).
// Local frame: x east, z south, floor y = 0. Town footprint maps as local = town + (91, 0, -31).
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ShelterUtil;

namespace Gyeongbo.Levels
{
    public sealed class ShelterRoom
    {
        public float x0 = -22, x1 = 22, z0 = -16, z1 = 16, ceil = 3.0f, drop = 2.72f;
    }

    public sealed class ShelterGrid
    {
        public float[] xs = { -16.2f, -8.1f, 0, 8.1f, 16.2f }, zs = { -10.5f, 0, 10.5f };
    }

    // A ceiling batten spot the services hang a fluorescent fitting on (panel: a square LED panel instead).
    public sealed class ShelterFixture
    {
        public float x, y, z, rx, ry;
        public bool panel;
    }

    // A real light the services create: { pos, color, base, dist, circuit } (circuit A stays on emergency supply).
    public sealed class ShelterLamp
    {
        public Vector3 pos;
        public int color;
        public float @base, dist;
        public string circuit;
    }

    public sealed class ShelterShellResult
    {
        public GameObject group;
        public List<WalkZone> walk = new List<WalkZone>();
        public List<Solid> solids = new List<Solid>();
        public List<ShelterFixture> fixtures = new List<ShelterFixture>();
        public List<ShelterLamp> lights = new List<ShelterLamp>();
        public List<Material> lift = new List<Material>();   // dyn.lift: the lift display and call button (dark without grid power)
        public Dictionary<string, Material> M = new Dictionary<string, Material>();
    }

    public static class ShelterShell
    {
        public static readonly ShelterRoom ROOM = new ShelterRoom();
        public static readonly ShelterGrid GRID = new ShelterGrid();
        // Only x0/x1 (STAIR, ACCESS, RAMP) or z0/z1 (EAST) are used, as in JS.
        public static readonly RectXZ STAIR = new RectXZ(3.6f, 6.4f, 0, 0), ACCESS = new RectXZ(-14.2f, -11.8f, 0, 0), RAMP = new RectXZ(8, 14, 0, 0), EAST = new RectXZ(0, 0, -1.1f, 1.1f);

        // A cylinder rod from a to b (JS space).
        public static GameObject Rod(Vector3 a, Vector3 b, float r, Material mat, int seg = 10)
        {
            var dir = b - a; float len = dir.magnitude;
            var m = Geo.Obj(Geo.Cylinder(r, r, len, seg), mat, "rod", true, true);
            J.SetPos(m, (a + b) / 2);
            m.transform.localRotation = Quaternion.FromToRotation(Vector3.up, J.V(dir.normalized));
            return m;
        }
        static GameObject Rod(float ax, float ay, float az, float bx, float by, float bz, float r, Material mat, int seg = 10) =>
            Rod(new Vector3(ax, ay, az), new Vector3(bx, by, bz), r, mat, seg);

        public static ShelterShellResult BuildShell(Kit kit, ShelterTex T)
        {
            var g = J.Group("shelter-shell");
            var res = new ShelterShellResult { group = g };
            var walk = res.walk; var solids = res.solids; var fixtures = res.fixtures; var lights = res.lights;
            GameObject add(GameObject m, GameObject parent = null) { (parent ?? g).Add(m); return m; }
            void solid(float x0, float x1, float z0, float z1, float y0 = -1, float y1 = 6.5f) => solids.Add(new Solid(x0, x1, z0, z1, y0, y1));
            At at(float x = 0, float y = 0, float z = 0, float rx = 0, float ry = 0, float rz = 0) => new At { x = x, y = y, z = z, rx = rx, ry = ry, rz = rz };
            var ROOM = ShelterShell.ROOM; var GRID = ShelterShell.GRID;
            const float PI = Mathf.PI;

            Material floor = T.FloorMaterial(), wall = kit.Surface("plaster", new SurfaceOpts { color = J.Hex(0xdcdad3) }), ceil = kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0xbdbcb6) }),
                pillar = kit.Surface("plaster", new SurfaceOpts { color = J.Hex(0xefeee9) }), band = kit.Mat("paint:#3f6a57", new MatSpec { roughness = 0.45f }), stripe = kit.Mat("paint:#e2b324", new MatSpec { roughness = 0.5f }),
                line = kit.Mat("paint:#ecebe3", new MatSpec { roughness = 0.5f, polygonOffset = true, polygonOffsetFactor = -1, polygonOffsetUnits = -1 }),
                blue = kit.Mat("paint:#2a64b8", new MatSpec { roughness = 0.5f, polygonOffset = true, polygonOffsetFactor = -1, polygonOffsetUnits = -1 }),
                concrete = kit.Surface("concrete"), step = kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0xc9c6bd) }), steel = kit.Mat("steel"), metal = kit.Mat("metal"),
                dark = kit.Mat("darkMetal"), hazard = T.HazardMaterial(), groove = T.GrooveMaterial(), tactileM = T.TactileMaterial(),
                rubber = kit.Mat("paint:#59656e", new MatSpec { roughness = 0.92f }), door = kit.Mat("paint:#b9b6aa", new MatSpec { roughness = 0.5f, metalness = 0.4f }), tile = kit.Surface("tile"),
                gyp = kit.Mat("whiteWall"), stainless = kit.Mat("metal", new MatSpec { roughness = 0.22f });
            var M = res.M;
            M["floor"] = floor; M["wall"] = wall; M["ceil"] = ceil; M["pillar"] = pillar; M["band"] = band; M["stripe"] = stripe; M["line"] = line; M["blue"] = blue;
            M["concrete"] = concrete; M["step"] = step; M["steel"] = steel; M["metal"] = metal; M["dark"] = dark; M["hazard"] = hazard; M["groove"] = groove; M["tactile"] = tactileM;
            M["rubber"] = rubber; M["door"] = door; M["tile"] = tile; M["gyp"] = gyp; M["stainless"] = stainless;

            // Sloped slabs. A is the lower-z end (z increases from A to B); top surface runs through both points.
            GameObject slopeZ(float x0, float x1, float zA, float yA, float zB, float yB, float th, Material mat, GameObject parent = null)
            {
                float dz = zB - zA, dy = yB - yA, L = Hypot(dz, dy), a = Mathf.Atan2(-dy, dz);
                var m = kit.Slab(x1 - x0, th, L, mat, at(rx: a));
                J.SetPos(m, (x0 + x1) / 2, (yA + yB) / 2 - Mathf.Cos(a) * th / 2, (zA + zB) / 2 - Mathf.Sin(a) * th / 2);
                return add(m, parent);
            }
            GameObject slopeX(float xA, float yA, float xB, float yB, float z0, float z1, float th, Material mat)
            {
                float dx = xB - xA, dy = yB - yA, L = Hypot(dx, dy), a = Mathf.Atan2(dy, dx);
                var m = kit.Slab(L, th, z1 - z0, mat, at(rz: a));
                J.SetPos(m, (xA + xB) / 2 + Mathf.Sin(a) * th / 2, (yA + yB) / 2 - Mathf.Cos(a) * th / 2, (z0 + z1) / 2);
                return add(m);
            }

            // ---- floor, ceiling, perimeter walls with openings ----
            add(kit.Plane(44, 32, floor, at(0, 0, 0)));
            add(kit.Slab(44.5f, 0.3f, 32.5f, ceil, at(y: ROOM.ceil)));
            walk.Add(new WalkZone(ROOM.x0, ROOM.x1, ROOM.z0, ROOM.z1, 0));

            // Wall along x at z (side -1: thickness toward -z). openings: [a, b, top, lintelTop]
            void wallX(float z, float x0, float x1, float side, float[][] openings = null)
            {
                float cur = x0, zc = z + side * 0.125f, inner = z - side * 0.012f;
                void seg(float a, float b)
                {
                    add(kit.Slab(b - a, ROOM.ceil, 0.25f, wall, at((a + b) / 2, 0, zc)));
                    add(kit.Box(b - a, 0.9f, 0.02f, band, at((a + b) / 2, 0, inner)));
                    add(kit.Box(b - a, 0.07f, 0.025f, stripe, at((a + b) / 2, 0.9f, inner)));
                    add(kit.Box(b - a, 0.006f, 0.24f, dark, at((a + b) / 2, 0, z - side * 0.14f)));  // drain grating
                    solid(a, b, Mathf.Min(z, z + side * 0.25f), Mathf.Max(z, z + side * 0.25f));
                }
                foreach (var o in openings ?? new float[0][])
                {
                    float a = o[0], b = o[1], top = o[2], lt = o.Length > 3 ? o[3] : ROOM.ceil;
                    if (a > cur) seg(cur, a);
                    add(kit.Slab(b - a, lt - top, 0.25f, wall, at((a + b) / 2, top, zc)));
                    solids.Add(new Solid(a, b, Mathf.Min(z, z + side * 0.25f), Mathf.Max(z, z + side * 0.25f), top, lt));
                    cur = b;
                }
                if (cur < x1) seg(cur, x1);
            }
            void wallZ(float x, float z0, float z1, float side, float[][] openings = null)
            {
                float cur = z0, xc = x + side * 0.125f, inner = x - side * 0.012f;
                void seg(float a, float b)
                {
                    add(kit.Slab(0.25f, ROOM.ceil, b - a, wall, at(xc, 0, (a + b) / 2)));
                    add(kit.Box(0.02f, 0.9f, b - a, band, at(inner, 0, (a + b) / 2)));
                    add(kit.Box(0.025f, 0.07f, b - a, stripe, at(inner, 0.9f, (a + b) / 2)));
                    add(kit.Box(0.24f, 0.006f, b - a, dark, at(x - side * 0.14f, 0, (a + b) / 2)));
                    solid(Mathf.Min(x, x + side * 0.25f), Mathf.Max(x, x + side * 0.25f), a, b);
                }
                foreach (var o in openings ?? new float[0][])
                {
                    float a = o[0], b = o[1], top = o[2];
                    if (a > cur) seg(cur, a);
                    add(kit.Slab(0.25f, ROOM.ceil - top, b - a, wall, at(xc, top, (a + b) / 2)));
                    cur = b;
                }
                if (cur < z1) seg(cur, z1);
            }
            wallX(ROOM.z0, ROOM.x0 - 0.25f, ROOM.x1 + 0.25f, -1, new[] { new[] { ACCESS.x0, ACCESS.x1, 2.6f }, new[] { STAIR.x0, STAIR.x1, 2.2f, 6.2f } });
            wallX(ROOM.z1, ROOM.x0 - 0.25f, ROOM.x1 + 0.25f, 1, new[] { new[] { RAMP.x0, RAMP.x1, 2.5f } });
            wallZ(ROOM.x0, ROOM.z0, ROOM.z1, -1);
            wallZ(ROOM.x1, ROOM.z0, ROOM.z1, 1, new[] { new[] { EAST.z0, EAST.z1, 2.2f } });

            // ---- pillars: zone colour band, corner guards, labels on four faces, drop panel ----
            var atlas = T.PillarAtlas();
            for (int row = 0; row < GRID.zs.Length; row++)
                for (int col = 0; col < GRID.xs.Length; col++)
                {
                    float pz = GRID.zs[row], px = GRID.xs[col];
                    var zone = ShelterTextures.ZONES[row];
                    add(kit.Slab(0.7f, ROOM.drop, 0.7f, pillar, at(px, 0, pz)));
                    add(kit.Box(0.72f, 1.05f, 0.72f, kit.Mat("paint:" + zone.color, new MatSpec { roughness = 0.5f }), at(px, 0, pz)));
                    foreach (var c in new[] { new[] { -1, -1 }, new[] { 1, -1 }, new[] { 1, 1 }, new[] { -1, 1 } }) add(kit.Box(0.09f, 1.2f, 0.09f, hazard, at(px + c[0] * 0.335f, 0, pz + c[1] * 0.335f)));
                    for (int f = 0; f < 4; f++)
                    {
                        float a = f * PI / 2;
                        var lbl = Geo.Obj(ShelterTextures.CellPlane(0.5f, 0.62f, ShelterTex.PillarCell(row, col)), atlas, "label", false, false);
                        J.SetPos(lbl, px + Mathf.Sin(a) * 0.356f, 1.95f, pz + Mathf.Cos(a) * 0.356f); J.SetYaw(lbl, a);
                        add(lbl);
                    }
                    add(kit.Slab(2.4f, ROOM.ceil - ROOM.drop, 2.4f, ceil, at(px, ROOM.drop, pz)));
                    solid(px - 0.37f, px + 0.37f, pz - 0.37f, pz + 0.37f);
                }

            // ---- bay lines: north bays, middle double row, south bays (2.5 m bays between pillar columns) ----
            var spans = new List<float> { ROOM.x0 }; spans.AddRange(GRID.xs); spans.Add(ROOM.x1);
            void ln(float x0, float x1, float z0, float z1, Material mat = null) => add(kit.Plane(x1 - x0, z1 - z0, mat ?? line, at((x0 + x1) / 2, 0.004f, (z0 + z1) / 2)));
            var bays = new List<float[]>();
            for (int i = 0; i < spans.Count - 1; i++)
            {
                float a = spans[i] + (i > 0 ? 0.4f : 0.1f), b = spans[i + 1] - (i < spans.Count - 2 ? 0.4f : 0.1f); int n = Round((b - a) / 2.5f); float w = (b - a) / n;
                for (int k = 0; k < n; k++) bays.Add(new[] { a + k * w, a + (k + 1) * w });
            }
            bool blockedN(float x0, float x1) => x1 > ACCESS.x0 - 1.8f && x0 < ACCESS.x1 + 0.6f || x1 > STAIR.x0 - 0.4f && x0 < STAIR.x1 + 0.4f;
            bool blockedS(float x0, float x1) => x1 > RAMP.x0 - 0.3f && x0 < RAMP.x1 + 0.3f;
            float[] accessBay = null;
            foreach (var bay in bays)
            {
                float x0 = bay[0], x1 = bay[1];
                Material lm = line;
                if (accessBay == null && x0 > ACCESS.x1 && x1 < -8) { accessBay = new[] { x0, x1 }; lm = blue; }
                if (!blockedN(x0, x1)) { ln(x0 - 0.06f, x0 + 0.06f, -16, -10.8f, lm); ln(x1 - 0.06f, x1 + 0.06f, -16, -10.8f, lm); ln(x0, x1, -10.86f, -10.74f, lm); }
                ln(x0 - 0.06f, x0 + 0.06f, -5.2f, 5.2f); ln(x1 - 0.06f, x1 + 0.06f, -5.2f, 5.2f);
                if (!blockedS(x0, x1)) { ln(x0 - 0.06f, x0 + 0.06f, 10.8f, 16); ln(x1 - 0.06f, x1 + 0.06f, 10.8f, 16); ln(x0, x1, 10.74f, 10.86f); }
            }
            ln(ROOM.x0, ROOM.x1, -5.26f, -5.14f); ln(ROOM.x0, ROOM.x1, 5.14f, 5.26f); ln(ROOM.x0, ROOM.x1, -0.06f, 0.06f);

            // painted symbols and hatches from the marking atlas
            var marks = T.MarkAtlas();
            GameObject mark(int i, float x, float z, float w, float d, float ry = 0)
            {
                var m = Geo.Obj(ShelterTextures.CellPlane(w, d, ShelterTex.MarkCell(i)), marks, "mark", false, true);
                J.SetRot(m, -PI / 2, 0, ry); J.SetPos(m, x, 0.006f, z);
                return add(m);
            }
            foreach (var x in new float[] { -16, -4, 12 }) mark(0, x, -8, 1.1f, 1.6f, PI / 2);   // north aisle runs west
            foreach (var x in new float[] { -12, 0, 18 }) mark(0, x, 8, 1.1f, 1.6f, -PI / 2);    // south aisle runs east
            if (accessBay != null) mark(1, (accessBay[0] + accessBay[1]) / 2, -13.4f, 1.6f, 1.6f);
            mark(2, 11, 13.2f, 2.2f, 2.2f); mark(3, 19.6f, -8, 1.8f, 1.8f, PI / 2); mark(4, 18.5f, -13.2f, 1.4f, 1.4f);
            mark(5, (STAIR.x0 + STAIR.x1) / 2, -14.75f, 3.2f, 2.5f); mark(5, -13.4f, -14.5f, 3.6f, 3); mark(5, 20.7f, 0, 2.6f, 2.6f);

            // tactile warning strips at the stair foot and the accessible ramp
            void tactile(float x0, float x1, float z0, float z1, float y = 0.008f) => add(kit.Plane(x1 - x0, z1 - z0, tactileM, at((x0 + x1) / 2, y, (z0 + z1) / 2)));
            tactile(STAIR.x0, STAIR.x1, -16.2f, -15.6f); tactile(ACCESS.x0, ACCESS.x1, -15.9f, -15.3f);

            ShelterShellParts.StairA(kit, res, add, solid);
            ShelterShellParts.RampB(kit, res, add, solid, slopeZ);
            ShelterShellParts.RampC(kit, res, add, solid, slopeZ, slopeX, tactile);
            ShelterShellParts.EastCore(kit, res, add, solid);
            return res;
        }
    }
}
