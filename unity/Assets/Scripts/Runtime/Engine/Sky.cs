// Ports engine/sky.js: sky dome, night dome with city glow, drifting clouds, stars, moon, a distant city ring
// (light group 'windows') with blinking aviation lights, layered hills, the sun/moon light, hemisphere light
// and the reflection environment per preset. The meshes are built in SkyParts.cs.
// ponytail: three.js's physical Sky shader, the GLSL night dome and the fbm cloud shader are stand-ins here: a
// vertex-coloured gradient dome (sky + night dome in one, recoloured on Apply) and a baked noise texture for the
// clouds. Upgrade: port the three shaders to HLSL (Shader Graph or a URP unlit shader) in the quality pass.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gyeongbo.Engine
{
    public sealed partial class Sky
    {
        const float D2R = Mathf.PI / 180;
        // Direction from azimuth/elevation (degrees), JS space.
        public static Vector3 DirFrom(float az, float el) =>
            new Vector3(Mathf.Sin(az * D2R) * Mathf.Cos(el * D2R), Mathf.Sin(el * D2R), -Mathf.Cos(az * D2R) * Mathf.Cos(el * D2R));

        Kit kit; string quality;
        Light sun;
        GameObject group, world, skyMesh, clouds, stars, moon, beacons, hillsNear, hillsFar;
        Material skyMat, cloudMat, starMat, moonMat, hillsNearMat, hillsFarMat;
        GeoData skyGeo; Mesh skyUnity;
        Vector3 keyDir = Vector3.up, sunDir = Vector3.up;
        readonly Vector3 moonDir = DirFrom(Presets.MOON[0], Presets.MOON[1]);
        EnvState cur;
        float time;
        bool roomEnv;
        Camera cleared;

        public static Sky Create(GameObject scene, Kit kit, string quality = "high")
        {
            var s = new Sky { kit = kit, quality = quality };
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = J.Hex(0xc4d2df); RenderSettings.fogDensity = 0.002f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            s.sun = new GameObject("sun").AddComponent<Light>();
            s.sun.type = LightType.Directional; s.sun.color = Color.white; s.sun.intensity = Kit.ToUnity(3);
            s.sun.shadows = LightShadows.Soft; s.sun.shadowBias = 0.05f; s.sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = s.sun;
            scene.Add(s.sun.gameObject);
            // Camera-following sky shells.
            s.group = J.Group("sky");
            s.MakeSkyDome(); s.MakeClouds(); s.stars = s.MakeStars(); s.moon = s.MakeMoon();
            s.group.Add(s.skyMesh, s.stars, s.moon, s.clouds);
            // Fixed distant world: city ring, aviation beacons, near and far hills (광교산 rises to the north).
            s.world = J.Group("skyline");
            var city = s.MakeSkyline(out s.beacons);
            s.hillsNear = s.HillRing(760, 25, 110, 5, new[] { new[] { Mathf.PI * 1.5f, 0.35f, 0.9f }, new[] { 0.4f, 0.25f, 0.4f } }, out s.hillsNearMat);
            s.hillsFar = s.HillRing(1100, 50, 150, 11, new[] { new[] { Mathf.PI * 1.35f, 0.5f, 0.6f }, new[] { 2.6f, 0.4f, 0.5f } }, out s.hillsFarMat);
            s.world.Add(city, s.beacons, s.hillsNear, s.hillsFar);
            scene.Add(s.group, s.world);
            s.SetQuality(quality);
            return s;
        }

        public EnvState State => cur;
        public Light Sun => sun;

        static Color Lin(float[] c) => new Color(c[0], c[1], c[2], 1);
        static Color Srgb(float[] c) => Lin(c).gamma;
        static void SetActive(GameObject o, bool on) { if (o.activeSelf != on) o.SetActive(on); }

        public void Apply(EnvState S)
        {
            cur = S;
            keyDir = DirFrom(S["keyAz"], S["keyEl"]); sunDir = DirFrom(S["sunAz"], S["sunEl"]);
            sun.color = Srgb(S.c["keyColor"]); sun.intensity = S["keyI"] > 0.01f ? Kit.ToUnity(S["keyI"]) : 0;
            sun.shadows = sun.intensity > 0 ? LightShadows.Soft : LightShadows.None;
            sun.transform.rotation = Quaternion.LookRotation(J.V(-keyDir));
            var fog = Lin(S.c["fogColor"]);
            RenderSettings.fogColor = fog.gamma; RenderSettings.fogDensity = S["fogDensity"];
            // Hemisphere light (three.js divides by pi) plus the environment's diffuse share (envI * radiance).
            EnvColors(S, roomEnv, out var eUp, out var eHor, out var eDown);
            float hi = S["hemiI"] / Mathf.PI, ei = S["envI"];
            Color hs = Lin(S.c["hemiSky"]) * hi, hg = Lin(S.c["hemiGround"]) * hi;
            RenderSettings.ambientSkyColor = (hs + eUp * ei).gamma;
            RenderSettings.ambientEquatorColor = ((hs + hg) * 0.5f + eHor * ei).gamma;
            RenderSettings.ambientGroundColor = (hg + eDown * ei).gamma;
            RenderSettings.reflectionIntensity = Mathf.Clamp01(ei * 2);
            ColorSky(S);
            Mat.SetOpacity(starMat, S["stars"]); SetActive(stars, S["stars"] > 0.005f);
            Mat.SetOpacity(moonMat, S["moon"]); SetActive(moon, S["moon"] > 0.005f);
            SetClouds(S["cloudCover"], Lin(S.c["cloudLit"]), Lin(S.c["cloudShade"]));
            Mat.SetColor(cloudMat, Srgb(S.c["cloudLit"])); Mat.SetOpacity(cloudMat, S["cloudOpacity"]); SetActive(clouds, S["cloudOpacity"] > 0.01f);
            var hills = Lin(S.c["hills"]);
            Mat.SetColor(hillsNearMat, hills.gamma); Mat.SetColor(hillsFarMat, Color.Lerp(hills, fog, 0.45f).gamma);
            bool vis = S["skyVis"] > 0.5f; SetActive(group, vis); SetActive(world, vis);
            // ponytail: shadowBox (the JS shadow camera size) has no per-light setting in URP; the pipeline asset's
            // shadow distance (UrpSetup) covers it.
        }

        // Per frame: shells follow the camera, clouds drift, beacons blink. (URP's cascades follow the camera, so the
        // JS shadow box that follows the player snapped to shadow texels is not needed.)
        public void Update(float dt, Camera camera, Vector3 playerJs, bool reduceMotion)
        {
            if (camera != null)
            {
                group.transform.position = camera.transform.position;
                if (cleared != camera) { cleared = camera; camera.clearFlags = CameraClearFlags.SolidColor; }
                camera.backgroundColor = RenderSettings.fogColor;   // scene.background = fog colour
            }
            moon.transform.localPosition = J.V(moonDir * 1500);
            moon.transform.localRotation = Quaternion.LookRotation(J.V(moonDir));
            if (!reduceMotion) time += dt;
            Mat.SetTiling(cloudMat, new Vector2(1f / CLOUD_SPAN, 1f / CLOUD_SPAN), new Vector2(time * 0.012f / CLOUD_SPAN, time * 0.004f / CLOUD_SPAN));
            SetActive(beacons, kit.IsLit("windows") && (reduceMotion || time % 1.6f < 0.8f));
        }

        // Reflection environment per preset, cached; indoor presets share one room environment.
        // ponytail: a small gradient cubemap (zenith / horizon / ground of the preset) instead of a PMREM of the sky
        // scene or three.js RoomEnvironment. Upgrade: reflection probes baked per level.
        readonly Dictionary<string, Cubemap> cache = new Dictionary<string, Cubemap>();
        public void Environment(string preset, bool rain)
        {
            var p = Presets.PRESETS[preset];
            roomEnv = p.env == "room";
            string key = roomEnv ? "room" : $"{preset}:{(rain && p.outdoor ? 1 : 0)}";
            if (!cache.TryGetValue(key, out var cube))
            {
                EnvColors(Presets.EnvStateOf(preset, rain), roomEnv, out var up, out var hor, out var down);
                cache[key] = cube = GradientCube(up, hor, down);
            }
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
        }

        public void SetQuality(string q)
        {
            quality = q;
            sun.shadowResolution = q == "high" ? LightShadowResolution.High : q == "medium" ? LightShadowResolution.Medium : LightShadowResolution.Low;
            int oct = q == "high" ? 5 : 4;
            if (oct != cloudOct) { cloudOct = oct; cloudCover = -1; if (cur != null) SetClouds(cur["cloudCover"], Lin(cur.c["cloudLit"]), Lin(cur.c["cloudShade"])); }
        }
    }
}
