// Ports levels/apartment-home.js: 302호 — 현관, living room, kitchen, small room with the desk, bedroom,
// balcony; the breaker panel, gas valve, fridge, sink, interaction points, spawns and camera anchors.
using System;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class HomeDims
    {
        public float x0 = -7.6f, x1 = 0.3f, zb = 8.0f, zo = 9.4f;
        public float ch = ApartmentParts.FL + 2.3f, bf = ApartmentParts.FL - 0.08f, top = ApartmentParts.FL + 0.42f;
    }

    public static class ApartmentHome
    {
        public static readonly HomeDims HOME = new HomeDims();
        public static void BuildHome(AptBuilder B, GameObject g, AptHandles H) => throw new NotImplementedException("port: levels/apartment-home.js:buildHome");
    }
}
