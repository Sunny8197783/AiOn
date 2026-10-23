// Ports engine/kit.js: shared building kit — procedural PBR surfaces, materials, facades, signs, switchable
// light groups and mesh helpers. Every level builds from this. Positions in At/Xform are JS space; the kit
// places objects with J.V / J.Euler. Box and cylinder y is the BOTTOM (kit.js convention).
// Inside Kit, Mat (the material class) must be written Gyeongbo.Engine.Mat: Kit.Mat is also a method.
using System;
using System.Collections.Generic;
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

    public sealed class Kit
    {
        public const string FONT = "'Pretendard','Noto Sans KR','Malgun Gothic','Apple SD Gothic Neo',sans-serif";

        // mulberry32: a seeded random in [0, 1) (rng(seed) in kit.js).
        public static Func<float> Rng(uint seed = 1) => throw new NotImplementedException("port: engine/kit.js:rng");

        public static Kit Create(string quality = "high") => throw new NotImplementedException("port: engine/kit.js:createKit");
        public string Quality => throw new NotImplementedException("port: engine/kit.js:quality");

        public T Memo<T>(string key, Func<T> make) => throw new NotImplementedException("port: engine/kit.js:memo");
        public Paint Canvas(int w, int h, Action<Paint, int, int> draw = null) => throw new NotImplementedException("port: engine/kit.js:canvas");
        // CanvasTexture: repeat lives on the material (MatSpec.repeat / Mat.SetTiling); color false = linear data.
        public Texture2D Texture(Paint c, bool color = true) => throw new NotImplementedException("port: engine/kit.js:texture");
        public Paint NormalFrom(Paint c, float strength = 2) => throw new NotImplementedException("port: engine/kit.js:normalFrom");
        // asphalt | sidewalk | concrete | tile | wood | grass | plaster | brick
        public Material Surface(string name, SurfaceOpts o = null) => throw new NotImplementedException("port: engine/kit.js:surface");
        public GeoData WorldUV(GeoData g, float w, float d) => throw new NotImplementedException("port: engine/kit.js:worldUV");
        // Preset name (curb, metal, steel, darkMetal, glass, darkGlass, rubber, plastic, fabric, whiteWall, yellow,
        // hazard, foliage, bark), 'paint:#rrggbb' or 'emit:#rrggbb'; extra overrides. Memoised per name + extra.
        public Material Mat(string name, MatSpec extra = null) => throw new NotImplementedException("port: engine/kit.js:mat");
        // apartment | office | shop | villa | civic; registered in light group 'windows'.
        public Material Facade(string kind, FacadeOpts o = null) => throw new NotImplementedException("port: engine/kit.js:facade");
        public Texture2D SignTexture(IList<string> lines, SignStyle style = null) => throw new NotImplementedException("port: engine/kit.js:signTexture");
        public GameObject Sign(IList<string> lines, SignOpts o = null) => throw new NotImplementedException("port: engine/kit.js:sign");
        public GameObject ShelterSign(float w = 0.45f) => throw new NotImplementedException("port: engine/kit.js:shelterSign");
        // arrow: left | right | up
        public GameObject GuideSign(string arrow = "right", float meters = 30, float w = 0.5f) => throw new NotImplementedException("port: engine/kit.js:guideSign");

        // ---- light groups: street | windows | shops | indoor | signs | emergency | any name ----
        // Lights are never removed, only dimmed to 0 (a changing light count is expensive).
        public Material Lit(string group, Material m, float? on = null) => throw new NotImplementedException("port: engine/kit.js:lit");
        public Light Lit(string group, Light l, float? on = null) => throw new NotImplementedException("port: engine/kit.js:lit");
        public void SetLit(string group, bool on, float scale = 1) => throw new NotImplementedException("port: engine/kit.js:setLit");
        // Per-object switch on top of its group (a lamp behind a shutter); obj is the Material or Light given to Lit.
        public void GateLit(UnityEngine.Object obj, bool on) => throw new NotImplementedException("port: engine/kit.js:gateLit");
        public bool IsLit(string group) => throw new NotImplementedException("port: engine/kit.js:isLit");

        // ---- mesh helpers (JS-space placement) ----
        public GameObject Place(GameObject mesh, At o = null, float lift = 0) => throw new NotImplementedException("port: engine/kit.js:place");
        public GameObject Box(float w, float h, float d, Material m, At o = null) => throw new NotImplementedException("port: engine/kit.js:box");
        public GameObject RBox(float w, float h, float d, float radius, Material m, At o = null) => throw new NotImplementedException("port: engine/kit.js:rbox");
        public GameObject Cyl(float rt, float rb, float h, Material m, At o = null, int seg = 16) => throw new NotImplementedException("port: engine/kit.js:cyl");
        // Horizontal plane at o.y, w (x) by d (z), world-scaled UVs; o.ry turns it about the vertical.
        public GameObject Plane(float w, float d, Material m, At o = null) => throw new NotImplementedException("port: engine/kit.js:plane");
        public GameObject Slab(float w, float h, float d, Material m, At o = null) => throw new NotImplementedException("port: engine/kit.js:slab");
        // Copies of one mesh; child i of the returned object is list[i] (move a child to update an instance).
        public GameObject Instanced(GeoData g, Material m, IList<Xform> list, bool cast = true, bool receive = true) => throw new NotImplementedException("port: engine/kit.js:instanced");
        // Collapse static meshes under root into one mesh per material (skips objects tagged keep). Returns root.
        public GameObject Merge(GameObject root) => throw new NotImplementedException("port: engine/kit.js:merge");
        // Excludes an object from Merge (three.js userData.keep = true).
        public static void Keep(GameObject o) => throw new NotImplementedException("port: engine/kit.js:userData.keep");

        // ---- lights (three.js units -> Unity; the conversion lives here only) ----
        public Light PointLight(Color color, float intensity, float distance = 0, float decay = 2) => throw new NotImplementedException("port: THREE.PointLight");
        public Light SpotLight(Color color, float intensity, float distance, float angle, float penumbra = 0, float decay = 2) => throw new NotImplementedException("port: THREE.SpotLight");
    }
}
