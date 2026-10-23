// Ports game/state.js: the functions that change G. Pure logic, no Unity.
// JSON text (save/load) lives in Runtime Core.GameJson (Logic has no JSON library); Load(Game) here is
// the validation half of state.js load(): it checks a parsed G and drops items no longer in the catalogue.
// Errors keep the JS messages (tests match on them): ArgumentException for bad input, InvalidOperationException
// for impossible moves, FormatException("invalid save: ...") for a bad save.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gyeongbo.Logic
{
    public sealed class BagLoadResult
    {
        public double weightKg, volumeL;
        public BagOver over = new BagOver();
    }

    public sealed class BagOver
    {
        public bool weight, volume;
    }

    public sealed class CheckResult
    {
        public bool ok;
        public string reason;
    }

    public static class State
    {
        public static readonly string[] STATS = { "health", "thirst", "fatigue", "tension" };
        public static readonly string[] LABELS = { "official", "site", "neighbor", "rumor", "self" };
        public static readonly string[] TOOLS = { "radio", "bag", "route", "extinguisher", "docs" };
        public static readonly string[] PEOPLE = { "manager", "clerk", "seoyeon" };

        // Every closure set conditions() may pick. Each keeps home -> shelter reachable on foot with only
        // the stairs and steep gates open, and home -> assembly reachable (TownMapTests proves it).
        public static readonly Dictionary<string, List<List<string>>> CLOSURE_SETS = new Dictionary<string, List<List<string>>>
        {
            ["dday"] = new List<List<string>> { L("yeonmuCenter"), L("yeonmuCenter", "danji") },
            ["evac"] = new List<List<string>> { L("yeonmuCenter", "sijang"), L("yeonmuCenter", "munhwaNorth"), L("danji", "munhwaNorth"), L("sijang", "munhwaSouth"), L("danji", "sijang") },
        };

        static List<string> L(params string[] s) => new List<string>(s);

        // mulberry32, same as engine/kit.js rng.
        public static Func<double> Rng(uint seed)
        {
            uint a = seed;
            return () =>
            {
                unchecked
                {
                    a += 0x6D2B79F5;
                    uint t = a;
                    t = (t ^ (t >> 15)) * (t | 1); // Math.imul: 32-bit wrap-around multiply
                    t ^= t + (t ^ (t >> 7)) * (t | 61);
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            };
        }

        // Situations that vary per seed. They change where and when, never what the safe action is.
        public static Cond Conditions(uint seed)
        {
            var r = Rng(seed);
            List<string> Pick(List<List<string>> list) => new List<string>(list[(int)Math.Floor(r() * list.Count)]);
            var c = new Cond();
            c.closures = new ClosurePlan { dday = Pick(CLOSURE_SETS["dday"]), evac = Pick(CLOSURE_SETS["evac"]) };
            c.eastGateClosed = true;
            c.accessOpensAt = "20:16";
            c.commsWeakAt = 19 * 60 + 44 + (int)Math.Floor(r() * 12); // D-day, after the 19:40 blackout
            var early = r() < 0.5 ? L() : L("water");
            var late = r() < 0.5 ? L("water", "elec") : L("water", "elec", "food");
            c.soldOut = new SoldOut { early = early, late = late }; // MART_SECTIONS ids
            c.rumorTrue = false;
            c.waterAt = 9 * 60 + 30 * (int)Math.Floor(r() * 4); // D+1
            c.evacNoticeAt = 7 * 60 + 15 * (int)Math.Floor(r() * 5); // D+12
            c.seoyeonAt = "cafe";
            return c;
        }

        static ItemEntry NewItem(int home = 0) => new ItemEntry { home = home };

        // seed null = random (JS default Math.random() * 2^31).
        public static Game CreateGame(uint? seed = null)
        {
            uint s = seed ?? (uint)new Random().Next();
            var G = new Game { v = 2, seed = s, chapter = "prologue", day = -3, minute = 17 * 60 + 30 };
            G.slots = new Slots { total = 6, used = 0 };
            G.money = 150000;
            G.stats = new Stats { health = 100, thirst = 10, fatigue = 20, tension = 25 };
            G.phone = new PhoneState { battery = 64, signal = "ok" };
            G.items = new Dictionary<string, ItemEntry>();
            foreach (var kv in Items.START_HOME) G.items[kv.Key] = NewItem(kv.Value);
            G.know = new Know();
            G.rel = new Dictionary<string, Rel>();
            foreach (var p in PEOPLE) G.rel[p] = new Rel { trust = 0, contact = null };
            G.skill = new Dictionary<string, List<SkillRun>>();
            foreach (var t in TOOLS) G.skill[t] = new List<SkillRun>();
            G.info = new List<InfoNote>();
            G.cond = Conditions(s);
            G.events = new List<GameEvent>();
            G.checkpoint = null;
            return G;
        }

        // Events carry `chapter` too, so chapter summaries do not depend on day ranges.
        public static GameEvent RecordEvent(Game G, string type, Dictionary<string, object> details = null)
        {
            if (string.IsNullOrEmpty(type)) throw new ArgumentException("event type must be a non-empty string");
            var e = new GameEvent { seq = (G.events.Count > 0 ? G.events[G.events.Count - 1].seq : 0) + 1, type = type, chapter = G.chapter, day = G.day, minute = G.minute, details = details ?? new Dictionary<string, object>() };
            G.events.Add(e);
            return e;
        }

        public static string Hhmm(int m) =>
            ((int)Math.Floor(m / 60.0)).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') + ":" + (m % 60).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');

        public static string FmtClock(Game G)
        {
            var d = G.day == 0 ? "D-day" : G.day < 0 ? "D" + G.day.ToString(CultureInfo.InvariantCulture) : "D+" + G.day.ToString(CultureInfo.InvariantCulture);
            return d + " " + Hhmm(G.minute);
        }

        public static double Adjust(Game G, string stat, double delta)
        {
            if (!STATS.Contains(stat)) throw new ArgumentException($"unknown stat: {stat}");
            if (double.IsNaN(delta) || double.IsInfinity(delta)) throw new ArgumentException($"delta must be a number, got {Js.Num(delta)}");
            return G.stats[stat] = Math.Min(100, Math.Max(0, G.stats[stat] + delta));
        }

        // Moves the clock. Drift counts boundaries crossed on an absolute clock, so ten 2-minute steps
        // add the same thirst as one 20-minute step.
        public static Game Advance(Game G, double minutes, bool awake = true)
        {
            if (double.IsNaN(minutes) || double.IsInfinity(minutes) || minutes < 0) throw new ArgumentException($"minutes must be >= 0, got {Js.Num(minutes)}");
            long t0 = (long)G.day * 1440 + G.minute, t1 = t0 + (long)Js.Round(minutes);
            double Crossed(int step) => Math.Floor((double)t1 / step) - Math.Floor((double)t0 / step);
            Adjust(G, "thirst", Crossed(20));
            if (awake) Adjust(G, "fatigue", Crossed(30));
            G.day = (int)Math.Floor(t1 / 1440.0);
            G.minute = (int)(t1 - (long)G.day * 1440);
            return G;
        }

        public static int SpendSlot(Game G, string actionId, int cost = 1)
        {
            if (cost < 1) throw new ArgumentException($"slot cost must be a positive integer, got {cost}");
            var left = G.slots.total - G.slots.used;
            if (cost > left) throw new InvalidOperationException($"not enough slots for {actionId}: {left} left, {cost} needed");
            G.slots.used += cost;
            G.slots.done.Add(actionId);
            RecordEvent(G, "slot", Details.Of("actionId", actionId, "cost", cost));
            return left - cost;
        }

        static int Qty(int n)
        {
            if (n < 1) throw new ArgumentException($"quantity must be a positive integer, got {n}");
            return n;
        }

        static ItemEntry Entry(Game G, string id)
        {
            if (id == null || !Items.CATALOG.ContainsKey(id)) throw new ArgumentException($"unknown item: {id}");
            if (!G.items.TryGetValue(id, out var e) || e == null) G.items[id] = e = NewItem();
            return e;
        }

        // e[from] for 'home' | 'bag'.
        static int At(ItemEntry e, string where) => where == "home" ? e.home : e.bag;
        static void SetAt(ItemEntry e, string where, int n) { if (where == "home") e.home = n; else e.bag = n; }

        // cart { id: qty }, prices { id: won } = what this shop has in stock (sold-out ids are left out).
        // 'cash' at an ATM is a purchase like any other: its price is withdrawn from money.
        public static int Buy(Game G, Dictionary<string, int> cart, Dictionary<string, int> prices)
        {
            var lines = (cart ?? new Dictionary<string, int>()).Where(kv => kv.Value != 0).ToList();
            long total = 0; // JS numbers do not wrap; past G.money it throws anyway
            foreach (var kv in lines)
            {
                if (kv.Key == null || !Items.CATALOG.ContainsKey(kv.Key)) throw new ArgumentException($"unknown item: {kv.Key}");
                Qty(kv.Value);
                if (prices == null || !prices.TryGetValue(kv.Key, out var p) || p < 0) throw new InvalidOperationException($"{kv.Key} is not in stock here");
                total += (long)p * kv.Value;
            }
            if (total > G.money) throw new InvalidOperationException($"not enough money: {total} > {G.money}");
            G.money -= (int)total;
            foreach (var kv in lines) { var e = Entry(G, kv.Key); e.home += kv.Value; e.bought += kv.Value; }
            if (lines.Count > 0)
            {
                var bought = new Dictionary<string, object>();
                foreach (var kv in lines) bought[kv.Key] = kv.Value;
                RecordEvent(G, "buy", Details.Of("cart", bought, "total", (int)total));
            }
            return (int)total;
        }

        static ItemEntry Move(Game G, string id, int n, string from, string to)
        {
            Qty(n);
            var e = Entry(G, id);
            if (At(e, from) < n) throw new InvalidOperationException($"only {At(e, from)} {id} at {from}, asked to move {n}");
            SetAt(e, from, At(e, from) - n);
            SetAt(e, to, At(e, to) + n);
            return e;
        }

        public static ItemEntry Pack(Game G, string id, int n = 1) => Move(G, id, n, "home", "bag");
        public static ItemEntry Unpack(Game G, string id, int n = 1) => Move(G, id, n, "bag", "home");

        static double Round2(double x) => Js.Round(x * 100) / 100;

        public static BagLoadResult BagLoad(Game G)
        {
            double w = 0, v = 0;
            foreach (var kv in G.items) { w += kv.Value.bag * Items.CATALOG[kv.Key].weightKg; v += kv.Value.bag * Items.CATALOG[kv.Key].volumeL; }
            double weightKg = Round2(w), volumeL = Round2(v);
            return new BagLoadResult { weightKg = weightKg, volumeL = volumeL, over = new BagOver { weight = weightKg > Items.BAG_LIMIT.weightKg, volume = volumeL > Items.BAG_LIMIT.volumeL } };
        }

        static bool Have(Game G, string id) => id != null && G.items.TryGetValue(id, out var e) && e != null && e.home + e.bag > 0;

        // Owned is not usable: a flashlight without batteries does not count.
        public static bool Usable(Game G, string id)
        {
            if (!Have(G, id)) return false;
            return Items.CATALOG.TryGetValue(id, out var c) && c.needsCheck != null ? G.items[id].@checked : true;
        }

        // Turns an owned item into a usable one. 'batteries' uses up one AA pack; 'charge' needs grid
        // power, which the director gates (before the blackout). Returns { ok, reason? } (reason is Korean).
        public static CheckResult Check(Game G, string id)
        {
            if (id == null || !Items.CATALOG.ContainsKey(id)) throw new ArgumentException($"unknown item: {id}");
            if (!Have(G, id)) return new CheckResult { ok = false, reason = "가지고 있지 않습니다." };
            var e = G.items[id];
            if (e.@checked) return new CheckResult { ok = true };
            if (Items.CATALOG[id].needsCheck == "batteries")
            {
                if (!Have(G, "batteries_aa")) return new CheckResult { ok = false, reason = "넣을 건전지가 없습니다." };
                Consume(G, "batteries_aa", 1, G.items["batteries_aa"].home > 0 ? "home" : "bag");
            }
            e.@checked = true;
            RecordEvent(G, "check", Details.Of("id", id));
            return new CheckResult { ok = true };
        }

        public static ItemEntry Consume(Game G, string id, int n = 1, string from = "home")
        {
            if (from != "home" && from != "bag") throw new ArgumentException($"consume from must be 'home' or 'bag', got {from}");
            Qty(n);
            var e = Entry(G, id);
            if (At(e, from) < n) throw new InvalidOperationException($"only {At(e, from)} {id} at {from}, asked to consume {n}");
            SetAt(e, from, At(e, from) - n);
            e.used += n;
            return e;
        }

        // Durable items (radio, flashlight, water_jug...) are used without being used up.
        public static ItemEntry Use(Game G, string id)
        {
            var e = Entry(G, id);
            if (!Usable(G, id)) throw new InvalidOperationException($"{id} is not usable");
            e.used += 1;
            return e;
        }

        public static InfoNote Note(Game G, string text, string label, string place = null)
        {
            if (text == null || text.Trim().Length == 0) throw new ArgumentException("note text must be a non-empty string");
            if (!LABELS.Contains(label)) throw new ArgumentException($"unknown info label: {label}");
            var t = text.Trim();
            var dup = G.info.FirstOrDefault(i => i.text == t && i.label == label);
            if (dup != null) return dup;
            var n = new InfoNote { chapter = G.chapter, day = G.day, minute = G.minute, text = t, label = label, place = place };
            G.info.Add(n);
            return n;
        }

        // Walking and running speed multiplier 0.6..1 from fatigue (above 50) and, once the bag is on the back,
        // its weight against BAG_LIMIT (up to -12 % at the limit, steeply more above it).
        public static double SpeedMul(Game G)
        {
            double weightKg = G.know != null && G.know.bagOn ? BagLoad(G).weightKg : 0, lim = Items.BAG_LIMIT.weightKg;
            var tired = Math.Max(0, G.stats.fatigue - 50) / 50 * 0.25;
            var load = weightKg <= lim ? 0.12 * weightKg / lim : 0.12 + (weightKg - lim) / lim * 0.4;
            var leg = G.injury != null && G.injury.Contains("leg") ? 0.7 : 1; // a broken leg from a fall stays for the rest of the run
            return Round2(Math.Min(1, Math.Max(0.6, 1 - tired - load)) * leg);
        }

        static bool InRange(double n, double lo, double hi) => n >= lo && n <= hi; // false for NaN, like JS num()
        static void Bad(string why) => throw new FormatException($"invalid save: {why}");

        // state.js load() minus JSON.parse: validates a parsed G (throws FormatException "invalid save: why"),
        // drops items removed from the catalogue, returns it.
        public static Game Load(Game parsed)
        {
            Validate(parsed, false);
            return parsed;
        }

        public static void Validate(Game G, bool nested = false)
        {
            if (G == null || G.v != 2) Bad("not a v2 game");
            if (G.chapter == null || !InRange(G.minute, 0, 1439)) Bad("clock");
            var s = G.slots;
            if (s == null || !InRange(s.used, 0, s.total) || s.done == null) Bad("slots");
            if (G.money < 0) Bad("money");
            if (G.stats == null || !STATS.All(k => InRange(G.stats[k], 0, 100))) Bad("stats");
            if (G.phone == null || !InRange(G.phone.battery, 0, 100)) Bad("phone");
            if (G.items == null) Bad("items");
            if (G.items.Keys.Any(id => !Items.CATALOG.ContainsKey(id))) // removed from the catalogue since this save
                G.items = G.items.Where(kv => Items.CATALOG.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value); // rebuilt, so insertion order stays
            foreach (var kv in G.items)
            {
                var e = kv.Value;
                if (e == null || e.home < 0 || e.bag < 0 || e.bought < 0 || e.used < 0) Bad($"item {kv.Key}");
            }
            if (G.know == null || G.cond == null || G.rel == null || !PEOPLE.All(p => G.rel.TryGetValue(p, out var r) && r != null)) Bad("know/cond/rel");
            if (G.skill == null || !TOOLS.All(t => G.skill.TryGetValue(t, out var runs) && runs != null)) Bad("skill");
            if (G.info == null || !G.info.All(i => i != null && i.text != null && LABELS.Contains(i.label))) Bad("info");
            if (G.events == null || !G.events.All(e => e != null && e.type != null)) Bad("events");
            if (G.checkpoint != null)
            {
                if (nested || G.checkpoint.key == null) Bad("checkpoint");
                Validate(G.checkpoint.state, true);
            }
        }

        public static CheckpointData Checkpoint(Game G, string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("checkpoint key must be a non-empty string");
            var old = G.checkpoint;
            G.checkpoint = null;
            Game state;
            try { state = G.Clone(); } finally { G.checkpoint = old; }
            return G.checkpoint = new CheckpointData { key = key, state = state };
        }

        // Rolls G back in place (callers keep their reference); the checkpoint stays for another retry.
        public static Game Restore(Game G)
        {
            var cp = G.checkpoint;
            if (cp == null) throw new InvalidOperationException("no checkpoint to restore");
            var back = cp.state.Clone();
            foreach (var f in typeof(Game).GetFields()) f.SetValue(G, f.GetValue(back));
            G.checkpoint = cp;
            return G;
        }
    }
}
