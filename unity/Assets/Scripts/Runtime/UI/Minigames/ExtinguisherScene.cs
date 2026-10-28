// ui/extinguisher.js canvas scene: corridor wall and floor, parcel boxes, flames and smoke, the extinguisher and
// its powder. Drawn on a Paint with the canvas calls of the JS, same numbers.
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.UI
{
    internal sealed class ExtState
    {
        public string phase, endKind;
        public float fireLv, danger, hold, sweepDir, shake;
        public int lastDir;
        public bool pinOut, aimed, spraying, holding, ended, closed;
        public readonly HashSet<string> done = new HashSet<string>();
        public List<ExtParticle> particles = new List<ExtParticle>();
    }

    internal sealed class ExtParticle { public float x, y, vx, vy, life, max, r; }

    internal static class ExtinguisherScene
    {
        public const float HOLD_S = 0.7f;
        const float PI = Mathf.PI;
        static readonly System.Random rnd = new System.Random();
        static float Rand() => (float)rnd.NextDouble();
        static float Nz(float a) => 0.5f + 0.5f * Mathf.Sin(a) * Mathf.Sin(a * 1.37f + 1.1f);
        static string A(float a) => Mg.Num(Math.Round(a, 4));
        static void RRect(Paint g, float x, float y, float w, float h, float r) { g.beginPath(); g.roundRect(x, y, w, h, r); }

        public static void Draw(Paint g, float t, float dt, ExtState S, bool rm, bool large)
        {
            int W = g.width, H = g.height;
            float floorY = H * 0.74f, ft = rm ? t * 0.25f : t;
            float fx = W * 0.76f, fy = floorY - 52, lv = S.fireLv, flick = rm ? 1 : 0.82f + 0.18f * Nz(ft * 9);
            g.save();
            if (S.shake > 0) g.translate(Mathf.Sin(t * 90) * S.shake * 6, 0);
            var gr = new PaintGradient(0, 0, 0, floorY); gr.addColorStop(0, "#303438"); gr.addColorStop(1, "#23262a");
            g.fillStyle = gr; g.fillRect(0, 0, W, floorY);
            g.strokeStyle = "rgba(255,255,255,.045)"; g.lineWidth = 2;
            for (float x = 120; x < W; x += 170) { g.beginPath(); g.moveTo(x, 0); g.lineTo(x, floorY); g.stroke(); }
            g.fillStyle = "#463c33"; g.fillRect(18, floorY - 236, 82, 236); g.fillStyle = "#5a4e42"; g.fillRect(25, floorY - 229, 68, 229);
            g.fillStyle = "#c9c2b4"; g.fillRect(80, floorY - 120, 8, 4); // door handle: the way out is behind you
            g.fillStyle = "#2e7d4f"; g.fillRect(34, floorY - 262, 50, 18); g.fillStyle = "#e9fff1"; g.font = "700 11px " + Kit.FONT; g.textAlign = "center"; g.fillText("비상구", 59, floorY - 249);
            gr = new PaintGradient(0, floorY, 0, H); gr.addColorStop(0, "#3b342d"); gr.addColorStop(1, "#1b1814");
            g.fillStyle = gr; g.fillRect(0, floorY, W, H - floorY);
            g.strokeStyle = "rgba(0,0,0,.35)"; g.lineWidth = 1;
            for (int i = 1; i < 6; i++) { float y = floorY + (H - floorY) * Mathf.Pow(i / 6f, 1.5f); g.beginPath(); g.moveTo(0, y); g.lineTo(W, y); g.stroke(); }
            for (int i = -6; i <= 6; i++) { g.beginPath(); g.moveTo(W / 2f + i * 40, floorY); g.lineTo(W / 2f + i * 150, H); g.stroke(); }
            g.fillStyle = "#18191a"; g.fillRect(0, floorY - 9, W, 9);
            if (lv > 0) // warm light thrown on wall and floor
            {
                var rg = new PaintGradient(fx, fy, 10, fx, fy, 150 + lv * 300);
                rg.addColorStop(0, $"rgba(255,150,60,{A(0.55f * lv * flick)})"); rg.addColorStop(1, "rgba(255,110,40,0)");
                g.globalCompositeOperation = "lighter"; g.fillStyle = rg; g.fillRect(0, 0, W, H); g.globalCompositeOperation = "source-over";
            }
            foreach (var (x, w, h, c, y0) in new[] { (fx - 78, 88f, 58f, "#a3774a", 0f), (fx + 6, 72f, 74f, "#b2834f", 0f), (fx - 46, 66f, 44f, "#986d43", 58f) })
            {
                float y = floorY - y0 - h;
                g.fillStyle = c; g.fillRect(x, y, w, h); g.fillStyle = "rgba(0,0,0,.18)"; g.fillRect(x + w * 0.7f, y, w * 0.3f, h);
                g.fillStyle = "rgba(210,190,150,.55)"; g.fillRect(x + w / 2 - 5, y, 10, h); // packing tape
                g.fillStyle = "#f2efe6"; g.fillRect(x + 8, y + h * 0.45f, 22, 13); // shipping label
            }
            if (S.done.Contains("sweep")) { g.fillStyle = "rgba(236,236,230,.75)"; g.beginPath(); g.ellipse(fx, floorY + 6, 120, 14, 0, 0, PI * 2); g.fill(); g.fillRect(fx - 80, fy - 30, 150, 8); }
            if (lv > 0) Flames(g, fx, fy, lv, ft, large, rm);
            if (lv > 0.04f)
            {
                float sh = 70 + lv * (large ? 210 : 140);
                var sm = new PaintGradient(0, 0, 0, sh);
                sm.addColorStop(0, $"rgba(18,18,20,{A(0.3f + lv * 0.5f)})"); sm.addColorStop(1, "rgba(18,18,20,0)");
                g.fillStyle = sm; g.fillRect(0, 0, W, sh);
            }
            var (tx, ty, ang) = DrawExt(g, 150, H - 20, S, ft, fx, fy);
            if (S.spraying && !rm || S.spraying && Rand() < 0.4f)
            {
                for (int i = 0; i < (rm ? 2 : 6); i++)
                {
                    float a = ang + (Rand() - 0.5f) * 0.35f, v = 260 + Rand() * 150;
                    S.particles.Add(new ExtParticle { x = tx, y = ty, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, life = 0, max = 0.55f + Rand() * 0.5f, r = 3 });
                }
            }
            if (S.particles.Count > 280) S.particles.RemoveRange(0, S.particles.Count - 280);
            foreach (var p in S.particles)
            {
                p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= 0.965f; p.vy = p.vy * 0.965f + 25 * dt; p.life += dt; p.r += 46 * dt;
                g.fillStyle = $"rgba(242,242,238,{A(Mathf.Max(0, 0.32f * (1 - p.life / p.max)))})";
                g.beginPath(); g.arc(p.x, p.y, p.r, 0, PI * 2); g.fill();
            }
            S.particles.RemoveAll(p => p.life >= p.max);
            g.restore();
        }

        static void Flames(Paint g, float cx, float bas, float lv, float t, bool large, bool rm)
        {
            float h = 46 + lv * (large ? 290 : 220), w = 34 + lv * 130; int n = 9;
            g.globalCompositeOperation = "lighter";
            for (int layer = 0; layer < 2; layer++)
            {
                for (int i = 0; i < n; i++)
                {
                    float u = (float)i / (n - 1) - 0.5f, k = 1 - Mathf.Abs(u) * 1.4f;
                    float hh = h * k * (0.62f + 0.38f * Nz(t * 6 + i * 1.9f)) * (layer == 1 ? 0.55f : 1), ww = (w / n) * (layer == 1 ? 0.9f : 1.6f);
                    float x = cx + u * w, sway = rm ? 0 : Mathf.Sin(t * 4 + i) * ww * 0.6f;
                    var grd = new PaintGradient(0, bas, 0, bas - hh);
                    grd.addColorStop(0, layer == 1 ? "rgba(255,250,215,.9)" : "rgba(255,170,60,.85)");
                    grd.addColorStop(0.5f, layer == 1 ? "rgba(255,210,95,.6)" : "rgba(255,98,28,.55)");
                    grd.addColorStop(1, "rgba(190,40,10,0)");
                    g.fillStyle = grd; g.beginPath(); g.moveTo(x - ww, bas);
                    g.quadraticCurveTo(x - ww * 0.9f, bas - hh * 0.55f, x + sway, bas - hh);
                    g.quadraticCurveTo(x + ww * 0.9f, bas - hh * 0.55f, x + ww, bas); g.closePath(); g.fill();
                }
            }
            if (!rm) for (int i = 0; i < 10 * lv; i++) // embers
            {
                float s = (t * 0.7f + i * 0.137f) % 1, ex = cx + Mathf.Sin(i * 7.3f + t) * w * 0.5f, ey = bas - s * h * 1.2f;
                g.fillStyle = $"rgba(255,190,90,{A(0.8f * (1 - s))})"; g.fillRect(ex, ey, 2.5f, 2.5f);
            }
            g.globalCompositeOperation = "source-over";
        }

        static (float x, float y, float ang) DrawExt(Paint g, float x, float y, ExtState S, float t, float tx, float ty)
        {
            float bw = 46, bh = 122, top = y - bh;
            g.fillStyle = "rgba(0,0,0,.4)"; g.beginPath(); g.ellipse(x, y + 2, 34, 6, 0, 0, PI * 2); g.fill();
            var gr = new PaintGradient(x - bw / 2, 0, x + bw / 2, 0);
            gr.addColorStop(0, "#7a110b"); gr.addColorStop(0.32f, "#e23a2c"); gr.addColorStop(0.5f, "#ff7a66"); gr.addColorStop(0.62f, "#c82b20"); gr.addColorStop(1, "#650e09");
            g.fillStyle = gr; RRect(g, x - bw / 2, top, bw, bh, 14); g.fill();
            g.fillStyle = "#f3f0e8"; g.fillRect(x - 17, top + 36, 34, 46);
            g.fillStyle = "#c42a1f"; g.font = "800 10px " + Kit.FONT; g.textAlign = "center";
            g.fillText("분말", x, top + 54); g.fillText("소화기", x, top + 68);
            g.fillStyle = "#2a2c2f"; g.fillRect(x - 9, top - 16, 18, 18);
            g.fillStyle = "#e8e8e4"; g.beginPath(); g.arc(x + 14, top - 6, 7, 0, PI * 2); g.fill();
            g.strokeStyle = "#2fa35a"; g.lineWidth = 2.5f; g.beginPath(); g.arc(x + 14, top - 6, 5, PI * 1.1f, PI * 1.6f); g.stroke();
            g.strokeStyle = "#222"; g.lineWidth = 1.2f; g.beginPath(); g.moveTo(x + 14, top - 6); g.lineTo(x + 11, top - 10); g.stroke(); // gauge needle in the green
            float sq = S.done.Contains("squeeze") ? 1 : Mathf.Min(1, S.hold / HOLD_S);
            g.strokeStyle = "#141516"; g.lineWidth = 6; g.lineCap = "round";
            g.beginPath(); g.moveTo(x - 6, top - 14); g.lineTo(x - 38, top - 18); g.stroke();
            g.beginPath(); g.moveTo(x - 6, top - 22); g.lineTo(x - 38, top - 38 + sq * 15); g.stroke();
            if (!S.pinOut)
            {
                g.strokeStyle = "#e9c23b"; g.lineWidth = 3; g.beginPath(); g.arc(x - 24, top - 31, 6, 0, PI * 2); g.stroke();
                g.strokeStyle = "#c9c9c9"; g.lineWidth = 2; g.beginPath(); g.moveTo(x - 18, top - 30); g.lineTo(x - 4, top - 25); g.stroke();
            }
            float tipX, tipY, ang;
            if (S.aimed) { tipX = x + 128; tipY = top + 4; ang = Mathf.Atan2(ty - tipY, tx - tipX) + S.sweepDir * 0.26f + (S.spraying ? Mathf.Sin(t * 3) * 0.03f : 0); }
            else { tipX = x + 34; tipY = y - 16; ang = PI * 0.45f; }
            g.strokeStyle = "#111214"; g.lineWidth = 7;
            g.beginPath(); g.moveTo(x + 8, top - 8); g.bezierCurveTo(x + 50, top + 6, tipX - 70, tipY + 44, tipX - 22 * Mathf.Cos(ang), tipY - 22 * Mathf.Sin(ang)); g.stroke();
            g.save(); g.translate(tipX, tipY); g.rotate(ang);
            g.fillStyle = "#1c1d20"; g.beginPath(); g.moveTo(-24, -5); g.lineTo(0, -9); g.lineTo(0, 9); g.lineTo(-24, 5); g.closePath(); g.fill();
            g.restore();
            return (tipX, tipY, ang);
        }
    }
}
