// Ports engine/people.js: procedural, skinned, clothed adults and elders with blended procedural animation
// (walk/brisk/run blend space, one-shot clips, head look, arm IK on walker/cart, hanging props).
// One SkinnedMeshRenderer per person; each visible prop adds one renderer. Feet at the origin facing JS -z;
// 'sit' / 'sitChair' put the hips there. ProceduralPeople is the IPeople the engine uses; Person is one IPerson.
// The skeleton, props, foot planting and arm IK are in PersonRig.cs.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.People.PeopleAnim;
using static Gyeongbo.People.PeopleBody;

namespace Gyeongbo.People
{
    public sealed class ProceduralPeople : IPeople
    {
        public bool Fallback => false;
        public IPerson CreatePerson(string preset = "crowd", int? seed = null) => Person.Create(preset, seed);
        public ICrowd CreateCrowd(CrowdOptions o) => Crowd.Create(o);
    }

    public sealed partial class Person : IPerson
    {
        public static readonly string[] ANIMS = PeopleAnim.ANIMS;
        public static readonly string[] PROPS = { "backpack", "waterJug", "walker", "flashlight", "phone", "baton", "basket", "cart" };
        static readonly Dictionary<string, string> AUTO = new Dictionary<string, string> { ["phone"] = "phone", ["carry"] = "waterJug", ["walker"] = "walker" }; // animations that need a prop shown
        const float FADE = 0.2f;
        static readonly HashSet<string> STAIRS = new HashSet<string> { "stairsUp", "stairsDown" };
        // Walk, brisk walk and run are one blend space: at a given speed the two nearest gaits are posed on one shared
        // phase and mixed, so a person speeding up goes from a stroll through a march into a run with no pop.
        static readonly string[] LOCO_N = { "walk", "brisk", "run" };
        static readonly float[] LOCO_V = { 1.3f, 2.1f, 3.9f };
        static (string, string, float) LocoMix(float v)
        {
            if (v <= LOCO_V[0]) return (LOCO_N[0], LOCO_N[0], 0);
            for (int i = 1; i < LOCO_V.Length; i++) if (v <= LOCO_V[i]) { float k = (v - LOCO_V[i - 1]) / (LOCO_V[i] - LOCO_V[i - 1]); return (LOCO_N[i - 1], LOCO_N[i], k * k * (3 - 2 * k)); }
            var n = LOCO_N[LOCO_N.Length - 1]; return (n, n, 0);
        }
        static float Clamp(float x, float a, float b) => Math.Min(b, Math.Max(a, x));
        static int autoSeed = 1;

        sealed class Act { public string name; public float t, dur, power; }

        readonly string preset;
        internal readonly PersonSpec Spec;
        readonly BodyDims d;
        readonly float[] P = new float[PLEN], Pp = new float[PLEN], Pq = new float[PLEN], Pc = new float[PLEN];
        readonly PoseCtx c = new PoseCtx();
        Act act; // the one-shot clip playing
        float leanF, leanS, leanTf, leanTs, aim, hold; bool aimOn, holdOn;
        readonly float o;
        string cur = "idle", prev = "idle", lastGrip;
        float fade = 1, t, ph, amp, ikW, lookYaw, wheelA, lookX, lookZ; float? speed; bool lookOn, shadow = true;
        readonly Dictionary<string, bool> want = new Dictionary<string, bool>();
        float seat = 0.45f;

        // preset: jeongmin | seoyeon | manager | clerk | guide | coworker | elder | crowd. seed null = next auto seed.
        public static Person Create(string preset = "crowd", int? seed = null) => new Person(preset, seed);

        public GameObject Group => group;
        public Renderer Mesh => smr;
        public Transform Bone(string name) => name != null && boneByName.TryGetValue(name, out var b) ? b : null;
        public float Height => d.H;
        public string Preset => preset;
        public float Seat { get => seat; set => seat = value; }
        public string Anim => cur;

        public void SetAnim(string name, float? speed = null, bool instant = false)
        {
            if (Array.IndexOf(ANIMS, name) < 0) { Debug.LogError($"people: unknown animation \"{name}\" for {preset}"); return; }
            if (speed.HasValue) this.speed = speed; // shortcut: null means "not given" (JS undefined); SetSpeed(null) clears it
            if (name == cur) return;
            prev = cur; cur = name; fade = instant ? 1 : 0;
            if (instant) prev = name;
        }

        public void SetSpeed(float? mps) { speed = mps == null ? (float?)null : Math.Max(0, float.IsNaN(mps.Value) ? 0 : mps.Value); }

        // Play a one-shot clip (punch, throw, pickup, vault, climb, land, hurt) over the current animation.
        // seconds overrides its length; power (0..1) scales clips that have a strength (land).
        public bool Play(string name, float? seconds = null, float power = 0.5f)
        {
            var info = PeopleClips.Info(name); if (info == null) { Debug.LogError($"people: unknown clip \"{name}\" for {preset}"); return false; }
            act = new Act { name = name, t = 0, dur = Math.Max(0.1f, seconds ?? info.dur), power = power }; return true;
        }
        public string Playing => act?.name;
        // Gait phase (a heel strikes each time it passes pi/2 + n*pi) and how much the legs are moving (0..~1.25).
        public float Phase => ph;
        public float Stride => amp;
        public float? ClipK => act != null ? act.t / act.dur : (float?)null; // progress of the playing clip, 0..1
        public float ClipLook => act != null ? PeopleClips.ClipLook(act.name, act.t / act.dur, act.power) : 0; // eye pitch the clip asks for
        // Lean forward (speeding up, +) and sideways into a turn (+ = top toward the left), radians-ish, eased.
        public void SetLean(float forward = 0, float side = 0) { leanTf = Clamp(forward, -0.4f, 0.4f); leanTs = Clamp(side, -0.4f, 0.4f); }
        // Look up (+) or down (-) by pitch radians, spread over spine, chest, neck and head; null to stop.
        public void SetAim(float? pitch) { aimOn = pitch != null; aim = aimOn ? Clamp(pitch.Value, -1.2f, 1.1f) : 0; }
        public void SetHold(bool on) { holdOn = on; }
        public void SetProp(string name, bool on = true)
        {
            if (Array.IndexOf(PROPS, name) < 0) { Debug.LogError($"people: unknown prop \"{name}\" for {preset}"); return; }
            want[name] = on; SyncProps();
        }
        public bool HasProp(string name) => name != null && want.TryGetValue(name, out var on) && on;
        public void LookAt(float? x, float? z) { if (x == null || z == null) { lookOn = false; return; } lookOn = true; lookX = x.Value; lookZ = z.Value; }
        public void SetShadow(bool on)
        {
            shadow = on; smr.shadowCastingMode = Cast(shadow);
            foreach (var pr in propRenderers) pr.r.shadowCastingMode = Cast(shadow && pr.cast);
        }
        public void Dispose()
        {
            foreach (var m in ownMeshes) UnityEngine.Object.Destroy(m); // the body (and a per-person backpack prop); shared prop meshes stay
            UnityEngine.Object.Destroy(group); // JS removeFromParent: out of the scene for good
        }
        public Action<float, float, bool> OnHit { get; set; }
        public bool Hostile { get; set; }

        bool Visible(string n) => objs.TryGetValue(n, out var ob) && ob.activeSelf;
        // with the rollator out, plain walking becomes walker walking
        string Eff(string n) => Visible("walker") && (n == "walk" || n == "brisk" || n == "run") ? "walker" : n;

        public void Update(float dt)
        {
            dt = Math.Min(dt, 0.1f);
            t += dt;
            string bas = Eff(cur); bool loco = Array.IndexOf(LOCO_N, bas) >= 0 && speed != null;
            var (la, lb, lw) = loco ? LocoMix(speed.Value) : (bas, bas, 0f);
            var sa = GaitStep(la, speed, d.H); var sb = lw > 0 ? GaitStep(lb, speed, d.H) : sa;
            ph = (ph + (sa.rate + (sb.rate - sa.rate) * lw) * dt) % (Mathf.PI * 200);
            amp += (sa.amp + (sb.amp - sa.amp) * lw - amp) * Math.Min(1, dt * 6);
            if (fade < 1) fade = Math.Min(1, fade + dt / FADE);
            c.t = t; c.ph = ph; c.amp = amp; c.d = d; c.o = o; c.seat = seat != 0 && !float.IsNaN(seat) ? seat : 0.45f; c.stoop = Spec.stoop; c.power = 0.5f;
            Pose(la, P, c);
            if (lw > 0) { Pose(lb, Pq, c); for (int i = 0; i < PLEN; i++) P[i] += (Pq[i] - P[i]) * lw; }
            if (fade < 1)
            {
                Pose(Eff(prev), Pp, c);
                float w = fade * fade * (3 - 2 * fade);
                for (int i = 0; i < PLEN; i++) P[i] = Pp[i] + (P[i] - Pp[i]) * w;
            }
            // Lean into speeding up / turning (SetLean), the head kept level the way people steady their gaze.
            float lk = Math.Min(1, dt * 6); leanF += (leanTf - leanF) * lk; leanS += (leanTs - leanS) * lk;
            if (leanF != 0 || leanS != 0)
            {
                P[B.spine * 3] -= leanF * 0.55f; P[B.chest * 3] -= leanF * 0.25f; P[B.head * 3] += leanF * 0.5f;
                P[B.spine * 3 + 2] += leanS * 0.45f; P[B.chest * 3 + 2] += leanS * 0.25f; P[B.head * 3 + 2] -= leanS * 0.55f;
            }
            // Where the eyes look up or down (SetAim) bends the spine, chest, neck and head a share each.
            if (aimOn) { P[B.spine * 3] += aim * 0.12f; P[B.chest * 3] += aim * 0.22f; P[B.neck * 3] += aim * 0.18f; P[B.head * 3] += aim * 0.3f; }
            // Something held ready to throw (SetHold): the right hand up in front of the chest.
            hold += ((holdOn ? 1 : 0) - hold) * Math.Min(1, dt * 8);
            if (hold > 0.01f)
            {
                float h = hold; int r = B.rArm * 3, f = B.rFore * 3;
                P[r] += (1.1f - P[r]) * h; P[r + 1] *= 1 - h; P[r + 2] += (0.12f - P[r + 2]) * h;
                P[f] += (1.2f - P[f]) * h; P[f + 1] += (-0.6f - P[f + 1]) * h; P[B.rHand * 3] += (0.15f - P[B.rHand * 3]) * h;
            }
            // One-shot clip over the result: eased in over 60 ms and out over the last 120 ms.
            if (act != null)
            {
                act.t += dt;
                float k = act.t / act.dur;
                if (k >= 1) act = null;
                else
                {
                    Array.Copy(P, Pc, PLEN); c.power = act.power; PeopleClips.Clip(act.name, Pc, c, k);
                    float w = Math.Min(1, Math.Min(act.t / 0.06f, (act.dur - act.t) / 0.12f)); var m = PeopleClips.MaskOf(act.name);
                    if (m == null) for (int i = 0; i < PLEN; i++) P[i] += (Pc[i] - P[i]) * w;
                    else foreach (int b in m) for (int j = b * 3; j < b * 3 + 3; j++) P[j] += (Pc[j] - P[j]) * w;
                }
            }
            if (lookOn) // turn head, neck and a little chest toward a world point
            {
                var v = J.FromUnity(group.transform.InverseTransformPoint(J.V(lookX, 0, lookZ)));
                float yaw = Clamp(Mathf.Atan2(-v.x, -v.z), -1.25f, 1.25f);
                lookYaw += (yaw - lookYaw) * Math.Min(1, dt * 4);
            }
            else lookYaw += (0 - lookYaw) * Math.Min(1, dt * 3);
            P[B.chest * 3 + 1] += lookYaw * 0.25f; P[B.neck * 3 + 1] += lookYaw * 0.3f; P[B.head * 3 + 1] += lookYaw * 0.45f;
            SyncProps();
            if (objs.TryGetValue("walker", out var walker)) // parked beside a seated person, rolling otherwise
            {
                bool parked = cur == "sit" || cur == "sitChair";
                J.SetPos(walker, parked ? 0.62f : 0, 0, parked ? -0.1f : 0); J.SetYaw(walker, parked ? Mathf.PI / 2 : 0);
                float v = GAIT.TryGetValue(Eff(cur), out var g) ? (speed ?? g.v) : 0;
                wheelA -= v * dt / PeopleProps.WALKER_R;
                foreach (var wb in wheels) wb.localRotation = J.Euler(wheelA, 0, 0);
            }
            ApplyPose(P);
            if (!STAIRS.Contains(cur) && !(fade < 1 && STAIRS.Contains(prev))) PlantFeet(); // on stairs the ground is not the plane y = 0
            string mode = GripOf(Eff(cur), Visible("walker"), Visible("cart"));
            ikW += ((mode != null ? 1 : 0) - ikW) * Math.Min(1, dt / FADE * 1.5f);
            bool needWorld = ikW > 0.01f; foreach (var hg in hangers) if (hg.activeSelf) needWorld = true;
            if (needWorld) gq = group.transform.rotation;
            if (mode != null) lastGrip = mode;
            if (ikW > 0.01f && lastGrip != null && !(objs.TryGetValue(lastGrip, out var gripObj) && !gripObj.activeSelf))
                foreach (int s in new[] { -1, 1 }) { GripTargets(lastGrip, s, out var tg, out var pl); SolveArm(s, tg, pl, ikW); }
            foreach (var hg in hangers) // keep jugs and baskets hanging plumb, with a little swing
            {
                if (!hg.activeSelf) continue;
                var pq = hg.transform.parent.rotation;
                hg.transform.localRotation = Quaternion.Inverse(pq) * gq * J.Euler(0.1f * amp * Mathf.Sin(ph - 0.6f) + 0.02f * Mathf.Sin(t * 1.3f), 0, 0.03f * Mathf.Sin(t * 0.9f + o));
            }
        }
    }
}
