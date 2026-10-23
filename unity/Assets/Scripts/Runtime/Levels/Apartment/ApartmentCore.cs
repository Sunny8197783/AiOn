// Ports levels/apartment-core.js: the lobby with 관리사무소, the elevator (car, doors, floor display) and the
// stairwell flights.
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class StairDims
    {
        public float x0 = 3.4f, x1 = 6.6f, zLand = 1.4f, zMid = 3.6f, zEnd = 5.0f, top = 11.2f;
        public float[] laneA = { 5.1f, 6.6f }, laneB = { 3.4f, 4.9f };
    }

    // Things the level wires up later (H in the JS): door302, door303, elev*, fire, firePoint, seoyeonSpot, candleWindow.
    public sealed class AptHandles
    {
        public AptDoor door302, door303;
        public GameObject elev;
        public Paint elevCanvas;
        public Texture2D elevTex;
        public Material elevMat, candleWindow;
        public AptFire fire;
        public Core.Point firePoint;
        public Core.Spawn seoyeonSpot;
    }

    public sealed class AptFire
    {
        public GameObject normal, scorched, soot, powder;
    }

    public static class ApartmentCore
    {
        public static readonly StairDims STAIR = new StairDims();
        public static void BuildLobby(AptBuilder B, GameObject g) => throw new NotImplementedException("port: levels/apartment-core.js:buildLobby");
        public static void BuildElevator(AptBuilder B, GameObject g, float y, AptHandles H) => throw new NotImplementedException("port: levels/apartment-core.js:buildElevator");
        public static void BuildStairs(AptBuilder B, GameObject g, GameObject dyn) => throw new NotImplementedException("port: levels/apartment-core.js:buildStairs");
    }
}
