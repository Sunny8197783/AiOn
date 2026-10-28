// Ports engine/core.js createEngine: camera, scene root, kit, post, sky + env presets (tweened), lazy levels
// from LevelRegistry, the player rig and the frame loop (a MonoBehaviour drives Frame()). People and fx come
// in through IPeople / IFx (Person/Crowd and Fx when present, the stand-ins in Fallbacks.cs otherwise).
// Env presets, the wet look and the frame loop are in GameEngineFrame.cs.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed class EngineOptions
    {
        public string quality = "high";
        public bool reduceMotion;
        public Action<string> onQualityChange;   // the engine lowered quality after 4 s under 30 fps
    }

    public sealed class EngineStats
    {
        public int fps, drawCalls, triangles;
        public float adapt;
    }

    public sealed partial class GameEngine
    {
        public static readonly string[] QUALITIES = { "low", "medium", "high" };

        // Every person made through People lands in Persons (engine/actions.js hits them).
        sealed class Tracked : IPeople
        {
            public IPeople inner; public HashSet<IPerson> persons;
            public bool Fallback => inner.Fallback;
            public IPerson CreatePerson(string preset = "crowd", int? seed = null) { var m = inner.CreatePerson(preset, seed); if (m?.Group != null) persons.Add(m); return m; }
            public ICrowd CreateCrowd(CrowdOptions o) => inner.CreateCrowd(o);
        }

        sealed class Loop : MonoBehaviour
        {
            internal GameEngine e;
            void Update() { e?.Frame(); }
        }

        GameObject scene; Camera camera; Kit kit; IPeople people; readonly HashSet<IPerson> persons = new HashSet<IPerson>();
        Post post; IFx fx; Sky sky; Controls controls; Loop loop;
        string quality; bool RM; Action<string> onQualityChange;
        Level level;
        readonly Dictionary<string, IPerson> models = new Dictionary<string, IPerson>();
        bool disposed;

        static Level Normalize(Level L, string id, GameObject scene)
        {
            if (L == null || L.group == null) throw new InvalidOperationException($"[engine] level \"{id}\" did not return a Level with a group");
            L.id = L.id ?? id; L.walk = L.walk ?? new List<WalkZone>(); L.solids = L.solids ?? new List<Solid>(); L.points = L.points ?? new List<Point>();
            L.spawns = L.spawns ?? new Dictionary<string, Spawn>(); L.npcs = L.npcs ?? new Dictionary<string, IPerson>();
            L.states = L.states ?? new Dictionary<string, object>(); L.anchors = L.anchors ?? new Dictionary<string, Anchor>();
            if (L.setState == null) L.setState = (k, v) => Debug.LogWarning($"[engine] level \"{id}\" has no setState (key {k})");
            if (L.walk.Count == 0) Debug.LogWarning($"[engine] level \"{id}\" has no walk zones; the player keeps the spawn height");
            if (L.spawns.Count == 0) Debug.LogWarning($"[engine] level \"{id}\" has no spawns");
            // three.js keeps a built level out of the scene until it is entered; here it waits inactive.
            L.group.SetActive(false);
            scene.Add(L.group);
            return L;
        }

        public static GameEngine Create(EngineOptions o)
        {
            o = o ?? new EngineOptions();
            var e = new GameEngine { quality = o.quality, RM = o.reduceMotion, onQualityChange = o.onQualityChange };
            if (Array.IndexOf(QUALITIES, e.quality) < 0) { Debug.LogWarning($"[engine] unknown quality \"{e.quality}\", using high"); e.quality = "high"; }
            e.scene = new GameObject("scene");
            var camGo = new GameObject("camera") { tag = "MainCamera" };
            e.camera = camGo.AddComponent<Camera>();
            e.camera.fieldOfView = 70; e.camera.nearClipPlane = 0.08f; e.camera.farClipPlane = 3000;
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            e.kit = Kit.Create(e.quality);
            e.sky = Sky.Create(e.scene, e.kit, e.quality);
            e.post = Post.Create(e.camera, e.quality, e.RM);
            e.controls = Controls.Create(e.camera, e.scene, () => e.level, e.RM, e.quality);
            // ---- people + fx (optional: the stand-ins keep the engine running) ----
            IPeople p;
            try { p = new Gyeongbo.People.ProceduralPeople(); }
            catch (Exception err) { Debug.LogError("[engine] people unavailable; using capsule people " + err); p = new CapsulePeople(); }
            e.people = new Tracked { inner = p, persons = e.persons };
            try { e.fx = Gyeongbo.Systems.Fx.Create(e.kit, e.RM); }
            catch (Exception err) { Debug.LogError("[engine] fx unavailable; fire, smoke and rain are disabled " + err); e.fx = new StubFx(); }
            e.controls.SetModel(e.PlayerModel("jeongmin"));
            e.SetEnv("day", false, 0);
            e.loop = new GameObject("engine-loop").AddComponent<Loop>();
            e.loop.e = e;
            return e;
        }

        // One cached model per preset; the 서연 interlude swaps the model the same controller drives.
        IPerson PlayerModel(string preset)
        {
            if (models.TryGetValue(preset, out var cached)) return cached;
            IPerson m;
            try { m = people.CreatePerson(preset, 1); if (m?.Group == null) throw new InvalidOperationException("createPerson returned no group"); }
            catch (Exception err) { Debug.LogError($"[engine] player model \"{preset}\" failed; using a capsule {err}"); m = new CapsulePeople().CreatePerson(preset); }
            return models[preset] = m;
        }

        public GameObject Scene => scene;      // root for things added to the world
        public Camera Camera => camera;
        public Kit Kit => kit;
        public IPeople People => people;
        // Every person made through People (actions.js may hit them); cine removes its extras.
        public HashSet<IPerson> Persons => persons;
        public Task Ready => Task.CompletedTask;   // people and fx are created synchronously
        public Post Post => post;
        public IFx Fx => fx;
        public string Quality => quality;
        public Level Level => level;
        public Player Player => controls.Player;
        public string View => controls.View;
        public Point Nearest => controls.Nearest;
        public bool ControlsEnabled => controls.Enabled;
        public string Env => envName;
        public bool ReduceMotion => RM;
        public bool Power => power;
        public bool Crouching => controls.Crouching;
        public bool PointerLocked => controls.Locked;
        // Slow motion for cutscenes, 0..2 (1 = real time).
        public float TimeScale { get => timeScale; set => timeScale = float.IsNaN(value) ? 0 : Mathf.Min(2, Mathf.Max(0, value)); }

        // ---- levels ----
        readonly Dictionary<string, Task<Level>> built = new Dictionary<string, Task<Level>>();
        readonly HashSet<string> done = new HashSet<string>();
        Task<Level> GetLevel(string id)
        {
            if (!built.TryGetValue(id ?? "", out var task))
            {
                var make = LevelRegistry.Get(id);
                if (make == null) return Task.FromException<Level>(new ArgumentException($"[engine] unknown level \"{id}\"; known: {string.Join(", ", LevelRegistry.Ids)}"));
                task = Build(id, make);
                built[id] = task;
            }
            return task;
        }
        async Task<Level> Build(string id, LevelBuilder make)
        {
            try { var L = Normalize(await make(kit, people, fx), id, scene); done.Add(id); return L; }
            catch { built.Remove(id); throw; } // a failed build may be retried
        }

        Task queue = Task.CompletedTask; bool busy;
        // Fade out, swap the level in, place the player (spawn null = 'entry' or the first spawn), run prepare
        // (level states, env), fade in. Calls queue: one enter at a time.
        public Task<Level> Enter(string levelId, string spawn = null, Func<Level, Task> prepare = null) => Queue(levelId, spawn, null, prepare);
        public Task<Level> Enter(string levelId, Spawn at, Func<Level, Task> prepare = null) => Queue(levelId, null, at, prepare);

        Task<Level> Queue(string levelId, string spawn, Spawn at, Func<Level, Task> prepare)
        {
            var run = Run(queue, levelId, spawn, at, prepare);
            queue = run;
            return run;
        }

        async Task<Level> Run(Task prev, string levelId, string spawn, Spawn at, Func<Level, Task> prepare)
        {
            try { await prev; } catch { /* the previous enter failed; this one still runs */ }
            busy = true;
            float fadeS = RM ? 0 : done.Contains(levelId) ? 0.1f : 0.35f; // a level already built swaps in a blink
            try
            {
                await post.FadeTo(0, fadeS);
                Level L;
                try { L = await GetLevel(levelId); }
                catch (Exception err) { Debug.LogError($"[engine] level \"{levelId}\" failed to load {err}"); throw; }
                if (L != level)
                {
                    if (wetList != null) { SetWet(wetList, 0); wetList = null; }
                    if (level != null) level.group.SetActive(false);
                    L.group.SetActive(true); level = L; ReapplyLit(); ApplyWet(wetNow);
                }
                var names = new List<string>(L.spawns.Keys);
                if (at != null) { if (!controls.Player.Teleport(at) && names.Count > 0) controls.Player.Teleport(names[0]); }
                else
                {
                    string want = spawn ?? (L.spawns.ContainsKey("entry") ? "entry" : names.Count > 0 ? names[0] : null);
                    if (!controls.Player.Teleport(want) && names.Count > 0) controls.Player.Teleport(names[0]);
                }
                controls.Snap(); controls.Update(0);
                if (prepare != null) { try { await prepare(L); } catch (Exception err) { Debug.LogError($"[engine] prepare for level \"{levelId}\" failed (continuing) {err}"); } }
                // ponytail: no shader precompile (renderer.compileAsync); Unity compiles variants on first use. A
                // ShaderVariantCollection warmed up here removes the first-frame hitch.
                Prewarm();
                return L;
            }
            finally
            {
                await post.FadeTo(1, fadeS);
                busy = false;
            }
        }

        // Build the other levels while the player is busy elsewhere, so a door never waits on a build; one at a time.
        bool warming; readonly HashSet<string> tried = new HashSet<string>();
        async void Prewarm()
        {
            if (warming) return; warming = true;
            try
            {
                await Awaitable.WaitForSecondsAsync(4);
                while (!disposed)
                {
                    string id = null;
                    var ids = new List<string>(LevelRegistry.ORDER); foreach (var k in LevelRegistry.Ids) if (!ids.Contains(k)) ids.Add(k);
                    foreach (var k in ids) if (LevelRegistry.Has(k) && !built.ContainsKey(k) && !tried.Contains(k)) { id = k; break; }
                    if (id == null) return;
                    tried.Add(id);
                    await Awaitable.WaitForSecondsAsync(1.5f); // requestIdleCallback stand-in
                    if (disposed) return;
                    try { await GetLevel(id); }
                    catch (Exception err) { Debug.LogWarning($"[engine] prewarm of \"{id}\" failed (it loads at the door instead) {err}"); }
                    await Awaitable.WaitForSecondsAsync(0.8f);
                }
            }
            catch (Exception err) { Debug.LogError("[engine] prewarm stopped " + err); }
        }

        public Task SetPlayerPreset(string preset) { controls.SetModel(PlayerModel(preset)); return Task.CompletedTask; }
        public void SetProfile(Profile p) => controls.SetProfile(p);
        public void Shake(float amount = 0.15f, float seconds = 1.2f) => controls.Shake(amount, seconds);
        public void SetSensitivity(float v) => controls.SetSensitivity(v);
        public void Act(string name, float? seconds = null) => controls.Act(name, seconds);
        public void SetHold(bool on) => controls.SetHold(on);
        public void SetModelHidden(bool v) => controls.SetModelHidden(v);
        public void SetGrade(Grade grade, float seconds = 1) => post.SetGrade(grade, seconds);
        public void SetView(string v) => controls.SetView(v);       // first | third
        public void SetControls(bool on) => controls.SetEnabled(on);
        // fn(camera, dt) places the camera each frame (JS space via J); null hands it back to the player rig.
        public void SetCameraOverride(Action<Camera, float> fn) => controls.SetOverride(fn);

        // ponytail: no pixel-ratio step (three.js setPixelRatio); URP render scale lives on the pipeline asset.
        public void SetQuality(string q)
        {
            if (Array.IndexOf(QUALITIES, q) < 0) { Debug.LogError($"[engine] unknown quality \"{q}\""); return; }
            quality = q;
            sky.SetQuality(q); post.SetQuality(q); controls.SetQuality(q);
        }
        public void SetReduceMotion(bool v) { RM = v; post.SetReduceMotion(RM); controls.SetReduceMotion(RM); fx.SetReduceMotion(RM); }

        // Subscriptions return an unsubscribe action.
        public Action OnInteract(Action<Point> fn) => controls.OnInteract(fn);
        public Action OnNearest(Action<Point> fn) => controls.OnNearest(fn);
        public Action OnKey(Action<KeyPress> fn) => controls.OnKey(fn);
        public Action OnBlocked(Action<Blocked> fn) => controls.OnBlocked(fn);
        public Action OnLand(Action<float> fn) => controls.OnLand(fn);    // drop height in metres
        // fn(dt, t) every frame (dt already scaled by TimeScale).
        public Action OnFrame(Action<float, float> fn) { frameFns.Add(fn); return () => frameFns.Remove(fn); }

        public void Dispose()
        {
            disposed = true;
            if (loop != null) UnityEngine.Object.Destroy(loop.gameObject);
            controls.Dispose();
            UnityEngine.Object.Destroy(scene);
            // Deactivate first: Destroy waits for the frame's end, and a reload's new engine looks for an AudioListener.
            if (camera != null) { camera.gameObject.SetActive(false); UnityEngine.Object.Destroy(camera.gameObject); }
        }
    }
}
