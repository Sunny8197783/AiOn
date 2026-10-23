// Ports game/relay.js: the message 정민 sends 서연 from the shelter, its rule-based reading, and the pins
// (why it cannot be applied to her as-is). Times come from G.relay, set by the D-day chapter from the real clock.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        public static readonly Dictionary<string, RelayEntrance> ENTRANCES = new Dictionary<string, RelayEntrance>
        {
            ["stairs"] = new RelayEntrance { label = "출입구 A · 계단", @short = "A 계단", seoyeonOk = false, card = "계단은 혼자 내려가지 않음" },
            ["ramp"] = new RelayEntrance { label = "출입구 B · 지하주차장 진출입로", @short = "B 진출입로", seoyeonOk = false, card = "가파른 내리막은 혼자 내려가지 않음" },
            ["access"] = new RelayEntrance { label = "출입구 C · 경사로", @short = "C 경사로", seoyeonOk = true },
        };
        public const int MAX_NOTE = 120;

        public static string Hhmm(int m) => State.Hhmm(m);

        // C opens after the message is sent and before 서연 reads it, so the message can never mention it from sight.
        public static RelayTimesData RelayTimes(int sentMinute, int? gateMinute = null)
        {
            var gate = gateMinute ?? sentMinute - 3;
            var sent = Math.Max(sentMinute, gate);
            var open = Math.Max(20 * 60 + 16, sent + 3);
            return new RelayTimesData { gateSeenAt = Hhmm(gate), sentAt = Hhmm(sent), accessOpenAt = Hhmm(open), receivedAt = Hhmm(open + 1) };
        }

        // What 정민 actually did, which the message may or may not say.
        public static NoteCtx NoteContext(Game G)
        {
            var events = G?.events ?? new List<GameEvent>();
            var entered = events.LastOrDefault(e => e.type == "route_attempt" && e.Str("who") == "jeongmin" && (e.Str("route") == "stairs" || e.Str("route") == "ramp"));
            var gate = events.FirstOrDefault(e => e.type == "gate_observed" && e.Str("who") == "jeongmin");
            var route = entered?.Str("route");
            return new NoteCtx { route = string.IsNullOrEmpty(route) ? "stairs" : route, observedVia = gate?.Str("via") == "notice_after" ? "notice_after" : "sign" };
        }

        static readonly Dictionary<string, string> ENTERED = new Dictionary<string, string> { ["stairs"] = "나는 계단(A)으로 들어왔어.", ["ramp"] = "나는 지하주차장 진출입로(B)로 들어왔어." };

        public static string ComposeNote(Chips chips, NoteCtx c = null)
        {
            chips ??= new Chips();
            string route = c?.route ?? "stairs", observedVia = c?.observedVia ?? "sign", gateSeenAt = c?.gateSeenAt ?? "20:09";
            var when = chips.time ? $"{gateSeenAt}에 " : "";
            var parts = new List<string>();
            if (chips.what == "gate_closed") parts.Add(observedVia == "notice_after" ? $"{when}도착해서 안내문을 보니 동쪽 출입구는 폐쇄래." : $"{when}동쪽 출입구가 닫혀 있었어.");
            if (chips.what == "all_closed") parts.Add($"{when}대피소가 닫혀 있었어.");
            if (chips.route) parts.Add(ENTERED.TryGetValue(route, out var en) ? en : ENTERED["stairs"]);
            if (chips.unknowns) parts.Add("다른 입구 상태는 확인 못 했어.");
            if (chips.advice == "follow_me") parts.Add("나처럼 오면 돼.");
            if (chips.advice == "direction") parts.Add(route == "ramp" ? "연무로 따라 오다가 문화로에서 왼쪽으로 꺾어." : "연무로 따라 서쪽으로 쭉 오면 왼쪽이야.");
            if (chips.advice == "check_notice") parts.Add("현장 안내를 꼭 다시 확인해.");
            return string.Join(" ", parts);
        }

        // JS regex semantics: \b and \d are ASCII there (in .NET they are Unicode, and Hangul counts as a word
        // character), so word edges are written as explicit ASCII lookarounds and digits as [0-9].
        const string WB = "(?<![A-Za-z0-9_])", WE = "(?![A-Za-z0-9_])";
        static Regex Re(string p, bool i = false) => new Regex(p, RegexOptions.CultureInvariant | (i ? RegexOptions.IgnoreCase : RegexOptions.None));

        // '막' alone is often the adverb "just", so only its closed forms count.
        const string SHUT = "폐쇄|닫|막[혀힌힘혔아았]";
        static readonly Regex UNKNOWN = Re(@"모르|몰라|몰랐|미확인|확인\s*(못|안)|확인하지");
        static readonly Regex EAST = Re("동쪽|동문");
        static readonly Regex NOT_THIS_WAY = Re(@"말고|빼고|피해|안\s*돼|쓰지\s*마|가지\s*마");
        static readonly Regex CLOCK = Re(WB + "([01]?[0-9]|2[0-3]):[0-5][0-9]" + WE);
        static readonly Regex HOUR = Re(@"[0-9]{1,2}\s*시(?!간)(\s*[0-9]{1,2}\s*분)?");
        static readonly (string field, (string value, Regex pattern)[] rules)[] RULES =
        {
            ("what", new[] { ("all_closed", Re($@"(전체|모두|전부|다)\s*({SHUT})|대피(소|시설)[^.,]{{0,8}}({SHUT})")), ("gate_closed", Re($"(동쪽|동문)[^.,]{{0,8}}({SHUT}|통제)")) }),
            ("route", new[] { ("stairs", Re("계단|" + WB + "A" + WE, true)), ("ramp", Re("진출입로|" + WB + "B" + WE, true)), ("access", Re(WB + "C" + WE + "|경사로", true)) }),
            ("advice", new[] { ("follow_me", Re(@"나처럼|따라\s*와|똑같이|같은\s*길|여기로")), ("direction", Re("오른쪽|왼쪽|쭉|건너|꺾")), ("check_notice", Re("안내[^.]{0,8}확인|확인[^.]{0,8}안내")) }),
        };

        // Rule-based reading of the message. Each quote is a whole clause, so it is always an exact substring.
        public static Claims ParseNote(string text)
        {
            var clauses = (text ?? "").Split('.', '!', '?', '\n').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
            var claims = new Claims();
            foreach (var (field, rules) in RULES)
            {
                foreach (var (value, pattern) in rules)
                {
                    var clause = clauses.FirstOrDefault(c => pattern.IsMatch(c)
                        && !(value == "all_closed" && (EAST.IsMatch(c) || UNKNOWN.IsMatch(c)))
                        && !(field == "route" && NOT_THIS_WAY.IsMatch(c)));
                    if (clause == null) continue;
                    if (field == "what") claims.what = value; else if (field == "route") claims.route = value; else claims.advice = value;
                    claims.quotes[field] = clause;
                    break;
                }
            }
            foreach (var clause in clauses)
            {
                var m = CLOCK.Match(clause);
                if (!m.Success) m = HOUR.Match(clause);
                if (m.Success) { claims.time = m.Value; claims.quotes["time"] = clause; break; }
            }
            var unknown = clauses.FirstOrDefault(c => UNKNOWN.IsMatch(c));
            if (unknown != null) { claims.unknowns = true; claims.quotes["unknowns"] = unknown; }
            return claims;
        }

        public static string NoteKind(Claims claims)
        {
            if (claims.what == "all_closed") return "generalized";
            if (claims.what == "gate_closed" && !string.IsNullOrEmpty(claims.time) && claims.unknowns) return "precise";
            return !string.IsNullOrEmpty(claims.what) || !string.IsNullOrEmpty(claims.time) || !string.IsNullOrEmpty(claims.route) || claims.unknowns || !string.IsNullOrEmpty(claims.advice) ? "partial" : "unparsed";
        }

        static readonly Regex CONTROL = new Regex(@"[\x00-\x1F\x7F]");

        // Player text is untrusted: cleaned of control characters and cut to MAX_NOTE when edited.
        public static Handover MakeHandover(Game G, Chips chips = null, string text = null, bool edited = false)
        {
            if (G?.events == null) throw new ArgumentException("올바른 게임 기록이 필요합니다.");
            chips ??= new Chips();
            var times = G.relay ?? RelayTimes(20 * 60 + 13);
            var c = NoteContext(G);
            c.gateSeenAt = times.gateSeenAt;
            var composed = ComposeNote(chips, c);
            // Player text is untrusted: plain string, no control characters, bounded length.
            var cleaned = edited ? CONTROL.Replace(text ?? "", " ").Trim() : "";
            if (cleaned.Length > MAX_NOTE) cleaned = cleaned.Substring(0, MAX_NOTE);
            var finalText = cleaned.Length > 0 ? cleaned : composed;
            var claims = ParseNote(finalText);
            claims.parsedBy = cleaned.Length > 0 ? "rules" : "chips";
            var handover = new Handover
            {
                text = finalText, chips = new Chips { what = chips.what, time = chips.time, route = chips.route, unknowns = chips.unknowns, advice = chips.advice },
                edited = cleaned.Length > 0, claims = claims, kind = NoteKind(claims), actualRoute = c.route, observedVia = c.observedVia,
                gateSeenAt = times.gateSeenAt, sentAt = times.sentAt, accessOpenAt = times.accessOpenAt, receivedAt = times.receivedAt,
            };
            State.RecordEvent(G, "handover_written", Details.Of("kind", handover.kind, "edited", handover.edited, "length", finalText.Length));
            G.handover = handover;
            return handover;
        }

        static string GateKnown(Handover h) => h?.observedVia == "notice_after" ? "도착 후 안내문으로 안 동쪽 출입구 한 곳" : $"직접 본 동쪽 출입구 한 곳 ({h?.gateSeenAt ?? ""})";
        static string Quote(Claims c, string field) => c.quotes.TryGetValue(field, out var q) ? q : null;

        // Three reasons the message cannot be applied as-is: body, time, and one more (scope/advice/place).
        public static List<Pin> BuildPins(Game G)
        {
            var h = G?.handover;
            var claims = h?.claims ?? ParseNote("");
            var actual = !string.IsNullOrEmpty(h?.actualRoute) ? h.actualRoute : NoteContext(G).route;
            var t = h != null ? new RelayTimesData { gateSeenAt = h.gateSeenAt, sentAt = h.sentAt, accessOpenAt = h.accessOpenAt, receivedAt = h.receivedAt }
                : G?.relay ?? RelayTimes(20 * 60 + 13);
            var named = claims.route == "stairs" || claims.route == "ramp" ? claims.route : null;
            var bodyAt = named ?? actual;
            var other = bodyAt == "stairs" ? "ramp" : "stairs";
            var body = new Pin
            {
                factor = "body", anchor = bodyAt,
                was = named != null ? $"“{Quote(claims, "route")}”" : $"메시지에 입구 없음 · 정민이 들어간 입구: {ENTRANCES[actual].@short}",
                now = $"서연의 카드: {ENTRANCES[bodyAt].card}",
                also = new Pin { anchor = other, now = $"✕ {ENTRANCES[other].@short} · {ENTRANCES[other].card}" },
            };
            var time = claims.route == "access"
                ? new Pin { factor = "time", anchor = "access", was = $"“{Quote(claims, "route")}”", now = $"{t.sentAt}의 C · 지금 상태는 안내판에서" }
                : new Pin { factor = "time", anchor = "notice", was = $"메시지: {claims.time ?? "시각 없음"} · 보냄 {t.sentAt}", now = $"지금 {t.receivedAt} · 안내판이 {t.accessOpenAt}에 바뀜 — 메시지 뒤의 일" };
            Pin third;
            if (claims.what == "all_closed") third = new Pin { factor = "scope", anchor = "gate", was = $"“{Quote(claims, "what")}”", now = $"정민이 확인한 것: {GateKnown(h)}" };
            else if (claims.advice == "follow_me") third = new Pin { factor = "advice", anchor = "start1", was = $"“{Quote(claims, "advice")}”", now = "정민의 다리, 정민의 출발점 기준의 길" };
            else if (claims.advice == "direction") third = new Pin { factor = "direction", anchor = "start1", was = $"“{Quote(claims, "advice")}”", now = "정민의 출발점(동쪽 아파트) 기준 방향 · 서연은 서쪽 카페에서 출발", line = "start0" };
            else third = new Pin { factor = "place", anchor = "start1", was = "정민: 동쪽 한빛아파트에서 출발", now = "서연: 서쪽 카페 쉼표에서 출발", line = "start0" };
            return new List<Pin> { body, time, third };
        }

        public static string[] ClosingLines(string kind)
        {
            if (kind == "generalized") return new[] { "정민이 본 것보다 넓은 말이었습니다. 그리고 정민의 시각, 정민의 몸, 정민의 자리에서 쓴 말이었습니다.", "서연은 지금의 안내로 다시 판단합니다." };
            if (kind == "unparsed") return new[] { "서연에게 닿은 것은 이 문장뿐이었습니다.", "서연은 지금의 안내에서 다시 시작합니다." };
            return new[] { "정민의 시각, 정민의 몸, 정민의 자리에서 쓴 말이었습니다.", "서연은 지금의 안내로 다시 판단합니다." };
        }

        // How each part of the message read from 서연's side. status: helped | recheck | mismatch | missing.
        public static List<ClaimRow> ReadClaims(Game G)
        {
            var h = G?.handover;
            var c = h?.claims;
            var rows = new List<ClaimRow>();
            if (c == null) return rows;
            void Add(string label, string quote, string status, string why, string sourceId) => rows.Add(new ClaimRow { label = label, quote = quote, status = status, why = why, sourceId = sourceId });
            var hasTime = !string.IsNullOrEmpty(c.time);
            if (c.what == "gate_closed") Add("본 것", Quote(c, "what"), hasTime ? "helped" : "recheck", hasTime ? "본 범위와 시각이 함께 있어 지금 안내와 비교할 수 있었습니다." : "언제 본 것인지 없어 지금도 그런지 판단하기 어려웠습니다.", hasTime ? "press_code" : "sms_list");
            if (c.what == "all_closed") Add("본 것", Quote(c, "what"), "mismatch", $"정민이 확인한 것은 {GateKnown(h)}뿐이었습니다.", "press_code");
            if (c.route == "stairs" || c.route == "ramp") Add("들어간 길", Quote(c, "route"), "mismatch", "서연의 카드 기준과 다른 입구였습니다.", "koddi_card");
            if (c.unknowns) Add("모르는 것", Quote(c, "unknowns"), "helped", "모르는 부분을 밝혀 서연이 직접 확인할 곳을 알 수 있었습니다.", "press_code");
            if (c.advice == "follow_me" || c.advice == "direction") Add("조언", Quote(c, "advice"), "mismatch", "쓴 사람의 다리와 출발점 기준의 말이었습니다.", "sms_area");
            if (c.advice == "check_notice") Add("조언", Quote(c, "advice"), "helped", "지금의 현장 안내를 다시 보게 했습니다.", "ps_airraid");
            if (!hasTime) Add("시각", null, "missing", "언제의 정보인지가 없었습니다.", "sms_list");
            if (!c.unknowns) Add("모르는 것", null, "missing", "확인하지 못한 부분이 적혀 있지 않았습니다.", "press_code");
            Add("C 경사로", c.route == "access" ? Quote(c, "route") : null, c.route == "access" ? "recheck" : "missing", $"{h.accessOpenAt}에 열렸습니다 — 메시지를 보낸 뒤의 일이라 지금의 안내에서만 알 수 있었습니다.", "ps_airraid");
            return rows;
        }
    }
}
