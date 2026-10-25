// ui/hud.js setCompass / setMap: the two HUD canvases, drawn with Paint and shown as background images.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public sealed partial class Hud
    {
        Label heading;
        Paint compassCv, mapCv;
        Texture2D compassTex, mapTex;

        static readonly Dictionary<int, string> NAMES = new Dictionary<int, string>
        {
            [0] = "북 N", [45] = "북동", [90] = "동 E", [135] = "남동", [180] = "남 S", [225] = "남서", [270] = "서 W", [315] = "북서",
        };
        static readonly string[] DIRS = { "북", "북동", "동", "남동", "남", "남서", "서", "북서" };

        static Texture2D Show(Paint p, ref Texture2D tex, VisualElement on)
        {
            if (tex == null)
            {
                tex = new Texture2D(p.width, p.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                on.style.backgroundImage = new StyleBackground(tex);
                on.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }
            p.Upload(tex);
            return tex;
        }

        // deg: heading clockwise from north (0 = N, 90 = E).
        public void SetCompass(float deg)
        {
            var g = compassCv ??= new Paint(360, 34);
            int W = g.width, H = g.height; const float PX = 2.2f;   // px per degree
            g.clearRect(0, 0, W, H);
            var bg = new PaintGradient(0, 0, W, 0);
            bg.addColorStop(0, "rgba(10,15,18,0)"); bg.addColorStop(0.15f, "rgba(10,15,18,.72)"); bg.addColorStop(0.85f, "rgba(10,15,18,.72)"); bg.addColorStop(1, "rgba(10,15,18,0)");
            g.fillStyle = bg; g.fillRect(0, 0, W, H);
            g.textAlign = "center"; g.textBaseline = "middle";
            int rd = (int)Math.Floor(deg + 0.5f);
            for (int d = -80; d <= 80; d++)
            {
                int a = rd + d; float x = W / 2f + (a - deg) * PX; int n = ((a % 360) + 360) % 360;
                if (NAMES.TryGetValue(n, out var name))
                {
                    g.fillStyle = n == 0 ? "#ff5a4a" : n % 90 == 0 ? "#f2b33d" : "#c9d3d6";
                    g.font = (n % 90 == 0 ? 800 : 600) + " " + (n % 90 == 0 ? 14 : 11) + "px Pretendard, 'Noto Sans KR', sans-serif";
                    g.fillText(name, x, H / 2f + 2);
                }
                else if (n % 15 == 0) { g.fillStyle = "rgba(220,230,232,.55)"; g.fillRect(x - 0.5f, H - 9, 1, 6); }
            }
            g.fillStyle = "#f2b33d"; g.beginPath(); g.moveTo(W / 2f - 5, 0); g.lineTo(W / 2f + 5, 0); g.lineTo(W / 2f, 6); g.fill();
            Show(g, ref compassTex, compassEl);
            var dir = DIRS[(int)Math.Floor((((deg % 360) + 360) % 360) / 45 + 0.5f) % 8];
            heading.text = dir + "쪽을 보고 있음";
        }

        // m: { x, y, z, yaw, walk, solids, points, scale (px per metre) } or null — north up, the player at the centre.
        public void SetMap(HudMap m)
        {
            if (m == null) { UiStyle.Hidden(mapBox, true); return; }
            UiStyle.Hidden(mapBox, false);
            var g = mapCv ??= new Paint(184, 184);
            float S = g.width, C = S / 2, k = m.scale, R = C / k;
            float X(float x) => C + (x - m.x) * k;
            float Z(float z) => C + (z - m.z) * k;
            bool Near(RectXZ o) => o.x1 > m.x - R && o.x0 < m.x + R && o.z1 > m.z - R && o.z0 < m.z + R;
            g.save(); g.clearRect(0, 0, S, S);
            g.beginPath(); g.arc(C, C, C - 1, 0, Mathf.PI * 2); g.clip();
            g.fillStyle = "rgba(8,12,15,.82)"; g.fillRect(0, 0, S, S);
            g.fillStyle = "rgba(150,165,170,.32)";
            if (m.walk != null)
                foreach (var w in m.walk)
                {
                    if (w == null || !Near(w)) continue;
                    float y0 = w.ramp != null ? Mathf.Min(w.ramp.y0, w.ramp.y1) : w.y, y1 = w.ramp != null ? Mathf.Max(w.ramp.y0, w.ramp.y1) : y0;
                    if (y1 < m.y - 1.6f || y0 > m.y + 1.6f) continue;   // this floor only
                    g.fillRect(X(w.x0), Z(w.z0), (w.x1 - w.x0) * k, (w.z1 - w.z0) * k);
                }
            g.fillStyle = "rgba(205,190,160,.62)";
            if (m.solids != null)
                foreach (var s in m.solids)
                {
                    if (s == null || s.off || !Near(s) || s.y1 < m.y + 0.4f || s.y0 > m.y + 2) continue;
                    g.fillRect(X(s.x0), Z(s.z0), Mathf.Max(1, (s.x1 - s.x0) * k), Mathf.Max(1, (s.z1 - s.z0) * k));
                }
            if (m.points != null)
                foreach (var p in m.points)
                {
                    if (p == null || !p.enabled || Mathf.Abs((p.y ?? m.y) - m.y) > 2.2f) continue;
                    float px = X(p.x), pz = Z(p.z);
                    if (px < -4 || px > S + 4 || pz < -4 || pz > S + 4) continue;
                    bool big = p.kind == "shelter" || p.kind == "door";
                    g.fillStyle = p.kind == "shelter" ? "#ff6b5a" : p.kind == "door" ? "#3cc6b5" : "#f2b33d";
                    g.beginPath(); g.arc(px, pz, big ? 3.6f : 2.6f, 0, Mathf.PI * 2); g.fill();
                }
            g.translate(C, C); g.rotate(-m.yaw);   // the arrow points up (north) at yaw 0
            g.fillStyle = "#ffffff"; g.strokeStyle = "#0a0f12"; g.lineWidth = 1.5f;
            g.beginPath(); g.moveTo(0, -9); g.lineTo(6, 7); g.lineTo(0, 3); g.lineTo(-6, 7); g.closePath(); g.fill(); g.stroke();
            g.restore();
            g.strokeStyle = "rgba(255,255,255,.22)"; g.lineWidth = 2; g.beginPath(); g.arc(C, C, C - 1, 0, Mathf.PI * 2); g.stroke();
            g.font = "800 13px Pretendard, 'Noto Sans KR', sans-serif"; g.textAlign = "center"; g.textBaseline = "middle";
            foreach (var (t, x, y, c) in new[] { ("N", C, 11f, "#ff5a4a"), ("S", C, S - 11, "#e8eef0"), ("E", S - 11, C, "#e8eef0"), ("W", 11f, C, "#e8eef0") })
            {
                g.fillStyle = "rgba(8,12,15,.85)"; g.beginPath(); g.arc(x, y, 9, 0, Mathf.PI * 2); g.fill(); g.fillStyle = c; g.fillText(t, x, y + 1);
            }
            Show(g, ref mapTex, mapEl);
        }
    }
}
