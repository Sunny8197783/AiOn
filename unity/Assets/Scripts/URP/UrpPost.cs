// URP backend for Engine.Post (engine/post.js): a global Volume with Tonemapping (ACES), ColorAdjustments
// (exposure, contrast, saturation, tint), SplitToning, Bloom, Vignette, FilmGrain, ChromaticAberration and camera
// MotionBlur, SMAA on the camera, the SSAO renderer feature on high; fades via the overlay (Engine.Post).
// Registered before the first scene loads. Not compiled by Tools~/check.sh: keep it small and plain.
// ponytail: maps the GLSL grade onto URP's stock overrides (contrast/saturation in percent, grain and split toning
// approximated, URP ACES instead of three.js ACESFilmic); the quality pass can port GradeShader as a fullscreen pass.
using System;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gyeongbo.URP
{
    public sealed class UrpPost : IPostBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => Post.Backend = new UrpPost();

        Camera camera;
        GameObject volumeGo;   // one per engine: a menu -> reload builds a new engine, so the old volume goes
        Tonemapping tone; ColorAdjustments color; SplitToning split; Bloom bloom; Vignette vignette; FilmGrain grain;
        ChromaticAberration chroma; MotionBlur blur;

        public void Init(Camera camera, string quality)
        {
            this.camera = camera;
            if (volumeGo != null) Object.Destroy(volumeGo);
            var go = volumeGo = new GameObject("post-volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            color = profile.Add<ColorAdjustments>(true);
            split = profile.Add<SplitToning>(true);
            bloom = profile.Add<Bloom>(true); bloom.clamp.Override(65472f);
            vignette = profile.Add<Vignette>(true); vignette.smoothness.Override(0.45f); vignette.rounded.Override(false);
            grain = profile.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Thin1); grain.response.Override(0.8f);
            chroma = profile.Add<ChromaticAberration>(true);
            blur = profile.Add<MotionBlur>(true); blur.mode.Override(MotionBlurMode.CameraOnly); blur.intensity.Override(0); blur.clamp.Override(0.04f);
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            SetQuality(quality);
        }

        static float Lerp(float a, float b, float k) => a + (b - a) * k;

        public void Apply(Grade g, float bloomStrength, float bloomRadius, float bloomThreshold, float fade)
        {
            if (color == null) return;
            // three.js: c * exp2(exposure); filmic contrast is a power around mid grey; saturation mixes with luma.
            color.postExposure.value = g.exposure ?? 0;
            color.contrast.value = Mathf.Clamp(((g.contrast ?? 1) - 1) * 100, -100, 100);
            color.saturation.value = Mathf.Clamp(((g.saturation ?? 1) - 1) * 100, -100, 100);
            var t = g.tint ?? new float[] { 1, 1, 1 }; float ta = g.tintAmount ?? 0;
            color.colorFilter.value = new Color(Lerp(1, t[0], ta), Lerp(1, t[1], ta), Lerp(1, t[2], ta)).gamma; // URP converts it back to linear
            // post.js split toning: cool shadows (0.9, 1, 1.08), warm highlights (1.06, 1, 0.92), by `split`.
            float s = g.split ?? 0.5f;
            split.shadows.value = new Color(0.5f - 0.1f * s * 2, 0.5f, 0.5f + 0.08f * s * 2);
            split.highlights.value = new Color(0.5f + 0.06f * s * 2, 0.5f, 0.5f - 0.08f * s * 2);
            vignette.intensity.value = Mathf.Clamp01(g.vignette ?? 0.2f);
            grain.intensity.value = Mathf.Clamp01((g.grain ?? 0.03f) * 6);
            bloom.intensity.value = Mathf.Max(0, bloomStrength);
            bloom.scatter.value = Mathf.Clamp01(0.45f + bloomRadius * 0.5f);
            bloom.threshold.value = Mathf.Pow(Mathf.Max(0, bloomThreshold), 1 / 2.2f);   // URP's threshold is gamma space
            var post = Post.Active;
            chroma.intensity.value = post != null && post.ReducedMotion ? 0 : 0.06f;
            blur.intensity.value = post != null && post.Blur ? 0.35f : 0;
        }

        public void SetQuality(string quality)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<ScreenSpaceAmbientOcclusion>()) f.SetActive(quality == "high");
            if (camera != null)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.antialiasing = quality != "low" ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
                data.antialiasingQuality = AntialiasingQuality.High;
            }
            if (bloom != null)
            {
                bloom.highQualityFiltering.value = quality == "high";
                bloom.downscale.Override(quality == "low" ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
            }
        }
    }
}
