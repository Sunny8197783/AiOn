// Ports story/prologue.js: 프롤로그 — a Tuesday at the 한결물류 office: delivery cancellations on the dashboard
// (drawn on a Paint and set as the office 'dashboard' state), sorting signals into 확인된 사실 / 추정 / 소문,
// the walk home. D-3 화요일 17:30 · 한결물류 연무센터 운영팀. None of the signals proves a war.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;

namespace Gyeongbo.Story
{
    public static class Prologue
    {
        sealed class Signal { public string id, from, meta, text, answer, why; }

        static readonly Signal[] SIGNALS =
        {
            new Signal { id = "cancel", from = "운영 대시보드", meta = "사내 데이터 · 17:25 집계", text = "오늘 배송 취소 접수 1,284건. 지난주 같은 요일의 3.2배입니다.", answer = "fact",
                why = "우리 시스템에서 직접 센 숫자라 확인된 사실입니다. 다만 왜 늘었는지는 이 숫자만으로 알 수 없습니다." },
            new Signal { id = "partner", from = "협력사 공지", meta = "B물류 공식 메일 · 16:50", text = "내일부터 서해안 노선 운송이 지연됩니다. (사유는 적혀 있지 않음)", answer = "fact",
                why = "공식 공지가 왔다는 것은 사실입니다. 사유를 짐작해 덧붙이면 그때부터는 추정이 됩니다." },
            new Signal { id = "cause", from = "옆자리 동료", meta = "대화", text = "\"취소가 많은 건 다들 전쟁 날 줄 알아서야. 뻔하지.\"", answer = "guess",
                why = "취소 증가(사실)에 이유를 붙인 해석입니다. 그럴 수도 있지만 확인된 것은 아닙니다." },
            new Signal { id = "water", from = "동네 단톡방", meta = "사진·출처 없음", text = "\"○○동 대형마트 생수 다 털렸대ㅋㅋ 빨리 사러 가\"", answer = "rumor",
                why = "누가 언제 본 것인지 알 수 없는 말입니다. 확인 전까지는 소문으로 두는 것이 안전합니다." },
            new Signal { id = "embassy", from = "거래처 담당자", meta = "전화 통화", text = "\"어느 나라 대사관 직원들이 철수한다던데요.\"", answer = "rumor",
                why = "출처가 확인되지 않았습니다. 설령 사실이어도 이런 신호 하나가 전쟁을 확정하지는 않습니다." },
        };
        static readonly (string id, string label)[] LABELS = { ("fact", "확인된 사실"), ("guess", "추정·해석"), ("rumor", "소문·미확인") };
        static string Label(string id) => LABELS.First(l => l.id == id).label;

        // The office dashboard for alerts 0..3.
        public static Paint DashboardCanvas(int alerts)
        {
            var g = new Paint(1024, 640);
            var FONT = Kit.FONT;
            g.fillStyle = "#0d1418"; g.fillRect(0, 0, 1024, 640);
            g.fillStyle = "#1b2a31"; g.fillRect(0, 0, 1024, 70);
            g.fillStyle = "#e8eef0"; g.font = $"700 30px {FONT}"; g.fillText("한결물류 · 연무센터 운영 대시보드", 28, 46);
            g.fillStyle = "#7fd1c4"; g.font = $"500 22px {FONT}"; g.fillText("D-3 (화) 17:25 기준", 760, 46);
            var rows = new (string a, string b, string d, bool hot)[]
            {
                ("배송 취소 접수", "1,284건", "+220%", true), ("서해안 노선", "운송 지연 공지", "내일부터", true), ("수도권 간선", "정상", "—", false),
                ("거래처 휴업 알림", "3곳", "+3", alerts > 1), ("반품 회수", "412건", "+12%", false), ("기사 결근 연락", "9명", "+6", alerts > 2),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var (a, b, d, hot) = rows[i];
                var y = 110 + i * 82;
                g.fillStyle = hot ? "#3a1d20" : "#16232a"; g.fillRect(28, y, 968, 66);
                g.fillStyle = hot ? "#ff8a8a" : "#cfd8dc"; g.font = $"600 26px {FONT}"; g.fillText(a, 52, y + 42);
                g.fillStyle = "#ffffff"; g.font = $"700 28px {FONT}"; g.fillText(b, 420, y + 42);
                g.fillStyle = hot ? "#ffb4a8" : "#8fa3ab"; g.font = $"600 24px {FONT}"; g.fillText(d, 820, y + 42);
            }
            return g;
        }

        static List<ObjectiveStep> Steps(bool a, bool b, bool c) => new List<ObjectiveStep> { new ObjectiveStep("대시보드 확인", a), new ObjectiveStep("팀장과 이야기", b), new ObjectiveStep("퇴근", c) };
        static TalkOptions T(string speaker, string role, params string[] lines) => new TalkOptions { speaker = speaker, role = role, lines = lines.ToList() };

        // Chapter runner contract: resolves when the chapter is done (null; only the ending returns a value).
        public static async Task<string> Run(Ctx ctx)
        {
            var G = ctx.G;
            G.chapter = "prologue";
            ctx.outdoorEnv = "afternoon"; ctx.indoorEnv = "interior"; ctx.power = true;
            ctx.SetLevelState("office", "dashboard", DashboardCanvas(1));
            ctx.SetLevelState("office", "alerts", 1);
            await ctx.Goto("office", "entry", "interior");
            ctx.hud.SetChapter("프롤로그", "평소와 다른 화요일");
            ctx.Refresh();
            ctx.Toast("자리로 가서 대시보드를 확인하자.", "self", 4200);

            int sorted = 0; bool talked = false;
            ctx.Objective("대시보드 확인하기", "내 자리의 모니터를 확인합니다.", Steps(false, false, false));
            await ctx.Roam(new RoamOptions
            {
                doors = false,
                on = new Dictionary<string, Func<Point, Task<object>>>
                {
                    ["desk"] = async _ =>
                    {
                        if (sorted > 0) { ctx.Toast("이미 확인했습니다. 팀장과 이야기해 보세요."); return null; }
                        await ctx.ui.Card(new CardOptions { kind = "notice", title = "오늘 들어온 신호 다섯 가지", meta = "무엇이 확인된 것이고 무엇이 짐작일까?",
                            body = new List<string> { "각 신호를 확인된 사실 · 추정·해석 · 소문·미확인 가운데 하나로 나눠 보세요.", "정답보다 \"어떻게 알게 됐는가\"가 중요합니다." },
                            actions = new List<Choice> { new Choice("ok", "나눠 보기", true) } });
                        foreach (var s in SIGNALS)
                        {
                            var t = T(s.from, s.meta, s.text);
                            t.choices = LABELS.Select(l => new Choice(l.id, l.label)).ToList();
                            var pick = await ctx.ui.Talk(t);
                            var ok = pick == s.answer;
                            ctx.Event("signal_sorted", Details.Of("id", s.id, "pick", pick, "ok", ok));
                            if (ok) ctx.Toast($"맞아요 · {Label(s.answer)}", "self", 2200); // only a miss stops to explain
                            else await ctx.ui.Talk(T($"\"{Label(s.answer)}\"에 가깝습니다", Label(s.answer), s.why));
                            if (s.answer == "fact") ctx.Note(s.text, "site");
                        }
                        sorted = SIGNALS.Length;
                        ctx.SetLevelState("office", "dashboard", DashboardCanvas(3));
                        ctx.SetLevelState("office", "alerts", 3);
                        ctx.Objective("팀장과 이야기하기", "윤 팀장이 자리에 있습니다.", Steps(true, false, false));
                        return null;
                    },
                    ["coworker"] = async _ =>
                    {
                        if (sorted == 0) { ctx.Toast("먼저 대시보드를 확인해 보세요."); return null; }
                        if (talked) { ctx.Toast("\"내일 봐요. 건전지 잊지 말고.\"", "neighbor"); return null; }
                        await ctx.ui.Talk(T("윤 팀장", "운영팀장", "정민 씨, 서해안 노선은 내일 전부 보류야. 고객 문의 오면 \"지연\"까지만 안내하고, 이유는 짐작해서 말하지 마."));
                        var q = T("윤 팀장", "운영팀장", "…다들 뭐라고 하는지는 나도 들었어.");
                        q.choices = new List<Choice> { new Choice("war", "다들 전쟁 난다고 하던데요?"), new Choice("ok", "확인된 것만 안내할게요.") };
                        var pick = await ctx.ui.Talk(q);
                        ctx.Event("talk_lead", Details.Of("pick", pick));
                        await ctx.ui.Talk(T("윤 팀장", "운영팀장",
                            pick == "war" ? "나도 몰라. 모르는 걸 아는 척하면 듣는 사람이 더 불안해져." : "그래, 그게 맞아. 모르는 건 모른다고 하는 게 일이야.",
                            "그래도 집에 비상용품은 한번 봐 둬. 나는 오늘 라디오 건전지부터 사려고. 있는 줄 알았는데 다 썼더라고."));
                        talked = true;
                        ctx.Objective("퇴근하기", "사무실 출입문으로 나갑니다.", Steps(true, true, false));
                        return null;
                    },
                    ["window"] = async _ => { await ctx.ui.Talk(T("창밖", "물류센터 마당", "출차를 기다리던 트럭들이 그대로 서 있다. 상자들이 마당에 쌓여 간다.")); return null; },
                    ["officeExit"] = _ =>
                    {
                        if (!talked) { ctx.Toast("아직 할 일이 남았습니다."); return Task.FromResult<object>(null); }
                        return Task.FromResult<object>("leave");
                    },
                },
            });

            await ctx.Skip("D-3 화 18:40", "D-2 수 18:30",
                "그날 밤, 정부는 일부 국가로의 출국을 제한한다고 발표했다. (이야기를 위한 가상 조건 · 실제 상황이 아닙니다)",
                "이틀 동안 뉴스는 엇갈렸다. 회사는 단축 근무에 들어갔다. 정민은 오늘 저녁과 내일 하루를 준비에 쓰기로 했다.");
            G.day = -2; G.minute = 18 * 60 + 30;
            return null;
        }
    }
}
