// Ports style.css and ui/relay.css. Unity cannot parse USS at runtime, so CSS classes become C# style rules:
// AddClass() tags the element (AddToClassList) and applies every rule registered for the class inline.
// The minigame group registers its own classes (minigames.css) from MinigameStyle.
using System;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class UiStyle
    {
        // Design tokens from style.css :root (colours as CSS strings, sizes in px).
        public static void Register(string className, Action<VisualElement> apply) => throw new NotImplementedException("port: style.css rule registry");
        public static VisualElement AddClass(VisualElement e, params string[] classNames) => throw new NotImplementedException("port: element.classList.add");
        public static void RemoveClass(VisualElement e, params string[] classNames) => throw new NotImplementedException("port: element.classList.remove");
        public static void Toggle(VisualElement e, string className, bool on) => throw new NotImplementedException("port: element.classList.toggle");
        // Registers the style.css and relay.css rules (called once by UiRoot.Create).
        public static void RegisterBase() => throw new NotImplementedException("port: style.css + ui/relay.css");
    }
}
