// app.js wiring: creating the services, the 청각 장애 sound cues and 저시력 speech, the body (falls, fights,
// thrown things), the global keys while roaming, the phone (P), the map (M), first aid (X) and the pause menu.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using Gyeongbo.Story;
using Gyeongbo.Systems;
using Gyeongbo.UI;
using UnityEngine;

namespace Gyeongbo.App
{
    public sealed partial class GameApp
    {
        // 청각 장애 mode: the sound is off, so loud sounds also arrive as on-screen cues.
        static readonly Dictionary<string, string> SOUND_CUES = new Dictionary<string, string>
        {
            ["boom"] = "[소리] 쾅— 큰 폭발음. 바닥이 울린다.", ["phoneAlert"] = "[진동] 휴대폰이 울리며 떨린다.", ["siren"] = "[소리] 사이렌이 울리기 시작했다.",
        };
        static readonly string[] OUCH = { "“아악! 왜 이래요!”", "“뭐 하는 거야!”", "“사람 살려요!”", "“그만해요, 제발!”", "“미쳤어?!”" };
        static readonly string[][] KEYS =
        {
            new[] { "W A S D · 방향키", "이동" }, new[] { "마우스", "둘러보기 — 화면을 한 번 클릭하면 마우스만 움직여도 시점이 돕니다 (Esc로 커서 되찾기)" },
            new[] { "Shift", "달리기 (기력이 줄어듭니다. 가방이 무거우면 더 빨리 지칩니다)" }, new[] { "Space", "점프 · 낮은 담/차/난간 앞에서 누르면 넘거나 올라감" },
            new[] { "클릭 · Q", "주먹 (손에 든 게 있으면 던지기)" }, new[] { "G", "바닥의 돌·병 줍기 / 던지기" }, new[] { "C", "엎드리기/웅크리기 (폭발·파편 피해가 줄어듭니다. Space로 일어서기)" }, new[] { "X", "구급함 쓰기" },
            new[] { "B", "분필로 바닥에 적기 — 다음 회차의 내가 보게 됩니다" }, new[] { "E · Enter", "살피기 · 대화 · 사용" },
            new[] { "F", "손전등 켜기/끄기 (정전 후)" }, new[] { "V", "1인칭 / 3인칭 전환" }, new[] { "P", "휴대폰 (문자 · FM 라디오)" }, new[] { "Tab", "수첩" }, new[] { "I", "소지품" }, new[] { "M", "지도" }, new[] { "H", "이 조작법 다시 보기" }, new[] { "Esc", "일시정지 · 설정" },
        };

        string itemLabel;
        bool flashOn;
        float flashT;

        void Setup()
        {
            settings = Settings.Load();
            audio = GameAudio.Create();
            GameEvents.UiSound += n => audio.Ui(n);
            engine = GameEngine.Create(new EngineOptions { quality = settings.quality, reduceMotion = settings.reduceMotion, onQualityChange = q => { settings.quality = q; settings.Save(); } });
            UiRoot.Create();
            hud = Hud.Create(UiRoot.HudLayer);
            audio.Played += (fn, arg) => { if (ProfileId == "deaf" && SOUND_CUES.TryGetValue(fn, out var text) && (fn != "siren" || arg is bool on && on)) hud.Toast(text, "site", 4200); };
            hud.OnCaption += Speak;
            engine.OnNearest(p => { if (p != null) Speak(p.label); });
            ui = new AppUi(engine);
            ctx = StoryRuntime.Create(engine, hud, ui, audio, settings, () => G, g => G = g, PersistCheckpoint);
            // The 서연 interlude swaps who you are; the engine keeps one controller, so swap the model it drives.
            ctx.setPlayerImpl = preset => engine.SetPlayerPreset(preset);
            ApplySettings();

            actions = Actions.Create(engine, new ActionCallbacks
            {
                onHit = (p, how, ko) =>
                {
                    if (G == null || danger.IsHostile(p) || how == "debris") return; // self-defence against a looter is not counted
                    G.karma = (G.karma ?? 0) - (ko ? 3 : 1);
                    ctx.Event("violence", Details.Of("how", how, "ko", ko));
                    var who = engine.Level?.npcs?.FirstOrDefault(kv => kv.Value == p).Key;
                    if (who != null && G.rel.TryGetValue(who, out var rel) && rel != null) rel.trust -= 2;
                    ctx.Toast(ko ? "상대가 바닥에 쓰러졌다." : OUCH[UnityEngine.Random.Range(0, OUCH.Length)], "site", 2600);
                },
                onHurt = Hurt,
                onSound = n => audio.Hit(n),
                onNear = label => { itemLabel = label; if (engine.Nearest == null) hud.Prompt(label); },
            });
            engine.OnNearest(p => { if (p == null && itemLabel != null) hud.Prompt(itemLabel); });
            engine.OnLand(drop =>
            {
                audio.Hit("land");
                if (G == null || drop < 3.2f) return;
                if (drop > 5.5f && !HasLeg())
                {
                    (G.injury ??= new List<string>()).Add("leg"); ApplySettings(); ctx.Event("injury", Details.Of("part", "leg", "drop", (int)Math.Floor(drop + 0.5)));
                    ctx.Toast("착지하는 순간 다리가 꺾였다. 뼈가 부러진 것 같다 — 이제 달릴 수 없다.", "self", 6500);
                }
                Hurt(drop > 10 ? 200 : (float)Math.Floor((drop - 3) * 15 + 0.5), "fall");
            });
            danger = Danger.Create(ctx, actions, Hurt);
            echo = Echo.Create(engine, ctx);
            // Cutscenes: the director, a harmless blast for them, and time skips shown as time passing over the town.
            ctx.cine = Cine.Create(ctx);
            ctx.boomImpl = (x, z, size) => danger.Trigger("boom", x, z, size);
            ctx.skipOverride = (from, to, text) => Cutscenes.Timelapse(ctx, from, to, text);
            ctx.beforeGoto = () => actions.Drop();
            engine.OnKey(k => Forget(OnKey(k.code), "key " + k.code));
            SetupFrame();
        }

        // 저시력 mode: what is in front of you and the captions are read aloud.
        // ponytail: no speech synthesis in plain Unity; logs instead. Upgrade: a platform TTS plugin (ko-KR) here.
        void Speak(string text)
        {
            if (ProfileId != "lowvision" || string.IsNullOrEmpty(text)) return;
            Debug.Log("[speak] " + text);
        }

        void Hurt(float n, string why)
        {
            if (G == null || !(n > 0)) return;
            ctx.deathCause = why; ctx.Stat("health", -n); hud.Hurt(n / 40);
        }

        // X: the first-aid kit (in the bag, or at home) patches you up; a broken leg stays broken.
        void FirstAid()
        {
            G.items.TryGetValue("first_aid", out var e);
            var atHome = engine.Level?.id == "apartment";
            if (e == null || !(e.bag > 0 || (atHome && e.home > 0))) { ctx.Toast("쓸 수 있는 구급함이 없다."); return; }
            if (G.stats.health >= 95) { ctx.Toast("지금은 처치할 상처가 없다."); return; }
            if (e.bag > 0) e.bag--; else e.home--;
            e.used++; ctx.Stat("health", 35); ctx.Event("first_aid", new Dictionary<string, object>()); ctx.Toast("상처를 소독하고 붕대를 감았다. 조금 낫다.", "self");
        }

        // Global keys while roaming. Menus open only during free roam, so no scripted step runs behind them.
        async Task OnKey(string code)
        {
            if (G == null || !engine.ControlsEnabled) return;
            if (code == "KeyF")
            {
                if (!flashOn && (ctx.lightSource == null || (ctx.lightSource == "phone" && G.phone.battery <= 0))) { ctx.Toast("켤 수 있는 불빛이 없다."); return; }
                flashOn = !flashOn; engine.Player.SetFlashlight(flashOn);
            }
            else if (code == "KeyV") { settings.view = settings.view == "first" ? "third" : "first"; settings.Save(); }
            else if (!ctx.roaming) return;
            else if (code == "Tab") await ctx.Modal(() => ui.Notebook(G.info));
            else if (code == "KeyI") await ctx.Modal(() => ui.Inventory(new InventoryOptions { home = G.items, bag = G.items, catalog = Items.CATALOG, load = State.BagLoad(G), limit = Items.BAG_LIMIT }));
            else if (code == "KeyH") await ctx.Modal(() => ControlsCard());
            else if (code == "KeyX") FirstAid();
            else if (code == "KeyB") await echo.Chalk();
            else if (code == "KeyP") await ctx.Modal(() => OpenPhone());
            else if (code == "KeyM") await ctx.Modal(() => ui.Routemap(new RouteMapOptions { mode = "view", from = NearestNode(), to = "shelter", options = new List<RouteOption>(), closures = KnownClosures(),
                gates = new Dictionary<string, string>(), known = new KnownMap(), playerAt = engine.Level?.id == "town" ? new XZ(engine.Player.X, engine.Player.Z) : (XZ?)null }));
            else if (code == "Escape") await OpenPause();
        }

        // The map shows closures in force now, and only once the player has learned of them this phase.
        List<string> KnownClosures()
        {
            var knows = G.chapter == "evac" ? G.know.evacClosuresKnown : G.know.closureSeen || G.know.closureHeard;
            return knows && ctx.levelStates.TryGetValue("town", out var town) && town.TryGetValue("closures", out var c) ? Sv.StrList(c) : new List<string>();
        }

        string NearestNode()
        {
            if (engine.Level?.id != "town") return "home";
            var p = new MapPoint { x = engine.Player.X, z = engine.Player.Z };
            return TownMap.NODES.OrderBy(kv => TownMap.Dist(kv.Value, p)).First().Key;
        }

        // ---- controls card (start of a new game, and H while roaming) ----
        Task<string> ControlsCard() => ui.Card(new CardOptions { kind = "info", title = "조작법", meta = "언제든 H 키로 다시 볼 수 있습니다",
            body = KEYS.Select(k => $"{k[0]} — {k[1]}").ToList(), actions = new List<Choice> { new Choice("ok", "확인", true) } });

        // ---- P: the phone (recent official texts, FM radio app) ----
        async Task OpenPhone()
        {
            if (G.phone.battery <= 0) { ctx.Toast("휴대폰이 꺼져 있다."); return; }
            var official = G.info.Where(i => i.label == "official").ToList();
            var messages = official.Skip(Math.Max(0, official.Count - 4)).Select(i => new PhoneMessage { from = "알림", kind = "safety", time = "", text = i.text }).ToList();
            var pick = await ui.Phone(new PhoneOptions { battery = G.phone.battery, signal = string.IsNullOrEmpty(G.phone.signal) ? "ok" : G.phone.signal, messages = messages,
                actions = new List<Choice> { new Choice("radio", "FM 라디오 앱"), new Choice("close", "닫기") } });
            if (pick == "radio") await Story.Common.RadioScene(ctx, Story.Common.CurrentBroadcast(G), "휴대폰", "phone");
        }

        async Task OpenPause()
        {
            var pick = await ctx.Modal(() => Menu.Pause());
            if (pick == "settings") { AssignSettings(await ctx.Modal(() => Menu.SettingsMenu(settings.Clone()))); ApplySettings(); }
            else if (pick == "save") { if (G?.checkpoint != null) SaveNow(); ctx.Toast("마지막 체크포인트를 저장했습니다."); }
            else if (pick == "menu") Reload();
        }

        // location.reload(): drop this game object and the world, start a fresh GameApp (the world is all code).
        // ponytail: the old chapter task is abandoned mid-await (never resumed) and UiRoot.Create must tolerate a
        // second call; upgrade: a CancellationToken through the chapters plus a UiRoot.Clear.
        void Reload()
        {
            try { engine.Dispose(); } catch (Exception e) { Debug.LogException(e); }
            Destroy(gameObject);
            var go = new GameObject("Gyeongbo");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }
    }
}
