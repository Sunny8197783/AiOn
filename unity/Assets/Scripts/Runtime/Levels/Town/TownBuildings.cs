// Ports levels/town-buildings.js: every building of game/townmap.js as facade shells (kit facades, night windows,
// parapets, rooftop plant, balcony slabs, apartment gable numbers), the logistics warehouse and its dock, the
// filler city outside the bounds (big background blocks split into villas with alleys so they read as streets),
// and the villa the D-day missile destroys, built unmerged so Town/TownStrike can swap it for a ruin.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Levels.TownUtil;

namespace Gyeongbo.Levels
{
    // A hole cut into one facade (the shelter's east entrance): side n|s|e|w, span a0..a1, up to y1.
    public sealed class FacadeHole
    {
        public string side;
        public float a0, a1, y1;
    }

    public sealed class FaceOpts
    {
        public float u0, u1 = 1, v0, v1 = 1;
        public bool cast = true;
    }

    // The strike villa: its group and footprint (null box when the town has none).
    public sealed class StrikeSite
    {
        public GameObject group;
        public StrikeBox box;
    }

    public sealed class StrikeBox
    {
        public float x0, x1, z0, z1, h;
    }

    public sealed class TownBuildingsResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public StrikeSite strike;
    }

    public static class TownBuildings
    {
        const float CURB = TownPlan.CURB;
        static readonly Dictionary<string, float> FH = new Dictionary<string, float> { ["apartment"] = 2.8f, ["villa"] = 2.9f, ["shop"] = 3.5f, ["mart"] = 4.6f, ["office"] = 3.7f, ["civic"] = 3.6f };
        static readonly HashSet<string> BACKGROUND = new HashSet<string> { "n1", "n2", "nw1", "s1", "s2", "sw1" };

        public static float HeightOf(Building b) =>
            b.h.HasValue ? (float)b.h.Value : b.floors * (FH.TryGetValue(b.kind ?? "", out var fh) ? fh : 3) + (b.kind == "apartment" ? 0.8f : 0);

        // Vertical plane for one building face, front side outward. side: 'n'|'s'|'e'|'w'. a0..a1 along the
        // face (world x for n/s, world z for e/w), y0..y1 height; u/v ranges into the texture. Texture u runs
        // left->right as seen from outside, so sub-ranges match world a0->a1 only on 's' and 'w' faces.
        public static GameObject FacePlane(string side, float c, float a0, float a1, float y0, float y1, Material material, FaceOpts o = null)
        {
            o ??= new FaceOpts();
            var g = Geo.Plane(a1 - a0, y1 - y0);
            for (int i = 0; i < g.uv.Count; i++) g.uv[i] = new Vector2(o.u0 + g.uv[i].x * (o.u1 - o.u0), o.v0 + g.uv[i].y * (o.v1 - o.v0));
            var m = Geo.Obj(g, material, null, o.cast, true);
            float mid = (a0 + a1) / 2, ym = (y0 + y1) / 2;
            if (side == "s" || side == "n") J.SetPos(m, mid, ym, c); else J.SetPos(m, c, ym, mid);
            J.SetYaw(m, side == "s" ? 0 : side == "n" ? Mathf.PI : side == "e" ? Mathf.PI / 2 : -Mathf.PI / 2);
            return m;
        }

        // holes: building id -> facade hole.
        public static TownBuildingsResult BuildBuildings(Kit kit, Dictionary<string, FacadeHole> holes = null)
        {
            holes ??= new Dictionary<string, FacadeHole>();
            var group = J.Group("buildings");
            var stat = J.Group("static", group); // swapped briefly while the strike villa is built into its own group
            var far = J.Group("far", group);
            // The villa the D-day missile destroys (town-strike.js): built unmerged so it can be swapped for a ruin.
            var strike = new StrikeSite { group = J.Group("strikeVilla", group), box = null };
            var solids = new List<Solid>();

            var roofMat = kit.Memo("town:roof", () => kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0xb5b2aa) }));
            var greenRoof = kit.Mat("paint:#5d8a6b", new MatSpec { roughness = 0.75f });
            var parapetMat = kit.Mat("whiteWall");
            Material metal = kit.Mat("metal"), steel = kit.Mat("steel");

            // Facade material with optional colour tint (clones share textures; registered for night windows).
            Material Fac(string kind, int floors, int cols, int seed, string tint = null) => kit.Memo($"town:fac:{kind}:{floors}:{cols}:{seed}:{tint ?? ""}", () =>
            {
                var bs = kit.Facade(kind, new FacadeOpts { floors = floors, cols = cols, seed = seed, lit = kind == "office" ? 0.45f : 0.6f });
                if (string.IsNullOrEmpty(tint)) return bs;
                var m = new Material(bs); Mat.SetColor(m, J.Css(tint)); kit.Lit("windows", m, 1.1f); return m;
            });

            // A box building from facade planes + roof + parapet. Long faces are n/s.
            void Shell(float x0, float x1, float z0, float z1, float h, Material lng, Material shrt, Material roof = null, Material parapet = null,
                float perL = 0, float perS = 0, float? y0Opt = null, FacadeHole hole = null, bool isFar = false)
            {
                float W = x1 - x0, D = z1 - z0;
                int Rep(float w, float per) => Math.Max(1, (int)Round(w / per));
                float uL = perL > 0 ? Rep(W, perL) : 1, uS = perS > 0 ? Rep(D, perS) : 1;
                float y0 = y0Opt ?? CURB;
                var faces = new (string side, float c, float a0, float a1, Material mat, float u1)[]
                    { ("s", z1, x0, x1, lng, uL), ("n", z0, x0, x1, lng, uL), ("e", x1, z0, z1, shrt, uS), ("w", x0, z0, z1, shrt, uS) };
                foreach (var (side, c, a0, a1, mat, u1) in faces)
                {
                    var target = isFar ? far : stat;
                    if (hole != null && hole.side == side)
                    {
                        float L = a1 - a0;
                        float U(float a) => (a - a0) / L * u1;
                        float V(float y) => (y - y0) / (h - y0);
                        target.Add(FacePlane(side, c, a0, hole.a0, y0, h, mat, new FaceOpts { u1 = U(hole.a0) }));
                        target.Add(FacePlane(side, c, hole.a1, a1, y0, h, mat, new FaceOpts { u0 = U(hole.a1), u1 = u1 }));
                        target.Add(FacePlane(side, c, hole.a0, hole.a1, hole.y1, h, mat, new FaceOpts { u0 = U(hole.a0), u1 = U(hole.a1), v0 = V(hole.y1) }));
                    }
                    else target.Add(FacePlane(side, c, a0, a1, y0, h, mat, new FaceOpts { u1 = u1, cast = !isFar }));
                }
                var top = Obj(Geo.Plane(W, D).RotateX(-Mathf.PI / 2), roof ?? roofMat, false, true);
                J.SetPos(top, (x0 + x1) / 2, h, (z0 + z1) / 2);
                (isFar ? far : stat).Add(top);
                if (!isFar) // parapet
                {
                    float p = 0.9f, t = 0.18f; var pm = parapet ?? parapetMat;
                    stat.Add(kit.Box(W, p, t, pm, At((x0 + x1) / 2, h, z0 + t / 2)), kit.Box(W, p, t, pm, At((x0 + x1) / 2, h, z1 - t / 2)),
                        kit.Box(t, p, D - 2 * t, pm, At(x0 + t / 2, h, (z0 + z1) / 2)), kit.Box(t, p, D - 2 * t, pm, At(x1 - t / 2, h, (z0 + z1) / 2)));
                }
            }
            // Rooftop clutter for low buildings: stair house, stainless tanks, AC units.
            void RoofKit(float x0, float x1, float z0, float z1, float h, Func<float> rr, bool green)
            {
                float cx = x0 + (x1 - x0) * (0.25f + rr() * 0.5f), cz = z0 + (z1 - z0) * (0.25f + rr() * 0.5f);
                stat.Add(kit.Box(3, 2.6f, 3.2f, parapetMat, At(cx, h, cz)), kit.Box(3.3f, 0.15f, 3.5f, roofMat, At(cx, h + 2.6f, cz)));
                for (int i = 0; i < 1 + (int)(rr() * 2); i++)
                    stat.Add(kit.Cyl(0.55f, 0.55f, 1.3f, metal, At(cx + 2.4f + i * 1.3f, h + 0.3f, cz + 1)), kit.Box(1.2f, 0.3f, 1.2f, steel, At(cx + 2.4f + i * 1.3f, h, cz + 1)));
                if (green) stat.Add(Obj(Geo.Plane(x1 - x0 - 0.4f, z1 - z0 - 0.4f).RotateX(-Mathf.PI / 2).Translate((x0 + x1) / 2, h + 0.02f, (z0 + z1) / 2), greenRoof));
            }

            // Apartment gable: plain wall, brand mark and the big 동 number.
            Material Gable(string label) => kit.Memo($"town:gable:{label}", () =>
            {
                var map = kit.Texture(kit.Canvas(512, 1536, (g, w, h) =>
                {
                    g.fillStyle = "#ebe6db"; g.fillRect(0, 0, w, h);
                    var gr = Kit.Rng((uint)(label.Length * 7 + 1)); for (int i = 0; i < 1500; i++) { g.fillStyle = $"rgba(0,0,0,{F(gr() * 0.035f)})"; g.fillRect(gr() * w, gr() * h, 3, 3); }
                    g.fillStyle = "#c9c2b2"; g.fillRect(0, 0, w * 0.1f, h); g.fillRect(w * 0.9f, 0, w * 0.1f, h);
                    g.fillStyle = "#3f7d5c"; g.fillRect(w * 0.1f, h * 0.035f, w * 0.8f, h * 0.012f);
                    g.fillStyle = "#e7a23a"; g.beginPath(); g.ellipse(w * 0.5f, h * 0.09f, w * 0.07f, w * 0.11f, 0.6f, 0, 7); g.fill();
                    g.fillStyle = "#3f7d5c"; g.font = $"800 {F(w * 0.14f)}px {Kit.FONT}"; g.textAlign = "center"; g.textBaseline = "middle"; g.fillText("한빛", w * 0.5f, h * 0.155f);
                    g.fillStyle = "#4b5560"; g.font = $"800 {F(w * 0.36f)}px {Kit.FONT}"; g.fillText(label, w * 0.5f, h * 0.27f);
                    g.fillStyle = "#5a6672"; for (int f = 0; f < 15; f++) { float y = h * 0.36f + f * h * 0.042f; g.fillRect(w * 0.44f, y, w * 0.12f, h * 0.02f); }
                }));
                return Mat.Lit(new MatSpec { map = map, roughness = 0.85f });
            });

            void Background(Building b)
            {
                float bx0 = (float)b.x0, bz0 = (float)b.z0, W = (float)(b.x1 - b.x0), D = (float)(b.z1 - b.z0);
                int nx = Math.Max(1, (int)Round(W / 17)), nz = Math.Max(1, (int)Round(D / 17)); const float gap = 3;
                float cw = (W - gap * (nx - 1)) / nx, cd = (D - gap * (nz - 1)) / nz; var rr = Kit.Rng(Seed(b.x0 * 13 + b.z0));
                int[] dFloors = { -1, 0, 0, 1 };
                string[] villaTints = { "#ffffff", "#d9c7b8", "#f2d5c0" }, shopTints = { "#ffffff", "#e6e0d2", "#cfd6dc" };
                for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
                {
                    float x0 = bx0 + i * (cw + gap), z0 = bz0 + j * (cd + gap); int floors = Math.Max(3, b.floors + dFloors[Idx(rr(), 4)]);
                    string kind = b.kind == "shop" || rr() < 0.3f ? "shop" : "villa"; int v = Idx(rr(), 3);
                    string tint = kind == "villa" ? villaTints[v] : shopTints[v];
                    float fh = FH[kind], h = CURB + floors * fh; bool hit = b.id == "s2" && i == 0 && j == 0;
                    var keep = stat; if (hit) { stat = strike.group; strike.box = new StrikeBox { x0 = x0, x1 = x0 + cw, z0 = z0, z1 = z0 + cd, h = h }; }
                    var fm = Fac(kind, floors, 4, v + 1, tint);
                    Shell(x0, x0 + cw, z0, z0 + cd, h, fm, fm, perL: 14, perS: 14);
                    RoofKit(x0, x0 + cw, z0, z0 + cd, h, rr, kind == "villa" && rr() < 0.7f);
                    stat = keep;
                }
            }

            void Warehouse(Building b, float h)
            {
                var ribs = kit.Memo("town:corrugated", () =>
                {
                    var c = kit.Canvas(256, 256, (g, w, hh) =>
                    {
                        for (int x = 0; x < w; x += 8) { int k = Math.Sin(x / 8.0 * Math.PI) > 0 ? 1 : 0; g.fillStyle = k != 0 ? "#a9b3b9" : "#8e989e"; g.fillRect(x, 0, 8, w); }
                        g.fillStyle = "rgba(80,60,40,0.08)"; g.fillRect(0, w * 0.85f, w, w * 0.15f);
                    });
                    var map = kit.Texture(c);
                    var nrm = kit.Texture(kit.NormalFrom(kit.Canvas(256, 256, (g, w, hh) =>
                    {
                        for (int x = 0; x < w; x++) { var v = F(128 + 100 * Math.Sin(x / 8.0 * Math.PI)); g.fillStyle = $"rgb({v},{v},{v})"; g.fillRect(x, 0, 1, w); }
                    }), 3), false);
                    return Mat.Lit(new MatSpec { map = map, normalMap = nrm, repeat = new Vector2(1 / 4f, 1 / 10f), roughness = 0.5f, metalness = 0.6f });
                });
                float bx0 = (float)b.x0, bx1 = (float)b.x1, bz0 = (float)b.z0, bz1 = (float)b.z1, W = bx1 - bx0, D = bz1 - bz0;
                // world-metre UVs so ribs keep their size: u range = width in metres, v = height in metres
                foreach (var (side, c, a0, a1) in new[] { ("s", bz1, bx0, bx1), ("n", bz0, bx0, bx1), ("e", bx1, bz0, bz1), ("w", bx0, bz0, bz1) })
                    stat.Add(FacePlane(side, c, a0, a1, CURB, h, ribs, new FaceOpts { u1 = a1 - a0, v1 = h - CURB }));
                stat.Add(Obj(Geo.Plane(W, D).RotateX(-Mathf.PI / 2).Translate((bx0 + bx1) / 2, h, (bz0 + bz1) / 2), kit.Mat("paint:#7d878d", new MatSpec { roughness = 0.5f, metalness = 0.5f })));
                // loading docks on the yard side (south): dock pit edge, bumpers, roll-up doors, canopy
                var dark = kit.Mat("paint:#16191c", new MatSpec { roughness = 0.9f });
                var shutter = kit.Memo("town:dockShutter", () => Mat.Lit(new MatSpec
                {
                    map = kit.Texture(kit.Canvas(128, 256, (g, w, hh) => { g.fillStyle = "#c3c7c9"; g.fillRect(0, 0, w, hh); g.fillStyle = "#9ea3a6"; for (int y = 0; y < hh; y += 10) g.fillRect(0, y, w, 3); })),
                    roughness = 0.5f, metalness = 0.5f,
                }));
                for (int i = 0; i < 6; i++)
                {
                    float x = 106 + i * 3.2f; bool open = i == 1 || i == 4;
                    stat.Add(FacePlane("s", bz1 + 0.02f, x - 1.3f, x + 1.3f, CURB + 1.1f, CURB + 4.1f, dark, new FaceOpts { cast = false }));
                    stat.Add(FacePlane("s", bz1 + 0.06f, x - 1.3f, x + 1.3f, open ? CURB + 3.3f : CURB + 1.1f, CURB + 4.1f, shutter, new FaceOpts { cast = false }));
                    stat.Add(kit.Box(0.25f, 0.5f, 0.2f, kit.Mat("rubber"), At(x - 1.1f, CURB + 0.7f, bz1 + 0.1f)), kit.Box(0.25f, 0.5f, 0.2f, kit.Mat("rubber"), At(x + 1.1f, CURB + 0.7f, bz1 + 0.1f)));
                    stat.Add(kit.Box(0.5f, 0.06f, 0.06f, kit.Mat("hazard"), At(x, CURB + 4.15f, bz1 + 0.05f)));
                }
                stat.Add(kit.Box(19.6f, 1.0f, 0.9f, kit.Surface("concrete"), At(114, CURB, bz1 + 0.45f)));
                stat.Add(kit.Box(19.6f, 0.04f, 0.12f, kit.Mat("hazard"), At(114, CURB + 1.0f, bz1 + 0.85f, cast: false)));
                stat.Add(kit.Box(20.5f, 0.3f, 4, steel, At(114, CURB + 4.6f, bz1 + 2)));
                solids.Add(new Solid(104.2f, 123.8f, bz1, bz1 + 0.9f, -1, CURB + 1, "dock"));
            }

            foreach (var b in TownMap.BUILDINGS)
            {
                float h = HeightOf(b); int seed = 7; foreach (var ch in b.id) seed = (seed * 31 + ch) % 997;
                float x0 = (float)b.x0, x1 = (float)b.x1, z0 = (float)b.z0, z1 = (float)b.z1;
                solids.Add(new Solid(x0, x1, z0, z1, -3, h + 1, b.id));
                if (BACKGROUND.Contains(b.id)) { Background(b); continue; }
                if (b.kind == "apartment")
                {
                    var lng = Fac("apartment", 15, 16, seed);
                    Shell(x0, x1, z0, z1, h, lng, Gable(b.label ?? ""));
                    // balcony slabs + fins on the south face, machine rooms and tank on the roof
                    for (int f = 1; f < 15; f++) stat.Add(kit.Box(x1 - x0, 0.14f, 0.35f, parapetMat, At((x0 + x1) / 2, CURB + f * 2.8f - 0.05f, z1 + 0.17f)));
                    for (float x = x0 + 6; x < x1; x += 12) stat.Add(kit.Box(0.3f, h - 0.4f, 0.45f, parapetMat, At(x, CURB, z1 + 0.22f)));
                    foreach (var x in new[] { x0 + 14, x1 - 14 }) stat.Add(kit.Box(5, 3.2f, 4.5f, parapetMat, At(x, h, (z0 + z1) / 2)), kit.Box(5.4f, 0.2f, 4.9f, roofMat, At(x, h + 3.2f, (z0 + z1) / 2)));
                    stat.Add(kit.Box(4, 2.2f, 2.6f, kit.Mat("paint:#5f87b0", new MatSpec { roughness = 0.45f }), At((x0 + x1) / 2, h + 0.4f, (z0 + z1) / 2)));
                    stat.Add(kit.Cyl(0.04f, 0.04f, 6, metal, At(x1 - 6, h, z0 + 2), 6));
                    continue;
                }
                if (b.kind == "warehouse") { Warehouse(b, h); continue; }
                string kind = b.kind == "mart" ? "shop" : b.kind;
                float W = x1 - x0, D = z1 - z0;
                int colsL = Math.Max(3, (int)Round(W / 3.6f)), colsS = Math.Max(2, (int)Round(D / 3.6f));
                Material lngM = Fac(kind, b.floors, colsL, seed), shrtM = Fac(kind, b.floors, colsS, seed + 1), parapetM = null;
                if (b.id == "culture") parapetM = kit.Mat("paint:#8a8f94", new MatSpec { roughness = 0.6f });
                holes.TryGetValue(b.id, out var hole);
                Shell(x0, x1, z0, z1, h, lngM, shrtM, parapet: parapetM, hole: hole, y0Opt: b.id == "culture" ? -0.9f : CURB);
                if (b.kind != "shop" || b.floors > 1) RoofKit(x0, x1, z0, z1, h, Kit.Rng((uint)seed), false);
            }

            // Filler blocks outside the bounds so the street ends in a city, not a plain (no shadows, merged).
            Material ffac = Fac("apartment", 18, 8, 77), fvil = Fac("villa", 5, 4, 78, "#e8d2c0"), fshop = Fac("shop", 4, 4, 79, "#e0dccf");
            var fr = Kit.Rng(99);
            float BX1 = (float)TownMap.BOUNDS.x1, BZ1 = (float)TownMap.BOUNDS.z1;
            for (int x = -300; x <= 300; x += 36) for (int z = -290; z <= 290; z += 34)
            {
                int ax = Math.Abs(x), az = Math.Abs(z);
                if (ax < BX1 + 14 && az < BZ1 + 14) continue;
                if (az < 20 || Math.Abs(Math.Abs(x) - 50) < 16) continue; // keep 연무로 / 문화로 / 한빛로 corridors open
                if (fr() < 0.18f) continue;
                bool big = fr() < 0.35f; float w = big ? 34 + fr() * 10 : 16 + fr() * 12, d = big ? 12 + fr() * 2 : 12 + fr() * 10;
                float x0 = x - w / 2 + (fr() - 0.5f) * 6, z0 = z - d / 2 + (fr() - 0.5f) * 6;
                int floors = big ? 15 + (int)(fr() * 8) : 3 + (int)(fr() * 3);
                float h = floors * (big ? 2.8f : 3);
                var lng = big ? ffac : fr() < 0.5f ? fvil : fshop;
                Shell(x0, x0 + w, z0, z0 + d, h, lng, lng, isFar: true, perL: big ? 24 : 14, perS: big ? 24 : 14, y0Opt: 0);
            }
            foreach (var r in far.GetComponentsInChildren<MeshRenderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }

            kit.Merge(stat); kit.Merge(far); kit.Merge(strike.group);
            return new TownBuildingsResult { group = group, solids = solids, strike = strike };
        }
    }
}
