// Ports game/townmap.js: the fictional 연무동 layout, one source of truth for the 3D town, the 2D map and routes.
// JS frame and JS numbers: x = east, z = south (north is -z), metres. Unity converts at its boundary (J.Map).
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    public class MapRect
    {
        public double x0, x1, z0, z1;
    }

    public sealed class Road : MapRect
    {
        public string id, name;
        public int lanes;
    }

    // Zebra crossing: road id + centre along that road (x for E-W roads, z for N-S roads).
    public sealed class Crosswalk
    {
        public string road;
        public double at;
    }

    // floors x 3 m unless h is given. door: which face has the main door ('n'|'s'|'e'|'w').
    public sealed class Building : MapRect
    {
        public string id, name, kind, label, door, sign;
        public int floors;
        public double? h;
    }

    public sealed class Area : MapRect
    {
        public string id, name, kind;
    }

    public class MapPoint
    {
        public double x, z;
    }

    // PLACES. kind: door | shelter | notice | npc | place.
    public sealed class MapPlace : MapPoint
    {
        public string label, kind, to;
    }

    public sealed class MapSpawn : MapPoint
    {
        public double yaw;
    }

    public sealed class MapNode : MapPoint
    {
        public string label;
    }

    // [from, to, segment id, tags]
    public sealed class Edge
    {
        public string a, b, seg;
        public List<string> tags = new List<string>();
    }

    public sealed class ShelterEntrance
    {
        public string node, place, segment, label, @short, way;
    }

    public sealed class Closure
    {
        public string label;
        public List<string> segments = new List<string>();
        public List<MapRect> barricades = new List<MapRect>();
    }

    public sealed class GraphEdge
    {
        public string to, seg;
        public List<string> tags;
        public double len;
    }

    public static class TownMap
    {
        public static readonly MapRect BOUNDS = new MapRect { x0 = -130, x1 = 130, z0 = -110, z1 = 110 };
        public const double SIDEWALK = 4;
        // Data tables: filled by the Logic porter from game/townmap.js (insertion order kept).
        public static readonly List<Road> ROADS = new List<Road>();
        public static readonly List<Crosswalk> CROSSWALKS = new List<Crosswalk>();
        public static readonly List<Building> BUILDINGS = new List<Building>();
        public static readonly List<Area> AREAS = new List<Area>();
        public static readonly Dictionary<string, MapPlace> PLACES = new Dictionary<string, MapPlace>();
        public static readonly Dictionary<string, MapSpawn> SPAWNS = new Dictionary<string, MapSpawn>();
        public static readonly Dictionary<string, MapNode> NODES = new Dictionary<string, MapNode>();
        public static readonly List<Edge> EDGES = new List<Edge>();
        public static readonly Dictionary<string, ShelterEntrance> SHELTER_ENTRANCES = new Dictionary<string, ShelterEntrance>();
        public static readonly Dictionary<string, Closure> CLOSURES = new Dictionary<string, Closure>();

        public static double Dist(MapPoint a, MapPoint b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
        // Edge list with lengths, skipping closed segments and closed shelter gates (gates null = all open).
        public static Dictionary<string, List<GraphEdge>> Graph(IList<string> closures = null, IDictionary<string, bool> gates = null) => throw new NotImplementedException("port: game/townmap.js:graph");
    }
}
