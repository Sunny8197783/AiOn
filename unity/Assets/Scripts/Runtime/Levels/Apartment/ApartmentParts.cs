// Ports levels/apartment-parts.js: floor heights, the apartment builder B (walk/solid/point registries,
// powered and backup light lists, sensors, auto doors, ticks, shared materials, walls with holes, windows,
// doors, rods, plates, canvas textures, exit signs) and the exit-sign runner pictogram.
// FL is shared with Systems/Cutscenes (the 302호 floor height).
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    // A hole in a wall: span a..b along the wall, y0..y1.
    public sealed class WallHole
    {
        public float a, b, y0, y1;
        public bool door;
    }

    public sealed class WallOpts
    {
        public float t = 0.1f;
        public int side = 1;
        public List<WallHole> holes = new List<WallHole>();
        public bool solid = true;
    }

    // An animated door leaf: k 0..1 open; set(k) moves it; auto = open radius for auto doors.
    public sealed class AptDoor
    {
        public float x, y, z, k, auto;
        public GameObject pivot;
        public Solid solid;
        public Action<float> set;
    }

    // { obj, base }: a light or emissive material switched with power.
    public sealed class Powered
    {
        public UnityEngine.Object obj;
        public float @base;
    }

    public sealed class Sensor
    {
        public float x, y, z, hold;
        public string kind;
    }

    public sealed class AptBuilder
    {
        public Kit kit;
        public List<WalkZone> walk = new List<WalkZone>();
        public List<Solid> solids = new List<Solid>();
        public List<Point> points = new List<Point>();
        public Dictionary<string, Spawn> spawns = new Dictionary<string, Spawn>();
        public Dictionary<string, Anchor> anchors = new Dictionary<string, Anchor>();
        public List<Powered> powered = new List<Powered>(), backup = new List<Powered>();
        public List<Sensor> sensors = new List<Sensor>();
        public List<AptDoor> doors = new List<AptDoor>();
        public List<Action<float, float, Dictionary<string, object>>> ticks = new List<Action<float, float, Dictionary<string, object>>>();
        public List<Dictionary<string, object>> fixtures = new List<Dictionary<string, object>>();
        public Dictionary<string, Material> M = new Dictionary<string, Material>();
        public Material emLampMat, redLamp, lobbyLamp, lockLed;

        public T OnPower<T>(T obj, float? @base = null) where T : UnityEngine.Object => throw new NotImplementedException("port: levels/apartment-parts.js:B.onPower");
        public T OnBackup<T>(T obj, float? @base = null) where T : UnityEngine.Object => throw new NotImplementedException("port: levels/apartment-parts.js:B.onBackup");
        public Solid Solid(float x0, float x1, float z0, float z1, float y0, float y1, string id = null) => throw new NotImplementedException("port: levels/apartment-parts.js:B.solid");
        public void Floor(float x0, float x1, float z0, float z1, float y) => throw new NotImplementedException("port: levels/apartment-parts.js:B.floor");
        public void Ramp(float x0, float x1, float z0, float z1, string axis, float y0, float y1) => throw new NotImplementedException("port: levels/apartment-parts.js:B.ramp");
        public Point Point(string id, string label, float x, float y, float z, string kind, float? r = null) => throw new NotImplementedException("port: levels/apartment-parts.js:B.point");
        public Material GlowMat(string hex, float intensity = 2, MatSpec extra = null) => throw new NotImplementedException("port: levels/apartment-parts.js:B.glowMat");
        public void WallZ(GameObject g, float z, float x0, float x1, float y0, float y1, Material mat, WallOpts o = null) => throw new NotImplementedException("port: levels/apartment-parts.js:B.wallZ");
        public void WallX(GameObject g, float x, float z0, float z1, float y0, float y1, Material mat, WallOpts o = null) => throw new NotImplementedException("port: levels/apartment-parts.js:B.wallX");
        public void Window(GameObject g, string axis, float c, float a, float b, float y0, float y1, Material mat = null, Material glass = null, int split = 2, float depth = 0.07f, float f = 0.05f) => throw new NotImplementedException("port: levels/apartment-parts.js:B.window");
        public AptDoor Door(GameObject g, string axis, float c, float a, float b, float y, float h, Material mat, string hinge = "a", int dir = 1, float t = 0.05f, float open = 0, bool solid = true, bool @lock = false, Material frameMat = null, float maxAngle = 1.5f) => throw new NotImplementedException("port: levels/apartment-parts.js:B.door");
        public AptDoor AutoDoor(AptDoor d, float r = 1.6f) => throw new NotImplementedException("port: levels/apartment-parts.js:B.autoDoor");
        public GameObject Rod(GameObject g, Vector3 a, Vector3 b, float r, Material mat, int seg = 8) => throw new NotImplementedException("port: levels/apartment-parts.js:B.rod");
        public GameObject Plate(GameObject g, IList<string> lines, float x, float y, float z, float ry = 0, float w = 0.3f, float h = 0.15f, SignStyle style = null) => throw new NotImplementedException("port: levels/apartment-parts.js:B.plate");
        public Texture2D Tex(string key, int w, int h, Action<Paint, int, int> draw) => throw new NotImplementedException("port: levels/apartment-parts.js:B.tex");
        public GameObject ExitSign(GameObject g, float x, float y, float z, float ry, string kind = "exit", string arrow = "right") => throw new NotImplementedException("port: levels/apartment-parts.js:B.exitSign");
    }

    public static class ApartmentParts
    {
        public const float FH = 2.8f, F3 = 5.6f, FL = 5.75f;   // floor-to-floor, 3rd floor, 302호 main floor (현관 step 0.15)

        public static AptBuilder CreateBuilder(Kit kit) => throw new NotImplementedException("port: levels/apartment-parts.js:createBuilder");
        // The running-figure pictogram of exit signs, drawn at (x, y) with size s.
        public static void Runner(Paint g, float x, float y, float s, string color, bool flip = false) => throw new NotImplementedException("port: levels/apartment-parts.js:runner");
    }
}
