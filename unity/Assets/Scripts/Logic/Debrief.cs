// Ports game/debrief.js: fail cards, chapter summaries and the ending's 생존 기록. Lines describe the situation,
// never blame the player or anyone else; safety statements are FACTS texts, game advice names planner actions.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Gyeongbo.Logic
{
    // failDebrief(): 당시 확인한 정보 / 판단에서 놓친 부분 / 다음에 시도할 준비 + source ids.
    public sealed class FailDebriefData
    {
        public string title, seen, missed, next;
        public List<string> sourceIds = new List<string>();
    }

    // A 'payoff' event as the player sees it.
    public sealed class Payoff
    {
        public string prep, moment, id;
    }

    public sealed class ChapterSummaryData
    {
        public List<string> lines = new List<string>();
        public List<Payoff> payoffs = new List<Payoff>();
    }

    public sealed class UnusedItem
    {
        public string item, why;
    }

    public sealed class HelpLine
    {
        public string who, what;
    }

    public sealed class Practice
    {
        public string text, sourceId;
    }

    public sealed class EndingRecordData
    {
        public EndingFate fate;
        public List<Payoff> helped = new List<Payoff>();
        public List<UnusedItem> unused = new List<UnusedItem>();
        public List<string> @unchecked = new List<string>();
        public List<HelpLine> people = new List<HelpLine>();
        public Practice practice;
    }

    public static class Debrief
    {
        static readonly Dictionary<string, string> PEOPLE = new Dictionary<string, string> { ["manager"] = "박영수 소장", ["clerk"] = "이수아", ["seoyeon"] = "오서연" };
        static readonly Dictionary<string, string> CONTACT = new Dictionary<string, string> { ["sms"] = "문자", ["door"] = "문 두드리기", ["both"] = "문자·문에 쪽지", ["office"] = "관리사무소 창구", ["store"] = "가게로 찾아가기" };
        static readonly Dictionary<string, string> TOOL = new Dictionary<string, string> { ["radio"] = "라디오", ["bag"] = "비상가방", ["route"] = "경로 정하기", ["extinguisher"] = "소화기", ["docs"] = "비상연락처 카드" };
        // Every result string the story passes to recordEncounter, so no raw id reaches the player.
        static readonly Dictionary<string, string> RESULT = new Dictionary<string, string>
        {
            ["out"] = "불을 껐습니다", ["retreat-ok"] = "대피하며 알렸습니다", ["escalated"] = "불이 커져 물러났습니다", ["incomplete"] = "끝까지 마치지 못했습니다",
            ["risky"] = "연기 속으로 바로 나섰습니다", ["practice"] = "사용 순서를 익혔습니다", ["tuned"] = "공식 방송을 찾아 들었습니다", ["opened"] = "라디오를 켜 보았습니다",
            ["missed"] = "공식 방송은 찾지 못했습니다", ["complete"] = "필요한 것을 다 챙겼습니다", ["partial"] = "몇 가지가 빠졌습니다", ["planned"] = "경로를 정해 두었습니다",
            ["written"] = "카드를 적어 두었습니다",
        };
        static readonly Dictionary<string, string> STAT = new Dictionary<string, string> { ["health"] = "체력", ["thirst"] = "갈증", ["fatigue"] = "피로", ["tension"] = "긴장" };
        static readonly Dictionary<string, string> LABEL = new Dictionary<string, string> { ["official"] = "공식 안내", ["site"] = "현장 확인", ["neighbor"] = "이웃에게 들음", ["rumor"] = "소문·미확인", ["self"] = "내 기록" };

        // ctx.payoff(prep, moment) passes a preparation id; the player sees the Korean name.
        static readonly Dictionary<string, string> PREP = new Dictionary<string, string>
        {
            ["bag"] = "비상가방", ["cash"] = "현금", ["docs"] = "비상연락처 카드·서류 사본", ["entrances"] = "대피소 출입구 확인", ["extinguisher"] = "소화기 위치·사용 순서",
            ["flashlight"] = "손전등 건전지", ["judgement"] = "화재 상황 판단", ["powerbank"] = "보조배터리 충전", ["radio"] = "라디오 주파수·건전지",
            ["route"] = "경로 확인", ["tub"] = "받아 둔 물", ["water_jug"] = "접이식 물통",
        };

        static string Name(Dictionary<string, string> table, object key) => key is string k && table.TryGetValue(k, out var v) ? v : key == null ? null : Js.Str(key);

        static Payoff PayoffOf(GameEvent e)
        {
            var prep = e.Get("prep");
            return new Payoff { prep = Name(PREP, prep), moment = e.Str("moment"), id = prep == null ? null : Js.Str(prep) };
        }

        static List<string> SourcesOf(IEnumerable<string> ids) => ids.Select(id => Facts.Fact(id)?.sourceId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        static GameEvent LastEncounter(Game G, string tool) => G.events.LastOrDefault(e => e.type == "encounter" && e.Str("tool") == tool);
        // HUD value words; health is better high, the other stats are better low.
        static string Word(string stat, double v) { var good = stat == "health" ? v : 100 - v; return good >= 60 ? "좋음" : good >= 30 ? "보통" : "나쁨"; }

        static string ResultWord(object r)
        {
            if (r is string s) return RESULT.TryGetValue(s, out var w) ? w : "해 봤습니다";
            var res = Js.Get(r, "result");
            if (Js.Truthy(res)) return res is string rs && RESULT.TryGetValue(rs, out var w2) ? w2 : "해 봤습니다";
            return Js.Get(r, "ok") is bool ok ? (ok ? "잘 해냈습니다" : "아쉬움이 남았습니다") : "해 봤습니다";
        }

        sealed class Card
        {
            public string title, seen, missed, next;
            public string[] facts;
        }

        static string Detail(Dictionary<string, object> d, string key) => d != null && d.TryGetValue(key, out var v) ? v as string : null;

        // Health ran out: a fall, a fight, a blast or debris. detail.why says which.
        static Card Dead(Game G, Dictionary<string, object> detail)
        {
            var why = Detail(detail, "why") ?? "hurt";
            var W = new Dictionary<string, string[]>
            {
                ["fall"] = new[] { "높은 곳에서 떨어졌습니다", "떨어지기 전, 아래까지의 높이를 가늠할 시간이 있었습니다.", "내려갈 때는 계단·사다리처럼 몸을 붙일 수 있는 길을 먼저 찾기" },
                ["fight"] = new[] { "몸싸움 끝에 쓰러졌습니다", "상대가 먼저 물러설 여지가 있었습니다.", "시비가 붙으면 물건을 내주거나 자리를 피하는 것도 선택지로 두기" },
                ["blast"] = new[] { "폭발에 휘말렸습니다", "휘파람 같은 낙하음이 들린 뒤 몇 초의 여유가 있었습니다.", "낙하음이 들리면 바로 엎드리고, 건물 안쪽·지하로 몸을 숨기기" },
                ["debris"] = new[] { "무너진 잔해에 다쳤습니다", "금이 가고 먼지가 떨어지는 건물은 무너지기 전에 신호를 보냅니다.", "흔들리거나 금 간 건물 곁에서는 멀리 떨어져 걷기" },
                ["fire"] = new[] { "불길 속에서 쓰러졌습니다", "연기가 짙어지기 전에 빠져나갈 길이 있었습니다.", "불이 번진 곳은 자세를 낮추고 바람이 불어오는 쪽으로 빠져나가기" },
                ["hurt"] = new[] { "더 이상 버틸 수 없을 만큼 다쳤습니다", "쉬면서 상처를 돌볼 수 있는 순간이 있었습니다.", "구급함을 챙기고, 다쳤을 때는 안전한 곳에서 먼저 처치하기" },
            };
            var t = W.TryGetValue(why, out var w) ? w : new[] { "더 이상 버틸 수 없을 만큼 다쳤습니다", "쉬면서 상처를 돌볼 수 있는 순간이 있었습니다.", "다친 뒤에는 먼저 안전한 곳에서 처치하기" };
            var facts = why == "blast" || why == "debris" ? new[] { "alert_airraid", "shelter_near" } : why == "fire" ? new[] { "fire_big" } : new[] { "mind_breath" };
            return new Card { title = t[0], seen = t[1], missed = G.injury != null && G.injury.Count > 0 ? "이미 다친 몸으로 움직이고 있었습니다." : "몸 상태를 살필 여유가 없었습니다.", next = t[2], facts = facts };
        }

        static Card Late(Game G, Dictionary<string, object> detail)
        {
            var k = G.know;
            return new Card
            {
                title = "대피소에 제때 닿지 못했습니다",
                seen = k.gateClosedSeen ? "동쪽 출입구가 닫혀 있다는 것은 현장에서 확인했습니다."
                    : k.shelterLocated ? "대피소가 생활문화센터 지하라는 것은 알고 있었습니다."
                        : "경보는 들었지만, 가까운 대피소 위치는 아직 확인 전이었습니다.",
                missed = !k.entrancesChecked ? "출입구 A·B·C 가운데 어디가 열려 있는지 미리 알 방법이 없었습니다."
                    : !k.routeChecked ? "통제된 길을 돌아가는 경로는 아직 정해 두지 않은 상태였습니다."
                        : "경보가 울린 뒤 출발하기까지 시간이 걸렸습니다.",
                next = !k.shelterLocated || !k.entrancesChecked ? "준비 단계에서 '대피소·경로 확인'으로 출입구 A·B·C를 둘러보기"
                    : "'비상가방 꾸리기'로 가방을 현관에 두고 바로 나설 수 있게 하기",
                facts = new[] { "shelter_know", "shelter_near", "bag_take" },
            };
        }

        static Card Exhausted(Game G, Dictionary<string, object> detail)
        {
            var s = G.stats;
            var load = State.BagLoad(G);
            var water = new[] { "water_500", "water_2l" }.Any(id => G.items.TryGetValue(id, out var e) && e != null && e.bag > 0);
            return new Card
            {
                title = "더 걷기 어려울 만큼 지쳤습니다",
                seen = $"그때 갈증은 {Word("thirst", s.thirst)}, 피로는 {Word("fatigue", s.fatigue)} 상태였습니다.",
                missed = load.over.weight ? $"가방이 {Js.Num(load.weightKg)}kg으로, 들고 걷기 편한 {Js.Num(Items.BAG_LIMIT.weightKg)}kg을 넘었습니다."
                    : !water ? "가방 안에 마실 물이 없었습니다." : "중간에 쉴 시간이 따로 없었습니다.",
                next = load.over.weight ? "'비상가방 꾸리기'에서 무게 막대를 보며 꼭 필요한 것부터 담기"
                    : !water ? "'비상가방 꾸리기'에서 작은 생수병을 먼저 담기" : "준비 칸 하나를 '휴식'에 남겨 두기",
                facts = load.over.weight ? new[] { "bag_foil", "bag_days" } : new[] { "bag_days" },
            };
        }

        // detail.scene: 'corridor' (3장, default), 'upstairs' (4장 stay: smoke from another flat), 'shelter' (4장 move).
        static Card Fire(Game G, Dictionary<string, object> detail)
        {
            var scene = Detail(detail, "scene") ?? "corridor";
            if (scene == "upstairs") return new Card
            {
                title = "연기 속으로 불을 끄러 나갔습니다",
                seen = "우리 집 안으로는 연기나 불길이 들어오지 않은 상태였습니다.",
                missed = Facts.Fact("fire_stay").text,
                next = "문손잡이를 손등으로 먼저 만져 보고, 연기가 들어오지 않으면 집 안에서 창문을 닫고 119에 신고하기",
                facts = new[] { "fire_stay", "fire_judge", "fire_door" },
            };
            if (scene == "shelter") return new Card
            {
                title = "대피소의 큰 불 앞에서 버텼습니다",
                seen = "불길이 이미 천장 배관에 닿아 있었습니다.",
                missed = Facts.Fact("fire_big").text,
                next = "천장까지 닿은 불은 끄려 하지 말고, \"불이야!\" 외치며 물러나 119에 알리기",
                facts = new[] { "fire_big", "fire_shout", "fire_119" },
            };
            var r = LastEncounter(G, "extinguisher")?.Get("result");
            var res = r is string rs ? rs : Js.Get(r, "result") as string;
            var firstError = Js.Get(r, "firstError") as string;
            var stepFact = Drills.EXT_STEPS.FirstOrDefault(st => st.id == firstError && firstError != null)?.facts[0];
            var factId = res == "escalated" ? "fire_big" : stepFact ?? "fire_shout";
            return new Card
            {
                title = "복도의 불이 커졌습니다",
                seen = G.know.extinguisherLocated ? "복도 소화기가 어디 있는지는 알고 있었습니다." : "복도 소화기 위치는 확인하기 전이었습니다.",
                missed = Facts.Fact(factId).text,
                next = G.know.extinguisherLocated ? "불길이 천장에 닿았는지 먼저 보고, 끌지 대피할지 정하기"
                    : "'집 장비 점검' 때 복도 소화기 위치와 사용 순서를 확인하기",
                facts = new[] { factId, "fire_small", "ext_1" },
            };
        }

        static Card LostWay(Game G, Dictionary<string, object> detail)
        {
            var k = G.know;
            return new Card
            {
                title = "길을 찾느라 시간이 걸렸습니다",
                seen = k.routeChecked ? "미리 본 경로가 현장 통제로 막혀 있었습니다." : $"{State.Hhmm(G.cond.commsWeakAt)}쯤부터 휴대폰 지도가 잘 열리지 않았습니다.",
                missed = Facts.Fact("shelter_signs").text,
                next = k.routeChecked ? "돌아가는 두 번째 경로까지 정해 두기" : "'대피소·경로 확인'으로 경로를 미리 걸어 보고 수첩에 적어 두기",
                facts = new[] { "shelter_signs", "evac_note" },
            };
        }

        static readonly Dictionary<string, Func<Game, Dictionary<string, object>, Card>> FAIL = new Dictionary<string, Func<Game, Dictionary<string, object>, Card>>
        {
            ["dead"] = Dead, ["late"] = Late, ["exhausted"] = Exhausted, ["fire"] = Fire, ["lostWay"] = LostWay,
        };

        // Fail types: dead, late, exhausted, fire, lostWay (the FAIL table's keys).
        public static readonly List<string> FAIL_TYPES = FAIL.Keys.ToList();

        // { title, seen, missed, next, sourceIds }: 당시 확인한 정보 / 판단에서 놓친 부분 / 다음에 시도할 준비.
        // detail keys: dead { why }, fire { scene }.
        public static FailDebriefData FailDebrief(Game G, string type, Dictionary<string, object> detail = null)
        {
            if (type == null || !FAIL.TryGetValue(type, out var make)) throw new ArgumentException($"unknown fail type: {type ?? "undefined"}");
            var d = make(G, detail);
            return new FailDebriefData { title = d.title, seen = d.seen, missed = d.missed, next = d.next, sourceIds = SourcesOf(d.facts) };
        }

        static double Number(object v) => Js.IsNumber(v) ? Convert.ToDouble(v) : 0;

        public static ChapterSummaryData ChapterSummary(Game G, string chapter)
        {
            var evs = G.events.Where(e => e.chapter == chapter).ToList();
            List<GameEvent> Of(string type) => evs.Where(e => e.type == type).ToList();
            var lines = new List<string>();
            var slots = Of("slot");
            if (slots.Count > 0) lines.Add($"준비에 쓴 시간: {Js.Num(slots.Sum(e => e.Num("cost") ?? 0))}칸");
            var bought = new Dictionary<string, double>();
            foreach (var e in Of("buy"))
                if (e.Get("cart") is IDictionary cart)
                    foreach (DictionaryEntry kv in cart) { var id = Js.Str(kv.Key); bought[id] = (bought.TryGetValue(id, out var n) ? n : 0) + Number(kv.Value); }
            if (bought.Count > 0) lines.Add($"산 물건: {string.Join(", ", bought.Select(kv => $"{Items.ItemName(kv.Key)} {Js.Num(kv.Value)}{(Items.CATALOG.TryGetValue(kv.Key, out var c) ? c.unit : "개")}"))}");
            var checkedNames = Of("check").Select(e => Items.ItemName(e.Str("id"))).ToList();
            if (checkedNames.Count > 0) lines.Add($"점검해서 쓸 수 있게 된 것: {string.Join(", ", checkedNames)}");
            foreach (var e in Of("encounter")) lines.Add($"{Name(TOOL, e.Get("tool"))}: {ResultWord(e.Get("result"))}");
            var notes = G.info.Where(i => i.chapter == chapter).ToList();
            if (notes.Count > 0)
            {
                var byLabel = LABEL.Select(kv => (l: kv.Value, n: notes.Count(i => i.label == kv.Key))).Where(x => x.n > 0);
                lines.Add($"수첩에 적은 정보 {notes.Count}개 ({string.Join(", ", byLabel.Select(x => $"{x.l} {x.n}"))})");
            }
            foreach (var e in Of("help")) lines.Add($"{Name(PEOPLE, e.Get("who"))}: {Js.Str(e.Get("what"))}");
            lines.Add($"지금 상태: {string.Join(", ", STAT.Select(kv => $"{kv.Value} {Word(kv.Key, G.stats[kv.Key])}"))}");
            return new ChapterSummaryData { lines = lines, payoffs = Of("payoff").Select(PayoffOf).ToList() };
        }

        static readonly Dictionary<string, string> CHECK_WHY = new Dictionary<string, string> { ["batteries"] = "건전지를 넣지 않아 켜 볼 수 없었습니다.", ["charge"] = "충전해 두지 않아 쓸 수 없었습니다." };
        static readonly Dictionary<string, string> KNOW_TEXT = new Dictionary<string, string>
        {
            ["shelterLocated"] = "가까운 대피소 위치",
            ["entrancesChecked"] = "대피소 출입구 A·B·C와 경사로",
            ["routeChecked"] = "통제 구간을 돌아가는 경로",
            ["extinguisherLocated"] = "복도 소화기 위치",
            ["radioChecked"] = "라디오 주파수와 건전지",
            ["contactsCard"] = "종이에 적은 비상연락처",
            ["docsCopied"] = "중요 서류 사본",
            ["meetPoint"] = "만남의 장소",
            ["waterPoint"] = "비상급수시설 위치",
        };
        // First unchecked know flag picks the real-life practice line.
        static readonly string[][] PRACTICE =
        {
            new[] { "shelterLocated", "shelter_find" }, new[] { "entrancesChecked", "shelter_signs" }, new[] { "contactsCard", "contact_card" }, new[] { "meetPoint", "contact_meet" },
            new[] { "radioChecked", "radio_batt" }, new[] { "extinguisherLocated", "ext_1" }, new[] { "docsCopied", "bag_copies" }, new[] { "waterPoint", "water_point" },
        };

        // Durable items count as used when items[id].used > 0 (State.Use / State.Consume).
        public static EndingRecordData EndingRecord(Game G)
        {
            var helped = G.events.Where(e => e.type == "payoff").Select(PayoffOf).ToList();
            var unused = new List<UnusedItem>();
            foreach (var kv in G.items)
            {
                if (!Items.CATALOG.TryGetValue(kv.Key, out var c)) continue;
                var e = kv.Value;
                if (c.needsCheck != null && !e.@checked && e.home + e.bag > 0) unused.Add(new UnusedItem { item = c.name, why = CHECK_WHY.TryGetValue(c.needsCheck, out var w) ? w : null });
                else if (e.bought > 0 && e.used == 0) unused.Add(new UnusedItem { item = c.name, why = "사 두었지만 쓸 일이 없었습니다." });
            }
            var uncheckedTexts = KNOW_TEXT.Where(kv => !G.know.Has(kv.Key)).Select(kv => kv.Value).ToList();
            var people = G.events.Where(e => e.type == "help").Select(e => new HelpLine { who = Name(PEOPLE, e.Get("who")), what = Js.Str(e.Get("what")) }).ToList();
            foreach (var kv in G.rel)
                if (!string.IsNullOrEmpty(kv.Value?.contact))
                    people.Add(new HelpLine { who = Name(PEOPLE, kv.Key), what = $"비상 연락 방법을 정했습니다 ({Name(CONTACT, kv.Value.contact)})" });
            var f = Facts.Fact(PRACTICE.FirstOrDefault(p => !G.know.Has(p[0]))?[1] ?? "shelter_know");
            return new EndingRecordData { fate = Endings.PickEnding(G), helped = helped, unused = unused, @unchecked = uncheckedTexts, people = people, practice = new Practice { text = f.text, sourceId = f.sourceId } };
        }
    }
}
