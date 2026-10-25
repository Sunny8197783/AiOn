// style.css @keyframes (fade, rise, drop, shake, flash, pulse, spin, grow, ring, alarm, hurt) as scheduled tweens,
// plus JS key names for UI Toolkit key events (e.key / e.code / e.repeat) and small baked gradient textures.
// Reduced motion: tweens jump to their end state (style.css .reduce-motion * { animation-duration: .01ms }).
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class UiFx
    {
        static float Now => Time.realtimeSinceStartup;

        // Runs f(t) with t 0..1 over seconds (loop = repeat forever while attached). Returns a stop action.
        public static Action Tween(VisualElement e, float seconds, Action<float> f, bool loop = false, float delay = 0)
        {
            if (e == null || f == null) return () => { };
            if (UiRoot.ReduceMotion || seconds <= 0) { f(loop ? 0 : 1); return () => { }; }
            float t0 = Now + delay;
            bool stopped = false;
            IVisualElementScheduledItem item = null;
            item = e.schedule.Execute(() =>
            {
                if (stopped) return;
                float t = (Now - t0) / seconds;
                if (t < 0) return;
                if (loop) t -= Mathf.Floor(t);
                else if (t >= 1) { t = 1; stopped = true; item?.Pause(); }
                try { f(t); } catch (Exception ex) { Debug.LogException(ex); stopped = true; item?.Pause(); }
            }).Every(16);
            return () => { stopped = true; item?.Pause(); };
        }

        static float Ease(float t) => 1 - Mathf.Pow(1 - t, 3);   // ~ cubic-bezier(.2,.7,.2,1)

        public static void Fade(VisualElement e, float s = 0.2f) => Tween(e, s, t => e.style.opacity = t);
        public static void Rise(VisualElement e, float s = 0.3f) => Tween(e, s, t =>
        {
            float k = Ease(t);
            e.style.opacity = k;
            e.style.translate = new Translate(0, 12 * (1 - k));
            e.style.scale = new Scale(Vector2.one * (0.98f + 0.02f * k));
        });
        public static void Drop(VisualElement e, float s = 0.35f) => Tween(e, s, t =>
        {
            float k = Ease(t);
            e.style.opacity = k;
            e.style.translate = new Translate(0, -14 * (1 - k));
        });
        public static void LineIn(VisualElement e) => Tween(e, 0.35f, t =>
        {
            float k = Ease(t);
            e.style.opacity = k;
            e.style.translate = new Translate(0, 5 * (1 - k));
        });
        // @keyframes shake: 20%,60% -5px; 40%,80% +5px.
        public static void Shake(VisualElement e, float s = 0.35f, int times = 1, float delay = 0) => Tween(e, s * times, t =>
        {
            float u = (t * times) % 1f;
            float x = t >= 1 ? 0 : Mathf.Sin(u * Mathf.PI * 5) * 5;
            e.style.translate = new Translate(x, 0);
        }, false, delay);
        // @keyframes flash: a ring of accent fading out (border colour stands in for the box-shadow).
        public static void Flash(VisualElement e) => Tween(e, 1.2f, t =>
        {
            var c = Css.C(Css.ACCENT); c.a = 0.7f * (1 - t);
            e.style.borderTopColor = c; e.style.borderRightColor = c; e.style.borderBottomColor = c;
            if (t >= 1) { e.style.borderTopColor = StyleKeyword.Null; e.style.borderRightColor = StyleKeyword.Null; e.style.borderBottomColor = StyleKeyword.Null; }
        });
        public static Action Spin(VisualElement e, float s) => Tween(e, s, t => e.style.rotate = new Rotate(360 * t), true);
        public static Action Pulse(VisualElement e, float s = 2.4f) => Tween(e, s, t => e.style.opacity = 1 - 0.45f * Mathf.Sin(t * Mathf.PI), true);
        public static void Grow(VisualElement e, float s) => Tween(e, s, t => e.style.width = Length.Percent(100 * t));
        // .loader-symbol::before/::after rings.
        public static void Ring(VisualElement sym)
        {
            for (int i = 0; i < 2; i++)
            {
                var r = new VisualElement { pickingMode = PickingMode.Ignore };
                Css.Abs(r, -2, -2, -2, -2);
                Css.Border(r, 2, Css.ACCENT);
                Css.Radius(r, 40);
                r.style.opacity = 0;
                sym.Add(r);
                Tween(r, 2, t => { r.style.scale = new Scale(Vector2.one * (0.6f + 1.2f * Ease(t))); r.style.opacity = 0.9f * (1 - t); }, true, i);
            }
        }

        // ---- keys ----
        static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> repeating = new HashSet<KeyCode>();
        // Called by Dom.Install: tracks held keys so KeyDown can tell a repeat (UI Toolkit has no e.repeat).
        internal static void TrackKeys(VisualElement root)
        {
            root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.None) return;
                if (!held.Add(e.keyCode)) repeating.Add(e.keyCode); else repeating.Remove(e.keyCode);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyUpEvent>(e => { held.Remove(e.keyCode); repeating.Remove(e.keyCode); }, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(e => { if (e.relatedTarget == null) { held.Clear(); repeating.Clear(); } }, TrickleDown.TrickleDown);
        }
        public static bool Repeat(KeyDownEvent e) => e != null && repeating.Contains(e.keyCode);
        // Enter / Space held down (auto-repeat): a held interact key must not press buttons.
        public static bool SubmitRepeating() => repeating.Contains(KeyCode.Return) || repeating.Contains(KeyCode.KeypadEnter) || repeating.Contains(KeyCode.Space);

        // JS e.key for the keys the UI looks at ("Escape", "Enter", " ", "Tab", "ArrowUp", "1".."9", "Shift"...).
        public static string Key(KeyDownEvent e)
        {
            if (e == null) return "";
            switch (e.keyCode)
            {
                case KeyCode.Escape: return "Escape";
                case KeyCode.Return: case KeyCode.KeypadEnter: return "Enter";
                case KeyCode.Space: return " ";
                case KeyCode.Tab: return "Tab";
                case KeyCode.UpArrow: return "ArrowUp";
                case KeyCode.DownArrow: return "ArrowDown";
                case KeyCode.LeftArrow: return "ArrowLeft";
                case KeyCode.RightArrow: return "ArrowRight";
                case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl: case KeyCode.RightControl: return "Control";
                case KeyCode.LeftAlt: case KeyCode.RightAlt: case KeyCode.AltGr: return "Alt";
                case KeyCode.LeftCommand: case KeyCode.RightCommand: case KeyCode.LeftWindows: case KeyCode.RightWindows: return "Meta";
            }
            if (e.keyCode >= KeyCode.Alpha0 && e.keyCode <= KeyCode.Alpha9) return ((int)(e.keyCode - KeyCode.Alpha0)).ToString();
            if (e.keyCode >= KeyCode.Keypad0 && e.keyCode <= KeyCode.Keypad9) return ((int)(e.keyCode - KeyCode.Keypad0)).ToString();
            if (e.keyCode >= KeyCode.A && e.keyCode <= KeyCode.Z) return ((char)('a' + (e.keyCode - KeyCode.A))).ToString();
            return e.keyCode.ToString();
        }

        // JS e.code ("KeyE", "Digit1", "Escape", "Space", "Enter", "Tab", "ArrowUp").
        public static string Code(KeyDownEvent e)
        {
            if (e == null) return "";
            if (e.keyCode >= KeyCode.A && e.keyCode <= KeyCode.Z) return "Key" + (char)('A' + (e.keyCode - KeyCode.A));
            if (e.keyCode >= KeyCode.Alpha0 && e.keyCode <= KeyCode.Alpha9) return "Digit" + (int)(e.keyCode - KeyCode.Alpha0);
            if (e.keyCode == KeyCode.Space) return "Space";
            if (e.keyCode == KeyCode.KeypadEnter) return "NumpadEnter";
            var k = Key(e);
            return k == "Shift" || k == "Control" || k == "Alt" || k == "Meta" ? k + (e.keyCode.ToString().StartsWith("Right") ? "Right" : "Left") : k;
        }

        // ---- baked textures for CSS gradients ----
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        // radial-gradient(ellipse at center, inner 'clear'% transparent -> edge colour): vignettes.
        public static Texture2D Vignette(float clear, Color edge)
        {
            string key = "v" + clear + edge;
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            const int N = 64;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2) * 1.414f;   // ellipse: 1 at the side centres
                    float k = Mathf.Clamp01((d - clear) / (1 - clear));
                    var c = edge; c.a *= k;
                    px[y * N + x] = c;
                }
            t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px); t.Apply(false);
            return cache[key] = t;
        }

        // Linear gradient from top to bottom over n stops (vertical: true) or left to right.
        public static Texture2D Gradient(bool vertical, params (float at, string css)[] stops)
        {
            string key = (vertical ? "gv" : "gh") + string.Join("|", Array.ConvertAll(stops, s => s.at + s.css));
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            const int N = 64;
            var g = new PaintGradient(0, 0, vertical ? 0 : N, vertical ? N : 0);
            foreach (var s in stops) g.addColorStop(s.at, s.css);
            var px = new Color32[N];
            for (int i = 0; i < N; i++) px[i] = g.At(vertical ? 0 : i + 0.5f, vertical ? i + 0.5f : 0);
            t = vertical ? new Texture2D(1, N, TextureFormat.RGBA32, false) : new Texture2D(N, 1, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            if (vertical) { var flip = new Color32[N]; for (int i = 0; i < N; i++) flip[i] = px[N - 1 - i]; px = flip; }   // texture row 0 = bottom
            t.SetPixels32(px); t.Apply(false);
            return cache[key] = t;
        }

        public static void BgImage(VisualElement e, Texture2D t)
        {
            e.style.backgroundImage = new StyleBackground(t);
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
        }
    }
}
