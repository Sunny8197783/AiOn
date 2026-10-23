// Ports tests/routes.test.mjs. JS gates may be booleans or 'open'/'closed'; the C# API takes booleans,
// so 'closed' is false and 'open' is true here.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gyeongbo.Logic;
using NUnit.Framework;

namespace Gyeongbo.Tests
{
    public class RoutesTests
    {
        static Dictionary<string, bool> Gates(params object[] kv)
        {
            var d = new Dictionary<string, bool>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = (bool)kv[i + 1];
            return d;
        }

        static readonly Dictionary<string, bool>[] GATE_SETS =
        {
            Gates("east", true, "stairs", true, "ramp", true, "access", true),
            Gates("east", false, "stairs", true, "ramp", true, "access", false),
            Gates("east", false, "stairs", true, "ramp", true, "access", true), // JS: 'closed' / 'open'
            Gates("east", false, "stairs", false, "ramp", false, "access", true),
        };

        static Dictionary<string, bool> AllShut() => Gates("east", false, "stairs", false, "ramp", false, "access", false);

        [Test]
        public void ShortestPathFindsTheShortestWayAndItsParts()
        {
            var adj = TownMap.Graph();
            var r = Routes.ShortestPath(adj, "home", "store");
            CollectionAssert.AreEqual(new[] { "home", "gate", "I4", "store" }, r.nodes);
            CollectionAssert.AreEqual(new[] { "aptPath", "hanbitMid", "yeonmuE" }, r.segments);
            Assert.AreEqual(0, r.tags.Count);
            Assert.IsTrue(r.length > 50 && r.length < 70);
            var self = Routes.ShortestPath(adj, "home", "home");
            CollectionAssert.AreEqual(new[] { "home" }, self.nodes);
            Assert.AreEqual(0, self.length);
            Assert.AreEqual(0, self.segments.Count);
            Assert.AreEqual(0, self.tags.Count);
        }

        [Test]
        public void ShortestPathEdgeCases()
        {
            var adj = TownMap.Graph();
            Assert.IsNull(Routes.ShortestPath(adj, "home", "moon"));
            Assert.IsNull(Routes.ShortestPath(null, "home", "store"));
            Assert.IsNull(Routes.ShortestPath(TownMap.Graph(null, AllShut()), "home", "shelter"));
            var viaA = Routes.ShortestPath(adj, "I3", "shelter");
            Assert.IsTrue(viaA.tags.Contains("stairs"));
            var noStairs = Routes.ShortestPath(adj, "I3", "shelter", new[] { "stairs" });
            Assert.IsFalse(noStairs.tags.Contains("stairs"));
            var pricey = Routes.ShortestPath(adj, "I3", "shelter", null, new Dictionary<string, double> { ["stairs"] = 100 });
            Assert.IsFalse(pricey.tags.Contains("stairs"));
            var never = Routes.ShortestPath(adj, "A", "shelter", null, new Dictionary<string, double> { ["stairs"] = double.PositiveInfinity, ["ramp"] = double.PositiveInfinity, ["level"] = double.PositiveInfinity });
            Assert.IsFalse(never?.tags.Contains("stairs") ?? false);
        }

        [Test]
        public void RouteOptionsGivesUpToThreeDistinctOptionsWithTime()
        {
            var G = State.CreateGame(1);
            var opts = Routes.RouteOptions(G, "home", "shelter", new[] { "yeonmuCenter" }, Gates("east", false, "access", false));
            Assert.IsTrue(opts.Count >= 2 && opts.Count <= 3);
            Assert.AreEqual(opts.Count, opts.Select(o => string.Join(",", o.segments)).Distinct().Count());
            Assert.AreEqual("shortest", opts[0].kind);
            foreach (var o in opts)
            {
                Assert.AreEqual(Math.Floor(o.length / 1.3 + 0.5), o.seconds);
                Assert.IsTrue(o.entrance == "stairs" || o.entrance == "ramp", o.entrance);
                Assert.IsFalse(o.segments.Any(s => TownMap.CLOSURES["yeonmuCenter"].segments.Contains(s)));
                Assert.IsFalse(o.segments.Contains("gateEast") || o.segments.Contains("gateC"));
            }
            Assert.IsTrue(opts.All(o => o.length >= opts[0].length));
            Assert.AreEqual(0, Routes.RouteOptions(G, "home", "shelter", null, AllShut()).Count);
            Assert.Throws<ArgumentException>(() => Routes.RouteOptions(G, "home", "shelter", mobility: "fly"));
        }

        [Test]
        public void CarryingOver8KgSlowsWalkingTheBagIsUsedWhenCarryingKgIsNotGiven()
        {
            var G = State.CreateGame(1);
            var light = Routes.RouteOptions(G, "home", "water")[0];
            var heavy = Routes.RouteOptions(G, "home", "water", carryingKg: 10)[0];
            Assert.AreEqual(Math.Floor(light.length / (1.3 * 0.8) + 0.5), heavy.seconds);
            State.Buy(G, new Dictionary<string, int> { ["water_2l"] = 3 }, new Dictionary<string, int> { ["water_2l"] = 1100 });
            State.Pack(G, "water_2l", 5);
            Assert.AreEqual(heavy.seconds, Routes.RouteOptions(G, "home", "water")[0].seconds);
            Assert.AreEqual(light.seconds, Routes.RouteOptions(null, "home", "water")[0].seconds);
        }

        [Test]
        public void WalkerRoutesNeverUseStairsOrSteepWays()
        {
            var sets = new List<IList<string>> { new string[0] };
            sets.AddRange(State.CLOSURE_SETS["dday"]);
            sets.AddRange(State.CLOSURE_SETS["evac"]);
            sets.Add(TownMap.CLOSURES.Keys.ToList());
            var pairs = new[] { ("cafe", "shelter"), ("home", "shelter"), ("home", "assembly"), ("store", "water") };
            int found = 0;
            foreach (var closures in sets)
                foreach (var gates in GATE_SETS)
                    foreach (var (from, to) in pairs)
                        foreach (var o in Routes.RouteOptions(null, from, to, closures, gates, "walker"))
                        {
                            found++;
                            Assert.IsTrue(!o.tags.Contains("stairs") && !o.tags.Contains("steep"), $"{from}->{to} {string.Join(",", o.segments)}");
                            Assert.AreEqual(Math.Floor(o.length / 0.9 + 0.5), o.seconds);
                        }
            Assert.Greater(found, 50);
            var seoyeon = Routes.RouteOptions(null, "cafe", "shelter", null, Gates("east", false), "walker")[0];
            Assert.AreEqual("access", seoyeon.entrance);
            Assert.AreEqual(0, Routes.RouteOptions(null, "cafe", "shelter", null, Gates("access", false, "east", false), "walker").Count);
        }

        [Test]
        public void WalkingOptionsIncludeAStepFreeOneWhenItDiffers()
        {
            var opts = Routes.RouteOptions(null, "home", "shelter", null, Gates("east", false));
            Assert.IsTrue(opts.Any(o => !o.tags.Contains("stairs") && !o.tags.Contains("steep")));
        }

        [Test]
        public void ExplainDescribesTheRouteInKorean()
        {
            var o = Routes.RouteOptions(null, "home", "shelter", null, Gates("east", false, "ramp", false, "access", false))[0];
            var s = Routes.Explain(o);
            Assert.IsTrue(Regex.IsMatch(s, "^103동 → 단지 정문"), s);
            Assert.IsTrue(Regex.IsMatch(s, @"대피소\(지하\)"), s);
            Assert.IsTrue(Regex.IsMatch(s, "계단"), s);
            Assert.IsTrue(Regex.IsMatch(s, "[0-9]+m, 약 [0-9]+분"), s);
            Assert.AreEqual("갈 수 있는 길이 없습니다.", Routes.Explain(null));
            var plain = Routes.Explain(new Route { nodes = new List<string> { "home", "gate" }, length = 22 });
            Assert.IsTrue(Regex.IsMatch(plain, "103동 → 단지 정문 · 22m, 약 1분$"), plain);
        }

        static string Show(IEnumerable<RouteOption> opts) => string.Join(" / ", opts.Select(o => $"{o.kind}|{o.entrance ?? "null"}|{o.length}|{o.seconds}|{string.Join(",", o.segments)}|{string.Join(",", o.tags)}"));

        // Port check: the same options, lengths and times node gives (Dijkstra ties, Math.hypot, rounding).
        [Test]
        public void RouteOptionsMatchTheJsOutput()
        {
            Assert.AreEqual("shortest|ramp|217|167|aptPath,hanbitS,danji,cultureS,gateB|steep / detour|stairs|323|248|aptPath,hanbitMid,hanbitN,sijang,munhwaN2,munhwaN1,yeonmuW3,gateA|stairs,crossing",
                Show(Routes.RouteOptions(null, "home", "shelter", new[] { "yeonmuCenter" }, Gates("east", false, "access", false))));
            Assert.AreEqual("shortest|stairs|205|158|aptPath,hanbitMid,yeonmuE,yeonmuC1,yeonmuC2,yeonmuW3,gateA|stairs,crossing / detour|ramp|217|167|aptPath,hanbitS,danji,cultureS,gateB|steep / stepFree|access|227|175|aptPath,hanbitMid,yeonmuE,yeonmuC1,yeonmuC2,yeonmuW3,cultureN,gateC|ramp,crossing",
                Show(Routes.RouteOptions(null, "home", "shelter", null, Gates("east", false))));
            Assert.AreEqual("shortest|access|54|60|cafePath,crossW,gateC|ramp,crossing / detour|access|156|173|cafePath,yeonmuW2,yeonmuW1,yeonmuW3,cultureN,gateC|ramp,crossing",
                Show(Routes.RouteOptions(null, "cafe", "shelter", null, Gates("east", false), "walker")));
            Assert.AreEqual("shortest|null|184|204|aptPath,hanbitMid,yeonmuE,yeonmuC1,yeonmuC2,munhwaN1|",
                Show(Routes.RouteOptions(null, "home", "assembly", new[] { "danji", "sijang" }, null, "walker")));
            Assert.AreEqual("shortest|null|121|116|yeonmuC1,yeonmuC2,yeonmuW1,yardPath| / detour|null|273|263|yeonmuE,hanbitN,sijang,munhwaN2,munhwaN1,yeonmuW1,yardPath| / detour|null|274|263|yeonmuE,hanbitMid,hanbitS,danji,munhwaS2,munhwaS1,yeonmuW1,yardPath|",
                Show(Routes.RouteOptions(null, "store", "water", carryingKg: 9)));
            Assert.AreEqual("103동 → 단지 정문 → 편의점 → 연무로 횡단보도 → A 계단 → 대피소(지하) · 205m, 약 3분 · 계단·횡단보도 있음",
                Routes.Explain(Routes.RouteOptions(null, "home", "shelter", null, Gates("east", false, "ramp", false, "access", false))[0]));
            Assert.AreEqual("카페 → 서쪽 횡단보도 → 행정복지센터 앞 → 연무로 횡단보도 → 연무마트 · 145m, 약 2분",
                Routes.Explain(Routes.ShortestPath(TownMap.Graph(), "cafe", "mart")));
        }
    }
}
