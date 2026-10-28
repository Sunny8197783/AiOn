// Ports ui/debrief.js: the soft-fail card (retry), the chapter card, and the ending's 생존 기록.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class DebriefUi
    {
        static VisualElement El(string tag, string cls, params object[] kids) => Dom.El(tag, cls, kids);

        static readonly Dictionary<string, string> PREP = new Dictionary<string, string>
        {
            ["bag"] = "비상가방", ["cash"] = "현금", ["docs"] = "비상연락카드·서류 사본", ["entrances"] = "대피소 출입구 확인", ["extinguisher"] = "소화기 위치 확인",
            ["flashlight"] = "손전등 점검", ["judgement"] = "정보를 가려 듣기", ["powerbank"] = "보조배터리 충전", ["radio"] = "라디오 점검", ["route"] = "경로 확인",
            ["tub"] = "물 받아 두기", ["water_jug"] = "물통 준비",
        };
        static string PrepName(string id) => id != null && PREP.TryGetValue(id, out var n) ? n : string.IsNullOrEmpty(id) ? "준비" : id;
        static List<T> Lines<T>(IEnumerable<T> v) => (v ?? Enumerable.Empty<T>()).Where(x => x != null && !(x is string s && s == "")).ToList();
        static List<string> Lines(string v) => string.IsNullOrEmpty(v) ? new List<string>() : new List<string> { v };
        static Func<bool> ArmedAfter(float ms) { float t0 = Time.realtimeSinceStartup; return () => (Time.realtimeSinceStartup - t0) * 1000 > ms; }

        // <a href target=_blank>: opens the page in the system browser.
        internal static VisualElement Link(string text, string url)
        {
            var a = El("a", null, text);
            a.focusable = true; a.tabIndex = 0; a.tooltip = url;
            a.RegisterCallback<ClickEvent>(_ => Application.OpenURL(url));
            a.RegisterCallback<NavigationSubmitEvent>(_ => Application.OpenURL(url));
            return a;
        }

        static string SrcText(SourceEntry s) => (string.IsNullOrEmpty(s.publisher) ? "" : s.publisher + " · ") + (s.title ?? "");
        static VisualElement SourceList(IEnumerable<SourceEntry> sources)
        {
            var list = Lines(sources);
            return list.Count > 0 ? El("ul", "src-list", list.Select(s => El("li", null, !string.IsNullOrEmpty(s.url) ? Link(SrcText(s), s.url) : El("span", null, SrcText(s))))) : null;
        }

        // Resolves 'retry' | 'menu'.
        public static Task<string> FailCard(FailCardOptions o)
        {
            o ??= new FailCardOptions();
            string title = o.title ?? "";
            VisualElement Row(string ic, string label, string v) => El("div", "fail-row", El("dt", null, Dom.Icon(ic), label), El("dd", null, Lines(v).Select(t => El("p", null, t))));
            var armed = ArmedAfter(400);
            ModalHandle<string> m = null;
            Action Go(string id) => () => { if (armed()) { Dom.UiSound("confirm"); m.Close(id); } };
            var content = El("scroll", "fail-card",
                El("p", "eyebrow", Dom.Icon("retry"), "다시 해 볼 수 있습니다"),
                El("h2", null, title != "" ? title : "이번에는 여기까지였습니다"),
                El("p", "fail-lead", "실패는 벌이 아니라 정보입니다. 무엇을 알고 있었고 무엇이 비어 있었는지 확인해 보세요."),
                El("dl", "fail-rows",
                    Row("site", "당시 확인한 정보", o.seen),
                    Row("eye", "판단에서 놓친 부분", o.missed),
                    Row("spark", "다음에 시도할 준비", o.next)),
                Lines(o.sources).Count > 0 ? El("section", "fail-sources", El("h3", null, Dom.Icon("official"), "참고한 공식 자료"), SourceList(o.sources)) : null,
                El("footer", "card-actions",
                    Dom.Btn("메인 메뉴", Go("menu"), ghost: true, iconName: "exit"),
                    Dom.Btn("체크포인트에서 다시", Go("retry"), primary: true, iconName: "retry", className: "autofocus")));
            m = Dom.Modal<string>("fail", title != "" ? title : "다시 시도", content);
            return m.Done;
        }

        public static Task ChapterCard(ChapterCardOptions o)
        {
            o ??= new ChapterCardOptions();
            string chapter = o.chapter ?? "", title = o.title ?? "";
            var armed = ArmedAfter(400);
            var pays = Lines(o.payoffs);
            var ls = Lines(o.lines);
            ModalHandle<bool> m = null;
            var content = El("scroll", "chapter-card",
                El("p", "eyebrow", Dom.Icon("check"), chapter + " 끝"),
                El("h2", null, title),
                pays.Count > 0 ? El("section", "payoffs",
                    El("h3", null, Dom.Icon("spark"), "준비가 도움이 된 순간"),
                    El("ul", null, pays.Select(p => El("li", null, El("b", null, PrepName(p.prep)), El("span", null, p.moment ?? ""))))) : null,
                El("section", "chapter-lines",
                    El("h3", null, Dom.Icon("book"), "이번 장의 기록"),
                    ls.Count > 0 ? El("ul", null, ls.Select(t => El("li", null, El("span", "li-dot", "·"), El("span", null, t)))) : El("p", "muted", "기록할 것이 많지 않았습니다.")),
                El("footer", "card-actions", Dom.Btn("다음으로", () => { if (armed()) m.Close(); }, primary: true, iconName: "arrow", className: "autofocus")));
            m = Dom.Modal<bool>("chapter", chapter + " 정리 · " + title, content);
            Dom.UiSound("payoff");
            return m.Done;
        }

        // Resolves the button id ('restart' | 'menu').
        public static Task<string> Ending(EndingRecordData record, IList<SourceEntry> sources)
        {
            record ??= new EndingRecordData();
            var fate = record.fate;
            var practice = record.practice;
            var src = !string.IsNullOrEmpty(practice?.sourceId) ? Lines(sources).FirstOrDefault(s => s.id == practice.sourceId) : null;
            VisualElement Section<T>(string ic, string title, IEnumerable<T> items, Func<T, object> render, string empty)
            {
                var list = Lines(items);
                return El("section", "doc-section",
                    El("h3", null, Dom.Icon(ic), title),
                    list.Count > 0 ? El("ul", null, list.Select(x => El("li", null, render(x)))) : El("p", "muted", empty));
            }
            var armed = ArmedAfter(400);
            ModalHandle<string> m = null;
            Action Go(string id) => () => { if (armed()) { Dom.UiSound("confirm"); m.Close(id); } };
            string srcText = src == null ? null : src.publisher + " · " + src.title;
            var doc = El("scroll", "ending-doc",
                El("header", "doc-head",
                    El("p", "doc-kicker", "연무동 · 한빛아파트 103동 302호 · 정민"),
                    El("h2", null, fate != null ? "엔딩 · " + fate.title : "생존 기록"),
                    El("p", "doc-sub", "전쟁 직전의 준비가 전쟁 이후의 선택지를 어떻게 바꾸었는지 돌아봅니다.")),
                fate != null ? El("section", "doc-section doc-fate", Lines(fate.lines).Select(t => El("p", null, t))) : null,
                Section("spark", "도움이 된 준비", record.helped, h => new object[] { El("b", null, PrepName(h.prep)), El("span", null, h.moment ?? "") }, "이번에는 미리 해 둔 준비가 큰 역할을 하지 못했습니다. 다음에는 하나만 먼저 해 보세요."),
                Section("box", "쓰지 못한 물건과 그 이유", record.unused, u => new object[] { El("b", null, u.item ?? ""), El("span", null, u.why ?? "") }, "가진 물건을 모두 쓸 수 있었습니다."),
                Section("rumor", "확인하지 못한 정보", record.@unchecked, t => El("span", null, t), "필요한 정보를 대부분 확인했습니다."),
                Section("people", "함께 버틴 사람들", record.people, p => new object[] { El("b", null, p.who ?? ""), El("span", null, p.what ?? "") }, "이번에는 혼자 버텼습니다. 이웃과의 연락 한 가지가 선택지를 넓힙니다."),
                !string.IsNullOrEmpty(practice?.text) ? El("section", "doc-section doc-practice",
                    El("h3", null, Dom.Icon("official"), "실제 생활에서 확인해 볼 한 가지"),
                    El("p", "practice-text", practice.text),
                    src != null ? El("p", "practice-src", "출처: ", !string.IsNullOrEmpty(src.url) ? Link(srcText, src.url) : (object)srcText,
                        !string.IsNullOrEmpty(src.checkedAt) ? " (확인 " + src.checkedAt + ")" : null) : null) : null,
                El("p", "doc-fiction", "이 기록은 가상의 이야기에서 나온 것입니다. 실제 상황에서는 재난방송·재난문자·지자체 안내를 따르세요."));
            var content = El("div", "ending-wrap", doc,
                El("footer", "card-actions ending-actions",
                    Dom.Btn("인쇄하기", () => Print(doc), ghost: true, iconName: "print"),
                    Dom.Btn("메인 메뉴", Go("menu"), ghost: true, iconName: "exit"),
                    Dom.Btn("처음부터 다시", Go("restart"), primary: true, iconName: "retry", className: "autofocus")));
            m = Dom.Modal<string>("ending", "생존 기록", content);
            Dom.UiSound("payoff");
            return m.Done;
        }

        // ponytail: window.print() stand-in — writes the record's text to survival-record.txt and opens it with the
        // system viewer (print from there); a styled PDF export would need a renderer.
        static void Print(VisualElement doc)
        {
            try
            {
                var sb = new StringBuilder();
                doc.Query<Label>().ForEach(l => { if (!string.IsNullOrEmpty(l.text) && l.text != "·") sb.AppendLine(l.text); });
                var path = Path.Combine(Application.persistentDataPath, "survival-record.txt");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Application.OpenURL("file://" + path.Replace('\\', '/'));
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
