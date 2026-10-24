// Ports story/runtime.js: the Ctx the chapters use — roam (free play until a handler answers, walk-through
// doors, deadlines, death), talk/cards through modals, the game clock while roaming, level changes with
// remembered states and the right env, notes, payoffs, stats, checkpoints, soft fails.
// The app (App/GameApp) sets cine, boom, setPlayer and wraps goto/skip as app.js does.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using Gyeongbo.UI;
using UnityEngine;

namespace Gyeongbo.Story
{
    public sealed class StoryRuntime : Ctx
    {
        // Default door wiring between levels: "level:point" -> (level, spawn).
        public static readonly Dictionary<string, string[]> DOORS = new Dictionary<string, string[]>
        {
            ["town:home"] = new[] { "apartment", "lobby" }, ["apartment:lobbyExit"] = new[] { "town", "home" },
            ["town:mart"] = new[] { "mart", "entry" }, ["mart:martExit"] = new[] { "town", "mart" },
            ["town:store"] = new[] { "store", "entry" }, ["store:storeExit"] = new[] { "town", "store" },
            ["town:office"] = new[] { "office", "entry" }, ["office:officeExit"] = new[] { "town", "office" },
            ["shelter:shelterExit"] = new[] { "town", "shelter" },
        };
        // Which env a level uses for the current story time unless a chapter overrides it.
        public static readonly HashSet<string> OUTDOOR = new HashSet<string> { "town" };

        Func<Game> getG;
        Action<Game> setG;
        Action<string> persistCheckpoint;
        bool powerOn = true;
        float acc;

        // persistCheckpoint(key): app.js persist.checkpoint (state.checkpoint + snapshot + save).
        public static StoryRuntime Create(GameEngine engine, Hud hud, IUi ui, IAudio audio, Settings settings, Func<Game> getG, Action<Game> setG, Action<string> persistCheckpoint)
        {
            var ctx = new StoryRuntime { engine = engine, hud = hud, ui = ui, audio = audio, settings = settings, getG = getG, setG = setG, persistCheckpoint = persistCheckpoint };
            engine.OnNearest(p => hud.Prompt(p?.label));
            engine.OnFrame((dt, t) =>
            {
                if (ctx.rate == 0 || !engine.ControlsEnabled || ctx.G == null) return;
                ctx.acc += dt * ctx.rate;
                if (ctx.acc >= 60) { var m = Mathf.Floor(ctx.acc / 60); ctx.acc -= m * 60; State.Advance(ctx.G, m); ctx.Refresh(); }
            });
            return ctx;
        }

        // Set by the app (app.js): the time-lapse skip, the cutscene blast, the player model swap, leaving with
        // something in hand. null = the runtime's own behaviour.
        public Func<string, string, string[], Task> skipOverride;
        public Action<float, float, float> boomImpl;
        public Func<string, Task> setPlayerImpl;
        public Action beforeGoto;

        public string AmbienceFor(string levelId)
        {
            if (levelId == "town") return outdoorEnv == "blackout" ? "blackout" : outdoorEnv == "night" || outdoorEnv == "dusk" ? "night" : "city";
            switch (levelId) { case "office": return "office"; case "mart": return "mart"; case "store": return "mart"; case "shelter": return "shelter"; default: return "indoor"; }
        }

        public override Game G => getG();
        // Grid power. The shelter runs on its own generator, so the engine sees power there either way.
        public override bool power { get => powerOn; set { powerOn = value; engine.SetPower(powerOn || engine.Level?.id == "shelter"); } }

        public override void Refresh()
        {
            var G = this.G;
            if (G == null) return;
            hud.SetClock(State.FmtClock(G));
            hud.SetStats(G.stats);
            hud.SetBattery(G.phone.battery);
            var load = State.BagLoad(G);
            hud.SetBag(G.know.bagOn ? new BagMeter { weightKg = load.weightKg, limitKg = Items.BAG_LIMIT.weightKg } : null);
            // Experience mode (settings.profile) slows on top of the story's speed; a heavy bag on the back tires faster.
            engine.Player.SpeedMul = (float)((speedOverride ?? State.SpeedMul(G)) * profileSpeed);
            engine.Player.StaminaUse = (float)(1 + (G.know.bagOn ? load.weightKg / Items.BAG_LIMIT.weightKg : 0));
            hud.SetTension((float)Math.Max(0, (G.stats.tension - 40) / 60));
        }

        public override void SetLevelState(string levelId, string key, object value)
        {
            if (!levelStates.TryGetValue(levelId, out var m)) levelStates[levelId] = m = new Dictionary<string, object>();
            m[key] = value;
            if (engine.Level?.id == levelId) engine.Level.setState?.Invoke(key, value);
        }

        // Enter a level and re-apply remembered states and the right lighting. prepare runs before the engine
        // precompiles shaders, so it compiles the lights the scene will really have.
        public override Task<Level> Goto(string levelId, string spawn = null, string env = null) => DoGoto(levelId, env, p => engine.Enter(levelId, spawn, p));
        public override Task<Level> Goto(string levelId, Spawn at, string env = null) => DoGoto(levelId, env, p => engine.Enter(levelId, at, p));

        async Task<Level> DoGoto(string levelId, string env, Func<Func<Level, Task>, Task<Level>> enter)
        {
            beforeGoto?.Invoke();
            var prepared = false;
            void Prepare(Level level)
            {
                prepared = true;
                engine.SetPower(power || levelId == "shelter");
                if (levelStates.TryGetValue(levelId, out var states))
                    foreach (var kv in states.ToList()) level.setState?.Invoke(kv.Key, kv.Value);
                engine.SetEnv(env ?? (OUTDOOR.Contains(levelId) ? outdoorEnv : levelId == "shelter" ? "underground" : power ? indoorEnv : "interiorDark"), false, 0);
            }
            var lv = await enter(l => { Prepare(l); return Task.CompletedTask; });
            if (!prepared) Prepare(lv);
            audio.Ambience(AmbienceFor(levelId), 1.2f);
            return lv;
        }

        public override void Objective(string title, string body = "", IList<ObjectiveStep> steps = null) => hud.SetObjective(title, body ?? "", steps ?? new List<ObjectiveStep>());
        public override void Toast(string text, string label = "self", int ms = 3800) => hud.Toast(text, label ?? "self", ms);
        // Captions off: same timing, nothing drawn, so story pacing does not change.
        public override Task Caption(string text, string speaker = null, int? ms = null) =>
            settings.captions == false && settings.profile != "deaf" ? Wait((ms ?? Math.Min(7000, 1500 + text.Length * 60)) / 1000f) : hud.Caption(text, speaker ?? "", ms);
        public override GameEvent Event(string type, Dictionary<string, object> details = null) => State.RecordEvent(G, type, details ?? new Dictionary<string, object>());
        public override void Note(string text, string label = "self", string place = null) { State.Note(G, text, label, place ?? engine.Level?.id); Toast($"메모: {text}", label); }
        public override void Payoff(string prep, string moment) { State.RecordEvent(G, "payoff", Details.Of("prep", prep, "moment", moment)); Toast($"준비가 도움이 됐습니다 · {moment}", "self", 4600); }
        public override void Stat(string stat, double delta) { State.Adjust(G, stat, delta); Refresh(); }
        public override void Pass(double minutes) { State.Advance(G, minutes); Refresh(); }
        public override string FactLine(string id) { var c = Facts.Cite(id); return c != null ? $"{c.text}\n— {c.source}" : ""; }

        // Modal helpers: controls off while a UI is open.
        public override async Task<T> Modal<T>(Func<Task<T>> fn)
        {
            engine.SetControls(false);
            try { return await fn(); } finally { engine.SetControls(true); Refresh(); }
        }
        public override async Task Modal(Func<Task> fn)
        {
            engine.SetControls(false);
            try { await fn(); } finally { engine.SetControls(true); Refresh(); }
        }
        public override Task<string> Say(string speaker, IList<string> lines, IList<Choice> choices = null, string role = null) =>
            Modal(() => ui.Talk(new TalkOptions { speaker = speaker, role = role ?? "", lines = lines.ToList(), choices = choices?.ToList() }));
        public override Task<string> Card(CardOptions o) => Modal(() => ui.Card(o));

        // app.js wraps skip: the time-lapse over the town, the card when that fails.
        public override async Task Skip(string from, string to, params string[] text)
        {
            if (skipOverride != null)
            {
                try { await skipOverride(from, to, text); return; }
                catch (Exception e) when (!(e is Gyeongbo.Core.Fail)) { Debug.LogError("[cine] time-lapse failed; showing the card\n" + e); }
            }
            engine.SetControls(false); await ui.TimeSkip(from, to, text); engine.SetControls(true);
        }

        public override void Checkpoint(string key) => persistCheckpoint(key);

        // Free roam until a handler returns something other than null (JS undefined).
        public override Task<object> Roam(RoamOptions o) => new RoamRun(this, o ?? new RoamOptions()).Start();

        // Fires fn after `seconds` on the main thread (JS setTimeout); errors are logged.
        internal static async Task Wait(float seconds) => await Awaitable.WaitForSecondsAsync(seconds);
        internal static void After(float seconds, Action fn) => _ = AfterTask(seconds, fn);
        static async Task AfterTask(float seconds, Action fn)
        {
            try { if (seconds > 0) await Awaitable.WaitForSecondsAsync(seconds); fn(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public override void Load(string json) { setG(GameJson.Load(json)); Refresh(); }
        public override void Boom(float x, float z, float size = 1) => boomImpl?.Invoke(x, z, size);
        // The 서연 interlude swaps who you are; the engine keeps one controller, so swap the model it drives.
        public override Task SetPlayer(string preset) => setPlayerImpl != null ? setPlayerImpl(preset) : engine.SetPlayerPreset(preset);

        // One ctx.roam call: interact handlers, walk-through doors, tick, deadline and death.
        sealed class RoamRun
        {
            readonly StoryRuntime ctx;
            readonly RoamOptions o;
            readonly TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            float prevRate;
            bool busy, done;
            HashSet<string> armed = new HashSet<string>();
            Action offI, offF;

            public RoamRun(StoryRuntime ctx, RoamOptions o) { this.ctx = ctx; this.o = o; }

            Dictionary<string, Func<Point, Task<object>>> On => o.on ?? new Dictionary<string, Func<Point, Task<object>>>();

            public Task<object> Start()
            {
                prevRate = ctx.rate; ctx.rate = o.rate ?? ctx.rate; ctx.roaming = true;
                offI = ctx.engine.OnInteract(p => _ = Interact(p));
                offF = ctx.engine.OnFrame(Frame);
                return tcs.Task;
            }

            void Finish(object value, Exception err)
            {
                if (done) return;
                done = true; offI?.Invoke(); offF?.Invoke(); ctx.rate = prevRate; ctx.roaming = false;
                if (err != null) tcs.TrySetException(err); else tcs.TrySetResult(value);
            }

            // Walk-through doors: stepping into a plain door (no handler of the chapter's own) goes through it, no key.
            // A door arms only once you have been more than 1.6 m from it, so arriving next to one never bounces you back.
            void WalkDoor()
            {
                var L = ctx.engine.Level;
                if (!o.doors || busy || L == null) return;
                foreach (var p in L.points ?? new List<Point>())
                {
                    var key = L.id + ":" + p.id;
                    if (!DOORS.ContainsKey(key) || On.ContainsKey(p.id) || On.ContainsKey(key) || !p.enabled) continue;
                    var pl = ctx.engine.Player;
                    var d = Mathf.Sqrt((pl.X - p.x) * (pl.X - p.x) + (pl.Z - p.z) * (pl.Z - p.z));
                    if (d > 1.6f) armed.Add(key); else if (d < 0.75f && armed.Contains(key)) { armed = new HashSet<string>(); _ = Interact(p); return; }
                }
            }

            async Task Interact(Point point)
            {
                if (busy || done) return;
                busy = true;
                try
                {
                    var key = ctx.engine.Level.id + ":" + point.id;
                    object value = null;
                    if (On.TryGetValue(point.id, out var h) && h != null) value = await ctx.Modal(() => h(point));
                    else if (On.TryGetValue(key, out var hk) && hk != null) value = await ctx.Modal(() => hk(point));
                    else if (o.shelter != null && point.kind == "shelter" && ctx.engine.Level.id == "town") value = await ctx.Modal(() => o.shelter(point));
                    else if (o.doors && DOORS.TryGetValue(key, out var door)) await ctx.Goto(door[0], door[1]);
                    else if (World.IsCommon(ctx, point)) await ctx.Modal(() => World.Common(ctx, point));
                    else if (On.TryGetValue("*", out var any) && any != null) value = await ctx.Modal(() => any(point));
                    if (value != null) Finish(value, null);
                }
                catch (Exception err) { Finish(null, err); }
                finally { busy = false; }
            }

            void Frame(float dt, float t)
            {
                if (done || !ctx.engine.ControlsEnabled) return; // a menu or dialog pauses timers and deadlines too
                try
                {
                    WalkDoor();
                    if (o.tick != null) { var v = o.tick(dt); if (v != null) Finish(v, null); }
                    var G = ctx.G;
                    if (G.stats.health <= 0) { Finish(null, new Gyeongbo.Core.Fail("dead", Details.Of("why", ctx.deathCause ?? "hurt"))); return; }
                    var dl = o.deadline;
                    if (dl != null && (G.day > dl.day || (G.day == dl.day && G.minute >= dl.minute))) Finish(null, new Gyeongbo.Core.Fail(dl.type));
                }
                catch (Exception err) { Finish(null, err); }
            }
        }
    }
}
