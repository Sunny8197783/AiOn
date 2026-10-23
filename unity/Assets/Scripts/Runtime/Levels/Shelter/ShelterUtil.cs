// Small helpers the shelter sub-builders share: JS-space matrices (three.js Matrix4.compose), InstancedMesh
// stand-ins (one static mesh per material, never merged again) and access to a kit mesh's material.
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gyeongbo.Levels
{
    static class ShelterUtil
    {
        // three.js Euler (XYZ order) -> quaternion, both in JS space (Rx * Ry * Rz).
        public static Quaternion JsQuat(float rx, float ry, float rz) =>
            Quaternion.AngleAxis(rx * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(ry * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(rz * Mathf.Rad2Deg, Vector3.forward);

        // new Matrix4().compose(V(x, y, z), Quaternion(Euler(rx, ry, rz)), V(sx, sy, sz)) in JS space.
        public static Matrix4x4 T4(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float sx = 1, float sy = 1, float sz = 1) =>
            Matrix4x4.TRS(new Vector3(x, y, z), JsQuat(rx, ry, rz), new Vector3(sx, sy, sz));

        // An InstancedMesh: copies of geo at the JS-space matrices, baked into one static mesh. Tagged keep so
        // kit.Merge leaves it alone (kit.merge skips instanced meshes in JS).
        // ponytail: baked copies instead of GPU instancing; fine for these static props, use Graphics.RenderMeshInstanced if counts grow.
        public static GameObject Inst(GameObject parent, GeoData geo, Material mat, IList<Matrix4x4> mats, bool cast = false, bool receive = false)
        {
            GameObject o;
            if (mats.Count == 0) o = J.Group("inst");
            else
            {
                var list = new List<GeoData>(mats.Count);
                foreach (var m in mats) list.Add(geo.Clone().ApplyMatrix(m));
                o = Geo.Obj(Geo.Merge(list), mat, "inst", cast, receive);
            }
            Kit.Keep(o);
            parent.Add(o);
            return o;
        }

        // The material of a kit mesh (sign, box): JS mesh.material.
        public static Material MatOf(GameObject o)
        {
            var r = o.GetComponent<Renderer>();
            if (r == null) r = o.GetComponentInChildren<Renderer>();
            return r != null ? r.sharedMaterial : null;
        }

        // mesh.castShadow = false
        public static void NoShadow(GameObject o)
        {
            foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // JS Math.round (half up), not banker's rounding.
        public static int Round(float v) => Mathf.FloorToInt(v + 0.5f);

        public static float Hypot(float a, float b) => Mathf.Sqrt(a * a + b * b);
    }
}
