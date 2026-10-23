// Ports engine/people-anim.js: procedural poses. A pose is a float[PLEN]: per bone local Euler (x, y, z) in
// three.js conventions, plus the pelvis offset at OFF. Convert per bone with J.Euler when applying to Unity bones.
using System;
using System.Collections.Generic;

namespace Gyeongbo.People
{
    // GAIT entries: nominal speed, stride and shape.
    public sealed class Gait
    {
        public float v, stride, A, K, load, As, el, bob, lean, bias;
        public bool run, fixedLean;
    }

    // The per-person context poses and clips read (c in the JS): time, phase, amplitude, dims, offset, seat...
    public sealed class PoseCtx
    {
        public float t, ph, amp, o, seat = 0.45f, stoop, power = 0.5f;
        public BodyDims d;
    }

    public static class PeopleAnim
    {
        public const int NB = 18, OFF = NB * 3, PLEN = OFF + 3;
        public static readonly string[] ANIMS = { "idle", "walk", "run", "brisk", "carry", "walker", "sit", "sitChair", "talk", "phone", "wave", "point", "crouch", "stairsUp", "stairsDown", "punch", "throw", "hurt", "down", "air" };
        public static readonly Dictionary<string, Gait> GAIT = new Dictionary<string, Gait>();

        public static void Pose(string name, float[] P, PoseCtx c) => throw new NotImplementedException("port: engine/people-anim.js:pose");
        // Which grip the hands take for an animation and the visible props ('walker' | 'cart' | null).
        public static string GripOf(string name, bool walker, bool cart) => throw new NotImplementedException("port: engine/people-anim.js:gripOf");
        // Gait phase advance for a speed (metres per second) and body height.
        public static float GaitStep(string name, float speed, float H) => throw new NotImplementedException("port: engine/people-anim.js:gaitStep");
    }
}
