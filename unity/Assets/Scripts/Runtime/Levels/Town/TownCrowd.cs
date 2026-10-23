// Ports levels/town-crowd.js: the town's crowds per mode (none normal busy evacuating sparse queue assembly) on
// the sidewalk paths clipped by the active closures, toward the open shelter entrances, the water queue and the
// assembly point. Crowds are created lazily and every call is guarded, so a missing or failing crowd module only
// empties the streets.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownCrowd
    {
        public static readonly string[] CROWD_MODES = { "none", "normal", "busy", "evacuating", "sparse", "queue", "assembly" };

        static readonly Dictionary<string, int> SIZES = new Dictionary<string, int> { ["main"] = 26, ["extra"] = 14, ["few"] = 8, ["line"] = 14 };
        // Where evacuees head for each shelter gate (just outside the entrance, on the walk surface).
        static readonly Dictionary<string, XZ> ENTRY = new Dictionary<string, XZ>
        {
            ["stairs"] = new XZ(-86, 11.8f), ["ramp"] = new XZ(-80, 55.4f), ["access"] = new XZ(-102.4f, 12.7f), ["east"] = new XZ(-66.4f, 31),
        };
        static readonly (string gate, float[][] path)[] EVAC =
        {
            ("stairs", new[] { new[] { 44f, 8 }, new[] { -44f, 8 }, new[] { -44f, 14 }, new[] { -56f, 14 }, new[] { -56f, 10 }, new[] { -75f, 10 }, new[] { -86f, 11.8f } }),
            ("stairs", new[] { new[] { -56f, -100 }, new[] { -56f, -8 }, new[] { -60f, -8 }, new[] { -60f, 9.6f }, new[] { -86f, 11.8f } }),
            ("stairs", new[] { new[] { -128f, -8 }, new[] { -104f, -8 }, new[] { -104f, 9.6f }, new[] { -86f, 11.8f } }),
            ("access", new[] { new[] { -128f, -8 }, new[] { -104f, -8 }, new[] { -104f, 9.6f }, new[] { -102.4f, 12.7f } }),
            ("access", new[] { new[] { -118f, -14 }, new[] { -104f, -8 }, new[] { -104f, 9.6f }, new[] { -102.4f, 12.7f } }),
            ("ramp", new[] { new[] { 44f, 54 }, new[] { -44f, 54 }, new[] { -56f, 54 }, new[] { -62f, 55.6f }, new[] { -80f, 55.4f } }),
            ("ramp", new[] { new[] { -56f, 104 }, new[] { -56f, 57 }, new[] { -62f, 55.6f }, new[] { -80f, 55.4f } }),
            ("east", new[] { new[] { -56f, 104 }, new[] { -56f, 31 }, new[] { -66.4f, 31 } }),
            ("east", new[] { new[] { -44f, 54 }, new[] { -44f, 31 }, new[] { -56f, 31 }, new[] { -66.4f, 31 } }),
            ("stairs", new[] { new[] { 100f, 27 }, new[] { 70f, 27 }, new[] { 62f, 20 }, new[] { 56f, 20 }, new[] { 56f, 8 }, new[] { 44f, 8 }, new[] { -44f, 8 }, new[] { -44f, 14 }, new[] { -56f, 14 }, new[] { -56f, 10 }, new[] { -86f, 11.8f } }),
        };
        static List<XZ> ToPts(float[][] p) => p.Select(q => new XZ(q[0], q[1])).ToList();

        // Insertion-ordered like the JS object (null = creation failed, never retried).
        readonly List<KeyValuePair<string, ICrowd>> crowds = new List<KeyValuePair<string, ICrowd>>();
        readonly HashSet<string> failed = new HashSet<string>();
        IPeople people;
        GameObject group;
        string quality;
        Func<float, float, float> heightFn;
        string mode = "none";
        List<string> closures = new List<string>();
        Dictionary<string, string> gates = new Dictionary<string, string> { ["east"] = "open", ["stairs"] = "open", ["ramp"] = "open", ["access"] = "closed" };

        public static TownCrowd Create(IPeople people, GameObject group, List<WalkZone> walk, string quality = "high") =>
            new TownCrowd { people = people, group = group, quality = quality ?? "high", heightFn = (x, z) => TownUtil.HeightAt(walk, x, z) };

        T Guard<T>(string what, Func<T> fn) where T : class
        {
            try { return fn(); }
            catch (Exception err) { if (failed.Add(what)) Debug.LogError($"[town] crowd {what} failed\n{err}"); return null; }
        }
        void Guard(string what, Action fn) => Guard<object>(what, () => { fn(); return null; });

        ICrowd Get(string name)
        {
            foreach (var kv in crowds) if (kv.Key == name) return kv.Value;
            var c = Guard($"create {name}", () =>
            {
                var cr = people.CreateCrowd(new CrowdOptions { count = SIZES[name], seed = 900 + crowds.Count * 17, people = people, heightAt = heightFn, quality = quality });
                if (cr?.Group == null) throw new Exception("createCrowd returned no group");
                cr.Group.name = $"crowd:{name}"; group.Add(cr.Group); return cr;
            });
            crowds.Add(new KeyValuePair<string, ICrowd>(name, c));
            return c;
        }

        List<RectXZ> Rects() => closures.SelectMany(id => TownPlan.BARRICADES.TryGetValue(id, out var r) ? r : new List<RectXZ>()).ToList();
        List<List<XZ>> Stroll() => TownPlan.ClipPaths(TownPlan.SIDEWALKS, Rects());
        (List<List<XZ>> paths, List<XZ> targets) Evac()
        {
            var open = EVAC.Where(e => gates.TryGetValue(e.gate, out var v) && v == "open").ToList();
            var use = open.Count > 0 ? open : EVAC.Where(e => e.gate == "stairs").ToList();
            return (TownPlan.ClipPaths(use.Select(e => ToPts(e.path)).ToList(), Rects()), use.Select(e => e.gate).Distinct().Select(g => ENTRY[g]).ToList());
        }

        void Run(string name, Action<ICrowd> fn) { var c = Get(name); if (c == null) return; c.Group.SetActive(true); Guard($"{name}.{mode}", () => fn(c)); }

        void Apply()
        {
            foreach (var kv in crowds) if (kv.Value != null) kv.Value.Group.SetActive(false);
            var walkers = Stroll();
            switch (mode)
            {
                case "none": return;
                case "normal": Run("main", c => { c.SetPaths(walkers); c.SetMode("stroll", new CrowdModeOptions { speed = 1.25f }); }); return;
                case "busy":
                    foreach (var n in new[] { "main", "extra" }) Run(n, c => { c.SetPaths(walkers); c.SetMode("stroll", new CrowdModeOptions { speed = 1.3f }); });
                    return;
                case "sparse": Run("few", c => { c.SetPaths(walkers); c.SetMode("stroll", new CrowdModeOptions { speed = 1.15f }); }); return;
                case "evacuating":
                {
                    var (paths, targets) = Evac();
                    foreach (var n in new[] { "main", "extra" }) Run(n, c => { c.SetPaths(paths); c.SetMode("brisk", new CrowdModeOptions { targets = targets, then = "despawn" }); });
                    return;
                }
                case "queue":
                    Run("line", c => c.SetMode("queue", new CrowdModeOptions { point = TownEvents.WATER_QUEUE, at = TownEvents.WATER_QUEUE, dir = new XZ(1, 0), spacing = 0.9f, face = new XZ(-1, 0) }));
                    Run("few", c => { c.SetPaths(walkers); c.SetMode("stroll", new CrowdModeOptions { speed = 1.1f }); });
                    return;
                case "assembly":
                    Run("main", c => c.SetMode("idle", new CrowdModeOptions { area = TownEvents.ASSEMBLY.area, paths = new List<List<XZ>>(), instant = true }));
                    // served leave east toward 문화로, clear of the buses
                    Run("line", c => c.SetMode("queue", new CrowdModeOptions { point = TownEvents.ASSEMBLY.queue, at = TownEvents.ASSEMBLY.queue, dir = new XZ(0, 1), spacing = 0.85f, face = new XZ(0, -1), leave = new XZ(1, 0) }));
                    return;
                default: Debug.LogError($"[town] crowd \"{mode}\" unknown; use {string.Join(" | ", CROWD_MODES)}"); return;
            }
        }

        // Returns the mode now in force (an unknown mode keeps the old one).
        public string SetMode(string m)
        {
            if (Array.IndexOf(CROWD_MODES, m) < 0) { Debug.LogError($"[town] crowd \"{m}\" unknown; use {string.Join(" | ", CROWD_MODES)}"); return mode; }
            mode = m; Apply(); return mode;
        }

        // Closures and gates changed: re-route the walkers. null = unchanged.
        public void SetContext(List<string> closures = null, System.Collections.Generic.Dictionary<string, string> gates = null)
        {
            if (closures != null) this.closures = closures;
            if (gates != null) { var g = new Dictionary<string, string>(this.gates); foreach (var kv in gates) g[kv.Key] = kv.Value; this.gates = g; }
            if (mode == "normal" || mode == "busy" || mode == "sparse" || mode == "evacuating" || mode == "queue") Apply();
        }

        public void Update(float dt, FrameCtx ctx)
        {
            foreach (var kv in crowds)
            {
                var c = kv.Value;
                if (c != null && c.Group.activeSelf) Guard($"{kv.Key}.update", () => c.Update(dt, ctx));
            }
        }

        public string Mode => mode;
    }
}
