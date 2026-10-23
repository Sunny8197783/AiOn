// Ports levels/apartment-corridor.js: the 3rd-floor corridor — unit doors 301-304, the hydrant cabinet and
// extinguisher, the fire spot near 304, the elevator landing.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public static class ApartmentCorridor
    {
        // Unit door spans along x: 301..304.
        public static readonly Dictionary<int, float[]> DOORS = new Dictionary<int, float[]>
        {
            [301] = new[] { -12.6f, -11.65f }, [302] = new[] { -1.75f, -0.8f }, [303] = new[] { 9.0f, 9.95f }, [304] = new[] { 16.0f, 16.95f },
        };
        public static readonly Vector3 FIRE_SPOT = new Vector3(19.2f, ApartmentParts.F3, -0.42f);   // JS space

        public static void BuildCorridor(AptBuilder B, GameObject g, GameObject dyn, AptHandles H) => throw new NotImplementedException("port: levels/apartment-corridor.js:buildCorridor");
    }
}
