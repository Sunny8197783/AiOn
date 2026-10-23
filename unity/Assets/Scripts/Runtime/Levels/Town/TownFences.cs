// Ports levels/town-fences.js: the fence lines of TownPlan.FENCES (steel palisade, park fence + hedge, hedge,
// block wall) with their blocking solids.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownFencesResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
    }

    public static class TownFences
    {
        public static TownFencesResult BuildFences(Kit kit) => throw new NotImplementedException("port: levels/town-fences.js:buildFences");
    }
}
