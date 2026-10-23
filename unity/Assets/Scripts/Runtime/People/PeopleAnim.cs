// Ports engine/people-anim.js: procedural poses. A pose is a float[PLEN]: per bone local Euler (x, y, z) in
// three.js conventions, plus the pelvis offset at OFF. Person converts per bone with J.Euler.
// Conventions (rest = arms hanging, facing -z, right side +x): +x flexes thighs/upper arms/elbows forward and
// tilts spine/neck/head back; knee flexion is -x; +y turns left; +z tilts the top toward -x (right-arm abduction).
using System;
using System.Collections.Generic;
using static Gyeongbo.People.PeopleBody;

namespace Gyeongbo.People
{
    // GAIT entries: nominal speed (m/s), metres per full gait cycle at that speed (for a 1.72 m adult), shape.
    public sealed class Gait
    {
        public float v, stride, A, K, load, As, el, bob, lean, bias;
        public bool run, fixedLean;
    }

    // The per-person context poses and clips read (c in the JS): { t, ph, amp, d, o, seat, stoop, power }.
    public sealed class PoseCtx
    {
        public float t, ph, amp, o, seat = 0.45f, stoop, power = 0.5f;
        public BodyDims d;
    }

    public static class PeopleAnim
    {
        public const int NB = 18, OFF = NB * 3, PLEN = OFF + 3;
        const float PI = (float)Math.PI;
        public static readonly string[] ANIMS = { "idle", "walk", "run", "brisk", "carry", "walker", "sit", "sitChair", "talk", "phone", "wave", "point", "crouch", "stairsUp", "stairsDown", "punch", "throw", "hurt", "down", "air" };
        public static readonly Dictionary<string, Gait> GAIT = new Dictionary<string, Gait>
        {
            ["walk"] = new Gait { v = 1.3f, stride = 1.42f, A = 0.36f, K = 1.0f, load = 0.14f, As = 0.3f, el = 0.2f, bob = 0.022f, lean = 0.03f },
            ["brisk"] = new Gait { v = 1.8f, stride = 1.7f, A = 0.45f, K = 1.1f, load = 0.16f, As = 0.48f, el = 0.55f, bob = 0.028f, lean = 0.09f },
            ["run"] = new Gait { v = 3.6f, stride = 2.5f, A = 0.62f, K = 1.75f, load = 0.3f, As = 0.62f, el = 1.45f, bob = 0.045f, lean = 0.2f, run = true },
            ["carry"] = new Gait { v = 1.0f, stride = 1.2f, A = 0.28f, K = 0.9f, load = 0.15f, As = 0.22f, el = 0.2f, bob = 0.024f, lean = 0.05f },
            ["walker"] = new Gait { v = 0.8f, stride = 0.95f, A = 0.25f, K = 0.85f, load = 0.12f, As = 0, el = 0.35f, bob = 0.012f, lean = 0.24f, fixedLean = true },
            ["stairsUp"] = new Gait { v = 0.55f, stride = 0.6f, A = 0.42f, K = 1.35f, load = 0.38f, As = 0.14f, el = 0.25f, bob = 0.03f, lean = 0.16f, bias = 0.35f, fixedLean = true },
            ["crouch"] = new Gait { v = 1.0f, stride = 0.8f }, // crouch-walk: only its rate/amp are used (POSES.crouch steps the folded legs)
            ["stairsDown"] = new Gait { v = 0.6f, stride = 0.62f, A = 0.3f, K = 0.6f, load = 0.55f, As = 0.12f, el = 0.25f, bob = 0.03f, lean = -0.05f, bias = 0.06f, fixedLean = true },
        };

        static float sin(float x) => (float)Math.Sin(x);
        static float cos(float x) => (float)Math.Cos(x);
        static float max(float a, float b) => Math.Max(a, b);
        static float min(float a, float b) => Math.Min(a, b);
        static float pow(float a, float b) => (float)Math.Pow(a, b);
        internal static void Add(float[] P, int b, float x, float y = 0, float z = 0) { P[b * 3] += x; P[b * 3 + 1] += y; P[b * 3 + 2] += z; }
        static float N2(float t, float a, float b, float p = 0) => sin(t * a + p) * 0.6f + sin(t * b + p * 1.7f) * 0.4f; // soft organic noise
        static readonly int[] SIDES = { -1, 1 };

        // Keep the feet planted when the pelvis slides sideways by dx.
        static void PlantX(float[] P, PoseCtx c, float dx)
        {
            float a = dx / (c.d.L1 + c.d.L2);
            foreach (int t in new[] { B.lThigh, B.rThigh }) { Add(P, t, 0, 0, -a); Add(P, t + 2, 0, 0, a); }
        }

        static void GaitPose(float[] P, Gait g, PoseCtx c)
        {
            float ph = c.ph, amp = c.amp;
            foreach (int s in SIDES)
            {
                float q = ph + (s > 0 ? PI : 0); int th0 = s < 0 ? B.lThigh : B.rThigh;
                float sw = max(0, cos(q + 0.35f));
                float th = amp * (g.A * sin(q) + 0.04f) + g.bias * sw * min(1, amp * 2);
                float kn = -amp * (g.K * pow(sw, 1.6f) + g.load * max(0, sin(2 * q - PI))) - 0.04f - g.bias * sw * 1.2f * min(1, amp * 2);
                float ft = -(th + kn) * 0.8f - amp * 0.35f * pow(max(0, sin(-q)), 2) + amp * 0.12f * pow(max(0, sin(q)), 4);
                Add(P, th0, th, 0, s * 0.03f); Add(P, th0 + 1, kn); Add(P, th0 + 2, ft, 0, -s * 0.03f);
            }
            float a = amp;
            P[OFF + 1] += g.run ? -g.bob * a * (1 + cos(2 * ph)) / 2 : -g.bob * a * (1 - cos(2 * ph)) / 2 - 0.01f * a;
            float dx = a * (g.run ? 0.006f : 0.014f) * cos(ph);
            P[OFF] += dx; PlantX(P, c, dx);
            float py = -0.25f * g.A * a * sin(ph), lean = g.lean * (g.fixedLean ? 1 : a);
            Add(P, B.pelvis, -lean * 0.3f, py, 0.03f * a * sin(2 * ph));
            Add(P, B.spine, -lean * 0.5f, -py * 0.6f); Add(P, B.chest, -lean * 0.3f + (g.run ? 0.02f * sin(2 * ph) : 0), -py * 0.9f);
            Add(P, B.neck, lean * 0.4f); Add(P, B.head, lean * 0.6f, py * 0.4f);
            foreach (int s in SIDES)
            {
                int arm = s < 0 ? B.lArm : B.rArm; float f = s * g.As * a * sin(ph);
                Add(P, arm, f, 0, s * (0.1f + 0.06f * a)); Add(P, arm + 1, g.run ? g.el : g.el + 0.45f * max(0, f) * (g.As > 0.4f ? 1.4f : 1)); Add(P, arm + 2, 0.08f);
            }
        }

        static void Idle(float[] P, PoseCtx c, float k = 1)
        {
            float t = c.t + c.o * 37, br = sin(t * 1.65f), sh = (sin(t * 0.31f) * 0.8f + sin(t * 0.13f + 1) * 0.2f) * k;
            float dx = 0.02f * sh; P[OFF] += dx; PlantX(P, c, dx);
            Add(P, B.pelvis, 0, 0, -0.03f * sh); Add(P, B.spine, 0, 0, 0.018f * sh); Add(P, B.chest, 0.012f * br, 0, 0.012f * sh);
            foreach (int s in SIDES) // the unweighted leg relaxes
            {
                int th = s < 0 ? B.lThigh : B.rThigh; float r = max(0, -s * sh);
                Add(P, th, 0.06f * r, 0, s * 0.035f); Add(P, th + 1, -0.13f * r - 0.03f); Add(P, th + 2, 0.07f * r, 0, -s * 0.035f);
                int arm = s < 0 ? B.lArm : B.rArm;
                Add(P, arm, 0.04f + 0.01f * br, 0, s * (0.09f + 0.008f * br)); Add(P, arm + 1, 0.16f); Add(P, arm + 2, 0.06f);
            }
            Add(P, B.neck, -0.006f * br); Add(P, B.head, -0.03f + 0.03f * sin(t * 0.17f) * k, 0.22f * sin(t * 0.21f) * sin(t * 0.083f + 1) * k);
        }

        // Leg angles for a seated/crouched pose: thigh flex th, hip-joint height hy; ankle at ankle height.
        // back: shins slope back from the knees (squat) instead of forward (sitting).
        static float LegsFold(float[] P, PoseCtx c, float th, float hy, float abd = 0.12f, bool back = false)
        {
            var d = c.d; float cc = (back ? -1 : 1) * (float)Math.Acos(Math.Min(1, Math.Max(-1, (hy - d.L1 * cos(th) - d.ankleY) / d.L2)));
            foreach (int s in SIDES) { int t = s < 0 ? B.lThigh : B.rThigh; Add(P, t, th, 0, s * abd); Add(P, t + 1, cc - th); Add(P, t + 2, -cc, 0, -s * abd); }
            P[OFF + 1] += hy - d.hipY;
            return d.L1 * sin(th) + d.L2 * sin(cc); // how far the ankles end up in front of the hips
        }

        static void Crouch(float[] P, PoseCtx c)
        {
            var d = c.d; float th = 1.9f, ccw = -0.35f, hy = d.ankleY + d.L1 * cos(th) + d.L2 * cos(ccw);
            float fwd = LegsFold(P, c, th, hy, 0.2f, true);
            P[OFF + 2] += fwd; // feet stay at the origin, hips go back
            Add(P, B.spine, -0.42f); Add(P, B.chest, -0.15f); Add(P, B.neck, 0.15f); Add(P, B.head, 0.32f);
            foreach (int s in SIDES) { int a = s < 0 ? B.lArm : B.rArm; Add(P, a, 0.85f, 0, s * 0.18f); Add(P, a + 1, 0.55f); }
            Add(P, B.chest, 0.01f * sin(c.t * 1.65f));
            if (c.amp > 0.02f) // moving low: short steps from the folded legs, a small bob, arms swinging a little
            {
                foreach (int s in SIDES)
                {
                    float q = c.ph + (s > 0 ? PI : 0), sw = max(0, cos(q + 0.35f)); int t0 = s < 0 ? B.lThigh : B.rThigh;
                    Add(P, t0, 0.34f * c.amp * sin(q)); Add(P, t0 + 1, -0.3f * c.amp * sw); Add(P, t0 + 2, 0.12f * c.amp * sin(q));
                    Add(P, s < 0 ? B.lArm : B.rArm, -s * 0.18f * c.amp * sin(c.ph));
                }
                P[OFF + 1] -= 0.018f * c.amp * (1 - cos(2 * c.ph)) / 2; Add(P, B.spine, 0, -0.08f * c.amp * sin(c.ph));
            }
        }

        static readonly Dictionary<string, Action<float[], PoseCtx>> POSES = new Dictionary<string, Action<float[], PoseCtx>>
        {
            ["idle"] = (P, c) => Idle(P, c),
            ["walk"] = (P, c) => GaitPose(P, GAIT["walk"], c),
            ["brisk"] = (P, c) => GaitPose(P, GAIT["brisk"], c),
            ["run"] = (P, c) => { GaitPose(P, GAIT["run"], c); Add(P, B.head, -0.05f); },
            ["stairsUp"] = (P, c) => GaitPose(P, GAIT["stairsUp"], c),
            ["stairsDown"] = (P, c) => { GaitPose(P, GAIT["stairsDown"], c); Add(P, B.lArm, 0, 0, -0.1f); Add(P, B.rArm, 0, 0, 0.1f); },
            ["carry"] = (P, c) => // heavy container in the right hand: lean away, free arm out for balance
            {
                GaitPose(P, GAIT["carry"], c); if (c.amp < 0.05f) Idle(P, c, 0.4f);
                float sw = c.amp * sin(c.ph);
                P[B.rArm * 3] = 0.05f * sw; P[B.rArm * 3 + 2] = 0.2f; P[B.rFore * 3] = 0.05f;
                P[B.lArm * 3 + 2] = -0.36f; Add(P, B.lFore, 0.15f);
                Add(P, B.spine, 0, 0, 0.1f); Add(P, B.chest, 0, 0, 0.05f); Add(P, B.head, 0, 0, -0.12f);
                P[OFF] += 0.02f; PlantX(P, c, 0.02f);
            },
            ["walker"] = (P, c) => { GaitPose(P, GAIT["walker"], c); if (c.amp < 0.05f) Idle(P, c, 0.25f); Add(P, B.head, -0.08f); },
            ["talk"] = (P, c) =>
            {
                Idle(P, c, 0.6f);
                float t = c.t + c.o * 23, g1 = 0.5f + 0.5f * N2(t, 1.3f, 0.7f), g2 = 0.5f + 0.5f * N2(t, 0.9f, 1.6f, 2);
                Add(P, B.rArm, 0.22f + 0.22f * g1 + 0.05f * sin(t * 2.9f), 0, 0.05f); Add(P, B.rFore, 0.95f + 0.4f * g1 + 0.12f * sin(t * 3.7f + 2), -0.5f - 0.35f * g1); Add(P, B.rHand, 0.12f * sin(t * 2.3f + 1));
                Add(P, B.lArm, 0.12f + 0.16f * g2, 0, -0.04f); Add(P, B.lFore, 0.55f + 0.45f * g2, 0.3f * g2);
                Add(P, B.chest, 0, 0.07f * N2(t, 0.9f, 0.4f, 2)); Add(P, B.head, 0.045f * sin(t * 2.1f + 0.5f), 0.1f * N2(t, 0.6f, 0.25f, 1));
            },
            ["phone"] = (P, c) =>
            {
                Idle(P, c, 0.5f);
                float t = c.t + c.o * 11, tap = 0.06f * max(0, sin(t * 7.5f)) * (sin(t * 0.8f) > 0 ? 1 : 0);
                Add(P, B.neck, -0.18f); Add(P, B.head, -0.4f);
                P[B.rArm * 3] = 0.34f; P[B.rArm * 3 + 2] = -0.13f; P[B.rFore * 3] = 1.6f; P[B.rFore * 3 + 1] = -1.25f; P[B.rHand * 3] = 0.1f + tap;
            },
            ["wave"] = (P, c) =>
            {
                Idle(P, c, 0.5f);
                float t = c.t * 6.8f;
                P[B.rArm * 3] = 0.25f; P[B.rArm * 3 + 2] = 2.45f + 0.12f * sin(t);
                P[B.rFore * 3] = 0.3f; P[B.rFore * 3 + 1] = -1.4f; P[B.rFore * 3 + 2] = 0.32f * sin(t + 0.6f); P[B.rHand * 3] = 0.1f;
                Add(P, B.spine, 0, 0, 0.04f); Add(P, B.head, 0.04f, -0.05f);
            },
            ["point"] = (P, c) =>
            {
                Idle(P, c, 0.4f);
                P[B.rArm * 3] = 1.42f + 0.05f * sin(c.t * 2.2f); P[B.rArm * 3 + 2] = 0.18f; P[B.rFore * 3] = 0.08f; P[B.rFore * 3 + 1] = -0.3f;
                Add(P, B.chest, 0, -0.12f); Add(P, B.head, 0.03f, -0.15f);
            },
            ["punch"] = (P, c) => // right straight, left hand guarding the face
            {
                Idle(P, c, 0.3f);
                P[B.rArm * 3] = 1.5f; P[B.rArm * 3 + 2] = 0.1f; P[B.rFore * 3] = 0.05f; P[B.lArm * 3] = 0.7f; P[B.lArm * 3 + 2] = -0.2f; P[B.lFore * 3] = 1.9f;
                Add(P, B.chest, 0, -0.4f); Add(P, B.spine, 0.08f, -0.15f); Add(P, B.head, 0.05f, 0.3f);
            },
            ["throw"] = (P, c) => // release: arm high in front, body turned through
            {
                Idle(P, c, 0.3f);
                P[B.rArm * 3] = 2.1f; P[B.rArm * 3 + 2] = 0.25f; P[B.rFore * 3] = 0.35f; P[B.lArm * 3] = 0.5f; P[B.lArm * 3 + 2] = -0.4f;
                Add(P, B.chest, 0.1f, -0.45f); Add(P, B.spine, 0.12f, -0.2f); Add(P, B.head, 0, 0.35f);
            },
            ["hurt"] = (P, c) => // knocked back: torso thrown back, arms flung out
            {
                Idle(P, c, 0.2f);
                Add(P, B.spine, 0.28f); Add(P, B.chest, 0.2f); Add(P, B.neck, 0.15f); Add(P, B.head, 0.25f);
                foreach (int s in SIDES) { int a = s < 0 ? B.lArm : B.rArm; Add(P, a, 0.4f, 0, s * 0.75f); Add(P, a + 1, 0.5f); }
            },
            ["air"] = (P, c) => // off the ground: one knee higher than the other, arms out a little for balance
            {
                Idle(P, c, 0.15f);
                Add(P, B.lThigh, 0.6f); Add(P, B.lShin, -1.0f); Add(P, B.lFoot, 0.35f); Add(P, B.rThigh, 0.25f); Add(P, B.rShin, -0.55f); Add(P, B.rFoot, 0.25f);
                foreach (int s in SIDES) { int a = s < 0 ? B.lArm : B.rArm; Add(P, a, 0.35f, 0, s * 0.38f); Add(P, a + 1, 0.45f); }
                Add(P, B.spine, -0.06f); Add(P, B.head, 0.05f);
            },
            ["down"] = (P, c) => // on the ground: crouched low, head down, arms over the head
            {
                Crouch(P, c); Add(P, B.spine, -0.25f); Add(P, B.head, 0.35f);
                foreach (int s in SIDES) { int a = s < 0 ? B.lArm : B.rArm; Add(P, a, 1.2f, 0, s * 0.2f); Add(P, a + 1, 1.6f); }
            },
            ["crouch"] = Crouch,
            ["sit"] = (P, c) => // on a floor mat, knees up; origin = under the hips. Hands rest on the knees (IK, see Person)
            {
                LegsFold(P, c, 2.0f, 0.058f * c.d.H, 0.2f);
                float t = c.t + c.o * 19;
                Add(P, B.pelvis, -0.12f); Add(P, B.lThigh, 0.12f); Add(P, B.rThigh, 0.12f); // tilt the hips, not the planted legs
                Add(P, B.spine, -0.08f, 0, 0.02f * sin(t * 0.2f)); Add(P, B.chest, -0.05f + 0.012f * sin(t * 1.5f));
                Add(P, B.head, 0.06f + 0.04f * sin(t * 0.15f), 0.3f * sin(t * 0.11f) * sin(t * 0.05f + 2));
                foreach (int s in SIDES) { int a = s < 0 ? B.lArm : B.rArm; Add(P, a, 0.7f, 0, s * 0.1f); Add(P, a + 1, 0.6f); }
            },
            ["sitChair"] = (P, c) => // origin = centre of the seat on the floor; seat height c.seat
            {
                LegsFold(P, c, 1.5f, c.seat + 0.04f * c.d.H, 0.1f);
                float t = c.t + c.o * 7, k = 0.5f + 0.5f * sin(t * 0.4f);
                Add(P, B.spine, 0.04f); Add(P, B.chest, 0.01f * sin(t * 1.6f)); Add(P, B.head, -0.12f + 0.05f * sin(t * 0.13f), 0.15f * sin(t * 0.09f));
                foreach (int s in SIDES)
                {
                    int a = s < 0 ? B.lArm : B.rArm;
                    Add(P, a, 0.3f, 0, s * 0.08f); Add(P, a + 1, 1.15f + 0.04f * k * sin(t * (s < 0 ? 5 : 6.3f)), s * 0.25f); Add(P, a + 2, -0.1f);
                }
            },
        };

        // Additive posture for age (stoop 0..0.3).
        static void Posture(float[] P, float stoop)
        {
            if (stoop == 0) return;
            Add(P, B.spine, -stoop * 0.5f); Add(P, B.chest, -stoop * 0.6f); Add(P, B.neck, -stoop * 0.2f); Add(P, B.head, stoop * 0.85f);
            foreach (int t in new[] { B.lThigh, B.rThigh }) { Add(P, t, stoop * 0.12f); Add(P, t + 1, -stoop * 0.22f); Add(P, t + 2, stoop * 0.1f); }
            P[OFF + 1] -= stoop * 0.02f;
        }

        // Fill P with the pose of `name`. c = { t, ph, amp, d, o, seat, stoop }.
        public static float[] Pose(string name, float[] P, PoseCtx c)
        {
            Array.Clear(P, 0, P.Length);
            (name != null && POSES.TryGetValue(name, out var f) ? f : POSES["idle"])(P, c);
            Posture(P, c.stoop);
            return P;
        }

        // Which hands hold something with IK this frame: 'walker' | 'cart' | 'knees' | null.
        public static string GripOf(string name, bool walker, bool cart)
        {
            if (name == "sit") return "knees";
            if (name == "sitChair" || name == "crouch" || name == "phone" || name == "wave" || name == "point" || name == "carry") return null;
            if (walker) return "walker";
            if (cart) return "cart";
            return null;
        }

        // Gait phase speed and amplitude for an animation at a given speed (m/s, null = nominal) and body height.
        public static (float rate, float amp) GaitStep(string name, float? speed, float H)
        {
            if (name == null || !GAIT.TryGetValue(name, out var g) || (name == "crouch" && speed == null)) return (0, 0); // a placed crouch stays still
            float v = speed ?? g.v, r = Math.Max(0, v) / g.v;
            float stride = g.stride * (H / 1.72f) * Math.Max(0.45f, (float)Math.Sqrt(Math.Min(1.6f, r)));
            return (2 * PI * v / stride, Math.Min(1.25f, (float)Math.Sqrt(r)));
        }
    }
}
