// engine/fx.js billboards(): camera-facing quads, one draw call per system. three.js expands them in the vertex
// shader; here the quads are rebuilt on the CPU right before each camera renders (beginCameraRendering /
// onPreCull), so they face the camera that draws them, whatever camera the caller passed to update().
// Per-instance data stays JS space: p is local to the system's object, or world space when `world` is set.
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gyeongbo.Systems
{
    internal sealed class FxBillboards
    {
        static readonly List<FxBillboards> live = new List<FxBillboards>();
        static bool hooked;

        readonly int max, cells;
        readonly Mesh mesh;
        readonly Vector3[] P;
        readonly float[] S, Rot;
        readonly int[] F;
        readonly Color[] C;
        readonly Vector3[] vtx, nrm;
        readonly Vector2[] uv;
        readonly Color[] col;
        readonly int[] tri, order;
        readonly float[] dist;
        int n, shownN = -1;
        bool dirty = true;

        public readonly GameObject obj;
        public bool world;        // instance positions are world JS space (extinguisher powder)
        public bool sort;         // back to front (normally blended particles)
        public bool lit;          // normals toward the camera (MeshLambertMaterial smoke)
        // gl_PointSize stand-in: size in pixels = size * scale / max(minDepth, depth), clamped to [pxMin, pxMax].
        public bool points;
        public float pxMin, pxMax = float.PositiveInfinity, minDepth;

        public FxBillboards(int max, Material material, int cells, string name)
        {
            this.max = Mathf.Max(1, max); this.cells = Mathf.Max(1, cells);
            int m = this.max;
            P = new Vector3[m]; S = new float[m]; Rot = new float[m]; F = new int[m]; C = new Color[m];
            vtx = new Vector3[m * 4]; nrm = new Vector3[m * 4]; uv = new Vector2[m * 4]; col = new Color[m * 4];
            tri = new int[m * 6]; order = new int[m]; dist = new float[m];
            for (int i = 0; i < m; i++)
            {
                // Corners BL BR TR TL on screen; Unity's front faces wind clockwise on screen.
                int v = i * 4, t = i * 6;
                tri[t] = v; tri[t + 1] = v + 2; tri[t + 2] = v + 1; tri[t + 3] = v; tri[t + 4] = v + 3; tri[t + 5] = v + 2;
            }
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            obj = Geo.Obj(mesh, material, name, false, false);
            live.Add(this);
            Hook();
        }

        public MeshRenderer Renderer => obj != null ? obj.GetComponent<MeshRenderer>() : null;
        public int RenderOrder { set { var r = Renderer; if (r != null) r.sortingOrder = value; } }

        public void Begin() => n = 0;

        public void Push(Vector3 p, float size, float rot, int frame, float r, float g, float b, float a)
        {
            if (n >= max || a <= 0.003f) return;
            P[n] = p; S[n] = size; Rot[n] = rot; F[n] = frame; C[n] = new Color(r, g, b, a); n++;
        }

        public void End() => dirty = true;

        public void Dispose()
        {
            live.Remove(this);
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
        }

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            RenderPipelineManager.beginCameraRendering += (context, cam) => ExpandAll(cam);
            Camera.onPreCull += ExpandAll;
        }

        static void ExpandAll(Camera cam)
        {
            if (cam == null || cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var b = live[i];
                if (b.obj == null) { live.RemoveAt(i); continue; }
                if (!b.obj.activeInHierarchy) continue;
                try { b.Expand(cam); } catch (Exception e) { Debug.LogException(e); live.RemoveAt(i); }
            }
        }

        void Expand(Camera cam)
        {
            if (n == 0 && shownN == 0 && !dirty) return;
            dirty = false;
            var tr = obj.transform;
            Matrix4x4 w2l = tr.worldToLocalMatrix, l2w = tr.localToWorldMatrix;
            var ct = cam.transform;
            Vector3 camPos = ct.position, fwd = ct.forward;
            Vector3 right = w2l.MultiplyVector(ct.right), up = w2l.MultiplyVector(ct.up);
            Vector3 normal = w2l.MultiplyVector(-fwd).normalized;
            float scale = cam.pixelHeight / (2 * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2));

            for (int i = 0; i < n; i++) order[i] = i;
            if (sort && n > 1)
            {
                Vector3 c = world ? camPos : tr.InverseTransformPoint(camPos);
                for (int i = 0; i < n; i++) dist[i] = (J.V(P[i]) - c).sqrMagnitude;
                Array.Sort(order, 0, n, Comparer<int>.Create((a, b) => dist[b].CompareTo(dist[a])));
            }

            for (int k = 0; k < n; k++)
            {
                int i = order[k];
                Vector3 u = J.V(P[i]);
                Vector3 center = world ? w2l.MultiplyPoint3x4(u) : u;
                float size = S[i];
                if (points)
                {
                    Vector3 wp = world ? u : l2w.MultiplyPoint3x4(u);
                    float depth = Vector3.Dot(wp - camPos, fwd);
                    float px = Mathf.Clamp(size * scale / Mathf.Max(minDepth, depth), pxMin, pxMax);
                    size = px * Mathf.Max(depth, 1e-4f) / scale;
                }
                float c = Mathf.Cos(Rot[i]) * size, s = Mathf.Sin(Rot[i]) * size;
                int fr = F[i];
                float cu = fr % cells, cv = fr / cells;
                int v = k * 4;
                for (int q = 0; q < 4; q++)
                {
                    float qx = q == 0 || q == 3 ? -0.5f : 0.5f, qy = q < 2 ? -0.5f : 0.5f;
                    vtx[v + q] = center + (c * qx - s * qy) * right + (s * qx + c * qy) * up;
                    uv[v + q] = new Vector2((cu + qx + 0.5f) / cells, (cv + qy + 0.5f) / cells);
                    col[v + q] = C[i];
                    nrm[v + q] = normal;
                }
            }
            mesh.Clear(true);
            if (n > 0)
            {
                mesh.SetVertices(vtx, 0, n * 4);
                mesh.SetUVs(0, uv, 0, n * 4);
                mesh.SetColors(col, 0, n * 4);
                if (lit) mesh.SetNormals(nrm, 0, n * 4);
                mesh.SetTriangles(tri, 0, n * 6, 0);
            }
            shownN = n;
        }
    }
}
