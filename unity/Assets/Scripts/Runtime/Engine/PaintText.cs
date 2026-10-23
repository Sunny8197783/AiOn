// Paint text: fillText / strokeText / measureText with the OS Korean font (PORTING.md "Text and fonts").
// Glyphs come from a Unity dynamic font: the characters are requested into its atlas, the atlas is read
// back once per rebuild, and each glyph is sampled through the inverse transform (so rotated text works).
// ponytail: OS font + GPU readback; the quality pass swaps in a bundled Korean SDF font. Without a font or
// a GPU (batch mode) text is skipped and measureText falls back to an estimate.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed partial class Paint
    {
        public static readonly string[] FONT_FAMILIES = { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "NanumGothic" };
        static Font osFont;
        static bool fontTried, atlasDirty = true;
        static Color32[] atlas;
        static int aw, ah;
        static readonly Regex FONT_RX = new Regex(@"(?:(bold|bolder|[1-9]00)\s+)?(?:\S+\s+)*?([\d.]+)px", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static Font OsFont()
        {
            if (fontTried) return osFont;
            fontTried = true;
            try
            {
                osFont = Font.CreateDynamicFontFromOSFont(FONT_FAMILIES, 32);
                Font.textureRebuilt += f => { if (f == osFont) atlasDirty = true; };
            }
            catch (Exception e) { Debug.LogWarning("[paint] no OS font for canvas text: " + e.Message); osFont = null; }
            return osFont;
        }

        // CSS font -> pixel size and bold ("700 62px 'Pretendard',..." / "bold 64px sans-serif").
        static void ParseFont(string css, out float px, out bool bold)
        {
            px = 10; bold = false;
            var m = FONT_RX.Match(css ?? "");
            if (!m.Success) return;
            float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out px);
            var w = m.Groups[1].Value.ToLowerInvariant();
            bold = w == "bold" || w == "bolder" || (w.Length == 3 && w[0] >= '6');
        }

        static bool ReadAtlas(Font f)
        {
            if (!atlasDirty && atlas != null) return true;
            try
            {
                var src = f.material.mainTexture;
                var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var prev = RenderTexture.active;
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
                tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                tex.Apply(false);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                atlas = tex.GetPixels32(); aw = src.width; ah = src.height;
                UnityEngine.Object.Destroy(tex);
                atlasDirty = false;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[paint] font atlas readback failed; text skipped: " + e.Message); atlas = null; return false; }
        }

        static float AtlasAlpha(Vector2 uv)
        {
            float x = uv.x * aw - 0.5f, y = uv.y * ah - 0.5f;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            float A(int xx, int yy) { if (xx < 0 || yy < 0 || xx >= aw || yy >= ah) return 0; var c = atlas[yy * aw + xx]; return Mathf.Max(c.a, c.r) / 255f; }
            return Mathf.Lerp(Mathf.Lerp(A(x0, y0), A(x0 + 1, y0), fx), Mathf.Lerp(A(x0, y0 + 1), A(x0 + 1, y0 + 1), fx), fy);
        }

        struct Glyph { public CharacterInfo ci; public float pen; }

        // Lays the string out; false when there is no usable font (then width is an estimate).
        bool Layout(string text, out List<Glyph> glyphs, out float widthPx, out float px)
        {
            ParseFont(s.font, out px, out bool bold);
            glyphs = new List<Glyph>(); widthPx = 0;
            int size = Mathf.Max(1, Mathf.RoundToInt(px));
            var f = OsFont();
            if (f == null || string.IsNullOrEmpty(text))
            {
                foreach (var ch in text ?? "") widthPx += ch >= 0x1100 ? px : px * 0.55f;
                return false;
            }
            var style = bold ? FontStyle.Bold : FontStyle.Normal;
            f.RequestCharactersInTexture(text, size, style);
            foreach (var ch in text)
            {
                if (char.IsControl(ch) || !f.GetCharacterInfo(ch, out var ci, size, style)) continue;
                glyphs.Add(new Glyph { ci = ci, pen = widthPx });
                widthPx += ci.advance;
            }
            return true;
        }

        public TextMetrics measureText(string text)
        {
            Layout(text, out _, out float w, out _);
            return new TextMetrics { width = w };
        }

        public void fillText(string text, float x, float y, float? maxWidth = null) => DrawText(text, x, y, maxWidth, s.fill);
        // ponytail: drawn as filled text in the stroke style (outlined text is only used for small labels).
        public void strokeText(string text, float x, float y, float? maxWidth = null) => DrawText(text, x, y, maxWidth, s.stroke);

        void DrawText(string text, float x, float y, float? maxWidth, PaintStyle style)
        {
            if (!Layout(text, out var glyphs, out float tw, out float px) || glyphs.Count == 0) return;
            if (!ReadAtlas(osFont)) return;
            float sx = maxWidth.HasValue && maxWidth.Value > 0 && tw > maxWidth.Value ? maxWidth.Value / tw : 1;
            float align = s.textAlign == "center" ? -tw / 2 : s.textAlign == "right" || s.textAlign == "end" ? -tw : 0;
            // ponytail: em-box metrics estimated (ascent 0.8 em, descent 0.2 em) instead of read from the font.
            float baseY = y + (s.textBaseline == "middle" ? 0.3f * px : s.textBaseline == "top" || s.textBaseline == "hanging" ? 0.8f * px
                : s.textBaseline == "bottom" || s.textBaseline == "ideographic" ? -0.2f * px : 0);
            foreach (var g in glyphs)
            {
                var ci = g.ci; if (ci.glyphWidth <= 0 || ci.glyphHeight <= 0) continue;
                float gx0 = x + (align + g.pen + ci.minX) * sx, gx1 = x + (align + g.pen + ci.maxX) * sx;
                float gy0 = baseY - ci.maxY, gy1 = baseY - ci.minY;
                Vector2 c0 = T(gx0, gy0), c1 = T(gx1, gy0), c2 = T(gx1, gy1), c3 = T(gx0, gy1);
                int bx0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(c0.x, c1.x), Mathf.Min(c2.x, c3.x))));
                int bx1 = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(c0.x, c1.x), Mathf.Max(c2.x, c3.x))));
                int by0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(c0.y, c1.y), Mathf.Min(c2.y, c3.y))));
                int by1 = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y))));
                for (int py = by0; py <= by1; py++)
                    for (int qx = bx0; qx <= bx1; qx++)
                    {
                        var u = Inv(qx + 0.5f, py + 0.5f);
                        if (u.x < gx0 || u.x >= gx1 || u.y < gy0 || u.y >= gy1) continue;
                        float su = (u.x - gx0) / (gx1 - gx0), tv = (u.y - gy0) / (gy1 - gy0);
                        var uv = ci.uvTopLeft + su * (ci.uvTopRight - ci.uvTopLeft) + tv * (ci.uvBottomLeft - ci.uvTopLeft);
                        float a = AtlasAlpha(uv);
                        if (a > 0.003f) Plot(qx, py, style, a);
                    }
            }
        }
    }
}
