// SHARED. The run context story code receives (story/runtime.js createRuntime + what app.js adds to ctx).
// Story/StoryRuntime implements the abstract members; App/GameApp creates it and sets cine, boom, setPlayer.
// Naming: JS ctx.foo(...) is ctx.Foo(...); JS state fields keep their JS names (ctx.outdoorEnv).
// Inside a class derived from Ctx, write Gyeongbo.Core.Fail for the exception type (Fail is also a method here).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using Gyeongbo.Systems;
using Gyeongbo.UI;
using UnityEngine;

namespace Gyeongbo.Core
{
    // ctx.roam options. on: point id | "level:pointId" | "*" -> handler. A handler (or tick) that returns
    // null keeps roaming (JS undefined); any other value ends the roam with that value.
    public sealed class RoamOptions
    {
        public Dictionary<string, Func<Point, Task<object>>> on = new Dictionary<string, Func<Point, Task<object>>>();
        public bool doors = true;                        // follow the default level doors
        public Deadline deadline;                        // Fail(deadline.type) once the clock passes it
        public float? rate;                              // game seconds per real second (null = keep ctx.rate)
        public Func<float, object> tick;                 // per frame while controls are on
        public Func<Point, Task<object>> shelter;        // town points of kind 'shelter'
    }

    public sealed class Deadline
    {
        public int day, minute;
        public string type;
    }

    public abstract class Ctx
    {
        // ---- services ----
        public GameEngine engine;
        public Hud hud;
        public IUi ui;
        public IAudio audio;
        public Settings settings;
        public Cine cine;                                // set by the app (story/cine.js)

        public abstract Game G { get; }

        // ---- run state (app.js snapshot() saves levelStates, power, outdoorEnv, indoorEnv, lightSource, trail, seoyeonAsk) ----
        public string resume;                            // checkpoint key a retry resumes from
        public float rate;                               // game seconds per real second while roaming (0 = stopped)
        public string outdoorEnv = "day", indoorEnv = "interior";
        // Grid power. Setting it also tells the engine (the shelter has its own generator).
        public abstract bool power { get; set; }
        public Dictionary<string, Dictionary<string, object>> levelStates = new Dictionary<string, Dictionary<string, object>>();
        public string lightSource;                       // flashlight | phone | null
        public List<XZ> trail;                           // where 정민 walked on D-day (dday.js), for the re-read
        public string seoyeonAsk;                        // yes | no | null
        public float? speedOverride;
        public float profileSpeed = 1;
        public string deathCause;
        public bool roaming;
        public Snapshot snap;

        // ---- story/runtime.js ----
        public abstract void Refresh();
        public abstract void SetLevelState(string levelId, string key, object value);
        // Enter a level, re-apply remembered states and the right env; spawn null = 'entry' or the first spawn.
        public abstract Task<Level> Goto(string levelId, string spawn = null, string env = null);
        public abstract Task<Level> Goto(string levelId, Spawn at, string env = null);
        public abstract void Objective(string title, string body = "", IList<ObjectiveStep> steps = null);
        public abstract void Toast(string text, string label = "self", int ms = 3800);
        // Resolves when the caption is done (same timing when captions are off).
        public abstract Task Caption(string text, string speaker = null, int? ms = null);
        public abstract GameEvent Event(string type, Dictionary<string, object> details = null);
        // place null = the current level id.
        public abstract void Note(string text, string label = "self", string place = null);
        public abstract void Payoff(string prep, string moment);
        public abstract void Stat(string stat, double delta);
        public abstract void Pass(double minutes);
        public abstract string FactLine(string id);
        // Controls off while a UI is open; refresh() after.
        public abstract Task<T> Modal<T>(Func<Task<T>> fn);
        public abstract Task Modal(Func<Task> fn);
        public abstract Task<string> Say(string speaker, IList<string> lines, IList<Choice> choices = null, string role = null);
        public Task<string> Say(string speaker, string line, IList<Choice> choices = null, string role = null) => Say(speaker, new[] { line }, choices, role);
        public abstract Task<string> Card(CardOptions o);
        // A time skip: the town time-lapse (story/cutscenes.js), the card if that fails.
        public abstract Task Skip(string from, string to, params string[] text);
        public abstract void Checkpoint(string key);
        public void Fail(string type, Dictionary<string, object> detail = null) => throw new Gyeongbo.Core.Fail(type, detail);
        // Free roam until a handler returns non-null (see RoamOptions).
        public abstract Task<object> Roam(RoamOptions o);
        // A roam tick: true once the player is within r metres of (x, z) in the level (JS space).
        public Func<float, object> Near(string levelId, float x, float z, float r = 3) =>
            _ => engine?.Level?.id == levelId && Mathf.Sqrt((engine.Player.X - x) * (engine.Player.X - x) + (engine.Player.Z - z) * (engine.Player.Z - z)) < r ? (object)true : null;
        public MapPlace TownPlace(string id) => TownMap.PLACES.TryGetValue(id, out var p) ? p : null;     // ctx.place
        public MapSpawn TownSpawn(string id) => TownMap.SPAWNS.TryGetValue(id, out var s) ? s : null;     // ctx.spawn
        public abstract void Load(string json);

        // ---- set up by app.js ----
        public abstract void Boom(float x, float z, float size = 1);   // a harmless blast for cutscenes (danger.trigger('boom'))
        public abstract Task SetPlayer(string preset);                 // swap the player model ('jeongmin' | 'seoyeon')
    }
}
