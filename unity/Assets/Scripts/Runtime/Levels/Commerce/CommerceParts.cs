// Ports the fixture half of levels/commerce-goods.js: cartsAndBaskets, gondola, panels, lamps, windowView, moodFor,
// npc, solidOf, yawTo.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // windowView(): a painted outside view; Set(mood) repaints.
    public sealed class WindowView
    {
        public GameObject mesh;
        internal Material mat;
        internal Func<string, Texture2D> paint;
        string cur = "day";
        public string Mood => cur;
        public void Set(string mood)
        {
            if (mood == cur || !CommerceGoods.MOODS.Contains(mood)) return;
            cur = mood; Mat.SetMap(mat, paint(mood)); float k = mood == "day" ? 0.95f : 1; Mat.SetColor(mat, new Color(k, k, k));
        }
    }

    sealed class CartParts
    {
        public Dictionary<string, GeoData> cart;
        public GeoData basket;
        public Dictionary<string, Material> mats;
        public Material redSolid;
    }

    public static partial class CommerceGoods
    {
        public static readonly string[] MOODS = { "day", "dusk", "night", "blackout" };

        // Wire shopping carts (nested in a row) and plastic baskets (stacked): a few instanced parts each.
        public static GameObject CartsAndBaskets(Kit kit, IList<Xform> carts = null, IList<Xform> baskets = null)
        {
            carts ??= new List<Xform>(); baskets ??= new List<Xform>();
            var P = kit.Memo("commerce:carts", () =>
            {
                Texture2D grid(string color, float line, int cell) => kit.Texture(kit.Canvas(64, 64, (g, W, H) =>
                {
                    g.strokeStyle = color; g.lineWidth = line;
                    for (int i = 0; i <= 64; i += cell) { g.beginPath(); g.moveTo(i, 0); g.lineTo(i, 64); g.stroke(); g.beginPath(); g.moveTo(0, i); g.lineTo(64, i); g.stroke(); }
                }));
                var wire = Mat.Lit(new MatSpec { map = grid("#d8dde2", 3, 8), alphaTest = 0.5f, metalness = 0.8f, roughness = 0.35f, side = "double" });
                var red = Mat.Lit(new MatSpec { map = grid("#c8202f", 5, 11), alphaTest = 0.5f, roughness = 0.5f, side = "double", color = J.Hex(0xffffff) });
                GeoData T(GeoData geo, float x, float y, float z, float rx = 0, float ry = 0, float rz = 0) => geo.RotateX(rx).RotateY(ry).RotateZ(rz).Translate(x, y, z);
                GeoData box(float w, float h, float d, float x, float y, float z) => T(Geo.Box(w, h, d), x, y + h / 2, z);
                GeoData pl(float w, float h, float x, float y, float z, float rx = 0, float ry = 0) => T(Geo.Plane(w, h), x, y, z, rx, ry);
                const float PI = Mathf.PI;
                var cart = new Dictionary<string, GeoData>
                {
                    ["wire"] = Geo.Merge(new[] { pl(0.52f, 0.86f, 0, 0.5f, 0, -PI / 2), pl(0.86f, 0.42f, -0.27f, 0.72f, 0, 0, PI / 2), pl(0.86f, 0.42f, 0.27f, 0.72f, 0, 0, PI / 2),
                        pl(0.56f, 0.42f, 0, 0.72f, -0.43f), pl(0.5f, 0.36f, 0, 0.7f, 0.43f), pl(0.5f, 0.3f, 0, 0.22f, 0.05f, -PI / 2) }),
                    ["metal"] = Geo.Merge(new[] { box(0.03f, 0.03f, 0.9f, -0.22f, 0.12f, 0), box(0.03f, 0.03f, 0.9f, 0.22f, 0.12f, 0), box(0.03f, 0.85f, 0.03f, -0.27f, 0.12f, 0.45f), box(0.03f, 0.85f, 0.03f, 0.27f, 0.12f, 0.45f),
                        box(0.025f, 0.4f, 0.025f, -0.22f, 0.12f, -0.42f), box(0.025f, 0.4f, 0.025f, 0.22f, 0.12f, -0.42f) }),
                    ["red"] = Geo.Merge(new[] { box(0.58f, 0.05f, 0.05f, 0, 0.98f, 0.5f), box(0.5f, 0.12f, 0.02f, 0, 0.82f, -0.44f) }),
                    ["black"] = Geo.Merge(new[] { (-0.22f, -0.4f), (0.22f, -0.4f), (-0.22f, 0.4f), (0.22f, 0.4f) }.Select(p => T(Geo.Cylinder(0.06f, 0.06f, 0.04f, 10), p.Item1, 0.06f, p.Item2, 0, 0, PI / 2)).ToList()),
                };
                var basket = Geo.Merge(new[] { pl(0.44f, 0.3f, 0, 0.005f, 0, -PI / 2), pl(0.44f, 0.24f, 0, 0.12f, 0.15f), pl(0.44f, 0.24f, 0, 0.12f, -0.15f), pl(0.3f, 0.24f, 0.22f, 0.12f, 0, 0, PI / 2), pl(0.3f, 0.24f, -0.22f, 0.12f, 0, 0, PI / 2) });
                return new CartParts { cart = cart, basket = basket, mats = new Dictionary<string, Material> { ["wire"] = wire, ["red"] = red, ["metal"] = kit.Mat("metal"), ["black"] = kit.Mat("rubber") }, redSolid = kit.Mat("paint:#c8202f") };
            });
            var grp = J.Group("carts");
            if (carts.Count > 0) foreach (var k in new[] { "wire", "metal", "red", "black" }) grp.Add(kit.Instanced(P.cart[k], k == "red" ? P.redSolid : P.mats[k], carts));
            if (baskets.Count > 0) grp.Add(kit.Instanced(P.basket, P.mats["red"], baskets));
            return grp;
        }

        // Gondola shelving. axis 'z' runs north-south centred on x; 'x' runs east-west centred on z.
        // sides: 'both' | 'neg' | 'pos' (face toward -x/-z or +x/+z). Returns { group, runs: [{ side, y, clear, x, z, dir, face, len, depth }] }.
        public static (GameObject group, List<StockRun> runs) Gondola(Kit kit, GondolaOpts o)
        {
            var g = J.Group("gondola");
            var steel = kit.Mat("paint:" + o.color, new MatSpec { metalness = 0.4f, roughness = 0.45f }); var dark = kit.Mat("darkMetal");
            float x = o.x, z = o.z, len = o.len, depth = o.depth, height = o.height; string sides = o.sides; var levels = o.levels;
            bool along = o.axis == "z"; float wide = sides == "both" ? depth * 2 + 0.05f : depth + 0.03f;
            float off = sides == "both" ? 0 : (sides == "pos" ? 1 : -1) * (depth / 2);
            (float x, float z) P(float a, float b) => along ? (x + b, z + a) : (x + a, z + b);
            GameObject B(float lA, float h, float lB, float a = 0, float b = 0, float y = 0, Material m = null)
            {
                var p = P(a, b);
                return kit.Box(along ? lB : lA, h, along ? lA : lB, m ?? steel, new At { x = p.x, y = y, z = p.z });
            }
            g.Add(B(len, height, 0.04f, b: sides == "both" ? 0 : off - (sides == "pos" ? 1 : -1) * depth / 2, m: kit.Mat("paint:#d9dad6", new MatSpec { roughness = 0.7f })));
            foreach (var e in new[] { -1, 1 }) g.Add(B(0.03f, height, wide, a: e * (len / 2 + 0.015f), b: off));
            g.Add(B(len, 0.1f, wide - 0.06f, b: off, m: dark));
            var runs = new List<StockRun>();
            foreach (var sgn in sides == "both" ? new[] { -1, 1 } : new[] { sides == "pos" ? 1 : -1 })
            {
                float bOff = sides == "both" ? sgn * (depth / 2 + 0.025f) : off;
                for (int i = 0; i < levels.Length; i++)
                {
                    float ly = levels[i];
                    g.Add(B(len, 0.025f, depth, b: bOff, y: ly - 0.025f));
                    float clear = (i + 1 < levels.Length ? levels[i + 1] : height + 0.25f) - ly - 0.04f;
                    float front = sides == "both" ? sgn * (depth + 0.025f) : off + sgn * depth / 2;
                    var s = P(-len / 2, front);
                    runs.Add(new StockRun { side = sgn, y = ly, clear = clear, x = s.x, z = s.z, dir = along ? new[] { 0f, 1f } : new[] { 1f, 0f }, face = along ? new[] { (float)sgn, 0f } : new[] { 0f, sgn }, len = len, depth = depth });
                }
            }
            return (g, runs);
        }

        // Emissive ceiling fixtures (instanced) registered in a light group; returns the mesh and its material.
        public static (GameObject obj, Material mat) Panels(Kit kit, IList<Xform> list, float w = 1.2f, float d = 0.3f, string color = "#f3f6ff", float on = 2.4f, string group = "indoor")
        {
            var m = Mat.Lit(new MatSpec { color = J.Hex(0xf6f6f6), emissive = J.Css(color), roughness = 0.4f });
            kit.Lit(group, m, on);
            return (kit.Instanced(Geo.Box(w, 0.035f, d), m, list, false, false), m);
        }
        // Real point lights registered in a light group.
        public static GameObject Lamps(Kit kit, IList<Xform> list, Color? color = null, float intensity = 14, float distance = 16, string group = "indoor")
        {
            var g = J.Group("lamps");
            foreach (var p in list)
            {
                var l = kit.PointLight(color ?? J.Hex(0xf2f4ff), intensity, distance, 2); J.SetPos(l.gameObject, p.x, p.y, p.z);
                kit.Lit(group, l, intensity); g.Add(l);
            }
            return g;
        }

        // Painted view outside a window. kind 'street' (shops, road) or 'sky' (horizon only). Set(mood) swaps the picture.
        public static WindowView MakeWindowView(Kit kit, float w, float h, string kind = "street", int seed = 3)
        {
            Texture2D paint(string mood) => kit.Memo($"commerce:view:{kind}:{mood}:{seed}", () => kit.Texture(kit.Canvas(1024, 512, (g, W, H) => CommerceView.Draw(g, W, H, kind, mood, seed))));
            var m = Mat.Unlit(new MatSpec { map = paint("day"), fog = false });
            return new WindowView { mesh = Geo.Obj(Geo.Plane(w, h), m, "windowView"), mat = m, paint = paint };
        }
        // Map an engine env preset to an outside mood (interiors default to `fallback`).
        public static string MoodFor(string env, string fallback) => env switch { "day" => "day", "afternoon" => "day", "dusk" => "dusk", "night" => "night", "blackout" => "blackout", "interiorDark" => "blackout", _ => fallback };

        // A person placed and animated; an invisible stand-in if people fail, so levels never crash.
        static bool warned;
        public static IPerson Npc(IPeople people, string preset, int seed, NpcAt at = null)
        {
            at ??= new NpcAt();
            IPerson p = null;
            try { p = people?.CreatePerson(preset, seed); } catch (Exception e) { Debug.LogError($"[commerce] createPerson failed {preset} {e}"); }
            if (p == null)
            {
                if (!warned) { warned = true; Debug.LogError("[commerce] people.createPerson unavailable; NPCs are invisible stand-ins"); }
                p = new CommerceStandIn(preset);
            }
            J.SetPos(p.Group, at.x, at.y, at.z); J.SetYaw(p.Group, at.ry);
            if (at.anim != null) p.SetAnim(at.anim);
            foreach (var pr in at.props ?? new List<string>()) p.SetProp(pr, true);
            return p;
        }

        // Axis-aligned solid from an object's world bounds (call before Kit.Merge removes the meshes), JS space.
        public static Solid SolidOf(GameObject obj, float pad = 0, string id = null)
        {
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var mf in obj.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    min = Vector3.Min(min, c); max = Vector3.Max(max, c);
                }
            }
            // Unity x is mirrored: JS x0 = -(Unity max x).
            return new Solid { x0 = -max.x - pad, x1 = -min.x + pad, y0 = min.y, y1 = max.y, z0 = min.z - pad, z1 = max.z + pad, id = id };
        }
        // Facing a direction: rotation.y for a model that faces -z.
        public static float YawTo(float dx, float dz) => Mathf.Atan2(-dx, -dz);
    }
}
