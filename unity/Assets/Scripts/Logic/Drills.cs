// Ports game/drills.js: minigame rules (encounter ladder, bag goals, radio tuning, extinguisher order, docs card).
// Safety text is never written here: goals and steps point at FACTS ids (game/facts.js).
using System;
using System.Collections.Generic;
using System.Linq;

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
        static List<string> L(params string[] s) => new List<string>(s);

        // --- Encounter ladder: guided -> hint -> free ---------------------------------------------

        // 'guided' | 'hint' | 'free'
        public static string LadderStage(Game G, string tool)
        {
            List<SkillRun> runs = null;
            if (G?.skill == null || tool == null || !G.skill.TryGetValue(tool, out runs) || runs == null) throw new ArgumentException($"unknown drill tool: {tool}");
            return runs.Count == 0 ? "guided" : runs.Count == 1 ? "hint" : "free";
        }

        // result: a string ("tuned", "complete", ...) or an eval object.
        public static SkillRun RecordEncounter(Game G, string tool, object result)
        {
            var stage = LadderStage(G, tool);
            var r = new SkillRun { day = G.day, minute = G.minute, stage = stage, result = result };
            G.skill[tool].Add(r);
            State.RecordEvent(G, "encounter", Details.Of("tool", tool, "result", result, "stage", stage));
            return r;
        }

        // --- Bag ------------------------------------------------------------------------------------

        static readonly List<string> WATER = L("water_500", "water_2l");
        static readonly Dictionary<string, double> LITRES = new Dictionary<string, double> { ["water_500"] = 0.5, ["water_2l"] = 2 }; // drinking water is counted in litres, not bottles
        static readonly List<string> FOOD = L("bars", "canned", "rice_instant");
        static readonly BagGoal LIGHT = new BagGoal { id = "light", label = "켜지는 손전등", items = L("flashlight"), facts = L("bag_days", "power_prep") };
        static readonly BagGoal INFO = new BagGoal { id = "info", label = "라디오 또는 충전된 보조배터리", items = L("radio", "powerbank"), facts = L("bag_days", "radio_batt", "comms_battery") };
        static readonly BagGoal MEDS = new BagGoal { id = "meds", label = "복용약·상비약", items = L("meds", "first_aid"), facts = L("bag_days", "contact_fields") };
        static readonly BagGoal WARMTH = new BagGoal { id = "warmth", label = "보온(은박담요·여벌 옷·우의)", items = L("blanket", "clothes", "raincoat"), facts = L("bag_foil", "home_clothes") };

        // Goals per context ("alert" | "water" | "evac"). min = units needed in the bag (default 1), or litres
        // when per maps item -> litres. The bag UI shows the labels and ticks a goal with the same rule (GoalMet).
        public static readonly Dictionary<string, List<BagGoal>> BAG_GOALS = new Dictionary<string, List<BagGoal>>
        {
            ["alert"] = new List<BagGoal>
            {
                new BagGoal { id = "water", label = "마실 물 2L 이상", items = WATER, min = 2, per = LITRES, facts = L("bag_days", "water_sphere") },
                new BagGoal { id = "food", label = "조리 없이 먹을 음식", items = FOOD, facts = L("bag_nocook") },
                LIGHT, INFO, MEDS,
                new BagGoal { id = "contacts", label = "연락처 카드 또는 서류 사본", items = L("contact_card", "doc_copies"), facts = L("contact_card", "bag_copies") },
                WARMTH,
            },
            ["water"] = new List<BagGoal>
            {
                new BagGoal { id = "container", label = "빈 물통", items = L("water_jug"), facts = L("water_point") },
            },
            ["evac"] = new List<BagGoal>
            {
                new BagGoal { id = "water", label = "마실 물 2L 이상", items = WATER, min = 2, per = LITRES, facts = L("bag_days", "water_sphere") },
                new BagGoal { id = "food", label = "조리 없이 먹을 음식 3개 이상", items = FOOD, min = 3, facts = L("bag_days", "bag_nocook") },
                LIGHT, INFO, MEDS,
                new BagGoal { id = "contacts", label = "비상연락처 카드", items = L("contact_card"), facts = L("contact_card", "contact_fields") },
                new BagGoal { id = "docs", label = "신분증·중요 서류 사본", items = L("id_card", "doc_copies"), facts = L("bag_copies", "docs_take") },
                new BagGoal { id = "cash", label = "현금", items = L("cash"), facts = L("bag_cash") },
                WARMTH,
                new BagGoal { id = "hygiene", label = "위생용품", items = L("hygiene_kit", "wipes", "tissue", "masks"), facts = L("bag_days") },
            },
        };

        // Why an item that fills no goal is worth a second look in a bag.
        static readonly Dictionary<string, string> EXTRA_TIPS = new Dictionary<string, string> { ["gas_stove"] = "shelter_rules", ["butane"] = "shelter_rules", ["candles"] = "candle_no", ["ramen"] = "bag_nocook", ["fridge_food"] = "food_4h", ["cooked_meat"] = "myth_meat" };
        const double WATER_TRIP_KG = 2; // game number: the way back carries up to 10 L of water

        public static bool GoalMet(BagGoal g, Func<string, double> count, Func<string, bool> ready) =>
            g.items.Where(ready).Aggregate(0.0, (s, id) => s + count(id) * (g.per != null && g.per.TryGetValue(id, out var p) ? p : 1)) >= (g.min ?? 1);

        public static BagEval EvalBag(Game G, string context)
        {
            if (context == null || !BAG_GOALS.TryGetValue(context, out var goals)) throw new ArgumentException($"unknown bag context: {context}");
            double InBag(string id) => G.items.TryGetValue(id, out var e) && e != null ? e.bag : 0;
            bool Ready(string id) => InBag(id) > 0 && (Items.CATALOG[id].needsCheck == null || G.items[id].@checked);
            var load = State.BagLoad(G);
            var r = new BagEval { weightKg = load.weightKg, volumeL = load.volumeL };
            var tips = r.tips;
            void Tip(string f) { if (!tips.Contains(f)) tips.Add(f); } // a Set kept in insertion order
            foreach (var g in goals)
            {
                if (GoalMet(g, InBag, Ready)) r.covered.Add(g.id);
                else { r.missing.Add(g.id); g.facts.ForEach(Tip); }
            }
            var wanted = new HashSet<string>(goals.SelectMany(g => g.items));
            var packed = G.items.Keys.Where(id => InBag(id) > 0).ToList();
            r.extra = packed.Where(id => !wanted.Contains(id)).ToList();
            foreach (var id in r.extra) if (EXTRA_TIPS.TryGetValue(id, out var t)) Tip(t);
            // Packed but not usable yet (bought is not usable): no batteries in, not charged.
            r.unready = packed.Where(id => Items.CATALOG[id].needsCheck != null && !G.items[id].@checked).ToList();
            var heavy = context == "water" ? load.weightKg > WATER_TRIP_KG : load.over.weight || load.over.volume;
            if (heavy) Tip(context == "water" ? "water_point" : "bag_foil");
            r.ok = r.missing.Count == 0 && !heavy;
            return r;
        }

        // --- Radio ----------------------------------------------------------------------------------

        public const double RADIO_WIDTH = 0.6; // MHz either side of a station where anything is heard

        public static RadioSignalResult RadioSignal(double freq, IList<Station> stations)
        {
            if (double.IsNaN(freq) || double.IsInfinity(freq)) throw new ArgumentException($"freq must be a number, got {Js.Num(freq)}");
            Station station = null;
            double clarity = 0;
            foreach (var s in stations ?? Array.Empty<Station>())
            {
                var c = Math.Max(0, 1 - Math.Abs(freq - s.freq) / RADIO_WIDTH);
                if (c > clarity) { clarity = c; station = s; }
            }
            return new RadioSignalResult { station = station, clarity = Js.Round(clarity * 1000) / 1000 };
        }

        // What stands between the player and the radio. An unchecked radio with spare batteries still
        // works, but loading them costs time at the worst moment.
        public static RadioNeedsResult RadioNeeds(Game G)
        {
            bool Has(string id) => G.items.TryGetValue(id, out var e) && e != null && e.home + e.bag > 0;
            if (!Has("radio")) return new RadioNeedsResult { ok = false, blocks = L("noRadio"), extraSeconds = 0, tips = L("power_prep") };
            if (G.items["radio"].@checked) return new RadioNeedsResult { ok = true, blocks = L(), extraSeconds = 0, tips = L() };
            if (!Has("batteries_aa")) return new RadioNeedsResult { ok = false, blocks = L("noBatteries"), extraSeconds = 0, tips = L("radio_batt") };
            return new RadioNeedsResult { ok = true, blocks = L("notChecked"), extraSeconds = 40, tips = L("radio_batt") };
        }

        // --- Extinguisher ---------------------------------------------------------------------------

        // Official order (fire_shout, ext_1..ext_3, ext_door).
        public static readonly List<ExtStep> EXT_STEPS = new List<ExtStep>
        {
            new ExtStep { id = "shout", label = "\"불이야!\" 외쳐 알리기", facts = L("fire_shout") },
            new ExtStep { id = "pin", label = "소화기를 옮겨 안전핀 뽑기", facts = L("ext_1") },
            new ExtStep { id = "aim", label = "바람·문을 등지고 노즐을 불 쪽으로", facts = L("ext_2", "ext_door") },
            new ExtStep { id = "squeeze", label = "손잡이 힘껏 움켜쥐기", facts = L("ext_3") },
            new ExtStep { id = "sweep", label = "빗자루로 쓸듯이 뿌리기", facts = L("ext_3") },
        };
        public static readonly List<ExtStep> EXT_CHOICES = new List<ExtStep>
        {
            new ExtStep { id = "fight", label = "소화기로 끈다", facts = L("fire_small") }, // candle-specific advice stays with the candle fire's own card
            new ExtStep { id = "retreat", label = "대피하며 알린다 (계단 · 119)", facts = L("fire_big", "fire_119") },
        };
        static readonly List<string> ORDER = EXT_STEPS.Select(s => s.id).ToList();
        static readonly List<string> OPS = ORDER.Skip(1).ToList(); // what physically puts the fire out

        // sequence: decision ('fight'|'retreat') and step ids in the order pressed. Retreating is always a
        // success; on a large fire it is the only correct choice. result 'out' needs pin→aim→squeeze→sweep
        // in order; `correct` also needs the alert first. firstError = the step/choice expected instead.
        public static ExtEval EvalExtinguisher(IList<string> sequence, string fire)
        {
            if (fire != "small" && fire != "large") throw new ArgumentException($"fire must be 'small' or 'large', got {fire ?? "undefined"}");
            if (sequence == null) throw new ArgumentException("sequence must be an array");
            foreach (var id in sequence)
                if (!ORDER.Contains(id) && id != "fight" && id != "retreat") throw new ArgumentException($"unknown extinguisher step: {id ?? "null"}");
            var decision = sequence.FirstOrDefault(id => id == "fight" || id == "retreat");
            if (sequence.Contains("retreat"))
            {
                var correct = fire == "small" || decision == "retreat";
                return new ExtEval { result = "retreat-ok", correct = correct, firstError = correct ? null : "retreat" };
            }
            if (sequence.Count == 0) return new ExtEval { result = "incomplete", correct = false, firstError = fire == "large" ? "retreat" : ORDER[0] };
            if (fire == "large") return new ExtEval { result = "escalated", correct = false, firstError = "retreat" };
            var steps = sequence.Where(id => ORDER.Contains(id)).Distinct().ToList(); // Distinct keeps first occurrences in order
            var ops = steps.Where(id => OPS.Contains(id)).ToList();
            var @out = ops.Count == OPS.Count && OPS.Select((id, i) => ops[i] == id).All(x => x);
            var firstError = ORDER.Where((id, i) => i >= steps.Count || steps[i] != id).FirstOrDefault();
            return new ExtEval { result = @out ? "out" : "incomplete", correct = firstError == null, firstError = firstError };
        }

        // --- Emergency contact card -----------------------------------------------------------------

        // why = the FACTS text of the field's first fact (JS: .map(f => ({ ...f, why: fact(f.facts[0]).text, recommended: true }))).
        static DocField Doc(string id, string label, params string[] facts) => new DocField { id = id, label = label, facts = L(facts), why = Facts.Fact(facts[0]).text, recommended = true };

        public static readonly List<DocField> DOC_FIELDS = new List<DocField>
        {
            Doc("contacts", "가족·지인 연락처", "contact_card", "contact_fields"),
            Doc("meds", "복용약·혈액형", "contact_fields"),
            Doc("shelter", "대피소 주소", "shelter_know", "contact_meet"),
            Doc("meet", "만남의 장소(1·2·3차)", "contact_meet"),
            Doc("idCopy", "신분증 사본", "bag_copies", "docs_take"),
            Doc("cash", "현금 챙기기 메모", "bag_cash"),
        };

        // card { fieldId: value } from the docs UI; where = 'bag' | 'home' | null (not given).
        public static DocsEval EvalDocs(IDictionary<string, string> card, string where = null)
        {
            bool Filled(string id) => card != null && card.TryGetValue(id, out var v) && v != null && v.Trim().Length > 0;
            var r = new DocsEval();
            r.covered = DOC_FIELDS.Where(f => Filled(f.id)).Select(f => f.id).ToList();
            r.missing = DOC_FIELDS.Where(f => f.recommended && !r.covered.Contains(f.id)).Select(f => f.id).ToList();
            r.tips = DOC_FIELDS.Where(f => r.missing.Contains(f.id)).SelectMany(f => f.facts).Distinct().ToList();
            var away = where != null && where != "bag";
            if (away && !r.tips.Contains("contact_card")) r.tips.Add("contact_card");
            r.ok = r.missing.Count == 0 && !away;
            return r;
        }
    }
}
