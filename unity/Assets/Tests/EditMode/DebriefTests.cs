// Ports tests/debrief.test.mjs. The JS test reads dist/story/*.js for the literals the story passes; the story
// is not part of this assembly, so those literals are listed here (taken from dist/story with the JS regexes).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gyeongbo.Logic;
using NUnit.Framework;

namespace Gyeongbo.Tests
{
    public class DebriefTests
    {
        static readonly string[] STORY_RESULTS = { "escalated", "risky", "retreat-ok", "planned", "practice", "written" };
        static readonly string[] STORY_PREPS = { "flashlight", "radio", "bag", "entrances", "route", "docs", "tub", "water_jug", "cash", "extinguisher", "judgement", "powerbank" };
        static readonly string[] STORY_CONTACTS = { "both", "office", "store" };
        static readonly Regex BLAME = new Regex(@"잘못|실수|탓|게으|무책임|undefined|null|NaN|\[object");
        static readonly Regex HANGUL = new Regex("[가-힣]");
        static readonly Regex LATIN = new Regex("[A-Za-z]");

        static Dictionary<string, int> Prices() => Items.CATALOG.ToDictionary(kv => kv.Key, kv => kv.Value.price);
        static Dictionary<string, int> Cart(params object[] kv) { var d = new Dictionary<string, int>(); for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = (int)kv[i + 1]; return d; }
        static HashSet<string> SrcIds() => new HashSet<string>(Facts.SOURCES.Select(s => s.id));

        static Game NewGame(Dictionary<string, int> cart = null)
        {
            var G = State.CreateGame(5);
            G.money = 10000000;
            State.Buy(G, cart ?? new Dictionary<string, int>(), Prices());
            return G;
        }

        static Game AlertReadyGame()
        {
            var G = NewGame(Cart("water_500", 1));
            G.know.shelterLocated = G.know.entrancesChecked = G.know.routeChecked = G.know.extinguisherLocated = G.know.gateClosedSeen = true;
            State.Pack(G, "water_500");
            return G;
        }

        static void Match(string pattern, string s, string msg = null) => Assert.IsTrue(s != null && Regex.IsMatch(s, pattern), msg ?? $"\"{s}\" should match /{pattern}/");

        [Test]
        public void EveryFactsSourceIdExistsAndSourcesAreUniqueHttpsLinks()
        {
            var srcIds = SrcIds();
            Assert.AreEqual(Facts.SOURCES.Count, srcIds.Count);
            Assert.AreEqual(Facts.FACTS.Count, Facts.FACTS.Select(f => f.id).Distinct().Count());
            foreach (var f in Facts.FACTS)
            {
                Assert.IsTrue(srcIds.Contains(f.sourceId), $"{f.id} -> {f.sourceId}");
                Assert.IsTrue(f.text.Trim().Length > 0, f.id);
            }
            foreach (var s in Facts.SOURCES) Assert.IsTrue(Uri.TryCreate(s.url, UriKind.Absolute, out var u) && u.Scheme == "https", s.id);
        }

        [Test]
        public void FailDebriefGivesThreeCalmKoreanLinesAndRealSourcesForEveryType()
        {
            CollectionAssert.AreEqual(new[] { "dead", "late", "exhausted", "fire", "lostWay" }, Debrief.FAIL_TYPES);
            var srcIds = SrcIds();
            var fresh = State.CreateGame(9);
            foreach (var G in new[] { fresh, AlertReadyGame() })
                foreach (var type in Debrief.FAIL_TYPES)
                {
                    var d = Debrief.FailDebrief(G, type);
                    foreach (var (k, v) in new[] { ("title", d.title), ("seen", d.seen), ("missed", d.missed), ("next", d.next) })
                    {
                        Assert.IsTrue(v != null && v.Trim().Length > 0, $"{type}.{k}");
                        Assert.IsTrue(HANGUL.IsMatch(v), $"{type}.{k}");
                        Assert.IsFalse(BLAME.IsMatch(v), $"{type}.{k}: {v}");
                    }
                    Assert.Greater(d.sourceIds.Count, 0);
                    Assert.AreEqual(d.sourceIds.Count, d.sourceIds.Distinct().Count());
                    foreach (var s in d.sourceIds) Assert.IsTrue(srcIds.Contains(s), s);
                }
            Assert.Throws<ArgumentException>(() => Debrief.FailDebrief(fresh, "boom"));
            Assert.Throws<ArgumentException>(() => Debrief.FailDebrief(fresh, null));
        }

        [Test]
        public void LateLinesFollowWhatWasChecked()
        {
            var G = State.CreateGame(1);
            var d = Debrief.FailDebrief(G, "late");
            Match("확인 전", d.seen);
            Match("대피소·경로 확인", d.next);
            G.know.shelterLocated = true;
            Match("생활문화센터", Debrief.FailDebrief(G, "late").seen);
            G.know.entrancesChecked = G.know.routeChecked = G.know.gateClosedSeen = true;
            d = Debrief.FailDebrief(G, "late");
            Match("동쪽 출입구", d.seen);
            Match("비상가방 꾸리기", d.next);
        }

        [Test]
        public void ExhaustedOverweightBagNoWaterOrNoRest()
        {
            var G = NewGame(Cart("water_2l", 2));
            State.Pack(G, "water_2l", 4);
            var d = Debrief.FailDebrief(G, "exhausted");
            Assert.IsTrue(d.missed.Contains("8.2kg") && d.missed.Contains($"{Items.BAG_LIMIT.weightKg}kg"), d.missed);
            Assert.IsTrue(d.sourceIds.Contains(Facts.Fact("bag_foil").sourceId));
            var dry = State.CreateGame(2);
            Match("마실 물", Debrief.FailDebrief(dry, "exhausted").missed);
            Match("휴식", Debrief.FailDebrief(AlertReadyGame(), "exhausted").next);
            Match("갈증은 좋음", Debrief.FailDebrief(dry, "exhausted").seen);
            State.Adjust(dry, "thirst", 80);
            Match("갈증은 나쁨", Debrief.FailDebrief(dry, "exhausted").seen);
        }

        [Test]
        public void FireTheLastExtinguisherEncounterPicksTheFactsLine()
        {
            var G = State.CreateGame(3);
            Assert.AreEqual(Facts.Fact("fire_shout").text, Debrief.FailDebrief(G, "fire").missed);
            Match("확인하기 전", Debrief.FailDebrief(G, "fire").seen);
            Drills.RecordEncounter(G, "extinguisher", "escalated"); // what story/evac.js passes
            Assert.AreEqual(Facts.Fact("fire_big").text, Debrief.FailDebrief(G, "fire").missed);
            Drills.RecordEncounter(G, "extinguisher", Drills.EvalExtinguisher(new[] { "shout", "aim", "pin", "squeeze", "sweep" }, "small"));
            var d = Debrief.FailDebrief(G, "fire");
            Assert.AreEqual(Facts.Fact("ext_1").text, d.missed, "first error was the pin");
            Assert.IsTrue(d.sourceIds.Contains("mois_extinguisher"));
            G.know.extinguisherLocated = true;
            Match("알고 있었습니다", Debrief.FailDebrief(G, "fire").seen);
            // After a save/load the eval object is a plain dictionary; the card reads it the same way.
            G.events[G.events.Count - 1].details["result"] = new Dictionary<string, object> { ["result"] = "incomplete", ["correct"] = false, ["firstError"] = "pin" };
            Assert.AreEqual(Facts.Fact("ext_1").text, Debrief.FailDebrief(G, "fire").missed);
        }

        [Test]
        public void LostWayUsesTheSeedTimes()
        {
            var L = State.CreateGame(11);
            var d = Debrief.FailDebrief(L, "lostWay");
            Assert.IsTrue(d.seen.Contains(State.Hhmm(L.cond.commsWeakAt)));
            Assert.AreEqual(Facts.Fact("shelter_signs").text, d.missed);
            L.know.routeChecked = true;
            Match("두 번째 경로", Debrief.FailDebrief(L, "lostWay").next);
        }

        [Test]
        public void ChapterSummaryCountsOnlyThatChapter()
        {
            var G = NewGame();
            G.chapter = "prep";
            State.SpendSlot(G, "mart", 2);
            State.SpendSlot(G, "bag", 1);
            State.Buy(G, Cart("water_500", 3, "batteries_aa", 1), Prices());
            State.Check(G, "flashlight");
            Drills.RecordEncounter(G, "bag", "partial");
            Drills.RecordEncounter(G, "extinguisher", Drills.EvalExtinguisher(new[] { "retreat" }, "small"));
            State.Note(G, "라디오 89.1MHz 재난방송", "official");
            State.Note(G, "내일 물이 끊긴대", "rumor");
            State.RecordEvent(G, "help", Details.Of("who", "seoyeon", "what", "연락 방법을 정했다"));
            State.RecordEvent(G, "payoff", Details.Of("prep", "bag", "moment", "현관에 둔 배낭을 메고 바로 나섰다"));
            G.chapter = "dday";
            State.RecordEvent(G, "payoff", Details.Of("prep", "radio", "moment", "안내를 바로 들었다"));

            var s = Debrief.ChapterSummary(G, "prep");
            CollectionAssert.AreEqual(new[]
            {
                "준비에 쓴 시간: 3칸",
                "산 물건: 생수 500mL 3병, AA 건전지 4개 1팩",
                "점검해서 쓸 수 있게 된 것: 손전등(LED)",
                "비상가방: 몇 가지가 빠졌습니다",
                "소화기: 대피하며 알렸습니다",
                "수첩에 적은 정보 2개 (공식 안내 1, 소문·미확인 1)",
                "오서연: 연락 방법을 정했다",
                "지금 상태: 체력 좋음, 갈증 좋음, 피로 좋음, 긴장 좋음",
            }, s.lines);
            Assert.AreEqual(1, s.payoffs.Count);
            Assert.AreEqual("비상가방|현관에 둔 배낭을 메고 바로 나섰다|bag", $"{s.payoffs[0].prep}|{s.payoffs[0].moment}|{s.payoffs[0].id}");
            var d = Debrief.ChapterSummary(G, "dday");
            Assert.AreEqual(1, d.lines.Count);
            CollectionAssert.AreEqual(new[] { "radio" }, d.payoffs.Select(p => p.id).ToList());
            var none = Debrief.ChapterSummary(State.CreateGame(1), "nothing");
            CollectionAssert.AreEqual(new[] { "지금 상태: 체력 좋음, 갈증 좋음, 피로 좋음, 긴장 좋음" }, none.lines);
            Assert.AreEqual(0, none.payoffs.Count);
            State.Adjust(G, "thirst", 80);
            State.Adjust(G, "fatigue", 30);
            State.Adjust(G, "health", -75);
            Assert.AreEqual("지금 상태: 체력 나쁨, 갈증 나쁨, 피로 보통, 긴장 좋음", Debrief.ChapterSummary(G, "x").lines[0]);
        }

        [Test]
        public void NoRawResultIdOrPrepIdReachesThePlayer()
        {
            var G = State.CreateGame(1);
            G.chapter = "c";
            var results = STORY_RESULTS.Cast<object>().Concat(new object[] { "out", "incomplete", "escalated", "retreat-ok", "something-new",
                Drills.EvalExtinguisher(new[] { "retreat" }, "large"), Drills.EvalBag(G, "alert"), new Dictionary<string, object> { ["station"] = null }, new RadioSignalResult(), null }).ToList();
            foreach (var r in results) Drills.RecordEncounter(G, "radio", r);
            foreach (var prep in STORY_PREPS) State.RecordEvent(G, "payoff", Details.Of("prep", prep, "moment", "순간"));
            var s = Debrief.ChapterSummary(G, "c");
            foreach (var line in s.lines) Assert.IsFalse(LATIN.IsMatch(line), line);
            foreach (var p in s.payoffs.Concat(Debrief.EndingRecord(G).helped)) Assert.AreNotEqual(p.id, p.prep, $"label for payoff '{p.id}'");
            foreach (var c in STORY_CONTACTS)
            {
                G.rel["seoyeon"].contact = c;
                foreach (var p in Debrief.EndingRecord(G).people) Assert.IsFalse(LATIN.IsMatch(p.what), p.what);
            }
        }

        [Test]
        public void EndingRecordOnAFreshGame()
        {
            var r = Debrief.EndingRecord(State.CreateGame(1));
            Assert.AreEqual(0, r.helped.Count);
            Assert.AreEqual(1, r.unused.Count);
            Assert.AreEqual($"{Items.CATALOG["flashlight"].name}|건전지를 넣지 않아 켜 볼 수 없었습니다.", $"{r.unused[0].item}|{r.unused[0].why}");
            Assert.AreEqual(9, r.@unchecked.Count);
            Assert.AreEqual(0, r.people.Count);
            Assert.AreEqual(Facts.Fact("shelter_find").text, r.practice.text);
            Assert.AreEqual("kr_shelters", r.practice.sourceId);
        }

        [Test]
        public void EndingRecordFollowsPayoffsUnusedItemsKnowFlagsAndPeople()
        {
            var G = NewGame(Cart("radio", 1, "batteries_aa", 1, "powerbank", 1, "tape", 1, "water_500", 2));
            State.Check(G, "radio");
            State.Consume(G, "water_500", 1);
            G.items["bogus"] = new ItemEntry { home = 1, bought = 1 }; // dropped from the catalogue
            State.RecordEvent(G, "payoff", Details.Of("prep", "flashlight", "moment", "정전 직후 손전등을 바로 켰다"));
            State.RecordEvent(G, "help", Details.Of("who", "seoyeon", "what", "계단에서 보행기를 옆에서 들어 주었다"));
            G.rel["manager"].contact = "both";
            G.rel["seoyeon"].contact = "sms";
            G.know.shelterLocated = true;
            var r = Debrief.EndingRecord(G);
            Assert.AreEqual(1, r.helped.Count);
            Assert.AreEqual("손전등 건전지|정전 직후 손전등을 바로 켰다|flashlight", $"{r.helped[0].prep}|{r.helped[0].moment}|{r.helped[0].id}");
            var why = r.unused.ToDictionary(u => u.item, u => u.why);
            CollectionAssert.AreEqual(new[] { "flashlight", "radio", "powerbank", "tape" }.Select(id => Items.CATALOG[id].name).OrderBy(n => n, StringComparer.Ordinal).ToList(),
                why.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList());
            Assert.AreEqual("충전해 두지 않아 쓸 수 없었습니다.", why[Items.CATALOG["powerbank"].name]);
            Assert.AreEqual("사 두었지만 쓸 일이 없었습니다.", why[Items.CATALOG["tape"].name]);
            Assert.IsTrue(!why.ContainsKey(Items.CATALOG["water_500"].name) && !why.ContainsKey(Items.CATALOG["batteries_aa"].name), "used items are not unused");
            Assert.IsFalse(r.@unchecked.Contains("가까운 대피소 위치"));
            CollectionAssert.AreEqual(new[]
            {
                "오서연|계단에서 보행기를 옆에서 들어 주었다",
                "박영수 소장|비상 연락 방법을 정했습니다 (문자·문에 쪽지)",
                "오서연|비상 연락 방법을 정했습니다 (문자)",
            }, r.people.Select(p => $"{p.who}|{p.what}").ToList());
            Assert.AreEqual(Facts.Fact("shelter_signs").text, r.practice.text, "next unchecked flag");
            Assert.AreEqual("law_sign", r.practice.sourceId);

            G.know.entrancesChecked = G.know.routeChecked = G.know.extinguisherLocated = G.know.radioChecked = G.know.contactsCard = G.know.docsCopied = G.know.waterPoint = true;
            G.know.meetPoint = "놀이터";
            var all = Debrief.EndingRecord(G);
            Assert.AreEqual(0, all.@unchecked.Count);
            Assert.AreEqual(Facts.Fact("shelter_know").text, all.practice.text);
            Assert.AreEqual("mois_four", all.practice.sourceId);
            Assert.IsTrue(SrcIds().Contains(all.practice.sourceId));
        }

        [Test]
        public void FireFailCardFollowsTheSceneItCameFrom()
        {
            var G = State.CreateGame(1);
            var up = Debrief.FailDebrief(G, "fire", Details.Of("scene", "upstairs"));
            var sh = Debrief.FailDebrief(G, "fire", Details.Of("scene", "shelter"));
            var co = Debrief.FailDebrief(G, "fire");
            Match("창문을 닫고|대기", up.missed);
            Assert.AreNotEqual(co.title, up.title);
            Assert.AreNotEqual(co.title, sh.title);
            foreach (var d in new[] { up, sh }) Assert.IsTrue(d.sourceIds.Count > 0 && !string.IsNullOrEmpty(d.next));
        }
    }
}
