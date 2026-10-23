// Ports story/cine.js: letterboxed camera moves over the live world, actors walking through them, timed events
// and captions; Space / Enter / Esc skips the rest. Positions and camera points are JS space.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Systems
{
    // cam: positions p and look-at targets t eased along a Catmull-Rom curve over the shot.
    public sealed class CamPath
    {
        public List<Vector3> p = new List<Vector3>(), t = new List<Vector3>();
        public float[] fov;                      // [from, to] or null
        public string ease = "inout";            // inout | linear | out | in
        public float hand = 0.04f;               // handheld sway in metres
    }

    public struct CamPose
    {
        public Vector3 p, t;
        public float? fov;
    }

    // [at, text, speaker?, ms?]
    public sealed class CaptionCue
    {
        public float at;
        public string text, speaker;
        public int? ms;
    }

    // [seconds, fn]
    public sealed class TimedCue
    {
        public float at;
        public Action fn;
    }

    // fill: a soft light kept on the subject; at is fixed or follows a function (null = off this frame).
    public sealed class ShotFill
    {
        public Vector3? at;
        public Func<Vector3?> follow;
        public float i = 6;
        public Color? color;
    }

    public sealed class Shot
    {
        public float dur;
        public CamPath cam;
        public Func<float, float, CamPose> camFn;   // (k 0..1, seconds) -> pose, instead of cam
        public List<CaptionCue> lines = new List<CaptionCue>();
        public List<TimedCue> at = new List<TimedCue>();
        public string env;                          // env preset at the start of the shot
        public float envSeconds;
        public float slow = 1;                      // world time scale
        public bool fade;
        public Func<Action> setup;                  // returns an optional cleanup
        public float exposure;                      // stops added to the grade
        public ShotFill fill;
    }

    public sealed class ActorOptions
    {
        public XZ? at;
        public List<XZ> path;
        public float speed = 1.3f, yaw, delay;
        public string anim = "idle";
        public List<string> props = new List<string>();
    }

    public sealed class Cine
    {
        public static Cine Create(Ctx ctx) => throw new NotImplementedException("port: story/cine.js:createCine");
        // Resolves 'done', 'skipped' or 'busy'. Controls are off and the HUD is letterboxed meanwhile.
        public Task<string> Play(IList<Shot> shots, string[] title = null) => throw new NotImplementedException("port: story/cine.js:play");
        public IPerson Actor(string preset, ActorOptions o) => throw new NotImplementedException("port: story/cine.js:actor");
        // A missile from `from` to `to` over `seconds` (JS space); removed when the cutscene ends.
        public IDisposable Missile(Vector3 from, Vector3 to, float seconds = 1) => throw new NotImplementedException("port: story/cine.js:missile");
        // Typed-out title lines (place, time) in the corner; null clears.
        public void Title(string[] lines) => throw new NotImplementedException("port: story/cine.js:title");
        public bool Busy => throw new NotImplementedException("port: story/cine.js:busy");
    }
}
