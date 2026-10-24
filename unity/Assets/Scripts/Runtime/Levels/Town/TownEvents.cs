// Ports levels/town-events.js: story set dressing that switches on and off — road closures (water-filled barriers,
// cones, tape, a portable light tower, a flashing lamp, a guide with a light baton and solids that truly block), the
// emergency water point (tank trailer, taps, table, tent, queue lane) and the evacuation buses with the assembly-point
// reception.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownEventsResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        // Applies closure ids; returns the ones now active.
        public Func<List<string>, List<string>> setClosures;
        public Action<bool> setWater, setBuses;
        public Action<float, float, FrameCtx> update;
        // One point per closure guide (id "closure_<closureId>", enabled while that closure is active).
        public List<Point> closurePoints = new List<Point>();
        public List<IPerson> guides = new List<IPerson>();
        public Func<List<string>> active;
    }

    public sealed class AssemblySite
    {
        public XZ queue;
        public RectXZ area;
    }

    public static class TownEvents
    {
        public static readonly float WATER_TAP_Z = -19.6f;
        public static readonly XZ WATER_QUEUE = new XZ(-78.6f, -18.4f);
        public static readonly List<XZ> BUS = new List<XZ> { new XZ(-69.5f, -40.5f), new XZ(-65f, -40.5f) };
        public static readonly AssemblySite ASSEMBLY = new AssemblySite { queue = new XZ(-62.6f, -44.6f), area = new RectXZ(-61.8f, -58.6f, -49f, -40f) };

        const float Y = TownPlan.CURB;
        sealed class ClosureSet { public GameObject group; public List<Solid> solids; public Light light; }

        public static TownEventsResult BuildEvents(Kit kit, IPeople people, List<WalkZone> walk)
        {
            var group = J.Group("events");
            var solids = new List<Solid>();
            float hy(float x, float z) => TownUtil.HeightAt(walk, x, z);
            var steel = kit.Mat("steel"); var dark = kit.Mat("darkMetal"); var white = kit.Mat("paint:#f4f4f0", new MatSpec { roughness = 0.5f });
            IPerson person(string preset, int seed)
            {
                try { var p = people.CreatePerson(preset, seed); p.Group.SetActive(false); group.Add(p.Group); return p; }
                catch (Exception err) { Debug.LogError($"[town] createPerson({preset}) failed\n{err}"); return null; }
            }

            // ================= closures =================
            var barrierGeo = Geo.RoundedBox(1.45f, 0.9f, 0.5f, 2, 0.08f);
            var coneMat = kit.Memo("town:cone", () => Mat.Lit(new MatSpec
            {
                roughness = 0.5f,
                map = kit.Texture(kit.Canvas(16, 128, (g, w, h) => { g.fillStyle = "#ee5a1c"; g.fillRect(0, 0, w, h); g.fillStyle = "#f4f4f4"; g.fillRect(0, h * 0.18f, w, h * 0.14f); g.fillRect(0, h * 0.45f, w, h * 0.1f); })),
            }));
            var coneGeo = Geo.Merge(new[] { Geo.Cylinder(0.035f, 0.16f, 0.7f, 14).Translate(0, 0.39f, 0), Geo.Box(0.38f, 0.04f, 0.38f).Translate(0, 0.02f, 0) });
            var tapeMat = kit.Memo("town:tape", () => Mat.Lit(new MatSpec
            {
                side = "double", roughness = 0.6f,
                map = kit.Texture(kit.Canvas(512, 32, (g, w, h) => { g.fillStyle = "#f2c230"; g.fillRect(0, 0, w, h); g.fillStyle = "#111"; g.font = "800 22px sans-serif"; g.textBaseline = "middle"; for (int x = 4; x < w; x += 128) g.fillText("통제선 출입금지", x, h / 2f); })),
            }));
            var towerHead = kit.Memo("town:towerHead", () => kit.Lit("emergency", Mat.Lit(new MatSpec { color = J.Hex(0xffffff), emissive = J.Hex(0xf6fbff), emissiveIntensity = 6, roughness = 0.3f }), 6));
            var flashMat = Mat.Lit(new MatSpec { color = J.Hex(0x5a3a00), emissive = J.Hex(0xffa21a), emissiveIntensity = 0, roughness = 0.4f });
            var barrierMat = kit.Mat("paint:#ffffff", new MatSpec { roughness = 0.55f });
            var closures = new Dictionary<string, ClosureSet>();
            foreach (var kv in TownPlan.BARRICADES)
            {
                string id = kv.Key;
                var g = J.Group($"closure:{id}", group); g.SetActive(false);
                var units = new List<Xform>(); var cones = new List<Xform>(); var col = new List<string>(); var sol = new List<Solid>();
                Light light = null;
                TownPlan.CLOSURE_GUIDES.TryGetValue(id, out var gp);
                foreach (var r in kv.Value)
                {
                    bool alongZ = r.z1 - r.z0 > r.x1 - r.x0; float L = alongZ ? r.z1 - r.z0 : r.x1 - r.x0; int n = (int)Math.Floor(L / 1.5f);
                    float cx = (r.x0 + r.x1) / 2, cz = (r.z0 + r.z1) / 2;
                    float W = alongZ ? r.x1 - r.x0 : r.z1 - r.z0; var rows = W >= 3 ? new[] { -W / 2 + 0.6f, W / 2 - 0.6f } : new[] { 0f };
                    foreach (var o in rows) for (int k = 0; k < n; k++)
                    {
                        float a = -L / 2 + 0.75f + k * (L - 1.5f) / Math.Max(1, n - 1), x = alongZ ? cx + o : cx + a, z = alongZ ? cz + a : cz + o;
                        units.Add(new Xform { x = x, y = hy(x, z), z = z, ry = alongZ ? Mathf.PI / 2 : 0 }); col.Add(k % 2 != 0 ? "#f2f2ee" : "#e4572a");
                    }
                    // cones in a taper on the approach side (towards the guide), tape on posts above the barriers
                    float side = 1;
                    if (gp != null) { float sg0 = Math.Sign(alongZ ? gp.x - cx : gp.z - cz); side = sg0 != 0 ? sg0 : 1; }
                    for (int k = 0; k < 6; k++) { float a = -L / 2 + 1 + k * (L - 2) / 5, off = 1.3f + (k % 2) * 0.4f, x = alongZ ? cx + side * off : cx + a, z = alongZ ? cz + a : cz + side * off; cones.Add(new Xform { x = x, y = hy(x, z), z = z, ry = k }); }
                    var tape = TownBKit.Mesh(Geo.Plane(L, 0.06f), tapeMat);
                    J.SetPos(tape, cx, Y + 1.15f, cz); J.SetYaw(tape, alongZ ? Mathf.PI / 2 : 0); g.Add(tape);
                    foreach (var s in new[] { -1, 1 }) g.Add(kit.Cyl(0.025f, 0.025f, 1.2f, steel, new At { x = alongZ ? cx : cx + s * L / 2, y = Y, z = alongZ ? cz + s * L / 2 : cz }, 6));
                    var sg = kit.Sign(new[] { "차량 · 보행 통제", TownMap.CLOSURES[id].label }, new SignOpts { w = 1.4f, h = 0.7f, bg = "#c42b25", fg = "#ffffff", border = "#ffffff", single = true });
                    float edge = (alongZ ? r.x1 - r.x0 : r.z1 - r.z0) / 2 + 0.45f, sx = alongZ ? cx + side * edge : cx, sz = alongZ ? cz : cz + side * edge;
                    J.SetPos(sg, sx, hy(sx, sz) + 1.0f, sz); J.SetYaw(sg, alongZ ? (side > 0 ? Mathf.PI / 2 : -Mathf.PI / 2) : (side > 0 ? 0 : Mathf.PI)); g.Add(sg, kit.Box(0.06f, 0.7f, 0.06f, dark, new At { x = sx, y = hy(sx, sz), z = sz }));
                    var lamp = kit.Box(0.16f, 0.12f, 0.16f, flashMat, new At { x = alongZ ? cx : cx + L / 2 - 0.75f, y = hy(cx, cz) + 0.9f, z = alongZ ? cz + L / 2 - 0.75f : cz, cast = false }); g.Add(lamp);
                    sol.Add(new Solid(r.x0, r.x1, r.z0, r.z1, -1, 3, $"closure:{id}") { off = true });
                }
                if (gp != null) // portable light tower beside the guide
                {
                    float tx = gp.x + gp.face.z * 1.6f, tz = gp.z - gp.face.x * 1.6f;
                    g.Add(kit.Cyl(0.04f, 0.05f, 3.2f, steel, new At { x = tx, y = hy(tx, tz), z = tz }, 8), kit.Box(0.9f, 0.5f, 0.5f, kit.Mat("paint:#f2c230"), new At { x = tx, y = hy(tx, tz), z = tz }));
                    var head = kit.Box(0.7f, 0.35f, 0.12f, towerHead, new At { x = tx, y = hy(tx, tz) + 3.1f, z = tz, ry = TownUtil.YawTo(-gp.face.x, -gp.face.z), cast = false }); g.Add(head);
                    var Ls = kit.SpotLight(J.Hex(0xf4f8ff), 0, 26, 0.7f, 0.6f, 1.6f);
                    kit.Lit("emergency", Ls, 120); kit.GateLit(Ls, false); group.Add(Ls); light = Ls; // outside g: lights never hide
                    J.SetPos(Ls.gameObject, tx, hy(tx, tz) + 3.2f, tz); J.LookAt(Ls.transform, tx - gp.face.x * 6, 0, tz - gp.face.z * 6); // three.js L.target
                    sol.Add(new Solid(tx - 0.5f, tx + 0.5f, tz - 0.3f, tz + 0.3f, -1, 3, $"closure:{id}") { off = true });
                }
                // ponytail: three.js instanceColor (alternating red/white barriers) -> one instanced mesh per colour with a
                // tinted copy of the white paint; upgrade: per-instance colour in Kit.Instanced.
                foreach (var css in col.Distinct())
                    g.Add(kit.Instanced(barrierGeo, TownUtil.Tinted(kit, "town:barrier", barrierMat, css), units.Where((u, i) => col[i] == css).ToList()));
                g.Add(kit.Instanced(coneGeo, coneMat, cones));
                solids.AddRange(sol);
                kit.Merge(g);
                closures[id] = new ClosureSet { group = g, solids = sol, light = light };
            }
            var guides = new List<IPerson> { person("guide", 71) };
            var active = new List<string>();
            List<string> setClosures(List<string> ids)
            {
                var list = (ids ?? new List<string>()).Where(cid =>
                {
                    if (cid == null || !closures.ContainsKey(cid)) { Debug.LogError($"[town] unknown closure \"{cid}\"; known: {string.Join(", ", TownMap.CLOSURES.Keys)}"); return false; }
                    return true;
                }).ToList();
                active = list;
                foreach (var c in closures) { bool on = list.Contains(c.Key); c.Value.group.SetActive(on); if (c.Value.light != null) kit.GateLit(c.Value.light, on); foreach (var s in c.Value.solids) s.off = !on; }
                for (int k = 0; k < list.Count; k++) { if (k >= guides.Count) guides.Add(person("guide", 72 + k)); else if (guides[k] == null) guides[k] = person("guide", 72 + k); }
                for (int k = 0; k < guides.Count; k++)
                {
                    var p = guides[k];
                    if (p == null) continue;
                    var cid = k < list.Count ? list[k] : null; p.Group.SetActive(cid != null);
                    if (cid == null) continue;
                    var gp = TownPlan.CLOSURE_GUIDES[cid];
                    J.SetPos(p.Group, gp.x, hy(gp.x, gp.z), gp.z); J.SetYaw(p.Group, TownUtil.YawTo(gp.face.x, gp.face.z));
                    p.SetProp("baton", true); p.SetAnim("idle");
                }
                return list;
            }

            // ================= water point =================
            var water = J.Group("waterPoint", group); water.SetActive(false);
            float px = (float)TownMap.PLACES["water"].x;
            {
                float tz = WATER_TAP_Z, y = hy(px, tz);
                var tank = TownBKit.Mesh(Geo.Cylinder(0.8f, 0.8f, 3.4f, 24).RotateZ(Mathf.PI / 2), kit.Mat("metal", new MatSpec { roughness = 0.22f }), cast: true); J.SetPos(tank, px, y + 1.55f, tz - 1.4f); water.Add(tank);
                foreach (var s in new[] { -1, 1 }) { var cap = TownBKit.Mesh(Geo.Cylinder(0.81f, 0.81f, 0.06f, 24).RotateZ(Mathf.PI / 2), steel); J.SetPos(cap, px + s * 1.7f, y + 1.55f, tz - 1.4f); water.Add(cap); }
                water.Add(kit.Box(3.8f, 0.12f, 1.4f, dark, new At { x = px, y = y + 0.62f, z = tz - 1.4f }), kit.Box(0.12f, 0.12f, 1.8f, dark, new At { x = px + 2.4f, y = y + 0.5f, z = tz - 1.4f, ry = Mathf.PI / 2 }));
                foreach (var s in new[] { -1, 1 }) { var w = TownBKit.Mesh(Geo.Cylinder(0.36f, 0.36f, 0.22f, 16).RotateZ(Mathf.PI / 2), kit.Mat("rubber")); J.SetPos(w, px, y + 0.36f, tz - 1.4f + s * 0.8f); J.SetYaw(w, Mathf.PI / 2); water.Add(w); }
                var label = kit.Sign(new[] { "비상급수", "음용수 · 연무동 행정복지센터" }, new SignOpts { w = 2.2f, h = 0.55f, bg = "#1f5fa8", single = true }); J.SetPos(label, px, y + 1.6f, tz - 0.58f); water.Add(label);
                var pipe = TownBKit.Mesh(Geo.Cylinder(0.05f, 0.05f, 3.2f, 10).RotateZ(Mathf.PI / 2), kit.Mat("metal")); water.Add(pipe);
                J.SetPos(pipe, px, y + 0.9f, tz - 0.3f);
                for (int k = 0; k < 4; k++) { float x = px - 1.2f + k * 0.8f; water.Add(kit.Cyl(0.025f, 0.025f, 0.25f, kit.Mat("metal"), new At { x = x, y = y + 0.65f, z = tz - 0.18f }, 8), kit.Box(0.08f, 0.06f, 0.12f, kit.Mat("paint:#1f5fa8"), new At { x = x, y = y + 0.92f, z = tz - 0.25f })); }
                water.Add(kit.Box(3.4f, 0.1f, 0.6f, kit.Mat("paint:#5a6a74"), new At { x = px, y = y + 0.25f, z = tz - 0.2f }), kit.Box(3.4f, 0.25f, 0.08f, steel, new At { x = px, y = y, z = tz + 0.08f }));
                // tent (몽골텐트) over the taps, valance text
                float T = 5, tx = px, tzc = tz - 0.8f;
                foreach (var (dx, dz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }) water.Add(kit.Box(0.06f, 2.4f, 0.06f, kit.Mat("metal"), new At { x = tx + dx * T / 2, y = y, z = tzc + dz * T / 2 }));
                var roof = TownBKit.Mesh(Geo.Cone(T * 0.72f, 1.3f, 4, 1, true), kit.Mat("paint:#f4f4ee", new MatSpec { roughness = 0.8f, side = "double" }), cast: true); J.SetYaw(roof, Mathf.PI / 4); J.SetPos(roof, tx, y + 3.05f, tzc); water.Add(roof);
                var val = kit.Sign(new[] { "비상급수 · 연무동" }, new SignOpts { w = T, h = 0.32f, bg = "#f4f4ee", fg = "#1f5fa8", single = true });
                foreach (var (ry, dx, dz) in new[] { (Mathf.PI, 0, -1), (0f, 0, 1), (Mathf.PI / 2, 1, 0), (-Mathf.PI / 2, -1, 0) }) { var v = TownBKit.Clone(val); J.SetPos(v, tx + dx * T / 2 * 1.002f, y + 2.24f, tzc + dz * T / 2 * 1.002f); J.SetYaw(v, ry); water.Add(v); }
                TownBKit.Drop(val); // only its clones are in the scene
                // table with bottles and bags, a notice
                float tbx = px - 4.2f, tbz = tz + 0.4f;
                water.Add(kit.Box(1.8f, 0.05f, 0.7f, white, new At { x = tbx, y = y + 0.72f, z = tbz }));
                foreach (var dx in new[] { -0.8f, 0.8f }) water.Add(kit.Box(0.05f, 0.72f, 0.6f, dark, new At { x = tbx + dx, y = y, z = tbz }));
                var bottles = new List<Xform>(); for (int k = 0; k < 14; k++) bottles.Add(new Xform { x = tbx - 0.75f + (k % 7) * 0.24f, y = y + 0.77f, z = tbz - 0.12f + (k / 7) * 0.24f });
                water.Add(kit.Instanced(Geo.Cylinder(0.055f, 0.06f, 0.32f, 10).Translate(0, 0.16f, 0), kit.Mat("paint:#9fd0ea", new MatSpec { roughness = 0.15f, transparent = true, opacity = 0.75f }), bottles, cast: false));
                var nt = kit.Sign(new[] { "비상급수 운영", "09:30 – 17:00", "현장 안내에 따라 주세요" }, new SignOpts { w = 0.7f, h = 0.9f, bg = "#ffffff", fg = "#1f4e8c", single = true }); J.SetPos(nt, tbx + 1.4f, y + 1.2f, tbz + 0.02f); water.Add(nt, kit.Box(0.05f, 0.75f, 0.05f, dark, new At { x = tbx + 1.4f, y = y, z = tbz - 0.02f }));
                // queue lane: floor marks every metre, stanchions with belts both sides
                var marks = new List<Xform>(); for (int k = 0; k < 10; k++) marks.Add(new Xform { x = WATER_QUEUE.x + k * 0.9f, y = y + 0.006f, z = WATER_QUEUE.z });
                water.Add(kit.Instanced(Geo.Plane(0.5f, 0.12f).RotateX(-Mathf.PI / 2), kit.Mat("paint:#f2c230", new MatSpec { polygonOffset = true, polygonOffsetFactor = -2 }), marks, cast: false));
                var belt = kit.Mat("paint:#1f5fa8", new MatSpec { roughness = 0.6f, side = "double" });
                const double QX = -78.6; // WATER.queue.x in doubles: the stanchion loop count must match JS float steps
                foreach (var z in new[] { WATER_QUEUE.z - 0.75f, WATER_QUEUE.z + 0.75f })
                {
                    for (double x = QX + 0.6; x <= QX + 8.7; x += 2.7) water.Add(kit.Cyl(0.03f, 0.03f, 0.95f, kit.Mat("metal"), new At { x = (float)x, y = y, z = z }, 8), kit.Cyl(0.14f, 0.14f, 0.03f, dark, new At { x = (float)x, y = y, z = z }, 12));
                    var b = TownBKit.Mesh(Geo.Plane(8.1f, 0.06f), belt); J.SetPos(b, WATER_QUEUE.x + 4.65f, y + 0.9f, z); water.Add(b);
                }
            }
            var waterSolids = new List<Solid>
            {
                new Solid(px - 2.6f, px + 2.6f, WATER_TAP_Z - 2.3f, WATER_TAP_Z + 0.15f, -1, 3, "waterTank") { off = true },
                new Solid(px - 5.2f, px - 3.2f, WATER_TAP_Z + 0.02f, WATER_TAP_Z + 0.8f, -1, 1, "waterTable") { off = true },
                new Solid(WATER_QUEUE.x + 0.5f, WATER_QUEUE.x + 8.8f, WATER_QUEUE.z - 0.8f, WATER_QUEUE.z - 0.7f, -1, 1.2f, "queueBelt") { off = true },
                new Solid(WATER_QUEUE.x + 0.5f, WATER_QUEUE.x + 8.8f, WATER_QUEUE.z + 0.7f, WATER_QUEUE.z + 0.8f, -1, 1.2f, "queueBelt") { off = true },
            };
            solids.AddRange(waterSolids);
            kit.Merge(water);
            void setWater(bool on) { water.SetActive(on); foreach (var s in waterSolids) s.off = !on; }

            // ================= buses + assembly reception =================
            var buses = J.Group("buses", group); buses.SetActive(false);
            var busSolids = new List<Solid>();
            var dest = kit.Sign(new[] { "임시 대피 버스" }, new SignOpts { w = 1.9f, h = 0.28f, bg = "#120b04", fg = "#ffae2a", single = true, emissive = 1 });
            kit.Lit("signs", TownBKit.MatOf(dest), 1.6f);
            var sideSign = kit.Sign(new[] { "임시 대피 버스", "연무동 → 지정 대피 시설" }, new SignOpts { w = 1.6f, h = 0.5f, bg = "#ffffff", fg = "#c42b25", single = true });
            for (int i = 0; i < BUS.Count; i++)
            {
                var b = BUS[i];
                float y = hy(b.x, b.z); var g = J.Group("bus", buses); J.SetPos(g, b.x, y, b.z);
                var body = kit.Mat(i != 0 ? "paint:#2f8a4a" : "paint:#1f5fa8", new MatSpec { roughness = 0.35f, metalness = 0.3f });
                g.Add(kit.RBox(2.5f, 1.3f, 11, 0.12f, white, new At { y = 0.35f }), kit.Box(2.46f, 1.06f, 10.7f, kit.Mat("darkGlass"), new At { y = 1.62f }), kit.RBox(2.5f, 0.48f, 11, 0.15f, white, new At { y = 2.62f }));
                g.Add(kit.Box(2.52f, 0.2f, 11.02f, body, new At { y = 1.32f }), kit.Box(2.52f, 0.12f, 11.02f, body, new At { y = 2.6f }));
                g.Add(kit.Box(0.04f, 2.2f, 1.1f, kit.Mat("paint:#202426"), new At { x = 1.25f, y = 0.4f, z = -4.3f }));
                foreach (var z in new[] { -3.6f, 3.4f }) foreach (var s in new[] { -1, 1 }) { var w = TownBKit.Mesh(Geo.Cylinder(0.5f, 0.5f, 0.3f, 18).RotateZ(Mathf.PI / 2), kit.Mat("rubber"), cast: true); J.SetPos(w, s * 1.1f, 0.5f, z); g.Add(w); }
                var fd = TownBKit.Clone(dest); J.SetPos(fd, 0, 2.88f, -5.51f); J.SetYaw(fd, Mathf.PI); g.Add(fd);
                var sd = TownBKit.Clone(sideSign); J.SetPos(sd, 1.27f, 2.0f, -2.2f); J.SetYaw(sd, Mathf.PI / 2); g.Add(sd);
                foreach (var s in new[] { -0.85f, 0.85f }) g.Add(kit.Box(0.3f, 0.12f, 0.04f, kit.Mat("emit:#fff3d6"), new At { x = s, y = 0.75f, z = -5.52f, cast = false }));
                busSolids.Add(new Solid(b.x - 1.35f, b.x + 1.35f, b.z - 5.6f, b.z + 5.6f, -1, 3.3f, "bus") { off = true });
            }
            TownBKit.Drop(dest); TownBKit.Drop(sideSign); // only their clones are in the scene
            {
                float ax = -59, az = -28.6f;
                var st = kit.Sign(new[] { "임시 집결지", "주민 이동 지원 접수" }, new SignOpts { w = 1.3f, h = 0.7f, bg = "#1f4e8c", single = false }); J.SetPos(st, ax, Y + 2.2f, az); J.SetYaw(st, Mathf.PI / 2); buses.Add(st, kit.Cyl(0.04f, 0.04f, 2.6f, steel, new At { x = ax, y = Y, z = az + 0.05f }, 8));
                float tx = -59.8f, tz = -33;
                foreach (var (dx, dz) in new[] { (-1.5f, -1.5f), (1.5f, -1.5f), (1.5f, 1.5f), (-1.5f, 1.5f) }) buses.Add(kit.Box(0.05f, 2.3f, 0.05f, kit.Mat("metal"), new At { x = tx + dx, y = Y, z = tz + dz }));
                var roof = TownBKit.Mesh(Geo.Cone(2.15f, 0.9f, 4, 1, true), kit.Mat("paint:#f4f4ee", new MatSpec { roughness = 0.8f, side = "double" })); J.SetYaw(roof, Mathf.PI / 4); J.SetPos(roof, tx, Y + 2.75f, tz); buses.Add(roof);
                buses.Add(kit.Box(0.7f, 0.05f, 1.8f, white, new At { x = tx - 0.4f, y = Y + 0.72f, z = tz }));
                foreach (var dz in new[] { -0.8f, 0.8f }) buses.Add(kit.Box(0.6f, 0.72f, 0.05f, dark, new At { x = tx - 0.4f, y = Y, z = tz + dz }));
                busSolids.Add(new Solid(ax - 0.12f, ax + 0.12f, az - 0.12f, az + 0.12f, -1, 3, "assemblySign") { off = true });
                busSolids.Add(new Solid(tx - 0.8f, tx, tz - 0.95f, tz + 0.95f, -1, 1, "assemblyTable") { off = true });
            }
            solids.AddRange(busSolids);
            kit.Merge(buses);
            void setBuses(bool on) { buses.SetActive(on); foreach (var s in busSolids) s.off = !on; }

            // ================= per-frame =================
            float gt = 0;
            void update(float dt, float t, FrameCtx ctx)
            {
                bool blink = (ctx != null && ctx.reduced) || Math.Floor(t * 1.6) % 2 == 0; // steady when reduced motion is on
                Mat.SetEmissiveIntensity(flashMat, active.Count > 0 && blink ? 4 : 0);
                gt += dt;
                var pl = ctx?.player;
                for (int k = 0; k < guides.Count; k++)
                {
                    var p = guides[k];
                    if (p == null || !p.Group.activeSelf) continue;
                    var at = J.Pos(p.Group);
                    float dx = pl != null ? pl.X - at.x : 99, dz = pl != null ? pl.Z - at.z : 99; bool near = dx * dx + dz * dz < 64;
                    if (near) p.LookAt(pl.X, pl.Z); else p.LookAt(null, null);
                    float phase = (gt + k * 2.3f) % 9;
                    p.SetAnim(near ? (phase < 3 ? "point" : "talk") : phase < 3 ? "wave" : "idle");
                    if (dx * dx + dz * dz < 6400) p.Update(dt);
                }
            }

            var closurePoints = TownMap.CLOSURES.Keys.Select(cid =>
            {
                var gp = TownPlan.CLOSURE_GUIDES[cid];
                return new Point { id = $"closure_{cid}", label = $"안내 요원 · {TownMap.CLOSURES[cid].label}", x = gp.x, y = hy(gp.x, gp.z), z = gp.z, r = 2.4f, kind = "npc", enabled = false };
            }).ToList();
            return new TownEventsResult
            {
                group = group, solids = solids, setClosures = setClosures, setWater = setWater, setBuses = setBuses, update = update,
                closurePoints = closurePoints, guides = guides, active = () => active,
            };
        }
    }
}
