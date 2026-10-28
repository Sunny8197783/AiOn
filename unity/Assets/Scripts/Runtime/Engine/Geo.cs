// three.js geometries for the ports: built in JS space with three.js parameters and conventions
// (PlaneGeometry in XY facing +z, CylinderGeometry centred on the origin, UV v up), transformed in JS space
// (translate/rotateX/...), then ToMesh() mirrors X for Unity (PORTING.md "Coordinates"). Owned by the engine
// group; signatures are shared by every level, people and fx port. Generators follow three.js r17x line by line
// (same vertex and index order); the rarer shapes live in GeoShapes.cs.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gyeongbo.Engine
{
    public sealed class GeoData
    {
        public List<Vector3> pos = new List<Vector3>();      // JS space
        public List<Vector3> normal = new List<Vector3>();   // JS space
        public List<Vector2> uv = new List<Vector2>();
        public List<int> index = new List<int>();            // three.js (counter-clockwise) order
        public List<Color> color;                            // optional vertex colours
        internal int version;                                // bumped by every transform (Geo.Obj mesh cache)

        public GeoData Translate(float x, float y, float z)
        {
            var d = new Vector3(x, y, z);
            for (int i = 0; i < pos.Count; i++) pos[i] += d;
            version++;
            return this;
        }

        // three.js Matrix4.makeRotationX/Y/Z applied to positions and normals.
        public GeoData RotateX(float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector3 R(Vector3 v) => new Vector3(v.x, c * v.y - s * v.z, s * v.y + c * v.z);
            for (int i = 0; i < pos.Count; i++) pos[i] = R(pos[i]);
            for (int i = 0; i < normal.Count; i++) normal[i] = R(normal[i]).normalized;
            version++;
            return this;
        }

        public GeoData RotateY(float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector3 R(Vector3 v) => new Vector3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
            for (int i = 0; i < pos.Count; i++) pos[i] = R(pos[i]);
            for (int i = 0; i < normal.Count; i++) normal[i] = R(normal[i]).normalized;
            version++;
            return this;
        }

        public GeoData RotateZ(float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector3 R(Vector3 v) => new Vector3(c * v.x - s * v.y, s * v.x + c * v.y, v.z);
            for (int i = 0; i < pos.Count; i++) pos[i] = R(pos[i]);
            for (int i = 0; i < normal.Count; i++) normal[i] = R(normal[i]).normalized;
            version++;
            return this;
        }

        public GeoData Scale(float x, float y, float z) => ApplyMatrix(Matrix4x4.Scale(new Vector3(x, y, z)));

        // m is a three.js Matrix4 in JS space (column vectors), e.g. from Matrix4x4.TRS on JS values.
        // Normals use the normal matrix (inverse transpose) and are renormalised, like applyMatrix4.
        public GeoData ApplyMatrix(Matrix4x4 m)
        {
            for (int i = 0; i < pos.Count; i++) pos[i] = m.MultiplyPoint3x4(pos[i]);
            if (normal.Count > 0)
            {
                var nm = m.inverse.transpose;
                for (int i = 0; i < normal.Count; i++) normal[i] = nm.MultiplyVector(normal[i]).normalized;
            }
            version++;
            return this;
        }

        public GeoData Clone() => new GeoData
        {
            pos = new List<Vector3>(pos), normal = new List<Vector3>(normal), uv = new List<Vector2>(uv),
            index = new List<int>(index), color = color != null ? new List<Color>(color) : null,
        };

        // Mirror X and build the Unity mesh (normals/tangents included). Missing normals are computed the way
        // three.js computeVertexNormals does (before the mirror); an empty index means a non-indexed list.
        public Mesh ToMesh(string name = null)
        {
            int n = pos.Count;
            var p = new List<Vector3>(n);
            foreach (var v in pos) p.Add(new Vector3(-v.x, v.y, v.z));
            var src = normal.Count == n ? normal : Geo.VertexNormals(this);
            var nn = new List<Vector3>(n);
            foreach (var v in src) nn.Add(new Vector3(-v.x, v.y, v.z));
            var tri = index;
            if (tri.Count == 0) { tri = new List<int>(n); for (int i = 0; i < n; i++) tri.Add(i); }
            var mesh = new Mesh { name = name ?? "geo", indexFormat = n > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(p);
            mesh.SetNormals(nn);
            bool hasUv = uv.Count == n && n > 0;
            if (hasUv) mesh.SetUVs(0, uv);
            if (color != null && color.Count == n) mesh.SetColors(color);
            mesh.SetTriangles(tri, 0, true);
            if (hasUv) mesh.RecalculateTangents();   // normal maps need tangents; cheap enough to always have them
            return mesh;
        }
    }

    public static partial class Geo
    {
        // ---- three.js BoxGeometry ----
        public static GeoData Box(float w, float h, float d, int ws = 1, int hs = 1, int ds = 1)
        {
            var g = new GeoData();
            int numberOfVertices = 0;
            void BuildPlane(int u, int v, int wAx, float udir, float vdir, float width, float height, float depth, int gridX, int gridY)
            {
                float segmentWidth = width / gridX, segmentHeight = height / gridY;
                float widthHalf = width / 2, heightHalf = height / 2, depthHalf = depth / 2;
                int gridX1 = gridX + 1, gridY1 = gridY + 1, vertexCounter = 0;
                var vec = new float[3];
                for (int iy = 0; iy < gridY1; iy++)
                {
                    float y = iy * segmentHeight - heightHalf;
                    for (int ix = 0; ix < gridX1; ix++)
                    {
                        float x = ix * segmentWidth - widthHalf;
                        vec[u] = x * udir; vec[v] = y * vdir; vec[wAx] = depthHalf;
                        g.pos.Add(new Vector3(vec[0], vec[1], vec[2]));
                        vec[u] = 0; vec[v] = 0; vec[wAx] = depth > 0 ? 1 : -1;
                        g.normal.Add(new Vector3(vec[0], vec[1], vec[2]));
                        g.uv.Add(new Vector2((float)ix / gridX, 1 - (float)iy / gridY));
                        vertexCounter++;
                    }
                }
                for (int iy = 0; iy < gridY; iy++)
                    for (int ix = 0; ix < gridX; ix++)
                    {
                        int a = numberOfVertices + ix + gridX1 * iy, b = numberOfVertices + ix + gridX1 * (iy + 1);
                        int c = numberOfVertices + (ix + 1) + gridX1 * (iy + 1), dd = numberOfVertices + (ix + 1) + gridX1 * iy;
                        g.index.Add(a); g.index.Add(b); g.index.Add(dd);
                        g.index.Add(b); g.index.Add(c); g.index.Add(dd);
                    }
                numberOfVertices += vertexCounter;
            }
            ws = Mathf.Max(1, ws); hs = Mathf.Max(1, hs); ds = Mathf.Max(1, ds);
            BuildPlane(2, 1, 0, -1, -1, d, h, w, ds, hs);    // px
            BuildPlane(2, 1, 0, 1, -1, d, h, -w, ds, hs);    // nx
            BuildPlane(0, 2, 1, 1, 1, w, d, h, ws, ds);      // py
            BuildPlane(0, 2, 1, 1, -1, w, d, -h, ws, ds);    // ny
            BuildPlane(0, 1, 2, 1, -1, w, h, d, ws, hs);     // pz
            BuildPlane(0, 1, 2, -1, -1, w, h, -d, ws, hs);   // nz
            return g;
        }

        // ---- three.js PlaneGeometry (XY plane facing +z) ----
        public static GeoData Plane(float w, float h, int ws = 1, int hs = 1)
        {
            var g = new GeoData();
            float wh = w / 2, hh = h / 2;
            int gridX = Mathf.Max(1, ws), gridY = Mathf.Max(1, hs), gridX1 = gridX + 1, gridY1 = gridY + 1;
            float sw = w / gridX, sh = h / gridY;
            for (int iy = 0; iy < gridY1; iy++)
            {
                float y = iy * sh - hh;
                for (int ix = 0; ix < gridX1; ix++)
                {
                    float x = ix * sw - wh;
                    g.pos.Add(new Vector3(x, -y, 0)); g.normal.Add(new Vector3(0, 0, 1));
                    g.uv.Add(new Vector2((float)ix / gridX, 1 - (float)iy / gridY));
                }
            }
            for (int iy = 0; iy < gridY; iy++)
                for (int ix = 0; ix < gridX; ix++)
                {
                    int a = ix + gridX1 * iy, b = ix + gridX1 * (iy + 1), c = ix + 1 + gridX1 * (iy + 1), d = ix + 1 + gridX1 * iy;
                    g.index.Add(a); g.index.Add(b); g.index.Add(d);
                    g.index.Add(b); g.index.Add(c); g.index.Add(d);
                }
            return g;
        }

        // ---- three.js CylinderGeometry (centred, axis y) ----
        public static GeoData Cylinder(float rt, float rb, float h, int radialSeg = 32, int heightSeg = 1, bool openEnded = false, float thetaStart = 0, float thetaLength = Mathf.PI * 2)
        {
            var g = new GeoData();
            radialSeg = Mathf.Max(1, radialSeg); heightSeg = Mathf.Max(1, heightSeg);
            int index = 0;
            var indexArray = new List<int[]>();
            float halfHeight = h / 2, slope = (rb - rt) / h;
            for (int y = 0; y <= heightSeg; y++)
            {
                var row = new int[radialSeg + 1];
                float v = (float)y / heightSeg, radius = v * (rb - rt) + rt;
                for (int x = 0; x <= radialSeg; x++)
                {
                    float u = (float)x / radialSeg, theta = u * thetaLength + thetaStart, sin = Mathf.Sin(theta), cos = Mathf.Cos(theta);
                    g.pos.Add(new Vector3(radius * sin, -v * h + halfHeight, radius * cos));
                    g.normal.Add(new Vector3(sin, slope, cos).normalized);
                    g.uv.Add(new Vector2(u, 1 - v));
                    row[x] = index++;
                }
                indexArray.Add(row);
            }
            for (int x = 0; x < radialSeg; x++)
                for (int y = 0; y < heightSeg; y++)
                {
                    int a = indexArray[y][x], b = indexArray[y + 1][x], c = indexArray[y + 1][x + 1], d = indexArray[y][x + 1];
                    if (rt > 0 || y != 0) { g.index.Add(a); g.index.Add(b); g.index.Add(d); }
                    if (rb > 0 || y != heightSeg - 1) { g.index.Add(b); g.index.Add(c); g.index.Add(d); }
                }
            void Cap(bool top)
            {
                int centerStart = index;
                float radius = top ? rt : rb, sign = top ? 1 : -1;
                for (int x = 1; x <= radialSeg; x++)
                {
                    g.pos.Add(new Vector3(0, halfHeight * sign, 0)); g.normal.Add(new Vector3(0, sign, 0)); g.uv.Add(new Vector2(0.5f, 0.5f));
                    index++;
                }
                int centerEnd = index;
                for (int x = 0; x <= radialSeg; x++)
                {
                    float u = (float)x / radialSeg, theta = u * thetaLength + thetaStart, cos = Mathf.Cos(theta), sin = Mathf.Sin(theta);
                    g.pos.Add(new Vector3(radius * sin, halfHeight * sign, radius * cos)); g.normal.Add(new Vector3(0, sign, 0));
                    g.uv.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f * sign + 0.5f));
                    index++;
                }
                for (int x = 0; x < radialSeg; x++)
                {
                    int c = centerStart + x, i = centerEnd + x;
                    if (top) { g.index.Add(i); g.index.Add(i + 1); g.index.Add(c); }
                    else { g.index.Add(i + 1); g.index.Add(i); g.index.Add(c); }
                }
            }
            if (!openEnded)
            {
                if (rt > 0) Cap(true);
                if (rb > 0) Cap(false);
            }
            return g;
        }

        public static GeoData Cone(float r, float h, int radialSeg = 32, int heightSeg = 1, bool openEnded = false) => Cylinder(0, r, h, radialSeg, heightSeg, openEnded);

        // ---- three.js SphereGeometry ----
        public static GeoData Sphere(float r = 1, int ws = 32, int hs = 16, float phiStart = 0, float phiLength = Mathf.PI * 2, float thetaStart = 0, float thetaLength = Mathf.PI)
        {
            var g = new GeoData();
            ws = Mathf.Max(3, ws); hs = Mathf.Max(2, hs);
            float thetaEnd = Mathf.Min(thetaStart + thetaLength, Mathf.PI);
            int index = 0;
            var grid = new List<int[]>();
            for (int iy = 0; iy <= hs; iy++)
            {
                var row = new int[ws + 1];
                float v = (float)iy / hs, uOffset = 0;
                if (iy == 0 && thetaStart == 0) uOffset = 0.5f / ws;
                else if (iy == hs && thetaEnd == Mathf.PI) uOffset = -0.5f / ws;
                for (int ix = 0; ix <= ws; ix++)
                {
                    float u = (float)ix / ws;
                    var vert = new Vector3(-r * Mathf.Cos(phiStart + u * phiLength) * Mathf.Sin(thetaStart + v * thetaLength),
                        r * Mathf.Cos(thetaStart + v * thetaLength),
                        r * Mathf.Sin(phiStart + u * phiLength) * Mathf.Sin(thetaStart + v * thetaLength));
                    g.pos.Add(vert); g.normal.Add(vert.normalized); g.uv.Add(new Vector2(u + uOffset, 1 - v));
                    row[ix] = index++;
                }
                grid.Add(row);
            }
            for (int iy = 0; iy < hs; iy++)
                for (int ix = 0; ix < ws; ix++)
                {
                    int a = grid[iy][ix + 1], b = grid[iy][ix], c = grid[iy + 1][ix], d = grid[iy + 1][ix + 1];
                    if (iy != 0 || thetaStart > 0) { g.index.Add(a); g.index.Add(b); g.index.Add(d); }
                    if (iy != hs - 1 || thetaEnd < Mathf.PI) { g.index.Add(b); g.index.Add(c); g.index.Add(d); }
                }
            return g;
        }

        // ---- three.js CircleGeometry (XY, facing +z) ----
        public static GeoData Circle(float r = 1, int seg = 32, float thetaStart = 0, float thetaLength = Mathf.PI * 2)
        {
            var g = new GeoData();
            seg = Mathf.Max(3, seg);
            g.pos.Add(Vector3.zero); g.normal.Add(new Vector3(0, 0, 1)); g.uv.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s <= seg; s++)
            {
                float a = thetaStart + (float)s / seg * thetaLength;
                var v = new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), 0);
                g.pos.Add(v); g.normal.Add(new Vector3(0, 0, 1)); g.uv.Add(new Vector2((v.x / r + 1) / 2, (v.y / r + 1) / 2));
            }
            for (int i = 1; i <= seg; i++) { g.index.Add(i); g.index.Add(i + 1); g.index.Add(0); }
            return g;
        }

        // ---- three.js RingGeometry (phiSegments 1) ----
        public static GeoData Ring(float inner = 0.5f, float outer = 1, int thetaSeg = 32)
        {
            var g = new GeoData();
            const int phiSeg = 1;
            float thetaStart = 0, thetaLength = Mathf.PI * 2;
            thetaSeg = Mathf.Max(3, thetaSeg);
            float radius = inner, step = (outer - inner) / phiSeg;
            for (int j = 0; j <= phiSeg; j++)
            {
                for (int i = 0; i <= thetaSeg; i++)
                {
                    float s = thetaStart + (float)i / thetaSeg * thetaLength;
                    var v = new Vector3(radius * Mathf.Cos(s), radius * Mathf.Sin(s), 0);
                    g.pos.Add(v); g.normal.Add(new Vector3(0, 0, 1)); g.uv.Add(new Vector2((v.x / outer + 1) / 2, (v.y / outer + 1) / 2));
                }
                radius += step;
            }
            for (int j = 0; j < phiSeg; j++)
            {
                int level = j * (thetaSeg + 1);
                for (int i = 0; i < thetaSeg; i++)
                {
                    int s = i + level, a = s, b = s + thetaSeg + 1, c = s + thetaSeg + 2, d = s + 1;
                    g.index.Add(a); g.index.Add(b); g.index.Add(d);
                    g.index.Add(b); g.index.Add(c); g.index.Add(d);
                }
            }
            return g;
        }

        // ---- three.js TorusGeometry (ring in the XY plane) ----
        public static GeoData Torus(float r = 1, float tube = 0.4f, int radialSeg = 12, int tubularSeg = 48, float arc = Mathf.PI * 2)
        {
            var g = new GeoData();
            radialSeg = Mathf.Max(1, radialSeg); tubularSeg = Mathf.Max(1, tubularSeg);
            for (int j = 0; j <= radialSeg; j++)
                for (int i = 0; i <= tubularSeg; i++)
                {
                    float u = (float)i / tubularSeg * arc, v = (float)j / radialSeg * Mathf.PI * 2;
                    var vert = new Vector3((r + tube * Mathf.Cos(v)) * Mathf.Cos(u), (r + tube * Mathf.Cos(v)) * Mathf.Sin(u), tube * Mathf.Sin(v));
                    var center = new Vector3(r * Mathf.Cos(u), r * Mathf.Sin(u), 0);
                    g.pos.Add(vert); g.normal.Add((vert - center).normalized);
                    g.uv.Add(new Vector2((float)i / tubularSeg, (float)j / radialSeg));
                }
            for (int j = 1; j <= radialSeg; j++)
                for (int i = 1; i <= tubularSeg; i++)
                {
                    int a = (tubularSeg + 1) * j + i - 1, b = (tubularSeg + 1) * (j - 1) + i - 1, c = (tubularSeg + 1) * (j - 1) + i, d = (tubularSeg + 1) * j + i;
                    g.index.Add(a); g.index.Add(b); g.index.Add(d);
                    g.index.Add(b); g.index.Add(c); g.index.Add(d);
                }
            return g;
        }

        // ---- new THREE.Mesh(geometry, material) ----
        sealed class MeshEntry { public Mesh mesh; public int version, count; }
        static readonly ConditionalWeakTable<GeoData, MeshEntry> meshes = new ConditionalWeakTable<GeoData, MeshEntry>();

        // One Unity mesh per GeoData (three.js shares a geometry between meshes); rebuilt after a transform.
        internal static Mesh MeshOf(GeoData g)
        {
            var e = meshes.GetOrCreateValue(g);
            if (e.mesh == null || e.version != g.version || e.count != g.pos.Count) { e.mesh = g.ToMesh(); e.version = g.version; e.count = g.pos.Count; }
            return e.mesh;
        }

        public static GameObject Obj(GeoData g, Material m, string name = null, bool cast = true, bool receive = true) => Obj(MeshOf(g), m, name, cast, receive);

        public static GameObject Obj(Mesh mesh, Material m, string name = null, bool cast = true, bool receive = true)
        {
            var go = new GameObject(name ?? "mesh");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receive;
            return go;
        }
    }
}
