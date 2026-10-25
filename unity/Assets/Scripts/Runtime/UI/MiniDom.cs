// Ports ui/mini-dom.js: the small DOM toolkit the minigames share (UI Minigames group calls it; the UI core
// group owns it): element builder, modal, label pills, header, the guided/hint/free help ladder, meters,
// number formats and the live-region announce.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class MiniDom
    {
        // className may hold several classes ("mg-btn mg-ghost"). Same element mapping as Dom.El.
        public static VisualElement El(string tag, string className = null, params object[] kids) => Dom.El(tag, className, kids);

        // put(parent, ...kids): nulls skipped, nested lists flattened.
        public static VisualElement Put(VisualElement parent, params object[] kids) => Dom.Append(parent, kids);

        public static string EscapeText(string s) => s ?? "";

        public static List<VisualElement> Focusables(VisualElement root) => Dom.Focusables(root);

        // Keeps Tab inside root (no modal-stack guard: minigames keep their own focus).
        public static void TrapFocus(VisualElement root) => Dom.Trap(root, false);

        // A dialog over the canvas. onKey(e) returns true when it handled the key. close(result) resolves done
        // and gives focus back to whatever had it before.
        public static ModalHandle<T> Modal<T>(string className = "", string label = "", IList<VisualElement> content = null, Func<KeyDownEvent, bool> onKey = null)
        {
            var prev = UiRoot.Root?.panel?.focusController?.focusedElement as VisualElement;
            var root = El("div", ("mg-panel " + (className ?? "")).Trim());
            root.focusable = true; root.tabIndex = -1;
            if (!string.IsNullOrEmpty(label)) root.tooltip = label;
            var back = El("div", "mg-backdrop", root);
            back.pickingMode = PickingMode.Position;
            if (content != null) Put(root, content);
            var m = new ModalHandle<T> { root = root, layer = back };
            var release = Dom.Trap(root, false);
            back.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.None) return;
                var k = UiFx.Key(e);
                e.StopPropagation();   // keys typed in a minigame never reach the world controls
                if (UiFx.Repeat(e) && (k == "Enter" || k == " ")) return;   // a held interact key must not press buttons here
                onKey?.Invoke(e);
            });
            back.RegisterCallback<NavigationSubmitEvent>(e => { if (UiFx.SubmitRepeating()) e.StopPropagation(); }, TrickleDown.TrickleDown);
            UiRoot.Overlay.Add(back);
            root.schedule.Execute(() =>
            {
                if (m.Closed) return;
                VisualElement target = null;
                root.Query<VisualElement>(className: "autofocus").ForEach(n => { if (target == null) target = n; });
                if (target == null) { var f = Focusables(root); target = f.Count > 0 ? f[0] : root; }
                target.Focus();
            });
            m.closer = result =>
            {
                if (m.Closed) return;
                m.Closed = true; release(); back.RemoveFromHierarchy();
                if (prev != null && prev.panel != null) prev.Focus();
                m.tcs.TrySetResult(result);
            };
            return m;
        }

        public static bool Reduced() => UiRoot.ReduceMotion;

        // Information label pills: icon + text, never colour alone.
        static readonly Dictionary<string, string[]> LABELS = new Dictionary<string, string[]>
        {
            ["official"] = new[] { "◆", "공식 안내" }, ["site"] = new[] { "◎", "현장 확인" }, ["neighbor"] = new[] { "●", "이웃에게 들음" },
            ["rumor"] = new[] { "?", "소문·미확인" }, ["self"] = new[] { "✎", "내 기록" },
        };
        public static VisualElement Pill(string label)
        {
            bool known = label != null && LABELS.ContainsKey(label);
            var L = known ? LABELS[label] : LABELS["self"];
            return El("span", "mg-pill is-" + (known ? label : "self"), El("span", null, L[0]), L[1]);
        }

        static readonly Dictionary<string, string> MODE_TEXT = new Dictionary<string, string> { ["guided"] = "안내 있음", ["hint"] = "힌트 가능", ["free"] = "혼자 판단", ["view"] = "보기" };
        // mode: guided | hint | free | view
        public static VisualElement Header(string eyebrow, string title, string mode, string id = null)
        {
            var h2 = El("h2", null, title);
            if (!string.IsNullOrEmpty(id)) h2.name = id;
            return El("header", "mg-head",
                El("div", null, El("p", "mg-eyebrow", eyebrow), h2),
                mode != null && MODE_TEXT.ContainsKey(mode) ? El("span", "mg-mode is-" + mode, MODE_TEXT[mode]) : null);
        }

        // guided: help shown; hint: a 힌트 button reveals it; free/view: null. make() builds the help nodes.
        public static VisualElement LadderHelp(string mode, Func<IList<VisualElement>> make)
        {
            if (mode == "guided") return El("div", "mg-help", make?.Invoke());
            if (mode != "hint") return null;
            var body = El("div", "mg-help hidden");
            var btn = (Button)El("button", "mg-btn mg-ghost", "힌트 보기");
            btn.clicked += () =>
            {
                bool open = UiStyle.IsHidden(body);
                if (open && body.childCount == 0) Put(body, make?.Invoke());
                UiStyle.Hidden(body, !open);
                Dom.SetText(btn.Q<Label>() ?? (VisualElement)btn, open ? "힌트 닫기" : "힌트 보기");
            };
            return El("div", "mg-hintbox", btn, body);
        }

        // 'ok' | 'warn' | 'over'
        public static string MeterState(double v, double max) => v > max ? "over" : v > max * 0.8 ? "warn" : "ok";
        public static readonly Dictionary<string, string> METER_WORD = new Dictionary<string, string> { ["ok"] = "여유", ["warn"] = "거의 가득", ["over"] = "초과" };
        // `${(Math.round(n * 10) / 10).toFixed(1)}kg`
        public static string FmtKg(double n) => (Math.Floor(n * 10 + 0.5) / 10).ToString("0.0", CultureInfo.InvariantCulture) + "kg";
        // `${Math.round(n).toLocaleString('ko-KR')}원`
        public static string Won(double n) => Math.Floor(n + 0.5).ToString("#,0", CultureInfo.InvariantCulture) + "원";

        // Live region for short announcements inside a minigame.
        // ponytail: no screen reader in the player; the text lands in a hidden live label (app TTS could read it).
        public static void Announce(string text)
        {
            var live = UiRoot.Live;
            if (live == null) return;
            live.text = "";
            live.schedule.Execute(() => live.text = text ?? "");
        }
    }
}
