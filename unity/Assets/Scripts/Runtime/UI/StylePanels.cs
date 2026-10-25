// style.css Phone, Planner (paper calendar), Notebook & inventory.
using UnityEngine;
using UnityEngine.UIElements;
using static Gyeongbo.UI.Css;

namespace Gyeongbo.UI
{
    static class StylePanels
    {
        static void R(string cls, System.Action<VisualElement> f) => UiStyle.Register(cls, f);
        static void Bare(VisualElement e) { Pad(e, 0); e.style.backgroundColor = Color.clear; Border(e, 0, "#0000"); }
        static void Radii(VisualElement e, float tl, float tr, float br, float bl)
        { e.style.borderTopLeftRadius = tl; e.style.borderTopRightRadius = tr; e.style.borderBottomRightRadius = br; e.style.borderBottomLeftRadius = bl; }

        internal static void Register()
        {
            // ---- phone ----
            R("phone", e => { if (!Has(e, "modal")) return; e.style.width = StyleKeyword.Auto; Bare(e); e.style.overflow = Overflow.Visible; });
            R("ealert", e => { if (!Has(e, "modal")) return; e.style.width = StyleKeyword.Auto; Bare(e); e.style.overflow = Overflow.Visible; });
            R("phone-frame", e =>
            {
                e.style.position = Position.Relative; Col(e); Size(e, Mathf.Min(360, Screen.width - 32), Mathf.Min(720, Screen.height - 32));
                Radius(e, 46); Bg(e, "#0a0e10"); Border(e, 10, "#1b2327"); e.style.overflow = Overflow.Hidden;
            });
            R("phone-notch", e => { Abs(e, 8); e.style.left = Length.Percent(50); e.style.marginLeft = -48; Size(e, 96, 26); Radius(e, 14); Bg(e, "#000"); });
            R("phone-status", e => { Row(e); e.style.justifyContent = Justify.SpaceBetween; Pad(e, 12, 24, 6, 24); Font(e, .8f, 800); });
            R("ps-right", e => Row(e, Rem(.5f)));
            R("ps-signal", e => { Row(e, 2, false, Align.FlexEnd); e.style.height = 12; });
            R("sig", e =>
            {
                e.style.width = 3; Radius(e, 1); Bg(e, Has(e, "on") ? "#ffffff" : "rgba(255,255,255,.25)");
                int n = e.parent != null ? e.parent.IndexOf(e) : 0; e.style.height = new[] { 4, 7, 10, 12 }[Mathf.Clamp(n, 0, 3)];
            });
            R("ps-note", e => { Font(e, .7f); Fg(e, "#ffd27f"); });
            R("ps-batt", e => { Row(e, Rem(.35f)); if (Has(e, "low")) Fg(e, "#ff8f92"); });
            R("phone-screen", e =>
            {
                e.style.flexGrow = 1; e.style.flexShrink = 1; Bg(e, "#0f171a");
                UiFx.BgImage(e, UiFx.Gradient(true, (0, "#121a1e"), (1, "#0c1214")));
                var c = (e as ScrollView)?.contentContainer ?? e; Pad(c, 8, 12, 12, 12); Col(c, 12);
            });
            R("phone-banner", e => { Row(e, Rem(.5f)); Margin(e, 0); Pad(e, Rem(.5f), Rem(.8f)); Radius(e, 12); Bg(e, "rgba(229,72,77,.18)"); Fg(e, "#ffb4b6"); Font(e, .84f, 700); });
            R("app-map", e => { e.style.position = Position.Relative; e.style.height = 150; Radius(e, 18); e.style.overflow = Overflow.Hidden; Bg(e, "#dfe6e2"); e.style.flexShrink = 0; });
            R("map-tiles", e =>
            {
                Abs(e, -20, -20, -20, -20); Bg(e, "#d9e4dc"); e.style.rotate = new Rotate(-8);
                var m = Within(e, "app-map");
                if (Has(m, "state-loading") || Has(m, "state-fail")) { e.style.opacity = .5f; Bg(e, "#dadada"); }
            });
            R("map-line", e => { e.style.position = Position.Absolute; Bg(e, "rgba(255,255,255,.9)"); });
            R("map-state", e =>
            {
                Fill(e); Col(e, Rem(.2f)); Center(e); e.style.unityTextAlign = TextAnchor.MiddleCenter; Fg(e, "#1b2427"); Font(e, .86f);
                var m = Within(e, "app-map");
                if (Has(m, "state-ok")) { e.style.alignItems = Align.FlexStart; e.style.justifyContent = Justify.FlexEnd; Pad(e, 10); }
                if (Has(m, "state-fail")) Fg(e, "#8b1d21");
                foreach (var i in e.Query<UiIcon>().ToList()) if (i.parent == e) i.em = 1.8f;
                if (Has(m, "state-ok")) foreach (var k in e.Children()) if (k is Label) { Bg(k, "rgba(255,255,255,.9)"); Pad(k, Rem(.1f), Rem(.5f)); Radius(k, 6); }
            });
            R("app-msgs", e => e.style.flexShrink = 0);
            R("app-msgs-title", e => { Row(e, Rem(.4f)); Margin(e, Rem(.2f), 0, Rem(.5f), 0); Font(e, .9f); Fg(e, TEXT_DIM); });
            R("msg-list", e => { Col(e, 10); Margin(e, 0); Pad(e, 0); });
            R("msg", e =>
            {
                Pad(e, Rem(.7f), Rem(.85f)); Radii(e, 16, 16, 16, 4); Bg(e, "#232d32"); Font(e, .9f);
                if (Has(e, "msg-emergency")) { Bg(e, "#3a1416"); Border(e, 1, "rgba(229,72,77,.6)"); }
                if (Has(e, "msg-safety")) { Bg(e, "#33280f"); Border(e, 1, "rgba(242,179,61,.45)"); }
                if (Has(e, "msg-me")) { e.style.alignSelf = Align.FlexEnd; e.style.maxWidth = Length.Percent(85); Radii(e, 16, 16, 4, 16); Bg(e, "#155e56"); }
            });
            R("msg-head", e => { Row(e, Rem(.5f)); e.style.marginBottom = Rem(.25f); Font(e, .76f); });
            R("msg-kind", e =>
            {
                Row(e, Rem(.3f)); Font(e, 0, 900); var m = Within(e, "msg");
                if (Has(m, "msg-emergency")) Fg(e, "#ff9ea1"); else if (Has(m, "msg-safety")) Fg(e, "#ffd27f");
            });
            R("msg-from", e => { Fg(e, TEXT_DIM); Bold(e); });
            R("msg-time", e => { e.style.marginLeft = StyleKeyword.Auto; Fg(e, MUTED); });
            R("msg-text", e => { Margin(e, 0); TextWrap(e); });
            R("phone-actions", e => { Row(e, 8); Pad(e, 10, 14, 8, 14); Bg(e, "#0c1214"); });
            R("phone-home", e => { Size(e, 110, 5); e.style.alignSelf = Align.Center; Margin(e, 4, 0, 10, 0); Radius(e, 3); Bg(e, "rgba(255,255,255,.5)"); });
            R("ealert-layer", e => { e.style.justifyContent = Justify.FlexStart; e.style.paddingTop = Mathf.Max(16, Vh(6)); Bg(e, "rgba(30,2,4,.55)"); });
            R("alert-sheet", e => { e.style.width = Mathf.Min(640, Screen.width - 32); Radius(e, 22); e.style.overflow = Overflow.Hidden; Bg(e, "#fbfbfc"); Fg(e, "#121619"); });
            R("alert-head", e =>
            {
                Row(e, Rem(.6f)); Pad(e, 14, 20); Fg(e, "#ffffff");
                var s = Within(e, "alert-sheet");
                Bg(e, Has(s, "alert-urgent") ? "#d97a1f" : Has(s, "alert-safety") ? "#2f3b41" : RED);
                foreach (var i in e.Query<UiIcon>().ToList()) i.em = 1.5f;
            });
            R("alert-title", e => { Margin(e, 0); Font(e, 1.25f); e.style.letterSpacing = Rem(1.25f) * .04f; });
            R("alert-time", e => { e.style.marginLeft = StyleKeyword.Auto; Bold(e); });
            R("alert-text", e => { Margin(e, 0); Pad(e, 20, 22, 16, 22); Font(e, 1.3f, 600); TextWrap(e); });
            R("alert-foot", e => { Row(e, Rem(1)); e.style.justifyContent = Justify.SpaceBetween; Pad(e, 0, 20, 18, 20); });
            R("alert-note", e => Fg(e, "#5c676d"));

            // ---- planner ----
            R("planner", e => { if (!Has(e, "modal")) return; MaxW(e, 1000); Bare(e); });
            R("planner-board", e => { e.style.position = Position.Relative; Pad(e, 34, 26, 24, 26); Radius(e, RADIUS_LG); Bg(e, PAPER); Fg(e, INK); });
            R("planner-strip", e => { Abs(e, 0, 0, null, 0); e.style.height = 16; Radii(e, RADIUS_LG, RADIUS_LG, 0, 0); Bg(e, "#b8312b"); });
            R("planner-head", e => e.style.position = Position.Relative);
            R("planner-title", e => Margin(e, Rem(.05f), 0, Rem(.1f), 0));
            R("planner-close", e => Abs(e, 0, 0));
            R("planner-sub", e => { Row(e, 0, true); Margin(e, 0, 0, Rem(.8f), 0); Fg(e, INK_DIM); });
            R("planner-left", e => { Fg(e, "#b8312b"); Font(e, 1.15f, 700); });
            R("slot-row", e => { Row(e, 8, true); e.style.marginBottom = Rem(1); });
            R("slot", e =>
            {
                bool sm = Screen.width <= 560; e.style.position = Position.Relative; Col(e); Center(e); Size(e, sm ? 48 : 64, sm ? 48 : 60);
                Radius(e, 10); Border(e, 2, "rgba(0,0,0,.22)"); Bg(e, PAPER2);   // ponytail: dashed borders drawn solid
                if (Has(e, "used")) { BorderColor(e, "rgba(0,0,0,.1)"); Bg(e, "#dcd6ca"); Fg(e, "#8d9396"); }
                if (Has(e, "preview")) { Border(e, 2, "#c27a00"); Bg(e, "#ffe2a3"); }
            });
            R("slot-no", e => Font(e, 1.1f, 900));
            R("slot-state", e => { Font(e, .7f, 800); Fg(e, INK_DIM); });
            R("planner-hint", e => { Row(e, Rem(.5f), false, Align.FlexStart); Margin(e, 0, 0, Rem(1), 0); Pad(e, Rem(.6f), Rem(.8f)); Radius(e, 10); Bg(e, "#fff4d6"); Border(e, 1, "#ecd08a"); Fg(e, "#5a4300"); Font(e, .92f); });
            R("planner-body", e => { if (Screen.width <= 820) Col(e, 16); else Row(e, 16, false, Align.FlexStart); });
            R("planner-grid", e => { e.style.flexGrow = 1.5f; e.style.flexShrink = 1; e.style.flexBasis = 0; Row(e, 10, true, Align.Stretch); });
            R("plan-card", e =>
            {
                bool hover = Has(e, "is-hover") || Has(e, "is-focus");
                e.style.flexGrow = 1; e.style.flexBasis = 220; e.style.minWidth = 200;
                Row(e, Rem(.6f), false, Align.FlexStart); Pad(e, Rem(.8f)); Radius(e, 12); Border(e, 1, "rgba(0,0,0,.12)"); Bg(e, PAPER2); Fg(e, INK);
                e.style.unityTextAlign = TextAnchor.UpperLeft;
                if (hover) { e.style.translate = new Translate(0, -2); BorderColor(e, "#c27a00"); }
                if (Has(e, "selected")) { Border(e, 2, "#c27a00"); Bg(e, "#fff6df"); }
                if (Has(e, "done") || Has(e, "blocked")) { Bg(e, "#ece8df"); Fg(e, "#6a7276"); }
            });
            R("plan-icon", e => Font(e, 1.5f));
            R("plan-main", e => { Col(e, Rem(.2f)); e.style.flexGrow = 1; e.style.flexShrink = 1; });
            R("plan-label", e => Font(e, 1, 700));
            R("plan-desc", e => { Font(e, .82f); Fg(e, INK_DIM); TextWrap(e); });
            R("plan-cost", e => { Pad(e, Rem(.1f), Rem(.55f)); Radius(e, 999); Bg(e, "#1c2326"); Fg(e, "#ffffff"); Font(e, .8f, 900); NoWrap(e); });
            R("plan-status", e => { Row(e, Rem(.3f)); Font(e, .8f, 800); if (Has(e, "ok")) Fg(e, "#0f7d72"); if (Has(e, "no")) Fg(e, "#a3221d"); });
            R("planner-detail", e => { e.style.flexGrow = 1; e.style.flexShrink = 1; e.style.flexBasis = 0; e.style.minWidth = 240; Pad(e, Rem(1)); Radius(e, 12); Bg(e, PAPER2); Border(e, 1, "rgba(0,0,0,.12)"); });
            R("t-h3", e => { if (Within(e, "planner-detail") != null) Margin(e, 0, 0, Rem(.3f), 0); });
            R("t-p", e => { if (Within(e, "planner-detail") != null && !Has(e, "plan-status")) { Margin(e, 0, 0, Rem(.7f), 0); Fg(e, Has(e, "muted") ? INK_DIM : "#3a4448"); } });
            R("plan-facts", e => { Row(e, 0, true); Margin(e, 0, 0, Rem(.8f), 0); });
            R("t-dt", e => { if (Within(e, "plan-facts") != null) { e.style.width = Length.Percent(40); Fg(e, INK_DIM); e.style.unityFontStyleAndWeight = FontStyle.Normal; } });
            R("t-dd", e => { if (Within(e, "plan-facts") != null) { e.style.width = Length.Percent(60); Margin(e, 0); Font(e, 0, 800); } });
            R("plan-lost", e => { Pad(e, Rem(.6f), Rem(.8f)); Radius(e, 10); Bg(e, "#fde8e6"); Fg(e, "#7b1c18"); Font(e, .9f); });
            R("t-ul", e => { if (Within(e, "plan-lost") != null) { Margin(e, Rem(.3f), 0, 0, 0); e.style.paddingLeft = Rem(1.2f); } });
            R("plan-confirm", e => { Row(e, 8, true); e.style.marginTop = Rem(1); });

            // ---- notebook & inventory ----
            foreach (var c in new[] { "notebook", "inventory", "about" })
                R(c, e => { if (Has(e, "modal")) { MaxW(e, 860); Col(e); } });
            foreach (var c in new[] { "nb", "inv", "about-panel" }) R(c, e => { Col(e); e.style.minHeight = 0; e.style.flexShrink = 1; });
            R("nb-filters", e => { Row(e, 6, true); e.style.marginBottom = Rem(.8f); });
            R("nb-filter", e =>
            {
                Row(e, Rem(.35f)); Pad(e, Rem(.25f), Rem(.4f), Rem(.25f), Rem(.25f)); Radius(e, 999);
                bool on = Has(e, "pressed"); Border(e, 1, on ? ACCENT : "rgba(0,0,0,0)"); Bg(e, on ? "rgba(242,179,61,.12)" : "rgba(255,255,255,.04)");
            });
            R("nb-count", e => { Font(e, .78f, 800); Fg(e, MUTED); e.style.paddingRight = Rem(.3f); });
            R("nb-pages", e => { e.style.maxHeight = Mathf.Min(Vh(56), 560); e.style.flexShrink = 1; var c = (e as ScrollView)?.contentContainer ?? e; Pad(c, Rem(.2f), Rem(.6f), Rem(.2f), 0); });
            R("t-h3", e => { if (e.parent != null && Has(e.parent, "nb-day")) { Margin(e, Rem(.4f), 0, Rem(.4f), 0); Pad(e, Rem(.3f), 0); Bg(e, PANEL_STRONG); Fg(e, ACCENT); Font(e, .95f); } });
            R("t-ol", e => { if (e.parent != null && Has(e.parent, "nb-day")) { Col(e, 8); Margin(e, 0, 0, Rem(1), 0); Pad(e, 0); } });
            R("nb-entry", e =>
            {
                Row(e, Rem(.7f), true, Align.FlexStart); Pad(e, Rem(.6f), Rem(.7f)); Radius(e, 10); Bg(e, "rgba(255,255,255,.04)");
                BorderLeft(e, 3, Has(e, "nb-official") ? TEAL : Has(e, "nb-site") ? BLUE : Has(e, "nb-neighbor") ? VIOLET : Has(e, "nb-rumor") ? GREY : Has(e, "nb-self") ? ACCENT : LINE_STRONG);
            });
            R("nb-time", e => { e.style.width = Rem(3.4f); Fg(e, MUTED); Font(e, .86f, 700); });
            R("nb-text", e => { Margin(e, 0); e.style.flexGrow = 1; e.style.flexShrink = 1; e.style.flexBasis = 0; e.style.minWidth = 120; TextWrap(e); });
            R("nb-place", e => { e.style.width = Length.Percent(100); e.style.paddingLeft = Rem(3.4f) + Rem(.7f); Fg(e, MUTED); Font(e, .78f); });
            R("nb-legend", e => { e.style.marginTop = Rem(.8f); Fg(e, TEXT_DIM); Font(e, .88f); });
            R("nb-summary", e => { Pad(e, Rem(.3f), 0); Bg(e, "rgba(0,0,0,0)"); Border(e, 0, "#0000"); Fg(e, TEXT_DIM); e.style.alignSelf = Align.FlexStart; });
            R("nb-legend-list", e => { Col(e, Rem(.35f)); Margin(e, Rem(.4f), 0, 0, 0); Pad(e, 0); });
            R("nb-legend-row", e => Row(e, Rem(.6f)));
            R("inv-panes", e => { if (Screen.width <= 820) Col(e, 14); else Row(e, 14, false, Align.FlexStart); e.style.minHeight = 0; });
            R("inv-pane", e =>
            {
                e.style.flexGrow = 1; e.style.flexBasis = 0; e.style.maxHeight = Mathf.Min(Vh(56), 540);
                Radius(e, 12); Bg(e, "rgba(255,255,255,.04)"); Border(e, 1, LINE);
                var c = (e as ScrollView)?.contentContainer ?? e; Pad(c, Rem(.8f));
            });
            R("t-h3", e => { if (e.parent != null && (Has(e.parent, "inv-pane") || Has(e.parent.parent, "inv-pane"))) { Row(e, Rem(.4f)); Margin(e, 0, 0, Rem(.6f), 0); Font(e, 1); } });
            R("inv-list", e => { Col(e, 6); Margin(e, 0); Pad(e, 0); });
            R("inv-row", e => { Row(e, Rem(.6f), true); Pad(e, Rem(.45f), Rem(.55f)); Radius(e, 8); Bg(e, "rgba(255,255,255,.03)"); });
            R("inv-cat", e => { e.style.width = Rem(4.2f); Font(e, .74f, 700); Fg(e, MUTED); });
            R("inv-name", e => { e.style.flexGrow = 1; e.style.flexShrink = 1; e.style.flexBasis = 0; TextWrap(e); });
            R("inv-qty", e => Font(e, 0, 800));
            R("inv-tags", e => { e.style.width = Length.Percent(100); e.style.paddingLeft = Rem(4.2f) + Rem(.6f); Row(e, 4, true); });
            R("inv-tag", e =>
            {
                Row(e, Rem(.25f)); Font(e, .74f, 700); Pad(e, Rem(.05f), Rem(.5f)); Radius(e, 999);
                if (Has(e, "ok")) { Fg(e, "#6fe0d2"); Border(e, 1, "rgba(60,198,181,.5)"); }
                if (Has(e, "warn")) { Fg(e, "#ffd27f"); Border(e, 1, "rgba(242,179,61,.5)"); }
                if (Has(e, "risk")) { Fg(e, "#ffb4b6"); Border(e, 1, "rgba(229,72,77,.5)"); }
            });
            R("inv-load", e => { Col(e, 6); e.style.marginBottom = Rem(.8f); });
            R("inv-meter", e => { Row(e, Rem(.6f)); Font(e, .84f); });
            R("inv-meter-label", e => e.style.width = Rem(2.6f));
            R("inv-meter-value", e => { if (Has(Within(e, "inv-meter"), "over")) { Fg(e, "#ffb4b6"); Font(e, 0, 800); } });
            R("inv-note", e => { Margin(e, Rem(.9f), 0, 0, 0); Fg(e, TEXT_DIM); Font(e, .88f); });
        }
    }
}
