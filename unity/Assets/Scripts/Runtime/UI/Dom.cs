// Ports ui/dom.js: tiny element builder for the story UI (UI Toolkit), focus-trapped modals, labels, icons,
// buttons, UI sounds (Core.GameEvents.Ui). Used by the UI core files.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    // modal() -> { root, close(result), done }
    public sealed class ModalHandle<T>
    {
        public VisualElement root;
        public void Close(T result = default) => throw new NotImplementedException("port: ui/dom.js:modal.close");
        public Task<T> Done => throw new NotImplementedException("port: ui/dom.js:modal.done");
    }

    public sealed class LabelInfo
    {
        public string text, icon;
    }

    public static class Dom
    {
        public static string EscapeText(string s) => s ?? "";
        // el(tag, attrs, ...children): tag picks the element (button -> Button, p/span/b/h2/small -> Label,
        // input -> TextField/Toggle, others -> VisualElement); children are elements or strings.
        public static VisualElement El(string tag, string className = null, params object[] children) => throw new NotImplementedException("port: ui/dom.js:el");
        public static void TrapFocus(VisualElement root) => throw new NotImplementedException("port: ui/dom.js:trapFocus");
        // onKey returns true when it handled the key.
        public static ModalHandle<T> Modal<T>(string className = "", string label = "", VisualElement content = null, Func<KeyDownEvent, bool> onKey = null, bool backdrop = true) => throw new NotImplementedException("port: ui/dom.js:modal");
        public static void FocusFirst(VisualElement root) => throw new NotImplementedException("port: ui/dom.js:focusFirst");
        // official | site | neighbor | rumor | self
        public static readonly Dictionary<string, LabelInfo> LABELS = new Dictionary<string, LabelInfo>();
        public static VisualElement LabelTag(string label) => throw new NotImplementedException("port: ui/dom.js:labelTag");
        public static VisualElement Icon(string name, string label = "", string className = "") => throw new NotImplementedException("port: ui/dom.js:icon");
        public static void UiSound(string name) => Core.GameEvents.Ui(name);
        public static Button Btn(string label, Action onclick = null, bool primary = false, bool ghost = false, string iconName = null, string className = "") => throw new NotImplementedException("port: ui/dom.js:btn");
        public static bool ReduceMotion() => UiRoot.ReduceMotion;
    }
}
