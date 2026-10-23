// Ports levels/shelter-camp.js: the camp — sleeping mats on a grid (one free mat for the player), reception
// table and chairs, the notice board, the radio corner, relief pallets, a lantern, banners.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // A seat on a mat or chair: position, facing, which mat.
    public sealed class ShelterSeat
    {
        public float x, z, yaw;
        public int mat;
    }

    public sealed class ShelterCampResult
    {
        public GameObject group, live, dial, banner, cloth;
        public List<Vector3> mats = new List<Vector3>();
        public List<ShelterSeat> seats = new List<ShelterSeat>();
        public List<Solid> solids = new List<Solid>();
        public Material boardMat;
        public Light lanternLight;
    }

    public static class ShelterCamp
    {
        public static readonly float[] MAT_COLS = new float[13];   // -20.4 + 2.2 i
        public static readonly float[] MAT_ROWS = { -3.9f, -1.55f, 1.55f, 3.9f };
        public static readonly float[] SOUTH_MAT_COLS = { -20.6f, -18.4f, -16.2f, -14, -11.8f, -9.6f }, SOUTH_MAT_ROWS = { 12.3f, 14.5f };
        public static readonly XZ RECEPTION = new XZ(2.0f, -8.6f), BOARD = new XZ(-3.0f, -13.6f), RADIO = new XZ(-5.5f, 7.4f), PALLETS = new XZ(-19.3f, -13.2f);
        // [dx, dz, ry] from the table.
        public static readonly Dictionary<string, float[][]> CHAIRS = new Dictionary<string, float[][]>();
        // The free mat the player can take (MAT_COLS[9], MAT_ROWS[3]).
        public static XZ SPOT => throw new NotImplementedException("port: levels/shelter-camp.js:SPOT");

        public static ShelterCampResult BuildCamp(Kit kit, ShelterTex T) => throw new NotImplementedException("port: levels/shelter-camp.js:buildCamp");
    }
}
