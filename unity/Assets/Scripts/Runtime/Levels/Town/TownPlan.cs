// Ports levels/town-plan.js: derived layout for the 3D town — fences sealing block interiors, barricade extents,
// closure guide posts, walkable blocks, sidewalk centre lines (clipped by closures), traffic lanes and the
// signal cycle. Pure data in JS space (frame as in game/townmap.js).
using System;
using System.Collections.Generic;
using Gyeongbo.Core;

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
        // Data tables: filled by the town porter from levels/town-plan.js.
        public static readonly List<FenceLine> FENCES = new List<FenceLine>();
        // Closure id -> barricade solids (rects the player cannot cross); yeonmuCenter widened.
        public static readonly Dictionary<string, List<RectXZ>> BARRICADES = new Dictionary<string, List<RectXZ>>();
        public static readonly Dictionary<string, GuideSpot> CLOSURE_GUIDES = new Dictionary<string, GuideSpot>();
        public static readonly List<RectXZ> BLOCKS = new List<RectXZ>();
        public static readonly List<List<XZ>> SIDEWALKS = new List<List<XZ>>();
        public static readonly List<Lane> LANES = new List<Lane>();
        public const float LANE_SPAN = 340;
        public static readonly List<SignalPhase> CYCLE = new List<SignalPhase>();

        // Remove the parts of axis-aligned polylines that run through the rects (1 m margin); drops stubs <= 3 m.
        public static List<List<XZ>> ClipPaths(IList<List<XZ>> paths, IList<RectXZ> rects) => throw new NotImplementedException("port: levels/town-plan.js:clipPaths");
        public static SignalState SignalAt(float t) => throw new NotImplementedException("port: levels/town-plan.js:signalAt");
    }
}
