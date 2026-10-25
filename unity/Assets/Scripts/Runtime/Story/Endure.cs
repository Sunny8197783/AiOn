// Ports story/endure.js: 3장 버티기 (D+1 ~ D+3) — the morning after, the water trip to the emergency tap
// (carrying water up the stairs), the evening, the store with 이수아's note, the corridor fire (extinguisher or
// retreat), day three; thirst and exhaustion checks while roaming.
// Helping costs rest; refusing costs nothing but the help.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using static Gyeongbo.Story.SB;

namespace Gyeongbo.Story
{
    public static class Endure
    {
        static string WaterId(Game G) => new[] { "water_500", "water_2l" }.FirstOrDefault(id => Home(G, id) + Bag(G, id) > 0);

        static bool Drink(Ctx ctx)
        {
            var G = ctx.G; var id = WaterId(G);
            if (id == null) { ctx.Toast("마실 물이 없다.", "self"); return false; }
            State.Consume(G, id, 1, G.items[id].home > 0 ? "home" : "bag");
            State.Adjust(G, "thirst", id == "water_2l" ? -45 : -20);
            ctx.Refresh();
            return true;
        }

        // The body giving out is a soft failure: a neighbour finds 정민, and the day starts over.
        static Func<float, object> BodyCheck(Ctx ctx) => dt => { var s = ctx.G.stats; if (s.thirst >= 95 || s.health <= 8) ctx.Fail("exhausted"); return null; };
        static bool OnThird(Ctx ctx) => ctx.engine.Level?.id == "apartment" && ctx.engine.Player.Y > 4;

        public static async Task<string> Run(Ctx ctx)
        {
            var G = ctx.G;
            G.chapter = "endure";
            ctx.hud.SetChapter("3장", "버티기");
            ctx.power = false; ctx.outdoorEnv = "day"; ctx.indoorEnv = "interior";
            ctx.SetLevelState("apartment", "power", "off");
            ctx.SetLevelState("store", "power", "off");
            ctx.SetLevelState("store", "clerk", "out"); // 이수아 left town the morning after the alert
            if (ctx.resume != "fire")
            {
                await ctx.Skip("D-day 금 밤", "D+1 토 07:00", "전기는 밤새 돌아오지 않았다. 아침에는 수도꼭지에서도 물이 나오지 않았다.");
                G.day = 1; G.minute = 7 * 60;
                ctx.SetLevelState("apartment", "windowView", "day");
                PrepActions.ApplyHome(ctx);
                await ctx.Goto("apartment", "home", "interiorDark");
                ctx.Refresh();
                await Morning(ctx);
                await WaterTrip(ctx);
                await Evening(ctx);
                ctx.Checkpoint("fire");
            }
            await CorridorFire(ctx);
            await DayThree(ctx);
            var sum = Debrief.ChapterSummary(G, "endure");
            await ctx.Modal(() => ctx.ui.ChapterCard(new ChapterCardOptions { chapter = "3장", title = "버티기", lines = sum.lines, payoffs = sum.payoffs }));
            return null;
        }

        static async Task Morning(Ctx ctx)
        {
            var G = ctx.G;
            bool fridge = false, drank = false, info = false;
            void show() => ctx.Objective("정전 이틀째 아침", "냉장고를 정리하고, 물을 마시고, 오늘의 안내를 확인합니다. 다 했으면 달력에서 하루를 시작합니다.", new List<ObjectiveStep> {
                new ObjectiveStep("냉장고 정리", fridge), new ObjectiveStep("물 마시기 (베란다 수납장)", drank), new ObjectiveStep("안내 확인 (라디오·이웃·1층 게시판)", info) });
            show();
            await ctx.Roam(new RoamOptions
            {
                tick = BodyCheck(ctx),
                on =
                {
                    ["homeFridge"] = H(async p =>
                    {
                        if (fridge) { ctx.Toast("냉장고는 정리했다."); return; }
                        await Common.FactCard(ctx, "정전 12시간째 냉장고", L("food_fridge", "food_4h", "food_door"));
                        foreach (var id in new[] { "fridge_food", "cooked_meat" })
                        {
                            var n = Home(G, id);
                            if (n == 0) continue;
                            var pick = await Talk(ctx, Items.CATALOG[id].name, $"냉장고 · {n}{Items.CATALOG[id].unit}",
                                L(id == "cooked_meat" ? "익힌 고기라 괜찮지 않을까? 냄새는 아직 잘 모르겠다." : "반찬 통이 미지근하다."),
                                Cs(C("eat", "먹는다"), C("toss", "버린다")));
                            if (pick == "eat")
                            {
                                State.Consume(G, id, 1); State.Adjust(G, "health", -12); State.Adjust(G, "tension", 8);
                                State.RecordEvent(G, "ate_spoiled", Details.Of("id", id));
                                ctx.Toast("오후 내내 배가 아팠다.", "self");
                                if (id == "cooked_meat") await Common.FactCard(ctx, "익혀도 마찬가지입니다", L("myth_meat", "food_doubt"));
                            }
                            else { State.Consume(G, id, n); State.RecordEvent(G, "tossed_food", Details.Of("id", id)); }
                        }
                        fridge = true; show();
                    }),
                    ["homeStock"] = H(p =>
                    {
                        if (drank) { ctx.Toast("아까 마셨다. 아껴 두자."); return Task.CompletedTask; }
                        if (!Drink(ctx)) return Task.CompletedTask;
                        drank = true; show();
                        if (G.know.tubFilled) ctx.Payoff("tub", "받아 둔 물로 세수하고 변기 물을 내렸다");
                        return Task.CompletedTask;
                    }),
                    ["homeRadio"] = H(async p =>
                    {
                        if (await Common.RadioScene(ctx, "water") != null) { G.know.waterPoint = true; info = true; show(); }
                    }),
                    ["homeExitDoor"] = H(p => ctx.Goto("apartment", "corridor3F")),
                    ["homeDoor"] = H(p => ctx.Goto("apartment", "homeEntry")),
                    ["seoyeonDoor"] = H(async p =>
                    {
                        if (ctx.seoyeonAsk != null) { ctx.Toast("\"이따 봬요.\"", "neighbor"); return; }
                        // JS returns `void ctx.ui.talk(...)` without awaiting it.
                        if (G.rel["seoyeon"].trust < 1) { _ = Talk(ctx, "303호", "문 너머", L("\"누구세요? …아, 302호요. 지금은 좀 바빠서요.\"")); return; }
                        var pick = await Talk(ctx, "오서연", "303호", L("아침 라디오에서 들었어요. 행정복지센터 마당에서 비상급수를 한대요. 9시 반부터요.", "저는 계단이 어려워서… 제 것도 한 통 부탁드려도 될까요? 물통은 제 걸 빌려 드릴게요. 대신 충전해 둔 보조배터리 하나 드릴게요."),
                            Cs(C("yes", "같이 받아 올게요"), C("no", "오늘은 제 것만 겨우 들 것 같아요")));
                        ctx.seoyeonAsk = pick;
                        G.know.waterPoint = true; info = true;
                        ctx.Note("서연: 행정복지센터 마당 비상급수 09:30부터", "neighbor");
                        if (pick == "no") await Talk(ctx, "오서연", "303호", L("괜찮아요. 소장님께 여쭤볼게요."));
                        show();
                    }),
                    ["lobbyNotice"] = H(async p =>
                    {
                        await ctx.ui.Card(Card("notice", "관리사무소 안내", "D+1 07:30 · 박영수 소장", L("단수가 계속되고 있습니다.", "행정복지센터 마당 비상급수시설 운영: 09:30 ~ 17:00 (물통 지참)", "엘리베이터 운행 중지. 계단에서는 손전등을 쓰세요."), Cs(C("ok", "확인", true))));
                        G.know.waterPoint = true; info = true; ctx.Note("관리사무소: 비상급수 09:30~17:00, 물통 지참", "official"); show();
                    }),
                    ["homePlanner"] = p => { if (info) return Task.FromResult<object>("done"); ctx.Toast("먼저 오늘의 안내를 확인하자."); return Task.FromResult<object>(null); },
                },
            });
            G.minute = Math.Max(G.minute, 9 * 60 + 20);
            ctx.Refresh();
        }

        static async Task WaterTrip(Ctx ctx)
        {
            var G = ctx.G;
            var jugs = Home(G, "water_jug") + Bag(G, "water_jug");
            var forSeoyeon = ctx.seoyeonAsk == "yes";
            ctx.SetLevelState("town", "traffic", "none");
            ctx.SetLevelState("town", "crowd", "queue");
            ctx.SetLevelState("town", "waterPoint", "on");
            ctx.SetLevelState("town", "closures", new List<string>());
            ctx.outdoorEnv = "day";
            await PrepActions.GoOut(ctx, "비상급수시설로", "행정복지센터 마당의 비상급수시설로 갑니다.");
            ctx.Objective("물 받기", "행정복지센터 마당 급수대에서 줄을 섭니다.", new List<ObjectiveStep>());
            await ctx.Roam(new RoamOptions { rate = 2, tick = BodyCheck(ctx), on = { ["water"] = p => Task.FromResult<object>("queue") } });
            await Common.FactCard(ctx, "비상급수시설", L("water_point", "water_sphere", "water_hands"));
            ctx.Pass(25);
            int carry;
            if (jugs > 0) { State.Use(G, "water_jug"); carry = 10; ctx.Payoff("water_jug", "접이식 물통으로 급수소 물을 받아 왔다"); }
            else { carry = 4; ctx.Toast("물통이 없어 빈 생수병 두 개에 4리터만 받았다.", "site"); }
            if (forSeoyeon) carry += 10;
            State.RecordEvent(G, "water_fetched", Details.Of("litres", carry, "forSeoyeon", forSeoyeon));
            ctx.speedOverride = carry > 12 ? 0.62f : 0.78f;
            ctx.engine.Player.Carrying = "water";
            State.Adjust(G, "fatigue", carry > 12 ? 14 : 8);
            ctx.Refresh();
            ctx.Objective("집으로", $"물 {carry}리터를 들고 103동 3층까지 돌아갑니다. 무겁다.", new List<ObjectiveStep>());
            var body = BodyCheck(ctx);
            await ctx.Roam(new RoamOptions
            {
                rate = 2, tick = dt => { body(dt); return OnThird(ctx) ? "up" : null; },
                on = { ["elevator"] = H(p => { ctx.Toast("엘리베이터가 멈춰 있다. 계단으로."); return Task.CompletedTask; }) },
            });
            ctx.speedOverride = null;
            ctx.engine.Player.Carrying = null;
            ctx.Refresh();
            Entry(G, "water_2l").home += forSeoyeon ? (int)Math.Floor((carry - 10) / 2.0) : (int)Math.Floor(carry / 2.0);
            if (forSeoyeon)
            {
                ctx.Objective("303호에 물 전하기", "서연의 문을 두드립니다.", new List<ObjectiveStep>());
                await ctx.Roam(new RoamOptions { on = { ["seoyeonDoor"] = p => Task.FromResult<object>("give") } });
                await Talk(ctx, "오서연", "303호", L("정말 고마워요. 덕분에 오늘은 버틸 수 있겠어요.", "약속한 보조배터리예요. 저는 하나 더 있어요."));
                var pb = Entry(G, "powerbank");
                pb.home += 1; pb.@checked = true;
                G.rel["seoyeon"].trust += 2;
                State.RecordEvent(G, "help", Details.Of("who", "seoyeon", "what", "급수소에서 물 한 통을 받아 3층까지 날라 주었다"));
                State.RecordEvent(G, "help", Details.Of("who", "seoyeon", "what", "서연이 충전된 보조배터리를 나눠 주었다"));
            }
            await ctx.Goto("apartment", "home");
            G.minute = Math.Max(G.minute, 13 * 60);
            ctx.Refresh();
        }

        static async Task Evening(Ctx ctx)
        {
            var G = ctx.G;
            G.minute = 17 * 60 + 30;
            ctx.outdoorEnv = "dusk";
            ctx.SetLevelState("town", "waterPoint", "off"); // closed at 17:00: tank, table and E all go
            ctx.SetLevelState("apartment", "windowView", "dusk");
            ctx.Refresh();
            var visited = false;
            ctx.Objective("저녁 · 할 수 있는 일", "편의점에 들러 볼 수도 있고, 집에서 쉴 수도 있습니다. 침대에서 하루를 마칩니다.", new List<ObjectiveStep> { new ObjectiveStep("바로편의점 (선택)", false), new ObjectiveStep("침대에서 쉬기", false) });
            await ctx.Roam(new RoamOptions
            {
                tick = BodyCheck(ctx),
                on =
                {
                    ["homeExitDoor"] = H(p => ctx.Goto("apartment", "corridor3F")),
                    ["homeDoor"] = H(p => ctx.Goto("apartment", "homeEntry")),
                    ["homeStock"] = H(p => { Drink(ctx); return Task.CompletedTask; }),
                    ["storeCounter"] = H(async p =>
                    {
                        if (visited) { ctx.Toast("계산대에는 아무도 없다."); return; }
                        visited = true;
                        await StoreEvening(ctx);
                        ctx.Objective("저녁 · 할 수 있는 일", "집으로 돌아가 침대에서 하루를 마칩니다.", new List<ObjectiveStep> { new ObjectiveStep("바로편의점 (선택)", true), new ObjectiveStep("침대에서 쉬기", false) });
                    }),
                    ["homeBed"] = p => Task.FromResult<object>("sleep"),
                },
            });
            State.Adjust(G, "fatigue", -30); State.Adjust(G, "tension", -8);
            G.day = 2; G.minute = 21 * 60;
        }

        // Nobody is behind the counter: 이수아 has gone to her family and left a note and a cash box.
        static async Task StoreEvening(Ctx ctx)
        {
            var G = ctx.G;
            await ctx.ui.Card(Card("notice", "계산대 위 쪽지", "바로편의점 · 비상등만 켜져 있다",
                L("정전으로 영업을 멈춥니다. 본가에 다녀올게요. — 이수아", "급하신 분은 남은 건전지·생수를 가져가시고, 현금은 계산대 옆 통에 넣어 주세요. 카드 단말기는 안 됩니다."),
                Cs(C("ok", "둘러본다", true))));
            var cash = Home(G, "cash") + Bag(G, "cash");
            if (cash > 0)
            {
                var buy = await Talk(ctx, "계산대 옆 현금 통", "현금만", L("선반에 건전지 한 팩과 작은 생수 몇 병이 남아 있다."), Cs(C("buy", "건전지 한 팩과 생수 두 병을 챙기고 현금을 넣는다"), C("no", "그냥 둔다")));
                if (buy == "buy")
                {
                    State.Consume(G, "cash", 1, G.items["cash"].home > 0 ? "home" : "bag");
                    var cart = new Dictionary<string, object> { ["batteries_aa"] = 1, ["water_500"] = 2 };
                    foreach (var kv in cart) { var e = Entry(G, kv.Key); e.home += (int)kv.Value; e.bought += (int)kv.Value; }
                    State.RecordEvent(G, "buy", Details.Of("cart", cart, "total", Items.STORE_ITEMS["batteries_aa"] + Items.STORE_ITEMS["water_500"] * 2, "paid", "cash"));
                    ctx.Payoff("cash", "카드가 안 되는 편의점에서 현금으로 건전지와 물을 샀다");
                }
            }
            else await Talk(ctx, "정민", "지갑", L("현금이 없다. 카드뿐이다. 카드로는 아무것도 살 수 없다."));
            var pick = await Talk(ctx, "102동 주민", "바로편의점 앞", L("여기 주인도 떠났나 봐요. 참, 그 얘기 들었어요? 생활문화센터 대피소가 폐쇄됐대요. SNS에 돌던데… 저도 확실한 건 모르겠어요."),
                Cs(C("spread", "큰일이네요. 다른 이웃들한테도 알려야겠어요"), C("check", "어디서 나온 말인지 먼저 확인해 볼게요")));
            State.RecordEvent(G, pick == "check" ? "rumor_checked" : "rumor_spread", Details.Of());
            ctx.Note("SNS: 생활문화센터 대피소 폐쇄? (출처 불명)", "rumor", "store");
            if (pick == "check")
            {
                var heard = State.Usable(G, "radio") ? await Common.RadioScene(ctx, "rumor") : null;
                await Talk(ctx, heard != null ? "재난방송" : "행정복지센터 게시판(주민의 휴대폰 사진)", "공식 안내", L("생활문화센터 대피소는 정상 운영 중입니다. 확인되지 않은 정보에 주의해 주십시오. (가상 안내)"));
                ctx.Note("생활문화센터 대피소 정상 운영 (공식 안내로 확인)", "official");
            }
            await Common.FactCard(ctx, "소문을 만났을 때", L("info_rumor", "shelter_rumor", "info_harm"));
            State.RecordEvent(G, "help", Details.Of("who", "clerk", "what", "가게를 닫으며 쪽지와 현금 통을 남겨 이웃이 필요한 물건을 가져가게 했다"));
            G.know.clerkLeft = true;
        }

        // D+2 night: a candle left burning by 304 tipped onto parcel boxes. Encounter 2 for the extinguisher.
        static async Task CorridorFire(Ctx ctx)
        {
            var G = ctx.G;
            G.day = 2; G.minute = 21 * 60 + 40;
            ctx.outdoorEnv = "blackout";
            ctx.SetLevelState("apartment", "windowView", "blackout");
            ctx.SetLevelState("apartment", "hallFire", "small");
            ctx.SetLevelState("apartment", "smoke", 0.25);
            await ctx.Goto("apartment", "home", "interiorDark");
            ctx.Refresh();
            ctx.audio.Fire(0.4f);
            await ctx.Caption("복도에서 타는 냄새가 난다. 문틈으로 주황빛이 흔들린다.", null, 3200);
            string size = "small", resolved = null; float t = 0; var shouted = false;
            void grow() { if (size == "small" && resolved == null) { size = "large"; ctx.SetLevelState("apartment", "hallFire", "large"); ctx.SetLevelState("apartment", "smoke", 0.7); ctx.audio.Fire(0.9f); ctx.Toast("불이 상자 더미로 번졌다. 천장까지 닿을 것 같다.", "site"); } }
            ctx.Objective("복도의 불", "판단하세요. 작은 불이면 소화기로, 커졌다면 알리고 대피합니다.", new List<ObjectiveStep>());

            async Task<object> retreat()
            {
                resolved = "retreat";
                await Common.FactCard(ctx, "대피하며 알리기", L("fire_shout", "fire_119", "fire_lift", "fire_big"));
                ctx.audio.Fire(0);
                ctx.SetLevelState("apartment", "hallFire", "out");
                State.RecordEvent(G, "fire_retreat", Details.Of("size", size));
                await Talk(ctx, "박영수 소장", "1층", L("다들 내려왔죠? 119에 신고했어요. 소방대가 금방 껐대요. 304호 어르신도 무사하시고요."));
                return "retreat";
            }
            async Task<object> fight()
            {
                var mode = Drills.LadderStage(G, "extinguisher");
                var fire = size; // judged on the fire the player saw when they chose
                var res = await ctx.ui.Extinguisher(new ExtinguisherOptions { mode = mode, fire = fire });
                var ev = Drills.EvalExtinguisher((IList<string>)res?.sequence ?? new List<string>(), fire);
                Drills.RecordEncounter(G, "extinguisher", ev.result);
                if (ev.result == "out")
                {
                    resolved = "out"; ctx.SetLevelState("apartment", "hallFire", "out"); ctx.SetLevelState("apartment", "smoke", 0.1); ctx.audio.Fire(0);
                    if (G.know.extinguisherLocated) ctx.Payoff("extinguisher", "소화기 위치와 순서를 알고 있어 작은 불을 바로 껐다");
                    return "out";
                }
                if (ev.result == "retreat-ok") return await retreat();
                if (ev.result == "escalated") { ctx.audio.Fire(0); ctx.Fail("fire"); }
                grow();
                return null;
            }

            // The fire spreads only while the player is free to act; dialogs and the drill have their own pace.
            await ctx.Roam(new RoamOptions
            {
                rate = 0,
                tick = dt => { if (resolved == null && ctx.engine.ControlsEnabled && (t += dt) > 28) grow(); return null; },
                on =
                {
                    ["homeExitDoor"] = H(p => ctx.Goto("apartment", "corridor3F")),
                    ["hallFire"] = async p =>
                    {
                        var pick = await Talk(ctx, "복도", size == "small" ? "택배 상자에 붙은 작은 불" : "천장까지 닿는 불길", L(size == "small" ? "304호 앞 택배 상자 위로 촛불이 넘어져 있다. 불길은 아직 작다." : "불길이 천장에 닿았다. 연기가 복도를 채운다."),
                            Cs(new Choice { id = "shout", label = "\"불이야!\" 외친다", disabled = shouted }, C("water", "물을 끼얹는다"), C("retreat", "대피하며 알린다 (계단 · 119)")));
                        if (pick == "shout") { shouted = true; State.RecordEvent(G, "fire_shout", Details.Of()); ctx.Toast("문이 열리고 이웃들이 나온다. 304호 어르신이 1층에서 올라오고 있다.", "site"); return null; }
                        // Once the fire reaches the ceiling the candle advice (extinguisher, blanket) no longer applies.
                        if (pick == "water")
                        {
                            if (size == "small") await Common.FactCard(ctx, "촛불 화재에 물은 위험할 수 있습니다", L("fire_candle"));
                            else await Common.FactCard(ctx, "불길이 이미 커졌습니다", L("fire_big", "fire_shout"));
                            grow(); return null;
                        }
                        return await retreat();
                    },
                    ["extinguisher"] = p => fight(),
                },
            });
            ctx.audio.Fire(0);
            State.Adjust(G, "tension", 10);
        }

        static async Task DayThree(Ctx ctx)
        {
            var G = ctx.G;
            G.day = 3; G.minute = 10 * 60;
            ctx.outdoorEnv = "day";
            ctx.SetLevelState("apartment", "windowView", "day");
            ctx.Objective(null);
            ctx.SetLevelState("apartment", "smoke", 0);
            await ctx.Goto("apartment", "home", "interiorDark");
            ctx.Refresh();
            if (G.stats.tension > 55) await Common.FactCard(ctx, "긴장이 높을 때", L("mind_breath", "mind_call"));
            var pick = await ctx.Say("오서연", L("어젯밤 괜찮으셨어요? 저 라디오 들은 거 메모해 뒀어요. 전기 복구 시점은 아직 정해지지 않았대요.", "앞으로 저녁마다 들은 걸 정리해서 문 앞에 붙여 둘게요. 필요하면 보세요."),
                Cs(C("thanks", "고마워요. 저는 급수소 소식을 전해 드릴게요"), C("ok", "네, 볼게요")), "303호 앞");
            if (pick == "thanks") { G.rel["seoyeon"].trust += 1; State.RecordEvent(G, "help", Details.Of("who", "seoyeon", "what", "라디오 메모와 급수소 소식을 서로 나누기로 했다")); }
            ctx.Note("서연 메모: 전기 복구 시점 미정 (라디오)", "neighbor");
            State.Adjust(G, "fatigue", -20); State.Adjust(G, "tension", -10);
            ctx.Refresh();
        }
    }
}
