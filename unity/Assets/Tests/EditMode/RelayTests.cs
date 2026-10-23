// Ports tests/relay.test.mjs. (parseNote(42) has no typed C# form and is left out.)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gyeongbo.Logic;
using NUnit.Framework;

namespace Gyeongbo.Tests
{
    public class RelayTests
    {
        const int GATE = 20 * 60 + 9;
        static readonly Regex BLAME = new Regex(@"실수|잘못|탓|서연(이|은)\s*못");

        // 정민 sees the east gate, enters A or B; the D-day chapter then sets G.relay (story/seoyeon.js).
        static Game Played(string route = "stairs", string via = "sign", int gate = GATE, int? sent = null)
        {
            var G = State.CreateGame(1);
            State.RecordEvent(G, "gate_observed", Details.Of("who", "jeongmin", "via", via, "minute", gate));
            State.RecordEvent(G, "route_attempt", Details.Of("who", "jeongmin", "route", route, "minute", gate + 2));
            G.relay = Relay.RelayTimes(sent ?? gate + 4, gate);
            return G;
        }

        // Every combination of chips the compose screen allows.
        static IEnumerable<Chips> AllChips()
        {
            foreach (var what in new[] { "gate_closed", "all_closed" })
                foreach (var time in new[] { false, true })
                    foreach (var route in new[] { false, true })
                        foreach (var unknowns in new[] { false, true })
                            foreach (var advice in new[] { null, "follow_me", "direction", "check_notice" })
                                yield return new Chips { what = what, time = time, route = route, unknowns = unknowns, advice = advice };
        }

        static string T(RelayTimesData t) => $"{t.gateSeenAt}|{t.sentAt}|{t.accessOpenAt}|{t.receivedAt}";
        static bool Le(string a, string b) => string.CompareOrdinal(a, b) <= 0;
        static bool Lt(string a, string b) => string.CompareOrdinal(a, b) < 0;

        [Test]
        public void RelayTimesCOpensAfterTheNoteIsSentAndBeforeSeoyeonReadsIt()
        {
            Assert.AreEqual("20:10|20:13|20:16|20:17", T(Relay.RelayTimes(20 * 60 + 13)));
            Assert.AreEqual("20:09|20:30|20:33|20:34", T(Relay.RelayTimes(20 * 60 + 30, GATE)));
            Assert.AreEqual("20:08", Relay.RelayTimes(20 * 60 + 5, 20 * 60 + 8).sentAt, "never sent before the gate was seen");
            for (int sent = 20 * 60; sent <= 21 * 60 + 30; sent++)
                for (int gate = sent - 20; gate <= sent + 5; gate++)
                {
                    var t = Relay.RelayTimes(sent, gate);
                    Assert.IsTrue(Le(t.gateSeenAt, t.sentAt) && Lt(t.sentAt, t.accessOpenAt) && Lt(t.accessOpenAt, t.receivedAt), T(t));
                    Assert.IsTrue(Le("20:16", t.accessOpenAt));
                }
            Assert.AreEqual(State.Hhmm(1234), Relay.Hhmm(1234));
        }

        [Test]
        public void EntrancesOpenToJeongminAreOutsideSeoyeonsCardOnlyCFitsIt()
        {
            CollectionAssert.AreEqual(new[] { "stairs", "ramp", "access" }, Relay.ENTRANCES.Keys.ToList());
            foreach (var id in new[] { "stairs", "ramp" })
            {
                Assert.IsFalse(Relay.ENTRANCES[id].seoyeonOk);
                Assert.IsTrue(!string.IsNullOrEmpty(Relay.ENTRANCES[id].card) && !string.IsNullOrEmpty(Relay.ENTRANCES[id].@short) && !string.IsNullOrEmpty(Relay.ENTRANCES[id].label));
            }
            Assert.IsTrue(Relay.ENTRANCES["access"].seoyeonOk);
        }

        [Test]
        public void NoteContextReadsJeongminsRealEntranceAndHowTheGateWasLearned()
        {
            string C(NoteCtx c) => $"{c.route}|{c.observedVia}";
            Assert.AreEqual("ramp|notice_after", C(Relay.NoteContext(Played("ramp", "notice_after"))));
            var G = Played("stairs");
            State.RecordEvent(G, "route_attempt", Details.Of("who", "seoyeon", "route", "ramp"));
            State.RecordEvent(G, "route_attempt", Details.Of("who", "jeongmin", "route", "access"));
            State.RecordEvent(G, "gate_observed", Details.Of("who", "jeongmin", "via", "notice_after"));
            Assert.AreEqual("stairs|sign", C(Relay.NoteContext(G)), "서연's moves, C and later notices do not count");
            State.RecordEvent(G, "route_attempt", Details.Of("who", "jeongmin", "route", "ramp"));
            Assert.AreEqual("ramp", Relay.NoteContext(G).route, "the last entrance 정민 used");
            foreach (var G0 in new[] { State.CreateGame(2), null, new Game { events = null } }) Assert.AreEqual("stairs|sign", C(Relay.NoteContext(G0)));
        }

        [Test]
        public void EveryChipNoteFitsNeverMentionsCAndParsesBackToExactlyTheChips()
        {
            foreach (var gateSeenAt in new[] { "20:09", Relay.RelayTimes(20 * 60 + 14).gateSeenAt })
                foreach (var route in new[] { "stairs", "ramp" })
                    foreach (var observedVia in new[] { "sign", "notice_after" })
                        foreach (var chips in AllChips())
                        {
                            var text = Relay.ComposeNote(chips, new NoteCtx { route = route, observedVia = observedVia, gateSeenAt = gateSeenAt });
                            Assert.LessOrEqual(text.Length, Relay.MAX_NOTE, text);
                            Assert.IsFalse(Regex.IsMatch(text, "C|경사로"), text);
                            Assert.AreEqual(chips.time, text.Contains(gateSeenAt), text);
                            var c = Relay.ParseNote(text);
                            Assert.AreEqual(chips.what, c.what, text);
                            Assert.AreEqual(chips.time, !string.IsNullOrEmpty(c.time), text);
                            Assert.AreEqual(chips.route ? route : null, c.route, text);
                            Assert.AreEqual(chips.unknowns, c.unknowns, text);
                            Assert.AreEqual(chips.advice, c.advice, text);
                            foreach (var quote in c.quotes.Values) Assert.IsTrue(text.Contains(quote), quote);
                        }
            Assert.AreEqual("", Relay.ComposeNote(null));
            Assert.AreEqual(Relay.ComposeNote(new Chips { route = true }, new NoteCtx { route = "stairs" }), Relay.ComposeNote(new Chips { route = true }, new NoteCtx { route = "access" }), "unknown route falls back to stairs");
        }

        static object Field(Claims c, string k) => k switch { "what" => c.what, "time" => c.time, "route" => c.route, "advice" => c.advice, "unknowns" => (object)c.unknowns, _ => throw new ArgumentException(k) };

        [Test]
        public void ParseNoteReadsFreeTextByRule()
        {
            var cases = new (string text, (string k, object v)[] expected)[]
            {
                ("동쪽문 막힘ㅠ 난 계단으로 옴 이쪽으로 ㄱㄱ", new[] { ("what", (object)"gate_closed"), ("route", "stairs") }),
                ("대피소 전부 닫힘", new[] { ("what", (object)"all_closed") }),
                ("다른 입구는 모두 확인 못 했어", new[] { ("what", (object)null), ("unknowns", true) }),
                ("C로 와", new[] { ("route", (object)"access") }),
                ("ㅋㅋㅋ", new[] { ("what", (object)null), ("route", null), ("advice", null), ("time", null) }),
                ("2시 10분에 동쪽 출입구 통제 중", new[] { ("what", (object)"gate_closed"), ("time", "2시 10분") }),
                ("20:09 대피소 동쪽 출입구 닫힘. 다른 입구는 몰라", new[] { ("what", (object)"gate_closed"), ("time", "20:09"), ("unknowns", true) }),
                ("나 대피소에 막 도착했어", new[] { ("what", (object)null) }),
                ("대피시설이 닫혀 있었어", new[] { ("what", (object)"all_closed") }),
                ("20:09 동쪽 닫힘. 나머지 다 닫혔는지 확인 못 함", new[] { ("what", (object)"gate_closed"), ("unknowns", true) }),
                ("한빛 생활문화센터 지하주차장으로 와", new[] { ("route", (object)null) }),
                ("계단 말고 다른 입구로 가", new[] { ("route", (object)null) }),
                ("1시간 전에 동쪽 문 닫혀 있었어", new[] { ("time", (object)null), ("what", "gate_closed") }),
                ("20:09 동쪽 폐쇄. B 진출입로 운영. C는 몰라. 안내 다시 확인!", new[] { ("what", (object)"gate_closed"), ("time", "20:09"), ("route", "ramp"), ("unknowns", true), ("advice", "check_notice") }),
            };
            foreach (var (text, expected) in cases)
            {
                var c = Relay.ParseNote(text);
                foreach (var (k, v) in expected) Assert.AreEqual(v, Field(c, k), $"{text} → {k}");
            }
            foreach (var empty in new[] { "", null })
            {
                var c = Relay.ParseNote(empty);
                Assert.AreEqual("null|null|null|False|null", $"{c.what ?? "null"}|{c.time ?? "null"}|{c.route ?? "null"}|{c.unknowns}|{c.advice ?? "null"}");
            }
            Assert.AreEqual("precise", Relay.NoteKind(Relay.ParseNote("20:09 대피소 동쪽 출입구 닫힘. 다른 입구는 몰라")));
            Assert.AreEqual("unparsed", Relay.NoteKind(Relay.ParseNote("ㅋㅋㅋ")));
            Assert.AreEqual("generalized", Relay.NoteKind(Relay.ParseNote("대피소 전부 닫힘")));
            Assert.AreEqual("partial", Relay.NoteKind(Relay.ParseNote("동쪽 출입구가 닫혀 있었어")));
            Assert.AreEqual("precise", Relay.NoteKind(Relay.ParseNote("20:09에 동쪽 출입구가 닫혀 있었어. 다른 입구 상태는 확인 못 했어.")));
        }

        // Port check: JS regex semantics (ASCII \b and \d next to Hangul, clause order, quotes) against node's output.
        [Test]
        public void ParseNoteMatchesTheJsOutput()
        {
            var cases = new[]
            {
                ("A계단으로 와", "null|null|stairs|false|null|partial|route=A계단으로 와"),
                ("a로 오면 돼", "null|null|stairs|false|null|partial|route=a로 오면 돼"),
                ("AB 둘 다 닫힘", "all_closed|null|null|false|null|generalized|what=AB 둘 다 닫힘"),
                ("B진출입로 운영 중", "null|null|ramp|false|null|partial|route=B진출입로 운영 중"),
                ("C경사로 열림", "null|null|access|false|null|partial|route=C경사로 열림"),
                ("ABC 모두 확인 못 함", "null|null|null|true|null|partial|unknowns=ABC 모두 확인 못 함"),
                ("입구A 막혀있음", "null|null|stairs|false|null|partial|route=입구A 막혀있음"),
                ("20:09에 동문 닫혔어", "gate_closed|20:09|null|false|null|partial|what=20:09에 동문 닫혔어;time=20:09에 동문 닫혔어"),
                ("8시간 기다림", "null|null|null|false|null|unparsed|"),
                ("8 시 30 분 동쪽 폐쇄", "gate_closed|8 시 30 분|null|false|null|partial|what=8 시 30 분 동쪽 폐쇄;time=8 시 30 분 동쪽 폐쇄"),
                ("9시에 다 막혔어", "all_closed|9시|null|false|null|generalized|what=9시에 다 막혔어;time=9시에 다 막혔어"),
                ("전부 막혀 있었어", "all_closed|null|null|false|null|generalized|what=전부 막혀 있었어"),
                ("확인 안 했어 다른 데는", "null|null|null|true|null|partial|unknowns=확인 안 했어 다른 데는"),
                ("모두 닫힘! 동쪽은 확실", "all_closed|null|null|false|null|generalized|what=모두 닫힘"),
                ("대피소는 지금 폐쇄됐어", "all_closed|null|null|false|null|generalized|what=대피소는 지금 폐쇄됐어"),
                ("계단 쓰지 마", "null|null|null|false|null|unparsed|"),
                ("경사로 피해서 와", "null|null|null|false|null|unparsed|"),
                ("왼쪽으로 꺾어서 쭉", "null|null|null|false|direction|partial|advice=왼쪽으로 꺾어서 쭉"),
                ("나처럼 따라와", "null|null|null|false|follow_me|partial|advice=나처럼 따라와"),
                ("안내판에서 다시 확인해", "null|null|null|false|check_notice|partial|advice=안내판에서 다시 확인해"),
                ("확인해 보고 안내 따라", "null|null|null|false|check_notice|partial|advice=확인해 보고 안내 따라"),
                ("120:30 동쪽", "null|null|null|false|null|unparsed|"),
                ("23:59 동쪽 통제", "gate_closed|23:59|null|false|null|partial|what=23:59 동쪽 통제;time=23:59 동쪽 통제"),
                ("19:5 동쪽", "null|null|null|false|null|unparsed|"),
                ("동쪽,닫힘", "null|null|null|false|null|unparsed|"),
                ("동쪽 출입구 근처 차량 통제", "null|null|null|false|null|unparsed|"),
                ("다 닫힘. 모르겠어. C로 가", "all_closed|null|access|true|null|generalized|what=다 닫힘;route=C로 가;unknowns=모르겠어"),
                ("막 왔어 B로", "null|null|ramp|false|null|partial|route=막 왔어 B로"),
                ("b 진출입로 말고 계단", "null|null|null|false|null|unparsed|"),
                ("미확인 정보: 전체 폐쇄", "null|null|null|true|null|partial|unknowns=미확인 정보: 전체 폐쇄"),
                ("대피시설 막힘\n계단으로 와", "all_closed|null|stairs|false|null|generalized|what=대피시설 막힘;route=계단으로 와"),
            };
            foreach (var (text, expect) in cases)
            {
                var c = Relay.ParseNote(text);
                var got = $"{c.what ?? "null"}|{c.time ?? "null"}|{c.route ?? "null"}|{(c.unknowns ? "true" : "false")}|{c.advice ?? "null"}|{Relay.NoteKind(c)}|{string.Join(";", c.quotes.Select(kv => kv.Key + "=" + kv.Value))}";
                Assert.AreEqual(expect, got, text);
            }
        }

        [Test]
        public void MakeHandoverKeepsThePlayersWordsSanitizesEditsAndFallsBackToChips()
        {
            var chips = new Chips { what = "gate_closed", time = true, route = true, unknowns = true, advice = null };
            var G = Played("ramp");
            var h = Relay.MakeHandover(G, chips);
            Assert.AreEqual(Relay.ComposeNote(chips, new NoteCtx { route = "ramp", observedVia = "sign", gateSeenAt = G.relay.gateSeenAt }), h.text);
            Assert.IsTrue(h.text.Contains("20:09"));
            Assert.AreEqual("precise", h.kind);
            Assert.AreEqual("chips", h.claims.parsedBy);
            Assert.AreEqual("ramp", h.actualRoute);
            Assert.AreEqual(T(G.relay), $"{h.gateSeenAt}|{h.sentAt}|{h.accessOpenAt}|{h.receivedAt}");
            Assert.IsTrue(ReferenceEquals(h, G.handover));
            var last = G.events[G.events.Count - 1];
            Assert.AreEqual($"precise|False|{h.text.Length}", $"{last.Str("kind")}|{last.Bool("edited")}|{last.Num("length")}");
            CollectionAssert.AreEqual(new[] { "kind", "edited", "length" }, last.details.Keys.ToList());
            Assert.AreEqual("handover_written", last.type);
            chips.what = "all_closed";
            Assert.AreEqual("gate_closed", h.chips.what, "chips are copied");

            var edited = Relay.MakeHandover(Played(), chips, $"  동쪽\u0007 막힘 {new string('가', 200)}", edited: true);
            Assert.AreEqual(Relay.MAX_NOTE, edited.text.Length);
            Assert.IsFalse(Regex.IsMatch(edited.text, @"[\x00-\x1F\x7F]"));
            Assert.AreEqual("rules", edited.claims.parsedBy);
            Assert.IsTrue(edited.edited);

            foreach (var text in new[] { "   ", "\u0000\u0007\n", null })
            {
                var blank = Relay.MakeHandover(Played(), chips, text, edited: true);
                Assert.AreEqual(Relay.ComposeNote(chips, new NoteCtx { route = "stairs", observedVia = "sign", gateSeenAt = "20:09" }), blank.text);
                Assert.IsFalse(blank.edited);
            }
            Assert.AreEqual("chips", Relay.MakeHandover(Played(), chips, "무시됨").claims.parsedBy, "text counts only when edited");

            var noRelay = State.CreateGame(3);
            Assert.AreEqual(Relay.RelayTimes(20 * 60 + 13).gateSeenAt, Relay.MakeHandover(noRelay, chips).gateSeenAt, "default times without G.relay");
            Assert.AreEqual("unparsed", Relay.MakeHandover(State.CreateGame(3)).kind);
            foreach (var bad in new[] { null, new Game { events = null } }) Assert.Throws<ArgumentException>(() => Relay.MakeHandover(bad, chips));
        }

        [Test]
        public void BuildPinsAlwaysGivesBodyTimeAndOneMoreReasonWhateverWasWritten()
        {
            var texts = new[] { "동쪽문 막힘ㅠ 난 계단으로 옴 이쪽으로 ㄱㄱ", "C로 와", "ㅋㅋㅋ", "대피소 전부 닫힘", "20:09 동쪽 폐쇄. B 진출입로 운영. C 경사로는 몰라. 안내 다시 확인" };
            var games = new List<Game>();
            foreach (var route in new[] { "stairs", "ramp" })
            {
                foreach (var chips in AllChips()) { var G = Played(route); Relay.MakeHandover(G, chips); games.Add(G); }
                foreach (var text in texts) { var G = Played(route); Relay.MakeHandover(G, null, text, edited: true); games.Add(G); }
            }
            games.Add(State.CreateGame(4));
            games.Add(Played("ramp"));
            var anchors = new HashSet<string> { "stairs", "ramp", "notice", "gate", "access", "start0", "start1" };
            foreach (var G in games)
            {
                var pins = Relay.BuildPins(G);
                Assert.AreEqual(3, pins.Count);
                Assert.AreEqual("body", pins[0].factor);
                Assert.AreEqual("time", pins[1].factor);
                Assert.IsTrue(new[] { "scope", "advice", "direction", "place" }.Contains(pins[2].factor));
                foreach (var pin in pins)
                {
                    Assert.IsTrue(anchors.Contains(pin.anchor), pin.anchor);
                    Assert.IsTrue(!string.IsNullOrEmpty(pin.was) && !string.IsNullOrEmpty(pin.now));
                    Assert.IsFalse(BLAME.IsMatch(pin.was + pin.now));
                }
                Assert.IsFalse(Relay.ENTRANCES[pins[0].anchor].seoyeonOk);
                if (pins[1].anchor == "notice") Assert.IsTrue(pins[1].now.Contains(G.relay?.receivedAt ?? "20:17"));
            }
            var scoped = Played(); Relay.MakeHandover(scoped, new Chips { what = "all_closed" });
            Assert.AreEqual("scope", Relay.BuildPins(scoped)[2].factor);
            var access = Played(); Relay.MakeHandover(access, null, "C로 와", edited: true);
            Assert.AreEqual("access", Relay.BuildPins(access)[1].anchor);
            Assert.AreEqual("stairs", Relay.BuildPins(access)[0].anchor, "body pin falls back to where 정민 entered");
            // 정민 who only read the notice after arriving never "saw" the gate.
            var told = Played("stairs", "notice_after"); Relay.MakeHandover(told, new Chips { what = "all_closed" });
            Assert.IsFalse((Relay.BuildPins(told)[2].now + string.Join(" ", Relay.ReadClaims(told).Select(r => r.why))).Contains("직접"));
        }

        [Test]
        public void ClosingLinesTwoCalmLinesForEveryKind()
        {
            foreach (var kind in new[] { "precise", "partial", "generalized", "unparsed", null })
            {
                var lines = Relay.ClosingLines(kind);
                Assert.AreEqual(2, lines.Length);
                foreach (var l in lines) Assert.IsFalse(BLAME.IsMatch(l));
            }
            Assert.AreNotEqual(string.Join("\n", Relay.ClosingLines("precise")), string.Join("\n", Relay.ClosingLines("generalized")));
        }

        [Test]
        public void ReadClaimsAlwaysListsCAsAfterTheNoteAndCitesRealSources()
        {
            var statuses = new HashSet<string> { "helped", "recheck", "mismatch", "missing" };
            var G = Played(); Relay.MakeHandover(G, new Chips { what = "gate_closed", route = true, advice = "follow_me" });
            var rows = Relay.ReadClaims(G);
            Assert.IsTrue(rows.Any(r => r.label == "C 경사로" && r.status == "missing" && r.why.Contains(G.relay.accessOpenAt)));
            Assert.IsTrue(rows.Any(r => r.label == "들어간 길" && r.status == "mismatch"));
            Assert.IsTrue(rows.Any(r => r.label == "시각" && r.status == "missing"));
            Assert.IsTrue(rows.Any(r => r.label == "본 것" && r.status == "recheck"));
            var precise = Played(); Relay.MakeHandover(precise, new Chips { what = "gate_closed", time = true, unknowns = true, advice = "check_notice" });
            var p = Relay.ReadClaims(precise);
            Assert.IsTrue(p.Any(r => r.label == "본 것" && r.status == "helped"));
            Assert.IsFalse(p.Any(r => r.status == "mismatch"));
            var access = Played(); Relay.MakeHandover(access, null, "C로 와", edited: true);
            Assert.AreEqual("recheck", Relay.ReadClaims(access).First(r => r.label == "C 경사로").status);
            foreach (var row in rows.Concat(p).Concat(Relay.ReadClaims(access)))
            {
                Assert.IsTrue(Facts.SOURCES.Any(s => s.id == row.sourceId), row.sourceId);
                Assert.IsTrue(statuses.Contains(row.status), row.status);
                Assert.IsFalse(BLAME.IsMatch(row.why));
            }
            Assert.AreEqual(0, Relay.ReadClaims(State.CreateGame(5)).Count);
            Assert.AreEqual(0, Relay.ReadClaims(null).Count);
        }
    }
}
