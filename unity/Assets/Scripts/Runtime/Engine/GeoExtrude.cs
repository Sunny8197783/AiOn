// three.js ExtrudeGeometry.addShape for a straight extrusion (steps 1, no extrudePath, bevelOffset 0) with the
// WorldUVGenerator, then computeVertexNormals on the non-indexed result (flat faces), as the vendored source does.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Engine
{
    static class GeoExtrude
    {
        public static GeoData Build(List<Vector2> shapePts, IList<IList<Vector2>> holeSrc, float depth, bool bevelEnabled, float bevelSize, float bevelThickness, int bevelSegments)
        {
            const int steps = 1;
            float bevelOffset = 0;
            if (!bevelEnabled) { bevelSegments = 0; bevelThickness = 0; bevelSize = 0; bevelOffset = 0; }
            var vertices = new List<Vector2>(shapePts);
            var holes = new List<List<Vector2>>();
            if (holeSrc != null) foreach (var h in holeSrc) holes.Add(Dedup(h));
            if (!ShapeUtils.IsClockWise(vertices))
            {
                vertices.Reverse();
                foreach (var h in holes) if (ShapeUtils.IsClockWise(h)) h.Reverse();
            }
            MergeOverlapping(vertices);
            foreach (var h in holes) MergeOverlapping(h);
            int numHoles = holes.Count;
            var contour = vertices;
            var all = new List<Vector2>(contour);
            foreach (var h in holes) all.AddRange(h);
            int vlen = all.Count;

            var contourMoves = new Vector2[contour.Count];
            for (int i = 0, il = contour.Count, j = il - 1, k = i + 1; i < il; i++, j++, k++)
            {
                if (j == il) j = 0;
                if (k == il) k = 0;
                contourMoves[i] = BevelVec(contour[i], contour[j], contour[k]);
            }
            var holesMoves = new List<Vector2[]>();
            var vertMoves = new List<Vector2>(contourMoves);
            foreach (var h in holes)
            {
                var m = new Vector2[h.Count];
                for (int i = 0, il = h.Count, j = il - 1, k = i + 1; i < il; i++, j++, k++)
                {
                    if (j == il) j = 0;
                    if (k == il) k = 0;
                    m[i] = BevelVec(h[i], h[j], h[k]);
                }
                holesMoves.Add(m);
                vertMoves.AddRange(m);
            }

            var ph = new List<Vector3>();   // placeholder
            void V(float x, float y, float z) => ph.Add(new Vector3(x, y, z));
            List<int[]> faces;
            if (bevelSegments == 0) faces = ShapeUtils.TriangulateShape(contour, holes);
            else
            {
                var contracted = new List<Vector2>(); var expanded = new List<List<Vector2>>();
                for (int b = 0; b < bevelSegments; b++)
                {
                    float t = (float)b / bevelSegments, z = bevelThickness * Mathf.Cos(t * Mathf.PI / 2), bs = bevelSize * Mathf.Sin(t * Mathf.PI / 2) + bevelOffset;
                    for (int i = 0; i < contour.Count; i++)
                    {
                        var vert = contour[i] + contourMoves[i] * bs;
                        V(vert.x, vert.y, -z);
                        if (t == 0) contracted.Add(vert);
                    }
                    for (int h = 0; h < numHoles; h++)
                    {
                        var hole = holes[h]; var one = new List<Vector2>();
                        for (int i = 0; i < hole.Count; i++)
                        {
                            var vert = hole[i] + holesMoves[h][i] * bs;
                            V(vert.x, vert.y, -z);
                            if (t == 0) one.Add(vert);
                        }
                        if (t == 0) expanded.Add(one);
                    }
                }
                faces = ShapeUtils.TriangulateShape(contracted, expanded);
            }
            float bsFull = bevelSize + bevelOffset;
            for (int i = 0; i < vlen; i++) { var vert = bevelEnabled ? all[i] + vertMoves[i] * bsFull : all[i]; V(vert.x, vert.y, 0); }
            for (int s = 1; s <= steps; s++)
                for (int i = 0; i < vlen; i++) { var vert = bevelEnabled ? all[i] + vertMoves[i] * bsFull : all[i]; V(vert.x, vert.y, depth / steps * s); }
            for (int b = bevelSegments - 1; b >= 0; b--)
            {
                float t = (float)b / bevelSegments, z = bevelThickness * Mathf.Cos(t * Mathf.PI / 2), bs = bevelSize * Mathf.Sin(t * Mathf.PI / 2) + bevelOffset;
                for (int i = 0; i < contour.Count; i++) { var vert = contour[i] + contourMoves[i] * bs; V(vert.x, vert.y, depth + z); }
                for (int h = 0; h < holes.Count; h++)
                    for (int i = 0; i < holes[h].Count; i++) { var vert = holes[h][i] + holesMoves[h][i] * bs; V(vert.x, vert.y, depth + z); }
            }

            var g = new GeoData();
            void Add(int i) => g.pos.Add(ph[i]);
            void F3(int a, int b, int c)
            {
                Add(a); Add(b); Add(c);
                int n = g.pos.Count;
                for (int k = n - 3; k < n; k++) g.uv.Add(new Vector2(g.pos[k].x, g.pos[k].y));
            }
            void F4(int a, int b, int c, int d)
            {
                Add(a); Add(b); Add(d); Add(b); Add(c); Add(d);
                int n = g.pos.Count;
                Vector3 pa = g.pos[n - 6], pb = g.pos[n - 3], pc = g.pos[n - 2], pd = g.pos[n - 1];
                Vector2[] uvs = Mathf.Abs(pa.y - pb.y) < Mathf.Abs(pa.x - pb.x)
                    ? new[] { new Vector2(pa.x, 1 - pa.z), new Vector2(pb.x, 1 - pb.z), new Vector2(pc.x, 1 - pc.z), new Vector2(pd.x, 1 - pd.z) }
                    : new[] { new Vector2(pa.y, 1 - pa.z), new Vector2(pb.y, 1 - pb.z), new Vector2(pc.y, 1 - pc.z), new Vector2(pd.y, 1 - pd.z) };
                g.uv.Add(uvs[0]); g.uv.Add(uvs[1]); g.uv.Add(uvs[3]); g.uv.Add(uvs[1]); g.uv.Add(uvs[2]); g.uv.Add(uvs[3]);
            }
            // lids
            if (bevelEnabled)
            {
                int offset = 0;
                foreach (var f in faces) F3(f[2] + offset, f[1] + offset, f[0] + offset);
                offset = vlen * (steps + bevelSegments * 2);
                foreach (var f in faces) F3(f[0] + offset, f[1] + offset, f[2] + offset);
            }
            else
            {
                foreach (var f in faces) F3(f[2], f[1], f[0]);
                foreach (var f in faces) F3(f[0] + vlen * steps, f[1] + vlen * steps, f[2] + vlen * steps);
            }
            // side walls
            void Sidewalls(List<Vector2> c, int layeroffset)
            {
                int i = c.Count;
                while (--i >= 0)
                {
                    int j = i, k = i - 1;
                    if (k < 0) k = c.Count - 1;
                    for (int s = 0, sl = steps + bevelSegments * 2; s < sl; s++)
                    {
                        int slen1 = vlen * s, slen2 = vlen * (s + 1);
                        F4(layeroffset + j + slen1, layeroffset + k + slen1, layeroffset + k + slen2, layeroffset + j + slen2);
                    }
                }
            }
            int lo = 0;
            Sidewalls(contour, lo); lo += contour.Count;
            foreach (var h in holes) { Sidewalls(h, lo); lo += h.Count; }
            for (int i = 0; i < g.pos.Count; i++) g.index.Add(i);
            g.normal = Geo.VertexNormals(g);
            return g;
        }

        static List<Vector2> Dedup(IList<Vector2> src)
        {
            var o = new List<Vector2>();
            foreach (var p in src) if (o.Count == 0 || o[o.Count - 1].x != p.x || o[o.Count - 1].y != p.y) o.Add(p);
            return o;
        }

        static void MergeOverlapping(List<Vector2> points)
        {
            if (points.Count == 0) return;
            const double TH = 1e-10, TH2 = TH * TH;
            var prev = points[0];
            for (int i = 1; i <= points.Count; i++)
            {
                int ci = i % points.Count;
                var cur = points[ci];
                double dx = cur.x - prev.x, dy = cur.y - prev.y, d2 = dx * dx + dy * dy;
                double sc = Math.Max(Math.Max(Math.Abs(cur.x), Math.Abs(cur.y)), Math.Max(Math.Abs(prev.x), Math.Abs(prev.y)));
                if (d2 <= TH2 * sc * sc) { points.RemoveAt(ci); i--; if (points.Count == 0) return; continue; }
                prev = cur;
            }
        }

        static Vector2 BevelVec(Vector2 inPt, Vector2 inPrev, Vector2 inNext)
        {
            double vtx, vty, shrink;
            double vpx = inPt.x - inPrev.x, vpy = inPt.y - inPrev.y, vnx = inNext.x - inPt.x, vny = inNext.y - inPt.y;
            double vpl2 = vpx * vpx + vpy * vpy, col = vpx * vny - vpy * vnx;
            if (Math.Abs(col) > 2.220446049250313e-16)
            {
                double vpl = Math.Sqrt(vpl2), vnl = Math.Sqrt(vnx * vnx + vny * vny);
                double ppx = inPrev.x - vpy / vpl, ppy = inPrev.y + vpx / vpl, pnx = inNext.x - vny / vnl, pny = inNext.y + vnx / vnl;
                double sf = ((pnx - ppx) * vny - (pny - ppy) * vnx) / (vpx * vny - vpy * vnx);
                vtx = ppx + vpx * sf - inPt.x; vty = ppy + vpy * sf - inPt.y;
                double tl2 = vtx * vtx + vty * vty;
                if (tl2 <= 2) return new Vector2((float)vtx, (float)vty);
                shrink = Math.Sqrt(tl2 / 2);
            }
            else
            {
                const double EPS = 2.220446049250313e-16;
                bool same = false;
                if (vpx > EPS) { if (vnx > EPS) same = true; }
                else if (vpx < -EPS) { if (vnx < -EPS) same = true; }
                else if (Math.Sign(vpy) == Math.Sign(vny)) same = true;
                if (same) { vtx = -vpy; vty = vpx; shrink = Math.Sqrt(vpl2); }
                else { vtx = vpx; vty = vpy; shrink = Math.Sqrt(vpl2 / 2); }
            }
            return new Vector2((float)(vtx / shrink), (float)(vty / shrink));
        }
    }
}
