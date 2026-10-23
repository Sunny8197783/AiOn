// Ports ui/bag.js: the bag-packing minigame (home list -> bag list, weight and volume meters, goal ticks).
// The story uses the 3D version (Grab3d.Bag3d); this 2D panel is the fallback and shares GoalStatus.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.UI
{
    // goalStatus row: ok null = not checked (free mode / no keywords).
    public sealed class GoalLine
    {
        public string label;
        public bool? ok;
    }

    public static class Bag
    {
        // bagOf(id) = units in the bag; an item with ready = false does not count.
        public static List<GoalLine> GoalStatus(IList<BagGoal> goals, string mode, Func<string, int> bagOf, IList<BagItem> items) => throw new NotImplementedException("port: ui/bag.js:goalStatus");
        public static Task<BagResult> Open(BagOptions o) => throw new NotImplementedException("port: ui/bag.js:bag");
    }
}
