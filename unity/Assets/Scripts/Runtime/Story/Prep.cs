// Ports story/prep.js: 1장 준비 · D-2 수요일 18:30 ~ D-1 목요일 밤 — six slots of time, more things to do than
// fit: the planner, each action (PrepActions), free time at home between slots, the evening before the alert.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.Story
{
    public static class Prep
    {
        // Slot start times: [day, minute]. Slot 0 is tonight; slots 1-5 are tomorrow.
        static readonly int[][] SLOT_AT = { new[] { -2, 18 * 60 + 30 }, new[] { -1, 9 * 60 }, new[] { -1, 11 * 60 }, new[] { -1, 13 * 60 + 30 }, new[] { -1, 15 * 60 + 30 }, new[] { -1, 17 * 60 + 30 }, new[] { -1, 19 * 60 + 30 } };
        static readonly string[] SLOT_LABEL = { "D-2 저녁", "D-1 오전", "D-1 오전", "D-1 오후", "D-1 오후", "D-1 저녁" };

        // Planner actions (mart shelter equip docs neighbors bag rest).
        public static readonly List<PlannerAction> ACTIONS = new List<PlannerAction>
        {
            new PlannerAction { id = "mart", label = "연무마트 장보기", cost = 2, icon = "🛒", desc = "물, 식량, 건전지, 라디오 같은 물품을 산다. 사람이 몰려 시간이 걸린다." },
            new PlannerAction { id = "shelter", label = "대피소·출입구 확인", cost = 1, icon = "🧭", desc = "지정 대피소까지 직접 걸어가 출입구와 경로를 확인한다." },
            new PlannerAction { id = "equip", label = "집 장비 점검", cost = 1, icon = "🔦", desc = "라디오·손전등에 건전지를 넣어 보고, 보조배터리를 충전하고, 소화기와 가스 밸브 위치를 확인한다." },
            new PlannerAction { id = "docs", label = "서류·연락처 정리", cost = 1, icon = "🗂", desc = "비상연락카드를 쓰고 중요 서류 사본을 만든다." },
            new PlannerAction { id = "neighbors", label = "이웃과 비상 연락 정하기", cost = 1, icon = "🤝", desc = "303호 서연, 관리사무소 박 소장, 편의점 수아와 이야기한다." },
            new PlannerAction { id = "bag", label = "비상가방 꾸리기", cost = 1, icon = "🎒", desc = "집에 있는 물품 가운데 들고 나갈 것을 배낭에 담아 현관에 둔다." },
            new PlannerAction { id = "rest", label = "휴식", cost = 1, icon = "🛏", desc = "쉬면서 피로와 긴장을 덜어 낸다." },
        };

        static void SetClockToSlot(Game G, int i) { var s = SLOT_AT[Math.Min(i, SLOT_AT.Length - 1)]; if (s[0] * 1440 + s[1] > G.day * 1440 + G.minute) { G.day = s[0]; G.minute = s[1]; } }
        static string EnvForSlot(int i) => i == 0 ? "night" : i >= 5 ? "dusk" : "day";
        static TalkOptions T(string speaker, string role, params string[] lines) => new TalkOptions { speaker = speaker, role = role, lines = lines.ToList() };
        static Choice Ok(string label) => new Choice("ok", label, true);

        public static async Task<string> Run(Ctx ctx)
        {
            var G = ctx.G;
            G.chapter = "prep";
            ctx.indoorEnv = "interiorNight"; ctx.power = true; ctx.outdoorEnv = "night";
            ctx.hud.SetChapter("1장", "준비 — 남은 시간 여섯 칸");
            ctx.Objective(null);
            PrepActions.ApplyHome(ctx);
            await ctx.Goto("apartment", "home");
            ctx.Refresh();
            await ctx.Card(new CardOptions { kind = "info", title = "103동 302호", meta = "D-2 수요일 18:30",
                body = new List<string> { "오늘 저녁 한 칸, 내일 다섯 칸. 모든 것을 다 할 수는 없다.", "거실 벽의 달력에서 할 일을 고르자. 물건을 많이 사는 것보다, 실제로 쓸 수 있게 해 두는 것이 중요하다." },
                actions = new List<Choice> { Ok("달력 보기") } });

            var opened = false;
            while (G.slots.used < G.slots.total)
            {
                var slot = G.slots.used;
                SetClockToSlot(G, slot);
                ctx.outdoorEnv = EnvForSlot(slot);
                ctx.indoorEnv = slot == 0 ? "interiorNight" : "interior";
                ctx.engine.SetEnv(ctx.indoorEnv, false, 0.5f);
                ctx.SetLevelState("apartment", "windowView", slot == 0 ? "night" : slot >= 5 ? "dusk" : "day");
                ctx.Refresh();
                ctx.Objective($"{SLOT_LABEL[slot]} · 무엇을 할까", "거실 벽의 달력(계획판)에서 고릅니다. 집 안을 둘러볼 수도 있습니다.",
                    new List<ObjectiveStep> { new ObjectiveStep($"남은 칸 {G.slots.total - G.slots.used}", false) });
                string pick;
                if (opened)
                {
                    var on = HomeFree(ctx);
                    on["homePlanner"] = async _ => await OpenPlanner(ctx, slot);   // a closed planner (null) keeps roaming
                    pick = (string)await ctx.Roam(new RoamOptions { doors = false, on = on });
                }
                else pick = await ctx.Modal(() => OpenPlanner(ctx, slot));
                opened = true;
                if (string.IsNullOrEmpty(pick)) continue;
                var action = ACTIONS.First(a => a.id == pick);
                State.SpendSlot(G, action.id, action.cost);
                ctx.Event("prep_action", Details.Of("id", action.id, "slot", slot));
                await RunAction(ctx, action, slot);
                if (slot == 0 && G.slots.used < G.slots.total)
                {
                    await ctx.Skip("D-2 수 밤", "D-1 목 09:00", "밤새 뉴스 속보 알림이 몇 번 울렸다. 정민은 잠을 설쳤다.");
                    State.Adjust(G, "fatigue", 6); State.Adjust(G, "tension", 6);
                }
            }
            await EveningBeforeAlert(ctx);
            return null;
        }

        static Task<string> OpenPlanner(Ctx ctx, int slot)
        {
            var G = ctx.G;
            var left = G.slots.total - G.slots.used;
            var done = new HashSet<string>(G.slots.done);
            var actions = ACTIONS.Select(a =>
            {
                var did = done.Contains(a.id) && a.id != "rest";
                return new PlannerAction { id = a.id, label = a.label, cost = a.cost, icon = a.icon, desc = a.desc, done = did,
                    disabled = a.cost > left || did || (slot == 0 && a.cost > 1),
                    reason = slot == 0 && a.cost > 1 ? "마트는 곧 문을 닫습니다. 내일 하세요." : a.cost > left ? $"남은 칸이 {left}칸이라 할 수 없습니다." : done.Contains(a.id) ? "이미 했습니다." : "" };
            }).ToList();
            var hint = slot == 0 ? "오늘 저녁은 한 칸뿐입니다. 마트는 두 칸이 드는 데다 곧 문을 닫습니다."
                : !State.Usable(G, "flashlight") && !(G.items.TryGetValue("batteries_aa", out var b) && b != null && b.home > 0) ? "손전등은 있지만 건전지가 없습니다. 있다는 것과 쓸 수 있다는 것은 다릅니다." : "";
            return ctx.ui.Planner(new PlannerOptions { day = SLOT_LABEL[slot], slotsTotal = G.slots.total, slotsUsed = G.slots.used, actions = actions, hint = hint });
        }

        static async Task RunAction(Ctx ctx, PlannerAction action, int slot)
        {
            var G = ctx.G;
            var end = SLOT_AT[Math.Min(slot + action.cost, SLOT_AT.Length - 1)];
            switch (action.id)
            {
                case "mart": await PrepActions.DoMart(ctx, slot); break;
                case "shelter": await PrepActions.DoShelterCheck(ctx); break;
                case "equip": await PrepActions.DoEquip(ctx); break;
                case "docs": await PrepActions.DoDocs(ctx); break;
                case "neighbors": await PrepActions.DoNeighbors(ctx); break;
                case "bag": await PrepActions.DoBag(ctx); break;
                case "rest":
                    await ctx.Card(new CardOptions { kind = "info", title = "휴식", meta = SLOT_LABEL[slot], body = new List<string> { "휴대폰을 내려놓고 잠깐 눈을 붙였다. 몸이 조금 가벼워졌다." }, actions = new List<Choice> { Ok("계속") } });
                    State.Adjust(G, "fatigue", -25); State.Adjust(G, "tension", -12);
                    break;
            }
            var t = Math.Max(G.day * 1440 + G.minute, end[0] * 1440 + end[1]); // absolute minutes, so a day change never rolls back
            G.day = (int)Math.Floor(t / 1440.0); G.minute = t - G.day * 1440;
            if (ctx.engine.Level?.id != "apartment") await PrepActions.GoHome(ctx);
            PrepActions.ApplyHome(ctx);
        }

        // Things the player can do at home without spending a slot.
        static Dictionary<string, Func<Point, Task<object>>> HomeFree(Ctx ctx)
        {
            var G = ctx.G;
            return new Dictionary<string, Func<Point, Task<object>>>
            {
                ["homeWindow"] = async _ => { await ctx.ui.Talk(T("창밖", "연무동", ctx.outdoorEnv == "day" ? "평소처럼 버스가 다닌다. 마트 주차장 쪽에 차가 많다." : "건너편 동의 불빛이 하나둘 켜진다. 평소와 다를 것 없어 보인다.")); return null; },
                ["homeFridge"] = async _ => { await ctx.ui.Talk(T("냉장고", "주방", "반찬 통 네 개, 어제 남은 익힌 고기 한 통. 전기가 끊기면 오래 버티지 못하는 것들이다.")); return null; },
                ["homeStock"] = async _ =>
                {
                    var lines = G.items.Where(kv => kv.Value.home > 0).Select(kv =>
                    {
                        Items.CATALOG.TryGetValue(kv.Key, out var c);
                        return $"{c?.name ?? kv.Key} {kv.Value.home}{c?.unit ?? ""}{(c?.needsCheck != null && !kv.Value.@checked ? " (아직 점검 안 함)" : "")}";
                    }).ToList();
                    await ctx.ui.Talk(T("베란다 수납장", "집 비축품", lines.Count > 0 ? string.Join(" · ", lines) : "비어 있다."));
                    return null;
                },
                ["homeBed"] = _ => { ctx.Toast("아직 잘 시간이 아니다."); return Task.FromResult<object>(null); },
                ["homeDesk"] = async _ =>
                {
                    if (G.know.shelterLocated) { ctx.Toast("지도앱에 생활문화센터 대피소를 저장해 두었다."); return null; }
                    var q = T("휴대폰", "지도앱", "지도앱에서 \"민방위 대피소\"를 검색해 볼까?");
                    q.choices = new List<Choice> { new Choice("yes", "검색한다"), new Choice("no", "나중에") };
                    if (await ctx.ui.Talk(q) != "yes") return null;
                    G.know.shelterLocated = true;
                    ctx.Event("shelter_located", Details.Of("via", "map_app"));
                    await ctx.ui.Talk(T("지도앱", "검색 결과", "가장 가까운 민방위 대피소: 연무 생활문화센터 지하주차장 (도보 약 4분).", "이름과 주소만 나온다. 출입구가 어디인지, 지금 열려 있는지는 지도만으로 알 수 없다."));
                    ctx.Note("가장 가까운 대피소: 연무 생활문화센터 지하주차장", "official");
                    return null;
                },
            };
        }

        // D-1 evening: news, the bathtub, then sleep. Leaves the clock at D-day 19:30.
        static async Task EveningBeforeAlert(Ctx ctx)
        {
            var G = ctx.G;
            G.day = -1; G.minute = 20 * 60;
            ctx.outdoorEnv = "night"; ctx.indoorEnv = "interiorNight";
            ctx.engine.SetEnv("interiorNight", false, 1);
            ctx.SetLevelState("apartment", "windowView", "night");
            if (ctx.engine.Level?.id != "apartment") await PrepActions.GoHome(ctx);
            ctx.Refresh();
            await ctx.Card(new CardOptions { kind = "news", title = "저녁 뉴스 (가상)", meta = "D-1 목 20:00",
                body = new List<string> { "정부는 국민들에게 비상시 국민행동요령을 미리 확인해 달라고 당부했습니다.", "일부 지역에서 생수와 건전지를 대량으로 사 가는 일이 이어지자, 사재기를 자제하고 필요한 만큼만 구입해 달라고 요청했습니다." },
                actions = new List<Choice> { Ok("TV를 끈다") } });
            void Show() => ctx.Objective("잠들기 전에", "할 수 있는 일이 남아 있다면 해 두고, 침대에서 잠듭니다.",
                new List<ObjectiveStep> { new ObjectiveStep("주방에서 큰 통·냄비에 물 받아 두기 — 선택", G.know.tubFilled), new ObjectiveStep("침대에서 잠들기", false) });
            Show();
            var on = HomeFree(ctx);
            on["homeSink"] = async _ =>
            {
                if (G.know.tubFilled) { ctx.Toast("큰 통과 냄비에 물을 받아 두었다."); return null; }
                var q = T("주방", "단수 대비", "단수에 대비해 큰 통과 냄비에 물을 받아 둘까? 씻거나 변기 물을 내리는 데 쓸 수 있다.");
                q.choices = new List<Choice> { new Choice("yes", "받아 둔다"), new Choice("no", "그만둔다") };
                if (await ctx.ui.Talk(q) == "yes") { G.know.tubFilled = true; ctx.Event("tub_filled", new Dictionary<string, object>()); ctx.Toast(ctx.FactLine("home_tub"), "official", 6000); Show(); }
                return null;
            };
            on["homeBed"] = _ => Task.FromResult<object>("sleep");
            await ctx.Roam(new RoamOptions { doors = false, on = on });
            await ctx.Skip("D-1 목 밤", "D-day 금 19:30", "다음 날, 회사는 오후 4시에 문을 닫았다. 정민은 일찍 집에 들어와 저녁을 먹었다.");
            G.day = 0; G.minute = 19 * 60 + 30;
            State.Adjust(G, "fatigue", -10);
        }
    }
}
