// Ports ui/docs.js: writing the emergency contact card. Tick a field to write it on the card, pick where the card
// is kept, and see what stays readable once the phone is dead. Resolves { card: { id: value }, where }.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class Docs
    {
        static readonly Dictionary<string, string[]> WHERE = new Dictionary<string, string[]>
        {
            ["bag"] = new[] { "배낭 앞주머니", "대피할 때 배낭과 함께 나갑니다." },
            ["home"] = new[] { "책상 서랍", "집 안에 있을 때만 꺼내 볼 수 있습니다." },
        };

        public static Task<DocsResult> Open(DocsOptions o)
        {
            MinigameStyle.Register();
            o ??= new DocsOptions();
            string mode = o.mode ?? "guided";
            var fields = o.fields ?? new List<Logic.DocField>();
            var carry = o.carry ?? new List<string> { "bag", "home" };
            var on = new HashSet<string>();
            string where = carry.Contains("bag") ? "bag" : carry.Count > 0 && carry[0] != null ? carry[0] : "home";
            DocsResult Result() => new DocsResult { card = fields.Where(f => on.Contains(f.id)).GroupBy(f => f.id).ToDictionary(g => g.Key, g => g.Last().value), where = where };
            ModalHandle<DocsResult> m = null;
            m = MiniDom.Modal<DocsResult>($"mg-docs is-{mode}", "비상연락카드 쓰기", null, e => { if (Mg.Key(e) == "Escape") { m.Close(Result()); return true; } return false; });
            // ponytail: the mg-write handwriting reveal (skipped under reduced motion) is not animated; the row just appears.

            var paper = Mg.El("ol", "mg-card-rows");
            var phoneOnly = Mg.El("ul", "mg-phone-only");
            var phoneCount = Mg.Text(null);
            phoneCount.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;

            void Render(string justWrote = null)
            {
                Mg.Replace(paper, fields.Where(f => on.Contains(f.id)).Select(f =>
                {
                    var li = Mg.El("li", f.id == justWrote ? "is-new" : null, Mg.Text("mg-card-k", f.label), Mg.Text("mg-card-v", f.value));
                    li.style.flexDirection = FlexDirection.Row;
                    return li;
                }).ToList());
                if (on.Count == 0) paper.Add(Mg.El("li", "mg-empty", "아직 아무것도 적지 않았다."));
                var left = fields.Where(f => !on.Contains(f.id)).ToList();
                phoneCount.text = $"{on.Count}/{fields.Count}개 항목";
                Mg.Replace(phoneOnly, left.Select(f => Mg.El("li", null, "✕ " + f.label)).ToList());
                if (left.Count == 0) phoneOnly.Add(Mg.El("li", "mg-phone-ok", "✓ 모두 종이에 적었다."));
                foreach (var li in phoneOnly.Children()) li.style.marginRight = 12;
            }

            Toggle first = null;
            var list = Mg.El("fieldset", "mg-fields", Mg.El("legend", null, "카드에 적을 것"), fields.Select((f, i) =>
            {
                var box = new Toggle();
                if (i == 0) first = box;
                box.RegisterValueChangedCallback(ev =>
                {
                    if (ev.newValue) on.Add(f.id); else on.Remove(f.id);
                    Render(f.id);
                    MiniDom.Announce(ev.newValue ? $"{f.label}을(를) 적었다." : $"{f.label}을(를) 지웠다.");
                });
                var rec = mode == "guided" && f.recommended;
                var lab = Mg.El("label", null,
                    Mg.El("b", null, f.label, rec ? Mg.El("span", "mg-badge is-rec", "★ 권장") : null),
                    Mg.Text("mg-spec", f.value),
                    !string.IsNullOrEmpty(f.why) && mode != "free" ? Mg.Text("mg-tip", f.why) : null);
                lab.style.flexGrow = 1; lab.style.marginLeft = 10;
                lab.RegisterCallback<ClickEvent>(_ => box.value = !box.value);   // <label for>
                return Mg.El("div", "mg-field" + (rec ? " is-rec" : ""), box, lab);
            }).ToList());

            var whereSet = Mg.Cls(new GroupBox(), "mg-where");   // radio buttons inside one GroupBox form one group
            whereSet.Add(Mg.El("legend", null, "카드를 둘 곳"));
            foreach (var k in carry.Where(k => k != null && WHERE.ContainsKey(k)))
            {
                var key = k;
                var r = new RadioButton { value = key == where };
                r.RegisterValueChangedCallback(ev => { if (ev.newValue) where = key; });
                var body = Mg.El("span", null, Mg.El("b", null, WHERE[key][0]), mode != "free" ? Mg.El("small", null, WHERE[key][1]) : null);
                body.style.marginLeft = 10;
                body.RegisterCallback<ClickEvent>(_ => r.value = true);
                whereSet.Add(Mg.El("label", "mg-radio-opt", r, body));
            }

            var doneBtn = Mg.Btn("mg-btn mg-primary", () => m.Close(Result()), "카드 다 썼다");
            var help = MiniDom.LadderHelp(mode, () => new List<VisualElement> {
                Mg.El("p", null, "휴대폰이 꺼져도 볼 수 있는 것은 종이에 적은 것뿐입니다. 꼭 필요한 연락처와 장소부터 적어 보세요."),
                mode == "hint" ? Mg.El("p", "mg-why", "각 항목 아래에 왜 필요한지가 함께 보입니다.") : null }.Where(x => x != null).ToList());

            MiniDom.Put(m.root,
                MiniDom.Header("서류·연락처 정리", "비상연락카드", mode, "mg-docs-title"),
                Mg.El("div", "mg-docs-grid",
                    Mg.El("div", "mg-docs-left", help, list, whereSet),
                    Mg.El("div", "mg-desk",
                        Mg.El("article", "mg-paper",
                            Mg.El("h3", null, "비상연락카드"), Mg.El("p", "mg-paper-sub", "지갑 크기 종이 한 장 · 볼펜으로"), paper),
                        Mg.El("section", "mg-deadphone",
                            Mg.El("div", "mg-phone-ico", Mg.El("span", null, "0%")),
                            Mg.El("div", null, Mg.El("p", null, "휴대폰이 꺼져도 카드로 볼 수 있음: ", phoneCount),
                                Mg.El("p", "mg-why", "휴대폰 속에만 있어 꺼지면 볼 수 없는 것:"), phoneOnly)))),
                Mg.El("footer", "mg-foot", Mg.El("p", "mg-keys", "Tab 이동 · Space 적기/지우기 · ↑↓ 보관 장소 · Esc 닫기"), Mg.El("div", "mg-actions", doneBtn)));
            Render();
            first?.Focus();   // data-autofocus on the first field
            return m.Done;
        }
    }
}
