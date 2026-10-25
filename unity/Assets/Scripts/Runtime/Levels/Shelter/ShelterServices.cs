// Ports levels/shelter-services.js: ceiling services and safety equipment of the basement car park — twin-tube
// fluorescent battens on two circuits (A stays on the emergency supply, B drops out), area lights, battery emergency
// lights, exit signs (피난구·통로 유도등), red sprinkler mains with heads, cable trays, a grey drain line, the west
// supply duct, two jet fans, hydrant cabinets (옥내소화전), extinguishers at pillars and CCTV domes.
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ShelterShell;
using static Gyeongbo.Levels.ShelterUtil;

namespace Gyeongbo.Levels
{
    // A real light with its three.js base intensity and circuit; unit converts three.js intensity to the Light's.
    public sealed class ShelterLight
    {
        public Light light;
        public float @base, unit;
        public string circuit;
    }

    public sealed class ShelterFan
    {
        public GameObject rotor;
        public float speed, angle;   // angle: the rotor's rotation.x
    }

    public sealed class ShelterServicesResult
    {
        public GameObject group, live;
        public Dictionary<string, Material> tubes = new Dictionary<string, Material>();   // A, B, F (the faulty one)
        public List<ShelterLight> lights = new List<ShelterLight>();
        public List<ShelterLight> emLights = new List<ShelterLight>();
        public Material emLamp, emLed, cctvLed;
        public List<ShelterFan> fans = new List<ShelterFan>();
        public int faulty;
    }

    public static class ShelterServices
    {
        const float PI = Mathf.PI;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static At at(float x = 0, float y = 0, float z = 0, float rx = 0, float ry = 0, float rz = 0) => new At { x = x, y = y, z = z, rx = rx, ry = ry, rz = rz };
        static Matrix4x4 S(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float s = 1) => T4(x, y, z, rx, ry, rz, s, s, s);

        // A light created at three.js intensity 1 to learn the unit, then dark until update sets it.
        static float Unit(Light l) { float u = l.intensity; l.intensity = 0; return u; }

        public static ShelterServicesResult BuildServices(Kit kit, ShelterTex T, ShelterShellResult shell)
        {
            var g = J.Group("shelter-services");
            var live = J.Group("shelter-services-live");   // animated, never merged
            var res = new ShelterServicesResult { group = g, live = live };
            GameObject add(GameObject m, GameObject parent = null) { (parent ?? g).Add(m); return m; }
            GameObject inst(GeoData geo, Material mat, List<Matrix4x4> mats, bool cast = false) => Inst(g, geo, mat, mats, cast, false);
            bool low = kit.Quality == "low";
            Material housing = kit.Mat("paint:#e7e7e2", new MatSpec { roughness = 0.4f, metalness = 0.3f }), red = kit.Mat("paint:#c4302b", new MatSpec { roughness = 0.45f, metalness = 0.2f }),
                galv = kit.Mat("metal", new MatSpec { color = J.Hex(0xb9bdc0), roughness = 0.5f }), grey = kit.Mat("paint:#9da2a6", new MatSpec { roughness = 0.6f }), dark = kit.Mat("darkMetal"),
                cable = kit.Mat("rubber"), brass = kit.Mat("metal", new MatSpec { color = J.Hex(0xc8a560), roughness = 0.35f }), white = kit.Mat("plastic");
            Material tube(int c) => Mat.Lit(new MatSpec { color = J.Hex(0xffffff), emissive = J.Hex(c), emissiveIntensity = 3.2f, roughness = 0.3f });
            var tubes = res.tubes;
            tubes["A"] = tube(0xeaf3ff); tubes["B"] = tube(0xeaf3ff); tubes["F"] = tube(0xe4f0ff);
            foreach (var m in tubes.Values) kit.Lit("indoor", m, 3.2f);

            // ---- fluorescent battens: grid along the aisles and over the bays, plus the shell's extra spots ----
            var spots = new List<ShelterFixture>();
            foreach (var z in new[] { -13.2f, -8, -2.6f, 2.6f, 8, 13.2f }) for (int i = 0; i < 14; i++) spots.Add(new ShelterFixture { x = -19.5f + 3 * i, y = 2.62f, z = z, ry = 0 });
            spots.AddRange(shell.fixtures);
            const int faulty = 33;
            var parts = new Dictionary<string, List<Matrix4x4>> { ["housing"] = new List<Matrix4x4>(), ["A"] = new List<Matrix4x4>(), ["B"] = new List<Matrix4x4>(), ["F"] = new List<Matrix4x4>(), ["rods"] = new List<Matrix4x4>(), ["panels"] = new List<Matrix4x4>() };
            for (int i = 0; i < spots.Count; i++)
            {
                var f = spots[i];
                var F = T4(f.x, f.y, f.z, f.rx, f.ry, 0);
                if (f.panel) { parts["panels"].Add(F); continue; }
                parts["housing"].Add(F);
                string key = i == faulty ? "F" : i % 2 == 1 ? "B" : "A";
                foreach (var dz in new[] { -0.04f, 0.04f }) parts[key].Add(F * T4(0, -0.035f, dz, 0, 0, PI / 2));
                foreach (var dx in new[] { -0.45f, 0.45f }) parts["rods"].Add(F * T4(dx, 0.2f, 0));
            }
            inst(Geo.Box(1.25f, 0.05f, 0.17f), housing, parts["housing"]);
            var tubeGeo = Geo.Cylinder(0.014f, 0.014f, 1.18f, 8);
            foreach (var k in new[] { "A", "B", "F" }) inst(tubeGeo, tubes[k], parts[k]);
            inst(Geo.Cylinder(0.006f, 0.006f, 0.36f, 5), dark, parts["rods"]);
            if (parts["panels"].Count > 0) inst(Geo.Box(0.6f, 0.04f, 0.6f), tubes["A"], parts["panels"]);

            // ---- real lights: area points on alternating circuits, one shadowed spot over the mats ----
            void point(Vector3 pos, int color, float @base, float dist, string circuit)
            {
                var l = kit.PointLight(J.Hex(color), 1, dist, 2); float u = Unit(l); l.shadows = LightShadows.None;
                J.SetPos(l.gameObject, pos); live.Add(l);
                res.lights.Add(new ShelterLight { light = l, @base = @base, unit = u, circuit = circuit });
            }
            var area = low
                ? new (float x, float z, string c)[] { (-12, -8, "A"), (6, -8, "B"), (-12, 8, "B"), (6, 8, "A") }
                : new (float x, float z, string c)[] { (-14, -8, "A"), (-2, -8, "B"), (10, -8, "A"), (-14, 8, "B"), (-2, 8, "A"), (10, 8, "B") };
            foreach (var a in area) point(V(a.x, 2.5f, a.z), 0xe6f0ff, low ? 22 : 16, 18, a.c);
            foreach (var l in shell.lights) point(l.pos, l.color, l.@base, l.dist, l.circuit);
            var spot = kit.SpotLight(J.Hex(0xedf3ff), 1, 15, 1.15f, 0.75f, 2); float su = Unit(spot);
            live.Add(spot); J.SetPos(spot.gameObject, -7, 2.85f, 0); J.LookAt(spot.transform, -7, 0, 0.4f);
            spot.shadows = low ? LightShadows.None : LightShadows.Soft;
            spot.shadowCustomResolution = kit.Quality == "high" ? 1024 : 512;
            // ponytail: three.js shadow.bias -0.0005 / normalBias 0.02 not converted; tune spot.shadowBias in Unity if acne shows.
            spot.shadowNearPlane = 0.5f;
            res.lights.Add(new ShelterLight { light = spot, @base = 34, unit = su, circuit = "A" });

            // ---- battery emergency lights (비상조명등): twin heads, dark until the power state says 'emergency' ----
            var emLamp = Mat.Lit(new MatSpec { color = J.Hex(0xf2f6ff), emissive = J.Hex(0xdde9ff), emissiveIntensity = 0, roughness = 0.2f });
            var emLed = Mat.Lit(new MatSpec { color = J.Hex(0x220000), emissive = J.Hex(0xff2a1a), emissiveIntensity = 1.5f });
            res.emLamp = emLamp; res.emLed = emLed;
            var emUnits = new[] { V(-4, -15.9f, 0), V(12, -15.9f, 0), V(-16, 15.9f, PI), V(2, 15.9f, PI), V(18, 15.9f, PI), V(-21.9f, -8, PI / 2), V(-21.9f, 8, PI / 2), V(21.9f, -8, -PI / 2), V(21.9f, 8, -PI / 2) };   // [x, z, ry]
            List<Matrix4x4> body = new List<Matrix4x4>(), heads = new List<Matrix4x4>(), leds = new List<Matrix4x4>();
            foreach (var u in emUnits)
            {
                var F = T4(u.x, 2.42f, u.y, 0, u.z, 0);
                body.Add(F * T4(0, 0, 0.06f));
                foreach (var dx in new[] { -0.1f, 0.1f }) heads.Add(F * T4(dx, -0.07f, 0.12f, 0.7f, 0, 0));
                leds.Add(F * T4(0.12f, 0.04f, 0.115f));
            }
            inst(Geo.Box(0.34f, 0.14f, 0.12f), white, body, true);
            inst(Geo.Cylinder(0.045f, 0.035f, 0.07f, 12), emLamp, heads);
            inst(Geo.Sphere(0.008f, 6, 4), emLed, leds);
            foreach (var (p, t) in new[] { (V(-4, 2.4f, -15.6f), V(-4, 0, -6)), (V(2, 2.4f, 15.6f), V(2, 0, 6)) })
            {
                var s = kit.SpotLight(J.Hex(0xdfe9ff), 1, 16, 0.95f, 0.85f, 2); float u = Unit(s); s.shadows = LightShadows.None;
                live.Add(s); J.SetPos(s.gameObject, p); J.LookAt(s.transform, t.x, t.y, t.z);
                res.emLights.Add(new ShelterLight { light = s, @base = 14, unit = u });
            }

            // ---- exit signs (emergency group: lit in every power state) ----
            var exitMat = Mat.Lit(new MatSpec { map = T.ExitTex(), emissive = J.Hex(0xffffff), emissiveMap = T.ExitTex(), roughness = 0.4f });
            kit.Lit("emergency", exitMat, 1.6f);
            void exitBox(float x, float y, float z, float ry)
            {
                add(kit.Box(0.44f, 0.23f, 0.07f, white, at(x - Mathf.Sin(ry) * 0.04f, y - 0.115f, z - Mathf.Cos(ry) * 0.04f, ry: ry)));
                var p = add(Geo.Obj(Geo.Plane(0.4f, 0.2f), exitMat, "exit", false, false)); J.SetPos(p, x, y, z); J.SetYaw(p, ry);
            }
            exitBox((STAIR.x0 + STAIR.x1) / 2, 2.66f, -15.62f, 0); exitBox(21.9f, 2.45f, 0, -PI / 2);
            exitBox((ACCESS.x0 + ACCESS.x1) / 2, 2.8f, -15.9f, 0); exitBox(STAIR.x1 - 0.1f, 5.7f, -16.85f, -PI / 2);
            // aisle guidance on the pillar faces that look into the aisles, arrows toward the nearest pedestrian exit
            var aisleMats = new Dictionary<int, Material>();
            Material aisleMat(int dir)
            {
                if (!aisleMats.TryGetValue(dir, out var m))
                    aisleMats[dir] = m = kit.Lit("emergency", Mat.Lit(new MatSpec { map = T.AisleTex(dir), emissive = J.Hex(0xffffff), emissiveMap = T.AisleTex(dir), roughness = 0.4f }), 1.3f);
                return m;
            }
            foreach (var px in GRID.xs)
            {
                float tA = px < -10 ? (ACCESS.x0 + ACCESS.x1) / 2 : px < 12 ? (STAIR.x0 + STAIR.x1) / 2 : 22;
                float tC = px < 5 ? (STAIR.x0 + STAIR.x1) / 2 : 22;
                foreach (var (pz, face, tx) in new[] { (GRID.zs[0], 1, tA), (GRID.zs[2], -1, tC) })
                {
                    float d = tx - px; int dir = (d != 0 ? System.Math.Sign(d) : 1) * face; float ry = face > 0 ? 0 : PI;
                    var p = add(Geo.Obj(Geo.Plane(0.36f, 0.135f), aisleMat(dir), "aisle", false, false)); J.SetPos(p, px, 1.0f, pz + face * 0.378f); J.SetYaw(p, ry);
                    add(kit.Box(0.38f, 0.15f, 0.03f, white, at(px, 0.925f, pz + face * 0.36f)));
                }
            }
            // hanging direction boards over the aisles
            void hang(string[] lines, float x, float z, float ry, float w = 1.7f)
            {
                var s = add(kit.Sign(lines, new SignOpts { w = w, h = 0.3f, bg = "#0d8a43", fg = "#ffffff", single = true, emissive = 0.6f })); J.SetPos(s, x, 2.38f, z); J.SetYaw(s, ry);
                kit.Lit("signs", MatOf(s), 0.6f);
                add(kit.Box(w + 0.04f, 0.32f, 0.03f, dark, at(x - Mathf.Sin(ry) * 0.02f, 2.22f, z - Mathf.Cos(ry) * 0.02f, ry: ry)));
                foreach (var d in new[] { -w / 2 + 0.1f, w / 2 - 0.1f }) add(Rod(V(x + Mathf.Cos(ry) * d, 2.54f, z - Mathf.Sin(ry) * d), V(x + Mathf.Cos(ry) * d, 3.0f, z - Mathf.Sin(ry) * d), 0.006f, dark, 4));
            }
            hang(new[] { "출입구 A · 계단 ↑" }, 5, -10.0f, 0); hang(new[] { "출입구 C · 경사로 ↑" }, -13, -10.0f, 0);
            hang(new[] { "동쪽 출입구 · 1층 로비 ↑" }, 19.6f, 0, -PI / 2, 2.0f); hang(new[] { "출입구 B · 차량 진출입로 ↑" }, 11, 11.2f, PI, 2.0f);

            // ---- sprinkler mains (red), branches and heads; hangers ----
            var mains = new[] { -12.4f, -5.2f, 5.2f, 12.4f };
            foreach (var z in mains) add(Rod(V(ROOM.x0 + 0.2f, 2.84f, z), V(ROOM.x1 - 0.2f, 2.84f, z), 0.055f, red, 12));
            List<Matrix4x4> br = new List<Matrix4x4>(), hd = new List<Matrix4x4>(), hang2 = new List<Matrix4x4>();
            foreach (var z in mains) for (int i = 0; i < 14; i++)
                {
                    float x = -18 + 3 * i;
                    // outer mains only branch toward the wall, clear of the pillar drop panels
                    foreach (var dz in Mathf.Abs(z) > 10 ? new[] { Mathf.Sign(z) * 1.3f } : new[] { -1.3f, 1.3f })
                    {
                        br.Add(T4(x, 2.82f, z + dz / 2, PI / 2, 0, 0));
                        hd.Add(T4(x, 2.76f, z + dz)); hd.Add(S(x, 2.73f, z + dz, 0, 0, 0, 0.6f));
                    }
                    hang2.Add(T4(x - 1.5f, 2.92f, z));
                }
            inst(Geo.Cylinder(0.022f, 0.022f, 1.3f, 8), red, br);
            inst(Geo.Cylinder(0.012f, 0.03f, 0.05f, 8), brass, hd);
            inst(Geo.Cylinder(0.008f, 0.008f, 0.16f, 4), dark, hang2);
            // grey drain line along the south wall with a downpipe at the corner
            add(Rod(V(ROOM.x0 + 0.3f, 2.88f, 15.6f), V(ROOM.x1 - 0.3f, 2.88f, 15.6f), 0.075f, grey, 12));
            add(Rod(V(ROOM.x0 + 0.3f, 0, 15.6f), V(ROOM.x0 + 0.3f, 2.95f, 15.6f), 0.075f, grey, 12));
            // cable trays (ladder type) with cables
            List<Matrix4x4> rungs = new List<Matrix4x4>(), th = new List<Matrix4x4>();
            foreach (var z in new[] { -8.9f, 8.9f })
            {
                foreach (var dz in new[] { -0.2f, 0.2f }) add(kit.Box(ROOM.x1 - ROOM.x0 - 0.4f, 0.08f, 0.02f, galv, at(0, 2.82f, z + dz)));
                for (double x = ROOM.x0 + 0.4; x < ROOM.x1 - 0.3; x += 0.3) rungs.Add(T4((float)x, 2.83f, z));
                for (double x = ROOM.x0 + 1; x < ROOM.x1; x += 1.5) foreach (var dz in new[] { -0.22f, 0.22f }) th.Add(T4((float)x, 2.94f, z + dz));
                foreach (var (dz, r) in new[] { (-0.12f, 0.018f), (-0.06f, 0.014f), (0f, 0.02f), (0.07f, 0.012f), (0.12f, 0.016f) })
                    add(Rod(V(ROOM.x0 + 0.3f, 2.86f + r, z + dz), V(ROOM.x1 - 0.3f, 2.86f + r, z + dz), r, cable, 6));
            }
            inst(Geo.Box(0.03f, 0.02f, 0.4f), galv, rungs);
            inst(Geo.Cylinder(0.006f, 0.006f, 0.12f, 4), dark, th);
            // supply duct along the west wall with grilles
            add(kit.Box(0.8f, 0.42f, ROOM.z1 - ROOM.z0 - 0.4f, galv, at(ROOM.x0 + 0.65f, 2.5f, 0)));
            for (double z = -13; z <= 13; z += 4.33) add(kit.Box(0.02f, 0.24f, 0.6f, dark, at(ROOM.x0 + 1.06f, 2.59f, (float)z)));

            // ---- jet fans: galvanised drum with grilles, rotor turning while the grid supply is on ----
            foreach (var (x, z) in new[] { (-3f, -8f), (12f, 8f) })
            {
                var drum = add(Geo.Obj(Geo.Cylinder(0.33f, 0.33f, 1.6f, 20, 1, true), kit.Mat("metal", new MatSpec { color = J.Hex(0xb9bdc0), roughness = 0.5f, side = "double" }), "fan-drum", false, false));
                J.SetRot(drum, 0, 0, PI / 2); J.SetPos(drum, x, 2.55f, z);
                foreach (var dx in new[] { -0.8f, 0.8f }) { var ring = add(Geo.Obj(Geo.Torus(0.33f, 0.025f, 6, 20), galv, "fan-ring", false, false)); J.SetPos(ring, x + dx, 2.55f, z); J.SetYaw(ring, PI / 2); }
                foreach (var dx in new[] { -0.5f, 0.5f }) add(kit.Box(0.06f, 0.12f, 0.5f, dark, at(x + dx, 2.88f, z)));
                var rotor = J.Group("fan-rotor"); J.SetPos(rotor, x, 2.55f, z); Kit.Keep(rotor);
                var hub = Geo.Obj(Geo.Cylinder(0.08f, 0.08f, 0.18f, 12), dark, "hub", false, false); J.SetRot(hub, 0, 0, PI / 2); rotor.Add(hub);
                for (int b = 0; b < 6; b++)
                {
                    var bl = Geo.Obj(Geo.Box(0.03f, 0.24f, 0.09f), galv, "blade", false, false);
                    J.SetPos(bl, 0, Mathf.Cos(b * PI / 3) * 0.16f, Mathf.Sin(b * PI / 3) * 0.16f); J.SetRot(bl, b * PI / 3 + 0.5f, 0, 0); rotor.Add(bl);
                }
                add(rotor, live); res.fans.Add(new ShelterFan { rotor = rotor, speed = 0 });
            }

            // ---- hydrant cabinets with their always-on red lamp ----
            var hydrantFace = Mat.Lit(new MatSpec { map = T.HydrantTex(), roughness = 0.45f, metalness = 0.2f });
            var hydLamp = kit.Lit("emergency", Mat.Lit(new MatSpec { color = J.Hex(0x400000), emissive = J.Hex(0xff2010), roughness = 0.3f }), 2.2f);
            foreach (var c in new[] { V(-21.9f, -2, PI / 2), V(-6, -15.9f, 0), V(-2, 15.9f, PI), V(21.9f, -9, -PI / 2) })   // [x, z, ry]
            {
                float x = c.x, z = c.y, ry = c.z, sx = Mathf.Sin(ry), cz = Mathf.Cos(ry);
                add(kit.Box(0.75f, 1.3f, 0.2f, red, at(x + sx * 0.1f, 0.5f, z + cz * 0.1f, ry: ry)));
                var f = add(Geo.Obj(Geo.Plane(0.7f, 1.24f), hydrantFace, "hydrant", false, false)); J.SetPos(f, x + sx * 0.205f, 1.15f, z + cz * 0.205f); J.SetYaw(f, ry);
                var l = add(Geo.Obj(Geo.Sphere(0.05f, 10, 6), hydLamp, "hydrant-lamp", false, false)); J.SetPos(l, x + sx * 0.1f, 1.86f, z + cz * 0.1f);
            }

            // ---- extinguishers beside pillars, with the red '소화기' plate (one shared material) ----
            var plate = kit.Sign(new[] { "소화기" }, new SignOpts { w = 0.32f, h = 0.14f, bg = "#d7262d", fg = "#ffffff", single = true });
            var ext = new[] { V(GRID.xs[0], GRID.zs[0], 1), V(GRID.xs[2], GRID.zs[0], 1), V(GRID.xs[4], GRID.zs[0], 1), V(GRID.xs[1], GRID.zs[1], -1), V(GRID.xs[3], GRID.zs[1], 1), V(GRID.xs[1], GRID.zs[2], -1), V(GRID.xs[3], GRID.zs[2], -1) };
            for (int i = 0; i < ext.Length; i++)
            {
                float px = ext[i].x, pz = ext[i].y, face = ext[i].z, z = pz + face * 0.5f, x = px + 0.22f;
                add(kit.Box(0.26f, 0.04f, 0.26f, dark, at(x, 0, z)));
                add(kit.Cyl(0.085f, 0.085f, 0.46f, red, at(x, 0.04f, z), 16));
                add(kit.Cyl(0.03f, 0.05f, 0.08f, dark, at(x, 0.5f, z), 8));
                add(Rod(V(x + 0.03f, 0.55f, z), V(x + 0.1f, 0.2f, z + face * 0.05f), 0.012f, cable, 5));
                var p = i == 0 ? plate : Object.Instantiate(plate);   // plate.clone(); the template itself is the first copy
                J.SetPos(p, px, 1.35f, pz + face * 0.358f); J.SetYaw(p, face > 0 ? 0 : PI); add(p);
            }

            // ---- CCTV domes with a blinking record LED; electrical panel by stair A ----
            var cctvLed = Mat.Lit(new MatSpec { color = J.Hex(0x300000), emissive = J.Hex(0xff1a1a), emissiveIntensity = 2 });
            res.cctvLed = cctvLed;
            foreach (var (x, z) in new[] { (2f, -11.6f), (-12f, -4.4f), (14f, 4.4f), (19f, -12f) })
            {
                add(kit.Cyl(0.09f, 0.09f, 0.04f, white, at(x, 2.96f, z), 14));
                var dome = add(Geo.Obj(Geo.Sphere(0.075f, 14, 8, 0, PI * 2, PI / 2, PI / 2), kit.Mat("darkGlass"), "cctv", false, false)); J.SetPos(dome, x, 2.96f, z);
                var led = add(Geo.Obj(Geo.Sphere(0.01f, 6, 4), cctvLed, "cctv-led", false, false)); J.SetPos(led, x + 0.08f, 2.95f, z);
            }
            var cc = add(kit.Sign(new[] { "CCTV 녹화 중" }, new SignOpts { w = 0.5f, h = 0.2f, bg = "#f4f2ec", fg = "#c8261f", single = true })); J.SetPos(cc, 0.4f, 1.75f, -15.97f);
            add(kit.Box(0.8f, 1.6f, 0.25f, kit.Mat("paint:#a9adb0", new MatSpec { roughness = 0.5f, metalness = 0.3f }), at(-1.5f, 0.3f, -15.86f)));
            var ep = add(kit.Sign(new[] { "분전반 · 관계자 외 조작 금지" }, new SignOpts { w = 0.7f, h = 0.12f, bg = "#f2c230", fg = "#111", single = true })); J.SetPos(ep, -1.5f, 1.6f, -15.73f);
            foreach (var (x, z, ry) in new[] { (21.97f, -5f, -PI / 2), (-21.97f, 5f, PI / 2) })
            {
                var b1 = add(kit.Sign(new[] { "B1" }, new SignOpts { w = 1.4f, h = 1.0f, bg = "#dcdad3", fg = "#2f8f5b", single = true })); J.SetPos(b1, x, 1.75f, z); J.SetYaw(b1, ry);
            }
            var ramps = add(kit.Sign(new[] { "출입구 C · 경사로", "휠체어 · 유모차 · 보행보조기" }, new SignOpts { w = 1.7f, h = 0.46f, bg = "#1d4fa0", single = true }));
            J.SetPos(ramps, ACCESS.x0 - 1.5f, 2.0f, -15.97f);
            var stairs = add(kit.Sign(new[] { "출입구 A · 계단" }, new SignOpts { w = 1.3f, h = 0.28f, bg = "#1d4fa0", single = true })); J.SetPos(stairs, STAIR.x0 - 0.8f, 2.0f, -15.97f);
            var rampB = add(kit.Sign(new[] { "지하주차장 출입구 B" }, new SignOpts { w = 1.6f, h = 0.28f, bg = "#1d4fa0", single = true })); J.SetPos(rampB, RAMP.x0 - 1.0f, 2.0f, 15.97f); J.SetYaw(rampB, PI);
            foreach (var (x, z, ry) in new[] { (ACCESS.x1 + 0.5f, -15.97f, 0f), (STAIR.x1 + 0.45f, -15.97f, 0f), (21.97f, 2.0f, -PI / 2) })
            {
                var s = add(kit.ShelterSign(0.36f)); J.SetPos(s, x, 1.85f, z); J.SetYaw(s, ry);
            }

            res.faulty = faulty;
            return res;
        }
    }
}
