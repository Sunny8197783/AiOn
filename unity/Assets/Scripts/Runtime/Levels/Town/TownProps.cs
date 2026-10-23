// Ports levels/town-props.js: street furniture — street lights (instanced heads in light group 'street', a few
// real lights), 전봇대 with sagging wires, the bus stop, bollards, hydrants, planters, vending machines,
// bins, bicycles, parked cars.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownPropsResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public List<Light> lights = new List<Light>();
    }

    public static class TownProps
    {
        // [x, z, ry] planter boxes.
        public static readonly List<float[]> PLANTERS = new List<float[]>();
        public static List<TreeSpot> ShrubSpots(Func<float> rand) => throw new NotImplementedException("port: levels/town-props.js:shrubSpots");
        // avoid: tree/shrub spots props must keep clear of.
        public static TownPropsResult BuildProps(Kit kit, List<TreeSpot> avoid = null) => throw new NotImplementedException("port: levels/town-props.js:buildProps");
    }
}
