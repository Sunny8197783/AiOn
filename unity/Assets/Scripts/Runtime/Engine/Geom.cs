// Ports engine/geom.js: pure geometry for the player rig and thrown things, in JS space — walk-zone
// ground height, circle-vs-AABB push-out, segment-vs-AABB entry. No Unity physics.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public struct Ground
    {
        public float y, slope;
    }

    public static class Geom
    {
        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;

        // Highest walk surface within `below` m below / 0.45 m above feetY at (x, z); any = ignore the band.
        public static Ground? GroundAt(IList<WalkZone> walk, float x, float z, float feetY, bool any = false, float below = 0.6f)
        {
            float? best = null; float slope = 0;
            if (walk == null) return null;
            foreach (var w in walk)
            {
                if (w == null || x < w.x0 - 0.02f || x > w.x1 + 0.02f || z < w.z0 - 0.02f || z > w.z1 + 0.02f) continue;
                float y = w.y, s = 0;
                if (w.ramp != null)
                {
                    var r = w.ramp; float span = r.axis == "x" ? w.x1 - w.x0 : w.z1 - w.z0;
                    float k = Clamp((r.axis == "x" ? x - w.x0 : z - w.z0) / (span != 0 ? span : 1), 0, 1);
                    y = r.y0 + (r.y1 - r.y0) * k; s = Math.Abs(r.y1 - r.y0) / (span != 0 ? span : 1);
                }
                if (!any && (y > feetY + 0.45f || y < feetY - below)) continue;
                if (best == null || y > best.Value) { best = y; slope = s; }
            }
            return best == null ? (Ground?)null : new Ground { y = best.Value, slope = slope };
        }

        // Tops of solids as flat walk zones.
        public static List<WalkZone> TopsOf(IEnumerable<Solid> solids)
        {
            var o = new List<WalkZone>();
            foreach (var s in solids) o.Add(new WalkZone(s.x0, s.x1, s.z0, s.z1, s.y1));
            return o;
        }

        static readonly float[,] NEAR = { { 0, 0 }, { 0.25f, 0 }, { -0.25f, 0 }, { 0, 0.25f }, { 0, -0.25f } };
        // Ground with a little footprint: a seam narrower than the body is not a hole.
        public static Ground? GroundNear(IList<WalkZone> walk, float x, float z, float feetY)
        {
            for (int i = 0; i < 5; i++) { var g = GroundAt(walk, x + NEAR[i, 0], z + NEAR[i, 1], feetY); if (g != null) return g; }
            return null;
        }

        // Push a circle out of every solid overlapping [feetY, feetY + h]; returns the new (x, z).
        public static (float x, float z) Collide(float px, float pz, float feetY, IList<Solid> solids, float r = 0.32f, float h = 1.72f)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                bool moved = false;
                foreach (var s in solids)
                {
                    if (s.off || s.y1 <= feetY + 0.05f || s.y0 >= feetY + h) continue;
                    float cx = Clamp(px, s.x0, s.x1), cz = Clamp(pz, s.z0, s.z1), dx = px - cx, dz = pz - cz, d2 = dx * dx + dz * dz;
                    if (d2 >= r * r) continue;
                    if (d2 > 1e-10f) { float d = Mathf.Sqrt(d2); px += dx / d * (r - d); pz += dz / d * (r - d); }
                    else
                    { // centre inside the box: leave by the shallowest side
                        float l = px - s.x0, rr = s.x1 - px, f = pz - s.z0, b = s.z1 - pz, m = Math.Min(Math.Min(l, rr), Math.Min(f, b));
                        if (m == l) px = s.x0 - r; else if (m == rr) px = s.x1 + r; else if (m == f) pz = s.z0 - r; else pz = s.z1 + r;
                    }
                    moved = true;
                }
                if (!moved) break;
            }
            return (px, pz);
        }

        // Segment o -> o + d (t in 0..1) against the solid grown by pad: entry t, or +Infinity (also when o is inside).
        public static float SegBox(Vector3 o, Vector3 d, Solid s, float pad)
        {
            float t0 = 0, t1 = 1;
            float[] lo = { s.x0 - pad, s.y0 - pad, s.z0 - pad }, hi = { s.x1 + pad, s.y1 + pad, s.z1 + pad }, O = { o.x, o.y, o.z }, D = { d.x, d.y, d.z };
            if (O[0] > lo[0] && O[0] < hi[0] && O[1] > lo[1] && O[1] < hi[1] && O[2] > lo[2] && O[2] < hi[2]) return float.PositiveInfinity;
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(D[i]) < 1e-9f) { if (O[i] < lo[i] || O[i] > hi[i]) return float.PositiveInfinity; continue; }
                float a = (lo[i] - O[i]) / D[i], b = (hi[i] - O[i]) / D[i];
                if (a > b) { var tmp = a; a = b; b = tmp; }
                t0 = Math.Max(t0, a); t1 = Math.Min(t1, b);
                if (t0 > t1) return float.PositiveInfinity;
            }
            return t0;
        }
    }
}
