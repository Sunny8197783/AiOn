// engine/sky.js meshes: the gradient sky + night dome, clouds, stars, moon, distant city with beacons, hill rings.
// Built in JS space (Geo) with the JS seeds and numbers; render order via the material queue (sky 1000, stars and
// moon 1001, clouds 1002: behind everything, as the JS renderOrder -5..-2 with depthWrite off).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed partial class Sky
    {
        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

        // ---- sky dome: day gradient with a sun glow, blended into the night dome (DOME_FS) by S.night ----
        void MakeSkyDome()
        {
            skyGeo = Geo.Sphere(1900, 48, 24);
            skyGeo.color = new List<Color>(skyGeo.pos.Count);
            for (int i = 0; i < skyGeo.pos.Count; i++) skyGeo.color.Add(Color.white);
            skyMat = Mat.Unlit(new MatSpec { vertexColors = true, side = "back", depthWrite = false, fog = false });
            skyMat.renderQueue = 1000;
            skyUnity = skyGeo.ToMesh("sky");
            skyMesh = Geo.Obj(skyUnity, skyMat, "sky", false, false);
        }

        void ColorSky(EnvState S)
        {
            Color fog = Lin(S.c["fogColor"]), zen = Lin(S.c["zenith"]), hor = Lin(S.c["horizon"]), glowC = Lin(S.c["glowColor"]), key = Lin(S.c["keyColor"]);
            float night = S["night"], glow = S["glow"], gain = S["skyGain"], turb = S["turbidity"], ray = S["rayleigh"];
            var dayZen = new Color(0.09f, 0.22f, 0.62f) * Mathf.Lerp(1, 0.55f, Mathf.Clamp01((ray - 1) / 3));
            var dayHor = fog * 1.1f;
            bool sunUp = S["sunEl"] > -6;
            var cols = new Color[skyGeo.pos.Count];
            for (int i = 0; i < cols.Length; i++)
            {
                var d = skyGeo.pos[i].normalized; float h = Mathf.Max(d.y, 0);
                Color day;
                if (d.y < 0) day = dayHor * 0.8f;
                else
                {
                    day = Color.Lerp(dayHor, dayZen, Mathf.Pow(h, 0.5f));
                    if (sunUp) { float s = Mathf.Max(Vector3.Dot(d, sunDir), 0); day += key * (Mathf.Pow(s, 8) * 0.5f * Mathf.Min(1, turb / 4) + Mathf.Pow(s, 64) * 1.5f); }
                }
                day *= gain;
                Color n = d.y < 0 ? hor * 0.6f + glowC * glow * 0.05f : Color.Lerp(hor, zen, Mathf.Pow(h, 0.45f)) + glowC * (glow * Mathf.Exp(-h * 9) * 0.08f);
                var c = Color.Lerp(day, n, night); c.a = 1; cols[i] = c;
            }
            skyUnity.colors = cols;
        }

        // The environment's radiance at the zenith, the horizon and below (stand-in for the PMREM sky scene / room).
        static void EnvColors(EnvState S, bool room, out Color up, out Color hor, out Color down)
        {
            if (room) { up = new Color(0.8f, 0.8f, 0.8f); hor = new Color(0.55f, 0.55f, 0.55f); down = new Color(0.35f, 0.35f, 0.35f); return; }
            float night = S["night"], gain = S["skyGain"];
            up = Color.Lerp(new Color(0.09f, 0.22f, 0.62f) * gain, Lin(S.c["zenith"]), night);
            hor = Color.Lerp(Lin(S.c["fogColor"]) * gain, Lin(S.c["horizon"]), night);
            down = Lin(S.c["hemiGround"]) * (0.5f * S["hemiI"] + 0.05f);
        }

        static Cubemap GradientCube(Color up, Color hor, Color down)
        {
            const int N = 16;
            var cube = new Cubemap(N, TextureFormat.RGBAHalf, false) { name = "env" };
            var px = new Color[N * N];
            foreach (CubemapFace f in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dy = f == CubemapFace.PositiveY ? 1 : f == CubemapFace.NegativeY ? -1 : 1 - 2 * (y + 0.5f) / N; // row 0 = top of a side face
                        var c = dy >= 0 ? Color.Lerp(hor, up, Mathf.Pow(dy, 0.5f)) : Color.Lerp(hor, down, Mathf.Min(1, -dy * 4));
                        c.a = 1; px[y * N + x] = c;
                    }
                cube.SetPixels(px, f);
            }
            cube.Apply(false);
            return cube;
        }

        // ---- clouds: CLOUD_FS as a baked fbm texture on an upper dome; coverage re-baked when it changes ----
        const float CLOUD_SPAN = 8; // the texture repeats every 8 noise units (uv = d.xz / (d.y + 0.1) * 1.6)
        const int CLOUD_N = 256;
        int cloudOct = 5; float cloudCover = -1, cloudShadeK = -1;
        Texture2D cloudTex; Paint cloudPaint; float[] cloudNoise;

        void MakeClouds()
        {
            var g = Geo.Sphere(1700, 48, 12, 0, Mathf.PI * 2, 0, Mathf.PI / 2);
            g.color = new List<Color>(g.pos.Count);
            for (int i = 0; i < g.pos.Count; i++)
            {
                var d = g.pos[i].normalized;
                g.uv[i] = new Vector2(d.x, d.z) / (Mathf.Max(d.y, 0) + 0.1f) * 1.6f;
                g.color.Add(new Color(1, 1, 1, d.y <= 0 ? 0 : Smooth(0, 0.2f, d.y)));
            }
            cloudPaint = new Paint(CLOUD_N, CLOUD_N);
            cloudTex = cloudPaint.ToTexture(true, true);
            cloudMat = Mat.Unlit(new MatSpec { map = cloudTex, vertexColors = true, transparent = true, depthWrite = false, side = "back", fog = false });
            cloudMat.renderQueue = 1002;
            clouds = Geo.Obj(g, cloudMat, "clouds", false, false);
        }

        static float H2(int x, int y) { unchecked { uint h = (uint)x * 374761393u + (uint)y * 668265263u; h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / 4294967296f; } }
        static float VNoise(float px, float py, int period)
        {
            int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py); float fx = px - ix, fy = py - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x0 = ((ix % period) + period) % period, y0 = ((iy % period) + period) % period, x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
            return Mathf.Lerp(Mathf.Lerp(H2(x0, y0), H2(x1, y0), fx), Mathf.Lerp(H2(x0, y1), H2(x1, y1), fx), fy);
        }

        void SetClouds(float cover, Color lit, Color shade)
        {
            float ll = lit.r * 0.2126f + lit.g * 0.7152f + lit.b * 0.0722f, sl = shade.r * 0.2126f + shade.g * 0.7152f + shade.b * 0.0722f;
            float shadeK = ll > 1e-4f ? Mathf.Clamp01(sl / ll) : 1;
            if (Mathf.Abs(cover - cloudCover) < 0.02f && Mathf.Abs(shadeK - cloudShadeK) < 0.05f) return;
            cloudCover = cover; cloudShadeK = shadeK;
            if (cloudNoise == null || cloudNoise.Length != CLOUD_N * CLOUD_N + 1 || (int)cloudNoise[CLOUD_N * CLOUD_N] != cloudOct)
            {
                cloudNoise = new float[CLOUD_N * CLOUD_N + 1];
                for (int y = 0; y < CLOUD_N; y++)
                    for (int x = 0; x < CLOUD_N; x++)
                    {
                        float s = 0, a = 0.5f; int per = (int)CLOUD_SPAN; float u = x * CLOUD_SPAN / CLOUD_N, v = y * CLOUD_SPAN / CLOUD_N;
                        for (int o = 0; o < cloudOct; o++) { s += a * VNoise(u, v, per); u *= 2; v *= 2; per *= 2; a *= 0.5f; }
                        cloudNoise[y * CLOUD_N + x] = s;
                    }
                cloudNoise[CLOUD_N * CLOUD_N] = cloudOct;
            }
            float edge = 1 - cover;
            var px = cloudPaint.Pixels;
            for (int i = 0; i < CLOUD_N * CLOUD_N; i++)
            {
                float n = cloudNoise[i], al = Smooth(edge, edge + 0.22f, n), thick = Smooth(edge, edge + 0.5f, n);
                float g = Mathf.Lerp(1, shadeK, thick * 0.75f);
                byte gb = (byte)Mathf.RoundToInt(Mathf.LinearToGammaSpace(g) * 255);
                px[i] = new Color32(gb, gb, gb, (byte)Mathf.RoundToInt(al * 255));
            }
            cloudPaint.Upload(cloudTex);
        }

        // ---- stars: 2400 points (seed 77) as small quads facing the centre (no point sprites in URP) ----
        GameObject MakeStars()
        {
            var r = Kit.Rng(77); const int n = 2400; const float R = 1800, S = 3.2f; // ~2.2 px at 1800 m
            var g = new GeoData { color = new List<Color>(n * 4) };
            for (int i = 0; i < n; i++)
            {
                float y = 0.02f + r() * 0.98f, th = r() * Mathf.PI * 2, s = Mathf.Sqrt(1 - y * y), b = 0.25f + 1.8f * Mathf.Pow(r(), 7), t = r();
                var p = new Vector3(Mathf.Cos(th) * s * R, y * R, Mathf.Sin(th) * s * R);
                var col = t < 0.15f ? new Color(b, b * 0.85f, b * 0.7f) : t < 0.35f ? new Color(b * 0.8f, b * 0.9f, b) : new Color(b, b, b);
                var nrm = -p.normalized; var ax = Vector3.Cross(nrm, Vector3.up).normalized; if (ax.sqrMagnitude < 1e-6f) ax = Vector3.right; var ay = Vector3.Cross(ax, nrm);
                int o = g.pos.Count;
                g.pos.Add(p - ax * S - ay * S); g.pos.Add(p + ax * S - ay * S); g.pos.Add(p + ax * S + ay * S); g.pos.Add(p - ax * S + ay * S);
                g.uv.Add(new Vector2(0, 0)); g.uv.Add(new Vector2(1, 0)); g.uv.Add(new Vector2(1, 1)); g.uv.Add(new Vector2(0, 1));
                for (int k = 0; k < 4; k++) { g.normal.Add(nrm); g.color.Add(col); }
                g.index.Add(o); g.index.Add(o + 1); g.index.Add(o + 2); g.index.Add(o); g.index.Add(o + 2); g.index.Add(o + 3);
            }
            var dot = kit.Texture(kit.Canvas(32, 32, (c, w, h) =>
            {
                var gr = new PaintGradient(16, 16, 0, 16, 16, 16); gr.addColorStop(0, "#fff"); gr.addColorStop(0.35f, "rgba(255,255,255,0.6)"); gr.addColorStop(1, "rgba(255,255,255,0)");
                c.fillStyle = gr; c.fillRect(0, 0, 32, 32);
            }));
            starMat = Mat.Unlit(new MatSpec { map = dot, vertexColors = true, transparent = true, depthWrite = false, blending = "additive", side = "double", fog = false });
            starMat.renderQueue = 1001;
            return Geo.Obj(g, starMat, "stars", false, false);
        }

        // ---- moon: a camera-facing sprite (seed 9), colour x2.5, 145 m at 1500 m ----
        GameObject MakeMoon()
        {
            var r = Kit.Rng(9);
            var map = kit.Texture(kit.Canvas(256, 256, (g, w, h) =>
            {
                var halo = new PaintGradient(128, 128, 24, 128, 128, 128);
                halo.addColorStop(0, "rgba(200,215,255,0.35)"); halo.addColorStop(0.35f, "rgba(160,180,230,0.08)"); halo.addColorStop(1, "rgba(0,0,0,0)");
                g.fillStyle = halo; g.fillRect(0, 0, 256, 256);
                g.save(); g.beginPath(); g.arc(128, 128, 30, 0, 7); g.clip();
                g.fillStyle = "#e8ecf2"; g.fillRect(90, 90, 80, 80);
                for (int i = 0; i < 10; i++)
                {
                    g.fillStyle = $"rgba(110,118,130,{Kit.JsNum(0.15f + r() * 0.2f)})"; g.beginPath();
                    float cx = 128 + (r() - 0.5f) * 40, cy = 128 + (r() - 0.5f) * 40, rr = 4 + r() * 9; g.arc(cx, cy, rr, 0, 7); g.fill();
                }
                g.restore();
            }));
            moonMat = Mat.Unlit(new MatSpec { map = map, color = new Color(2.5f, 2.5f, 2.5f).gamma, transparent = true, depthWrite = false, blending = "additive", side = "double", fog = false });
            moonMat.renderQueue = 1001;
            return Geo.Obj(Geo.Plane(145, 145), moonMat, "moon", false, false);
        }
    }
}
