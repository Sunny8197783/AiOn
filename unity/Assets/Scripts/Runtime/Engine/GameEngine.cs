// Ports engine/core.js createEngine: camera, scene root, kit, post, sky + env presets (tweened), lazy levels
// from LevelRegistry, the player rig and the frame loop (a MonoBehaviour drives Frame()). People and fx come
// in through IPeople / IFx (Person/Crowd and Fx when present, the stand-ins in Fallbacks.cs otherwise).
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

    public sealed class GameEngine
    {
        public static readonly string[] QUALITIES = { "low", "medium", "high" };

        public static GameEngine Create(EngineOptions o) => throw new NotImplementedException("port: engine/core.js:createEngine");

        public GameObject Scene => throw new NotImplementedException("port: engine/core.js:scene");      // root for things added to the world
        public Camera Camera => throw new NotImplementedException("port: engine/core.js:camera");
        public Kit Kit => throw new NotImplementedException("port: engine/core.js:kit");
        public IPeople People => throw new NotImplementedException("port: engine/core.js:people");
        // Every person made through People (actions.js may hit them); cine removes its extras.
        public HashSet<IPerson> Persons => throw new NotImplementedException("port: engine/core.js:persons");
        public Task Ready => throw new NotImplementedException("port: engine/core.js:ready");
        public Post Post => throw new NotImplementedException("port: engine/core.js:post");
        public IFx Fx => throw new NotImplementedException("port: engine/core.js:fx");
        public string Quality => throw new NotImplementedException("port: engine/core.js:quality");
        public Level Level => throw new NotImplementedException("port: engine/core.js:level");
        public Player Player => throw new NotImplementedException("port: engine/core.js:player");
        public string View => throw new NotImplementedException("port: engine/core.js:view");
        public Point Nearest => throw new NotImplementedException("port: engine/core.js:nearest");
        public bool ControlsEnabled => throw new NotImplementedException("port: engine/core.js:controlsEnabled");
        public string Env => throw new NotImplementedException("port: engine/core.js:env");
        public bool ReduceMotion => throw new NotImplementedException("port: engine/core.js:reduceMotion");
        public bool Power => throw new NotImplementedException("port: engine/core.js:power");
        public bool Crouching => throw new NotImplementedException("port: engine/core.js:crouching");
        public bool PointerLocked => throw new NotImplementedException("port: engine/core.js:pointerLocked");
        // Slow motion for cutscenes, 0..2 (1 = real time).
        public float TimeScale { get => throw new NotImplementedException("port: engine/core.js:timeScale"); set => throw new NotImplementedException("port: engine/core.js:timeScale"); }

        // Fade out, swap the level in, place the player (spawn null = 'entry' or the first spawn), run prepare
        // (level states, env), fade in. Calls queue: one enter at a time.
        public Task<Level> Enter(string levelId, string spawn = null, Func<Level, Task> prepare = null) => throw new NotImplementedException("port: engine/core.js:enter");
        public Task<Level> Enter(string levelId, Spawn at, Func<Level, Task> prepare = null) => throw new NotImplementedException("port: engine/core.js:enter");
        // Tween to an env preset (Presets.PRESETS); rain only outdoors.
        public void SetEnv(string preset, bool rain = false, float seconds = 1.5f) => throw new NotImplementedException("port: engine/core.js:setEnv");
        public Task SetPlayerPreset(string preset) => throw new NotImplementedException("port: engine/core.js:setPlayerPreset");
        // Grid power: street lamps, lit windows, shop fronts and signs stay dark without it.
        public void SetPower(bool on) => throw new NotImplementedException("port: engine/core.js:setPower");
        public void SetProfile(Profile p) => throw new NotImplementedException("port: engine/core.js:setProfile");
        public void Shake(float amount = 0.15f, float seconds = 1.2f) => throw new NotImplementedException("port: engine/core.js:shake");
        public void SetSensitivity(float v) => throw new NotImplementedException("port: engine/core.js:setSensitivity");
        public void Act(string name, float? seconds = null) => throw new NotImplementedException("port: engine/core.js:act");
        public void SetHold(bool on) => throw new NotImplementedException("port: engine/core.js:setHold");
        public void SetModelHidden(bool v) => throw new NotImplementedException("port: engine/core.js:setModelHidden");
        public void SetGrade(Grade grade, float seconds = 1) => throw new NotImplementedException("port: engine/core.js:setGrade");
        public void SetView(string v) => throw new NotImplementedException("port: engine/core.js:setView");       // first | third
        public void SetControls(bool on) => throw new NotImplementedException("port: engine/core.js:setControls");
        // fn(camera, dt) places the camera each frame (JS space via J); null hands it back to the player rig.
        public void SetCameraOverride(Action<Camera, float> fn) => throw new NotImplementedException("port: engine/core.js:setCameraOverride");
        public void SetQuality(string q) => throw new NotImplementedException("port: engine/core.js:setQuality");
        public void SetReduceMotion(bool v) => throw new NotImplementedException("port: engine/core.js:setReduceMotion");

        // Subscriptions return an unsubscribe action.
        public Action OnInteract(Action<Point> fn) => throw new NotImplementedException("port: engine/core.js:onInteract");
        public Action OnNearest(Action<Point> fn) => throw new NotImplementedException("port: engine/core.js:onNearest");
        public Action OnKey(Action<KeyPress> fn) => throw new NotImplementedException("port: engine/core.js:onKey");
        public Action OnBlocked(Action<Blocked> fn) => throw new NotImplementedException("port: engine/core.js:onBlocked");
        public Action OnLand(Action<float> fn) => throw new NotImplementedException("port: engine/core.js:onLand");    // drop height in metres
        // fn(dt, t) every frame (dt already scaled by TimeScale).
        public Action OnFrame(Action<float, float> fn) => throw new NotImplementedException("port: engine/core.js:onFrame");

        public EngineStats Stats() => throw new NotImplementedException("port: engine/core.js:stats");
        public void Dispose() => throw new NotImplementedException("port: engine/core.js:dispose");
    }
}
