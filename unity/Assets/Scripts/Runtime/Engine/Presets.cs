// Ports engine/presets.js: environment presets per time of day (sun, sky, fog, exposure, bloom, clouds, grade,
// light groups) and the flat tweenable state the engine blends between them.
// Preset names (used as strings everywhere): day afternoon dusk night blackout interior interiorNight interiorDark underground.
using System;
using System.Collections.Generic;

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
    }

    public static class Presets
    {
        public static readonly float[] MOON = { 140, 34 };
        public static readonly Dictionary<string, EnvPreset> PRESETS = new Dictionary<string, EnvPreset>();
        public static EnvState EnvStateOf(string name, bool rain = false) => throw new NotImplementedException("port: engine/presets.js:envState");
        public static Grade GradeFor(string name, bool rain = false) => throw new NotImplementedException("port: engine/presets.js:gradeFor");
        public static EnvState MixState(EnvState a, EnvState b, float k) => throw new NotImplementedException("port: engine/presets.js:mixState");
    }
}
