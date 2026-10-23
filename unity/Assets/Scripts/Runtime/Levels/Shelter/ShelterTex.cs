// Ports levels/shelter-tex.js: canvas textures for the basement car-park shelter — green epoxy floor, pillar
// zone labels, painted floor markings, exit signs (피난구·통로 유도등), anti-slip ramp concrete, tactile paving,
// notice sheets, roster, banner, hydrant, water packs and cartons; CellPlane picks one cell of an atlas.
using System;
using System.Collections.Generic;
using System.Globalization;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class ShelterZone
    {
        public string letter, color;
    }

    // shelterTextures(kit): every texture/material is memoised on the kit, as in JS.
    public sealed class ShelterTex
    {
        static readonly string FONT = Kit.FONT;
        readonly Kit kit;
        readonly int S;

        public ShelterTex(Kit kit)
        {
            this.kit = kit;
            S = kit.Quality == "low" ? 512 : 1024;
        }

        static string Rgba(string rgb, float a) => "rgba(" + rgb + "," + a.ToString("R", CultureInfo.InvariantCulture) + ")";

        // handwriting-like line
        static void Scribble(Paint g, Func<float> r, float x, float y, float w, string color = "rgba(40,44,52,0.55)", float lw = 2)
        {
            g.strokeStyle = color; g.lineWidth = lw; g.beginPath(); g.moveTo(x, y);
            for (float px = x; px < x + w; px += 6 + r() * 6) g.lineTo(px, y + (r() - 0.5f) * lw * 2.5f);
            g.stroke();
        }

        // ---- green epoxy floor: mottled coat, trowel arcs, worn lanes, tyre marks, oil stains (4 m tile) ----
        public Material FloorMaterial() => kit.Memo("sh:floor", () =>
        {
            var r = Kit.Rng(4021);
            var color = kit.Canvas(S, S, (g, w, h) =>
            {
                g.fillStyle = "#5b7867"; g.fillRect(0, 0, w, h);
                for (int i = 0; i < 26; i++) { g.fillStyle = Rgba(r() < 0.5f ? "40,60,50" : "150,175,160", 0.04f + r() * 0.06f); g.beginPath(); g.ellipse(r() * w, r() * h, 40 + r() * 160, 30 + r() * 110, r() * 3, 0, 7); g.fill(); }
                g.strokeStyle = "rgba(255,255,255,0.025)"; g.lineWidth = 6;
                for (int i = 0; i < 40; i++) { g.beginPath(); g.arc(r() * w, r() * h, 30 + r() * 90, r() * 6, r() * 6 + 1.2f); g.stroke(); }
                for (int i = 0; i < S * 14; i++) { g.fillStyle = r() < 0.5f ? "rgba(20,30,25,0.18)" : "rgba(220,235,225,0.12)"; g.fillRect(r() * w, r() * h, 1.2f, 1.2f); }
                for (int i = 0; i < 7; i++)
                { // tyre marks: paired dark arcs
                    float x = r() * w, y = r() * h, rad = 200 + r() * 500, a0 = r() * 6;
                    g.strokeStyle = Rgba("18,20,20", 0.05f + r() * 0.06f); g.lineWidth = 12 + r() * 8;
                    foreach (var off in new float[] { 0, 46 }) { g.beginPath(); g.arc(x, y, rad + off, a0, a0 + 0.6f + r() * 0.5f); g.stroke(); }
                }
                for (int i = 0; i < 5; i++)
                { // oil drips
                    g.fillStyle = Rgba("25,22,18", 0.12f + r() * 0.12f); g.beginPath(); g.ellipse(r() * w, r() * h, 8 + r() * 24, 6 + r() * 16, r() * 3, 0, 7); g.fill();
                }
                for (int i = 0; i < 18; i++) { g.fillStyle = "rgba(230,240,232,0.08)"; g.fillRect(r() * w, r() * h, 2 + r() * 40, 1 + r() * 2); } // scuffs
            });
            var height = kit.Canvas(S / 2, S / 2, (g, w, h) => { g.fillStyle = "#808080"; g.fillRect(0, 0, w, h); for (int i = 0; i < w * 20; i++) { g.fillStyle = r() < 0.5f ? "#868686" : "#7a7a7a"; g.fillRect(r() * w, r() * h, 1.5f, 1.5f); } });
            var rough = kit.Canvas(S / 2, S / 2, (g, w, h) =>
            {
                g.fillStyle = "#6a6a6a"; g.fillRect(0, 0, w, h);
                for (int i = 0; i < 30; i++) { g.fillStyle = Rgba(r() < 0.6f ? "200,200,200" : "70,70,70", 0.1f + r() * 0.25f); g.beginPath(); g.ellipse(r() * w, r() * h, 10 + r() * 60, 6 + r() * 30, r() * 3, 0, 7); g.fill(); }
            });
            return Mat.Lit(new MatSpec
            {
                map = kit.Texture(color), normalMap = kit.Texture(kit.NormalFrom(height, 1.2f), false),
                roughnessMap = kit.Texture(rough, false), roughness = 0.75f, metalness = 0, normalScale = new Vector2(0.35f, 0.35f),
                repeat = new Vector2(0.25f, 0.25f),
            });
        });

        // ---- concrete ramp with the round anti-slip grooves Korean car ramps have (1 m tile) ----
        public Material GrooveMaterial() => kit.Memo("sh:groove", () =>
        {
            const int n = 256; var r = Kit.Rng(77);
            var color = kit.Canvas(n, n, (g, w, h) =>
            {
                g.fillStyle = "#8f8f8a"; g.fillRect(0, 0, n, n);
                for (int i = 0; i < n * 30; i++) { g.fillStyle = r() < 0.5f ? "rgba(0,0,0,0.08)" : "rgba(255,255,255,0.06)"; g.fillRect(r() * n, r() * n, 1.3f, 1.3f); }
                g.strokeStyle = "rgba(40,40,38,0.55)"; g.lineWidth = 5;
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) { g.beginPath(); g.arc(32 + x * 64, 32 + y * 64, 22, 0, 7); g.stroke(); }
            });
            var height = kit.Canvas(n, n, (g, w, h) =>
            {
                g.fillStyle = "#909090"; g.fillRect(0, 0, n, n); g.strokeStyle = "#303030"; g.lineWidth = 5;
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) { g.beginPath(); g.arc(32 + x * 64, 32 + y * 64, 22, 0, 7); g.stroke(); }
            });
            return Mat.Lit(new MatSpec { map = kit.Texture(color), normalMap = kit.Texture(kit.NormalFrom(height, 3), false), roughness = 0.92f });
        });

        // ---- yellow tactile warning blocks (점형블록), 30 cm tiles with 6x6 domes ----
        public Material TactileMaterial() => kit.Memo("sh:tactile", () =>
        {
            const int n = 128;
            var color = kit.Canvas(n, n, (g, w, h) => { g.fillStyle = "#e8b818"; g.fillRect(0, 0, n, n); g.fillStyle = "rgba(0,0,0,0.25)"; g.fillRect(0, 0, n, 2); g.fillRect(0, 0, 2, n); for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++) { g.fillStyle = "rgba(255,240,170,0.5)"; g.beginPath(); g.arc(11 + x * 21, 11 + y * 21, 6, 0, 7); g.fill(); } });
            var height = kit.Canvas(n, n, (g, w, h) => { g.fillStyle = "#404040"; g.fillRect(0, 0, n, n); g.fillStyle = "#e0e0e0"; for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++) { g.beginPath(); g.arc(11 + x * 21, 11 + y * 21, 6.5f, 0, 7); g.fill(); } });
            return Mat.Lit(new MatSpec { map = kit.Texture(color), normalMap = kit.Texture(kit.NormalFrom(height, 5), false), roughness = 0.6f });
        });

        // ---- yellow/black chevrons for ramp walls, speed bumps and height bars (1 m tile) ----
        public Material HazardMaterial() => kit.Memo("sh:hazard", () => Mat.Lit(new MatSpec
        {
            map = kit.Texture(kit.Canvas(128, 128, (g, w, h) =>
            {
                g.fillStyle = "#e9b21c"; g.fillRect(0, 0, 128, 128); g.fillStyle = "#1d1d1d";
                for (int i = -2; i < 4; i++) { g.beginPath(); g.moveTo(i * 64, 128); g.lineTo(i * 64 + 32, 128); g.lineTo(i * 64 + 160, 0); g.lineTo(i * 64 + 128, 0); g.closePath(); g.fill(); }
            })), roughness = 0.55f,
        }));

        // ---- pillar labels: one atlas, 5 columns x 3 zone rows; each cell 'A1' + '지하1층' ----
        public Material PillarAtlas() => kit.Memo("sh:pillars", () =>
        {
            const int cw = 160, ch = 200;
            var c = kit.Canvas(cw * 5, ch * 3, (g, w, h) =>
            {
                for (int row = 0; row < ShelterTextures.ZONES.Count; row++)
                {
                    var z = ShelterTextures.ZONES[row];
                    for (int col = 0; col < 5; col++)
                    {
                        int x = col * cw, y = row * ch;
                        g.fillStyle = "#f4f2ec"; g.fillRect(x, y, cw, ch);
                        g.fillStyle = z.color; g.fillRect(x + 8, y + 8, cw - 16, ch - 58);
                        g.fillStyle = "#ffffff"; g.textAlign = "center"; g.textBaseline = "middle";
                        g.font = $"800 92px {FONT}"; g.fillText($"{z.letter}{col + 1}", x + cw / 2f, y + (ch - 50) / 2f + 4);
                        g.fillStyle = "#333"; g.font = $"700 30px {FONT}"; g.fillText("지하 1층", x + cw / 2f, y + ch - 26);
                    }
                }
            });
            return Mat.Lit(new MatSpec { map = kit.Texture(c), roughness = 0.6f });
        });
        // [u0, v0, u1, v1] of a pillar label cell.
        public static float[] PillarCell(int row, int col) => new[] { col / 5f, 1 - (row + 1) / 3f, (col + 1) / 5f, 1 - row / 3f };

        // ---- painted floor markings atlas (transparent): 0 arrow, 1 wheelchair, 2 '서행', 3 'B1', 4 '경차', 5 hatch ----
        public Material MarkAtlas() => kit.Memo("sh:marks", () =>
        {
            const int n = 256;
            var c = kit.Canvas(n * 6, n, (g, w, h) =>
            {
                g.clearRect(0, 0, n * 6, n);
                g.fillStyle = "#f1efe6"; // arrow
                g.beginPath(); g.moveTo(128, 14); g.lineTo(206, 110); g.lineTo(156, 110); g.lineTo(156, 242); g.lineTo(100, 242); g.lineTo(100, 110); g.lineTo(50, 110); g.closePath(); g.fill();
                g.fillStyle = "#2a64b8"; g.fillRect(n + 24, 24, 208, 208); // wheelchair bay symbol
                g.strokeStyle = "#ffffff"; g.fillStyle = "#ffffff"; g.lineWidth = 16; g.lineCap = "round";
                g.beginPath(); g.arc(n + 120, 62, 16, 0, 7); g.fill();
                g.beginPath(); g.moveTo(n + 118, 86); g.lineTo(n + 118, 150); g.lineTo(n + 176, 150); g.lineTo(n + 196, 196); g.stroke();
                g.beginPath(); g.moveTo(n + 118, 112); g.lineTo(n + 160, 112); g.stroke();
                g.lineWidth = 12; g.beginPath(); g.arc(n + 114, 168, 44, -1.1f, 3.9f); g.stroke();
                g.fillStyle = "#f1efe6"; g.textAlign = "center"; g.textBaseline = "middle";
                g.font = $"900 110px {FONT}"; g.fillText("서행", n * 2 + 128, 128);
                g.lineWidth = 12; g.strokeStyle = "#f1efe6"; g.beginPath(); g.arc(n * 3 + 128, 128, 104, 0, 7); g.stroke();
                g.font = $"900 120px {FONT}"; g.fillText("B1", n * 3 + 128, 134);
                g.fillStyle = "#4c8fd6"; g.font = $"900 104px {FONT}"; g.fillText("경차", n * 4 + 128, 128);
                g.save(); g.beginPath(); g.rect(n * 5, 0, n, n); g.clip(); g.fillStyle = "#e9c22a";
                for (int i = -4; i < 8; i++) { g.beginPath(); g.moveTo(n * 5 + i * 48, n); g.lineTo(n * 5 + i * 48 + 18, n); g.lineTo(n * 5 + i * 48 + 18 + n, 0); g.lineTo(n * 5 + i * 48 + n, 0); g.closePath(); g.fill(); }
                g.restore();
            });
            return Mat.Lit(new MatSpec { map = kit.Texture(c), transparent = true, roughness = 0.5f, depthWrite = false, polygonOffset = true, polygonOffsetFactor = -2, polygonOffsetUnits = -2 });
        });
        public static float[] MarkCell(int i) => new[] { i / 6f, 0, (i + 1) / 6f, 1 };

        // Running figure for exit pictograms. dir 1 = runs right.
        static void Runner(Paint g, float x, float y, float s, string color, float dir = 1)
        {
            g.strokeStyle = color; g.fillStyle = color; g.lineWidth = 11 * s; g.lineCap = "round"; g.lineJoin = "round";
            Vector2 P(float px, float py) => new Vector2(x + px * s * dir, y + py * s);
            void Line(params float[] pts)
            {
                g.beginPath(); var a = P(pts[0], pts[1]); g.moveTo(a.x, a.y);
                for (int i = 2; i < pts.Length; i += 2) { var b = P(pts[i], pts[i + 1]); g.lineTo(b.x, b.y); }
                g.stroke();
            }
            g.beginPath(); var head = P(14, -58); g.arc(head.x, head.y, 11 * s, 0, 7); g.fill();
            Line(6, -40, -6, 6); Line(-6, 6, 18, 30, 12, 58); Line(-6, 6, -26, 30, -46, 30);
            Line(-26, -22, 4, -34, 26, -12);
        }

        // 피난구유도등: green field, white figure running out through a door.
        public Texture2D ExitTex() => kit.Memo("sh:exit", () => kit.Texture(kit.Canvas(512, 256, (g, w, h) =>
        {
            g.fillStyle = "#0d8a43"; g.fillRect(0, 0, 512, 256);
            g.fillStyle = "#f5fff6"; g.fillRect(300, 44, 150, 176); g.fillStyle = "#0d8a43"; g.fillRect(318, 60, 114, 160);
            Runner(g, 220, 140, 1.45f, "#f5fff6", 1);
            g.fillStyle = "#f5fff6"; g.font = $"800 34px {FONT}"; g.textAlign = "center"; g.fillText("비상구", 376, 150);
        })));

        // 통로유도등: white field, green arrow and figure. dir 1 = right, -1 = left.
        public Texture2D AisleTex(int dir = 1) => kit.Memo($"sh:aisle:{dir}", () => kit.Texture(kit.Canvas(512, 192, (g, w, h) =>
        {
            g.fillStyle = "#f3f8f1"; g.fillRect(0, 0, 512, 192);
            g.strokeStyle = "#0d8a43"; g.lineWidth = 8; g.strokeRect(6, 6, 500, 180);
            Runner(g, 256 - dir * 60, 104, 1.05f, "#0d8a43", dir);
            g.fillStyle = "#0d8a43"; g.save(); g.translate(256 + dir * 120, 96); g.scale(dir, 1);
            g.beginPath(); g.moveTo(-70, -18); g.lineTo(10, -18); g.lineTo(10, -44); g.lineTo(70, 0); g.lineTo(10, 44); g.lineTo(10, 18); g.lineTo(-70, 18); g.closePath(); g.fill(); g.restore();
        })));

        // ---- notice board: header, pinned sheets; director text on the first sheet (no invented advice) ----
        // Redraws the shared board canvas and returns its (shared) texture; lines null/empty = the default sheets.
        public Texture2D DrawBoard(IList<string> lines)
        {
            var g = kit.Memo("sh:boardCanvas", () => kit.Canvas(1024, 640));
            var r = Kit.Rng(19);
            g.fillStyle = "#f6f6f2"; g.fillRect(0, 0, 1024, 640);
            g.fillStyle = "#1f4e8c"; g.fillRect(0, 0, 1024, 92);
            g.fillStyle = "#ffffff"; g.font = $"800 52px {FONT}"; g.textAlign = "left"; g.textBaseline = "middle"; g.fillText("대피소 알림", 34, 48);
            g.font = $"600 26px {FONT}"; g.textAlign = "right"; g.fillText("연무 생활문화센터 지하주차장", 990, 48);
            void Sheet(float x, float y, float w, float h, float rot, string head, IList<object> body)
            {
                g.save(); g.translate(x + w / 2, y + h / 2); g.rotate(rot); g.translate(-w / 2, -h / 2);
                g.fillStyle = "rgba(0,0,0,0.12)"; g.fillRect(5, 6, w, h); g.fillStyle = "#ffffff"; g.fillRect(0, 0, w, h);
                g.fillStyle = "#d7262d"; g.beginPath(); g.arc(w / 2, 12, 8, 0, 7); g.fill();
                g.fillStyle = "#20242a"; g.textAlign = "left"; g.textBaseline = "top";
                if (head != null) { g.font = $"800 30px {FONT}"; g.fillText(head, 22, 30); }
                float ty = head != null ? 80 : 34;
                foreach (var line in body)
                {
                    if (line is string s)
                    {
                        int size = 26; g.font = $"600 {size}px {FONT}";
                        while (g.measureText(s).width > w - 44 && size > 14) { size -= 2; g.font = $"600 {size}px {FONT}"; }
                        g.fillText(s, 22, ty); ty += size + 14;
                    }
                    else { Scribble(g, r, 22, ty + 10, Convert.ToSingle(line, CultureInfo.InvariantCulture), "rgba(60,64,72,0.45)", 3); ty += 28; }
                }
                g.restore();
            }
            bool has = lines != null && lines.Count > 0;
            var main = new List<object>();
            if (has) for (int i = 0; i < lines.Count && i < 9; i++) main.Add(lines[i]);
            else main.AddRange(new object[] { 180f, 260f, 220f, 240f });
            Sheet(40, 120, 520, 480, -0.01f, has ? null : "안내", main);
            Sheet(600, 120, 380, 230, 0.02f, "대피자 명부", new object[] { 300f, 260f, 280f });
            Sheet(610, 380, 360, 220, -0.015f, "구호물품", new object[] { 220f, 280f, 180f });
            var t = kit.Memo("sh:boardTex", () => kit.Texture(g));
            g.Upload(t);   // texture.needsUpdate = true
            return t;
        }

        // ---- clipboard roster sheet (명부): ruled table with handwritten rows ----
        public Texture2D RosterTex() => kit.Memo("sh:roster", () => kit.Texture(kit.Canvas(256, 340, (g, w, h) =>
        {
            var r = Kit.Rng(5);
            g.fillStyle = "#fbfbf7"; g.fillRect(0, 0, 256, 340);
            g.fillStyle = "#111"; g.font = $"800 24px {FONT}"; g.textAlign = "center"; g.fillText("대피자 명부", 128, 30);
            g.strokeStyle = "rgba(30,30,30,0.5)"; g.lineWidth = 1;
            for (int i = 0; i < 13; i++) { g.beginPath(); g.moveTo(12, 48 + i * 22); g.lineTo(244, 48 + i * 22); g.stroke(); }
            foreach (var x in new float[] { 12, 44, 120, 190, 244 }) { g.beginPath(); g.moveTo(x, 48); g.lineTo(x, 312); g.stroke(); }
            for (int i = 0; i < 8; i++) { Scribble(g, r, 50, 59 + i * 22, 50 + r() * 20, "rgba(20,40,120,0.75)", 1.6f); Scribble(g, r, 126, 59 + i * 22, 40 + r() * 20, "rgba(20,40,120,0.75)", 1.6f); }
        })));

        // ---- cloth banner ----
        public Texture2D BannerTex(string text) => kit.Memo($"sh:banner:{text}", () => kit.Texture(kit.Canvas(1536, 224, (g, w, h) =>
        {
            g.fillStyle = "#f7f5ee"; g.fillRect(0, 0, 1536, 224);
            g.fillStyle = "#1f4e8c"; g.fillRect(0, 0, 1536, 18); g.fillRect(0, 206, 1536, 18);
            g.fillStyle = "#d7262d"; g.fillRect(40, 44, 110, 140);
            g.fillStyle = "#fff"; g.beginPath(); g.arc(95, 100, 38, 0, 7); g.fill(); g.fillStyle = "#f39200"; g.beginPath(); g.arc(95, 100, 26, 0, 7); g.fill();
            g.fillStyle = "#1d4fa0"; g.beginPath(); g.moveTo(95, 82); g.lineTo(112, 112); g.lineTo(78, 112); g.closePath(); g.fill();
            g.fillStyle = "#16233a"; g.font = $"900 104px {FONT}"; g.textAlign = "left"; g.textBaseline = "middle";
            int size = 104; while (g.measureText(text).width > 1300 && size > 40) { size -= 4; g.font = $"900 {size}px {FONT}"; }
            g.fillText(text, 190, 118);
        })));

        // ---- hydrant cabinet face (옥내소화전함) ----
        public Texture2D HydrantTex() => kit.Memo("sh:hydrant", () => kit.Texture(kit.Canvas(256, 400, (g, w, h) =>
        {
            g.fillStyle = "#c8261f"; g.fillRect(0, 0, 256, 400);
            g.strokeStyle = "rgba(0,0,0,0.35)"; g.lineWidth = 6; g.strokeRect(14, 14, 228, 372);
            g.fillStyle = "#ffffff"; g.textAlign = "center"; g.font = $"900 60px {FONT}"; g.fillText("소화전", 128, 120);
            g.font = $"700 26px {FONT}"; g.fillText("HYDRANT", 128, 160);
            g.fillStyle = "rgba(255,255,255,0.85)"; g.fillRect(40, 220, 176, 70); g.fillStyle = "#c8261f"; g.font = $"800 28px {FONT}"; g.fillText("발신기", 128, 266);
            g.fillStyle = "#222"; g.fillRect(214, 190, 14, 60);
        })));

        // ---- six-pack of 2 L bottles seen from the side (shrink wrap + labels) ----
        public Texture2D WaterPackTex() => kit.Memo("sh:waterpack", () => kit.Texture(kit.Canvas(256, 128, (g, w, h) =>
        {
            g.fillStyle = "#a9c9de"; g.fillRect(0, 0, 256, 128);
            for (int i = 0; i < 3; i++)
            {
                int x = i * 85;
                g.fillStyle = "rgba(255,255,255,0.35)"; g.fillRect(x + 10, 0, 18, 128);
                g.fillStyle = "#1e5fb0"; g.fillRect(x + 4, 52, 78, 38);
                g.fillStyle = "#ffffff"; g.font = $"800 20px {FONT}"; g.textAlign = "center"; g.fillText("생수 2L", x + 43, 77);
                g.fillStyle = "#2b7bd0"; g.fillRect(x + 30, 0, 26, 8);
            }
        })));

        public Texture2D CartonTex(string label) => kit.Memo($"sh:carton:{label}", () => kit.Texture(kit.Canvas(256, 160, (g, w, h) =>
        {
            g.fillStyle = "#b48a5a"; g.fillRect(0, 0, 256, 160);
            g.fillStyle = "rgba(0,0,0,0.08)"; g.fillRect(0, 70, 256, 18);
            g.fillStyle = "#2b2b2b"; g.font = $"800 34px {FONT}"; g.textAlign = "center"; g.fillText(label, 128, 62);
            g.font = $"600 20px {FONT}"; g.fillText("구호물품 · 행정복지센터", 128, 130);
        })));
    }

    public static class ShelterTextures
    {
        public static readonly List<ShelterZone> ZONES = new List<ShelterZone>
        {
            new ShelterZone { letter = "A", color = "#2f8f5b" },  // north pillar row
            new ShelterZone { letter = "B", color = "#d99a1e" },  // middle row
            new ShelterZone { letter = "C", color = "#2f6db5" },  // south row
        };

        public static ShelterTex Build(Kit kit) => new ShelterTex(kit);

        // A w x h plane whose UVs cover one atlas cell [u0, v0, u1, v1] (PillarCell / MarkCell).
        public static GeoData CellPlane(float w, float h, float[] cell)
        {
            var geo = Geo.Plane(w, h);
            for (int i = 0; i < geo.uv.Count; i++)
            {
                var uv = geo.uv[i];
                geo.uv[i] = new Vector2(cell[0] + uv.x * (cell[2] - cell[0]), cell[1] + uv.y * (cell[3] - cell[1]));
            }
            return geo;
        }
    }
}
