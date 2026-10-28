// Ports ui/dom.js: tiny element builder for the story UI (UI Toolkit), focus-trapped modals, labels, icons,
// buttons, UI sounds (Core.GameEvents.Ui). Used by the UI core files.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    // modal() -> { root, close(result), done }
    public sealed class ModalHandle<T>
    {
        public VisualElement root;
        public VisualElement layer;                // the backdrop (.modal-layer / .mg-backdrop)
        internal Action<T> closer;
        internal readonly TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
        public bool Closed { get; internal set; }
        public void Close(T result = default) => closer?.Invoke(result);
        public Task<T> Done => tcs.Task;
    }

    public sealed class LabelInfo
    {
        public string text, icon;
    }

    public static class Dom
    {
        public static string EscapeText(string s) => s ?? "";

        static readonly HashSet<string> TEXT_TAGS = new HashSet<string> { "p", "span", "b", "strong", "i", "em", "small", "h1", "h2", "h3", "h4", "time", "kbd", "output", "legend", "summary", "dt", "dd", "a", "mark", "label", "li" };

        // el(tag, attrs, ...children): tag picks the element (button -> Button, p/span/b/h2/small -> Label,
        // input -> TextField/Toggle, others -> VisualElement); children are elements or strings.
        // A text tag whose children are all strings is one Label; with element children it is a wrapping row
        // of Labels and elements (inline flow).
        public static VisualElement El(string tag, string className = null, params object[] children)
        {
            tag = string.IsNullOrEmpty(tag) ? "div" : tag;
            var kids = Flatten(children);
            VisualElement e;
            if (tag == "button") { e = new Button { focusable = true }; e.style.flexDirection = FlexDirection.Row; e.style.alignItems = Align.Center; Hover(e); }
            else if (tag == "input" || tag == "textarea") { var tf = new TextField { multiline = tag == "textarea" }; e = tf; }
            else if (tag == "checkbox") e = new Toggle();
            else if (tag == "scroll") e = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };   // overflow: auto
            else if (TEXT_TAGS.Contains(tag) && kids.TrueForAll(k => k is string)) e = new Label(string.Concat(kids)) { enableRichText = false };
            else
            {
                e = new VisualElement();
                if (TEXT_TAGS.Contains(tag)) { e.style.flexDirection = FlexDirection.Row; e.style.flexWrap = Wrap.Wrap; e.style.alignItems = Align.Center; }
            }
            e.name = tag;
            e.AddToClassList("t-" + tag);
            if (!(e is Label)) foreach (var k in kids) e.Add(k as VisualElement ?? Text(k.ToString()));
            UiStyle.Restyle(e);
            if (!string.IsNullOrEmpty(className)) UiStyle.AddClass(e, className);
            Css.ApplyGap(e);
            return e;
        }

        public static Label Text(string s) => new Label(s ?? "") { enableRichText = false, name = "#text" };

        // Children list: nulls / false / true skipped, nested lists flattened (kids.flat(Infinity)).
        internal static List<object> Flatten(IEnumerable kids)
        {
            var o = new List<object>();
            if (kids == null) return o;
            foreach (var k in kids)
            {
                if (k == null || k is bool) continue;
                if (k is string || k is VisualElement) o.Add(k);
                else if (k is IEnumerable en) o.AddRange(Flatten(en));
                else o.Add(Convert.ToString(k, System.Globalization.CultureInfo.InvariantCulture));
            }
            return o;
        }

        // append(): adds kids (strings become text), restyles them in their new context, re-applies the gap.
        public static VisualElement Append(VisualElement parent, params object[] kids)
        {
            foreach (var k in Flatten(kids))
            {
                var v = k as VisualElement ?? Text((string)k);
                parent.Add(v);
                UiStyle.Restyle(v);
            }
            Css.ApplyGap(parent);
            return parent;
        }

        // replaceChildren().
        public static VisualElement Replace(VisualElement parent, params object[] kids)
        {
            parent.Clear();
            return Append(parent, kids);
        }

        // Sets a text element's text (textContent).
        public static void SetText(VisualElement e, string text)
        {
            if (e is TextElement t) { t.text = text ?? ""; return; }
            e.Clear();
            Append(e, text ?? "");
        }

        // :hover as the is-hover class.
        public static void Hover(VisualElement e)
        {
            e.RegisterCallback<PointerEnterEvent>(_ => UiStyle.Toggle(e, "is-hover", true));
            e.RegisterCallback<PointerLeaveEvent>(_ => UiStyle.Toggle(e, "is-hover", false));
        }

        // ---- focus ----
        static bool pointerFocus;
        static readonly List<VisualElement> stack = new List<VisualElement>();
        static bool IsTop(VisualElement root) => stack.Count == 0 || stack[stack.Count - 1] == root;

        // Called once by UiRoot.Create: key repeat tracking, the focus-visible ring and the focusin guard.
        internal static void Install(VisualElement root)
        {
            UiFx.TrackKeys(root);
            root.RegisterCallback<PointerDownEvent>(_ => pointerFocus = true, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(_ => pointerFocus = false, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusInEvent>(e =>
            {
                var t = e.target as VisualElement;
                if (t == null) return;
                if (!pointerFocus && t.tabIndex >= 0 && t.focusable) Ring(t, true);
                // Minigame and relay dialogs (mini-dom, compose) sit above this stack and keep their own focus.
                if (stack.Count == 0) return;
                var top = stack[stack.Count - 1];
                if (top.panel == null || Contains(top, t) || InClass(t, "mg-backdrop") || InClass(t, "relay-modal")) return;
                var f = Focusables(top);
                (f.Count > 0 ? f[0] : top).Focus();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(e => { if (e.target is VisualElement t) Ring(t, false); }, TrickleDown.TrickleDown);
        }

        // :focus-visible { outline: 3px solid var(--teal); outline-offset: 2px }
        static void Ring(VisualElement t, bool on)
        {
            var old = t.Q<VisualElement>("focus-ring");
            if (old != null && old.parent == t) old.RemoveFromHierarchy();
            if (!on) return;
            var r = new VisualElement { name = "focus-ring", pickingMode = PickingMode.Ignore };
            Css.Abs(r, -5, -5, -5, -5);
            Css.Border(r, 3, Css.Has(t, "plan-card") ? "#0f7d72" : Css.TEAL);
            Css.Radius(r, t.resolvedStyle.borderTopLeftRadius + 5);
            t.Add(r);
        }

        static bool Contains(VisualElement root, VisualElement e)
        {
            for (var p = e; p != null; p = p.parent) if (p == root) return true;
            return false;
        }
        static bool InClass(VisualElement e, string cls)
        {
            for (var p = e; p != null; p = p.parent) if (p.ClassListContains(cls)) return true;
            return false;
        }

        static bool Visible(VisualElement e, VisualElement root)
        {
            for (var p = e; p != null; p = p.parent)
            {
                if (UiStyle.IsHidden(p) || p.resolvedStyle.display == DisplayStyle.None || p.style.display.value == DisplayStyle.None) return false;
                if (p == root) break;
            }
            return true;
        }

        // FOCUSABLE: enabled buttons and fields, [tabindex] >= 0; not hidden.
        public static List<VisualElement> Focusables(VisualElement root)
        {
            var list = new List<VisualElement>();
            root.Query<VisualElement>().ForEach(n =>
            {
                if (n == root || !n.focusable || n.tabIndex < 0 || !n.enabledInHierarchy || n.name == "focus-ring") return;
                if (n.parent is TextField || (n.parent?.parent is TextField)) return;   // the field itself counts once
                if (n is TextField tf && !tf.enabledInHierarchy) return;
                if (Visible(n, root)) list.Add(n);
            });
            return list;
        }

        static VisualElement Focused(VisualElement e) => e?.panel?.focusController?.focusedElement as VisualElement;

        // Keeps Tab / Shift+Tab inside root. Returns a release function.
        internal static Action Trap(VisualElement root, bool guardStack)
        {
            EventCallback<KeyDownEvent> onKey = e =>
            {
                if (e.keyCode != KeyCode.Tab) return;
                e.StopPropagation();
                var list = Focusables(root);
                if (list.Count == 0) { root.Focus(); return; }
                var at = Focused(root);
                int i = at == null ? -1 : list.IndexOf(at);
                bool inside = at != null && Contains(root, at);
                VisualElement to;
                if (e.shiftKey) to = !inside || i <= 0 ? list[list.Count - 1] : list[i - 1];
                else to = !inside || i < 0 || i == list.Count - 1 ? list[0] : list[i + 1];
                to.Focus();
            };
            // Tab / arrows would also move focus through the panel's own navigation: the UI here moves it.
            EventCallback<NavigationMoveEvent> onNav = e => e.StopPropagation();
            root.RegisterCallback(onKey, TrickleDown.TrickleDown);
            root.RegisterCallback(onNav, TrickleDown.TrickleDown);
            return () => { root.UnregisterCallback(onKey, TrickleDown.TrickleDown); root.UnregisterCallback(onNav, TrickleDown.TrickleDown); };
        }

        public static void TrapFocus(VisualElement root) => Trap(root, true);

        // onKey returns true when it handled the key.
        public static ModalHandle<T> Modal<T>(string className = "", string label = "", VisualElement content = null, Func<KeyDownEvent, bool> onKey = null, bool backdrop = true)
        {
            className ??= "";
            var prev = Focused(UiRoot.Root);
            var root = El("div", ("modal " + className).Trim());
            root.focusable = true; root.tabIndex = -1;
            var layerCls = new List<string> { "modal-layer", backdrop ? "" : "no-backdrop" };
            foreach (var c in className.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) layerCls.Add(c + "-layer");
            var layer = El("div", string.Join(" ", layerCls).Trim(), root);
            layer.pickingMode = PickingMode.Position;
            if (content != null) Append(root, content);
            var m = new ModalHandle<T> { root = root, layer = layer };
            var release = Trap(root, true);
            EventCallback<KeyDownEvent> keyHandler = e =>
            {
                if (e.keyCode == KeyCode.None) return;
                e.StopPropagation();
                var k = UiFx.Key(e);
                if (UiFx.Repeat(e) && (k == "Enter" || k == " ")) return;
                onKey?.Invoke(e);
            };
            // A held interact key must not press buttons here.
            EventCallback<NavigationSubmitEvent> onSubmit = e => { if (UiFx.SubmitRepeating()) e.StopPropagation(); };
            root.RegisterCallback(keyHandler);
            root.RegisterCallback(onSubmit, TrickleDown.TrickleDown);
            m.closer = result =>
            {
                if (m.Closed) return;
                m.Closed = true;
                release(); root.UnregisterCallback(keyHandler);
                stack.Remove(root);
                layer.RemoveFromHierarchy();
                UiSound("close");
                var back = prev != null && prev.panel != null ? prev : (stack.Count > 0 ? stack[stack.Count - 1] : null);
                if (back != null) back.Focus(); else Focused(UiRoot.Root)?.Blur();
                m.tcs.TrySetResult(result);
            };
            UiRoot.ModalLayer.Add(layer);
            stack.Add(root);
            UiSound("open");
            UiFx.Fade(layer);
            // .modal { animation: rise .3s }; .modal.timeskip fade .6s; .modal.landing fade .8s
            if (Css.Has(root, "timeskip")) UiFx.Fade(root, .6f);
            else if (Css.Has(root, "landing")) UiFx.Fade(root, .8f);
            else UiFx.Rise(root);
            root.schedule.Execute(() => { if (!m.Closed && !Contains(root, Focused(root))) FocusFirst(root); });
            FocusFirst(root);
            return m;
        }

        // [data-autofocus] is the "autofocus" class.
        public static void FocusFirst(VisualElement root)
        {
            VisualElement target = null;
            root.Query<VisualElement>(className: "autofocus").ForEach(n => { if (target == null && n.enabledInHierarchy && Visible(n, root)) target = n; });
            if (target == null) { var f = Focusables(root); target = f.Count > 0 ? f[0] : root; }
            target.Focus();
        }

        // Info labels (text + icon, never colour alone).
        public static readonly Dictionary<string, LabelInfo> LABELS = new Dictionary<string, LabelInfo>
        {
            ["official"] = new LabelInfo { text = "공식 안내", icon = "official" },
            ["site"] = new LabelInfo { text = "현장 확인", icon = "site" },
            ["neighbor"] = new LabelInfo { text = "이웃에게 들음", icon = "neighbor" },
            ["rumor"] = new LabelInfo { text = "소문·미확인", icon = "rumor" },
            ["self"] = new LabelInfo { text = "내 기록", icon = "self" },
        };
        public static VisualElement LabelTag(string label)
        {
            var known = label != null && LABELS.ContainsKey(label);
            var L = known ? LABELS[label] : LABELS["self"];
            return El("span", "tag tag-" + (known ? label : "self"), Icon(L.icon), L.text);
        }

        // Stroke icons, 24x24.
        public static VisualElement Icon(string name, string label = "", string className = "")
        {
            var i = new UiIcon(name) { name = "svg" };
            if (!string.IsNullOrEmpty(label)) i.tooltip = label;
            UiStyle.AddClass(i, ("icon " + (className ?? "")).Trim());
            return i;
        }

        public static void UiSound(string name) => Core.GameEvents.Ui(name);

        // Shared button factory: btn('확인', { primary, icon, onclick, ... }). Data-autofocus: className "autofocus".
        public static Button Btn(string label, Action onclick = null, bool primary = false, bool ghost = false, string iconName = null, string className = "")
        {
            var cls = string.Join(" ", new[] { "btn", primary ? "primary" : "", ghost ? "ghost" : "", className ?? "" }).Trim();
            var b = (Button)El("button", cls, iconName != null ? Icon(iconName) : null, El("span", null, label ?? ""));
            if (onclick != null) b.clicked += onclick;
            return b;
        }

        // The label <span> of a btn().
        public static Label BtnLabel(Button b) => b.Q<Label>(name: "span");

        public static bool ReduceMotion() => UiRoot.ReduceMotion;
    }
}
