// Ports game/relay.js: the message 정민 sends 서연 from the shelter, its rule-based reading, and the pins.
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    public sealed class RelayEntrance
    {
        public string label, @short, card;
        public bool seoyeonOk;
    }

    // G.relay
    public sealed class RelayTimesData
    {
        public string gateSeenAt, sentAt, accessOpenAt, receivedAt;
    }

    public sealed class NoteCtx
    {
        public string route = "stairs";        // 'stairs' | 'ramp'
        public string observedVia = "sign";    // 'sign' | 'notice_after'
        public string gateSeenAt = "20:09";
    }

    // The compose chips. what: null | 'gate_closed' | 'all_closed'; advice: null | 'follow_me' | 'direction' | 'check_notice'.
    public sealed class Chips
    {
        public string what;
        public bool time, route, unknowns;
        public string advice;
    }

    public sealed class Claims
    {
        public string what, time, route, advice;
        public bool unknowns;
        public Dictionary<string, string> quotes = new Dictionary<string, string>();
        public string parsedBy;   // 'rules' | 'chips'
    }

    // G.handover
    public sealed class Handover
    {
        public string text;
        public Chips chips;
        public bool edited;
        public Claims claims;
        public string kind;        // 'precise' | 'partial' | 'generalized' | 'unparsed'
        public string actualRoute, observedVia;
        public string gateSeenAt, sentAt, accessOpenAt, receivedAt;
    }

    // One reason the message cannot be applied as-is. factor: body | time | scope | advice | direction | place.
    public sealed class Pin
    {
        public string factor, anchor, was, now, line;
        public Pin also;
    }

    // readClaims rows. status: helped | recheck | mismatch | missing.
    public sealed class ClaimRow
    {
        public string label, quote, status, why, sourceId;
    }

    public static class Relay
    {
        public static readonly Dictionary<string, RelayEntrance> ENTRANCES = new Dictionary<string, RelayEntrance>();
        public const int MAX_NOTE = 120;

        public static string Hhmm(int m) => State.Hhmm(m);
        public static RelayTimesData RelayTimes(int sentMinute, int? gateMinute = null) => throw new NotImplementedException("port: game/relay.js:relayTimes");
        public static NoteCtx NoteContext(Game G) => throw new NotImplementedException("port: game/relay.js:noteContext");
        public static string ComposeNote(Chips chips, NoteCtx c = null) => throw new NotImplementedException("port: game/relay.js:composeNote");
        public static Claims ParseNote(string text) => throw new NotImplementedException("port: game/relay.js:parseNote");
        public static string NoteKind(Claims claims) => throw new NotImplementedException("port: game/relay.js:noteKind");
        // Player text is untrusted: cleaned of control characters and cut to MAX_NOTE when edited.
        public static Handover MakeHandover(Game G, Chips chips = null, string text = null, bool edited = false) => throw new NotImplementedException("port: game/relay.js:makeHandover");
        public static List<Pin> BuildPins(Game G) => throw new NotImplementedException("port: game/relay.js:buildPins");
        public static string[] ClosingLines(string kind) => throw new NotImplementedException("port: game/relay.js:closingLines");
        public static List<ClaimRow> ReadClaims(Game G) => throw new NotImplementedException("port: game/relay.js:readClaims");
    }
}
