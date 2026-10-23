// URP backend for Engine.Post (engine/post.js): a global Volume with Tonemapping (ACES), ColorAdjustments
// (exposure, contrast, saturation, tint), Bloom, Vignette, FilmGrain, SMAA on the camera; fades via the
// overlay. Registered before the first scene loads. Not compiled by Tools~/check.sh: keep it small and plain.
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

        public void Init(Camera camera, string quality) => throw new NotImplementedException("port: engine/post.js -> URP Volume + camera data");
        public void Apply(Grade grade, float bloom, float bloomRadius, float bloomThreshold, float fade) => throw new NotImplementedException("port: engine/post.js grade/bloom -> Volume overrides");
        public void SetQuality(string quality) => throw new NotImplementedException("port: engine/post.js setQuality -> URP quality");
    }
}
