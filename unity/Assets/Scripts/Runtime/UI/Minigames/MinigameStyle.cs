// Ports ui/minigames.css: the .mg-* classes, registered as UiStyle rules.
// UI Toolkit has flexbox only: CSS grids become flex rows/columns with the same gaps and ratios. Compound selectors
// (.mg-mode.is-guided) are registered with dotted keys (applied when the element has every class); state selectors
// the runtime cannot express (data-state, :has, :hover, ::before) are applied by the minigames through the
// helpers at the bottom. Sizes: em -> px at the 16px panel font.
// ponytail: no backdrop blur, hover, transitions or keyframe animations (mg-fill/mg-write); the bar fill animates
// in code. Upgrade path: a USS asset once the project can ship one.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class MinigameStyle
    {
        // Design tokens (.mg-panel custom properties).
        public const string AMBER = "#f2b33d", TEAL = "#3cc6b5", RED = "#e5484d", BLUE = "#5b9cf0", VIOLET = "#a78bfa";
        public const string TEXT = "#eef2f3", MUTED = "#9fb0b5", LINE = "rgba(255,255,255,.1)", CARD = "rgba(255,255,255,.04)";
        const float F = 16f;   // 1em in the panel

        static bool registered;

        static Color C(string css) => Mg.C(css);
        static void R(string key, Action<IStyle> a) => UiStyle.Register(key, e => a(e.style));
        static void Pad(IStyle s, float v, float h) { s.paddingTop = s.paddingBottom = v; s.paddingLeft = s.paddingRight = h; }
        static void Radius(IStyle s, float r) { s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = r; }
        static void Border(IStyle s, float w, string css) { s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = w; BorderColor(s, css); }
        static void BorderColor(IStyle s, string css) { var c = C(css); s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = c; }
        static void Row(IStyle s, float gap) { s.flexDirection = FlexDirection.Row; s.alignItems = Align.Center; Gap(s, gap); }
        static void Col(IStyle s, float gap) { s.flexDirection = FlexDirection.Column; Gap(s, gap); }
        // ponytail: UI Toolkit has no gap property; children get a margin through Gap() at build time instead.
        static void Gap(IStyle s, float gap) { }
        static void Em(IStyle s, float em) => s.fontSize = em * F;

        // Registers every minigames.css rule with UiStyle (idempotent; the minigames call it before building).
        public static void Register()
        {
            if (registered) return;
            registered = true;

            R("mg-backdrop", s => { s.position = Position.Absolute; s.left = s.top = s.right = s.bottom = 0; s.alignItems = Align.Center; s.justifyContent = Justify.Center; Pad(s, 16, 16); s.backgroundColor = C("rgba(5,9,11,.58)"); });
            R("mg-panel", s => { s.width = Length.Percent(100); s.maxWidth = 1040; s.maxHeight = Length.Percent(100); s.overflow = Overflow.Hidden; Pad(s, 20, 24); Radius(s, 16); Border(s, 1, LINE); s.backgroundColor = C("rgba(14,20,23,.95)"); s.color = C(TEXT); s.fontSize = F; });
            R("mg-head", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.FlexStart; s.justifyContent = Justify.SpaceBetween; s.marginBottom = 14; });
            R("mg-eyebrow", s => { s.color = C(AMBER); Em(s, .78f); s.unityFontStyleAndWeight = FontStyle.Bold; s.letterSpacing = .08f * F * .78f; });
            R("mg-mode", s => { s.flexShrink = 0; Pad(s, 4, 10); Radius(s, 999); Em(s, .78f); Border(s, 1, LINE); s.color = C(MUTED); });
            R("mg-mode.is-guided", s => { BorderColor(s, TEAL); s.color = C(TEAL); });
            R("mg-mode.is-hint", s => { BorderColor(s, AMBER); s.color = C(AMBER); });
            foreach (var k in new[] { "mg-why", "mg-spec", "mg-note" }) R(k, s => { s.color = C(MUTED); Em(s, .85f); s.whiteSpace = WhiteSpace.Normal; });
            R("mg-keys", s => { s.color = C(MUTED); Em(s, .8f); s.whiteSpace = WhiteSpace.Normal; });
            R("mg-sub", s => s.marginTop = 14);
            R("mg-empty", s => { s.color = C(MUTED); Pad(s, 10, 4); Em(s, .9f); });

            // buttons
            R("mg-btn", s => { s.color = C(TEXT); s.minHeight = 44; Pad(s, 9, 16); Radius(s, 10); Border(s, 1, "rgba(255,255,255,.2)"); s.backgroundColor = C("rgba(255,255,255,.06)"); s.unityTextAlign = TextAnchor.MiddleLeft; s.fontSize = F; s.marginLeft = s.marginRight = 0; });
            R("mg-primary", s => { s.backgroundColor = C(AMBER); s.color = C("#1b1406"); BorderColor(s, "rgba(0,0,0,0)"); s.unityFontStyleAndWeight = FontStyle.Bold; s.unityTextAlign = TextAnchor.MiddleCenter; });
            R("mg-ghost", s => s.backgroundColor = C("rgba(0,0,0,0)"));
            R("mg-actions", s => { s.flexDirection = FlexDirection.Row; s.flexWrap = Wrap.Wrap; s.justifyContent = Justify.FlexEnd; });
            R("mg-foot", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.Center; s.justifyContent = Justify.SpaceBetween; s.flexWrap = Wrap.Wrap; s.marginTop = 16; s.paddingTop = 14; s.borderTopWidth = 1; s.borderTopColor = C(LINE); });

            // label pills
            R("mg-pill", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.Center; Pad(s, 2, 9); Radius(s, 999); Em(s, .78f); s.unityFontStyleAndWeight = FontStyle.Bold; Border(s, 1, MUTED); s.whiteSpace = WhiteSpace.NoWrap; });
            void Pill(string k, string col) => R("mg-pill." + k, s => { s.color = C(col); BorderColor(s, col); });
            Pill("is-official", TEAL); Pill("is-site", BLUE); Pill("is-neighbor", VIOLET); Pill("is-rumor", "#b9c0c3"); Pill("is-self", AMBER);
            R("mg-badges", s => { s.flexDirection = FlexDirection.Row; s.flexWrap = Wrap.Wrap; s.alignItems = Align.Center; });
            R("mg-badge", s => { s.flexDirection = FlexDirection.Row; Pad(s, 1, 8); Radius(s, 6); Em(s, .76f); s.backgroundColor = C("rgba(255,255,255,.08)"); s.color = C("#cfd8db"); s.marginRight = 6; });
            R("mg-badge.is-out", s => { s.backgroundColor = C("rgba(229,72,77,.18)"); s.color = C("#ffb0b2"); });
            R("mg-badge.is-low", s => { s.backgroundColor = C("rgba(242,179,61,.16)"); s.color = C("#ffd58a"); });
            R("mg-badge.is-rec", s => { s.backgroundColor = C("rgba(60,198,181,.15)"); s.color = C("#8fe6da"); s.marginLeft = 6; });

            // help (guided / hint)
            R("mg-help", s => { Pad(s, 10, 12); Radius(s, 10); s.borderLeftWidth = 3; s.borderLeftColor = C(TEAL); s.backgroundColor = C("rgba(60,198,181,.08)"); s.marginBottom = 12; s.whiteSpace = WhiteSpace.Normal; });
            R("mg-hintbox", s => { s.alignItems = Align.FlexStart; s.marginBottom = 12; });
            R("mg-tips", s => s.flexDirection = FlexDirection.Column);
            R("mg-tip", s => { s.color = C("#9fdcd3"); Em(s, .82f); s.marginTop = 3; s.whiteSpace = WhiteSpace.Normal; });

            // meters
            R("mg-meter", s => s.marginBottom = 10);
            R("mg-bar", s => { s.height = 8; Radius(s, 99); s.backgroundColor = C("rgba(255,255,255,.1)"); s.overflow = Overflow.Hidden; });
            R("mg-bar-fill", s => { s.height = Length.Percent(100); s.width = 0; Radius(s, 99); s.backgroundColor = C("#4cc38a"); });
            R("mg-sum", s => { s.flexDirection = FlexDirection.Row; s.flexWrap = Wrap.Wrap; s.flexGrow = 1; });

            // bag
            R("mg-bag-grid", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.Stretch; });
            R("mg-pane", s => { Radius(s, 12); Pad(s, 12, 12); s.backgroundColor = C(CARD); Border(s, 1, LINE); s.minHeight = 220; s.flexGrow = 1; s.flexBasis = 0; s.marginRight = 14; });
            R("mg-pane.is-bag", s => { s.backgroundColor = C("rgba(52,69,57,.25)"); BorderColor(s, "rgba(140,190,150,.25)"); });
            R("mg-list", s => { s.maxHeight = 420; s.overflow = Overflow.Hidden; });
            R("mg-row", s => { s.width = Length.Percent(100); s.flexDirection = FlexDirection.Row; s.alignItems = Align.Center; Pad(s, 8, 10); s.minHeight = 48; s.marginBottom = 6; });
            R("mg-row-main", s => { s.flexGrow = 1; s.flexShrink = 1; s.minWidth = 0; s.marginLeft = 5; s.marginRight = 5; });
            R("mg-count", s => { s.color = C(AMBER); s.unityFontStyleAndWeight = FontStyle.Bold; });
            R("mg-arrow", s => { s.color = C(TEAL); s.unityFontStyleAndWeight = FontStyle.Bold; Em(s, 1.1f); });
            R("mg-side", s => { s.flexGrow = .85f; s.flexBasis = 0; s.minWidth = 220; });
            R("mg-goals", s => s.marginBottom = 8);

            // radio
            R("mg-radio-body", s => { s.flexDirection = FlexDirection.Row; Pad(s, 18, 18); Radius(s, 18); s.backgroundColor = C("#26282a"); });
            R("mg-speaker", s => { s.width = 150; s.flexShrink = 0; Radius(s, 12); s.backgroundColor = C("#232527"); s.marginRight = 16; Border(s, 2, "rgba(0,0,0,.5)"); });
            R("mg-radio-face", s => { s.flexGrow = 1; s.minWidth = 0; });
            R("mg-lcd", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.FlexEnd; Pad(s, 8, 12); Radius(s, 8); s.backgroundColor = C("#120e06"); s.color = C("#ffc861"); s.marginBottom = 10; });
            R("mg-freq", s => { Em(s, 2); s.color = C("#ffc861"); s.marginLeft = 8; s.marginRight = 8; });
            R("mg-led", s => { s.width = 9; s.height = 9; Radius(s, 5); s.backgroundColor = C("#3b1a14"); s.alignSelf = Align.Center; });
            R("mg-led.on", s => s.backgroundColor = C("#ff5a3c"));
            R("mg-sigbox", s => { s.marginLeft = StyleKeyword.Auto; s.flexDirection = FlexDirection.Row; s.alignItems = Align.Center; });
            R("mg-sig", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.FlexEnd; s.height = 18; s.marginRight = 8; });
            R("mg-wave", s => { Radius(s, 6); s.marginBottom = 10; });
            R("mg-dial-canvas", s => Radius(s, 6));
            R("mg-dial", s => { Radius(s, 8); s.marginBottom = 10; });
            R("mg-knobrow", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.Center; s.justifyContent = Justify.FlexEnd; s.color = C("#8d9296"); });
            R("mg-knob", s => { s.width = 46; s.height = 46; Radius(s, 32); s.backgroundColor = C("#45494d"); Border(s, 5, "#3a3d40"); s.marginLeft = 10; });
            R("mg-radio-status", s => { s.marginTop = 10; s.color = C("#ffd58a"); s.flexDirection = FlexDirection.Row; s.alignItems = Align.Center; });
            R("mg-caption", s => { s.marginTop = 12; s.minHeight = 76; Pad(s, 12, 14); Radius(s, 12); s.backgroundColor = C("rgba(0,0,0,.45)"); Border(s, 1, LINE); });
            R("mg-cap-who", s => { s.flexDirection = FlexDirection.Row; s.flexWrap = Wrap.Wrap; s.alignItems = Align.Center; s.color = C(MUTED); Em(s, .85f); s.marginBottom = 4; });
            R("mg-cap-text", s => { Em(s, 1.08f); s.whiteSpace = WhiteSpace.Normal; });
            R("mg-transcript", s => { s.maxHeight = 200; s.overflow = Overflow.Hidden; });
            R("mg-tr-who", s => { s.color = C("#ffc861"); Em(s, .8f); s.marginRight = 10; });
            R("mg-tr-text", s => { s.flexGrow = 1; s.flexShrink = 1; s.whiteSpace = WhiteSpace.Normal; });
            R("mg-chip", s => { s.minHeight = 36; Pad(s, 4, 12); Em(s, .85f); Radius(s, 999); });

            // extinguisher
            R("mg-ext-grid", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.FlexStart; });
            R("mg-ext-stage", s => { s.flexGrow = 1.35f; s.flexBasis = 0; s.marginRight = 16; });
            R("mg-ext-canvas", s => { Radius(s, 12); s.backgroundColor = C("#1a1c1e"); s.marginBottom = 10; });
            R("mg-ext-side", s => { s.flexGrow = 1; s.flexBasis = 0; });
            R("mg-q", s => { s.unityFontStyleAndWeight = FontStyle.Bold; s.marginBottom = 8; });
            R("mg-choice", s => { s.width = Length.Percent(100); s.marginBottom = 8; s.whiteSpace = WhiteSpace.Normal; });
            R("mg-retreat", s => { s.width = Length.Percent(100); BorderColor(s, "rgba(60,198,181,.5)"); s.marginTop = 10; s.whiteSpace = WhiteSpace.Normal; });
            R("is-next", s => { BorderColor(s, AMBER); s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 2; });
            R("mg-step-btn", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.Center; s.width = Length.Percent(100); s.marginBottom = 8; s.overflow = Overflow.Hidden; });
            R("mg-step-btn.is-done", s => { BorderColor(s, "rgba(76,195,138,.6)"); s.color = C("#b7ecd9"); });
            R("mg-check", s => { s.width = 1.2f * F; s.unityFontStyleAndWeight = FontStyle.Bold; s.marginRight = 10; });
            R("mg-holdtip", s => { s.marginLeft = StyleKeyword.Auto; s.color = C(MUTED); Em(s, .78f); });
            R("mg-feedback", s => { s.minHeight = 1.5f * F; s.color = C("#ffd58a"); s.marginTop = 10; s.whiteSpace = WhiteSpace.Normal; });
            R("mg-end", s => { Pad(s, 12, 12); Radius(s, 12); s.backgroundColor = C(CARD); Border(s, 1, LINE); });
            R("mg-end.is-out", s => BorderColor(s, "rgba(76,195,138,.5)"));
            R("mg-end.is-spread", s => BorderColor(s, "rgba(229,72,77,.55)"));
            R("mg-end.is-overwhelmed", s => BorderColor(s, "rgba(229,72,77,.55)"));

            // docs
            R("mg-docs-grid", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.FlexStart; });
            R("mg-docs-left", s => { s.flexGrow = 1; s.flexBasis = 0; s.marginRight = 16; });
            R("mg-fields", s => s.flexDirection = FlexDirection.Column);
            R("mg-where", s => s.marginTop = 14);
            R("mg-field", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.FlexStart; Pad(s, 8, 10); Radius(s, 10); s.backgroundColor = C(CARD); Border(s, 1, "rgba(0,0,0,0)"); s.marginBottom = 6; });
            R("mg-field.is-rec", s => BorderColor(s, "rgba(60,198,181,.25)"));
            R("mg-radio-opt", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.FlexStart; Pad(s, 8, 10); Radius(s, 10); s.backgroundColor = C(CARD); s.marginBottom = 6; });
            R("mg-desk", s => { s.flexGrow = 1; s.flexBasis = 0; Pad(s, 18, 18); Radius(s, 14); s.backgroundColor = C("#5d4129"); });
            R("mg-paper", s => { s.rotate = new Rotate(new Angle(-1.2f, AngleUnit.Degree)); s.paddingTop = 16; s.paddingLeft = s.paddingRight = 18; s.paddingBottom = 18; Radius(s, 4); s.color = C("#23262a"); s.backgroundColor = C("#fbf8f0"); s.minHeight = 240; s.marginBottom = 14; });
            R("mg-paper-sub", s => { Em(s, .78f); s.color = C("#6c6f73"); s.marginBottom = 6; });
            R("mg-card-k", s => { s.color = C("#5b6066"); Em(s, .85f); s.minWidth = 90; s.marginRight = 8; });
            R("mg-card-v", s => { s.color = C("#1f3d7a"); s.flexGrow = 1; s.whiteSpace = WhiteSpace.Normal; });
            R("mg-deadphone", s => { s.flexDirection = FlexDirection.Row; Pad(s, 10, 12); Radius(s, 12); s.backgroundColor = C("rgba(10,12,14,.72)"); });
            R("mg-phone-ico", s => { s.flexShrink = 0; s.width = 34; s.height = 60; Radius(s, 8); Border(s, 3, "#4a4f55"); s.backgroundColor = C("#050607"); s.alignItems = Align.Center; s.justifyContent = Justify.Center; s.color = C(RED); s.marginRight = 12; });
            R("mg-phone-only", s => { s.flexDirection = FlexDirection.Row; s.flexWrap = Wrap.Wrap; Em(s, .85f); s.color = C("#ffb0b2"); });

            // route map
            R("mg-map-grid", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.FlexStart; });
            R("mg-map-wrap", s => { s.flexGrow = 1.6f; s.flexBasis = 0; s.minWidth = 0; Radius(s, 12); s.overflow = Overflow.Hidden; s.marginRight = 16; });
            R("mg-map-side", s => { s.flexGrow = 1; s.flexBasis = 0; });
            R("mg-routes", s => s.marginBottom = 6);
            R("mg-route", s => { s.flexDirection = FlexDirection.Row; s.alignItems = Align.FlexStart; Pad(s, 10, 10); Radius(s, 10); s.backgroundColor = C(CARD); Border(s, 1, LINE); s.marginBottom = 8; });
            R("mg-route-body", s => { s.flexGrow = 1; s.flexShrink = 1; s.whiteSpace = WhiteSpace.Normal; });
            R("mg-swatch", s => { s.width = 34; s.height = 8; s.marginRight = 6; });
            R("mg-gates", s => Em(s, .9f));
            R("mg-closures", s => { Em(s, .9f); s.color = C("#ffb0b2"); });
            R("mg-gate-ico", s => { s.width = 22; s.height = 22; s.alignItems = Align.Center; s.justifyContent = Justify.Center; Radius(s, 11); s.unityFontStyleAndWeight = FontStyle.Bold; Em(s, .8f); s.color = C("#0c1214"); s.backgroundColor = C("#8b9599"); s.marginRight = 8; s.unityTextAlign = TextAnchor.MiddleCenter; });
            R("mg-legend", s => { s.flexDirection = FlexDirection.Row; s.flexWrap = Wrap.Wrap; s.marginTop = 10; s.color = C(MUTED); Em(s, .8f); });

            // grab3d: the 3D shop / bag overlay
            R("grab3d", s => { s.position = Position.Absolute; s.left = s.top = s.right = s.bottom = 0; s.color = C("#f2f2f2"); s.fontSize = 15; });
            R("grab3d-top", s => { s.position = Position.Absolute; s.top = 16; s.alignSelf = Align.Center; s.maxWidth = 720; Pad(s, 10, 18); Radius(s, 12); s.backgroundColor = C("rgba(16,18,22,.78)"); s.unityTextAlign = TextAnchor.MiddleCenter; });
            R("grab3d-foot", s => { s.position = Position.Absolute; s.bottom = 16; s.alignSelf = Align.Center; s.width = 880; s.maxWidth = Length.Percent(96); Pad(s, 12, 16); Radius(s, 14); s.backgroundColor = C("rgba(16,18,22,.84)"); });
            R("grab3d-held", s => { s.flexDirection = FlexDirection.Row; s.flexWrap = Wrap.Wrap; s.marginBottom = 8; });
            R("grab3d-chip", s => { Pad(s, 4, 10); Radius(s, 99); Border(s, 1, "rgba(255,255,255,.25)"); s.backgroundColor = C("rgba(255,255,255,.08)"); s.color = C("#f2f2f2"); s.marginRight = 6; s.marginBottom = 6; });
            R("grab3d-empty", s => s.opacity = .6f);
            R("grab3d-sum", s => { s.flexDirection = FlexDirection.Row; s.flexWrap = Wrap.Wrap; s.unityFontStyleAndWeight = FontStyle.Bold; s.marginBottom = 8; });
            R("grab3d-goals", s => { s.flexDirection = FlexDirection.Row; s.flexWrap = Wrap.Wrap; s.unityFontStyleAndWeight = FontStyle.Normal; Em(s, .9f); });
            R("grab3d-toast", s => { s.position = Position.Absolute; s.top = Length.Percent(42); s.alignSelf = Align.Center; Pad(s, 8, 16); Radius(s, 10); s.backgroundColor = C("rgba(140,30,20,.9)"); s.opacity = 0; });
            R("grab3d-toast.on", s => s.opacity = 1);
            RegisterStates();
        }

        // ---- state classes the minigames toggle (UiStyle restyles on every class change, so state lives in classes) ----

        // [data-state] > .mg-bar-fill: ok | warn | over.
        public static void BarState(VisualElement fill, string state) { UiStyle.Toggle(fill, "is-warn", state == "warn"); UiStyle.Toggle(fill, "is-over", state == "over"); }
        // .grab3d-sum [data-state]
        public static void TextState(VisualElement e, string state) { UiStyle.Toggle(e, "st-warn", state == "warn"); UiStyle.Toggle(e, "st-over", state == "over"); }
        public static void Done(VisualElement e, bool on) => UiStyle.Toggle(e, "is-done", on);
        // .is-open / .is-closed .mg-gate-ico
        public static void GateIcon(VisualElement ico, string st) { UiStyle.Toggle(ico, "is-open", st == "open"); UiStyle.Toggle(ico, "is-closed", st == "closed"); }
        // .mg-route:has(input:checked)
        public static void RouteChecked(VisualElement route, bool on) => UiStyle.Toggle(route, "is-checked", on);
        // .mg-chip[aria-pressed=true]
        public static void Pressed(VisualElement chip, bool on) => UiStyle.Toggle(chip, "is-pressed", on);
        public static void Next(VisualElement b, bool on) => UiStyle.Toggle(b, "is-next", on);

        static void RegisterStates()
        {
            R("mg-bar-fill.is-warn", s => s.backgroundColor = C(AMBER));
            R("mg-bar-fill.is-over", s => s.backgroundColor = C(RED));
            R("st-warn", s => s.color = C("#ffc04d"));
            R("st-over", s => s.color = C("#ff6b5a"));
            R("is-done", s => s.color = C("#b7ecd9"));
            R("mg-goal.is-done", s => s.color = C("#b7ecd9"));
            R("mg-goal-mark.is-done", s => { s.color = C("#4cc38a"); s.unityFontStyleAndWeight = FontStyle.Bold; });
            R("mg-check.is-done", s => s.color = C("#4cc38a"));
            R("grab3d-goal.is-done", s => s.color = C("#4cc38a"));
            R("mg-gate-ico.is-open", s => s.backgroundColor = C(TEAL));
            R("mg-gate-ico.is-closed", s => { s.backgroundColor = C(RED); Radius(s, 4); });
            R("mg-route.is-checked", s => { BorderColor(s, AMBER); s.backgroundColor = C("rgba(242,179,61,.1)"); });
            R("mg-chip.is-pressed", s => { BorderColor(s, AMBER); s.backgroundColor = C("rgba(242,179,61,.16)"); });
            R("mg-phone-ok", s => s.color = C("#b7ecd9"));
            R("mg-closures-why", s => s.color = C(MUTED));
        }
    }
}
