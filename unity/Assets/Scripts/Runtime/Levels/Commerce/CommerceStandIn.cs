// The invisible NPC stand-in of levels/commerce-goods.js npc(): an empty group that accepts every call, so a level
// keeps working when people.createPerson is unavailable.
using System;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Levels
{
    sealed class CommerceStandIn : IPerson
    {
        public CommerceStandIn(string preset) { Preset = preset; Group = new GameObject("npc-standin:" + preset); }
        public GameObject Group { get; }
        public Renderer Mesh => null;
        public Transform Bone(string name) => null;
        public float Height => 1.7f;
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
}
