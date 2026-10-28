// Ports ui/radio.js: a portable radio (or the phone's FM app). Switch it on, pull out the antenna (the phone needs
// earphones as its antenna), then turn the tuning knob — drag it round, scroll on it, or ←/→ (Shift for fine
// steps) — while the needle crawls along the dial. The signal meter, waveform and static (onSignal → audio) follow
// the clarity. Tuned in, the broadcast arrives line by line as captions; '메모' keeps a line.
// Resolves { station, official, heard, noted: [{ text, kind }] }; official = an official line was heard.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class Radio
    {
        const double MIN = 87.5, MAX = 108, LOCK = 0.75;
        static double R1(double f) => Mg.Round(Math.Min(MAX, Math.Max(MIN, f)) * 10) / 10;
        static string Word(double c) => c <= 0 ? "신호 없음" : c < 0.4 ? "약함" : c < LOCK ? "보통" : "잘 들림";
        static double LineSeconds(string s) => Math.Min(5.5, Math.Max(2.4, 1.2 + s.Length * 0.055));
        static string F1(double f) => f.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        static readonly System.Random rnd = new System.Random();

        sealed class Heard { public Station station; public string text; }

        public static Task<RadioResult> Open(RadioOptions o)
        {
            MinigameStyle.Register();
            o ??= new RadioOptions();
            string mode = o.mode ?? "guided";
            var stations = o.stations ?? new List<Station>();
            bool phone = o.device == "phone";
            string aerial = phone ? "이어폰" : "안테나";
            var onSignal = o.onSignal;
            double freq = R1(double.IsNaN(o.start) || o.start == 0 ? 89.5 : o.start);
            bool power = false, antenna = false, canPower = false, closed = false;
            float frameAcc = 0, lockT = 0, nextAt = 0.8f, clar = 0;
            Station cur = null; string shownState = "";
            var pos = new Dictionary<Station, int>(); var heard = new List<Heard>(); var noted = new List<Heard>(); var seen = new HashSet<string>();
            var target = stations.FirstOrDefault(s => s.kind == "official") ?? stations.FirstOrDefault();
            bool rm = MiniDom.Reduced();

            Station Best()
            {
                var n = new Dictionary<Station, int>();
                foreach (var h in heard) n[h.station] = (n.TryGetValue(h.station, out var c) ? c : 0) + 1;
                Station top = null;
                foreach (var h in heard) if (top == null || n[h.station] >= n[top]) top = h.station; // ties: the later one
                return top;
            }
            void Signal(float? v) { try { onSignal?.Invoke(v); } catch (Exception) { /* audio is optional */ } }
            ModalHandle<RadioResult> m = null;
            var dial = new MgCanvas(720, 112, "mg-dial-canvas");
            var wave = new MgCanvas(360, 56, "mg-wave");
            void Finish()
            {
                if (closed) return;
                closed = true; Signal(null); dial.Dispose(); wave.Dispose();
                m.Close(new RadioResult { station = Best(), official = heard.Any(h => h.station.kind == "official"), heard = heard.Select(h => h.text).ToList(), noted = noted.Select(x => new RadioNote { text = x.text, kind = x.station.kind }).ToList() });
            }
            m = MiniDom.Modal<RadioResult>("mg-radio", phone ? "휴대폰 FM 라디오" : "휴대용 라디오", null, e => { if (Mg.Key(e) == "Escape") { Finish(); return true; } return false; });

            // --- faceplate ---
            var slider = Mg.El("div", "mg-dial", dial.el);
            slider.focusable = true; slider.tabIndex = 0;
            var readout = Mg.Text("mg-freq");
            var bars = Enumerable.Range(0, 5).Select(i => { var b = Mg.Cls(new VisualElement(), "mg-sig-bar"); b.style.height = Length.Percent(20 * (i + 1)); return b; }).ToList();
            var sigBars = Mg.El("span", "mg-sig", bars);
            var sigText = Mg.Text(null);
            var knob = Mg.Cls(new VisualElement(), "mg-knob");
            knob.style.width = 64; knob.style.height = 64;
            var knobMark = Mg.Cls(new VisualElement(), "mg-knob-mark"); knob.Add(knobMark);
            var powerLed = Mg.Cls(new VisualElement(), "mg-led");
            var status = Mg.El("p", "mg-radio-status");
            var capWho = Mg.El("p", "mg-cap-who"); var capText = Mg.Text("mg-cap-text");
            var caption = Mg.El("div", "mg-caption", capWho, capText);
            var transcript = Mg.Cls(new ScrollView(), "mg-transcript");
            var off = Mg.Btn("mg-btn mg-primary", Finish, phone ? "앱 닫기" : "라디오 끄기");
            Button powerBtn = null, antBtn = null;
            powerBtn = Mg.Btn("mg-btn", () =>
            {
                if (!canPower) return;
                if (power) { power = false; UiStyle.Toggle(powerLed, "on", false); Update(); } else PowerOn();
                powerBtn.text = power ? (phone ? "앱 끄기" : "전원 끄기") : (phone ? "FM 라디오 앱 켜기" : "전원 켜기");
            }, phone ? "FM 라디오 앱 켜기" : "전원 켜기");
            antBtn = Mg.Btn("mg-btn", () => { antenna = !antenna; antBtn.text = antenna ? (phone ? "이어폰 빼기" : "안테나 접기") : (phone ? "이어폰 꽂기" : "안테나 펴기"); Update(); }, phone ? "이어폰 꽂기" : "안테나 펴기");

            void SetFreq(double f) { if (!power) return; freq = R1(f); Update(); }
            slider.RegisterCallback<KeyDownEvent>(e =>
            {
                double fine = e.shiftKey ? 0.1 : 0.3;
                var k = Mg.Key(e);
                double? step = k == "ArrowRight" || k == "ArrowUp" ? fine : k == "ArrowLeft" || k == "ArrowDown" ? -fine : k == "PageUp" ? 1 : k == "PageDown" ? -1 : (double?)null;
                if (step != null) { e.StopPropagation(); SetFreq(freq + step.Value); }
                else if (k == "Home" || k == "End") { e.StopPropagation(); SetFreq(k == "Home" ? MIN : MAX); }
            });
            // Tuning knob: drag round its centre; one full turn moves the needle 2 MHz. Scrolling turns it in 0.1 MHz clicks.
            float? knobA = null;
            float AngleOf(Vector2 world) { var r = knob.worldBound; return Mathf.Atan2(world.y - r.center.y, world.x - r.center.x); }
            knob.RegisterCallback<PointerDownEvent>(e => { if (!power) return; e.StopPropagation(); knob.CapturePointer(e.pointerId); knobA = AngleOf(e.position); });
            knob.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (knobA == null || !knob.HasPointerCapture(e.pointerId)) return;
                float a = AngleOf(e.position), d = a - knobA.Value; if (d > Mathf.PI) d -= 2 * Mathf.PI; if (d < -Mathf.PI) d += 2 * Mathf.PI;
                knobA = a; freq = Math.Min(MAX, Math.Max(MIN, freq + d / (2 * Mathf.PI) * 2)); Update();
            });
            void EndKnob(int id) { if (knob.HasPointerCapture(id)) knob.ReleasePointer(id); knobA = null; freq = R1(freq); Update(); }
            knob.RegisterCallback<PointerUpEvent>(e => EndKnob(e.pointerId));
            knob.RegisterCallback<PointerCancelEvent>(e => EndKnob(e.pointerId));
            foreach (var tgt in new[] { knob, slider }) tgt.RegisterCallback<WheelEvent>(e => { if (!power) return; e.StopPropagation(); SetFreq(freq + (e.delta.y < 0 ? 0.1 : -0.1)); });

            void DrawDial()
            {
                var g = dial.g; int W = g.width, H = g.height;
                float X(double f) => 30 + (float)((f - MIN) / (MAX - MIN)) * (W - 60);
                var bg = new PaintGradient(0, 0, 0, H);
                bg.addColorStop(0, power ? "#3b2a0d" : "#22211f"); bg.addColorStop(0.5f, power ? "#5a4113" : "#2a2927"); bg.addColorStop(1, power ? "#2b1e09" : "#1c1b1a");
                g.fillStyle = bg; g.fillRect(0, 0, W, H);
                if (power) { var glow = new PaintGradient(X(freq), H * 0.55f, 4, X(freq), H * 0.55f, 220); glow.addColorStop(0, "rgba(255,200,110,.28)"); glow.addColorStop(1, "rgba(255,200,110,0)"); g.fillStyle = glow; g.fillRect(0, 0, W, H); }
                g.strokeStyle = power ? "#ffd79a" : "#77736c"; g.fillStyle = g.strokeStyle; g.lineWidth = 2;
                g.font = "600 19px " + Kit.FONT; g.textAlign = "center";
                for (int i = 0; i <= 40; i++)
                {
                    double f = 88 + i * 0.5; float x = X(f); bool major = i % 4 == 0, mid = i % 2 == 0;
                    g.beginPath(); g.moveTo(x, 16); g.lineTo(x, 16 + (major ? 24 : mid ? 15 : 8)); g.stroke();
                    if (major) g.fillText(Mg.Num(f), x, 64);
                }
                g.font = "700 15px " + Kit.FONT; g.textAlign = "left"; g.fillText("FM", 8, 98); g.textAlign = "right"; g.fillText("MHz", W - 8, 98);
                g.strokeStyle = "rgba(255,255,255,.12)"; g.beginPath(); g.moveTo(30, 82); g.lineTo(W - 30, 82); g.stroke();
                g.save(); if (power) { g.shadowColor = "#ff5a3c"; g.shadowBlur = 12; }
                g.strokeStyle = "#ff4a35"; g.lineWidth = 3; g.beginPath(); g.moveTo(X(freq), 8); g.lineTo(X(freq), H - 8); g.stroke(); g.restore();
                var gl = new PaintGradient(0, 0, 0, H * 0.45f); gl.addColorStop(0, "rgba(255,255,255,.14)"); gl.addColorStop(1, "rgba(255,255,255,0)");
                g.fillStyle = gl; g.fillRect(0, 0, W, H * 0.45f); // glass reflection
                dial.Upload();
            }

            void DrawWave(float t, float clarity)
            {
                var g = wave.g; int W = g.width, H = g.height;
                g.fillStyle = "#120e06"; g.fillRect(0, 0, W, H);
                if (power)
                {
                    g.strokeStyle = "#ffc861"; g.lineWidth = 1.6f; g.shadowColor = "#ffb23d"; g.shadowBlur = 6; g.beginPath();
                    float voice = cur != null ? 1 : 0.25f;
                    for (int x = 0; x <= W; x += 3)
                    {
                        float v = clarity * voice * Mathf.Sin(x * 0.09f + t * 9) * Mathf.Sin(x * 0.017f + t * 2.1f) * H * 0.34f;
                        float n = (float)(rnd.NextDouble() * 2 - 1) * (1 - clarity) * H * 0.4f;
                        if (x != 0) g.lineTo(x, H / 2f + v + n); else g.moveTo(x, H / 2f + v + n);
                    }
                    g.stroke(); g.shadowBlur = 0;
                }
                wave.Upload();
            }

            void AddHeard(Station station, int i)
            {
                string key = $"{Mg.Num(station.freq)}:{i}", text = station.lines[i];
                if (seen.Contains(key)) return;
                seen.Add(key); heard.Add(new Heard { station = station, text = text });
                Button memo = null;
                memo = Mg.Btn("mg-btn mg-chip", () =>
                {
                    int j = noted.FindIndex(x => x.text == text); bool on = j < 0;
                    if (on) noted.Add(new Heard { text = text, station = station }); else noted.RemoveAt(j);
                    MinigameStyle.Pressed(memo, on); memo.text = on ? "✓ 메모함" : "✎ 메모";
                }, "✎ 메모");
                var li = Mg.El("li", "mg-tr-row", Mg.Text("mg-tr-who", $"{Mg.Num(station.freq)}MHz"), Mg.Text("mg-tr-text", text), memo);
                transcript.Add(li);
                transcript.schedule.Execute(() => transcript.ScrollTo(li));
            }

            void Update()
            {
                // Antenna folded / no earphones: only a fraction of the signal gets through, never enough to lock.
                var sig = power ? Drills.RadioSignal(R1(freq), stations) : new RadioSignalResult { station = null, clarity = 0 };
                var station = sig.station; float clarity = (float)(sig.clarity * (antenna ? 1 : 0.6));
                Signal(power ? clarity : (float?)null);
                readout.text = power ? F1(freq) : "--.-";
                for (int i = 0; i < bars.Count; i++) UiStyle.Toggle(bars[i], "on", power && clarity * 5 > i + 0.01f);
                sigText.text = power ? $"신호 {Word(clarity)}{(antenna ? "" : $" · {aerial} 없음")}" : "꺼짐";
                knob.style.rotate = new Rotate(new Angle((float)((freq - MIN) * 180), AngleUnit.Degree)); // 2 MHz per turn, as the drag
                var locked = power && station != null && clarity >= LOCK ? station : null;
                slider.tooltip = $"{F1(freq)}MHz, 신호 {Word(clarity)}{(locked != null ? $", {locked.name}" : "")}";
                if (locked != cur) { cur = locked; lockT = 0; nextAt = 0.8f; }
                string state = !power ? "off" : cur != null ? $"on:{Mg.Num(cur.freq)}" : clarity > 0 ? "near" : "static";
                if (state != shownState)
                {
                    shownState = state;
                    if (cur != null) { capWho.Clear(); MiniDom.Put(capWho, Mg.El("b", null, cur.name), mode == "guided" && (cur.kind == "official" || cur.kind == "rumor") ? MiniDom.Pill(cur.kind) : null); capText.text = "…"; }
                    else if (state == "near") { capWho.Clear(); capText.text = mode == "free" ? "…지직… 말소리가 섞여 들린다…" : "…지직… 말소리가 섞여 들린다. 조금만 더 맞춰 보자."; }
                    else if (state == "static") { capWho.Clear(); capText.text = "치이이익— (잡음뿐이다)"; }
                }
                clar = clarity;
                DrawDial();
            }

            Mg.Loop((dt, now) =>
            {
                if (closed) return false;
                if (cur != null && cur.lines != null && cur.lines.Count > 0)
                {
                    lockT += dt;
                    if (lockT >= nextAt)
                    {
                        int i = pos.TryGetValue(cur, out var p) ? p : 0;
                        capText.text = cur.lines[i];
                        AddHeard(cur, i);
                        pos[cur] = (i + 1) % cur.lines.Count;
                        nextAt = lockT + (float)LineSeconds(cur.lines[i]) + (i + 1 == cur.lines.Count ? 2.5f : 0);
                    }
                }
                frameAcc += dt;
                if (!rm || frameAcc > 0.16f) { frameAcc = 0; DrawWave(now, clar); }
                return true;
            });
            var help = MiniDom.LadderHelp(mode, () => new List<VisualElement> {
                Mg.El("p", null, $"전원을 켜고 {(aerial == "안테나" ? "안테나를 편 뒤" : "이어폰을 꽂은 뒤")}, 손잡이를 천천히 돌려 보세요 — 목표 {(target != null ? $"{Mg.Num(target.freq)}MHz" : "방송이 들리는 곳")}"),
                Mg.El("p", "mg-why", $"잡음이 줄고 신호 막대가 차오르면 방송이 들립니다. {(phone ? "휴대폰 FM 라디오는 이어폰 줄이 안테나 역할을 합니다." : "안테나를 접으면 신호가 약해집니다.")}") });

            MiniDom.Put(m.root,
                MiniDom.Header(phone ? "FM 라디오" : "라디오", phone ? "휴대폰 FM 라디오 앱 · 배터리 사용" : "휴대용 라디오", mode, "mg-radio-title"),
                help,
                Mg.El("div", "mg-radio-body",
                    Mg.El("div", "mg-speaker"),
                    Mg.El("div", "mg-radio-face",
                        Mg.El("div", "mg-lcd", powerLed, readout, Mg.El("small", null, "MHz"), Mg.El("span", "mg-sigbox", sigBars, sigText)),
                        wave.el, slider, Mg.El("div", "mg-knobrow", powerBtn, antBtn, Mg.El("small", null, "TUNING · 돌리기"), knob))),
                status, caption,
                Mg.El("h3", "mg-sub", "들은 내용"), transcript,
                Mg.El("footer", "mg-foot", Mg.El("p", "mg-keys", "손잡이를 끌어 돌리기 · 휠 0.1MHz · 다이얼에서 ←/→ 0.3MHz · Shift+←/→ 0.1MHz · PgUp/PgDn 1MHz · Esc 끄기"), Mg.El("div", "mg-actions", off)));

            void PowerOn() { if (closed || !canPower) return; power = true; powerBtn.text = phone ? "앱 끄기" : "전원 끄기"; UiStyle.Toggle(powerLed, "on", true); status.Clear(); Update(); }
            Update();
            DrawWave(0, 0);
            // The player switches it on; the batteries (if they had to be found first) come before that.
            void Ready() { if (closed) return; canPower = true; Mg.Replace(status, phone ? "FM 라디오 앱을 켜세요." : "전원 스위치를 켜세요."); powerBtn.Focus(); }
            if (!o.battery)
            {
                Mg.Replace(status, phone ? "배터리가 없어 켜지지 않는다." : "건전지가 없어 켜지지 않는다.");
                capText.text = "";
            }
            else if (o.extraSeconds > 0)
            {
                float wait = rm ? 1.2f : Mathf.Min(4, o.extraSeconds / 10f);
                var fill = Mg.Cls(new VisualElement(), "mg-bar-fill"); fill.style.width = 0;
                var bar = Mg.El("span", "mg-bar", fill); bar.style.width = 140;
                Mg.Replace(status, "어둠 속에서 건전지를 찾아 넣는 중… ", bar);
                float t0 = Time.unscaledTime;
                Mg.Loop((dt, now) => { fill.style.width = Length.Percent(Mathf.Min(1, (now - t0) / wait) * 100); return !closed && now - t0 < wait; });   // mg-fill-anim
                Mg.Later(m.root, wait, Ready);
            }
            else Ready();
            slider.Focus();   // data-autofocus
            return m.Done;
        }
    }
}
