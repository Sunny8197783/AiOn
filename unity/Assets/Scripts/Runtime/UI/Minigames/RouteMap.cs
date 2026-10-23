// Ports ui/routemap.js: the 2D town map (roads, blocks, closures, entrances) and choosing an evacuation route;
// routes are told apart by colour AND dash. mode 'view' just shows the map (M key).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;

namespace Gyeongbo.UI
{
    // drawMap options.
    public sealed class MapDraw
    {
        public int W, H;
        public List<string> closures = new List<string>();
        public Dictionary<string, string> gates = new Dictionary<string, string>();
        public List<RouteOption> options = new List<RouteOption>();
        public int sel = -1;
        public XZ? player, dest;
        public float? playerYaw;
        public bool labels = true;
        public float dpr = 1;
    }

    public static class RouteMap
    {
        // project(x, z) -> canvas pixel; s = pixels per metre.
        public static void DrawMap(Paint g, Func<double, double, UnityEngine.Vector2> project, float s, MapDraw o) => throw new NotImplementedException("port: ui/routemap.js:drawMap");
        // Resolves { choice: index | null }.
        public static Task<RouteMapResult> Open(RouteMapOptions o) => throw new NotImplementedException("port: ui/routemap.js:routemap");
    }
}
