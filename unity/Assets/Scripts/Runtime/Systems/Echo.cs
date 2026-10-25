// Ports story/echo.js: 전 회차의 나 — the path you walked comes back as a pale ghost, where you fell is marked,
// chalk notes (B) stay on the ground. Kept with Core.Store (key gyeongbo.echo.v1), last 3 runs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static Gyeongbo.Systems.SysUtil;

namespace Gyeongbo.Systems
{
    public sealed class EchoRun
    {
        public List<EchoTrailPoint> trail = new List<EchoTrailPoint>();
        public List<EchoMark> deaths = new List<EchoMark>();
        public List<EchoMark> notes = new List<EchoMark>();
    }

    // trail entries are [levelId, x, z] in JS.
    public sealed class EchoTrailPoint
    {
        public string lv;
        public float x, z;
    }

    // deaths { lv, x, z, why, yaw } and notes { lv, x, z, text, yaw }.
    public sealed class EchoMark
    {
        public string lv, why, text;
        public float x, z, yaw;
    }

    public sealed class EchoData
    {
        public List<EchoRun> runs = new List<EchoRun>();
    }

    public sealed class Echo
    {
        const string KEY = "gyeongbo.echo.v1";
        const int RUNS = 3, MAX_TRAIL = 1500, MAX_NOTES = 30;
        static readonly string[] PRESETS = { "여기 위험해", "이 길 막혔음", "물 있음", "숨을 곳 있음", "여기서 맞았다. 엎드려" };
        static readonly Dictionary<string, string> WHY = new Dictionary<string, string>
        {
            { "fall", "추락" }, { "fight", "몸싸움" }, { "blast", "포격" }, { "debris", "붕괴" }, { "fire", "화재" }, { "hurt", "부상" },
        };

        static bool Worth(EchoRun r) => r.trail.Count > 4 || r.notes.Count > 0 || r.deaths.Count > 0;   // a run that left a mark

        // ---- readEcho: saved runs come from storage, keep only well-formed entries ----
        static bool Num(JToken t, out float v)
        {
            v = 0;
            if (t == null || (t.Type != JTokenType.Integer && t.Type != JTokenType.Float)) return false;
            double d = t.Value<double>();
            if (double.IsNaN(d) || double.IsInfinity(d)) return false;
            v = (float)d; return true;
        }
        static JArray Arr(JToken o, string key) => o is JObject j ? j[key] as JArray : null;
        static List<T> Last<T>(List<T> l, int n) => l.Count > n ? l.GetRange(l.Count - n, n) : l;
        static string JsString(JToken t)   // String(why ?? '')
        {
            if (t == null || t.Type == JTokenType.Null || t.Type == JTokenType.Undefined) return "";
            if (t.Type == JTokenType.Boolean) return t.Value<bool>() ? "true" : "false";
            if (t is JValue v) return Convert.ToString(v.Value, System.Globalization.CultureInfo.InvariantCulture);
            return t.Type == JTokenType.Array ? string.Join(",", t.Select(JsString)) : "[object Object]";
        }

        public static EchoData ReadEcho(string raw)
        {
            JToken o;
            try
            {
                if (raw == null) return new EchoData();
                using (var r = new JsonTextReader(new System.IO.StringReader(raw)) { DateParseHandling = DateParseHandling.None })
                    o = JToken.ReadFrom(r);
            }
            catch { return new EchoData(); }
            var runsArr = Arr(o, "runs");
            var runs = runsArr == null ? new List<JToken>() : Last(runsArr.ToList(), RUNS);
            var data = new EchoData();
            foreach (var r in runs)
            {
                var run = new EchoRun();
                foreach (var p in Arr(r, "trail") ?? new JArray())
                    if (p is JArray a && a.Count > 0 && a[0].Type == JTokenType.String && Num(a.Count > 1 ? a[1] : null, out var x) && Num(a.Count > 2 ? a[2] : null, out var z))
                        run.trail.Add(new EchoTrailPoint { lv = (string)a[0], x = x, z = z });
                run.trail = Last(run.trail, MAX_TRAIL);
                foreach (var d in Arr(r, "deaths") ?? new JArray())
                    if (d is JObject j && j["lv"]?.Type == JTokenType.String && Num(j["x"], out var x) && Num(j["z"], out var z))
                        run.deaths.Add(new EchoMark { lv = (string)j["lv"], x = x, z = z, why = JsString(j["why"]), yaw = Num(j["yaw"], out var y) ? y : 0 });
                run.deaths = Last(run.deaths, 10);
                foreach (var n in Arr(r, "notes") ?? new JArray())
                    if (n is JObject j && j["lv"]?.Type == JTokenType.String && Num(j["x"], out var x) && Num(j["z"], out var z) && j["text"]?.Type == JTokenType.String)
                    {
                        string text = (string)j["text"];
                        run.notes.Add(new EchoMark { lv = (string)j["lv"], x = x, z = z, text = text.Length > 24 ? text.Substring(0, 24) : text, yaw = Num(j["yaw"], out var y) ? y : 0 });
                    }
                run.notes = Last(run.notes, MAX_NOTES);
                if (Worth(run)) data.runs.Add(run);
            }
            return data;
        }

        static string WriteEcho(IEnumerable<EchoRun> runs) => new JObject
        {
            ["runs"] = new JArray(runs.Select(r => new JObject
            {
                ["trail"] = new JArray(r.trail.Select(p => new JArray(p.lv, (double)p.x, (double)p.z))),
                ["deaths"] = new JArray(r.deaths.Select(d => new JObject { ["lv"] = d.lv, ["x"] = (double)d.x, ["z"] = (double)d.z, ["why"] = d.why, ["yaw"] = (double)d.yaw })),
                ["notes"] = new JArray(r.notes.Select(n => new JObject { ["lv"] = n.lv, ["x"] = (double)n.x, ["z"] = (double)n.z, ["text"] = n.text, ["yaw"] = (double)n.yaw })),
            })),
        }.ToString(Formatting.None);

        // A word written on the ground: a 512x128 canvas on a flat plane w x w/4.
        static GameObject TextPlane(string text, string color, float w = 3)
        {
            var c = new Paint(512, 128);
            c.font = "bold 64px sans-serif"; c.textAlign = "center"; c.textBaseline = "middle";
            c.fillStyle = color; c.globalAlpha = 0.9f; c.fillText(text, 256, 64);
            for (int i = 0; i < 400; i++) c.clearRect(Rand() * 512, Rand() * 128, 2, 2);   // chalky gaps
            var geo = Geo.Plane(w, w / 4).RotateX(-Mathf.PI / 2);
            // the X mirror would show the word backwards from above; flip u so it reads as the JS camera saw it
            for (int i = 0; i < geo.uv.Count; i++) geo.uv[i] = new Vector2(1 - geo.uv[i].x, geo.uv[i].y);
            var m = Geo.Obj(geo, Mat.Unlit(new MatSpec { map = c.ToTexture(true, true), transparent = true, depthWrite = false, polygonOffset = true, polygonOffsetFactor = -3 }), "echo-text", false, false);
            var rd = m.GetComponent<Renderer>(); if (rd != null) rd.sortingOrder = 5;
            return m;
        }

        readonly GameEngine engine;
        readonly Ctx ctx;
        EchoData data;
        EchoRun run;                       // the run being recorded
        Level shownL; GameObject shownGroup; bool shownTold; bool shownSet;
        IPerson ghost; int ghostI; List<XZ> ghostPts = new List<XZ>();
        float lastSave;

        Echo(GameEngine engine, Ctx ctx)
        {
            this.engine = engine; this.ctx = ctx;
            data = ReadEcho(Store.Get(KEY));
            Application.quitting += Save;  // pagehide
            engine.OnFrame(Frame);
        }

        public static Echo Create(GameEngine engine, Ctx ctx) => new Echo(engine, ctx);

        List<EchoRun> Past => data.runs;   // earlier runs only

        void Save()
        {
            var all = new List<EchoRun>(data.runs);
            if (run != null && Worth(run)) all.Add(run);
            Store.Set(KEY, WriteEcho(Last(all, RUNS)));
        }

        static float Y(Level L, float x, float z) => Geom.GroundAt(L.walk, x, z, 0, true)?.y ?? 0;

        void Show(Level L)   // build the marks of earlier runs for this level
        {
            if (shownGroup != null) UnityEngine.Object.Destroy(shownGroup);
            shownL = L; shownSet = true; shownTold = false;
            var group = shownGroup = J.Group("echo-marks");
            bool any = false;
            for (int k = 0; k < Past.Count; k++)
            {
                var r = Past[k];
                int age = Past.Count - k;   // 1 = the last run
                foreach (var d in r.deaths.Where(d => d.lv == L.id))
                {
                    var m = TextPlane("✕", "#e0302a", 1.6f); J.SetPos(m, d.x, Y(L, d.x, d.z) + 0.08f, d.z); group.Add(m);
                    // the label on the near side, read facing the way you were going
                    var t = TextPlane(age + "회 전의 내가 쓰러진 곳 · " + (d.why != null && WHY.TryGetValue(d.why, out var w) ? w : ""), "#ff8a7a", 3);
                    J.SetPos(t, d.x + Mathf.Sin(d.yaw) * 0.9f, Y(L, d.x, d.z) + 0.08f, d.z + Mathf.Cos(d.yaw) * 0.9f); J.SetYaw(t, d.yaw); group.Add(t); any = true;
                }
                foreach (var n in r.notes.Where(n => n.lv == L.id))
                {
                    var t = TextPlane(n.text, "#f4f1e8"); J.SetPos(t, n.x, Y(L, n.x, n.z) + 0.08f, n.z); J.SetYaw(t, n.yaw + Rand() * 0.3f - 0.15f); group.Add(t); any = true;
                }
            }
            // the last run's walk through this level, for the ghost
            var last = Past.Count > 0 ? Past[Past.Count - 1] : null;
            ghostPts = last != null ? last.trail.Where(p => p.lv == L.id).Select(p => new XZ(p.x, p.z)).ToList() : new List<XZ>();
            ghostI = 0;
            if (ghost != null && ghost.Group == null) ghost = null;   // destroyed with an unloaded level
            if (ghostPts.Count > 4)
            {
                if (ghost == null) ghost = MakeGhost();
                if (ghost != null) { L.group.Add(ghost.Group); ghost.Group.SetActive(true); J.SetPos(ghost.Group, ghostPts[0].x, Y(L, ghostPts[0].x, ghostPts[0].z), ghostPts[0].z); any = true; }
            }
            else if (ghost != null) { J.Remove(ghost.Group); ghost.Group.SetActive(false); }   // a detached object still renders in Unity
            L.group.Add(group);
            if (any && !shownTold) { shownTold = true; ctx.Toast("이전 회차의 내가 남긴 흔적이 보인다. 그때의 나는 이 길로 걸었다.", "self", 5200); }
        }

        IPerson MakeGhost()
        {
            try
            {
                var p = engine.People.CreatePerson("jeongmin", 1);
                var tint = J.Hex(0x6fa8ff);
                foreach (var r in p.Group.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.materials;   // instanced copies (m.clone())
                    // ponytail: relies on Mat.SetOpacity switching an opaque URP material to transparent; a ghost shader in the quality pass
                    foreach (var c in mats) { Mat.SetOpacity(c, 0.3f); c.SetInt("_ZWrite", 0); Mat.SetEmissive(c, tint, 0.6f); }
                    r.materials = mats;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                p.SetAnim("walk"); p.Hostile = false;
                engine.Persons.Remove(p);   // not something to punch
                return p;
            }
            catch (Exception err) { Debug.LogError("[echo] ghost model failed " + err); return null; }
        }

        void Frame(float dt, float t)
        {
            var L = engine.Level; var G = ctx.G;
            if (L != null && (!shownSet || shownL != L)) Show(L);
            if (ghost != null && ghostPts.Count > 4 && L != null)   // walk the old path at an easy pace, then start over
            {
                var g = J.Pos(ghost.Group); var to = ghostPts[ghostI];
                float d = Hypot(to.x - g.x, to.z - g.z);
                if (d < 0.3f) ghostI = (ghostI + 1) % ghostPts.Count;
                else { float k = Mathf.Min(1, (1.5f * dt) / d); g.x += (to.x - g.x) * k; g.z += (to.z - g.z) * k; J.SetYaw(ghost.Group, Mathf.Atan2(-(to.x - g.x), -(to.z - g.z))); }
                if (ghostI == 0 && d > 6) { g.x = to.x; g.z = to.z; }
                g.y = Geom.GroundAt(L.walk, g.x, g.z, g.y + 0.3f, false, 2)?.y ?? g.y;
                J.SetPos(ghost.Group, g);
                ghost.SetSpeed(1.5f); ghost.Update(dt);
            }
            if (run == null || G == null || !ctx.roaming || L == null) return;
            var P = engine.Player; var lastP = run.trail.Count > 0 ? run.trail[run.trail.Count - 1] : null;
            if (lastP == null || lastP.lv != L.id || Hypot(lastP.x - P.X, lastP.z - P.Z) > 1.5f)
            {
                run.trail.Add(new EchoTrailPoint { lv = L.id, x = JsRound(P.X * 10) / 10, z = JsRound(P.Z * 10) / 10 });
                if (run.trail.Count > MAX_TRAIL) run.trail.RemoveAt(0);
            }
            if (t - lastSave > 10) { lastSave = t; Save(); }
        }

        // A new game: the run being recorded becomes an echo.
        public void Begin()
        {
            if (run != null && Worth(run)) { data.runs.Add(run); data.runs = Last(data.runs, RUNS); }
            run = new EchoRun(); Save(); shownSet = false; shownL = null;
        }

        // Continuing a save keeps recording into the same run.
        public void Resume() { if (run == null) run = new EchoRun(); }

        public void Death(string why)
        {
            var L = engine.Level; if (run == null || L == null) return;
            run.deaths.Add(new EchoMark { lv = L.id, x = engine.Player.X, z = engine.Player.Z, why = why, yaw = engine.Player.Yaw }); Save();
        }

        // B while roaming: write on the ground (asks through ctx.Say).
        public async Task Chalk()
        {
            var L = engine.Level; if (run == null || L == null) return;
            var choices = PRESETS.Select((t, i) => new Choice(i.ToString(), t)).ToList();
            choices.Add(new Choice("own", "직접 쓰기")); choices.Add(new Choice("no", "그만"));
            string pick = await ctx.Say("분필", new[] { "바닥에 뭐라고 적을까? 다음 회차의 내가 보게 된다." }, choices);
            string text = int.TryParse(pick, out var idx) && idx >= 0 && idx < PRESETS.Length && idx.ToString() == pick ? PRESETS[idx] : null;
            if (pick == "own") { text = ((await Prompt("바닥에 적을 말 (24자까지)")) ?? "").Trim(); if (text.Length > 24) text = text.Substring(0, 24); }
            if (string.IsNullOrEmpty(text) || pick == "no") return;
            var P = engine.Player;
            // just ahead of you (the JS caps notes and saves only in a comment here; the periodic save keeps them)
            run.notes.Add(new EchoMark { lv = L.id, x = P.X + Mathf.Sin(P.Yaw) * -1.2f, z = P.Z + Mathf.Cos(P.Yaw) * -1.2f, text = text, yaw = P.Yaw });
            var n = run.notes[run.notes.Count - 1]; var m = TextPlane(text, "#f4f1e8");
            J.SetPos(m, n.x, P.Y + 0.08f, n.z); J.SetYaw(m, n.yaw); L.group.Add(m);
            ctx.Toast("바닥에 분필로 적었다. 다음 회차의 내가 이걸 보게 된다.", "self");
        }

        // window.prompt stand-in: a text field on the overlay; Enter confirms, Esc cancels (null).
        // ponytail: plain inline-styled box; restyle with UiStyle in the UI pass
        static Task<string> Prompt(string label)
        {
            var tcs = new TaskCompletionSource<string>();
            var box = new VisualElement();
            box.style.position = Position.Absolute; box.style.left = Length.Percent(50); box.style.top = Length.Percent(40);
            box.style.translate = new Translate(Length.Percent(-50), 0);
            box.style.backgroundColor = new Color(0.08f, 0.08f, 0.09f, 0.95f); box.style.paddingLeft = box.style.paddingRight = 16;
            box.style.paddingTop = box.style.paddingBottom = 12; box.style.minWidth = 320;
            var lab = new Label(label); lab.style.color = Color.white; box.Add(lab);
            var field = new TextField { maxLength = 24 }; box.Add(field);
            void Done(string v) { box.RemoveFromHierarchy(); tcs.TrySetResult(v); }
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Done(field.value);
                else if (e.keyCode == KeyCode.Escape) Done(null);
            }, TrickleDown.TrickleDown);
            try { UiRoot.Overlay.Add(box); field.Focus(); }
            catch (Exception e) { Debug.LogError("[echo] prompt failed " + e); tcs.TrySetResult(null); }
            return tcs.Task;
        }
    }
}
