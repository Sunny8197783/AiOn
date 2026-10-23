// Ports levels/shelter-shell.js: the car-park shell — floor with lane markings, pillars on the GRID, ceiling
// with ducts and pipes, walls, the stair A, access C and ramp B openings, walk zones and solids.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class ShelterRoom
    {
        public float x0 = -22, x1 = 22, z0 = -16, z1 = 16, ceil = 3.0f, drop = 2.72f;
    }

    public sealed class ShelterGrid
    {
        public float[] xs = { -16.2f, -8.1f, 0, 8.1f, 16.2f }, zs = { -10.5f, 0, 10.5f };
    }

    public sealed class ShelterShellResult
    {
        public GameObject group, dyn;
        public List<WalkZone> walk = new List<WalkZone>();
        public List<Solid> solids = new List<Solid>();
        public List<Vector3> fixtures = new List<Vector3>();
        public List<Light> lights = new List<Light>();
        public Dictionary<string, Material> M = new Dictionary<string, Material>();
    }

    public static class ShelterShell
    {
        public static readonly ShelterRoom ROOM = new ShelterRoom();
        public static readonly ShelterGrid GRID = new ShelterGrid();
        public static readonly RectXZ STAIR = new RectXZ(3.6f, 6.4f, 0, 0), ACCESS = new RectXZ(-14.2f, -11.8f, 0, 0), RAMP = new RectXZ(8, 14, 0, 0), EAST = new RectXZ(0, 0, -1.1f, 1.1f);

        // A cylinder rod from a to b (JS space).
        public static GameObject Rod(Vector3 a, Vector3 b, float r, Material mat, int seg = 10) => throw new NotImplementedException("port: levels/shelter-shell.js:rod");
        public static ShelterShellResult BuildShell(Kit kit, ShelterTex T) => throw new NotImplementedException("port: levels/shelter-shell.js:buildShell");
    }
}
