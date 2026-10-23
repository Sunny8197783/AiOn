// Ports game/endings.js: which ending a run reaches (G.know.path) and the lines that colour it.
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    public sealed class EndingFate
    {
        public string id, title;
        public List<string> lines = new List<string>();
    }

    public static class Endings
    {
        public static EndingFate PickEnding(Game G) => throw new NotImplementedException("port: game/endings.js:pickEnding");
    }
}
