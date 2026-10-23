// Ports tests/drills.test.mjs. Inputs JS allows but C# types cannot express ('89.1' as a frequency,
// a string as the sequence, false as a card value) are covered by their nearest typed case (null).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gyeongbo.Logic;
using NUnit.Framework;

namespace Gyeongbo.Tests
{
    public class DrillsTests
    {
        static Dictionary<string, int> Prices() => Items.CATALOG.ToDictionary(kv => kv.Key, kv => kv.Value.price);

        static Game NewGame(Dictionary<string, int> cart = null)
        {
            var G = State.CreateGame(3);
            G.money = 10000000;
            State.Buy(G, cart ?? new Dictionary<string, int>(), Prices());
            return G;
        }

        static void PackAll(Game G, Dictionary<string, int> list) { foreach (var kv in list) State.Pack(G, kv.Key, kv.Value); }
        static void AssertFacts(IEnumerable<string> ids) { foreach (var id in ids) Assert.IsNotNull(Facts.Fact(id), $"fact {id} exists"); }
        static Dictionary<string, int> Cart(params object[] kv) { var d = new Dictionary<string, int>(); for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = (int)kv[i + 1]; return d; }

        // A bag that covers every 'alert' goal with usable (checked) items.
        static Game AlertBag(Dictionary<string, int> extraCart = null)
        {
            var cart = Cart("water_500", 2, "bars", 2, "radio", 1, "batteries_aa", 2, "blanket", 1, "contact_card", 1);
            foreach (var kv in extraCart ?? new Dictionary<string, int>()) cart[kv.Key] = kv.Value;
            var G = NewGame(cart);
            Assert.IsTrue(State.Check(G, "flashlight").ok);
            Assert.IsTrue(State.Check(G, "radio").ok);
            PackAll(G, Cart("water_2l", 1, "bars", 1, "flashlight", 1, "radio", 1, "meds", 1, "contact_card", 1, "blanket", 1)); // water counts in litres: 2 L
            return G;
        }

        [Test]
        public void EveryGoalStepAndFieldPointsAtRealItemsAndFacts()
        {
            foreach (var kv in Drills.BAG_GOALS)
            {
                var goals = kv.Value;
                Assert.Greater(goals.Count, 0, kv.Key);
                Assert.AreEqual(goals.Count, goals.Select(g => g.id).Distinct().Count(), $"{kv.Key} goal ids unique");
                foreach (var g in goals)
                {
                    Assert.IsTrue(!string.IsNullOrEmpty(g.label) && g.items.Count > 0 && g.facts.Count > 0, $"{kv.Key}.{g.id}");
                    foreach (var id in g.items) Assert.IsTrue(Items.CATALOG.ContainsKey(id), $"{kv.Key}.{g.id}: {id}");
                    AssertFacts(g.facts);
                }
            }
            foreach (var s in Drills.EXT_STEPS.Concat(Drills.EXT_CHOICES)) AssertFacts(s.facts);
            foreach (var f in Drills.DOC_FIELDS) AssertFacts(f.facts);
            foreach (var kv in Items.CATALOG) foreach (var f in kv.Value.facts) Assert.IsNotNull(Facts.Fact(f), $"{kv.Key} cites {f}");
        }

        [Test]
        public void LadderStageClimbsPerToolAndRecordEncounterLogsIt()
        {
            var G = State.CreateGame(1);
            Assert.AreEqual("guided", Drills.LadderStage(G, "radio"));
            var r = Drills.RecordEncounter(G, "radio", "tuned");
            Assert.AreEqual($"-3,{17 * 60 + 30},guided,tuned", $"{r.day},{r.minute},{r.stage},{r.result}");
            var last = G.events[G.events.Count - 1];
            CollectionAssert.AreEqual(new[] { "tool", "result", "stage" }, last.details.Keys.ToList());
            Assert.AreEqual("radio,tuned,guided", $"{last.Str("tool")},{last.Get("result")},{last.Str("stage")}");
            Assert.AreEqual("encounter", last.type);
            Assert.AreEqual("hint", Drills.LadderStage(G, "radio"));
            Assert.AreEqual("hint", Drills.RecordEncounter(G, "radio", "missed").stage);
            Assert.AreEqual("free", Drills.LadderStage(G, "radio"));
            Drills.RecordEncounter(G, "radio", "tuned");
            Assert.AreEqual("free", Drills.LadderStage(G, "radio"), "stays free");
            Assert.AreEqual(3, G.skill["radio"].Count);
            foreach (var tool in new[] { "bag", "route", "extinguisher", "docs" }) Assert.AreEqual("guided", Drills.LadderStage(G, tool));
            var n = G.events.Count;
            Assert.Throws<ArgumentException>(() => Drills.LadderStage(G, "nope"));
            Assert.Throws<ArgumentException>(() => Drills.RecordEncounter(G, "nope", "x"));
            Assert.AreEqual(n, G.events.Count, "a bad tool records nothing");
            Assert.Throws<ArgumentException>(() => Drills.LadderStage(null, "radio"));
            Assert.Throws<ArgumentException>(() => Drills.LadderStage(new Game { skill = new Dictionary<string, List<SkillRun>>() }, "radio"));
        }

        [Test]
        public void EvalBagEmptyBagMissesEveryGoal()
        {
            var G = State.CreateGame(2);
            foreach (var ctx in Drills.BAG_GOALS.Keys)
            {
                var r = Drills.EvalBag(G, ctx);
                Assert.IsFalse(r.ok);
                Assert.AreEqual(0, r.covered.Count);
                CollectionAssert.AreEqual(Drills.BAG_GOALS[ctx].Select(g => g.id).ToList(), r.missing);
                Assert.AreEqual(0, r.extra.Count);
                Assert.AreEqual(0, r.weightKg);
                Assert.AreEqual(0, r.volumeL);
                Assert.Greater(r.tips.Count, 0);
                AssertFacts(r.tips);
            }
            Assert.Throws<ArgumentException>(() => Drills.EvalBag(G, "picnic"));
            Assert.Throws<ArgumentException>(() => Drills.EvalBag(G, null));
        }

        [Test]
        public void EvalBagAFullAlertBagWithCheckedGearIsOk()
        {
            var r = Drills.EvalBag(AlertBag(), "alert");
            Assert.IsTrue(r.ok);
            CollectionAssert.AreEqual(Drills.BAG_GOALS["alert"].Select(g => g.id).ToList(), r.covered);
            Assert.AreEqual(0, r.missing.Count);
            Assert.AreEqual(0, r.extra.Count);
            Assert.AreEqual(0, r.unready.Count);
            Assert.AreEqual(0, r.tips.Count);
            Assert.AreEqual(2.82, r.weightKg); // 1.29 kg of gear + one 2 L bottle instead of 500 mL
        }

        [Test]
        public void EvalBagBoughtIsNotUsableUncheckedGearDoesNotCoverAGoal()
        {
            var G = NewGame(Cart("radio", 1));
            PackAll(G, Cart("radio", 1, "flashlight", 1));
            var r = Drills.EvalBag(G, "alert");
            Assert.IsTrue(r.missing.Contains("info") && r.missing.Contains("light"));
            CollectionAssert.AreEqual(new[] { "flashlight", "radio" }, r.unready.OrderBy(x => x, StringComparer.Ordinal).ToList());
            Assert.IsTrue(r.tips.Contains("radio_batt"));
            AssertFacts(r.tips);
        }

        [Test]
        public void EvalBagExtrasGetAFactsTipOverweightFailsEvenWhenCovered()
        {
            var G = AlertBag(Cart("gas_stove", 1, "candles", 1, "water_2l", 2));
            PackAll(G, Cart("gas_stove", 1, "candles", 1, "ramen", 1));
            var r = Drills.EvalBag(G, "alert");
            CollectionAssert.AreEqual(new[] { "candles", "gas_stove", "ramen" }, r.extra.OrderBy(x => x, StringComparer.Ordinal).ToList());
            foreach (var t in new[] { "shelter_rules", "candle_no", "bag_nocook" }) Assert.IsTrue(r.tips.Contains(t), t);
            Assert.IsTrue(r.ok, "extras alone do not fail the bag");
            State.Pack(G, "water_2l", 3);
            r = Drills.EvalBag(G, "alert");
            Assert.Greater(r.weightKg, 8);
            Assert.AreEqual(0, r.missing.Count);
            Assert.IsFalse(r.ok);
            Assert.IsTrue(r.tips.Contains("bag_foil"));
            AssertFacts(r.tips);
        }

        [Test]
        public void EvalBagWaterTripAnEmptyJugAndALightBag()
        {
            var G = NewGame(Cart("water_jug", 1));
            State.Pack(G, "water_jug");
            var r = Drills.EvalBag(G, "water");
            Assert.IsTrue(r.ok);
            CollectionAssert.AreEqual(new[] { "container" }, r.covered);
            Assert.AreEqual(0, r.tips.Count);
            State.Pack(G, "water_2l");
            r = Drills.EvalBag(G, "water");
            Assert.IsFalse(r.ok, "over 2 kg before the walk back");
            CollectionAssert.AreEqual(new[] { "water_2l" }, r.extra);
            Assert.IsTrue(r.tips.Contains("water_point"));
        }

        [Test]
        public void EvalBagEvacWaterCountsInLitresFoodInUnits()
        {
            var G = NewGame(Cart("water_500", 4, "bars", 3));
            PackAll(G, Cart("water_500", 1, "bars", 2));
            var r = Drills.EvalBag(G, "evac");
            Assert.IsTrue(r.missing.Contains("water") && r.missing.Contains("food"), "half a litre is not enough water");
            PackAll(G, Cart("water_500", 3, "canned", 1));
            r = Drills.EvalBag(G, "evac");
            Assert.IsTrue(r.covered.Contains("water") && r.covered.Contains("food"));
            AssertFacts(r.tips);
        }

        [Test]
        public void RadioSignalClarityFallsOffWithinWidthAndPicksTheNearestStation()
        {
            var a = new Station { freq = 89.1, name = "A" };
            var b = new Station { freq = 90.0, name = "B" };
            Assert.AreEqual(0.6, Drills.RADIO_WIDTH);
            void Same(RadioSignalResult r, Station s, double c, string msg = null) { Assert.IsTrue(ReferenceEquals(s, r.station), msg); Assert.AreEqual(c, r.clarity, msg); }
            Same(Drills.RadioSignal(89.1, new[] { a, b }), a, 1);
            Same(Drills.RadioSignal(89.4, new[] { a }), a, 0.5);
            Same(Drills.RadioSignal(89.7, new[] { a }), null, 0, "edge of the band is silent");
            Same(Drills.RadioSignal(95.0, new[] { a, b }), null, 0);
            Assert.IsTrue(ReferenceEquals(b, Drills.RadioSignal(89.7, new[] { a, b }).station));
            Assert.IsTrue(ReferenceEquals(a, Drills.RadioSignal(89.5, new[] { a, b }).station));
            foreach (var s in new[] { new Station[0], null }) Same(Drills.RadioSignal(89.1, s), null, 0);
            foreach (var f in new[] { double.NaN, double.PositiveInfinity }) Assert.Throws<ArgumentException>(() => Drills.RadioSignal(f, new[] { a }));
        }

        [Test]
        public void RadioNeedsNoRadioNoBatteriesUncheckedChecked()
        {
            string N(RadioNeedsResult r) => $"{r.ok}|{string.Join(",", r.blocks)}|{r.extraSeconds}|{string.Join(",", r.tips)}";
            var G = State.CreateGame(4);
            G.money = 10000000;
            Assert.AreEqual("False|noRadio|0|power_prep", N(Drills.RadioNeeds(G)));
            State.Buy(G, Cart("radio", 1), Prices());
            CollectionAssert.AreEqual(new[] { "noBatteries" }, Drills.RadioNeeds(G).blocks);
            Assert.IsFalse(Drills.RadioNeeds(G).ok);
            State.Buy(G, Cart("batteries_aa", 1), Prices());
            State.Pack(G, "radio");
            Assert.AreEqual("True|notChecked|40|radio_batt", N(Drills.RadioNeeds(G)), "radio in the bag counts");
            State.Check(G, "radio");
            Assert.AreEqual("True||0|", N(Drills.RadioNeeds(G)));
        }

        [Test]
        public void ExtStepsKeepTheOfficialOrderAndChoicesAreFightRetreat()
        {
            CollectionAssert.AreEqual(new[] { "shout", "pin", "aim", "squeeze", "sweep" }, Drills.EXT_STEPS.Select(s => s.id).ToList());
            CollectionAssert.AreEqual(new[] { "fight", "retreat" }, Drills.EXT_CHOICES.Select(c => c.id).ToList());
            foreach (var s in Drills.EXT_STEPS.Concat(Drills.EXT_CHOICES)) Assert.IsTrue(Regex.IsMatch(s.label, "[가-힣]"));
        }

        static string E(ExtEval e) => $"{e.result}|{e.correct}|{e.firstError ?? "null"}";
        static ExtEval Ext(string fire, params string[] seq) => Drills.EvalExtinguisher(seq, fire);

        [Test]
        public void EvalExtinguisherSmallFireRightAndWrongOrders()
        {
            Assert.AreEqual("out|True|null", E(Ext("small", "fight", "shout", "pin", "aim", "squeeze", "sweep")));
            Assert.AreEqual("out|False|shout", E(Ext("small", "fight", "pin", "aim", "squeeze", "sweep")), "out, but nobody was alerted");
            Assert.AreEqual("incomplete|False|pin", E(Ext("small", "shout", "aim", "pin", "squeeze", "sweep")));
            Assert.AreEqual("incomplete|False|sweep", E(Ext("small", "shout", "pin", "aim", "squeeze")));
            Assert.AreEqual("out", Ext("small", "shout", "pin", "pin", "aim", "squeeze", "sweep").result, "repeated presses are ignored");
            Assert.AreEqual("incomplete|False|shout", E(Ext("small", "fight")));
            Assert.AreEqual("incomplete|False|shout", E(Ext("small")));
        }

        [Test]
        public void EvalExtinguisherRetreatingIsAlwaysASuccessFightingALargeFireIsNot()
        {
            foreach (var fire in new[] { "small", "large" }) Assert.AreEqual("retreat-ok|True|null", E(Ext(fire, "retreat")));
            Assert.AreEqual("retreat-ok", Ext("small", "shout", "retreat").result);
            Assert.AreEqual("escalated|False|retreat", E(Ext("large", "fight", "shout", "pin", "aim", "squeeze", "sweep")));
            Assert.AreEqual("retreat-ok|False|retreat", E(Ext("large", "fight", "pin", "retreat")), "late retreat still gets out");
            Assert.AreEqual("incomplete|False|retreat", E(Ext("large")));
        }

        [Test]
        public void EvalExtinguisherRejectsBadInput()
        {
            Assert.Throws<ArgumentException>(() => Drills.EvalExtinguisher(new string[0], null));
            Assert.Throws<ArgumentException>(() => Drills.EvalExtinguisher(new string[0], "medium"));
            Assert.Throws<ArgumentException>(() => Drills.EvalExtinguisher(null, "small"));
            Assert.Throws<ArgumentException>(() => Drills.EvalExtinguisher(new[] { "pin", "water" }, "small"));
        }

        [Test]
        public void DocFieldsSixRecommendedFieldsEachExplainedByItsFactsText()
        {
            CollectionAssert.AreEqual(new[] { "contacts", "meds", "shelter", "meet", "idCopy", "cash" }, Drills.DOC_FIELDS.Select(f => f.id).ToList());
            foreach (var f in Drills.DOC_FIELDS)
            {
                Assert.IsTrue(f.recommended);
                Assert.AreEqual(Facts.Fact(f.facts[0]).text, f.why);
                Assert.IsTrue(Regex.IsMatch(f.label, "[가-힣]"));
            }
        }

        [Test]
        public void EvalDocsFullCardInTheBagCardLeftAtHomeEmptyAndBlankValues()
        {
            var ids = Drills.DOC_FIELDS.Select(f => f.id).ToList();
            var full = ids.ToDictionary(id => id, id => "적음");
            var r = Drills.EvalDocs(full, "bag");
            Assert.IsTrue(r.ok);
            CollectionAssert.AreEqual(ids, r.covered);
            Assert.AreEqual(0, r.missing.Count);
            Assert.AreEqual(0, r.tips.Count);
            Assert.IsTrue(Drills.EvalDocs(full).ok, "where is optional");
            r = Drills.EvalDocs(full, "home");
            Assert.IsFalse(r.ok);
            CollectionAssert.AreEqual(ids, r.covered);
            Assert.AreEqual(0, r.missing.Count);
            CollectionAssert.AreEqual(new[] { "contact_card" }, r.tips);
            var cards = new[] { null, new Dictionary<string, string>(), new Dictionary<string, string> { ["contacts"] = "   ", ["meds"] = null, ["shelter"] = null, ["meet"] = "" } };
            foreach (var card in cards)
            {
                r = Drills.EvalDocs(card, "bag");
                Assert.IsFalse(r.ok);
                Assert.AreEqual(0, r.covered.Count);
                CollectionAssert.AreEqual(ids, r.missing);
                AssertFacts(r.tips);
            }
            r = Drills.EvalDocs(new Dictionary<string, string> { ["contacts"] = "엄마 010", ["meds"] = "0" }, "bag");
            CollectionAssert.AreEqual(new[] { "contacts", "meds" }, r.covered);
            Assert.IsFalse(r.missing.Contains("contacts"));
        }
    }
}
