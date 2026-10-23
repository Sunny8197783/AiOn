// The JS -> Unity boundary (PORTING.md "Coordinates"). JS/three.js is right-handed, Unity left-handed:
// the port mirrors X. Gameplay data stays in JS numbers; only Transforms and meshes see Unity space.
// Also the three.js-style scene helpers every port needs (group.add, hex colours, CSS colours).
using System;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public static class J
    {
        // ---- points and directions ----
        public static Vector3 V(float x, float y, float z) => new Vector3(-x, y, z);
        public static Vector3 V(Vector3 js) => new Vector3(-js.x, js.y, js.z);
        public static Vector3 V(double x, double y, double z) => new Vector3((float)-x, (float)y, (float)z);
        // Unity -> JS (x, y, z). The mirror is its own inverse.
        public static Vector3 FromUnity(Vector3 u) => new Vector3(-u.x, u.y, u.z);
        // 2D map data (game/townmap.js x/z, JS numbers) -> Unity ground point.
        public static Vector3 Map(double x, double z) => new Vector3((float)-x, 0f, (float)z);

        // ---- rotations ----
        // JS rotation.y = a (radians) -> Unity rotation.
        public static Quaternion Yaw(float a) => Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
        // Unity rotation -> JS yaw (radians, -pi..pi).
        public static float YawOf(Quaternion q) => Wrap(-q.eulerAngles.y * Mathf.Deg2Rad);
        // JS rotation.set(x, y, z) (XYZ order) -> Unity rotation.
        public static Quaternion Euler(float x, float y, float z) =>
            Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(-y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(-z * Mathf.Rad2Deg, Vector3.forward);
        public static float Wrap(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));

        // ---- transforms (JS object.position / rotation are local to the parent) ----
        public static void SetPos(GameObject o, float x, float y, float z) => o.transform.localPosition = V(x, y, z);
        public static void SetPos(GameObject o, Vector3 js) => o.transform.localPosition = V(js);
        public static Vector3 Pos(GameObject o) => FromUnity(o.transform.localPosition);
        public static void SetYaw(GameObject o, float a) => o.transform.localRotation = Yaw(a);
        public static float GetYaw(GameObject o) => YawOf(o.transform.localRotation);
        public static void SetRot(GameObject o, float x, float y, float z) => o.transform.localRotation = Euler(x, y, z);
        public static void LookAt(Transform t, float x, float y, float z) => t.LookAt(V(x, y, z));
        public static Vector3 WorldPos(GameObject o) => FromUnity(o.transform.position);

        // ---- scene graph (three.js Group / add / removeFromParent) ----
        public static GameObject Group(string name = "group", GameObject parent = null)
        {
            var g = new GameObject(name);
            if (parent != null) g.transform.SetParent(parent.transform, false);
            return g;
        }

        // parent.add(a, b, ...): keeps each child's local transform.
        public static GameObject Add(this GameObject parent, params GameObject[] kids)
        {
            foreach (var k in kids) if (k != null) k.transform.SetParent(parent.transform, false);
            return parent;
        }

        public static GameObject Add(this GameObject parent, params Component[] kids)
        {
            foreach (var k in kids) if (k != null) k.transform.SetParent(parent.transform, false);
            return parent;
        }

        public static void Remove(GameObject o) { if (o != null) o.transform.SetParent(null, true); }

        // ---- colours (three.js Color from 0xRRGGBB or CSS strings; components are sRGB like Unity's Color) ----
        public static Color Hex(int hex, float k = 1f) =>
            new Color(((hex >> 16) & 255) / 255f * k, ((hex >> 8) & 255) / 255f * k, (hex & 255) / 255f * k, 1f);
        public static Color Css(string css) => PaintColor.Parse(css);

        // ---- meshes ported from JS geometry: mirror X of every vertex and normal, keep the triangle order and UVs ----
        public static Mesh MirrorMesh(Vector3[] jsPositions, Vector3[] jsNormals, Vector2[] uvs, int[] triangles, Color[] colors = null)
        {
            var p = new Vector3[jsPositions.Length];
            for (int i = 0; i < p.Length; i++) p[i] = V(jsPositions[i]);
            var m = new Mesh { indexFormat = p.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.vertices = p;
            if (jsNormals != null) { var n = new Vector3[jsNormals.Length]; for (int i = 0; i < n.Length; i++) n[i] = V(jsNormals[i]); m.normals = n; }
            if (uvs != null) m.uv = uvs;
            if (colors != null) m.colors = colors;
            m.triangles = triangles;
            if (jsNormals == null) m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
