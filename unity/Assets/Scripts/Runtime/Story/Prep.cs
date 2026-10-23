// Ports story/prep.js: 1장 준비 — six slots of time from D-2 evening to D-1 evening, the planner, each action
// (PrepActions), free time at home between slots, the evening before the alert.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.Story
{
    public static class Prep
    {
        // Planner actions (mart shelter equip docs neighbors bag rest). Data: filled by the story porter.
        public static readonly List<PlannerAction> ACTIONS = new List<PlannerAction>();

        public static Task<string> Run(Ctx ctx) => throw new NotImplementedException("port: story/prep.js:prep");
    }
}
