// SHARED. Facade over engine/fx.js (fire, smoke, rain, dust, extinguisher powder). Option fields are the
// JS keys; positions JS space. Each effect's Group is parented by the caller (level group or scene).
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Core
{
    public interface IFxObject
    {
        GameObject Group { get; }
        void Update(float dt, Camera camera = null);
        void Dispose();
    }

    public interface IFire : IFxObject
    {
        float Level { get; }
        void SetLevel(float v);              // 0..1
        Light Light { get; }                 // null when light = 0
    }

    public interface ISmoke : IFxObject
    {
        float Density { get; }
        void SetDensity(float v);            // 0..1
    }

    public interface IRain : IFxObject
    {
        bool On { get; }
        void SetOn(bool v);
    }

    public interface IDust : IFxObject
    {
        void SetOn(bool v);
        void SetDensity(float v);
    }

    public interface ISpray : IFxObject
    {
        bool On { get; }
        void SetOn(bool v);
        void Aim(Vector3 jsTarget);
    }

    public sealed class FireOptions
    {
        public float x, y, z;
        public float scale = 1, radius = 0.28f, level = 1;
        public int count = 46;
        public float light = 1;              // JS light: false -> 0 (no light), number -> multiplier
        public bool shadow;
    }

    public sealed class SmokeOptions
    {
        public float x, y, z;
        public int count = 72;
        public float ceiling = 2.4f, spread = 4, radius = 0.25f, density = 0.5f;
        public Color? color;
        public RectXZ bounds;                // relative to (x, z)
    }

    public sealed class RainOptions
    {
        public int count = 9000;
    }

    public sealed class Box3
    {
        public float x0 = -4, x1 = 4, y0 = 0.2f, y1 = 2.6f, z0 = -4, z1 = 4;
    }

    public sealed class DustOptions
    {
        public Box3 box = new Box3();
        public int count = 260;
        public float size = 0.012f, opacity = 0.35f;
        public Color? color;
        public bool? reduceMotion;
    }

    public sealed class SprayOptions
    {
        public float x, y, z;
        public int count = 170;
        public float speed = 8;
        public float? floorY;
    }

    public interface IFx
    {
        bool Stub { get; }                   // the engine's no-op stand-in
        IFire Fire(FireOptions o);
        ISmoke Smoke(SmokeOptions o);
        IRain Rain(GameObject scene, Camera camera, RainOptions o = null);
        IDust Dust(DustOptions o);
        ISpray ExtinguisherSpray(SprayOptions o);
        bool ReduceMotion { get; }
        void SetReduceMotion(bool v);
    }
}
