// Ports tests/townmap.test.mjs: the pedestrian graph is wired, and every closure set keeps the way home -> shelter.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Logic;
using NUnit.Framework;

namespace Gyeongbo.Tests
{
    public class TownMapTests
    {
        static HashSet<string> Segs() => new HashSet<string>(TownMap.EDGES.Select(e => e.seg));

        static bool Reachable(Dictionary<string, List<GraphEdge>> adj, string from, string to)
        {
            var seen = new HashSet<string> { from };
            var todo = new Stack<string>();
            todo.Push(from);
            while (todo.Count > 0) foreach (var e in adj[todo.Pop()]) if (seen.Add(e.to)) todo.Push(e.to);
            return seen.Contains(to);
        }

        // Worst case on any night: east gate closed, entrance C still shut (opens 20:16).
        static Dictionary<string, bool> WorstGates() => new Dictionary<string, bool> { ["east"] = false, ["stairs"] = true, ["ramp"] = true, ["access"] = false };

        [Test]
        public void EveryEdgeEndpointIsANodeAndSegmentIdsAreUnique()
        {
            foreach (var e in TownMap.EDGES)
            {
                Assert.IsTrue(TownMap.NODES.ContainsKey(e.a), $"edge {e.seg}: unknown node {e.a}");
                Assert.IsTrue(TownMap.NODES.ContainsKey(e.b), $"edge {e.seg}: unknown node {e.b}");
                Assert.IsNotNull(e.tags);
            }
            Assert.AreEqual(TownMap.EDGES.Count, Segs().Count);
        }

        [Test]
        public void EveryClosureSegmentExistsEveryShelterEntranceIsWired()
        {
            var segs = Segs();
            foreach (var kv in TownMap.CLOSURES)
            {
                Assert.Greater(kv.Value.segments.Count, 0, kv.Key);
                foreach (var s in kv.Value.segments) Assert.IsTrue(segs.Contains(s), $"closure {kv.Key}: unknown segment {s}");
            }
            foreach (var kv in TownMap.SHELTER_ENTRANCES)
            {
                Assert.IsTrue(TownMap.NODES.ContainsKey(kv.Value.node), kv.Key);
                Assert.IsTrue(TownMap.PLACES.ContainsKey(kv.Value.place), kv.Key);
                var edge = TownMap.EDGES.FirstOrDefault(x => x.seg == kv.Value.segment);
                Assert.IsTrue(edge != null && (edge.a == "shelter" || edge.b == "shelter"), $"{kv.Key} segment must lead into the shelter");
            }
            foreach (var id in new[] { "home", "shelter" }) Assert.IsTrue(TownMap.SPAWNS.ContainsKey(id), id);
        }

        [Test]
        public void GraphIsSymmetricAndDropsClosedSegmentsAndGates()
        {
            var open = TownMap.Graph();
            foreach (var kv in open) foreach (var e in kv.Value) Assert.IsTrue(open[e.to].Any(x => x.to == kv.Key && x.seg == e.seg));
            var g = TownMap.Graph(new[] { "yeonmuCenter" }, WorstGates());
            var all = g.Values.SelectMany(es => es).Select(e => e.seg).ToList();
            foreach (var s in new[] { "yeonmuC1", "yeonmuC2", "gateEast", "gateC" }) Assert.IsFalse(all.Contains(s), s);
            Assert.IsTrue(all.Contains("gateA"));
        }

        [Test]
        public void ConditionsOnlyEverProduceClosureSetsFromClosureSets()
        {
            string Key(IEnumerable<string> l) => string.Join("+", l.OrderBy(x => x, StringComparer.Ordinal));
            for (uint seed = 1; seed <= 500; seed++)
            {
                var c = State.Conditions(seed).closures;
                Assert.IsTrue(State.CLOSURE_SETS["dday"].Any(l => Key(l) == Key(c.dday)), $"seed {seed} dday");
                Assert.IsTrue(State.CLOSURE_SETS["evac"].Any(l => Key(l) == Key(c.evac)), $"seed {seed} evac");
            }
        }

        [Test]
        public void HomeToShelterStaysReachableOnFootForEveryClosureSet()
        {
            for (uint seed = 1; seed <= 500; seed++)
            {
                var c = State.Conditions(seed).closures;
                foreach (var set in new[] { c.dday, c.evac })
                    Assert.IsTrue(Reachable(TownMap.Graph(set, WorstGates()), "home", "shelter"), $"seed {seed}: {string.Join(",", set)}");
            }
            foreach (var set in State.CLOSURE_SETS["dday"].Concat(State.CLOSURE_SETS["evac"]))
                Assert.IsTrue(Reachable(TownMap.Graph(set, WorstGates()), "home", "shelter"), string.Join(",", set));
        }

        [Test]
        public void HomeToAssemblyPointStaysReachableUnderEveryEvacClosureSet()
        {
            foreach (var set in State.CLOSURE_SETS["evac"]) Assert.IsTrue(Reachable(TownMap.Graph(set), "home", "assembly"), string.Join(",", set));
        }

        [Test]
        public void TheReachabilityCheckCanFail()
        {
            var g = TownMap.Graph(new[] { "yeonmuCenter", "danji", "sijang" }, WorstGates());
            Assert.IsFalse(Reachable(g, "home", "shelter"));
        }

        // Port check: edge lengths are Math.hypot to the bit (node values), so rounded route lengths agree.
        [Test]
        public void DistMatchesJsMathHypot()
        {
            var expect = new Dictionary<string, double>
            {
                ["aptPath"] = 21.840329667841555, ["hanbitS"] = 40.607881008493905, ["yeonmuC1"] = 37.107950630558946, ["munhwaN2"] = 25.298221281347036,
                ["cultureE"] = 11.599999999999994, ["gateEast"] = 21.400000000000006, ["gateA"] = 20.39607805437114, ["gateC"] = 24.413111231467404,
            };
            foreach (var e in TownMap.EDGES.Where(x => expect.ContainsKey(x.seg)))
                Assert.AreEqual(expect[e.seg], TownMap.Dist(TownMap.NODES[e.a], TownMap.NODES[e.b]), e.seg);
            Assert.AreEqual(0, TownMap.Dist(TownMap.NODES["home"], TownMap.NODES["home"]));
        }
    }
}
