// Ports game/townmap.js: the fictional 연무동 layout, one source of truth for the 3D town, the 2D map and routes.
// JS frame and JS numbers: x = east, z = south (north is -z), metres, ground y = 0. Unity converts at its boundary (J.Map).
// The tables below are generated from the JS module (values verbatim, insertion order kept).
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
        static List<string> L(params string[] s) => new List<string>(s);
        static Edge E(string a, string b, string seg, params string[] tags) => new Edge { a = a, b = b, seg = seg, tags = new List<string>(tags) };
        static MapRect R(double x0, double x1, double z0, double z1) => new MapRect { x0 = x0, x1 = x1, z0 = z0, z1 = z1 };

        public static readonly MapRect BOUNDS = new MapRect { x0 = -130, x1 = 130, z0 = -110, z1 = 110 };
        public const double SIDEWALK = 4; // sidewalk width along every road edge, curb height 0.12

        public static readonly List<Road> ROADS = new List<Road>
        {
            new Road { id = "yeonmu", name = "연무로", x0 = -130, x1 = 130, z0 = -6, z1 = 6, lanes = 4 },
            new Road { id = "munhwa", name = "문화로", x0 = -54, x1 = -46, z0 = -110, z1 = 110, lanes = 2 },
            new Road { id = "hanbit", name = "한빛로", x0 = 46, x1 = 54, z0 = -110, z1 = 110, lanes = 2 },
            new Road { id = "sijang", name = "시장길", x0 = -46, x1 = 46, z0 = -64, z1 = -56, lanes = 2 },
            new Road { id = "danji", name = "단지길", x0 = -46, x1 = 46, z0 = 56, z1 = 64, lanes = 2 },
        };

        // Zebra crossings: road id + centre coordinate along that road (x for E-W roads, z for N-S roads).
        public static readonly List<Crosswalk> CROSSWALKS = new List<Crosswalk>
        {
            new Crosswalk { road = "yeonmu", at = -104 }, new Crosswalk { road = "yeonmu", at = -60 }, new Crosswalk { road = "yeonmu", at = 0 }, new Crosswalk { road = "yeonmu", at = 60 },
            new Crosswalk { road = "munhwa", at = -14 }, new Crosswalk { road = "munhwa", at = 14 }, new Crosswalk { road = "hanbit", at = -14 }, new Crosswalk { road = "hanbit", at = 14 },
            new Crosswalk { road = "sijang", at = 0 }, new Crosswalk { road = "danji", at = 0 },
        };

        // Building footprints. floors x 3 m unless h is given. door: which face has the main door.
        public static readonly List<Building> BUILDINGS = new List<Building>
        {
            new Building { id = "apt103", name = "한빛아파트 103동", kind = "apartment", x0 = 64, x1 = 112, z0 = 30, z1 = 42, floors = 15, label = "103", door = "n" },
            new Building { id = "apt102", name = "한빛아파트 102동", kind = "apartment", x0 = 64, x1 = 112, z0 = 58, z1 = 70, floors = 15, label = "102" },
            new Building { id = "apt101", name = "한빛아파트 101동", kind = "apartment", x0 = 64, x1 = 112, z0 = 86, z1 = 98, floors = 15, label = "101" },
            new Building { id = "aptOffice", name = "한빛아파트 관리동", kind = "civic", x0 = 116, x1 = 126, z0 = 30, z1 = 40, floors = 1 },
            new Building { id = "store", name = "바로편의점", kind = "shop", x0 = 30, x1 = 42, z0 = 12, z1 = 22, floors = 1, door = "n", sign = "바로편의점" },
            new Building { id = "shopsS", name = "상가", kind = "shop", x0 = 16, x1 = 29, z0 = 12, z1 = 22, floors = 2, sign = "연무약국" },
            new Building { id = "mart", name = "연무마트", kind = "mart", x0 = -36, x1 = 4, z0 = -50, z1 = -24, floors = 2, door = "s", sign = "연무마트" },
            new Building { id = "office", name = "한결물류 연무센터", kind = "office", x0 = 70, x1 = 104, z0 = -48, z1 = -22, floors = 4, door = "s", sign = "한결물류" },
            new Building { id = "warehouse", name = "한결물류 창고", kind = "warehouse", x0 = 64, x1 = 124, z0 = -104, z1 = -60, floors = 1, h = 10 },
            new Building { id = "center", name = "연무동 행정복지센터", kind = "civic", x0 = -104, x1 = -74, z0 = -50, z1 = -28, floors = 3, door = "s", sign = "연무동 행정복지센터" },
            new Building { id = "cafe", name = "카페 쉼표", kind = "shop", x0 = -126, x1 = -110, z0 = -34, z1 = -16, floors = 2, door = "s", sign = "카페 쉼표" },
            new Building { id = "culture", name = "연무 생활문화센터", kind = "civic", x0 = -112, x1 = -70, z0 = 16, z1 = 46, floors = 3, door = "e", sign = "연무 생활문화센터" },
            // Background blocks (not enterable)
            new Building { id = "n1", kind = "shop", x0 = -40, x1 = -8, z0 = -104, z1 = -70, floors = 4 },
            new Building { id = "n2", kind = "villa", x0 = 0, x1 = 40, z0 = -104, z1 = -70, floors = 4 },
            new Building { id = "nw1", kind = "villa", x0 = -126, x1 = -66, z0 = -104, z1 = -62, floors = 4 },
            new Building { id = "s1", kind = "villa", x0 = -40, x1 = -4, z0 = 70, z1 = 104, floors = 4 },
            new Building { id = "s2", kind = "villa", x0 = 4, x1 = 40, z0 = 70, z1 = 104, floors = 5 },
            new Building { id = "sw1", kind = "villa", x0 = -126, x1 = -62, z0 = 60, z1 = 104, floors = 4 },
        };

        // Open areas the town builder fills (park, lots, yards).
        public static readonly List<Area> AREAS = new List<Area>
        {
            new Area { id = "park", name = "연무공원", kind = "park", x0 = -42, x1 = 12, z0 = 12, z1 = 52 },
            new Area { id = "martLot", name = "마트 주차장", kind = "parking", x0 = -36, x1 = 4, z0 = -22, z1 = -12 },
            new Area { id = "centerYard", name = "행정복지센터 마당", kind = "plaza", x0 = -104, x1 = -60, z0 = -26, z1 = -12 },
            new Area { id = "aptGrounds", name = "한빛아파트 단지", kind = "complex", x0 = 58, x1 = 128, z0 = 12, z1 = 104 },
            new Area { id = "officeLot", name = "물류센터 마당", kind = "parking", x0 = 58, x1 = 128, z0 = -56, z1 = -12 },
        };

        // Interaction points in the town. kind: door (to another level), shelter, notice, npc, place.
        public static readonly Dictionary<string, MapPlace> PLACES = new Dictionary<string, MapPlace>
        {
            ["home"] = new MapPlace { x = 78, z = 28.5, label = "103동 출입구", kind = "door", to = "apartment" },
            ["store"] = new MapPlace { x = 36, z = 10.6, label = "바로편의점", kind = "door", to = "store" },
            ["mart"] = new MapPlace { x = -16, z = -22.8, label = "연무마트 입구", kind = "door", to = "mart" },
            ["office"] = new MapPlace { x = 87, z = -20.8, label = "한결물류 연무센터", kind = "door", to = "office" },
            ["center"] = new MapPlace { x = -89, z = -26.8, label = "행정복지센터", kind = "place" },
            ["water"] = new MapPlace { x = -80, z = -17, label = "비상급수시설", kind = "place" },
            ["cafe"] = new MapPlace { x = -118, z = -14.8, label = "카페 쉼표", kind = "place" },
            ["assembly"] = new MapPlace { x = -60, z = -36, label = "임시 집결지", kind = "place" },
            ["shelterEast"] = new MapPlace { x = -68.6, z = 31, label = "동쪽 출입구", kind = "shelter", to = "shelter" },
            ["shelterA"] = new MapPlace { x = -86, z = 13.4, label = "출입구 A · 계단", kind = "shelter", to = "shelter" },
            ["shelterB"] = new MapPlace { x = -80, z = 50, label = "출입구 B · 지하주차장 진출입로", kind = "shelter", to = "shelter" },
            ["shelterC"] = new MapPlace { x = -104, z = 13.4, label = "출입구 C · 경사로", kind = "shelter", to = "shelter" },
            ["shelterNotice"] = new MapPlace { x = -94, z = 12, label = "현장 안내판", kind = "notice" },
            ["aptNotice"] = new MapPlace { x = 72, z = 28.5, label = "단지 게시판", kind = "notice" },
            ["centerNotice"] = new MapPlace { x = -96, z = -20, label = "행정복지센터 게시판", kind = "notice" },
        };

        // Spawn points in the town (yaw 0 faces north / -z).
        public static readonly Dictionary<string, MapSpawn> SPAWNS = new Dictionary<string, MapSpawn>
        {
            ["home"] = new MapSpawn { x = 78, z = 26, yaw = 0 }, ["store"] = new MapSpawn { x = 36, z = 8.6, yaw = 0 }, ["mart"] = new MapSpawn { x = -16, z = -21, yaw = Math.PI },
            ["office"] = new MapSpawn { x = 87, z = -18.5, yaw = Math.PI }, ["cafe"] = new MapSpawn { x = -118, z = -12.5, yaw = Math.PI }, ["shelter"] = new MapSpawn { x = -86, z = 10.8, yaw = Math.PI },
        };

        // Pedestrian network for the 2D map and route planning. Node ids are short.
        public static readonly Dictionary<string, MapNode> NODES = new Dictionary<string, MapNode>
        {
            ["home"] = new MapNode { x = 78, z = 26, label = "103동" }, ["gate"] = new MapNode { x = 57, z = 20, label = "단지 정문" },
            ["I1"] = new MapNode { x = -50, z = -60 }, ["I2"] = new MapNode { x = 50, z = -60 }, ["I3"] = new MapNode { x = -50, z = 0 }, ["I4"] = new MapNode { x = 50, z = 0 }, ["I5"] = new MapNode { x = -50, z = 60 }, ["I6"] = new MapNode { x = 50, z = 60 },
            ["store"] = new MapNode { x = 36, z = 9, label = "편의점" }, ["M"] = new MapNode { x = 0, z = 0, label = "연무로 횡단보도" }, ["mart"] = new MapNode { x = -16, z = -10, label = "연무마트" },
            ["office"] = new MapNode { x = 87, z = -18, label = "한결물류" }, ["yard"] = new MapNode { x = -66, z = -9, label = "행정복지센터 앞" }, ["water"] = new MapNode { x = -80, z = -16, label = "비상급수시설" },
            ["assembly"] = new MapNode { x = -58, z = -36, label = "임시 집결지" }, ["W"] = new MapNode { x = -104, z = 0, label = "서쪽 횡단보도" }, ["cafe"] = new MapNode { x = -118, z = -12, label = "카페" },
            ["S1"] = new MapNode { x = -57, z = 31, label = "문화로" }, ["east"] = new MapNode { x = -68.6, z = 31, label = "동쪽 출입구" },
            ["A"] = new MapNode { x = -86, z = 11, label = "A 계단" }, ["C"] = new MapNode { x = -104, z = 11, label = "C 경사로" }, ["B"] = new MapNode { x = -80, z = 52, label = "B 진출입로" },
            ["shelter"] = new MapNode { x = -90, z = 31, label = "대피소(지하)" },
        };

        // [from, to, segment id, tags]. Segments are what closures switch off. Tags describe the way itself.
        public static readonly List<Edge> EDGES = new List<Edge>
        {
            E("home", "gate", "aptPath"), E("gate", "I4", "hanbitMid"), E("gate", "I6", "hanbitS"),
            E("I4", "store", "yeonmuE"), E("store", "M", "yeonmuC1"), E("M", "I3", "yeonmuC2"), E("M", "mart", "martLot"),
            E("I4", "office", "officePath"), E("I2", "I4", "hanbitN"), E("I1", "I2", "sijang"), E("I1", "assembly", "munhwaN2"),
            E("assembly", "I3", "munhwaN1"), E("I3", "yard", "yeonmuW1"), E("yard", "water", "yardPath"), E("yard", "W", "yeonmuW2"),
            E("W", "cafe", "cafePath"), E("W", "C", "crossW", "crossing"), E("I3", "A", "yeonmuW3", "crossing"), E("A", "C", "cultureN"),
            E("I3", "S1", "munhwaS1"), E("S1", "east", "cultureE"), E("S1", "I5", "munhwaS2"), E("I5", "B", "cultureS"),
            E("I5", "I6", "danji"),
            // Shelter entrances. Which ones are open is game state (see SHELTER_ENTRANCES), not a closure.
            E("east", "shelter", "gateEast", "level"), E("A", "shelter", "gateA", "stairs"), E("B", "shelter", "gateB", "steep"), E("C", "shelter", "gateC", "ramp"),
        };

        public static readonly Dictionary<string, ShelterEntrance> SHELTER_ENTRANCES = new Dictionary<string, ShelterEntrance>
        {
            ["east"] = new ShelterEntrance { node = "east", place = "shelterEast", segment = "gateEast", label = "동쪽 출입구", way = "level" },
            ["stairs"] = new ShelterEntrance { node = "A", place = "shelterA", segment = "gateA", label = "출입구 A · 계단", @short = "A 계단", way = "stairs" },
            ["ramp"] = new ShelterEntrance { node = "B", place = "shelterB", segment = "gateB", label = "출입구 B · 지하주차장 진출입로", @short = "B 진출입로", way = "steep" },
            ["access"] = new ShelterEntrance { node = "C", place = "shelterC", segment = "gateC", label = "출입구 C · 경사로", @short = "C 경사로", way = "ramp" },
        };

        // Road closures: which segments they cut, and where the 3D barricades stand (rects the player cannot cross).
        public static readonly Dictionary<string, Closure> CLOSURES = new Dictionary<string, Closure>
        {
            ["yeonmuCenter"] = new Closure { label = "연무로 중앙 구간 통제", segments = L("yeonmuC1", "yeonmuC2"), barricades = new List<MapRect> { R(27, 29, -10, 10), R(-45, -43, -10, 10) } },
            ["danji"] = new Closure { label = "단지길 통제", segments = L("danji"), barricades = new List<MapRect> { R(-1, 1, 52, 68) } },
            ["sijang"] = new Closure { label = "시장길 통제", segments = L("sijang"), barricades = new List<MapRect> { R(-1, 1, -68, -52) } },
            ["munhwaSouth"] = new Closure { label = "문화로 남쪽 통제", segments = L("munhwaS2"), barricades = new List<MapRect> { R(-58, -42, 44, 46) } },
            ["munhwaNorth"] = new Closure { label = "문화로 북쪽 통제", segments = L("munhwaN1"), barricades = new List<MapRect> { R(-58, -42, -20, -18) } },
        };

        // Math.hypot(a.x - b.x, a.z - b.z), computed the way V8 does (scaled Kahan sum) so lengths match JS to the bit.
        public static double Dist(MapPoint a, MapPoint b) => Hypot(a.x - b.x, a.z - b.z);

        static double Hypot(double p, double q)
        {
            if (double.IsInfinity(p) || double.IsInfinity(q)) return double.PositiveInfinity;
            if (double.IsNaN(p) || double.IsNaN(q)) return double.NaN;
            double x = Math.Abs(p), y = Math.Abs(q), max = Math.Max(x, y);
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            foreach (var v in new[] { x, y })
            {
                double n = v / max, summand = n * n - compensation, preliminary = sum + summand;
                compensation = (preliminary - sum) - summand;
                sum = preliminary;
            }
            return Math.Sqrt(sum) * max;
        }

        // Edge list with lengths, skipping closed segments and closed shelter gates. gates null = all open;
        // otherwise a gate missing from gates counts as closed, as JS !gates[k] does.
        public static Dictionary<string, List<GraphEdge>> Graph(IList<string> closures = null, IDictionary<string, bool> gates = null)
        {
            var cut = new HashSet<string>();
            foreach (var id in closures ?? Array.Empty<string>())
                if (id != null && CLOSURES.TryGetValue(id, out var c)) cut.UnionWith(c.segments);
            if (gates != null)
                foreach (var kv in SHELTER_ENTRANCES)
                    if (!(gates.TryGetValue(kv.Key, out var open) && open)) cut.Add(kv.Value.segment);
            var adj = new Dictionary<string, List<GraphEdge>>();
            foreach (var id in NODES.Keys) adj[id] = new List<GraphEdge>();
            foreach (var e in EDGES)
            {
                if (cut.Contains(e.seg)) continue;
                var len = Dist(NODES[e.a], NODES[e.b]);
                adj[e.a].Add(new GraphEdge { to = e.b, seg = e.seg, tags = e.tags, len = len });
                adj[e.b].Add(new GraphEdge { to = e.a, seg = e.seg, tags = e.tags, len = len });
            }
            return adj;
        }
    }
}
