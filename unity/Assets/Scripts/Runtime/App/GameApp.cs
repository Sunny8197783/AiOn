// Ports app.js: boot, settings and experience modes (PROFILES), save/continue, the chapter runner with soft-fail
// retries from the last checkpoint. Global keys, falls and fights are in AppPlay.cs; per-frame work (stamina,
// compass/minimap, footsteps, the wheelchair stair help, the guardian who walks along) in AppFrame.cs.
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Gyeongbo.App
{
    public sealed partial class GameApp : MonoBehaviour
    {
        // The chapter runner's order: [id, chapter]. Every chapter resolves null except 'ending'.
        public static readonly List<KeyValuePair<string, Func<Ctx, Task<string>>>> CHAPTERS = new List<KeyValuePair<string, Func<Ctx, Task<string>>>>
        {
            new KeyValuePair<string, Func<Ctx, Task<string>>>("prologue", Prologue.Run),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("prep", Prep.Run),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("dday", Dday.Run),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("endure", Endure.Run),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("crossroads", Evac.Crossroads),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("evac", Evac.Evacuation),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("ending", Evac.Ending),
        };

        // Experience modes (체험 모드): how the body moves and what the senses get; speed multiplies the story's speed.
        public static readonly Dictionary<string, Profile> PROFILES = new Dictionary<string, Profile>
        {
            ["none"] = new Profile { run = true, jump = true, eye = 1.62f, stairs = true, speed = 1 },
            ["mobility"] = new Profile { run = false, jump = false, eye = 1.62f, stairs = true, speed = 0.62f },    // 보행 장애: no running, slow
            ["wheelchair"] = new Profile { run = false, jump = false, eye = 1.18f, stairs = false, speed = 0.85f }, // seated eye height, ramps only
            ["lowvision"] = new Profile { run = true, jump = true, eye = 1.62f, stairs = true, speed = 0.8f },      // 저시력: blurred world, spoken prompts
            ["deaf"] = new Profile { run = true, jump = true, eye = 1.62f, stairs = true, speed = 1 },              // 청각 장애: no sound, captions and cues
            ["guardian"] = new Profile { run = false, jump = false, eye = 1.62f, stairs = true, speed = 0.72f },    // 지체 장애: a guardian walks along
        };

        Settings settings;
        GameAudio audio;
        GameEngine engine;
        Hud hud;
        AppUi ui;
        StoryRuntime ctx;
        Actions actions;
        Danger danger;
        Echo echo;
        Game G;
        int progressIndex;

        // Unity entry point: everything else is awaited inside Run; nothing escapes unlogged.
        async void Start()
        {
            try { await Run(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                try { UiRoot.ShowLoading("문제가 생겨 게임을 계속할 수 없습니다. 다시 시작해 주세요."); } catch (Exception e2) { Debug.LogException(e2); }
            }
        }

        string ProfileId => settings.profile != null && PROFILES.ContainsKey(settings.profile) ? settings.profile : "none";
        bool HasLeg() => G?.injury != null && G.injury.Contains("leg");

        void ApplySettings()
        {
            var p = PROFILES[ProfileId];
            UiRoot.SetTextScale(settings.textScale);
            UiRoot.SetReduceMotion(settings.reduceMotion);
            UiRoot.SetLowVision(ProfileId == "lowvision");
            audio.SetVolume(settings.volume); audio.SetMute(settings.mute || ProfileId == "deaf");
            audio.SetBus("ambience", settings.volAmbience); audio.SetBus("effects", settings.volEffects); audio.SetBus("ui", settings.volUi);
            engine.SetView(settings.view);
            engine.SetQuality(settings.quality); engine.SetReduceMotion(settings.reduceMotion);
            engine.SetSensitivity(settings.sensitivity);
            var q = p.Clone(); q.run = p.run && !HasLeg(); engine.SetProfile(q);   // a broken leg ends running for the rest of the run
            ctx.profileSpeed = p.speed; if (G != null) ctx.Refresh();
            settings.Save();
        }

        // Object.assign(settings, edited)
        void AssignSettings(Settings s) { if (s != null) JsonConvert.PopulateObject(JsonConvert.SerializeObject(s), settings); }

        // Runtime state a checkpoint needs besides G; saved with G so a reload resumes the same world.
        // Paints (the office dashboard) are redrawn by their chapter, so they are left out.
        Snapshot MakeSnapshot()
        {
            var states = new JObject();
            foreach (var lv in ctx.levelStates)
            {
                var o = new JObject();
                foreach (var kv in lv.Value) if (!(kv.Value is Paint) && !(kv.Value is UnityEngine.Object)) o[kv.Key] = kv.Value == null ? JValue.CreateNull() : JToken.FromObject(kv.Value);
                states[lv.Key] = o;
            }
            var raw = new JObject { ["levelStates"] = states, ["power"] = ctx.power, ["outdoorEnv"] = ctx.outdoorEnv, ["indoorEnv"] = ctx.indoorEnv,
                ["lightSource"] = ctx.lightSource, ["trail"] = JToken.FromObject(ctx.trail ?? new List<XZ>()), ["seoyeonAsk"] = ctx.seoyeonAsk };
            return Snapshot.Read(raw);
        }

        // Object.assign(ctx, readSnap(snap)): a fresh plain copy, so later changes never touch the saved snapshot.
        void ApplySnap(Snapshot saved)
        {
            var s = Snapshot.Read(saved == null ? null : JToken.FromObject(saved));
            ctx.levelStates = s.levelStates;
            if (s.power.HasValue) ctx.power = s.power.Value;
            if (s.outdoorEnv != null) ctx.outdoorEnv = s.outdoorEnv;
            if (s.indoorEnv != null) ctx.indoorEnv = s.indoorEnv;
            if (s.lightSource != null) ctx.lightSource = s.lightSource;
            if (s.trail != null) ctx.trail = s.trail;
            if (s.seoyeonAsk != null) ctx.seoyeonAsk = s.seoyeonAsk;
        }

        void SaveNow() => SaveStore.Write(new SaveFile { index = progressIndex, G = G, snap = ctx.snap });
        void PersistCheckpoint(string key) { State.Checkpoint(G, key); ctx.snap = MakeSnapshot(); SaveNow(); }

        static void Forget(Task t, string what) => t?.ContinueWith(x => Debug.LogError($"[app] {what} failed\n{x.Exception}"), TaskContinuationOptions.OnlyOnFaulted);

        // Per-scene state that must not leak into a retry or the next run.
        void ResetTransient()
        {
            ctx.speedOverride = null; engine.Player.Carrying = null;
            flashOn = false; engine.Player.SetFlashlight(false);
            Forget(ctx.SetPlayer("jeongmin"), "setPlayer"); hud.SetCinematic(false); engine.SetControls(true);
            actions.Drop(); danger.Reset(); ctx.deathCause = null; ApplySettings();
        }

        void ResetRun(JToken snap)
        {
            ctx.lightSource = null; ctx.seoyeonAsk = null; ctx.trail = null; ctx.snap = null;
            ctx.levelStates = new Dictionary<string, Dictionary<string, object>>(); ctx.power = true; ctx.outdoorEnv = "day"; ctx.indoorEnv = "interior"; ctx.rate = 0;
            if (snap != null && snap.Type != JTokenType.Null) { ctx.snap = Snapshot.Read(snap); ApplySnap(ctx.snap); }
            // Saves from before the snapshot existed: the light in hand follows from G.
            if (ctx.lightSource == null && G.events.Any(e => e.type == "blackout")) ctx.lightSource = Story.Common.LightSource(ctx);
            ResetTransient();
        }

        // ---- who is who: before the story starts ----
        async Task Intro()
        {
            try { await Cutscenes.Opening(ctx); } catch (Exception err) { Debug.LogError("[cine] opening failed\n" + err); }
            engine.SetControls(false);
            var pick = await ui.Card(new CardOptions { kind = "info", title = "체험 모드", meta = "설정(Esc)에서 언제든 바꿀 수 있습니다",
                body = new List<string> { "같은 재난도 몸과 감각에 따라 전혀 다른 길이 됩니다. 정민의 조건을 골라 보세요.",
                    "보행 장애: 달릴 수 없고 걸음이 느립니다. · 휠체어: 앉은 눈높이, 계단은 도움 없이 못 지나고 경사로만 씁니다.",
                    "저시력: 앞이 흐릿하게 보이고, 눈앞의 대상과 자막을 소리로 읽어 줍니다. · 청각 장애: 소리가 들리지 않고 자막과 화면 신호로만 알 수 있습니다.",
                    "지체 장애(보호자 동반): 보호자가 곁에서 함께 걷습니다. 달릴 수 없습니다." },
                actions = new List<Choice> { new Choice("none", "기본", true), new Choice("mobility", "보행 장애"), new Choice("wheelchair", "휠체어"),
                    new Choice("lowvision", "저시력"), new Choice("deaf", "청각 장애"), new Choice("guardian", "보호자 동반") } });
            if (pick != null && PROFILES.ContainsKey(pick)) { settings.profile = pick; if (pick == "deaf") settings.captions = true; ApplySettings(); }
            StoryRuntime.After(1.5f, () => hud.Toast("조작법은 언제든 H 키로 볼 수 있습니다.", "self", 5000));
        }

        async Task<string> RunChapters(int start, string resumeKey)
        {
            for (int i = start; i < CHAPTERS.Count; i++)
            {
                var id = CHAPTERS[i].Key; var fn = CHAPTERS[i].Value;
                progressIndex = i;
                ctx.resume = resumeKey;
                hud.SetObjective(null);
                if (resumeKey == null) PersistCheckpoint(id);
                resumeKey = null;
                for (;;)
                {
                    try { var r = await fn(ctx); if (id == "ending") return r; break; }
                    catch (Fail err)
                    {
                        if (err.type == "dead") echo.Death(err.detail.TryGetValue("why", out var why) ? why as string : null);
                        engine.SetControls(false); audio.Siren(false); audio.Fire(0); hud.SetCinematic(false);
                        var d = Debrief.FailDebrief(G, err.type, err.detail);
                        var pick = await ui.FailCard(new FailCardOptions { title = d.title, seen = d.seen, missed = d.missed, next = d.next,
                            sources = (d.sourceIds ?? new List<string>()).Select(Facts.Source).Where(s => s != null).ToList() });
                        if (pick != "retry") { engine.SetControls(true); return "menu"; }
                        var key = G.checkpoint.key;
                        State.Restore(G);
                        ApplySnap(ctx.snap);
                        ResetTransient();
                        ctx.resume = key == id ? null : key;
                    }
                }
            }
            return null;
        }

        // app.js boot(): landing -> new or continue -> intro -> run chapters -> back to the landing.
        public async Task Run()
        {
            Setup();
            UiRoot.HideLoading();
            for (;;)
            {
                var saved = SaveStore.ReadRaw();
                var savedG = saved?["G"];
                var hasSave = savedG != null && savedG.Type == JTokenType.Object;
                var pick = await Menu.Landing(hasSave);
                audio.Unlock();
                if (pick == "settings") { AssignSettings(await Menu.SettingsMenu(settings.Clone())); ApplySettings(); continue; }
                if (pick == "about") { await Menu.About(Facts.SOURCES); continue; }
                int start = 0; string resume = null;
                if (pick == "continue" && hasSave)
                {
                    try { G = GameJson.Load(savedG.ToString(Formatting.None)); } catch (Exception e) { Debug.LogError("save rejected: " + e.Message); G = null; }
                }
                if (G?.checkpoint != null)
                {
                    var key = G.checkpoint.key; State.Restore(G);
                    var idx = saved?["index"];
                    start = idx != null && idx.Type == JTokenType.Integer && (int)idx >= 0 && (int)idx < CHAPTERS.Count ? (int)idx : 0;
                    resume = CHAPTERS[start].Key == key ? null : key;
                }
                else G = State.CreateGame((uint)new System.Random().Next());   // crypto random >>> 1: 31 bits
                var fresh = G.checkpoint == null;
                if (fresh) echo.Begin(); else echo.Resume();
                ResetRun(G.checkpoint != null ? saved?["snap"] : null);
                engine.SetView(settings.view);
                if (fresh) { engine.SetControls(false); await Intro(); engine.SetControls(true); }
                hud.Show();
                var result = await RunChapters(start, resume);
                hud.Hide();
                if (result == "restart") SaveStore.Clear();
                G = null;
            }
        }
    }
}
