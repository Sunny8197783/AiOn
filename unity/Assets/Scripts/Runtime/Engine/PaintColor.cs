// Paint's value types: CSS colour parsing, fill/stroke styles, gradients, ImageData, TextMetrics.
// Mirrors the Canvas 2D API names so ported drawing code reads like the JS.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public static class PaintColor
    {
        static readonly Dictionary<string, Color> cache = new Dictionary<string, Color>();
        static readonly Dictionary<string, string> NAMED = new Dictionary<string, string>
        {
            ["black"] = "#000000", ["white"] = "#ffffff", ["red"] = "#ff0000", ["green"] = "#008000", ["blue"] = "#0000ff",
            ["yellow"] = "#ffff00", ["orange"] = "#ffa500", ["gray"] = "#808080", ["grey"] = "#808080", ["transparent"] = "#00000000",
        };

        // '#rgb', '#rgba', '#rrggbb', '#rrggbbaa', 'rgb(r,g,b)', 'rgba(r,g,b,a)', 'hsl(h,s%,l%)', 'hsla(...)', a few names.
        // Unknown strings give opaque black (the canvas ignores them; drawing black is easier to spot).
        public static Color Parse(string css)
        {
            if (string.IsNullOrEmpty(css)) return Color.black;
            if (cache.TryGetValue(css, out var c)) return c;
            c = ParseRaw(css.Trim().ToLowerInvariant());
            if (cache.Count < 4096) cache[css] = c;
            return c;
        }

        static Color ParseRaw(string s)
        {
            if (NAMED.TryGetValue(s, out var named)) s = named;
            if (s.StartsWith("#"))
            {
                var h = s.Substring(1);
                if (h.Length == 3 || h.Length == 4) h = string.Concat(Array.ConvertAll(h.ToCharArray(), ch => new string(ch, 2)));
                if ((h.Length == 6 || h.Length == 8) && uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
                {
                    if (h.Length == 6) v = (v << 8) | 0xff;
                    return new Color(((v >> 24) & 255) / 255f, ((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
                }
                return Color.black;
            }
            int open = s.IndexOf('('), close = s.LastIndexOf(')');
            if (open < 0 || close < open) return Color.black;
            var fn = s.Substring(0, open).Trim();
            var parts = s.Substring(open + 1, close - open - 1).Replace("/", " ").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float N(int i, float scale) // number or percentage; scale = what 100% means
            {
                if (i >= parts.Length) return i == 3 ? 1f : 0f;
                var p = parts[i];
                bool pct = p.EndsWith("%");
                float.TryParse(pct ? p.TrimEnd('%') : p.Replace("deg", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var f);
                return pct ? f / 100f * scale : f;
            }
            if (fn == "rgb" || fn == "rgba") return new Color(N(0, 255) / 255f, N(1, 255) / 255f, N(2, 255) / 255f, Mathf.Clamp01(N(3, 1)));
            if (fn == "hsl" || fn == "hsla")
            {
                float hue = ((N(0, 360) % 360) + 360) % 360 / 360f, sat = Mathf.Clamp01(N(1, 1)), lig = Mathf.Clamp01(N(2, 1));
                var rgb = HslToRgb(hue, sat, lig);
                rgb.a = Mathf.Clamp01(N(3, 1));
                return rgb;
            }
            return Color.black;
        }

        static Color HslToRgb(float h, float s, float l)
        {
            float q = l < 0.5f ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            float T(float t) { t = t < 0 ? t + 1 : t > 1 ? t - 1 : t; return t < 1f / 6 ? p + (q - p) * 6 * t : t < 0.5f ? q : t < 2f / 3 ? p + (q - p) * (2f / 3 - t) * 6 : p; }
            return new Color(T(h + 1f / 3), T(h), T(h - 1f / 3), 1);
        }
    }

    // A fill or stroke style: a solid colour or a gradient. Assign a CSS string, a Color or a gradient.
    public sealed class PaintStyle
    {
        public readonly Color color;
        public readonly PaintGradient gradient;
        PaintStyle(Color c, PaintGradient g) { color = c; gradient = g; }
        public static implicit operator PaintStyle(string css) => new PaintStyle(PaintColor.Parse(css), null);
        public static implicit operator PaintStyle(Color c) => new PaintStyle(c, null);
        public static implicit operator PaintStyle(PaintGradient g) => new PaintStyle(Color.black, g);
        // Colour at a point in the gradient's own (user) space.
        public Color At(float ux, float uy) => gradient == null ? color : gradient.At(ux, uy);
    }

    public sealed class PaintGradient
    {
        readonly bool radial;
        readonly float x0, y0, r0, x1, y1, r1;
        readonly List<KeyValuePair<float, Color>> stops = new List<KeyValuePair<float, Color>>();

        public PaintGradient(float x0, float y0, float x1, float y1) { this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1; }
        public PaintGradient(float x0, float y0, float r0, float x1, float y1, float r1) { radial = true; this.x0 = x0; this.y0 = y0; this.r0 = r0; this.x1 = x1; this.y1 = y1; this.r1 = r1; }

        public void addColorStop(float offset, string css) => addColorStop(offset, PaintColor.Parse(css));
        public void addColorStop(float offset, Color c)
        {
            int i = stops.FindIndex(s => s.Key > offset);
            stops.Insert(i < 0 ? stops.Count : i, new KeyValuePair<float, Color>(Mathf.Clamp01(offset), c));
        }

        public Color At(float x, float y)
        {
            if (stops.Count == 0) return new Color(0, 0, 0, 0);
            float t;
            if (!radial) { float dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy; t = l2 > 0 ? ((x - x0) * dx + (y - y0) * dy) / l2 : 0; }
            else
            {
                // ponytail: concentric approximation (centre moves from (x0,y0) to (x1,y1) with t); exact two-circle
                // cones are not used by the game's gradients.
                float d = Mathf.Sqrt((x - x1) * (x - x1) + (y - y1) * (y - y1));
                t = r1 > r0 ? (d - r0) / (r1 - r0) : 0;
            }
            t = Mathf.Clamp01(t);
            if (t <= stops[0].Key) return stops[0].Value;
            for (int i = 1; i < stops.Count; i++)
                if (t <= stops[i].Key)
                {
                    var a = stops[i - 1]; var b = stops[i];
                    float k = b.Key > a.Key ? (t - a.Key) / (b.Key - a.Key) : 1;
                    // canvas interpolates in premultiplied space, so a fade to rgba(...,0) keeps its hue
                    var pa = new Color(a.Value.r * a.Value.a, a.Value.g * a.Value.a, a.Value.b * a.Value.a, a.Value.a);
                    var pb = new Color(b.Value.r * b.Value.a, b.Value.g * b.Value.a, b.Value.b * b.Value.a, b.Value.a);
                    var p = Color.Lerp(pa, pb, k);
                    return p.a > 1e-5f ? new Color(p.r / p.a, p.g / p.a, p.b / p.a, p.a) : new Color(0, 0, 0, 0);
                }
            return stops[stops.Count - 1].Value;
        }
    }

    // getImageData / createImageData: RGBA bytes, row 0 at the top.
    public sealed class ImageData
    {
        public readonly int width, height;
        public readonly byte[] data;
        public ImageData(int width, int height) { this.width = width; this.height = height; data = new byte[width * height * 4]; }
    }

    public sealed class TextMetrics
    {
        public float width;
    }
}
