// Vehicle data and geometry for TownTraffic (levels/town-traffic.js TYPES, vehicleGeo, ROWS, KEEP_CLEAR, PALETTE).
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    internal sealed class CarType
    {
        public float L, W, r, weight, v, pillar;
        public float[] axles, lights, box;
        public float[][] top, glass;
    }

    internal sealed class CarGeo { public GeoData body, glass, dark, lamp; }

    internal static class TownTrafficCars
    {
        public static readonly string[] PARTS = { "body", "glass", "dark", "lamp" };

        static float[][] P(params float[] v) { var r = new float[v.Length / 2][]; for (int i = 0; i < r.Length; i++) r[i] = new[] { v[2 * i], v[2 * i + 1] }; return r; }

        // Side profiles in (s from the front bumper, y). Wheel arches are cut into the bottom edge. Insertion order kept.
        public static readonly List<KeyValuePair<string, CarType>> TYPES = new List<KeyValuePair<string, CarType>>
        {
            new KeyValuePair<string, CarType>("sedan", new CarType { L = 4.7f, W = 1.82f, r = 0.32f, axles = new[] { 0.95f, 3.85f }, weight = 0.5f, v = 1,
                top = P(0.05f, 0.32f, 0, 0.55f, 0.08f, 0.72f, 0.9f, 0.86f, 1.65f, 0.95f, 2.25f, 1.4f, 3.45f, 1.42f, 4.15f, 1.0f, 4.62f, 0.95f, 4.7f, 0.62f, 4.66f, 0.32f),
                glass = P(1.62f, 0.955f, 2.24f, 1.415f, 2.32f, 1.36f, 3.4f, 1.37f, 3.46f, 1.428f, 4.17f, 1.005f, 4.1f, 0.96f, 1.7f, 0.94f), pillar = 2.85f, lights = new[] { 0.68f, 0.86f, -0.01f, 4.69f } }),
            new KeyValuePair<string, CarType>("suv", new CarType { L = 4.6f, W = 1.88f, r = 0.36f, axles = new[] { 1.0f, 3.75f }, weight = 0.3f, v = 0.95f,
                top = P(0.05f, 0.4f, 0, 0.66f, 0.1f, 0.92f, 1.0f, 1.02f, 1.55f, 1.08f, 2.08f, 1.66f, 4.18f, 1.69f, 4.52f, 1.5f, 4.6f, 1.02f, 4.58f, 0.4f),
                glass = P(1.52f, 1.09f, 2.06f, 1.69f, 2.14f, 1.62f, 4.12f, 1.63f, 4.21f, 1.705f, 4.545f, 1.505f, 4.5f, 1.08f, 1.6f, 1.07f), pillar = 2.95f, lights = new[] { 0.8f, 1.05f, -0.01f, 4.65f } }),
            new KeyValuePair<string, CarType>("truck", new CarType { L = 5.1f, W = 1.76f, r = 0.33f, axles = new[] { 0.78f, 3.9f }, weight = 0.2f, v = 0.85f,
                top = P(0, 0.42f, 0, 1.0f, 0.08f, 1.72f, 0.34f, 1.98f, 1.62f, 1.98f, 1.62f, 0.95f, 5.1f, 0.95f, 5.1f, 0.42f),
                glass = P(0.035f, 1.12f, 0.115f, 1.78f, 0.36f, 1.995f, 1.0f, 1.995f, 1.0f, 1.12f), box = new[] { 1.7f, 5.1f, 0.98f, 2.42f }, pillar = 0, lights = new[] { 0.62f, 0.8f, -0.05f, 5.14f } }),
        };
        public static CarType Type(string id) => TYPES.First(t => t.Key == id).Value;

        // Rows of marked bays (same lines as town-ground): [x0, x1, zLine, depth, step, kind]
        public static readonly (float x0, float x1, float zl, float depth, float step, string kind)[] ROWS =
        {
            (-35, 3, -22, 5, 2.5f, null), (6, 40, -51.5f, 5, 2.5f, null), (6, 40, -10.5f, -5, 2.5f, null), (6, 40, -31, 5, 2.5f, null), (6, 40, -31, -5, 2.5f, null),
            (74.5f, 82, -10.5f, -5.5f, 2.5f, null), (92, 126, -10.5f, -5.5f, 2.5f, null), (104.4f, 123.6f, -60, 6, 3.2f, "truck"), (-124, -90, 59.5f, -5, 2.5f, null),
            (60, 126, 13.5f, 5, 2.5f, null), (62, 116, 57, -5, 2.5f, null), (62, 116, 85, -5, 2.5f, null), (14, 40, 51.5f, -5, 2.5f, null), (14, 40, 22.5f, 5, 2.5f, null),
        };
        public static readonly RectXZ[] KEEP_CLEAR = { new RectXZ(-20.5f, -11.5f, -23, -16), new RectXZ(-9.5f, -5.5f, -23, -16) };
        public static readonly List<KeyValuePair<string, float>> PALETTE = new List<KeyValuePair<string, float>>
        {
            W("#f2f2ef", 35), W("#8d9196", 20), W("#16181b", 15), W("#c4c7ca", 12), W("#1f2f4f", 8), W("#8e1f22", 4), W("#c9b89a", 3), W("#2f4a3a", 3),
        };
        public static readonly List<KeyValuePair<string, float>> TRUCK_PALETTE = new List<KeyValuePair<string, float>> { W("#f2f2ef", 6), W("#2d5fa8", 3), W("#c4c7ca", 1) };
        static KeyValuePair<string, float> W(string v, float w) => new KeyValuePair<string, float>(v, w);

        public static CarGeo VehicleGeo(CarType T)
        {
            float L = T.L, Wd = T.W, r = T.r, bev = 0.05f;
            var outline = T.top.Select(p => new Vector2(p[0], p[1])).ToList();
            float af = T.axles[0], ar = T.axles[1], R = r + 0.06f, by = T.top[0][1];
            List<Vector2> Arch(float c)
            {
                var pts = new List<Vector2>();
                for (int k = 0; k <= 10; k++) { float a = k / 10f * Mathf.PI; pts.Add(new Vector2(c + R * Mathf.Cos(a), Mathf.Max(by, r + R * Mathf.Sin(a) * 0.95f))); }
                return pts;
            }
            outline.AddRange(Arch(ar)); outline.AddRange(Arch(af));
            GeoData ToCar(GeoData g, float depth) => g.Translate(-L / 2, 0, -depth / 2).RotateY(-Mathf.PI / 2);
            // ponytail: Geo.Extrude has no bevelSegments/curveSegments knobs (JS 2 / 4); the engine default stands in.
            var bodyParts = new List<GeoData> { ToCar(Geo.Extrude(outline, Wd - 2 * bev, true, bev, bev), Wd - 2 * bev) };
            if (T.box != null) { float s0 = T.box[0], s1 = T.box[1], y0 = T.box[2], y1 = T.box[3]; bodyParts.Add(Geo.Box(Wd + 0.14f, y1 - y0, s1 - s0).Translate(0, (y0 + y1) / 2, (s0 + s1) / 2 - L / 2)); }
            if (T.pillar != 0) bodyParts.Add(Geo.Box(Wd + 0.02f, 0.42f, 0.09f).Translate(0, (T.glass[2][1] + T.glass[0][1]) / 2, T.pillar - L / 2));
            var glass = ToCar(Geo.Extrude(T.glass.Select(p => new Vector2(p[0], p[1])).ToList(), Wd + 0.012f), Wd + 0.012f);
            float Z(float s) => s - L / 2;
            float hx = Wd / 2 - 0.13f;
            var tyres = new List<GeoData>(); var hubs = new List<GeoData>();
            foreach (var s in T.axles) foreach (var sx in new[] { -1, 1 })
            {
                tyres.Add(Geo.Cylinder(r, r, 0.22f, 16).RotateZ(Mathf.PI / 2).Translate(sx * hx, r, Z(s)));
                hubs.Add(Geo.Cylinder(r * 0.62f, r * 0.62f, 0.02f, 12).RotateZ(Mathf.PI / 2).Translate(sx * (hx + 0.115f), r, Z(s)));
            }
            var dark = new List<GeoData>(tyres)
            {
                Geo.Box(Wd * 0.98f, 0.16f, 0.12f).Translate(0, T.top[0][1] + 0.06f, Z(0.04f)), Geo.Box(Wd * 0.98f, 0.16f, 0.12f).Translate(0, T.top[0][1] + 0.06f, Z(L - 0.04f)),
                Geo.Box(Wd * 0.5f, 0.17f, 0.04f).Translate(0, T.lights[0] - 0.12f, Z(0) - 0.01f),
            };
            foreach (var sx in new[] { -1, 1 }) dark.Add(Geo.Box(0.1f, 0.09f, 0.16f).Translate(sx * (Wd / 2 + 0.07f), T.glass[0][1] + 0.06f, Z(T.glass[0][0] + 0.15f)));
            dark.AddRange(hubs);
            dark.Add(Geo.Box(0.52f, 0.11f, 0.02f).Translate(0, T.top[0][1] + 0.2f, Z(L) + 0.07f));
            // Head and tail lamps share one material: UVs pick the white or red half of a 2 px texture.
            IEnumerable<GeoData> Lamp(float s, float y, float w, float d, float u) => new[] { -1, 1 }.Select(sx =>
            {
                var g = Geo.Box(w, 0.12f, d).Translate(sx * (Wd / 2 - 0.22f - w / 2 + 0.1f), y, Z(s));
                for (int k = 0; k < g.uv.Count; k++) g.uv[k] = new Vector2(u, 0.5f);
                return g;
            }).ToList();
            return new CarGeo
            {
                body = Geo.Merge(bodyParts), glass = glass, dark = Geo.Merge(dark),
                lamp = Geo.Merge(Lamp(T.lights[2], T.lights[0], 0.36f, 0.07f, 0.25f).Concat(Lamp(T.lights[3], T.lights[1], 0.32f, 0.06f, 0.75f)).ToList()),
            };
        }

        public static GeoData Part(CarGeo g, string part) => part == "body" ? g.body : part == "glass" ? g.glass : part == "dark" ? g.dark : g.lamp;
    }
}
