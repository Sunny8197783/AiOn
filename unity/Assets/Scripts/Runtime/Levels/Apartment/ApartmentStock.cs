// Ports levels/apartment-stock.js: 베란다 수납장 — a white open shelving cabinet against the east partition of the 302
// balcony, plus a floor row for extra 2 L water. Every stock kind is one instanced mesh whose drawn count follows
// setStock({ itemId: count }), so the shelf visibly fills up or empties as the player buys, packs and uses things.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ApartmentParts;

namespace Gyeongbo.Levels
{
    public sealed class AptStock
    {
        public Action<Dictionary<string, int>> setStock;
        public List<string> ids = new List<string>();
    }

    public static class ApartmentStock
    {
        const double ZA = 8.12, ZZ = 9.28, XF = -0.13, XB = 0.26; // inner box: front x, back x

        // Slot block { z0, z1, nx, nz, ny, y, h, s } or the floor row.
        sealed class Block
        {
            public double z0, z1, y, h; public int nx, nz, ny; public double[] s; public bool floor;
        }

        sealed class Kind { public string id; public int per, cap; public GameObject mesh; }

        static Block Blk(double z0, double z1, int nx, int nz, int ny, double y, double h = 0, double[] s = null) =>
            new Block { z0 = z0, z1 = z1, nx = nx, nz = nz, ny = ny, y = y, h = h, s = s };

        public static AptStock BuildStock(AptBuilder B, GameObject g, GameObject dyn)
        {
            var kit = B.kit; var M = B.M; var r = Kit.Rng(302); double BF = ApartmentHome.HOME.bf;
            var SHELF = new[] { 0.08, 0.48, 0.88, 1.28, 1.66 }.Select(h => BF + h).ToArray();
            var white = kit.Mat("paint:#f1efe9", new MatSpec { roughness = 0.5f });
            // carcass: sides, back, top, plinth, shelves; a cardboard box and a toolbox on top
            g.Add(B.Box(0.44, 2.0, 0.02, white, A(x: 0.07, y: BF, z: ZA - 0.01)), B.Box(0.44, 2.0, 0.02, white, A(x: 0.07, y: BF, z: ZZ + 0.01)));
            g.Add(B.Box(0.02, 2.0, 1.2, white, A(x: XB + 0.02, y: BF, z: (ZA + ZZ) / 2)), B.Box(0.44, 0.02, 1.2, white, A(x: 0.07, y: BF + 1.98, z: (ZA + ZZ) / 2)));
            foreach (var y in SHELF) g.Add(B.Box(0.42, 0.02, 1.16, white, A(x: 0.065, y: y - 0.02, z: (ZA + ZZ) / 2)));
            g.Add(B.Box(0.42, 0.06, 1.16, kit.Mat("paint:#cfcbc2", new MatSpec { roughness = 0.6f }), A(x: 0.065, y: BF, z: (ZA + ZZ) / 2)));
            g.Add(B.Box(0.36, 0.22, 0.46, M.cardboard, A(x: 0.06, y: BF + 2.0, z: 8.45, ry: 0.05)), B.Box(0.2, 0.14, 0.4, kit.Mat("paint:#b8322a", new MatSpec { roughness = 0.45f, metalness = 0.3f }), A(x: 0.05, y: BF + 2.0, z: 9.0)));
            B.Solid(XF - 0.04, ApartmentHome.HOME.x1, ZA - 0.03, ZZ + 0.03, BF, BF + 2.25);

            // ---- item geometries (base at y = 0) and materials ----
            GeoData vc(GeoData geo, Func<float, float, string> colorAt) // per-vertex colours from (y, normal); linear like three.js attributes
            {
                geo.color = new List<Color>(geo.pos.Count);
                for (int i = 0; i < geo.pos.Count; i++) geo.color.Add(J.Css(colorAt(geo.pos[i].y, geo.normal[i].y)).linear);
                return geo;
            }
            var prof = new[] { (0.0, 0.0), (0.044, 0.0), (0.051, 0.008), (0.051, 0.109), (0.0512, 0.11), (0.0512, 0.19), (0.051, 0.191), (0.051, 0.2), (0.049, 0.215), (0.03, 0.258), (0.016, 0.283), (0.016, 0.296), (0.0172, 0.297), (0.0172, 0.315), (0.0, 0.315) };
            var bottle = vc(Geo.Lathe(prof.Select(p => new Vector2((float)p.Item1, (float)p.Item2)).ToList(), 14), (y, ny) => y > 0.2965f ? "#1f5fbf" : y > 0.1095f && y < 0.1905f ? "#2c7fd1" : "#d4ecf6");
            var can = vc(Geo.Cylinder(0.043f, 0.043f, 0.046f, 18).Translate(0, 0.023f, 0), (y, ny) => Math.Abs(ny) > 0.5f ? "#c9cdd1" : "#d9a62c");
            var butane = vc(Geo.Cylinder(0.033f, 0.033f, 0.2f, 14, 10).Translate(0, 0.1f, 0), (y, ny) => ny > 0.5f || y > 0.185f ? "#c9cdd1" : y > 0.08f && y < 0.125f ? "#f4f1ea" : "#e0661a");
            var roll = Geo.Cylinder(0.055f, 0.055f, 0.1f, 16).Translate(0, 0.05f, 0);
            GeoData cube(float sx, float sy, float sz) => Geo.Box(sx, sy, sz).Translate(0, sy / 2, 0);
            Material vmat(float rough, float metal = 0) => Mat.Lit(new MatSpec { vertexColors = true, roughness = rough, metalness = metal });
            Material print(string key, string bg, string fg, string[] lines, Action<Paint, float, float> art = null) => Mat.Lit(new MatSpec
            {
                roughness = 0.55f,
                map = B.Tex("stock:" + key, 256, 128, (c, w, h) =>
                {
                    c.fillStyle = bg; c.fillRect(0, 0, w, h); art?.Invoke(c, w, h);
                    c.fillStyle = fg; c.textAlign = "center"; c.textBaseline = "middle";
                    for (int i = 0; i < lines.Length; i++) { c.font = Fnt(i != 0 ? 600 : 800, i != 0 ? 22 : 36); c.fillText(lines[i], w / 2, h * (lines.Length == 1 ? 0.5f : 0.36f + i * 0.36f)); }
                }),
            });
            void cross(Paint c, float w, float h) { c.fillStyle = "#d7262d"; c.fillRect(w * 0.08f, h * 0.3f, 40, 14); c.fillRect(w * 0.08f + 13, h * 0.3f - 13, 14, 40); }
            void bowl(Paint c, float w, float h) { c.fillStyle = "#ffd34d"; c.beginPath(); c.arc(w * 0.82f, h * 0.62f, 26, 0, Mathf.PI); c.fill(); }
            void foil(Paint c, float w, float h) { var gr = new PaintGradient(0, 0, w, h); gr.addColorStop(0, "#d7b65a"); gr.addColorStop(0.5f, "#f4e3a0"); gr.addColorStop(1, "#b8943c"); c.fillStyle = gr; c.fillRect(0, 0, w, h); }

            // ---- kinds: [id, geometry, material, per unit, slot blocks] ----
            var KINDS = new (string id, GeoData geo, Material mat, int per, Block[] blocks)[]
            {
                ("water_2l", bottle, vmat(0.1f), 1, new[] { Blk(8.14, 9.0, 2, 8, 1, SHELF[0]), new Block { floor = true } }),
                ("water_500", bottle, vmat(0.1f), 1, new[] { Blk(9.02, 9.26, 5, 3, 1, SHELF[0], s: new[] { 0.64, 0.66, 0.64 }) }),
                ("ramen", cube(0.14f, 0.055f, 0.19f), print("ramen", "#d8312a", "#fff3c4", new[] { "얼큰라면", "5개입" }, bowl), 1, new[] { Blk(8.14, 8.74, 2, 3, 4, SHELF[1], h: 0.056) }),
                ("rice_instant", cube(0.115f, 0.04f, 0.13f), print("rice", "#f7f6f2", "#c4161c", new[] { "즉석밥", "210g" }), 1, new[] { Blk(8.78, 9.26, 3, 3, 4, SHELF[1], h: 0.041) }),
                ("canned", can, vmat(0.35f, 0.4f), 1, new[] { Blk(8.14, 8.6, 3, 5, 3, SHELF[2], h: 0.047) }),
                ("batteries_aa", cube(0.02f, 0.16f, 0.11f), print("aa", "#1d1d1f", "#f2c230", new[] { "AA 알카라인", "4입" }), 1, new[] { Blk(8.62, 8.84, 6, 2, 1, SHELF[2]) }),
                ("candles", cube(0.07f, 0.11f, 0.2f), print("candle", "#f6f0dc", "#8a5a1c", new[] { "양초", "6입" }), 1, new[] { Blk(8.86, 9.26, 3, 2, 2, SHELF[2], h: 0.112) }),
                ("gas_stove", cube(0.27f, 0.1f, 0.34f), print("stove", "#2a2c30", "#f4f4f1", new[] { "휴대용 가스레인지" }), 1, new[] { Blk(8.14, 8.5, 1, 1, 3, SHELF[3], h: 0.102) }),
                ("butane", butane, vmat(0.35f, 0.5f), 4, new[] { Blk(8.52, 8.92, 4, 5, 1, SHELF[3]) }),
                ("first_aid", cube(0.14f, 0.1f, 0.2f), print("aid", "#f8f8f6", "#d7262d", new[] { "구급함" }, cross), 1, new[] { Blk(8.95, 9.26, 2, 1, 2, SHELF[3], h: 0.102) }),
                ("tissue", roll, kit.Mat("paint:#f7f7f4", new MatSpec { roughness = 0.95f }), 1, new[] { Blk(8.14, 8.5, 3, 3, 2, SHELF[4], h: 0.1) }),
                ("blanket", cube(0.1f, 0.03f, 0.16f), print("foil", "#d7b65a", "#5a4410", new[] { "은박 담요" }, foil), 1, new[] { Blk(8.52, 8.76, 1, 1, 6, SHELF[4], h: 0.031) }),
                ("wipes", cube(0.11f, 0.07f, 0.2f), print("wipes", "#e9f5ec", "#2f8a4a", new[] { "물티슈", "100매" }), 1, new[] { Blk(8.78, 9.0, 1, 1, 3, SHELF[4], h: 0.071) }),
                ("water_jug", cube(0.18f, 0.06f, 0.22f), print("jug", "#2f6fb2", "#ffffff", new[] { "접이식 물통", "10L" }), 1, new[] { Blk(9.02, 9.26, 1, 1, 2, SHELF[4], h: 0.061) }),
            };
            var kinds = KINDS.Select(K =>
            {
                var slots = new List<Xform>();
                foreach (var b in K.blocks)
                {
                    if (b.floor) { for (int row = 0; row < 2; row++) for (int i = 0; i < 12; i++) slots.Add(X(x: -0.32 - i * 0.108, y: BF, z: 9.25 - row * 0.108, ry: r() * 6.28)); continue; }
                    double dx = (XB - XF) / b.nx, dz = (b.z1 - b.z0) / b.nz; bool round = K.geo == bottle || K.geo == can || K.geo == butane;
                    for (int k = 0; k < b.ny; k++) for (int i = 0; i < b.nx; i++) for (int j = 0; j < b.nz; j++)
                    {
                        double x = XF + (i + 0.5) * dx + (r() - 0.5) * 0.008, y = b.y + k * b.h, z = b.z0 + (j + 0.5) * dz + (r() - 0.5) * 0.008, ry = (r() - 0.5) * (round ? 6 : 0.12);
                        slots.Add(X(x: x, y: y, z: z, ry: ry, sx: b.s?[0], sy: b.s?[1], sz: b.s?[2]));
                    }
                }
                var mesh = kit.Instanced(K.geo, K.mat, slots, cast: true); dyn.Add(mesh);
                var kind = new Kind { id = K.id, per = K.per, mesh = mesh, cap = slots.Count };
                Show(kind, 0); // mesh.count = 0
                return kind;
            }).ToList();
            B.Point("homeStock", "베란다 수납장", -0.75, BF, 8.7, "stock", 1.6);

            // counts: { itemId: n } (missing ids -> 0). Shown units = n * per, capped by the slots.
            void setStock(Dictionary<string, int> counts)
            {
                foreach (var k in kinds) Show(k, Math.Max(0, Math.Min(k.cap, (counts != null && counts.TryGetValue(k.id, out var n) ? n : 0) * k.per)));
            }
            return new AptStock { setStock = setStock, ids = kinds.Select(k => k.id).ToList() };
        }

        // InstancedMesh.count: child i of the kit's instanced object is slot i.
        static void Show(Kind k, int count)
        {
            var t = k.mesh.transform;
            for (int i = 0; i < t.childCount; i++) t.GetChild(i).gameObject.SetActive(i < count);
        }
    }
}
