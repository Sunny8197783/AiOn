// Ports story/evac.js: 4장 남을까, 떠날까 (crossroads, D+6: stay and the smoke from upstairs, or move and the
// shelter fire), 5장 공식 대피 (evacuation, D+12: pack one bag, the closures, the assembly point and the buses or
// another path), and the ending (생존 기록 through ctx.ui.Ending). Both choices in 4장 are valid under different
// conditions; the fire in 4장 is the third encounter, where stepping back is the success.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Story.SB;

namespace Gyeongbo.Story
{
    public static class Evac
    {
        static int Stock(Game G, params string[] ids) => ids.Sum(id => Home(G, id) + Bag(G, id));

        public static async Task<string> Crossroads(Ctx ctx)
        {
            var G = ctx.G;
            G.chapter = "crossroads";
            ctx.hud.SetChapter("4장", "남을까, 떠날까");
            ctx.power = false; ctx.outdoorEnv = "day";
            if (ctx.resume != "fire4")
            {
                await ctx.Skip("D+3 월", "D+6 목 10:00", "하루 한두 시간씩 전기가 들어왔다 나갔다. 급수소 줄은 날마다 길어졌다.");
                G.day = 6; G.minute = 10 * 60;
                PrepActions.ApplyHome(ctx);
                await ctx.Goto("apartment", "home", "interiorDark");
                ctx.Refresh();
                var heard = await ctx.Modal(() => Common.RadioScene(ctx, "crossroads"));
                await ctx.Card(Card("notice", "연무구 안내 (가상)", "D+6 · 행정복지센터 게시판·라디오",
                    L("희망하는 주민은 생활문화센터 임시 거처로 옮길 수 있습니다. (권고, 명령 아님)", "임시 거처: 급수·급식 지원, 공용 공간, 개인 공간 협소", "집에 머무는 주민도 비상급수시설을 계속 이용할 수 있습니다.", heard != null ? "(라디오에서 같은 내용을 들었다.)" : "(서연이 문 앞에 붙여 둔 메모에서 같은 내용을 읽었다.)"),
                    Cs(C("ok", "지금 상황 보기", true))));
                int water = Stock(G, "water_2l", "water_500"), food = Stock(G, "rice_instant", "canned", "bars", "ramen");
                G.know.stayMove = await ctx.Card(Card("info", "지금 내 상황", "판단 재료",
                    L($"마실 물 {water}병 · 먹을 것 {food}개", $"휴대폰 {N(G.phone.battery)}% · 몸 상태: 피로 {N(G.stats.fatigue)} · 긴장 {N(G.stats.tension)}",
                      "박 소장: \"저는 남아서 건물 봐야죠.\" · 서연: \"저는 주민센터에 이동 지원을 신청해 뒀어요.\"", G.know.clerkLeft ? "바로편의점 문에 쪽지: \"본가에 다녀옵니다 — 이수아\"" : "바로편의점은 문을 닫았다.",
                      "어느 쪽도 정답이 정해져 있지 않습니다. 지금의 조건으로 고르세요."),
                    Cs(C("stay", "집에 남는다"), C("move", "생활문화센터 임시 거처로 옮긴다"))));
                State.RecordEvent(G, "decision", Details.Of("stayMove", G.know.stayMove, "water", water, "food", food));
                ctx.Checkpoint("fire4");
            }
            if (G.know.stayMove == "move") await MoveAndFire(ctx); else await StayAndSmoke(ctx);
            var sum = Debrief.ChapterSummary(G, "crossroads");
            await ctx.Modal(() => ctx.ui.ChapterCard(new ChapterCardOptions { chapter = "4장", title = "남을까, 떠날까", lines = sum.lines, payoffs = sum.payoffs }));
            return null;
        }

        // Stay: smoke from another flat. The fact-based answer is to judge, not to run or to fight.
        static async Task StayAndSmoke(Ctx ctx)
        {
            var G = ctx.G;
            G.day = 7; G.minute = 22 * 60 + 10; ctx.outdoorEnv = "blackout";
            ctx.SetLevelState("apartment", "windowView", "blackout");
            ctx.SetLevelState("apartment", "smoke", 0.35);
            await ctx.Goto("apartment", "home", "interiorDark");
            ctx.Refresh();
            await ctx.Caption("밤 10시. 위층 어딘가에서 화재경보가 울리고, 복도 쪽에서 연기 냄새가 난다.", null, 3500);
            var felt = false;
            for (;;)
            {
                var pick = await ctx.Say("판단", L("우리 집 안으로 연기나 불길이 들어오지는 않았다. 어떻게 할까?"), Cs(
                    new Choice { id = "handle", label = "현관 문손잡이를 손등으로 만져 본다", disabled = felt },
                    C("fight", "복도로 나가 소화기로 끄러 간다"),
                    C("run", "바로 복도로 뛰어나가 계단으로 내려간다"),
                    C("stay", "창문을 닫고 119에 신고한 뒤 안내방송에 따른다")), "22:10");
                State.RecordEvent(G, "smoke_choice", Details.Of("pick", pick));
                if (pick == "handle") { felt = true; await ctx.Say("현관 문손잡이", L("뜨겁다. 문 너머 복도에 연기가 차 있는 것 같다.", F("fire_door")), null, "손등으로"); continue; }
                if (pick == "fight") { Drills.RecordEncounter(G, "extinguisher", "escalated"); ctx.Fail("fire", Details.Of("scene", "upstairs")); }
                if (pick == "run")
                {
                    State.Adjust(G, "health", -15); Drills.RecordEncounter(G, "extinguisher", "risky");
                    await Common.FactCard(ctx, "연기 속으로 나가기 전에", L("fire_judge", "fire_door", "fire_stay"));
                    break;
                }
                Drills.RecordEncounter(G, "extinguisher", "retreat-ok");
                await Common.FactCard(ctx, "상황을 보고 판단했습니다", L("fire_stay", "fire_judge", "fire_119"));
                ctx.Payoff("judgement", "불을 끄러 가지도, 연기 속으로 뛰어들지도 않고 상황을 보고 기다렸다");
                break;
            }
            ctx.SetLevelState("apartment", "smoke", 0);
            await ctx.Say("박영수 소장", L("5층 세대 주방에서 난 불이었대요. 소방대가 껐고 다친 사람은 없대요."), null, "방송");
        }

        // Move: temporary stay in the shelter; someone cooks with a burner and the boxes catch.
        static async Task MoveAndFire(Ctx ctx)
        {
            var G = ctx.G;
            G.day = 7; G.minute = 19 * 60 + 30;
            ctx.SetLevelState("shelter", "crowd", "settled");
            ctx.SetLevelState("shelter", "phase", "calm");
            ctx.SetLevelState("shelter", "power", "emergency");
            ctx.SetLevelState("shelter", "fire", "large");
            await ctx.Goto("shelter", "spot");
            {   // face the burning corner (JS space)
                var pl = ctx.engine.Player; var anchors = ctx.engine.Level?.anchors;
                if (anchors != null && anchors.TryGetValue("fire", out var f) && f != null) pl.Yaw = Mathf.Atan2(-(f.x - pl.X), -(f.z - pl.Z));
            }
            ctx.Refresh();
            await ctx.Caption("저녁. 주차장 구석에서 누군가 휴대용 버너로 라면을 끓이다 쌓아 둔 상자에 불이 옮겨 붙었다. 불길이 천장 배관에 닿는다.", null, 4200);
            await Common.FactCard(ctx, "대피소에서는", L("shelter_rules"));
            var res = await ctx.Modal(() => ctx.ui.Extinguisher(new ExtinguisherOptions { mode = Drills.LadderStage(G, "extinguisher"), fire = "large" }));
            var ev = Drills.EvalExtinguisher((IList<string>)res?.sequence ?? new List<string>(), "large");
            Drills.RecordEncounter(G, "extinguisher", ev.result);
            ctx.SetLevelState("shelter", "fire", "none");
            if (ev.result == "escalated") ctx.Fail("fire", Details.Of("scene", "shelter"));
            await Common.FactCard(ctx, "물러서는 것도 성공입니다", L("fire_big", "fire_shout", "fire_119"));
            ctx.Payoff("judgement", "천장까지 닿은 불 앞에서 물러나 알리고 대피했다");
        }

        public static async Task<string> Evacuation(Ctx ctx)
        {
            var G = ctx.G;
            G.chapter = "evac";
            ctx.hud.SetChapter("5장", "공식 대피");
            ctx.outdoorEnv = "day";
            if (ctx.resume != "depart")
            {
                await ctx.Skip("D+7", "D+12 수", G.know.stayMove == "move" ? "닷새가 더 지났다. 임시 거처에서 지내다 오늘 아침 짐을 챙기러 집에 들렀다." : "닷새가 더 지났다. 이웃 몇 집은 이미 떠났다.");
                G.day = 12; G.minute = G.cond.evacNoticeAt;
                PrepActions.ApplyHome(ctx);
                await ctx.Goto("apartment", "home", "interiorDark");
                ctx.Refresh();
                await Common.EmergencyText(ctx, "안전안내문자", State.Hhmm(G.minute), $"[안전안내문자] 연무동 주민 이동 지원. 오늘 14:00까지 행정복지센터 앞 임시 집결지로 이동. 1인 1가방, 신분증 지참 {Common.AREA}");
                await Common.FactCard(ctx, "대피 안내를 받았을 때", L("evac_order", "evac_kit", "evac_note"));
                var heard = await ctx.Modal(() => Common.RadioScene(ctx, "evac"));
                G.know.evacClosuresKnown = heard != null;
                // One bag, third encounter: no tips unless the ladder says so.
                var items = PrepActions.BagItems(G);
                var mode = Drills.LadderStage(G, "bag");
                var res = await ctx.Modal(() => ctx.ui.Bag(new BagOptions { mode = mode, context = "evac", limit = Items.BAG_LIMIT, items = items, goals = Drills.BAG_GOALS["evac"], tips = new List<string>() }));
                PrepActions.ApplyBag(G, res?.bag ?? new Dictionary<string, int>());
                var ev = Drills.EvalBag(G, "evac");
                var missing = ev.missing != null && ev.missing.Count > 0;
                Drills.RecordEncounter(G, "bag", missing ? "partial" : "complete");
                G.know.bagOn = true;
                if (G.know.bagPacked && !missing) ctx.Payoff("bag", "미리 꾸려 본 경험으로 한 가방을 빠르게 챙겼다");
                G.minute = Math.Max(G.minute, 13 * 60 + 15);
                ctx.Checkpoint("depart");
            }
            await Depart(ctx);
            return null;
        }

        static readonly Dictionary<string, string> PATH_CAPTION = new Dictionary<string, string>
        {
            ["missed"] = "버스는 이미 떠났다. 텅 빈 집결지에 바람만 분다.", ["stay"] = "가방을 내려놓았다. 오늘부터 이 건물이 내 자리다.", ["seoyeon"] = "서연의 문 앞에 가방을 내려놓고 앉았다. 지원 차량은 늦어지고 있었다.",
            ["volunteer"] = "조끼를 받아 입었다. 급수대 앞 줄이 길다.", ["walk"] = "도시의 끝, 통제선을 지나 국도로 들어섰다. 뒤에서 또 포성이 울렸다.",
        };

        static async Task Depart(Ctx ctx)
        {
            var G = ctx.G;
            var closures = G.cond.closures.evac;
            ctx.SetLevelState("town", "closures", closures);
            ctx.SetLevelState("town", "crowd", "assembly");
            ctx.SetLevelState("town", "buses", "on");
            ctx.SetLevelState("town", "waterPoint", "off");
            ctx.SetLevelState("town", "notices", "evac");
            await ctx.Goto("apartment", "home", "interiorDark");
            ctx.Refresh();
            bool noted = false, said = false;
            // The last choice is yours: the bus is one way out, not the answer. Each path ends the story differently.
            void show() => ctx.Objective("오늘 어떻게 할까", $"14:00 버스는 행정복지센터 앞 집결지에서 떠납니다. {(G.know.evacClosuresKnown ? $"오늘 통제: {string.Join(", ", closures.Select(c => TownMap.CLOSURES[c].label))}" : "어디가 통제인지는 아직 모릅니다.")}", new List<ObjectiveStep> {
                new ObjectiveStep("문에 쪽지 남기기 (선택)", noted), new ObjectiveStep("버스 타기: 14:00까지 집결지", false), new ObjectiveStep("남기: 관리사무소 박 소장에게", false),
                new ObjectiveStep("서연과 함께: 303호", false), new ObjectiveStep("돕기: 행정복지센터 자원봉사", false), new ObjectiveStep("걸어서 떠나기: 큰길 끝까지", false) });
            show();
            var B = new RectXZ(-124, 124, -104, 104); // past these, on a road, you have walked out of town
            var on = new Dictionary<string, Func<Point, Task<object>>>
            {
                ["homeExitDoor"] = H(async p =>
                {
                    if (!noted)
                    {
                        var pick = await Talk(ctx, "현관", "나가기 전에", L("문에 쪽지를 남길까?"), Cs(C("note", $"\"{State.Hhmm(G.minute)} 행정복지센터 집결지로 떠납니다 — 302호 정민\""), C("no", "그냥 나간다")));
                        noted = true;
                        if (pick == "note") { State.RecordEvent(G, "note_left", Details.Of()); ctx.Toast(F("evac_note"), "official", 5000); }
                        show();
                    }
                    await ctx.Goto("apartment", "corridor3F");
                }),
                ["homeDoor"] = H(p => ctx.Goto("apartment", "homeEntry")),
                ["seoyeonDoor"] = async p =>
                {
                    if (said) return null;
                    said = true;
                    var pick = await Talk(ctx, "오서연", "303호", L("저는 지원 차량이 3시에 와요. 집결지에서 다시 만나요.", "같이 기다려 줄 필요는 없어요. 정민 씨는 버스 시간 맞춰 가세요."), Cs(C("go", "집결지에서 만나요"), C("card", "제 연락처 카드 사진 찍어 두세요"), C("wait", "같이 기다렸다가 지원 차량으로 같이 가요")));
                    if (pick == "card") State.RecordEvent(G, "help", Details.Of("who", "seoyeon", "what", "떠나기 전 서로의 연락처를 다시 확인했다"));
                    if (pick == "wait") { State.RecordEvent(G, "help", Details.Of("who", "seoyeon", "what", "지원 차량이 올 때까지 곁을 지켰다")); return "seoyeon"; }
                    return null;
                },
                ["managerWindow"] = async p =>
                {
                    var pick = await Talk(ctx, "박영수 소장", "관리사무소", L("저는 남아요. 건물 지키는 사람도 있어야죠. 조심히 가요, 302호."), Cs(C("go", "소장님도 조심하세요"), C("stay", "저도 남겠습니다. 같이 지켜요")));
                    State.RecordEvent(G, "help", Details.Of("who", "manager", "what", "마지막까지 건물을 지키며 길을 안내했다"));
                    return pick == "stay" ? "stay" : null;
                },
                ["center"] = async p =>
                {
                    var pick = await Talk(ctx, "행정복지센터", "자원봉사 접수", L("급수 안내하고 거동 불편한 분들께 물 나를 사람이 모자라요.", "남아서 도와주실 수 있어요? 버스는 놓치게 될 거예요."), Cs(C("yes", "남아서 돕겠습니다"), C("no", "죄송해요, 버스를 타야 해요")));
                    return pick == "yes" ? "volunteer" : null;
                },
                ["assembly"] = p => Task.FromResult<object>("bus"),
            };
            foreach (var id in TownMap.CLOSURES.Keys)
                on["closure_" + id] = H(p => Talk(ctx, "안내 요원", TownMap.CLOSURES[id].label, L("이 구간은 통제 중입니다. 다른 길로 돌아가 주세요.")));
            var path = (string)await ctx.Roam(new RoamOptions
            {
                rate = 4,
                tick = dt =>
                {
                    var pl = ctx.engine.Player;
                    if (ctx.engine.Level?.id == "town" && (pl.X < B.x0 || pl.X > B.x1 || pl.Z < B.z0 || pl.Z > B.z1)) return "walk";
                    if (G.day > 12 || G.minute >= 14 * 60) return "missed";
                    return null;
                },
                on = on,
            });
            G.know.path = path;
            State.RecordEvent(G, "path", Details.Of("path", path));
            if (path != "bus") { await ctx.Caption(PATH_CAPTION.TryGetValue(path ?? "", out var cap) ? cap : null, null, 4000); return; }
            State.RecordEvent(G, "arrived_assembly", Details.Of("minute", G.minute));
            if (G.know.routeChecked) ctx.Payoff("route", "동네 길을 미리 알아 둔 덕분에 통제 구간을 돌아 집결지에 닿았다");
            var card = Bag(G, "contact_card") + Bag(G, "doc_copies") > 0;
            var hasId = Bag(G, "id_card") + Home(G, "id_card") > 0; // picked up from the wallet (Story/World)
            await ctx.Say("집결지 접수", L("이름, 연락처, 복용약을 적어 주세요. 신분증 있으세요?",
                hasId ? "(지갑에서 신분증을 꺼내 보여 주었다.)" : "(신분증을 집에 두고 나왔다. 이름과 주소, 생년월일로 본인 확인을 하느라 한참 걸렸다.)",
                card ? "(가방 앞주머니의 연락처 카드와 서류 사본도 함께 건넸다.)" : "(연락처는 기억나는 대로 적었다.)"), null, "임시 집결지");
            if (!hasId) { ctx.Pass(15); ctx.Toast(F("docs_take"), "official", 5000); }
            if (card || hasId) ctx.Payoff("docs", hasId ? "챙겨 나온 신분증으로 접수를 바로 마쳤다" : "연락처 카드와 서류 사본으로 접수를 바로 마쳤다");
            await ctx.Caption("버스 창밖으로 연무동이 멀어진다.", null, 3500);
        }

        // Resolves the ending card's button id ('restart' clears the save).
        public static Task<string> Ending(Ctx ctx)
        {
            var G = ctx.G;
            G.chapter = "ending";
            State.RecordEvent(G, "ending", Details.Of("load", State.BagLoad(G).weightKg));
            return ctx.Modal(() => ctx.ui.Ending(Debrief.EndingRecord(G), Facts.SOURCES));
        }
    }
}
