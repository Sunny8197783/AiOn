// Ports ui/hud.js: chapter + clock, objective card, stats, phone battery, bag load, interact prompt, toasts and
// captions, hurt flash, tension vignette, compass, minimap, stamina bar and the cinematic letterbox.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    // hud.setBag({ weightKg, limitKg }) or null.
    public sealed class BagMeter
    {
        public double weightKg, limitKg;
    }

    // hud.setMap({ x, y, z, yaw, walk, solids, points, scale }) — JS space; scale = pixels per metre.
    public sealed class HudMap
    {
        public float x, y, z, yaw, scale;
        public List<WalkZone> walk;
        public List<Solid> solids;
        public List<Point> points;
    }

    public sealed class Hud
    {
        public static Hud Create(VisualElement root) => throw new NotImplementedException("port: ui/hud.js:createHud");

        // Raised with every caption's text (app.js speaks it in the 저시력 profile).
        public event Action<string> OnCaption;
        void Captioned(string text) => OnCaption?.Invoke(text);

        public void Show() => throw new NotImplementedException("port: ui/hud.js:show");
        public void Hide() => throw new NotImplementedException("port: ui/hud.js:hide");
        public void SetChapter(string label, string title) => throw new NotImplementedException("port: ui/hud.js:setChapter");
        public void SetClock(string text) => throw new NotImplementedException("port: ui/hud.js:setClock");
        // title null hides the card.
        public void SetObjective(string title, string body = "", IList<ObjectiveStep> steps = null) => throw new NotImplementedException("port: ui/hud.js:setObjective");
        public void SetStats(Stats s) => throw new NotImplementedException("port: ui/hud.js:setStats");
        public void SetBattery(double pct) => throw new NotImplementedException("port: ui/hud.js:setBattery");
        public void SetBag(BagMeter load) => throw new NotImplementedException("port: ui/hud.js:setBag");
        // null hides the prompt.
        public void Prompt(string text) => throw new NotImplementedException("port: ui/hud.js:prompt");
        // label: official | site | neighbor | rumor | self
        public void Toast(string text, string label = "self", int ms = 3800) => throw new NotImplementedException("port: ui/hud.js:toast");
        // Resolves when the caption leaves (ms null = by length).
        public Task Caption(string text, string speaker = "", int? ms = null) => throw new NotImplementedException("port: ui/hud.js:caption");
        public void Hurt(float k = 0.5f) => throw new NotImplementedException("port: ui/hud.js:hurt");
        public void SetTension(float level) => throw new NotImplementedException("port: ui/hud.js:setTension");
        public void SetCompass(float deg) => throw new NotImplementedException("port: ui/hud.js:setCompass");
        // null hides the minimap.
        public void SetMap(HudMap m) => throw new NotImplementedException("port: ui/hud.js:setMap");
        public void SetStamina(float v, bool winded) => throw new NotImplementedException("port: ui/hud.js:setStamina");
        public void SetCinematic(bool on) => throw new NotImplementedException("port: ui/hud.js:setCinematic");
    }
}
