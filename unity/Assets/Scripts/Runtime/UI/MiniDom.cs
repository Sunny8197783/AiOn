// Ports ui/mini-dom.js: the small DOM toolkit the minigames share (UI Minigames group calls it; the UI core
// group owns it): element builder, modal, label pills, header, the guided/hint/free help ladder, meters,
// number formats and the live-region announce.
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class MiniDom
    {
        public static VisualElement El(string tag, string className = null, params object[] kids) => throw new NotImplementedException("port: ui/mini-dom.js:el");
        // put(parent, ...kids): nulls skipped, nested lists flattened.
        public static VisualElement Put(VisualElement parent, params object[] kids) => throw new NotImplementedException("port: ui/mini-dom.js:put");
        public static string EscapeText(string s) => s ?? "";
        public static List<VisualElement> Focusables(VisualElement root) => throw new NotImplementedException("port: ui/mini-dom.js:focusables");
        public static void TrapFocus(VisualElement root) => throw new NotImplementedException("port: ui/mini-dom.js:trapFocus");
        public static ModalHandle<T> Modal<T>(string className = "", string label = "", IList<VisualElement> content = null, Func<KeyDownEvent, bool> onKey = null) => throw new NotImplementedException("port: ui/mini-dom.js:modal");
        public static bool Reduced() => UiRoot.ReduceMotion;
        public static VisualElement Pill(string label) => throw new NotImplementedException("port: ui/mini-dom.js:pill");
        // mode: guided | hint | free | view
        public static VisualElement Header(string eyebrow, string title, string mode, string id = null) => throw new NotImplementedException("port: ui/mini-dom.js:header");
        // guided: help shown; hint: a 힌트 button reveals it; free/view: null. make() builds the help nodes.
        public static VisualElement LadderHelp(string mode, Func<IList<VisualElement>> make) => throw new NotImplementedException("port: ui/mini-dom.js:ladderHelp");
        // 'ok' | 'warn' | 'over'
        public static string MeterState(double v, double max) => v > max ? "over" : v > max * 0.8 ? "warn" : "ok";
        public static readonly Dictionary<string, string> METER_WORD = new Dictionary<string, string> { ["ok"] = "여유", ["warn"] = "거의 가득", ["over"] = "초과" };
        public static string FmtKg(double n) => throw new NotImplementedException("port: ui/mini-dom.js:fmtKg");
        public static string Won(double n) => throw new NotImplementedException("port: ui/mini-dom.js:won");
        public static void Announce(string text) => throw new NotImplementedException("port: ui/mini-dom.js:announce");
    }
}
