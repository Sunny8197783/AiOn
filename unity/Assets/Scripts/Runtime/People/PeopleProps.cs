// Ports engine/people-props.js: hand-held and carried props (phone, baton, flashlight, water jug, basket,
// cart, walker) and the bags baked into the body (backpack, crossbody).
using System;
using Gyeongbo.Engine;

namespace Gyeongbo.People
{
    public static class PeopleProps
    {
        public static readonly float[][] WALKER_WHEELS = { new[] { -0.27f, 0.1f, -0.86f }, new[] { 0.27f, 0.1f, -0.86f }, new[] { -0.27f, 0.1f, -0.3f }, new[] { 0.27f, 0.1f, -0.3f } };
        public const float WALKER_R = 0.1f;

        public static GeoData PhoneGeo(float s, float k) => throw new NotImplementedException("port: engine/people-props.js:phoneGeo");
        public static GeoData BatonGeo(float s, float k) => throw new NotImplementedException("port: engine/people-props.js:batonGeo");
        public static GeoData FlashlightGeo(float s, float k) => throw new NotImplementedException("port: engine/people-props.js:flashlightGeo");
        public static GeoData WaterJugGeo() => throw new NotImplementedException("port: engine/people-props.js:waterJugGeo");
        public static GeoData BasketGeo(string c = "#c22f38") => throw new NotImplementedException("port: engine/people-props.js:basketGeo");
        public static GeoData CartGeo() => throw new NotImplementedException("port: engine/people-props.js:cartGeo");
        public static GeoData WalkerGeo(float hy) => throw new NotImplementedException("port: engine/people-props.js:walkerGeo");
        // Baked into the body builder before build().
        public static void Backpack(MeshBuilder b, BodyBuild P, string color) => throw new NotImplementedException("port: engine/people-props.js:backpack");
        public static void Crossbody(MeshBuilder b, BodyBuild P, string color) => throw new NotImplementedException("port: engine/people-props.js:crossbody");
    }
}
