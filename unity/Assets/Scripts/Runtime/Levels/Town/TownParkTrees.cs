// Part of the levels/town-park.js port: every tree and shrub instanced. Crowns are leaf cards with outward
// (spherical) normals, ambient-occlusion vertex colours, autumn tints per instance and wind sway; trunks with
// four branches; tree-pit grates under the street ginkgos; fallen leaves under trees, in gutters and on lawns.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.TownUtil;

namespace Gyeongbo.Levels
{
    internal static class TownParkTrees
    {
        const float Y = TownPlan.CURB;

        internal sealed class TreeType
        {
            public int n; public float rx, ry, rz, cy, size, trunk, r0; public string tex; public string[] tints;
        }

        internal static readonly Dictionary<string, TreeType> TYPES = new Dictionary<string, TreeType>
        {
            ["ginkgo"] = new TreeType { n = 46, rx = 1.9f, ry = 3.3f, rz = 1.9f, cy = 5.7f, size = 1.5f, trunk = 7.2f, r0 = 0.17f, tex = "fan", tints = new[] { "#f4c531", "#eab526", "#f0cf47", "#d4c446", "#e2a82a", "#b9b84a" } },
            ["zelkova"] = new TreeType { n = 72, rx = 4.1f, ry = 2.7f, rz = 4.1f, cy = 6.4f, size = 2.0f, trunk = 6.6f, r0 = 0.25f, tex = "oval", tints = new[] { "#c98b3c", "#b8732f", "#a39143", "#c9a64c", "#8f9a45", "#b0602c" } },
            ["maple"] = new TreeType { n = 40, rx = 2.4f, ry = 1.8f, rz = 2.4f, cy = 3.8f, size = 1.2f, trunk = 4.0f, r0 = 0.12f, tex = "oval", tints = new[] { "#c8402c", "#d65a2a", "#a8302a", "#e2782c", "#b84a2e" } },
            ["shrub"] = new TreeType { n = 20, rx = 0.85f, ry = 0.5f, rz = 0.85f, cy = 0.5f, size = 0.62f, trunk = 0, r0 = 0, tex = "oval", tints = new[] { "#4f6b35", "#5b7a3c", "#6a7f3e", "#56703a", "#8a3b2c" } },
        };

        static Texture2D LeafTex(Kit kit, string kind) => kit.Memo($"town:leafTex:{kind}", () =>
        {
            var r = Kit.Rng(kind == "fan" ? 11u : 12u);
            return kit.Texture(kit.Canvas(256, 256, (g, w, h) =>
            {
                g.strokeStyle = "rgba(78,60,44,0.95)"; g.lineWidth = 2.2f; g.lineCap = "round";
                for (int i = 0; i < 7; i++) { g.beginPath(); g.moveTo(w / 2f, h / 2f); g.quadraticCurveTo(w / 2f + (r() - 0.5f) * w * 0.5f, h / 2f + (r() - 0.5f) * h * 0.5f, w / 2f + (r() - 0.5f) * w * 0.85f, h / 2f + (r() - 0.5f) * h * 0.85f); g.stroke(); }
                for (int i = 0; i < 120; i++)
                {
                    float a = r() * 6.283f, d = Mathf.Sqrt(r()) * w * 0.43f, s = 8 + r() * 9; int v = 170 + (int)(r() * 85);
                    g.fillStyle = $"rgb({v},{v},{Math.Max(0, v - (int)(r() * 25))})";
                    g.save(); g.translate(w / 2f + Mathf.Cos(a) * d, h / 2f + Mathf.Sin(a) * d); g.rotate(r() * 6.283f); g.beginPath();
                    if (kind == "fan") { g.moveTo(0, 0); g.arc(0, 0, s, -0.95f, -0.1f); g.lineTo(0, 0); g.arc(0, 0, s, 0.1f, 0.95f); g.closePath(); }
                    else g.ellipse(s * 0.5f, 0, s * 0.62f, s * 0.3f, 0, 0, 7);
                    g.fill(); g.restore();
                }
            }));
        });

        // Outward normals must survive double-sided rendering (JS drops the back-face normal flip).
        // ponytail: URP Lit flips back-face normals; the quality pass's foliage shader should keep them outward.
        static Material Foliage(Kit kit, string kind, float amp) => kit.Memo($"town:foliage:{kind}:{F(amp)}", () =>
        {
            var m = Mat.Lit(new MatSpec { map = LeafTex(kit, kind), alphaTest = 0.5f, side = "double", roughness = 0.82f, vertexColors = true });
            return Sway(m, amp, 1.6f, 0);
        });

        static Material Bark(Kit kit) => kit.Memo("town:bark", () =>
        {
            var r = Kit.Rng(5);
            var h = kit.Canvas(64, 256, (g, w, hh) => { g.fillStyle = "#808080"; g.fillRect(0, 0, w, hh); for (int i = 0; i < 90; i++) { g.fillStyle = r() < 0.5f ? "#4a4a4a" : "#b0b0b0"; g.fillRect(r() * w, r() * hh, 1 + r() * 2, 8 + r() * 30); } });
            var c = kit.Canvas(64, 256, (g, w, hh) => { g.fillStyle = "#5a4a3c"; g.fillRect(0, 0, w, hh); for (int i = 0; i < 90; i++) { g.fillStyle = r() < 0.5f ? "rgba(30,24,18,0.5)" : "rgba(140,128,112,0.35)"; g.fillRect(r() * w, r() * hh, 1 + r() * 2, 8 + r() * 30); } });
            return Mat.Lit(new MatSpec { map = kit.Texture(c), normalMap = kit.Texture(kit.NormalFrom(h, 3), false), repeat = new Vector2(2, 1), roughness = 0.95f });
        });

        // three.js Euler XYZ (intrinsic): v' = Rx * Ry * Rz * v, right-handed JS space.
        static Vector3 RotXYZ(Vector3 v, float ax, float ay, float az)
        {
            float c = Mathf.Cos(az), s = Mathf.Sin(az); float x = v.x * c - v.y * s, y = v.x * s + v.y * c, z = v.z;
            c = Mathf.Cos(ay); s = Mathf.Sin(ay); float x2 = x * c + z * s; z = -x * s + z * c; x = x2;
            c = Mathf.Cos(ax); s = Mathf.Sin(ax); float y2 = y * c - z * s; z = y * s + z * c; y = y2;
            return new Vector3(x, y, z);
        }

        static GeoData CrownGeo(TreeType T, Func<float> r)
        {
            var g = new GeoData { color = new List<Color>() };
            var corners = new[] { (-0.5f, -0.5f), (0.5f, -0.5f), (0.5f, 0.5f), (-0.5f, 0.5f) };
            for (int i = 0; i < T.n; i++)
            {
                var d = new Vector3(r() * 2 - 1, r() * 2 - 1, r() * 2 - 1); if (d.sqrMagnitude < 1e-4f) d = new Vector3(0, 1, 0); d.Normalize();
                float t = 0.4f + 0.6f * Mathf.Sqrt(r());
                var c = new Vector3(d.x * T.rx * t, T.cy + d.y * T.ry * t, d.z * T.rz * t);
                float s = T.size * (0.7f + 0.6f * r());
                float ex = r() * 6.28f, ey = r() * 6.28f, ez = r() * 6.28f;
                float ao = Math.Min(1, 0.42f + 0.4f * t + 0.25f * (d.y * 0.5f + 0.5f));
                int bs = g.pos.Count;
                foreach (var (a, b) in corners)
                {
                    var v = RotXYZ(new Vector3(a * s, b * s, 0), ex, ey, ez) + c;
                    g.pos.Add(v);
                    g.normal.Add(new Vector3(v.x / (T.rx * T.rx), (v.y - T.cy) / (T.ry * T.ry) + 0.12f, v.z / (T.rz * T.rz)).normalized);
                    g.uv.Add(new Vector2(a + 0.5f, b + 0.5f)); g.color.Add(new Color(ao, ao, ao, 1));
                }
                g.index.AddRange(new[] { bs, bs + 1, bs + 2, bs, bs + 2, bs + 3 });
            }
            return g;
        }

        static GeoData TrunkGeo(TreeType T, Func<float> r)
        {
            var parts = new List<GeoData> { Geo.Cylinder(T.r0 * 0.55f, T.r0, T.trunk, 7).Translate(0, T.trunk / 2, 0) };
            for (int i = 0; i < 4; i++)
            {
                float len = Math.Max(T.rx, T.ry) * (0.7f + r() * 0.3f);
                var b = Geo.Cylinder(T.r0 * 0.18f, T.r0 * 0.38f, len, 5).Translate(0, len / 2, 0);
                b.RotateZ(0.55f + r() * 0.35f).RotateY(i * 1.57f + r() * 0.6f).Translate(0, T.cy - T.ry * 0.55f + r() * 0.6f, 0);
                parts.Add(b);
            }
            return Geo.Merge(parts);
        }

        // Crowns, trunks (with solids), pit grates and fallen leaves for all spots; rand is the park's rng(4242).
        internal static void Plant(Kit kit, GameObject group, List<Solid> solids, List<TreeSpot> spots, Func<float> rand)
        {
            var order = new List<string>(); var byType = new Dictionary<string, List<TreeSpot>>();
            foreach (var s in spots) { if (!byType.TryGetValue(s.type, out var l)) { byType[s.type] = l = new List<TreeSpot>(); order.Add(s.type); } l.Add(s); }
            var bark = Bark(kit);
            foreach (var type in order)
            {
                var list = byType[type]; var T = TYPES[type]; var r = Kit.Rng((uint)(type.Length * 31));
                var tf = list.ConvertAll(s => new Xform { x = s.x, y = Y, z = s.z, ry = s.ry, s = s.s });
                var crownGeo = CrownGeo(T, r); var fol = Foliage(kit, T.tex, type == "shrub" ? 0.02f : 0.011f);
                // ponytail: one instanced mesh per tint (material colour) instead of three.js instanceColor.
                var crowns = J.Group($"crowns:{type}", group);
                for (int k = 0; k < T.tints.Length; k++)
                {
                    var sub = new List<Xform>();
                    for (int i = 0; i < list.Count; i++) if ((int)Mathf.Floor(list[i].tint * T.tints.Length) % T.tints.Length == k) sub.Add(tf[i]);
                    if (sub.Count > 0) crowns.Add(kit.Instanced(crownGeo, Tinted(kit, $"town:foliage:{T.tex}:{type}", fol, T.tints[k]), sub, true));
                }
                if (T.trunk > 0)
                {
                    group.Add(kit.Instanced(TrunkGeo(T, r), bark, tf));
                    foreach (var s in list) solids.Add(new Solid(s.x - 0.3f, s.x + 0.3f, s.z - 0.3f, s.z + 0.3f, -0.5f, 3, "tree"));
                }
            }
            // Tree-pit grates under street trees (gingkos on sidewalks).
            var grates = new List<Xform>();
            foreach (var s in spots) if (s.type == "ginkgo") grates.Add(new Xform { x = s.x, y = Y + 0.006f, z = s.z });
            var gr = kit.Memo("town:treeGrate", () => Mat.Lit(new MatSpec
            {
                map = kit.Texture(kit.Canvas(128, 128, (g, w, h) =>
                {
                    g.fillStyle = "#3b3a37"; g.fillRect(0, 0, w, w); g.strokeStyle = "#1d1d1c"; g.lineWidth = 3;
                    for (int i = 1; i < 6; i++) { g.beginPath(); g.arc(64, 64, i * 11, 0, 7); g.stroke(); }
                    for (int a = 0; a < 12; a++) { g.beginPath(); g.moveTo(64, 64); g.lineTo(64 + Mathf.Cos(a / 1.91f) * 64, 64 + Mathf.Sin(a / 1.91f) * 64); g.stroke(); }
                    g.fillStyle = "#2a2018"; g.beginPath(); g.arc(64, 64, 10, 0, 7); g.fill();
                })),
                roughness = 0.6f, metalness = 0.6f, polygonOffset = true, polygonOffsetFactor = -2,
            }));
            group.Add(kit.Instanced(Geo.Plane(1.3f, 1.3f).RotateX(-Mathf.PI / 2), gr, grates, false));

            // ---- fallen leaves: under street trees, gutters, lawns ----
            var leafOne = kit.Memo("town:leafOne", () => Mat.Lit(new MatSpec
            {
                map = kit.Texture(kit.Canvas(64, 64, (g, w, h) =>
                {
                    g.fillStyle = "#ffffff"; g.beginPath(); g.moveTo(32, 58); g.arc(32, 58, 46, -2.5f, -1.62f); g.lineTo(32, 58); g.arc(32, 58, 46, -1.52f, -0.64f); g.closePath(); g.fill();
                    g.strokeStyle = "rgba(120,100,60,0.6)"; g.beginPath(); g.moveTo(32, 58); g.lineTo(32, 64); g.stroke();
                })),
                alphaTest = 0.5f, side = "double", roughness = 0.9f, polygonOffset = true, polygonOffsetFactor = -3,
            }));
            string[] LT = { "#e9b628", "#d9a325", "#c7802c", "#b5582a", "#8a6a3a", "#e5c84a" };
            var fallen = new List<Xform>(); var fcol = new List<int>();
            void Drop(float x, float y, float z) { fallen.Add(new Xform { x = x, y = y, z = z, ry = rand() * 6.28f, rz = (rand() - 0.5f) * 0.3f, s = 0.7f + rand() * 0.7f }); fcol.Add(Idx(rand(), LT.Length)); }
            foreach (var s in spots)
            {
                int n = s.type == "shrub" ? 0 : s.type == "ginkgo" ? 14 : 18;
                for (int i = 0; i < n; i++) { float a = rand() * 6.28f, d = Mathf.Sqrt(rand()) * 3.2f; Drop(s.x + Mathf.Cos(a) * d, Y + 0.008f, s.z + Mathf.Sin(a) * d * (s.type == "ginkgo" ? 0.55f : 1)); }
            }
            for (int i = 0; i < 260; i++) { float x = -128 + rand() * 256, side = rand() < 0.5f ? -1 : 1; Drop(x, 0.006f, side * (5.3f + rand() * 0.6f)); }
            for (int i = 0; i < 380; i++) Drop(-41 + rand() * 52, Y + 0.008f, 13 + rand() * 38);
            var leafGeo = Geo.Plane(0.15f, 0.15f).RotateX(-Mathf.PI / 2);
            var leaves = J.Group("leaves", group);
            for (int k = 0; k < LT.Length; k++)
            {
                var sub = new List<Xform>();
                for (int i = 0; i < fallen.Count; i++) if (fcol[i] == k) sub.Add(fallen[i]);
                if (sub.Count > 0) leaves.Add(kit.Instanced(leafGeo, Tinted(kit, "town:leafOne", leafOne, LT[k]), sub, false));
            }
        }
    }
}
