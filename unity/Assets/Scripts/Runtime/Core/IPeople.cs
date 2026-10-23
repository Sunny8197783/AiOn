// SHARED. Facades over engine/people.js and engine/crowd.js so levels, story and the engine call people
// without depending on the People implementation (or the capsule stand-in the engine falls back to).
// Positions are JS space: place a person with J.SetPos(p.Group, x, y, z) / J.SetYaw(p.Group, yaw).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Core
{
    public interface IPerson
    {
        GameObject Group { get; }            // feet at the origin, facing JS -z (J.Yaw handles the mirror)
        Renderer Mesh { get; }               // the body renderer (first-person cut material), may be null
        Transform Bone(string name);         // 'head', 'neck', 'rHand', ... (people-body B names); null if none
        float Height { get; }
        string Preset { get; }
        float Seat { get; set; }             // seat height for 'sitChair' (store sets 0.76)
        string Anim { get; }
        // name: one of people-anim ANIMS. speed: gait speed m/s; instant: no cross-fade.
        void SetAnim(string name, float? speed = null, bool instant = false);
        void SetSpeed(float? mps);
        // One-shot clip (punch, throw, pickup, vault, climb, land, hurt) over the current animation.
        bool Play(string name, float? seconds = null, float power = 0.5f);
        string Playing { get; }
        float Phase { get; }                 // gait phase: a heel strikes at pi/2 + n*pi
        float Stride { get; }                // how much the legs move, 0..~1.25
        float? ClipK { get; }
        float ClipLook { get; }
        void SetLean(float forward = 0, float side = 0);
        void SetAim(float? pitch);
        void SetHold(bool on);
        void SetProp(string name, bool on = true);   // people.js PROPS
        bool HasProp(string name);
        void LookAt(float? x, float? z);     // JS space; null stops looking
        void SetShadow(bool on);
        void Update(float dt);
        void Dispose();
        // Set by crowds and story/danger.js: the player hit this person (player x, z, knocked out).
        Action<float, float, bool> OnHit { get; set; }
        bool Hostile { get; set; }           // story/danger.js looter: hits on it are self-defence
    }

    public interface IPeople
    {
        bool Fallback { get; }               // true when the engine runs on capsule stand-ins
        IPerson CreatePerson(string preset = "crowd", int? seed = null);
        ICrowd CreateCrowd(CrowdOptions o);
    }

    // createCrowd({ count, seed, people, quality|kit, heightAt, player })
    public sealed class CrowdOptions
    {
        public int count = 24;
        public int seed = 1;
        public IPeople people;
        public string quality;
        public Engine.Kit kit;
        public Func<float, float, float> heightAt;   // (x, z) -> ground y, JS space
        public Engine.Player player;
    }

    // setMode(mode, opts): modes stroll | brisk | queue | sit | idle | none (engine/crowd.js header).
    public sealed class CrowdModeOptions
    {
        public int? count;
        public List<List<XZ>> paths;
        public RectXZ area;
        public Func<float, float, float> heightAt;
        public Dictionary<string, float> props;      // prop name -> probability
        public bool? instant;
        public float? speed;
        public string then;                          // brisk: 'respawn' | 'despawn' | 'idle'
        public List<XZ> targets;
        public XZ? at, point, dir, leave, face;      // queue
        public float? spacing, serve, standing, y;
        public string carry;
        public List<CrowdSpot> spots;                // sit
    }

    public sealed class CrowdSpot
    {
        public float x, z;
        public float? yaw;
    }

    public interface ICrowd
    {
        GameObject Group { get; }
        void Update(float dt, FrameCtx ctx = null);
        void SetMode(string mode, CrowdModeOptions o = null);
        void SetPaths(List<List<XZ>> paths);
        void SetArea(RectXZ area);
        string Mode { get; }
        int Active { get; }
        IReadOnlyList<IPerson> Members { get; }
        void Dispose();
    }
}
