// Ports levels/town.js: 연무동 — assembles ground, buildings, park, props, fences, fronts, the shelter site,
// events, traffic, the strike and the crowd; points from game/townmap.js PLACES plus vending machines and the
// bus stop; spawns; NPC guides; notice-board texts; states and per-frame update.
// States: traffic (normal|light|none) crowd (TownCrowd.CROWD_MODES) closures (string list of CLOSURES ids)
// gates ({ east|stairs|ramp|access: open|closed }) waterPoint buses (on|off) notices (none|dday|water|evac)
// noticeText ({ boardId: lines | null }) strike (none|hit) season (autumn).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public static class Town
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("town", BuildTown);

        static readonly string[] GATE_VALUES = { "open", "closed" };
        static readonly string[] GATE_KEYS = { "east", "stairs", "ramp", "access" };
        static Dictionary<string, string> DefaultGates() => new Dictionary<string, string> { ["east"] = "open", ["stairs"] = "open", ["ramp"] = "open", ["access"] = "closed" };
        // Notice-board text per `notices` state (facility and schedule facts of this fictional town only).
        static readonly Dictionary<string, Dictionary<string, string[]>> NOTICES = new Dictionary<string, Dictionary<string, string[]>>
        {
            ["none"] = new Dictionary<string, string[]>
            {
                ["shelterNotice"] = new[] { "민방위 대피시설 안내", "연무 생활문화센터 지하주차장", "출입구: 동쪽 정문 · A 계단 · B 진출입로", "C 경사로: 공사 중 (10월 말 개방 예정)" },
                ["aptNotice"] = new[] { "한빛아파트 알림", "10/25(일) 단지 가을 대청소", "분리수거: 수요일 · 일요일", "관리사무소 031-000-0103" },
                ["centerNotice"] = new[] { "연무동 소식", "가을 문화축제 10/31(토) 연무공원", "행정복지센터 운영 09:00–18:00" },
            },
            ["dday"] = new Dictionary<string, string[]>
            {
                ["aptNotice"] = new[] { "관리사무소 안내", "정전으로 엘리베이터 운행 중지", "대피소: 연무 생활문화센터 지하주차장" },
                ["centerNotice"] = new[] { "연무동 행정복지센터", "대피소: 연무 생활문화센터 지하주차장", "현장 안내 요원 배치" },
            },
            ["water"] = new Dictionary<string, string[]>
            {
                ["shelterNotice"] = new[] { "민방위 대피시설 안내", "연무 생활문화센터 지하주차장", "운영 변경은 현장 안내판에 게시" },
                ["aptNotice"] = new[] { "단수 안내", "단지 전체 단수 중", "비상급수: 행정복지센터 마당" },
                ["centerNotice"] = new[] { "비상급수 안내", "행정복지센터 마당 비상급수시설", "운영 09:30–17:00 · 용기 지참" },
            },
            ["evac"] = new Dictionary<string, string[]>
            {
                ["shelterNotice"] = new[] { "대피소 안내", "주민 이동 지원 중", "임시 집결지: 행정복지센터 앞" },
                ["aptNotice"] = new[] { "주민 이동 안내", "오늘 14:00 임시 집결지 (행정복지센터 앞)", "남는 주민은 관리사무소에 알려 주세요" },
                ["centerNotice"] = new[] { "주민 이동 지원", "임시 집결지: 행정복지센터 앞", "14:00 버스 출발 · 1인 1가방 · 신분증 지참" },
            },
        };
        static readonly Dictionary<string, string> ACCENT = new Dictionary<string, string> { ["none"] = "#1f4e8c", ["dday"] = "#c42b25", ["water"] = "#1f5fa8", ["evac"] = "#c42b25" };

        public static Task<Level> BuildTown(Kit kit, IPeople people, IFx fx)
        {
            var group = J.Group("town");
            var quality = kit.Quality ?? "high";

            var ground = TownGround.BuildGround(kit, TownShelter.SHELTER_HOLES);
            var buildings = TownBuildings.BuildBuildings(kit, new Dictionary<string, FacadeHole> { ["culture"] = TownShelter.FACADE_HOLE });
            var spots = TownPark.TreeSpots(Kit.Rng(77)).Concat(TownProps.ShrubSpots(Kit.Rng(78))).ToList();
            var park = TownPark.BuildPark(kit, spots);
            var props = TownProps.BuildProps(kit, spots);
            var fences = TownFences.BuildFences(kit);
            var fronts = TownFronts.BuildFronts(kit);
            var site = TownShelter.BuildShelterSite(kit);
            var walk = ground.walk.Concat(site.walk).Concat(fronts.walk).Concat(park.walk).ToList();
            var events = TownEvents.BuildEvents(kit, people, walk);
            var traffic = TownTraffic.BuildTraffic(kit);
            var strike = TownStrike.BuildStrike(kit, fx, buildings.strike);
            group.Add(ground.group, buildings.group, park.group, props.group, fences.group, fronts.group, site.group, events.group, traffic.group, strike.group);
            var solids = buildings.solids.Concat(site.solids).Concat(fronts.solids).Concat(park.solids).Concat(props.solids).Concat(fences.solids).Concat(events.solids).Concat(traffic.solids).ToList();
            float Hy(float x, float z) => TownUtil.HeightAt(walk, x, z);

            // ---- points, spawns ----
            var R = new Dictionary<string, float> { ["door"] = 1.8f, ["shelter"] = 2.2f, ["notice"] = 1.9f, ["place"] = 2.4f };
            var points = TownMap.PLACES.Select(kv =>
            {
                var p = kv.Value; float x = (float)p.x, z = (float)p.z;
                return new Point { id = kv.Key, label = p.label, x = x, y = Hy(x, z), z = z, r = kv.Key == "assembly" ? 4.5f : p.kind != null && R.TryGetValue(p.kind, out var r) ? r : 1.8f, kind = p.kind, to = p.to };
            }).ToList();
            points.AddRange(events.closurePoints);
            // Street furniture the player can use (story/world.js): the vending machines (town-props) and the bus stop.
            var vend = new[] { (-60.6f, 37.6f), (43.4f, 15.2f), (7.2f, 11.3f) };
            for (int i = 0; i < vend.Length; i++) { var (x, z) = vend[i]; points.Add(new Point { id = $"vending{i}", label = "음료 자판기", x = x, y = Hy(x, z), z = z, r = 1.5f, kind = "vending" }); }
            points.Add(new Point { id = "busStop", label = "버스 정류장", x = 11.5f, y = Hy(11.5f, -8.4f), z = -8.4f, r = 2.2f, kind = "busStop" });
            Spawn Sp(float x, float z, float yaw) => new Spawn(x, Hy(x, z), z, yaw);
            var spawns = TownMap.SPAWNS.ToDictionary(kv => kv.Key, kv => Sp((float)kv.Value.x, (float)kv.Value.z, (float)kv.Value.yaw));
            // extra spawns (documented in the report)
            spawns["water"] = Sp(-80, -15.6f, Mathf.PI); spawns["assembly"] = Sp(-57.2f, -36, Mathf.PI / 2); spawns["shelterC"] = Sp(-100.6f, 12.0f, Mathf.PI / 2);
            spawns["east"] = Sp(-65.6f, 31, Mathf.PI / 2); spawns["park"] = Sp(0, 9.2f, Mathf.PI); spawns["crossing"] = Sp(0, -8.6f, Mathf.PI);

            // ---- NPCs ----
            IPerson Mk(string preset, int seed, float ax, float az, XZ face)
            {
                try
                {
                    var p = people.CreatePerson(preset, seed);
                    J.SetPos(p.Group, ax, Hy(ax, az), az); J.SetYaw(p.Group, TownUtil.YawTo(face.x, face.z));
                    p.Group.SetActive(false); group.Add(p.Group); p.SetAnim("idle");
                    return p;
                }
                catch (Exception err) { Debug.LogError($"[town] NPC {preset} failed\n{err}"); return null; }
            }
            var npcs = new Dictionary<string, IPerson>();
            void Put(string k, IPerson p) { if (p != null) npcs[k] = p; }
            var gE = TownShelter.GUIDE_SPOTS["guideEast"]; var gC = TownShelter.GUIDE_SPOTS["guideC"];
            Put("guideEast", Mk("guide", 61, gE.x, gE.z, gE.face));
            Put("guideC", Mk("guide", 62, gC.x, gC.z, gC.face));
            if (events.guides.Count > 0) Put("closureGuide", events.guides[0]);
            Put("clerkOutside", Mk("clerk", 63, 39.4f, 11.1f, new XZ(0, -1)));
            IPerson Npc(string k) => npcs.TryGetValue(k, out var p) ? p : null;

            var crowd = TownCrowd.Create(people, group, walk, quality);

            // ---- states ----
            string sTraffic = "normal", sCrowd = "none", sWater = "off", sBuses = "off", sNotices = "none", sSeason = "autumn", sStrike = "none";
            var closures = new List<string>(); var gates = DefaultGates(); var noticeText = new Dictionary<string, List<string>>();
            void DrawNotices()
            {
                var set = NOTICES.TryGetValue(sNotices, out var ns) ? ns : NOTICES["none"]; var g = gates;
                foreach (var kv in fronts.boards)
                {
                    var id = kv.Key;
                    IList<string> lines = noticeText.TryGetValue(id, out var own) ? own : set.TryGetValue(id, out var a) ? a : NOTICES["none"].TryGetValue(id, out var b) ? b : new string[0];
                    if (!noticeText.ContainsKey(id) && id == "shelterNotice" && sNotices == "dday")
                        lines = new[] { "현장 안내", $"동쪽 출입구 {(g["east"] == "closed" ? "폐쇄" : "운영")}", $"A 계단 {(g["stairs"] == "closed" ? "폐쇄" : "운영")}",
                            $"B 진출입로 {(g["ramp"] == "closed" ? "폐쇄" : "운영 (차량 통제)")}", $"C 경사로 {(g["access"] == "open" ? "개방 · 안내 요원 있음" : "준비 중")}" };
                    kv.Value.Draw(lines.Count > 0 ? lines[0] ?? "" : "", lines.Skip(1).ToList(), ACCENT.TryGetValue(sNotices, out var ac) ? ac : ACCENT["none"]);
                }
            }
            void RefreshNpcs()
            {
                Npc("guideEast")?.Group.SetActive(gates["east"] == "closed");
                Npc("guideC")?.Group.SetActive(gates["access"] == "open" && sCrowd != "none");
                Npc("clerkOutside")?.Group.SetActive(sCrowd == "normal" || sCrowd == "busy");
            }
            bool OnOff(string v) => v == "on" || v == "off";
            void SetState(string key, object value)
            {
                switch (key)
                {
                    case "traffic": traffic.setMode(Sv.Str(value)); sTraffic = traffic.mode(); break;
                    case "crowd": sCrowd = crowd.SetMode(Sv.Str(value)); break;
                    case "closures":
                    {
                        var plain = Sv.Plain(value);
                        var list = plain == null || (plain is string s0 && s0 == "") || (plain is bool b0 && !b0) ? new List<string>() : Sv.StrList(plain);
                        closures = events.setClosures(list);
                        foreach (var p in events.closurePoints) p.enabled = closures.Contains(p.id.Substring(8));
                        crowd.SetContext(closures: closures);
                        break;
                    }
                    case "gates":
                    {
                        var next = new Dictionary<string, string>(gates);
                        foreach (var kv in Sv.StrMap(value))
                        {
                            if (!GATE_KEYS.Contains(kv.Key) || !GATE_VALUES.Contains(kv.Value)) { Debug.LogError($"[town] gates.{kv.Key} = \"{kv.Value}\" invalid; use {string.Join("/", GATE_KEYS)} : open | closed"); continue; }
                            next[kv.Key] = kv.Value;
                        }
                        gates = next; site.setGates(new Dictionary<string, string>(next)); crowd.SetContext(gates: new Dictionary<string, string>(next)); DrawNotices();
                        break;
                    }
                    case "waterPoint":
                    {
                        var v = Sv.Str(value);
                        if (!OnOff(v)) { Debug.LogError($"[town] waterPoint \"{v}\" invalid; use on | off"); return; }
                        sWater = v; events.setWater(v == "on");
                        var wp = points.FirstOrDefault(p => p.id == "water"); if (wp != null) wp.enabled = v == "on"; // gone from view = gone from E
                        break;
                    }
                    case "buses":
                    {
                        var v = Sv.Str(value);
                        if (!OnOff(v)) { Debug.LogError($"[town] buses \"{v}\" invalid; use on | off"); return; }
                        sBuses = v; events.setBuses(v == "on"); break;
                    }
                    case "notices":
                    {
                        var v = Sv.Str(value);
                        if (v == null || !NOTICES.ContainsKey(v)) { Debug.LogError($"[town] notices \"{v}\" invalid; use {string.Join(" | ", NOTICES.Keys)}"); return; }
                        sNotices = v; DrawNotices(); break;
                    }
                    case "noticeText":
                    {
                        foreach (var kv in Sv.StrListMap(value))
                        {
                            if (!fronts.boards.ContainsKey(kv.Key)) { Debug.LogError($"[town] noticeText: unknown board \"{kv.Key}\"; use {string.Join(", ", fronts.boards.Keys)}"); continue; }
                            if (kv.Value == null) noticeText.Remove(kv.Key); else noticeText[kv.Key] = kv.Value;
                        }
                        DrawNotices();
                        break;
                    }
                    case "strike":
                    {
                        var v = Sv.Str(value);
                        if (v != "none" && v != "hit") { Debug.LogError($"[town] strike \"{v}\" invalid; use none | hit"); return; }
                        sStrike = v; strike.setHit(v == "hit"); break;
                    }
                    case "season": { var v = Sv.Str(value); if (v != "autumn") Debug.LogWarning($"[town] season \"{v}\" not built; the town is autumn only"); break; }
                    default: Debug.LogWarning($"[town] unknown state \"{key}\""); return;
                }
                RefreshNpcs();
            }
            SetState("traffic", "normal"); SetState("closures", new List<string>()); SetState("gates", DefaultGates()); SetState("waterPoint", "off");
            SetState("buses", "off"); SetState("notices", "none"); SetState("crowd", "normal");

            // ---- per frame ----
            float nt = 0;
            var localNpcs = new[] { "guideEast", "guideC", "clerkOutside" }.Select(Npc).Where(p => p != null).ToList();
            var clerk = Npc("clerkOutside");
            void Update(float dt, float t, FrameCtx ctx)
            {
                ctx ??= new FrameCtx();
                // JS polls the reduce-motion class once a second; here the frame context carries it.
                TownUtil.SetWind(t, ctx.reduced ? 0 : 1);
                // No grid power after the blackout: dead signals, and nobody drives through town any more.
                if (!ctx.power && traffic.mode() != "none") SetState("traffic", "none");
                traffic.update(dt, t, ctx);
                strike.update(dt, t, ctx.camera, ctx.env == "night" || ctx.env == "blackout" || ctx.env == "dusk");
                events.update(dt, t, ctx);
                crowd.Update(dt, ctx);
                nt += dt;
                var pl = ctx.player;
                for (int k = 0; k < localNpcs.Count; k++)
                {
                    var p = localNpcs[k];
                    if (!p.Group.activeSelf) continue;
                    var at = J.Pos(p.Group);
                    float dx = pl != null ? pl.X - at.x : 99, dz = pl != null ? pl.Z - at.z : 99, d2 = dx * dx + dz * dz;
                    if (p != clerk)
                    {
                        bool near = d2 < 49; float ph = (nt + k * 3.1f) % 10;
                        p.LookAt(near ? pl.X : (float?)null, near ? pl.Z : (float?)null);
                        p.SetAnim(near ? "talk" : ph < 3 ? "point" : ph < 5 ? "wave" : "idle");
                    }
                    if (d2 < 6400) p.Update(dt);
                }
            }

            var level = new Level
            {
                id = "town", group = group, env = "day", walk = walk, solids = solids, points = points, spawns = spawns, npcs = npcs,
                states = new Dictionary<string, object>
                {
                    ["traffic"] = new[] { "normal", "light", "none" }, ["crowd"] = TownCrowd.CROWD_MODES, ["closures"] = TownMap.CLOSURES.Keys.ToArray(),
                    ["gates"] = new Dictionary<string, string[]> { ["east"] = new[] { "open", "closed" }, ["stairs"] = new[] { "open", "closed" }, ["ramp"] = new[] { "open", "closed" }, ["access"] = new[] { "closed", "open" } },
                    ["waterPoint"] = new[] { "off", "on" }, ["buses"] = new[] { "off", "on" }, ["strike"] = new[] { "none", "hit" }, ["notices"] = NOTICES.Keys.ToArray(), ["noticeText"] = new string[0], ["season"] = new[] { "autumn" },
                },
                setState = SetState, update = Update,
                state = () => new Dictionary<string, object>
                {
                    ["traffic"] = sTraffic, ["crowd"] = sCrowd, ["closures"] = new List<string>(closures), ["gates"] = new Dictionary<string, string>(gates), ["waterPoint"] = sWater,
                    ["buses"] = sBuses, ["notices"] = sNotices, ["noticeText"] = noticeText.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value)), ["season"] = sSeason, ["strike"] = sStrike,
                },
                anchors = new Dictionary<string, Anchor>
                {
                    ["craneShelter"] = new Anchor { x = -80, y = 110, z = 31, look = new Vector3(-89, 0, 31) },
                    ["streetHome"] = new Anchor { x = 66, y = 1.7f, z = 21, look = new Vector3(78, 2.2f, 29.6f) },
                    ["assemblyView"] = new Anchor { x = -52, y = 7, z = -24, look = new Vector3(-64, 1.5f, -40) },
                },
            };
            return Task.FromResult(level);
        }
    }
}
