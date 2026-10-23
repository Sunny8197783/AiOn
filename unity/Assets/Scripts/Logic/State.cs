// Ports game/state.js: the functions that change G. Pure logic, no Unity.
// JSON text (save/load) lives in Runtime Core.GameJson (Logic has no JSON library); Load(Game) here is
// the validation half of state.js load(): it checks a parsed G and drops items no longer in the catalogue.
// Errors keep the JS messages (tests match on them): ArgumentException for bad input, InvalidOperationException
// for impossible moves, FormatException("invalid save: ...") for a bad save.
using System;
using System.Collections.Generic;

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

        // CLOSURE_SETS: phase -> list of closure-id sets conditions() picks from.
        public static readonly Dictionary<string, List<List<string>>> CLOSURE_SETS = new Dictionary<string, List<List<string>>>();

        // mulberry32 (same as engine/kit.js rng).
        public static Func<double> Rng(uint seed) => throw new NotImplementedException("port: game/state.js:rng");
        public static Cond Conditions(uint seed) => throw new NotImplementedException("port: game/state.js:conditions");
        // seed null = random (JS default Math.random() * 2^31).
        public static Game CreateGame(uint? seed = null) => throw new NotImplementedException("port: game/state.js:createGame");
        public static GameEvent RecordEvent(Game G, string type, Dictionary<string, object> details = null) => throw new NotImplementedException("port: game/state.js:recordEvent");
        public static string Hhmm(int m) => throw new NotImplementedException("port: game/state.js:hhmm");
        public static string FmtClock(Game G) => throw new NotImplementedException("port: game/state.js:fmtClock");
        public static double Adjust(Game G, string stat, double delta) => throw new NotImplementedException("port: game/state.js:adjust");
        public static Game Advance(Game G, double minutes, bool awake = true) => throw new NotImplementedException("port: game/state.js:advance");
        public static int SpendSlot(Game G, string actionId, int cost = 1) => throw new NotImplementedException("port: game/state.js:spendSlot");
        // cart { id: qty }, prices { id: won } (sold-out ids left out). Returns the total paid.
        public static int Buy(Game G, Dictionary<string, int> cart, Dictionary<string, int> prices) => throw new NotImplementedException("port: game/state.js:buy");
        public static ItemEntry Pack(Game G, string id, int n = 1) => throw new NotImplementedException("port: game/state.js:pack");
        public static ItemEntry Unpack(Game G, string id, int n = 1) => throw new NotImplementedException("port: game/state.js:unpack");
        public static BagLoadResult BagLoad(Game G) => throw new NotImplementedException("port: game/state.js:bagLoad");
        public static bool Usable(Game G, string id) => throw new NotImplementedException("port: game/state.js:usable");
        public static CheckResult Check(Game G, string id) => throw new NotImplementedException("port: game/state.js:check");
        public static ItemEntry Consume(Game G, string id, int n = 1, string from = "home") => throw new NotImplementedException("port: game/state.js:consume");
        public static ItemEntry Use(Game G, string id) => throw new NotImplementedException("port: game/state.js:use");
        public static InfoNote Note(Game G, string text, string label, string place = null) => throw new NotImplementedException("port: game/state.js:note");
        public static double SpeedMul(Game G) => throw new NotImplementedException("port: game/state.js:speedMul");
        // state.js load() minus JSON.parse: validates a parsed G (throws FormatException "invalid save: why"),
        // drops items removed from the catalogue, returns it.
        public static Game Load(Game parsed) => throw new NotImplementedException("port: game/state.js:load/validate");
        public static void Validate(Game G, bool nested = false) => throw new NotImplementedException("port: game/state.js:validate");
        public static CheckpointData Checkpoint(Game G, string key) => throw new NotImplementedException("port: game/state.js:checkpoint");
        // Rolls G back in place (callers keep their reference); the checkpoint stays for another retry.
        public static Game Restore(Game G) => throw new NotImplementedException("port: game/state.js:restore");
    }
}
