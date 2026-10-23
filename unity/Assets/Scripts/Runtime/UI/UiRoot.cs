// The UI Toolkit root (index.html + the <html> classes): one UIDocument with PanelSettings made at runtime,
// the Korean font at the root (PORTING.md "Text and fonts"), layers for the HUD, overlays (cine bars,
// relay notes, minigame panels) and modals, the loading screen. Shared by both UI groups, story and app.
using System;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class UiRoot
    {
        public static void Create() => throw new NotImplementedException("port: index.html + UIDocument/PanelSettings at runtime");
        public static VisualElement Root => throw new NotImplementedException("port: document.body");
        public static VisualElement HudLayer => throw new NotImplementedException("port: #ui");
        public static VisualElement Overlay => throw new NotImplementedException("port: #overlay (cine, relay notes, 3D grab panels)");
        public static VisualElement ModalLayer => throw new NotImplementedException("port: modal host");

        // <html> classes and --text-scale (app.js applySettings).
        public static void SetTextScale(float scale) => throw new NotImplementedException("port: --text-scale");
        public static void SetReduceMotion(bool on) => throw new NotImplementedException("port: html.reduce-motion");
        public static void SetLowVision(bool on) => throw new NotImplementedException("port: html.sim-lowvision");
        public static bool ReduceMotion => throw new NotImplementedException("port: reduceMotion()");

        // #loading / #loading-text.
        public static void ShowLoading(string text = null) => throw new NotImplementedException("port: #loading");
        public static void HideLoading() => throw new NotImplementedException("port: #loading hidden");
    }
}
