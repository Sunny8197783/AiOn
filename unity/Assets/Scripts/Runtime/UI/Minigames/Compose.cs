// Ports ui/compose.js: 정민's message to 서연 (chips or free text), the phone/marked note panel, and the
// re-read captions. Chip labels are the exact fragments that go into the message (Relay.ComposeNote).
// Styles are the relay.css rules UiStyle.RegisterBase registers.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public sealed class ComposeContext
    {
        public string route, observedVia, gateSeenAt, sentAt;
    }

    // compose() resolves { chips, text, edited } (text is untrusted: Relay.MakeHandover cleans it).
    public sealed class ComposeResult
    {
        public Chips chips;
        public string text;
        public bool edited;
    }

    public static class Compose
    {
        static VisualElement Root() => UiRoot.Overlay;

        // Every chip label is the exact fragment that goes into the message.
        public static Task<ComposeResult> Write(ComposeContext c)
        {
            var tcs = new TaskCompletionSource<ComposeResult>();
            var ctx = new NoteCtx { route = c.route ?? "stairs", observedVia = c.observedVia ?? "sign", gateSeenAt = c.gateSeenAt ?? "20:09" };
            var chips = new Chips { what = null, time = false, route = false, unknowns = false, advice = null };
            bool edited = false;
            var prev = Mg.Focused();
            var inputs = new List<VisualElement>();

            // fieldset of radio pills; pick(value) runs on change.
            VisualElement RadioRow(string legend, Action<string> pick, params (string value, string label)[] options)
            {
                var row = Mg.Cls(new GroupBox(), "chips");
                row.Add(Mg.El("legend", null, legend));
                foreach (var (value, label) in options)
                {
                    var r = Mg.Cls(new RadioButton(label), "pill");
                    r.RegisterValueChangedCallback(ev => { if (ev.newValue) { pick(value); Refresh(); } });
                    inputs.Add(r); row.Add(r);
                }
                return row;
            }

            var text = Mg.Cls(new TextField { multiline = true, maxLength = Relay.MAX_NOTE }, null);
            text.name = "note-text";
            text.style.minHeight = 72;
            var send = Mg.Btn("primary", null, $"보내기 · {c.sentAt}");
            send.SetEnabled(false);
            Button reset = null;
            reset = Mg.Btn("secondary", () => { edited = false; Refresh(); }, "칩으로 다시 쓰기");
            Mg.Hidden(reset, true);
            var unknown = Mg.Cls(new Toggle { text = Relay.ComposeNote(new Chips { unknowns = true }, ctx) }, "pill");
            unknown.RegisterValueChangedCallback(ev => { chips.unknowns = ev.newValue; Refresh(); });
            inputs.Add(unknown);

            var box = Mg.El("div", "relay-modal",
                Mg.El("div", "relay-card",
                    Mg.El("p", "eyebrow", $"서연(303호)에게 문자 · {c.sentAt}"),
                    Mg.El("h2", null, "무엇을 남길까요?"),
                    Mg.El("p", null, "정민은 대피소에 들어왔습니다. 서연에게 보낼 문자를 고르거나 직접 고쳐 쓰세요."),
                    c.observedVia == "notice_after" ? Mg.El("p", "quote", "입구 안내문: “동쪽 출입구 폐쇄 · A 계단 / B 진출입로 이용”") : null,
                    RadioRow("무엇을 봤나", v => chips.what = v, ("gate_closed", Relay.ComposeNote(new Chips { what = "gate_closed" }, ctx)), ("all_closed", Relay.ComposeNote(new Chips { what = "all_closed" }, ctx))),
                    RadioRow("언제", v => chips.time = v == "true", ("true", $"{c.gateSeenAt}에"), ("false", "시각 없이")),
                    RadioRow("내가 쓴 길", v => chips.route = v == "true", ("true", Relay.ComposeNote(new Chips { route = true }, ctx)), ("false", "쓰지 않기")),
                    Mg.El("fieldset", "chips", Mg.El("legend", null, "모르는 것"), unknown),
                    RadioRow("한마디", v => chips.advice = string.IsNullOrEmpty(v) ? null : v,
                        ("follow_me", Relay.ComposeNote(new Chips { advice = "follow_me" }, ctx)), ("direction", Relay.ComposeNote(new Chips { advice = "direction" }, ctx)),
                        ("check_notice", Relay.ComposeNote(new Chips { advice = "check_notice" }, ctx)), ("", "없음")),
                    Mg.El("label", "note-label", "보낼 문장 ", Mg.El("small", null, $"최대 {Relay.MAX_NOTE}자 · 직접 고칠 수 있어요")),
                    text,
                    Mg.El("div", "compose-actions", reset, send)));

            void Refresh()
            {
                if (!edited) text.SetValueWithoutNotify(Relay.ComposeNote(chips, ctx));
                send.SetEnabled(edited ? (text.value ?? "").Trim().Length > 0 : chips.what != null);
                Mg.Hidden(reset, !edited);
            }
            text.RegisterValueChangedCallback(_ => { edited = true; Refresh(); });   // only user input notifies ('input')
            // Radio groups make the Tab order differ from the element list; keep Tab inside the box.
            MiniDom.TrapFocus(box);
            bool sent = false;
            send.clicked += () =>
            {
                if (sent) return; sent = true;
                box.RemoveFromHierarchy(); prev?.Focus();
                tcs.TrySetResult(new ComposeResult { chips = chips, text = text.value ?? "", edited = edited });
            };
            Root().Add(box);
            Refresh();
            inputs.FirstOrDefault()?.Focus();
            return tcs.Task;
        }

        // The message as 서연 sees it. mode: 'phone' | 'marked' (claims underlined).
        static VisualElement panel;
        static string Plain(string s) => $"<noparse>{(s ?? "").Replace("</noparse>", "")}</noparse>";

        public static void ShowNote(Handover hand, string mode)
        {
            HideNote();
            var body = Mg.Text(null);
            body.name = "relay-note-text";
            string t = hand.text ?? "";
            if (mode == "marked")
            {
                // mark.claim: underlined and tinted (rich text: the panel cannot hold inline elements).
                var quotes = (hand.claims?.quotes?.Values ?? Enumerable.Empty<string>()).Where(q => !string.IsNullOrEmpty(q)).Distinct();
                var spans = quotes.Select(q => (i: t.IndexOf(q, StringComparison.Ordinal), q)).Where(x => x.i >= 0).OrderBy(x => x.i).ToList();
                var sb = new System.Text.StringBuilder();
                int at = 0;
                foreach (var (i, q) in spans) { if (i < at) continue; sb.Append(Plain(t.Substring(at, i - at))).Append("<mark=#f6bb6e55><u>").Append(Plain(q)).Append("</u></mark>"); at = i + q.Length; }
                sb.Append(Plain(t.Substring(at)));
                body.enableRichText = true;
                body.text = sb.ToString();
            }
            else { body.enableRichText = false; body.text = t; }
            panel = Mg.El("aside", "relay-note phone", Mg.El("small", null, $"정민 · 302호 이웃 · 보냄 {hand.sentAt} · 받음 {hand.receivedAt}"), body);
            Root().Add(panel);
        }

        public static void HideNote() { panel?.RemoveFromHierarchy(); panel = null; }

        // One step of the re-read. Resolves 'next', or 'skip' on Esc. The focused button carries the caption.
        static VisualElement caption;
        public static Task<string> RereadCaption(string text, Pin pin = null, bool last = false)
        {
            var tcs = new TaskCompletionSource<string>();
            caption?.RemoveFromHierarchy();
            bool finished = false;
            VisualElement cap = null;
            void Done(string v) { if (finished) return; finished = true; cap.RemoveFromHierarchy(); if (caption == cap) caption = null; tcs.TrySetResult(v); }
            var btn = Mg.Btn("primary", () => Done("next"), last ? "서연으로 계속 ▸" : "다음 ▸");
            cap = Mg.El("div", "relay-caption",
                Mg.El("p", null, text),
                pin != null ? Mg.El("p", "pin-lines", Mg.El("span", "was", pin.was), Mg.El("span", "now", pin.now), pin.also != null ? Mg.El("span", "now", pin.also.now) : null) : null,
                btn, Mg.El("small", null, "E · Enter · Space로 넘기기 · Esc로 끝까지"));
            caption = cap;
            // window keydown (capture): polled from GameInput so it works wherever focus is. Down() is a fresh press,
            // which also skips held-key repeats (e.repeat).
            Mg.Loop((dt, t) =>
            {
                if (finished) return false;
                if (caption != cap) return false;   // replaced by a newer caption: like the JS, this one never resolves
                if (GameInput.Down("Escape")) Done("skip");
                else if (GameInput.Down("KeyE") || ((GameInput.Down("Enter") || GameInput.Down("NumpadEnter") || GameInput.Down("Space")) && Mg.Focused() != btn)) Done("next");
                return !finished;
            });
            Root().Add(cap);
            btn.Focus();
            return tcs.Task;
        }
    }
}
