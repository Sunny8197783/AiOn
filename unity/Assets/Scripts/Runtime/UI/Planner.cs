// Ports ui/planner.js: the preparation planner (six slots, action cards with cost, done/blocked state).
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.UI
{
    public static class Planner
    {
        // Resolves the picked action id, or null when closed.
        public static Task<string> Show(PlannerOptions o) => throw new NotImplementedException("port: ui/planner.js:planner");
    }
}
