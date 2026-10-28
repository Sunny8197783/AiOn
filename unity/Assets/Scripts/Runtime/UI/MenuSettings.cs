// Ports ui/menu.js settings(): 보기·소리·조작·접근성. Radio groups are segmented buttons (.seg > .seg-opt.checked),
// checkboxes are switch buttons (.set-toggle.on), ranges are SliderInts stepped by 5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Gyeongbo.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static partial class Menu
    {
        sealed class Radios { public string value; public List<Button> opts = new List<Button>(); }

        // Resolves the edited copy (cancel = the values it got).
        public static Task<Settings> SettingsMenu(Settings current)
        {
            var cur = current?.Clone() ?? new Settings();
            float scale0 = Css.TextScale;
            var groups = new Dictionary<string, Radios>();
            var toggles = new Dictionary<string, bool>();
            var ranges = new Dictionary<string, SliderInt>();

            VisualElement RadiosEl(string name, string legend, string[][] options, string value, string note = null)
            {
                var g = groups[name] = new Radios();
                foreach (var o in options)
                {
                    string v = o[0];
                    var opt = (Button)El("button", "seg-opt", El("span", null, El("b", null, El("span", "seg-check hidden", "✓ "), o[1]), o.Length > 2 ? El("small", null, o[2]) : null));
                    opt.clicked += () => Pick(name, v);
                    g.opts.Add(opt);
                    opt.userData = v;
                }
                var el = El("div", "set-group", El("legend", null, legend), El("div", "seg", g.opts), note != null ? El("p", "set-note", note) : null);
                Select(name, value);
                return el;
            }
            void Select(string name, string v)
            {
                var g = groups[name]; g.value = null;
                foreach (var b in g.opts)
                {
                    bool on = (string)b.userData == v;
                    if (on) g.value = v;
                    UiStyle.Toggle(b, "checked", on);
                    UiStyle.Hidden(b.Q(className: "seg-check"), !on);
                }
            }
            void Pick(string name, string v)
            {
                Select(name, v);
                if (name == "textScale") UiRoot.SetTextScale(float.Parse(v, CultureInfo.InvariantCulture));   // live preview of --text-scale
            }
            VisualElement Toggle(string name, string label, string sub, bool value)
            {
                toggles[name] = value;
                var b = (Button)El("button", "set-toggle" + (value ? " on" : ""), El("span", "switch", El("span", "switch-knob")), El("span", "set-toggle-text", El("b", null, label), sub != null ? El("small", null, sub) : null));
                b.clicked += () => { toggles[name] = !toggles[name]; UiStyle.Toggle(b, "on", toggles[name]); };
                return b;
            }
            object[] Range(string name, string label, float value, int min = 0, int max = 100, Func<int, string> fmt = null, VisualElement labelIcon = null)
            {
                fmt ??= v => v + "%";
                int start = Mathf.Clamp((int)Math.Floor(value * 100 + 0.5), min, max);
                var outEl = El("output", null, fmt(start));
                var input = new SliderInt(min, max) { value = start, focusable = true, name = "set-" + name };
                UiStyle.AddClass(input, "set-slider");
                input.RegisterValueChangedCallback(e =>
                {
                    int v = Mathf.Clamp(Mathf.RoundToInt(e.newValue / 5f) * 5, min, max);   // step="5"
                    if (v != e.newValue) { input.SetValueWithoutNotify(v); }
                    Dom.SetText(outEl, fmt(v));
                });
                ranges[name] = input;
                return new object[] { El("div", "set-range", El("span", null, labelIcon, label), outEl), input };
            }
            string[] O(string v, string label, string sub = null) => sub == null ? new[] { v, label } : new[] { v, label, sub };
            string Num(float v) => v.ToString(CultureInfo.InvariantCulture);

            var textScales = new[] { 0.9f, 1, 1.15f, 1.3f };
            string curScale = null;
            foreach (var s in textScales) if (Mathf.Approximately(s, cur.textScale)) curScale = Num(s);

            ModalHandle<Settings> m = null;
            void Cancel() { UiRoot.SetTextScale(scale0); m.Close(cur.Clone()); }
            void Submit()
            {
                Dom.UiSound("confirm");
                var r = cur.Clone();
                r.quality = groups["quality"].value ?? cur.quality; r.view = groups["view"].value ?? cur.view;
                r.textScale = groups["textScale"].value != null ? float.Parse(groups["textScale"].value, CultureInfo.InvariantCulture) : 1;
                r.volume = ranges["volume"].value / 100f; r.mute = toggles["mute"]; r.captions = toggles["captions"]; r.reduceMotion = toggles["reduceMotion"];
                r.sensitivity = ranges["sensitivity"].value / 100f; if (!(r.sensitivity > 0)) r.sensitivity = 1;
                r.minimap = toggles["minimap"]; r.profile = groups["profile"].value ?? "none";
                r.volAmbience = ranges["volAmbience"].value / 100f; r.volEffects = ranges["volEffects"].value / 100f; r.volUi = ranges["volUi"].value / 100f;
                m.Close(r);
            }

            var cols = El("div", "set-cols",
                El("div", "set-col",
                    RadiosEl("quality", "그래픽 품질", new[] { O("low", "낮음", "가벼운 노트북"), O("medium", "보통", "대부분의 PC"), O("high", "높음", "그림자·주변광") }, cur.quality, "느려지면 게임이 한 번 자동으로 품질을 낮춥니다."),
                    RadiosEl("view", "시점", new[] { O("third", "3인칭", "어깨 너머"), O("first", "1인칭", "정민의 눈") }, cur.view),
                    RadiosEl("textScale", "글자 크기", new[] { O("0.9", "작게"), O("1", "보통"), O("1.15", "크게"), O("1.3", "아주 크게") }, curScale),
                    RadiosEl("profile", "체험 모드", new[] { O("none", "기본"), O("mobility", "보행 장애", "달리기 불가 · 느린 걸음"), O("wheelchair", "휠체어", "앉은 눈높이 · 계단 불가"),
                        O("lowvision", "저시력", "흐린 시야 · 음성 안내"), O("deaf", "청각 장애", "소리 없음 · 자막과 신호"), O("guardian", "보호자 동반", "지체 장애 · 함께 걷기") }, cur.profile,
                        "몸과 감각의 조건에 따라 같은 길도 달라집니다."),
                    El("div", "set-group", El("legend", null, "조작"),
                        Range("sensitivity", "마우스 감도", cur.sensitivity, 30, 300, v => (v / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "배"),
                        Toggle("minimap", "미니맵 표시", "오른쪽 위의 작은 지도 (북쪽이 위)", cur.minimap))),
                El("div", "set-col",
                    El("div", "set-group", El("legend", null, "소리"),
                        Range("volume", "전체 음량", cur.volume, labelIcon: Dom.Icon("sound")),
                        Range("volAmbience", "환경음 (거리 · 실내)", cur.volAmbience), Range("volEffects", "효과음 (사이렌 · 불 · 라디오)", cur.volEffects), Range("volUi", "버튼 소리", cur.volUi),
                        Toggle("mute", "소리 끄기", "사이렌과 경보음도 꺼집니다. 자막은 계속 나옵니다.", cur.mute)),
                    El("div", "set-group", El("legend", null, "접근성"),
                        Toggle("captions", "자막 표시", "사이렌·방송·소리를 글로도 보여 줍니다.", cur.captions),
                        Toggle("reduceMotion", "움직임 줄이기", "화면 흔들림, 머리 흔들림, 깜빡임과 전환 효과를 줄입니다.", cur.reduceMotion))));
            var form = El("div", "settings-form",
                El("scroll", "set-scroll", cols),
                El("div", "set-actions", Dom.Btn("취소", Cancel, ghost: true), Dom.Btn("적용", Submit, primary: true, iconName: "check")));
            var content = El("div", "settings-panel", El("p", "eyebrow", Dom.Icon("sliders"), "설정"), El("h2", null, "보기·소리·조작·접근성"), form);
            m = Dom.Modal<Settings>("settings", "설정", content, e => { if (UiFx.Key(e) == "Escape") { Cancel(); return true; } return false; });
            // form.querySelector('input:checked')?.focus()
            var first = groups["quality"].opts.Find(b => b.ClassListContains("checked"));
            first?.Focus();
            return m.Done;
        }
    }
}
