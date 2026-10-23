// Ports levels/commerce-goods.js: shared by the mart, the store and the office — packaged goods (label atlas,
// instanced per product), shelf runs (gondolas), carts and baskets, ceiling panels and lamps, the painted view
// out of the windows per mood, NPC placement, solids from bounds.
using System;
using System.Collections.Generic;
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

    // goods.stock({ sec, x, z, dir, face, len, depth, y, clear, items, seed, fill, edge, rows, stack })
    public sealed class StockRun
    {
        public string sec = "misc", dir, face;
        public float x, z, len, depth = 0.42f, y, clear = 0.32f, fill = 0.92f;
        public List<string> items = new List<string>();
        public int seed = 1;
        public bool edge = true;
        public int? rows, stack;
    }

    public sealed class GoodsSection
    {
        public GameObject full, @out;
    }

    public sealed class Goods
    {
        public void Stock(StockRun run) => throw new NotImplementedException("port: levels/commerce-goods.js:stock");
        // { group, sections: { id: { full, out } } } — sections swap to empty shelves when sold out.
        public (GameObject group, Dictionary<string, GoodsSection> sections) Build() => throw new NotImplementedException("port: levels/commerce-goods.js:build");
    }

    public sealed class GondolaOpts
    {
        public float x, z, len, height = 1.86f, depth = 0.45f;
        public string axis = "z", sides = "both", color = "#e9e9e5";
        public float[] levels = { 0.12f, 0.5f, 0.86f, 1.22f, 1.58f };
    }

    // windowView(): a painted outside view; Set(mood) repaints.
    public sealed class WindowView
    {
        public GameObject mesh;
        public string Mood => throw new NotImplementedException("port: levels/commerce-goods.js:windowView.mood");
        public void Set(string mood) => throw new NotImplementedException("port: levels/commerce-goods.js:windowView.set");
    }

    // npc(people, preset, seed, at): placement and starting state.
    public sealed class NpcAt
    {
        public float x, y, z, ry;
        public string anim;
        public List<string> props;
    }

    public static class CommerceGoods
    {
        public static readonly Dictionary<string, Product> PRODUCTS = new Dictionary<string, Product>();
        public static readonly string[] MOODS = { "day", "dusk", "night", "blackout" };

        public static Goods CreateGoods(Kit kit) => throw new NotImplementedException("port: levels/commerce-goods.js:createGoods");
        // carts / baskets: placements (x, y, z, ry). Returns their group.
        public static GameObject CartsAndBaskets(Kit kit, IList<Xform> carts = null, IList<Xform> baskets = null) => throw new NotImplementedException("port: levels/commerce-goods.js:cartsAndBaskets");
        public static (GameObject group, List<Solid> runs) Gondola(Kit kit, GondolaOpts o) => throw new NotImplementedException("port: levels/commerce-goods.js:gondola");
        // Ceiling light panels at list positions (light group `group`, default 'indoor').
        public static GameObject Panels(Kit kit, IList<Xform> list, float w = 1.2f, float d = 0.3f, string color = "#f3f6ff", float on = 2.4f, string group = "indoor") => throw new NotImplementedException("port: levels/commerce-goods.js:panels");
        public static GameObject Lamps(Kit kit, IList<Xform> list, Color? color = null, float intensity = 14, float distance = 16, string group = "indoor") => throw new NotImplementedException("port: levels/commerce-goods.js:lamps");
        // kind: street | ... ; seed picks the variation.
        public static WindowView MakeWindowView(Kit kit, float w, float h, string kind = "street", int seed = 3) => throw new NotImplementedException("port: levels/commerce-goods.js:windowView");
        public static string MoodFor(string env, string fallback) => env switch { "day" => "day", "afternoon" => "day", "dusk" => "dusk", "night" => "night", "blackout" => "blackout", "interiorDark" => "blackout", _ => fallback };
        // A person placed and animated; an invisible stand-in if people fail, so levels never crash.
        public static IPerson Npc(IPeople people, string preset, int seed, NpcAt at = null) => throw new NotImplementedException("port: levels/commerce-goods.js:npc");
        // Solid from an object's world bounds (before Kit.Merge removes its meshes), JS space.
        public static Solid SolidOf(GameObject obj, float pad = 0, string id = null) => throw new NotImplementedException("port: levels/commerce-goods.js:solidOf");
        public static float YawTo(float dx, float dz) => Mathf.Atan2(-dx, -dz);
    }
}
