// style.css: browser defaults for the tags Dom.El builds (t-<tag>), base, buttons, info labels, meters, loading.
using UnityEngine;
using UnityEngine.UIElements;
using static Gyeongbo.UI.Css;

namespace Gyeongbo.UI
{
    static class StyleBase
    {
        static void R(string cls, System.Action<VisualElement> f) => UiStyle.Register(cls, f);

        internal static void Register()
        {
            // ---- user-agent defaults (registered first so every class rule wins) ----
            R("t-p", e => { e.style.marginTop = Rem(1); e.style.marginBottom = Rem(1); TextWrap(e); });
            R("t-h1", e => { Font(e, 2, 700); e.style.marginTop = Rem(.67f); e.style.marginBottom = Rem(.67f); });
            R("t-h2", e => { Font(e, 1.5f, 700); e.style.marginTop = Rem(.83f); e.style.marginBottom = Rem(.83f); });
            R("t-h3", e => { Font(e, 1.17f, 700); e.style.marginTop = Rem(1); e.style.marginBottom = Rem(1); });
            R("t-h4", e => { Font(e, 1, 700); e.style.marginTop = Rem(1.33f); e.style.marginBottom = Rem(1.33f); });
            foreach (var t in new[] { "t-b", "t-strong", "t-dt", "t-legend", "t-summary" }) R(t, Bold);
            R("t-small", e => Font(e, .83f));
            R("t-a", e => { Fg(e, TEAL); });
            R("t-ul", e => { e.style.marginTop = Rem(1); e.style.marginBottom = Rem(1); e.style.paddingLeft = 40; });
            R("t-ol", e => { e.style.marginTop = Rem(1); e.style.marginBottom = Rem(1); e.style.paddingLeft = 40; });
            R("t-dl", e => { e.style.marginTop = Rem(1); e.style.marginBottom = Rem(1); });
            R("t-dd", e => e.style.marginLeft = 40);
            R("t-fieldset", e => { Border(e, 0, "#0000"); });
            R("t-kbd", e =>
            {
                e.style.unityTextAlign = TextAnchor.MiddleCenter; e.style.minWidth = Rem(1.7f * .78f);
                Pad(e, Rem(.1f * .78f), Rem(.45f * .78f)); Radius(e, 6);
                Border(e, 1, LINE_STRONG); e.style.borderBottomWidth = 2; Bg(e, "rgba(255,255,255,.06)");
                Font(e, .78f, 700);
                if (Within(e, "hud-prompt") != null) { Bg(e, TEAL); Fg(e, "#052421"); BorderColor(e, TEAL); }
            });

            R("sr-only", e => e.style.display = DisplayStyle.None);
            R("icon", e => { if (e is UiIcon i) i.em = Within(e, "tag") != null ? 1 : Within(e, "inv-tag") != null ? .95f : 1.15f; });
            R("eyebrow", e =>
            {
                Row(e, Rem(.45f)); e.style.marginTop = 0; e.style.marginBottom = Rem(.3f);
                Fg(e, Within(e, "planner-head") != null ? "#a4610a" : ACCENT); Font(e, .8f, 800); e.style.letterSpacing = Rem(.8f) * .08f;
                if (Within(e, "relay-card") != null) { e.style.marginBottom = 0; }
            });
            R("muted", e => Fg(e, Within(e, "planner-detail") != null || Within(e, "doc-section") != null ? INK_DIM : MUTED));
            R("empty", e => { Fg(e, MUTED); e.style.paddingTop = Rem(1.2f); e.style.paddingBottom = Rem(1.2f); e.style.unityTextAlign = TextAnchor.MiddleCenter; });
            R("panel", e => { Bg(e, PANEL); Border(e, 1, LINE); Radius(e, RADIUS); });

            // ---- buttons (.btn, .primary, .ghost, .big, :hover, :disabled) and the contexts that restyle them ----
            R("btn", e =>
            {
                bool primary = Has(e, "primary"), ghost = Has(e, "ghost"), hover = Has(e, "is-hover");
                Row(e, Rem(.5f)); e.style.justifyContent = Justify.Center;
                e.style.minHeight = 44; Pad(e, Rem(.6f), Rem(1.15f)); Radius(e, 12);
                Border(e, 1, LINE_STRONG); Fg(e, TEXT); Bold(e);
                Bg(e, primary ? (hover ? "#ffc65e" : ACCENT) : ghost ? "rgba(0,0,0,0)" : hover ? "rgba(255,255,255,.13)" : "rgba(255,255,255,.07)");
                if (primary) { BorderColor(e, ACCENT); Fg(e, ON_ACCENT); }
                if (Has(e, "big")) { e.style.minHeight = 54; Pad(e, Rem(.75f), Rem(1.6f)); Font(e, 1.08f); }
                if (!e.enabledSelf) e.style.opacity = .45f;
                if (!primary && Within(e, "card-notice") != null) { Fg(e, INK); BorderColor(e, "rgba(0,0,0,.25)"); }
                if (ghost && Within(e, "planner-board") != null) { Fg(e, INK); BorderColor(e, "rgba(0,0,0,.25)"); }
                if (Within(e, "phone-actions") != null) { e.style.flexGrow = 1; e.style.flexBasis = 0; }
                if (primary && Within(e, "alert-sheet") != null) { Bg(e, "#121619"); BorderColor(e, "#121619"); Fg(e, "#ffffff"); e.style.minWidth = 120; }
                if (Within(e, "pause-actions") != null) e.style.justifyContent = Justify.FlexStart;
            });
            R("modal-close", e => { Abs(e, 16, 16); e.style.minHeight = 40; Pad(e, Rem(.4f), Rem(.8f)); });

            // ---- info labels: icon + text, never colour alone ----
            R("tag", e =>
            {
                Row(e, Rem(.3f)); e.style.alignSelf = Align.FlexStart; Pad(e, Rem(.12f), Rem(.6f), Rem(.12f), Rem(.45f));
                Radius(e, 999); Border(e, 1, "#0000"); Font(e, .74f, 800); NoWrap(e); e.style.flexShrink = 0;
            });
            void TagC(string cls, string fg, string border, string bg) => R(cls, e => { Fg(e, fg); BorderColor(e, border); Bg(e, bg); });
            TagC("tag-official", "#6fe0d2", "rgba(60,198,181,.55)", "rgba(60,198,181,.14)");
            TagC("tag-site", "#8cc2ff", "rgba(90,166,255,.55)", "rgba(90,166,255,.14)");
            TagC("tag-neighbor", "#c6b3ff", "rgba(169,139,255,.55)", "rgba(169,139,255,.14)");
            TagC("tag-rumor", "#c4cbce", "rgba(200,208,212,.6)", "rgba(154,166,171,.1)");   // ponytail: dashed border drawn solid
            TagC("tag-self", "#ffd27f", "rgba(242,179,61,.55)", "rgba(242,179,61,.13)");
            R("tag-all", e => { Fg(e, TEXT); BorderColor(e, LINE_STRONG); });

            // ---- meters ----
            R("meter", e =>
            {
                e.style.position = Position.Relative; Size(e, Within(e, "hud-bag") != null ? 54 : 64, 6); Radius(e, 4);
                Bg(e, "rgba(255,255,255,.14)"); e.style.overflow = Overflow.Hidden;
                if (Within(e, "inv-meter") != null) { e.style.width = StyleKeyword.Auto; e.style.flexGrow = 1; e.style.height = 8; }
            });
            R("meter-fill", e =>
            {
                Abs(e, 0, null, 0, 0); Radius(e, 4); Transition(e, "width", .4f);
                var bag = Within(e, "hud-bag"); var inv = Within(e, "inv-meter");
                Bg(e, bag != null && Has(bag, "over") ? RED : inv != null && Has(inv, "near") ? ACCENT : inv != null && Has(inv, "over") ? RED : TEAL);
            });
            R("batt", e =>
            {
                var c = BattColor(e);
                e.style.position = Position.Relative; Size(e, Within(e, "ps-batt") != null ? 20 : 24, Within(e, "ps-batt") != null ? 10 : 12);
                Border(e, 2, c); Radius(e, 3);
            });
            R("batt-fill", e => { Abs(e, 1, null, 1, 1); e.style.maxWidth = Length.Percent(100); Radius(e, 1); Bg(e, BattColor(e)); });
            R("spinner", e => { Size(e, 28, 28); Radius(e, 14); Border(e, 3, "rgba(255,255,255,.2)"); e.style.borderTopColor = C(TEAL); });

            // ---- loading ----
            R("loading", e =>
            {
                Fill(e); Col(e, 18); Center(e); e.pickingMode = PickingMode.Position;
                Bg(e, "#1a2428"); UiFx.BgImage(e, UiFx.Vignette(0, C("#080b0d")));
                Fg(e, TEXT_DIM); e.style.letterSpacing = Rem(1) * .04f;
            });
            R("loader-symbol", e => { e.style.position = Position.Relative; Size(e, 54, 54); Radius(e, 27); Border(e, 2, "rgba(242,179,61,.25)"); });
        }

        // currentColor of the battery glyph.
        static string BattColor(VisualElement e)
        {
            var chip = Within(e, "hud-battery");
            if (chip != null) return Has(chip, "dead") ? MUTED : Has(chip, "low") ? "#ffb4b6" : TEXT;
            var ps = Within(e, "ps-batt");
            if (ps != null) return Has(ps, "low") ? "#ff8f92" : TEXT;
            return TEXT;
        }
    }
}
