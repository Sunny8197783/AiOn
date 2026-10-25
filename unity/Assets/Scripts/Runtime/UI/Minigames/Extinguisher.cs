// Ports ui/extinguisher.js: decide first (끈다 / 대피하며 알린다), then work the extinguisher by pressing the steps
// in order — press-and-hold for 손잡이, then sweep the nozzle by moving the mouse left and right over the fire
// (←/→ strokes from the keyboard) — over a live canvas of the fire (ExtinguisherScene).
// Retreat is always available. Resolves { sequence, result } (result from Drills.EvalExtinguisher).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class Extinguisher
    {
        static readonly string[] OPS = { "pin", "aim", "squeeze", "sweep" };
        static readonly Dictionary<string, float> GROW_S = new Dictionary<string, float> { ["guided"] = 70, ["hint"] = 45, ["free"] = 32 }; // seconds until a small fire spreads
        static List<string> FactText(IEnumerable<string> ids) => (ids ?? Enumerable.Empty<string>()).Select(id => Facts.Fact(id)?.text).Where(t => !string.IsNullOrEmpty(t)).ToList();
        static readonly Dictionary<string, Dictionary<string, string>> MISTAKE = new Dictionary<string, Dictionary<string, string>>
        {
            ["squeeze"] = new Dictionary<string, string> { ["pin"] = "안전핀이 꽂혀 있어 손잡이가 눌리지 않는다.", ["aim"] = "노즐이 불을 향하지 않았다. 분말이 엉뚱한 곳으로 간다." },
            ["sweep"] = new Dictionary<string, string> { ["pin"] = "안전핀이 그대로다. 아무것도 나오지 않는다.", ["aim"] = "노즐을 먼저 불 쪽으로 향해야 한다.", ["squeeze"] = "손잡이를 쥐지 않아 분말이 나오지 않는다." },
            ["aim"] = new Dictionary<string, string> { ["pin"] = "안전핀을 먼저 뽑아야 한다." },
        };
        static string Mistake(string id, string next) => next != null && MISTAKE.TryGetValue(id, out var d) && d.TryGetValue(next, out var s) ? s : "순서가 맞지 않는다.";
        static int Hash(string s) => s.Aggregate(7, (a, c) => (a * 31 + c) % 1009);

        public static Task<ExtinguisherResult> Open(ExtinguisherOptions o)
        {
            MinigameStyle.Register();
            o ??= new ExtinguisherOptions();
            string mode = o.mode ?? "guided";
            var steps = o.steps ?? Drills.EXT_STEPS;
            var choices = o.choices ?? Drills.EXT_CHOICES;
            bool practice = o.practice, large = o.fire == "large";
            var S = new ExtState { phase = practice ? "act" : "decide", fireLv = practice ? 0.3f : large ? 0.88f : 0.36f };
            var seq = practice ? new List<string> { "fight" } : new List<string>();
            bool rm = MiniDom.Reduced();
            float growPerS = practice ? 0 : (1 / (GROW_S.TryGetValue(mode, out var gs) ? gs : 40)) * (large ? 2.4f : 1);

            string Result()
            {
                try { return Drills.EvalExtinguisher(seq, large ? "large" : "small").result; }
                catch (Exception err) { Debug.LogError($"[extinguisher] could not evaluate {string.Join(",", seq)} {err}"); return "incomplete"; }
            }
            ModalHandle<ExtinguisherResult> m = null;
            var cv = new MgCanvas(640, 360, "mg-ext-canvas");
            void Finish() { if (S.closed) return; S.closed = true; cv.Dispose(); m.Close(new ExtinguisherResult { sequence = seq.ToList(), result = Result() }); }
            void Say(string text) => feedback.text = text;
            Label feedback = Mg.Text("mg-feedback");
            m = MiniDom.Modal<ExtinguisherResult>("mg-ext", "소화기", null, OnKey);

            // --- layout ---
            var dangerBar = Mg.Cls(new VisualElement(), "mg-bar-fill"); var dangerText = Mg.Text(null);
            dangerText.style.unityFontStyleAndWeight = FontStyle.Bold;
            var danger = Mg.El("div", "mg-meter mg-danger", Mg.El("small", null, "▲ " + (large ? "버틸 수 있는 시간" : "불이 번지기까지")), dangerText, Mg.El("span", "mg-bar", dangerBar));
            Mg.Hidden(danger, practice);
            var actions = Mg.El("div", "mg-ext-actions");
            var helpBox = Mg.El("div", "mg-ext-help");
            var retreatBtn = Mg.Btn("mg-btn mg-retreat", () => { if (practice) Finish(); else Retreat(); }, "⤺ " + (practice ? "연습 그만하기" : "물러나 알린다 (계단 · 119)"));

            MiniDom.Put(m.root,
                MiniDom.Header(practice ? "소화기 · 연습" : "소화기", practice ? "사용 순서 익히기" : large ? "천장까지 닿는 불길" : "작은 불", mode, "mg-ext-title"),
                practice ? Mg.El("p", "mg-why", "연습입니다. 실제 불은 없습니다.") : null,
                Mg.El("div", "mg-ext-grid",
                    Mg.El("div", "mg-ext-stage", cv.el, danger),
                    Mg.El("div", "mg-ext-side", helpBox, actions, feedback, retreatBtn)),
                Mg.El("p", "mg-keys", "Tab으로 고르기 · Enter/Space 누르기 · 손잡이는 길게 누르기 · 쓸 때는 불 위에서 마우스를 좌우로 (←/→도 됨) · Esc 물러나기"));

            // --- decision ---
            void RenderDecide()
            {
                var rec = large ? "retreat" : "fight";
                Button first = null;
                Mg.Replace(actions, Mg.El("p", "mg-q", "어떻게 할까?"), choices.Where(c => c.id == "fight").Select(c =>
                {
                    var b = Mg.Btn("mg-btn mg-choice" + (mode == "guided" && c.id == rec ? " is-next" : ""), () => { seq.Add("fight"); S.phase = "act"; RenderAct(); Say("소화기를 들고 불 앞에 섰다."); },
                        Mg.El("b", null, c.label), mode == "guided" ? FactText(c.facts).Select(t => Mg.El("small", null, t)).ToList() : null);
                    first ??= b;
                    return b;
                }).ToList());
                var r = choices.FirstOrDefault(c => c.id == "retreat");
                if (mode == "guided" && r != null) { retreatBtn.text = ""; Mg.Replace(retreatBtn, Mg.El("b", null, "⤺ " + r.label), FactText(r.facts).Select(t => Mg.El("small", null, t)).ToList()); }
                MinigameStyle.Next(retreatBtn, mode == "guided" && rec == "retreat");
                Mg.Replace(helpBox, MiniDom.LadderHelp(mode == "guided" ? "free" : mode, () => choices.Select(c => Mg.El("p", null, Mg.El("b", null, $"{c.label}: "), string.Join(" ", FactText(c.facts)))).ToList()));
                first?.Focus();
            }

            // --- acting ---
            var order = steps.OrderBy(s => Hash(s.id)).ToList();
            var btns = new Dictionary<string, Button>();
            var fills = new Dictionary<string, VisualElement>();
            bool keyHold = false;
            void RenderAct()
            {
                MinigameStyle.Next(retreatBtn, false);
                if (!practice) { retreatBtn.Clear(); retreatBtn.text = "⤺ 물러나 알린다 (계단 · 119)"; }
                btns.Clear(); fills.Clear();
                Mg.Replace(actions, Mg.El("p", "mg-q", "순서대로 누르세요"), Mg.El("div", "mg-steps", order.Select(s =>
                {
                    var fill = Mg.Cls(new VisualElement(), "mg-step-fill");
                    fill.pickingMode = PickingMode.Ignore;
                    var b = Mg.Btn("mg-btn mg-step-btn", null, fill, Mg.Text("mg-check", "○"), Mg.El("span", null, s.label),
                        s.id == "squeeze" ? Mg.Text("mg-holdtip", "길게 누르기") : s.id == "sweep" ? Mg.Text("mg-holdtip", "마우스로 좌우로 쓸기") : null);
                    btns[s.id] = b; fills[s.id] = fill;
                    if (s.id == "squeeze")
                    {
                        b.clickable = null;   // a click does nothing; only the hold counts
                        b.RegisterCallback<PointerDownEvent>(e => { b.CapturePointer(e.pointerId); StartHold(); }, TrickleDown.TrickleDown);
                        b.RegisterCallback<PointerUpEvent>(e => { b.ReleasePointer(e.pointerId); StopHold(); });
                        b.RegisterCallback<PointerCancelEvent>(_ => StopHold());
                        b.RegisterCallback<PointerCaptureOutEvent>(_ => StopHold());
                        b.RegisterCallback<KeyDownEvent>(e =>
                        {
                            var k = Mg.Key(e);
                            if ((k == " " || k == "Enter") && !keyHold) { keyHold = true; e.StopPropagation(); StartHold(); }
                            else if (k == " " || k == "Enter") e.StopPropagation();
                        });
                        b.RegisterCallback<KeyUpEvent>(e => { var k = Mg.Key(e); if (k == " " || k == "Enter") { keyHold = false; e.StopPropagation(); StopHold(); } });
                    }
                    else { var id = s.id; b.clicked += () => Press(id); }
                    return b;
                }).ToList()));
                if (order.Count > 0) btns[order[0].id].Focus();
                RefreshSteps();
            }

            string NextOp() => OPS.FirstOrDefault(id => !S.done.Contains(id));
            string Expected() => !S.done.Contains("shout") && S.done.Count == 0 ? "shout" : NextOp();
            void RefreshSteps()
            {
                foreach (var kv in btns)
                {
                    bool done = S.done.Contains(kv.Key);
                    MinigameStyle.Done(kv.Value, done);
                    MinigameStyle.Next(kv.Value, mode == "guided" && !done && kv.Key == Expected());
                    var check = kv.Value.Q<Label>(className: "mg-check");
                    if (check != null) { check.text = done ? "✓" : "○"; MinigameStyle.Done(check, done); }
                }
                var nx = steps.FirstOrDefault(s => s.id == Expected());
                List<VisualElement> Make() => nx != null ? new List<VisualElement> { Mg.El("p", null, Mg.El("b", null, "다음: "), nx.label) }.Concat(FactText(nx.facts).Select(t => Mg.El("p", "mg-why", t))).ToList() : new List<VisualElement>();
                if (mode == "guided") Mg.Replace(helpBox, Mg.El("div", "mg-help", Make()));
                else if (mode == "hint")
                {
                    Button hb = null;
                    hb = Mg.Btn("mg-btn mg-ghost", () => { var at = helpBox.IndexOf(hb); helpBox.Remove(hb); helpBox.Insert(Math.Max(0, at), Mg.El("div", "mg-help", Make())); }, "힌트: 다음 순서");
                    Mg.Replace(helpBox, hb);
                }
                else helpBox.Clear();
            }

            void Press(string id)
            {
                if (S.phase != "act" || S.ended) return;
                if (id == "shout")
                {
                    if (S.done.Contains("shout")) { Say("이미 알렸다."); return; }
                    S.done.Add("shout"); seq.Add("shout"); RefreshSteps();
                    Say("\"불이야!\" 소리에 이웃들이 문을 열고 내다본다."); return;
                }
                if (S.done.Contains(id) && id != "sweep") { Say("이미 했다."); return; }
                if (id == "sweep" && S.done.Contains("squeeze")) { Say("불 그림 위에서 마우스를 좌우로 크게 움직여 쓸어 주세요. (키보드: ←/→)"); return; }
                if (id != NextOp()) { DoMistake(Mistake(id, NextOp())); return; }
                S.done.Add(id); seq.Add(id);
                if (id == "pin") { S.pinOut = true; Say("안전핀을 뽑았다."); }
                if (id == "aim") { S.aimed = true; Say(large ? "노즐을 불 쪽으로 향했다. 열기가 얼굴까지 닿는다." : "문을 등지고 노즐을 불 쪽으로 향했다."); }
                RefreshSteps();
            }
            void StartHold()
            {
                if (S.phase != "act" || S.ended || S.done.Contains("squeeze")) return;
                if (NextOp() != "squeeze") { DoMistake(Mistake("squeeze", NextOp())); return; }
                S.holding = true;
            }
            void StopHold()
            {
                if (!S.holding) return;
                S.holding = false;
                if (!S.done.Contains("squeeze")) { S.hold = 0; if (fills.TryGetValue("squeeze", out var f)) f.style.width = 0; Say("손잡이를 끝까지 힘껏 쥐어야 한다. 길게 누르세요."); }
            }
            void Squeezed()
            {
                S.holding = false; S.done.Add("squeeze"); seq.Add("squeeze"); S.spraying = true;
                Say("손잡이를 움켜쥐자 하얀 분말이 뿜어져 나온다. 불 위에서 마우스를 좌우로 움직여 쓸듯이!");
                RefreshSteps(); if (btns.TryGetValue("sweep", out var sb)) sb.Focus();
            }
            void Stroke(int dir)
            {
                if (!S.done.Contains("squeeze") || S.ended) return;
                bool alt = dir != S.lastDir;
                S.lastDir = dir; S.sweepDir = dir;
                S.fireLv = Mathf.Max(0, S.fireLv - (large ? 0.012f : alt ? 0.085f : 0.03f));
                if (!alt && !large) Say("한쪽으로만 뿌리고 있다. 좌우로 쓸듯이.");
                if (!large && S.fireLv <= 0.02f) { S.fireLv = 0; seq.Add("sweep"); S.done.Add("sweep"); End("out"); }
            }
            // Mouse sweep: the nozzle follows the pointer across the canvas; every swing of ~1/6 of the width that
            // turns back counts as one stroke (alternating strokes are what put the fire out).
            float swing = 0; int swingDir = 0; float? lastX = null;
            cv.el.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!S.done.Contains("squeeze") || S.ended) { lastX = null; return; }
                float w = cv.el.layout.width; if (!(w > 0)) return;
                float x = e.localPosition.x / w;
                S.sweepDir = Mathf.Max(-1, Mathf.Min(1, (x - 0.76f) * 4));
                if (lastX != null)
                {
                    float dx = x - lastX.Value; int dir = Math.Sign(dx);
                    if (dir != 0 && dir != swingDir) { swingDir = dir; swing = 0; }
                    swing += Mathf.Abs(dx);
                    if (swing > 0.16f) { swing = -10; Stroke(swingDir); } // one stroke per swing until it turns back
                }
                lastX = x;
            });
            cv.el.RegisterCallback<PointerLeaveEvent>(_ => lastX = null);

            void DoMistake(string text)
            {
                S.danger = Mathf.Min(1, S.danger + (practice ? 0 : 0.12f)); S.shake = rm ? 0 : 0.35f;
                Say(text);
            }
            // Retreat stays the right call after the fire got away, so it works from the end screen too.
            void Retreat() { if (S.closed || S.endKind == "out") return; if (!seq.Contains("retreat")) seq.Add("retreat"); Finish(); }

            void End(string kind)
            {
                S.ended = true; S.endKind = kind; S.spraying = kind == "out" ? false : S.spraying;
                var recap = steps.Select(s => Mg.El("li", S.done.Contains(s.id) ? "is-done" : null, (S.done.Contains(s.id) ? "✓ " : "○ ") + s.label)).ToList();
                bool shoutFirst = seq.Where(id => id != "fight").FirstOrDefault() == "shout";
                string head = kind == "out" ? (practice ? "연습 완료" : "불이 꺼졌다") : kind == "spread" ? "불이 번졌다" : "더는 버틸 수 없다";
                var body = new List<object> { Mg.El("h3", null, head) };
                if (kind == "out")
                {
                    body.Add(Mg.El("ol", "mg-order", recap));
                    if (!shoutFirst) body.Add(Mg.El("p", "mg-why", Facts.Fact("fire_shout")?.text ?? ""));
                    if (practice) body.Add(FactText(new[] { "ext_1", "ext_2", "ext_3" }).Select(t => Mg.El("p", "mg-why", t)).ToList());
                }
                else if (kind == "spread") body.Add(Mg.El("p", null, "상자 더미로 옮겨 붙었다. 이제는 작은 불이 아니다."));
                else body.Add(Mg.El("p", null, "연기와 열기가 덮쳐 온다. 소화기 한 대로는 감당할 수 없는 불이다."));
                var ok = Mg.Btn("mg-btn mg-primary", Finish, kind == "out" ? "확인" : kind == "spread" ? "계속" : "그래도 버틴다");
                body.Add(ok);
                Mg.Replace(actions, Mg.El("div", $"mg-end is-{kind}", body));
                helpBox.Clear();
                Mg.Hidden(retreatBtn, kind == "out" || practice);
                MiniDom.Announce(head);
                ok.Focus();
            }

            bool OnKey(KeyDownEvent e)
            {
                var k = Mg.Key(e);
                if (k == "Escape") { if (practice || S.endKind == "out") Finish(); else Retreat(); return true; }
                if (S.phase == "act" && !S.ended && S.done.Contains("squeeze") && (k == "ArrowLeft" || k == "ArrowRight")) { Stroke(k == "ArrowLeft" ? -1 : 1); return true; }
                return false;
            }

            // ponytail: the 640x360 scene is redrawn on the CPU (Paint) every frame; drop to every other frame if it costs too much.
            Mg.Loop((dt, now) =>
            {
                if (S.closed) return false;
                if (S.phase == "act" && !S.ended)
                {
                    S.danger = Mathf.Min(1, S.danger + growPerS * dt);
                    S.fireLv = Mathf.Min(1, S.fireLv + growPerS * dt * 0.4f);
                    if (S.holding)
                    {
                        S.hold += dt; if (fills.TryGetValue("squeeze", out var f)) f.style.width = Length.Percent(Mathf.Min(1, S.hold / ExtinguisherScene.HOLD_S) * 100);
                        if (S.hold >= ExtinguisherScene.HOLD_S) Squeezed();
                    }
                    if (S.danger >= 1) End(large ? "overwhelmed" : "spread");
                }
                S.sweepDir *= Mathf.Pow(0.02f, dt); S.shake = Mathf.Max(0, S.shake - dt);
                dangerBar.style.width = Length.Percent(S.danger * 100);
                MinigameStyle.BarState(dangerBar, S.danger > 0.75f ? "over" : S.danger > 0.45f ? "warn" : "ok");
                var word = S.danger > 0.75f ? "곧 번짐" : S.danger > 0.45f ? "서둘러야 함" : "여유 있음";
                if (word != dangerText.text) { if (!string.IsNullOrEmpty(dangerText.text) && !S.ended) MiniDom.Announce($"불이 번지기까지: {word}"); dangerText.text = word; }
                ExtinguisherScene.Draw(cv.g, now, dt, S, rm, large);
                cv.Upload();
                cv.el.tooltip = $"불길 {(S.fireLv > 0.66f ? "큼" : S.fireLv > 0.25f ? "중간" : S.fireLv > 0 ? "작음" : "꺼짐")} · 안전핀 {(S.pinOut ? "뽑음" : "꽂혀 있음")} · 노즐 {(S.aimed ? "불 쪽" : "내려 둠")}{(S.spraying ? " · 분사 중" : "")}";
                return !S.closed;
            });

            if (S.phase == "decide") RenderDecide(); else RenderAct();
            return m.Done;
        }
    }
}
