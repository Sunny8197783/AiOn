// Ports levels/town-fronts.js: shop fronts and signs at street level (편의점, 약국, 마트, 카페, 행정복지센터...),
// awnings, glass doors with their walk-in floors, and the notice boards (shelterNotice, aptNotice, centerNotice).
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownFrontsResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public List<WalkZone> walk = new List<WalkZone>();
        public Dictionary<string, Board> boards = new Dictionary<string, Board>();   // notice-board id -> board
    }

    public static class TownFronts
    {
        public static TownFrontsResult BuildFronts(Kit kit) => throw new NotImplementedException("port: levels/town-fronts.js:buildFronts");
    }
}
