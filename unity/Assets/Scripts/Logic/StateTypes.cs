// The shape of G (game/state.js createGame and the keys the story adds later). Field names are the
// JS keys so the save JSON keeps the JS shape; `checked` is a C# keyword, so it is `@checked` here.
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    public sealed class Game
    {
        public int v = 2;
        public uint seed;
        public string chapter = "prologue";
        public int day = -3;
        public int minute = 17 * 60 + 30;
        public Slots slots = new Slots();
        public int money = 150000;
        public Stats stats = new Stats();
        public PhoneState phone = new PhoneState();
        public Dictionary<string, ItemEntry> items = new Dictionary<string, ItemEntry>();
        public Know know = new Know();
        public Dictionary<string, Rel> rel = new Dictionary<string, Rel>();
        public Dictionary<string, List<SkillRun>> skill = new Dictionary<string, List<SkillRun>>();
        public List<InfoNote> info = new List<InfoNote>();
        public Cond cond = new Cond();
        public List<GameEvent> events = new List<GameEvent>();
        public CheckpointData checkpoint;
        // Added by the story / app (absent in a fresh G).
        public int? karma;
        public List<string> injury;
        public RelayTimesData relay;
        public Handover handover;

        // structuredClone(G): a deep copy (checkpoint/restore). Event details are copied too.
        public Game Clone() => throw new NotImplementedException("port: game/state.js:structuredClone(G)");
    }

    public sealed class Slots
    {
        public int total = 6;
        public int used;
        public List<string> done = new List<string>();
    }

    public sealed class Stats
    {
        public double health = 100, thirst = 10, fatigue = 20, tension = 25;
        // G.stats[stat] by name ('health' | 'thirst' | 'fatigue' | 'tension').
        public double this[string stat]
        {
            get => stat switch { "health" => health, "thirst" => thirst, "fatigue" => fatigue, "tension" => tension, _ => throw new ArgumentException("unknown stat: " + stat) };
            set { switch (stat) { case "health": health = value; break; case "thirst": thirst = value; break; case "fatigue": fatigue = value; break; case "tension": tension = value; break; default: throw new ArgumentException("unknown stat: " + stat); } }
        }
    }

    public sealed class PhoneState
    {
        public double battery = 64;
        public string signal = "ok";
    }

    public sealed class ItemEntry
    {
        public int home, bag, bought, used;
        public bool @checked;
    }

    // G.know: what the player has found out or done. Every key the JS story sets is listed.
    public sealed class Know
    {
        public bool shelterLocated, entrancesChecked, routeChecked, extinguisherLocated, radioChecked;
        public bool contactsCard, docsCopied, waterPoint, gateClosedSeen;
        public string meetPoint;
        public bool bagOn, bagPacked, breaker, gasValve, candleLit, candleOut, clerkLeft;
        public bool closureHeard, closureSeen, entrancesHeard, evacClosuresKnown, idTaken, radioOn, strikeSeen, tubFilled, altRoute;
        public string path, stayMove;
        public double? radioFreq;
        public List<string> cardFields, plannedRoute;

        // !!G.know[key] for the keys game/debrief.js looks up by name.
        public bool Has(string key) => throw new NotImplementedException("port: game/debrief.js:G.know[k]");
    }

    public sealed class Rel
    {
        public int trust;
        public string contact;
    }

    // G.skill[tool][]: one encounter. result is a string or an eval object (BagEval, ExtEval).
    public sealed class SkillRun
    {
        public int day, minute;
        public string stage;
        public object result;
    }

    public sealed class InfoNote
    {
        public string chapter;
        public int day, minute;
        public string text, label, place;
    }

    // G.cond: situations that vary per seed (state.js conditions()).
    public sealed class Cond
    {
        public ClosurePlan closures = new ClosurePlan();
        public bool eastGateClosed = true;
        public string accessOpensAt = "20:16";
        public int commsWeakAt;
        public SoldOut soldOut = new SoldOut();
        public bool rumorTrue;
        public int waterAt;
        public int evacNoticeAt;
        public string seoyeonAt = "cafe";
    }

    public sealed class ClosurePlan
    {
        public List<string> dday = new List<string>();
        public List<string> evac = new List<string>();
    }

    public sealed class SoldOut
    {
        public List<string> early = new List<string>();
        public List<string> late = new List<string>();
    }

    // One recorded event. details values are plain CLR data: string, bool, long/int/double,
    // Dictionary<string, object>, List<object> (Core.GameJson converts loaded JSON to these).
    public sealed class GameEvent
    {
        public int seq;
        public string type, chapter;
        public int day, minute;
        public Dictionary<string, object> details = new Dictionary<string, object>();

        public object Get(string key) => details != null && details.TryGetValue(key, out var v) ? v : null;
        public string Str(string key) => Get(key) as string;
        public double? Num(string key) => Get(key) is IConvertible c && !(c is string) && !(c is bool) ? Convert.ToDouble(c) : (double?)null;
        public bool Bool(string key) => Get(key) is bool b && b;
    }

    public sealed class CheckpointData
    {
        public string key;
        public Game state;
    }

    // ctx.event(type, { a: 1, b: 'x' }) => Details.Of("a", 1, "b", "x").
    public static class Details
    {
        public static Dictionary<string, object> Of(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }
    }
}
