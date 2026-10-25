// Ports story/seoyeon.js: the 서연 interlude — "같은 문장, 다른 사람": 정민 writes a message from the shelter
// (UI Compose), the point of view switches to 서연 at 카페 쉼표, the re-read of the message against what 정민
// actually did (NoteCheck pins, Relay.BuildPins), then 서연's own walk to an entrance she can use.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using Gyeongbo.UI;
using UnityEngine;
using static Gyeongbo.Story.SB;

namespace Gyeongbo.Story
{
    public static class Seoyeon
    {
        // 서연's start in town (JS space).
        public static readonly Spawn CAFE = new Spawn(-118, 0, -12.5f, (float)Math.PI);

        static Vector3 At(MapPoint p) => new Vector3((float)p.x, 0, (float)p.z);   // JS space, as NoteCheck takes it
        static readonly Dictionary<string, Vector3> ANCHOR = new Dictionary<string, Vector3>
        {
            ["stairs"] = At(TownMap.PLACES["shelterA"]), ["ramp"] = At(TownMap.PLACES["shelterB"]), ["notice"] = At(TownMap.PLACES["shelterNotice"]),
            ["gate"] = At(TownMap.PLACES["shelterEast"]), ["access"] = At(TownMap.PLACES["shelterC"]), ["start0"] = At(TownMap.SPAWNS["home"]),
            ["start1"] = new Vector3(CAFE.x, CAFE.y ?? 0, CAFE.z),
        };
        static int ToMin(string s) { var hm = s.Split(':'); return int.Parse(hm[0]) * 60 + int.Parse(hm[1]); }

        public static async Task SeoyeonRelay(Ctx ctx)
        {
            var G = ctx.G;
            var gate = G.events.FirstOrDefault(e => e.type == "gate_observed" && e.Str("who") == "jeongmin");
            var gateMin = gate?.Num("minute");
            G.relay = Relay.RelayTimes(G.minute + 2, gateMin.HasValue ? (int)gateMin.Value : G.minute - 3);
            G.handover = null;
            if (!Contact(G, "seoyeon"))
            {
                await ctx.Card(Card("info", "서연에게 연락할 방법이 없다", "303호 이웃", L("서연의 번호를 모른다. 서연은 오늘 저녁 밖에 있다고 했던 것 같은데….", "준비할 때 이웃과 연락 방법을 정해 두지 않았다."), Cs(C("ok", "계속", true))));
            }
            else
            {
                if (G.phone.battery <= 3 && State.Usable(G, "powerbank")) { State.Use(G, "powerbank"); G.phone.battery = 45; ctx.Payoff("powerbank", "꺼져 가던 휴대폰을 보조배터리로 살렸다"); }
                if (G.phone.battery <= 0)
                {
                    await ctx.Card(Card("info", "휴대폰이 꺼졌다", "배터리 0%", L("서연에게 문자를 보내려 했지만 휴대폰이 켜지지 않는다."), Cs(C("ok", "계속", true))));
                }
                else
                {
                    var nc = Relay.NoteContext(G);
                    var res = await ctx.Modal(() => Compose.Write(new ComposeContext { route = nc.route, observedVia = nc.observedVia, gateSeenAt = G.relay.gateSeenAt, sentAt = G.relay.sentAt }));
                    Relay.MakeHandover(G, res?.chips, res?.text, res != null && res.edited);
                    Common.DrainBattery(G, 2);
                    ctx.Toast($"{F("comms_text")}", "official", 5000);
                }
            }

            // 서연's side. The clock is hers now.
            G.minute = ToMin(G.relay.receivedAt);
            ctx.hud.SetChapter("2장", "같은 문장, 다른 사람");
            ctx.Refresh();
            var body = L("이제 303호 서연입니다. 서연은 보행 보조기를 쓰고, 병원 진료 뒤 카페에 있다가 경보를 들었습니다.");
            body.AddRange(PrepActions.SEOYEON_CARD.Select(l => $"· {l}"));
            await ctx.Card(Card("info", "시점 전환 · 오서연", $"{G.relay.receivedAt} · 카페 쉼표 앞", body, Cs(C("ok", "서연으로", true))));
            ctx.SetLevelState("town", "gates", new Dictionary<string, string> { ["east"] = "closed", ["stairs"] = "open", ["ramp"] = "open", ["access"] = "open" });
            ctx.SetLevelState("town", "crowd", "evacuating");
            await ctx.Goto("town", CAFE);
            _ = ctx.SetPlayer("seoyeon");   // JS: ctx.setPlayer?.('seoyeon') without await
            ctx.speedOverride = 0.55f;
            ctx.Refresh();
            if (G.handover != null) await Reread(ctx);
            else await ctx.Card(Card("sms", "받은 문자 없음", G.relay.receivedAt, L("서연에게 정민의 문자는 오지 않았습니다. 서연은 지금의 현장 안내만으로 판단합니다."), Cs(C("ok", "계속", true))));
            await Walk(ctx);
            ctx.speedOverride = null;
            _ = ctx.SetPlayer("jeongmin");
            G.minute = Math.Max(G.minute, ToMin(G.relay.receivedAt) + 5);
            ctx.hud.SetChapter("2장", "정전과 경보");
        }

        static async Task Reread(Ctx ctx)
        {
            var G = ctx.G; var h = G.handover; var pins = Relay.BuildPins(G);
            var engine = ctx.engine;
            engine.SetControls(false);
            ctx.hud.SetCinematic(true);
            var fx = NoteCheck.CreateReread(engine, CAFE, ctx.settings.reduceMotion);
            Pin placePin(int i)
            {
                var p = pins[i];
                fx.Pin(i + 1, ANCHOR[p.anchor], p.was, p.now);
                // ponytail: JS labels this pin '✕'; NoteCheck.Pin takes an int, so 0 stands in (contract request: string n).
                if (p.also != null) fx.Pin(0, ANCHOR[p.also.anchor], Relay.ENTRANCES[p.also.anchor].label, p.also.now, 2.2f);
                if (p.line != null) fx.Link(ANCHOR[p.anchor], ANCHOR[p.line]);
                return p;
            }
            var steps = new List<Func<Task<string>>>
            {
                () => { engine.SetView("first"); fx.Eye(-0.5f); Compose.ShowNote(h, "phone"); return Compose.RereadCaption("방금 정민으로서 쓴 문장입니다. 서연에게 닿은 것은 이 문장뿐입니다."); },
                () => { Compose.ShowNote(h, "marked"); return Compose.RereadCaption(h.kind == "unparsed" ? "이 문장에서 입구·시각·범위를 찾지 못했어요." : "문장을 나눠 봅니다. 표시한 부분이 이 문장이 서연에게 말하는 것입니다."); },
                () => { Compose.HideNote(); engine.SetView("third"); fx.Crane(); fx.ShowPath(ctx.trail ?? new List<XZ>()); return Compose.RereadCaption("정민이 실제로 걸은 길 — 당신은 알고 있지만, 서연은 보지 못했습니다."); },
                () => Compose.RereadCaption("정민이 들어갈 수 있던 입구는 둘 다 서연의 기준과 다릅니다.", placePin(0)),
                () => Compose.RereadCaption($"메시지는 {h.sentAt}에 보낸 것입니다. 지금은 {h.receivedAt}, 그 사이 안내판이 바뀌었습니다.", placePin(1)),
                () => Compose.RereadCaption(string.Join(" ", Relay.ClosingLines(h.kind)), placePin(2), true),
            };
            for (int i = 0; i < steps.Count; i++)
            {
                // Esc: jump to the closing step with every pin placed (step 3+i places pin i).
                if ((await steps[i]()) == "skip" && i < steps.Count - 1)
                {
                    Compose.HideNote();
                    if (i < 2) { engine.SetView("third"); fx.Crane(); fx.ShowPath(ctx.trail ?? new List<XZ>()); }
                    for (int j = Math.Max(0, i - 2); j < 2; j++) placePin(j);
                    i = steps.Count - 2;
                }
            }
            State.RecordEvent(G, "reread_done", Details.Of("pins", pins.Select(p => (object)p.factor).ToList()));
            fx.Dispose(); Compose.HideNote();
            ctx.hud.SetCinematic(false);
            engine.SetView(ctx.settings.view);
            engine.SetControls(true);
        }

        static async Task Walk(Ctx ctx)
        {
            var G = ctx.G; var t = G.relay;
            var read = false;
            void show() => ctx.Objective("서연 · 지금의 안내로 대피하기", "정민의 문자는 참고만 합니다. 현장 안내판을 보고, 서연에게 맞는 출입구로 갑니다.", new List<ObjectiveStep> { new ObjectiveStep("생활문화센터 현장 안내판 확인", read), new ObjectiveStep("서연에게 맞는 출입구로", false) });
            show();
            await ctx.Roam(new RoamOptions
            {
                rate = 2, doors = false,
                on =
                {
                    ["shelterNotice"] = H(async p =>
                    {
                        var pick = await ctx.ui.Card(Card("notice", "지금 운영 중인 출입구", $"현장 안내 · {t.accessOpenAt} 갱신 · 이 시설",
                            L("동쪽 출입구: 폐쇄 유지", "A 계단: 운영", "B 지하주차장 진출입로: 운영 · 차량 통제 · 내리막이 가파름", $"C 경사로: {t.accessOpenAt} 개방 · 유도블록 · 안내 요원 있음", "어느 입구로 갈지는 서연이 정합니다."),
                            Cs(C("access", "C로 갈게요", true), C("stairs", "A로 갈게요"), C("ramp", "B로 갈게요"))));
                        read = true;
                        State.RecordEvent(G, "notice_read", Details.Of("who", "seoyeon", "minute", G.minute));
                        State.RecordEvent(G, "notice_choice", Details.Of("who", "seoyeon", "route", pick));
                        ctx.Note($"현장 안내 {t.accessOpenAt}: C 경사로 개방 (서연이 확인)", "site");
                        if (pick == "access") ctx.Toast($"C 경사로 · {t.accessOpenAt}에 열림 · 정민이 문자를 보낸 {t.sentAt} 뒤의 일", "site", 5000);
                        show();
                    }),
                },
                shelter = async point =>
                {
                    if (point.id == "shelterEast") { ctx.Toast("동쪽 출입구 폐쇄 · 안내판을 확인하세요", "site"); return null; }
                    if (point.id == "shelterNotice") return null;
                    if (point.id == "shelterC")
                    {
                        State.RecordEvent(G, "route_attempt", Details.Of("who", "seoyeon", "route", "access", "minute", G.minute));
                        await ctx.ui.Card(Card("info", "서연이 출입구 C로 대피했습니다", State.Hhmm(G.minute), L("완만한 경사로를 보조기와 함께 내려갔습니다.", F("shelter_rules")), Cs(C("ok", "정민에게로", true))));
                        return "in";
                    }
                    var route = point.id == "shelterA" ? "stairs" : "ramp";
                    State.RecordEvent(G, "route_attempt", Details.Of("who", "seoyeon", "route", route, "minute", G.minute));
                    var pick = await Talk(ctx, "오서연", "가까이 와서 보니", L(
                        route == "stairs" ? "계단은 혼자 내려가지 않아요. 보조기와 떨어지지도 않을 거예요." : "이 내리막은 가팔라서 혼자 내려가지 않아요.",
                        $"입구의 안내 요원이 C 경사로가 {t.accessOpenAt}에 열렸다고 알려 준다."),
                        Cs(C("self", "C 경사로로 계획을 바꾼다"), C("escort", "안내 요원에게 C까지 함께 걸어 달라고 한다")));
                    State.RecordEvent(G, "help_choice", Details.Of("who", "seoyeon", "choice", pick));
                    if (pick == "escort")
                    {
                        await Talk(ctx, "안내 요원", "출입구", L("어떻게 도와드릴까요?"));
                        await Talk(ctx, "오서연", "도움 요청", L("C까지 옆에서 같이 걸어 주세요. 보조기는 제가 밀게요.", F("people_ask")));
                    }
                    ctx.Toast("C 경사로로 계획을 바꿨다.", "self");
                    return null;
                },
            });
        }
    }
}
