// Ports the stand-ins in engine/core.js: capsule people and no-op fx, used when the real implementations
// fail to start so the game keeps running.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Engine
{
    // core.js capsulePerson / emptyCrowd behind IPeople.
    public sealed class CapsulePeople : IPeople
    {
        public bool Fallback => true;
        public IPerson CreatePerson(string preset = "crowd", int? seed = null) => new CapsulePerson(preset ?? "crowd");
        public ICrowd CreateCrowd(CrowdOptions o) => new EmptyCrowd();
    }

    sealed class CapsulePerson : IPerson
    {
        static readonly Dictionary<string, int> PERSON_COLORS = new Dictionary<string, int>
        { ["jeongmin"] = 0x2b3a55, ["seoyeon"] = 0xa98457, ["manager"] = 0x2a3550, ["clerk"] = 0x3d7a4a, ["guide"] = 0xd6e33a, ["coworker"] = 0x55606e, ["elder"] = 0x7a6a5a };

        public CapsulePerson(string preset)
        {
            Preset = preset;
            Group = J.Group($"person-{preset}");
            var m = Mat.Lit(new MatSpec { color = J.Hex(PERSON_COLORS.TryGetValue(preset, out var c) ? c : 0x6b6f78), roughness = 0.92f });
            var body = Geo.Obj(Geo.Capsule(0.24f, 1.24f, 6, 14), m, "body");
            J.SetPos(body, 0, 0.86f, 0);
            Group.Add(body);
        }

        public GameObject Group { get; }
        public Renderer Mesh => null;
        public Transform Bone(string name) => null;
        public float Height => 1.72f;
        public string Preset { get; }
        public float Seat { get; set; }
        public string Anim { get; private set; } = "idle";
        public void SetAnim(string name, float? speed = null, bool instant = false) => Anim = name;
        public void SetSpeed(float? mps) { }
        public bool Play(string name, float? seconds = null, float power = 0.5f) => false;
        public string Playing => null;
        public float Phase => 0;
        public float Stride => 0;
        public float? ClipK => null;
        public float ClipLook => 0;
        public void SetLean(float forward = 0, float side = 0) { }
        public void SetAim(float? pitch) { }
        public void SetHold(bool on) { }
        public void SetProp(string name, bool on = true) { }
        public bool HasProp(string name) => false;
        public void LookAt(float? x, float? z) { }
        public void SetShadow(bool on) { }
        public void Update(float dt) { }
        public void Dispose() { if (Group != null) UnityEngine.Object.Destroy(Group); }
        public Action<float, float, bool> OnHit { get; set; }
        public bool Hostile { get; set; }
    }

    sealed class EmptyCrowd : ICrowd
    {
        public GameObject Group { get; } = J.Group("crowd");
        public void Update(float dt, FrameCtx ctx = null) { }
        public void SetMode(string mode, CrowdModeOptions o = null) { }
        public void SetPaths(List<List<XZ>> paths) { }
        public void SetArea(RectXZ area) { }
        public string Mode => "none";
        public int Active => 0;
        public IReadOnlyList<IPerson> Members => Array.Empty<IPerson>();
        public void Dispose() { if (Group != null) UnityEngine.Object.Destroy(Group); }
    }

    // core.js stubFx behind IFx.
    public sealed class StubFx : IFx
    {
        public bool Stub => true;
        public IFire Fire(FireOptions o) => new StubFxObject();
        public ISmoke Smoke(SmokeOptions o) => new StubFxObject();
        public IRain Rain(GameObject scene, Camera camera, RainOptions o = null) => new StubFxObject();
        public IDust Dust(DustOptions o) => new StubFxObject();
        public ISpray ExtinguisherSpray(SprayOptions o) => new StubFxObject();
        public bool ReduceMotion => false;
        public void SetReduceMotion(bool v) { }
    }

    // fxObj(): a group and no-op setters.
    sealed class StubFxObject : IFire, ISmoke, IRain, IDust, ISpray
    {
        public GameObject Group { get; } = J.Group("fx");
        public void Update(float dt, Camera camera = null) { }
        public void Dispose() { if (Group != null) UnityEngine.Object.Destroy(Group); }
        public float Level => 0;
        public void SetLevel(float v) { }
        public Light Light => null;
        public float Density => 0;
        public void SetDensity(float v) { }
        public bool On => false;
        public void SetOn(bool v) { }
        public void Aim(Vector3 jsTarget) { }
    }
}
