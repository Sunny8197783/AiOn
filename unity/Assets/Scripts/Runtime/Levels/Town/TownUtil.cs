// Ports levels/town-util.js: helpers shared by the town modules — rect carving, walk-zone heights, world-UV
// flat geometry, fence/rect solids, wind sway for foliage and cloth, Hangul canvas notice boards.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // board(kit, { w, h, paper }) -> { canvas, texture, draw(title, lines, accent, sub) }; draw re-uploads.
    public sealed class Board
    {
        public Paint canvas;
        public Texture2D texture;
        public void Draw(string title, IList<string> lines, string accent = "#1f4e8c", string sub = "") => throw new NotImplementedException("port: levels/town-util.js:board.draw");
    }

    public static class TownUtil
    {
        // Subtract holes from a walk rect (keeps y / ramp); non-overlapping pieces.
        public static List<WalkZone> Carve(WalkZone r, IList<RectXZ> holes) => throw new NotImplementedException("port: levels/town-util.js:carve");
        public static float ZoneY(WalkZone w, float x, float z) => throw new NotImplementedException("port: levels/town-util.js:zoneY");
        // Highest walk surface at (x, z); 0 outside every zone.
        public static float HeightAt(IList<WalkZone> walk, float x, float z) => throw new NotImplementedException("port: levels/town-util.js:heightAt");
        // Yaw (0 = facing -z) for facing direction (dx, dz), JS convention.
        public static float YawTo(float dx, float dz) => Mathf.Atan2(-dx, -dz);
        // Horizontal plane over a rect at height y, UVs in world metres.
        public static GeoData FlatGeo(RectXZ r, float y = 0) => throw new NotImplementedException("port: levels/town-util.js:flatGeo");
        public static GameObject Flat(RectXZ r, float y, Material material, bool receive = true) => throw new NotImplementedException("port: levels/town-util.js:flat");
        // Fence line [x0, z0, x1, z1] -> blocking box, ends padded so corners close.
        public static Solid LineSolid(FenceLine line, float t = 0.2f, float y0 = -0.5f, float y1 = 2) => throw new NotImplementedException("port: levels/town-util.js:lineSolid");
        public static Solid RectSolid(RectXZ r, float y0 = -0.5f, float y1 = 3, string id = null) => throw new NotImplementedException("port: levels/town-util.js:rectSolid");
        // Shared wind (WIND.uTime / uWind): Town.cs sets it each frame; 0 wind with reduced motion.
        public static void SetWind(float time, float wind) => throw new NotImplementedException("port: levels/town-util.js:WIND");
        // Makes a material sway with height above its origin (foliage, banners); returns it.
        public static Material Sway(Material material, float amp = 0.06f, float freq = 1.7f, float @ref = 1) => throw new NotImplementedException("port: levels/town-util.js:sway");
        public static Board MakeBoard(Kit kit, int w = 512, int h = 384, string paper = "#f4f1e8") => throw new NotImplementedException("port: levels/town-util.js:board");
        // Deterministic pick from [(value, weight)].
        public static T Pick<T>(Func<float> r, IList<KeyValuePair<T, float>> list) => throw new NotImplementedException("port: levels/town-util.js:pick");
    }
}
