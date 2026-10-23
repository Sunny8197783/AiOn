// Ports engine/post.js: colour grade, bloom, ambient occlusion, eye adaptation and fades. Runtime has no URP
// types: the URP assembly registers a backend (Post.Backend = new UrpPost()) from BeforeSceneLoad; without one
// the grade is a no-op and fades use a plain overlay.
// Here: the tweened grade, the fade (a black overlay in front of the camera, before bloom and tone mapping, so
// it scales the HDR frame like the JS uFade), camera motion-blur detection and the hand-off to the backend.
// ponytail: no eye adaptation (it meters the rendered frame on the GPU; needs a URP renderer feature): AdaptValue()
// stays 1. Upgrade: a ScriptableRendererFeature writing the 64x64 log-luminance and 1x1 adapt targets.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Gyeongbo.Engine
{
    // presets.js grade. Partial grades (cine sets only exposure) leave the null fields as they are.
    public sealed class Grade
    {
        public float? saturation, contrast, exposure, vignette, grain, tintAmount, split;
        public float[] tint;
        public Grade Clone() => (Grade)MemberwiseClone();
    }

    public interface IPostBackend
    {
        void Init(Camera camera, string quality);
        void Apply(Grade grade, float bloom, float bloomRadius, float bloomThreshold, float fade);
        void SetQuality(string quality);
    }

    public sealed class Post
    {
        public static IPostBackend Backend;
        // The post chain of the running engine (the backend reads Blur / ReducedMotion from it).
        public static Post Active { get; private set; }

        // The full grade (post.js DEFAULT_GRADE keys; shadows/highlights are the split-toning tints).
        sealed class Full
        {
            public float saturation = 1, contrast = 1, exposure = 0, vignette = 0.2f, grain = 0.03f, tintAmount = 0, split = 0.5f;
            public float[] tint = { 1, 1, 1 }, shadows = { 0.9f, 1, 1.08f }, highlights = { 1.06f, 1, 0.92f };
            public Full Clone() { var o = (Full)MemberwiseClone(); o.tint = (float[])tint.Clone(); o.shadows = (float[])shadows.Clone(); o.highlights = (float[])highlights.Clone(); return o; }
        }
        static float Ease(float k) => k * k * (3 - 2 * k);

        Camera camera;
        IPostBackend backend;
        string quality;
        Full cur = new Full(), from, to;
        float k = 1, dur;
        float fade = 1, fadeTarget = 1, fadeRate;
        bool RM, blurOn;
        readonly List<TaskCompletionSource<bool>> waiters = new List<TaskCompletionSource<bool>>();
        float bloom = 0.3f, bloomRadius = 0.5f, bloomThreshold = 0.85f;
        internal float envExposure = 1;          // renderer.toneMappingExposure (env preset), multiplied before tone mapping
        Quaternion prevQ;
        GameObject overlay; Material overlayMat;

        public static Post Create(Camera camera, string quality = "high", bool reduceMotion = false)
        {
            var p = new Post { camera = camera, quality = quality, RM = reduceMotion };
            p.from = p.cur.Clone(); p.to = p.cur.Clone();
            p.prevQ = camera != null ? camera.transform.rotation : Quaternion.identity;
            if (Backend != null)
            {
                try { Backend.Init(camera, quality); p.backend = Backend; }
                catch (Exception e) { Debug.LogError("[engine/post] post backend failed; rendering without grade/bloom: " + e); }
            }
            p.MakeOverlay();
            p.SetQuality(quality);
            Active = p;
            return p;
        }

        // A copy of the grade being eased toward.
        public Grade Grade => new Grade { saturation = to.saturation, contrast = to.contrast, exposure = to.exposure, vignette = to.vignette, grain = to.grain, tintAmount = to.tintAmount, split = to.split, tint = (float[])to.tint.Clone() };

        public void SetGrade(Grade g, float seconds = 1)
        {
            g = g ?? new Grade();
            from = cur.Clone();
            var t = to.Clone();
            static float Pick(float? v, float fallback) => v.HasValue && !float.IsNaN(v.Value) && !float.IsInfinity(v.Value) ? v.Value : fallback;
            t.saturation = Pick(g.saturation, t.saturation); t.contrast = Pick(g.contrast, t.contrast); t.exposure = Pick(g.exposure, t.exposure);
            t.vignette = Pick(g.vignette, t.vignette); t.grain = Pick(g.grain, t.grain); t.tintAmount = Pick(g.tintAmount, t.tintAmount); t.split = Pick(g.split, t.split);
            if (g.tint != null) t.tint = g.tint.Length == 3 ? (float[])g.tint.Clone() : (float[])from.tint.Clone();
            to = t;
            dur = Mathf.Max(0, seconds); k = dur > 0 ? 0 : 1;
            if (dur <= 0) cur = to.Clone();
        }

        // Fade to/from black (0 = black, 1 = clear); resolves when done.
        public Task FadeTo(float v, float seconds)
        {
            fadeTarget = v;
            if (!(seconds > 0)) { fade = v; ApplyOverlay(); return Task.CompletedTask; }
            fadeRate = Mathf.Abs(v - fade) / seconds;
            var tcs = new TaskCompletionSource<bool>();
            waiters.Add(tcs);
            return tcs.Task;
        }

        public void SetBloom(float strength, float? radius = null, float? threshold = null)
        {
            bloom = strength; if (radius.HasValue) bloomRadius = radius.Value; if (threshold.HasValue) bloomThreshold = threshold.Value;
        }

        // The composer follows the screen in Unity; kept for the JS call sites.
        public void SetSize(int w, int h) { }

        public void SetQuality(string q)
        {
            quality = q;
            blurOn = q != "low";
            try { backend?.SetQuality(q); } catch (Exception e) { Debug.LogError("[engine/post] backend setQuality failed: " + e); }
        }

        public void SetReduceMotion(bool v) => RM = v;
        public bool ReducedMotion => RM;
        // Camera motion blur this frame (a turn faster than ~0.1 deg/frame at 60 fps, not a cut).
        public bool Blur { get; private set; }

        // Jump the eye adaptation straight to the current frame (after a fade or a level swap).
        public void SnapAdapt() { }
        // The current adapted exposure multiplier (see the ponytail note above).
        public float AdaptValue() => 1;

        void Update(float dt)
        {
            if (k < 1)
            {
                k = Mathf.Min(1, k + dt / dur); float e = Ease(k);
                cur.saturation = from.saturation + (to.saturation - from.saturation) * e; cur.contrast = from.contrast + (to.contrast - from.contrast) * e;
                cur.exposure = from.exposure + (to.exposure - from.exposure) * e; cur.vignette = from.vignette + (to.vignette - from.vignette) * e;
                cur.grain = from.grain + (to.grain - from.grain) * e; cur.tintAmount = from.tintAmount + (to.tintAmount - from.tintAmount) * e;
                cur.split = from.split + (to.split - from.split) * e;
                for (int i = 0; i < 3; i++)
                {
                    cur.tint[i] = from.tint[i] + (to.tint[i] - from.tint[i]) * e;
                    cur.shadows[i] = from.shadows[i] + (to.shadows[i] - from.shadows[i]) * e;
                    cur.highlights[i] = from.highlights[i] + (to.highlights[i] - from.highlights[i]) * e;
                }
            }
            if (fade != fadeTarget) { float s = fadeRate * dt; fade = Mathf.Abs(fadeTarget - fade) <= s ? fadeTarget : fade + Mathf.Sign(fadeTarget - fade) * s; }
            if (fade == fadeTarget && waiters.Count > 0) { var w = waiters.ToArray(); waiters.Clear(); foreach (var t in w) t.TrySetResult(true); }
        }

        // Camera motion blur from the camera's turn since the last frame, as if the shutter were open for ~1/60 s.
        void Motion(float dt)
        {
            if (camera == null) { Blur = false; return; }
            var q = camera.transform.rotation;
            var dq = Quaternion.Inverse(prevQ) * q;
            prevQ = q;
            float kk = dt > 0 ? Mathf.Min(1, (1f / 60) / dt) : 0, ang = 2 * Mathf.Acos(Mathf.Min(1, Mathf.Abs(dq.w)));
            Blur = blurOn && !RM && ang * kk > 0.002f && ang < 0.6f; // a cut is not a turn
        }

        public void Render(float dt)
        {
            Update(dt); Motion(dt);
            ApplyOverlay();
            if (backend == null) return;
            var g = new Grade
            {
                saturation = cur.saturation, contrast = cur.contrast, exposure = cur.exposure + Mathf.Log(Mathf.Max(envExposure, 1e-4f), 2),
                vignette = cur.vignette, grain = cur.grain * (RM ? 0.5f : 1), tintAmount = cur.tintAmount, split = cur.split, tint = (float[])cur.tint.Clone(),
            };
            try { backend.Apply(g, bloom, bloomRadius, bloomThreshold, fade); }
            catch (Exception e) { Debug.LogError("[engine/post] backend apply failed; post disabled: " + e); backend = null; }
        }

        // ---- the fade overlay: a black quad just past the near plane, drawn last ----
        void MakeOverlay()
        {
            if (camera == null) return;
            overlayMat = Mat.Unlit(new MatSpec { color = Color.black, transparent = true, depthTest = false, depthWrite = false, fog = false, side = "double" });
            overlayMat.renderQueue = 5000;
            overlay = Geo.Obj(Geo.Plane(2, 2), overlayMat, "fade", false, false);
            overlay.transform.SetParent(camera.transform, false);
            ApplyOverlay();
        }

        void ApplyOverlay()
        {
            if (overlay == null) return;
            bool on = fade < 0.999f;
            var r = overlay.GetComponent<Renderer>();
            if (r.enabled != on) r.enabled = on;
            if (!on) return;
            float d = Mathf.Max(0.01f, camera.nearClipPlane * 1.5f);
            overlay.transform.localPosition = new Vector3(0, 0, d);
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one * d * 8;
            Mat.SetOpacity(overlayMat, Mathf.Clamp01(1 - fade));
        }
    }
}
