// style.css Landing; Pause, settings, about (debriefs in StyleDebrief).
using UnityEngine;
using UnityEngine.UIElements;
using static Gyeongbo.UI.Css;

namespace Gyeongbo.UI
{
    static class StyleMenus
    {
        static void R(string cls, System.Action<VisualElement> f) => UiStyle.Register(cls, f);
        static bool In(VisualElement e, string cls) => Within(e, cls) != null;
        static bool Small => Screen.width <= 820;

        internal static void Register()
        {
            // ---- landing ----
            R("landing-layer", e => { Pad(e, 0); Bg(e, "#05070c"); });
            R("landing", e =>
            {
                if (!Has(e, "modal")) return;
                e.style.width = Length.Percent(100); e.style.height = Length.Percent(100); e.style.maxWidth = StyleKeyword.None; e.style.maxHeight = StyleKeyword.None;
                Pad(e, 0); Border(e, 0, "#0000"); Radius(e, 0); Bg(e, "#05070c");
            });
            R("landing-wrap", e => { e.style.position = Position.Relative; e.style.flexGrow = 1; Col(e); e.style.overflow = Overflow.Hidden; });
            R("landing-art", e => Fill(e));
            R("landing-shade", e =>
            {
                Fill(e); e.pickingMode = PickingMode.Ignore;
                UiFx.BgImage(e, UiFx.Gradient(false, (0, "rgba(4,6,10,.92)"), (.46f, "rgba(4,6,10,.62)"), (1, "rgba(4,6,10,.12)")));
            });
            R("landing-shade-low", e => { Fill(e); e.pickingMode = PickingMode.Ignore; UiFx.BgImage(e, UiFx.Gradient(true, (0, "rgba(4,6,10,0)"), (.7f, "rgba(4,6,10,0)"), (1, "rgba(4,6,10,.9)"))); });
            R("landing-main", e =>
            {
                e.style.position = Position.Relative; e.style.flexGrow = 1; e.style.width = Length.Percent(100); e.style.maxWidth = 1360; e.style.alignSelf = Align.Center;
                float gap = Mathf.Clamp(Vw(5), 24, 72);
                if (Small) Col(e, gap); else Row(e, gap, false, Align.Center);
                if (Small) e.style.alignItems = Align.Stretch;
                float side = Mathf.Clamp(Vw(6), 16, 88);
                Pad(e, Mathf.Clamp(Vh(7), 28, 80), side, 24, side);
            });
            R("landing-hero", e => { e.style.flexGrow = 1.3f; e.style.flexShrink = 1; e.style.flexBasis = 0; e.style.minWidth = 0; });
            R("landing-chapters", e =>
            {
                if (!Small) { e.style.flexGrow = .7f; e.style.flexBasis = 0; e.style.minWidth = 280; }
                Pad(e, 18, 18, 12, 18); Radius(e, RADIUS_LG); Bg(e, "rgba(10,14,20,.66)"); Border(e, 1, LINE);
            });
            R("landing-eyebrow", e => { Row(e, Rem(.6f)); Margin(e, 0); Fg(e, TEXT_DIM); Font(e, .9f, 700); e.style.letterSpacing = Rem(.9f) * .04f; });
            R("live-dot", e => { Size(e, 9, 9); Radius(e, 4.5f); Bg(e, RED); });
            R("landing-title", e =>
            {
                Row(e, 0, true, Align.FlexEnd); Margin(e, Rem(.25f), 0, Rem(.3f), 0);
                float fs = Screen.width <= 560 ? Mathf.Clamp(Vw(15), Rem(2.6f), Rem(4)) : Mathf.Clamp(Vw(9.5f), Rem(3.2f), Rem(7.6f));
                e.style.fontSize = fs; Bold(e); e.style.letterSpacing = -.03f * fs;
            });
            R("bracket", e => { Fg(e, ACCENT); e.style.unityFontStyleAndWeight = FontStyle.Normal; });
            R("title-ko", e => { Fg(e, "#ffffff"); Pad(e, 0, 2); });   // ponytail: white-to-gold gradient text drawn white
            R("title-en", e =>
            {
                float fs = (Within(e, "landing-title")?.style.fontSize.value.value ?? Rem(4)) * .2f;
                e.style.width = Length.Percent(100); e.style.marginTop = fs * .55f; e.style.fontSize = fs; Bold(e); e.style.letterSpacing = fs * .55f; Fg(e, TEXT_DIM);
            });
            R("landing-pitch", e =>
            {
                Margin(e, 0, 0, Rem(.5f), 0); e.style.paddingLeft = Rem(.8f); BorderLeft(e, 4, ACCENT);
                e.style.fontSize = Mathf.Clamp(Vw(2.3f), Rem(1.2f), Rem(1.7f)); Bold(e); Fg(e, "#ffffff");
            });
            R("landing-sub", e => { Margin(e, 0, 0, Rem(1.2f), 0); e.style.maxWidth = Rem(1.02f) * 34; Fg(e, TEXT_DIM); Font(e, 1.02f); });
            R("landing-loop", e => { Row(e, Rem(.4f), true); Margin(e, 0, 0, Rem(1.6f), 0); Pad(e, 0); Bold(e); });
            R("t-li", e =>
            {
                if (e.parent == null || !Has(e.parent, "landing-loop")) return;
                Row(e, Rem(.4f)); Pad(e, Rem(.3f), Rem(.8f), Rem(.3f), Rem(.35f)); Radius(e, 999); Bg(e, "rgba(255,255,255,.06)"); Border(e, 1, LINE);
            });
            R("loop-arrow", e => { Fg(e, ACCENT); e.style.marginLeft = -Rem(.1f); });
            R("loop-no", e =>
            {
                float s = Rem(.78f) * 1.5f; Size(e, s, s); Radius(e, s / 2); Bg(e, ACCENT); Fg(e, ON_ACCENT); Font(e, .78f);
                e.style.unityTextAlign = TextAnchor.MiddleCenter;
            });
            R("landing-actions", e => Row(e, 10, true));
            R("t-h2", e => { if (e.parent != null && Has(e.parent, "landing-chapters")) { Margin(e, 0, 0, Rem(.6f), 0); Font(e, .9f); Fg(e, ACCENT); e.style.letterSpacing = Rem(.9f) * .08f; } });
            R("t-ol", e => { if (e.parent != null && Has(e.parent, "landing-chapters")) { Margin(e, 0); Pad(e, 0); } });
            R("t-li", e =>
            {
                if (e.parent == null || !In(e, "landing-chapters")) return;
                e.style.flexWrap = Wrap.NoWrap; e.style.alignItems = Align.FlexStart; Pad(e, Rem(.55f), 0);
                if (e.parent.IndexOf(e) > 0) BorderTop(e, 1, LINE);
            });
            R("ch-no", e => { e.style.width = Rem(4.6f); e.style.flexShrink = 0; e.style.marginRight = Rem(.6f); Fg(e, ACCENT); Font(e, .86f, 900); e.style.paddingTop = Rem(.1f); });
            R("ch-main", e => { Col(e, Rem(.05f)); e.style.alignItems = Align.FlexStart; e.style.flexShrink = 1; });
            R("t-b", e => { if (In(e, "ch-main")) Font(e, 1); });
            R("t-small", e => { if (In(e, "ch-main")) { Fg(e, MUTED); Font(e, .78f, 700); } });
            R("ch-line", e => { Fg(e, TEXT_DIM); Font(e, .86f); TextWrap(e); });
            R("landing-foot", e =>
            {
                e.style.position = Position.Relative; Row(e, 10, true); e.style.justifyContent = Justify.SpaceBetween;
                float side = Mathf.Clamp(Vw(6), 16, 88); Pad(e, 14, side, 18, side);
                BorderTop(e, 1, LINE); Bg(e, "rgba(4,6,10,.78)"); Font(e, .84f); Fg(e, TEXT_DIM);
            });
            R("fiction-note", e => { Row(e, Rem(.5f), false, Align.FlexStart); e.style.flexWrap = Wrap.NoWrap; Margin(e, 0); e.style.maxWidth = Rem(.84f) * 62; e.style.flexShrink = 1; });
            R("icon", e => { if (In(e, "fiction-note")) { Fg(e, TEAL); e.style.marginTop = Rem(.15f); } });
            R("t-span", e => { if (e.parent != null && Has(e.parent, "fiction-note")) { e.style.flexShrink = 1; TextWrap(e); } });
            R("keys-note", e => { Row(e, Rem(.9f)); Margin(e, 0); });
            R("t-span", e => { if (e.parent != null && Has(e.parent, "keys-note")) Row(e, Rem(.3f)); });

            // ---- pause ----
            R("pause", e => { if (Has(e, "modal")) MaxW(e, 560); });
            R("pause-panel", e => { e.style.flexShrink = 1; e.style.minHeight = 0; });
            R("pause-actions", e => Col(e, 8));
            R("pause-confirm", e => { e.style.marginTop = 10; Pad(e, Rem(.8f), Rem(1)); Radius(e, 12); Bg(e, "rgba(229,72,77,.12)"); Border(e, 1, "rgba(229,72,77,.45)"); });
            R("t-p", e => { if (e.parent != null && Has(e.parent, "pause-confirm")) Margin(e, 0, 0, Rem(.6f), 0); });
            R("row", e => { if (In(e, "pause-confirm") || Has(e.parent, "pause-confirm")) Row(e, 8); });
            R("key-list", e => { Col(e, Rem(.25f)); Margin(e, Rem(1.2f), 0, 0, 0); e.style.paddingTop = Rem(1); BorderTop(e, 1, LINE); Font(e, .86f); });
            R("key-row", e => Row(e, Rem(1), false, Align.FlexStart));
            R("t-dt", e => { if (In(e, "key-list")) { e.style.width = Length.Percent(42); e.style.flexShrink = 0; Fg(e, TEXT); Font(e, 0, 800); TextWrap(e); } });
            R("t-dd", e => { if (In(e, "key-list")) { Margin(e, 0); Fg(e, TEXT_DIM); e.style.flexShrink = 1; TextWrap(e); } });

            // ---- settings ----
            R("settings", e => { if (Has(e, "modal")) MaxW(e, 860); });
            foreach (var c in new[] { "settings-panel", "settings-form" }) R(c, e => { Col(e); e.style.flexShrink = 1; e.style.minHeight = 0; });
            R("set-scroll", e => { e.style.flexShrink = 1; e.style.minHeight = 0; });
            R("set-cols", e => { if (Small) Col(e, 18); else Row(e, 18, false, Align.FlexStart); });
            R("set-col", e => { e.style.flexGrow = 1; e.style.flexBasis = 0; e.style.minWidth = 0; });
            R("set-group", e => { Margin(e, 0, 0, 14, 0); Pad(e, 0); });
            R("t-legend", e => { if (In(e, "set-group")) { Pad(e, 0); e.style.marginBottom = Rem(.5f); Font(e, .88f, 900); Fg(e, ACCENT); e.style.letterSpacing = Rem(.88f) * .04f; } });
            R("seg", e => Row(e, 6, true, Align.Stretch));
            R("seg-opt", e =>
            {
                bool on = Has(e, "checked");
                e.style.flexGrow = 1; e.style.flexShrink = 1; e.style.flexBasis = 0; e.style.minWidth = Rem(5.5f);
                e.style.flexDirection = FlexDirection.Column; e.style.justifyContent = Justify.Center;
                Pad(e, Rem(.55f), Rem(.7f)); Radius(e, 10); Border(e, on ? 2 : 1, on ? ACCENT : LINE_STRONG);
                Bg(e, on ? "rgba(242,179,61,.15)" : Has(e, "is-hover") ? "rgba(255,255,255,.08)" : "rgba(255,255,255,.04)"); Fg(e, TEXT);
            });
            R("t-span", e => { if (e.parent is Button && Has(e.parent, "seg-opt")) { Col(e, Rem(.05f)); e.style.alignItems = Align.Center; } });
            R("t-small", e => { if (In(e, "seg-opt")) { Fg(e, MUTED); Font(e, .74f); TextWrap(e); e.style.unityTextAlign = TextAnchor.MiddleCenter; } });
            R("seg-check", e => Fg(e, ACCENT));
            R("set-note", e => { Margin(e, Rem(.4f), 0, 0, 0); Fg(e, MUTED); Font(e, .8f); });
            R("set-toggle", e =>
            {
                Row(e, Rem(.8f), false, Align.FlexStart); e.style.justifyContent = Justify.FlexStart; Pad(e, Rem(.6f), 0);
                e.style.backgroundColor = Color.clear; Border(e, 0, "#0000"); Fg(e, TEXT); e.style.unityTextAlign = TextAnchor.UpperLeft;
            });
            R("set-toggle-text", e => { Col(e); e.style.alignItems = Align.FlexStart; e.style.flexShrink = 1; });
            R("t-small", e => { if (In(e, "set-toggle")) { Fg(e, MUTED); Font(e, .8f); TextWrap(e); } });
            R("switch", e =>
            {
                e.style.position = Position.Relative; Size(e, 44, 26); e.style.flexShrink = 0; e.style.marginTop = Rem(.1f); Radius(e, 999);
                Bg(e, Has(Within(e, "set-toggle"), "on") ? TEAL : "rgba(255,255,255,.18)");
            });
            R("switch-knob", e =>
            {
                Abs(e, 3, null, null, 3); Size(e, 20, 20); Radius(e, 10); Bg(e, "#ffffff");
                e.style.translate = new Translate(Has(Within(e, "set-toggle"), "on") ? 18 : 0, 0);
            });
            R("set-range", e => { Row(e); e.style.justifyContent = Justify.SpaceBetween; Bold(e); });
            R("t-span", e => { if (e.parent != null && Has(e.parent, "set-range")) Row(e, Rem(.4f)); });
            R("set-slider", e => { e.style.width = Length.Percent(100); Margin(e, Rem(.5f), 0); e.style.minHeight = 24; });
            R("set-actions", e => { Row(e, 10); e.style.justifyContent = Justify.FlexEnd; e.style.marginTop = 12; e.style.paddingTop = 12; BorderTop(e, 1, LINE); e.style.flexShrink = 0; });

            // ---- about ----
            R("about-scroll", e => { e.style.maxHeight = Mathf.Min(Vh(60), 620); e.style.flexShrink = 1; e.style.paddingRight = Rem(.5f); });
            R("t-h3", e => { if (In(e, "about-scroll") && !In(e, "src-group")) { Row(e, Rem(.45f)); Margin(e, Rem(1), 0, Rem(.5f), 0); Font(e, 1.02f); } });
            R("about-fiction", e => { Pad(e, Rem(.2f), Rem(1), Rem(.6f), Rem(1)); Radius(e, 12); Bg(e, "rgba(60,198,181,.08)"); Border(e, 1, "rgba(60,198,181,.3)"); });
            R("t-p", e => { if (In(e, "about-fiction")) Fg(e, TEXT_DIM); });
            R("t-h4", e => { if (In(e, "src-group")) { Margin(e, Rem(.8f), 0, Rem(.3f), 0); Font(e, .9f); Fg(e, TEXT_DIM); } });
            R("t-small", e => { if (In(e, "src-group")) Fg(e, MUTED); });

            StyleDebrief.Register();
        }
    }
}
