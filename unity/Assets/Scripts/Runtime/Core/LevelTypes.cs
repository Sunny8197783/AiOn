// SHARED. The data a level hands the engine and the story (levels/*.js Level contract, engine/geom.js shapes).
// All coordinates here are JS space (x = east, z = south, yaw 0 faces -z, positive yaw turns left); convert
// with J.V / J.Yaw only when touching a Transform. Field names are the JS keys so ports copy numbers verbatim.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Core
{
    // { x, z } point (paths, targets, trail).
    [Serializable]
    public struct XZ
    {
        public float x, z;
        public XZ(float x, float z) { this.x = x; this.z = z; }
    }

    // Axis-aligned rect { x0, x1, z0, z1 } (areas, holes, barricades).
    public class RectXZ
    {
        public float x0, x1, z0, z1;
        public RectXZ() { }
        public RectXZ(float x0, float x1, float z0, float z1) { this.x0 = x0; this.x1 = x1; this.z0 = z0; this.z1 = z1; }
        public bool Contains(float x, float z) => x >= x0 && x <= x1 && z >= z0 && z <= z1;
    }

    // A walk zone: flat at y, or a ramp from y0 to y1 along axis 'x' | 'z' (stairs are ramps).
    public sealed class WalkZone : RectXZ
    {
        public float y;
        public Ramp ramp;
        public WalkZone() { }
        public WalkZone(float x0, float x1, float z0, float z1, float y = 0) : base(x0, x1, z0, z1) { this.y = y; }
    }

    public sealed class Ramp
    {
        public string axis;   // "x" | "z"
        public float y0, y1;
        public Ramp() { }
        public Ramp(string axis, float y0, float y1) { this.axis = axis; this.y0 = y0; this.y1 = y1; }
    }

    // A solid box the player capsule slides along; off = ignored (an open door, a lifted barrier).
    public sealed class Solid : RectXZ
    {
        public float y0, y1;
        public bool off;
        public string id;
        public Solid() { }
        public Solid(float x0, float x1, float z0, float z1, float y0, float y1, string id = null) : base(x0, x1, z0, z1) { this.y0 = y0; this.y1 = y1; this.id = id; }
    }

    // An interaction point (E). r: reach in metres; enabled false hides it from E and doors.
    // desc: what E shows when no chapter handles the point (story/world.js); gets the run context.
    public sealed class Point
    {
        public string id, label, kind, to;
        public float x, z;
        public float? y;
        public float r = 1.6f;
        public bool enabled = true;
        public Func<Ctx, string[]> desc;
        public Point() { }
        public Point(string id, string label, float x, float? y, float z, string kind = null, float r = 1.6f)
        { this.id = id; this.label = label; this.x = x; this.y = y; this.z = z; this.kind = kind; this.r = r; }
    }

    // A spawn: y null = ground height at (x, z); yaw null = keep the current yaw.
    public sealed class Spawn
    {
        public float x, z;
        public float? y, yaw;
        public Spawn() { }
        public Spawn(float x, float? y, float z, float? yaw = null) { this.x = x; this.y = y; this.z = z; this.yaw = yaw; }
    }

    // A camera anchor for cutscenes: position plus `target` (interiors) or `look` (town), JS space.
    public sealed class Anchor
    {
        public float x, y, z;
        public Vector3? target, look;
    }

    // What level.update and crowds get each frame (engine/core.js frame()).
    public sealed class FrameCtx
    {
        public Camera camera;
        public Engine.Player player;
        public string env;
        public bool power = true;
        public bool reduced;
    }
}
