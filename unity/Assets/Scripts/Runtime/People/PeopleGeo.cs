// Ports engine/people-geo.js: the per-vertex PBR material (pattern, roughness, sheen as vertex data), ring
// helpers (ringAt, zoned) and the shared people material. The mesh builder that lofts rings into one indexed
// (optionally skinned) mesh is MeshBuilder in PeopleMesh.cs. Everything here is JS space.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.People
{
    // A loft ring { y, rx, rz?, x?, z?, p? (superellipse, 2 = ellipse), a? (half arc), ac? (arc centre), c?, pbr? }.
    // Torso rows are rings too (props read their z / rz for the back offset). f and k are the per-row tags the
    // JS colour functions read (head row height f, wheel ring k).
    public sealed class BodyRow
    {
        public float y, rx;
        public float? rz, x, z, p, a, ac;
        public Color? c;
        public float[] pbr;
        public float f, k;
        internal Zone zone;              // which zoned() zone dressed it (JS compares zone colour objects)
        public BodyRow Clone() => (BodyRow)MemberwiseClone();
    }

    // zoned() zone: { y (from here upward), c, pbr, t (thickness) }.
    public sealed class Zone
    {
        public float y, t;
        public Color c;
        public float[] pbr;
        public Zone(float y, Color c, float[] pbr, float t) { this.y = y; this.c = c; this.pbr = pbr; this.t = t; }
    }

    public static class PeopleGeo
    {
        // Procedural surface patterns evaluated in the fragment shader from pUv (metres): fabric folds, checks...
        public static readonly Dictionary<string, int> PAT = new Dictionary<string, int> { ["none"] = 0, ["fabric"] = 1, ["check"] = 2, ["knit"] = 3, ["denim"] = 4, ["hair"] = 5, ["quilt"] = 6, ["skin"] = 7, ["grid"] = 8 };
        // [roughness, metalness, emissive (x vertex colour), pattern]
        public static readonly Dictionary<string, float[]> PBR = new Dictionary<string, float[]>
        {
            ["skin"] = new[] { 0.55f, 0, 0, 7 }, ["fabric"] = new[] { 0.93f, 0, 0, 1 }, ["knit"] = new[] { 0.97f, 0, 0, 3 },
            ["denim"] = new[] { 0.9f, 0, 0, 4 }, ["check"] = new[] { 0.9f, 0, 0, 2 }, ["quilt"] = new[] { 0.58f, 0, 0, 6 },
            ["nylon"] = new[] { 0.6f, 0, 0, 1 }, ["hair"] = new[] { 0.5f, 0, 0, 5 }, ["perm"] = new[] { 0.72f, 0, 0, 3 },
            ["leather"] = new[] { 0.42f, 0, 0, 0 }, ["suede"] = new[] { 0.85f, 0, 0, 1 }, ["rubber"] = new[] { 0.88f, 0, 0, 0 }, ["plastic"] = new[] { 0.45f, 0, 0, 0 },
            ["metal"] = new[] { 0.3f, 0.85f, 0, 0 }, ["paint"] = new[] { 0.38f, 0.55f, 0, 0 }, ["fluo"] = new[] { 0.7f, 0, 0.14f, 1 }, ["reflect"] = new[] { 0.28f, 0.6f, 0.3f, 0 },
            ["grid"] = new[] { 0.5f, 0, 0, 8 }, ["paper"] = new[] { 0.75f, 0, 0, 0 }, ["glossy"] = new[] { 0.25f, 0, 0, 0 },
        };

        // sRGB hex -> linear working colour (three.js Color with colour management), optionally darkened/brightened.
        public static Color Col(string hex, float k = 1)
        {
            var c = J.Css(hex).linear;
            return new Color(c.r * k, c.g * k, c.b * k, 1);
        }

        static Material shared;
        // One material for every person and prop: vertex colours + per-vertex PBR + patterns.
        // ponytail: URP Lit stand-in with vertex colours (MatSpec.vertexColors); the per-vertex roughness /
        // metalness / emissive / pattern ride along in the mesh (uv2 = aPbr, uv0 = pUv) unused. The quality pass
        // writes a Shader Graph that reads them (the JS onBeforeCompile patterns are in people-geo.js).
        public static Material VpbrMaterial()
        {
            if (shared != null) return shared;
            shared = Mat.Lit(new MatSpec { vertexColors = true, roughness = 1, metalness = 0 });
            shared.name = "people-vpbr-1";
            return shared;
        }

        // Linear interpolation of ring rows (ascending y) at height y; numeric fields only.
        public static BodyRow RingAt(IList<BodyRow> rows, float y)
        {
            if (y <= rows[0].y) { var o0 = rows[0].Clone(); o0.y = y; return o0; }
            for (int i = 0; i < rows.Count - 1; i++)
            {
                BodyRow a = rows[i], b = rows[i + 1];
                if (y <= b.y)
                {
                    float t = b.y == a.y ? 0 : (y - a.y) / (b.y - a.y);
                    var o = a.Clone(); o.y = y;
                    o.rx = a.rx + (b.rx - a.rx) * t;
                    o.rz = Lerp(a.rz, b.rz, 0, t); o.x = Lerp(a.x, b.x, 0, t); o.z = Lerp(a.z, b.z, 0, t);
                    o.p = Lerp(a.p, b.p, 2, t); o.a = Lerp(a.a, b.a, Mathf.PI, t);
                    return o;
                }
            }
            var ol = rows[rows.Count - 1].Clone(); ol.y = y; return ol;
        }

        static float? Lerp(float? a, float? b, float d, float t)
        {
            if (a == null && b == null) return null;
            float av = a ?? d, bv = b ?? d;
            return av + (bv - av) * t;
        }

        // Dress ascending rows in zones: each row gets its zone's colour/finish/thickness; at each zone edge two
        // rings are inserted so colours and cuffs stay crisp.
        public static List<BodyRow> Zoned(IList<BodyRow> rows, IList<Zone> zones)
        {
            var zs = zones.OrderBy(z => z.y).ToList();   // stable, like Array.prototype.sort
            Zone ZoneAt(float y) { var z = zs[0]; foreach (var q in zs) if (y >= q.y) z = q; return z; }
            BodyRow Dress(BodyRow r, Zone z)
            {
                var o = r.Clone();
                o.rx = r.rx + z.t; o.rz = (r.rz ?? r.rx) + z.t; o.c = z.c; o.pbr = z.pbr; o.zone = z;
                return o;
            }
            var output = new List<BodyRow>();
            float y0 = rows[0].y, y1 = rows[rows.Count - 1].y;
            var edges = zs.Skip(1).Select(z => z.y).Where(y => y > y0 && y < y1).ToList();
            int e = 0;
            foreach (var r in rows)
            {
                while (e < edges.Count && edges[e] <= r.y)
                {
                    float y = edges[e];
                    var b = RingAt(rows, y);
                    output.Add(Dress(b, ZoneAt(y - 1e-6f))); output.Add(Dress(b, ZoneAt(y)));
                    e++;
                }
                var last = output.Count > 0 ? output[output.Count - 1] : null;
                if (last == null || last.y != r.y || last.zone != ZoneAt(r.y)) output.Add(Dress(r, ZoneAt(r.y)));
            }
            return output;
        }

        // ---- JS-space matrices (three.js Matrix4 numbers; Unity's types do the arithmetic) ----
        // makeRotationFromEuler(Euler(rx, ry, rz)) (XYZ order).setPosition(x, y, z)
        public static Matrix4x4 At(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0)
        {
            var q = Quaternion.AngleAxis(rx * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(ry * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(rz * Mathf.Rad2Deg, Vector3.forward);
            return Matrix4x4.TRS(new Vector3(x, y, z), q, Vector3.one);
        }
    }
}
