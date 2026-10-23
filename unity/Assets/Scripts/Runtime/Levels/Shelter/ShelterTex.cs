// Ports levels/shelter-tex.js: the shelter's canvas textures — epoxy floor, grooves, tactile strips, hazard
// bands, pillar zone atlas (A/B/C), floor marks, exit and aisle signs, the notice board, roster, banner,
// hydrant, water packs and cartons; cellPlane picks one cell of an atlas.
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class ShelterZone
    {
        public string letter, color;
    }

    public sealed class ShelterTex
    {
        public Material floorMaterial, grooveMaterial, tactileMaterial, hazardMaterial;
        public Texture2D pillarAtlas, markAtlas, exitTex, aisleTex, rosterTex, bannerTex, hydrantTex, waterPackTex, cartonTex;
        // Redraws the notice board (lines; null = the default text).
        public Action<IList<string>> drawBoard;
    }

    public static class ShelterTextures
    {
        public static readonly List<ShelterZone> ZONES = new List<ShelterZone>
        {
            new ShelterZone { letter = "A", color = "#2f8f5b" }, new ShelterZone { letter = "B", color = "#d99a1e" }, new ShelterZone { letter = "C", color = "#2f6db5" },
        };

        public static ShelterTex Build(Kit kit) => throw new NotImplementedException("port: levels/shelter-tex.js:shelterTextures");
        // A w x h plane showing atlas cell `cell` (cells counted as in the JS atlas layout).
        public static GeoData CellPlane(float w, float h, int cell) => throw new NotImplementedException("port: levels/shelter-tex.js:cellPlane");
    }
}
