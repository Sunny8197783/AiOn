// Ports the rig half of engine/people.js createPerson: the skeleton and skinned body, props (hand, hanging,
// body and root props, the skinned walker), foot planting on the ground plane and two-bone arm IK. Bones are
// Unity Transforms built from the JS rest joints with J.V; poses go on with J.Euler (the X mirror lives there),
// and the world-space IK runs on Unity vectors (mirroring both sides of every step keeps the JS solution).
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.Rendering;
using static Gyeongbo.People.PeopleAnim;
using static Gyeongbo.People.PeopleBody;

namespace Gyeongbo.People
{
    public sealed partial class Person
    {
        GameObject group;
        SkinnedMeshRenderer smr;
        Mesh bodyMesh;
        readonly Material mat;
        Transform[] bones;
        readonly Dictionary<string, Transform> boneByName = new Dictionary<string, Transform>();
        readonly float[][] Jt;
        readonly Dictionary<string, GameObject> objs = new Dictionary<string, GameObject>();
        readonly List<GameObject> hangers = new List<GameObject>();
        readonly List<(Renderer r, bool cast)> propRenderers = new List<(Renderer r, bool cast)>();
        readonly List<Mesh> ownMeshes = new List<Mesh>();
        Transform[] wheels = new Transform[0];
        GameObject walkerObj;
        float walkerHy;
        Quaternion gq = Quaternion.identity;
        readonly float k;
        readonly Func<float, float> front, back;
        readonly float zt, zh, rt, rh;

        static ShadowCastingMode Cast(bool on) => on ? ShadowCastingMode.On : ShadowCastingMode.Off;

        Person(string preset, int? seed)
        {
            this.preset = preset;
            Spec = PeopleOutfit.PresetSpec(preset, seed ?? autoSeed++);
            var bb = BuildBody(Spec); d = bb.d; Jt = bb.J; var rows = bb.rows;
            back = (y) => { var r = rows.Aggregate((a, q) => Math.Abs(q.y - y) < Math.Abs(a.y - y) ? q : a); return r.z.Value + r.rz.Value + (Spec.outer != null ? 0.012f * d.H : 0.004f * d.H); };
            front = (y) => bb.chestF(y, null);
            if (Spec.bake?.backpack != null) PeopleProps.Backpack(bb.b, new BagOpts { d = d, front = front, back = back, c = Spec.bake.backpack, skin = B.chest });
            if (Spec.bake?.crossbody != null) PeopleProps.Crossbody(bb.b, new BagOpts { d = d, front = front, back = back, c = Spec.bake.crossbody, skinChest = B.chest, skinPelvis = B.pelvis });
            mat = PeopleGeo.VpbrMaterial();

            // skeleton in rest pose (identity local rotations)
            string name = $"person:{preset}";
            group = J.Group(name);
            var meshGo = J.Group(name, group);
            bones = new Transform[NB];
            for (int i = 0; i < NB; i++) { bones[i] = new GameObject(BONE_NAMES[i]).transform; boneByName[BONE_NAMES[i]] = bones[i]; }
            for (int i = 0; i < NB; i++)
            {
                int p = PARENT[i]; float[] q = p >= 0 ? Jt[p] : new float[] { 0, 0, 0 };
                bones[i].SetParent(p >= 0 ? bones[p] : meshGo.transform, false);
                bones[i].localPosition = J.V(Jt[i][0] - q[0], Jt[i][1] - q[1], Jt[i][2] - q[2]);
            }
            bodyMesh = bb.b.Build(name); ownMeshes.Add(bodyMesh);
            bodyMesh.bindposes = bones.Select(b => b.worldToLocalMatrix * meshGo.transform.localToWorldMatrix).ToArray();
            smr = meshGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = bodyMesh; smr.bones = bones; smr.rootBone = bones[0]; smr.sharedMaterial = mat;
            smr.localBounds = new Bounds(J.V(0, d.H * 0.5f, -0.15f), Vector3.one * (d.H * 0.85f * 2)); // covers sit/crouch/reach
            smr.shadowCastingMode = ShadowCastingMode.On; smr.receiveShadows = true;

            // ---- state ----
            o = (float)(((seed ?? 7) * 0.618034) % 1);
            t = o * 50; ph = o * 6.28f;
            foreach (var n in PROPS) want[n] = Spec.props != null && Spec.props.Contains(n);
            k = (d.H / 1.72f) * (d.f ? 0.86f : 1);
            // Ground contact: toe and heel sole points relative to the ankle.
            float shoeL = 0.155f * d.H * (d.f ? 0.97f : 1); zt = -0.74f * shoeL; zh = 0.2f * shoeL;
            rt = new Vector2(d.ankleY, zt).magnitude; rh = new Vector2(d.ankleY, zh).magnitude;
            SyncProps();
            Update(0);
        }

        Transform HandHolder(int side) => bones[side < 0 ? B.lHand : B.rHand]; // prop holder in hand space

        GameObject AddMesh(string name, Mesh geo, Transform parent, bool cast)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = geo;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = Cast(cast && shadow); mr.receiveShadows = false;
            propRenderers.Add((mr, cast));
            return go;
        }

        void MakeProp(string name)
        {
            GameObject obj = null;
            int side = name == "basket" || (name == "flashlight" && want["baton"]) ? -1 : 1; // flashlight goes left when a baton is in the right hand
            if (name == "phone") obj = AddMesh(name, PeopleProps.PhoneGeo(side, k), HandHolder(side), false);
            else if (name == "baton") obj = AddMesh(name, PeopleProps.BatonGeo(side, k), HandHolder(side), false);
            else if (name == "flashlight") obj = AddMesh(name, PeopleProps.FlashlightGeo(side, k), HandHolder(side), false);
            else if (name == "waterJug" || name == "basket")
            {
                obj = new GameObject(name); obj.transform.SetParent(HandHolder(side), false); obj.transform.localPosition = J.V(-side * 0.012f * k, -0.072f * k, 0);
                AddMesh(name, name == "basket" ? PeopleProps.BasketGeo() : PeopleProps.WaterJugGeo(), obj.transform, true);
                hangers.Add(obj);
            }
            else if (name == "backpack")
            {
                var bb = new MeshBuilder(false); float[] cj = Jt[B.chest];
                PeopleProps.Backpack(bb, new BagOpts { d = d, front = front, back = back, c = "#2b3242", m = Matrix4x4.Translate(new Vector3(-cj[0], -cj[1], -cj[2])) });
                var mesh = bb.Build(name); ownMeshes.Add(mesh);
                obj = AddMesh(name, mesh, bones[B.chest], true);
            }
            else if (name == "cart") obj = AddMesh(name, PeopleProps.CartGeo(), group.transform, true);
            else if (name == "walker")
            {
                float hy = d.wrY - 0.01f;
                obj = J.Group(name, group);
                var wb = new Transform[1 + PeopleProps.WALKER_WHEELS.Length];
                wb[0] = J.Group("frame", obj).transform;
                for (int i = 0; i < PeopleProps.WALKER_WHEELS.Length; i++)
                {
                    var p = PeopleProps.WALKER_WHEELS[i];
                    wb[i + 1] = new GameObject("wheel" + i).transform; wb[i + 1].SetParent(wb[0], false); wb[i + 1].localPosition = J.V(p[0], p[1], p[2]);
                }
                var wr = obj.AddComponent<SkinnedMeshRenderer>();
                wr.sharedMesh = PeopleProps.WalkerGeo(hy); wr.bones = wb; wr.rootBone = wb[0]; wr.sharedMaterial = mat;
                wr.localBounds = new Bounds(J.V(0, 0.45f, -0.55f), Vector3.one * 1.5f);
                wr.shadowCastingMode = Cast(shadow); wr.receiveShadows = false;
                propRenderers.Add((wr, true));
                wheels = wb.Skip(1).ToArray(); walkerObj = obj; walkerHy = hy;
            }
            objs[name] = obj;
        }

        void SyncProps()
        {
            foreach (var n in PROPS)
            {
                bool on = want[n] || (AUTO.TryGetValue(cur, out var a) && a == n) || (fade < 1 && AUTO.TryGetValue(prev, out var b) && b == n);
                if (on && !objs.ContainsKey(n)) MakeProp(n);
                if (objs.TryGetValue(n, out var ob) && ob.activeSelf != on) ob.SetActive(on);
            }
        }

        void ApplyPose(float[] Q)
        {
            for (int i = 1; i < NB; i++) bones[i].localRotation = J.Euler(Q[i * 3], Q[i * 3 + 1], Q[i * 3 + 2]);
            float[] jp = Jt[B.pelvis];
            bones[B.pelvis].localPosition = J.V(jp[0] + Q[OFF], jp[1] + Q[OFF + 1], jp[2] + Q[OFF + 2]);
        }

        static float AsinC(float x) => Mathf.Asin(Clamp(x, -1, 1));
        static Matrix4x4 Local(Transform b) => Matrix4x4.TRS(b.localPosition, b.localRotation, Vector3.one);

        // Ground contact for the procedural gaits (ground = plane y = 0 under the person): lift the pelvis when the
        // lower ankle is below its standing height (never more, so no foot floats), then pitch each foot within the
        // range that keeps both its toe and heel sole points at or above the ground. y is the same in both spaces.
        void PlantFeet()
        {
            int[] feet = { B.lFoot, B.rFoot };
            var FM = new Matrix4x4[2];
            for (int k = 0; k < 2; k++)
            {
                int f = feet[k]; FM[k] = Matrix4x4.identity;
                foreach (int i in new[] { B.root, B.pelvis, f - 2, f - 1, f }) FM[k] *= Local(bones[i]);
            }
            float lift = Math.Max(0, d.ankleY - Math.Min(FM[0].m13, FM[1].m13));
            if (lift > 0) { bones[B.pelvis].localPosition += new Vector3(0, lift, 0); FM[0].m13 += lift; FM[1].m13 += lift; }
            for (int k = 0; k < 2; k++)
            {
                int f = feet[k]; var M = FM[k]; float ya = M.m13;
                float yt = M.MultiplyPoint3x4(new Vector3(0, -d.ankleY, zt)).y, yh = M.MultiplyPoint3x4(new Vector3(0, -d.ankleY, zh)).y;
                float lo = AsinC((ya - yt) / rt) - AsinC(ya / rt); // toe-up rotation that puts the toe on the ground
                float hi = AsinC(ya / rh) - AsinC((ya - yh) / rh); // ... and the heel
                P[f * 3] += lo <= hi ? Clamp(0, lo, hi) : (lo + hi) / 2;
                bones[f].localRotation = J.Euler(P[f * 3], P[f * 3 + 1], P[f * 3 + 2]);
            }
        }

        static Vector3 Norm(Vector3 v) { float l = v.magnitude; return l > 0 ? v / l : v; } // three.js normalize

        // Two-bone arm IK in world space: wrist to target, elbow toward pole (world direction), weight w.
        void SolveArm(int side, Vector3 target, Vector3 pole, float w)
        {
            int a = side < 0 ? B.lArm : B.rArm; Transform up = bones[a], fo = bones[a + 1]; float L1 = d.U1, L2 = d.U2;
            Vector3 S = up.position, D = target - S;
            float dist = Clamp(D.magnitude, Math.Abs(L1 - L2) + 1e-3f, L1 + L2 - 1e-3f);
            D = Norm(D);
            float A = Mathf.Acos(Clamp((L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist), -1, 1));
            float E = Mathf.PI - Mathf.Acos(Clamp((L1 * L1 + L2 * L2 - dist * dist) / (2 * L1 * L2), -1, 1));
            Vector3 N = Norm(pole - D * Vector3.Dot(pole, D));
            Vector3 U = D * Mathf.Cos(A) + N * Mathf.Sin(A);
            Vector3 Y = -U, Z = Norm(N - U * Vector3.Dot(N, U));
            var q = Quaternion.LookRotation(Z, Y); // basis (Y x Z, Y, Z), as makeBasis(X, Y, Z) with X = Y x Z
            up.localRotation = Quaternion.Slerp(up.localRotation, Quaternion.Inverse(up.parent.rotation) * q, w);
            fo.localRotation = Quaternion.Slerp(fo.localRotation, J.Euler(E, 0, 0), w);
        }

        // Wrist target (world) + elbow pole (world direction).
        void GripTargets(string mode, int s, out Vector3 target, out Vector3 pole)
        {
            if (mode == "walker") target = walkerObj.transform.TransformPoint(J.V(s * 0.26f, walkerHy + 0.045f, -0.255f));
            else if (mode == "cart") target = group.transform.TransformPoint(J.V(s * 0.2f, 1.045f, -0.35f));
            else target = bones[s < 0 ? B.lShin : B.rShin].position + gq * J.V(s * 0.01f, 0.07f, 0.03f);
            pole = gq * J.V(s * (mode == "knees" ? 0.7f : 0.45f), -0.35f, 0.75f).normalized;
        }
    }
}
