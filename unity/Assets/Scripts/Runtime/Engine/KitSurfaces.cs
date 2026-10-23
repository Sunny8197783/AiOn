// kit.js SURFACES: each draws { color, height } canvases at S x S (tiling sizes in metres), with the same
// seeded random sequence as the JS so the textures match.
using System;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed partial class Kit
    {
        sealed class SurfaceDraw { public Paint color, height; public float tile, rough; }

        static Color Rgba(float r, float g, float b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);
        static Color Rgb(float r, float g, float b) => new Color((int)r / 255f, (int)g / 255f, (int)b / 255f, 1);

        SurfaceDraw DrawSurface(string name, Func<float> r)
        {
            switch (name)
            {
                case "asphalt": return Asphalt(r);
                case "sidewalk": return Sidewalk(r);
                case "concrete": return Concrete(r);
                case "tile": return Tile(r);
                case "wood": return Wood(r);
                case "grass": return Grass(r);
                case "plaster": return Plaster(r);
                case "brick": return Brick(r);
            }
            throw new ArgumentException($"kit.surface: unknown surface {name}");
        }

        // worn, slightly warm grey: broad patches and fine aggregate, low-contrast so it does not shimmer
        SurfaceDraw Asphalt(Func<float> r)
        {
            var color = Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#3f3e3c"; g.fillRect(0, 0, w, h);
                for (int i = 0; i < 14; i++)
                {
                    bool dark = r() < 0.5f;
                    g.fillStyle = dark ? Rgba(28, 28, 27, 0.06f + r() * 0.1f) : Rgba(82, 80, 76, 0.06f + r() * 0.1f);
                    g.beginPath(); g.ellipse(r() * w, r() * h, 30 + r() * 110, 20 + r() * 60, r() * 3, 0, 7); g.fill();
                }
                g.Speckle(r, S * 22, new[] { "#474643", "#363533", "#4c4a47", "#32312f" }, 0.6f, 1.4f);
                g.strokeStyle = "rgba(18,18,18,0.45)"; g.lineWidth = 1.2f;
                for (int i = 0; i < 3; i++)
                {
                    g.beginPath(); float x = r() * w, y = r() * h; g.moveTo(x, y);
                    for (int k = 0; k < 8; k++) { x += (r() - 0.5f) * 40; y += (r() - 0.5f) * 40; g.lineTo(x, y); }
                    g.stroke();
                }
            });
            var height = Canvas(S, S, (g, w, h) => { g.fillStyle = "#808080"; g.fillRect(0, 0, w, h); g.Speckle(r, S * 30, new[] { "#929292", "#6e6e6e", "#9a9a9a", "#666666" }, 0.6f, 1.6f); });
            return new SurfaceDraw { color = color, height = height, tile = 4, rough = 0.9f };
        }

        // Korean interlocking pavers, 20 x 10 cm, running bond
        SurfaceDraw Sidewalk(Func<float> r)
        {
            int cols = 10, rows = 20; float pw = (float)S / cols, ph = (float)S / rows;
            var tones = new[] { "#9c9590", "#a8a19a", "#8f8882", "#b0a79e", "#968c86", "#a39890" };
            var color = Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#6f6a66"; g.fillRect(0, 0, w, h);
                for (int y = 0; y < rows; y++) for (int x = -1; x < cols; x++)
                {
                    float ox = (y % 2) * pw / 2;
                    g.fillStyle = tones[(int)(r() * tones.Length)]; g.fillRect(x * pw + ox + 1.5f, y * ph + 1.5f, pw - 3, ph - 3);
                }
                g.Speckle(r, S * 10, new[] { "rgba(0,0,0,0.12)", "rgba(255,255,255,0.08)" });
            });
            var height = Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#202020"; g.fillRect(0, 0, w, h); g.fillStyle = "#c8c8c8";
                for (int y = 0; y < rows; y++) for (int x = -1; x < cols; x++) g.fillRect(x * pw + (y % 2) * pw / 2 + 2, y * ph + 2, pw - 4, ph - 4);
            });
            return new SurfaceDraw { color = color, height = height, tile = 2, rough = 0.85f };
        }

        SurfaceDraw Concrete(Func<float> r)
        {
            var color = Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#a7a7a3"; g.fillRect(0, 0, w, h);
                for (int i = 0; i < 10; i++)
                {
                    bool dark = r() < 0.5f;
                    g.fillStyle = dark ? Rgba(70, 70, 66, 0.05f + r() * 0.07f) : Rgba(200, 200, 195, 0.05f + r() * 0.07f);
                    g.beginPath(); g.ellipse(r() * w, r() * h, 30 + r() * 90, 20 + r() * 60, r() * 3, 0, 7); g.fill();
                }
                g.Speckle(r, S * 12, new[] { "#9a9a96", "#b4b4b0", "#8e8e8a" });
            });
            var height = Canvas(S, S, (g, w, h) => { g.fillStyle = "#808080"; g.fillRect(0, 0, w, h); g.Speckle(r, S * 12, new[] { "#8c8c8c", "#747474" }); });
            return new SurfaceDraw { color = color, height = height, tile = 3, rough = 0.9f };
        }

        // 60 cm floor tiles
        SurfaceDraw Tile(Func<float> r)
        {
            int n = 4; float s = (float)S / n;
            var color = Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#9d9c97"; g.fillRect(0, 0, w, h);
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { int v = 222 + (int)(r() * 14); g.fillStyle = Rgb(v, v - 2, v - 6); g.fillRect(x * s + 1, y * s + 1, s - 2, s - 2); }
                g.Speckle(r, S * 6, new[] { "rgba(0,0,0,0.05)" });
            });
            var height = Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#303030"; g.fillRect(0, 0, w, h); g.fillStyle = "#d0d0d0";
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) g.fillRect(x * s + 2, y * s + 2, s - 4, s - 4);
            });
            return new SurfaceDraw { color = color, height = height, tile = 2.4f, rough = 0.35f };
        }

        // floor planks 15 cm
        SurfaceDraw Wood(Func<float> r)
        {
            int n = 8; float s = (float)S / n;
            var color = Canvas(S, S, (g, w, h) =>
            {
                for (int i = 0; i < n; i++)
                {
                    float b0 = 150 + r() * 30, b1 = 104 + r() * 22, b2 = 66 + r() * 16;
                    g.fillStyle = Rgb(b0, b1, b2); g.fillRect(0, i * s, w, s);
                    for (int k = 0; k < 40; k++)
                    {
                        g.strokeStyle = Rgba(70, 40, 20, 0.05f + r() * 0.08f); g.beginPath();
                        float y = i * s + r() * s; g.moveTo(0, y);
                        float c1 = y + (r() - 0.5f) * 6, c2 = y + (r() - 0.5f) * 6;
                        g.bezierCurveTo(w * 0.3f, c1, w * 0.6f, c2, w, y); g.stroke();
                    }
                    g.fillStyle = "rgba(40,24,12,0.6)"; g.fillRect(0, i * s, w, 1.5f); g.fillRect(r() * w, i * s, 1.5f, s);
                }
            });
            var height = Canvas(S, S, (g, w, h) => { g.fillStyle = "#909090"; g.fillRect(0, 0, w, h); g.fillStyle = "#404040"; for (int i = 0; i < n; i++) g.fillRect(0, i * s, w, 2); });
            return new SurfaceDraw { color = color, height = height, tile = 1.2f, rough = 0.55f };
        }

        SurfaceDraw Grass(Func<float> r)
        {
            var color = Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#4d6b34"; g.fillRect(0, 0, w, h);
                g.Speckle(r, S * 50, new[] { "#5c7d3d", "#3f5a2a", "#6b8a45", "#45622f", "#7a8f4e" }, 1, 3);
            });
            var height = Canvas(S, S, (g, w, h) => { g.fillStyle = "#808080"; g.fillRect(0, 0, w, h); g.Speckle(r, S * 40, new[] { "#a0a0a0", "#606060" }, 1, 3); });
            return new SurfaceDraw { color = color, height = height, tile = 3, rough = 0.95f };
        }

        SurfaceDraw Plaster(Func<float> r)
        {
            var color = Canvas(S, S, (g, w, h) => { g.fillStyle = "#e9e5dc"; g.fillRect(0, 0, w, h); g.Speckle(r, S * 8, new[] { "rgba(0,0,0,0.03)", "rgba(255,255,255,0.05)" }, 1, 4); });
            var height = Canvas(S, S, (g, w, h) => { g.fillStyle = "#808080"; g.fillRect(0, 0, w, h); g.Speckle(r, S * 10, new[] { "#888", "#777" }, 1, 3); });
            return new SurfaceDraw { color = color, height = height, tile = 2, rough = 0.9f };
        }

        static readonly int[][] BRICKS = { new[] { 150, 72, 54 }, new[] { 136, 64, 48 }, new[] { 160, 84, 62 }, new[] { 122, 58, 44 } };
        SurfaceDraw Brick(Func<float> r)
        {
            int rows = 16, cols = 4; float bh = (float)S / rows, bw = (float)S / cols;
            var color = Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#b9b2a6"; g.fillRect(0, 0, w, h);
                for (int y = 0; y < rows; y++) for (int x = -1; x <= cols; x++)
                {
                    var t = BRICKS[(int)(r() * 4)];
                    g.fillStyle = Rgb(t[0], t[1], t[2]); g.fillRect(x * bw + (y % 2) * bw / 2 + 1.5f, y * bh + 1.5f, bw - 3, bh - 3);
                }
            });
            var height = Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#303030"; g.fillRect(0, 0, w, h); g.fillStyle = "#c0c0c0";
                for (int y = 0; y < rows; y++) for (int x = -1; x <= cols; x++) g.fillRect(x * bw + (y % 2) * bw / 2 + 2, y * bh + 2, bw - 4, bh - 4);
            });
            return new SurfaceDraw { color = color, height = height, tile = 1.6f, rough = 0.88f };
        }
    }
}
