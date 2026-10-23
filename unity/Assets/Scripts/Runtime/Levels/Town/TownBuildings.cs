// Ports levels/town-buildings.js: every building of game/townmap.js as facade shells (kit facades, entrances,
// roofs, the logistics dock), the filler city outside the bounds, and the villa the D-day missile destroys,
// built unmerged so Town/TownStrike can swap it for a ruin.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // A hole cut into one facade (the shelter's east entrance): side n|s|e|w, span a0..a1, up to y1.
    public sealed class FacadeHole
    {
        public string side;
        public float a0, a1, y1;
    }

    public sealed class FaceOpts
    {
        public float u0, u1 = 1, v0, v1 = 1;
        public bool cast = true;
    }

    // The strike villa: its group and footprint (null box when the town has none).
    public sealed class StrikeSite
    {
        public GameObject group;
        public StrikeBox box;
    }

    public sealed class StrikeBox
    {
        public float x0, x1, z0, z1, h;
    }

    public sealed class TownBuildingsResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public StrikeSite strike;
    }

    public static class TownBuildings
    {
        public static float HeightOf(Building b) => throw new NotImplementedException("port: levels/town-buildings.js:heightOf");
        // Vertical plane for one face, front outward; side n|s|e|w; a0..a1 along the face, y0..y1 height.
        public static GameObject FacePlane(string side, float c, float a0, float a1, float y0, float y1, Material material, FaceOpts o = null) => throw new NotImplementedException("port: levels/town-buildings.js:facePlane");
        // holes: building id -> facade hole.
        public static TownBuildingsResult BuildBuildings(Kit kit, Dictionary<string, FacadeHole> holes = null) => throw new NotImplementedException("port: levels/town-buildings.js:buildBuildings");
    }
}
