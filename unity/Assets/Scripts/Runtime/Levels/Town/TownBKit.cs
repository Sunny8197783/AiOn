// Small helpers shared by the town B ports (Town, TownEvents, TownShelter, TownStrike, TownTraffic): three.js
// idioms the frozen Kit/Geo API does not spell out (bare THREE.Mesh flags, mesh.material, object.clone()).
using System;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    internal static class TownBKit
    {
        // new THREE.Mesh(geometry, material): three.js meshes neither cast nor receive shadows unless told to.
        public static GameObject Mesh(GeoData g, Material m, bool cast = false, bool receive = false) => Geo.Obj(g, m, null, cast, receive);

        // mesh.material of a kit sign / single-mesh object.
        public static Material MatOf(GameObject o)
        {
            var r = o != null ? o.GetComponentInChildren<Renderer>(true) : null;
            return r != null ? r.sharedMaterial : null;
        }

        // object.clone(): shares geometry and material.
        public static GameObject Clone(GameObject o) => UnityEngine.Object.Instantiate(o);

        // A prototype that was only cloned (never added in JS) must not stay in the scene.
        public static void Drop(GameObject o) { if (o != null) UnityEngine.Object.Destroy(o); }

        // JS Math.round (halves round up), not banker's rounding.
        public static float Round(float v) => (float)Math.Floor(v + 0.5);

        // three.js material.side = FrontSide on a material created double-sided.
        // ponytail: sets the URP Lit _Cull property directly (no Mat.SetSide in the frozen API); upgrade: Mat.SetSide.
        public static void FrontSide(Material m)
        {
            if (m != null && m.HasProperty("_Cull")) m.SetFloat("_Cull", 2f);
        }
    }
}
