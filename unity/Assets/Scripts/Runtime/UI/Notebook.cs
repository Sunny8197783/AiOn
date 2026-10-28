// Ports ui/notebook.js: 수첩 (G.info by day, filtered by label) and 소지품 (home stock and bag with the load meter).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class Notebook
    {
        static VisualElement El(string tag, string cls, params object[] kids) => Dom.El(tag, cls, kids);

        static string DayLabel(int d) => d < 0 ? "D" + d : d == 0 ? "D-day" : "D+" + d;
        static string Hhmm(int m) => ((int)Math.Floor(m / 60.0) % 24).ToString("00") + ":" + (m % 60).ToString("00");
        static readonly Dictionary<string, string> PLACE = new Dictionary<string, string>
        {
            ["office"] = "사무실", ["apartment"] = "집", ["town"] = "동네", ["mart"] = "연무마트", ["store"] = "바로편의점", ["shelter"] = "대피소", ["culture"] = "생활문화센터",
        };
        static readonly Dictionary<string, string> LABEL_WHY = new Dictionary<string, string>
        {
            ["official"] = "정부·지자체·재난방송·재난문자",
            ["site"] = "내 눈으로 직접 본 것",
            ["neighbor"] = "아는 사람에게 들은 것",
            ["rumor"] = "누가 처음 말했는지 모르는 것",
            ["self"] = "내가 정리하거나 한 일",
        };
        static readonly string[] LABEL_KEYS = { "official", "site", "neighbor", "rumor", "self" };   // Object.keys(LABELS)

        static string Lab(InfoNote e) => e.label != null && Dom.LABELS.ContainsKey(e.label) ? e.label : "self";

        static Button CloseButton<T>(Func<ModalHandle<T>> m)
        {
            var b = Dom.Btn("닫기", () => m().Close(), ghost: true, iconName: "close", className: "modal-close");
            b.tooltip = "닫기";
            return b;
        }

        // notebook(entries: [{ day, minute, text, label, place }])
        public static Task Show(IList<InfoNote> entries)
        {
            var list = (entries ?? new List<InfoNote>()).Where(e => e != null && !string.IsNullOrEmpty(e.text)).OrderBy(e => e.day).ThenBy(e => e.minute).ToList();
            string filter = "all";
            var counts = LABEL_KEYS.ToDictionary(k => k, k => list.Count(e => Lab(e) == k));
            var keys = new List<string> { "all" }; keys.AddRange(LABEL_KEYS);
            var filterBtns = new List<Button>();
            var body = El("scroll", "nb-pages");
            body.focusable = true; body.tabIndex = 0; body.tooltip = "수첩 기록";

            void Render()
            {
                var shown = list.Where(e => filter == "all" || Lab(e) == filter).ToList();
                if (shown.Count == 0) { Dom.Replace(body, El("p", "empty", list.Count > 0 ? "이 분류의 기록이 없습니다." : "아직 적은 것이 없습니다. 확인한 정보는 여기에 쌓입니다.")); return; }
                var days = new List<KeyValuePair<int, List<InfoNote>>>();
                foreach (var e in shown)
                {
                    int i = days.FindIndex(d => d.Key == e.day);
                    if (i < 0) { days.Add(new KeyValuePair<int, List<InfoNote>>(e.day, new List<InfoNote>())); i = days.Count - 1; }
                    days[i].Value.Add(e);
                }
                Dom.Replace(body, days.Select(d => El("section", "nb-day",
                    El("h3", null, DayLabel(d.Key)),
                    El("ol", null, d.Value.Select(e => El("li", "nb-entry nb-" + Lab(e),
                        El("time", "nb-time", Hhmm(e.minute)),
                        Dom.LabelTag(e.label),
                        El("p", "nb-text", e.text),
                        e.place != null && PLACE.TryGetValue(e.place, out var pl) ? El("small", "nb-place", pl) : null))))));
                if (body is ScrollView sv && sv.contentContainer.childCount > 0)
                {
                    var last = sv.contentContainer[sv.contentContainer.childCount - 1];
                    sv.schedule.Execute(() => sv.ScrollTo(last)).StartingIn(30);
                }
            }

            foreach (var k in keys)
            {
                var b = (Button)El("button", "nb-filter" + (k == "all" ? " all pressed" : ""),
                    k == "all" ? El("span", "tag tag-all", Dom.Icon("book"), "전체") : Dom.LabelTag(k),
                    El("span", "nb-count", (k == "all" ? list.Count : counts[k]).ToString()));
                var key = k;
                b.clicked += () =>
                {
                    filter = key;
                    for (int i = 0; i < filterBtns.Count; i++) UiStyle.Toggle(filterBtns[i], "pressed", keys[i] == key);
                    Render();
                };
                filterBtns.Add(b);
            }

            var legendList = El("ul", "nb-legend-list hidden", LABEL_KEYS.Select(k => El("li", "nb-legend-row", Dom.LabelTag(k), El("span", null, LABEL_WHY[k]))));
            var summary = (Button)El("button", "nb-summary", "출처 표시는 무엇을 뜻하나요?");
            summary.clicked += () => UiStyle.Hidden(legendList, !UiStyle.IsHidden(legendList));   // <details> open/close

            ModalHandle<bool> m = null;
            var head = El("header", "nb-head", El("p", "eyebrow", Dom.Icon("book"), "정민의 수첩"), El("h2", null, "확인한 것과 들은 것"));
            var filters = El("div", "nb-filters", filterBtns);
            filters.tooltip = "정보 출처로 거르기";
            var content = El("div", "nb", head, filters, body, El("div", "nb-legend", summary, legendList));
            m = Dom.Modal<bool>("notebook", "수첩", content, e => { if (UiFx.Key(e) == "Escape") { m.Close(); return true; } return false; });
            Dom.Append(head, CloseButton(() => m));
            Render();
            filterBtns[0].Focus();
            return m.Done;
        }

        static readonly Dictionary<string, string> CATEGORY = new Dictionary<string, string>
        {
            ["water"] = "물", ["food"] = "먹을 것", ["info"] = "정보", ["power"] = "전원", ["light"] = "불빛", ["health"] = "건강", ["hygiene"] = "위생",
            ["warmth"] = "보온", ["tool"] = "도구", ["fire"] = "화기", ["docs"] = "서류·현금",
        };
        static readonly List<string> ORDER = new List<string> { "water", "food", "info", "power", "light", "health", "hygiene", "warmth", "tool", "fire", "docs" };
        static readonly Dictionary<string, string> CHECK = new Dictionary<string, string> { ["batteries"] = "건전지를 넣어 봐야 쓸 수 있음", ["charge"] = "충전해 둬야 쓸 수 있음", ["tune"] = "주파수를 맞춰 봐야 함" };
        static int Count(ItemEntry v, string key) => v == null ? 0 : key == "home" ? v.home : key == "bag" ? v.bag : 0;
        static string Num(double v) => v.ToString(CultureInfo.InvariantCulture);

        // inventory({ home, bag, catalog, load: { weightKg, volumeL, over }, limit: { weightKg, volumeL } })
        public static Task Inventory(InventoryOptions o)
        {
            o ??= new InventoryOptions();
            var home = o.home ?? new Dictionary<string, ItemEntry>();
            var bag = o.bag ?? new Dictionary<string, ItemEntry>();
            var catalog = o.catalog ?? new Dictionary<string, CatalogItem>();
            var ids = home.Keys.Concat(bag.Keys).Distinct().ToList();

            VisualElement RowsFor(string where, Dictionary<string, ItemEntry> src)
            {
                var items = ids.Select(id => new
                {
                    n = Count(src.TryGetValue(id, out var v) ? v : null, where),
                    c = catalog.TryGetValue(id, out var c) && c != null ? c : new CatalogItem { name = id, unit = "개", category = "tool" },
                    entry = home.TryGetValue(id, out var h) && h != null ? h : bag.TryGetValue(id, out var b) ? b : null,
                })
                    .Where(x => x.n > 0)
                    // ponytail: ordinal name order (Hangul syllables are code-point ordered); CompareInfo("ko") if mixed scripts matter
                    .OrderBy(x => ORDER.IndexOf(x.c.category)).ThenBy(x => x.c.name ?? "", StringComparer.Ordinal).ToList();
                if (items.Count == 0) return El("p", "empty", where == "bag" ? "가방이 비어 있습니다." : "집에 남은 것이 없습니다.");
                return El("ul", "inv-list", items.Select(x =>
                {
                    var tags = new List<VisualElement>();
                    var c = x.c;
                    if (!string.IsNullOrEmpty(c.needsCheck))
                        tags.Add(x.entry != null && x.entry.@checked ? El("span", "inv-tag ok", Dom.Icon("check"), "점검 완료")
                            : El("span", "inv-tag warn", Dom.Icon("alert"), "점검 전 · " + (CHECK.TryGetValue(c.needsCheck, out var why) ? why : "확인 필요")));
                    if (c.risk == "fire") tags.Add(El("span", "inv-tag risk", Dom.Icon("flame"), "화기 주의"));
                    if (c.risk == "spoil") tags.Add(El("span", "inv-tag risk", Dom.Icon("hourglass"), "정전 땐 오래 못 둠"));
                    return El("li", "inv-row",
                        El("span", "inv-cat", c.category != null && CATEGORY.TryGetValue(c.category, out var cat) ? cat : "기타"),
                        El("b", "inv-name", c.name ?? ""),
                        El("span", "inv-qty", x.n + (string.IsNullOrEmpty(c.unit) ? "개" : c.unit)),
                        tags.Count > 0 ? El("span", "inv-tags", tags) : null);
                }));
            }

            VisualElement Meter(string label, double v, double max, string unit, bool over)
            {
                double pct = max != 0 ? Math.Min(100, v / max * 100) : 0;
                var state = over ? "over" : pct > 80 ? "near" : "ok";
                var fill = El("span", "meter-fill");
                fill.style.width = Length.Percent((float)pct);
                return El("div", "inv-meter " + state,
                    El("span", "inv-meter-label", label),
                    El("span", "meter", fill),
                    El("span", "inv-meter-value", v.ToString("0.0", CultureInfo.InvariantCulture) + " / " + Num(max) + unit + (over ? " · 초과" : pct > 80 ? " · 거의 참" : "")));
            }

            VisualElement loadBox = null;
            if (o.load != null && o.limit != null)
            {
                loadBox = El("div", "inv-load",
                    Meter("무게", o.load.weightKg, o.limit.weightKg, "kg", o.load.over != null && o.load.over.weight),
                    Meter("부피", o.load.volumeL, o.limit.volumeL, "L", o.load.over != null && o.load.over.volume));
                loadBox.tooltip = "가방 무게와 부피";
            }
            ModalHandle<bool> m = null;
            var close = CloseButton(() => m);
            var head = El("header", "nb-head", El("p", "eyebrow", Dom.Icon("box"), "소지품"), El("h2", null, "집 비축과 가방"));
            var content = El("div", "inv",
                head,
                El("div", "inv-panes",
                    El("scroll", "inv-pane", El("h3", null, Dom.Icon("home"), "집 비축"), RowsFor("home", home)),
                    El("scroll", "inv-pane", El("h3", null, Dom.Icon("bag"), "가방"), loadBox, RowsFor("bag", bag))),
                El("p", "inv-note", "산 것과 쓸 수 있는 것은 다릅니다. \"점검 전\" 물건은 미리 확인해 두어야 필요할 때 씁니다."));
            m = Dom.Modal<bool>("inventory", "소지품", content, e =>
            {
                if (UiFx.Key(e) == "Escape" || (UiFx.Code(e) == "KeyI" && !UiFx.Repeat(e))) { m.Close(); return true; }
                return false;
            });
            Dom.Append(head, close);
            close.Focus();
            return m.Done;
        }
    }
}
