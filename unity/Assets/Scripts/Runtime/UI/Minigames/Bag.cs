// Ports ui/bag.js: the bag-packing minigame (home list -> bag list, weight and volume meters, goal ticks).
// The story uses the 3D version (Grab3d.Bag3d); this 2D panel is the fallback and shares GoalStatus.
// guided shows item tips inline, hint behind a button, free shows nothing extra.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    // goalStatus row: ok null = not checked (free mode / no keywords).
    public sealed class GoalLine
    {
        public string label;
        public bool? ok;
    }

    public static class Bag
    {
        static readonly Dictionary<string, string> TITLE = new Dictionary<string, string> { ["alert"] = "비상가방 꾸리기", ["water"] = "물 받으러 갈 준비", ["evac"] = "대피 가방 하나" };
        static readonly Dictionary<string, string> TAG = new Dictionary<string, string> { ["water"] = "물", ["food"] = "식품", ["info"] = "정보", ["power"] = "전원", ["light"] = "빛", ["health"] = "건강", ["hygiene"] = "위생", ["warmth"] = "보온", ["tool"] = "도구", ["docs"] = "서류", ["fire"] = "화기 주의", ["spoil"] = "상하기 쉬움" };
        static readonly HashSet<string> WARN = new HashSet<string> { "fire", "spoil" };

        // Which items tick a goal line, matched on the director's wording. A line ticks when every group it
        // mentions has something in the bag. ponytail: keyword match; pass goal objects if wording drifts.
        static readonly (Regex re, string[] ids)[] GOAL_WORDS =
        {
            (new Regex("물(?!통)"), new[] { "water_500", "water_2l" }), (new Regex("음식|식량"), new[] { "bars", "canned", "rice_instant" }), (new Regex("빛|손전등"), new[] { "flashlight" }),
            (new Regex("정보|라디오"), new[] { "radio", "powerbank" }), (new Regex("약|구급"), new[] { "meds", "first_aid" }), (new Regex("연락"), new[] { "contact_card" }), (new Regex("신분증|서류|사본"), new[] { "id_card", "doc_copies" }),
            (new Regex("현금"), new[] { "cash" }), (new Regex("보온|담요|여벌"), new[] { "blanket", "clothes", "raincoat" }), (new Regex("위생"), new[] { "hygiene_kit", "wipes", "tissue", "masks" }), (new Regex("물통"), new[] { "water_jug" }),
        };

        // Goal lines with a tick each. bagOf(id) = units in the bag; an item marked ready = false does not count.
        // A goal with no id and no items stands for a plain wording line (JS string goal).
        public static List<GoalLine> GoalStatus(IList<BagGoal> goals, string mode, Func<string, int> bagOf, IList<BagItem> items)
        {
            bool InBag(string id) => bagOf(id) > 0 && items.FirstOrDefault(i => i.id == id)?.ready != false;
            return (goals ?? new List<BagGoal>()).Select(g =>
            {
                bool text = IsText(g);
                var groups = text ? GOAL_WORDS.Where(w => w.re.IsMatch(g.label ?? "")).Select(w => w.ids).ToList() : new List<string[]> { g.items.ToArray() };
                if (mode == "free" || groups.Count == 0) return new GoalLine { label = g.label, ok = null };
                return new GoalLine { label = g.label, ok = text ? groups.All(ids => ids.Any(InBag)) : Drills.GoalMet(g, id => bagOf(id), InBag) };
            }).ToList();
        }

        // JS goals may be plain strings; in C# that is a BagGoal carrying only its label.
        static bool IsText(BagGoal g) => g.id == null && (g.items == null || g.items.Count == 0);

        static int Units(int n) => Math.Max(0, n);

        public static Task<BagResult> Open(BagOptions o)
        {
            MinigameStyle.Register();
            o ??= new BagOptions();
            string mode = o.mode ?? "guided", context = o.context ?? "alert";
            var limit = o.limit ?? new BagLimit { weightKg = 8, volumeL = 25 };
            var items = o.items ?? new List<BagItem>();
            var goals = o.goals ?? new List<BagGoal>();
            var tips = o.tips ?? new List<string>();
            var st = new Dictionary<string, Dictionary<string, int>>();
            foreach (var i in items) st[i.id] = new Dictionary<string, int> { ["home"] = Units(i.home), ["bag"] = Units(i.bag) };
            (double w, double v) Load() => items.Aggregate((w: 0.0, v: 0.0), (a, i) => (a.w + i.weightKg * st[i.id]["bag"], a.v + i.volumeL * st[i.id]["bag"]));
            BagResult Result() { var r = new BagResult(); foreach (var i in items) r.bag[i.id] = st[i.id]["bag"]; return r; }
            var title = TITLE.TryGetValue(context, out var t) ? t : "가방 꾸리기";
            ModalHandle<BagResult> m = null;
            m = MiniDom.Modal<BagResult>($"mg-bag is-{mode}", title, null, e => { if (Mg.Key(e) == "Escape") { m.Close(Result()); return true; } return false; });

            var lists = new Dictionary<string, VisualElement> { ["home"] = Mg.El("ul", "mg-list"), ["bag"] = Mg.El("ul", "mg-list") };
            var counts = new Dictionary<string, Label> { ["home"] = Mg.Text(null), ["bag"] = Mg.Text(null) };
            var meters = new[] { "w", "v" }.Select(k => (k, bar: Mg.Cls(new VisualElement(), "mg-bar-fill"), text: Mg.Text(null))).ToList();
            var goalList = Mg.El("ul", "mg-goals");
            Button doneBtn = null;

            void Move(string id, string from, int n)
            {
                var to = from == "home" ? "bag" : "home";
                int k = Math.Min(n, st[id][from]);
                if (k == 0) return;
                st[id][from] -= k; st[id][to] += k;
                var it = items.First(i => i.id == id);
                Render(from, id);
                var (w, _) = Load();
                MiniDom.Announce($"{it.name} {k}개를 {(to == "bag" ? "가방에 넣었다" : "집에 두었다")}. 가방 {MiniDom.FmtKg(w)}");
            }

            VisualElement Row(BagItem it, string where)
            {
                int n = st[it.id][where];
                var tags = (it.tags ?? new List<string>()).Where(x => TAG.ContainsKey(x)).Distinct().ToList();
                var btn = Mg.Btn("mg-row", null,
                    where == "bag" ? Mg.Text("mg-arrow", "←") : null,
                    Mg.El("span", "mg-row-main",
                        Mg.El("b", null, it.name, Mg.Text("mg-count", $" ×{n}")),
                        Mg.El("span", "mg-badges", Mg.Text("mg-spec", $"{MiniDom.FmtKg(it.weightKg)} · {Mg.Num(it.volumeL)}L"),
                            tags.Select(x => Mg.El("span", "mg-badge" + (WARN.Contains(x) ? " is-low" : ""), WARN.Contains(x) ? "! " : null, TAG[x])).ToList()),
                        !string.IsNullOrEmpty(it.tip) && mode != "free" ? Mg.Text("mg-tip", it.tip) : null),
                    where == "home" ? Mg.Text("mg-arrow", "→") : null);
                btn.userData = it.id;
                btn.tooltip = $"{it.name} {n}개 · {MiniDom.FmtKg(it.weightKg)}씩 · {(where == "home" ? "가방에 하나 넣기" : "하나 꺼내 두기")}";
                Mg.OnClick(btn, shift => Move(it.id, where, shift ? int.MaxValue : 1));
                return Mg.El("li", null, btn);
            }

            List<Button> RowsOf(string where) => lists[where].Query<Button>(className: "mg-row").ToList();

            void Render(string focusWhere = null, string focusId = null)
            {
                int idx = focusWhere != null ? RowsOf(focusWhere).FindIndex(b => (string)b.userData == focusId) : -1;
                foreach (var where in new[] { "home", "bag" })
                {
                    var shown = items.Where(i => st[i.id][where] > 0).ToList();
                    Mg.Replace(lists[where], shown.Select(i => Row(i, where)).ToList());
                    if (shown.Count == 0) lists[where].Add(Mg.El("li", "mg-empty", where == "bag" ? "아직 비어 있다." : "집에 남은 물건이 없다."));
                    counts[where].text = $"{shown.Sum(i => st[i.id][where])}개";
                }
                var (w, v) = Load();
                foreach (var mt in meters)
                {
                    double val = mt.k == "w" ? w : v, max = mt.k == "w" ? limit.weightKg : limit.volumeL;
                    var s = MiniDom.MeterState(val, max);
                    mt.bar.style.width = Length.Percent((float)Math.Min(100, val / max * 100));
                    MinigameStyle.BarState(mt.bar, s);
                    mt.text.text = $"{(mt.k == "w" ? MiniDom.FmtKg(val) : $"{Mg.Fixed1(val)}L")} / {(mt.k == "w" ? MiniDom.FmtKg(max) : $"{Mg.Num(max)}L")} · {MiniDom.METER_WORD[s]}";
                }
                Mg.Replace(goalList, GoalStatus(goals, mode, id => st.TryGetValue(id, out var x) ? x["bag"] : 0, items).Select(g => g.ok == null
                    ? Mg.El("li", "mg-goal is-plain", Mg.Text("mg-goal-mark", "·"), g.label)
                    : Mg.El("li", g.ok == true ? "mg-goal is-done" : "mg-goal", Mg.Text(g.ok == true ? "mg-goal-mark is-done" : "mg-goal-mark", g.ok == true ? "✓" : "○"), g.label)).ToList());
                if (focusWhere == null) return;
                var rowsNow = RowsOf(focusWhere);
                VisualElement target = rowsNow.FirstOrDefault(b => (string)b.userData == focusId)
                    ?? (rowsNow.Count > 0 ? rowsNow[Math.Min(Math.Max(idx, 0), rowsNow.Count - 1)] : null)
                    ?? RowsOf(focusWhere == "home" ? "bag" : "home").FirstOrDefault() ?? (VisualElement)doneBtn;
                target?.Focus();
            }

            foreach (var whereKey in new[] { "home", "bag" })
            {
                var where = whereKey;
                lists[where].RegisterCallback<KeyDownEvent>(e =>
                {
                    var btn = (e.target as VisualElement)?.GetFirstOfType<Button>();
                    if (btn == null || !btn.ClassListContains("mg-row")) return;
                    var id = (string)btn.userData;
                    var key = Mg.Key(e);
                    if ((where == "home" && key == "ArrowRight") || (where == "bag" && (key == "ArrowLeft" || key == "Backspace" || key == "Delete"))) { e.StopPropagation(); Move(id, where, e.shiftKey ? int.MaxValue : 1); }
                    else if (key == "Enter" && e.shiftKey) { e.StopPropagation(); Move(id, where, int.MaxValue); }
                    else if (key == "ArrowDown" || key == "ArrowUp")
                    {
                        e.StopPropagation();
                        var all = RowsOf(where); int i = all.IndexOf(btn);
                        if (all.Count > 0) all[Math.Max(0, Math.Min(all.Count - 1, i + (key == "ArrowDown" ? 1 : -1)))].Focus();
                    }
                    else if ((where == "home" && key == "ArrowLeft") || (where == "bag" && key == "ArrowRight"))
                    {
                        e.StopPropagation();
                        ((VisualElement)RowsOf(where == "home" ? "bag" : "home").FirstOrDefault() ?? btn).Focus();
                    }
                }, TrickleDown.TrickleDown);
            }

            doneBtn = Mg.Btn("mg-btn mg-primary", () => m.Close(Result()), "다 챙겼다");
            var empty = Mg.Btn("mg-btn mg-ghost", () =>
            {
                foreach (var i in items) { st[i.id]["home"] += st[i.id]["bag"]; st[i.id]["bag"] = 0; }
                Render(); MiniDom.Announce("가방을 비웠다.");
            }, "가방 비우기");

            var help = MiniDom.LadderHelp(mode, () => new List<VisualElement> {
                Mg.El("p", null, "목표 목록에서 ○ 표시가 남은 것을 먼저 채워 보세요. 무게와 부피 막대가 넘치지 않게."),
                tips.Count > 0 ? Mg.El("ul", "mg-tips", tips.Select(x => Mg.El("li", null, "· " + x)).ToList()) : null,
                mode == "hint" ? Mg.El("p", "mg-why", "각 물건 아래에 도움말이 함께 보입니다.") : null,
            }.Where(x => x != null).ToList());

            MiniDom.Put(m.root,
                MiniDom.Header("가방", title, mode, "mg-bag-title"),
                Mg.El("div", "mg-bag-grid",
                    Mg.El("section", "mg-pane", Mg.El("h3", null, "집 비축 ", counts["home"]), lists["home"]),
                    Mg.El("section", "mg-pane is-bag", Mg.El("h3", null, "가방 ", counts["bag"]), lists["bag"]),
                    Mg.El("aside", "mg-side",
                        meters.Select(mt => Mg.El("div", "mg-meter", Mg.El("small", null, mt.k == "w" ? "⚖ 무게" : "▣ 부피"), mt.text,
                            Mg.El("span", "mg-bar", mt.bar))).ToList(),
                        Mg.El("p", "mg-why", $"무게가 {MiniDom.FmtKg(limit.weightKg)}을 넘으면 걸음이 느려집니다."),
                        goals.Count > 0 ? Mg.El("h3", null, "챙길 것") : null, goalList,
                        help)),
                Mg.El("footer", "mg-foot", Mg.El("p", "mg-keys", "누르기(Enter) 한 개 · Shift+Enter 전부 · → 넣기 · ← 꺼내기 · ↑↓ 이동 · Esc 닫기"), Mg.El("div", "mg-actions", empty, doneBtn)));
            Render();
            return m.Done;
        }
    }
}
