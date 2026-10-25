// The UI Toolkit root (index.html + the <html> classes): one UIDocument with PanelSettings made at runtime,
// the Korean font at the root (PORTING.md "Text and fonts"), layers for the HUD, overlays (cine bars,
// relay notes, minigame panels) and modals, the loading screen. Shared by both UI groups, story and app.
// Layer order = CSS z-index order: #ui (HUD, 10) < modals (40) < #overlay (cine/relay/minigames, 55-60)
// < hud-float (toasts, captions, 70) < #loading (90).
using System;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class UiRoot
    {
        static UIDocument doc;
        static VisualElement root, hud, overlay, modals, loading, lowVision;
        static Label loadingText, live;
        static bool reduce;

        public static void Create()
        {
            if (root != null) return;
            var go = new GameObject("UI");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false);
            // ponytail: no ThemeStyleSheet (it is an editor-made asset), so no built-in control visuals; every
            // control here is styled inline by UiStyle. Upgrade: a theme .tss in Resources for TextField carets.
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ConstantPixelSize;
            ps.scale = 1;
            ps.sortingOrder = 100;
            ps.clearColor = false;
            doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            go.SetActive(true);
            root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            root.AddToClassList("ui-root");
            ApplyFont(root);
            root.style.color = Css.C(Css.TEXT);
            root.style.whiteSpace = WhiteSpace.Normal;
            root.style.fontSize = Css.Rem(1);

            UiStyle.RegisterBase();
            Dom.Install(root);

            // html.sim-lowvision #world { filter: blur(6px) brightness(.6) ... }: the world is not a DOM node.
            // ponytail: a dark veil stands in for the blur; upgrade: a blur pass in Post for the lowvision profile.
            lowVision = Layer("lowvision-veil");
            lowVision.style.backgroundColor = new Color(0.02f, 0.03f, 0.04f, 0.42f);
            lowVision.style.display = DisplayStyle.None;
            hud = Layer("ui");
            modals = Layer("modal-host");
            overlay = Layer("overlay");
            live = new Label { name = "live" };
            UiStyle.AddClass(live, "sr-only");
            root.Add(live);

            loading = new VisualElement { name = "loading" };
            UiStyle.AddClass(loading, "loading");
            var sym = new VisualElement();
            UiStyle.AddClass(sym, "loader-symbol");
            loadingText = new Label("연무동을 불러오는 중…") { name = "loading-text", enableRichText = false };
            loading.Add(sym);
            loading.Add(loadingText);
            root.Add(loading);
            UiFx.Ring(sym);
        }

        static VisualElement Layer(string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Css.Fill(e);
            root.Add(e);
            return e;
        }

        // FontAsset from the OS family list (Malgun Gothic, Apple SD Gothic Neo, Noto Sans CJK KR, NanumGothic).
        static void ApplyFont(VisualElement e)
        {
            foreach (var fam in Paint.FONT_FAMILIES)
            {
                try
                {
                    var fa = FontAsset.CreateFontAsset(fam, "Regular");
                    if (fa == null) continue;
                    e.style.unityFontDefinition = FontDefinition.FromSDFFont(fa);
                    return;
                }
                catch (Exception ex) { Debug.LogWarning("[ui] font " + fam + ": " + ex.Message); }
            }
            Debug.LogWarning("[ui] no Korean OS font found; UI text uses the default font");
        }

        public static VisualElement Root => root;
        public static VisualElement HudLayer => hud;
        public static VisualElement Overlay => overlay;
        public static VisualElement ModalLayer => modals;
        internal static Label Live => live;

        // hud-float sits above modals and overlays but below the loading screen.
        internal static void AddFloat(VisualElement e)
        {
            int i = loading != null ? root.IndexOf(loading) : -1;
            if (i >= 0) root.Insert(i, e); else root.Add(e);
        }

        // <html> classes and --text-scale (app.js applySettings).
        public static void SetTextScale(float scale)
        {
            if (!(scale > 0)) scale = 1;
            if (Mathf.Approximately(scale, Css.TextScale)) return;
            Css.TextScale = scale;
            if (root == null) return;
            root.style.fontSize = Css.Rem(1);
            UiStyle.Restyle(root);
        }

        public static void SetReduceMotion(bool on)
        {
            reduce = on;
            if (root != null) UiStyle.Toggle(root, "reduce-motion", on);
        }

        public static void SetLowVision(bool on)
        {
            if (root == null) return;
            UiStyle.Toggle(root, "sim-lowvision", on);
            lowVision.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static bool ReduceMotion => reduce;

        // #loading / #loading-text.
        public static void ShowLoading(string text = null)
        {
            if (loading == null) return;
            if (text != null) loadingText.text = text;
            UiStyle.Hidden(loading, false);
            loading.BringToFront();
        }

        public static void HideLoading()
        {
            if (loading != null) UiStyle.Hidden(loading, true);
        }
    }
}
