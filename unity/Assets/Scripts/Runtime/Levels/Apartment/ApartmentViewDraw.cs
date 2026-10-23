// Ports levels/apartment-view.js (data + canvases): the four window views (VIEWS), the far ring of sky, hills and city
// (drawFar) and the corridor face of a 15-floor 복도식 block (drawFacade), redrawn whenever the view changes.
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // One window view. Numbers used as flags (lamps, moon, beacon, exit) are 0 | 1 like the JS; unset colours are null.
    sealed class AptViewSpec
    {
        public float tint, far, fac, lamps, win, stars, moon, glow, beacon, kitchen, corridor, stair, exit, dots;
        public string cap, bFar, bNear, ground, dayWin, lit, lit2, wall, recess, parapet, rail, core, door, winC, litWin, lamp;
        public string[] sky, hill;
    }

    // The far ring's random layout (stars, ridge lines, buildings, the low buildings with a neon cross).
    sealed class AptViewLayout
    {
        public sealed class Star { public float x, y, s, a; }
        public sealed class Bldg { public float x, w, h; public bool far; }
        public readonly List<Star> stars = new List<Star>();
        public readonly List<double[]> ridges = new List<double[]>();
        public List<Bldg> bldgs = new List<Bldg>();
        public readonly List<Bldg> crosses = new List<Bldg>();
    }

    static class ApartmentViewDraw
    {
        public static readonly string[] NAMES = { "day", "dusk", "night", "blackout" };
        public static readonly Dictionary<string, AptViewSpec> VIEWS = new Dictionary<string, AptViewSpec>
        {
            ["day"] = new AptViewSpec
            {
                tint = 0.95f, far = 1, fac = 1, lamps = 0, cap = "#4f86cf", sky = new[] { "#4f86cf", "#9cc2e6", "#dde8ee" }, hill = new[] { "#9aabb9", "#b2bec7" }, bFar = "#c3c9cd", bNear = "#adb3b6", ground = "#8d9188",
                win = 0, dayWin = "#7f8f9a", stars = 0, moon = 0, glow = 0, beacon = 0,
                wall = "#e4dfd4", recess = "#9b978f", parapet = "#e9e4d9", rail = "#7d7a73", core = "#d9d4c9", door = "#6a5d52", winC = "#73828d", kitchen = 0, corridor = 0, stair = 0, exit = 0, dots = 0,
            },
            ["dusk"] = new AptViewSpec
            {
                tint = 0.5f, far = 1.1f, fac = 1.15f, lamps = 1, cap = "#1f2550", sky = new[] { "#1f2550", "#7a5878", "#f0955a" }, hill = new[] { "#3b3150", "#4e3d58" }, bFar = "#55485e", bNear = "#3f3648", ground = "#2c2630",
                win = 0.35f, lit = "#ffc98a", lit2 = "#dbe6ff", stars = 0.04f, moon = 0, glow = 0.12f, beacon = 1,
                wall = "#5a5258", recess = "#2e2a30", parapet = "#605860", rail = "#2a272b", core = "#544d54", door = "#241f22", winC = "#2a2e38", litWin = "#ffcf96", kitchen = 0.4f, corridor = 0.9f, lamp = "#fff3d6", stair = 0.3f, exit = 0, dots = 0,
            },
            ["night"] = new AptViewSpec
            {
                tint = 0.15f, far = 1.45f, fac = 1.6f, lamps = 1, cap = "#03050c", sky = new[] { "#03050c", "#0b1020", "#2a2430" }, hill = new[] { "#07090f", "#0b0d14" }, bFar = "#10131c", bNear = "#0b0d14", ground = "#07080b",
                win = 0.6f, lit = "#ffd9a0", lit2 = "#dbe8ff", stars = 0.15f, moon = 1, glow = 0.22f, beacon = 1,
                wall = "#15161b", recess = "#0b0b0e", parapet = "#1c1c20", rail = "#0a0a0c", core = "#131418", door = "#0a090a", winC = "#0c0e12", litWin = "#ffd39a", kitchen = 0.55f, corridor = 1, lamp = "#fff6e2", stair = 0.25f, exit = 0, dots = 0,
            },
            ["blackout"] = new AptViewSpec
            {
                tint = 0.07f, far = 1.3f, fac = 1.4f, lamps = 0, cap = "#010208", sky = new[] { "#010208", "#050a18", "#0b1324" }, hill = new[] { "#04060c", "#060912" }, bFar = "#080b14", bNear = "#05070d", ground = "#030406",
                win = 0.012f, lit = "#ff9a4a", lit2 = "#e8eeff", stars = 1, moon = 1, glow = 0, beacon = 0,
                wall = "#0c0f16", recess = "#06070a", parapet = "#10131b", rail = "#050609", core = "#0b0d13", door = "#050506", winC = "#07080b", litWin = "#ff9a4a", kitchen = 0.02f, corridor = 0, stair = 0, exit = 1, dots = 0.05f,
            },
        };

        // GLSL-style hash in doubles (the JS numbers; float would change every window).
        public static double Hash(double x, double y) { double s = Math.Sin(x * 12.9898 + y * 78.233) * 43758.5453; return s - Math.Floor(s); }

        static PaintStyle Style(string css) => css == null ? null : (PaintStyle)css;

        public static void DrawFar(Paint c, float w, float h, AptViewSpec V, AptViewLayout L)
        {
            const float H0 = 470;
            var gr = new PaintGradient(0, 0, 0, H0); gr.addColorStop(0, V.sky[0]); gr.addColorStop(0.62f, V.sky[1]); gr.addColorStop(1, V.sky[2]);
            c.fillStyle = gr; c.fillRect(0, 0, w, h);
            int n = ApartmentParts.JsRound(L.stars.Count * (double)V.stars);
            for (int i = 0; i < n; i++) { var s = L.stars[i]; c.fillStyle = new Color(235 / 255f, 240 / 255f, 1f, s.a); c.fillRect(s.x, s.y, s.s, s.s); }
            if (V.moon != 0)
            {
                const float mx = 1250, my = 300; // azimuth ~140° like the engine's moon, low enough to show through windows
                var g = new PaintGradient(mx, my, 4, mx, my, 60); g.addColorStop(0, "rgba(220,230,255,0.35)"); g.addColorStop(1, "rgba(220,230,255,0)");
                c.fillStyle = g; c.fillRect(mx - 60, my - 60, 120, 120); c.fillStyle = "#f2f0e6"; c.beginPath(); c.arc(mx, my, 9, 0, 7); c.fill();
            }
            for (int i = 0; i < L.ridges.Count; i++)
            {
                var ridge = L.ridges[i];
                c.fillStyle = V.hill[i]; c.beginPath(); c.moveTo(0, H0);
                for (int x = 0; x < ridge.Length; x++) c.lineTo(x * 8, (float)(H0 - ridge[x]));
                c.lineTo(w, H0); c.closePath(); c.fill();
            }
            if (V.glow != 0) { var g = new PaintGradient(0, H0 - 90, 0, H0); g.addColorStop(0, "rgba(255,150,80,0)"); g.addColorStop(1, new Color(1, 150 / 255f, 80 / 255f, V.glow)); c.fillStyle = g; c.fillRect(0, H0 - 90, w, 90); }
            PaintStyle bFar = V.bFar, bNear = V.bNear, lit = Style(V.lit), lit2 = Style(V.lit2), dayWin = Style(V.dayWin);
            foreach (var b in L.bldgs)
            {
                float top = H0 - b.h;
                c.fillStyle = b.far ? bFar : bNear; c.fillRect(b.x, top, b.w, b.h + 4);
                for (double y = top + 4; y < H0 - 3; y += 6) for (double x = b.x + 2; x < b.x + b.w - 2; x += 4)
                {
                    double k = Hash(x, y);
                    if (k < V.win) { c.fillStyle = k < V.win * 0.2 ? lit2 : lit; c.fillRect((float)x, (float)y, 2, 3); }
                    else if (dayWin != null && k > 0.4) { c.fillStyle = dayWin; c.fillRect((float)x, (float)y, 2, 3); }
                }
                if (V.beacon != 0 && b.h > 80) { c.fillStyle = "#ff3a2a"; c.fillRect(b.x + b.w / 2 - 1.5f, top - 3, 3, 3); }
            }
            if (V.beacon != 0) foreach (var b in L.crosses) if (b != null) // red neon crosses on low buildings
            {
                float x = b.x + b.w / 2, y = H0 - b.h - 16; c.fillStyle = "rgba(255,40,40,0.25)"; c.fillRect(x - 7, y - 4, 14, 22); c.fillStyle = "#ff4a3a"; c.fillRect(x - 1, y, 2, 14); c.fillRect(x - 4, y + 4, 8, 2);
            }
            c.fillStyle = V.ground; c.fillRect(0, H0, w, h - H0);
        }

        // Corridor face of a 15-floor 복도식 block (64 m x 44.8 m at 16 px/m): parapets, doors and kitchen windows, two stair cores.
        public static void DrawFacade(Paint c, float w, float h, AptViewSpec V)
        {
            const float fh = 45; var cores = new[] { 256, 768 };
            PaintStyle litWin = Style(V.litWin), winC = V.winC, door = V.door, recess = V.recess, parapet = V.parapet, core = V.core;
            c.fillStyle = V.wall; c.fillRect(0, 0, w, h);
            c.fillStyle = parapet; c.fillRect(0, fh * 0.55f, w, fh * 0.45f);
            for (int f = 0; f < 15; f++)
            {
                float y = h - (f + 1) * fh;
                c.fillStyle = recess; c.fillRect(0, y, w, fh * 0.62f);
                for (int x = 14; x < w - 40; x += 64)
                {
                    bool atCore = false; foreach (var cx in cores) if (Math.Abs(cx - (x + 22)) < 46) atCore = true;
                    if (atCore) continue;
                    c.fillStyle = door; c.fillRect(x, y + 5, 12, fh * 0.57f);
                    c.fillStyle = Hash(x, f) < V.kitchen ? litWin : winC; c.fillRect(x + 22, y + 9, 24, 10);
                    if (V.dots != 0 && Hash(x + 5, f + 3) < V.dots) { c.fillStyle = Hash(x, f + 11) < 0.5 ? "#ffb060" : "#f4f6ff"; c.fillRect(x + 30, y + 12, 5, 4); }
                }
                if (V.corridor != 0) for (int x = 60; x < w; x += 128) if (Hash(x * 3, f * 7) < V.corridor)
                {
                    var g = new PaintGradient(x, y + 3, 1, x, y + 14, 34); g.addColorStop(0, "rgba(255,244,220,0.55)"); g.addColorStop(1, "rgba(255,244,220,0)");
                    c.fillStyle = g; c.fillRect(x - 36, y, 72, fh); c.fillStyle = V.lamp; c.fillRect(x - 6, y + 1, 12, 3);
                }
                c.fillStyle = parapet; c.fillRect(0, y + fh * 0.62f, w, fh * 0.38f); c.fillStyle = V.rail; c.fillRect(0, y + fh * 0.62f, w, 2);
                foreach (var cx in cores)
                {
                    c.fillStyle = core; c.fillRect(cx - 24, y, 48, fh);
                    c.fillStyle = Hash(cx, f) < V.stair ? litWin : winC; c.fillRect(cx - 12, y + 10, 24, 18);
                    if (V.exit != 0) { c.fillStyle = "#2bff7a"; c.fillRect(cx + 15, y + 6, 6, 3); }
                }
            }
            c.fillStyle = recess; c.fillRect(0, h - fh, w, fh * 0.7f); // ground-floor pilotis
            if (V.beacon != 0) foreach (var cx in cores) { c.fillStyle = "#ff3a2a"; c.fillRect(cx - 2, 2, 5, 4); }
        }
    }
}
