// Ports engine/people-clips.js: keyed one-shot action clips (punch, throw, pickup, vault, climb, land, hurt)
// layered over the gait; 'upper' clips write only the upper body, 'full' clips own the whole pose.
using System;
using System.Collections.Generic;

namespace Gyeongbo.People
{
    public sealed class ClipInfo
    {
        public string mask;          // 'upper' | 'full'
        public float dur;
    }

    public static class PeopleClips
    {
        public static readonly List<string> CLIP_NAMES = new List<string>();
        public static ClipInfo Info(string name) => throw new NotImplementedException("port: engine/people-clips.js:clipInfo");
        // Bones a clip may write; null = the whole pose.
        public static int[] MaskOf(string name) => throw new NotImplementedException("port: engine/people-clips.js:maskOf");
        // Eye pitch the clip asks for at progress k.
        public static float ClipLook(string name, float k, float power = 1) => throw new NotImplementedException("port: engine/people-clips.js:clipLook");
        public static float[] Clip(string name, float[] P, PoseCtx c, float k) => throw new NotImplementedException("port: engine/people-clips.js:clip");
        // Keyframe track [[t, v], ...]: eased between keys, held at the ends.
        public static float Kf(float k, float[][] keys) => throw new NotImplementedException("port: engine/people-clips.js:kf");
    }
}
