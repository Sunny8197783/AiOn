// Ports ui/phone.js: the phone (battery, signal, recent messages, map app, actions) and the full-screen
// emergency alert (위급재난문자 / 안전안내문자).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class Phone
    {
        static readonly Dictionary<string, (string text, int bars)> SIGNAL = new Dictionary<string, (string, int)>
        {
            ["ok"] = ("신호 좋음", 4), ["weak"] = ("신호 약함", 1), ["none"] = ("서비스 없음", 0),
        };
        static readonly Dictionary<string, (string label, string icon)> KIND = new Dictionary<string, (string, string)>
        {
            ["emergency"] = ("위급재난문자", "alert"),
            ["safety"] = ("안전안내문자", "info"),
            ["sms"] = ("문자", "sms"),
            ["me"] = ("나", "sms"),
        };

        static VisualElement El(string tag, string cls, params object[] kids) => Dom.El(tag, cls, kids);
        static float Now => Time.realtimeSinceStartup * 1000;
        static string Rnd(double v) => Math.Floor(v + 0.5).ToString(CultureInfo.InvariantCulture);

        static VisualElement StatusBar(string time, string signal, double battery)
        {
            var (sigText, bars) = SIGNAL.TryGetValue(signal ?? "", out var s) ? s : SIGNAL["ok"];
            double b = double.IsNaN(battery) ? 0 : Math.Max(0, Math.Min(100, battery));
            var fill = El("span", "batt-fill");
            fill.style.width = Length.Percent((float)b);
            var sig = El("span", "ps-signal", new[] { 1, 2, 3, 4 }.Select(n => El("i", "sig" + (n <= bars ? " on" : ""))));
            sig.tooltip = sigText;
            return El("div", "phone-status",
                El("span", "ps-time", time ?? ""),
                El("span", "ps-right",
                    sig,
                    bars == 0 ? El("span", "ps-note", "서비스 없음") : bars == 1 ? El("span", "ps-note", "약함") : null,
                    El("span", "ps-batt" + (b <= 20 ? " low" : ""), El("span", "batt", fill), Rnd(b) + "%")));
        }

        static VisualElement MapApp(string state)
        {
            object[] body;
            VisualElement spinner = null;
            switch (state)
            {
                case "ok": body = new object[] { Dom.Icon("map"), El("b", null, "지도"), El("span", null, "현재 위치 · 연무동") }; break;
                case "loading": spinner = El("span", "spinner"); body = new object[] { spinner, El("b", null, "지도를 불러오는 중…"), El("span", null, "데이터 연결이 느립니다") }; break;
                case "fail": body = new object[] { Dom.Icon("alert"), El("b", null, "지도를 불러올 수 없습니다"), El("span", null, "네트워크 연결을 확인해 주세요") }; break;
                default: body = new object[0]; break;
            }
            // .map-tiles: white street grid on green-grey (the 46px repeating linear gradients).
            var tiles = El("div", "map-tiles");
            for (int i = 0; i < 6; i++) { var v = El("div", "map-line"); v.style.left = i * 46; v.style.top = 0; v.style.bottom = 0; v.style.width = 6; tiles.Add(v); UiStyle.Restyle(v); }
            for (int i = 0; i < 5; i++) { var h = El("div", "map-line"); h.style.top = i * 46; h.style.left = 0; h.style.right = 0; h.style.height = 5; tiles.Add(h); UiStyle.Restyle(h); }
            var app = El("section", "app-map state-" + state, tiles, El("div", "map-state", body));
            if (spinner != null) UiFx.Spin(spinner, 1);
            return app;
        }

        static VisualElement Bubble(PhoneMessage m)
        {
            var (kindLabel, ic) = KIND.TryGetValue(m.kind ?? "", out var k) ? k : KIND["sms"];
            bool official = m.kind == "emergency" || m.kind == "safety";
            return El("li", "msg msg-" + (string.IsNullOrEmpty(m.kind) ? "sms" : m.kind),
                El("div", "msg-head",
                    official ? El("span", "msg-kind", Dom.Icon(ic), kindLabel) : El("b", "msg-from", m.kind == "me" ? "나" : (string.IsNullOrEmpty(m.from) ? "알 수 없음" : m.from)),
                    official && !string.IsNullOrEmpty(m.from) && m.from != kindLabel ? El("span", "msg-from", m.from) : null,
                    El("time", "msg-time", m.time ?? "")),
                El("p", "msg-text", m.text ?? ""));
        }

        // phone({ battery, signal, messages, mapApp, actions }) -> actionId
        public static Task<string> Show(PhoneOptions o)
        {
            o ??= new PhoneOptions();
            var acts = o.actions != null && o.actions.Count > 0 ? o.actions.Where(a => a != null).ToList() : new List<Choice> { new Choice("close", "닫기") };
            var msgs = (o.messages ?? new List<PhoneMessage>()).Where(x => x != null).ToList();
            var time = msgs.Count > 0 ? msgs[msgs.Count - 1].time ?? "" : "";
            bool low = o.battery <= 15;
            float t0 = Now;
            ModalHandle<string> m = null;
            var list = msgs.Count > 0 ? El("ol", "msg-list", msgs.Select(Bubble)) : null;
            var screen = El("scroll", "phone-screen",
                low ? El("p", "phone-banner", Dom.Icon("battery"), "배터리 " + Rnd(Math.Max(0, o.battery)) + "% · 절전 모드") : null,
                o.mapApp != null ? MapApp(o.mapApp) : null,
                El("section", "app-msgs",
                    El("h3", "app-msgs-title", Dom.Icon("sms"), "메시지"),
                    (object)list ?? El("p", "empty", "새 메시지가 없습니다.")));
            var frame = El("div", "phone-frame",
                El("div", "phone-notch"),
                StatusBar(time, o.signal, o.battery),
                screen,
                El("div", "phone-actions", acts.Select((a, n) => (VisualElement)Dom.Btn(a.label ?? a.id, () => { if (Now - t0 > 200) { Dom.UiSound("click"); m.Close(a.id); } },
                    a.primary, !a.primary, null, n == 0 ? "autofocus" : ""))),
                El("div", "phone-home"));
            var escId = acts.FirstOrDefault(a => a.id == "close" || a.id == "back")?.id ?? (acts.Count == 1 ? acts[0].id : null);
            m = Dom.Modal<string>("phone", "휴대폰", frame, e => { if (UiFx.Key(e) == "Escape" && escId != null) { m.Close(escId); return true; } return false; });
            // Newest message in view.
            if (list != null && list.childCount > 0 && screen is ScrollView sv)
            {
                var last = list[list.childCount - 1];
                sv.schedule.Execute(() => sv.ScrollTo(last)).StartingIn(30);
            }
            return m.Done;
        }

        // emergencyAlert({ kind: '위급재난문자'|'긴급재난문자'|'안전안내문자', text, time })
        public static Task EmergencyAlert(AlertOptions o)
        {
            o ??= new AlertOptions();
            var kind = o.kind ?? "위급재난문자";
            var level = kind.Contains("위급") ? "critical" : kind.Contains("긴급") ? "urgent" : "safety";
            float t0 = Now;
            ModalHandle<bool> m = null;
            var sheet = El("div", "alert-sheet alert-" + level,
                El("header", "alert-head",
                    Dom.Icon(level == "safety" ? "info" : "alert"),
                    El("h2", "alert-title", kind),
                    !string.IsNullOrEmpty(o.time) ? El("time", "alert-time", o.time) : null),
                El("p", "alert-text", o.text ?? ""),
                El("footer", "alert-foot",
                    El("small", "alert-note", "게임 속 가상 문자입니다"),
                    Dom.Btn("확인", () => { if (Now - t0 > 300) m.Close(); }, primary: true, className: "autofocus")));
            m = Dom.Modal<bool>("ealert ealert-" + level, kind, sheet, e => { if (UiFx.Key(e) == "Escape") { m.Close(); return true; } return false; });
            UiFx.Drop(sheet, .4f);
            if (level == "critical")
            {
                UiFx.Shake(sheet, .5f, 2, .4f);
                // @keyframes alarm (1s, 3 times): the backdrop pulses to deep red.
                var layer = m.layer;
                UiFx.Tween(layer, 3, t =>
                {
                    float k = t >= 1 ? 0 : Mathf.Sin((t * 3 % 1) * Mathf.PI);
                    layer.style.backgroundColor = Color.Lerp(new Color(30 / 255f, 2 / 255f, 4 / 255f, .55f), new Color(120 / 255f, 6 / 255f, 12 / 255f, .6f), k);
                });
            }
            return m.Done;
        }
    }
}
