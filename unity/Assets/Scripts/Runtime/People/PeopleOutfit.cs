// Ports engine/people-outfit.js: character specs — the named cast and a seeded generator for believable Korean
// neighbourhood crowds (age group drives height, posture, clothing and hair).
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.People
{
    // A garment / hair / shoe entry. len is the outer garment's LEN key; lenF is a skirt's hem (fraction of H).
    public sealed class Garment
    {
        public string kind, c, pbr, len, sole, style, legs, belt, collar, sleeves;
        public float? lenF;
        public bool open;
        public Garment Clone() => (Garment)MemberwiseClone();
    }

    public sealed class Extras
    {
        public string scarf, lanyard, cap, glasses, tie, mask;
        public bool nameTag;
        public Extras Clone() => (Extras)MemberwiseClone();
    }

    // Bags baked into the body: backpack / crossbody colour.
    public sealed class Bake
    {
        public string backpack, crossbody;
        public Bake Clone() => (Bake)MemberwiseClone();
    }

    // { H, sex, girth, belly, stoop, skin, hair, top, outer, bottom, shoes, extras, props, bake }
    public sealed class PersonSpec
    {
        public float H = 1.72f, girth = 1, belly, stoop;
        public string sex = "m", skin;
        public Garment hair, top, outer, bottom, shoes;
        public Extras extras = new Extras();
        public List<string> props = new List<string>();
        public Bake bake = new Bake();

        // structuredClone
        public PersonSpec Clone()
        {
            var s = (PersonSpec)MemberwiseClone();
            s.hair = hair?.Clone(); s.top = top?.Clone(); s.outer = outer?.Clone(); s.bottom = bottom?.Clone(); s.shoes = shoes?.Clone();
            s.extras = extras?.Clone(); s.props = props != null ? new List<string>(props) : null; s.bake = bake?.Clone();
            return s;
        }
    }

    public static class PeopleOutfit
    {
        static Garment G(string kind, string c, string pbr = null) => new Garment { kind = kind, c = c, pbr = pbr };

        public static readonly Dictionary<string, PersonSpec> PRESETS = new Dictionary<string, PersonSpec>
        {
            ["jeongmin"] = new PersonSpec { H = 1.74f, sex = "m", girth = 1, skin = "#dcb391", hair = new Garment { style = "short", c = "#17130f" },
                top = G("tee", "#d9d7d0", "knit"), outer = new Garment { kind = "jacket", c = "#1e2840", open = true, len = "hip" },
                bottom = G("trousers", "#5c5f65"), shoes = new Garment { kind = "sneaker", c = "#25272b", sole = "#e9e6df" } },
            ["seoyeon"] = new PersonSpec { H = 1.62f, sex = "f", girth = 0.95f, skin = "#ecc9aa", hair = new Garment { style = "bob", c = "#2b1d15" },
                top = G("knit", "#ece3d2", "knit"), outer = new Garment { kind = "coat", c = "#a87d52", len = "knee" },
                bottom = G("slacks", "#2a2a2f"), shoes = G("flat", "#3b2b22"), extras = new Extras { scarf = "#7a2e35" }, props = new List<string> { "walker" } },
            ["manager"] = new PersonSpec { H = 1.68f, sex = "m", girth = 1.07f, belly = 0.012f, stoop = 0.08f, skin = "#cc9f7c", hair = new Garment { style = "short", c = "#9b9893" },
                top = G("shirt", "#a9bfd6", "check"), outer = new Garment { kind = "vest", c = "#1e2a46", len = "waist" },
                bottom = G("slacks", "#3a3b40"), shoes = G("dress", "#3b2a1f"), extras = new Extras { lanyard = "#1f4e8c" } },
            ["clerk"] = new PersonSpec { H = 1.63f, sex = "f", girth = 0.96f, skin = "#eecbab", hair = new Garment { style = "tied", c = "#3a281d" },
                top = G("hoodie", "#8b9097", "knit"), outer = new Garment { kind = "vest", c = "#2e8b4f", open = true, len = "waist" },
                bottom = G("jeans", "#3d5a80", "denim"), shoes = new Garment { kind = "sneaker", c = "#efeee8", sole = "#efeee8" }, extras = new Extras { nameTag = true } },
            ["guide"] = new PersonSpec { H = 1.75f, sex = "m", girth = 1.04f, skin = "#d2a582", hair = new Garment { style = "short", c = "#151210" },
                top = G("windbreaker", "#202634", "nylon"), outer = new Garment { kind = "safety", c = "#d8f23a", open = true, len = "waist" },
                bottom = G("trousers", "#23252a"), shoes = G("boot", "#1c1c1e"), extras = new Extras { cap = "#1f2433" }, props = new List<string> { "baton" } },
            ["coworker"] = new PersonSpec { H = 1.64f, sex = "f", girth = 0.97f, skin = "#e8c4a2", hair = new Garment { style = "long", c = "#241912" },
                top = G("blouse", "#f1eee8"), outer = new Garment { kind = "cardigan", c = "#b9a58a", pbr = "knit", open = true, len = "hip" },
                bottom = G("slacks", "#30323a"), shoes = G("flat", "#1f1f22"), extras = new Extras { lanyard = "#c23b3b", glasses = "#2a2420" } },
            ["elder"] = new PersonSpec { H = 1.53f, sex = "f", girth = 1.06f, belly = 0.008f, stoop = 0.22f, skin = "#c99c78", hair = new Garment { style = "perm", c = "#bdbab5" },
                top = G("knit", "#7d4655", "knit"), outer = new Garment { kind = "vest", c = "#545a48", pbr = "quilt", len = "waist" },
                bottom = G("trousers", "#4c4640"), shoes = new Garment { kind = "sneaker", c = "#5c5650", sole = "#d9d4cb" } },
        };

        static readonly string[] SKIN = { "#f0d2b8", "#e9c4a3", "#ddb391", "#d2a481", "#c69672" };
        static readonly string[] HAIR_DARK = { "#15110e", "#1f1712", "#2b1e16", "#3e2b1f", "#5a3e2a" };
        static readonly string[] HAIR_GREY = { "#77736e", "#9b9893", "#bdbab5", "#d7d4cf" };
        static readonly string[] OUTER_C = { "#1c1d21", "#2a2f3a", "#3b3f45", "#56514b", "#8b7d6b", "#a68a64", "#5b6150", "#6e5a4a", "#2e3b4e", "#7c7f84", "#c8bca6" };
        static readonly string[] HIKE_C = { "#b8352f", "#cf6a2a", "#2a7f8a", "#5b3f8f", "#2f6db0", "#3d8b45", "#a33f6b" };
        static readonly string[] TOP_C = { "#e9e6df", "#d8d3c9", "#2b2b2e", "#7a8088", "#b9c3cf", "#c7b9a3", "#8e3b46", "#3f5a7a", "#5d6b4f", "#f2efe6" };
        static readonly string[] BOT_C = { "#1f2024", "#2c2f36", "#4a4c52", "#6b625a", "#9c8f7d", "#3b3a36" };
        static readonly string[] JEANS_C = { "#3d5a80", "#2b3b55", "#56708f", "#1e2533" };
        static readonly string[] BAG_C = { "#2b3242", "#1c1d21", "#4b5a3c", "#6d2f33", "#3a4f6b", "#8a7a62", "#2f2f2f" };

        // JS `x >>> 0` of a double (ToUint32): the seed hashes multiply in doubles before wrapping.
        internal static uint ToUint32(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            double m = Math.Truncate(v) % 4294967296.0;
            if (m < 0) m += 4294967296.0;
            return (uint)m;
        }

        // arr[(r() * arr.length) | 0]; the index is clamped because a float rng can round up to 1.
        internal static T Pick<T>(Func<float> r, T[] a) => a[Math.Min(a.Length - 1, (int)(r() * a.Length))];

        // Random crowd member. Age group drives height, posture, clothing and hair.
        public static PersonSpec CrowdSpec(int seed)
        {
            var r = Kit.Rng(ToUint32(seed * 2654435761.0));
            string pick(string[] a) => Pick(r, a);
            bool p(float x) => r() < x;
            float u = r(); string age = u < 0.3f ? "young" : u < 0.65f ? "adult" : u < 0.87f ? "middle" : "elder";
            bool f = p(0.52f), old = age == "elder", middle = age == "middle";
            float H = (f ? 1.555f + r() * 0.12f : 1.665f + r() * 0.13f) - (old ? 0.05f : middle ? 0.015f : 0);
            var s = new PersonSpec { H = H, sex = f ? "f" : "m" };
            s.girth = 0.92f + r() * (old || middle ? 0.22f : 0.16f); s.skin = pick(SKIN);
            if (!f && (middle || old)) s.belly = r() * 0.014f;
            s.stoop = old ? 0.12f + r() * 0.14f : middle ? r() * 0.05f : 0;
            bool greyHair = old || (middle && p(0.45f));
            string hc = greyHair ? pick(HAIR_GREY) : pick(HAIR_DARK);
            s.hair = new Garment { c = hc, style = f ? (old || (middle && p(0.55f)) ? "perm" : pick(new[] { "bob", "long", "tied", "long" })) : "short" };
            // clothing
            string topKind = f ? pick(new[] { "knit", "blouse", "tee", "knit", "hoodie" }) : pick(new[] { "tee", "shirt", "knit", "hoodie", "shirt" });
            s.top = new Garment { kind = topKind, c = pick(TOP_C) };
            s.top.pbr = topKind == "knit" || topKind == "hoodie" ? "knit" : topKind == "shirt" && p(0.3f) ? "check" : "fabric";
            float o = r();
            if (middle && o < 0.4f || old && o < 0.3f) { s.outer = new Garment { kind = "jacket", c = pick(HIKE_C), pbr = "nylon", len = "hip" }; s.outer.open = p(0.3f); }
            else if (old && o < 0.75f) s.outer = new Garment { kind = "vest", c = pick(OUTER_C), pbr = "quilt", len = "waist" };
            else if (o < 0.6f) { s.outer = new Garment { kind = "jacket", c = pick(OUTER_C) }; s.outer.open = p(0.55f); s.outer.len = "hip"; s.outer.pbr = p(0.3f) ? "nylon" : "fabric"; }
            else if (o < 0.72f) { s.outer = new Garment { kind = "coat", c = pick(new[] { "#c8b08a", "#1c1d21", "#56514b", "#a68a64", "#3b3f45" }) }; s.outer.len = p(0.5f) ? "knee" : "thigh"; }
            else if (o < 0.8f) s.outer = new Garment { kind = "padded", c = pick(OUTER_C), len = "hip" };
            else if (o < 0.88f && f) s.outer = new Garment { kind = "cardigan", c = pick(new[] { "#b9a58a", "#8a8f96", "#d8cfc0", "#6d5a50" }), pbr = "knit", open = true, len = "hip" };
            if (s.top.kind == "shirt" && !f && age == "adult" && s.outer?.kind == "jacket" && !s.outer.open) s.extras.tie = pick(new[] { "#2a3550", "#5a2a30", "#3b3f45" });
            string bk = f && !old && p(0.3f) ? "skirt" : p(0.4f) && !old ? "jeans" : p(0.5f) ? "slacks" : "trousers";
            s.bottom = new Garment { kind = bk, c = bk == "jeans" ? pick(JEANS_C) : pick(BOT_C), pbr = bk == "jeans" ? "denim" : "fabric" };
            if (bk == "skirt") { s.bottom.lenF = 0.27f + r() * 0.08f; if (p(0.6f)) s.bottom.legs = pick(new[] { "#1d1d20", "#3a3532", "#2a2a30" }); }
            if (s.outer?.kind == "jacket" && s.outer.pbr == "nylon" && (middle || old)) s.bottom = new Garment { kind = "trousers", c = pick(new[] { "#2c2f36", "#4a4c52", "#3b3a36" }), pbr = "nylon" };
            if (f && !old && p(0.35f)) s.shoes = new Garment { kind = "flat", c = pick(new[] { "#1f1f22", "#3b2b22", "#8a6a52" }) };
            else if (p(0.65f)) { s.shoes = new Garment { kind = "sneaker", c = pick(new[] { "#f0eee8", "#25272b", "#7c7f84", "#2e3b4e", "#d8d3c9" }) }; s.shoes.sole = p(0.7f) ? "#ece9e2" : "#1d1d1f"; }
            else if (p(0.6f)) s.shoes = new Garment { kind = "dress", c = pick(new[] { "#1c1c1e", "#3b2a1f" }) };
            else s.shoes = new Garment { kind = "boot", c = pick(new[] { "#3b2a1f", "#1c1c1e", "#6b5440" }) };
            // accessories
            if (p(old ? 0.25f : 0.1f)) s.extras.mask = p(0.75f) ? "#f2f2f0" : "#202124";
            if (p(age == "young" ? 0.18f : 0.3f)) s.extras.glasses = pick(new[] { "#1b1b1d", "#4a3a2e", "#8a8f96" });
            if (!f && p(old ? 0.3f : 0.08f)) s.extras.cap = pick(new[] { "#2a2f3a", "#56514b", "#1c1d21", "#b9a58a" });
            if (s.outer?.kind != "coat" && !old && p(age == "young" ? 0.45f : age == "adult" ? 0.28f : 0.2f)) s.bake.backpack = pick(BAG_C);
            else if (p(0.22f)) s.bake.crossbody = pick(new[] { "#3a2e28", "#1c1d21", "#6b5440", "#8a3a3a" });
            if (p(0.15f) && s.extras.mask == null) s.extras.scarf = pick(new[] { "#7a2e35", "#c8bca6", "#3b3f45", "#5b6150" });
            return s;
        }

        // Spec for a preset name (unknown names fall back to a crowd member and report it).
        public static PersonSpec PresetSpec(string preset, int seed)
        {
            if (preset == "crowd") return CrowdSpec(seed);
            if (preset == null || !PRESETS.TryGetValue(preset, out var p)) { Debug.LogError($"people: unknown preset \"{preset}\", using crowd"); return CrowdSpec(seed); }
            return p.Clone();
        }
    }
}
