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
        // Highest walk surface within `below` m below / 0.45 m above feetY at (x, z); any = ignore the band.
        public static Ground? GroundAt(IList<WalkZone> walk, float x, float z, float feetY, bool any = false, float below = 0.6f) => throw new NotImplementedException("port: engine/geom.js:groundAt");
        // Tops of solids as flat walk zones.
        public static List<WalkZone> TopsOf(IEnumerable<Solid> solids) => throw new NotImplementedException("port: engine/geom.js:topsOf");
        // Ground with a little footprint: a seam narrower than the body is not a hole.
        public static Ground? GroundNear(IList<WalkZone> walk, float x, float z, float feetY) => throw new NotImplementedException("port: engine/geom.js:groundNear");
        // Push a circle out of every solid overlapping [feetY, feetY + h]; returns the new (x, z).
        public static (float x, float z) Collide(float px, float pz, float feetY, IList<Solid> solids, float r = 0.32f, float h = 1.72f) => throw new NotImplementedException("port: engine/geom.js:collide");
        // Segment o -> o + d (t in 0..1) against the solid grown by pad: entry t, or +Infinity (also when o is inside).
        public static float SegBox(Vector3 o, Vector3 d, Solid s, float pad) => throw new NotImplementedException("port: engine/geom.js:segBox");
    }
}
