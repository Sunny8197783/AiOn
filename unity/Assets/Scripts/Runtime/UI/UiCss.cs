// Small inline-style helpers for the UiStyle rules (CSS values -> UI Toolkit IStyle). Lengths are px; rem is
// 16px * text scale (html font-size). em is approximated by rem.
// ponytail: em = rem, vh/vw read once from Screen when the rule runs (no resize re-layout); no grid, gap,
// box-shadow, backdrop-filter or gradients in UI Toolkit, so grids become flex rows/columns, gaps become child
// margins (Css.Gap) and gradients become solid colours or small baked textures.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class Css
    {
        // style.css :root tokens.
        public const string BG = "#0f1416", BG2 = "#141c1f";
        public const string PANEL = "rgba(13,19,22,.74)", PANEL_STRONG = "rgba(15,22,26,.95)", PANEL_SOLID = "#121a1d";
        public const string LINE = "rgba(255,255,255,.1)", LINE_STRONG = "rgba(255,255,255,.22)";
        public const string TEXT = "#eef3f4", TEXT_DIM = "#b8c5c9", MUTED = "#8ea0a6";
        public const string ACCENT = "#f2b33d", ON_ACCENT = "#1d1505";
        public const string TEAL = "#3cc6b5", BLUE = "#5aa6ff", VIOLET = "#a98bff", GREY = "#9aa6ab", RED = "#e5484d", GREEN = "#4cc38a";
        public const string PAPER = "#f4f0e7", PAPER2 = "#fffdf8", INK = "#1c2326", INK_DIM = "#5b666b";
        public const float RADIUS_SM = 10, RADIUS = 14, RADIUS_LG = 18;

        public static Color C(string css) => PaintColor.Parse(css);
        public static float TextScale = 1;
        public static float Rem(float r) => r * 16 * TextScale;
        public static float Vh(float v) => Screen.height * v / 100f;
        public static float Vw(float v) => Screen.width * v / 100f;

        public static bool Has(VisualElement e, string cls) => e != null && e.ClassListContains(cls);
        // Nearest ancestor (not e itself) with the class, or null: descendant selectors.
        public static VisualElement Within(VisualElement e, string cls)
        {
            for (var p = e?.parent; p != null; p = p.parent) if (p.ClassListContains(cls)) return p;
            return null;
        }

        public static void Bg(VisualElement e, string css) => e.style.backgroundColor = C(css);
        public static void Fg(VisualElement e, string css) => e.style.color = C(css);
        public static void Font(VisualElement e, float rem, int weight = 0)
        {
            if (rem > 0) e.style.fontSize = Rem(rem);
            if (weight > 0) e.style.unityFontStyleAndWeight = weight >= 600 ? FontStyle.Bold : FontStyle.Normal;
        }
        public static void Bold(VisualElement e) => e.style.unityFontStyleAndWeight = FontStyle.Bold;
        public static void Pad(VisualElement e, float t, float r = -1, float b = -1, float l = -1)
        {
            if (r < 0) r = t; if (b < 0) b = t; if (l < 0) l = r;
            e.style.paddingTop = t; e.style.paddingRight = r; e.style.paddingBottom = b; e.style.paddingLeft = l;
        }
        public static void Margin(VisualElement e, float t, float r = -1, float b = -1, float l = -1)
        {
            if (r < 0) r = t; if (b < 0) b = t; if (l < 0) l = r;
            e.style.marginTop = t; e.style.marginRight = r; e.style.marginBottom = b; e.style.marginLeft = l;
        }
        public static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }
        public static void Border(VisualElement e, float w, string css)
        {
            e.style.borderTopWidth = w; e.style.borderRightWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w;
            BorderColor(e, css);
        }
        public static void BorderColor(VisualElement e, string css)
        {
            var c = C(css);
            e.style.borderTopColor = c; e.style.borderRightColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c;
        }
        public static void BorderLeft(VisualElement e, float w, string css) { e.style.borderLeftWidth = w; e.style.borderLeftColor = C(css); }
        public static void BorderTop(VisualElement e, float w, string css) { e.style.borderTopWidth = w; e.style.borderTopColor = C(css); }
        public static void BorderBottom(VisualElement e, float w, string css) { e.style.borderBottomWidth = w; e.style.borderBottomColor = C(css); }

        // display:flex rows / columns.
        public static void Row(VisualElement e, float gap = 0, bool wrap = false, Align align = Align.Center)
        {
            e.style.flexDirection = FlexDirection.Row; e.style.alignItems = align;
            if (wrap) e.style.flexWrap = Wrap.Wrap;
            if (gap > 0) Gap(e, gap, wrap ? gap : 0);
        }
        public static void Col(VisualElement e, float gap = 0)
        {
            e.style.flexDirection = FlexDirection.Column;
            if (gap > 0) Gap(e, gap);
        }
        public static void Center(VisualElement e)
        {
            e.style.alignItems = Align.Center; e.style.justifyContent = Justify.Center;
        }
        public static void Abs(VisualElement e, float? top = null, float? right = null, float? bottom = null, float? left = null)
        {
            e.style.position = Position.Absolute;
            if (top.HasValue) e.style.top = top.Value;
            if (right.HasValue) e.style.right = right.Value;
            if (bottom.HasValue) e.style.bottom = bottom.Value;
            if (left.HasValue) e.style.left = left.Value;
        }
        public static void Fill(VisualElement e) => Abs(e, 0, 0, 0, 0);
        public static void Size(VisualElement e, float w, float h) { e.style.width = w; e.style.height = h; }
        public static void MaxW(VisualElement e, float px) { e.style.width = Length.Percent(100); e.style.maxWidth = px; }
        public static void TextWrap(VisualElement e) => e.style.whiteSpace = WhiteSpace.Normal;
        public static void NoWrap(VisualElement e) => e.style.whiteSpace = WhiteSpace.NoWrap;
        public static void Shadow(VisualElement e, string css = "rgba(0,0,0,.7)", float blur = 6, float y = 1)
            => e.style.textShadow = new TextShadow { color = C(css), blurRadius = blur, offset = new Vector2(0, y) };
        public static void Transition(VisualElement e, string prop, float seconds)
        {
            float s = UiRoot.ReduceMotion ? 0.0001f : seconds;
            e.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName(prop) };
            e.style.transitionDuration = new List<TimeValue> { new TimeValue(s, TimeUnit.Second) };
            e.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOut) };
        }

        // gap: margins between children, re-applied whenever Dom/MiniDom add children.
        sealed class GapInfo { public float main, cross; }
        static readonly ConditionalWeakTable<VisualElement, GapInfo> gaps = new ConditionalWeakTable<VisualElement, GapInfo>();
        public static void Gap(VisualElement e, float main, float cross = 0)
        {
            gaps.Remove(e);
            gaps.Add(e, new GapInfo { main = main, cross = cross });
            ApplyGap(e);
        }
        public static void ApplyGap(VisualElement e)
        {
            if (e == null || !gaps.TryGetValue(e, out var g)) return;
            bool row = e.style.flexDirection.value == FlexDirection.Row;
            int last = -1;
            for (int i = e.childCount - 1; i >= 0; i--) if (!UiStyle.IsHidden(e[i]) && e[i].style.position.value != Position.Absolute) { last = i; break; }
            for (int i = 0; i < e.childCount; i++)
            {
                var k = e[i];
                if (k.style.position.value == Position.Absolute) continue;
                float m = i < last ? g.main : 0;
                if (row) { k.style.marginRight = m; if (g.cross > 0) k.style.marginBottom = g.cross; }
                else k.style.marginBottom = m;
            }
        }
    }
}
