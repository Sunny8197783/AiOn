// kit.js light groups and merge, plus the three.js -> Unity light conversion (the only place it happens).
// three.js (physically based) divides diffuse and specular by pi; URP does not, so a three.js light of
// intensity I is a URP light of I / pi. three.js distance 0 means unbounded inverse-square falloff; URP needs a
// range, so it gets the distance where the light has faded to almost nothing.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gyeongbo.Engine
{
    public sealed partial class Kit
    {
        // ---- light groups: 'street' | 'windows' | 'shops' | 'indoor' | 'signs' | 'emergency' | any name ----
        // Lights are never hidden, only dimmed to 0: a changing light count is expensive.
        sealed class LitGroup { public readonly List<LitItem> items = new List<LitItem>(); public bool on = true; public float scale = 1; }
        sealed class LitItem { public UnityEngine.Object obj; public float @base; public bool isLight, off; }
        readonly Dictionary<string, LitGroup> groups = new Dictionary<string, LitGroup>();
        readonly Dictionary<UnityEngine.Object, (LitGroup g, LitItem it)> itemOf = new Dictionary<UnityEngine.Object, (LitGroup, LitItem)>();

        // unlitColor (a backlit panel such as a vending machine) also darkens its colour when its power is off.
        static void Put(LitGroup G, LitItem it)
        {
            float v = G.on && !it.off ? it.@base * G.scale : 0;
            if (it.isLight) { var l = (Light)it.obj; if (l != null) l.intensity = v; return; }
            var m = (Material)it.obj; if (m == null) return;
            Gyeongbo.Engine.Mat.SetEmissiveIntensity(m, v);
            var dim = Gyeongbo.Engine.Mat.InfoOf(m).unlitColor;
            if (dim.HasValue) { float k = dim.Value + (1 - dim.Value) * Mathf.Min(1, v / (it.@base != 0 ? it.@base : 1)); Gyeongbo.Engine.Mat.SetLinearColor(m, new Color(k, k, k)); }
        }

        LitItem Register(string group, UnityEngine.Object obj, bool isLight, float @base)
        {
            if (!groups.TryGetValue(group, out var G)) groups[group] = G = new LitGroup();
            var it = new LitItem { obj = obj, @base = @base, isLight = isLight };
            G.items.Add(it); itemOf[obj] = (G, it); Put(G, it);
            return it;
        }

        // on: the emissive intensity when the group is on (default: the material's own).
        public Material Lit(string group, Material m, float? on = null)
        {
            Register(group, m, false, on ?? Gyeongbo.Engine.Mat.GetEmissiveIntensity(m));
            return m;
        }

        // on: the three.js intensity when the group is on (default: the light's current intensity).
        public Light Lit(string group, Light l, float? on = null)
        {
            Register(group, l, true, on.HasValue ? ToUnity(on.Value) : l.intensity);
            return l;
        }

        public void SetLit(string group, bool on, float scale = 1)
        {
            if (!groups.TryGetValue(group, out var G)) return;
            G.on = on; G.scale = scale;
            foreach (var it in G.items) Put(G, it);
        }

        // Per-object switch on top of its group (a lamp behind a shutter); obj is the Material or Light given to Lit.
        public void GateLit(UnityEngine.Object obj, bool on)
        {
            if (obj != null && itemOf.TryGetValue(obj, out var r)) { r.it.off = !on; Put(r.g, r.it); }
        }

        public bool IsLit(string group) => groups.TryGetValue(group, out var G) && G.on;

        // ---- lights (three.js units -> Unity; the conversion lives here only) ----
        internal static float ToUnity(float threeIntensity) => threeIntensity / Mathf.PI;
        static float RangeFor(float unityIntensity) => Mathf.Clamp(Mathf.Sqrt(Mathf.Max(unityIntensity, 0) / 0.002f), 5, 300);

        // ponytail: URP falls off with the inverse square only; three.js decay other than 2 is ignored.
        public Light PointLight(Color color, float intensity, float distance = 0, float decay = 2)
        {
            var l = new GameObject("PointLight").AddComponent<Light>();
            l.type = LightType.Point; l.color = color; l.intensity = ToUnity(intensity); l.shadows = LightShadows.None;
            l.range = distance > 0 ? distance : RangeFor(l.intensity);
            return l;
        }

        // angle: the cone's half angle (radians); penumbra 0..1 of it is soft. Points along its Transform's +z
        // (aim it with J.LookAt).
        public Light SpotLight(Color color, float intensity, float distance, float angle, float penumbra = 0, float decay = 2)
        {
            var l = new GameObject("SpotLight").AddComponent<Light>();
            SetupSpot(l, color, intensity, distance, angle, penumbra);
            return l;
        }

        internal static void SetupSpot(Light l, Color color, float intensity, float distance, float angle, float penumbra)
        {
            l.type = LightType.Spot; l.color = color; l.intensity = ToUnity(intensity); l.shadows = LightShadows.None;
            l.spotAngle = Mathf.Clamp(2 * angle * Mathf.Rad2Deg, 1, 179);
            l.innerSpotAngle = l.spotAngle * Mathf.Clamp01(1 - penumbra);
            l.range = distance > 0 ? distance : RangeFor(l.intensity);
        }

        // ---- merge ----
        static readonly ConditionalWeakTable<GameObject, object> kept = new ConditionalWeakTable<GameObject, object>();
        static readonly object KEEP = new object();
        // Excludes an object from Merge (three.js userData.keep = true).
        public static void Keep(GameObject o) { if (o != null && !kept.TryGetValue(o, out _)) kept.Add(o, KEEP); }
        internal static bool IsKept(GameObject o) => kept.TryGetValue(o, out _);

        // Collapse static meshes under root into one mesh per material (fewer draw calls). Skips Keep objects,
        // skinned meshes, other renderers and multi-material meshes. Returns root.
        public GameObject Merge(GameObject root)
        {
            var inv = root.transform.worldToLocalMatrix;
            var byMat = new Dictionary<string, (Material mat, bool cast, bool receive, List<CombineInstance> parts, long verts)>();
            var order = new List<string>();
            var victims = new List<MeshFilter>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                if (IsKept(go)) continue;
                var mr = go.GetComponent<MeshRenderer>();
                var mesh = mf.sharedMesh;
                if (mr == null || mesh == null || !mesh.isReadable || mr.sharedMaterials.Length != 1) continue;
                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0) || !mesh.HasVertexAttribute(VertexAttribute.Normal)) continue;
                var mat = mr.sharedMaterial;
                bool cast = mr.shadowCastingMode != ShadowCastingMode.Off, receive = mr.receiveShadows;
                string key = (mat != null ? IdOf(mat) : "0") + (cast ? "c" : "") + (receive ? "r" : "");
                if (!byMat.TryGetValue(key, out var e)) { e = (mat, cast, receive, new List<CombineInstance>(), 0); order.Add(key); }
                var m = inv * mf.transform.localToWorldMatrix;
                for (int s = 0; s < mesh.subMeshCount; s++) e.parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = m });
                e.verts += mesh.vertexCount;
                byMat[key] = e;
                victims.Add(mf);
            }
            foreach (var mf in victims)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (Application.isPlaying) { UnityEngine.Object.Destroy(mr); UnityEngine.Object.Destroy(mf); }
                else { UnityEngine.Object.DestroyImmediate(mr); UnityEngine.Object.DestroyImmediate(mf); }
            }
            foreach (var key in order)
            {
                var e = byMat[key];
                var merged = new Mesh { name = "merged", indexFormat = e.verts > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                merged.CombineMeshes(e.parts.ToArray(), true, true);
                var go = Geo.Obj(merged, e.mat, "merged", e.cast, e.receive);
                go.transform.SetParent(root.transform, false);
            }
            return root;
        }
    }
}
