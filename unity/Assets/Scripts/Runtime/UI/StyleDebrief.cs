// style.css Debriefs: fail card, chapter card, the ending's 생존 기록 document.
using UnityEngine;
using UnityEngine.UIElements;
using static Gyeongbo.UI.Css;

namespace Gyeongbo.UI
{
    static class StyleDebrief
    {
        static void R(string cls, System.Action<VisualElement> f) => UiStyle.Register(cls, f);
        static bool In(VisualElement e, string cls) => Within(e, cls) != null;
        static bool ParentIs(VisualElement e, string cls) => e.parent != null && (Has(e.parent, cls) || Has(e.parent.parent, cls));

        internal static void Register()
        {
            R("fail", e => { if (Has(e, "modal")) { MaxW(e, 680); BorderTop(e, 4, ACCENT); } });
            R("chapter", e => { if (Has(e, "modal")) MaxW(e, 680); });
            foreach (var c in new[] { "fail-card", "chapter-card", "ending-doc" }) R(c, e => { e.style.flexShrink = 1; e.style.minHeight = 0; });
            R("fail-lead", e => { Margin(e, 0, 0, Rem(1), 0); Fg(e, TEXT_DIM); });
            R("fail-rows", e => { Col(e, 10); Margin(e, 0); });
            R("fail-row", e => { Pad(e, Rem(.8f), Rem(1)); Radius(e, 12); Bg(e, "rgba(255,255,255,.04)"); Border(e, 1, LINE); });
            R("t-dt", e => { if (In(e, "fail-row")) { Row(e, Rem(.4f)); Font(e, .9f, 900); Fg(e, ACCENT); } });
            R("t-dd", e => { if (In(e, "fail-row")) Margin(e, Rem(.3f), 0, 0, 0); });
            R("t-p", e => { if (In(e, "fail-row")) Margin(e, 0); });
            R("t-h3", e => { if (ParentIs(e, "fail-sources") || ParentIs(e, "payoffs") || ParentIs(e, "chapter-lines")) { Row(e, Rem(.4f)); Margin(e, Rem(1), 0, Rem(.4f), 0); Font(e, 1); } });
            // .src-group ul, .src-list, .about-credits ul
            R("t-ul", e => { if (Has(e, "src-list") || In(e, "src-group") || In(e, "about-credits")) { Margin(e, 0); e.style.paddingLeft = Rem(1.2f); Col(e, Rem(.25f)); Font(e, .92f); } });
            R("t-ul", e => { if (In(e, "payoffs") || In(e, "chapter-lines")) { Margin(e, 0); Pad(e, 0); Col(e, 6); } });
            R("t-li", e =>
            {
                if (In(e, "payoffs")) { Col(e, Rem(.1f)); e.style.alignItems = Align.Stretch; Pad(e, Rem(.6f), Rem(.8f)); Radius(e, 10); Bg(e, "rgba(242,179,61,.1)"); BorderLeft(e, 3, ACCENT); }
                if (In(e, "chapter-lines")) { e.style.flexWrap = Wrap.NoWrap; Pad(e, Rem(.4f), Rem(.2f), Rem(.4f), Rem(.3f)); Fg(e, TEXT_DIM); BorderBottom(e, 1, LINE); }
            });
            R("t-b", e => { if (In(e, "payoffs")) Fg(e, ACCENT); });
            R("li-dot", e => { e.style.width = Rem(.9f); e.style.flexShrink = 0; Fg(e, ACCENT); Font(e, 0, 900); });
            R("t-span", e => { if (e.parent != null && Has(e.parent, "t-li") && (In(e, "chapter-lines") || In(e, "payoffs")) && !Has(e, "li-dot")) { e.style.flexShrink = 1; TextWrap(e); } });

            // ---- ending ----
            R("ending", e => { if (Has(e, "modal")) { MaxW(e, 860); Pad(e, 0); e.style.backgroundColor = Color.clear; Border(e, 0, "#0000"); } });
            R("ending-wrap", e => { Col(e); e.style.flexShrink = 1; e.style.minHeight = 0; });
            R("ending-doc", e =>
            {
                Pad(e, Mathf.Clamp(Vw(4), 22, 44)); Bg(e, PAPER); Fg(e, INK);
                e.style.borderTopLeftRadius = RADIUS_LG; e.style.borderTopRightRadius = RADIUS_LG;
            });
            R("doc-head", e => { BorderBottom(e, 3, "rgba(0,0,0,.3)"); e.style.marginBottom = Rem(1.2f); e.style.paddingBottom = Rem(.8f); });   // ponytail: double border drawn solid
            R("doc-kicker", e => { Margin(e, 0); Fg(e, INK_DIM); Font(e, .86f, 700); e.style.letterSpacing = Rem(.86f) * .04f; });
            R("t-h2", e => { if (In(e, "doc-head")) { Margin(e, Rem(.1f), 0); e.style.fontSize = Mathf.Clamp(Vw(4), Rem(1.8f), Rem(2.4f)); e.style.letterSpacing = e.style.fontSize.value.value * .1f; } });
            R("doc-sub", e => { Margin(e, 0); Fg(e, "#3a4448"); });
            R("doc-section", e => e.style.marginBottom = Rem(1.2f));
            R("t-h3", e => { if (ParentIs(e, "doc-section")) { Row(e, Rem(.45f)); Margin(e, 0, 0, Rem(.5f), 0); Font(e, 1.05f); Fg(e, "#8a4f00"); } });
            R("t-ul", e => { if (In(e, "doc-section")) { Margin(e, 0); Pad(e, 0); Col(e, Rem(.4f)); } });
            R("t-li", e =>
            {
                if (!In(e, "doc-section")) return;
                e.style.flexWrap = Wrap.NoWrap; e.style.alignItems = Align.FlexStart; Pad(e, Rem(.4f), 0); BorderBottom(e, 1, "rgba(0,0,0,.18)");   // ponytail: dashed drawn solid
                if (Screen.width <= 560) Col(e, Rem(.2f));
            });
            R("t-b", e => { if (In(e, "doc-section") && e.parent != null && Has(e.parent, "t-li")) { e.style.minWidth = Rem(8); e.style.marginRight = Rem(1); TextWrap(e); } });
            R("t-span", e => { if (In(e, "doc-section") && e.parent != null && Has(e.parent, "t-li")) { e.style.flexGrow = 1; e.style.flexShrink = 1; e.style.flexBasis = 0; TextWrap(e); } });
            R("doc-practice", e => { Pad(e, Rem(1), Rem(1.1f)); Radius(e, 12); Bg(e, "#ffffff"); Border(e, 2, "#c27a00"); });
            R("practice-text", e => { Margin(e, 0, 0, Rem(.5f), 0); Font(e, 1.08f, 700); });
            R("practice-src", e => { Margin(e, 0); Font(e, .86f); Fg(e, INK_DIM); });
            R("t-a", e => { if (In(e, "practice-src")) Fg(e, "#0f6f66"); });
            R("doc-fiction", e => { Margin(e, Rem(1.2f), 0, 0, 0); Font(e, .82f); Fg(e, INK_DIM); });
            R("ending-actions", e =>
            {
                Margin(e, 0); Pad(e, 14, 18); Bg(e, PANEL_STRONG); Border(e, 1, LINE); e.style.flexShrink = 0;
                e.style.borderBottomLeftRadius = RADIUS_LG; e.style.borderBottomRightRadius = RADIUS_LG;
            });
        }
    }
}
