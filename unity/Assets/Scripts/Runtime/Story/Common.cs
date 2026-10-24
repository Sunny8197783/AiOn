// Ports story/common.js: scenes several chapters reuse — the radio (portable or the phone FM app), emergency
// texts, official fact notices, the light in your hand, the phone battery.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using UnityEngine;

namespace Gyeongbo.Story
{
    public static class Common
    {
        public const string AREA = "[연무구(가상)]";
        static string F(string id) => Facts.Fact(id)?.text;
        static Func<Game, List<string>> K(params string[] lines) => _ => lines.ToList();

        // Broadcast scripts. Every line is fiction for the scenario; official advice lines quote FACTS.
        public static readonly Dictionary<string, Func<Game, List<string>>> BROADCASTS = new Dictionary<string, Func<Game, List<string>>>
        {
            ["normal"] = K("지금은 평상시 프로그램입니다. (가상 방송)", "비상시에는 이 방송에서 정부와 지자체 안내를 전합니다."),
            ["blackout"] = K("연무구청이 알려 드립니다. (가상 방송)", "오늘 19시 40분쯤 연무동 일대에 정전이 발생했습니다. 원인을 확인하고 있으며 복구 시간은 아직 정해지지 않았습니다.", "라디오와 재난문자로 전달되는 안내를 계속 들어 주십시오."),
            ["shelter"] = _ => new List<string> { "공습경보가 발령 중입니다. (가상 방송)", "대피소 안에서는 관리자의 안내를 따르고, 출입구와 통로를 막지 마십시오.", F("radio_listen") },
            ["water"] = _ => new List<string> { "연무구청이 알려 드립니다. (가상 방송)", "단수가 이어지고 있어 연무동 행정복지센터 마당의 비상급수시설을 운영합니다.", $"운영 시간은 오전 9시 30분부터 오후 5시까지, 물통을 가져오십시오. {F("water_point")}" },
            ["rumor"] = _ => new List<string> { "연무구청이 알려 드립니다. (가상 방송)", "생활문화센터 대피소는 정상 운영 중입니다. 확인되지 않은 정보에 주의해 주십시오.", F("info_rumor") },
            ["crossroads"] = _ => new List<string> { "연무구청이 알려 드립니다. (가상 방송)", "희망하는 주민은 생활문화센터 임시 거처로 옮길 수 있습니다. 권고이며 명령이 아닙니다.", "집에 머무는 주민도 행정복지센터 비상급수시설을 계속 이용할 수 있습니다.", F("info_rumor") },
            // Today's closures come from this run's conditions, so the broadcast never names an open road.
            ["evac"] = G => new List<string> { "연무구청이 알려 드립니다. (가상 방송)", "연무동 주민 이동 지원을 시작합니다. 행정복지센터 앞 임시 집결지에서 오후 2시까지 버스가 출발합니다.",
                $"한 사람당 가방 하나, 신분증을 챙기십시오. 오늘 통제 구간: {string.Join(", ", G.cond.closures.evac.Select(id => Regex.Replace(TownMap.CLOSURES[id].label, " 통제$", "")))}. 현장 안내를 따르십시오." },
        };

        public static bool Has(Game G, string id) => G.items.TryGetValue(id, out var e) && e != null && e.home + e.bag > 0;

        // What the disaster-broadcast station is saying now (the phone radio opened with P).
        public static string CurrentBroadcast(Game G)
        {
            bool After(string t) => G.events.Any(e => e.type == t);
            if (G.chapter == "dday") return After("alert") ? "shelter" : After("blackout") ? "blackout" : "normal";
            if (G.chapter == "endure") return G.day <= 1 ? "water" : "rumor";
            if (G.chapter == "crossroads") return "crossroads";
            if (G.chapter == "evac" || G.chapter == "ending") return "evac";
            return "normal";
        }

        static TalkOptions T(string speaker, string role, params string[] lines) => new TalkOptions { speaker = speaker, role = role, lines = lines.ToList() };

        // Radio scene. Returns heard lines, or null when it cannot be played. Without a working radio the phone's
        // FM app (earphones as the antenna) is offered while the phone has battery; it costs battery.
        // device: 'phone' skips the portable radio (the P key).
        public static async Task<List<string>> RadioScene(Ctx ctx, string script, string title = "라디오", string device = null)
        {
            var G = ctx.G;
            title ??= "라디오";
            string dev = device; int extraSeconds = 0;
            if (dev == null && Has(G, "radio"))
            {
                dev = "radio";
                if (!G.items["radio"].@checked)
                {
                    var r = State.Check(G, "radio");
                    if (r.ok) { extraSeconds = 40; ctx.Pass(2); ctx.Toast("어둠 속에서 건전지를 찾아 넣었다. 시간이 조금 걸렸다."); }
                    else { await ctx.ui.Talk(T(title, "휴대용 라디오", "건전지가 없어 켜지지 않는다.", F("radio_batt"))); State.RecordEvent(G, "radio_blocked", Details.Of("script", script)); dev = null; }
                }
            }
            if (dev == null)
            {
                if (G.phone.battery <= 0) { await ctx.ui.Talk(T(title, "라디오 없음", "라디오가 없고, 휴대폰도 꺼져 있다.")); return null; }
                var t = T(title, "라디오 없음", "쓸 수 있는 라디오가 없다. 휴대폰 FM 라디오 앱으로 들어 볼까?", $"(배터리가 닳는다 · 지금 {G.phone.battery}%)");
                t.choices = new List<Choice> { new Choice("phone", "휴대폰 FM 라디오를 켠다"), new Choice("no", "그만둔다") };
                if (await ctx.ui.Talk(t) != "phone") return null;
                dev = "phone";
            }
            if (dev == "phone" && G.phone.battery <= 0) { await ctx.ui.Talk(T(title, "휴대폰", "휴대폰이 꺼져 있다.")); return null; }
            var lines = BROADCASTS.TryGetValue(script ?? "", out var b) ? b(G) : new List<string>();
            var mode = Drills.LadderStage(G, "radio");
            var start = mode == "free" ? 89.5 : (G.know.radioFreq ?? 89.5);
            var res = await ctx.ui.Radio(new RadioOptions { mode = mode, start = start, battery = true, extraSeconds = extraSeconds, device = dev,
                onSignal = c => ctx.audio.Radio(c != null, c ?? 0), stations = new List<Station> {
                    new Station { freq = 95.1, name = "음악 방송", kind = "music", lines = new List<string> { "(잔잔한 음악이 흐른다)" } },
                    new Station { freq = 99.3, name = "알 수 없는 방송", kind = "rumor", lines = new List<string> { "…들은 얘긴데요, 그쪽 대피소는 이미 꽉 찼다더라고요… (출처를 밝히지 않는 방송)" } },
                    new Station { freq = PrepActions.RADIO_FREQ, name = $"재난방송 주관방송사 라디오 (게임 속 주파수 {PrepActions.RADIO_FREQ}MHz)", kind = "official", lines = lines },
                } });
            if (dev == "phone") { DrainBattery(G, 3); ctx.Refresh(); } else State.Use(G, "radio");
            Drills.RecordEncounter(G, "radio", res != null && res.official ? "tuned" : "missed");
            if (res != null && res.official) G.know.radioFreq = PrepActions.RADIO_FREQ;
            foreach (var n in res?.noted ?? new List<RadioNote>()) ctx.Note(n.text, n.kind == "official" ? "official" : "rumor");
            return res != null && res.official ? lines : null;
        }

        // Light in the dark: flashlight if usable, else the phone (drains battery).
        public static string LightSource(Ctx ctx)
        {
            var G = ctx.G;
            if (G.items.TryGetValue("flashlight", out var f) && f != null && f.@checked && Has(G, "flashlight")) return "flashlight";
            return G.phone.battery > 0 ? "phone" : null;
        }

        public static async Task EmergencyText(Ctx ctx, string kind, string time, string text)
        {
            var G = ctx.G;
            kind ??= "위급재난문자";
            State.RecordEvent(G, "sms", Details.Of("kind", kind, "time", time));
            if (G.phone.battery <= 0) { ctx.Toast("휴대폰이 꺼져 있어 문자를 볼 수 없다. 주변 안내를 따른다.", "site"); return; }
            if (kind == "위급재난문자") ctx.audio.PhoneAlert("emergency"); else ctx.audio.PhoneAlert("safety");
            ctx.Note(text, "official");
            await ctx.Modal(() => ctx.ui.EmergencyAlert(new AlertOptions { kind = kind, text = text, time = time }));
        }

        // Official guidance arrives as notices that do not stop play, and lands in the phone's messages (P) to reread.
        public static Task FactCard(Ctx ctx, string title, IList<string> ids)
        {
            var lines = ids.Select(F).Where(s => s != null).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i]; var first = i == 0;
                State.Note(ctx.G, t, "official", ctx.engine.Level?.id);
                StoryRuntime.After(i * 2.6f, () => ctx.Toast(first ? $"{title} — {t}" : t, "official", 6500));
            }
            return Task.CompletedTask;
        }

        public static void DrainBattery(Game G, double n) => G.phone.battery = Math.Max(0, G.phone.battery - n);
    }
}
