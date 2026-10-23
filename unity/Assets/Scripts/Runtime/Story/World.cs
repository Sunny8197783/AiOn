// Ports story/world.js: things the player can use or look at in any chapter when the chapter has no handler
// for the point — ATMs, vending machines, the bus stop, the wallet with the ID card, and a short description
// for everything that has one (point.desc on the level, or DESC here). StoryRuntime.Roam calls this before '*'.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.Story
{
    public static class World
    {
        // "level:pointId" -> lines for the current run. Data: filled by the story porter (Korean verbatim).
        public static readonly Dictionary<string, Func<Ctx, string[]>> DESC = new Dictionary<string, Func<Ctx, string[]>>();

        public static bool IsCommon(Ctx ctx, Point point) => throw new NotImplementedException("port: story/world.js:isCommon");
        public static Task Common(Ctx ctx, Point point) => throw new NotImplementedException("port: story/world.js:common");
    }
}
