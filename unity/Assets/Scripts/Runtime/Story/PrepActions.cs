// Ports story/prep-actions.js: the preparation actions — the mart run, checking the shelter and routes, the
// home equipment check, the contact card, the neighbours, packing the bag — and the helpers later chapters
// reuse (applyHome, goOut, bagItems, applyBag). Each is played in 3D (walk there, look, decide).
// Docs, neighbours and the bag are in PrepActionsHome.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.Story
{
    public static partial class PrepActions
    {
        public static readonly string[] SEOYEON_CARD = { "보행 보조기를 씁니다", "평지와 완만한 경사로는 혼자 갑니다", "계단과 가파른 내리막은 혼자 내려가지 않습니다", "보조기와 떨어지지 않습니다", "도움이 필요하면 제가 말합니다" };
        public const double RADIO_FREQ = 102.1;   // game frequency of the disaster-broadcast station (fictional)

        static bool HasAt(Game G, string id, string where = "home") => G.items.TryGetValue(id, out var e) && e != null && (where == "home" ? e.home : e.bag) > 0;
        static bool Owned(Game G, string id) => HasAt(G, id) || HasAt(G, id, "bag");
        static string F(string id) => Facts.Fact(id)?.text;
        static List<string> Tips(params string[] ids) => ids.Select(F).Where(s => !string.IsNullOrEmpty(s)).ToList();
        static TalkOptions T(string speaker, string role, params string[] lines) => new TalkOptions { speaker = speaker, role = role, lines = lines.ToList() };
        static TalkOptions T(string speaker, string role, string[] lines, params Choice[] choices) => new TalkOptions { speaker = speaker, role = role, lines = lines.ToList(), choices = choices.ToList() };
        static readonly Task<object> Keep = Task.FromResult<object>(null);
        static string Freq => RADIO_FREQ.ToString(CultureInfo.InvariantCulture);

        // Shows home stock, bag, radio and wallet in the apartment from G (level states).
        public static void ApplyHome(Ctx ctx)
        {
            var G = ctx.G;
            var stock = G.items.ToDictionary(kv => kv.Key, kv => (object)kv.Value.home);
            ctx.SetLevelState("apartment", "stock", stock);
            ctx.SetLevelState("apartment", "bag", G.know.bagPacked ? "packed" : "empty");
            ctx.SetLevelState("apartment", "radio", HasAt(G, "radio") || HasAt(G, "radio", "bag") ? (G.know.radioOn ? "on" : "off") : "none");
            ctx.SetLevelState("apartment", "power", ctx.power ? "on" : "off");
        }

        // Back home after an errand: the walk takes time, heavier with shopping.
        public static async Task GoHome(Ctx ctx, double carryKg = 0)
        {
            var minutes = 6 + Math.Floor(Math.Max(0, carryKg - 6) * 1.5 + 0.5);
            ctx.Pass(minutes);
            await ctx.Goto("apartment", "home");
        }

        // Leave 302호 for the town with an objective; resolves once in town. With power on the elevator takes you straight down.
        public static async Task GoOut(Ctx ctx, string title, string body)
        {
            ctx.Objective(title, body, new List<ObjectiveStep>());
            if (ctx.engine.Level?.id == "town") return;
            await ctx.Roam(new RoamOptions
            {
                on = new Dictionary<string, Func<Point, Task<object>>>
                {
                    ["homeExitDoor"] = async _ =>
                    {
                        if (!ctx.power) { await ctx.Goto("apartment", "corridor3F"); return null; }
                        var pick = await ctx.ui.Talk(T("현관", "302호", new[] { "나가자." }, new Choice("lift", "엘리베이터로 1층까지"), new Choice("hall", "복도로 나간다")));
                        if (pick == "lift") { await ctx.Goto("town", "home"); return "out"; }
                        await ctx.Goto("apartment", "corridor3F");
                        return null;
                    },
                    ["homeDoor"] = async _ => { await ctx.Goto("apartment", "homeEntry"); return null; },
                    ["elevator"] = async _ =>
                    {
                        if (!ctx.power) { ctx.Toast("엘리베이터가 멈춰 있다. 계단을 이용하자."); return null; }
                        await ctx.Goto("apartment", ctx.engine.Player.Y > 1 ? "lobby" : "corridor3F");
                        return null;
                    },
                },
                tick = _ => ctx.engine.Level?.id == "town" ? "out" : null,
            });
        }

        // Bag UI items from G: everything at home or already packed. ready = usable now (checked, or needs no check).
        public static List<BagItem> BagItems(Game G, bool withTips = false) =>
            G.items.Where(kv => Items.CATALOG.ContainsKey(kv.Key) && kv.Value.home + kv.Value.bag > 0).Select(kv =>
            {
                var c = Items.CATALOG[kv.Key]; var v = kv.Value;
                return new BagItem { id = kv.Key, name = c.name, weightKg = c.weightKg, volumeL = c.volumeL, home = v.home, bag = v.bag,
                    tip = withTips ? F(c.facts?.FirstOrDefault()) ?? "" : "", tags = new[] { c.category, c.risk }.Where(s => !string.IsNullOrEmpty(s)).ToList(), ready = c.needsCheck == null || v.@checked };
            }).ToList();

        static List<ShopItem> ShopItems(Game G, string section, List<string> soldOut) =>
            Items.MART_SECTIONS[section].items.Select(id =>
            {
                var c = Items.CATALOG[id];
                var note = c.needsCheck == "batteries" ? "건전지 별매" : c.needsCheck == "charge" ? "사용 전 충전 필요" : c.risk == "fire" ? "화기 주의" : c.needs != null && c.needs.Contains("hot_water") ? "끓는 물 필요" : "";
                return new ShopItem { id = id, name = c.name, price = c.price, unit = c.unit, weightKg = c.weightKg, volumeL = c.volumeL, stock = soldOut.Contains(id) ? "out" : id == "water_2l" ? "low" : "ok", note = note, label = "" };
            }).ToList();

        static List<string> SoldOutAt(Game G, int slot)
        {
            var so = G.cond?.soldOut ?? new SoldOut();
            var list = slot <= 0 ? new List<string>() : slot <= 2 ? (so.early ?? new List<string>()) : (so.late ?? so.early ?? new List<string>());
            return list.SelectMany(x => Items.MART_SECTIONS.TryGetValue(x, out var s) ? s.items : new List<string> { x }).ToList();
        }

        public static async Task DoMart(Ctx ctx, int slot)
        {
            var G = ctx.G;
            var soldOut = SoldOutAt(G, slot);
            ctx.SetLevelState("mart", "crowd", "busy");
            ctx.SetLevelState("mart", "soldOut", Items.MART_SECTIONS.Keys.Where(s => Items.MART_SECTIONS[s].items.All(soldOut.Contains)).ToList());
            await GoOut(ctx, "연무마트에 가기", "단지 정문으로 나가 연무로 건너편 연무마트로 갑니다.");
            ctx.Objective("장보기", "진열대에서 고르고 계산대에서 계산합니다. 필요한 만큼, 들고 올 수 있는 만큼.", new List<ObjectiveStep> { new ObjectiveStep("진열대 둘러보기", false), new ObjectiveStep("계산하기", false) });
            double carried = 0; bool boughtAny = false, hoard = false;
            var info = new List<InfoLine> { new InfoLine { text = "생수 1인 6병 한정", label = "site" } };
            if (G.rel.TryGetValue("clerk", out var clerk) && !string.IsNullOrEmpty(clerk?.contact)) info.Add(new InfoLine { text = "수아: \"건전지는 오늘 오전에 더 들어온다고 했어요. 확실하진 않아요.\"", label = "neighbor" });
            Func<Point, Task<object>> Section(string sec) => async _ =>
            {
                var res = await ctx.ui.Shop(new ShopOptions { title = Items.MART_SECTIONS[sec].label, money = G.money, items = ShopItems(G, sec, soldOut), info = info, budgetNote = $"남은 돈 {World.Won(G.money)}원" });
                if (res == null || res.total == 0) return null;
                var cart = new Dictionary<string, int>(res.cart ?? new Dictionary<string, int>());
                if (cart.TryGetValue("water_2l", out var w) && w != 0) { var already = G.items.TryGetValue("water_2l", out var we) && we != null ? we.bought : 0; cart["water_2l"] = Math.Max(0, Math.Min(w, 6 - already)); }
                foreach (var id in cart.Keys.ToList()) if (cart[id] == 0 || soldOut.Contains(id)) cart.Remove(id);
                if (cart.Count == 0) { ctx.Toast("담을 수 있는 물건이 없습니다.", "site"); return null; }
                if (cart.Values.Any(q => q > 8)) hoard = true;
                var prices = cart.Keys.ToDictionary(id => id, id => Items.CATALOG[id].price);
                try { State.Buy(G, cart, prices); }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { ctx.Toast(string.IsNullOrEmpty(e.Message) ? "살 수 없습니다." : e.Message, "site"); return null; }
                boughtAny = true;
                carried += cart.Sum(kv => Items.CATALOG[kv.Key].weightKg * kv.Value);
                ctx.Event("bought", Details.Of("cart", cart.ToDictionary(kv => kv.Key, kv => (object)kv.Value), "slot", slot));
                return null;
            };
            await ctx.Roam(new RoamOptions
            {
                on = new Dictionary<string, Func<Point, Task<object>>>
                {
                    ["shelfWater"] = Section("water"), ["shelfFood"] = Section("food"), ["shelfDaily"] = Section("daily"), ["shelfElec"] = Section("elec"), ["shelfHealth"] = Section("health"),
                    ["atm"] = async _ =>
                    {
                        var pick = await ctx.ui.Talk(T("ATM", "연무마트 입구", new[] { $"잔액 {World.Won(G.money)}원. 현금을 뽑을까?" },
                            new Choice("1", "5만 원") { disabled = G.money < 50000 }, new Choice("2", "10만 원") { disabled = G.money < 100000 }, new Choice("no", "그만")));
                        if (pick == "1" || pick == "2")
                        {
                            var n = int.Parse(pick, CultureInfo.InvariantCulture);
                            State.Buy(G, new Dictionary<string, int> { ["cash"] = n }, new Dictionary<string, int> { ["cash"] = 50000 });
                            ctx.Event("cash", Details.Of("n", n)); ctx.Toast($"현금 {n * 5}만 원을 찾았다.");
                        }
                        return null;
                    },
                    ["checkout"] = async _ =>
                    {
                        if (!boughtAny) { ctx.Toast("아직 고른 물건이 없습니다."); return null; }
                        if (hoard)
                        {
                            await ctx.ui.Talk(T("계산원", "연무마트", "오늘 많이들 사 가셔서요… 다른 분들 몫도 남겨 주시면 감사하겠습니다."));
                            ctx.Event("hoard", new Dictionary<string, object>()); ctx.Toast(F("home_nohoard"), "official", 6000);
                        }
                        await ctx.ui.Talk(T("계산 완료", $"남은 돈 {World.Won(G.money)}원", $"산 물건 무게 약 {carried.ToString("F1", CultureInfo.InvariantCulture)}kg. {(carried > 12 ? "한 번에 들고 가기 버겁다." : "들고 갈 만하다.")}"));
                        return "done";
                    },
                    ["martExit"] = async _ =>
                    {
                        if (boughtAny) { ctx.Toast("계산대에서 계산을 마치세요."); return null; }
                        var pick = await ctx.ui.Talk(T("연무마트", "출입구", new[] { "아무것도 사지 않고 나갈까?" }, new Choice("yes", "나간다"), new Choice("no", "더 둘러본다")));
                        return pick == "yes" ? "done" : null;
                    },
                },
            });
            if (carried > 12) ctx.Toast("무거워서 두 번에 나눠 옮겼다.");
            await GoHome(ctx, carried);
        }

        static readonly Dictionary<string, string[]> ENTRANCE_TEXT = new Dictionary<string, string[]>
        {
            ["shelterEast"] = new[] { "동쪽 출입구 · 생활문화센터 정문", "유리문 안쪽 계단과 엘리베이터로 지하주차장에 내려간다. 평소 가장 많이 쓰는 문이다." },
            ["shelterA"] = new[] { "출입구 A · 계단", "지하주차장으로 바로 내려가는 계단 출입구. 계단 양쪽에 손잡이가 있다." },
            ["shelterB"] = new[] { "출입구 B · 지하주차장 진출입로", "차가 드나드는 경사로다. 경사가 가파르고, 평소에는 차가 다닌다." },
            ["shelterC"] = new[] { "출입구 C · 경사로", "완만한 경사로와 양쪽 손잡이. 셔터가 내려져 있고 안내문이 붙어 있다: \"공사 중 · 10월 말 개방 예정. 비상시에는 현장 안내에 따라 열 수 있습니다.\"" },
        };

        public static async Task DoShelterCheck(Ctx ctx)
        {
            var G = ctx.G;
            await GoOut(ctx, "생활문화센터 대피소 확인하기", "연무로를 따라 서쪽, 연무 생활문화센터로 갑니다.");
            var seen = new List<string>(); bool board = false;   // insertion-ordered set
            List<ObjectiveStep> Steps() => new List<ObjectiveStep> { new ObjectiveStep($"출입구 살펴보기 ({seen.Count}/4)", seen.Count >= 3), new ObjectiveStep("현장 안내판 읽기", board) };
            ctx.Objective("출입구와 안내판 확인", "각 출입구 앞에서 E로 살펴봅니다.", Steps());
            Func<Point, Task<object>> Look(string id) => async _ =>
            {
                var title = ENTRANCE_TEXT[id][0]; var text = ENTRANCE_TEXT[id][1];
                await ctx.ui.Talk(T(title, "현장 확인", text));
                if (!seen.Contains(id)) { seen.Add(id); ctx.Note($"{title}: {text}", "site", "culture"); }
                ctx.Objective("출입구와 안내판 확인", "각 출입구 앞에서 E로 살펴봅니다.", Steps());
                return seen.Count >= 3 && board ? "done" : null;
            };
            await ctx.Roam(new RoamOptions
            {
                doors = false,
                on = new Dictionary<string, Func<Point, Task<object>>>
                {
                    ["shelterEast"] = Look("shelterEast"), ["shelterA"] = Look("shelterA"), ["shelterB"] = Look("shelterB"), ["shelterC"] = Look("shelterC"),
                    ["shelterNotice"] = async _ =>
                    {
                        await ctx.ui.Card(new CardOptions { kind = "notice", title = "대피소", meta = "빨간 바탕의 대피소 표지판 · SHELTER",
                            body = new List<string> { "연무 생활문화센터 지하주차장 · 민방위 대피소", "출입구: 동쪽 정문, A 계단, B 지하주차장 진출입로 (C 경사로 공사 중)", F("shelter_signs") },
                            actions = new List<Choice> { new Choice("ok", "확인", true) } });
                        board = true; ctx.Note("생활문화센터 대피소 출입구: 동쪽 정문, A 계단, B 진출입로 (C 공사 중)", "site", "culture");
                        ctx.Objective("출입구와 안내판 확인", "각 출입구 앞에서 E로 살펴봅니다.", Steps());
                        return seen.Count >= 3 ? "done" : null;
                    },
                },
            });
            var options = Routes.RouteOptions(G, "home", "shelter", new List<string>(), new Dictionary<string, bool> { ["east"] = true, ["stairs"] = true, ["ramp"] = true, ["access"] = false }, "walk");
            var mode = Drills.LadderStage(G, "route");
            var res = await ctx.Modal(() => ctx.ui.Routemap(new RouteMapOptions { mode = mode, from = "home", to = "shelter", options = options, closures = new List<string>(),
                gates = new Dictionary<string, string> { ["east"] = "open", ["stairs"] = "open", ["ramp"] = "open", ["access"] = "closed" }, known = new KnownMap(), playerAt = new XZ(ctx.engine.Player.X, ctx.engine.Player.Z) }));
            Drills.RecordEncounter(G, "route", "planned");
            G.know.shelterLocated = true; G.know.entrancesChecked = seen.Count >= 3; G.know.routeChecked = true; G.know.altRoute = options.Count > 1;
            var ci = res?.choice ?? 0;
            G.know.plannedRoute = ci >= 0 && ci < options.Count ? options[ci]?.nodes : null;
            State.RecordEvent(G, "shelter_checked", Details.Of("seen", seen.Cast<object>().ToList(), "board", board));
            ctx.Note("집 → 대피소 경로를 확인했다. 막히면 돌아갈 길도 지도에서 봐 두었다.", "self");
        }

        public static async Task DoEquip(Ctx ctx)
        {
            var G = ctx.G;
            bool radio = false, light = false, power = false, ext = false, gas = false, breaker = false;
            List<ObjectiveStep> Steps() => new List<ObjectiveStep> {
                new ObjectiveStep("라디오 (거실 탁자)", radio), new ObjectiveStep("손전등 (베란다 수납장)", light), new ObjectiveStep("보조배터리 (책상)", power),
                new ObjectiveStep("소화기 위치 (3층 복도)", ext), new ObjectiveStep("가스 밸브 (주방)", gas), new ObjectiveStep("분전반 (현관)", breaker) };
            void Show() => ctx.Objective("집 장비 점검", "다 마치면 달력(계획판)에서 끝냅니다.", Steps());
            Show();
            await ctx.Roam(new RoamOptions
            {
                on = new Dictionary<string, Func<Point, Task<object>>>
                {
                    ["homeRadio"] = async _ =>
                    {
                        if (radio) { ctx.Toast("라디오는 점검을 마쳤다."); return null; }
                        if (!Owned(G, "radio")) { await ctx.ui.Talk(T("거실 탁자", "라디오 자리", "라디오가 없다. 정전되면 방송을 들을 방법을 따로 찾아야 한다.")); radio = true; ctx.Event("radio_missing", new Dictionary<string, object>()); Show(); return null; }
                        var ok = State.Check(G, "radio");
                        if (!ok.ok) { await ctx.ui.Talk(T("휴대용 라디오", "건전지식", "건전지가 없어 켜지지 않는다. 라디오는 있지만 지금은 쓸 수 없다.")); ctx.Event("radio_blocked", Details.Of("why", "batteries")); return null; }
                        var res = await ctx.ui.Radio(new RadioOptions { mode = "guided", start = 89.5, battery = true, extraSeconds = 0, stations = new List<Station> {
                            new Station { freq = 95.1, name = "음악 방송", kind = "music", lines = new List<string> { "(잔잔한 음악이 흐른다)" } },
                            new Station { freq = RADIO_FREQ, name = $"재난방송 주관방송사 라디오 (게임 속 주파수 {Freq}MHz)", kind = "official", lines = new List<string> { "지금은 평상시 프로그램입니다.", "비상시에는 이 방송에서 정부와 지자체 안내를 전합니다. (가상 방송)" } },
                        } });
                        Drills.RecordEncounter(G, "radio", res != null && res.official ? "tuned" : "opened");
                        G.know.radioChecked = true; if (res != null && res.official) G.know.radioFreq = RADIO_FREQ;
                        ctx.Note($"라디오 점검 완료. 재난방송 주파수 {Freq}MHz에 맞춰 두었다.", "self");
                        ctx.Toast(F("radio_batt"), "official", 5000);
                        radio = true; Show();
                        return null;
                    },
                    ["homeStock"] = async _ =>
                    {
                        if (light) { ctx.Toast("손전등은 점검을 마쳤다."); return null; }
                        if (!State.Check(G, "flashlight").ok) { await ctx.ui.Talk(T("손전등", "베란다 수납장", "손전등은 있는데 건전지가 없다. 켜지지 않는다.")); ctx.Event("light_blocked", new Dictionary<string, object>()); return null; }
                        light = true; ctx.Toast("손전등이 켜진다. 현관 신발장 위에 두었다."); Show();
                        return null;
                    },
                    ["homeDesk"] = _ =>
                    {
                        if (power) { ctx.Toast("보조배터리는 충전해 두었다."); return Keep; }
                        if (!Owned(G, "powerbank")) { ctx.Toast("보조배터리가 없다. 휴대폰을 완충해 두는 수밖에 없다."); G.phone.battery = 100; power = true; Show(); return Keep; }
                        State.Check(G, "powerbank"); G.phone.battery = 100; power = true; ctx.Toast("보조배터리와 휴대폰을 완충했다."); Show();
                        return Keep;
                    },
                    ["extinguisher"] = async _ =>
                    {
                        if (ext) { ctx.Toast("소화기 위치를 확인했다."); return null; }
                        await ctx.ui.Card(new CardOptions { kind = "notice", title = "3층 복도 소화기", meta = "옥내소화전 옆", body = new List<string> { F("ext_1"), F("ext_2"), F("ext_3") }, actions = new List<Choice> { new Choice("ok", "사용 순서 익혀 보기", true) } });
                        await ctx.ui.Extinguisher(new ExtinguisherOptions { mode = "guided", fire = "small", practice = true });
                        Drills.RecordEncounter(G, "extinguisher", "practice");
                        G.know.extinguisherLocated = true; ext = true; Show();
                        return null;
                    },
                    ["homeGas"] = async _ => { G.know.gasValve = true; gas = true; await ctx.ui.Talk(T("가스 중간 밸브", "주방", "레인지 옆 가스 중간 밸브 위치를 확인했다.", F("alert_caution"))); Show(); return null; },
                    ["homeBreaker"] = async _ => { G.know.breaker = true; breaker = true; await ctx.ui.Talk(T("분전반", "현관 위", "누전차단기와 개폐기가 있다.", F("power_home"))); Show(); return null; },
                    ["homePlanner"] = async _ =>
                    {
                        var pick = await ctx.ui.Talk(T("점검", "달력", new[] { "점검을 마칠까?" }, new Choice("yes", "마친다"), new Choice("no", "조금 더 한다")));
                        return pick == "yes" ? "done" : null;
                    },
                },
            });
            ctx.Event("equip_done", Details.Of("radio", radio, "light", light, "power", power, "ext", ext, "gas", gas, "breaker", breaker));
        }
    }
}
