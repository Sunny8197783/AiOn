// Ports engine/people-geo.js: the per-vertex PBR material (pattern, roughness, sheen as vertex data) and the
// mesh builder that lofts rings into one indexed (optionally skinned) geometry.
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.People
{
    // Per-vertex surface: [pattern, roughness, ...] (people-geo.js PBR entries).
    public sealed class Pbr
    {
        public float[] v;
    }

    // builder(skinned) -> { loft, pipe, rbox, blob, build, vertexCount }
    public sealed class MeshBuilder
    {
        public MeshBuilder(bool skinned = false) => throw new NotImplementedException("port: engine/people-geo.js:builder");
        // Rings [{ y, rx, rz, x, z }] lofted into a tube; options carry colour, pbr, skin weights, seg, caps.
        public void Loft(IList<BodyRow> rings, LoftOpts o) => throw new NotImplementedException("port: engine/people-geo.js:loft");
        public void Pipe(IList<Vector3> pts, float r, LoftOpts o) => throw new NotImplementedException("port: engine/people-geo.js:pipe");
        public void RBox(float w, float h, float d, float radius, LoftOpts o) => throw new NotImplementedException("port: engine/people-geo.js:rbox");
        public void Blob(IList<BodyRow> rows, LoftOpts o) => throw new NotImplementedException("port: engine/people-geo.js:blob");
        public GeoData Build() => throw new NotImplementedException("port: engine/people-geo.js:build");
        public int VertexCount => throw new NotImplementedException("port: engine/people-geo.js:vertexCount");
    }

    public sealed class LoftOpts
    {
        public Color c = Color.white;
        public Pbr pbr;
        public int seg = 16;
        public bool cap;
        public int bone;                     // single-bone skin; skin overrides it
        public float[] skin;                 // [b0, w0, b1, w1, ...] up to 4 bones
        public Matrix4x4? m;
    }

    public static class PeopleGeo
    {
        public static readonly Dictionary<string, int> PAT = new Dictionary<string, int> { ["none"] = 0, ["fabric"] = 1, ["check"] = 2, ["knit"] = 3, ["denim"] = 4, ["hair"] = 5, ["quilt"] = 6, ["skin"] = 7, ["grid"] = 8 };
        public static readonly Dictionary<string, Pbr> PBR = new Dictionary<string, Pbr>();
        public static Color Col(string hex, float k = 1) => J.Css(hex) * new Color(k, k, k, 1);
        public static Material VpbrMaterial() => throw new NotImplementedException("port: engine/people-geo.js:vpbrMaterial");
        public static BodyRow RingAt(IList<BodyRow> rows, float y) => throw new NotImplementedException("port: engine/people-geo.js:ringAt");
        public static List<BodyRow> Zoned(IList<BodyRow> rows, IList<float[]> zones) => throw new NotImplementedException("port: engine/people-geo.js:zoned");
    }
}
