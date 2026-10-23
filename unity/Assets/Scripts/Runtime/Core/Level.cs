// SHARED. The level contract (levels/*.js return value, engine/core.js normalizeLevel) and the registry
// app.js LEVELS was: each level's main file registers its builder (see PORTING.md "Contract").
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Core
{
    // What a level builder returns. Built like the JS object literal: new Level { id = "mart", group = ..., ... }.
    public sealed class Level
    {
        public string id;
        public GameObject group;                 // the level root; the engine parents it under the scene
        public string env;                       // default env preset name ('day', 'interior', 'underground')
        public List<WalkZone> walk = new List<WalkZone>();
        public List<Solid> solids = new List<Solid>();
        public List<Point> points = new List<Point>();
        public Dictionary<string, Spawn> spawns = new Dictionary<string, Spawn>();
        public Dictionary<string, IPerson> npcs = new Dictionary<string, IPerson>();
        // Allowed values per state key (documentation for the dev panel and validation), e.g. power: ["on","off"].
        public Dictionary<string, object> states = new Dictionary<string, object>();
        // setState(key, value): value is a string, number, string list or string-keyed map; read it with Sv.*
        // because remembered states come back from the save as JSON.
        public Action<string, object> setState;
        // update(dt, t, frame) once per frame while the level is current.
        public Action<float, float, FrameCtx> update;
        // Optional: current state values (town).
        public Func<Dictionary<string, object>> state;
        public Dictionary<string, Anchor> anchors = new Dictionary<string, Anchor>();
    }

    // levels/<id>.js build<Name>(kit, people, fx). Async so a builder may spread work over frames.
    public delegate Task<Level> LevelBuilder(Kit kit, IPeople people, IFx fx);

    public static class LevelRegistry
    {
        // app.js LEVELS order: prewarm builds the others in this order.
        public static readonly string[] ORDER = { "town", "apartment", "mart", "store", "office", "shelter" };
        static readonly Dictionary<string, LevelBuilder> builders = new Dictionary<string, LevelBuilder>();

        // Called from each level's [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] Register().
        public static void Register(string id, LevelBuilder build)
        {
            if (string.IsNullOrEmpty(id) || build == null) throw new ArgumentException("level id and builder are required");
            builders[id] = build;
        }
        public static bool Has(string id) => id != null && builders.ContainsKey(id);
        public static LevelBuilder Get(string id) => id != null && builders.TryGetValue(id, out var b) ? b : null;
        public static IEnumerable<string> Ids => builders.Keys;
    }
}
