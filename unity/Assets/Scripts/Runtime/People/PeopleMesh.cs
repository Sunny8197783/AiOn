// Ports builder() from engine/people-geo.js: accumulates lofted parts into one indexed mesh with per-vertex
// colour, PBR (aPbr), pattern coordinates (pUv) and optional skin weights, so one person (or one prop) is a
// single mesh with one shared material. Parts are built in JS space; Build() mirrors X into a Unity Mesh
// keeping the triangle order (PORTING.md "Coordinates", like J.MirrorMesh).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gyeongbo.People
{
    // Loft / pipe / rbox / blob options: { seg, color: Color | fn(ring, theta, i, p), pbr: array | fn,
    // skin: bone | fn(p) -> [b0,w0,b1,w1,...], cap, flip, m (Matrix4 applied after normals), p, rings }.
    public sealed class LoftOpts
    {
        public Color? c;
        public Func<BodyRow, float, int, Vector3, Color> color;
        public float[] pbr;
        public Func<BodyRow, float, int, Vector3, float[]> pbrFn;
        public int? seg;
        public bool cap, flip;
        public int bone;                     // skin: bone (a number); skin fn overrides it
        public Func<Vector3, float[]> skin;  // all the JS skin functions read only the point
        public Matrix4x4? m;
        public float? p;                     // rbox / blob superellipse exponent
        public int? rings;                   // blob rows
        public LoftOpts Copy() => (LoftOpts)MemberwiseClone();
    }

    public sealed class MeshBuilder
    {
        readonly bool skinned;
        readonly List<Vector3> P = new List<Vector3>(), N = new List<Vector3>();
        readonly List<Color> C = new List<Color>();
        readonly List<Vector2> U = new List<Vector2>();
        readonly List<Vector4> R = new List<Vector4>();
        readonly List<BoneWeight> W = new List<BoneWeight>();
        readonly List<int> I = new List<int>();

        public MeshBuilder(bool skinned = false) { this.skinned = skinned; }
        public int VertexCount => P.Count;

        static void PushSkin(List<float> output, float[] sk, int bone)
        {
            if (sk == null) { output.Add(bone); output.Add(1); for (int k = 0; k < 6; k++) output.Add(0); return; }
            for (int k = 0; k < 8; k++) output.Add(k < sk.Length ? sk[k] : 0);
        }

        static Vector3 Norm(Vector3 v) { float l = v.magnitude; return l > 0 ? v / l : v; }   // three.js normalize

        void AddPart(List<Vector3> pos, List<Color> cols, List<Vector2> uvs, List<float[]> pbrs, List<float> skin, List<int> idx, Matrix4x4? m)
        {
            int bas = P.Count, n = pos.Count;
            var nrm = new Vector3[n];
            for (int t = 0; t < idx.Count; t += 3) // area-weighted face normals
            {
                Vector3 a = pos[idx[t]], b = pos[idx[t + 1]], c = pos[idx[t + 2]];
                Vector3 e1 = b - a, e2 = c - a;
                var fn = new Vector3(e1.y * e2.z - e1.z * e2.y, e1.z * e2.x - e1.x * e2.z, e1.x * e2.y - e1.y * e2.x);
                nrm[idx[t]] += fn; nrm[idx[t + 1]] += fn; nrm[idx[t + 2]] += fn;
            }
            Matrix4x4 nm = m.HasValue ? m.Value.inverse.transpose : Matrix4x4.identity;
            for (int i = 0; i < n; i++)
            {
                Vector3 v = pos[i], nv = nrm[i];
                if (m.HasValue) { v = m.Value.MultiplyPoint3x4(v); nv = nm.MultiplyVector(nv); }
                if (nv.sqrMagnitude < 1e-20f) nv = new Vector3(0, 1, 0);
                P.Add(v); N.Add(Norm(nv));
            }
            C.AddRange(cols); U.AddRange(uvs);
            foreach (var pb in pbrs) R.Add(new Vector4(pb[0], pb[1], pb[2], pb[3]));
            if (skinned) for (int i = 0; i < n; i++) // up to 4 influences, normalised
                {
                    int k = i * 8;
                    float s = skin[k + 1] + skin[k + 3] + skin[k + 5] + skin[k + 7]; if (s == 0) s = 1;
                    W.Add(Weight((int)skin[k], skin[k + 1] / s, (int)skin[k + 2], skin[k + 3] / s, (int)skin[k + 4], skin[k + 5] / s, (int)skin[k + 6], skin[k + 7] / s));
                }
            foreach (var k in idx) I.Add(k + bas);
        }

        // Unity wants the influences sorted by weight, largest first.
        static BoneWeight Weight(int b0, float w0, int b1, float w1, int b2, float w2, int b3, float w3)
        {
            var bs = new[] { b0, b1, b2, b3 }; var ws = new[] { w0, w1, w2, w3 };
            Array.Sort(ws, bs); Array.Reverse(ws); Array.Reverse(bs);
            return new BoneWeight { boneIndex0 = bs[0], weight0 = ws[0], boneIndex1 = bs[1], weight1 = ws[1], boneIndex2 = bs[2], weight2 = ws[2], boneIndex3 = bs[3], weight3 = ws[3] };
        }

        // theta 0 points +z (back), +-PI points -z (front), +PI/2 points +x. Arc mode (any ring has a) leaves a gap.
        public void Loft(IList<BodyRow> rings0, LoftOpts o)
        {
            var rings = new List<BodyRow>(rings0);
            if (o.cap)
            {
                var f = rings[0].Clone(); f.rx = 0.0006f; f.rz = 0.0006f;
                var l = rings[rings.Count - 1].Clone(); l.rx = 0.0006f; l.rz = 0.0006f;
                rings.Insert(0, f); rings.Add(l);
            }
            int seg = o.seg ?? 12;
            bool arc = false; foreach (var r in rings) if (r.a != null) arc = true;
            int n = arc ? seg + 1 : seg;
            bool desc = rings[rings.Count - 1].y < rings[0].y;
            var pos = new List<Vector3>(); var cols = new List<Color>(); var uvs = new List<Vector2>(); var pbrs = new List<float[]>(); var skin = new List<float>(); var idx = new List<int>();
            float v = 0;
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                if (i > 0) { var q = rings[i - 1]; v += new Vector3(r.y - q.y, (r.x ?? 0) - (q.x ?? 0), (r.z ?? 0) - (q.z ?? 0)).magnitude; }
                float rz = r.rz ?? r.rx, a = r.a ?? Mathf.PI, ac = r.ac ?? 0, e = 2 / (r.p ?? 2), circ = (r.rx + rz) / 2;
                for (int k = 0; k < n; k++)
                {
                    float th = arc ? ac - a + 2 * a * k / seg : -Mathf.PI + 2 * Mathf.PI * k / seg;
                    float s = Mathf.Sin(th), c = Mathf.Cos(th);
                    var p = new Vector3((r.x ?? 0) + r.rx * Math.Sign(s) * Mathf.Pow(Mathf.Abs(s), e), r.y, (r.z ?? 0) + rz * Math.Sign(c) * Mathf.Pow(Mathf.Abs(c), e));
                    pos.Add(p);
                    cols.Add(o.color != null ? o.color(r, th, i, p) : (r.c ?? o.c.Value));
                    uvs.Add(new Vector2(th * circ, v));
                    pbrs.Add(o.pbrFn != null ? o.pbrFn(r, th, i, p) : (r.pbr ?? o.pbr ?? PeopleGeo.PBR["fabric"]));
                    PushSkin(skin, o.skin?.Invoke(p), o.bone);
                }
            }
            bool outward = desc != o.flip;
            for (int i = 0; i < rings.Count - 1; i++) for (int k = 0; k < seg; k++)
                {
                    int k1 = arc ? k + 1 : (k + 1) % seg;
                    int a = i * n + k, b = i * n + k1, c = (i + 1) * n + k, d = (i + 1) * n + k1;
                    if (outward) idx.AddRange(new[] { a, c, b, b, c, d }); else idx.AddRange(new[] { a, b, c, b, d, c });
                }
            AddPart(pos, cols, uvs, pbrs, skin, idx, o.m);
        }

        struct PipeRing { public Vector3 p, nr, b; public float r; }

        // Tube through 3D points; r: one radius or per-point radii. Ends are capped.
        public void Pipe(IList<Vector3> pts, float r, LoftOpts o) => Pipe(pts, null, r, o);
        public void Pipe(IList<Vector3> pts, float[] radii, LoftOpts o) => Pipe(pts, radii, 0, o);

        void Pipe(IList<Vector3> V, float[] radii, float r, LoftOpts o)
        {
            int seg = o.seg ?? 8, n = V.Count;
            var T = new Vector3[n];
            for (int i = 0; i < n; i++) T[i] = Norm(V[Math.Min(n - 1, i + 1)] - V[Math.Max(0, i - 1)]);
            var nr = Mathf.Abs(T[0].y) < 0.9f ? new Vector3(0, 1, 0) : new Vector3(1, 0, 0);
            var ringsP = new List<PipeRing>();
            for (int i = 0; i < n; i++)
            {
                nr = Norm(nr - T[i] * Vector3.Dot(nr, T[i]));
                ringsP.Add(new PipeRing { p = V[i], nr = nr, b = Vector3.Cross(T[i], nr), r = radii != null ? radii[i] : r });
            }
            var all = new List<PipeRing>();
            var first = ringsP[0]; first.r = 0.0004f; all.Add(first);
            all.AddRange(ringsP);
            var lastR = ringsP[n - 1]; lastR.r = 0.0004f; all.Add(lastR);
            var pos = new List<Vector3>(); var cols = new List<Color>(); var uvs = new List<Vector2>(); var pbrs = new List<float[]>(); var skin = new List<float>(); var idx = new List<int>();
            Color cc = o.c.Value; float[] pb = o.pbr ?? PeopleGeo.PBR["plastic"];
            float v = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var R0 = all[i];
                if (i > 0) v += Vector3.Distance(R0.p, all[i - 1].p);
                for (int k = 0; k < seg; k++)
                {
                    float th = 2 * Mathf.PI * k / seg, c = Mathf.Cos(th), s = Mathf.Sin(th);
                    pos.Add(R0.p + R0.r * (R0.nr * c + R0.b * s));
                    cols.Add(cc); uvs.Add(new Vector2(th * R0.r, v)); pbrs.Add(pb);
                    PushSkin(skin, o.skin?.Invoke(R0.p), o.bone);
                }
            }
            for (int i = 0; i < all.Count - 1; i++) for (int k = 0; k < seg; k++)
                {
                    int k1 = (k + 1) % seg, a = i * seg + k, b = i * seg + k1, c = (i + 1) * seg + k, d = (i + 1) * seg + k1;
                    idx.AddRange(new[] { a, b, c, b, d, c });
                }
            AddPart(pos, cols, uvs, pbrs, skin, idx, o.m);
        }

        // Rounded box standing on y=0 (w along x, d along z), optional matrix o.m to place it.
        public void RBox(float w, float h, float d, float rad, LoftOpts o)
        {
            float hw = w / 2, hd = d / 2, rr = Mathf.Min(Mathf.Min(rad, hw), Mathf.Min(hd, h / 2)), p = o.p ?? 6;
            BodyRow Ring(float y, float k) => new BodyRow { y = y, rx = Mathf.Max(0.0006f, hw - rr * k), rz = Mathf.Max(0.0006f, hd - rr * k), p = p };
            var rings = new List<BodyRow> { Ring(0, 1), Ring(rr * 0.29f, 0.29f), Ring(rr, 0), Ring(h - rr, 0), Ring(h - rr * 0.29f, 0.29f), Ring(h, 1) };
            var lo = o.Copy(); lo.seg = o.seg ?? 16; lo.cap = true;
            Loft(rings, lo);
        }

        // Ellipsoid centred at c with radii r.
        public void Blob(Vector3 c, Vector3 r, LoftOpts o)
        {
            int n = o.rings ?? 8;
            var rows = new List<BodyRow>();
            for (int k = 0; k <= n; k++)
            {
                float f = -Mathf.PI / 2 + Mathf.PI * k / n, cf = Mathf.Max(0.02f, Mathf.Cos(f));
                rows.Add(new BodyRow { y = c.y + r.y * Mathf.Sin(f), x = c.x, z = c.z, rx = r.x * cf, rz = r.z * cf, p = o.p });
            }
            var lo = o.Copy(); lo.seg = o.seg ?? 10;
            Loft(rows, lo);
        }

        // -> Unity Mesh (X mirrored, same triangle order). pUv -> uv0, aPbr -> uv2, skin -> boneWeights; the caller
        // sets bindposes for skinned meshes.
        public Mesh Build(string name = null)
        {
            var m = new Mesh { name = name ?? "people", indexFormat = P.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            var pos = new Vector3[P.Count]; var nrm = new Vector3[N.Count]; var col = new Color[C.Count];
            for (int i = 0; i < pos.Length; i++) { pos[i] = new Vector3(-P[i].x, P[i].y, P[i].z); nrm[i] = new Vector3(-N[i].x, N[i].y, N[i].z); }
            for (int i = 0; i < col.Length; i++) col[i] = new Color(C[i].r, C[i].g, C[i].b, 1);
            m.vertices = pos; m.normals = nrm; m.colors = col;
            m.SetUVs(0, U); m.SetUVs(2, R);
            if (skinned) m.boneWeights = W.ToArray();
            m.SetTriangles(I, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
