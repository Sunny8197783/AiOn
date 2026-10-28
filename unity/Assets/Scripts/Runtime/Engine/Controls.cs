// Ports engine/controls.js: the player rig, input and camera. Movement runs in JS space against
// level.walk / level.solids (Geom); the camera and the model Transform are placed through J.
// Key codes are the JS KeyboardEvent.code strings ("KeyF", "Tab", "Escape", "Space"...), see GameInput.
// WASD/arrows move, Shift runs (stamina), Space jumps, the mouse looks (pointer lock after one click; Alt+arrows
// also work), E/Enter interacts, V toggles the view; M, Tab, I, P, H, Esc, F, V, G, Q, C, X, B go to onKey.
// The per-frame rig is in ControlsUpdate.cs.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;
using UnityEngine.UIElements;

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

        internal Controls c;
        public float X { get => c.position.x; set => c.position.x = value; }
        public float Y { get => c.position.y; set => c.position.y = value; }
        public float Z { get => c.position.z; set => c.position.z = value; }
        public float Yaw { get => c.lookYaw; set => c.lookYaw = c.camYaw = c.bodyYaw = value; }
        public float Pitch { get => c.lookPitch; set => c.lookPitch = c.camPitch = Controls.Clamp(value, -1.25f, 1.1f); }
        public Vector3 Position => new Vector3(X, Y, Z);     // JS space
        public float SpeedMul = 1, StaminaUse = 1;
        public float Radius = RADIUS, Height = HEIGHT, Eye = EYE;
        public IPerson Model => c.model;
        public float Stamina => c.stamina;
        public bool Winded => c.winded;
        public bool Flashlight => c.flashOn;
        public void SetFlashlight(bool on)
        {
            c.flashOn = on; c.spot.intensity = c.flashOn ? Kit.ToUnity(Controls.FLASH * c.flashK) : 0;
            c.spot.shadows = c.flashOn && c.spotShadow ? LightShadows.Soft : LightShadows.None;
            c.beam.SetActive(c.flashOn); c.model?.SetProp("flashlight", c.flashOn);
        }
        // 'bag' | 'water' | null: shows the backpack / water jug on the model.
        public string Carrying
        {
            get => c.carrying;
            set
            {
                c.carrying = value == "bag" || value == "water" ? value : null;
                c.model?.SetProp("backpack", c.carrying == "bag"); c.model?.SetProp("waterJug", c.carrying == "water");
            }
        }
        // A spawn name in level.spawns; false when unknown.
        public bool Teleport(string spawn)
        {
            var L = c.getLevel();
            Spawn s = null;
            if (spawn != null && L?.spawns != null) L.spawns.TryGetValue(spawn, out s);
            if (s == null) { Debug.LogError($"[engine/controls] unknown spawn {spawn} in level {L?.id}"); return false; }
            return Teleport(s);
        }
        public bool Teleport(Spawn s)
        {
            var L = c.getLevel();
            if (s == null || !float.IsFinite(s.x) || !float.IsFinite(s.z)) { Debug.LogError($"[engine/controls] unknown spawn in level {L?.id}"); return false; }
            var g = Geom.GroundAt(L?.walk, s.x, s.z, s.y ?? 0, s.y == null);
            c.position = new Vector3(s.x, s.y ?? g?.y ?? 0, s.z);
            if (s.yaw.HasValue && float.IsFinite(s.yaw.Value)) Yaw = s.yaw.Value;
            c.lookPitch = c.camPitch = -0.05f; c.vel = Vector2.zero; c.lastGround = c.position.y; c.snap = true; c.vy = 0; c.air = false; c.move = null;
            return true;
        }
    }

    public sealed partial class Controls
    {
        internal const float FLASH = 6; // flashlight candela; dimmed further when the beam hits something close
        static readonly float TP_DIST = 3.4f, TP_HEIGHT = 1.7f, TP_SHOULDER = 0.45f;
        const float EYE_FWD = 0.1f, EYE_UP = 0.07f; // first person: where the eyes sit relative to the head joint
        const float FOV = 70, FOV_RUN = 4, D2R = Mathf.PI / 180, LOOK = 0.0042f, LOCK_LOOK = 0.0024f;
        const float JUMP_V = 3.6f, GRAVITY = 12, STAIR_SLOPE = 0.2f;
        static readonly string[] UI_KEYS = { "KeyM", "Tab", "KeyI", "Escape", "KeyF", "KeyV", "KeyP", "KeyH", "KeyG", "KeyQ", "KeyC", "KeyX", "KeyB" };
        static readonly Dictionary<string, int[]> MOVE = new Dictionary<string, int[]>
        {
            ["KeyW"] = new[] { 0, 1 }, ["ArrowUp"] = new[] { 0, 1 }, ["KeyS"] = new[] { 0, -1 }, ["ArrowDown"] = new[] { 0, -1 },
            ["KeyA"] = new[] { -1, 0 }, ["ArrowLeft"] = new[] { -1, 0 }, ["KeyD"] = new[] { 1, 0 }, ["ArrowRight"] = new[] { 1, 0 },
        };
        static readonly string[] HELD = { "KeyW", "ArrowUp", "KeyS", "ArrowDown", "KeyA", "ArrowLeft", "KeyD", "ArrowRight", "ShiftLeft", "ShiftRight", "AltLeft", "AltRight" };
        static float Damp(float k, float dt) => 1 - Mathf.Exp(-k * dt);
        static float Wrap(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));
        internal static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;

        internal sealed class Move { public float t, dur, x0, z0, y0, x1, z1, y1, arc; public string clip; }
        sealed class Blend { public float k; public Vector3 pos; public Quaternion rot; }
        sealed class Prof { public bool run = true, jump = true, stairs = true; public float eye = Player.EYE; }

        Camera camera; GameObject scene; internal Func<Level> getLevel;
        bool RM, enabled = true; string view = "first"; float viewK; Action<Camera, float> overrideFn; Blend blend;
        readonly HashSet<string> keys = new HashSet<string>();
        readonly List<Action<KeyPress>> keyFns = new List<Action<KeyPress>>();
        readonly List<Action<Point>> interactFns = new List<Action<Point>>(), nearestFns = new List<Action<Point>>();
        readonly List<Action<Blocked>> blockedFns = new List<Action<Blocked>>();
        readonly List<Action<float>> landFns = new List<Action<float>>();
        internal Vector3 position; internal Vector2 vel; Vector3 pivot;
        internal float lookYaw, lookPitch = -0.05f, camYaw, camPitch = -0.05f;
        internal float bodyYaw; float phase, bobAmp, camFrac = 1; Point nearest; internal IPerson model; internal string carrying; internal bool flashOn;
        string anim = ""; internal float lastGround; internal bool snap = true; float beamStrength = 1; internal float flashK = 1;
        float shakeT, shakeDur = 1, shakeA;
        internal float vy; internal bool air; internal float stamina = 1; internal bool winded; bool blocked; float sens = 1, peakY; internal Move move; string actAnim; float actT; bool crouch;
        float airT, accel, lastFwd, lastYaw, lookOff; bool modelHidden, relock, modelBroken;
        readonly Prof prof = new Prof();
        Player player;
        internal Light spot; internal bool spotShadow; internal GameObject beam; Material beamMat; Vector3 aim;
        Transform cutHead; Vector3 cutHeadScale = Vector3.one;
        int polledFrame = -1;

        static void Emit<T>(List<Action<T>> set, T arg)
        {
            foreach (var fn in set.ToArray()) { try { fn(arg); } catch (Exception e) { Debug.LogError("[engine/controls] listener failed " + e); } }
        }
        static Action Sub<T>(List<Action<T>> set, Action<T> fn) { set.Add(fn); return () => set.Remove(fn); }

        // The flashlight beam: a cone with its apex at the origin opening toward JS +z; the JS shader's falloff
        // along its length is baked into the vertex alpha.
        // ponytail: no view-angle fresnel on the beam (JS fragment shader); a custom additive shader can add it.
        GameObject MakeBeam()
        {
            const float LEN = 7; float R = Mathf.Tan(0.42f) * LEN;
            var g = Geo.Cone(R, LEN, 32, 1, true).Translate(0, -LEN / 2, 0).RotateX(-Mathf.PI / 2);
            g.color = new List<Color>(g.pos.Count);
            foreach (var p in g.pos)
            {
                float t = p.z / LEN, s = Mathf.Clamp01(t / 0.18f), a = s * s * (3 - 2 * s) * Mathf.Pow(Mathf.Max(0, 1 - t), 1.6f);
                g.color.Add(new Color(1, 1, 1, a));
            }
            beamMat = Mat.Unlit(new MatSpec { color = Color.black, vertexColors = true, transparent = true, depthWrite = false, blending = "additive", side = "double", fog = false });
            beamMat.renderQueue = 3010;
            var m = Geo.Obj(g, beamMat, "beam", false, false);
            m.SetActive(false);
            return m;
        }

        public static Controls Create(Camera camera, GameObject scene, Func<Level> getLevel, bool reduceMotion = false, string quality = "high")
        {
            var c = new Controls { camera = camera, scene = scene, getLevel = getLevel, RM = reduceMotion };
            c.player = new Player { c = c };
            // Flashlight: one SpotLight that always exists (intensity 0 when off), plus a faint additive cone.
            c.spot = new GameObject("flashlight").AddComponent<Light>();
            Kit.SetupSpot(c.spot, J.Hex(0xfff1dc), 0, 28, 0.42f, 0.6f);
            c.spot.shadowNearPlane = 0.4f; c.spot.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
            c.spotShadow = quality == "high";
            c.beam = c.MakeBeam();
            scene.Add(c.spot.gameObject, c.beam);
            return c;
        }

        public Player Player => player;

        public void SetModel(IPerson m)
        {
            RestoreHead();
            if (model?.Group != null) J.Remove(model.Group);
            model = m; modelBroken = false; if (m?.Group == null) return;
            foreach (var r in m.Group.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            scene.Add(m.Group);
            player.Carrying = carrying; m.SetProp("flashlight", flashOn); anim = "";
        }

        // ---- input (polled once per frame; JS listens to keydown / pointer events) ----
        static readonly List<UIDocument> docs = new List<UIDocument>();
        static int docsFrame = -999;
        static List<UIDocument> Docs()
        {
            if (Time.frameCount - docsFrame > 60) { docsFrame = Time.frameCount; docs.Clear(); docs.AddRange(UnityEngine.Object.FindObjectsByType<UIDocument>()); }
            return docs;
        }
        // JS editable(e.target): a focused text field gets the keys, not the player.
        static bool Typing()
        {
            foreach (var d in Docs())
            {
                var f = d != null ? d.rootVisualElement?.panel?.focusController?.focusedElement as VisualElement : null;
                if (f != null && (f is TextField || f.GetFirstAncestorOfType<TextField>() != null)) return true;
            }
            return false;
        }
        // JS pointerdown only reaches the canvas when no UI element is under the pointer.
        // ponytail: full-screen UI layers must use PickingMode.Ignore (CSS pointer-events: none) or they eat world clicks.
        static bool OverUi(Vector2 screen)
        {
            foreach (var d in Docs())
            {
                var root = d != null ? d.rootVisualElement : null; var panel = root?.panel;
                if (panel == null) continue;
                var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y)));
                if (picked != null && picked != root) return true;
            }
            return false;
        }
        static string KeyOf(string code) => code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal) ? code.Substring(3).ToLowerInvariant() : code == "Space" ? " " : code;

        bool IsLocked() => GameInput.Locked;
        void Lock() { try { GameInput.SetLocked(true); } catch (Exception) { /* refused */ } }
        void Turn(float dx, float dy, float k) { lookYaw -= dx * k * sens; lookPitch = Clamp(lookPitch - dy * k * sens * 0.85f, -1.25f, 1.1f); }

        void Poll()
        {
            if (polledFrame == Time.frameCount) return;
            polledFrame = Time.frameCount;
            keys.Clear();
            if (Typing()) return;
            foreach (var code in UI_KEYS)
            {
                if (!GameInput.Down(code) || !enabled) continue;
                if (code == "Tab" && keyFns.Count == 0) continue;
                if (code == "Escape" && IsLocked()) GameInput.SetLocked(false); // the browser leaves pointer lock on Esc
                if (code == "KeyV") SetView(view == "first" ? "third" : "first");
                if (code == "KeyC" && move == null) crouch = !crouch;
                Emit(keyFns, new KeyPress { code = code, key = KeyOf(code) });
            }
            if (!enabled) return;
            if (GameInput.Down("KeyE") || GameInput.Down("Enter") || GameInput.Down("NumpadEnter")) { if (nearest != null) Emit(interactFns, nearest); }
            if (GameInput.Down("Space"))
            {
                if (crouch) crouch = false;
                else if (!air && move == null && prof.jump && overrideFn == null && !Vault()) { vy = JUMP_V; air = true; peakY = position.y; stamina = Mathf.Max(0, stamina - 0.06f); }
            }
            foreach (var code in HELD) if (GameInput.Held(code)) keys.Add(code);
            // Mouse: one click on the world locks the pointer, then plain mouse movement looks around.
            for (int b = 0; b <= 2; b += 2)
            {
                if (!GameInput.MouseDown(b) || OverUi(GameInput.MousePosition)) continue;
                if (!IsLocked()) Lock();
                else if (b == 0) Emit(keyFns, new KeyPress { code = "Mouse0", key = "" });
            }
            // ponytail: no touch drag-look (JS pointer fallback); add with the Input System's touchscreen if needed.
            if (IsLocked()) { var d = GameInput.MouseDelta; Turn(d.x, -d.y, LOCK_LOOK); }
        }

        public void SetView(string v) { if (v == "first" || v == "third") view = v; else Debug.LogError("[engine/controls] unknown view " + v); }
        // Menus need the cursor: release the lock while controls are off and take it back afterwards.
        public void SetEnabled(bool on)
        {
            enabled = on;
            if (!enabled) { keys.Clear(); if (IsLocked()) { relock = true; GameInput.SetLocked(false); } }
            else if (relock) { relock = false; Lock(); }
        }
        public void SetOverride(Action<Camera, float> fn)
        {
            if (fn == null && overrideFn != null && !RM) blend = new Blend { k = 0, pos = camera.transform.position, rot = camera.transform.rotation };
            overrideFn = fn;
        }

        public void Dispose()
        {
            if (IsLocked()) GameInput.SetLocked(false);
            RestoreHead();
            if (spot != null) UnityEngine.Object.Destroy(spot.gameObject);
            if (beam != null) UnityEngine.Object.Destroy(beam);
            if (model?.Group != null) J.Remove(model.Group);
        }

        public string View => view;
        public bool Enabled => enabled;
        public Point Nearest => nearest;
        public Action OnKey(Action<KeyPress> fn) => Sub(keyFns, fn);
        public Action OnInteract(Action<Point> fn) => Sub(interactFns, fn);
        public Action OnNearest(Action<Point> fn) => Sub(nearestFns, fn);
        public Action OnBlocked(Action<Blocked> fn) => Sub(blockedFns, fn);
        public Action OnLand(Action<float> fn) => Sub(landFns, fn);
        // Play a one-off body animation (punch, throw, hurt) for `seconds`, over whatever the legs do.
        public void Act(string name, float? seconds = null) { if (model == null || !model.Play(name, seconds)) { actAnim = name; actT = seconds ?? 0.4f; } }
        public bool Busy => move != null;
        public void SetHold(bool on) => model?.SetHold(on);
        public void SetModelHidden(bool v) => modelHidden = v; // cutscenes show their own people
        // Low to the ground (C): slower, smaller, safer from blasts and debris.
        public bool Crouching { get => crouch; set => crouch = value; }
        public bool Locked => GameInput.Locked;
        // { run, jump, eye, stairs }.
        public void SetProfile(Profile p) { if (p == null) return; prof.run = p.run; prof.jump = p.jump; prof.eye = p.eye; prof.stairs = p.stairs; }
        public void Shake(float amount = 0.15f, float seconds = 1.2f) { shakeA = amount; shakeDur = shakeT = Mathf.Max(0.05f, seconds); }
        public void SetSensitivity(float v) => sens = Clamp(float.IsNaN(v) || v == 0 ? 1 : v, 0.3f, 3);
        public void SetBeamStrength(float v) => beamStrength = v;
        public void SetQuality(string q) { spotShadow = q == "high"; spot.shadows = flashOn && spotShadow ? LightShadows.Soft : LightShadows.None; }
        public void SetReduceMotion(bool v) => RM = v;
        public void Snap() => snap = true;
    }
}
