// Ports game/routes.js: Dijkstra over the townmap pedestrian graph and up to three route options.
using System;
using System.Collections.Generic;
using System.Linq;

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
        static readonly Dictionary<string, double> SPEED = new Dictionary<string, double> { ["walk"] = 1.3, ["walker"] = 0.9 }; // m/s
        static readonly string[] STEP_TAGS = { "stairs", "steep" };    // impassable with a rollator walker
        const double HEAVY_KG = 8;

        // Dijkstra. adj = TownMap.Graph(); tagCost multiplies an edge length per tag (double.PositiveInfinity = never use).
        public static Route ShortestPath(Dictionary<string, List<GraphEdge>> adj, string from, string to, IList<string> avoidTags = null, IDictionary<string, double> tagCost = null)
        {
            if (adj == null || from == null || to == null || !adj.ContainsKey(from) || !adj.ContainsKey(to)) return null;
            avoidTags ??= Array.Empty<string>();
            // Insertion-ordered like the JS object, so ties pick the same node.
            var cost = new Dictionary<string, double> { [from] = 0 };
            var prev = new Dictionary<string, (string from, GraphEdge e)>();
            var done = new HashSet<string>();
            for (;;)
            {
                string u = null;
                foreach (var n in cost.Keys) if (!done.Contains(n) && (u == null || cost[n] < cost[u])) u = n;
                if (u == null) return null;
                if (u == to) break;
                done.Add(u);
                foreach (var e in adj[u])
                {
                    var tags = e.tags ?? new List<string>();
                    if (done.Contains(e.to) || tags.Any(avoidTags.Contains)) continue;
                    var c = cost[u] + e.len * tags.Aggregate(1.0, (m, t) => m * (tagCost != null && tagCost.TryGetValue(t, out var k) ? k : 1));
                    if (!double.IsNaN(c) && !double.IsInfinity(c) && !(cost.TryGetValue(e.to, out var old) && c >= old)) { cost[e.to] = c; prev[e.to] = (u, e); }
                }
            }
            var r = new Route { nodes = new List<string> { to } };
            double length = 0;
            for (var n = to; n != from; n = prev[n].from)
            {
                var (p, e) = prev[n];
                r.nodes.Insert(0, p);
                r.segments.Insert(0, e.seg);
                foreach (var t in e.tags ?? new List<string>()) if (!r.tags.Contains(t)) r.tags.Add(t);
                length += e.len;
            }
            r.length = (int)Js.Round(length);
            return r;
        }

        static Dictionary<string, List<GraphEdge>> Without(Dictionary<string, List<GraphEdge>> adj, List<string> segs) => segs.Count > 0
            ? adj.ToDictionary(kv => kv.Key, kv => kv.Value.Where(e => !segs.Contains(e.seg)).ToList())
            : adj;

        // The route's segments, longest first (a stable sort, as JS Array.prototype.sort is).
        static List<string> BySize(Route r) => r.segments
            .Select((seg, i) => (seg, d: TownMap.Dist(TownMap.NODES[r.nodes[i]], TownMap.NODES[r.nodes[i + 1]])))
            .OrderByDescending(x => x.d).Select(x => x.seg).ToList();

        // Up to 3 distinct options: the shortest, a detour around the shortest's longest segment, and a
        // step-free one (walk) or a second detour (walker). Each adds { kind, seconds, entrance }.
        // gates: entrance -> open; a gate not listed is open (the town's 'open'/'closed' strings convert with == "open").
        // mobility: 'walk' | 'walker'. carryingKg null = the bag load of G.
        public static List<RouteOption> RouteOptions(Game G, string from, string to, IList<string> closures = null, IDictionary<string, bool> gates = null, string mobility = "walk", double? carryingKg = null)
        {
            if (mobility == null || !SPEED.TryGetValue(mobility, out var walkSpeed)) throw new ArgumentException($"unknown mobility: {mobility}");
            var open = TownMap.SHELTER_ENTRANCES.Keys.ToDictionary(k => k, k => !(gates != null && gates.TryGetValue(k, out var v) && !v));
            var adj = TownMap.Graph(closures ?? Array.Empty<string>(), open);
            var kg = carryingKg ?? (G != null ? State.BagLoad(G).weightKg : 0);
            var speed = walkSpeed * (kg > HEAVY_KG ? 0.8 : 1);
            var @base = mobility == "walker" ? STEP_TAGS : Array.Empty<string>();
            Route Find(List<string> cut = null, IList<string> avoidTags = null) => ShortestPath(Without(adj, cut ?? new List<string>()), from, to, avoidTags ?? @base);

            var opts = new List<RouteOption>();
            bool Add(Route r, string kind)
            {
                if (r == null || opts.Any(o => string.Join(",", o.segments) == string.Join(",", r.segments))) return false;
                var entrance = TownMap.SHELTER_ENTRANCES.Keys.FirstOrDefault(k => r.segments.Contains(TownMap.SHELTER_ENTRANCES[k].segment));
                opts.Add(new RouteOption { nodes = r.nodes, length = r.length, segments = r.segments, tags = r.tags, kind = kind, seconds = (int)Js.Round(r.length / speed), entrance = entrance });
                return true;
            }
            if (!Add(Find(), "shortest")) return new List<RouteOption>();
            foreach (var seg in BySize(opts[0])) if (Add(Find(new List<string> { seg }), "detour")) break;
            if (mobility == "walk") Add(Find(null, STEP_TAGS), "stepFree");
            if (opts.Count < 3 && opts.Count > 1) Add(Find(new List<string> { BySize(opts[0]).FirstOrDefault(), BySize(opts[1]).FirstOrDefault() }), "detour");
            return opts.Take(3).ToList();
        }

        static readonly Dictionary<string, string> TAG_NOTE = new Dictionary<string, string> { ["stairs"] = "계단", ["steep"] = "가파른 진출입로", ["ramp"] = "경사로", ["crossing"] = "횡단보도" };

        // '103동 → 단지 정문 → … → 대피소(지하) · 410m, 약 5분 · 계단·횡단보도 있음'
        public static string Explain(Route option)
        {
            if (option?.nodes == null || option.nodes.Count == 0) return "갈 수 있는 길이 없습니다.";
            var names = option.nodes.Select(n => n != null && TownMap.NODES.TryGetValue(n, out var node) ? node.label : null).Where(l => !string.IsNullOrEmpty(l)).ToList();
            var via = string.Join(" → ", names.Where((n, i) => i == 0 || n != names[i - 1]));
            var secs = option is RouteOption o ? o.seconds : option.length / SPEED["walk"];
            var notes = (option.tags ?? new List<string>()).Select(t => TAG_NOTE.TryGetValue(t, out var w) ? w : null).Where(w => w != null).ToList();
            return $"{via} · {option.length}m, 약 {Js.Num(Math.Max(1, Js.Round(secs / 60)))}분{(notes.Count > 0 ? $" · {string.Join("·", notes)} 있음" : "")}";
        }
    }
}
