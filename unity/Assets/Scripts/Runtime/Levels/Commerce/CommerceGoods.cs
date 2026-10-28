// Ports levels/commerce-goods.js: shared by the mart, the store and the office — packaged goods (label atlas,
// instanced per product), shelf runs (gondolas), carts and baskets, ceiling panels and lamps, the painted view
// out of the windows per mood, NPC placement, solids from bounds. Labels live in CommerceLabels, the painted
// view in CommerceView, the NPC stand-in in CommerceStandIn.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // PRODUCTS entry: [shape, w, h, d, labels, stack].
    public sealed class Product
    {
        public string shape;
        public float w, h, d;
        public List<string> labels = new List<string>();
        public int stack = 1;
    }

    // goods.stock({ sec, x, z, dir, face, len, depth, y, clear, items, seed, fill, edge, rows, stack }); gondola runs
    // are the same shape plus `side` (JS spreads a run into stock()).
    public sealed class StockRun
    {
        public string sec = "misc";
        public float[] dir, face;
        public float x, z, len, depth = 0.42f, y, clear = 0.32f, fill = 0.92f;
        public List<string> items = new List<string>();
        public int seed = 1, side;
        public bool edge = true;
        public int? rows, stack;
        // { ...run, sec, items, seed, edge }
        public StockRun With(string sec, IEnumerable<string> items, int seed, bool? edge = null)
        {
            var r = (StockRun)MemberwiseClone(); r.sec = sec; r.items = items.ToList(); r.seed = seed; if (edge.HasValue) r.edge = edge.Value; return r;
        }
    }

    public sealed class GoodsSection
    {
        public GameObject full, @out;
    }

    // One instanced product: transform, atlas cell, brightness.
    sealed class GoodsItem { public Xform t; public int cell; public float tint; }

    sealed class GoodsShared
    {
        public Dictionary<string, GeoData> geoms;
        public Texture2D atlas;
        public float pad;
        public Material stripMat, tagMat;
        public int deep;
        public readonly Dictionary<string, GeoData> cellGeo = new Dictionary<string, GeoData>();
        public readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    }

    public sealed class Goods
    {
        readonly Kit kit;
        readonly GoodsShared S;
        readonly List<string> order = new List<string>();
        readonly Dictionary<string, List<GoodsItem>> buckets = new Dictionary<string, List<GoodsItem>>();
        readonly List<string> tagOrder = new List<string>();
        readonly Dictionary<string, List<Xform>> tags = new Dictionary<string, List<Xform>>();
        readonly List<GeoData> strips = new List<GeoData>();
        internal Goods(Kit kit, GoodsShared s) { this.kit = kit; S = s; }

        void Push(string key, GoodsItem item)
        {
            if (!buckets.TryGetValue(key, out var l)) { buckets[key] = l = new List<GoodsItem>(); order.Add(key); }
            l.Add(item);
        }

        // rows/stack override the automatic depth rows (bottles: 1) and stack height.
        public void Stock(StockRun run)
        {
            var r = Kit.Rng((uint)(run.seed * 131 + 7));
            float ax = run.dir[0], az = run.dir[1], ox = run.face[0], oz = run.face[1], ry = Mathf.Atan2(ox, oz), block = run.len / run.items.Count;
            float x = run.x, z = run.z, y = run.y;
            for (int bi = 0; bi < run.items.Count; bi++)
            {
                var id = run.items[bi];
                if (!CommerceGoods.PRODUCTS.TryGetValue(id, out var P)) { Debug.LogError($"[commerce] unknown product {id}"); continue; }
                string shape = P.shape; float w = P.w, h = P.h, d = P.d;
                bool isFixed = CommerceGoods.FIXED.Contains(shape);
                float fw = w + 0.012f; int n = Math.Max(1, (int)Math.Floor(block / fw)); float padA = (block - n * fw) / 2;
                int rows = Math.Max(1, Math.Min(run.rows ?? (shape.StartsWith("bottle") ? 1 : S.deep), (int)Math.Floor(run.depth / (d + 0.01f))));
                int stack = Math.Max(1, Math.Min(run.stack ?? P.stack, (int)Math.Floor(run.clear / (h + 0.004f))));
                int cell = CommerceLabels.CELL[P.labels[(bi + run.seed) % P.labels.Count]];
                int kept = 0;
                for (int j = 0; j < n; j++)
                {
                    if (r() > 0.985f) continue; // a sold-through facing
                    float a = bi * block + padA + fw * (j + 0.5f);
                    for (int k = 0; k < rows; k++)
                    {
                        if (k == 0 && r() > run.fill) continue;
                        float back = 0.025f + d * (k + 0.5f) + 0.01f * k + (r() - 0.5f) * 0.012f;
                        for (int m = 0; m < stack; m++)
                        {
                            var t = new Xform { x = x + ax * a - ox * back, y = y + m * (h + 0.003f), z = z + az * a - oz * back };
                            t.ry = ry + (r() - 0.5f) * 0.08f; float tint = 0.9f + r() * 0.12f;
                            if (!isFixed) { t.sx = w; t.sy = h; t.sz = d; }
                            Push($"{run.sec}|{shape}", new GoodsItem { t = t, cell = cell, tint = tint });
                        }
                    }
                    // a few leftovers stay behind on a sold-out shelf
                    if (kept < 2 && r() < 1.4f / n)
                    {
                        kept++;
                        var t = new Xform { x = x + ax * a - ox * (0.06f + r() * 0.12f), y = y }; t.z = z + az * a - oz * (0.06f + r() * 0.12f); t.ry = ry + (r() - 0.5f) * 1.2f;
                        if (!isFixed) { t.sx = w; t.sy = h; t.sz = d; }
                        Push($"{run.sec}:out|{shape}", new GoodsItem { t = t, cell = cell, tint = 0.85f });
                    }
                }
            }
            if (run.edge)
            {
                var g = Geo.Plane(run.len, 0.04f);
                for (int i = 0; i < g.uv.Count; i++) g.uv[i] = new Vector2(g.uv[i].x * run.len, g.uv[i].y);
                g = g.RotateY(ry).Translate(x + ax * run.len / 2 + ox * 0.012f, y - 0.022f, z + az * run.len / 2 + oz * 0.012f); strips.Add(g);
                if (!tags.ContainsKey(run.sec)) { tags[run.sec] = new List<Xform>(); tagOrder.Add(run.sec); }
                for (float a = 0.35f; a < run.len; a += 0.9f) tags[run.sec].Add(new Xform { x = x + ax * a + ox * 0.016f, y = y - 0.02f, z = z + az * a + oz * 0.016f, ry = ry });
            }
        }

        // { group, sections: { id: { full, out } } } — sections swap to empty shelves when sold out.
        public (GameObject group, Dictionary<string, GoodsSection> sections) Build()
        {
            var group = J.Group("goods");
            var sections = new Dictionary<string, GoodsSection>();
            GoodsSection sect(string sec)
            {
                if (sections.TryGetValue(sec, out var s)) return s;
                s = sections[sec] = new GoodsSection { full = J.Group("full", group), @out = J.Group("out", group) };
                s.@out.SetActive(false); return s;
            }
            foreach (var key in order)
            {
                var list = buckets[key];
                var parts = key.Split('|'); var shape = parts[1]; var sk = parts[0].Split(':'); string sec = sk[0]; bool isOut = sk.Length > 1;
                var holder = isOut ? sect(sec).@out : sect(sec).full;
                // ponytail: the JS per-instance aCell/instanceColor become one batch per (atlas cell, brightness step)
                // with the cell baked into the UVs; an instanced shader with per-instance data removes the batching.
                foreach (var grp in list.GroupBy(it => (it.cell, step: Mathf.Round(it.tint / 0.04f))))
                    holder.Add(kit.Instanced(CommerceGoods.CellGeo(S, shape, grp.Key.cell), CommerceGoods.GoodsMat(S, shape, grp.Key.step * 0.04f), grp.Select(it => it.t).ToList(), false, true));
            }
            foreach (var sec in tagOrder) sect(sec).@out.Add(kit.Instanced(Geo.Plane(0.11f, 0.055f), S.tagMat, tags[sec], false, false));
            if (strips.Count > 0) group.Add(Geo.Obj(Geo.Merge(strips), S.stripMat, "strips", cast: false, receive: true));
            return (group, sections);
        }
    }

    public sealed class GondolaOpts
    {
        public float x, z, len, height = 1.86f, depth = 0.45f;
        public string axis = "z", sides = "both", color = "#e9e9e5";
        public float[] levels = { 0.12f, 0.5f, 0.86f, 1.22f, 1.58f };
    }

    // npc(people, preset, seed, at): placement and starting state.
    public sealed class NpcAt
    {
        public float x, y, z, ry;
        public string anim;
        public List<string> props;
    }

    public static partial class CommerceGoods
    {
        // name: [shape, w, h, d, labels, stack]. For fixed-size shapes (bottles, cans, cups) w/h/d are the real sizes.
        public static readonly Dictionary<string, Product> PRODUCTS = new Dictionary<string, Product>
        {
            ["water2"] = P("bottle2", 0.11f, 0.32f, 0.11f, new[] { "water2a", "water2b" }), ["water5"] = P("bottle5", 0.068f, 0.21f, 0.068f, new[] { "water5", "water5b" }),
            ["drink"] = P("bottle5", 0.068f, 0.21f, 0.068f, new[] { "cola", "cider", "ion", "tea" }), ["can"] = P("can", 0.068f, 0.123f, 0.068f, new[] { "coffeeCan", "juiceCan", "soda" }, 2),
            ["vita"] = P("can", 0.068f, 0.123f, 0.068f, new[] { "vita" }, 2), ["soap"] = P("bottle5", 0.068f, 0.21f, 0.068f, new[] { "soap" }),
            ["ramen"] = P("pouch", 0.3f, 0.1f, 0.22f, new[] { "ramenR", "ramenB", "ramenW" }, 3), ["cup"] = P("cup", 0.11f, 0.1f, 0.11f, new[] { "cupR", "cupU" }, 3),
            ["rice"] = P("box", 0.15f, 0.12f, 0.2f, new[] { "rice" }, 2), ["tuna"] = P("tin", 0.09f, 0.046f, 0.09f, new[] { "tuna" }, 4), ["ham"] = P("mbox", 0.1f, 0.055f, 0.06f, new[] { "ham" }, 4),
            ["bar"] = P("box", 0.15f, 0.2f, 0.05f, new[] { "bar" }), ["cracker"] = P("pouch", 0.16f, 0.22f, 0.06f, new[] { "cracker" }), ["chips"] = P("pouch", 0.2f, 0.28f, 0.08f, new[] { "chips", "chips2" }),
            ["cookie"] = P("box", 0.2f, 0.08f, 0.13f, new[] { "cookie", "choco" }, 3), ["tissue"] = P("box", 0.45f, 0.32f, 0.24f, new[] { "tissue" }, 2), ["wipes"] = P("pouch", 0.2f, 0.07f, 0.12f, new[] { "wipes" }, 4),
            ["bags"] = P("box", 0.16f, 0.1f, 0.08f, new[] { "bags" }, 3), ["tape"] = P("box", 0.1f, 0.1f, 0.05f, new[] { "tape" }, 2), ["rain"] = P("pouch", 0.16f, 0.22f, 0.03f, new[] { "rain" }),
            ["foil"] = P("pouch", 0.12f, 0.17f, 0.02f, new[] { "foil" }), ["jug"] = P("box", 0.25f, 0.25f, 0.08f, new[] { "jug" }), ["stove"] = P("box", 0.36f, 0.12f, 0.3f, new[] { "stove" }, 3),
            ["butane"] = P("butane", 0.068f, 0.19f, 0.068f, new[] { "butane" }), ["whistle"] = P("box", 0.08f, 0.15f, 0.02f, new[] { "whistle" }), ["radio"] = P("box", 0.2f, 0.14f, 0.08f, new[] { "radio" }, 2),
            ["aa"] = P("box", 0.1f, 0.16f, 0.02f, new[] { "aa", "aaa" }), ["flash"] = P("box", 0.08f, 0.22f, 0.05f, new[] { "flash" }), ["power"] = P("box", 0.1f, 0.16f, 0.04f, new[] { "power" }),
            ["candle"] = P("box", 0.12f, 0.2f, 0.06f, new[] { "candle" }), ["aid"] = P("box", 0.26f, 0.18f, 0.1f, new[] { "aid" }, 2), ["mask"] = P("box", 0.2f, 0.12f, 0.1f, new[] { "mask" }, 3),
            ["sanit"] = P("bottle5", 0.068f, 0.21f, 0.068f, new[] { "sanit" }), ["band"] = P("box", 0.1f, 0.08f, 0.03f, new[] { "band" }, 3), ["deter"] = P("box", 0.25f, 0.3f, 0.12f, new[] { "deter" }),
            ["towel"] = P("box", 0.24f, 0.25f, 0.12f, new[] { "towel" }), ["milk"] = P("box", 0.075f, 0.2f, 0.075f, new[] { "milk" }), ["yogurt"] = P("cupS", 0.066f, 0.06f, 0.066f, new[] { "yogurt" }, 2),
            ["tofu"] = P("box", 0.12f, 0.05f, 0.1f, new[] { "tofu" }, 2), ["mix"] = P("box", 0.3f, 0.1f, 0.1f, new[] { "mix" }, 2), ["soup"] = P("pouch", 0.14f, 0.19f, 0.02f, new[] { "soup" }),
            ["cereal"] = P("box", 0.2f, 0.28f, 0.07f, new[] { "cereal" }), ["oil"] = P("bottle5", 0.068f, 0.21f, 0.068f, new[] { "oil", "soy" }), ["kimchi"] = P("pouch", 0.16f, 0.2f, 0.06f, new[] { "kimchi" }),
            ["pack6"] = P("box", 0.34f, 0.33f, 0.23f, new[] { "pack6", "pack6b" }),
            ["gim"] = P("pouch", 0.22f, 0.05f, 0.15f, new[] { "gim" }, 3), ["soymilk"] = P("box", 0.06f, 0.11f, 0.04f, new[] { "soymilk" }, 2), ["gum"] = P("box", 0.06f, 0.1f, 0.02f, new[] { "gum" }),
        };
        static Product P(string shape, float w, float h, float d, string[] labels, int stack = 1) => new Product { shape = shape, w = w, h = h, d = d, labels = labels.ToList(), stack = stack };
        internal static readonly HashSet<string> FIXED = new HashSet<string> { "bottle2", "bottle5", "can", "tin", "butane", "cup", "cupS" };

        // Bottom-centred unit geometries; lathe UV v follows real height so caps and label bands land right.
        static GeoData Lathe(float[,] profile, int seg)
        {
            int n = profile.GetLength(0); float H = profile[n - 1, 1];
            var pts = new List<Vector2>(); for (int i = 0; i < n; i++) pts.Add(new Vector2(profile[i, 0], profile[i, 1]));
            var g = Geo.Lathe(pts, seg);
            for (int i = 0; i < g.uv.Count; i++) g.uv[i] = new Vector2(g.uv[i].x, g.pos[i].y / H);
            return g.RotateY(Mathf.PI);
        }
        static GeoData CylG(float rt, float rb, float h, int seg) => Geo.Cylinder(rt, rb, h, seg).Translate(0, h / 2, 0).RotateY(Mathf.PI);
        static Dictionary<string, GeoData> MakeGeoms(bool hi)
        {
            int s = hi ? 10 : 7;
            return new Dictionary<string, GeoData>
            {
                ["bottle2"] = Lathe(new float[,] { { 0, 0 }, { 0.047f, 0.003f }, { 0.054f, 0.03f }, { 0.053f, 0.08f }, { 0.054f, 0.15f }, { 0.054f, 0.2f }, { 0.05f, 0.235f }, { 0.03f, 0.27f }, { 0.016f, 0.288f }, { 0.017f, 0.3f }, { 0.017f, 0.32f }, { 0, 0.32f } }, s),
                ["bottle5"] = Lathe(new float[,] { { 0, 0 }, { 0.03f, 0.003f }, { 0.033f, 0.02f }, { 0.032f, 0.07f }, { 0.033f, 0.13f }, { 0.028f, 0.16f }, { 0.014f, 0.185f }, { 0.014f, 0.21f }, { 0, 0.21f } }, s),
                ["can"] = CylG(0.033f, 0.033f, 0.123f, s), ["tin"] = CylG(0.045f, 0.045f, 0.046f, s + 2), ["butane"] = CylG(0.033f, 0.033f, 0.19f, s),
                ["cup"] = CylG(0.055f, 0.044f, 0.1f, s + 2), ["cupS"] = CylG(0.033f, 0.027f, 0.06f, s),
                ["box"] = Geo.Box(1, 1, 1).Translate(0, 0.5f, 0), ["mbox"] = Geo.Box(1, 1, 1).Translate(0, 0.5f, 0),
                ["pouch"] = Pillow(),
            };
        }
        // Pillow pack: a 2x2-segment box whose front/back faces bulge out (about 32 triangles).
        static GeoData Pillow()
        {
            var g = Geo.Box(1, 1, 1, 2, 2, 1);
            for (int i = 0; i < g.pos.Count; i++)
            {
                var p = g.pos[i]; float x = Mathf.Abs(p.x), y = Mathf.Abs(p.y);
                if (x < 0.4f && y < 0.4f) p.z *= 1.35f; else if (x < 0.4f || y < 0.4f) p.z *= 1.12f;
                g.pos[i] = p;
            }
            ComputeNormals(g);
            return g.Translate(0, 0.5f, 0);
        }
        // BufferGeometry.computeVertexNormals for an indexed geometry.
        internal static void ComputeNormals(GeoData g)
        {
            var n = new Vector3[g.pos.Count];
            for (int i = 0; i + 2 < g.index.Count; i += 3)
            {
                int a = g.index[i], b = g.index[i + 1], c = g.index[i + 2];
                var f = Vector3.Cross(g.pos[c] - g.pos[b], g.pos[a] - g.pos[b]);
                n[a] += f; n[b] += f; n[c] += f;
            }
            g.normal = n.Select(v => v.normalized).ToList();
        }

        static readonly Dictionary<string, string> MAT_KIND = new Dictionary<string, string>
        { ["bottle2"] = "pet", ["bottle5"] = "pet", ["can"] = "tin", ["tin"] = "tin", ["butane"] = "tin", ["mbox"] = "tin", ["cup"] = "paper", ["cupS"] = "paper", ["box"] = "paper", ["pouch"] = "film" };

        // Atlas + materials, built once per kit.
        static GoodsShared Shared(Kit kit) => kit.Memo("commerce:goods", () =>
        {
            bool hi = kit.Quality != "low"; int S = hi ? 2048 : 1024; float c = (float)S / CommerceLabels.GRID, pad = 3f / S;
            var atlas = kit.Texture(kit.Canvas(S, S, (g, W, H) =>
            {
                for (int i = 0; i < CommerceLabels.LABELS.Count; i++)
                {
                    g.save(); g.translate((i % CommerceLabels.GRID) * c, (i / CommerceLabels.GRID) * c);
                    g.beginPath(); g.rect(0, 0, c, c); g.clip(); CommerceLabels.DrawLabel(g, c, CommerceLabels.LABELS[i].Value); g.restore();
                }
            }));
            // Shelf-edge price strip (1 m per repeat) and the red '품절' tag.
            var strip = kit.Texture(kit.Canvas(512, 32, (g, W, H) =>
            {
                var r = Kit.Rng(5); g.fillStyle = "#f4f4f0"; g.fillRect(0, 0, 512, 32);
                for (int x = 6; x < 500; x += 64 + (int)(r() * 30))
                {
                    bool promo = r() < 0.2f; g.fillStyle = promo ? "#ffd23a" : "#ffffff"; g.fillRect(x, 3, 52, 26);
                    g.strokeStyle = "#c9c9c4"; g.strokeRect(x, 3, 52, 26);
                    g.fillStyle = promo ? "#c4161c" : "#1d1d1f"; g.font = $"800 15px {Kit.FONT}"; g.textAlign = "center";
                    g.fillText(((1 + (int)(r() * 18)) * 500 - 10).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture), x + 26, 23);
                }
            }));
            var tagMat = Mat.Lit(new MatSpec { map = kit.SignTexture(new[] { "품절" }, new SignStyle { w = 256, h = 128, bg = "#d7262d", fg = "#ffffff", border = "#ffffff" }), roughness = 0.5f, emissive = J.Hex(0x330000) });
            return new GoodsShared { geoms = MakeGeoms(hi), atlas = atlas, pad = pad, stripMat = Mat.Lit(new MatSpec { map = strip, roughness = 0.4f }), tagMat = tagMat, deep = hi ? 2 : 1 };
        });

        // The shape's unit geometry with UVs moved into one atlas cell (JS: vMapUv * scale + aCell in the shader).
        internal static GeoData CellGeo(GoodsShared S, string shape, int cell)
        {
            var key = shape + "#" + cell;
            if (S.cellGeo.TryGetValue(key, out var g)) return g;
            int GRID = CommerceLabels.GRID; float scale = 1f / GRID - 2 * S.pad;
            float cu = (float)(cell % GRID) / GRID + S.pad, cv = 1 - (float)(cell / GRID + 1) / GRID + S.pad;
            g = S.geoms[shape].Clone();
            for (int i = 0; i < g.uv.Count; i++) g.uv[i] = new Vector2(g.uv[i].x * scale + cu, g.uv[i].y * scale + cv);
            return S.cellGeo[key] = g;
        }
        // paper / film / pet / tin over the atlas, tinted by the instance brightness (three.js instanceColor, linear).
        internal static Material GoodsMat(GoodsShared S, string shape, float tint)
        {
            var kind = MAT_KIND[shape]; var key = kind + "#" + tint.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            if (S.mats.TryGetValue(key, out var m)) return m;
            float v = Mathf.LinearToGammaSpace(tint);
            var spec = new MatSpec { map = S.atlas, color = new Color(v, v, v) };
            if (kind == "paper") spec.roughness = 0.62f;
            else if (kind == "film") spec.roughness = 0.32f;
            else if (kind == "pet") { spec.roughness = 0.08f; spec.envMapIntensity = 1.6f; }
            else { spec.roughness = 0.3f; spec.metalness = 0.6f; }
            return S.mats[key] = Mat.Lit(spec);
        }

        // Per-level goods collector. Stock() fills shelf runs; Build() returns { group, sections: { id: { full, out } } }.
        public static Goods CreateGoods(Kit kit) => new Goods(kit, Shared(kit));

        // An instanced list with per-instance colours (sRGB): one batch per distinct colour, in first-seen order.
        internal static GameObject InstancedByColor(Kit kit, GeoData geo, Func<Color, Material> matFor, IList<Xform> list, IList<Color> colors, bool cast = true, bool receive = true, string name = "instanced")
        {
            var g = J.Group(name);
            foreach (var grp in Enumerable.Range(0, list.Count).GroupBy(i => colors[i]))
                g.Add(kit.Instanced(geo, matFor(grp.Key), grp.Select(i => list[i]).ToList(), cast, receive));
            return g;
        }
        // sRGB of (linear base colour x linear k), i.e. three.js color.set(css).multiplyScalar(k) / setScalar(k).
        internal static Color Scaled(Color srgb, float k) { var c = srgb.linear * k; c.a = 1; return c.gamma; }
        // ponytail: per-instance random brightness snaps to a few steps so instances share materials.
        internal static float Step(float k, float step = 0.05f) => Mathf.Round(k / step) * step;
    }
}
