// Ports levels/town-park.js: 연무공원 (paths, playground, outdoor gym, pavilion, benches) and every tree in town
// (street ginkgos in pits, park zelkovas and maples, complex rows) instanced, plus shrubs from TownProps.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // A tree or shrub: { x, z, type, s, ry, tint }; type ginkgo | zelkova | maple | shrub.
    public sealed class TreeSpot
    {
        public float x, z, s, ry, tint;
        public string type;
    }

    public sealed class TownParkResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public List<WalkZone> walk = new List<WalkZone>();
        public List<TreeSpot> spots = new List<TreeSpot>();
    }

    public static class TownPark
    {
        public static readonly List<XZ> PARK_PATH = new List<XZ>();
        public static List<TreeSpot> TreeSpots(Func<float> rand) => throw new NotImplementedException("port: levels/town-park.js:treeSpots");
        // spots: all trees and shrubs to plant (Town passes TreeSpots + TownProps.ShrubSpots).
        public static TownParkResult BuildPark(Kit kit, List<TreeSpot> spots) => throw new NotImplementedException("port: levels/town-park.js:buildPark");
    }
}
