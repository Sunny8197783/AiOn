// Path helpers, the per-member agent record and the viewer hook for Crowd (engine/crowd.js). JS space.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.People
{
    // A prepared polyline: points, cumulative arc length, total length.
    sealed class CrowdPathP
    {
        public List<XZ> p; public List<float> cum; public float len;

        static bool Ok(XZ q) => !float.IsNaN(q.x) && !float.IsInfinity(q.x) && !float.IsNaN(q.z) && !float.IsInfinity(q.z);

        public static CrowdPathP Prep(List<XZ> pts)
        {
            var p = new List<XZ>(); if (pts != null) foreach (var q in pts) if (Ok(q)) p.Add(q);
            if (p.Count < 2) return null;
            var cum = new List<float> { 0 };
            for (int i = 1; i < p.Count; i++) cum.Add(cum[i - 1] + Hyp(p[i].x - p[i - 1].x, p[i].z - p[i - 1].z));
            return cum[cum.Count - 1] > 0.5f ? new CrowdPathP { p = p, cum = cum, len = cum[cum.Count - 1] } : null;
        }

        internal static float Hyp(float x, float z) => (float)Math.Sqrt(x * x + z * z);
        static float Clamp(float x, float a, float b) => Math.Min(b, Math.Max(a, x));

        // Point and unit tangent at arc length s.
        public void At(float s, ref CrowdTmp o)
        {
            s = Clamp(s, 0, len);
            int i = 1;
            while (i < p.Count - 1 && cum[i] < s) i++;
            XZ a = p[i - 1], b = p[i]; float L = cum[i] - cum[i - 1]; if (L == 0) L = 1; float t = (s - cum[i - 1]) / L;
            o.x = a.x + (b.x - a.x) * t; o.z = a.z + (b.z - a.z) * t; o.tx = (b.x - a.x) / L; o.tz = (b.z - a.z) / L;
        }

        // Nearest path and arc length to (x, z).
        public static (int pi, float s)? Project(List<CrowdPathP> paths, float x, float z)
        {
            (int, float)? best = null; float bd = float.PositiveInfinity;
            for (int pi = 0; pi < paths.Count; pi++)
            {
                var P = paths[pi];
                for (int i = 1; i < P.p.Count; i++)
                {
                    XZ a = P.p[i - 1], b = P.p[i]; float dx = b.x - a.x, dz = b.z - a.z, L2 = dx * dx + dz * dz; if (L2 == 0) L2 = 1;
                    float t = Clamp(((x - a.x) * dx + (z - a.z) * dz) / L2, 0, 1), d = (a.x + dx * t - x) * (a.x + dx * t - x) + (a.z + dz * t - z) * (a.z + dz * t - z);
                    if (d < bd) { bd = d; best = (pi, P.cum[i - 1] + (float)Math.Sqrt(L2) * t); }
                }
            }
            return best;
        }
    }

    struct CrowdTmp { public float x, z, tx, tz; }

    // One crowd member's state (the JS agent object).
    sealed class CrowdAgent
    {
        public IPerson p; public int i;
        public float x, z, vx, vz, yaw; public float? face;
        public bool on, placed;
        public string state = "stand";
        public int path = -1; public float s, dir = 1, lane, speed = 1.2f;
        public XZ? goal;
        public float timer, stopT;
        public string anim = "", standAnim = "idle", moveAnim = "walk";
        public bool moving, sh = true;
        public float acc, dy, jit, slow;
        public HashSet<string> props = new HashSet<string>();
        public float hurtT, fleeT, downT, fx, fz;
    }

    // mesh.onBeforeRender = hook: remembers the camera that last rendered a crowd member.
    // ponytail: OnWillRenderObject + Camera.current, which URP may leave null; levels pass FrameCtx.camera anyway.
    sealed class ViewerHook : MonoBehaviour
    {
        public Action<Camera> onRender;
        void OnWillRenderObject() { var cam = Camera.current; if (cam != null) onRender?.Invoke(cam); }
    }
}
