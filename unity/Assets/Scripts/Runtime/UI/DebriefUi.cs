// Ports ui/debrief.js: the soft-fail card (retry), the chapter card, and the ending's 생존 기록.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.UI
{
    public static class DebriefUi
    {
        // Resolves 'retry' | 'menu'.
        public static Task<string> FailCard(FailCardOptions o) => throw new NotImplementedException("port: ui/debrief.js:failCard");
        public static Task ChapterCard(ChapterCardOptions o) => throw new NotImplementedException("port: ui/debrief.js:chapterCard");
        // Resolves the button id ('restart' | 'menu').
        public static Task<string> Ending(EndingRecordData record, IList<SourceEntry> sources) => throw new NotImplementedException("port: ui/debrief.js:ending");
    }
}
