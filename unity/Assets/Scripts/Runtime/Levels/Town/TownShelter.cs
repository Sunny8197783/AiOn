// Ports levels/town-shelter.js: the 연무 생활문화센터 shelter site above ground — the east entrance, stair A,
// driveway B, ramp C (pits cut into the ground), shelter signs, gate shutters and guide posts.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownShelterResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public List<WalkZone> walk = new List<WalkZone>();
        // gates { east|stairs|ramp|access: open|closed }
        public Action<Dictionary<string, string>> setGates;
    }

    public static class TownShelter
    {
        // Ground rects cut out for the stair and ramp pits.
        public static readonly List<RectXZ> SHELTER_HOLES = new List<RectXZ>();
        public static readonly FacadeHole FACADE_HOLE = new FacadeHole { side = "s", a0 = -84.5f, a1 = -75.5f, y1 = 2.0f };
        public static readonly Dictionary<string, GuideSpot> GUIDE_SPOTS = new Dictionary<string, GuideSpot>
        {
            ["guideEast"] = new GuideSpot { x = -66.4f, z = 28.9f, face = new XZ(1, 0) },
            ["guideC"] = new GuideSpot { x = -100.4f, z = 12.7f, face = new XZ(0.4f, -1) },
        };

        public static TownShelterResult BuildShelterSite(Kit kit) => throw new NotImplementedException("port: levels/town-shelter.js:buildShelterSite");
    }
}
