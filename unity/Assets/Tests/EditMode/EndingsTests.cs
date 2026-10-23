// Ports tests/endings.test.mjs.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gyeongbo.Logic;
using NUnit.Framework;

namespace Gyeongbo.Tests
{
    public class EndingsTests
    {
        [Test]
        public void EachLastDayPathHasItsOwnEndingColouredByTheRun()
        {
            var titles = new HashSet<string>();
            foreach (var path in new[] { "bus", "missed", "stay", "seoyeon", "volunteer", "walk" })
            {
                var G = State.CreateGame(3);
                G.know.path = path;
                var e = Endings.PickEnding(G);
                Assert.AreEqual(path, e.id);
                titles.Add(e.title);
                Assert.IsTrue(e.lines.Count >= 1 && e.lines.All(l => Regex.IsMatch(l, "[가-힣]")));
            }
            Assert.AreEqual(6, titles.Count);
            var W = State.CreateGame(3);
            W.know.path = "walk";
            W.injury = new List<string> { "leg" };
            W.karma = -4;
            var w = Endings.PickEnding(W);
            Assert.IsTrue(w.lines.Any(l => l.Contains("구호 트럭")) && w.lines.Any(l => l.Contains("소문")));
            Assert.AreEqual("bus", Endings.PickEnding(State.CreateGame(3)).id); // no path yet: the default
        }
    }
}
