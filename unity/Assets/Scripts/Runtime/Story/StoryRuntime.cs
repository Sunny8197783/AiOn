// Ports story/runtime.js: the Ctx the chapters use — roam (free play until a handler answers, walk-through
// doors, deadlines, death), talk/cards through modals, the game clock while roaming, level changes with
// remembered states and the right env, notes, payoffs, stats, checkpoints, soft fails.
// The app (App/GameApp) sets cine, boom, setPlayer and wraps goto/skip as app.js does.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using Gyeongbo.UI;

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
        public static readonly HashSet<string> OUTDOOR = new HashSet<string> { "town" };

        // persistCheckpoint(key): app.js persist.checkpoint (state.checkpoint + snapshot + save).
        public static StoryRuntime Create(GameEngine engine, Hud hud, IUi ui, IAudio audio, Settings settings, Func<Game> getG, Action<Game> setG, Action<string> persistCheckpoint) => throw new NotImplementedException("port: story/runtime.js:createRuntime");

        // Set by the app (app.js): the time-lapse skip, the cutscene blast, the player model swap, leaving with
        // something in hand. null = the runtime's own behaviour.
        public Func<string, string, string[], Task> skipOverride;
        public Action<float, float, float> boomImpl;
        public Func<string, Task> setPlayerImpl;
        public Action beforeGoto;

        public string AmbienceFor(string levelId) => throw new NotImplementedException("port: story/runtime.js:ambienceFor");

        public override Game G => throw new NotImplementedException("port: story/runtime.js:ctx.G");
        public override bool power { get => throw new NotImplementedException("port: story/runtime.js:ctx.power"); set => throw new NotImplementedException("port: story/runtime.js:ctx.power"); }
        public override void Refresh() => throw new NotImplementedException("port: story/runtime.js:refresh");
        public override void SetLevelState(string levelId, string key, object value) => throw new NotImplementedException("port: story/runtime.js:setLevelState");
        public override Task<Level> Goto(string levelId, string spawn = null, string env = null) => throw new NotImplementedException("port: story/runtime.js:goto");
        public override Task<Level> Goto(string levelId, Spawn at, string env = null) => throw new NotImplementedException("port: story/runtime.js:goto");
        public override void Objective(string title, string body = "", IList<ObjectiveStep> steps = null) => throw new NotImplementedException("port: story/runtime.js:objective");
        public override void Toast(string text, string label = "self", int ms = 3800) => throw new NotImplementedException("port: story/runtime.js:toast");
        public override Task Caption(string text, string speaker = null, int? ms = null) => throw new NotImplementedException("port: story/runtime.js:caption");
        public override GameEvent Event(string type, Dictionary<string, object> details = null) => throw new NotImplementedException("port: story/runtime.js:event");
        public override void Note(string text, string label = "self", string place = null) => throw new NotImplementedException("port: story/runtime.js:note");
        public override void Payoff(string prep, string moment) => throw new NotImplementedException("port: story/runtime.js:payoff");
        public override void Stat(string stat, double delta) => throw new NotImplementedException("port: story/runtime.js:stat");
        public override void Pass(double minutes) => throw new NotImplementedException("port: story/runtime.js:pass");
        public override string FactLine(string id) => throw new NotImplementedException("port: story/runtime.js:factLine");
        public override Task<T> Modal<T>(Func<Task<T>> fn) => throw new NotImplementedException("port: story/runtime.js:modal");
        public override Task Modal(Func<Task> fn) => throw new NotImplementedException("port: story/runtime.js:modal");
        public override Task<string> Say(string speaker, IList<string> lines, IList<Choice> choices = null, string role = null) => throw new NotImplementedException("port: story/runtime.js:say");
        public override Task<string> Card(CardOptions o) => throw new NotImplementedException("port: story/runtime.js:card");
        public override Task Skip(string from, string to, params string[] text) => throw new NotImplementedException("port: story/runtime.js:skip");
        public override void Checkpoint(string key) => throw new NotImplementedException("port: story/runtime.js:checkpoint");
        public override Task<object> Roam(RoamOptions o) => throw new NotImplementedException("port: story/runtime.js:roam");
        public override void Load(string json) => throw new NotImplementedException("port: story/runtime.js:load");
        public override void Boom(float x, float z, float size = 1) => throw new NotImplementedException("port: app.js:ctx.boom");
        public override Task SetPlayer(string preset) => throw new NotImplementedException("port: app.js:ctx.setPlayer");
    }
}
