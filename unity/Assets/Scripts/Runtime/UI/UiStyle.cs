// Ports style.css and ui/relay.css. Unity cannot parse USS at runtime, so CSS classes become C# style rules:
// AddClass() tags the element (AddToClassList) and applies every rule registered for the class inline.
// The minigame group registers its own classes (minigames.css) from MinigameStyle.
// Cascade: a class change restyles the element and its subtree. Restyle resets the "paint" properties
// (colours, borders, display, opacity, transforms) and re-runs every rule of every class the element has,
// in registration order, so modifier classes (.on, .low, .over, .selected) can be removed again and rules can
// look at ancestors (Css.Within) like descendant selectors. Sizes set by code (fill widths) are kept.
// The "hidden" class is the [hidden] attribute: display none after all rules (!important).
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class UiStyle
    {
        struct Rule { public int seq; public Action<VisualElement> apply; }
        static readonly Dictionary<string, List<Rule>> rules = new Dictionary<string, List<Rule>>();
        // "a.b" keys: apply when the element has every one of the classes (compound selectors like .mg-mode.is-guided).
        static readonly List<KeyValuePair<string[], Rule>> compound = new List<KeyValuePair<string[], Rule>>();
        static int seq;
        static bool baseDone;

        public static void Register(string className, Action<VisualElement> apply)
        {
            if (string.IsNullOrEmpty(className) || apply == null) return;
            if (className.IndexOf('.') >= 0)
            {
                compound.Add(new KeyValuePair<string[], Rule>(className.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries), new Rule { seq = seq++, apply = apply }));
                return;
            }
            if (!rules.TryGetValue(className, out var list)) rules[className] = list = new List<Rule>();
            list.Add(new Rule { seq = seq++, apply = apply });
        }

        // Several classes in one string ("btn primary") are split like className.
        public static VisualElement AddClass(VisualElement e, params string[] classNames)
        {
            if (e == null) return null;
            bool changed = false;
            foreach (var c in Split(classNames)) if (!e.ClassListContains(c)) { e.AddToClassList(c); changed = true; }
            if (changed) Restyle(e);
            return e;
        }

        public static void RemoveClass(VisualElement e, params string[] classNames)
        {
            if (e == null) return;
            bool changed = false;
            foreach (var c in Split(classNames)) if (e.ClassListContains(c)) { e.RemoveFromClassList(c); changed = true; }
            if (changed) Restyle(e);
        }

        public static void Toggle(VisualElement e, string className, bool on)
        {
            if (on) AddClass(e, className); else RemoveClass(e, className);
        }

        // [hidden] attribute.
        public static void Hidden(VisualElement e, bool on) => Toggle(e, "hidden", on);
        public static bool IsHidden(VisualElement e) => e != null && e.ClassListContains("hidden");

        // Re-run the rules of e and its subtree (after a class change, a text-scale change, or new children).
        public static void Restyle(VisualElement e)
        {
            if (e == null) return;
            Reset(e.style);
            var cls = e.GetClasses();
            List<Rule> mine = null;
            foreach (var c in cls)
                if (rules.TryGetValue(c, out var list)) (mine ??= new List<Rule>()).AddRange(list);
            foreach (var kv in compound)
            {
                bool all = true;
                foreach (var c in kv.Key) if (!e.ClassListContains(c)) { all = false; break; }
                if (all) (mine ??= new List<Rule>()).Add(kv.Value);
            }
            if (mine != null)
            {
                mine.Sort((a, b) => a.seq.CompareTo(b.seq));
                foreach (var r in mine)
                {
                    try { r.apply(e); } catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
                }
            }
            if (e.ClassListContains("hidden")) e.style.display = DisplayStyle.None;
            if (e is UiIcon ic) ic.Resize();
            for (int i = 0; i < e.childCount; i++) Restyle(e[i]);
        }

        static void Reset(IStyle s)
        {
            var n = StyleKeyword.Null;
            s.display = n; s.opacity = n; s.color = n; s.backgroundColor = n; s.visibility = n;
            s.borderTopColor = n; s.borderRightColor = n; s.borderBottomColor = n; s.borderLeftColor = n;
            s.borderTopWidth = n; s.borderRightWidth = n; s.borderBottomWidth = n; s.borderLeftWidth = n;
            s.translate = n; s.scale = n; s.rotate = n; s.unityFontStyleAndWeight = n; s.backgroundImage = n;
        }

        static IEnumerable<string> Split(string[] names)
        {
            if (names == null) yield break;
            foreach (var n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                foreach (var p in n.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)) yield return p;
            }
        }

        // Registers the style.css and relay.css rules (called once by UiRoot.Create).
        public static void RegisterBase()
        {
            if (baseDone) return;
            baseDone = true;
            StyleBase.Register();
            StyleHud.Register();
            StyleDialogs.Register();
            StylePanels.Register();
            StyleMenus.Register();
            StyleRelay.Register();
        }
    }
}
