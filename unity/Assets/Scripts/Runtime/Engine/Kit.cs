// Ports engine/kit.js: shared building kit — procedural PBR surfaces, materials, facades, signs, switchable
// light groups and mesh helpers. Every level builds from this. Positions in At/Xform are JS space; the kit
// places objects with J.V / J.Euler. Box and cylinder y is the BOTTOM (kit.js convention).
// Inside Kit, Mat (the material class) must be written Gyeongbo.Engine.Mat: Kit.Mat is also a method.
// Surfaces: KitSurfaces.cs; facades and signs: KitSigns.cs; merge and lights: KitMesh.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Gyeongbo.Engine
{
    // kit.place options o: { x, y, z, ry, rx, rz, cast = true, receive = true }.
    public sealed class At
    {
        public float x, y, z, rx, ry, rz;
        public bool cast = true, receive = true;
    }

    // kit.instanced list item: { x, y, z, rx, ry, rz, s, sx, sy, sz }.
    public sealed class Xform
    {
        public float x, y, z, rx, ry, rz;
        public float? s, sx, sy, sz;
    }

    // kit.surface(name, { repeat, color, roughness }); color is a three.js hex (J.Hex) or null.
    public sealed class SurfaceOpts
    {
        public Vector2? repeat;
        public Color? color;
        public float? roughness;
    }

    // kit.facade(kind, { floors, cols, seed, label, lit, w, h }).
    public sealed class FacadeOpts
    {
        public int floors = 5, cols = 6, seed = 1, w = 1024, h = 1024;
        public string label = "";
        public float lit = 0.55f;
    }

    // kit.signTexture(lines, style): canvas pixels and colours (CSS strings).
    public sealed class SignStyle
    {
        public int w = 512, h = 256, weight = 700;
        public string bg = "#1f4e8c", fg = "#ffffff", border, align = "center";
        public float pad = 0.08f;
        public float[] sizes;
    }

    // kit.sign(lines, { w, h, emissive, single, ...style }): w/h in metres.
    public sealed class SignOpts
    {
        public float w = 1.2f, h = 0.6f, emissive;
        public bool single;
        public int weight = 700;
        public string bg = "#1f4e8c", fg = "#ffffff", border, align = "center";
        public float pad = 0.08f;
        public float[] sizes;
    }

    public sealed partial class Kit
    {
        public const string FONT = "'Pretendard','Noto Sans KR','Malgun Gothic','Apple SD Gothic Neo',sans-serif";

        // mulberry32: a seeded random in [0, 1) (rng(seed) in kit.js).
        public static Func<float> Rng(uint seed = 1)
        {
            uint a = seed;
            return () =>
            {
                unchecked
                {
                    a += 0x6D2B79F5;
                    uint t = a;
                    t = (t ^ (t >> 15)) * (t | 1);
                    t ^= t + (t ^ (t >> 7)) * (t | 61);
                    double v = (t ^ (t >> 14)) / 4294967296.0;
                    return (float)Math.Min(v, 0.99999994);   // a float never rounds up to 1 (index = (int)(r() * n) stays in range)
                }
            };
        }

        string quality;
        int S;
        readonly Dictionary<string, object> cache = new Dictionary<string, object>();

        public static Kit Create(string quality = "high") => new Kit { quality = quality, S = quality == "low" ? 256 : 512 };
        public string Quality => quality;

        public T Memo<T>(string key, Func<T> make)
        {
            if (!cache.TryGetValue(key, out var v)) { v = make(); cache[key] = v; }
            return (T)v;
        }

        public Paint Canvas(int w, int h, Action<Paint, int, int> draw = null)
        {
            var c = new Paint(w, h);
            draw?.Invoke(c, w, h);
            return c;
        }

        // CanvasTexture: repeat lives on the material (MatSpec.repeat / Mat.SetTiling); color false = linear data.
        public Texture2D Texture(Paint c, bool color = true)
        {
            var t = c.ToTexture(color);
            t.anisoLevel = 8;   // min(8, the GPU's max) like kit.js; Unity clamps to what the GPU supports
            return t;
        }

        // Grayscale height canvas -> tangent-space normal map canvas.
        public Paint NormalFrom(Paint c, float strength = 2) => c.NormalFrom(strength);

        sealed class SurfaceTex { public Texture2D map, normalMap; public float tile, rough; }
        SurfaceTex BaseSurface(string name) => Memo($"surfaceTex:{name}", () =>
        {
            var s = DrawSurface(name, Rng((uint)(name.Length * 977 + 13)));
            return new SurfaceTex { map = Texture(s.color), normalMap = Texture(NormalFrom(s.height, name == "sidewalk" || name == "tile" || name == "brick" ? 4 : 2), false), tile = s.tile, rough = s.rough };
        });
        // The shared colour texture of a surface (core.js wet look matches surface materials by it).
        internal Texture2D SurfaceMap(string name) => BaseSurface(name).map;

        static string Num(float? v) => v.HasValue ? v.Value.ToString("R", CultureInfo.InvariantCulture) : "undefined";

        // Material for a named surface (asphalt | sidewalk | concrete | tile | wood | grass | plaster | brick).
        // tile = metres per texture repeat; pass { repeat } to override.
        public Material Surface(string name, SurfaceOpts o = null)
        {
            o = o ?? new SurfaceOpts();
            string rep = o.repeat.HasValue ? Num(o.repeat.Value.x) + "," + Num(o.repeat.Value.y) : "undefined";
            string col = o.color.HasValue ? ColorUtility.ToHtmlStringRGBA(o.color.Value) : "undefined";
            return Memo($"surface:{name}:{rep}:{col}:{Num(o.roughness)}", () =>
            {
                var b = BaseSurface(name);
                var repeat = o.repeat ?? new Vector2(1 / b.tile, 1 / b.tile);
                return Gyeongbo.Engine.Mat.Lit(new MatSpec { map = b.map, normalMap = b.normalMap, roughness = o.roughness ?? b.rough, metalness = 0, color = o.color ?? Color.white, repeat = repeat });
            });
        }

        // World-space UVs: scale a mesh's UVs so a surface texture tiles by metres. Use on planes/boxes built at real size.
        public GeoData WorldUV(GeoData g, float w, float d)
        {
            for (int i = 0; i < g.uv.Count; i++) g.uv[i] = new Vector2(g.uv[i].x * w, g.uv[i].y * d);
            g.version++;
            return g;
        }

        // ---- plain PBR materials by name, or 'paint:#rrggbb', 'emit:#rrggbb' ----
        static MatSpec P(int color, float roughness, float? metalness = null, bool? transparent = null, float? opacity = null) =>
            new MatSpec { color = J.Hex(color), roughness = roughness, metalness = metalness, transparent = transparent, opacity = opacity };
        static readonly Dictionary<string, MatSpec> PRESETS = new Dictionary<string, MatSpec>
        {
            ["curb"] = P(0xb8b6b0, 0.8f), ["metal"] = P(0x9aa0a6, 0.35f, 0.85f),
            ["steel"] = P(0x5d6368, 0.45f, 0.8f), ["darkMetal"] = P(0x2a2d31, 0.5f, 0.7f),
            ["glass"] = P(0x9fb8c8, 0.05f, 0.9f, true, 0.55f),
            ["darkGlass"] = P(0x1d2a33, 0.08f, 0.9f), ["rubber"] = P(0x1b1b1c, 0.9f),
            ["plastic"] = P(0xdcdcdc, 0.45f), ["fabric"] = P(0x6b6f78, 1),
            ["whiteWall"] = P(0xeeeae2, 0.92f), ["yellow"] = P(0xf2c230, 0.6f),
            ["hazard"] = P(0xf0b323, 0.5f), ["foliage"] = P(0x4a6b33, 0.95f), ["bark"] = P(0x4b3a2c, 1),
        };
        static readonly ConditionalWeakTable<UnityEngine.Object, string> ids = new ConditionalWeakTable<UnityEngine.Object, string>();
        static int nextId;
        internal static string IdOf(UnityEngine.Object o) => ids.GetValue(o, _ => (++nextId).ToString(CultureInfo.InvariantCulture));
        static readonly FieldInfo[] SPEC_FIELDS = typeof(MatSpec).GetFields(BindingFlags.Public | BindingFlags.Instance);

        // { ...a, ...b }: b's non-null fields win.
        static MatSpec Spread(MatSpec a, MatSpec b)
        {
            var o = a.Clone();
            if (b != null) foreach (var f in SPEC_FIELDS) { var v = f.GetValue(b); if (v != null) f.SetValue(o, v); }
            return o;
        }
        // JSON.stringify(extra) for the memo key.
        static string SpecKey(MatSpec s)
        {
            if (s == null) return "";
            var parts = new List<string>();
            foreach (var f in SPEC_FIELDS)
            {
                var v = f.GetValue(s); if (v == null) continue;
                string t = v is UnityEngine.Object uo ? "#" + IdOf(uo) : v is float fl ? Num(fl) : v is Color c ? ColorUtility.ToHtmlStringRGBA(c) + Num(c.r) + Num(c.g) + Num(c.b) : v is Vector2 v2 ? Num(v2.x) + "," + Num(v2.y) : v.ToString();
                parts.Add(f.Name + ":" + t);
            }
            return "{" + string.Join(",", parts) + "}";
        }

        // Preset name (curb, metal, steel, darkMetal, glass, darkGlass, rubber, plastic, fabric, whiteWall, yellow,
        // hazard, foliage, bark), 'paint:#rrggbb' or 'emit:#rrggbb'; extra overrides. Memoised per name + extra.
        public Material Mat(string name, MatSpec extra = null)
        {
            return Memo($"mat:{name}:{SpecKey(extra)}", () =>
            {
                if (name.StartsWith("paint:", StringComparison.Ordinal)) return Gyeongbo.Engine.Mat.Lit(Spread(new MatSpec { color = J.Css(name.Substring(6)), roughness = 0.55f }, extra));
                if (name.StartsWith("emit:", StringComparison.Ordinal)) { var c = J.Css(name.Substring(5)); return Gyeongbo.Engine.Mat.Lit(Spread(new MatSpec { color = c, emissive = c, emissiveIntensity = 2, roughness = 0.4f }, extra)); }
                if (!PRESETS.TryGetValue(name, out var p)) throw new ArgumentException($"kit.mat: unknown material {name}");
                return Gyeongbo.Engine.Mat.Lit(Spread(p, extra));
            });
        }

        // ---- mesh helpers. o: { x, y, z, ry, rx, rz, cast=true, receive=true }. Box and cylinder y is the BOTTOM. ----
        public GameObject Place(GameObject mesh, At o = null, float lift = 0)
        {
            o = o ?? new At();
            J.SetPos(mesh, o.x, o.y + lift, o.z);
            J.SetRot(mesh, o.rx, o.ry, o.rz);
            var r = mesh.GetComponent<Renderer>();
            if (r != null) { r.shadowCastingMode = o.cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = o.receive; }
            return mesh;
        }

        public GameObject Box(float w, float h, float d, Material m, At o = null) => Place(Geo.Obj(Geo.Box(w, h, d), m, "box"), o, h / 2);
        public GameObject RBox(float w, float h, float d, float radius, Material m, At o = null) =>
            Place(Geo.Obj(Geo.RoundedBox(w, h, d, 3, Mathf.Min(radius, Mathf.Min(w / 2, Mathf.Min(h / 2, d / 2)))), m, "rbox"), o, h / 2);
        public GameObject Cyl(float rt, float rb, float h, Material m, At o = null, int seg = 16) => Place(Geo.Obj(Geo.Cylinder(rt, rb, h, seg), m, "cyl"), o, h / 2);

        // Horizontal plane at o.y, w (x) by d (z), world-scaled UVs; o.ry turns it about the vertical.
        public GameObject Plane(float w, float d, Material m, At o = null)
        {
            o = o ?? new At();
            var go = Geo.Obj(WorldUV(Geo.Plane(w, d), w, d), m, "plane");
            J.SetRot(go, -Mathf.PI / 2, 0, o.ry);
            J.SetPos(go, o.x, o.y, o.z);
            var r = go.GetComponent<Renderer>();
            r.receiveShadows = o.receive; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // Box with world-scaled UVs (for surface materials on walls/floors).
        public GameObject Slab(float w, float h, float d, Material m, At o = null)
        {
            var g = Geo.Box(w, h, d);
            for (int i = 0; i < g.uv.Count; i++)
            {
                float ax = Mathf.Abs(g.normal[i].x), ay = Mathf.Abs(g.normal[i].y);
                float su = ax > 0.5f ? d : w, sv = ax > 0.5f ? h : ay > 0.5f ? d : h;
                g.uv[i] = new Vector2(g.uv[i].x * su, g.uv[i].y * sv);
            }
            return Place(Geo.Obj(g, m, "slab"), o, h / 2);
        }

        // Copies of one mesh; child i of the returned object is list[i] (move a child to update an instance).
        public GameObject Instanced(GeoData g, Material m, IList<Xform> list, bool cast = true, bool receive = true)
        {
            var root = J.Group("instanced");
            Keep(root);
            var mesh = Geo.MeshOf(g);
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                var c = Geo.Obj(mesh, m, "instance", cast, receive);
                Keep(c);
                c.transform.SetParent(root.transform, false);
                J.SetPos(c, t.x, t.y, t.z); J.SetRot(c, t.rx, t.ry, t.rz);
                c.transform.localScale = new Vector3(t.sx ?? t.s ?? 1, t.sy ?? t.s ?? 1, t.sz ?? t.s ?? 1);
            }
            return root;
        }
    }
}
