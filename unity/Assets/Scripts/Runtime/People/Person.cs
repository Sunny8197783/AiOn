// Ports engine/people.js: procedural, skinned, clothed adults and elders with blended procedural animation
// (walk/brisk/run blend space, one-shot clips, head look, arm IK on walker/cart, hanging props).
// Feet at the origin facing JS -z; 'sit' / 'sitChair' put the hips there. ProceduralPeople is the IPeople
// the engine uses; Person is one IPerson.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.People
{
    public sealed class ProceduralPeople : IPeople
    {
        public bool Fallback => false;
        public IPerson CreatePerson(string preset = "crowd", int? seed = null) => Person.Create(preset, seed);
        public ICrowd CreateCrowd(CrowdOptions o) => Crowd.Create(o);
    }

    public sealed class Person : IPerson
    {
        public static readonly string[] ANIMS = PeopleAnim.ANIMS;
        public static readonly string[] PROPS = { "backpack", "waterJug", "walker", "flashlight", "phone", "baton", "basket", "cart" };

        // preset: jeongmin | seoyeon | manager | clerk | guide | coworker | elder | crowd. seed null = next auto seed.
        public static Person Create(string preset = "crowd", int? seed = null) => throw new NotImplementedException("port: engine/people.js:createPerson");

        public GameObject Group => throw new NotImplementedException("port: engine/people.js:group");
        public Renderer Mesh => throw new NotImplementedException("port: engine/people.js:mesh");
        public Transform Bone(string name) => throw new NotImplementedException("port: engine/people.js:bones");
        public float Height => throw new NotImplementedException("port: engine/people.js:height");
        public string Preset => throw new NotImplementedException("port: engine/people.js:preset");
        public float Seat { get => throw new NotImplementedException("port: engine/people.js:seat"); set => throw new NotImplementedException("port: engine/people.js:seat"); }
        public string Anim => throw new NotImplementedException("port: engine/people.js:anim");
        public void SetAnim(string name, float? speed = null, bool instant = false) => throw new NotImplementedException("port: engine/people.js:setAnim");
        public void SetSpeed(float? mps) => throw new NotImplementedException("port: engine/people.js:setSpeed");
        public bool Play(string name, float? seconds = null, float power = 0.5f) => throw new NotImplementedException("port: engine/people.js:play");
        public string Playing => throw new NotImplementedException("port: engine/people.js:playing");
        public float Phase => throw new NotImplementedException("port: engine/people.js:phase");
        public float Stride => throw new NotImplementedException("port: engine/people.js:stride");
        public float? ClipK => throw new NotImplementedException("port: engine/people.js:clipK");
        public float ClipLook => throw new NotImplementedException("port: engine/people.js:clipLook");
        public void SetLean(float forward = 0, float side = 0) => throw new NotImplementedException("port: engine/people.js:setLean");
        public void SetAim(float? pitch) => throw new NotImplementedException("port: engine/people.js:setAim");
        public void SetHold(bool on) => throw new NotImplementedException("port: engine/people.js:setHold");
        public void SetProp(string name, bool on = true) => throw new NotImplementedException("port: engine/people.js:setProp");
        public bool HasProp(string name) => throw new NotImplementedException("port: engine/people.js:hasProp");
        public void LookAt(float? x, float? z) => throw new NotImplementedException("port: engine/people.js:lookAt");
        public void SetShadow(bool on) => throw new NotImplementedException("port: engine/people.js:setShadow");
        public void Update(float dt) => throw new NotImplementedException("port: engine/people.js:update");
        public void Dispose() => throw new NotImplementedException("port: engine/people.js:dispose");
        public Action<float, float, bool> OnHit { get; set; }
        public bool Hostile { get; set; }
    }
}
