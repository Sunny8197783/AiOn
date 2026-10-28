// engine/sky.js fixed distant world: the city ring (light group 'windows'), aviation beacons and the hill rings.
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed partial class Sky
    {
        // Distant city: one merged mesh of boxes with a tiled window texture (8 floors x 8 bays per 32 x 24 m tile).
        GameObject MakeSkyline(out GameObject beaconsOut)
        {
            var r = Kit.Rng(4242); var cells = new List<int[]>();
            var map = kit.Texture(kit.Canvas(512, 512, (g, w, h) =>
            {
                g.fillStyle = "#b9b4aa"; g.fillRect(0, 0, 512, 512);
                for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                    {
                        g.fillStyle = "rgba(0,0,0,0.07)"; g.fillRect(x * 64, y * 64 + 58, 64, 3);
                        g.fillStyle = "#2b3440"; g.fillRect(x * 64 + 8, y * 64 + 14, 48, 34);
                        g.fillStyle = "rgba(170,195,215,0.18)"; g.fillRect(x * 64 + 8, y * 64 + 14, 20, 34);
                        cells.Add(new[] { x * 64 + 8, y * 64 + 14 });
                    }
            }));
            var emissiveMap = kit.Texture(kit.Canvas(512, 512, (g, w, h) =>
            {
                g.fillStyle = "#000"; g.fillRect(0, 0, 512, 512);
                foreach (var c in cells) if (r() < 0.42f) { g.fillStyle = r() < 0.75f ? "#ffcf8a" : "#d8ecff"; g.globalAlpha = 0.5f + r() * 0.5f; g.fillRect(c[0], c[1], 48, 34); }
            }));
            var geoms = new List<GeoData>(); var tops = new List<Vector3>(); var placed = new List<Vector3>(); // (x, z, rad)
            for (int tries = 0; geoms.Count < 160 && tries < 1500; tries++)
            {
                float ang = r() * Mathf.PI * 2, dist = 330 + Mathf.Pow(r(), 0.7f) * 300, x = Mathf.Cos(ang) * dist, z = Mathf.Sin(ang) * dist, k = r();
                float w, d, h, ry;
                if (k < 0.05f) { w = d = 24 + r() * 8; h = 95 + r() * 45; ry = r() * Mathf.PI; } // landmark tower
                else if (k < 0.6f) { w = 48 + r() * 30; d = 13 + r() * 4; h = 39 + Mathf.Floor(r() * 10) * 3; ry = (r() - 0.5f) * 0.3f; } // apartment slab facing south
                else { w = 18 + r() * 26; d = 14 + r() * 18; h = 9 + Mathf.Floor(r() * 8) * 3; ry = r() * Mathf.PI; } // low-rise
                float rad = Mathf.Max(w, d) * 0.6f;
                bool hit = false;
                foreach (var p in placed) if (Mathf.Sqrt((p.x - x) * (p.x - x) + (p.y - z) * (p.y - z)) < p.z + rad) { hit = true; break; }
                if (hit) continue;
                placed.Add(new Vector3(x, z, rad));
                var g = Geo.Box(w, h, d); float off = Mathf.Floor(r() * 8) / 8;
                for (int i = 0; i < g.uv.Count; i++)
                {
                    var n = g.normal[i];
                    if (Mathf.Abs(n.y) > 0.5f) { g.uv[i] = new Vector2(0.004f, 0.004f); continue; } // roof: a plain wall texel
                    g.uv[i] = new Vector2(g.uv[i].x * (Mathf.Abs(n.x) > 0.5f ? d : w) / 32 + off, g.uv[i].y * h / 24);
                }
                g.RotateY(ry); g.Translate(x, h / 2 - 2, z); geoms.Add(g);
                if (h > 90) tops.Add(new Vector3(x, h - 1, z));
            }
            var mat = Mat.Lit(new MatSpec { map = map, emissiveMap = emissiveMap, emissive = Color.white, emissiveIntensity = 1, roughness = 0.9f, metalness = 0 });
            kit.Lit("windows", mat, 1.3f);
            var city = Geo.Obj(Geo.Merge(geoms), mat, "city", false, false);
            var list = new List<Xform>(); foreach (var t in tops) list.Add(new Xform { x = t.x, y = t.y, z = t.z });
            var bmat = Mat.Unlit(new MatSpec { color = new Color(9, 0.5f, 0.3f).gamma, fog = false });
            beaconsOut = kit.Instanced(Geo.Sphere(1.2f, 8, 6), bmat, list, false, false);
            return city;
        }

        // A ring of hills (unlit silhouette, colour set per preset). peaks: [angle rad, width rad, extra height 0..1].
        GameObject HillRing(float R, float @base, float amp, uint seed, float[][] peaks, out Material mat)
        {
            var r = Kit.Rng(seed); const int seg = 220;
            float[] ph = { r() * 6.28f, r() * 6.28f, r() * 6.28f, r() * 6.28f };
            var g = new GeoData { color = new List<Color>() };
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2;
                float h = 0.5f + 0.25f * Mathf.Sin(a * 3 + ph[0]) + 0.15f * Mathf.Sin(a * 7 + ph[1]) + 0.08f * Mathf.Sin(a * 13 + ph[2]) + 0.05f * Mathf.Sin(a * 29 + ph[3]);
                foreach (var pk in peaks) { float da = Mathf.Atan2(Mathf.Sin(a - pk[0]), Mathf.Cos(a - pk[0])); h += pk[2] * Mathf.Exp(-(da * da) / (pk[1] * pk[1])); }
                float x = Mathf.Cos(a) * R, z = Mathf.Sin(a) * R;
                g.pos.Add(new Vector3(x, -30, z)); g.pos.Add(new Vector3(x, @base + amp * Mathf.Max(0, h), z));
                g.color.Add(new Color(0.6f, 0.6f, 0.6f)); g.color.Add(new Color(1, 1, 1));
                if (i < seg) { int k = i * 2; g.index.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            }
            mat = Mat.Unlit(new MatSpec { vertexColors = true, fog = false, side = "double" });
            return Geo.Obj(g, mat, "hills", false, false);
        }
    }
}
