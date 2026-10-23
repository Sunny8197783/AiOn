// Ports levels/apartment.js: 한빛아파트 103동 — ground-floor lobby with 관리사무소 (박 소장), the stairwell, the
// 3rd-floor corridor (301-304, 옥내소화전 + 분말소화기, the hallway-fire spot near 304), 302호 (정민's flat) and the view
// out of the windows. Lighting: room lamps, motion-sensor lamps (센서등) and battery emergency lamps share a small pool
// of point lights given to the fixtures nearest the player, so the light count never changes. Green exit signs are in
// the kit's 'emergency' group and stay lit.
// States: wallet power stock bag radio hallFire smoke seoyeonDoor candles windowView.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ApartmentParts;

namespace Gyeongbo.Levels
{
    public static class Apartment
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("apartment", BuildApartment);

        static readonly Dictionary<string, string[]> STR_STATES = new Dictionary<string, string[]>
        {
            ["wallet"] = new[] { "home", "taken" },
            ["power"] = new[] { "on", "off" },
            ["bag"] = new[] { "none", "empty", "packed" },
            ["radio"] = new[] { "none", "off", "on" },
            ["hallFire"] = new[] { "none", "small", "large", "out" },
            ["seoyeonDoor"] = new[] { "closed", "open" },
            ["candles"] = new[] { "none", "lit" },
            ["windowView"] = new[] { "day", "dusk", "night", "blackout" },
        };
        static readonly Dictionary<string, int[]> STOCK = new Dictionary<string, int[]>
        {
            ["water_2l"] = new[] { 0, 2, 6, 12, 24, 40 }, ["water_500"] = new[] { 0, 4, 12 }, ["rice_instant"] = new[] { 0, 2, 6, 12 }, ["canned"] = new[] { 0, 1, 4, 12 }, ["ramen"] = new[] { 0, 5, 10, 20 },
            ["batteries_aa"] = new[] { 0, 1, 3 }, ["candles"] = new[] { 0, 1, 2 }, ["gas_stove"] = new[] { 0, 1 }, ["butane"] = new[] { 0, 1, 3 }, ["first_aid"] = new[] { 0, 1 }, ["tissue"] = new[] { 0, 2, 6 }, ["blanket"] = new[] { 0, 2 }, ["wipes"] = new[] { 0, 1, 2 }, ["water_jug"] = new[] { 0, 1 },
        };

        // STATES in the JS key order (documentation for the dev panel; validation uses STR_STATES).
        static Dictionary<string, object> States() => new Dictionary<string, object>
        {
            ["wallet"] = STR_STATES["wallet"], ["power"] = STR_STATES["power"], ["stock"] = STOCK, ["bag"] = STR_STATES["bag"], ["radio"] = STR_STATES["radio"],
            ["hallFire"] = STR_STATES["hallFire"], ["smoke"] = new[] { 0, 0.25, 0.5, 0.75, 1 }, ["seoyeonDoor"] = STR_STATES["seoyeonDoor"], ["candles"] = STR_STATES["candles"], ["windowView"] = STR_STATES["windowView"],
        };

        public static Task<Level> BuildApartment(Kit kit, IPeople people, IFx fx)
        {
            try { return Task.FromResult(Build(kit, people, fx)); }
            catch (Exception e) { return Task.FromException<Level>(e); }
        }

        static float Hypot(float a, float b) => Mathf.Sqrt(a * a + b * b);
        static float Hypot(float a, float b, float c) => Mathf.Sqrt(a * a + b * b + c * c);

        static Level Build(Kit kit, IPeople people, IFx fx)
        {
            var B = CreateBuilder(kit); var H = new AptHandles();
            B.fixtures.Clear(); // room lamps { x, y, z, color, intensity, distance } (on with power)
            var group = J.Group("level-apartment");
            var dyn = J.Group("apt-dynamic");
            var areas = new[] { "lobby", "stairs", "corridor", "home" }.Select(n => { var a = J.Group("apt-" + n); group.Add(a); return a; }).ToArray();
            group.Add(dyn);
            ApartmentCore.BuildLobby(B, areas[0]); ApartmentCore.BuildElevator(B, areas[0], 0, H);
            ApartmentCore.BuildStairs(B, areas[1], dyn);
            // battery emergency lamp (비상조명등) on the lobby wall beside the stair door; lit only in a blackout
            areas[0].Add(B.Box(0.32, 0.12, 0.08, B.M.white, A(x: 3.2, y: 2.3, z: -0.14)));
            foreach (var dx in new[] { -0.1, 0.1 }) areas[0].Add(B.Cyl(0.035, 0.03, 0.05, B.emLampMat, A(x: 3.2 + dx, y: 2.25, z: -0.16, cast: false)));
            B.sensors.Add(new Sensor { x = 3.2f, y = 2.2f, z = -0.45f, kind = "backup" });
            areas[1].Add(B.Slab(3.4, 0.2, 3.7, B.M.concrete, A(x: 5, y: -0.21, z: 3.25))); // stairwell ground floor under the flights
            ApartmentCorridor.BuildCorridor(B, areas[2], dyn, H);
            // live doors: merge each moving leaf (pivot) and gather their static frames into one merged group
            var frames = J.Group("apt-door-frames");
            foreach (var o in dyn.transform.Cast<Transform>().ToList()) if (o.GetComponent<MeshRenderer>() != null) frames.Add(o.gameObject);
            dyn.Add(frames); kit.Merge(frames);
            ApartmentHome.BuildHome(B, areas[3], H); // after the corridor: it fits the inner lock onto H.door302's leaf
            foreach (var D in B.doors.Concat(new[] { H.door303 })) kit.Merge(D.pivot);
            kit.Merge(H.fire.normal); kit.Merge(H.fire.scorched);
            var props = ApartmentProps.BuildProps(B, areas[3], dyn); var stock = ApartmentStock.BuildStock(B, areas[3], dyn); var view = ApartmentView.BuildView(B);
            group.Add(view.group);
            B.fixtures.Add(new Fixture { x = 4, y = 2.3f, z = -3.3f, color = 0xfff3e2, intensity = 4.5f, distance = 11 });
            B.fixtures.Add(new Fixture { x = 10.75f, y = 2.25f, z = -4, color = 0xfff3e2, intensity = 2.5f, distance = 6 });
            foreach (var a in areas) kit.Merge(a);

            // ---- fixtures + light pool ----
            var fixtures = B.fixtures.Select(f => { f.kind = "room"; return f; }).Concat(B.sensors.Select(s => s.kind == "backup"
                ? new Fixture { x = s.x, y = s.y, z = s.z, color = 0xe6eeff, intensity = 2.4f, distance = 7, kind = "backup" }
                : new Fixture { x = s.x, y = s.y - 0.25f, z = s.z, color = 0xfff1dc, intensity = 3.0f, distance = 6.5f, kind = "sensor", hold = 0, ceil = s.y })).ToList();
            // Zones keep pooled lights from shining through closed walls: a fixture lights the player's zone, or a
            // neighbouring zone while the door between them is open.
            string zoneOf(float x, float y, float z) => x > 3.3f && x < 6.7f && z > 0 ? "stairs" : y > 4 ? (z > 0.05f && x < 0.35f ? "home" : "corridor") : "lobby";
            AptDoor stairDoor(float y) => B.doors.FirstOrDefault(D => Math.Abs(D.x - 5) < 0.1f && Math.Abs(D.y - y) < 0.1f);
            var LINKS = new[] { ("home", "corridor", H.door302), ("corridor", "stairs", stairDoor(F3)), ("stairs", "lobby", stairDoor(0)) };
            bool linked(string a, string b) => LINKS.Any(L => ((L.Item1 == a && L.Item2 == b) || (L.Item1 == b && L.Item2 == a)) && L.Item3 != null && L.Item3.k > 0.1f);
            foreach (var f in fixtures) { f.w = 0; f.on = false; f.zone = zoneOf(f.x, f.y, f.z); }
            int POOL = kit.Quality == "low" ? 3 : 5;
            var pool = Enumerable.Range(0, POOL).Select(_ => { var l = kit.PointLight(J.Hex(0xffffff), 0, 8, 2); l.shadows = LightShadows.None; dyn.Add(l); return l; }).ToArray();
            // sensor lamp diffusers: an "off" plastic dome for every lamp, a glowing dome shown over it while lit
            var sensors = fixtures.Where(f => f.kind == "sensor").ToList();
            var dome = Geo.Sphere(0.12f, 16, 6, 0, Mathf.PI * 2, 0, Mathf.PI / 2).Scale(1, 0.45f, 1).RotateX(Mathf.PI);
            var at = sensors.Select(f => X(x: f.x, y: f.ceil, z: f.z)).ToList();
            dyn.Add(kit.Instanced(dome, kit.Mat("paint:#f3f2ee", new MatSpec { roughness = 0.35f }), at, cast: false));
            var glowDomes = kit.Instanced(dome, Mat.Unlit(new MatSpec { color = Lin(3.2, 3.0, 2.6) }), at.Select(p => X(x: p.x, y: p.y, z: p.z, s: 1.03)).ToList(), false, false);
            dyn.Add(glowDomes);
            // setMatrixAt(i, on ? mOn[i] : mOff): instance i shown or hidden
            void glowAt(int i, bool on) => glowDomes.transform.GetChild(i).gameObject.SetActive(on);
            for (int i = 0; i < sensors.Count; i++) glowAt(i, false);

            bool power = true;
            void setPower(string v)
            {
                power = v != "off";
                foreach (var p in B.powered) AptBuilder.SetI(p.obj, power ? p.@base : 0);
                foreach (var p in B.backup) AptBuilder.SetI(p.obj, power ? 0 : p.@base);
            }
            void stepLights(float dt, float px, float py, float pz)
            {
                float head = py + 1.6f; string pzone = zoneOf(px, py + 0.5f, pz);
                for (int i = 0; i < sensors.Count; i++)
                {
                    var f = sensors[i];
                    if (power && f.zone == pzone && Hypot(px - f.x, pz - f.z) < 3.4f && Math.Abs(head - f.y) < 1.8f) f.hold = 9;
                    f.hold = Math.Max(0, f.hold - dt);
                    bool on = power && f.hold > 0;
                    if (on != f.on) { f.on = on; glowAt(i, on); }
                }
                foreach (var f in fixtures)
                {
                    if (f.kind != "sensor") f.on = f.kind == "backup" ? !power : power;
                    float d = Hypot(px - f.x, (head - f.y) * 1.6f, pz - f.z);
                    f.d = !f.on ? float.PositiveInfinity : f.zone == pzone ? d : linked(f.zone, pzone) ? d * 2 + 4 : float.PositiveInfinity;
                    f.best = false;
                }
                foreach (var f in fixtures.Where(f => f.on && f.d < 20).OrderBy(f => f.d).Take(POOL - 1)) f.best = true;
                foreach (var f in fixtures) f.w += ((f.best ? 1 : 0) - f.w) * Math.Min(1, dt * 4);
                var lit = fixtures.Where(f => f.w > 0.01f).OrderByDescending(f => f.w).ToList();
                for (int i = 0; i < pool.Length; i++)
                {
                    var l = pool[i]; var f = i < lit.Count ? lit[i] : null;
                    if (f == null) { l.intensity = 0; continue; }
                    // As in the JS: its trailing comment swallows the distance/intensity lines, so pooled lights stay at 0.
                    J.SetPos(l.gameObject, f.x, f.y - 0.4f, f.z); l.color = J.Hex(f.color); // a point light this close to the ceiling would blow it out l.distance = f.distance; l.intensity = f.intensity * f.w;
                }
            }

            // ---- fire + smoke (created now so the scene's light count stays fixed) ----
            var FS = ApartmentCorridor.FIRE_SPOT;
            var fire = fx.Fire(new FireOptions { x = FS.x - 0.05f, y = F3 + 0.02f, z = FS.z + 0.05f, scale = 1.2f, level = 0 });
            float sz = FS.z - 0.4f;
            var smoke = fx.Smoke(new SmokeOptions { x = FS.x, y = F3 + 0.5f, z = sz, ceiling = 1.88f, spread = 14, density = 0, bounds = new RectXZ(-15.7f - FS.x, 21.7f - FS.x, -1.85f - sz, -0.15f - sz) });
            var haze = fx.Smoke(new SmokeOptions { x = -1.4f, y = FL + 0.2f, z = 1.6f, ceiling = 2.08f, spread = 5, density = 0, count = 40, color = J.Hex(0x57524d), bounds = new RectXZ(-6.1f, 1.6f, -1.5f, 6.3f) });
            dyn.Add(fire.Group, smoke.Group, haze.Group);
            void setFire(string v)
            {
                var F = H.fire;
                fire.SetLevel(v == "small" ? 0.4f : v == "large" ? 1 : 0);
                F.normal.SetActive(v == "none" || v == "small"); F.scorched.SetActive(v == "large" || v == "out");
                F.soot.SetActive(v != "none"); F.powder.SetActive(v == "out");
                H.firePoint.label = v == "none" ? "304호 앞 복도" : v == "out" ? "꺼진 불 자리" : "304호 앞 불";
            }

            // ---- NPCs ----
            var manager = people.CreatePerson("manager", 31);
            J.SetPos(manager.Group, 10.4f, 0, -4.1f); J.SetYaw(manager.Group, Mathf.PI / 2); manager.SetAnim("sitChair"); dyn.Add(manager.Group);
            var seoyeon = people.CreatePerson("seoyeon", 7); var spot = H.seoyeonSpot;
            J.SetPos(seoyeon.Group, spot.x, spot.y ?? F3, spot.z); J.SetYaw(seoyeon.Group, spot.yaw ?? 0);
            seoyeon.SetAnim("idle"); seoyeon.Group.SetActive(false); dyn.Add(seoyeon.Group);
            float door303 = 0;

            // ---- spawns, anchors ----
            B.spawns["lobby"] = new Spawn(4, 0, -5.4f, Mathf.PI); B.spawns["corridor3F"] = new Spawn(-1.275f, F3, -1.0f, -Mathf.PI / 2);
            B.spawns["stairs3F"] = new Spawn(5.0f, F3, 0.9f, Mathf.PI); B.spawns["stairs1F"] = new Spawn(5.0f, 0, 0.9f, Mathf.PI);
            B.anchors["lobbyView"] = new Anchor { x = 8.4f, y = 2.2f, z = -6.1f, target = new Vector3(1.0f, 1.0f, -1.0f) };
            B.anchors["corridorView"] = new Anchor { x = -14.5f, y = F3 + 2.0f, z = -1.6f, target = new Vector3(10, F3 + 0.9f, -0.4f) };
            B.anchors["fireView"] = new Anchor { x = 14.5f, y = F3 + 1.7f, z = -1.6f, target = new Vector3(FS.x, F3 + 0.4f, FS.z) };
            B.anchors["stairWell"] = new Anchor { x = 5.0f, y = F3 + 2.2f, z = 4.6f, target = new Vector3(5.0f, 1.0f, 1.0f) };

            // S holds normalized values (strings, the smoke number, the stock map) so a loaded JSON token compares like the JS value.
            var S = new Dictionary<string, object> { ["wallet"] = "home", ["power"] = "on", ["stock"] = new Dictionary<string, int>(), ["bag"] = "empty", ["radio"] = "none", ["hallFire"] = "none", ["smoke"] = 0f, ["seoyeonDoor"] = "closed", ["candles"] = "none", ["windowView"] = "day" };
            var apply = new Dictionary<string, Action<object>>
            {
                ["power"] = v => setPower((string)v), ["stock"] = v => stock.setStock((Dictionary<string, int>)v), ["bag"] = v => props.setBag((string)v), ["radio"] = v => props.setRadio((string)v), ["hallFire"] = v => setFire((string)v),
                ["smoke"] = v => { float d = (float)v; smoke.SetDensity(d); haze.SetDensity(d * 0.5f); }, ["seoyeonDoor"] = v => seoyeon.Group.SetActive((string)v == "open"),
                ["candles"] = v => props.setCandle((string)v), ["windowView"] = v => view.setView((string)v), ["wallet"] = v => props.setWallet((string)v),
            };
            void setState(string key, object value)
            {
                if (key == null || !apply.TryGetValue(key, out var fn)) { Debug.LogWarning($"[apartment] unknown state \"{key}\"; known: {string.Join(", ", apply.Keys)}"); return; }
                object v;
                if (key == "smoke") { float n = Sv.Plain(value) is bool b ? (b ? 1 : 0) : Sv.Num(value); v = float.IsNaN(n) ? 0f : Math.Min(1f, Math.Max(0f, n)); } // Number(value) || 0
                else if (key == "stock")
                {
                    var p = Sv.Plain(value);
                    if (p == null || p is IConvertible) { Debug.LogError($"[apartment] stock needs {{ itemId: count }} {value}"); return; }
                    v = Sv.IntMap(value);
                }
                else
                {
                    var s = Sv.Str(value);
                    if (s == null || !STR_STATES[key].Contains(s)) { Debug.LogError($"[apartment] bad value for {key}: {value} allowed: {string.Join(", ", STR_STATES[key])}"); return; }
                    v = s;
                }
                S[key] = v; fn(v);
            }
            foreach (var kv in S.ToList()) apply[kv.Key](kv.Value);

            void update(float dt, float t, FrameCtx ctx)
            {
                var pl = ctx?.player; float px = pl != null ? pl.X : 0, py = pl != null ? pl.Y : 0, pz = pl != null ? pl.Z : 0; var cam = ctx?.camera;
                foreach (var D in B.doors) // auto doors: open while the player is close on the same floor
                {
                    float want = Hypot(px - D.x, pz - D.z) < D.auto && Math.Abs(py - D.y) < 1.2f ? 1 : 0;
                    if (D.k != want) D.set(D.k + Math.Sign(want - D.k) * Math.Min(Math.Abs(want - D.k), dt * 1.8f));
                }
                float want303 = (string)S["seoyeonDoor"] == "open" ? 0.72f : 0;
                if (door303 != want303) { door303 += Math.Sign(want303 - door303) * Math.Min(Math.Abs(want303 - door303), dt * 1.2f); H.door303.set(door303); }
                stepLights(dt, px, py, pz);
                if (power) foreach (var tick in B.ticks) tick(dt, t, S);
                else if (H.candleWindow != null) Mat.SetEmissiveIntensity(H.candleWindow, 0.1f + 0.03f * Mathf.Sin(t * 7.1f) + 0.02f * Mathf.Sin(t * 12.7f)); // a neighbour's candle in 301
                props.update(dt, t); view.update(dt);
                fire.Update(dt, cam); smoke.Update(dt, cam); haze.Update(dt, cam);
                float dm = Hypot(px - 10.4f, pz + 4.1f);
                if (py < 2 && dm < 25) // 박 소장 looks up when the player comes to the window
                {
                    if (dm < 4.5f) manager.LookAt(px, pz); else manager.LookAt(null, null);
                    manager.Update(dt);
                }
                if (seoyeon.Group.activeSelf)
                {
                    if (Hypot(px - spot.x, pz - spot.z) < 4 && Math.Abs(py - F3) < 1) seoyeon.LookAt(px, pz); else seoyeon.LookAt(null, null);
                    seoyeon.Update(dt);
                }
            }

            return new Level
            {
                id = "apartment", group = group, env = "interior",
                walk = B.walk, solids = B.solids, points = B.points, spawns = B.spawns,
                npcs = new Dictionary<string, IPerson> { ["manager"] = manager, ["seoyeon"] = seoyeon }, states = States(), setState = setState, update = update, anchors = B.anchors,
            };
        }
    }
}
