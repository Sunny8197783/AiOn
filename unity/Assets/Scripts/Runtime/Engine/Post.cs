// Ports engine/post.js: colour grade, bloom, ambient occlusion, eye adaptation and fades. Runtime has no URP
// types: the URP assembly registers a backend (Post.Backend = new UrpPost()) from BeforeSceneLoad; without one
// the grade is a no-op and fades use a plain overlay.
using System;
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

        public static Post Create(Camera camera, string quality = "high", bool reduceMotion = false) => throw new NotImplementedException("port: engine/post.js:createPost");
        public Grade Grade => throw new NotImplementedException("port: engine/post.js:grade");       // a copy of the grade being eased toward
        public void SetGrade(Grade g, float seconds = 1) => throw new NotImplementedException("port: engine/post.js:setGrade");
        // 0 = black, 1 = clear.
        public Task FadeTo(float v, float seconds) => throw new NotImplementedException("port: engine/post.js:fadeTo");
        public void SetBloom(float strength, float? radius = null, float? threshold = null) => throw new NotImplementedException("port: engine/post.js:setBloom");
        public void SetSize(int w, int h) => throw new NotImplementedException("port: engine/post.js:setSize");
        public void SetQuality(string q) => throw new NotImplementedException("port: engine/post.js:setQuality");
        public void SetReduceMotion(bool v) => throw new NotImplementedException("port: engine/post.js:setReduceMotion");
        public void SnapAdapt() => throw new NotImplementedException("port: engine/post.js:snapAdapt");
        public float AdaptValue() => throw new NotImplementedException("port: engine/post.js:adaptValue");
        public void Render(float dt) => throw new NotImplementedException("port: engine/post.js:render");
    }
}
