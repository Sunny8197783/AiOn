// Ports game/drills.js: minigame rules (encounter ladder, bag goals, radio tuning, extinguisher order, docs card).
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    // A bag goal. min = units (or litres when per maps item -> litres); default 1.
    public sealed class BagGoal
    {
        public string id, label;
        public List<string> items = new List<string>();
        public double? min;
        public Dictionary<string, double> per;
        public List<string> facts = new List<string>();
    }

    public sealed class BagEval
    {
        public bool ok;
        public double weightKg, volumeL;
        public List<string> covered = new List<string>(), missing = new List<string>(), extra = new List<string>(), unready = new List<string>(), tips = new List<string>();
    }

    // A radio station (also what ui/radio.js shows). kind: "official" | "music" | "rumor".
    public sealed class Station
    {
        public double freq;
        public string name, kind;
        public List<string> lines = new List<string>();
    }

    public sealed class RadioSignalResult
    {
        public Station station;
        public double clarity;
    }

    public sealed class RadioNeedsResult
    {
        public bool ok;
        public List<string> blocks = new List<string>();
        public int extraSeconds;
        public List<string> tips = new List<string>();
    }

    // EXT_STEPS and EXT_CHOICES entries.
    public sealed class ExtStep
    {
        public string id, label;
        public List<string> facts = new List<string>();
    }

    public sealed class ExtEval
    {
        public string result;      // "out" | "retreat-ok" | "escalated" | "incomplete"
        public bool correct;
        public string firstError;
    }

    // DOC_FIELDS entries; also the docs UI field (value is what the card says).
    public sealed class DocField
    {
        public string id, label, why, value;
        public bool recommended = true;
        public List<string> facts = new List<string>();
    }

    public sealed class DocsEval
    {
        public bool ok;
        public List<string> covered = new List<string>(), missing = new List<string>(), tips = new List<string>();
    }

    public static class Drills
    {
        // 'guided' | 'hint' | 'free'
        public static string LadderStage(Game G, string tool) => throw new NotImplementedException("port: game/drills.js:ladderStage");
        // result: a string ("tuned", "complete", ...) or an eval object.
        public static SkillRun RecordEncounter(Game G, string tool, object result) => throw new NotImplementedException("port: game/drills.js:recordEncounter");

        // Data: filled by the Logic porter ("alert" | "water" | "evac").
        public static readonly Dictionary<string, List<BagGoal>> BAG_GOALS = new Dictionary<string, List<BagGoal>>();
        public static bool GoalMet(BagGoal g, Func<string, double> count, Func<string, bool> ready) => throw new NotImplementedException("port: game/drills.js:goalMet");
        public static BagEval EvalBag(Game G, string context) => throw new NotImplementedException("port: game/drills.js:evalBag");

        public const double RADIO_WIDTH = 0.6;
        public static RadioSignalResult RadioSignal(double freq, IList<Station> stations) => throw new NotImplementedException("port: game/drills.js:radioSignal");
        public static RadioNeedsResult RadioNeeds(Game G) => throw new NotImplementedException("port: game/drills.js:radioNeeds");

        public static readonly List<ExtStep> EXT_STEPS = new List<ExtStep>();
        public static readonly List<ExtStep> EXT_CHOICES = new List<ExtStep>();
        // sequence: 'fight'|'retreat' and step ids in pressed order; fire: 'small' | 'large'.
        public static ExtEval EvalExtinguisher(IList<string> sequence, string fire) => throw new NotImplementedException("port: game/drills.js:evalExtinguisher");

        public static readonly List<DocField> DOC_FIELDS = new List<DocField>();
        // card { fieldId: value }; where 'bag' | 'home' | null.
        public static DocsEval EvalDocs(IDictionary<string, string> card, string where = null) => throw new NotImplementedException("port: game/drills.js:evalDocs");
    }
}
