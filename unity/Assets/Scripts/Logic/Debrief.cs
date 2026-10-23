// Ports game/debrief.js: fail cards, chapter summaries and the ending's 생존 기록.
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    // failDebrief(): 당시 확인한 정보 / 판단에서 놓친 부분 / 다음에 시도할 준비 + source ids.
    public sealed class FailDebriefData
    {
        public string title, seen, missed, next;
        public List<string> sourceIds = new List<string>();
    }

    // A 'payoff' event as the player sees it.
    public sealed class Payoff
    {
        public string prep, moment, id;
    }

    public sealed class ChapterSummaryData
    {
        public List<string> lines = new List<string>();
        public List<Payoff> payoffs = new List<Payoff>();
    }

    public sealed class UnusedItem
    {
        public string item, why;
    }

    public sealed class HelpLine
    {
        public string who, what;
    }

    public sealed class Practice
    {
        public string text, sourceId;
    }

    public sealed class EndingRecordData
    {
        public EndingFate fate;
        public List<Payoff> helped = new List<Payoff>();
        public List<UnusedItem> unused = new List<UnusedItem>();
        public List<string> @unchecked = new List<string>();
        public List<HelpLine> people = new List<HelpLine>();
        public Practice practice;
    }

    public static class Debrief
    {
        // Fail types: dead, late, exhausted, fire, lostWay (filled with the FAIL table).
        public static readonly List<string> FAIL_TYPES = new List<string>();
        // detail keys: dead { why }, fire { scene }.
        public static FailDebriefData FailDebrief(Game G, string type, Dictionary<string, object> detail = null) => throw new NotImplementedException("port: game/debrief.js:failDebrief");
        public static ChapterSummaryData ChapterSummary(Game G, string chapter) => throw new NotImplementedException("port: game/debrief.js:chapterSummary");
        public static EndingRecordData EndingRecord(Game G) => throw new NotImplementedException("port: game/debrief.js:endingRecord");
    }
}
