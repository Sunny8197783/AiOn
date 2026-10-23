// Ports story/evac.js: 4장 남을까, 떠날까 (crossroads: stay and the smoke from upstairs, or move and the shelter
// fire), 5장 공식 대피 (evacuation: pack one bag, the closures, the assembly point and the buses or another
// path), and the ending (생존 기록 through ctx.ui.Ending).
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.Story
{
    public static class Evac
    {
        public static Task<string> Crossroads(Ctx ctx) => throw new NotImplementedException("port: story/evac.js:crossroads");
        public static Task<string> Evacuation(Ctx ctx) => throw new NotImplementedException("port: story/evac.js:evacuation");
        // Resolves the ending card's button id ('restart' clears the save).
        public static Task<string> Ending(Ctx ctx) => throw new NotImplementedException("port: story/evac.js:ending");
    }
}
