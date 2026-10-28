// Ports ui/menu.js skyline(): the landing screen's animated skyline. Apartment slabs at dusk; windows go dark in a
// wave (blackout), emergency lights and stars come out, a siren's sound rings spread from a rooftop, then the town
// lights up again. ~18 s loop. Drawn with Paint (canvas 2D port) into a texture behind the landing screen.
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    static class MenuSkyline
    {
        sealed class Win { public float x, y, w, h, k, off; public string col; }
        sealed class Slab { public float x, y, w, h; public List<Win> win; public string label; public bool exit; }
        sealed class Layer { public List<Slab> list; public string color; }
        sealed class Star { public float x, y, r, k; }

        static readonly string[] WARM = { "#ffd58a", "#ffe3ad", "#ffc56b", "#f6e7c8", "#bcd7ff" };
        static float Round(float v) => Mathf.Floor(v + 0.5f);   // Math.round

        // Returns stop().
        internal static Action Start(VisualElement canvas)
        {
            uint rng = 0x9e3779b9;
            float Rand() { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; return (rng % 100000) / 100000f; }
            float W = 0, H = 0, dpr = 1;
            var layers = new List<Layer>(); var stars = new List<Star>(); var poles = new List<float>();
            Paint g = null; Texture2D tex = null;
            bool alive = true;

            Slab MakeSlab(float x, float y, float w, float h, int cols, int rows, float lit, float pad)
            {
                var win = new List<Win>();
                float cw = (w - pad * 2) / cols, rh = (h - pad * 2) / rows;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        if (Rand() > lit) continue;
                        var wn = new Win { x = x + pad + c * cw + cw * 0.18f, y = y + pad + r * rh + rh * 0.22f, w = cw * 0.64f, h = rh * 0.5f };
                        wn.col = WARM[(int)Mathf.Floor(Rand() * WARM.Length)]; wn.k = Rand(); wn.off = Rand();
                        win.Add(wn);
                    }
                return new Slab { x = x, y = y, w = w, h = h, win = win };
            }

            void Layout()
            {
                W = canvas.resolvedStyle.width; H = canvas.resolvedStyle.height;
                if (!(W > 1)) W = Screen.width; if (!(H > 1)) H = Screen.height;
                // ponytail: CPU canvas capped at 640 px wide (JS: devicePixelRatio up to 1.5); a shader skyline in the quality pass
                dpr = Mathf.Min(1, 640f / W);
                int pw = Mathf.Max(1, (int)Round(W * dpr)), ph = Mathf.Max(1, (int)Round(H * dpr));
                if (g == null || g.width != pw || g.height != ph)
                {
                    g = new Paint(pw, ph);
                    if (tex != null) UnityEngine.Object.Destroy(tex);
                    tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                    UiFx.BgImage(canvas, tex);
                }
                g.setTransform(dpr, 0, 0, dpr, 0, 0);
                rng = 0x9e3779b9;
                float ground = H * 0.86f;
                layers = new List<Layer>();
                // Far city: low boxes with sparse tiny windows.
                var far = new List<Slab>();
                for (float x = -20; x < W + 40;)
                {
                    float w = 30 + Rand() * 70, h = H * (0.08f + Rand() * 0.2f);
                    far.Add(MakeSlab(x, ground - h, w, h, (int)Mathf.Max(2, Round(w / 9)), (int)Round(h / 9), 0.25f, 2)); x += w + Rand() * 8;
                }
                // Apartment slabs (15 floors) with big building numbers.
                var mid = new List<Slab>(); int num = 101;
                for (float x = -40 + Rand() * 40; x < W + 60;)
                {
                    float w = 110 + Rand() * 90, h = Mathf.Min(H * 0.62f, H * (0.4f + Rand() * 0.16f)); int cols = (int)Round(w / 15);
                    var s = MakeSlab(x, ground - h, w, h, cols, 15, 0.55f, 4); s.label = (num++).ToString(); s.exit = true; mid.Add(s); x += w + 30 + Rand() * 70;
                }
                // Villas in front.
                var near = new List<Slab>();
                for (float x = -10; x < W + 40;)
                {
                    float w = 60 + Rand() * 60, h = H * (0.1f + Rand() * 0.07f);
                    near.Add(MakeSlab(x, ground - h, w, h, (int)Round(w / 16), 4, 0.5f, 5)); x += w + 6 + Rand() * 20;
                }
                layers = new List<Layer> { new Layer { list = far, color = "#141a28" }, new Layer { list = mid, color = "#0f1420" }, new Layer { list = near, color = "#0a0e16" } };
                stars = new List<Star>();
                for (int i = 0; i < 140; i++) { var st = new Star(); st.x = Rand() * W; st.y = Rand() * H * 0.55f; st.r = Rand() * 1.2f + 0.2f; st.k = Rand(); stars.Add(st); }
                poles = new List<float>();
                for (float x = 60 + Rand() * 80; x < W; x += 230 + Rand() * 80) poles.Add(x);
            }

            string Mix(string a, string b, float k)
            {
                int pa = Convert.ToInt32(a.Substring(1), 16), pb = Convert.ToInt32(b.Substring(1), 16);
                int C(int s) => (int)Round(((pa >> s) & 255) * (1 - k) + ((pb >> s) & 255) * k);
                return "rgb(" + C(16) + "," + C(8) + "," + C(0) + ")";
            }
            string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            void Frame(float now)
            {
                if (!alive || g == null) return;
                bool still = Dom.ReduceMotion();
                const float T = 18000;
                float p = still ? 0.2f : (now % T) / T;
                // dark: 0 lit -> 1 blackout
                float wave = p < 0.42f ? -1 : p < 0.52f ? (p - 0.42f) / 0.1f : p < 0.84f ? 2 : -2;
                float dark = p < 0.42f ? 0 : p < 0.52f ? (p - 0.42f) / 0.1f : p < 0.84f ? 1 : 1 - (p - 0.84f) / 0.16f;
                float ground = H * 0.86f;

                var sky = new PaintGradient(0, 0, 0, ground);
                sky.addColorStop(0, Mix("#0d1530", "#05070f", dark));
                sky.addColorStop(0.55f, Mix("#2b2a4a", "#0b1020", dark));
                sky.addColorStop(1, Mix("#8a4d3f", "#1a1626", dark));
                g.fillStyle = sky; g.fillRect(0, 0, W, H);

                foreach (var s in stars) { g.globalAlpha = (0.15f + dark * 0.85f) * (0.6f + 0.4f * Mathf.Sin(now / 900 + s.k * 20)); g.fillStyle = "#e8eefc"; g.fillRect(s.x, s.y, s.r, s.r); }
                g.globalAlpha = 1;
                // Moon with halo.
                float mx = W * 0.8f, my = H * 0.17f;
                var halo = new PaintGradient(mx, my, 4, mx, my, 90); halo.addColorStop(0, "rgba(240,236,220,.35)"); halo.addColorStop(1, "rgba(240,236,220,0)");
                g.fillStyle = halo; g.fillRect(mx - 90, my - 90, 180, 180);
                g.fillStyle = "#f1ecdd"; g.beginPath(); g.arc(mx, my, 15, 0, Mathf.PI * 2); g.fill();
                // City glow on the horizon fades in the blackout.
                var glow = new PaintGradient(0, ground - H * 0.3f, 0, ground);
                glow.addColorStop(0, "rgba(255,170,90,0)"); glow.addColorStop(1, "rgba(255,170,90," + F(0.22f * (1 - dark)) + ")");
                g.fillStyle = glow; g.fillRect(0, ground - H * 0.3f, W, H * 0.3f);

                for (int li = 0; li < layers.Count; li++)
                {
                    var L = layers[li];
                    foreach (var b in L.list)
                    {
                        g.fillStyle = L.color; g.fillRect(b.x, b.y, b.w, b.h);
                        if (li == 1) { g.fillStyle = "#161c2a"; g.fillRect(b.x + b.w * 0.42f, b.y - 10, b.w * 0.16f, 10); g.fillRect(b.x, b.y - 3, b.w, 3); }
                        foreach (var w in b.win)
                        {
                            bool on;
                            if (wave == -1) on = w.off > 0.04f || Mathf.Sin(now / 3000 + w.k * 50) > 0;
                            else if (wave == -2) on = w.k < (p - 0.84f) / 0.16f;
                            else if (wave == 2) on = false;
                            else on = (w.x / W) > wave;
                            if (on) { g.fillStyle = w.col; g.globalAlpha = li == 0 ? 0.55f : 0.92f; g.fillRect(w.x, w.y, w.w, w.h); }
                            else if (dark > 0.5f && w.k > 0.985f) { g.fillStyle = "#f3f6ff"; g.globalAlpha = 0.5f + 0.5f * Mathf.Sin(now / 400 + w.k * 99); g.fillRect(w.x + w.w * 0.3f, w.y, w.w * 0.3f, w.h * 0.6f); }
                        }
                        g.globalAlpha = 1;
                        if (b.label != null)
                        {
                            g.fillStyle = "rgba(220,226,236," + F(0.55f - dark * 0.35f) + ")"; g.font = "700 " + Round(b.w * 0.16f) + "px Pretendard, 'Noto Sans KR', 'Malgun Gothic', sans-serif";
                            g.textAlign = "center"; g.fillText(b.label, b.x + b.w / 2, b.y + b.w * 0.2f);
                            if (b.exit && dark > 0.2f) { g.fillStyle = "rgba(60,230,140," + F(dark) + ")"; for (int f = 1; f < 15; f += 2) g.fillRect(b.x + b.w - 7, b.y + (b.h / 15) * f + 4, 3, 2); }
                        }
                    }
                }
                // Siren rings from the tallest rooftop while the town is dark.
                if (dark > 0.6f && !still && layers[1].list.Count > 0)
                {
                    var top = layers[1].list[0];
                    foreach (var b in layers[1].list) if (b.y < top.y) top = b;
                    float sx = top.x + top.w / 2, sy = top.y - 14;
                    g.fillStyle = "#c9cfd8"; g.fillRect(sx - 2, sy, 4, 12); g.fillRect(sx - 7, sy - 6, 14, 6);
                    for (int i = 0; i < 3; i++)
                    {
                        float k = ((now / 2600) + i / 3f) % 1;
                        g.strokeStyle = "rgba(242,179,61," + F((1 - k) * 0.5f * dark) + ")"; g.lineWidth = 1.5f;
                        g.beginPath(); g.arc(sx, sy - 3, 10 + k * Mathf.Min(W, H) * 0.45f, Mathf.PI * 1.05f, Mathf.PI * 1.95f); g.stroke();
                    }
                }
                // Street: ground, poles with sagging wires, street lamps (off in the blackout), passing headlights.
                g.fillStyle = "#07090e"; g.fillRect(0, ground, W, H - ground);
                g.strokeStyle = "rgba(10,12,18,.95)"; g.lineWidth = 1.2f;
                for (int i = 0; i < poles.Count; i++)
                {
                    float x = poles[i];
                    g.fillStyle = "#05070b"; g.fillRect(x - 2.5f, ground - H * 0.3f, 5, H * 0.3f); g.fillRect(x - 18, ground - H * 0.28f, 36, 3);
                    if (i + 1 < poles.Count) foreach (var dy in new[] { 0f, 7f }) { float x2 = poles[i + 1], y = ground - H * 0.28f + dy; g.beginPath(); g.moveTo(x, y); g.quadraticCurveTo((x + x2) / 2, y + 22, x2, y); g.stroke(); }
                    if (dark < 0.5f)
                    {
                        var lamp = new PaintGradient(x + 16, ground - H * 0.27f, 1, x + 16, ground - H * 0.27f, 60);
                        lamp.addColorStop(0, "rgba(255,214,150," + F(0.7f * (1 - dark * 2)) + ")"); lamp.addColorStop(1, "rgba(255,214,150,0)");
                        g.fillStyle = lamp; g.fillRect(x - 44, ground - H * 0.27f - 60, 120, 120);
                    }
                }
                if (!still) for (int i = 0; i < 3; i++)
                {
                    float k = ((now / (7000 + i * 2300)) + i * 0.37f) % 1, x = i % 2 == 1 ? W * (1.1f - k * 1.2f) : W * (k * 1.2f - 0.1f), y = ground + (H - ground) * (0.35f + i * 0.18f);
                    var hl = new PaintGradient(x, y, 1, x, y, 26); hl.addColorStop(0, "rgba(255,244,214,.9)"); hl.addColorStop(1, "rgba(255,244,214,0)");
                    g.fillStyle = hl; g.fillRect(x - 26, y - 26, 52, 52);
                }
                g.Upload(tex);
            }

            float Now() => Time.realtimeSinceStartup * 1000;
            EventCallback<GeometryChangedEvent> onResize = _ => { Layout(); if (Dom.ReduceMotion()) Frame(Now()); };
            canvas.RegisterCallback(onResize);
            Layout();
            Frame(Now());
            // ponytail: ~20 fps redraw (JS requestAnimationFrame); reduced motion draws one still frame per layout
            var timer = canvas.schedule.Execute(() => { if (!Dom.ReduceMotion()) Frame(Now()); }).Every(50);
            return () =>
            {
                alive = false; timer.Pause(); canvas.UnregisterCallback(onResize);
                if (tex != null) UnityEngine.Object.Destroy(tex);
            };
        }
    }
}
