// Small helpers the minigame ports share on top of MiniDom/UiStyle: typed elements (Button, Label) with classes,
// text updates, hidden, a Paint shown as a canvas element, the per-frame loop (requestAnimationFrame) and
// KeyDownEvent -> JS KeyboardEvent.key names.
using System;
using System.Threading.Tasks;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    internal static class Mg
    {
        public static T Cls<T>(T e, string cls) where T : VisualElement
        {
            if (!string.IsNullOrEmpty(cls)) UiStyle.AddClass(e, cls.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            return e;
        }

        public static VisualElement El(string tag, string cls, params object[] kids) => MiniDom.El(tag, cls, kids);
        public static Label Text(string cls, string text = "") => Cls(new Label(text ?? ""), cls);

        public static Button Btn(string cls, Action onClick, params object[] kids)
        {
            var b = Cls(new Button(), cls);
            if (kids.Length == 1 && kids[0] is string s) b.text = s; else MiniDom.Put(b, kids);
            if (onClick != null) b.clicked += onClick;
            return b;
        }

        // element.textContent = s
        public static void SetText(VisualElement e, string s)
        {
            if (e is TextElement t) { t.Clear(); t.text = s ?? ""; }
            else { e.Clear(); e.Add(new Label(s ?? "")); }
        }

        // replaceChildren(...kids)
        public static void Replace(VisualElement parent, params object[] kids) { parent.Clear(); MiniDom.Put(parent, kids); }

        // the [hidden] attribute = UiStyle's "hidden" class.
        public static void Hidden(VisualElement e, bool hidden) => UiStyle.Toggle(e, "hidden", hidden);
        public static bool IsHidden(VisualElement e) => e.ClassListContains("hidden");

        public static Color C(string css) => J.Css(css);

        // String(n) for a JS number; Math.round (halves up).
        public static string Num(double n) => n.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        public static double Round(double x) { var r = Math.Floor(x); return x - r >= 0.5 ? r + 1 : r; }
        // n.toFixed(1)
        public static string Fixed1(double n) => (Round(n * 10) / 10).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        // Shift held on a click (e.shiftKey).
        public static void OnClick(Button b, Action<bool> fn) =>
            b.clickable.clickedWithEventInfo += (ev) => fn(ev is IPointerEvent p ? p.shiftKey : ev is IMouseEvent m ? m.shiftKey : ev is KeyboardEventBase<KeyDownEvent> k && k.shiftKey);

        public static VisualElement Focused() => UiRoot.Root?.focusController?.focusedElement as VisualElement;

        // KeyboardEvent.key for the keys the minigames read ("" when not one of them).
        public static string Key(IKeyboardEvent e)
        {
            switch (e.keyCode)
            {
                case KeyCode.Escape: return "Escape";
                case KeyCode.LeftArrow: return "ArrowLeft";
                case KeyCode.RightArrow: return "ArrowRight";
                case KeyCode.UpArrow: return "ArrowUp";
                case KeyCode.DownArrow: return "ArrowDown";
                case KeyCode.Return: case KeyCode.KeypadEnter: return "Enter";
                case KeyCode.Space: return " ";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.Delete: return "Delete";
                case KeyCode.PageUp: return "PageUp";
                case KeyCode.PageDown: return "PageDown";
                case KeyCode.Home: return "Home";
                case KeyCode.End: return "End";
                case KeyCode.Tab: return "Tab";
                case KeyCode.M: return "m";
                case KeyCode.E: return "e";
                default: return "";
            }
        }

        // requestAnimationFrame loop: tick(dt, t) each frame (dt capped at 0.1 s like the JS); return false to stop.
        public static void Loop(Func<float, float, bool> tick) => _ = Run(tick);
        static async Task Run(Func<float, float, bool> tick)
        {
            try
            {
                while (true)
                {
                    await Awaitable.NextFrameAsync();
                    if (!tick(Mathf.Min(0.1f, Time.unscaledDeltaTime), Time.unscaledTime)) return;
                }
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // setTimeout(fn, ms) on the element's scheduler.
        public static IVisualElementScheduledItem Later(VisualElement on, float seconds, Action fn) => on.schedule.Execute(fn).StartingIn((long)(seconds * 1000));
    }

    // A <canvas>: a Paint drawn on the CPU, shown as the element's background, uploaded after each draw.
    // The element keeps the canvas aspect ratio at whatever width the layout gives it.
    internal sealed class MgCanvas
    {
        public Paint g;
        public Texture2D tex;
        public readonly Image el;

        public MgCanvas(int w, int h, string cls)
        {
            el = Mg.Cls(new Image { scaleMode = ScaleMode.StretchToFill }, cls);
            el.style.width = Length.Percent(100);
            el.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float want = el.resolvedStyle.width * g.height / g.width;
                if (want > 0 && Mathf.Abs(el.resolvedStyle.height - want) > 0.5f) el.style.height = want;
            });
            Resize(w, h);
        }

        public int W => g.width;
        public int H => g.height;

        public void Resize(int w, int h)
        {
            if (g != null && g.width == w && g.height == h) return;
            if (tex != null) UnityEngine.Object.Destroy(tex);
            g = new Paint(w, h);
            tex = g.ToTexture(true, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            el.image = tex;   // not a style: survives UiStyle restyles
        }

        public void Upload() => g.Upload(tex);
        public void Dispose() { if (tex != null) UnityEngine.Object.Destroy(tex); tex = null; }
    }
}
