// Ports engine/people-body.js: bone layout, body measurements from a spec, rest joints, and the lofted,
// skinned body with clothes, hair and face built into one mesh (JS space; mirror at ToMesh time).
using System;
using System.Collections.Generic;

namespace Gyeongbo.People
{
    public sealed class BodyDims
    {
        public float H, g, hh, ankleY, kneeY, hipY, pelvisY, waistY, chestY, shY, neckY, headY, elY, wrY, shX, hipX, L1, L2, U1, U2;
        public bool f;
    }

    // A loft row of the torso (back offset for props).
    public sealed class BodyRow
    {
        public float y, rx, rz, z, x;
    }

    // buildBody() -> { b, d, J, rows, chestF }
    public sealed class BodyBuild
    {
        public MeshBuilder b;
        public BodyDims d;
        public float[][] J;
        public List<BodyRow> rows = new List<BodyRow>();
        public float chestF;
    }

    public static class PeopleBody
    {
        // Bone indices (people-body.js B).
        public static class B
        {
            public const int root = 0, pelvis = 1, spine = 2, chest = 3, neck = 4, head = 5, lArm = 6, lFore = 7, lHand = 8, rArm = 9, rFore = 10, rHand = 11, lThigh = 12, lShin = 13, lFoot = 14, rThigh = 15, rShin = 16, rFoot = 17;
        }
        public static readonly string[] BONE_NAMES = { "root", "pelvis", "spine", "chest", "neck", "head", "lArm", "lFore", "lHand", "rArm", "rFore", "rHand", "lThigh", "lShin", "lFoot", "rThigh", "rShin", "rFoot" };
        public static readonly int[] PARENT = { -1, 0, 1, 2, 3, 4, 3, 6, 7, 3, 9, 10, 1, 12, 13, 1, 15, 16 };

        public static float Ss(float a, float b, float x) { float t = Math.Min(1f, Math.Max(0f, (x - a) / (b - a))); return t * t * (3 - 2 * t); }
        public static BodyDims Dims(PersonSpec spec) => throw new NotImplementedException("port: engine/people-body.js:dims");
        // Rest joint positions [x, y, z] per bone (root space, JS).
        public static float[][] Joints(BodyDims d) => throw new NotImplementedException("port: engine/people-body.js:joints");
        public static BodyBuild BuildBody(PersonSpec spec) => throw new NotImplementedException("port: engine/people-body.js:buildBody");
    }
}
