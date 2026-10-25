// Ports story/dday.js: 2장 정전과 경보 — the D-day evening at home, the 19:40 blackout, the 20:05 air-raid alert,
// leaving with (or without) the bag, the stairs, the streets toward the shelter (closures, the closed east gate,
// the strike), entering through a gate, inside the shelter, then 서연's relay (Seoyeon) and the chapter card.
// What was prepared shows up here as time saved, not as a score.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using Gyeongbo.Systems;
using UnityEngine;
using static Gyeongbo.Story.SB;

namespace Gyeongbo.Story
{
    public static class Dday
    {
        public const int ALERT = 20 * 60 + 5, LATE = 20 * 60 + 30;

        // A few seconds after 정민 steps outside, a missile hits a villa south of the park (no story place):
        // the blast, the shake, then fire and a smoke column that stay for the rest of the war.
        // Shown as a cutscene (Systems.Cutscenes); the town keeps the ruin, the fire and the smoke column after it.
        static void Strike(Ctx ctx)
        {
            var G = ctx.G;
            G.know.strikeSeen = true;
            var hit = false;
            Action impact = () =>
            {
                if (hit) return; hit = true;
                ctx.audio.Boom(0.55f);
                ctx.SetLevelState("town", "strike", "hit");
                State.RecordEvent(G, "strike", Details.Of("minute", G.minute));
                ctx.Stat("tension", 18);
            };
            _ = StrikeRun(ctx, impact);   // JS does not await the scene either
        }

        static async Task StrikeRun(Ctx ctx, Action impact)
        {
            try { await Cutscenes.StrikeScene(ctx, impact); }
            catch (Exception err) { Debug.LogError("[cine] strike scene failed"); Debug.LogException(err); }
            finally { impact(); ctx.Toast("멈추지 말고 대피소로. 무너진 건물 쪽으로 가지 않는다.", "self", 5200); }
        }

        // Later, more blasts far away: a dull boom and a slight tremor every half minute or so.
        static Action<float> FarBlasts(Ctx ctx)
        {
            float t = 18 + UnityEngine.Random.value * 20;
            return dt => { if ((t -= dt) > 0) return; t = 25 + UnityEngine.Random.value * 35; ctx.audio.Boom(1); ctx.engine.Shake(0.05f, 0.8f); };
        }

        public static async Task<string> Run(Ctx ctx)
        {
            var G = ctx.G;
            G.chapter = "dday";
            ctx.hud.SetChapter("2장", "정전과 경보");
            if (ctx.resume != "alert")
            {
                await Evening(ctx);
                ctx.Checkpoint("alert");
            }
            else
            {
                BlackoutState(ctx);
                await ctx.Goto("apartment", "home");
            }
            await Alert(ctx);
            await Shelter(ctx);
            await Seoyeon.SeoyeonRelay(ctx);
            await AfterAlert(ctx);
            return null;
        }

        static void BlackoutState(Ctx ctx)
        {
            ctx.power = false; ctx.outdoorEnv = "blackout"; ctx.indoorEnv = "interior";
            ctx.SetLevelState("apartment", "power", "off");
            ctx.SetLevelState("apartment", "windowView", "blackout");
            ctx.SetLevelState("store", "power", "off");
            ctx.G.phone.signal = "weak";
            PrepActions.ApplyHome(ctx);
        }

        static async Task Evening(Ctx ctx)
        {
            var G = ctx.G;
            G.day = 0; G.minute = 19 * 60 + 30;
            ctx.power = true; ctx.indoorEnv = "interiorNight"; ctx.outdoorEnv = "night";
            ctx.SetLevelState("apartment", "windowView", "night");
            PrepActions.ApplyHome(ctx);
            await ctx.Goto("apartment", "home");
            ctx.Refresh();
            if (Contact(G, "seoyeon")) await ctx.Card(Card("sms", "오서연", "19:32 · 문자", L("정민 씨, 병원 끝나고 카페 쉼표에서 잠깐 쉬고 있어요. 9시쯤 들어가요."), Cs(C("ok", "확인", true))));
            ctx.Objective("금요일 저녁", "집에서 쉬는 중입니다.", new List<ObjectiveStep>());
            float t = 0;
            await ctx.Roam(new RoamOptions
            {
                doors = false,
                tick = dt => (t += dt) > 7 ? "go" : null,
                on = { ["*"] = p => Task.FromResult<object>("go") },
            });

            // 19:40 the lights go out.
            G.minute = 19 * 60 + 40;
            var @out = false;
            Action cut = () =>
            {
                if (@out) return; @out = true;
                BlackoutState(ctx);
                ctx.engine.SetPower(false);
                ctx.engine.SetEnv("interiorDark", seconds: 0.4f);
                ctx.audio.Ambience("blackout", 0.3f);
            };
            try { await Cutscenes.BlackoutScene(ctx, cut); } catch (Exception err) { Debug.LogError("[cine] blackout scene failed"); Debug.LogException(err); }
            cut();
            State.RecordEvent(G, "blackout", Details.Of());
            ctx.Refresh();
            var src = Common.LightSource(ctx);
            ctx.lightSource = src;
            ctx.Toast(src == "flashlight" ? "F 키: 손전등 켜기/끄기" : src == "phone" ? "F 키: 휴대폰 손전등 (배터리가 빨리 닳습니다)" : "빛이 없다. 천천히 움직이자.", "self", 5000);
            if (src == "flashlight") { State.Use(G, "flashlight"); ctx.Payoff("flashlight", "정전 직후 손전등을 바로 켰다"); }

            var done = new HashSet<string>();
            while (!done.Contains("radio") && !done.Contains("phone"))
            {
                var choices = Cs(
                    new Choice { id = "radio", label = "라디오를 켠다", disabled = !Common.Has(G, "radio"), reason = "라디오가 없다" },
                    C("phone", "휴대폰을 본다"),
                    new Choice { id = "window", label = "창밖을 본다", disabled = done.Contains("window") },
                    new Choice { id = "hall", label = "복도에 나가 본다", disabled = done.Contains("hall") });
                if (Common.Has(G, "candles") && !G.know.candleLit) choices.Add(C("candle", "양초를 켠다"));
                var pick = await ctx.Say("정전", L("무엇부터 할까?"), choices, "19:41");
                if (pick != null) done.Add(pick);
                if (pick == "radio")
                {
                    var heard = await ctx.Modal(() => Common.RadioScene(ctx, "blackout"));
                    if (heard != null && G.know.radioChecked) ctx.Payoff("radio", "맞춰 둔 주파수로 정전 안내를 바로 들었다");
                    if (heard == null) done.Remove("radio");
                }
                else if (pick == "phone")
                {
                    Common.DrainBattery(G, 3);
                    await ctx.Modal(() => ctx.ui.Phone(new PhoneOptions
                    {
                        battery = G.phone.battery, signal = "weak", mapApp = "fail",
                        messages = new List<PhoneMessage> { new PhoneMessage { from = "안전안내문자", time = "19:46", kind = "safety", text = $"[안전안내문자] 19:46 연무동 일대 정전 발생. 복구 시간 미정. 추가 안내 예정 {Common.AREA}" } },
                        actions = Cs(C("close", "닫기")),
                    }));
                    ctx.Note("19:46 연무동 일대 정전, 복구 시간 미정 (안전안내문자)", "official");
                    ctx.Toast("지도앱이 열리지 않는다. 데이터가 거의 잡히지 않는다.", "site");
                }
                else if (pick == "window")
                {
                    await ctx.Say("창밖", L("연무동 전체가 어둡다. 건너편 동 창문에 휴대폰 불빛이 몇 개 흔들린다."), null, "정전");
                }
                else if (pick == "hall")
                {
                    await ctx.Say("3층 복도", L("비상구 표시등만 초록색으로 켜져 있다. 304호 어르신이 문을 열고 내다본다.", "\"정전인가 봐요. 나는 초 하나 켜 놨어요.\""), null, "정전");
                }
                else if (pick == "candle")
                {
                    G.know.candleLit = true; State.Use(G, "candles");
                    ctx.SetLevelState("apartment", "candles", "lit");
                    State.RecordEvent(G, "candle_lit", Details.Of());
                    await Common.FactCard(ctx, "양초에 대한 안내는 공식 자료끼리 다릅니다", L("candle_prep", "candle_no", "candle_rules"));
                }
            }
            ctx.Pass(ALERT - G.minute > 0 ? ALERT - G.minute : 0);
        }

        static async Task Alert(Ctx ctx)
        {
            var G = ctx.G;
            G.minute = ALERT;
            ctx.Refresh();
            ctx.audio.Siren(true);
            // Town for the evacuation (set first: the siren cutscene shows the street filling).
            var closures = G.cond.closures.dday;
            ctx.SetLevelState("town", "traffic", "none");
            ctx.SetLevelState("town", "crowd", "evacuating");
            ctx.SetLevelState("town", "closures", closures);
            ctx.SetLevelState("town", "gates", new Dictionary<string, string> { ["east"] = "closed", ["stairs"] = "open", ["ramp"] = "open", ["access"] = "closed" });
            ctx.SetLevelState("town", "notices", "dday");
            try { await Cutscenes.SirenScene(ctx); }
            catch (Exception err) { Debug.LogError("[cine] siren scene failed"); Debug.LogException(err); _ = ctx.Caption("공습경보 사이렌 — 1분간 오르내리는 소리 (5초 상승 · 3초 하강 반복)", null, 6000); }
            await Common.EmergencyText(ctx, "위급재난문자", "20:05", $"[위급재난문자] 20:05 미사일 발사, 연무동지역 공습경보, 지하시설 또는 건물 내로 대피. EVACUATION, MISSILES ATTACK {Common.AREA}");
            State.RecordEvent(G, "alert", Details.Of("minute", ALERT));
            await Common.FactCard(ctx, "공습경보", L("alert_airraid", "bag_take", "alert_stairs", "alert_night"));

            // What is still open depends on this run's closures; nobody names a road that is closed tonight.
            var detour = string.Join("이나 ", new[] { new[] { "danji", "단지길" }, new[] { "sijang", "시장길" } }.Where(r => !closures.Contains(r[0])).Select(r => r[1]));
            bool left = false, inStairs = false; float outT = 0;
            var blasts = FarBlasts(ctx);
            var trail = ctx.trail = new List<XZ>();
            List<ObjectiveStep> steps() => new List<ObjectiveStep> { new ObjectiveStep("배낭 챙기기", G.know.bagOn || left), new ObjectiveStep("계단으로 1층까지", inStairs), new ObjectiveStep("가까운 대피소로", false) };
            void show() => ctx.Objective("대피하기", "지하시설이나 유리창 없는 건물 안으로. 엘리베이터는 쓰지 않습니다.", steps());
            show();
            if (!G.know.idTaken) ctx.Toast("지갑과 신분증은 거실 탁자 위에 있다. 나가기 전에 챙기자. (E)", "self", 5000);

            async Task takeBag()
            {
                if (G.know.bagOn) return;
                if (G.know.bagPacked && G.items.Values.Any(e => e != null && e.bag > 0)) { G.know.bagOn = true; ctx.Payoff("bag", "현관에 둔 배낭을 메고 바로 나섰다"); State.RecordEvent(G, "departure", Details.Of("bag", "packed")); return; }
                var pick = await Talk(ctx, "현관", "배낭이 비어 있다", L("지금 챙길까, 그냥 나갈까?"), Cs(C("pack", "급히 챙긴다 (시간이 든다)"), C("go", "휴대폰만 들고 나간다")));
                if (pick != "pack") { State.RecordEvent(G, "departure", Details.Of("bag", "none")); return; }
                var items = PrepActions.BagItems(G);
                var mode = Drills.LadderStage(G, "bag");
                var res = await ctx.ui.Bag(new BagOptions { mode = mode, context = "alert", limit = Items.BAG_LIMIT, items = items, goals = Drills.BAG_GOALS["alert"], tips = new List<string>() });
                PrepActions.ApplyBag(G, res?.bag ?? new Dictionary<string, int>());
                var ev = Drills.EvalBag(G, "alert");
                Drills.RecordEncounter(G, "bag", ev.missing != null && ev.missing.Count > 0 ? "partial" : "complete");
                ctx.Pass(4);
                G.know.bagOn = true;
                ctx.Toast("물건을 고르느라 4분이 지났다.", "self");
                State.RecordEvent(G, "departure", Details.Of("bag", "late", "delay", 4));
            }

            var on = new Dictionary<string, Func<Point, Task<object>>>
            {
                ["homeBag"] = H(async p => { await takeBag(); show(); }),
                ["homeRadio"] = H(async p =>
                {
                    if (G.know.candleLit && !G.know.candleOut) { G.know.candleOut = true; ctx.SetLevelState("apartment", "candles", "none"); State.RecordEvent(G, "candle_out", Details.Of()); ctx.Toast("촛불을 껐다."); return; }
                    ctx.Toast("지금은 대피가 먼저다.");
                    await Task.CompletedTask;
                }),
                ["homeExitDoor"] = H(async p =>
                {
                    if (G.know.candleLit && !G.know.candleOut)
                    {
                        var pick = await Talk(ctx, "거실", "촛불", L("탁자 위 촛불이 아직 켜져 있다."), Cs(C("out", "끄고 나간다"), C("leave", "그대로 나간다")));
                        if (pick == "out") { G.know.candleOut = true; ctx.SetLevelState("apartment", "candles", "none"); }
                        State.RecordEvent(G, pick == "out" ? "candle_out" : "candle_left", Details.Of());
                        if (pick != "out") ctx.Toast(F("candle_rules"), "official", 6000);
                    }
                    await takeBag();
                    left = true;
                    await ctx.Goto("apartment", "corridor3F");
                    show();
                }),
                ["elevator"] = H(p => { ctx.Toast($"정전으로 멈춰 있다. {F("alert_stairs")}", "official", 5000); State.RecordEvent(G, "elevator_tried", Details.Of()); return Task.CompletedTask; }),
                ["managerWindow"] = H(async p =>
                {
                    if (Contact(G, "manager"))
                    {
                        await Talk(ctx, "박영수 소장", "1층 현관", L($"정민 씨! 생활문화센터로 가요. 연무로 가운데는 통제한대요. {detour}로 돌아가요!"));
                        G.know.closureHeard = true; ctx.Note($"박 소장: 연무로 중앙 구간 통제, {detour}로 우회", "neighbor");
                    }
                    else await Talk(ctx, "관리사무소장", "1층 현관", L("생활문화센터 대피소로 가세요! 길 막힌 데 있으니 안내 잘 보시고요!"));
                }),
            };
            foreach (var id in TownMap.CLOSURES.Keys)
                on["closure_" + id] = H(async p =>
                {
                    await Talk(ctx, "안내 요원", TownMap.CLOSURES[id].label, L($"이 구간은 통제 중입니다. 돌아가 주세요. 생활문화센터는 {detour}로 갈 수 있습니다."));
                    G.know.closureSeen = true; ctx.Note($"{TownMap.CLOSURES[id].label} (현장 안내 요원)", "site");
                });
            on["shelterNotice"] = H(p => Talk(ctx, "현장 안내판", State.Hhmm(G.minute), L("동쪽 출입구 폐쇄 · A 계단 운영 · B 진출입로 운영 (차량 통제) · C 경사로 준비 중")));
            on["*"] = H(async p => { if (p.kind == "notice") await Talk(ctx, p.label, "안내", L("대피소: 연무 생활문화센터 지하주차장. 유도표지판의 화살표를 따라가세요.")); });

            await ctx.Roam(new RoamOptions
            {
                rate = 3, deadline = new Deadline { day = 0, minute = LATE, type = "late" },
                tick = dt =>
                {
                    var pl = ctx.engine.Player;
                    var inTown = ctx.engine.Level?.id == "town";
                    if (inTown)
                    {
                        var hasLast = trail.Count > 0;
                        var last = hasLast ? trail[trail.Count - 1] : default;
                        if (!hasLast || Mathf.Sqrt((last.x - pl.X) * (last.x - pl.X) + (last.z - pl.Z) * (last.z - pl.Z)) > 1.5f) trail.Add(new XZ(pl.X, pl.Z));
                    }
                    if (inTown && !G.know.strikeSeen && (outT += dt) > 4) Strike(ctx);
                    else if (G.know.strikeSeen) blasts(dt);
                    if (!inStairs && ctx.engine.Level?.id == "apartment" && pl.Y < 1 && left) { inStairs = true; show(); if (ctx.lightSource == "flashlight") ctx.Payoff("flashlight", "손전등으로 어두운 계단을 내려갔다"); }
                    return null;
                },
                on = on,
                shelter = p => EnterGate(ctx, p),
            });
        }

        static async Task<object> EnterGate(Ctx ctx, Point point)
        {
            var G = ctx.G;
            bool gateSeen() => G.events.Any(e => e.type == "gate_observed" && e.Str("who") == "jeongmin");
            if (point.id == "shelterEast")
            {
                if (!gateSeen()) State.RecordEvent(G, "gate_observed", Details.Of("who", "jeongmin", "via", "sign", "minute", G.minute));
                G.know.gateClosedSeen = true;
                await ctx.ui.Card(Card("notice", "동쪽 출입구 폐쇄", $"현장 안내 · {State.Hhmm(G.minute)}", L("동쪽 출입구 폐쇄 · A 계단 / B 진출입로 이용", "확인한 사실: 이 출입구가 닫혀 있음. 이것만으로는 시설 전체의 운영 여부를 알 수 없다."), Cs(C("ok", "다른 출입구로", true))));
                if (G.know.entrancesChecked) ctx.Payoff("entrances", "닫힌 정문 앞에서 망설이지 않고 다른 출입구로 향했다");
                return null;
            }
            if (point.id == "shelterC") { ctx.Toast("셔터가 내려져 있다: \"준비 중\"", "site"); return null; }
            var route = point.id == "shelterA" ? "stairs" : "ramp";
            if (!gateSeen()) State.RecordEvent(G, "gate_observed", Details.Of("who", "jeongmin", "via", "notice_after", "minute", G.minute));
            State.RecordEvent(G, "route_attempt", Details.Of("who", "jeongmin", "route", route, "minute", G.minute));
            State.RecordEvent(G, "arrived", Details.Of("minute", G.minute, "late", G.minute > ALERT + 15));
            if (G.know.routeChecked) ctx.Payoff("route", "확인해 둔 경로 덕분에 막힌 길에서 돌아갈 길을 알았다");
            ctx.audio.Siren(false);
            await ctx.Goto("shelter", route == "stairs" ? "stairs" : "ramp");
            return "arrived";
        }

        static async Task Shelter(Ctx ctx)
        {
            var G = ctx.G;
            ctx.SetLevelState("shelter", "crowd", "arriving");
            ctx.SetLevelState("shelter", "ways", L("stairs", "ramp")); // east closed, C not open yet: nobody walks in there
            ctx.SetLevelState("shelter", "phase", "alert");
            ctx.SetLevelState("shelter", "power", "emergency");
            ctx.engine.SetEnv("underground", seconds: 0.8f);
            var signed = false;
            ctx.Objective("대피소 안에서", "접수대에서 명부를 쓰고 자리를 잡습니다.", new List<ObjectiveStep> { new ObjectiveStep("명부 쓰기 (접수대)", false), new ObjectiveStep("자리 잡기", false) });
            var thuds = FarBlasts(ctx);
            await ctx.Roam(new RoamOptions
            {
                rate = 0, doors = false, tick = dt => { thuds(dt); return null; },
                on =
                {
                    ["shelterExit"] = H(p => { ctx.Toast("경보가 해제될 때까지 대피소 안에 머뭅니다.", "site"); return Task.CompletedTask; }),
                    ["reception"] = H(async p =>
                    {
                        if (signed) { ctx.Toast("명부를 썼다."); return; }
                        var card = Bag(G, "contact_card") > 0 || Bag(G, "doc_copies") > 0;
                        await Talk(ctx, "대피소 관리자", "접수대", L("이름이랑 연락처, 복용하는 약 있으면 여기 적어 주세요.", card ? "(배낭 앞주머니의 비상연락카드를 보고 바로 적었다.)" : "(휴대폰을 보며 기억나는 대로 적었다. 배터리가 아깝다.)"));
                        if (card) ctx.Payoff("docs", "비상연락카드를 보고 명부를 바로 적었다"); else Common.DrainBattery(G, 2);
                        await Talk(ctx, "대피소 관리자", "접수대", L(F("shelter_rules"), F("shelter_rumor")));
                        signed = true;
                        ctx.Objective("대피소 안에서", "자리를 잡습니다.", new List<ObjectiveStep> { new ObjectiveStep("명부 쓰기 (접수대)", true), new ObjectiveStep("자리 잡기", false) });
                    }),
                    ["shelterRadio"] = H(p => Talk(ctx, "공용 라디오", "재난방송 (가상)", Common.BROADCASTS["shelter"](G))),
                    ["shelterBoard"] = H(p => Talk(ctx, "대피소 게시판", "안내", L("연무 생활문화센터 지하주차장 · 민방위 대피소", F("shelter_share")))),
                    ["spot"] = p => { if (signed) return Task.FromResult<object>("sit"); ctx.Toast("먼저 접수대에서 명부를 써 주세요.", "site"); return Task.FromResult<object>(null); },
                },
            });
            if (G.know.bagOn) foreach (var id in new[] { "water_500", "water_2l", "blanket" }) if (Bag(G, id) > 0) { State.Use(G, id); ctx.Payoff("bag", "배낭에서 물과 담요를 꺼냈다"); break; }
            if (!G.know.bagOn) { ctx.Stat("thirst", 6); ctx.Toast("맨손으로 나와 마실 물이 없다. 옆 사람이 물 한 모금을 나눠 주었다.", "neighbor"); }
        }

        static async Task AfterAlert(Ctx ctx)
        {
            var G = ctx.G;
            var h = G.handover;
            ctx.SetLevelState("shelter", "ways", L("stairs", "ramp", "access"));
            await ctx.Goto("shelter", "spot");
            ctx.SetLevelState("shelter", "crowd", "settled");
            var npcs = ctx.engine.Level?.npcs;
            if (npcs != null && npcs.TryGetValue("seoyeon", out var sy) && sy != null) sy.Group.SetActive(true);
            var lines = h == null ? L("정민 씨! 다행이다. 저는 C로 들어왔어요. 안내판에 C가 열렸다고 해서요.")
                : h.kind == "generalized" ? L("메시지 고마워요. 대피소가 닫혔다길래 잠깐 놀랐는데, 안내판엔 C가 열렸다고 해서 다시 봤어요.")
                : h.kind == "precise" ? L("메시지 고마워요. 언제 뭘 봤는지, 뭘 모르는지 써 줘서 판단하기 쉬웠어요. 저는 C로 왔어요.")
                : L("메시지 고마워요. 저는 안내판 보고 C로 왔어요. 계단은 저한테 안 맞아서요.");
            await ctx.Say("오서연", lines, null, "대피소");
            G.rel["seoyeon"].trust += 1;

            void later(int m) { G.minute = Math.Max(G.minute, m); ctx.Refresh(); } // 서연's walk may already be past these
            later(20 * 60 + 35);
            await Common.EmergencyText(ctx, "위급재난문자", "20:35", $"[위급재난문자] 20:35 연무동지역 경계경보로 변경, 대피소에서 나와 경계 태세를 유지하며 이동 가능 {Common.AREA}");
            await Common.FactCard(ctx, "경계경보로 바뀌었습니다", L("alert_downgrade", "alert_caution"));
            later(20 * 60 + 50);
            await Common.EmergencyText(ctx, "안전안내문자", "20:50", $"[안전안내문자] 20:50 연무동지역 경보해제. 정부 안내방송을 계속 들어 주십시오 {Common.AREA}");

            // Home together. The building is still dark; 서연 lives on the same floor.
            ctx.outdoorEnv = "blackout";
            ctx.SetLevelState("town", "crowd", "sparse");
            await ctx.Goto("apartment", "lobby");
            var pick = await ctx.Say("오서연", L("엘리베이터가 안 되네요. 3층이니까 계단으로 천천히 갈게요."), Cs(C("ask", "\"어떻게 도와드릴까요?\""), C("carry", "보행기를 들어 드릴게요"), C("go", "먼저 올라갈게요")), "1층 계단 앞");
            if (pick == "ask")
            {
                await ctx.Say("오서연", L("보행기를 접어서 제 옆에서 들어 주세요. 저는 난간 잡고 천천히 갈게요. 떨어져서 먼저 가지만 않으면 돼요."), null, "1층 계단 앞");
                ctx.Stat("fatigue", 6); G.rel["seoyeon"].trust += 2; State.RecordEvent(G, "help", Details.Of("who", "seoyeon", "what", "어떻게 도울지 먼저 묻고, 계단에서 보행기를 옆에서 들어 주었다"));
                ctx.Toast(F("people_ask"), "official", 6000);
            }
            else if (pick == "carry")
            {
                await ctx.Say("오서연", L("아, 잠깐만요. 보행기는 제 옆에 있어야 해서요. 접어서 바로 옆에서 들어 주시면 좋겠어요."), null, "1층 계단 앞");
                ctx.Stat("fatigue", 6); G.rel["seoyeon"].trust += 1; State.RecordEvent(G, "help", Details.Of("who", "seoyeon", "what", "계단에서 보행기를 옆에서 들어 주었다"));
            }
            else
            {
                await ctx.Say("오서연", L("네, 천천히 갈게요. 소장님이 계단 비춰 주신대요."), null, "1층 계단 앞");
            }
            later(21 * 60 + 20);
            await ctx.Goto("apartment", "home");
            var sum = Debrief.ChapterSummary(G, "dday");
            await ctx.Modal(() => ctx.ui.ChapterCard(new ChapterCardOptions { chapter = "2장", title = "정전과 경보", lines = sum.lines, payoffs = sum.payoffs }));
        }
    }
}
