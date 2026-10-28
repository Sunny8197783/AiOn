// Ports ui/hud.js: chapter + clock, objective card, stats, phone battery, bag load, interact prompt, toasts and
// captions, hurt flash, tension vignette, compass, minimap, stamina bar and the cinematic letterbox.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using UnityEngine;
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

    public sealed partial class Hud
    {
        // key, label, icon, higherIsWorse
        static readonly (string key, string label, string icon, bool worse)[] STATS =
        {
            ("health", "체력", "heart", false),
            ("thirst", "갈증", "drop", true),
            ("fatigue", "피로", "moon", true),
            ("tension", "긴장", "pulse", true),
        };
        static (string word, string state) Condition(double good) => good >= 67 ? ("좋음", "good") : good >= 34 ? ("보통", "mid") : ("나쁨", "bad");
        static double Clamp(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(100, v));
        static readonly string[][] KEYS =
        {
            new[] { "WASD", "이동" }, new[] { "Shift", "달리기" }, new[] { "Space", "점프·넘기" }, new[] { "E", "살피기" }, new[] { "Q", "주먹" },
            new[] { "G", "줍기·던지기" }, new[] { "C", "엎드리기" }, new[] { "X", "구급함" }, new[] { "B", "분필" }, new[] { "F", "불빛" },
            new[] { "P", "휴대폰" }, new[] { "Tab", "수첩" }, new[] { "I", "소지품" }, new[] { "M", "지도" }, new[] { "V", "시점" },
            new[] { "H", "조작법" }, new[] { "Esc", "메뉴" },
        };

        VisualElement hud, objective, objSteps, battery, bagChip, bagFill, battFill, prompt, mapBox, stamina, staminaFill;
        VisualElement vignette, hurtEl, banner, toasts, captionBox, compassEl, mapEl;
        Label chapterNo, chapterTitle, clockText, objTitle, objBody, battText, bagText, promptText;
        readonly Dictionary<string, (VisualElement row, VisualElement fill, Label value)> statEls = new Dictionary<string, (VisualElement, VisualElement, Label)>();
        string lastChapter = "", lastObjective = "";
        Action captionDone;
        IVisualElementScheduledItem bannerTimer;
        Action stopPulse;

        static VisualElement El(string tag, string cls, params object[] kids) => Dom.El(tag, cls, kids);
        static Label L(string tag, string cls) => (Label)Dom.El(tag, cls);

        public static Hud Create(VisualElement root)
        {
            var h = new Hud();
            h.Build(root ?? UiRoot.HudLayer);
            return h;
        }

        void Build(VisualElement root)
        {
            // Top-left: chapter, clock, objective.
            chapterNo = L("span", "hud-chapter-no");
            chapterTitle = L("span", "hud-chapter-title");
            clockText = L("span", "hud-clock-text");
            objTitle = L("h2", "obj-title");
            objBody = L("p", "obj-body");
            objSteps = El("ol", "obj-steps");
            objective = El("section", "hud-objective panel hidden", El("p", "obj-eyebrow", Dom.Icon("arrow"), "목표"), objTitle, objBody, objSteps);
            var topLeft = El("div", "hud-tl", El("div", "hud-chapter", chapterNo, chapterTitle), El("div", "hud-clock", Dom.Icon("clock"), clockText), objective);

            // Top-right: battery and bag.
            battFill = El("span", "batt-fill");
            battText = L("span", "batt-text");
            battery = El("div", "hud-chip hud-battery hidden", El("span", "batt", battFill), battText);
            bagFill = El("span", "meter-fill");
            bagText = L("span", "bag-text");
            bagChip = El("div", "hud-chip hud-bag hidden", Dom.Icon("bag"), El("span", "meter", bagFill), bagText);
            var topRight = El("div", "hud-tr", battery, bagChip);

            // Bottom-left: stats.
            var rows = new List<VisualElement>();
            foreach (var (key, label, ic, _) in STATS)
            {
                var fill = El("span", "stat-fill"); var value = L("span", "stat-word");
                var row = El("div", "stat", Dom.Icon(ic), El("span", "stat-label", label), El("span", "stat-bar", fill), value);
                statEls[key] = (row, fill, value);
                rows.Add(row);
            }
            var stats = El("section", "hud-stats panel", rows);

            promptText = L("span", "prompt-text");
            prompt = El("div", "hud-prompt hidden", El("kbd", null, "E"), promptText);
            var keyEls = new List<VisualElement>();
            foreach (var k in KEYS) keyEls.Add(El("span", "hud-key", El("kbd", null, k[0]), k[1]));
            var keys = El("p", "hud-keys", keyEls);

            // Compass strip (top centre) and minimap (top right). Yaw 0 faces north (-z); positive yaw turns west.
            compassEl = El("div", "hud-compass");
            mapEl = El("div", "hud-minimap");
            heading = L("span", "sr-only");
            mapBox = El("div", "hud-map hidden", mapEl, heading);
            staminaFill = El("span", "stamina-fill");
            stamina = El("div", "hud-stamina hidden", El("span", "stamina-label", "기력"), El("span", "stamina-bar", staminaFill));

            vignette = El("div", "hud-vignette"); hurtEl = El("div", "hud-hurt");
            hurtEl.style.unityBackgroundImageTintColor = new Color(1, 1, 1, 0);
            banner = El("div", "hud-banner");
            var bars = new[] { El("div", "letterbox top"), El("div", "letterbox bottom") };

            hud = El("div", "hud hidden", vignette, hurtEl, bars, banner, compassEl, mapBox, topLeft, topRight, stats, stamina, prompt, keys);
            foreach (var c in hud.Children()) UiStyle.AddClass(c, "hud-child");
            vignette.style.unityBackgroundImageTintColor = new Color(1, 1, 1, 0);
            root.Add(hud);
            UiStyle.Restyle(hud);
            NoPick(hud);

            // Toasts and captions float above modals, so they live directly on the root.
            toasts = El("div", "toasts");
            captionBox = El("div", "captions");
            var flt = El("div", "hud-float", toasts, captionBox);
            NoPick(flt);
            UiRoot.AddFloat(flt);

            // body:has(.modal-layer) .hud-prompt { opacity: 0 }
            hud.schedule.Execute(() => prompt.style.opacity = UiRoot.ModalLayer != null && UiRoot.ModalLayer.childCount > 0 ? 0 : 1).Every(100);
        }

        static void NoPick(VisualElement e)
        {
            e.pickingMode = PickingMode.Ignore;
            foreach (var c in e.Children()) NoPick(c);
        }

        static IVisualElementScheduledItem Later(VisualElement on, long ms, Action a) => on.schedule.Execute(a).StartingIn(Math.Max(0, ms));

        // Raised with every caption's text (app.js speaks it in the 저시력 profile).
        public event Action<string> OnCaption;
        void Captioned(string text) => OnCaption?.Invoke(text);

        public void Show() => UiStyle.Hidden(hud, false);
        public void Hide() { UiStyle.Hidden(hud, true); UiStyle.Hidden(prompt, true); UiStyle.RemoveClass(hud, "cinematic"); }

        public void SetChapter(string label, string title)
        {
            chapterNo.text = label ?? "";
            chapterTitle.text = title ?? "";
            var key = (label ?? "undefined") + "|" + (title ?? "undefined");
            if (key == lastChapter) return;
            lastChapter = key;
            Dom.Replace(banner, El("span", "banner-no", label ?? ""), El("span", "banner-title", title ?? ""));
            NoPick(banner);
            UiStyle.RemoveClass(banner, "show");
            banner.schedule.Execute(() => UiStyle.AddClass(banner, "show"));
            bannerTimer?.Pause();
            bannerTimer = Later(banner, 3600, () => UiStyle.RemoveClass(banner, "show"));
        }

        public void SetClock(string text) => clockText.text = text ?? "";

        // title null hides the card.
        public void SetObjective(string title, string body = "", IList<ObjectiveStep> steps = null)
        {
            if (string.IsNullOrEmpty(title)) { UiStyle.Hidden(objective, true); lastObjective = ""; return; }
            UiStyle.Hidden(objective, false);
            objTitle.text = title;
            objBody.text = body ?? "";
            UiStyle.Hidden(objBody, string.IsNullOrEmpty(body));
            var items = new List<VisualElement>();
            if (steps != null)
                foreach (var s in steps)
                {
                    if (s == null) continue;
                    var txt = (Label)El("span", null, s.text ?? "");
                    if (s.done) { txt.enableRichText = true; txt.text = "<s><noparse>" + (s.text ?? "") + "</noparse></s>"; }
                    items.Add(El("li", "obj-step" + (s.done ? " done" : ""), Dom.Icon(s.done ? "check" : "circle"), txt, El("span", "sr-only", s.done ? " (완료)" : " (남음)")));
                }
            Dom.Replace(objSteps, items);
            NoPick(objSteps);
            UiStyle.Hidden(objSteps, steps == null || steps.Count == 0);
            if (title != lastObjective) UiFx.Flash(objective);
            lastObjective = title;
        }

        public void SetStats(Stats s)
        {
            if (s == null) return;
            foreach (var (key, label, _, worse) in STATS)
            {
                double v = Clamp(s[key]), good = worse ? 100 - v : v;
                var (word, state) = Condition(good);
                var (row, fill, value) = statEls[key];
                if (!Css.Has(row, "st-" + state)) { row.RemoveFromClassList("st-good"); row.RemoveFromClassList("st-mid"); row.RemoveFromClassList("st-bad"); UiStyle.AddClass(row, "st-" + state); }
                fill.style.width = Length.Percent((float)good);
                value.text = word;
                row.tooltip = label + " " + Math.Round(v, MidpointRounding.AwayFromZero) + " · " + word;
            }
        }

        // NaN hides the chip (JS null).
        public void SetBattery(double pct)
        {
            if (double.IsNaN(pct)) { UiStyle.Hidden(battery, true); return; }
            var p = Clamp(pct);
            UiStyle.Hidden(battery, false);
            UiStyle.Toggle(battery, "low", p <= 20);
            UiStyle.Toggle(battery, "dead", p <= 0);
            battFill.style.width = Length.Percent((float)p);
            battText.text = p <= 0 ? "꺼짐" : Rnd(p) + "%" + (p <= 20 ? " 부족" : "");
            battery.tooltip = "휴대폰 배터리 " + Rnd(p) + "%";
        }

        static string Rnd(double v) => Math.Floor(v + 0.5).ToString(CultureInfo.InvariantCulture);
        // String(number) for the bag limit (8 -> "8", 7.5 -> "7.5").
        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        public void SetBag(BagMeter load)
        {
            if (load == null) { UiStyle.Hidden(bagChip, true); return; }
            double w = double.IsNaN(load.weightKg) ? 0 : load.weightKg, lim = load.limitKg == 0 || double.IsNaN(load.limitKg) ? 8 : load.limitKg;
            bool over = w > lim;
            UiStyle.Hidden(bagChip, false);
            UiStyle.Toggle(bagChip, "over", over);
            bagFill.style.width = Length.Percent((float)Math.Min(100, w / lim * 100));
            var wf = w.ToString("0.0", CultureInfo.InvariantCulture);
            bagText.text = wf + " / " + Num(lim) + "kg" + (over ? " 무거움" : "");
            bagChip.tooltip = "배낭 무게 " + wf + "kg, 기준 " + Num(lim) + "kg" + (over ? ", 무거움" : "");
        }

        // null hides the prompt.
        public void Prompt(string text)
        {
            bool was = UiStyle.IsHidden(prompt);
            UiStyle.Hidden(prompt, string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) { promptText.text = text; if (was) UiFx.Rise(prompt, .25f); }
        }

        // label: official | site | neighbor | rumor | self
        public void Toast(string text, string label = "self", int ms = 3800)
        {
            label ??= "self";
            var t = El("div", "toast toast-" + label, Dom.LabelTag(label), El("span", "toast-text", text ?? ""));
            NoPick(t);
            Dom.Append(toasts, t);
            UiFx.Drop(t);
            while (toasts.childCount > 4) toasts.RemoveAt(0);
            Css.ApplyGap(toasts);
            Later(toasts, ms, () => { UiStyle.AddClass(t, "out"); Later(toasts, 400, () => { t.RemoveFromHierarchy(); Css.ApplyGap(toasts); }); });
        }

        // Resolves when the caption leaves (ms null = by length).
        public Task Caption(string text, string speaker = "", int? ms = null)
        {
            Captioned(text);
            captionDone?.Invoke();
            int dur = ms ?? Math.Max(2600, Math.Min(9000, 900 + (text ?? "").Length * 85));
            var c = El("div", "caption", !string.IsNullOrEmpty(speaker) ? El("b", "caption-speaker", speaker) : null, El("span", null, text ?? ""));
            NoPick(c);
            Dom.Replace(captionBox, c);
            UiFx.Fade(c, .3f);
            var tcs = new TaskCompletionSource<bool>();
            IVisualElementScheduledItem timer = null;
            Action finish = null;
            finish = () =>
            {
                timer?.Pause();
                if (captionDone == finish) captionDone = null;
                UiStyle.AddClass(c, "out");
                Later(captionBox, 350, () => c.RemoveFromHierarchy());
                tcs.TrySetResult(true);
            };
            timer = Later(captionBox, dur, finish);
            captionDone = finish;
            return tcs.Task;
        }

        // A red flash at the screen edge when the body takes a hit; k 0..1 is how hard.
        public void Hurt(float k = 0.5f)
        {
            float kk = Mathf.Max(0.2f, Mathf.Min(1, k));
            // Opacity animates through the tint so a restyle never leaves the flash showing.
            UiFx.Tween(hurtEl, 0.7f, t => hurtEl.style.unityBackgroundImageTintColor = new Color(1, 1, 1, kk * 0.85f * (1 - t)));
        }

        public void SetTension(float level)
        {
            float k = float.IsNaN(level) ? 0 : Mathf.Clamp01(level);
            vignette.style.unityBackgroundImageTintColor = new Color(1, 1, 1, k * 0.6f);
            bool pulse = k > 0.3f;
            if (pulse && stopPulse == null) stopPulse = UiFx.Pulse(vignette, 2.4f);
            else if (!pulse && stopPulse != null) { stopPulse(); stopPulse = null; vignette.style.opacity = StyleKeyword.Null; UiStyle.Restyle(vignette); }
        }

        public void SetStamina(float v, bool winded)
        {
            float k = float.IsNaN(v) ? 0 : Mathf.Clamp01(v);
            UiStyle.Hidden(stamina, k > 0.995f && !winded);
            staminaFill.style.width = Length.Percent(k * 100);
            UiStyle.Toggle(stamina, "winded", winded);
        }

        public void SetCinematic(bool on)
        {
            UiStyle.Toggle(hud, "cinematic", on);
            if (on) UiStyle.Hidden(prompt, true);
        }
    }
}
