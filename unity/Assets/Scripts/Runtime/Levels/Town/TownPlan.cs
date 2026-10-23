// Ports levels/town-plan.js: derived layout for the 3D town — fences sealing block interiors, barricade extents,
// closure guide posts, walkable blocks, sidewalk centre lines (clipped by closures), traffic lanes and the
// signal cycle. Pure data in JS space (frame as in game/townmap.js).
// Rule the fences keep: every block interior is closed except the pockets listed below, so a closure
// barricade (which spans road + both sidewalks) cannot be walked around through a park, lot or yard.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.Levels
{
    // [x0, z0, x1, z1, kind]; kind: mesh | park | hedge | wall.
    public sealed class FenceLine
    {
        public float x0, z0, x1, z1;
        public string kind;
    }

    // Where someone stands and which way they face (face is a direction, JS space).
    public sealed class GuideSpot
    {
        public float x, z;
        public XZ face;
    }

    // A traffic lane: axis x | z, travel dir +1/-1, centre line c, signal group EW | NS, top speed, stop lines.
    public sealed class Lane
    {
        public string axis, group;
        public int dir;
        public float c, vmax;
        public float[] stops;
    }

    // [group, colour, seconds]
    public sealed class SignalPhase
    {
        public string group, color;
        public float seconds;
    }

    public sealed class SignalState
    {
        public string EW, NS;
        public float left;
    }

    public static class TownPlan
    {
        public const float CURB = 0.12f;

        // Axis-aligned fence lines [x0, z0, x1, z1, kind]. Openings are gaps between lines.
        // kinds: mesh (steel palisade), park (low park fence + hedge), hedge, wall (block wall).
        public static readonly List<FenceLine> FENCES = new List<FenceLine>
        {
            // C-N block: sealed except the mart lot, which opens onto 연무로 (x -36..4).
            F(-42, -10, -36, -10, "hedge"), F(4, -10, 42, -10, "mesh"), F(-42, -52, -42, -10, "hedge"),
            F(-42, -52, 42, -52, "wall"), F(42, -52, 42, -10, "mesh"), F(-36, -24, -36, -10, "wall"), F(4, -24, 4, -10, "wall"),
            // C-S block: park gates north (x -3..3) and west (z 28..33) only; shop alleys and the back lot sealed.
            F(-42, 12, -3, 12, "park"), F(3, 12, 12, 12, "park"), F(-42, 12, -42, 28, "park"), F(-42, 33, -42, 52, "park"),
            F(-42, 52, 12, 52, "park"), F(12, 12, 12, 52, "park"), F(12, 12, 16, 12, "wall"), F(29, 12, 30, 12, "wall"),
            F(42, 22, 42, 52, "mesh"), F(12, 52, 42, 52, "mesh"),
            // Background blocks north of 시장길 and south of 단지길.
            F(-42, -68, 42, -68, "wall"), F(-42, -110, -42, -68, "wall"), F(42, -110, 42, -68, "wall"),
            F(-42, 68, 42, 68, "wall"), F(-42, 68, -42, 110, "wall"), F(42, 68, 42, 110, "wall"),
            // W-N block: centre yard + cafe front open to 연무로, assembly bay open to 문화로, the rest sealed.
            F(-130, -16, -126, -16, "hedge"), F(-110, -16, -104, -16, "hedge"), F(-104, -28, -104, -16, "hedge"),
            F(-74, -27.5f, -58.2f, -27.5f, "hedge"), F(-58.2f, -27.5f, -58.2f, -10, "hedge"),
            F(-74, -56.5f, -74, -50, "wall"), F(-74, -56.5f, -58, -56.5f, "wall"), F(-58, -110, -58, -56.5f, "wall"),
            // W-S block: north walkway (A, C) and east forecourt open; B driveway open to 문화로 south of z 46.
            F(-130, 16.5f, -112, 16.5f, "wall"), F(-130, 46, -112, 46, "wall"), F(-70, 46, -58, 46, "hedge"),
            F(-130, 60, -126, 60, "wall"), F(-62, 60, -58, 60, "wall"), F(-58, 60, -58, 110, "wall"),
            // E-N block: logistics yard, truck gate x 62..72, pedestrian gate x 84..90.
            F(58, -10, 62, -10, "mesh"), F(72, -10, 84, -10, "mesh"), F(90, -10, 130, -10, "mesh"),
            F(58, -110, 58, -10, "mesh"), F(58, -60, 64, -60, "mesh"), F(124, -60, 130, -60, "mesh"),
            // E-S block: apartment complex, main gate on 한빛로 (z 15..25).
            F(58, 10, 130, 10, "mesh"), F(58, 10, 58, 15, "mesh"), F(58, 25, 58, 110, "mesh"),
        };

        // Barricade solids. yeonmuCenter's rects are widened to meet the fence/building lines so the corner
        // squares and the 2 m strips in front of the park and shops cannot be used to step around them.
        public static readonly Dictionary<string, List<RectXZ>> BARRICADES = Barricades();

        // Where the guide of each closure stands (the side a player from 103동 usually arrives on) and faces.
        public static readonly Dictionary<string, GuideSpot> CLOSURE_GUIDES = new Dictionary<string, GuideSpot>
        {
            ["yeonmuCenter"] = new GuideSpot { x = 30.4f, z = 8.4f, face = new XZ(1, 0) },
            ["danji"] = new GuideSpot { x = 2.4f, z = 54, face = new XZ(1, 0) },
            ["sijang"] = new GuideSpot { x = 2.4f, z = -54, face = new XZ(1, 0) },
            ["munhwaSouth"] = new GuideSpot { x = -56, z = 47.4f, face = new XZ(0, 1) },
            ["munhwaNorth"] = new GuideSpot { x = -56, z = -21.4f, face = new XZ(0, -1) },
        };

        // Walkable rects at curb height (everything between the roads). Roads are y 0.
        public static readonly List<RectXZ> BLOCKS = new List<RectXZ>
        {
            new RectXZ(-130, -54, -110, -6), new RectXZ(-130, -54, 6, 110),
            new RectXZ(54, 130, -110, -6), new RectXZ(54, 130, 6, 110),
            new RectXZ(-46, 46, -110, -64), new RectXZ(-46, 46, -56, -6),
            new RectXZ(-46, 46, 6, 56), new RectXZ(-46, 46, 64, 110),
        };

        // Sidewalk centre lines (2 m from the curb) the crowd strolls on. Closures cut them (see ClipPaths).
        public static readonly List<List<XZ>> SIDEWALKS = new List<List<XZ>>
        {
            P(-128, -8, -56, -8), P(-44, -8, 44, -8), P(56, -8, 128, -8),
            P(-128, 8, -56, 8), P(-44, 8, 44, 8), P(56, 8, 128, 8),
            P(-56, -108, -56, -8), P(-56, 8, -56, 108), P(-44, -54, -44, -8), P(-44, 8, -44, 54),
            P(44, -54, 44, -8), P(44, 8, 44, 54), P(56, -108, 56, -8), P(56, 8, 56, 108),
            P(-44, -54, 44, -54), P(-44, -66, 44, -66), P(-44, 54, 44, 54), P(-44, 66, 44, 66),
            // crossings (zebra) and inner walks
            P(0, -8, 0, 8), P(-104, -8, -104, 8), P(-56, -14, -44, -14), P(-56, 14, -44, 14),
            P(44, -14, 56, -14), P(44, 14, 56, 14), P(-60, -8, -60, 8), P(60, -8, 60, 8),
            P(0, 8, 0, 11, -15, 22, -15, 40, -30, 44, -38, 30.5f, -44, 30.5f),
            P(56, 20, 62, 20, 70, 27, 100, 27), P(-66, -11, -80, -14, -96, -18),
        };

        // Traffic lanes (Korea drives on the right). s runs in the travel direction; stops are world
        // coordinates along the axis where cars wait at red. group: which signal phase lets them go.
        public static readonly List<Lane> LANES = new List<Lane>
        {
            new Lane { axis = "x", dir = 1, c = 1.6f, group = "EW", vmax = 12, stops = new[] { -107.5f, -63.5f, -3.5f, 44.5f } },
            new Lane { axis = "x", dir = 1, c = 4.4f, group = "EW", vmax = 10.5f, stops = new[] { -107.5f, -63.5f, -3.5f, 44.5f } },
            new Lane { axis = "x", dir = -1, c = -1.6f, group = "EW", vmax = 12, stops = new[] { 63.5f, 3.5f, -44.5f, -100.5f } },
            new Lane { axis = "x", dir = -1, c = -4.4f, group = "EW", vmax = 10.5f, stops = new[] { 63.5f, 3.5f, -44.5f, -100.5f } },
            new Lane { axis = "z", dir = -1, c = -48, group = "NS", vmax = 9, stops = new[] { 17.5f } },
            new Lane { axis = "z", dir = 1, c = -52, group = "NS", vmax = 9, stops = new[] { -17.5f } },
            new Lane { axis = "z", dir = -1, c = 52, group = "NS", vmax = 9, stops = new[] { 17.5f } },
            new Lane { axis = "z", dir = 1, c = 48, group = "NS", vmax = 9, stops = new[] { -17.5f } },
        };
        public const float LANE_SPAN = 340; // lanes run -340..340 so cars wrap far out in the city ring

        // Signal cycle (seconds): EW green, EW amber, all red, NS green, NS amber, all red.
        public static readonly List<SignalPhase> CYCLE = new List<SignalPhase>
        {
            S("EW", "green", 20), S("EW", "amber", 3), S("ALL", "red", 1.5f), S("NS", "green", 13), S("NS", "amber", 3), S("ALL", "red", 1.5f),
        };

        static FenceLine F(float x0, float z0, float x1, float z1, string kind) => new FenceLine { x0 = x0, z0 = z0, x1 = x1, z1 = z1, kind = kind };
        static SignalPhase S(string group, string color, float seconds) => new SignalPhase { group = group, color = color, seconds = seconds };
        static List<XZ> P(params float[] xz)
        {
            var o = new List<XZ>();
            for (int i = 0; i + 1 < xz.Length; i += 2) o.Add(new XZ(xz[i], xz[i + 1]));
            return o;
        }

        static Dictionary<string, List<RectXZ>> Barricades()
        {
            var o = new Dictionary<string, List<RectXZ>>();
            foreach (var kv in TownMap.CLOSURES)
            {
                var list = new List<RectXZ>();
                foreach (var b in kv.Value.barricades) list.Add(new RectXZ((float)b.x0, (float)b.x1, (float)b.z0, (float)b.z1));
                o[kv.Key] = list;
            }
            o["yeonmuCenter"] = new List<RectXZ> { new RectXZ(27, 29, -10, 12), new RectXZ(-46, -42, -10, 12) };
            return o;
        }

        // Remove the parts of axis-aligned polylines that run through active barricades (1 m margin).
        public static List<List<XZ>> ClipPaths(IList<List<XZ>> paths, IList<RectXZ> rects)
        {
            var output = new List<List<XZ>>();
            void Flush(List<XZ> c) { if (c != null && c.Count > 1) output.Add(c); }
            foreach (var path in paths)
            {
                List<XZ> cur = null;
                for (int i = 1; i < path.Count; i++)
                {
                    XZ a = path[i - 1], b = path[i];
                    var pieces = new List<(float u, float v)> { (0f, 1f) };
                    foreach (var r in rects)
                    {
                        var t = SegRect(a, b, r.x0 - 1, r.x1 + 1, r.z0 - 1, r.z1 + 1);
                        if (t == null) continue;
                        var next = new List<(float u, float v)>();
                        foreach (var (u, v) in pieces)
                        {
                            float q0 = Math.Min(v, t.Value.t0), p1 = Math.Max(u, t.Value.t1);
                            if (q0 - u > 1e-3f) next.Add((u, q0));
                            if (v - p1 > 1e-3f) next.Add((p1, v));
                        }
                        pieces = next;
                    }
                    XZ At(float s) => new XZ(a.x + (b.x - a.x) * s, a.z + (b.z - a.z) * s);
                    if (pieces.Count == 0) { Flush(cur); cur = null; }
                    foreach (var (u, v) in pieces)
                    {
                        if (u > 0 || cur == null) { Flush(cur); cur = new List<XZ> { At(u) }; }
                        cur.Add(At(v));
                        if (v < 1) { Flush(cur); cur = null; }
                    }
                }
                Flush(cur);
            }
            return output.FindAll(p => Hypot(p[p.Count - 1].x - p[0].x, p[p.Count - 1].z - p[0].z) > 3);
        }

        // Parametric [t0, t1] where segment a-b is inside rect r, or null (slab method).
        static (float t0, float t1)? SegRect(XZ a, XZ b, float x0, float x1, float z0, float z1)
        {
            float t0 = 0, t1 = 1;
            for (int k = 0; k < 2; k++)
            {
                float p = k == 0 ? a.x : a.z, d = k == 0 ? b.x - a.x : b.z - a.z, lo = k == 0 ? x0 : z0, hi = k == 0 ? x1 : z1;
                if (Math.Abs(d) < 1e-9f) { if (p < lo || p > hi) return null; continue; }
                float u = (lo - p) / d, v = (hi - p) / d; if (u > v) { var s = u; u = v; v = s; }
                t0 = Math.Max(t0, u); t1 = Math.Min(t1, v); if (t0 >= t1) return null;
            }
            return (t0, t1);
        }

        static float Hypot(float x, float z) => (float)Math.Sqrt(x * x + z * z);

        public static SignalState SignalAt(float t)
        {
            float total = 0;
            foreach (var c in CYCLE) total += c.seconds;
            float u = ((t % total) + total) % total;
            foreach (var c in CYCLE)
            {
                if (u < c.seconds) return new SignalState { EW = c.group == "EW" ? c.color : "red", NS = c.group == "NS" ? c.color : "red", left = c.seconds - u };
                u -= c.seconds;
            }
            return new SignalState { EW = "red", NS = "red", left = 0 };
        }
    }
}
