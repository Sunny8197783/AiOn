// Ports story/dday.js: 2장 정전과 경보 — the D-day evening at home, the 19:40 blackout, the 20:05 air-raid alert,
// leaving with (or without) the bag, the stairs, the streets toward the shelter (closures, the closed east gate,
// the strike), entering through a gate, inside the shelter, then 서연's relay (Seoyeon) and the chapter card.
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.Story
{
    public static class Dday
    {
        public const int ALERT = 20 * 60 + 5, LATE = 20 * 60 + 30;

        public static Task<string> Run(Ctx ctx) => throw new NotImplementedException("port: story/dday.js:dday");
    }
}
