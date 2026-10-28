// style.css HUD section: chapter + clock, objective, chips, stats, prompt, keys, vignette, hurt, letterbox,
// banner, toasts, captions, compass, minimap, stamina.
using UnityEngine;
using UnityEngine.UIElements;
using static Gyeongbo.UI.Css;

namespace Gyeongbo.UI
{
    static class StyleHud
    {
        static void R(string cls, System.Action<VisualElement> f) => UiStyle.Register(cls, f);
        static void CenterX(VisualElement e) { e.style.left = Length.Percent(50); e.style.translate = new Translate(Length.Percent(-50), 0); }

        internal static void Register()
        {
            R("hud", e => { Fill(e); Fg(e, TEXT); });
            // .hud > * { transition: opacity .5s } and .hud.cinematic > :not(.letterbox):not(.hud-vignette):not(.hud-banner) { opacity: 0 }
            R("hud-child", e =>
            {
                Transition(e, "opacity", .5f);
                if (Has(e.parent, "cinematic") && !Has(e, "letterbox") && !Has(e, "hud-vignette") && !Has(e, "hud-banner")) e.style.opacity = 0;
            });
            R("hud-tl", e => { Abs(e, 16, null, null, 16); e.style.width = Mathf.Min(330, Screen.width - 32 - (Screen.width <= 560 ? 120 : 0)); Col(e, 8); });
            R("hud-chapter", e => { Row(e, Rem(.55f), false, Align.FlexEnd); });
            R("hud-chapter-no", e => { Fg(e, ACCENT); Font(e, .86f, 900); e.style.letterSpacing = Rem(.86f) * .06f; Shadow(e, "rgba(0,0,0,.7)", 12, 2); });
            R("hud-chapter-title", e => { Font(e, .95f, 700); Shadow(e, "rgba(0,0,0,.7)", 12, 2); });
            R("hud-clock", e => { Row(e, Rem(.45f)); e.style.alignSelf = Align.FlexStart; Pad(e, Rem(.3f), Rem(.8f)); Radius(e, 999); Bg(e, PANEL); Border(e, 1, LINE); Bold(e); });
            R("hud-objective", e => { Pad(e, 12, 14); BorderLeft(e, 3, ACCENT); });
            R("obj-eyebrow", e => { Row(e, Rem(.35f)); Margin(e, 0); Fg(e, ACCENT); Font(e, .72f, 900); e.style.letterSpacing = Rem(.72f) * .1f; });
            R("obj-title", e => { Margin(e, Rem(.15f), 0, Rem(.1f), 0); Font(e, 1.05f); });
            R("obj-body", e => { Margin(e, 0); Fg(e, TEXT_DIM); Font(e, .86f); if (Screen.width <= 560) e.style.display = DisplayStyle.None; });
            R("obj-steps", e => { Margin(e, Rem(.5f), 0, 0, 0); e.style.paddingLeft = 0; Col(e, Rem(.2f)); Font(e, .86f); if (Screen.width <= 560) e.style.display = DisplayStyle.None; });
            R("obj-step", e =>
            {
                Row(e, Rem(.45f)); bool done = Has(e, "done");
                Fg(e, done ? MUTED : TEXT_DIM);
                foreach (var i in e.Query<UiIcon>().ToList()) { i.em = 1; i.style.color = C(done ? TEAL : MUTED); }
            });
            R("hud-tr", e => { Abs(e, 16, 16); Row(e, 8); });
            R("hud-chip", e =>
            {
                Row(e, Rem(.5f)); Pad(e, Rem(.35f), Rem(.8f)); Radius(e, 999); Bg(e, PANEL); Border(e, 1, LINE); Font(e, .86f, 800);
                if (Has(e, "hud-battery")) { Fg(e, Has(e, "dead") ? MUTED : Has(e, "low") ? "#ffb4b6" : TEXT); if (Has(e, "low")) BorderColor(e, "rgba(229,72,77,.6)"); }
                if (Has(e, "hud-bag") && Has(e, "over")) BorderColor(e, "rgba(229,72,77,.6)");
            });
            R("hud-stats", e =>
            {
                Abs(e, null, null, 16, 16); e.style.width = Screen.width <= 560 ? Screen.width - 32 : 236; Pad(e, 10, 12); Col(e, 6);
                if (Screen.width <= 560) { Row(e, 12, true); }
            });
            R("stat", e =>
            {
                Row(e, Rem(.45f)); Font(e, .82f, 700);
                if (Screen.width <= 560) e.style.width = Length.Percent(45);
                var bad = Has(e, "st-bad");
                foreach (var i in e.Query<UiIcon>().ToList()) { i.style.color = bad ? C("#ff9ea1") : (StyleColor)StyleKeyword.Null; }
            });
            R("stat-label", e => { Fg(e, TEXT_DIM); e.style.width = Rem(2.6f); });
            R("stat-bar", e => { e.style.height = 6; e.style.flexGrow = 1; Radius(e, 4); Bg(e, "rgba(255,255,255,.12)"); e.style.overflow = Overflow.Hidden; if (Screen.width <= 560) e.style.display = DisplayStyle.None; });
            R("stat-fill", e =>
            {
                e.style.height = Length.Percent(100); Radius(e, 4); Transition(e, "width", .5f);
                var row = Within(e, "stat");
                Bg(e, Has(row, "st-bad") ? RED : Has(row, "st-mid") ? ACCENT : TEAL);
            });
            R("stat-word", e =>
            {
                e.style.width = Rem(2.6f); e.style.unityTextAlign = TextAnchor.MiddleRight; Font(e, .78f);
                var row = Within(e, "stat");
                if (Has(row, "st-bad")) Fg(e, "#ff9ea1"); else if (Has(row, "st-mid")) Fg(e, "#ffd27f");
            });
            R("hud-prompt", e =>
            {
                Abs(e, null, null, Screen.height * .2f); CenterX(e); Row(e, Rem(.6f)); Pad(e, Rem(.5f), Rem(1), Rem(.5f), Rem(.55f));
                Radius(e, 999); Bg(e, "rgba(10,15,18,.82)"); Border(e, 1, "rgba(60,198,181,.55)"); Bold(e); NoWrap(e);
            });
            R("hud-keys", e =>
            {
                Abs(e, null, 16, 16); Margin(e, 0); Row(e, Rem(.8f), true); e.style.justifyContent = Justify.FlexEnd; e.style.maxWidth = 380;
                Font(e, .74f); Fg(e, TEXT_DIM); e.style.opacity = .75f; Shadow(e, "#000", 6, 1);
                if (Screen.width <= 820) e.style.display = DisplayStyle.None;
            });
            R("hud-key", e => Row(e, Rem(.3f)));
            R("hud-vignette", e => { Fill(e); UiFx.BgImage(e, UiFx.Vignette(.5f, new Color(110 / 255f, 8 / 255f, 14 / 255f, 1))); });
            R("hud-hurt", e => { Fill(e); UiFx.BgImage(e, UiFx.Vignette(.35f, new Color(170 / 255f, 0, 0, 1))); });
            R("letterbox", e =>
            {
                Abs(e, null, 0, null, 0); e.style.height = Vh(11); Bg(e, "#000");
                bool top = Has(e, "top");
                if (top) e.style.top = 0; else e.style.bottom = 0;
                e.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(top ? 0 : 100));
                Transition(e, "scale", .7f);
                e.style.scale = new Scale(new Vector2(1, Has(e.parent, "cinematic") ? 1 : 0));
            });
            R("hud-banner", e =>
            {
                Abs(e, Screen.height * .3f, 0, null, 0); Col(e, Rem(.2f)); e.style.alignItems = Align.Center;
                bool show = Has(e, "show");
                e.style.opacity = show ? 1 : 0; e.style.translate = new Translate(0, show ? 0 : 8);
                e.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName> { "opacity", "translate" };
                e.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { UiRoot.ReduceMotion ? 0.0001f : .9f, UiRoot.ReduceMotion ? 0.0001f : .9f };
                Shadow(e, "rgba(0,0,0,.9)", 30, 4);
            });
            R("banner-no", e => { Fg(e, ACCENT); Font(e, 1, 900); e.style.letterSpacing = Rem(1) * .3f; });
            R("banner-title", e => Font(e, Mathf.Clamp(Vw(4.5f) / Rem(1), 1.8f, 3), 900));
            R("hud-float", e => Fill(e));
            R("toasts", e => { Abs(e, 16); CenterX(e); e.style.width = Mathf.Min(520, Screen.width - 32); Col(e, 8); });
            R("toast", e =>
            {
                Row(e, Rem(.6f), false, Align.FlexStart); Pad(e, Rem(.65f), Rem(.9f)); Radius(e, 12); Bg(e, "rgba(12,18,21,.92)"); Border(e, 1, LINE); Font(e, .92f);
                e.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName> { "opacity", "translate" };
                e.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { UiRoot.ReduceMotion ? 0.0001f : .4f, UiRoot.ReduceMotion ? 0.0001f : .4f };
                if (Has(e, "out")) { e.style.opacity = 0; e.style.translate = new Translate(0, -6); }
                var tag = e.Q(className: "tag"); if (tag != null) tag.style.marginTop = Rem(.1f);
            });
            R("toast-text", e => { e.style.flexShrink = 1; TextWrap(e); });
            R("captions", e => { Abs(e, null, null, Vh(11) + 22); CenterX(e); e.style.width = Mathf.Min(760, Screen.width - 32); e.style.alignItems = Align.Center; });
            R("caption", e =>
            {
                Row(e, 0, true); e.style.justifyContent = Justify.Center; Pad(e, Rem(.6f), Rem(1.1f)); Radius(e, 12); Bg(e, "rgba(0,0,0,.78)"); Font(e, 1.05f);
                e.style.unityTextAlign = TextAnchor.MiddleCenter; Transition(e, "opacity", .35f);
                if (Has(e, "out")) e.style.opacity = 0;
            });
            R("caption-speaker", e => { Fg(e, ACCENT); e.style.marginRight = Rem(.5f); });
            R("hud-compass", e => { Abs(e, Screen.width <= 720 ? 8 : 12); CenterX(e); e.style.width = Mathf.Min(360, Screen.width - 32); e.style.height = 34; });
            R("hud-map", e => { bool sm = Screen.width <= 720; Abs(e, sm ? 54 : 58, 16); Size(e, sm ? 128 : 184, sm ? 128 : 184); });
            R("hud-minimap", e => { e.style.width = Length.Percent(100); e.style.height = Length.Percent(100); });
            R("hud-stamina", e =>
            {
                Abs(e, null, null, Screen.height * .2f - 44); CenterX(e); Row(e, Rem(.5f)); Font(e, .72f, 800); Fg(e, TEXT_DIM); Shadow(e, "#000", 4, 1);
            });
            R("stamina-bar", e => { Size(e, 160, 6); Radius(e, 3); Bg(e, "rgba(255,255,255,.14)"); e.style.overflow = Overflow.Hidden; });
            R("stamina-fill", e => { e.style.height = Length.Percent(100); Bg(e, Has(Within(e, "hud-stamina"), "winded") ? "#ff8a6a" : "#7fe0a0"); });
        }
    }
}
