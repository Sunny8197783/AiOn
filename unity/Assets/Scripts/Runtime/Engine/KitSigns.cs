// kit.js facades (one texture per building face with night window glow) and signs (text panels, the 2023
// civil-defence shelter sign, the shelter guide sign).
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed partial class Kit
    {
        internal static float JsRound(float v) => Mathf.Floor(v + 0.5f);
        internal static string JsNum(float v) => v.ToString(CultureInfo.InvariantCulture);

        static readonly Dictionary<string, string> WALLS = new Dictionary<string, string>
        { ["apartment"] = "#e8e3d8", ["office"] = "#8d99a3", ["shop"] = "#d9d2c4", ["villa"] = "#a8604a", ["civic"] = "#cfcac0" };

        // ---- Facades: kind 'apartment' | 'office' | 'shop' | 'villa' | 'civic'; registered in light group 'windows'. ----
        public Material Facade(string kind, FacadeOpts o = null)
        {
            o = o ?? new FacadeOpts();
            int floors = o.floors, cols = o.cols, seed = o.seed, w = o.w, h = o.h; string label = o.label ?? ""; float litShare = o.lit;
            return Memo($"facade:{kind}:{floors}:{cols}:{seed}:{label}:{JsNum(litShare)}", () =>
            {
                var r = Rng(unchecked((uint)(seed * 7919 + kind.Length)));
                float fh = (float)h / floors, cw = (float)w / cols;
                var glowCells = new List<(float x, float y, float w, float h, bool on)>();
                var color = Canvas(w, h, (g, _, __) =>
                {
                    g.fillStyle = WALLS.TryGetValue(kind, out var wall) ? wall : "#ddd"; g.fillRect(0, 0, w, h);
                    for (int i = 0; i < 2000; i++) { g.fillStyle = Rgba(0, 0, 0, r() * 0.04f); g.fillRect(r() * w, r() * h, 2 + r() * 6, 2 + r() * 6); }
                    for (int f = 0; f < floors; f++)
                    {
                        float y = f * fh;
                        if (kind == "apartment")
                        {
                            g.fillStyle = "rgba(0,0,0,0.08)"; g.fillRect(0, y + fh - 4, w, 4);
                            for (int c = 0; c < cols; c++)
                            {
                                float x = c * cw; bool big = c % 2 == 0;
                                float wx = x + cw * 0.08f, wy = y + fh * 0.18f, ww = cw * (big ? 0.84f : 0.5f), wh = fh * (big ? 0.68f : 0.42f);
                                g.fillStyle = "#c9c3b6"; g.fillRect(wx - 3, wy - 3, ww + 6, wh + 6);
                                bool on = r() < litShare; glowCells.Add((wx, wy, ww, wh, on));
                                g.fillStyle = on ? "#3c4650" : "#26313b"; g.fillRect(wx, wy, ww, wh);
                                g.fillStyle = "rgba(180,205,220,0.18)"; g.fillRect(wx, wy, ww * 0.45f, wh);
                                g.fillStyle = "#d8d3c8"; g.fillRect(wx + ww / 2 - 1.5f, wy, 3, wh); g.fillRect(wx, wy + wh * 0.62f, ww, 3);
                                if (big) { g.fillStyle = "#f2efe8"; g.fillRect(wx - 2, wy + wh * 0.72f, ww + 4, wh * 0.28f + 3); }
                                else if (r() < 0.6f)
                                {
                                    g.fillStyle = "#b8b5ae"; g.fillRect(wx + ww + cw * 0.06f, wy + wh * 0.4f, cw * 0.28f, fh * 0.32f); g.strokeStyle = "#8a877f";
                                    for (int k = 0; k < 6; k++) { g.beginPath(); g.moveTo(wx + ww + cw * 0.07f, wy + wh * 0.42f + k * fh * 0.05f); g.lineTo(wx + ww + cw * 0.33f, wy + wh * 0.42f + k * fh * 0.05f); g.stroke(); }
                                }
                            }
                        }
                        else if (kind == "office")
                        {
                            g.fillStyle = "#5f6b75"; g.fillRect(0, y + fh - 6, w, 6);
                            for (int c = 0; c < cols; c++)
                            {
                                float wx = c * cw + 3, wy = y + 6, ww = cw - 6, wh = fh - 14; bool on = r() < litShare; glowCells.Add((wx, wy, ww, wh, on));
                                g.fillStyle = on ? "#2f4250" : "#1e2b35"; g.fillRect(wx, wy, ww, wh); g.fillStyle = "rgba(190,215,230,0.15)"; g.fillRect(wx, wy, ww, wh * 0.4f);
                            }
                        }
                        else if (kind == "villa" || kind == "civic")
                        {
                            for (int c = 0; c < cols; c++)
                            {
                                float wx = c * cw + cw * 0.25f, wy = y + fh * 0.25f, ww = cw * 0.5f, wh = fh * 0.45f;
                                g.fillStyle = "#e9e6df"; g.fillRect(wx - 4, wy - 4, ww + 8, wh + 8);
                                bool on = r() < litShare; glowCells.Add((wx, wy, ww, wh, on));
                                g.fillStyle = on ? "#3a4652" : "#25303a"; g.fillRect(wx, wy, ww, wh); g.fillStyle = "#e9e6df"; g.fillRect(wx + ww / 2 - 2, wy, 4, wh);
                            }
                        }
                        else
                        { // shop: upper floors windows, ground floor storefront drawn by level
                            if (f < floors - 1)
                                for (int c = 0; c < cols; c++)
                                {
                                    float wx = c * cw + cw * 0.15f, wy = y + fh * 0.2f, ww = cw * 0.7f, wh = fh * 0.55f; bool on = r() < litShare; glowCells.Add((wx, wy, ww, wh, on));
                                    g.fillStyle = on ? "#3a4652" : "#25303a"; g.fillRect(wx, wy, ww, wh);
                                }
                            else { g.fillStyle = "#2a3238"; g.fillRect(cw * 0.1f, y + fh * 0.15f, w - cw * 0.2f, fh * 0.8f); glowCells.Add((cw * 0.1f, y + fh * 0.15f, w - cw * 0.2f, fh * 0.8f, true)); }
                        }
                    }
                    if (label.Length > 0) { g.fillStyle = "#5b6770"; g.font = $"700 {JsNum(JsRound(h * 0.09f))}px {FONT}"; g.textAlign = "center"; g.fillText(label, w * 0.5f, h * 0.12f); }
                });
                var emissive = Canvas(w, h, (g, _, __) =>
                {
                    g.fillStyle = "#000"; g.fillRect(0, 0, w, h);
                    foreach (var cell in glowCells) if (cell.on) { bool warm = r() < 0.75f; g.fillStyle = warm ? "#ffcf8a" : "#d8ecff"; g.globalAlpha = 0.55f + r() * 0.45f; g.fillRect(cell.x, cell.y, cell.w, cell.h); }
                    g.globalAlpha = 1;
                });
                var m = Gyeongbo.Engine.Mat.Lit(new MatSpec { map = Texture(color), emissiveMap = Texture(emissive), emissive = Color.white, emissiveIntensity = 0, roughness = 0.75f, metalness = 0.05f });
                Lit("windows", m, 1.1f);
                return m;
            });
        }

        // ---- Signs ----
        // lines: string[]; returns a texture. Text auto-fits the width.
        public Texture2D SignTexture(IList<string> lines, SignStyle style = null)
        {
            var st = style ?? new SignStyle();
            int w = st.w, h = st.h; string bg = st.bg, fg = st.fg, border = st.border, align = st.align; int weight = st.weight; float pad = st.pad; var sizes = st.sizes;
            return Memo($"sign:{string.Join("|", lines)}:{w}:{h}:{bg}:{fg}:{border ?? "null"}:{align}", () => Texture(Canvas(w, h, (g, _, __) =>
            {
                g.fillStyle = bg; g.fillRect(0, 0, w, h);
                if (border != null) { g.strokeStyle = border; g.lineWidth = h * 0.04f; g.strokeRect(g.lineWidth / 2, g.lineWidth / 2, w - g.lineWidth, h - g.lineWidth); }
                float lh = h * (1 - pad * 2) / lines.Count;
                g.fillStyle = fg; g.textAlign = align; g.textBaseline = "middle";
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    float size = sizes != null && i < sizes.Length ? sizes[i] : lh * 0.72f;
                    g.font = $"{weight} {JsNum(size)}px {FONT}";
                    while (g.measureText(line).width > w * (1 - pad * 2) && size > 8) { size -= 2; g.font = $"{weight} {JsNum(size)}px {FONT}"; }
                    g.fillText(line, align == "left" ? w * pad : align == "right" ? w * (1 - pad) : w / 2f, h * pad + lh * (i + 0.5f));
                }
            })));
        }

        // A flat sign panel (plane, both sides unless single). Mesh origin at panel centre.
        public GameObject Sign(IList<string> lines, SignOpts o = null)
        {
            o = o ?? new SignOpts();
            var map = SignTexture(lines, new SignStyle { w = 512, h = Math.Max(64, (int)JsRound(512 * o.h / o.w)), weight = o.weight, bg = o.bg, fg = o.fg, border = o.border, align = o.align, pad = o.pad, sizes = o.sizes });
            bool em = o.emissive != 0;
            var m = Gyeongbo.Engine.Mat.Lit(new MatSpec { map = map, roughness = 0.6f, emissive = em ? Color.white : Color.black, emissiveMap = em ? map : null, emissiveIntensity = o.emissive, side = o.single ? "front" : "double" });
            return Geo.Obj(Geo.Plane(o.w, o.h), m, "sign");
        }

        // Official civil-defence shelter sign (2023 design): red field, white ring, green C·D ring,
        // orange disc with blue triangle, '대피소 / SHELTER' plus Chinese and Japanese. Ratio 45 x 60.
        public GameObject ShelterSign(float w = 0.45f)
        {
            var map = Memo("shelterSign", () => Texture(Canvas(450, 600, (g, _, __) =>
            {
                g.fillStyle = "#d7262d"; g.fillRect(0, 0, 450, 600);
                float cx = 225, cy = 205;
                g.fillStyle = "#ffffff"; g.beginPath(); g.arc(cx, cy, 165, 0, 7); g.fill();
                g.strokeStyle = "#1f9a4a"; g.lineWidth = 26; g.beginPath(); g.arc(cx, cy, 138, 0.35f, Mathf.PI - 0.35f); g.stroke(); g.beginPath(); g.arc(cx, cy, 138, Mathf.PI + 0.35f, Mathf.PI * 2 - 0.35f); g.stroke();
                g.fillStyle = "#f39200"; g.beginPath(); g.arc(cx, cy, 108, 0, 7); g.fill();
                g.fillStyle = "#1d4fa0"; g.beginPath(); g.moveTo(cx, cy - 78); g.lineTo(cx + 68, cy + 42); g.lineTo(cx - 68, cy + 42); g.closePath(); g.fill();
                g.fillStyle = "#fffbe6"; g.textAlign = "center"; g.textBaseline = "middle";
                g.font = $"800 68px {FONT}"; g.fillText("대피소", cx, 425);
                g.font = $"700 40px {FONT}"; g.fillText("SHELTER", cx, 488);
                g.font = $"600 26px {FONT}"; g.fillText("应急避难场所 · 待避所", cx, 545);
            })));
            var m = Gyeongbo.Engine.Mat.Lit(new MatSpec { map = map, roughness = 0.5f, emissive = Color.white, emissiveMap = map, emissiveIntensity = 0.05f });
            Lit("signs", m, 0.25f);
            return Geo.Obj(Geo.Plane(w, w * 60 / 45), m, "shelterSign");
        }

        // Shelter guide sign: white field, small shelter mark, blue running figure, blue distance and arrow.
        // arrow: 'left' | 'right' | 'up'. Ratio 50 x 25.
        public GameObject GuideSign(string arrow = "right", float meters = 30, float w = 0.5f)
        {
            var map = Memo($"guideSign:{arrow}:{JsNum(meters)}", () => Texture(Canvas(500, 250, (g, _, __) =>
            {
                g.fillStyle = "#ffffff"; g.fillRect(0, 0, 500, 250);
                g.fillStyle = "#d7262d"; g.fillRect(24, 40, 96, 128);
                g.fillStyle = "#ffffff"; g.beginPath(); g.arc(72, 90, 34, 0, 7); g.fill();
                g.fillStyle = "#f39200"; g.beginPath(); g.arc(72, 90, 23, 0, 7); g.fill();
                g.fillStyle = "#1d4fa0"; g.beginPath(); g.moveTo(72, 74); g.lineTo(87, 100); g.lineTo(57, 100); g.closePath(); g.fill();
                g.strokeStyle = "#1d4fa0"; g.fillStyle = "#1d4fa0"; g.lineWidth = 13; g.lineCap = "round";
                float fx = arrow == "left" ? -1 : 1, px = 190;
                g.beginPath(); g.arc(px + 12 * fx, 52, 14, 0, 7); g.fill();
                g.beginPath(); g.moveTo(px + 6 * fx, 72); g.lineTo(px - 6 * fx, 128); g.stroke();
                g.beginPath(); g.moveTo(px - 6 * fx, 128); g.lineTo(px + 22 * fx, 160); g.lineTo(px + 18 * fx, 196); g.stroke();
                g.beginPath(); g.moveTo(px - 6 * fx, 128); g.lineTo(px - 32 * fx, 158); g.lineTo(px - 52 * fx, 160); g.stroke();
                g.beginPath(); g.moveTo(px - 30 * fx, 90); g.lineTo(px + 4 * fx, 80); g.lineTo(px + 34 * fx, 104); g.stroke();
                g.font = $"800 62px {FONT}"; g.textAlign = "center"; g.textBaseline = "middle"; g.fillText($"{JsNum(meters)}m", 330, 205);
                g.save(); g.translate(395, 92); g.rotate(arrow == "left" ? Mathf.PI : arrow == "up" ? -Mathf.PI / 2 : 0);
                g.beginPath(); g.moveTo(-70, -16); g.lineTo(20, -16); g.lineTo(20, -40); g.lineTo(72, 0); g.lineTo(20, 40); g.lineTo(20, 16); g.lineTo(-70, 16); g.closePath(); g.fill(); g.restore();
            })));
            var m = Gyeongbo.Engine.Mat.Lit(new MatSpec { map = map, roughness = 0.5f, emissive = Color.white, emissiveMap = map, emissiveIntensity = 0.05f, side = "double" });
            Lit("signs", m, 0.3f);
            return Geo.Obj(Geo.Plane(w, w / 2), m, "guideSign");
        }
    }
}
