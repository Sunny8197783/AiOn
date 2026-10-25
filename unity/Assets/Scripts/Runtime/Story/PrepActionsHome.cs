// Ports the rest of story/prep-actions.js: the contact card (docs), the neighbours (서연, 박 소장, 수아) and
// packing the emergency bag, plus applyBag.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.Story
{
    public static partial class PrepActions
    {
        // { home: 0, bag: 0, bought: 0, checked, used: 0, ...(G.items[id] || {}) }
        static ItemEntry EnsureItem(Game G, string id, bool @checked)
        {
            if (!G.items.TryGetValue(id, out var e) || e == null) G.items[id] = e = new ItemEntry { @checked = @checked };
            return e;
        }

        public static async Task DoDocs(Ctx ctx)
        {
            var G = ctx.G;
            ctx.Objective("서류·연락처 정리", "작은방 책상에서 비상연락카드를 씁니다.", new List<ObjectiveStep>());
            await ctx.Roam(new RoamOptions { on = new Dictionary<string, Func<Point, Task<object>>> { ["homeDesk"] = _ => Task.FromResult<object>("go") } });
            var fields = new List<DocField>
            {
                new DocField { id = "me", label = "개인사항 (이름·주소·생년월일)", value = "정민 · 연무동 한빛아파트 103동 302호", why = F("contact_fields"), recommended = true },
                new DocField { id = "blood", label = "혈액형", value = "A형", why = "다쳤을 때 도움이 됩니다.", recommended = true },
                new DocField { id = "meds", label = "복용약", value = "알레르기약 (1일 1회)", why = "약이 떨어지거나 잃어버렸을 때 필요합니다.", recommended = true },
                new DocField { id = "family", label = "가족 연락처", value = "부모님(대구) 010-○○○○-○○○○", why = F("contact_card"), recommended = true },
                new DocField { id = "work", label = "직장 연락처", value = "한결물류 연무센터 운영팀", why = "집·직장 연락처를 함께 적습니다.", recommended = true },
                new DocField { id = "neighbors", label = "이웃 연락처", value = "303호 오서연 · 관리사무소", why = "휴대폰이 꺼져도 연락할 사람을 찾을 수 있습니다.", recommended = true },
                new DocField { id = "agencies", label = "신고·구호기관", value = "119 · 112 · 정전 123", why = F("power_123"), recommended = true },
                new DocField { id = "meet", label = "만남의 장소 1·2·3차", value = "① 생활문화센터 대피소 ② 행정복지센터 ③ 연무공원 정자", why = F("contact_meet"), recommended = true },
                new DocField { id = "idcopy", label = "신분증·통장·계약서 사본", value = "프린터로 사본 1부씩", why = F("bag_copies"), recommended = true },
            };
            var res = await ctx.Modal(() => ctx.ui.Docs(new DocsOptions { mode = "guided", fields = fields, carry = new List<string> { "bag", "home" } }));
            Drills.RecordEncounter(G, "docs", "written");
            var card = res?.card ?? new Dictionary<string, string>();
            var where = res?.where == "bag" ? "bag" : "home";
            var cc = EnsureItem(G, "contact_card", true);
            if (where == "bag") cc.bag += 1; else cc.home += 1;
            G.know.contactsCard = true;
            G.know.cardFields = card.Keys.ToList();
            if (card.TryGetValue("idcopy", out var idc) && !string.IsNullOrEmpty(idc))
            {
                var dc = EnsureItem(G, "doc_copies", true);
                if (where == "bag") dc.bag += 1; else dc.home += 1;
                G.know.docsCopied = true;
            }
            if (card.TryGetValue("meet", out var mt) && !string.IsNullOrEmpty(mt)) G.know.meetPoint = "생활문화센터 대피소";
            ctx.Event("docs_done", Details.Of("fields", card.Keys.Cast<object>().ToList(), "where", where));
            ctx.Toast(where == "bag" ? "비상연락카드를 배낭 앞주머니에 넣었다." : "비상연락카드를 책상 서랍에 넣었다.");
        }

        public static async Task DoNeighbors(Ctx ctx)
        {
            bool seoyeon = false, manager = false, clerk = false;
            List<ObjectiveStep> Steps() => new List<ObjectiveStep> { new ObjectiveStep("303호 서연", seoyeon), new ObjectiveStep("관리사무소 박 소장 (1층)", manager), new ObjectiveStep("바로편의점 수아", clerk) };
            void Show() => ctx.Objective("이웃과 비상 연락 정하기", "세 사람을 만나 이야기합니다. 다 만나면 집으로 돌아옵니다.", Steps());
            Show();
            await ctx.Roam(new RoamOptions
            {
                on = new Dictionary<string, Func<Point, Task<object>>>
                {
                    ["homeExitDoor"] = async _ => { await ctx.Goto("apartment", "corridor3F"); return null; },
                    ["homeDoor"] = async _ => { await ctx.Goto("apartment", "homeEntry"); return seoyeon && manager && clerk ? "done" : null; },
                    ["elevator"] = async _ => { await ctx.Goto("apartment", ctx.engine.Player.Y > 1 ? "lobby" : "corridor3F"); return null; },
                    ["seoyeonDoor"] = async _ => { if (seoyeon) { ctx.Toast("\"내일 봬요.\"", "neighbor"); return null; } await TalkSeoyeon(ctx); seoyeon = true; Show(); return null; },
                    ["managerWindow"] = async _ => { if (manager) { ctx.Toast("박 소장은 전화 통화 중이다."); return null; } await TalkManager(ctx); manager = true; Show(); return null; },
                    ["storeCounter"] = async _ => { if (clerk) { ctx.Toast("\"또 오세요.\"", "neighbor"); return null; } await TalkClerk(ctx); clerk = true; Show(); return null; },
                    ["homePlanner"] = async _ =>
                    {
                        var pick = await ctx.ui.Talk(T("이웃", "달력", new[] { "이 정도로 마칠까?" }, new Choice("yes", "마친다"), new Choice("no", "더 만난다")));
                        return pick == "yes" ? "done" : null;
                    },
                },
            });
            ctx.Event("neighbors_done", Details.Of("seoyeon", seoyeon, "manager", manager, "clerk", clerk));
        }

        static async Task TalkSeoyeon(Ctx ctx)
        {
            var G = ctx.G;
            ctx.SetLevelState("apartment", "seoyeonDoor", "open");
            await ctx.ui.Talk(T("오서연", "303호", "아, 302호 정민 씨죠? 오서연이에요.", "저는 보행 보조기를 써요. 이 카드에 제 상황을 적어 뒀어요."));
            await ctx.ui.Card(new CardOptions { kind = "info", title = "서연의 재난 대비 카드", meta = "서연이 직접 쓴 카드", body = SEOYEON_CARD.Concat(new[] { F("people_card") }).ToList(), actions = new List<Choice> { new Choice("ok", "읽었다", true) } });
            var pick = await ctx.ui.Talk(T("오서연", "303호", new[] { "비상시에 서로 어떻게 연락할까요? 문자가 제일 편해요.", "혹시 대피소 출입구 상황을 알게 되면, 확인한 것과 못 한 것을 나눠서 보내 줄래요? 저한테 맞는 입구는 제가 다시 볼게요." },
                new Choice("sms", "문자로 연락해요"), new Choice("both", "문자도 하고, 문에 쪽지도 남길게요")));
            G.rel["seoyeon"].contact = pick == "both" ? "both" : "sms";
            G.rel["seoyeon"].trust += 1;
            if (!HasAt(G, "batteries_aa"))
            {
                var b = await ctx.ui.Talk(T("오서연", "303호", new[] { "건전지 있으세요? 저 두 팩 사 놨는데 한 팩 나눠 드릴까요?" }, new Choice("yes", "고마워요, 받을게요"), new Choice("no", "괜찮아요")));
                if (b == "yes") { EnsureItem(G, "batteries_aa", false).home += 1; ctx.Event("gift", Details.Of("from", "seoyeon", "item", "batteries_aa")); }
            }
            await ctx.ui.Talk(T("오서연", "303호", "내일 저녁엔 병원 진료가 있어서 좀 늦게 들어와요. 무슨 일 있으면 문자 주세요."));
            ctx.Note("서연(303호): 문자로 연락. 출입구 상황은 확인한 것과 못 한 것을 나눠서.", "neighbor", "apartment");
            ctx.SetLevelState("apartment", "seoyeonDoor", "closed");
        }

        static async Task TalkManager(Ctx ctx)
        {
            var G = ctx.G;
            await ctx.ui.Talk(T("박영수 소장", "관리사무소", "아이고, 302호. 오늘 민원이 밀려서 길게는 못 해요.", "정전되면 엘리베이터는 못 써요. 계단 비상등은 배터리로 켜지고요.",
                "우리 단지 대피소는 길 건너 생활문화센터 지하예요. 정문이 동쪽 문이고, 계단 A, 차 다니는 진출입로 B. C 경사로는 아직 공사 중이에요.", "소화기는 층마다 복도 소화전 옆에 있어요."));
            G.rel["manager"].contact = "office"; G.rel["manager"].trust += 1;
            G.know.entrancesHeard = true;
            ctx.Note("박 소장: 대피소 출입구는 동쪽 정문·A 계단·B 진출입로 (C 공사 중). 정전 시 엘리베이터 사용 불가.", "neighbor", "apartment");
        }

        static async Task TalkClerk(Ctx ctx)
        {
            var G = ctx.G;
            await ctx.ui.Talk(T("이수아", "바로편의점", "어서 오세요. 건전지요? 오늘 거의 다 나갔어요. 내일 오전에 더 들어온다고는 했는데, 확실하진 않아요.", "생수는 마트가 더 싸요. 1인 6병 한정이래요.",
                "정전되면 카드 단말기가 안 될 수도 있대요. 현금 조금 있으면 좋을 거예요. 저도 들은 얘기지만요."));
            await ctx.ui.Talk(T("이수아", "바로편의점", "무슨 일 있으면 가게로 오세요. 근데 저도 언제까지 일할지는 몰라요. 본가가 지방이라서요."));
            G.rel["clerk"].contact = "store"; G.rel["clerk"].trust += 1;
            ctx.Note("수아: 건전지 내일 오전 입고 예정(확실하지 않음). 정전 시 현금만 받을 수도 있음.", "neighbor", "store");
        }

        public static async Task DoBag(Ctx ctx)
        {
            var G = ctx.G;
            ctx.Objective("비상가방 꾸리기", "현관의 배낭에 들고 나갈 물건을 담습니다.", new List<ObjectiveStep>());
            await ctx.Roam(new RoamOptions { on = new Dictionary<string, Func<Point, Task<object>>> { ["homeBag"] = _ => Task.FromResult<object>("go") } });
            var mode = Drills.LadderStage(G, "bag");
            var items = BagItems(G, true);
            var goals = Drills.BAG_GOALS["alert"];
            var res = await ctx.Modal(() => ctx.ui.Bag(new BagOptions { mode = mode, context = "alert", limit = Items.BAG_LIMIT, items = items, goals = goals, tips = Tips("bag_days", "bag_nocook", "bag_copies", "bag_foil", "bag_door") }));
            ApplyBag(G, res?.bag ?? new Dictionary<string, int>());
            var ev = Drills.EvalBag(G, "alert");
            var missing = ev.missing ?? new List<string>();
            Drills.RecordEncounter(G, "bag", missing.Count > 0 ? "partial" : "complete");
            G.know.bagPacked = G.items.Values.Any(e => e.bag > 0);
            ctx.SetLevelState("apartment", "bag", G.know.bagPacked ? "packed" : "empty");
            ctx.Event("bag_packed", Details.Of("weightKg", ev.weightKg, "missing", missing.Cast<object>().ToList()));
            ctx.Toast(missing.Count > 0 ? $"배낭을 현관에 두었다. 빠진 것: {string.Join(", ", missing.Select(id => Drills.BAG_GOALS["alert"].FirstOrDefault(g => g.id == id)?.label ?? id))}" : "배낭을 현관에 두었다. 필요한 것을 고루 담았다.");
        }

        // Moves items between home and bag so the bag holds exactly target { id: units }.
        public static void ApplyBag(Game G, IDictionary<string, int> target)
        {
            foreach (var kv in G.items.ToList())
            {
                var v = kv.Value;
                var want = Math.Max(0, Math.Min(target != null && target.TryGetValue(kv.Key, out var t) ? t : 0, v.home + v.bag));
                if (want > v.bag) State.Pack(G, kv.Key, want - v.bag); else if (want < v.bag) State.Unpack(G, kv.Key, v.bag - want);
            }
        }
    }
}
