// Ports ui/dialog.js: talk (speaker, lines one by one, choices), story cards, and the time-skip card.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class Dialog
    {
        static Func<bool> ArmedAfter(float ms = 220) { float t0 = Time.realtimeSinceStartup; return () => (Time.realtimeSinceStartup - t0) * 1000 > ms; }
        static string Initial(string name)
        {
            var s = (string.IsNullOrEmpty(name) ? "·" : name).Trim();
            return s.Length > 0 ? s.Substring(0, char.IsHighSurrogate(s[0]) && s.Length > 1 ? 2 : 1) : "·";
        }
        static VisualElement El(string tag, string cls, params object[] kids) => Dom.El(tag, cls, kids);
        static VisualElement Focused(VisualElement e) => e?.panel?.focusController?.focusedElement as VisualElement;

        // talk({ speaker, role, portrait?, lines, choices? }) -> choiceId | null
        // Lines advance with Enter / Space / E / click; choices appear after the last line (1–9 pick directly).
        // A disabled choice stays focusable and shows its reason, so it is never colour-only.
        public static Task<string> Talk(TalkOptions o)
        {
            o ??= new TalkOptions();
            string speaker = o.speaker ?? "", role = o.role ?? "";
            var list = (o.lines ?? new List<string>()).Where(l => !string.IsNullOrEmpty(l)).ToList();
            if (list.Count == 0) list.Add("…");
            var opts = o.choices != null ? o.choices.Where(c => c != null).ToList() : new List<Choice>();
            int i = 0; Func<bool> choiceArmed = () => false;

            var lineEl = (Label)El("p", "talk-line");
            var counter = (Label)El("span", "talk-count");
            var next = Dom.Btn("다음", null, iconName: "arrow", className: "talk-next autofocus");
            var choiceBox = El("div", "talk-choices hidden");
            var face = o.portrait != null ? El("div", "talk-portrait", o.portrait) : El("div", "talk-portrait mono", Initial(speaker));
            var hint = El("span", "talk-hint", El("kbd", null, "Enter"), " 또는 ", El("kbd", null, "E"), " 계속");
            var box = El("div", "talk-box", face,
                El("div", "talk-main",
                    El("header", "talk-head", El("b", "talk-speaker", speaker), role != "" ? El("span", "talk-role", role) : null, counter),
                    lineEl, choiceBox,
                    El("div", "talk-foot", hint, next)));

            ModalHandle<string> m = null;
            var buttons = new List<Button>();
            for (int n = 0; n < opts.Count; n++)
            {
                var c = opts[n];
                var reason = c.disabled ? (string.IsNullOrEmpty(c.reason) ? "지금은 고를 수 없습니다." : c.reason) : "";
                var b = (Button)El("button", "choice" + (c.disabled ? " is-disabled" : ""),
                    El("span", "choice-no", n < 9 ? (n + 1).ToString() : "·"),
                    El("span", "choice-text", El("span", "choice-label", c.label ?? c.id),
                        !string.IsNullOrEmpty(c.note) ? El("small", "choice-note", c.note) : null,
                        reason != "" ? El("small", "choice-reason", Dom.Icon("lock"), reason) : null));
                b.RegisterCallback<FocusInEvent>(_ => UiStyle.Toggle(b, "is-focus", true));
                b.RegisterCallback<FocusOutEvent>(_ => UiStyle.Toggle(b, "is-focus", false));
                b.clicked += () => Pick(c, b);
                buttons.Add(b);
            }
            Dom.Append(choiceBox, buttons);

            void Render()
            {
                lineEl.text = list[i];
                UiFx.LineIn(lineEl);
                counter.text = list.Count > 1 ? (i + 1) + " / " + list.Count : "";
                bool last = i == list.Count - 1;
                if (last && opts.Count > 0)
                {
                    UiStyle.Hidden(choiceBox, false); UiStyle.Hidden(next, true);
                    UiStyle.Hidden(hint, true);
                    Css.ApplyGap(box.Q(className: "talk-foot"));
                    choiceArmed = ArmedAfter(260);
                    (buttons.FirstOrDefault(b => !b.ClassListContains("is-disabled")) ?? buttons[0]).Focus();
                }
                else
                {
                    Dom.BtnLabel(next).text = last ? "닫기" : "다음";
                    next.Focus();
                }
            }
            void Advance()
            {
                if (!UiStyle.IsHidden(choiceBox)) return;
                if (i < list.Count - 1) { i++; Dom.UiSound("tick"); Render(); } else m.Close(null);
            }
            void Pick(Choice c, Button b)
            {
                if (!choiceArmed()) return;
                if (c.disabled) { UiFx.Shake(b); Dom.UiSound("error"); return; }
                Dom.UiSound("confirm");
                m.Close(c.id);
            }
            bool OnKey(KeyDownEvent e)
            {
                var key = UiFx.Key(e);
                if (UiFx.Code(e) == "KeyE" && !UiFx.Repeat(e)) { Advance(); return true; }
                if (key == "Escape")
                {
                    if (opts.Count == 0) m.Close(null);
                    else if (UiStyle.IsHidden(choiceBox)) { i = list.Count - 1; Render(); }
                    return true;
                }
                if (UiStyle.IsHidden(choiceBox)) return false;
                if (int.TryParse(key, out var n) && n >= 1 && n <= Math.Min(9, buttons.Count)) { Pick(opts[n - 1], buttons[n - 1]); return true; }
                if (key == "ArrowDown" || key == "ArrowUp")
                {
                    int at = buttons.IndexOf(Focused(lineEl) as Button);
                    buttons[((at + (key == "ArrowDown" ? 1 : buttons.Count - 1)) % buttons.Count + buttons.Count) % buttons.Count].Focus();
                    return true;
                }
                return false;
            }

            m = Dom.Modal<string>("talk", speaker != "" ? "대화 · " + speaker : "대화", box, OnKey, backdrop: false);
            next.clicked += Advance;
            lineEl.RegisterCallback<ClickEvent>(_ => Advance());
            Render();
            return m.Done;
        }

        // card({ kind, title, meta, body, actions }) -> actionId
        static readonly Dictionary<string, string[]> KIND = new Dictionary<string, string[]>
        {
            ["alert"] = new[] { "alert", "경보" }, ["news"] = new[] { "news", "뉴스" }, ["notice"] = new[] { "notice", "안내문" },
            ["info"] = new[] { "info", "알아 둘 것" }, ["sms"] = new[] { "sms", "문자" },
        };

        public static Task<string> Card(CardOptions o)
        {
            o ??= new CardOptions();
            var kind = o.kind ?? "info";
            var k = KIND.TryGetValue(kind, out var kk) ? kk : KIND["info"];
            string ic = k[0], kindLabel = k[1], title = o.title ?? "", meta = o.meta ?? "";
            var acts = o.actions != null && o.actions.Count > 0 ? o.actions.Where(a => a != null).ToList() : new List<Choice> { new Choice("ok", "확인", true) };
            var armed = ArmedAfter();
            var rows = (o.body ?? new List<string>()).Where(b => !string.IsNullOrEmpty(b)).Select(b => El("p", null, b)).ToList();
            bool hasPrimary = acts.Any(a => a.primary);
            ModalHandle<string> m = null;
            var buttons = acts.Select((a, n) => (VisualElement)Dom.Btn(a.label ?? a.id, () => { if (armed()) { Dom.UiSound("confirm"); m.Close(a.id); } },
                a.primary, !a.primary && hasPrimary, null, a.primary || (!hasPrimary && n == 0) ? "autofocus" : "")).ToList();
            var content = El("article", "story-card card-" + kind,
                El("header", "card-head", El("span", "card-kind", Dom.Icon(ic), kindLabel), meta != "" ? El("span", "card-meta", meta) : null),
                El("h2", "card-title", title),
                El("div", "card-body", rows),
                El("footer", "card-actions", buttons));
            m = Dom.Modal<string>("card card-" + kind, title != "" ? title : kindLabel, content,
                e => { if (UiFx.Key(e) == "Escape" && acts.Count == 1) { m.Close(acts[0].id); return true; } return false; });
            if (kind == "alert") Dom.UiSound("alert");
            return m.Done;
        }

        // timeSkip({ from, to, text }). Full-screen interstitial; continues on any key/click or after a
        // reading-time based delay (at least 2.5 s). Several text parts are one paragraph.
        public static async Task TimeSkip(string from, string to, params string[] text)
        {
            from ??= ""; to ??= "";
            var t = string.Join(" ", (text ?? new string[0]).Where(s => s != null));
            int ms = Math.Max(2500, Math.Min(7500, 1400 + t.Length * 55));
            var cont = Dom.Btn("계속", null, ghost: true, iconName: "arrow", className: "skip-continue autofocus");
            var fromEl = (Label)El("span", "skip-from");
            fromEl.enableRichText = true; fromEl.text = "<s><noparse>" + from + "</noparse></s>";
            var arrow = El("span", "skip-arrow", Dom.Icon("hourglass"));
            var bar = El("span", "skip-bar");
            var content = El("div", "skip-inner",
                El("p", "skip-times", fromEl, arrow, El("span", "skip-to", to)),
                El("p", "skip-text", t),
                El("div", "skip-progress", bar),
                cont);
            var armed = ArmedAfter(400);
            ModalHandle<bool> m = null;
            m = Dom.Modal<bool>("timeskip", from + "에서 " + to + "로 시간이 흐릅니다", content, e =>
            {
                var key = UiFx.Key(e);
                if (key != "Tab" && !UiFx.Repeat(e) && armed() && key != "Shift" && key != "Control" && key != "Alt" && key != "Meta") { m.Close(); return true; }
                return false;
            });
            m.layer.RegisterCallback<ClickEvent>(_ => { if (armed()) m.Close(); });
            bar.style.width = 0;
            UiFx.Grow(bar, ms / 1000f);
            var stopSpin = UiFx.Spin(arrow.Q<UiIcon>(), 2.4f);
            var timer = m.layer.schedule.Execute(() => m.Close()).StartingIn(ms);
            await m.Done;
            timer.Pause(); stopSpin();
        }
    }
}
