// Ports story/cine.js: letterboxed camera moves over the live world, actors walking through them, timed events
// and captions; Space / Enter / Esc skips the rest. Positions and camera points are JS space.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.UI;
using UnityEngine;
using UnityEngine.UIElements;
using static Gyeongbo.Systems.SysUtil;

namespace Gyeongbo.Systems
{
    // cam: positions p and look-at targets t eased along a Catmull-Rom curve over the shot.
    public sealed class CamPath
    {
        public List<Vector3> p = new List<Vector3>(), t = new List<Vector3>();
        public float[] fov;                      // [from, to] or null
        public string ease = "inout";            // inout | linear | out | in
        public float hand = 0.04f;               // handheld sway in metres
    }

    public struct CamPose
    {
        public Vector3 p, t;
        public float? fov;
    }

    // [at, text, speaker?, ms?]
    public sealed class CaptionCue
    {
        public float at;
        public string text, speaker;
        public int? ms;
    }

    // [seconds, fn]
    public sealed class TimedCue
    {
        public float at;
        public Action fn;
    }

    // fill: a soft light kept on the subject; at is fixed or follows a function (null = off this frame).
    public sealed class ShotFill
    {
        public Vector3? at;
        public Func<Vector3?> follow;
        public float i = 6;
        public Color? color;
    }

    public sealed class Shot
    {
        public float dur;
        public CamPath cam;
        public Func<float, float, CamPose> camFn;   // (k 0..1, seconds) -> pose, instead of cam
        public List<CaptionCue> lines = new List<CaptionCue>();
        public List<TimedCue> at = new List<TimedCue>();
        public string env;                          // env preset at the start of the shot
        public float envSeconds;
        public float slow = 1;                      // world time scale
        public bool fade;
        public Func<Action> setup;                  // returns an optional cleanup
        public float exposure;                      // stops added to the grade
        public ShotFill fill;
    }

    public sealed class ActorOptions
    {
        public XZ? at;
        public List<XZ> path;
        public float speed = 1.3f, yaw, delay;
        public string anim = "idle";
        public List<string> props = new List<string>();
    }

    public sealed class Cine
    {
        static readonly string[] SKIP = { "Space", "Enter", "NumpadEnter", "Escape" };

        static float Ease(string name, float k)
        {
            switch (name)
            {
                case "linear": return k;
                case "out": return 1 - (1 - k) * (1 - k);
                case "in": return k * k;
                default: return k * k * (3 - 2 * k);   // inout (and anything unknown)
            }
        }

        sealed class Disposer : IDisposable
        {
            Action fn; public Disposer(Action fn) { this.fn = fn; }
            public void Dispose() { var f = fn; fn = null; f?.Invoke(); }
        }

        readonly Ctx ctx;
        readonly GameEngine engine;
        readonly Hud hud;
        readonly List<IDisposable> live = new List<IDisposable>();   // actors and effects to remove when the cutscene ends
        readonly Light fill; readonly float fillUnit;
        bool busy; float baseExp;
        VisualElement el, titleEl, barTop, barBottom, skipEl;
        int titleT, seed = 900;

        Cine(Ctx ctx)
        {
            this.ctx = ctx; engine = ctx.engine; hud = ctx.hud;
            // A fill light for dark shots (shot.fill), in the scene from the start.
            fill = PointLight(engine.Kit, J.Hex(0xdfe6ff), 14, 1.6f, out fillUnit);
            fill.name = "cine-fill";
            engine.Scene.Add(fill.gameObject); J.SetPos(fill.gameObject, 0, -500, 0);
        }

        public static Cine Create(Ctx ctx) => new Cine(ctx);

        public bool Busy => busy;

        // ---- the overlay: two black bars, the typed title, the skip hint (style.css .cine*) ----
        // ponytail: inline styles in vh-equivalent percentages; move to UiStyle rules in the UI pass
        void Overlay()
        {
            if (el != null) return;
            el = new VisualElement { name = "cine", pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute; el.style.left = el.style.right = el.style.top = el.style.bottom = 0;
            el.style.display = DisplayStyle.None;
            VisualElement Bar(bool top)
            {
                var b = new VisualElement { pickingMode = PickingMode.Ignore };
                b.style.position = Position.Absolute; b.style.left = b.style.right = 0; b.style.height = Length.Percent(12);
                if (top) b.style.top = 0; else b.style.bottom = 0;
                b.style.backgroundColor = Color.black;
                b.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(top ? 0 : 100));
                b.style.scale = new Scale(new Vector3(1, 0, 1));
                b.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") };
                b.style.transitionDuration = new List<TimeValue> { new TimeValue(engine.ReduceMotion ? 0 : 0.7f) };
                el.Add(b); return b;
            }
            barTop = Bar(true); barBottom = Bar(false);
            titleEl = new VisualElement { pickingMode = PickingMode.Ignore };
            titleEl.style.position = Position.Absolute; titleEl.style.left = Length.Percent(5); titleEl.style.top = Length.Percent(15);
            titleEl.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
            titleEl.style.transitionDuration = new List<TimeValue> { new TimeValue(1) };
            el.Add(titleEl);
            skipEl = new Label("Space · 건너뛰기") { pickingMode = PickingMode.Ignore };
            skipEl.style.position = Position.Absolute; skipEl.style.right = Length.Percent(3); skipEl.style.bottom = Length.Percent(7);
            skipEl.style.color = new Color(1, 1, 1, 0.55f); skipEl.style.fontSize = 13; skipEl.style.opacity = 0;
            skipEl.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
            skipEl.style.transitionDuration = new List<TimeValue> { new TimeValue(0.6f) };
            skipEl.style.transitionDelay = new List<TimeValue> { new TimeValue(0.8f) };
            el.Add(skipEl);
            try { UiRoot.Overlay.Add(el); } catch (Exception e) { Debug.LogError("[cine] overlay failed " + e); }
        }

        void SetOn(bool on)
        {
            var s = new Scale(new Vector3(1, on ? 1 : 0, 1));
            barTop.style.scale = s; barBottom.style.scale = s; skipEl.style.opacity = on ? 1 : 0;
        }

        // Typed-out title lines in the corner (place, time), the way a film opens on a location.
        public void Title(string[] lines)
        {
            Overlay();
            Cancel(titleT); titleEl.style.opacity = 1;
            titleEl.Clear(); if (lines == null || lines.Length == 0) return;
            titleT = After(6.5f, () => titleEl.style.opacity = 0);   // read, then out of the way
            for (int i = 0; i < lines.Length; i++)
            {
                var p = new Label("") { pickingMode = PickingMode.Ignore };
                p.style.color = (Color)(i > 0 ? new Color32(0xc9, 0xc4, 0xb6, 255) : new Color32(0xf2, 0xef, 0xe6, 255));
                p.style.fontSize = i > 0 ? 15 : 20; p.style.unityFontStyleAndWeight = i > 0 ? FontStyle.Normal : FontStyle.Bold;
                p.style.marginBottom = 5; p.style.minHeight = 24; p.style.letterSpacing = 1;
                titleEl.Add(p);
                var text = lines[i] ?? "";
                // [...text]: whole code points
                var chars = new List<string>();
                for (int j = 0; j < text.Length; j++) chars.Add(char.IsHighSurrogate(text[j]) && j + 1 < text.Length ? text.Substring(j++, 2) : text[j].ToString());
                for (int j = 0; j < chars.Count; j++) { var ch = chars[j]; After(0.4f + i * 0.7f + j * 0.045f, () => p.text += ch); }
            }
        }

        // three.js CatmullRomCurve3 (centripetal, open) getPoint(t).
        static Vector3 CatmullRom(IList<Vector3> pts, float t)
        {
            int l = pts.Count;
            float p = (l - 1) * t; int ip = Mathf.FloorToInt(p); float w = p - ip;
            if (w == 0 && ip == l - 1) { ip = l - 2; w = 1; }
            Vector3 p0 = ip > 0 ? pts[(ip - 1) % l] : 2 * pts[0] - pts[1];
            Vector3 p1 = pts[ip % l], p2 = pts[(ip + 1) % l];
            Vector3 p3 = ip + 2 < l ? pts[(ip + 2) % l] : 2 * pts[l - 1] - pts[l - 2];
            float dt0 = Mathf.Pow((p0 - p1).sqrMagnitude, 0.25f), dt1 = Mathf.Pow((p1 - p2).sqrMagnitude, 0.25f), dt2 = Mathf.Pow((p2 - p3).sqrMagnitude, 0.25f);
            if (dt1 < 1e-4f) dt1 = 1;
            if (dt0 < 1e-4f) dt0 = dt1;
            if (dt2 < 1e-4f) dt2 = dt1;
            float C(float x0, float x1, float x2, float x3)
            {
                float t1 = ((x1 - x0) / dt0 - (x2 - x0) / (dt0 + dt1) + (x2 - x1) / dt1) * dt1;
                float t2 = ((x2 - x1) / dt1 - (x3 - x1) / (dt1 + dt2) + (x3 - x2) / dt2) * dt1;
                float c2 = -3 * x1 + 3 * x2 - 2 * t1 - t2, c3 = 2 * x1 - 2 * x2 + t1 + t2;
                return x1 + t1 * w + c2 * w * w + c3 * w * w * w;
            }
            return new Vector3(C(p0.x, p1.x, p2.x, p3.x), C(p0.y, p1.y, p2.y, p3.y), C(p0.z, p1.z, p2.z, p3.z));
        }

        static CamPose Pose(Shot shot, float k, float s)
        {
            if (shot.camFn != null) return shot.camFn(k, s);
            var cam = shot.cam;
            float e = Ease(cam.ease, k);
            Vector3 At(List<Vector3> pts) => pts.Count == 1 ? pts[0] : pts.Count == 2 ? Vector3.LerpUnclamped(pts[0], pts[1], e) : CatmullRom(pts, e);
            return new CamPose { p = At(cam.p), t = At(cam.t), fov = cam.fov != null ? cam.fov[0] + (cam.fov[1] - cam.fov[0]) * e : (float?)null };
        }

        Task RunShot(Shot shot, Func<bool> skipped)
        {
            var tcs = new TaskCompletionSource<bool>();
            Action cleanup = shot.setup?.Invoke();
            if (shot.env != null) engine.SetEnv(shot.env, false, shot.envSeconds);
            engine.TimeScale = shot.slow;
            engine.Post.SetGrade(new Grade { exposure = baseExp + shot.exposure }, shot.exposure != 0 ? 0.4f : 0.2f);
            if (shot.fade) { _ = Forget(engine.Post.FadeTo(0, 0), "cine"); _ = Forget(engine.Post.FadeTo(1, 0.6f), "cine"); }
            var events = new List<TimedCue>(shot.at ?? new List<TimedCue>());
            foreach (var c in shot.lines ?? new List<CaptionCue>()) { var cc = c; events.Add(new TimedCue { at = cc.at, fn = () => _ = Forget(ctx.Caption(cc.text, cc.speaker, cc.ms), "cine") }); }
            events = events.OrderBy(e => e.at).ToList();   // stable, like Array.sort
            float hand = shot.cam != null ? shot.cam.hand : 0.04f; bool RM = engine.ReduceMotion;
            float s = 0; bool done = false;
            float t0 = Time.realtimeSinceStartup;
            Action off = null;
            void Finish()
            {
                if (done) return; done = true; off?.Invoke(); fill.intensity = 0;
                try { cleanup?.Invoke(); } catch (Exception err) { Debug.LogError("[cine] shot cleanup failed " + err); }
                tcs.TrySetResult(true);
            }
            engine.SetCameraOverride((camera, dt) =>
            {
                if (done) return;
                s = Time.realtimeSinceStartup - t0;   // real seconds, whatever the world's time scale or frame rate
                while (events.Count > 0 && events[0].at <= s) { var ev = events[0]; events.RemoveAt(0); try { ev.fn?.Invoke(); } catch (Exception err) { Debug.LogError("[cine] event failed " + err); } }
                var P = Pose(shot, Mathf.Min(1, s / shot.dur), s);
                var look = P.t;
                if (!RM && hand != 0) look += new Vector3(hand * (Mathf.Sin(s * 1.3f) + 0.5f * Mathf.Sin(s * 3.1f + 1)), hand * 0.7f * Mathf.Sin(s * 1.7f + 2), hand * Mathf.Sin(s * 0.9f + 4));
                camera.transform.position = J.V(P.p);
                camera.transform.LookAt(J.V(look));
                if (P.fov.HasValue && P.fov.Value != 0 && Mathf.Abs(camera.fieldOfView - P.fov.Value) > 0.01f) camera.fieldOfView = P.fov.Value;
                if (shot.fill != null)
                {
                    var f = shot.fill; var at = f.follow != null ? f.follow() : f.at;
                    if (at.HasValue) J.SetPos(fill.gameObject, at.Value);
                    fill.intensity = f.i * fillUnit; if (f.color.HasValue) fill.color = f.color.Value;
                }
                if (s >= shot.dur || skipped()) Finish();
            });
            off = engine.OnFrame((dt, t) =>   // ends a shot even when the camera override is not running
            {
                if (skipped()) Finish();
                else if (Time.realtimeSinceStartup - t0 > shot.dur + 2) { Debug.LogWarning($"[cine] shot overran (camera override lost?) after {s:F2} of {shot.dur} s"); Finish(); }
            });
            return tcs.Task;
        }

        // Play shots in order; resolves 'done', 'skipped' or 'busy'. Controls are off and the HUD is hidden meanwhile.
        public async Task<string> Play(IList<Shot> shots, string[] title = null)
        {
            if (busy) return "busy";
            busy = true;
            bool skipped = false;
            bool Skipped() { if (!skipped && SKIP.Any(GameInput.Down)) skipped = true; return skipped; }
            Overlay();
            engine.SetControls(false); hud.SetCinematic(true); el.style.display = DisplayStyle.Flex; After(0, () => { if (busy) SetOn(true); });
            engine.SetModelHidden(true);
            baseExp = engine.Post.Grade.exposure ?? 0;
            Title(title);
            try { foreach (var shot in shots) { if (Skipped()) break; await RunShot(shot, Skipped); } }
            finally
            {
                engine.SetCameraOverride(null); engine.TimeScale = 1; _ = Forget(engine.Post.FadeTo(1, 0), "cine"); engine.Post.SetGrade(new Grade { exposure = baseExp }, 0.6f);
                foreach (var o in live.ToList()) { try { o.Dispose(); } catch (Exception e) { Debug.LogException(e); } }
                live.Clear();
                SetOn(false); After(0.7f, () => { if (!busy) el.style.display = DisplayStyle.None; });
                Title(null); hud.SetCinematic(false); engine.SetModelHidden(false); engine.SetControls(true);
                busy = false;
            }
            return skipped ? "skipped" : "done";
        }

        // Someone in the shot: stands at `at` or walks `path` ([[x, z], ...]) at `speed`, in the current level.
        public IPerson Actor(string preset, ActorOptions o)
        {
            o = o ?? new ActorOptions();
            var L = engine.Level; var p = engine.People.CreatePerson(preset, seed++);   // crowd extras each look different
            engine.Persons.Remove(p);   // a cutscene extra, not someone to punch
            foreach (var n in o.props ?? new List<string>()) p.SetProp(n, true);
            float Y(float x, float z) => Geom.GroundAt(L.walk, x, z, 0.5f, true)?.y ?? 0;
            var path = o.path;
            XZ start = path != null ? path[0] : o.at ?? new XZ(0, 0);
            J.SetPos(p.Group, start.x, Y(start.x, start.z), start.z); J.SetYaw(p.Group, o.yaw);
            p.SetAnim(o.anim); L.group.Add(p.Group);
            int i = 1; float wait = o.delay, speed = o.speed;
            var off = engine.OnFrame((dt, t) =>
            {
                if (path != null && i < path.Count && (wait -= dt) <= 0)
                {
                    var g = J.Pos(p.Group); var to = path[i];
                    float dx = to.x - g.x, dz = to.z - g.z, d = Hypot(dx, dz);
                    if (d < 0.2f) { if (++i >= path.Count) { p.SetAnim("idle"); p.SetSpeed(0); } }
                    else
                    {
                        float st = Mathf.Min(d, speed * dt); g.x += (dx / d) * st; g.z += (dz / d) * st; g.y = Y(g.x, g.z);
                        J.SetPos(p.Group, g);
                        float ry = J.GetYaw(p.Group), aim = Mathf.Atan2(-dx, -dz);
                        J.SetYaw(p.Group, ry + Mathf.Atan2(Mathf.Sin(aim - ry), Mathf.Cos(aim - ry)) * Mathf.Min(1, dt * 8));
                        if (p.Anim != "walk") p.SetAnim("walk");
                        p.SetSpeed(speed);
                    }
                }
                p.Update(dt);
            });
            live.Add(new Disposer(() => { off(); p.Dispose(); if (p.Group != null) { J.Remove(p.Group); UnityEngine.Object.Destroy(p.Group); } }));
            return p;
        }

        // A missile coming down: a white-hot head with a long smoke-and-fire trail, from `from` to `to` in `seconds`.
        public IDisposable Missile(Vector3 from, Vector3 to, float seconds = 1)
        {
            Vector3 A = from, B = to, dir = (B - A).normalized; var L = engine.Level;
            var g = J.Group("cine-missile");
            var trailGeo = Geo.Cylinder(0.25f, 0.9f, 40, 8, 1, true).Translate(0, -20, 0).RotateX(-Mathf.PI / 2);
            // ponytail: HDR colours (2.2, 1.4, 0.8) / (8, 6, 4) need an HDR camera + bloom to glow like the JS
            var trail = Geo.Obj(trailGeo, Mat.Unlit(new MatSpec { color = new Color(2.2f, 1.4f, 0.8f), transparent = true, opacity = 0.55f, blending = "additive", depthWrite = false, fog = false }), "trail", false, false);
            g.Add(trail);
            var head = Geo.Obj(Geo.Sphere(0.6f, 10, 8), Mat.Unlit(new MatSpec { color = new Color(8, 6, 4), fog = false }), "head", false, false);
            g.Add(head);
            L.group.Add(g); J.SetPos(g, A);
            g.transform.LookAt(g.transform.parent != null ? g.transform.parent.TransformPoint(J.V(A - dir)) : J.V(A - dir));   // +z (the trail) points back up the flight path
            float t = 0;
            var off = engine.OnFrame((dt, _) => { t += dt; float k = Mathf.Min(1, t / seconds); J.SetPos(g, Vector3.LerpUnclamped(A, B, k * k)); g.SetActive(k < 1); });
            var o = new Disposer(() => { off(); J.Remove(g); UnityEngine.Object.Destroy(g); });
            live.Add(o);
            return o;
        }
    }
}
