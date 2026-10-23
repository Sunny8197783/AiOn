// The rarer three.js geometries (polyhedra, capsule, RoundedBoxGeometry addon, lathe, shape, extrude) and
// mergeGeometries / computeVertexNormals, ported from the vendored three.core.js line by line.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public static partial class Geo
    {
        // ---- PolyhedronGeometry (non-indexed: index is 0..n-1) ----
        static GeoData Polyhedron(float[] vertices, int[] indices, float radius, int detail)
        {
            var vb = new List<Vector3>();
            Vector3 V(int i) => new Vector3(vertices[i * 3], vertices[i * 3 + 1], vertices[i * 3 + 2]);
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = V(indices[i]), b = V(indices[i + 1]), c = V(indices[i + 2]);
                int cols = detail + 1;
                var v = new List<Vector3[]>();
                for (int ii = 0; ii <= cols; ii++)
                {
                    var aj = Vector3.Lerp(a, c, (float)ii / cols); var bj = Vector3.Lerp(b, c, (float)ii / cols);
                    int rows = cols - ii;
                    var row = new Vector3[rows + 1];
                    for (int j = 0; j <= rows; j++) row[j] = j == 0 && ii == cols ? aj : Vector3.Lerp(aj, bj, (float)j / rows);
                    v.Add(row);
                }
                for (int ii = 0; ii < cols; ii++)
                    for (int j = 0; j < 2 * (cols - ii) - 1; j++)
                    {
                        int k = j / 2;
                        if (j % 2 == 0) { vb.Add(v[ii][k + 1]); vb.Add(v[ii + 1][k]); vb.Add(v[ii][k]); }
                        else { vb.Add(v[ii][k + 1]); vb.Add(v[ii + 1][k + 1]); vb.Add(v[ii + 1][k]); }
                    }
            }
            for (int i = 0; i < vb.Count; i++) vb[i] = vb[i].normalized * radius;
            float Azimuth(Vector3 p) => Mathf.Atan2(p.z, -p.x);
            float Inclination(Vector3 p) => Mathf.Atan2(-p.y, Mathf.Sqrt(p.x * p.x + p.z * p.z));
            var uvs = new List<Vector2>();
            foreach (var p in vb) uvs.Add(new Vector2(Azimuth(p) / 2 / Mathf.PI + 0.5f, 1 - (Inclination(p) / Mathf.PI + 0.5f)));
            for (int i = 0; i + 2 < vb.Count; i += 3) // correctUVs
            {
                float azi = Azimuth((vb[i] + vb[i + 1] + vb[i + 2]) / 3);
                for (int k = 0; k < 3; k++)
                {
                    var uv = uvs[i + k]; var p = vb[i + k];
                    if (azi < 0 && uv.x == 1) uv.x -= 1;
                    if (p.x == 0 && p.z == 0) uv.x = azi / 2 / Mathf.PI + 0.5f;
                    uvs[i + k] = uv;
                }
            }
            for (int i = 0; i + 2 < uvs.Count; i += 3) // correctSeam
            {
                float x0 = uvs[i].x, x1 = uvs[i + 1].x, x2 = uvs[i + 2].x, max = Mathf.Max(x0, Mathf.Max(x1, x2)), min = Mathf.Min(x0, Mathf.Min(x1, x2));
                if (max > 0.9f && min < 0.1f)
                    for (int k = 0; k < 3; k++) if (uvs[i + k].x < 0.2f) uvs[i + k] += new Vector2(1, 0);
            }
            var g = new GeoData { pos = vb, uv = uvs };
            for (int i = 0; i < vb.Count; i++) g.index.Add(i);
            g.normal = detail == 0 ? VertexNormals(g) : vb.ConvertAll(p => p.normalized);   // flat vs smooth
            return g;
        }

        public static GeoData Icosahedron(float r = 1, int detail = 0)
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var v = new[] { -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, 0, 0, -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, t, 0, -1, t, 0, 1, -t, 0, -1, -t, 0, 1 };
            var i = new[] { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            return Polyhedron(v, i, r, detail);
        }

        public static GeoData Dodecahedron(float r = 1, int detail = 0)
        {
            float t = (1 + Mathf.Sqrt(5)) / 2, q = 1 / t;
            var v = new[] { -1, -1, -1, -1, -1, 1, -1, 1, -1, -1, 1, 1, 1, -1, -1, 1, -1, 1, 1, 1, -1, 1, 1, 1,
                0, -q, -t, 0, -q, t, 0, q, -t, 0, q, t, -q, -t, 0, -q, t, 0, q, -t, 0, q, t, 0, -t, 0, -q, t, 0, -q, -t, 0, q, t, 0, q };
            var i = new[] { 3, 11, 7, 3, 7, 15, 3, 15, 13, 7, 19, 17, 7, 17, 6, 7, 6, 15, 17, 4, 8, 17, 8, 10, 17, 10, 6,
                8, 0, 16, 8, 16, 2, 8, 2, 10, 0, 12, 1, 0, 1, 18, 0, 18, 16, 6, 10, 2, 6, 2, 13, 6, 13, 15,
                2, 16, 18, 2, 18, 3, 2, 3, 13, 18, 1, 9, 18, 9, 11, 18, 11, 3, 4, 14, 12, 4, 12, 0, 4, 0, 8,
                11, 9, 5, 11, 5, 19, 11, 19, 7, 19, 5, 14, 19, 14, 4, 19, 4, 17, 1, 12, 14, 1, 14, 5, 1, 5, 9 };
            return Polyhedron(v, i, r, detail);
        }

        // ---- CapsuleGeometry (radius, height of the straight part, heightSegments 1) ----
        public static GeoData Capsule(float r = 1, float length = 1, int capSeg = 4, int radialSeg = 8)
        {
            var g = new GeoData();
            float height = Mathf.Max(0, length);
            capSeg = Mathf.Max(1, capSeg); radialSeg = Mathf.Max(3, radialSeg);
            const int heightSeg = 1;
            float halfHeight = height / 2, capArc = Mathf.PI / 2 * r, total = 2 * capArc + height;
            int numV = capSeg * 2 + heightSeg, perRow = radialSeg + 1;
            for (int iy = 0; iy <= numV; iy++)
            {
                float arc, py, pr, ny;
                if (iy <= capSeg)
                {
                    float sp = (float)iy / capSeg, a = sp * Mathf.PI / 2;
                    py = -halfHeight - r * Mathf.Cos(a); pr = r * Mathf.Sin(a); ny = -r * Mathf.Cos(a); arc = sp * capArc;
                }
                else if (iy <= capSeg + heightSeg)
                {
                    float sp = (float)(iy - capSeg) / heightSeg;
                    py = -halfHeight + sp * height; pr = r; ny = 0; arc = capArc + sp * height;
                }
                else
                {
                    float sp = (float)(iy - capSeg - heightSeg) / capSeg, a = sp * Mathf.PI / 2;
                    py = halfHeight + r * Mathf.Sin(a); pr = r * Mathf.Cos(a); ny = r * Mathf.Sin(a); arc = capArc + height + sp * capArc;
                }
                float v = Mathf.Clamp01(arc / total), uOff = iy == 0 ? 0.5f / radialSeg : iy == numV ? -0.5f / radialSeg : 0;
                for (int ix = 0; ix <= radialSeg; ix++)
                {
                    float u = (float)ix / radialSeg, th = u * Mathf.PI * 2, s = Mathf.Sin(th), c = Mathf.Cos(th);
                    g.pos.Add(new Vector3(-pr * c, py, pr * s));
                    g.normal.Add(new Vector3(-pr * c, ny, pr * s).normalized);
                    g.uv.Add(new Vector2(u + uOff, v));
                }
                if (iy > 0)
                {
                    int prev = (iy - 1) * perRow;
                    for (int ix = 0; ix < radialSeg; ix++)
                    {
                        int i1 = prev + ix, i2 = prev + ix + 1, i3 = iy * perRow + ix, i4 = iy * perRow + ix + 1;
                        g.index.Add(i1); g.index.Add(i2); g.index.Add(i3);
                        g.index.Add(i2); g.index.Add(i4); g.index.Add(i3);
                    }
                }
            }
            return g;
        }

        // ---- RoundedBoxGeometry addon (non-indexed) ----
        public static GeoData RoundedBox(float w, float h, float d, int seg = 2, float radius = 0.1f)
        {
            int total = seg * 2 + 1;
            radius = Mathf.Min(w / 2, Mathf.Min(h / 2, Mathf.Min(d / 2, radius)));
            var g = ToNonIndexed(Box(1, 1, 1, total, total, total));
            if (total == 1) return Box(1, 1, 1, 1, 1, 1);
            var box = new Vector3(w, h, d) / 2 - Vector3.one * radius;
            int faceTris = g.pos.Count / 6;
            float half = 0.5f / total;
            float GetUv(Vector3 faceDir, Vector3 n, int uvAxis, int projAxis, float side)
            {
                float totArc = 2 * Mathf.PI * radius / 4, center = Mathf.Max(side - 2 * radius, 0), halfArc = Mathf.PI / 4;
                var t = n; t[projAxis] = 0; t = t.normalized;
                float arcUv = 0.5f * totArc / (totArc + center), arcAngle = 1f - Vector3.Angle(t, faceDir) * Mathf.Deg2Rad / halfArc;
                if (Math.Sign(t[uvAxis]) == 1) return arcAngle * arcUv;
                return center / (totArc + center) + arcUv + arcUv * (1f - arcAngle);
            }
            for (int i = 0; i < g.pos.Count; i++)
            {
                var p = g.pos[i];
                var n = new Vector3(p.x - Math.Sign(p.x) * half, p.y - Math.Sign(p.y) * half, p.z - Math.Sign(p.z) * half).normalized;
                g.pos[i] = new Vector3(box.x * Math.Sign(p.x) + n.x * radius, box.y * Math.Sign(p.y) + n.y * radius, box.z * Math.Sign(p.z) + n.z * radius);
                g.normal[i] = n;
                Vector2 uv;
                switch (i / faceTris)
                {
                    case 0: uv = new Vector2(GetUv(Vector3.right, n, 2, 1, d), 1 - GetUv(Vector3.right, n, 1, 2, h)); break;
                    case 1: uv = new Vector2(1 - GetUv(Vector3.left, n, 2, 1, d), 1 - GetUv(Vector3.left, n, 1, 2, h)); break;
                    case 2: uv = new Vector2(1 - GetUv(Vector3.up, n, 0, 2, w), GetUv(Vector3.up, n, 2, 0, d)); break;
                    case 3: uv = new Vector2(1 - GetUv(Vector3.down, n, 0, 2, w), 1 - GetUv(Vector3.down, n, 2, 0, d)); break;
                    case 4: uv = new Vector2(1 - GetUv(Vector3.forward, n, 0, 1, w), 1 - GetUv(Vector3.forward, n, 1, 0, h)); break;
                    default: uv = new Vector2(GetUv(Vector3.back, n, 0, 1, w), 1 - GetUv(Vector3.back, n, 1, 0, h)); break;
                }
                g.uv[i] = uv;
            }
            return g;
        }

        // ---- LatheGeometry: (radius, y) profile revolved around y ----
        public static GeoData Lathe(IList<Vector2> points, int seg = 12, float phiStart = 0, float phiLength = Mathf.PI * 2)
        {
            var g = new GeoData();
            seg = Mathf.Max(1, seg); phiLength = Mathf.Clamp(phiLength, 0, Mathf.PI * 2);
            int n = points.Count;
            var init = new Vector3[n];
            Vector3 prevN = Vector3.zero;
            for (int j = 0; j < n; j++)
            {
                if (j == n - 1 && j > 0) { init[j] = prevN; continue; }
                if (n < 2) { init[j] = Vector3.zero; continue; }
                float dx = points[j + 1].x - points[j].x, dy = points[j + 1].y - points[j].y;
                var nn = new Vector3(dy, -dx, 0);
                if (j == 0) { prevN = nn; init[j] = nn.normalized; }
                else { var cur = nn; init[j] = (nn + prevN).normalized; prevN = cur; }
            }
            for (int i = 0; i <= seg; i++)
            {
                float phi = phiStart + i * (1f / seg) * phiLength, s = Mathf.Sin(phi), c = Mathf.Cos(phi);
                for (int j = 0; j < n; j++)
                {
                    g.pos.Add(new Vector3(points[j].x * s, points[j].y, points[j].x * c));
                    g.uv.Add(new Vector2((float)i / seg, n > 1 ? (float)j / (n - 1) : 0));
                    g.normal.Add(new Vector3(init[j].x * s, init[j].y, init[j].x * c));
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < n - 1; j++)
                {
                    int b0 = j + i * n, a = b0, b = b0 + n, c = b0 + n + 1, d = b0 + 1;
                    g.index.Add(a); g.index.Add(b); g.index.Add(d);
                    g.index.Add(c); g.index.Add(d); g.index.Add(b);
                }
            return g;
        }

        // Shape.extractPoints for a polygon outline: consecutive duplicates dropped (Path.getPoints).
        static List<Vector2> Points(IList<Vector2> src)
        {
            var o = new List<Vector2>();
            foreach (var p in src) if (o.Count == 0 || o[o.Count - 1].x != p.x || o[o.Count - 1].y != p.y) o.Add(p);
            return o;
        }

        // ---- ShapeGeometry: a flat shape in the XY plane facing +z, world UVs ----
        public static GeoData Shape(IList<Vector2> outline, IList<IList<Vector2>> holes = null)
        {
            var g = new GeoData();
            var shape = Points(outline);
            var hs = new List<List<Vector2>>();
            if (holes != null) foreach (var h in holes) hs.Add(Points(h));
            if (!ShapeUtils.IsClockWise(shape)) shape.Reverse();
            foreach (var h in hs) if (ShapeUtils.IsClockWise(h)) h.Reverse();
            var faces = ShapeUtils.TriangulateShape(shape, hs);
            var all = new List<Vector2>(shape);
            foreach (var h in hs) all.AddRange(h);
            foreach (var v in all) { g.pos.Add(new Vector3(v.x, v.y, 0)); g.normal.Add(new Vector3(0, 0, 1)); g.uv.Add(v); }
            foreach (var f in faces) { g.index.Add(f[0]); g.index.Add(f[1]); g.index.Add(f[2]); }
            return g;
        }

        // ---- ExtrudeGeometry (steps 1, bevelSegments 3, straight extrusion along +z, world UVs, flat normals) ----
        public static GeoData Extrude(IList<Vector2> outline, float depth, bool bevel = false, float bevelSize = 0, float bevelThickness = 0, IList<IList<Vector2>> holes = null)
            => Extrude(outline, depth, bevel, bevelSize, bevelThickness, 3, holes);

        // The same with three.js bevelSegments (town-traffic uses 2).
        public static GeoData Extrude(IList<Vector2> outline, float depth, bool bevel, float bevelSize, float bevelThickness, int bevelSegments, IList<IList<Vector2>> holes = null)
            => GeoExtrude.Build(Points(outline), holes, depth, bevel, bevelSize, bevelThickness, bevelSegments);

        // ---- mergeGeometries (indices offset; vertex colours kept only when every part has them) ----
        public static GeoData Merge(IList<GeoData> list)
        {
            var o = new GeoData();
            bool colors = list.Count > 0;
            foreach (var g in list) if (g.color == null || g.color.Count != g.pos.Count) colors = false;
            if (colors) o.color = new List<Color>();
            foreach (var g in list)
            {
                int off = o.pos.Count;
                o.pos.AddRange(g.pos);
                o.normal.AddRange(g.normal.Count == g.pos.Count ? g.normal : VertexNormals(g));
                if (g.uv.Count == g.pos.Count) o.uv.AddRange(g.uv); else for (int i = 0; i < g.pos.Count; i++) o.uv.Add(Vector2.zero);
                if (colors) o.color.AddRange(g.color);
                if (g.index.Count == 0) for (int i = 0; i < g.pos.Count; i++) o.index.Add(off + i);
                else foreach (var i in g.index) o.index.Add(off + i);
            }
            return o;
        }

        // BufferGeometry.toNonIndexed.
        internal static GeoData ToNonIndexed(GeoData g)
        {
            if (g.index.Count == 0) return g.Clone();
            var o = new GeoData();
            bool hasN = g.normal.Count == g.pos.Count, hasUv = g.uv.Count == g.pos.Count, hasC = g.color != null && g.color.Count == g.pos.Count;
            if (hasC) o.color = new List<Color>();
            for (int k = 0; k < g.index.Count; k++)
            {
                int i = g.index[k];
                o.pos.Add(g.pos[i]);
                if (hasN) o.normal.Add(g.normal[i]);
                if (hasUv) o.uv.Add(g.uv[i]);
                if (hasC) o.color.Add(g.color[i]);
                o.index.Add(k);
            }
            return o;
        }

        // BufferGeometry.computeVertexNormals: area-weighted sum of face normals per vertex, normalised.
        internal static List<Vector3> VertexNormals(GeoData g)
        {
            var n = new Vector3[g.pos.Count];
            void Face(int a, int b, int c)
            {
                var cb = Vector3.Cross(g.pos[c] - g.pos[b], g.pos[a] - g.pos[b]);
                n[a] += cb; n[b] += cb; n[c] += cb;
            }
            if (g.index.Count > 0) for (int i = 0; i + 2 < g.index.Count; i += 3) Face(g.index[i], g.index[i + 1], g.index[i + 2]);
            else for (int i = 0; i + 2 < g.pos.Count; i += 3) Face(i, i + 1, i + 2);
            var o = new List<Vector3>(n.Length);
            foreach (var v in n) o.Add(v.sqrMagnitude > 0 ? v.normalized : Vector3.zero);
            return o;
        }
    }
}
