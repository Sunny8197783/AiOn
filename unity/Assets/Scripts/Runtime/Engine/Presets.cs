// Ports engine/presets.js: environment presets per time of day (sun, sky, fog, exposure, bloom, clouds, grade,
// light groups) and the flat tweenable state the engine blends between them.
// Preset names (used as strings everywhere): day afternoon dusk night blackout interior interiorNight interiorDark underground.
// Angles are degrees: azimuth from north (-z) clockwise toward east (+x), elevation above the horizon.
// lit: light group -> scale (0 = off). Groups not listed are left as they are.
// bloom: [strength, radius, threshold] (threshold in linear HDR before tone mapping; lower at night so lamps glow).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed class EnvPreset
    {
        public bool outdoor, noSky;
        public string env;                       // 'sky' | 'room'
        public float shadowBox;
        public float[] sun, key;                 // [azimuth, elevation] degrees, azimuth from north (-z) clockwise to east
        public string keyColor;
        public float keyI, envI, exposure;
        public string hemiSky, hemiGround;
        public float hemiI;
        public string fogColor;
        public float fogDensity;
        public float[] bloom;                    // [strength, radius, threshold]
        public Dictionary<string, float> sky = new Dictionary<string, float>();      // turbidity rayleigh mie mieG gain
        public Dictionary<string, object> night = new Dictionary<string, object>();  // amount zenith horizon glow glowColor stars moon
        public Dictionary<string, object> clouds = new Dictionary<string, object>(); // cover opacity lit shade gain
        public string hills;
        public Grade grade;
        public Dictionary<string, float> lit = new Dictionary<string, float>();      // light group -> scale
    }

    // envState(): numbers and linear-RGB arrays, mixed linearly.
    public sealed class EnvState
    {
        public Dictionary<string, float> n = new Dictionary<string, float>();
        public Dictionary<string, float[]> c = new Dictionary<string, float[]>();
        public float this[string key] { get => n.TryGetValue(key, out var v) ? v : 0; set => n[key] = value; }
        public EnvState Clone()
        {
            var o = new EnvState { n = new Dictionary<string, float>(n) };
            foreach (var kv in c) o.c[kv.Key] = (float[])kv.Value.Clone();
            return o;
        }
    }

    public static class Presets
    {
        public static readonly float[] MOON = { 140, 34 };

        static Dictionary<string, float> Sk(float turbidity, float rayleigh, float mie, float mieG, float gain) =>
            new Dictionary<string, float> { ["turbidity"] = turbidity, ["rayleigh"] = rayleigh, ["mie"] = mie, ["mieG"] = mieG, ["gain"] = gain };
        static Dictionary<string, object> Night(float amount, string zenith, string horizon, float glow, string glowColor, float stars, float moon) =>
            new Dictionary<string, object> { ["amount"] = amount, ["zenith"] = zenith, ["horizon"] = horizon, ["glow"] = glow, ["glowColor"] = glowColor, ["stars"] = stars, ["moon"] = moon };
        static Dictionary<string, object> Clouds(float cover, float opacity, string lit, string shade, float gain) =>
            new Dictionary<string, object> { ["cover"] = cover, ["opacity"] = opacity, ["lit"] = lit, ["shade"] = shade, ["gain"] = gain };
        static Dictionary<string, float> Lit(params object[] kv)
        {
            var d = new Dictionary<string, float>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = Convert.ToSingle(kv[i + 1]);
            return d;
        }
        static Grade Gr(float saturation, float contrast, float exposure, float vignette, float grain, float[] tint, float tintAmount, float? split = null) =>
            new Grade { saturation = saturation, contrast = contrast, exposure = exposure, vignette = vignette, grain = grain, tint = tint, tintAmount = tintAmount, split = split };
        static Dictionary<string, object> NIGHT_OFF => Night(0, "#0a1430", "#2a3550", 0, "#ff9a55", 0, 0);

        public static readonly Dictionary<string, EnvPreset> PRESETS = new Dictionary<string, EnvPreset>
        {
            ["day"] = new EnvPreset
            { // clear autumn 13:00
                outdoor = true, env = "sky", shadowBox = 70,
                sun = new float[] { 195, 50 }, key = new float[] { 195, 50 }, keyColor = "#fff2df", keyI = 4.6f,
                hemiSky = "#c9d6ea", hemiGround = "#6e6250", hemiI = 0.28f, envI = 0.5f,
                fogColor = "#b9c4cf", fogDensity = 0.0026f, exposure = 0.84f, bloom = new[] { 0.22f, 0.35f, 0.95f },
                sky = Sk(2.2f, 1.0f, 0.004f, 0.8f, 1.0f),
                night = NIGHT_OFF,
                clouds = Clouds(0.32f, 0.95f, "#ffffff", "#a3adbd", 0.95f),
                hills = "#8c9fae",
                grade = Gr(0.92f, 1.14f, 0, 0.22f, 0.025f, new[] { 1f, 0.98f, 0.95f }, 0.06f, 0.6f),
                lit = Lit("street", 0, "windows", 0, "shops", 0.5f, "signs", 0.4f, "indoor", 1, "emergency", 1),
            },
            ["afternoon"] = new EnvPreset
            { // 17:30, low warm sun from the west-southwest, long shadows
                outdoor = true, env = "sky", shadowBox = 70,
                sun = new float[] { 248, 8 }, key = new float[] { 248, 8 }, keyColor = "#ffb36a", keyI = 4.0f,
                hemiSky = "#aebbd4", hemiGround = "#7a5236", hemiI = 0.25f, envI = 0.45f,
                fogColor = "#e3bf9f", fogDensity = 0.0028f, exposure = 0.9f, bloom = new[] { 0.35f, 0.5f, 0.9f },
                sky = Sk(4.5f, 2.0f, 0.006f, 0.86f, 1.0f),
                night = NIGHT_OFF,
                clouds = Clouds(0.38f, 0.95f, "#ffd2a1", "#8a7f8f", 1.0f),
                hills = "#a99088",
                grade = Gr(0.95f, 1.14f, 0, 0.26f, 0.03f, new[] { 1f, 0.92f, 0.82f }, 0.22f, 0.6f),
                lit = Lit("street", 0, "windows", 0.25f, "shops", 0.8f, "signs", 0.6f, "indoor", 1, "emergency", 1),
            },
            ["dusk"] = new EnvPreset
            { // 18:40, sun just below the horizon: orange band, purple zenith, street lights on
                outdoor = true, env = "sky", shadowBox = 70,
                sun = new float[] { 258, -3 }, key = new float[] { 255, 6 }, keyColor = "#ff8c5a", keyI = 0.7f,
                hemiSky = "#6a6fa8", hemiGround = "#3a2c2e", hemiI = 0.55f, envI = 0.6f,
                fogColor = "#7d6a86", fogDensity = 0.0026f, exposure = 1.15f, bloom = new[] { 0.6f, 0.55f, 0.8f },
                sky = Sk(6, 3.2f, 0.009f, 0.9f, 1.1f),
                night = Night(0.35f, "#12183a", "#3a2a4a", 0.2f, "#ff9a55", 0.05f, 0.6f),
                clouds = Clouds(0.45f, 0.95f, "#ff9a62", "#4a3a5c", 0.6f),
                hills = "#3b3048",
                grade = Gr(0.98f, 1.12f, 0.05f, 0.32f, 0.035f, new[] { 1f, 0.88f, 0.95f }, 0.2f, 0.55f),
                lit = Lit("street", 1, "windows", 0.7f, "shops", 1, "signs", 1, "indoor", 1, "emergency", 1),
            },
            ["night"] = new EnvPreset
            { // city lit: sodium/LED glow on the horizon, few stars
                outdoor = true, env = "sky", shadowBox = 70,
                sun = new float[] { 270, -25 }, key = MOON, keyColor = "#9fb4e0", keyI = 0.25f,
                hemiSky = "#1c2640", hemiGround = "#1a1612", hemiI = 0.35f, envI = 0.35f,
                fogColor = "#232634", fogDensity = 0.0028f, exposure = 1.25f, bloom = new[] { 0.9f, 0.6f, 0.7f },
                sky = Sk(2, 1, 0.005f, 0.8f, 1.0f),
                night = Night(1, "#04070f", "#1a1c28", 0.55f, "#ff9a55", 0.15f, 1),
                clouds = Clouds(0.35f, 0.9f, "#6b5a52", "#1a1c26", 0.12f),
                hills = "#0b0d14",
                grade = Gr(0.88f, 1.14f, 0.1f, 0.34f, 0.045f, new[] { 0.92f, 0.95f, 1.08f }, 0.15f, 0.7f),
                lit = Lit("street", 1, "windows", 1, "shops", 1, "signs", 1, "indoor", 1, "emergency", 1),
            },
            ["blackout"] = new EnvPreset
            { // grid down: no city glow, moonlight and a full star field, cold grade
                outdoor = true, env = "sky", shadowBox = 70,
                sun = new float[] { 270, -25 }, key = MOON, keyColor = "#a9c2ff", keyI = 0.45f,
                hemiSky = "#141c30", hemiGround = "#07080a", hemiI = 0.22f, envI = 0.25f,
                fogColor = "#0b0f18", fogDensity = 0.0032f, exposure = 1.35f, bloom = new[] { 1.1f, 0.65f, 0.7f },
                sky = Sk(2, 1, 0.005f, 0.8f, 1.0f),
                night = Night(1, "#02040a", "#0b1020", 0, "#ff9a55", 1, 1),
                clouds = Clouds(0.28f, 0.9f, "#3a4258", "#07090f", 0.05f),
                hills = "#05070b",
                grade = Gr(0.65f, 1.12f, 0.1f, 0.42f, 0.06f, new[] { 0.82f, 0.92f, 1.12f }, 0.35f),
                lit = Lit("street", 0, "windows", 0, "shops", 0, "signs", 0.8f, "indoor", 0, "emergency", 1),
            },
            ["interior"] = new EnvPreset
            { // warm room light, daylight through windows
                outdoor = false, env = "room", shadowBox = 24,
                sun = new float[] { 210, 35 }, key = new float[] { 210, 35 }, keyColor = "#fff0dc", keyI = 2.2f,
                hemiSky = "#fff1e0", hemiGround = "#6b5848", hemiI = 0.18f, envI = 0.22f,
                fogColor = "#d8d0c4", fogDensity = 0.006f, exposure = 0.78f, bloom = new[] { 0.25f, 0.4f, 1.4f },
                sky = Sk(3, 1.2f, 0.005f, 0.8f, 1.0f),
                night = NIGHT_OFF,
                clouds = Clouds(0.32f, 0.95f, "#ffffff", "#a3adbd", 0.95f),
                hills = "#8c9fae",
                grade = Gr(0.95f, 1.12f, 0, 0.28f, 0.03f, new[] { 1f, 0.95f, 0.88f }, 0.18f, 0.5f),
                lit = Lit("indoor", 1, "shops", 1, "signs", 1, "emergency", 1, "street", 0, "windows", 0),
            },
            ["interiorNight"] = new EnvPreset
            { // extra (not in spec 4.2): indoor lights on at night, lit city outside the windows
                outdoor = false, env = "room", shadowBox = 24,
                sun = new float[] { 270, -25 }, key = MOON, keyColor = "#9fb4e0", keyI = 0.15f,
                hemiSky = "#3a342e", hemiGround = "#16130f", hemiI = 0.22f, envI = 0.38f,
                fogColor = "#141821", fogDensity = 0.006f, exposure = 1.1f, bloom = new[] { 0.5f, 0.5f, 0.8f },
                sky = Sk(2, 1, 0.005f, 0.8f, 1.0f),
                night = Night(1, "#04070f", "#1a1c28", 0.55f, "#ff9a55", 0.15f, 1),
                clouds = Clouds(0.35f, 0.9f, "#6b5a52", "#1a1c26", 0.12f),
                hills = "#0b0d14",
                grade = Gr(1.0f, 1.06f, 0.05f, 0.3f, 0.04f, new[] { 1f, 0.93f, 0.84f }, 0.18f),
                lit = Lit("indoor", 1, "windows", 1, "street", 1, "shops", 1, "signs", 1, "emergency", 1),
            },
            ["interiorDark"] = new EnvPreset
            { // indoor lights off, cold moonlight through the windows
                outdoor = false, env = "room", shadowBox = 24,
                sun = new float[] { 270, -25 }, key = MOON, keyColor = "#9fb6ea", keyI = 0.5f,
                hemiSky = "#1a2238", hemiGround = "#0a0a0c", hemiI = 0.12f, envI = 0.06f,
                fogColor = "#0a0d14", fogDensity = 0.01f, exposure = 1.4f, bloom = new[] { 0.9f, 0.6f, 0.75f },
                sky = Sk(2, 1, 0.005f, 0.8f, 1.0f),
                night = Night(1, "#02040a", "#0b1020", 0.1f, "#ff9a55", 0.6f, 1),
                clouds = Clouds(0.28f, 0.9f, "#3a4258", "#07090f", 0.05f),
                hills = "#05070b",
                grade = Gr(0.7f, 1.1f, 0.15f, 0.45f, 0.06f, new[] { 0.85f, 0.92f, 1.1f }, 0.3f),
                lit = Lit("indoor", 0, "windows", 0, "street", 0, "shops", 0, "signs", 0.8f, "emergency", 1),
            },
            ["underground"] = new EnvPreset
            { // cool fluorescent car park, slight haze, no sky
                outdoor = false, env = "room", shadowBox = 30, noSky = true,
                sun = new float[] { 270, -25 }, key = new float[] { 200, 60 }, keyColor = "#ffffff", keyI = 0,
                hemiSky = "#cfe3ea", hemiGround = "#3a3f40", hemiI = 0.3f, envI = 0.3f,
                fogColor = "#2a3236", fogDensity = 0.02f, exposure = 1.1f, bloom = new[] { 0.55f, 0.5f, 0.85f },
                sky = Sk(2, 1, 0.005f, 0.8f, 1.0f),
                night = NIGHT_OFF,
                clouds = Clouds(0, 0, "#000000", "#000000", 0),
                hills = "#1a1f22",
                grade = Gr(0.85f, 1.06f, 0, 0.35f, 0.05f, new[] { 0.9f, 1.02f, 1.04f }, 0.25f),
                lit = Lit("indoor", 1, "emergency", 1),
            },
        };

        static float[] Lerp(float[] a, float[] b, float k) { var o = new float[a.Length]; for (int i = 0; i < a.Length; i++) o[i] = a[i] + (b[i] - a[i]) * k; return o; }
        static float[] Grey(float[] c) { float l = c[0] * 0.2126f + c[1] * 0.7152f + c[2] * 0.0722f; return new[] { l, l, l }; }
        static float[] Mul(float[] c, float k) => new[] { c[0] * k, c[1] * k, c[2] * k };
        // new THREE.Color(hex).multiplyScalar(k).toArray(): linear RGB.
        static float[] C(string hex, float k = 1) { var c = J.Css(hex).linear; return new[] { c.r * k, c.g * k, c.b * k }; }
        static float F(object v) => Convert.ToSingle(v);

        // Flat tweenable state (numbers and linear-RGB arrays) for a preset.
        public static EnvState EnvStateOf(string name, bool rain = false)
        {
            if (name == null || !PRESETS.TryGetValue(name, out var p)) throw new ArgumentException($"[engine] unknown env preset \"{name}\"");
            var s = new EnvState();
            s["keyAz"] = p.key[0]; s["keyEl"] = p.key[1]; s.c["keyColor"] = C(p.keyColor); s["keyI"] = p.keyI;
            s["sunAz"] = p.sun[0]; s["sunEl"] = p.sun[1];
            s.c["hemiSky"] = C(p.hemiSky); s.c["hemiGround"] = C(p.hemiGround); s["hemiI"] = p.hemiI; s["envI"] = p.envI;
            s.c["fogColor"] = C(p.fogColor); s["fogDensity"] = p.fogDensity; s["exposure"] = p.exposure;
            s["bloom"] = p.bloom[0]; s["bloomRadius"] = p.bloom[1]; s["bloomThreshold"] = p.bloom.Length > 2 ? p.bloom[2] : 0.85f;
            s["turbidity"] = p.sky["turbidity"]; s["rayleigh"] = p.sky["rayleigh"]; s["mie"] = p.sky["mie"]; s["mieG"] = p.sky["mieG"]; s["skyGain"] = p.sky["gain"];
            s["night"] = F(p.night["amount"]); s.c["zenith"] = C((string)p.night["zenith"]); s.c["horizon"] = C((string)p.night["horizon"]);
            s["glow"] = F(p.night["glow"]); s.c["glowColor"] = C((string)p.night["glowColor"]);
            s["stars"] = F(p.night["stars"]); s["moon"] = F(p.night["moon"]);
            float gain = F(p.clouds["gain"]);
            s["cloudCover"] = F(p.clouds["cover"]); s["cloudOpacity"] = F(p.clouds["opacity"]);
            s.c["cloudLit"] = C((string)p.clouds["lit"], gain); s.c["cloudShade"] = C((string)p.clouds["shade"], gain);
            s.c["hills"] = C(p.hills); s["skyVis"] = p.noSky ? 0 : 1; s["shadowBox"] = p.shadowBox; s["wet"] = 0;
            if (rain && p.outdoor)
            { // overcast, hazier, darker, wet ground
                s["fogDensity"] *= 1.8f; s.c["fogColor"] = Lerp(s.c["fogColor"], Grey(s.c["fogColor"]), 0.65f);
                s["keyI"] *= 0.3f; s["turbidity"] = Math.Max(s["turbidity"], 10); s["skyGain"] *= 0.55f; s["stars"] = 0; s["moon"] *= 0.15f;
                s["cloudCover"] = 0.97f; s["cloudOpacity"] = 1; s.c["cloudLit"] = Mul(Grey(s.c["cloudLit"]), 0.55f); s.c["cloudShade"] = Mul(Grey(s.c["cloudShade"]), 0.5f);
                s["hemiI"] *= 1.15f; s["envI"] *= 0.85f; s["bloom"] += 0.1f; s["wet"] = 1; s.c["hills"] = Lerp(s.c["hills"], s.c["fogColor"], 0.6f);
            }
            return s;
        }

        public static Grade GradeFor(string name, bool rain = false)
        {
            var p = PRESETS[name];
            var g = p.grade.Clone(); g.tint = (float[])p.grade.tint?.Clone();
            if (rain && p.outdoor) { g.saturation *= 0.85f; g.contrast *= 0.97f; }
            return g;
        }

        // Linear blend of two states (numbers and arrays).
        public static EnvState MixState(EnvState a, EnvState b, float k)
        {
            var o = new EnvState();
            foreach (var kv in b.n) { float x = a.n.TryGetValue(kv.Key, out var av) ? av : float.NaN; o.n[kv.Key] = x + (kv.Value - x) * k; }
            foreach (var kv in b.c) o.c[kv.Key] = a.c.TryGetValue(kv.Key, out var ac) ? Lerp(ac, kv.Value, k) : (float[])kv.Value.Clone();
            return o;
        }
    }
}
