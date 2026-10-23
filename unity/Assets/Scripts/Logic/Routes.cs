// Ports game/routes.js: Dijkstra over the townmap pedestrian graph and up to three route options.
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    public class Route
    {
        public List<string> nodes = new List<string>();
        public int length;                         // metres, rounded
        public List<string> segments = new List<string>();
        public List<string> tags = new List<string>();
    }

    // routeOptions() entries. kind: 'shortest' | 'detour' | 'stepFree'; entrance: SHELTER_ENTRANCES key or null.
    public sealed class RouteOption : Route
    {
        public string kind;
        public int seconds;
        public string entrance;
    }

    public static class Routes
    {
        // adj = TownMap.Graph(); tagCost multiplies an edge length per tag (double.PositiveInfinity = never).
        public static Route ShortestPath(Dictionary<string, List<GraphEdge>> adj, string from, string to, IList<string> avoidTags = null, IDictionary<string, double> tagCost = null) => throw new NotImplementedException("port: game/routes.js:shortestPath");
        // gates: entrance -> open; a gate not listed is open (the town's 'open'/'closed' strings convert with == "open").
        // mobility: 'walk' | 'walker'. carryingKg null = the bag load of G.
        public static List<RouteOption> RouteOptions(Game G, string from, string to, IList<string> closures = null, IDictionary<string, bool> gates = null, string mobility = "walk", double? carryingKg = null) => throw new NotImplementedException("port: game/routes.js:routeOptions");
        // '103동 → 단지 정문 → … → 대피소(지하) · 410m, 약 5분 · 계단·횡단보도 있음'
        public static string Explain(Route option) => throw new NotImplementedException("port: game/routes.js:explain");
    }
}
