// Ports levels/town-fences.js: the fence lines of TownPlan.FENCES (green welded-mesh panels, brick block walls,
// box hedges and low park railings), each with a blocking solid, plus invisible walls just outside BOUNDS so
// nobody walks off into the filler city.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Levels.TownUtil;

namespace Gyeongbo.Levels
{
    public sealed class TownFencesResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
    }

    public static class TownFences
    {
        const float Y = TownPlan.CURB;

        public static TownFencesResult BuildFences(Kit kit)
        {
            var group = J.Group("fences");
            var stat = J.Group("static", group);
            var solids = new List<Solid>();

            Material AlphaMat(string key, Action<Paint, int, int> draw, int color) => kit.Memo($"town:fence:{key}", () => Mat.Lit(new MatSpec
            {
                map = kit.Texture(kit.Canvas(256, 256, draw)), alphaTest = 0.5f, side = "double", color = J.Hex(color), roughness = 0.5f, metalness = 0.5f,
            }));
            // One texture = one 2.4 m panel: frame, vertical wires every ~5 cm, horizontal every ~20 cm, two V folds.
            var meshMat = AlphaMat("mesh", (g, w, h) =>
            {
                g.strokeStyle = "#ffffff"; g.lineWidth = 2;
                for (int x = 3; x < w; x += 6) { g.beginPath(); g.moveTo(x, 0); g.lineTo(x, h); g.stroke(); }
                for (int y = 4; y < h; y += 26) { g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.stroke(); }
                g.lineWidth = 5; foreach (var y in new[] { h * 0.3f, h * 0.72f }) { g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.stroke(); }
                g.lineWidth = 10; g.strokeRect(5, 5, w - 10, h - 10);
            }, 0x2f6b49);
            var parkMat = AlphaMat("park", (g, w, h) =>
            {
                g.fillStyle = "#ffffff";
                for (int x = 6; x < w; x += 26) g.fillRect(x, h * 0.08f, 7, h * 0.86f);
                g.fillRect(0, h * 0.06f, w, 14); g.fillRect(0, h * 0.82f, w, 10);
                for (int x = 19; x < w; x += 52) { g.beginPath(); g.arc(x, h * 0.06f, 10, 0, 7); g.fill(); }
            }, 0x2a2e33);
            var hedgeMat = kit.Memo("town:hedge", () =>
            {
                var r = Kit.Rng(31);
                string[] greens = { "#3f6230", "#4d7337", "#36552a", "#5c7f3c", "#6b6a32" };
                void Draw(Paint g, int w, int h, bool shade)
                {
                    g.fillStyle = shade ? "#404040" : "#2f4a26"; g.fillRect(0, 0, w, h);
                    for (int i = 0; i < 2600; i++)
                    {
                        float v = r(); int k = (int)(80 + v * 140);
                        g.fillStyle = shade ? $"rgb({k},{k},{k})" : greens[Idx(v, 5)];
                        g.beginPath(); g.ellipse(r() * w, r() * h, 3 + r() * 4, 1.5f + r() * 2, r() * 3, 0, 7); g.fill();
                    }
                }
                var map = kit.Texture(kit.Canvas(256, 256, (g, w, h) => Draw(g, w, h, false)));
                var nrm = kit.Texture(kit.NormalFrom(kit.Canvas(256, 256, (g, w, h) => Draw(g, w, h, true)), 3), false);
                return Mat.Lit(new MatSpec { map = map, normalMap = nrm, roughness = 0.95f });
            });
            Material brick = kit.Surface("brick", new SurfaceOpts { color = J.Hex(0xe8ddd2) }), coping = kit.Mat("curb"),
                post = kit.Mat("paint:#2c5c40", new MatSpec { roughness = 0.45f, metalness = 0.5f }), dark = kit.Mat("darkMetal");

            var posts = new List<Xform>(); var ppost = new List<Xform>();
            // Vertical panel along a fence line; uv.x repeats every `per` metres.
            GameObject Panel(float x0, float z0, float x1, float z1, float h, float y0, Material mat, float per)
            {
                float L = Hypot(x1 - x0, z1 - z0);
                var g = Geo.Plane(L, h);
                for (int i = 0; i < g.uv.Count; i++) g.uv[i] = new Vector2(g.uv[i].x * L / per, g.uv[i].y);
                var m = Obj(g, mat, true, true);
                J.SetPos(m, (x0 + x1) / 2, y0 + h / 2, (z0 + z1) / 2); J.SetYaw(m, -Mathf.Atan2(z1 - z0, x1 - x0));
                return m;
            }
            foreach (var line in TownPlan.FENCES)
            {
                float x0 = line.x0, z0 = line.z0, x1 = line.x1, z1 = line.z1, L = Hypot(x1 - x0, z1 - z0);
                string kind = line.kind;
                if (L < 0.05f) continue;
                var s = LineSolid(line); s.id = $"fence:{kind}"; solids.Add(s);
                float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, ry = Math.Abs(x1 - x0) > Math.Abs(z1 - z0) ? 0 : Mathf.PI / 2;
                void Steps(float per, List<Xform> list)
                {
                    int n = Math.Max(1, (int)Round(L / per));
                    for (int i = 0; i <= n; i++) list.Add(new Xform { x = x0 + (x1 - x0) * i / n, y = Y, z = z0 + (z1 - z0) * i / n });
                }
                if (kind == "mesh")
                {
                    stat.Add(Panel(x0, z0, x1, z1, 1.7f, Y + 0.06f, meshMat, 2.4f));
                    stat.Add(kit.Box(L, 0.05f, 0.05f, post, At(cx, Y + 1.74f, cz, ry)));
                    Steps(2.4f, posts);
                }
                else if (kind == "park")
                {
                    stat.Add(Panel(x0, z0, x1, z1, 1.05f, Y, parkMat, 2.0f));
                    Steps(2.0f, ppost);
                }
                else if (kind == "wall")
                {
                    stat.Add(kit.Slab(L + 0.2f, 1.8f, 0.2f, brick, At(cx, Y - 0.05f, cz, ry)));
                    stat.Add(kit.Box(L + 0.3f, 0.07f, 0.3f, coping, At(cx, Y + 1.75f, cz, ry)));
                }
                else if (kind == "hedge")
                {
                    stat.Add(kit.Slab(L + 0.3f, 1.05f, 0.75f, hedgeMat, At(cx, Y, cz, ry)));
                    stat.Add(kit.Slab(L + 0.1f, 0.18f, 0.62f, hedgeMat, At(cx, Y + 1.04f, cz, ry)));
                }
                else Debug.LogError($"[town] unknown fence kind \"{kind}\"");
            }
            if (posts.Count > 0) group.Add(kit.Instanced(Geo.Box(0.06f, 1.85f, 0.06f).Translate(0, 0.92f, 0), post, posts));
            if (ppost.Count > 0) group.Add(kit.Instanced(Geo.Box(0.07f, 1.12f, 0.07f).Translate(0, 0.56f, 0), dark, ppost));

            // Invisible edge of the playable town.
            float bx0 = (float)TownMap.BOUNDS.x0, bx1 = (float)TownMap.BOUNDS.x1, bz0 = (float)TownMap.BOUNDS.z0, bz1 = (float)TownMap.BOUNDS.z1;
            solids.Add(new Solid(bx0 - 2, bx0 + 0.4f, bz0 - 2, bz1 + 2, -5, 20, "bounds")); solids.Add(new Solid(bx1 - 0.4f, bx1 + 2, bz0 - 2, bz1 + 2, -5, 20, "bounds"));
            solids.Add(new Solid(bx0 - 2, bx1 + 2, bz0 - 2, bz0 + 0.4f, -5, 20, "bounds")); solids.Add(new Solid(bx0 - 2, bx1 + 2, bz1 - 0.4f, bz1 + 2, -5, 20, "bounds"));

            kit.Merge(stat);
            return new TownFencesResult { group = group, solids = solids };
        }
    }
}
