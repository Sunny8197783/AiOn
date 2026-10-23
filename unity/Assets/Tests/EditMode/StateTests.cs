// Ports tests/state.test.mjs. JSON text (save/load parse errors) belongs to Core.GameJson, so the round trip
// here is State.Load(G.Clone()); cases JS types allow but C# types cannot express (seed 'x', qty 1.5) are left out.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gyeongbo.Logic;
using NUnit.Framework;

namespace Gyeongbo.Tests
{
    public class StateTests
    {
        static Dictionary<string, int> MartPrices() => Items.CATALOG.Where(kv => !kv.Value.notSold && !kv.Value.atmOnly).ToDictionary(kv => kv.Key, kv => kv.Value.price);

        // Structural dump of plain data (public fields, lists, dictionaries) for deepEqual.
        internal static string Dump(object o)
        {
            switch (o)
            {
                case null: return "null";
                case string s: return "\"" + s + "\"";
                case bool b: return b ? "true" : "false";
                case IDictionary d: return "{" + string.Join(",", d.Keys.Cast<object>().Select(k => k + ":" + Dump(d[k]))) + "}";
                case IEnumerable e: return "[" + string.Join(",", e.Cast<object>().Select(Dump)) + "]";
            }
            var t = o.GetType();
            if (t.IsPrimitive || o is decimal) return Convert.ToDouble(o).ToString("R");
            return t.Name + "{" + string.Join(",", t.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name + ":" + Dump(f.GetValue(o)))) + "}";
        }

        static void Item(ItemEntry e, int home, int bag, int bought, bool chk, int used, string msg = null) =>
            Assert.AreEqual($"{home},{bag},{bought},{chk},{used}", $"{e.home},{e.bag},{e.bought},{e.@checked},{e.used}", msg);

        static T Throws<T>(TestDelegate code, string part) where T : Exception
        {
            var e = Assert.Throws<T>(code);
            StringAssert.Contains(part, e.Message);
            return e;
        }

        [Test]
        public void CreateGameStartsFromTheSpecStateAndStartHome()
        {
            var G = State.CreateGame(7);
            Assert.AreEqual(2, G.v);
            Assert.AreEqual(7u, G.seed);
            Assert.AreEqual("D-3 17:30", State.FmtClock(G));
            Assert.AreEqual("6,0,0", $"{G.slots.total},{G.slots.used},{G.slots.done.Count}");
            Assert.AreEqual(150000, G.money);
            Assert.AreEqual("100,10,20,25", $"{G.stats.health},{G.stats.thirst},{G.stats.fatigue},{G.stats.tension}");
            CollectionAssert.AreEqual(Items.START_HOME.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(), G.items.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
            foreach (var kv in Items.START_HOME) Item(G.items[kv.Key], kv.Value, 0, 0, false, 0, kv.Key);
            Assert.IsFalse(State.Usable(G, "flashlight"), "owned flashlight has no batteries");
            Assert.IsTrue(State.Usable(G, "water_2l"));
            Assert.IsFalse(State.Usable(G, "radio"));
            Assert.AreEqual(Dump(State.Conditions(7)), Dump(G.cond));
            Assert.IsNull(G.know.meetPoint);
            Assert.AreEqual(0, G.rel["seoyeon"].trust);
            Assert.IsNull(G.rel["seoyeon"].contact);
            CollectionAssert.AreEqual(new[] { "radio", "bag", "route", "extinguisher", "docs" }, G.skill.Keys.ToList());
            Assert.IsNull(G.checkpoint);
            State.CreateGame(); // random seed when none is given
        }

        [Test]
        public void ConditionsAreDeterministicAndKeepTheSafeFactsFixed()
        {
            var seen = new HashSet<string>();
            for (uint s = 1; s <= 500; s++)
            {
                var a = State.Conditions(s);
                Assert.AreEqual(Dump(State.Conditions(s)), Dump(a));
                Assert.IsTrue(a.closures.dday.Contains("yeonmuCenter"));
                Assert.IsTrue(a.eastGateClosed);
                Assert.IsFalse(a.rumorTrue);
                Assert.AreEqual("20:16", a.accessOpensAt);
                Assert.AreEqual("cafe", a.seoyeonAt);
                Assert.IsTrue(a.commsWeakAt > 19 * 60 + 40 && a.commsWeakAt < 20 * 60 + 5);
                Assert.IsTrue(a.soldOut.late.Contains("water"));
                seen.Add(Dump(a));
            }
            Assert.Greater(seen.Count, 10, "seeds vary the situation");
        }

        // Port check: the C# mulberry32 and conditions() give the numbers node gives for the same seeds.
        [Test]
        public void RngAndConditionsMatchTheJsNumbers()
        {
            var r = State.Rng(1);
            Assert.AreEqual(0.6270739405881613, r());
            Assert.AreEqual(0.002735721180215478, r());
            Assert.AreEqual(0.5274470399599522, r());
            var q = State.Rng(4294967295);
            Assert.AreEqual(0.8964226141106337, q());
            Assert.AreEqual(0.189478256739676, q());
            string C(Cond c) => $"{string.Join("+", c.closures.dday)}/{string.Join("+", c.closures.evac)}/{c.commsWeakAt}/{string.Join("+", c.soldOut.early)}/{string.Join("+", c.soldOut.late)}/{c.waterAt}/{c.evacNoticeAt}";
            Assert.AreEqual("yeonmuCenter+danji/yeonmuCenter+sijang/1190/water/water+elec+food/570/465", C(State.Conditions(1)));
            Assert.AreEqual("yeonmuCenter/yeonmuCenter+sijang/1195/water/water+elec+food/570/450", C(State.Conditions(7)));
            Assert.AreEqual("yeonmuCenter+danji/danji+munhwaNorth/1194/water/water+elec/600/435", C(State.Conditions(42)));
            Assert.AreEqual("yeonmuCenter/yeonmuCenter+munhwaNorth/1191/water/water+elec/540/465", C(State.Conditions(2026)));
            Assert.AreEqual("yeonmuCenter+danji/yeonmuCenter+sijang/1192/water/water+elec+food/600/465", C(State.Conditions(4294967295)));
        }

        [Test]
        public void RecordEventNumbersEventsAndStampsTimeAndChapter()
        {
            var G = State.CreateGame(1);
            var a = State.RecordEvent(G, "payoff", Details.Of("prep", "x", "moment", "y"));
            var b = State.RecordEvent(G, "help");
            Assert.AreEqual(1, a.seq);
            Assert.AreEqual(2, b.seq);
            Assert.AreEqual("prologue", a.chapter);
            Assert.AreEqual(-3, a.day);
            Assert.AreEqual(0, b.details.Count);
            Assert.Throws<ArgumentException>(() => State.RecordEvent(G, ""));
        }

        [Test]
        public void FmtClockAndHhmm()
        {
            var G = State.CreateGame(1);
            G.day = 0; G.minute = 20 * 60 + 5;
            Assert.AreEqual("D-day 20:05", State.FmtClock(G));
            G.day = 12; G.minute = 7;
            Assert.AreEqual("D+12 00:07", State.FmtClock(G));
            Assert.AreEqual("15:20", State.Hhmm(15 * 60 + 20));
        }

        [Test]
        public void AdvanceMovesTheClockAcrossDaysAndDriftsStats()
        {
            var G = State.CreateGame(1);
            State.Advance(G, 60);
            Assert.AreEqual("D-3 18:30", State.FmtClock(G));
            Assert.AreEqual(13, G.stats.thirst);
            Assert.AreEqual(22, G.stats.fatigue);
            for (int i = 0; i < 10; i++) State.Advance(G, 2); // small steps add up like one long step
            Assert.AreEqual(14, G.stats.thirst);
            State.Advance(G, 6 * 60);
            Assert.AreEqual("D-2 00:50", State.FmtClock(G));
            var f = G.stats.fatigue;
            State.Advance(G, 120, awake: false);
            Assert.AreEqual(f, G.stats.fatigue);
            State.Advance(G, 0);
            Assert.Throws<ArgumentException>(() => State.Advance(G, -5));
            Assert.Throws<ArgumentException>(() => State.Advance(G, double.NaN));
        }

        [Test]
        public void AdjustClampsAndRejectsUnknownStats()
        {
            var G = State.CreateGame(1);
            Assert.AreEqual(100, State.Adjust(G, "health", 50));
            Assert.AreEqual(0, State.Adjust(G, "thirst", -99));
            Assert.AreEqual(35, State.Adjust(G, "tension", 10));
            Assert.Throws<ArgumentException>(() => State.Adjust(G, "mood", 1));
            Assert.Throws<ArgumentException>(() => State.Adjust(G, "health", double.NaN));
        }

        [Test]
        public void SpendSlotSpendsUntilThePlannerIsFull()
        {
            var G = State.CreateGame(1);
            Assert.AreEqual(4, State.SpendSlot(G, "mart", 2));
            Assert.AreEqual(3, State.SpendSlot(G, "bag"));
            CollectionAssert.AreEqual(new[] { "mart", "bag" }, G.slots.done);
            Throws<InvalidOperationException>(() => State.SpendSlot(G, "x", 4), "not enough slots");
            Assert.Throws<ArgumentException>(() => State.SpendSlot(G, "x", 0));
            Assert.AreEqual(3, G.slots.used);
            Assert.AreEqual(2, G.events.Count(e => e.type == "slot"));
        }

        [Test]
        public void BuyChecksStockAndMoneyAddsToHomeAndBought()
        {
            var G = State.CreateGame(1);
            var mart = MartPrices();
            Assert.AreEqual(24000 + 9000, State.Buy(G, new Dictionary<string, int> { ["radio"] = 1, ["batteries_aa"] = 2, ["water_2l"] = 0 }, mart));
            Assert.AreEqual(150000 - 33000, G.money);
            Item(G.items["radio"], 1, 0, 1, false, 0);
            Assert.AreEqual(Items.START_HOME["water_2l"], G.items["water_2l"].home);
            Throws<InvalidOperationException>(() => State.Buy(G, new Dictionary<string, int> { ["radio"] = 1 }, Items.STORE_ITEMS), "not in stock");
            Throws<ArgumentException>(() => State.Buy(G, new Dictionary<string, int> { ["nope"] = 1 }, mart), "unknown item");
            Assert.Throws<ArgumentException>(() => State.Buy(G, new Dictionary<string, int> { ["water_2l"] = -1 }, mart));
            Throws<InvalidOperationException>(() => State.Buy(G, new Dictionary<string, int> { ["gas_stove"] = 10 }, mart), "not enough money");
            Assert.AreEqual(117000, G.money, "failed purchases change nothing");
            Assert.AreEqual(0, State.Buy(G, new Dictionary<string, int>(), mart));
            Assert.AreEqual(0, State.Buy(G, null, mart));
        }

        [Test]
        public void CashComesFromAnAtmAndLivesInItems()
        {
            var G = State.CreateGame(1);
            State.Buy(G, new Dictionary<string, int> { ["cash"] = 1 }, new Dictionary<string, int> { ["cash"] = Items.CATALOG["cash"].price });
            Assert.AreEqual(100000, G.money);
            Assert.AreEqual(1, G.items["cash"].home);
            Assert.IsTrue(State.Usable(G, "cash"));
        }

        [Test]
        public void PackUnpackAndBagLoad()
        {
            var G = State.CreateGame(1);
            string L(BagLoadResult b) => $"{b.weightKg},{b.volumeL},{b.over.weight},{b.over.volume}";
            Assert.AreEqual("0,0,False,False", L(State.BagLoad(G)));
            State.Pack(G, "water_2l", 2);
            State.Pack(G, "clothes");
            Assert.AreEqual(0, G.items["water_2l"].home);
            Assert.AreEqual(2, G.items["water_2l"].bag);
            Assert.AreEqual("4.7,7.4,False,False", L(State.BagLoad(G)));
            Throws<InvalidOperationException>(() => State.Pack(G, "water_2l", 1), "only 0");
            Throws<InvalidOperationException>(() => State.Pack(G, "radio", 1), "only 0");
            Throws<ArgumentException>(() => State.Pack(G, "nope", 1), "unknown item");
            State.Unpack(G, "water_2l");
            Assert.AreEqual(1, G.items["water_2l"].home);
            State.Buy(G, new Dictionary<string, int> { ["water_2l"] = 4 }, MartPrices());
            State.Pack(G, "water_2l", 5);
            Assert.IsTrue(State.BagLoad(G).over.weight);
            Assert.Greater(State.BagLoad(G).weightKg, Items.BAG_LIMIT.weightKg);
        }

        [Test]
        public void CheckTurnsOwnedIntoUsableBatteriesAreUsedUp()
        {
            var G = State.CreateGame(1);
            var r = State.Check(G, "radio");
            Assert.IsFalse(r.ok);
            Assert.AreEqual("가지고 있지 않습니다.", r.reason);
            Assert.IsFalse(State.Check(G, "flashlight").ok, "no batteries yet");
            State.Buy(G, new Dictionary<string, int> { ["batteries_aa"] = 1, ["radio"] = 1, ["powerbank"] = 1 }, MartPrices());
            r = State.Check(G, "flashlight");
            Assert.IsTrue(r.ok);
            Assert.IsNull(r.reason);
            Assert.IsTrue(State.Usable(G, "flashlight"));
            Assert.AreEqual(0, G.items["batteries_aa"].home);
            Assert.AreEqual(1, G.items["batteries_aa"].used);
            Assert.IsFalse(State.Check(G, "radio").ok, "the one pack is gone");
            Assert.IsTrue(State.Check(G, "flashlight").ok, "checking twice is free");
            Assert.IsTrue(State.Check(G, "powerbank").ok);
            Assert.IsTrue(State.Usable(G, "powerbank"));
            Assert.IsTrue(State.Check(G, "water_2l").ok);
            Assert.Throws<ArgumentException>(() => State.Check(G, "nope"));
        }

        [Test]
        public void ConsumeAndUse()
        {
            var G = State.CreateGame(1);
            State.Pack(G, "ramen", 2);
            State.Consume(G, "ramen", 1, "bag");
            State.Consume(G, "ramen", 3);
            Item(G.items["ramen"], 0, 1, 0, false, 4);
            Throws<InvalidOperationException>(() => State.Consume(G, "ramen", 1), "only 0");
            Assert.Throws<ArgumentException>(() => State.Consume(G, "ramen", 1, "car"));
            Assert.Throws<ArgumentException>(() => State.Consume(G, "ramen", 0, "bag"));
            Throws<InvalidOperationException>(() => State.Use(G, "flashlight"), "not usable");
            State.Buy(G, new Dictionary<string, int> { ["batteries_aa"] = 1 }, MartPrices());
            State.Check(G, "flashlight");
            State.Use(G, "flashlight");
            Assert.AreEqual(1, G.items["flashlight"].used);
            Assert.AreEqual(1, G.items["flashlight"].home);
        }

        [Test]
        public void NoteRecordsLabelledInfoOnce()
        {
            var G = State.CreateGame(1);
            var n = State.Note(G, "  급수차 9시  ", "official", "centerNotice");
            Assert.AreEqual("prologue,-3,1050,급수차 9시,official,centerNotice", $"{n.chapter},{n.day},{n.minute},{n.text},{n.label},{n.place}");
            Assert.IsTrue(ReferenceEquals(n, State.Note(G, "급수차 9시", "official")));
            State.Note(G, "급수차 9시", "rumor");
            Assert.AreEqual(2, G.info.Count);
            Assert.Throws<ArgumentException>(() => State.Note(G, "", "self"));
            Assert.Throws<ArgumentException>(() => State.Note(G, "x", "gossip"));
        }

        [Test]
        public void SpeedMulStaysWithinRangeAndFallsWithFatigueAndLoad()
        {
            var G = State.CreateGame(1);
            var fresh = State.SpeedMul(G);
            Assert.AreEqual(1, fresh);
            State.Adjust(G, "fatigue", 80);
            var tired = State.SpeedMul(G);
            Assert.IsTrue(tired < fresh && tired >= 0.6);
            Assert.AreEqual(0.75, tired); // node: 0.75
            State.Buy(G, new Dictionary<string, int> { ["water_2l"] = 20 }, MartPrices());
            State.Pack(G, "water_2l", 22);
            Assert.AreEqual(tired, State.SpeedMul(G), "a bag left by the door slows nobody");
            G.know.bagOn = true;
            Assert.AreEqual(0.6, State.SpeedMul(G));
        }

        [Test]
        public void SaveLoadRoundTripAndValidation()
        {
            var G = State.CreateGame(3);
            State.Buy(G, new Dictionary<string, int> { ["radio"] = 1 }, MartPrices());
            State.Note(G, "라디오 샀음", "self");
            State.Checkpoint(G, "ch1");
            Assert.AreEqual(Dump(G), Dump(State.Load(G.Clone())));
            Throws<FormatException>(() => State.Load(null), "invalid save");
            Game With(Action<Game> edit) { var c = G.Clone(); edit(c); return c; }
            Throws<FormatException>(() => State.Load(With(c => c.v = 1)), "invalid save");
            Throws<FormatException>(() => State.Load(With(c => c.stats.health = 300)), "stats");
            Throws<FormatException>(() => State.Load(With(c => c.items = new Dictionary<string, ItemEntry> { ["radio"] = new ItemEntry { home = -1 } })), "item radio");
            Throws<FormatException>(() => State.Load(With(c => c.info = new List<InfoNote> { new InfoNote { text = null, label = "self" } })), "info");
            Throws<FormatException>(() => State.Load(With(c => c.checkpoint.state.minute = 2000)), "clock");
            var old = G.Clone();
            old.items["retired_item"] = new ItemEntry { home = 1 };
            Assert.IsFalse(State.Load(old).items.ContainsKey("retired_item"), "items removed from the catalogue are dropped");
        }

        [Test]
        public void CheckpointAndRestoreRollBackInPlace()
        {
            var G = State.CreateGame(5);
            var reference = G;
            State.Checkpoint(G, "dday");
            State.Buy(G, new Dictionary<string, int> { ["radio"] = 1 }, MartPrices());
            State.Advance(G, 90);
            State.RecordEvent(G, "payoff", new Dictionary<string, object>());
            State.Restore(G);
            Assert.IsTrue(ReferenceEquals(reference, G));
            Assert.AreEqual(150000, G.money);
            Assert.IsFalse(G.items.ContainsKey("radio"));
            Assert.AreEqual(0, G.events.Count);
            Assert.AreEqual("dday", G.checkpoint.key);
            G.money = 1;
            State.Restore(G);
            Assert.AreEqual(150000, G.money, "can retry twice from the same checkpoint");
            Throws<InvalidOperationException>(() => State.Restore(State.CreateGame(1)), "no checkpoint");
            Assert.Throws<ArgumentException>(() => State.Checkpoint(G, ""));
        }

        // Port check: Clone is a structuredClone, event details included (Checkpoint/Restore depend on it).
        [Test]
        public void CloneCopiesDeeplyIncludingEventDetails()
        {
            var G = State.CreateGame(2);
            State.RecordEvent(G, "x", Details.Of("list", new List<object> { 1L, "a" }, "map", new Dictionary<string, object> { ["k"] = 2.5 }));
            Drills.RecordEncounter(G, "extinguisher", Drills.EvalExtinguisher(new[] { "retreat" }, "small"));
            G.injury = new List<string> { "leg" };
            var c = G.Clone();
            Assert.AreEqual(Dump(G), Dump(c));
            ((List<object>)c.events[0].details["list"]).Add("b");
            ((Dictionary<string, object>)c.events[0].details["map"])["k"] = 9.0;
            ((ExtEval)c.skill["extinguisher"][0].result).result = "out";
            c.injury.Add("arm");
            c.items["water_2l"].home = 99;
            Assert.AreEqual(2, ((List<object>)G.events[0].details["list"]).Count);
            Assert.AreEqual(2.5, ((Dictionary<string, object>)G.events[0].details["map"])["k"]);
            Assert.AreEqual("retreat-ok", ((ExtEval)G.skill["extinguisher"][0].result).result);
            Assert.AreEqual(1, G.injury.Count);
            Assert.AreEqual(2, G.items["water_2l"].home);
        }

        [Test]
        public void ABrokenLegSlowsTheRunForGood()
        {
            var G = State.CreateGame(4);
            var b = State.SpeedMul(G);
            G.injury = new List<string> { "leg" };
            Assert.AreEqual(Math.Floor(b * 0.7 * 100 + 0.5) / 100, State.SpeedMul(G));
            CollectionAssert.AreEqual(new[] { "leg" }, State.Load(G.Clone()).injury);
        }
    }
}
