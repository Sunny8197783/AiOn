// Ports levels/town-ground.js: roads with markings, curbs, 보도블록 sidewalks, tactile paving at crossings,
// lots and lawns. Block tops sit at curb height and are carved around the shelter pits (holes).
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownGroundResult
    {
        public GameObject group;
        public List<WalkZone> walk = new List<WalkZone>();
    }

    public static class TownGround
    {
        public static TownGroundResult BuildGround(Engine.Kit kit, IList<RectXZ> holes = null) => throw new NotImplementedException("port: levels/town-ground.js:buildGround");
        // Thin quad from p to q (world xz) of width w at height y: park paths, painted bands.
        public static GameObject Strip(XZ p, XZ q, float w, float y, Material material) => throw new NotImplementedException("port: levels/town-ground.js:strip");
    }
}
