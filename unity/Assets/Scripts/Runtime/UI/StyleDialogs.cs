// style.css Modals, Talk, Story cards, Time skip.
using UnityEngine;
using UnityEngine.UIElements;
using static Gyeongbo.UI.Css;

namespace Gyeongbo.UI
{
    static class StyleDialogs
    {
        static void R(string cls, System.Action<VisualElement> f) => UiStyle.Register(cls, f);
        static VisualElement Modal(VisualElement e) => Has(e, "modal") ? e : Within(e, "modal");

        internal static void Register()
        {
            R("modal-layer", e =>
            {
                Fill(e); Col(e); Center(e); Pad(e, 16); Bg(e, "rgba(4,8,10,.58)");
                if (Has(e, "no-backdrop")) e.style.backgroundColor = Color.clear;
            });
            R("modal", e =>
            {
                e.style.position = Position.Relative; MaxW(e, 720); e.style.maxHeight = Screen.height - 32; e.style.flexShrink = 1;
                e.style.overflow = Overflow.Hidden;
                if (Screen.width <= 560) Pad(e, 18, 16); else Pad(e, 24, 26);
                Radius(e, RADIUS_LG); Bg(e, PANEL_STRONG); Border(e, 1, LINE); Fg(e, TEXT);
            });
            // .modal h2
            R("t-h2", e => { if (Modal(e) != null) { Margin(e, Rem(.1f), 0, Rem(.5f), 0); Font(e, 1.5f); } });
            R("nb-head", e => { e.style.position = Position.Relative; e.style.paddingRight = Screen.width <= 560 ? 0 : Rem(7); });

            // ---- talk ----
            R("talk-layer", e =>
            {
                e.style.justifyContent = Justify.FlexEnd; Pad(e, 16, 16, Mathf.Max(28, Vh(4)), 16);
                e.style.backgroundColor = Color.clear;
                UiFx.BgImage(e, UiFx.Gradient(true, (0, "rgba(3,6,8,0)"), (.5f, "rgba(3,6,8,0)"), (1, "rgba(3,6,8,.7)")));
            });
            R("talk", e => { if (!Has(e, "modal")) return; MaxW(e, 880); Pad(e, 0); e.style.backgroundColor = Color.clear; Border(e, 0, "#0000"); e.style.overflow = Overflow.Visible; });
            R("talk-box", e =>
            {
                bool sm = Screen.width <= 560;
                if (sm) Col(e); else Row(e, 16, false, Align.FlexStart);
                Pad(e, sm ? 14 : 18, sm ? 14 : 20); Radius(e, RADIUS_LG); Bg(e, "rgba(11,16,19,.9)"); Border(e, 1, LINE);
            });
            R("talk-portrait", e =>
            {
                Size(e, 76, 76); Radius(e, 16); e.style.overflow = Overflow.Hidden; Bg(e, "#1c262a"); e.style.flexShrink = 0;
                if (Has(e, "mono")) { Center(e); Font(e, 2, 900); Fg(e, ACCENT); Bg(e, "#222c31"); Border(e, 1, "rgba(242,179,61,.3)"); e.style.unityTextAlign = TextAnchor.MiddleCenter; }
                if (Screen.width <= 560) e.style.display = DisplayStyle.None;
            });
            R("talk-main", e => { e.style.flexGrow = 1; e.style.flexShrink = 1; });
            R("talk-head", e => { Row(e, Rem(.7f), true, Align.FlexEnd); });
            R("talk-speaker", e => { Font(e, 1.12f); Fg(e, ACCENT); });
            R("talk-role", e => { Fg(e, MUTED); Font(e, .86f); });
            R("talk-count", e => { e.style.marginLeft = StyleKeyword.Auto; Fg(e, MUTED); Font(e, .8f); });
            R("talk-line", e => { Margin(e, Rem(.45f), 0, Rem(.2f), 0); Font(e, 1.12f); e.style.minHeight = Rem(1.12f) * 3.4f; TextWrap(e); e.pickingMode = PickingMode.Position; });
            R("talk-foot", e => { Row(e, 12); e.style.justifyContent = Justify.FlexEnd; });
            R("talk-hint", e => { Fg(e, MUTED); Font(e, .8f); });
            R("talk-choices", e => { Col(e, 8); e.style.marginTop = Rem(.6f); });
            R("choice", e =>
            {
                if (Within(e, "talk") == null) return;
                bool on = Has(e, "is-hover") || Has(e, "is-focus");
                Row(e, Rem(.75f), false, Align.FlexStart); e.style.width = Length.Percent(100); Pad(e, Rem(.7f), Rem(.9f)); Radius(e, 12);
                Border(e, 1, on ? ACCENT : LINE_STRONG); Bg(e, on ? "rgba(242,179,61,.12)" : "rgba(255,255,255,.05)"); Fg(e, TEXT);
                e.style.unityTextAlign = TextAnchor.UpperLeft;
                if (Has(e, "is-disabled")) e.style.opacity = .7f;
            });
            R("choice-no", e =>
            {
                Center(e); Size(e, Rem(.85f) * 1.7f, Rem(.85f) * 1.7f); e.style.flexShrink = 0; Radius(e, 8); Font(e, .85f, 900); e.style.unityTextAlign = TextAnchor.MiddleCenter;
                bool off = Has(Within(e, "choice"), "is-disabled");
                Bg(e, off ? "rgba(255,255,255,.08)" : "rgba(242,179,61,.18)"); Fg(e, off ? MUTED : ACCENT);
            });
            R("choice-text", e => { Col(e, Rem(.15f)); e.style.flexShrink = 1; });
            R("choice-label", e => { Bold(e); TextWrap(e); });
            R("choice-note", e => { Fg(e, TEXT_DIM); Font(e, .84f); TextWrap(e); });
            R("choice-reason", e => { Row(e, Rem(.3f)); Fg(e, "#ffb4b6"); Font(e, .84f); });

            // ---- story cards ----
            R("card", e =>
            {
                if (!Has(e, "modal")) return;
                MaxW(e, 640);
                if (Has(e, "card-alert")) BorderTop(e, 6, RED);
                if (Has(e, "card-news")) BorderTop(e, 4, BLUE);
                if (Has(e, "card-notice")) { Bg(e, PAPER); Fg(e, INK); Border(e, 0, "#0000"); }
            });
            R("story-card", e => { if (Has(Modal(e), "card-sms")) Pad(e, 4); });
            R("card-head", e => { Row(e, Rem(1), true); e.style.justifyContent = Justify.SpaceBetween; });
            R("card-kind", e =>
            {
                Row(e, Rem(.4f)); Font(e, .82f, 900); e.style.letterSpacing = Rem(.82f) * .08f;
                var m = Modal(e);
                Fg(e, Has(m, "card-alert") ? "#ff9ea1" : Has(m, "card-news") ? "#8cc2ff" : Has(m, "card-notice") ? "#9a5b00" : Has(m, "card-sms") ? "#ffd27f" : TEAL);
            });
            R("card-meta", e => { Fg(e, Has(Modal(e), "card-notice") ? INK_DIM : MUTED); Font(e, .84f); });
            R("card-title", e => Margin(e, Rem(.4f), 0, Rem(.5f), 0));
            R("card-body", e => Col(e, Rem(.7f)));
            // .card-body p, p:first-child, notice and sms variants
            R("t-p", e =>
            {
                var body = Within(e, "card-body");
                if (body == null || e.parent != body) return;
                bool first = body.IndexOf(e) == 0; var m = Modal(e);
                Margin(e, 0); Fg(e, first ? TEXT : TEXT_DIM);
                if (Has(m, "card-notice")) { Fg(e, first ? INK : "#3a4448"); if (first) e.style.unityFontStyleAndWeight = FontStyle.Bold; }
                if (Has(m, "card-sms")) { Pad(e, Rem(.8f), Rem(1)); Radius(e, 16); e.style.borderBottomLeftRadius = 4; Bg(e, "#2a353a"); Fg(e, TEXT); }
            });
            R("card-actions", e => { Row(e, 10, true); e.style.justifyContent = Justify.FlexEnd; e.style.marginTop = Rem(1.3f); });

            // ---- time skip ----
            R("timeskip-layer", e => { Bg(e, "#030506"); Pad(e, 24); });
            R("timeskip", e =>
            {
                if (!Has(e, "modal")) return;
                MaxW(e, 720); e.style.backgroundColor = Color.clear; Border(e, 0, "#0000"); e.style.alignItems = Align.Center; e.style.unityTextAlign = TextAnchor.MiddleCenter;
            });
            R("skip-inner", e => { Col(e); e.style.alignItems = Align.Center; e.style.width = Length.Percent(100); });
            R("skip-times", e =>
            {
                Row(e, Rem(1), true); e.style.justifyContent = Justify.Center; Margin(e, 0, 0, Rem(1), 0);
                Font(e, Mathf.Clamp(Vw(3.6f) / Rem(1), 1.3f, 2), 900);
            });
            R("skip-from", e => Fg(e, MUTED));
            R("skip-to", e => Fg(e, ACCENT));
            R("skip-arrow", e => { Fg(e, TEXT_DIM); foreach (var i in e.Query<UiIcon>().ToList()) i.em = 1.2f; });
            R("skip-text", e => { Margin(e, 0, 0, Rem(1.6f), 0); e.style.maxWidth = Rem(1.12f) * 34; Font(e, 1.12f); Fg(e, TEXT_DIM); e.style.unityTextAlign = TextAnchor.MiddleCenter; });
            R("skip-progress", e => { e.style.width = Mathf.Min(260, Screen.width * .6f); e.style.height = 3; Margin(e, 0, 0, Rem(1.2f), 0); Bg(e, "rgba(255,255,255,.12)"); Radius(e, 2); e.style.overflow = Overflow.Hidden; });
            R("skip-bar", e => { e.style.height = Length.Percent(100); Bg(e, ACCENT); });
        }
    }
}
