// Ports levels/shelter.js: the 연무 생활문화센터 basement car park (B1) in use as a civil-defence shelter.
// Built from ShelterShell (structure, the four ways in), ShelterServices (lights, pipes, safety kit), ShelterCamp
// (mats, reception, board, radio, supplies, parked cars) and ShelterPeople (evacuees, staff, 서연).
// Local frame: x east, z south, floor y = 0, yaw 0 faces north (-z). Town footprint: local = town + (91, 0, -31).
// States: crowd 'arriving'|'settled'|'leaving', phase 'alert'|'calm', power 'grid'|'emergency' (emergency: circuit B
// tubes and area lights off, battery emergency lights on, jet fans spin down, lift display dark),
// fire 'none'|'large' (a camping-burner fire on stacked cartons in the north-west corner: flames reach the cable
// tray and sprinkler main, a smoke layer spreads under the slab, nearby evacuees have moved away, a solid blocks it),
// boardText string[]|null (lines the director pins on the notice board; no text is invented here),
// ways string[] (open entrances among stairs|ramp|access|east, default all; arrivals and leavers use only these).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ShelterCamp;

namespace Gyeongbo.Levels
{
    public static class Shelter
    {
        public static readonly XZ FIRE = new XZ(-18.6f, -9.2f);
        const float PI = Mathf.PI;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("shelter", BuildShelter);

        static readonly Dictionary<string, string[]> STR_STATES = new Dictionary<string, string[]>
        {
            ["crowd"] = new[] { "arriving", "settled", "leaving" }, ["phase"] = new[] { "alert", "calm" }, ["power"] = new[] { "grid", "emergency" }, ["fire"] = new[] { "none", "large" },
        };
        static Dictionary<string, object> States() => new Dictionary<string, object>
        {
            ["crowd"] = STR_STATES["crowd"], ["phase"] = STR_STATES["phase"], ["power"] = STR_STATES["power"], ["fire"] = STR_STATES["fire"],
            ["boardText"] = new object[] { null, new[] { "연무 생활문화센터 지하주차장 · 민방위 대피소" } }, ["ways"] = ShelterPeople.ENTRANCES,   // ways: any non-empty subset
        };

        // Faulty tube: steady, with a short starter stutter every 9 s (steady when reduceMotion).
        static float Flicker(float t) => t % 9 < 1.1f ? (Mathf.Sin(t * 71) * Mathf.Sin(t * 23) > 0 ? 0.95f : 0.08f) : 0.9f;

        static Texture2D SootTex(Kit kit) => kit.Memo("sh:soot", () => kit.Texture(kit.Canvas(256, 256, (g, w, h) =>
        {
            var r = Kit.Rng(91);
            for (int i = 0; i < 14; i++)
            {
                float x = 128 + (r() - 0.5f) * 60, y = 128 + (r() - 0.5f) * 60, rad = 40 + r() * 45;
                var gr = new PaintGradient(x, y, 0, x, y, rad);
                gr.addColorStop(0, "rgba(14,11,9,0.5)"); gr.addColorStop(1, "rgba(14,11,9,0)");
                g.fillStyle = gr; g.fillRect(0, 0, 256, 256);
            }
        })));

        public static Task<Level> BuildShelter(Kit kit, IPeople people, IFx fx)
        {
            try { return Task.FromResult(Build(kit, people, fx)); }
            catch (Exception e) { return Task.FromException<Level>(e); }
        }

        static At at(float x = 0, float y = 0, float z = 0, float ry = 0) => new At { x = x, y = y, z = z, ry = ry };

        static Level Build(Kit kit, IPeople people, IFx fx)
        {
            var T = ShelterTextures.Build(kit);
            var ROOM = ShelterShell.ROOM;
            var group = J.Group("level-shelter");
            var shell = ShelterShell.BuildShell(kit, T); var services = ShelterServices.BuildServices(kit, T, shell); var camp = BuildCamp(kit, T);
            foreach (var g in new[] { shell.group, services.group, camp.group }) group.Add(kit.Merge(g));
            group.Add(services.live, camp.live);
            var crowd = ShelterPeople.BuildPeople(kit, people, camp.seats, shell.walk);
            group.Add(crowd.group);

            // ---- fire corner: cartons stacked beside a camping burner with a pot; only shown while the fire burns ----
            var fireProps = J.Group("shelter-fire-props"); fireProps.SetActive(false); group.Add(fireProps);
            var ember = Mat.Lit(new MatSpec { color = J.Hex(0x2a1d14), roughness = 1, emissive = J.Hex(0xff4a12), emissiveIntensity = 0 });
            Material @char = kit.Mat("paint:#1c1814", new MatSpec { roughness = 1 }), card = kit.Mat("paint:#a7835a", new MatSpec { roughness = 0.95f });
            foreach (var (dx, dz, y, w, h, d, ry, m) in new[] { (0f, 0f, 0f, 0.55f, 0.42f, 0.42f, 0.1f, ember), (0.05f, 0.02f, 0.42f, 0.48f, 0.36f, 0.38f, -0.15f, ember),
                (-0.55f, -0.1f, 0f, 0.5f, 0.4f, 0.4f, 0.3f, @char), (0.2f, -0.55f, 0f, 0.46f, 0.34f, 0.36f, -0.4f, card), (-0.4f, 0.45f, 0f, 0.42f, 0.3f, 0.34f, 0.6f, ember) })
                fireProps.Add(kit.Box(w, h, d, m, at(FIRE.x + dx, y, FIRE.z + dz, ry)));
            float bx = FIRE.x + 0.75f, bz = FIRE.z + 0.55f;
            fireProps.Add(kit.Box(0.34f, 0.1f, 0.27f, kit.Mat("paint:#2d3034", new MatSpec { roughness = 0.5f, metalness = 0.4f }), at(bx, 0, bz, 0.4f)));
            fireProps.Add(kit.Cyl(0.1f, 0.09f, 0.09f, kit.Mat("darkMetal"), at(bx, 0.1f, bz), 16));
            fireProps.Add(kit.Box(0.15f, 0.025f, 0.12f, kit.Mat("paint:#c8321f"), at(bx + 0.3f, 0, bz + 0.2f, 1.1f)));   // ramen packet
            var soot = Mat.Unlit(new MatSpec { map = SootTex(kit), transparent = true, depthWrite = false, polygonOffset = true, polygonOffsetFactor = -2 });
            var ceilSoot = Geo.Obj(Geo.Plane(4.5f, 4.5f), soot, "ceil-soot", false, false); J.SetRot(ceilSoot, PI / 2, 0, 0); J.SetPos(ceilSoot, FIRE.x, ROOM.ceil - 0.005f, FIRE.z);
            var floorSoot = Geo.Obj(Geo.Plane(2.6f, 2.6f), soot, "floor-soot", false, false); J.SetRot(floorSoot, -PI / 2, 0, 0); J.SetPos(floorSoot, FIRE.x, 0.008f, FIRE.z);
            fireProps.Add(ceilSoot, floorSoot);

            // ---- fx: flames on the stack (warm flickering light), a low lick at its base, ceiling smoke, dust motes ----
            var flames = fx.Fire(new FireOptions { x = FIRE.x, y = 0.7f, z = FIRE.z, scale = 2.1f, radius = 0.36f, level = 0 });
            var lick = fx.Fire(new FireOptions { x = FIRE.x - 0.45f, y = 0.02f, z = FIRE.z + 0.4f, scale = 0.85f, light = 0, level = 0 });
            var smoke = fx.Smoke(new SmokeOptions
            {
                x = FIRE.x, y = 0.6f, z = FIRE.z, ceiling = ROOM.ceil - 0.75f, spread = 12, radius = 0.45f, count = 120, density = 0, color = J.Hex(0x2b2724),
                bounds = new RectXZ(ROOM.x0 + 0.4f - FIRE.x, ROOM.x1 - 0.4f - FIRE.x, ROOM.z0 + 0.4f - FIRE.z, ROOM.z1 - 0.4f - FIRE.z),
            });
            var motes = fx.Dust(new DustOptions { box = new Box3 { x0 = -21.5f, x1 = 21.5f, y0 = 0.2f, y1 = 2.8f, z0 = -15.5f, z1 = 15.5f }, count = 420, opacity = 0.22f });
            group.Add(flames.Group, lick.Group, smoke.Group, motes.Group);
            var fireSolid = new Solid(FIRE.x - 1.0f, FIRE.x + 1.2f, FIRE.z - 0.9f, FIRE.z + 1.0f, -1, 3, "fire") { off = true };
            var solids = shell.solids.Concat(camp.solids).ToList(); solids.Add(fireSolid);

            Point P(string id, string label, float x, float z, string kind, float r = 1.6f) => new Point(id, label, x, 0, z, kind, r);
            var spot = SPOT;
            var points = new List<Point>
            {
                P("fromStairs", "출입구 A · 계단", 2.9f, -15.2f, "entrance", 1.3f),
                P("shelterExit", "계단으로 나가기 · 지상", 5, -16.7f, "door", 1.4f),
                P("fromRamp", "출입구 B · 차량 진출입로", 11, 15.0f, "entrance", 1.8f),
                P("fromAccess", "출입구 C · 경사로", -13, -15.2f, "entrance", 1.6f),
                P("reception", "접수대 · 대피자 명부", RECEPTION.x, RECEPTION.z - 0.85f, "counter", 1.7f),
                P("shelterBoard", "대피소 게시판", BOARD.x, BOARD.z + 0.9f, "notice", 1.7f),
                P("shelterRadio", "공용 라디오", RADIO.x, RADIO.z - 0.75f, "radio", 1.6f),
                P("spot", "빈 자리 · 돗자리", spot.x, spot.z, "seat", 1.6f),
            };
            var spawns = new Dictionary<string, Spawn>
            {
                ["stairs"] = new Spawn(5, 0, -14.6f, PI),            // stair A foot, facing into the hall
                ["ramp"] = new Spawn(11, 0, 14.4f, 0),               // bottom of car ramp B
                ["access"] = new Spawn(-13, 0, -14.6f, PI),          // bottom of accessible ramp C
                ["east"] = new Spawn(23.6f, 0, 0, PI / 2),           // lift hall behind the east fire doors
                ["spot"] = new Spawn(spot.x, 0, spot.z + 1.4f, 0.45f), // at the free mat, the fire corner in view
            };
            var anchors = new Dictionary<string, Anchor>
            {
                ["overview"] = new Anchor { x = 19.5f, y = 2.45f, z = 9.5f }, ["reception"] = new Anchor { x = 4.5f, y = 1.65f, z = -11.8f }, ["stairFoot"] = new Anchor { x = 5, y = 1.6f, z = -13.5f },
                ["spot"] = new Anchor { x = spot.x + 2.6f, y = 1.5f, z = spot.z + 3.2f }, ["fire"] = new Anchor { x = -11.5f, y = 1.7f, z = -5.6f },
            };

            // ---- states ----
            string crowdS = "settled", phase = "calm", power = "grid", fire = "none"; List<string> boardText = null; IList<string> ways = ShelterPeople.ENTRANCES;
            void setState(string key, object v)
            {
                if (key == "boardText")
                {
                    var p = Sv.Plain(v);
                    boardText = p is IEnumerable && !(p is string) ? Sv.StrList(p).Select(s => s ?? "null").ToList() : null;   // v.map(String)
                    Mat.SetMap(camp.boardMat, T.DrawBoard(boardText)); return;
                }
                if (key == "ways")
                {
                    var w = Sv.StrList(v).Distinct().ToList();
                    if (w.Count == 0 || !w.All(x => ShelterPeople.ENTRANCES.Contains(x))) { Debug.LogError($"[shelter] ways must be a non-empty array of {string.Join("|", ShelterPeople.ENTRANCES)} {v}"); return; }
                    ways = w; crowd.setWays(w); return;
                }
                if (key == null || !STR_STATES.TryGetValue(key, out var allowed)) { Debug.LogError($"[shelter] unknown state {key} {v}"); return; }
                if (!(Sv.Plain(v) is string s0) || !allowed.Contains(s0)) { Debug.LogError($"[shelter] {key} must be {string.Join("|", allowed)} {v}"); return; }
                if (key == "crowd") { crowdS = s0; crowd.setCrowd(s0); }
                else if (key == "phase") { phase = s0; crowd.setPhase(s0); }
                else if (key == "power") power = s0;
                else if (key == "fire")
                {
                    fire = s0; bool on = s0 == "large";
                    flames.SetLevel(on ? 1 : 0); lick.SetLevel(on ? 0.8f : 0); smoke.SetDensity(on ? 0.9f : 0);   // smoke thins out over seconds
                    fireProps.SetActive(on); fireSolid.off = !on; crowd.setFire(on ? FIRE : (XZ?)null);
                }
            }

            float fan = 14;
            void update(float dt, float t, FrameCtx ctx)
            {
                bool grid = power == "grid", rm = fx != null && fx.ReduceMotion; var tubes = services.tubes;
                float s = Mat.GetEmissiveIntensity(tubes["A"]) / 3.2f;   // the env preset's 'indoor' level (0..1); circuit B and lights follow it
                Mat.SetEmissiveIntensity(tubes["B"], grid ? s * 3.2f : 0);
                Mat.SetEmissiveIntensity(tubes["F"], s * 3.2f * (rm ? 0.55f : Flicker(t)));
                foreach (var L in services.lights) L.light.intensity = L.@base * L.unit * s * (grid || L.circuit == "A" ? 1 : 0);
                Mat.SetEmissiveIntensity(services.emLamp, grid ? 0 : 2.4f);
                foreach (var l in services.emLights) l.light.intensity = grid ? 0 : l.@base * l.unit;
                foreach (var m in shell.lift) Mat.SetEmissiveIntensity(m, grid ? 1.2f : 0);
                fan += ((grid ? 14 : 0) - fan) * Math.Min(1, dt * 0.35f);
                foreach (var f in services.fans) { f.angle += fan * dt; J.SetRot(f.rotor, f.angle, 0, 0); }
                Mat.SetEmissiveIntensity(services.cctvLed, t % 1.4f < 0.18f ? 2.6f : 0.25f);
                Mat.SetEmissiveIntensity(camp.dial, phase == "alert" ? 1.6f : 1.1f);
                J.SetRot(camp.banner, rm ? 0 : Mathf.Sin(t * 0.55f) * 0.02f * (0.3f + fan / 14), 0, 0);   // ventilation draught
                Mat.SetEmissiveIntensity(ember, fire == "large" ? 0.9f + (rm ? 0 : 0.35f * Mathf.Sin(t * 9.1f) * Mathf.Sin(t * 3.7f)) : 0);
                var cam = ctx?.camera;
                flames.Update(dt); lick.Update(dt); smoke.Update(dt, cam); motes.Update(dt, cam);
                crowd.update(dt, t, ctx?.player);
            }
            crowd.setPhase(phase);

            return new Level
            {
                id = "shelter", group = group, env = "underground", walk = shell.walk, solids = solids, points = points, spawns = spawns,
                npcs = crowd.npcs, states = States(), setState = setState, update = update, anchors = anchors,
            };
        }
    }
}
