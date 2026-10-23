// Ports levels/apartment-parts.js: floor heights, the apartment builder B (walk/solid/point registries,
// powered and backup light lists, sensors, auto doors, ticks, shared materials, walls with holes, windows,
// doors, rods, plates, canvas textures, exit signs) and the exit-sign runner pictogram.
// FL is shared with Systems/Cutscenes (the 302호 floor height).
// Frame: x east, z south, metres (JS space). Floor N stands at y = FH * (N - 1). The builder takes doubles so
// the JS numbers copy verbatim; it casts once at the Kit / Core boundary.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // A hole in a wall: span a..b along the wall, y0..y1; door = the wall's solid has a gap there too.
    public sealed class WallHole
    {
        public double a, b, y0, y1;
        public bool door;
        public WallHole() { }
        public WallHole(double a, double b, double y0, double y1, bool door = false) { this.a = a; this.b = b; this.y0 = y0; this.y1 = y1; this.door = door; }
    }

    // wall options { t = 0.1, side = 1, holes = [], solid = true }
    public sealed class WallOpts
    {
        public double t = 0.1;
        public int side = 1;
        public List<WallHole> holes = new List<WallHole>();
        public bool solid = true;
    }

    // An animated door leaf: k 0..1 open; set(k) moves it; auto = open radius for auto doors.
    public sealed class AptDoor
    {
        public float x, y, z, k, target, auto;
        public GameObject pivot;
        public Solid solid;
        public Action<float> set;
    }

    // { obj, base }: a light or emissive material switched with power.
    public sealed class Powered
    {
        public UnityEngine.Object obj;
        public float @base;
    }

    // Motion-sensor ceiling lamps ('sensor') and battery emergency lamps ('backup').
    public sealed class Sensor
    {
        public float x, y, z, hold, floor;
        public string kind;
    }

    // A light-pool fixture: room lamps { x, y, z, color, intensity, distance } plus the pool's bookkeeping.
    public sealed class Fixture
    {
        public float x, y, z, intensity, distance, hold, ceil, w, d;
        public int color;
        public string kind, zone;
        public bool on, best;
    }

    // B.M: the shared materials.
    public sealed class AptMats
    {
        public Material wallpaper, concrete, paintWhite, ceiling, corridorWall, skirting, stairWall, stairBand, exterior,
            lobbyFloor, lobbyWall, corridorFloor, step, nosing, wood, entryTile, balconyTile, paving, frame, alu, darkFrame,
            glass, frosted, mirror, steel, metal, darkMetal, rubber, plastic, unitDoor, fireDoor, red, yellowPipe,
            black, white, cardboard, fabric, darkWood, lightWood, rail, stone;
    }

    public sealed class AptBuilder
    {
        public Kit kit;
        public List<WalkZone> walk = new List<WalkZone>();
        public List<Solid> solids = new List<Solid>();
        public List<Point> points = new List<Point>();
        public Dictionary<string, Spawn> spawns = new Dictionary<string, Spawn>();
        public Dictionary<string, Anchor> anchors = new Dictionary<string, Anchor>();
        public List<Powered> powered = new List<Powered>();   // on while the building has power
        public List<Powered> backup = new List<Powered>();    // battery emergency lamps: on only while the power is off
        public List<Sensor> sensors = new List<Sensor>();     // motion-sensor ceiling lamps and backup lamps
        public List<AptDoor> doors = new List<AptDoor>();     // auto doors (open when the player comes close)
        public List<Action<float, float, Dictionary<string, object>>> ticks = new List<Action<float, float, Dictionary<string, object>>>(); // (dt, t, S)
        public List<Fixture> fixtures = new List<Fixture>();  // room lamps (on with power)
        public AptMats M;
        public Material emLampMat, redLamp, lobbyLamp, lockLed;
        readonly Dictionary<string, Material> exitMats = new Dictionary<string, Material>();

        public AptBuilder(Kit kit)
        {
            this.kit = kit;
            Material s(string name, int? color = null, double? roughness = null) =>
                color == null && roughness == null ? kit.Surface(name) : kit.Surface(name, new SurfaceOpts { color = color.HasValue ? J.Hex(color.Value) : (Color?)null, roughness = (float?)roughness });
            Material p(string hex, double roughness, double? metalness = null) => kit.Mat("paint:" + hex, new MatSpec { roughness = (float)roughness, metalness = (float?)metalness });
            M = new AptMats
            {
                wallpaper = s("plaster", 0xf3efe5), concrete = s("concrete"),
                paintWhite = p("#ecebe6", 0.92), ceiling = p("#f4f3ef", 0.95),
                corridorWall = p("#e2dccf", 0.85), skirting = p("#6f6a62", 0.6),
                stairWall = p("#e9e5d8", 0.9), stairBand = p("#6f8f78", 0.55),
                exterior = p("#e6e1d6", 0.9),
                lobbyFloor = s("tile", 0xd6cdbd, 0.2), lobbyWall = s("tile", 0xcfc6b6, 0.3),
                corridorFloor = s("tile", 0xa9a7a0, 0.38), step = s("concrete", 0xc4bfb3, 0.55),
                nosing = p("#2a2a29", 0.95),
                wood = s("wood", 0xe8d6b8), entryTile = s("tile", 0x8e8c87, 0.3),
                balconyTile = s("tile", 0xc9c0b0, 0.6), paving = s("sidewalk"),
                frame = p("#e9e9e6", 0.4), alu = p("#9aa0a3", 0.35, 0.7),
                darkFrame = p("#3a3c3f", 0.45, 0.6),
                glass = Mat.Lit(new MatSpec { color = J.Hex(0xc4d6de), roughness = 0.03f, metalness = 0.1f, transparent = true, opacity = 0.16f, depthWrite = false }),
                frosted = Mat.Lit(new MatSpec { color = J.Hex(0xdfe6e6), roughness = 0.4f, transparent = true, opacity = 0.85f }),
                mirror = kit.Mat("metal", new MatSpec { color = J.Hex(0xdfe6ea), roughness = 0.04f, metalness = 1 }),
                steel = kit.Mat("steel"), metal = kit.Mat("metal"), darkMetal = kit.Mat("darkMetal"), rubber = kit.Mat("rubber"), plastic = kit.Mat("plastic"),
                unitDoor = p("#5b5047", 0.42, 0.35), fireDoor = p("#b9b2a1", 0.5, 0.3),
                red = p("#c4161c", 0.4, 0.2), yellowPipe = p("#e7b400", 0.4, 0.2),
                black = p("#151515", 0.35), white = p("#f4f4f1", 0.3), cardboard = p("#b08a5c", 0.95),
                fabric = p("#8d867b", 0.97), darkWood = p("#5e4632", 0.6), lightWood = p("#c9a67c", 0.55),
                rail = p("#4f5a52", 0.5, 0.4), stone = p("#3b3a38", 0.3),
            };
        }

        // ---- registries ----
        T Reg<T>(List<Powered> list, T obj, float? b) where T : UnityEngine.Object
        {
            list.Add(new Powered { obj = obj, @base = b ?? (obj is Light l ? l.intensity : obj is Material m ? Mat.GetEmissiveIntensity(m) : 0) });
            return obj;
        }
        public T OnPower<T>(T obj, float? @base = null) where T : UnityEngine.Object => Reg(powered, obj, @base);
        public T OnBackup<T>(T obj, float? @base = null) where T : UnityEngine.Object => Reg(backup, obj, @base);
        // setI: a light's intensity or a material's emissive intensity.
        public static void SetI(UnityEngine.Object o, float v) { if (o is Light l) l.intensity = v; else if (o is Material m) Mat.SetEmissiveIntensity(m, v); }

        public Solid Solid(double x0, double x1, double z0, double z1, double y0, double y1, string id = null)
        {
            var s = new Solid((float)x0, (float)x1, (float)z0, (float)z1, (float)y0, (float)y1, string.IsNullOrEmpty(id) ? null : id);
            solids.Add(s); return s;
        }
        public void Floor(double x0, double x1, double z0, double z1, double y) => walk.Add(new WalkZone((float)x0, (float)x1, (float)z0, (float)z1, (float)y));
        public void Ramp(double x0, double x1, double z0, double z1, string axis, double y0, double y1) =>
            walk.Add(new WalkZone((float)x0, (float)x1, (float)z0, (float)z1) { ramp = new Ramp(axis, (float)y0, (float)y1) });
        public Point Point(string id, string label, double x, double y, double z, string kind, double? r = null)
        {
            var p = new Point(id, label, (float)x, (float)y, (float)z, kind);
            if (r.HasValue && r.Value != 0) p.r = (float)r.Value;
            points.Add(p); return p;
        }

        // A fresh emissive material (power-switched ones must not be the kit's shared memo copies).
        public Material GlowMat(string hex, double intensity = 2)
        {
            var c = J.Css(hex);
            return Mat.Lit(new MatSpec { color = ApartmentParts.Scale(c, 0.3), emissive = c, emissiveIntensity = (float)intensity, roughness = 0.4f });
        }

        // ---- kit wrappers taking JS doubles ----
        public GameObject Box(double w, double h, double d, Material m, At o = null) => kit.Box((float)w, (float)h, (float)d, m, o);
        public GameObject RBox(double w, double h, double d, double r, Material m, At o = null) => kit.RBox((float)w, (float)h, (float)d, (float)r, m, o);
        public GameObject Cyl(double rt, double rb, double h, Material m, At o = null, int seg = 16) => kit.Cyl((float)rt, (float)rb, (float)h, m, o, seg);
        public GameObject Slab(double w, double h, double d, Material m, At o = null) => kit.Slab((float)w, (float)h, (float)d, m, o);
        public GameObject Plane(double w, double d, Material m, At o = null) => kit.Plane((float)w, (float)d, m, o);

        // kit.instanced + setColorAt: three.js multiplies each instance colour into the material colour. Here every
        // distinct colour gets its own instanced mesh; matFor gets the instance colour (linear, as three.js stores it).
        // items[i] is list[i]'s instance (move it to update the instance).
        public GameObject InstancedColors(GeoData geo, Func<Color, Material> matFor, IList<Xform> list, IList<string> colors, bool cast, bool receive, out GameObject[] items)
        {
            var root = J.Group("instanced-colors");
            items = new GameObject[list.Count];
            foreach (var css in colors.Distinct())
            {
                var idx = Enumerable.Range(0, list.Count).Where(i => colors[i] == css).ToList();
                var im = kit.Instanced(geo, matFor(J.Css(css).linear), idx.Select(i => list[i]).ToList(), cast, receive);
                root.Add(im);
                for (int k = 0; k < idx.Count; k++) items[idx[k]] = im.transform.GetChild(k).gameObject;
            }
            return root;
        }
        // The same over a lit paint material (kit.mat('paint:' + baseHex, { roughness })).
        public GameObject InstancedPaint(GeoData geo, string baseHex, double roughness, IList<Xform> list, IList<string> colors, bool cast = true, bool receive = true)
        {
            var baseC = J.Css(baseHex);
            return InstancedColors(geo, c => Mat.Lit(new MatSpec { color = ApartmentParts.Tint(baseC, c), roughness = (float)roughness }), list, colors, cast, receive, out _);
        }

        // ---- walls: a slab of thickness t on one side of the plane (side -1 / +1), with holes ----
        void Wall(GameObject g, string axis, double c, double a0, double a1, double y0, double y1, Material mat, WallOpts o)
        {
            o ??= new WallOpts();
            double t = o.t, mid = c + o.side * t / 2;
            void seg(double a, double b, double lo, double hi)
            {
                if (b - a < 1e-3 || hi - lo < 1e-3) return;
                g.Add(axis == "z" ? Slab(b - a, hi - lo, t, mat, ApartmentParts.A(x: (a + b) / 2, y: lo, z: mid)) : Slab(t, hi - lo, b - a, mat, ApartmentParts.A(x: mid, y: lo, z: (a + b) / 2)));
            }
            var hs = o.holes.Where(h => h.b > a0 && h.a < a1).OrderBy(h => h.a).ToList();
            double u = a0;
            foreach (var h in hs) { seg(u, h.a, y0, y1); seg(h.a, h.b, y0, Math.Max(y0, h.y0)); seg(h.a, h.b, Math.Min(y1, h.y1), y1); u = h.b; }
            seg(u, a1, y0, y1);
            if (!o.solid) return;
            double v = a0;
            void push(double a, double b) { if (b - a > 1e-3) { if (axis == "z") Solid(a, b, c - 0.12, c + 0.12, y0, y1); else Solid(c - 0.12, c + 0.12, a, b, y0, y1); } }
            foreach (var h in hs.Where(h => h.door)) { push(v, h.a); v = h.b; }
            push(v, a1);
        }
        public void WallZ(GameObject g, double z, double x0, double x1, double y0, double y1, Material mat, WallOpts o = null) => Wall(g, "z", z, x0, x1, y0, y1, mat, o); // plane z = const, spans x
        public void WallX(GameObject g, double x, double z0, double z1, double y0, double y1, Material mat, WallOpts o = null) => Wall(g, "x", x, z0, z1, y0, y1, mat, o); // plane x = const, spans z

        // ---- window frame + glass in an opening. axis 'z': opening spans x on plane z = c; 'x': spans z on plane x = c ----
        public void Window(GameObject g, string axis, double c, double a, double b, double y0, double y1, Material mat = null, Material glass = null, int split = 2, double depth = 0.07, double f = 0.05)
        {
            mat ??= M.frame; glass ??= M.glass;
            double w = b - a, h = y1 - y0;
            void put(double len, double hh, double d, Material m, double u, double y, bool cast = true) =>
                g.Add(axis == "z" ? Box(len, hh, d, m, ApartmentParts.A(x: u, y: y, z: c, cast: cast)) : Box(d, hh, len, m, ApartmentParts.A(x: c, y: y, z: u, cast: cast)));
            put(w, f, depth, mat, (a + b) / 2, y0); put(w, f, depth, mat, (a + b) / 2, y1 - f);
            put(f, h, depth, mat, a + f / 2, y0); put(f, h, depth, mat, b - f / 2, y0);
            for (int i = 1; i < split; i++) put(f * 0.9, h, depth, mat, a + w * i / split, y0);
            put(w - 2 * f, h - 2 * f, 0.012, glass, (a + b) / 2, y0 + f, cast: false);
        }

        // ---- hinged door in an opening [a,b] on plane c. hinge 'a'|'b'; dir: which side it swings to (-1|+1 along the normal) ----
        public AptDoor Door(GameObject g, string axis, double c, double a, double b, double y, double h, Material mat, string hinge = "a", int dir = 1, double t = 0.05, double open = 0, bool solid = true, bool @lock = false, Material frameMat = null, double maxAngle = 1.5)
        {
            frameMat ??= M.darkFrame;
            double w = b - a, lw = w - 0.03; var pivot = J.Group("door"); int sgn = hinge == "a" ? 1 : -1;
            double hu = hinge == "a" ? a + 0.015 : b - 0.015;
            if (axis == "z") ApartmentParts.SetPos(pivot, hu, y, c); else ApartmentParts.SetPos(pivot, c, y, hu);
            GameObject along(double len, double hh, double d, Material m, double u, double yy, double n = 0) =>
                axis == "z" ? Box(len, hh, d, m, ApartmentParts.A(x: u, y: yy, z: n)) : Box(d, hh, len, m, ApartmentParts.A(x: n, y: yy, z: u));
            pivot.Add(along(lw, h - 0.01, t, mat, sgn * lw / 2, 0));
            foreach (var n in new[] { -1, 1 }) // lever handles both faces, near the free edge
            {
                pivot.Add(along(0.13, 0.022, 0.022, M.metal, sgn * (lw - 0.13), 1.0, n * (t / 2 + 0.04)));
                pivot.Add(along(0.03, 0.03, 0.045, M.metal, sgn * (lw - 0.07), 0.99, n * (t / 2 + 0.02)));
            }
            if (@lock) // digital door lock on the outside face (dir is the outside)
            {
                pivot.Add(along(0.075, 0.2, 0.035, M.black, sgn * (lw - 0.09), 1.12, dir * (t / 2 + 0.018)));
                var led = lockLed ??= OnPower(GlowMat("#4aa8ff", 1.6));
                pivot.Add(along(0.04, 0.05, 0.004, led, sgn * (lw - 0.09), 1.2, dir * (t / 2 + 0.037)));
                pivot.Add(along(0.02, 0.02, 0.02, M.metal, sgn * lw / 2, 1.55, dir * (t / 2 + 0.01))); // peephole
            }
            g.Add(pivot);
            if (frameMat != null) // frame posts + head
            {
                double ft = 0.05, fd = 0.24;
                g.Add(axis == "z" ? Box(ft, h + ft, fd, frameMat, ApartmentParts.A(x: a - ft / 2, y: y, z: c)) : Box(fd, h + ft, ft, frameMat, ApartmentParts.A(x: c, y: y, z: a - ft / 2)));
                g.Add(axis == "z" ? Box(ft, h + ft, fd, frameMat, ApartmentParts.A(x: b + ft / 2, y: y, z: c)) : Box(fd, h + ft, ft, frameMat, ApartmentParts.A(x: c, y: y, z: b + ft / 2)));
                g.Add(axis == "z" ? Box(w + 2 * ft, ft, fd, frameMat, ApartmentParts.A(x: (a + b) / 2, y: y + h, z: c)) : Box(fd, ft, w + 2 * ft, frameMat, ApartmentParts.A(x: c, y: y + h, z: (a + b) / 2)));
            }
            int ang = axis == "z" ? (hinge == "a" ? -dir : dir) : (hinge == "a" ? dir : -dir);
            var D = new AptDoor { pivot = pivot, k = (float)open, target = (float)open, x = (float)(axis == "z" ? (a + b) / 2 : c), z = (float)(axis == "z" ? c : (a + b) / 2), y = (float)y };
            D.set = k => { D.k = k; ApartmentParts.SetYaw(pivot, ang * k * maxAngle); if (D.solid != null) D.solid.off = k > 0.12f; };
            if (solid) D.solid = axis == "z" ? Solid(a, b, c - 0.1, c + 0.1, y, y + h) : Solid(c - 0.1, c + 0.1, a, b, y, y + h);
            D.set((float)open);
            return D;
        }
        public AptDoor AutoDoor(AptDoor d, double r = 1.6) { d.auto = (float)r; doors.Add(d); return d; }

        // Cylinder between two points (handrails, pipes, rods). a, b JS space.
        public GameObject Rod(GameObject g, Vector3 a, Vector3 b, double r, Material mat, int seg = 8)
        {
            var d = b - a; float len = d.magnitude;
            var m = Geo.Obj(Geo.Cylinder((float)r, (float)r, len, seg), mat, "rod", true, true);
            J.SetPos(m, a + d * 0.5f);
            m.transform.localRotation = Quaternion.FromToRotation(Vector3.up, J.V(d.normalized)); // setFromUnitVectors(+y, d), mirrored
            g.Add(m); return m;
        }

        // Flat sign plate placed on a wall: { x, y (centre), z, ry, w, h, bg, fg, border }.
        public GameObject Plate(GameObject g, string text, double x, double y, double z, double ry = 0, double w = 0.3, double h = 0.15, string bg = null, string fg = null, string border = null)
        {
            var o = new SignOpts { w = (float)w, h = (float)h, single = true, border = border };
            if (bg != null) o.bg = bg;
            if (fg != null) o.fg = fg;
            var m = kit.Sign(new[] { text }, o);
            ApartmentParts.SetPos(m, x, y, z); ApartmentParts.SetYaw(m, ry);
            var rend = m.GetComponent<Renderer>(); if (rend != null) rend.receiveShadows = true;
            g.Add(m); return m;
        }

        // Texture drawn on a canvas (memoised by key). draw gets the canvas size as floats (JS numbers divide exactly).
        public Texture2D Tex(string key, int w, int h, Action<Paint, float, float> draw) =>
            kit.Memo("apt:tex:" + key, () => kit.Texture(kit.Canvas(w, h, (c, cw, ch) => draw(c, cw, ch))));

        // Exit signs: 'exit' = 피난구유도등 (green field, white figure running into a doorway), 'pass' = 통로유도등 (white field,
        // green figure and arrow). Always lit (light group 'emergency'); ry turns the face, arrow 'left'|'right' for 'pass'.
        public GameObject ExitSign(GameObject g, double x, double y, double z, double ry, string kind = "exit", string arrow = "right")
        {
            var key = kind + arrow;
            if (!exitMats.TryGetValue(key, out var mat))
            {
                var map = Tex("exit:" + key, 256, 128, (c, w, h) => ApartmentParts.DrawExitSign(c, w, h, kind, arrow));
                mat = exitMats[key] = kit.Lit("emergency", Mat.Lit(new MatSpec { map = map, emissive = J.Hex(0xffffff), emissiveMap = map, emissiveIntensity = 2.4f, roughness = 0.4f }), 2.4f);
            }
            var grp = J.Group("exit-sign"); ApartmentParts.SetPos(grp, x, y, z); ApartmentParts.SetYaw(grp, ry);
            grp.Add(Box(0.44, 0.24, 0.06, M.white, ApartmentParts.A(y: -0.12, z: -0.03)));
            var face = ApartmentParts.Obj(Geo.Plane(0.4f, 0.2f), mat); ApartmentParts.SetPos(face, 0, 0, 0.002);
            grp.Add(face); g.Add(grp);
            return grp;
        }
    }

    public static class ApartmentParts
    {
        public const float FH = 2.8f, F3 = 5.6f, FL = 5.75f;   // floor-to-floor, 3rd floor, 302호 main floor (현관 step 0.15)

        public static AptBuilder CreateBuilder(Kit kit) => new AptBuilder(kit);

        // ---- small JS-number helpers shared by the apartment files ----
        // kit place options { x, y, z, rx, ry, rz, cast, receive }
        public static At A(double x = 0, double y = 0, double z = 0, double rx = 0, double ry = 0, double rz = 0, bool cast = true, bool receive = true) =>
            new At { x = (float)x, y = (float)y, z = (float)z, rx = (float)rx, ry = (float)ry, rz = (float)rz, cast = cast, receive = receive };
        // kit.instanced list item { x, y, z, rx, ry, rz, s, sx, sy, sz }
        public static Xform X(double x = 0, double y = 0, double z = 0, double rx = 0, double ry = 0, double rz = 0, double? s = null, double? sx = null, double? sy = null, double? sz = null) =>
            new Xform { x = (float)x, y = (float)y, z = (float)z, rx = (float)rx, ry = (float)ry, rz = (float)rz, s = (float?)s, sx = (float?)sx, sy = (float?)sy, sz = (float?)sz };
        public static Vector3 V3(double x, double y, double z) => new Vector3((float)x, (float)y, (float)z);   // JS space
        public static void SetPos(GameObject o, double x, double y, double z) => J.SetPos(o, (float)x, (float)y, (float)z);
        public static void SetYaw(GameObject o, double a) => J.SetYaw(o, (float)a);
        public static void SetRot(GameObject o, double x, double y, double z) => J.SetRot(o, (float)x, (float)y, (float)z);
        // new THREE.Mesh(geo, mat): three.js meshes neither cast nor receive shadows unless told to.
        public static GameObject Obj(GeoData geo, Material m, bool cast = false, bool receive = false) => Geo.Obj(geo, m, null, cast, receive);
        // CSS font with the kit's family list; the size is printed with '.' whatever the OS locale.
        public static string Fnt(int weight, double px) => weight + " " + ((float)px).ToString(CultureInfo.InvariantCulture) + "px " + Kit.FONT;
        // new THREE.Color(r, g, b): components are linear; Unity material colours are sRGB.
        public static Color Lin(double r, double g, double b) => new Color((float)r, (float)g, (float)b, 1).gamma;
        // three.js color.multiplyScalar(k) (in linear space) on an sRGB colour, alpha kept at 1.
        public static Color Scale(Color srgb, double k) { var l = srgb.linear; return new Color(l.r * (float)k, l.g * (float)k, l.b * (float)k, 1).gamma; }
        // material colour (sRGB) x instance colour (linear) -> sRGB.
        public static Color Tint(Color srgb, Color linear) { var l = srgb.linear; return new Color(l.r * linear.r, l.g * linear.g, l.b * linear.b, 1).gamma; }
        // JS Math.round (halves round up, unlike Math.Round's banker's rounding).
        public static int JsRound(double v) => (int)Math.Floor(v + 0.5);

        // Running figure (pictogram) at hip (x, y), scale s px per unit, facing +x unless flip.
        public static void Runner(Paint g, float x, float y, float s, string color, bool flip = false)
        {
            g.save(); g.translate(x, y); g.scale(flip ? -s : s, s);
            g.strokeStyle = color; g.fillStyle = color; g.lineCap = "round"; g.lineJoin = "round"; g.lineWidth = 0.24f;
            g.beginPath(); g.arc(0.42f, -1.3f, 0.25f, 0, 7); g.fill();
            void line(params float[] pts) { g.beginPath(); g.moveTo(pts[0], pts[1]); for (int i = 2; i < pts.Length; i += 2) g.lineTo(pts[i], pts[i + 1]); g.stroke(); }
            g.lineWidth = 0.3f; line(0.25f, -0.95f, -0.05f, -0.12f);
            g.lineWidth = 0.22f; line(-0.6f, -0.72f, -0.05f, -0.88f, 0.25f, -0.62f, 0.68f, -0.5f); line(-0.05f, -0.12f, 0.45f, 0.28f, 0.4f, 0.86f); line(-0.05f, -0.12f, -0.42f, 0.35f, -0.92f, 0.42f);
            g.restore();
        }

        internal static void DrawExitSign(Paint g, float w, float h, string kind, string arrow)
        {
            const string green = "#0b8a3e";
            if (kind == "exit")
            {
                g.fillStyle = green; g.fillRect(0, 0, w, h);
                g.fillStyle = "#ffffff"; g.fillRect(w * 0.6f, h * 0.14f, w * 0.3f, h * 0.74f); // doorway
                g.fillStyle = green; g.fillRect(w * 0.64f, h * 0.22f, w * 0.22f, h * 0.66f);
                Runner(g, w * 0.4f, h * 0.56f, h * 0.27f, "#ffffff");
                g.fillStyle = "#ffffff"; g.font = Fnt(800, h * 0.14f); g.textAlign = "center"; g.fillText("비상구", w * 0.75f, h * 0.97f - 2);
            }
            else
            {
                g.fillStyle = "#f8faf6"; g.fillRect(0, 0, w, h);
                bool left = arrow == "left";
                Runner(g, left ? w * 0.68f : w * 0.32f, h * 0.55f, h * 0.26f, green, left);
                g.fillStyle = green; g.save(); g.translate(left ? w * 0.26f : w * 0.74f, h * 0.5f); if (left) g.scale(-1, 1);
                g.beginPath(); g.moveTo(-34, -10); g.lineTo(6, -10); g.lineTo(6, -26); g.lineTo(36, 0); g.lineTo(6, 26); g.lineTo(6, 10); g.lineTo(-34, 10); g.closePath(); g.fill(); g.restore();
                g.strokeStyle = green; g.lineWidth = 6; g.strokeRect(3, 3, w - 6, h - 6);
            }
        }
    }
}
