// Small helpers shared by the Systems ports: JS Math.random / Math.round semantics, three.js-unit point lights
// through Kit, fire-and-forget tasks that log instead of vanishing, and setTimeout on real (unscaled) time.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Systems
{
    internal static class SysUtil
    {
        static readonly System.Random rng = new System.Random();

        // Math.random(): [0, 1), never 1 (UnityEngine.Random.value can return 1).
        public static float Rand() => (float)rng.NextDouble();
        public static float Rnd(float a, float b) => a + Rand() * (b - a);

        // Math.round: halves round up (C# Math.Round rounds them to even).
        public static float JsRound(float v) => Mathf.Floor(v + 0.5f);
        public static int JsRoundI(double v) => (int)Math.Floor(v + 0.5);

        public static float Hypot(float x, float z) => Mathf.Sqrt(x * x + z * z);

        // A three.js PointLight made through Kit (it owns the unit conversion). Created at intensity 1 to read the
        // Unity intensity per three.js unit, then dimmed to 0; set light.intensity = threeUnits * unit afterwards.
        // ponytail: assumes Kit converts intensity linearly; ask the engine for a converter if it does not.
        public static Light PointLight(Kit kit, Color color, float distance, float decay, out float unit)
        {
            var l = kit.PointLight(color, 1, distance, decay);
            unit = l.intensity;
            l.intensity = 0;
            return l;
        }

        // A promise nobody awaits (JS fire-and-forget): `_ = Forget(t)`; continues on the main thread, logs a failure.
        public static async Task Forget(Task t, string what = "task")
        {
            try { if (t != null) await t; }
            catch (Exception e) { Debug.LogError("[" + what + "] " + e); }
        }

        // ---- setTimeout / clearTimeout on real time (performance.now), whatever the world's time scale ----
        sealed class Timer { public int id; public float due; public Action fn; }
        static readonly List<Timer> timers = new List<Timer>();
        static int nextId = 1;
        static TimerRunner runner;

        public static int After(float seconds, Action fn)
        {
            if (runner == null)
            {
                var go = new GameObject("gyeongbo-timers") { hideFlags = HideFlags.HideInHierarchy };
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<TimerRunner>();
            }
            var t = new Timer { id = nextId++, due = Time.realtimeSinceStartup + Mathf.Max(0, seconds), fn = fn };
            timers.Add(t);
            return t.id;
        }

        public static void Cancel(int id) { if (id != 0) timers.RemoveAll(t => t.id == id); }

        internal static void RunTimers()
        {
            float now = Time.realtimeSinceStartup;
            while (true)
            {
                Timer next = null;
                foreach (var t in timers) if (t.due <= now && (next == null || t.due < next.due)) next = t;
                if (next == null) return;
                timers.Remove(next);
                try { next.fn?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        sealed class TimerRunner : MonoBehaviour
        {
            void Update() => RunTimers();
        }
    }
}
