// Ports levels/town-util.js: helpers shared by the town modules — rect carving, walk-zone heights, world-UV
// flat geometry, fence/rect solids, wind sway for foliage and cloth, Hangul canvas notice boards.
using System;
using System.Collections.Generic;
using System.Globalization;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // board(kit, { w, h, paper }) -> { canvas, texture, draw(title, lines, accent, sub) }; draw re-uploads.
    public sealed class Board
    {
        public Paint canvas;
        public Texture2D texture;
        internal int w = 512, h = 384;
        internal string paper = "#f4f1e8";

        // Notice/board canvas: header band + lines.
        public void Draw(string title, IList<string> lines, string accent = "#1f4e8c", string sub = "")
        {
            var g = canvas;
            g.fillStyle = paper; g.fillRect(0, 0, w, h);
            g.fillStyle = accent; g.fillRect(0, 0, w, h * 0.17f);
            g.fillStyle = "#ffffff"; g.font = $"800 {TownUtil.Round(h * 0.085f)}px {Kit.FONT}"; g.textBaseline = "middle"; g.textAlign = "left";
            g.fillText(title ?? "", w * 0.05f, h * 0.085f, w * 0.9f);
            bool hasSub = !string.IsNullOrEmpty(sub);
            if (hasSub) { g.fillStyle = "#5a5a5a"; g.font = $"600 {TownUtil.Round(h * 0.045f)}px {Kit.FONT}"; g.fillText(sub, w * 0.05f, h * 0.22f, w * 0.9f); }
            g.fillStyle = "#1d1d1d";
            float y = h * (hasSub ? 0.32f : 0.27f), size = TownUtil.Round(h * 0.06f);
            if (lines != null)
                for (int i = 0; i < lines.Count && i < 9; i++)
                {
                    g.font = $"600 {size}px {Kit.FONT}";
                    foreach (var part in Wrap(g, lines[i] ?? "", w * 0.9f)) { if (y > h * 0.95f) break; g.fillText(part, w * 0.05f, y); y += size * 1.35f; }
                }
            canvas.Upload(texture);
        }

        static List<string> Wrap(Paint g, string text, float max)
        {
            var output = new List<string>(); string line = "";
            for (int i = 0; i < text.Length; i++)
            {
                // for (const ch of text): one code point (surrogate pairs stay together)
                string ch = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? text.Substring(i++, 2) : text[i].ToString();
                if (g.measureText(line + ch).width > max && line.Length > 0) { output.Add(line); line = ch.TrimStart(); }
                else line += ch;
            }
            if (line.Length > 0) output.Add(line);
            return output;
        }
    }

    public static class TownUtil
    {
        // Subtract holes from a walk rect (keeps y / ramp); non-overlapping pieces.
        public static List<WalkZone> Carve(WalkZone r, IList<RectXZ> holes)
        {
            var parts = new List<WalkZone> { r };
            if (holes == null) return parts;
            foreach (var h in holes)
            {
                var next = new List<WalkZone>();
                foreach (var p in parts)
                {
                    if (h.x1 <= p.x0 || h.x0 >= p.x1 || h.z1 <= p.z0 || h.z0 >= p.z1) { next.Add(p); continue; }
                    if (h.z0 > p.z0) next.Add(With(p, p.x0, p.x1, p.z0, h.z0));
                    if (h.z1 < p.z1) next.Add(With(p, p.x0, p.x1, h.z1, p.z1));
                    float z0 = Math.Max(p.z0, h.z0), z1 = Math.Min(p.z1, h.z1);
                    if (h.x0 > p.x0) next.Add(With(p, p.x0, h.x0, z0, z1));
                    if (h.x1 < p.x1) next.Add(With(p, h.x1, p.x1, z0, z1));
                }
                parts = next;
            }
            return parts;
        }

        static WalkZone With(WalkZone p, float x0, float x1, float z0, float z1) => new WalkZone(x0, x1, z0, z1, p.y) { ramp = p.ramp };

        public static float ZoneY(WalkZone w, float x, float z)
        {
            if (w.ramp == null) return w.y;
            float t = w.ramp.axis == "x" ? (x - w.x0) / (w.x1 - w.x0) : (z - w.z0) / (w.z1 - w.z0);
            return w.ramp.y0 + (w.ramp.y1 - w.ramp.y0) * Math.Min(1, Math.Max(0, t));
        }

        // Highest walk surface at (x, z); 0 outside every zone.
        public static float HeightAt(IList<WalkZone> walk, float x, float z)
        {
            float best = float.NegativeInfinity;
            foreach (var w in walk) if (x >= w.x0 && x <= w.x1 && z >= w.z0 && z <= w.z1) best = Math.Max(best, ZoneY(w, x, z));
            return float.IsNegativeInfinity(best) ? 0 : best;
        }

        // Yaw (0 = facing -z) for facing direction (dx, dz), JS convention.
        public static float YawTo(float dx, float dz) => Mathf.Atan2(-dx, -dz);

        // Horizontal plane over a rect at height y, UVs in world metres (textures line up across pieces).
        public static GeoData FlatGeo(RectXZ r, float y = 0)
        {
            var g = Geo.Plane(r.x1 - r.x0, r.z1 - r.z0).RotateX(-Mathf.PI / 2).Translate((r.x0 + r.x1) / 2, y, (r.z0 + r.z1) / 2);
            for (int i = 0; i < g.pos.Count; i++) g.uv[i] = new Vector2(g.pos[i].x, -g.pos[i].z);
            return g;
        }

        public static GameObject Flat(RectXZ r, float y, Material material, bool receive = true) => Geo.Obj(FlatGeo(r, y), material, null, false, receive);

        // Fence line [x0, z0, x1, z1] -> blocking box (0.4 m thick, 2 m tall), ends padded so corners close.
        public static Solid LineSolid(FenceLine line, float t = 0.2f, float y0 = -0.5f, float y1 = 2) =>
            new Solid(Math.Min(line.x0, line.x1) - t, Math.Max(line.x0, line.x1) + t, Math.Min(line.z0, line.z1) - t, Math.Max(line.z0, line.z1) + t, y0, y1);

        public static Solid RectSolid(RectXZ r, float y0 = -0.5f, float y1 = 3, string id = null) => new Solid(r.x0, r.x1, r.z0, r.z1, y0, y1, id);

        // Shared wind (WIND.uTime / uWind): Town.cs sets it each frame; 0 wind with reduced motion.
        // ponytail: global shader values only; URP Lit has no vertex sway, so nothing moves until the quality pass
        // adds a sway vertex function (Shader Graph / HLSL) that reads these globals and the Sway() properties.
        public static void SetWind(float time, float wind)
        {
            Shader.SetGlobalFloat("_SwayTime", time);
            Shader.SetGlobalFloat("_SwayWind", wind);
        }

        // Makes a material sway with height above its origin (foliage, banners); returns it.
        // ponytail: records amp/freq/ref (+ keyword GYEONGBO_SWAY) for the sway shader the quality pass adds.
        public static Material Sway(Material material, float amp = 0.06f, float freq = 1.7f, float @ref = 1)
        {
            if (material == null) return null;
            material.SetFloat("_SwayAmp", amp); material.SetFloat("_SwayFreq", freq); material.SetFloat("_SwayRef", @ref);
            material.EnableKeyword("GYEONGBO_SWAY");
            return material;
        }

        public static Board MakeBoard(Kit kit, int w = 512, int h = 384, string paper = "#f4f1e8")
        {
            var c = kit.Canvas(w, h);
            return new Board { canvas = c, texture = kit.Texture(c), w = w, h = h, paper = paper };
        }

        // Deterministic pick from [(value, weight)].
        public static T Pick<T>(Func<float> r, IList<KeyValuePair<T, float>> list)
        {
            float sum = 0;
            foreach (var kv in list) sum += kv.Value;
            float t = r() * sum;
            foreach (var kv in list) { if ((t -= kv.Value) <= 0) return kv.Key; }
            return list[list.Count - 1].Key;
        }

        // ---- private helpers for the town A files ----
        // JS Math.round (half up; C# Math.Round is banker's rounding).
        internal static float Round(float v) => Mathf.Floor(v + 0.5f);
        // CSS numbers ("800 40.8px", "rgb(...)") need '.' whatever the OS culture.
        internal static string F(float v) => v.ToString(CultureInfo.InvariantCulture);
        internal static string F(double v) => v.ToString(CultureInfo.InvariantCulture);
        // kit.rng(seed) does seed >>> 0 (truncate, wrap to uint32).
        internal static uint Seed(double v) => unchecked((uint)(long)v);
        // (r() * n) | 0 as an index, guarded against a float rng rounding up to 1.
        internal static int Idx(float r, int n) => Math.Min((int)(r * n), n - 1);
        internal static float Hypot(float x, float z) => Mathf.Sqrt(x * x + z * z);
        internal static At At(float x, float y, float z, float ry = 0, bool cast = true, float rx = 0, float rz = 0) =>
            new At { x = x, y = y, z = z, ry = ry, rx = rx, rz = rz, cast = cast };
        // new THREE.Mesh(geo, mat): three.js meshes neither cast nor receive shadows unless told to.
        internal static GameObject Obj(GeoData g, Material m, bool cast = false, bool receive = false) => Geo.Obj(g, m, null, cast, receive);
        internal static Material MatOf(GameObject o) { var r = o != null ? o.GetComponentInChildren<Renderer>() : null; return r != null ? r.sharedMaterial : null; }
        internal static void CastShadow(GameObject o, bool on)
        {
            foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // A copy of a (memoised) material with another base colour: stands in for three.js instanceColor.
        internal static Material Tinted(Kit kit, string key, Material baseMat, string css) => kit.Memo($"{key}:tint:{css}", () =>
        {
            var m = new Material(baseMat); Mat.SetColor(m, J.Css(css)); return m;
        });
    }
}
