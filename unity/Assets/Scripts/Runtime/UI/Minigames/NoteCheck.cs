// Ports notecheck.js: the 3D pieces of the "다시 읽기" scene — scripted camera (서연's eye, a crane view),
// 정민's walked path, labelled pins and links between them. The step order and text live in Story/Seoyeon.
// Positions are JS space.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gyeongbo.UI
{
    public sealed class NoteCheck
    {
        const string AMBER = "#f6bb6e";
        const string TEAL = "#85c7bb";
        static float Ease(float t) => t * t * (3 - 2 * t);
        static string Clip(string text, int max) => text.Length > max ? $"{text.Substring(0, max - 1)}…" : text;

        readonly GameEngine world;
        readonly Spawn start;
        readonly bool reduceMotion;
        readonly GameObject group = J.Group("reread");
        readonly List<Object> owned = new List<Object>();
        readonly List<MgSprite> sprites = new List<MgSprite>();
        readonly List<Func<float, bool>> tickers = new List<Func<float, bool>>();   // returns true when finished
        Action<Camera, float> cameraFn;

        T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        Mesh OwnMesh(GeoData g) => Own(g.ToMesh());
        Material Basic(float? opacity = null, bool doubleSide = false) => Own(Gyeongbo.Engine.Mat.Unlit(new MatSpec
        {
            color = J.Css(AMBER), transparent = opacity.HasValue ? true : (bool?)null, opacity = opacity,
            depthWrite = opacity.HasValue ? false : (bool?)null, side = doubleSide ? "double" : null,
        }));

        NoteCheck(GameEngine world, Spawn start, bool reduceMotion)
        {
            this.world = world; this.start = start; this.reduceMotion = reduceMotion;
            world.Scene.Add(group);
            world.SetCameraOverride((camera, dt) =>
            {
                cameraFn?.Invoke(camera, dt);
                foreach (var tick in tickers.ToArray()) if (tick(dt)) tickers.Remove(tick);
                foreach (var s in sprites) s.Face(camera);   // THREE.Sprite faces the camera on its own
            });
        }

        // start: 서연's position and yaw (JS space).
        public static NoteCheck CreateReread(GameEngine world, Spawn start, bool reduceMotion = false) => new NoteCheck(world, start, reduceMotion);

        // First person at the start, easing the gaze to pitch.
        public void Eye(float pitch)
        {
            float current = -0.35f, yaw = start.yaw ?? 0;
            cameraFn = (camera, dt) =>
            {
                current += (pitch - current) * (reduceMotion ? 1 : 1 - Mathf.Exp(-dt * 2.5f));
                camera.transform.position = J.V(start.x, 1.5f, start.z);
                camera.transform.LookAt(J.V(start.x - Mathf.Sin(yaw) * Mathf.Cos(current), 1.5f + Mathf.Sin(current), start.z - Mathf.Cos(yaw) * Mathf.Cos(current)));
            };
        }

        // Rise above the neighbourhood so both people's starts and all shelter entrances are in frame.
        // Nearly top-down so the buildings never hide entrance C.
        public void Crane(Vector3? to = null, Vector3? target = null)
        {
            Vector3 toU = J.V(to ?? new Vector3(-25, 190, 95)), targetU = J.V(target ?? new Vector3(-25, 0, 12));
            Vector3 from = default, lookFrom = default;
            float t = -1;
            cameraFn = (camera, dt) =>
            {
                var tr = camera.transform;
                if (t < 0) { from = tr.position; lookFrom = from + tr.forward * 8; t = 0; }
                t = reduceMotion ? 1 : Mathf.Min(1, t + dt / 1.6f);
                tr.position = Vector3.Lerp(from, toU, Ease(t));
                tr.LookAt(Vector3.Lerp(lookFrom, targetU, Ease(t)));
            };
        }

        // 준's recorded steps as amber dots, with a translucent figure walking them once.
        public void ShowPath(IList<XZ> points)
        {
            int every = Math.Max(1, (int)Math.Ceiling(points.Count / 120.0));
            var pts = new List<XZ>();
            for (int i = 0; i < points.Count; i++) if (i % every == 0) pts.Add(points[i]);
            if (pts.Count == 0) return;
            // ponytail: one GameObject per dot (≤121) instead of an InstancedMesh; count = how many are active.
            var discMesh = OwnMesh(Geo.Circle(1.1f, 14).RotateX(-Mathf.PI / 2));
            var discMat = Basic(0.85f);
            var discs = new GameObject[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                discs[i] = Geo.Obj(discMesh, discMat, "pathDot", false, false);
                J.SetPos(discs[i], pts[i].x, 0.1f, pts[i].z);
                discs[i].SetActive(reduceMotion);
                group.Add(discs[i]);
            }
            var ghost = Geo.Obj(OwnMesh(Geo.Cylinder(0.6f, 0.6f, 2, 12)), Basic(0.45f), "ghost", false, false);
            group.Add(ghost);
            float t = reduceMotion ? 1 : 0;
            Func<float, bool> tick = dt =>
            {
                t = Mathf.Min(1, t + dt / 2.5f);
                int i = Math.Min(pts.Count - 1, (int)Mathf.Floor(t * (pts.Count - 1)));
                for (int k = 0; k < discs.Length; k++) discs[k].SetActive(k <= i);
                J.SetPos(ghost, pts[i].x, 0.9f, pts[i].z);
                return t >= 1;
            };
            if (!tick(0)) tickers.Add(tick);
        }

        MgSprite Label(int n, string was, string now, float centerX)
        {
            var g = new Paint(1024, 300);
            g.fillStyle = "rgba(16,28,30,.9)";
            g.fillRect(0, 0, 1024, 300);
            g.fillStyle = AMBER;
            g.beginPath();
            g.arc(70, 150, 44, 0, Mathf.PI * 2);
            g.fill();
            g.fillStyle = "#16282a";
            g.font = "700 56px " + Kit.FONT;
            g.textAlign = "center";
            g.textBaseline = "middle";
            g.fillText(n == 0 ? "✕" : n.ToString(), 70, 152);   // Seoyeon passes 0 for JS's '✕' pin
            g.textAlign = "left";
            g.font = "700 46px " + Kit.FONT;
            g.fillStyle = AMBER;
            g.fillText(Clip(was ?? "", 26), 140, 100, 860);
            g.fillStyle = TEAL;
            g.fillText(Clip(now ?? "", 26), 140, 200, 860);
            // ponytail: MgSprite's material keeps fog (JS sprite has fog: false); fine at crane height in clear weather.
            var sprite = new MgSprite(g, centerX, 0.5f, 10);
            sprites.Add(sprite);
            return sprite;
        }

        // A numbered pin at `at` with what the message said (was) and what holds now (now).
        // A pole at the place the reason belongs, topped by "what the note said" over "what is true for 서연".
        public void Pin(int n, Vector3 at, string was, string now, float scale = 2.6f)
        {
            var sprite = Label(n, was, now, at.x < -95 ? 0.85f : 0.5f);   // C's label grows west so it clears A's
            J.SetPos(sprite.go, at.x, 6.5f * scale, at.z);
            sprite.Scale(13 * scale, 3.8f * scale);
            var pole = Geo.Obj(OwnMesh(Geo.Cylinder(0.08f, 0.08f, 6 * scale, 6)), Basic(), "pole", false, false);
            J.SetPos(pole, at.x, 3 * scale, at.z);
            group.Add(sprite.go, pole);
        }

        // A flat amber strip on the ground between two places (e.g. the two starting points).
        public void Link(Vector3 a, Vector3 b)
        {
            float length = Mathf.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
            var strip = Geo.Obj(OwnMesh(Geo.Plane(0.35f, length)), Basic(0.7f, true), "link", false, false);
            J.SetRot(strip, -Mathf.PI / 2, 0, Mathf.Atan2(b.x - a.x, b.z - a.z));
            J.SetPos(strip, (a.x + b.x) / 2, 0.12f, (a.z + b.z) / 2);
            group.Add(strip);
        }

        public void Dispose()
        {
            world.SetCameraOverride(null);
            tickers.Clear();
            Object.Destroy(group);
            foreach (var s in sprites) s.Dispose();
            sprites.Clear();
            foreach (var o in owned) Object.Destroy(o);
            owned.Clear();
        }
    }
}
