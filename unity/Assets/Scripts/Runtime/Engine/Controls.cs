// Ports engine/controls.js: the player rig, input and camera. Movement runs in JS space against
// level.walk / level.solids (Geom); the camera and the model Transform are placed through J.
// Key codes are the JS KeyboardEvent.code strings ("KeyF", "Tab", "Escape", "Space"...), see GameInput.
using System;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed class KeyPress
    {
        public string code, key;
    }

    // onBlocked: the player pushed against something at (x, z) trying to reach (toX, toZ).
    public sealed class Blocked
    {
        public float x, z, toX, toZ;
    }

    // controls.player. X/Y/Z/Yaw are JS space (yaw 0 faces -z, positive turns left).
    public sealed class Player
    {
        public static readonly float RADIUS = 0.32f, HEIGHT = 1.72f, EYE = 1.62f, WALK = 3.0f, RUN = 5.2f;   // PLAYER

        public float X { get => throw new NotImplementedException("port: engine/controls.js:player.x"); set => throw new NotImplementedException("port: engine/controls.js:player.x"); }
        public float Y { get => throw new NotImplementedException("port: engine/controls.js:player.y"); set => throw new NotImplementedException("port: engine/controls.js:player.y"); }
        public float Z { get => throw new NotImplementedException("port: engine/controls.js:player.z"); set => throw new NotImplementedException("port: engine/controls.js:player.z"); }
        public float Yaw { get => throw new NotImplementedException("port: engine/controls.js:player.yaw"); set => throw new NotImplementedException("port: engine/controls.js:player.yaw"); }
        public float Pitch { get => throw new NotImplementedException("port: engine/controls.js:player.pitch"); set => throw new NotImplementedException("port: engine/controls.js:player.pitch"); }
        public Vector3 Position => new Vector3(X, Y, Z);     // JS space
        public float SpeedMul = 1, StaminaUse = 1;
        public float Radius = RADIUS, Height = HEIGHT, Eye = EYE;
        public IPerson Model => throw new NotImplementedException("port: engine/controls.js:player.model");
        public float Stamina => throw new NotImplementedException("port: engine/controls.js:player.stamina");
        public bool Winded => throw new NotImplementedException("port: engine/controls.js:player.winded");
        public bool Flashlight => throw new NotImplementedException("port: engine/controls.js:player.flashlight");
        public void SetFlashlight(bool on) => throw new NotImplementedException("port: engine/controls.js:player.setFlashlight");
        // 'bag' | 'water' | null: shows the backpack / water jug on the model.
        public string Carrying { get => throw new NotImplementedException("port: engine/controls.js:player.carrying"); set => throw new NotImplementedException("port: engine/controls.js:player.carrying"); }
        // A spawn name in level.spawns; false when unknown.
        public bool Teleport(string spawn) => throw new NotImplementedException("port: engine/controls.js:player.teleport");
        public bool Teleport(Spawn at) => throw new NotImplementedException("port: engine/controls.js:player.teleport");
    }

    public sealed class Controls
    {
        public static Controls Create(Camera camera, GameObject scene, Func<Level> getLevel, bool reduceMotion = false, string quality = "high") => throw new NotImplementedException("port: engine/controls.js:createControls");

        public Player Player => throw new NotImplementedException("port: engine/controls.js:player");
        public void Update(float dt) => throw new NotImplementedException("port: engine/controls.js:update");
        public void SetModel(IPerson m) => throw new NotImplementedException("port: engine/controls.js:setModel");
        public void SetView(string v) => throw new NotImplementedException("port: engine/controls.js:setView");
        public void SetEnabled(bool on) => throw new NotImplementedException("port: engine/controls.js:setEnabled");
        public void SetOverride(Action<Camera, float> fn) => throw new NotImplementedException("port: engine/controls.js:setOverride");
        public void Dispose() => throw new NotImplementedException("port: engine/controls.js:dispose");
        public string View => throw new NotImplementedException("port: engine/controls.js:view");
        public bool Enabled => throw new NotImplementedException("port: engine/controls.js:enabled");
        public Point Nearest => throw new NotImplementedException("port: engine/controls.js:nearest");
        public Action OnKey(Action<KeyPress> fn) => throw new NotImplementedException("port: engine/controls.js:onKey");
        public Action OnInteract(Action<Point> fn) => throw new NotImplementedException("port: engine/controls.js:onInteract");
        public Action OnNearest(Action<Point> fn) => throw new NotImplementedException("port: engine/controls.js:onNearest");
        public Action OnBlocked(Action<Blocked> fn) => throw new NotImplementedException("port: engine/controls.js:onBlocked");
        public Action OnLand(Action<float> fn) => throw new NotImplementedException("port: engine/controls.js:onLand");
        public void Act(string name, float? seconds = null) => throw new NotImplementedException("port: engine/controls.js:act");
        public bool Busy => throw new NotImplementedException("port: engine/controls.js:busy");
        public void SetHold(bool on) => throw new NotImplementedException("port: engine/controls.js:setHold");
        public void SetModelHidden(bool v) => throw new NotImplementedException("port: engine/controls.js:setModelHidden");
        public bool Crouching { get => throw new NotImplementedException("port: engine/controls.js:crouching"); set => throw new NotImplementedException("port: engine/controls.js:crouching"); }
        public bool Locked => throw new NotImplementedException("port: engine/controls.js:locked");
        public void SetProfile(Profile p) => throw new NotImplementedException("port: engine/controls.js:setProfile");
        public void Shake(float amount = 0.15f, float seconds = 1.2f) => throw new NotImplementedException("port: engine/controls.js:shake");
        public void SetSensitivity(float v) => throw new NotImplementedException("port: engine/controls.js:setSensitivity");
        public void SetBeamStrength(float v) => throw new NotImplementedException("port: engine/controls.js:setBeamStrength");
        public void SetQuality(string q) => throw new NotImplementedException("port: engine/controls.js:setQuality");
        public void SetReduceMotion(bool v) => throw new NotImplementedException("port: engine/controls.js:setReduceMotion");
        public void Snap() => throw new NotImplementedException("port: engine/controls.js:snap");
    }
}
