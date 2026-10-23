// three.js geometries for the ports: built in JS space with three.js parameters and conventions
// (PlaneGeometry in XY facing +z, CylinderGeometry centred on the origin, UV v up), transformed in JS space
// (translate/rotateX/...), then ToMesh() mirrors X for Unity (PORTING.md "Coordinates"). Owned by the engine
// group; signatures are shared by every level, people and fx port.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed class GeoData
    {
        public List<Vector3> pos = new List<Vector3>();      // JS space
        public List<Vector3> normal = new List<Vector3>();   // JS space
        public List<Vector2> uv = new List<Vector2>();
        public List<int> index = new List<int>();            // three.js (counter-clockwise) order
        public List<Color> color;                            // optional vertex colours

        public GeoData Translate(float x, float y, float z) => throw new NotImplementedException("port: BufferGeometry.translate");
        public GeoData RotateX(float a) => throw new NotImplementedException("port: BufferGeometry.rotateX");
        public GeoData RotateY(float a) => throw new NotImplementedException("port: BufferGeometry.rotateY");
        public GeoData RotateZ(float a) => throw new NotImplementedException("port: BufferGeometry.rotateZ");
        public GeoData Scale(float x, float y, float z) => throw new NotImplementedException("port: BufferGeometry.scale");
        // m is a three.js Matrix4 in JS space (column vectors), e.g. from Matrix4x4.TRS on JS values.
        public GeoData ApplyMatrix(Matrix4x4 m) => throw new NotImplementedException("port: BufferGeometry.applyMatrix4");
        public GeoData Clone() => throw new NotImplementedException("port: BufferGeometry.clone");
        // Mirror X and build the Unity mesh (normals/tangents included).
        public Mesh ToMesh(string name = null) => throw new NotImplementedException("port: GeoData -> Mesh");
    }

    public static class Geo
    {
        public static GeoData Box(float w, float h, float d, int ws = 1, int hs = 1, int ds = 1) => throw new NotImplementedException("port: THREE.BoxGeometry");
        public static GeoData Plane(float w, float h, int ws = 1, int hs = 1) => throw new NotImplementedException("port: THREE.PlaneGeometry");
        public static GeoData Cylinder(float rt, float rb, float h, int radialSeg = 32, int heightSeg = 1, bool openEnded = false, float thetaStart = 0, float thetaLength = Mathf.PI * 2) => throw new NotImplementedException("port: THREE.CylinderGeometry");
        public static GeoData Cone(float r, float h, int radialSeg = 32, int heightSeg = 1, bool openEnded = false) => throw new NotImplementedException("port: THREE.ConeGeometry");
        public static GeoData Sphere(float r = 1, int ws = 32, int hs = 16, float phiStart = 0, float phiLength = Mathf.PI * 2, float thetaStart = 0, float thetaLength = Mathf.PI) => throw new NotImplementedException("port: THREE.SphereGeometry");
        public static GeoData Circle(float r = 1, int seg = 32, float thetaStart = 0, float thetaLength = Mathf.PI * 2) => throw new NotImplementedException("port: THREE.CircleGeometry");
        public static GeoData Ring(float inner = 0.5f, float outer = 1, int thetaSeg = 32) => throw new NotImplementedException("port: THREE.RingGeometry");
        public static GeoData Torus(float r = 1, float tube = 0.4f, int radialSeg = 12, int tubularSeg = 48, float arc = Mathf.PI * 2) => throw new NotImplementedException("port: THREE.TorusGeometry");
        public static GeoData Icosahedron(float r = 1, int detail = 0) => throw new NotImplementedException("port: THREE.IcosahedronGeometry");
        public static GeoData Dodecahedron(float r = 1, int detail = 0) => throw new NotImplementedException("port: THREE.DodecahedronGeometry");
        public static GeoData Capsule(float r = 1, float length = 1, int capSeg = 4, int radialSeg = 8) => throw new NotImplementedException("port: THREE.CapsuleGeometry");
        public static GeoData RoundedBox(float w, float h, float d, int seg = 2, float radius = 0.1f) => throw new NotImplementedException("port: RoundedBoxGeometry");
        // points: (radius, y) profile, revolved around Y.
        public static GeoData Lathe(IList<Vector2> points, int seg = 12, float phiStart = 0, float phiLength = Mathf.PI * 2) => throw new NotImplementedException("port: THREE.LatheGeometry");
        // A flat THREE.Shape in the XY plane (outline and optional holes, JS space).
        public static GeoData Shape(IList<Vector2> outline, IList<IList<Vector2>> holes = null) => throw new NotImplementedException("port: THREE.ShapeGeometry");
        public static GeoData Extrude(IList<Vector2> outline, float depth, bool bevel = false, float bevelSize = 0, float bevelThickness = 0, IList<IList<Vector2>> holes = null) => throw new NotImplementedException("port: THREE.ExtrudeGeometry");
        // mergeGeometries (all must carry the same attributes).
        public static GeoData Merge(IList<GeoData> list) => throw new NotImplementedException("port: BufferGeometryUtils.mergeGeometries");

        // new THREE.Mesh(geometry, material): a GameObject with MeshFilter + MeshRenderer, casting/receiving shadows.
        public static GameObject Obj(GeoData g, Material m, string name = null, bool cast = true, bool receive = true) => throw new NotImplementedException("port: new THREE.Mesh");
        public static GameObject Obj(Mesh mesh, Material m, string name = null, bool cast = true, bool receive = true) => throw new NotImplementedException("port: new THREE.Mesh");
    }
}
