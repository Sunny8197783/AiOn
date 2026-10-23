// Part of the levels/town-fronts.js port: the sign/interior atlases (one texture, one draw call each) and their
// painters, plus the flags (태극기, city flag, 연무동) on the 행정복지센터 poles.
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.TownUtil;

namespace Gyeongbo.Levels
{
    // Atlas of equal slots; Plane(i, w, h) returns a PlaneGeometry whose UVs cover slot i.
    internal sealed class TownAtlas
    {
        public Texture2D tex;
        int W, H, cols; float sw, sh;

        public static TownAtlas Make(Kit kit, string key, int W, int H, int cols, int rows, Action<Paint, int, float, float> paint)
        {
            float sw = (float)W / cols, sh = (float)H / rows;
            var tex = kit.Memo($"town:atlas:{key}", () => kit.Texture(kit.Canvas(W, H, (g, w, h) =>
            {
                for (int i = 0; i < cols * rows; i++) { g.save(); g.translate((i % cols) * sw, (i / cols) * sh); g.beginPath(); g.rect(0, 0, sw, sh); g.clip(); paint(g, i, sw, sh); g.restore(); }
            })));
            return new TownAtlas { tex = tex, W = W, H = H, cols = cols, sw = sw, sh = sh };
        }

        public GeoData Plane(int i, float w, float h)
        {
            var g = Geo.Plane(w, h); const float pad = 2;
            float u0 = ((i % cols) * sw + pad) / W, u1 = ((i % cols + 1) * sw - pad) / W;
            float v1 = 1 - ((i / cols) * sh + pad) / H, v0 = 1 - ((i / cols + 1) * sh - pad) / H;
            for (int k = 0; k < g.uv.Count; k++) g.uv[k] = new Vector2(u0 + g.uv[k].x * (u1 - u0), v0 + g.uv[k].y * (v1 - v0));
            return g;
        }
    }

    internal static class TownFrontsPaint
    {
        // Horizontal signboards: [text, bg, fg, mark]. mark: 'B' brand disc, '약' pharmacy disc, 'line' accent bar.
        internal static readonly (string text, string bg, string fg, string mark)[] SIGNS =
        {
            ("바로편의점", "#0f8a6c", "#ffffff", "B"), ("연무약국", "#1f7a3a", "#ffffff", "약"), ("연무분식", "#e8a32a", "#3a1d0e", null), ("연무마트", "#c8312a", "#ffffff", "line"),
            ("한결물류", "#ffffff", "#1d4f8c", "line"), ("연무동 행정복지센터", "#2b4c7e", "#ffffff", null), ("연무 생활문화센터", "#3e5a6e", "#ffffff", null), ("카페 쉼표", "#2d2a26", "#f3e3c3", null),
            ("관리사무소", "#3f6b4a", "#ffffff", null), ("한빛아파트 103동", "#f4f0e6", "#3f7d5c", null), ("수학 · 영어 학원", "#1d3f8f", "#ffffff", null), ("치킨 · 호프", "#e7c44a", "#5a2a10", null),
            ("헤어살롱 연", "#7a3a6a", "#ffffff", null), ("크린세탁", "#2f7fb5", "#ffffff", null), ("한빛공인중개사", "#c84a2a", "#ffffff", null), ("할매국밥", "#3b2a1e", "#f2d28a", null),
            ("PC방", "#141c3a", "#5fd0ff", null), ("연무태권도", "#b52a2a", "#ffffff", null), ("꽃집 봄", "#f3d6dc", "#8a2a4a", null), ("연무정육점", "#a8262a", "#ffffff", null),
            ("엄마손반찬", "#f2e6c8", "#4a3a20", null), ("연무철물", "#2f3a44", "#f2c230", null), ("한빛아파트 102동", "#f4f0e6", "#3f7d5c", null), ("한빛아파트 101동", "#f4f0e6", "#3f7d5c", null),
        };
        internal static readonly (string text, string bg)[] VSIGNS = { ("약", "#1f7a3a"), ("학원", "#1d3f8f"), ("치킨", "#c8312a"), ("국밥", "#3b2a1e"), ("PC", "#141c3a"), ("미용실", "#7a3a6a"), ("노래방", "#5a2a8a"), ("세탁", "#2f7fb5") };
        internal static readonly string[] INTERIORS = { "store", "pharmacy", "snack", "mart", "cafe", "lobby", "office", "civic" };

        internal static void PaintSign(Paint g, int i, float w, float h)
        {
            var (text, bg, fg, mark) = i < SIGNS.Length ? SIGNS[i] : ("", "#333", "#fff", null);
            g.fillStyle = bg; g.fillRect(0, 0, w, h);
            g.fillStyle = "rgba(255,255,255,0.12)"; g.fillRect(0, 0, w, h * 0.08f);
            float x0 = w * 0.06f;
            if (mark == "B" || mark == "약")
            {
                g.fillStyle = mark == "B" ? "#f39a1e" : "#ffffff"; g.beginPath(); g.arc(h * 0.62f, h / 2, h * 0.36f, 0, 7); g.fill();
                g.fillStyle = mark == "B" ? "#ffffff" : "#c8312a"; g.font = $"900 {F(h * 0.48f)}px {Kit.FONT}"; g.textAlign = "center"; g.textBaseline = "middle"; g.fillText(mark, h * 0.62f, h * 0.53f);
                x0 = h * 1.15f;
            }
            if (mark == "line") { g.fillStyle = fg; g.fillRect(0, h * 0.86f, w, h * 0.06f); }
            float size = h * 0.6f; g.font = $"800 {F(size)}px {Kit.FONT}";
            while (g.measureText(text).width > w - x0 - w * 0.05f && size > 10) { size -= 2; g.font = $"800 {F(size)}px {Kit.FONT}"; }
            g.fillStyle = fg; g.textAlign = "center"; g.textBaseline = "middle"; g.fillText(text, (x0 + w * 0.95f) / 2, h * 0.53f);
        }

        internal static void PaintVSign(Paint g, int i, float w, float h)
        {
            var (text, bg) = i < VSIGNS.Length ? VSIGNS[i] : ("", "#333");
            g.fillStyle = bg; g.fillRect(0, 0, w, h); g.strokeStyle = "#ffffff"; g.lineWidth = 4; g.strokeRect(6, 6, w - 12, h - 12);
            var chars = new List<string>();
            for (int k = 0; k < text.Length; k++) chars.Add(char.IsHighSurrogate(text[k]) && k + 1 < text.Length ? text.Substring(k++, 2) : text[k].ToString());
            float step = Math.Min(w * 0.9f, (h - 30) / chars.Count);
            g.fillStyle = "#ffffff"; g.font = $"800 {F(step * 0.78f)}px {Kit.FONT}"; g.textAlign = "center"; g.textBaseline = "middle";
            for (int k = 0; k < chars.Count; k++) g.fillText(chars[k], w / 2, 15 + step * (k + 0.5f));
        }

        internal static void PaintInterior(Paint g, int i, float w, float h)
        {
            string kind = INTERIORS[i];
            long seed = i * 97 + 13;
            float r() { seed = seed * 16807 % 2147483647; return seed / 2147483647f; }
            bool warm = kind == "cafe" || kind == "snack" || kind == "lobby";
            g.fillStyle = warm ? "#5a4030" : kind == "civic" || kind == "office" ? "#c9ccc8" : "#e8ece9"; g.fillRect(0, 0, w, h);
            g.fillStyle = warm ? "#3a2a20" : "#b8bcb8"; g.fillRect(0, h * 0.78f, w, h * 0.22f);
            for (int x = 20; x < w; x += 70)
            {
                g.fillStyle = warm ? "#ffd9a0" : "#ffffff";
                if (kind == "cafe") { g.beginPath(); g.arc(x + 20, h * 0.2f, 9, 0, 7); g.fill(); g.fillRect(x + 19, 0, 2, h * 0.2f); }
                else g.fillRect(x, h * 0.04f, 50, 6);
            }
            if (kind == "store" || kind == "mart" || kind == "pharmacy")
            {
                int n = kind == "pharmacy" ? 3 : 5;
                for (int s = 0; s < n; s++)
                {
                    float x = 14 + s * (w - 28) / n, sw = (w - 28) / n - 14;
                    g.fillStyle = "#d0d4d2"; g.fillRect(x, h * 0.22f, sw, h * 0.56f);
                    for (int row = 0; row < 4; row++) for (int k = 0; k < 7; k++)
                    {
                        g.fillStyle = $"hsl({(int)(r() * 360)},{F(50 + r() * 30)}%,{F(45 + r() * 20)}%)";
                        g.fillRect(x + 4 + k * sw / 7, h * 0.25f + row * h * 0.13f, sw / 7 - 3, h * 0.1f);
                    }
                }
                if (kind == "store") { g.fillStyle = "#cfe8ff"; g.fillRect(w * 0.72f, h * 0.18f, w * 0.25f, h * 0.6f); g.fillStyle = "#0f8a6c"; g.fillRect(0, h * 0.12f, w, 8); }
                if (kind == "pharmacy") { g.fillStyle = "#1f7a3a"; g.fillRect(w * 0.8f, h * 0.3f, 40, 12); g.fillRect(w * 0.8f + 14, h * 0.3f - 14, 12, 40); }
            }
            else if (kind == "snack")
            {
                g.fillStyle = "#c8312a"; g.fillRect(w * 0.05f, h * 0.12f, w * 0.5f, h * 0.2f); g.fillStyle = "#fff"; g.font = $"700 {F(h * 0.1f)}px {Kit.FONT}"; g.fillText("떡볶이 · 김밥 · 순대", w * 0.08f, h * 0.25f);
                g.fillStyle = "#8a8f94"; g.fillRect(w * 0.6f, h * 0.45f, w * 0.35f, h * 0.33f); for (int k = 0; k < 4; k++) { g.fillStyle = "#c8312a"; g.fillRect(w * 0.08f + k * 60, h * 0.6f, 30, h * 0.18f); }
            }
            else if (kind == "cafe")
            {
                for (int k = 0; k < 5; k++) { g.fillStyle = "#8a5a3a"; g.fillRect(30 + k * 95, h * 0.58f, 60, 8); g.fillRect(56 + k * 95, h * 0.58f, 8, h * 0.2f); }
                g.fillStyle = "#2d2a26"; g.fillRect(w * 0.7f, h * 0.35f, w * 0.28f, h * 0.43f); g.fillStyle = "#f3e3c3"; g.fillRect(w * 0.72f, h * 0.12f, w * 0.24f, h * 0.16f);
            }
            else if (kind == "lobby")
            {
                g.fillStyle = "#9a8f80"; g.fillRect(w * 0.08f, h * 0.25f, w * 0.35f, h * 0.4f);
                for (int y = 0; y < 5; y++) for (int x = 0; x < 6; x++) { g.fillStyle = "#c9c2b4"; g.fillRect(w * 0.09f + x * w * 0.057f, h * 0.27f + y * h * 0.075f, w * 0.05f, h * 0.06f); }
                g.fillStyle = "#6a7a8a"; g.fillRect(w * 0.6f, h * 0.2f, w * 0.18f, h * 0.58f);
            }
            else
            {
                g.fillStyle = "#7a8a94"; g.fillRect(w * 0.1f, h * 0.45f, w * 0.5f, h * 0.33f); g.fillStyle = "#2b4c7e"; g.fillRect(w * 0.15f, h * 0.15f, w * 0.3f, h * 0.12f);
                for (int k = 0; k < 6; k++) { g.fillStyle = "#4a5560"; g.fillRect(w * 0.65f + k * 25, h * 0.55f, 14, h * 0.23f); }
            }
            var glare = new PaintGradient(0, 0, w, h); glare.addColorStop(0, "rgba(255,255,255,0.18)"); glare.addColorStop(0.5f, "rgba(255,255,255,0)"); g.fillStyle = glare; g.fillRect(0, 0, w, h);
        }

        // Flag on a pole top, sways in the wind. k: 0 태극기, 1 city flag, 2 연무동.
        internal static GameObject Flag(Kit kit, int k, float x, float y, float z)
        {
            var map = kit.Memo($"town:flag:{k}", () => kit.Texture(kit.Canvas(256, 384, (g, CW, CH) =>
            {
                const float W = 3, H = 2; g.setTransform(0, -CH / W, CW / H, 0, 0, CH); // flag coords (W x H, hoist at fx 0) -> rotated canvas
                g.fillStyle = k == 0 ? "#ffffff" : k == 1 ? "#1d4fa0" : "#2f7a4a"; g.fillRect(0, 0, W, H);
                if (k == 0)
                {
                    float cx = 1.5f, cy = 1, r = 0.5f, PI = Mathf.PI;
                    g.fillStyle = "#cd2e3a"; g.beginPath(); g.arc(cx, cy, r, PI, 0); g.arc(cx + r / 2, cy, r / 2, 0, PI, true); g.arc(cx - r / 2, cy, r / 2, 0, PI); g.fill();
                    g.fillStyle = "#0047a0"; g.beginPath(); g.arc(cx, cy, r, 0, PI); g.arc(cx - r / 2, cy, r / 2, PI, 0, true); g.arc(cx + r / 2, cy, r / 2, PI, 0); g.fill();
                    g.fillStyle = "#000000";
                    void Tri(float tx, float ty, float ang, int[] pat)
                    {
                        g.save(); g.translate(tx, ty); g.rotate(ang);
                        for (int i = 0; i < pat.Length; i++) { float yy = -0.12f + i * 0.12f; if (pat[i] != 0) g.fillRect(-0.25f, yy, 0.5f, 0.08f); else { g.fillRect(-0.25f, yy, 0.21f, 0.08f); g.fillRect(0.04f, yy, 0.21f, 0.08f); } }
                        g.restore();
                    }
                    Tri(0.62f, 0.42f, -PI / 3.3f, new[] { 1, 1, 1 }); Tri(2.38f, 1.58f, -PI / 3.3f, new[] { 0, 0, 0 }); Tri(2.38f, 0.42f, PI / 3.3f, new[] { 0, 1, 0 }); Tri(0.62f, 1.58f, PI / 3.3f, new[] { 1, 0, 1 });
                }
                else
                {
                    g.fillStyle = "#ffffff"; g.beginPath(); g.arc(1.5f, 1, 0.42f, 0, 7); g.fill();
                    // JS draws '800 0.3px' text in flag units; Paint lays glyphs out in whole pixels, so the same
                    // transform is scaled by 1/100 and the text drawn at 30px (identical placement).
                    g.setTransform(0, -CH / W / 100, CW / H / 100, 0, 0, CH);
                    g.fillStyle = k == 1 ? "#1d4fa0" : "#2f7a4a"; g.font = $"800 30px {Kit.FONT}"; g.textAlign = "center"; g.textBaseline = "middle"; g.fillText(k == 1 ? "시" : "연무", 150, 102);
                }
            })));
            var m = Mat.Lit(new MatSpec { map = map, side = "double", roughness = 0.9f });
            Sway(m, 0.09f, 3.1f, 0);
            var mesh = Obj(Geo.Plane(0.9f, 1.35f, 4, 10).Translate(0, 0.675f, 0), m, true, false);
            J.SetRot(mesh, 0, 0, -Mathf.PI / 2); J.SetPos(mesh, x + 0.05f, y - 0.45f, z);
            return mesh;
        }
    }
}
