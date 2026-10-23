// Ports engine/people-clips.js: one-shot action clips layered over whatever the legs are doing, keyed like hand
// animation (anticipation, action, follow-through, recovery) and eased between keys. A clip writes only the bones
// it names ('upper' clips leave the legs to the gait; 'full' clips own the whole body, pelvis included).
// Optional look track: the pitch the eyes take during the clip (the first-person camera follows it).
// Clip(name, P, c, k): P is filled from the base pose already (PeopleAnim layout), k = 0..1.
using System;
using System.Collections.Generic;
using UnityEngine;
using static Gyeongbo.People.PeopleBody;
using static Gyeongbo.People.PeopleAnim;

namespace Gyeongbo.People
{
    public sealed class ClipInfo
    {
        public string mask;          // 'upper' | 'full'
        public float dur;
    }

    public static class PeopleClips
    {
        sealed class ClipDef { public string mask; public float dur; public float[][] look; public Action<float[], PoseCtx, float> f; }

        static float Ease(float x) => x * x * (3 - 2 * x);
        // Keyframe track [[t, v], ...]: eased between keys, held at the ends.
        public static float Kf(float k, float[][] keys)
        {
            if (k <= keys[0][0]) return keys[0][1];
            for (int i = 1; i < keys.Length; i++)
            {
                float t1 = keys[i][0], v1 = keys[i][1];
                if (k <= t1) { float t0 = keys[i - 1][0], v0 = keys[i - 1][1]; return v0 + (v1 - v0) * Ease((k - t0) / (t1 - t0)); }
            }
            return keys[keys.Length - 1][1];
        }
        // kf(k, T(times, vals)) without building the pairs.
        static float KfT(float k, float[] times, float[] vals)
        {
            if (k <= times[0]) return vals[0];
            for (int i = 1; i < times.Length; i++)
                if (k <= times[i]) return vals[i - 1] + (vals[i] - vals[i - 1]) * Ease((k - times[i - 1]) / (times[i] - times[i - 1]));
            return vals[vals.Length - 1];
        }
        // Set bone b's rotation from three tracks (x, y, z), each a values list over the clip's key times; null = 0.
        static void Set(float[] P, int b, float k, float[] times, float[] x = null, float[] y = null, float[] z = null)
        {
            P[b * 3] = x == null ? 0 : KfT(k, times, x); P[b * 3 + 1] = y == null ? 0 : KfT(k, times, y); P[b * 3 + 2] = z == null ? 0 : KfT(k, times, z);
        }
        static float[] L(params float[] v) => v;
        static readonly float[] tmp = new float[OFF + 3];
        static readonly int[] SIDES = { -1, 1 };

        static readonly int[] UPPER = { B.spine, B.chest, B.neck, B.head, B.lArm, B.lFore, B.lHand, B.rArm, B.rFore, B.rHand };
        static readonly Dictionary<string, ClipDef> CLIPS = new Dictionary<string, ClipDef>
        {
            // Right straight: weight back and the shoulder turned away, then the hip and chest drive the fist out along
            // the line of sight, a beat at full reach, back to guard. The left hand stays up by the face throughout.
            ["punch"] = new ClipDef { mask = "upper", dur = 0.42f, look = new[] { L(0, 0), L(0.46f, -0.04f), L(1, 0) }, f = (P, c, k) =>
            {
                var t = L(0, 0.28f, 0.46f, 0.62f, 1);
                Set(P, B.rArm, k, t, L(0.35f, 0.6f, 1.68f, 1.6f, 0.2f), null, L(0.18f, 0.32f, 0.02f, 0.05f, 0.12f));
                Set(P, B.rFore, k, t, L(1.3f, 2.1f, 0.06f, 0.2f, 0.35f), L(0, -0.3f, -0.1f, -0.1f, 0));
                Set(P, B.rHand, k, t);
                Set(P, B.lArm, k, t, L(0.55f, 0.75f, 0.7f, 0.7f, 0.15f), null, L(-0.22f, -0.28f, -0.25f, -0.25f, -0.12f));
                Set(P, B.lFore, k, t, L(1.6f, 2.0f, 1.95f, 1.9f, 0.4f), L(0, 0.35f, 0.3f, 0.3f, 0));
                Set(P, B.spine, k, t, L(0, 0.03f, -0.1f, -0.08f, 0), L(0, -0.14f, 0.18f, 0.15f, 0));
                Set(P, B.chest, k, t, L(0, 0.02f, -0.06f, -0.05f, 0), L(0, -0.3f, 0.38f, 0.32f, 0));
                Set(P, B.neck, k, t, null, L(0, 0.2f, -0.25f, -0.2f, 0));
                Set(P, B.head, k, t, L(0, 0, 0.05f, 0.04f, 0), L(0, 0.24f, -0.3f, -0.27f, 0));
            } },
            // Overhand throw: the off hand points at the target while the arm cocks back behind the head and the chest
            // turns away; then hips, chest and arm whip through in order, the hand releases high in front and follows
            // through across the body.
            ["throw"] = new ClipDef { mask = "upper", dur = 0.6f, f = (P, c, k) =>
            {
                var t = L(0, 0.38f, 0.52f, 0.7f, 1);
                Set(P, B.rArm, k, t, L(0.3f, 2.55f, 1.85f, 0.75f, 0.2f), null, L(0.12f, 0.6f, 0.3f, -0.25f, 0.1f));
                Set(P, B.rFore, k, t, L(0.4f, 1.9f, 0.35f, 0.5f, 0.3f), L(0, -0.4f, 0, 0.2f, 0));
                Set(P, B.rHand, k, t, L(0, -0.5f, 0.3f, 0.2f, 0));
                Set(P, B.lArm, k, t, L(0.1f, 1.25f, 0.8f, 0.25f, 0.1f), null, L(-0.1f, -0.15f, -0.35f, -0.3f, -0.1f));
                Set(P, B.lFore, k, t, L(0.3f, 0.25f, 0.9f, 1.1f, 0.3f));
                Set(P, B.spine, k, t, L(0, 0.12f, -0.12f, -0.22f, 0), L(0, -0.25f, 0.12f, 0.28f, 0));
                Set(P, B.chest, k, t, L(0, 0.1f, -0.05f, -0.12f, 0), L(0, -0.45f, 0.3f, 0.52f, 0));
                Set(P, B.neck, k, t, null, L(0, 0.3f, -0.18f, -0.35f, 0));
                Set(P, B.head, k, t, L(0, -0.05f, 0.05f, 0.08f, 0), L(0, 0.35f, -0.22f, -0.4f, 0));
            } },
            // Bend down and pick something off the ground in front of the right foot: hips back and down, back fairly
            // flat, the right hand reaches the floor, closes, and the body comes back up.
            ["pickup"] = new ClipDef { mask = "full", dur = 0.8f, look = new[] { L(0, 0), L(0.4f, -1.05f), L(0.6f, -0.95f), L(1, 0) }, f = (P, c, k) =>
            {
                float bend = Kf(k, new[] { L(0, 0), L(0.42f, 1), L(0.58f, 1), L(1, 0) });
                Pose("crouch", tmp, c);
                for (int i = 0; i < tmp.Length; i++) P[i] += (tmp[i] - P[i]) * bend * 0.85f;
                var t = L(0, 0.42f, 0.55f, 0.7f, 1);
                Add(P, B.spine, KfT(k, t, L(0, -0.35f, -0.38f, -0.2f, 0)));
                Add(P, B.chest, KfT(k, t, L(0, -0.1f, -0.1f, -0.05f, 0)), KfT(k, t, L(0, 0.15f, 0.15f, 0.05f, 0)));
                Set(P, B.rArm, k, t, L(0.1f, 1.35f, 1.25f, 0.7f, 0.15f), null, L(0.1f, 0.02f, 0.02f, 0.1f, 0.1f));
                Set(P, B.rFore, k, t, L(0.2f, 0.15f, 0.45f, 0.9f, 0.25f));
                Set(P, B.rHand, k, t, L(0, 0.25f, 0.5f, 0.3f, 0));
                Set(P, B.lArm, k, t, L(0.05f, 0.55f, 0.55f, 0.3f, 0.05f), null, L(-0.1f, -0.25f, -0.25f, -0.15f, -0.1f));
                Set(P, B.lFore, k, t, L(0.2f, 0.7f, 0.7f, 0.4f, 0.2f));
                Add(P, B.head, KfT(k, t, L(0, 0.25f, 0.25f, 0.1f, 0)));
            } },
            // Vault over a low wall: both hands go down onto the top, the body leans over them, the knees tuck up to the
            // side, then the legs swing through and reach for the ground on the far side; land soft.
            ["vault"] = new ClipDef { mask = "full", dur = 0.6f, look = new[] { L(0, 0), L(0.22f, -0.6f), L(0.5f, -0.35f), L(0.85f, 0.05f), L(1, 0) }, f = (P, c, k) =>
            {
                var t = L(0, 0.22f, 0.5f, 0.75f, 1);
                foreach (int s in SIDES)
                {
                    int a = s < 0 ? B.lArm : B.rArm, th = s < 0 ? B.lThigh : B.rThigh;
                    Set(P, a, k, t, L(0.4f, 1.25f, 0.65f, 0.3f, 0.2f), null, L(s * 0.15f, s * 0.12f, s * 0.35f, s * 0.55f, s * 0.2f));
                    Set(P, a + 1, k, t, L(0.6f, 0.25f, 0.15f, 0.35f, 0.3f));
                    Set(P, a + 2, k, t, L(0, -0.6f, -0.4f, 0, 0));
                    Set(P, th, k, t, L(0.2f, 0.6f, s < 0 ? 1.7f : 1.45f, 0.55f, 0.15f), null, L(0, 0, s < 0 ? -0.05f : 0.25f, 0, 0));
                    Set(P, th + 1, k, t, L(-0.3f, -0.9f, -2.0f, -0.5f, -0.35f));
                    Set(P, th + 2, k, t, L(0.1f, 0.3f, 0.4f, 0.1f, 0.15f));
                }
                Set(P, B.spine, k, t, L(-0.1f, -0.45f, -0.3f, -0.05f, -0.08f), null, L(0, 0, 0.12f, 0.04f, 0));
                Set(P, B.chest, k, t, L(0, -0.15f, -0.1f, 0, 0));
                Set(P, B.neck, k, t, L(0, 0.15f, 0.1f, 0, 0));
                Set(P, B.head, k, t, L(0, 0.3f, 0.2f, 0.05f, 0));
                P[OFF + 1] = KfT(k, t, L(-0.05f, -0.08f, 0.05f, 0, -0.08f));
            } },
            // Climb onto something chest-high: reach up, hands on the edge, pull while one knee comes up onto it, stand.
            ["climb"] = new ClipDef { mask = "full", dur = 0.8f, look = new[] { L(0, 0), L(0.25f, 0.2f), L(0.6f, -0.3f), L(1, 0) }, f = (P, c, k) =>
            {
                var t = L(0, 0.25f, 0.55f, 0.8f, 1);
                foreach (int s in SIDES)
                {
                    int a = s < 0 ? B.lArm : B.rArm, th = s < 0 ? B.lThigh : B.rThigh; bool lead = s > 0;
                    Set(P, a, k, t, L(0.3f, 2.5f, 1.0f, 0.4f, 0.15f), null, L(s * 0.1f, s * 0.2f, s * 0.35f, s * 0.25f, s * 0.1f));
                    Set(P, a + 1, k, t, L(0.4f, 0.3f, 1.6f, 0.9f, 0.3f));
                    Set(P, th, k, t, L(0.1f, 0.25f, lead ? 1.9f : 0.4f, lead ? 0.9f : 0.2f, 0.05f));
                    Set(P, th + 1, k, t, L(-0.2f, -0.3f, lead ? -2.1f : -0.6f, lead ? -1.2f : -0.3f, -0.08f));
                    Set(P, th + 2, k, t, L(0.1f, 0.15f, lead ? 0.3f : 0.2f, 0.2f, 0));
                }
                Set(P, B.spine, k, t, L(0, 0.05f, -0.4f, -0.2f, 0));
                Set(P, B.chest, k, t, L(0, 0.1f, -0.15f, -0.05f, 0));
                Set(P, B.head, k, t, L(0, -0.25f, 0.2f, 0.1f, 0));
            } },
            // Landing: the legs take the drop — knees and hips fold, arms come forward for balance — and straighten.
            // c.power 0..1 scales how deep (a hop vs. a two-storey drop).
            ["land"] = new ClipDef { mask = "full", dur = 0.4f, look = new[] { L(0, 0), L(0.25f, -0.18f), L(1, 0) }, f = (P, c, k) =>
            {
                float w = Kf(k, new[] { L(0, 0.2f), L(0.22f, 1), L(0.5f, 0.6f), L(1, 0) }) * (0.35f + 0.65f * c.power);
                Pose("crouch", tmp, c);
                for (int i = 0; i < tmp.Length; i++) P[i] += (tmp[i] - P[i]) * w;
                foreach (int s in SIDES) { int a = s < 0 ? B.lArm : B.rArm; Add(P, a, 0.5f * w, 0, s * 0.35f * w); }
            } },
            // Hit: the head and chest snap back and away, the arms come up, then settle.
            ["hurt"] = new ClipDef { mask = "upper", dur = 0.45f, look = new[] { L(0, 0), L(0.15f, 0.12f), L(1, 0) }, f = (P, c, k) =>
            {
                var t = L(0, 0.15f, 0.4f, 1);
                Set(P, B.spine, k, t, L(0, 0.25f, 0.15f, 0), L(0, 0.12f, 0.06f, 0));
                Set(P, B.chest, k, t, L(0, 0.18f, 0.1f, 0), L(0, 0.15f, 0.08f, 0));
                Set(P, B.neck, k, t, L(0, 0.2f, 0.1f, 0));
                Set(P, B.head, k, t, L(0, 0.35f, 0.15f, 0), L(0, 0.3f, 0.1f, 0), L(0, 0.15f, 0.05f, 0));
                foreach (int s in SIDES)
                {
                    int a = s < 0 ? B.lArm : B.rArm;
                    Set(P, a, k, t, L(0.1f, 0.9f, 0.7f, 0.1f), null, L(s * 0.1f, s * 0.3f, s * 0.25f, s * 0.1f));
                    Set(P, a + 1, k, t, L(0.2f, 1.7f, 1.5f, 0.3f));
                }
            } },
        };

        public static readonly List<string> CLIP_NAMES = new List<string> { "punch", "throw", "pickup", "vault", "climb", "land", "hurt" };
        public static ClipInfo Info(string name) => name != null && CLIPS.TryGetValue(name, out var c) ? new ClipInfo { mask = c.mask, dur = c.dur } : null;
        // Bones a clip writes: everything (null) for 'full', the torso and arms for 'upper'.
        public static int[] MaskOf(string name) => CLIPS[name].mask == "full" ? null : UPPER;
        // Where the eyes go during a clip (radians of pitch, - = down): the first-person camera follows it.
        public static float ClipLook(string name, float k, float power = 1) =>
            name != null && CLIPS.TryGetValue(name, out var c) && c.look != null ? Kf(Math.Min(1, Math.Max(0, k)), c.look) * (name == "land" ? power : 1) : 0;
        public static float[] Clip(string name, float[] P, PoseCtx c, float k) { CLIPS[name].f(P, c, Math.Min(1, Math.Max(0, k))); return P; }

        // clips.test.mjs cannot run in EditMode (it tests Runtime code): its checks run here as Debug.Asserts.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        static void SelfCheck()
        {
            var keys = new[] { L(0, 1), L(0.4f, 3), L(1, -1) };
            Debug.Assert(Kf(-1, keys) == 1 && Kf(0.4f, keys) == 3 && Kf(2, keys) == -1, "people-clips: kf ends/keys");
            float mid = Kf(0.2f, keys); Debug.Assert(mid > 1 && mid < 3, "people-clips: kf between keys");
            var c = new PoseCtx { t = 1, ph = 0.5f, amp = 0.8f, d = Dims(new PersonSpec { H = 1.72f, sex = "m" }), o = 0.3f, seat = 0.45f, stoop = 0, power = 0.7f };
            foreach (var name in CLIP_NAMES) foreach (float k in new[] { 0f, 0.3f, 0.6f, 1f })
                {
                    var P = new float[PLEN]; for (int i = 0; i < PLEN; i++) P[i] = 0.123f;
                    var output = Clip(name, (float[])P.Clone(), c, k);
                    bool finite = true; foreach (var v in output) if (float.IsNaN(v) || float.IsInfinity(v)) finite = false;
                    Debug.Assert(finite, $"people-clips: {name} at {k} not finite");
                    var m = MaskOf(name);
                    if (m != null) for (int i = 0; i < OFF + 3; i++) if (Array.IndexOf(m, i / 3) < 0 || i >= OFF) Debug.Assert(output[i] == P[i], $"people-clips: {name} touched {i}");
                }
            Debug.Assert(ClipLook("pickup", 0.45f) < -0.5f && ClipLook("nope", 0.5f) == 0, "people-clips: clipLook");
        }
    }
}
