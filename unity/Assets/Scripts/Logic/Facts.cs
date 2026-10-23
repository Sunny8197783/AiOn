// Ports game/facts.js: verified sources and facts. Every safety line in the game comes from here (item 9).
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    public sealed class SourceEntry
    {
        public string id, publisher, title, url, checkedAt;
    }

    public sealed class FactEntry
    {
        public string id, topic, sourceId, text;
    }

    public sealed class CiteResult
    {
        public string text, source, url;
    }

    public static class Facts
    {
        // Data tables: filled by the Logic porter from game/facts.js (Korean text verbatim, checkedAt kept).
        public static readonly List<SourceEntry> SOURCES = new List<SourceEntry>();
        public static readonly List<FactEntry> FACTS = new List<FactEntry>();

        public static FactEntry Fact(string id) => throw new NotImplementedException("port: game/facts.js:fact");
        public static SourceEntry Source(string id) => throw new NotImplementedException("port: game/facts.js:source");
        // { text, source: "publisher · title", url } or null.
        public static CiteResult Cite(string id) => throw new NotImplementedException("port: game/facts.js:cite");
    }
}
