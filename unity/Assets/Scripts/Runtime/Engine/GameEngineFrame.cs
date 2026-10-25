// engine/core.js: environment presets (tweened), grid power, the rain wet look, fps watch and the frame loop.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Unity.Profiling;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed partial class GameEngine
    {
        static float Ease(float k) => k * k * (3 - 2 * k);
        static float Clamp(float v, float a, float b) => Mathf.Min(b, Mathf.Max(a, v));

        // ---- environment presets (tweened) ----
        string envName; EnvState envFrom, envTo; float envK = 1, envDur; IRain rainFx; float beam = 1;
        Dictionary<string, float> litFrom = new Dictionary<string, float>(), litTo = new Dictionary<string, float>();
        readonly Dictionary<string, float> litNow = new Dictionary<string, float>();
        bool power = true;
        // Grid power off (blackout): street lamps, lit windows, shop fronts and signs stay dark whatever the sky
        // preset says; battery-backed 'emergency' and 'indoor' (interiors pick their own preset) are untouched.
        static readonly HashSet<string> GRID = new HashSet<string> { "street", "windows", "shops", "signs" };
        Dictionary<string, float> LitTarget(string preset)
        {
            var o = new Dictionary<string, float>();
            foreach (var kv in Presets.PRESETS[preset].lit) o[kv.Key] = !power && GRID.Contains(kv.Key) ? 0 : kv.Value;
            return o;
        }
        static Dictionary<string, float> Merge(Dictionary<string, float> a, Dictionary<string, float> b)
        {
            var o = new Dictionary<string, float>(a); foreach (var kv in b) o[kv.Key] = kv.Value; return o;
        }

        void StepEnv(float dt)
        {
            envK = envDur > 0 ? Mathf.Min(1, envK + dt / envDur) : 1;
            float e = Ease(envK); var S = Presets.MixState(envFrom, envTo, e);
            sky.Apply(S);
            post.envExposure = S["exposure"];   // renderer.toneMappingExposure
            post.SetBloom(S["bloom"], S["bloomRadius"], S["bloomThreshold"]);
            beam = Clamp(1.25f - S["keyI"] * 0.45f - S["hemiI"] * 1.2f, 0.08f, 1);
            controls.SetBeamStrength(beam);
            ApplyWet(S["wet"]);
            foreach (var g in new List<string>(litTo.Keys))
            {
                float a = litFrom.TryGetValue(g, out var f) ? f : kit.IsLit(g) ? 1 : 0, v = a + (litTo[g] - a) * e;
                litNow[g] = v; kit.SetLit(g, v > 0.0005f, v);
            }
        }

        // Tween to an env preset (Presets.PRESETS); rain only outdoors.
        public void SetEnv(string preset, bool rain = false, float seconds = 1.5f)
        {
            if (preset == null || !Presets.PRESETS.TryGetValue(preset, out var P)) { Debug.LogError($"[engine] unknown env preset \"{preset}\"; known: {string.Join(", ", Presets.PRESETS.Keys)}"); return; }
            bool outdoorRain = rain && P.outdoor;
            envTo = Presets.EnvStateOf(preset, rain);
            envFrom = sky.State != null ? sky.State.Clone() : envTo;
            envDur = float.IsNaN(seconds) ? 0 : Mathf.Max(0, seconds); envK = 0;
            litFrom = new Dictionary<string, float>(litNow); litTo = Merge(litNow, LitTarget(preset));
            envName = preset;
            try { sky.Environment(preset, rain); } catch (Exception err) { Debug.LogError("[engine] reflection environment failed " + err); }
            post.SetGrade(Presets.GradeFor(preset, rain), envDur);
            if (outdoorRain && rainFx == null) { try { rainFx = fx.Rain(scene, camera); } catch (Exception err) { Debug.LogError("[engine] rain fx failed " + err); } }
            rainFx?.SetOn(outdoorRain);
            if (envDur == 0) StepEnv(0);
        }
        void ReapplyLit() { foreach (var kv in litNow) kit.SetLit(kv.Key, kv.Value > 0.0005f, kv.Value); }

        // Grid power for the current scene; re-applies the lit groups of the current preset at once.
        public void SetPower(bool on)
        {
            power = on;
            if (envName == null) return;
            litTo = Merge(litTo, LitTarget(envName));
            if (envK >= 1) foreach (var kv in new List<KeyValuePair<string, float>>(litTo)) { litNow[kv.Key] = kv.Value; kit.SetLit(kv.Key, kv.Value > 0.0005f, kv.Value); }
        }

        // Wet look while it rains: asphalt / sidewalk / concrete surface materials (matched by their shared kit
        // texture) and any material made with wet = true get lower roughness and a darker albedo. The dry values
        // are kept and restored when the rain stops. ponytail: no envMapIntensity boost (no URP Lit equivalent).
        sealed class Dry { public float roughness; public Color color; }
        readonly Dictionary<Material, Dry> dry = new Dictionary<Material, Dry>();
        List<Material> wetList; float wetNow;
        List<Material> CollectWet(GameObject root)
        {
            var srcs = new HashSet<Texture>();
            foreach (var n in new[] { "asphalt", "sidewalk", "concrete" }) srcs.Add(kit.SurfaceMap(n));
            var o = new HashSet<Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (Mat.IsStandard(m) && (Mat.InfoOf(m).wet || (Mat.GetMap(m) is Texture t && srcs.Contains(t)))) o.Add(m);
            foreach (var m in o) if (!dry.ContainsKey(m)) dry[m] = new Dry { roughness = Mat.GetRoughness(m), color = Mat.GetLinearColor(m) };
            return new List<Material>(o);
        }
        void SetWet(List<Material> list, float w)
        {
            foreach (var m in list)
            {
                if (m == null) continue;
                var d = dry[m];
                Mat.SetRoughness(m, Mathf.Max(0.08f, d.roughness * (1 - 0.65f * w))); Mat.SetLinearColor(m, d.color * (1 - 0.28f * w));
            }
        }
        void ApplyWet(float w)
        {
            float from = wetNow;
            if (w > 0.001f && wetList == null && level != null) { wetList = CollectWet(level.group); from = 0; } // freshly collected = dry
            if (wetList != null && w != from) SetWet(wetList, w);
            wetNow = w;
        }

        // ---- frame loop ----
        readonly List<Action<float, float>> frameFns = new List<Action<float, float>>();
        readonly HashSet<string> logged = new HashSet<string>();
        void LogOnce(string key, Exception err) { if (logged.Add(key)) Debug.LogError($"[engine] {key} {err}"); }
        float timeScale = 1; // slow motion (cutscenes); 1 = real time
        float t, fps = 60, acc, slow; int frames; bool downgraded;
        readonly FrameCtx frameCtx = new FrameCtx();
        ProfilerRecorder drawRec, triRec;

        void Measure(float raw)
        {
            if (raw > 0.5f) { acc = 0; frames = 0; return; } // the app was paused
            acc += raw; frames++;
            if (acc < 0.5f) return;
            fps = frames / acc; acc = 0; frames = 0;
            if (downgraded || busy || quality == "low" || t < 3) { slow = 0; return; }
            slow = fps < 30 ? slow + 0.5f : 0;
            if (slow >= 4)
            {
                downgraded = true;
                string q = quality == "high" ? "medium" : "low";
                Debug.LogWarning($"[engine] fps {fps:0.0} < 30 for 4 s; quality {quality} -> {q}");
                SetQuality(q);
                try { onQualityChange?.Invoke(q); } catch (Exception err) { Debug.LogError("[engine] onQualityChange failed " + err); }
            }
        }

        internal void Frame()
        {
            float raw = Mathf.Max(0, Time.unscaledDeltaTime);
            float dt = Mathf.Min(raw, 0.1f) * timeScale; t += dt;
            try { controls.Update(dt); } catch (Exception err) { LogOnce("player update failed", err); }
            if (envK < 1) StepEnv(dt);
            if (level?.update != null)
            {
                frameCtx.camera = camera; frameCtx.player = controls.Player; frameCtx.env = envName; frameCtx.power = power; frameCtx.reduced = RM;
                try { level.update(dt, t, frameCtx); } catch (Exception err) { LogOnce($"level \"{level.id}\" update failed", err); }
            }
            sky.Update(dt, camera, controls.Player.Position, RM);
            if (rainFx != null) { try { rainFx.Update(dt, camera); } catch (Exception err) { LogOnce("rain update failed", err); } }
            foreach (var fn in frameFns.ToArray()) { try { fn(dt, t); } catch (Exception err) { LogOnce("onFrame listener failed", err); } }
            post.Render(dt);
            Measure(raw);
        }

        // Draw calls / triangles come from the profiler counters (editor and development builds; 0 elsewhere).
        public EngineStats Stats()
        {
            if (!drawRec.Valid) drawRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            if (!triRec.Valid) triRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            return new EngineStats { fps = Mathf.RoundToInt(fps), drawCalls = (int)drawRec.LastValue, triangles = (int)triRec.LastValue, adapt = (float)Math.Round(post.AdaptValue(), 2) };
        }
    }
}
