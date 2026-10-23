// engine/fx.js rain, dust and extinguisherSpray. three.js runs rain and dust in vertex shaders; this port
// evaluates the same formulas on the CPU each update (streak lines, splash and mote billboards).
// ponytail: CPU-side rain (9000 streaks at high quality); a GPU shader in the quality pass if it shows in profiles.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Systems.SysUtil;

namespace Gyeongbo.Systems
{
    public sealed partial class Fx
    {
        static float Fract(float v) => v - Mathf.Floor(v);
        static float Mod(float x, float y) => x - y * Mathf.Floor(x / y);   // GLSL mod

        // Splash rings (frame 0) and ring + centre drop (frame 1), the fragment shader of the splash points.
        Texture2D SplashTex() => Memo("splash", () => Pixels(128, 2, (u, v, cell) =>
        {
            float qy = (v * 2 - 1) * 2.2f, d = Hypot(u, qy);
            float ring = Smooth(0.55f, 0.85f, d) * (1 - Smooth(0.85f, 1, d)) + (cell == 1 ? 1 - Smooth(0, 0.3f, d) : 0);
            return new Vector4(255, 255, 255, ring < 0.02f ? 0 : Clamp01(ring) * 255);
        }));
        Texture2D MoteTex() => Memo("mote", () => Pixels(32, 1, (u, v, cell) =>
            new Vector4(255, 255, 255, (1 - Smooth(0.2f, 1, Hypot(u, v * 2 - 1))) * 255)));

        // ---- rain: world-anchored streaks wrapped around the camera + splash rings on the ground ----
        public IRain Rain(GameObject scene, Camera camera, RainOptions o = null) => new RainFx(this, scene, camera, o ?? new RainOptions());

        sealed class RainFx : IRain
        {
            const int S = 36, H = 22, R = 16;
            readonly Fx fx;
            readonly Camera camera;
            readonly int n, m;
            readonly float[] seeds, sp;
            readonly Mesh lines;
            readonly Vector3[] lv;
            readonly Color[] lc;
            readonly int[] li;
            readonly FxBillboards splashes;
            readonly Material lineMat, splashMat;
            float uTime, uOpacity;
            Vector3 uCam;
            readonly Vector2 uWind = new Vector2(1.2f, 0.5f);
            const float uSpeed = 9, uLen = 0.045f, uGround = 0;
            Color uColor = new Color(0.7f, 0.74f, 0.8f);
            bool on;
            float shown;
            public GameObject Group { get; }
            public bool On => on;
            public void SetOn(bool v) => on = v;

            public RainFx(Fx fx, GameObject scene, Camera camera, RainOptions o)
            {
                this.fx = fx; this.camera = camera;
                Group = J.Group("fx-rain");
                n = JsRoundI(o.count * fx.Q);
                seeds = new float[n * 3];
                for (int i = 0; i < n * 3; i++) seeds[i] = Rand();
                lv = new Vector3[n * 2]; lc = new Color[n * 2]; li = new int[n * 2];
                for (int i = 0; i < li.Length; i++) li[i] = i;
                lines = new Mesh { name = "fx-rain-lines", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                lines.MarkDynamic();
                lineMat = Mat.Unlit(new MatSpec { transparent = true, depthWrite = false, vertexColors = true, fog = true });
                Group.Add(Geo.Obj(lines, lineMat, "fx-rain-lines", false, false));
                m = JsRoundI(700 * fx.Q);
                sp = new float[m * 4];
                for (int i = 0; i < m * 4; i++) sp[i] = Rand();
                splashMat = Mat.Unlit(new MatSpec { map = fx.SplashTex(), transparent = true, depthWrite = false, vertexColors = true, fog = true });
                splashes = new FxBillboards(m, splashMat, 2, "fx-rain-splashes") { points = true, minDepth = 0.5f };
                Group.Add(splashes.obj);
                if (scene != null) scene.Add(Group);
                Group.SetActive(false);
            }

            public void Update(float dt, Camera cam = null)
            {
                cam = cam != null ? cam : camera;
                dt = Dt(dt);
                bool rm = fx.RM;
                shown += ((on ? 1 : 0) - shown) * Mathf.Min(1, dt * 0.8f);
                Group.SetActive(shown > 0.01f);
                if (!Group.activeSelf) return;
                uTime += dt * (rm ? 0.65f : 1);
                uOpacity = shown;
                if (cam != null) uCam = J.FromUnity(cam.transform.position);
                if (RenderSettings.fog) { var fc = RenderSettings.fogColor.linear; uColor = new Color(fc.r * 0.85f + 0.12f, fc.g * 0.85f + 0.13f, fc.b * 0.85f + 0.15f); }
                Streaks(rm ? n / 2 : n);
                Splashes(rm ? m / 2 : m);
            }

            void Streaks(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float ax = seeds[i * 3], ay = seeds[i * 3 + 1], az = seeds[i * 3 + 2];
                    float spd = uSpeed * (0.85f + 0.3f * Fract(ax * 91.7f));
                    float y = uCam.y - H * 0.35f + Mod(ay * H - uTime * spd - uCam.y + H * 0.35f, H);
                    float x = ax * S + uWind.x * uTime * (spd / uSpeed), z = az * S + uWind.y * uTime * (spd / uSpeed);
                    x = uCam.x + Mod(x - uCam.x + S / 2f, S) - S / 2f; z = uCam.z + Mod(z - uCam.z + S / 2f, S) - S / 2f;
                    float fade = (y >= uGround ? 1 : 0) * (1 - Smooth(S * 0.3f, S / 2f, Hypot(x - uCam.x, z - uCam.z))) * uOpacity * 0.42f;
                    for (int w = 0; w < 2; w++)   // aSeed.w: 0 = the drop, 1 = the tail
                    {
                        lv[i * 2 + w] = J.V(x - uWind.x * uLen * w, y + spd * uLen * w, z - uWind.y * uLen * w);
                        lc[i * 2 + w] = new Color(uColor.r, uColor.g, uColor.b, (1 - w * 0.85f) * fade);
                    }
                }
                lines.Clear(true);
                if (count == 0) return;
                lines.SetVertices(lv, 0, count * 2);
                lines.SetColors(lc, 0, count * 2);
                lines.SetIndices(li, 0, count * 2, MeshTopology.Lines, 0);
            }

            void Splashes(int count)
            {
                bool camOk = uCam.y >= uGround - 3 && uCam.y <= uGround + 40;
                splashes.Begin();
                for (int i = 0; i < count && camOk; i++)
                {
                    float sx = sp[i * 4], sy = sp[i * 4 + 1], sz = sp[i * 4 + 2], sw = sp[i * 4 + 3];
                    float cyc = uTime * (2.2f + sw) + sz * 7, k = Mathf.Floor(cyc), ph = Fract(cyc);
                    float hx = Fract((float)Math.Sin(k * 12.9898 + sx * 78.233) * 43758.5453f), hz = Fract((float)Math.Sin(k * 39.3468 + sy * 11.135) * 43758.5453f);
                    float x = (sx + hx) * R * 2, z = (sy + hz) * R * 2;
                    x = uCam.x + Mod(x - uCam.x + R, R * 2) - R; z = uCam.z + Mod(z - uCam.z + R, R * 2) - R;
                    float a = (1 - ph) * (1 - Smooth(R * 0.6f, R, Hypot(x - uCam.x, z - uCam.z)));
                    splashes.Push(new Vector3(x, uGround + 0.015f, z), 0.05f + ph * 0.16f, 0, ph <= 0.3f ? 1 : 0,
                        uColor.r * 1.2f, uColor.g * 1.2f, uColor.b * 1.2f, a * uOpacity * 0.5f);
                }
                splashes.End();
            }

            public void Dispose()
            {
                if (Group != null) UnityEngine.Object.Destroy(Group);
                splashes.Dispose();
                UnityEngine.Object.Destroy(lines); UnityEngine.Object.Destroy(lineMat); UnityEngine.Object.Destroy(splashMat);
            }
        }

        // ---- dust: slow motes drifting in a box (basements, light shafts) ----
        public IDust Dust(DustOptions o) => new DustFx(this, o ?? new DustOptions());

        sealed class DustFx : IDust
        {
            readonly Fx fx;
            readonly DustOptions o;
            readonly int n;
            readonly Vector3[] pos;
            readonly float[] seed;
            readonly FxBillboards points;
            readonly Color col;
            readonly float y0, yh;
            float uTime;
            bool on = true;
            int drawn;
            public GameObject Group { get; }

            public DustFx(Fx fx, DustOptions o)
            {
                this.fx = fx; this.o = o;
                var bx = o.box ?? new Box3();
                n = JsRoundI(o.count * fx.Q);
                pos = new Vector3[n]; seed = new float[n * 4];
                for (int i = 0; i < n; i++)
                {
                    pos[i] = new Vector3(bx.x0 + Rand() * (bx.x1 - bx.x0), bx.y0 + Rand() * (bx.y1 - bx.y0), bx.z0 + Rand() * (bx.z1 - bx.z0));
                    for (int k = 0; k < 4; k++) seed[i * 4 + k] = Rand();
                }
                uTime = Rand() * 100;
                col = (o.color ?? J.Hex(0xfff1d6)).linear;
                y0 = bx.y0; yh = bx.y1 - bx.y0;
                points = new FxBillboards(n, fx.Memo("moteMat", () => Mat.Unlit(new MatSpec { map = fx.MoteTex(), blending = "additive", transparent = true, depthWrite = false, vertexColors = true })), 1, "fx-dust-motes")
                    { points = true, pxMin = 1, pxMax = 5, minDepth = 0.2f };
                Group = MakeGroup("fx-dust", 0, 0, 0);
                Group.Add(points.obj);
                drawn = n;
                Motes();
            }

            public void SetOn(bool v) { on = v; Group.SetActive(on); }
            public void SetDensity(float v) { drawn = JsRoundI(n * Clamp01(v)); Motes(); }

            public void Update(float dt, Camera camera = null)
            {
                if (!on) return;
                uTime += (float.IsNaN(dt) ? 0 : Mathf.Min(dt, 0.05f)) * ((o.reduceMotion ?? fx.RM) ? 0.3f : 1);
                Motes();
            }

            void Motes()
            {
                points.Begin();
                for (int i = 0; i < drawn; i++)
                {
                    float sx = seed[i * 4], sy = seed[i * 4 + 1], sz = seed[i * 4 + 2], sw = seed[i * 4 + 3];
                    var p = pos[i] + new Vector3(Mathf.Sin(uTime * (0.05f + sx * 0.08f) + sw * 6.28f), 0, Mathf.Cos(uTime * (0.04f + sy * 0.07f) + sz * 6.28f)) * 0.35f;
                    p.y = y0 + Mod(pos[i].y - y0 + uTime * (0.01f + sz * 0.025f), yh);
                    float h = (p.y - y0) / yh;
                    float a = (0.55f + 0.45f * Mathf.Sin(uTime * (0.3f + sy * 0.5f) + sx * 20)) * Smooth(0, 0.08f, h) * (1 - Smooth(0.85f, 1, h));
                    points.Push(p, o.size * (0.6f + sx), 0, 0, col.r, col.g, col.b, a * o.opacity);
                }
                points.End();
            }

            public void Dispose()
            {
                if (Group != null) UnityEngine.Object.Destroy(Group);
                points.Dispose();
            }
        }

        // ---- extinguisher spray: dry-chemical powder jet from the group's origin (the nozzle) toward aim() ----
        // Particles live in world space, so the group can be parented to a hand.
        public ISpray ExtinguisherSpray(SprayOptions o) => new SprayFx(this, o ?? new SprayOptions());

        sealed class Grain { public float x, y, z, vx, vy, vz, age, life, rot, spin, shade, size; public int frame; }

        sealed class SprayFx : ISpray
        {
            readonly Fx fx;
            readonly SprayOptions o;
            readonly int max;
            readonly FxBillboards sys;
            readonly List<Grain> P = new List<Grain>();
            readonly float floorY;
            Vector3 target;
            bool on, aimed;
            float acc;
            public GameObject Group { get; }
            public bool On => on;
            public void SetOn(bool v) => on = v;
            public void Aim(Vector3 jsTarget) { target = jsTarget; aimed = true; }

            public SprayFx(Fx fx, SprayOptions o)
            {
                this.fx = fx; this.o = o;
                Group = MakeGroup("fx-spray", o.x, o.y, o.z);
                max = JsRoundI(o.count * fx.Q);
                sys = fx.Billboards(max, fx.SmokeMat(), 2, Group, "fx-spray-powder");
                sys.world = true; sys.sort = true; sys.lit = true; sys.RenderOrder = 5;
                floorY = o.floorY ?? float.NegativeInfinity;
            }

            public void Update(float dt, Camera camera = null)
            {
                dt = Dt(dt);
                bool rm = fx.RM; float spd = rm ? 0.7f : 1;
                var t = Group.transform;
                Vector3 origin = J.FromUnity(t.position);
                if (!aimed) target = J.FromUnity(t.TransformPoint(J.V(0, 0, -3)));
                Vector3 dir = (target - origin).normalized, up = Vector3.up;
                Vector3 side = Vector3.Cross(dir, up).normalized; if (side.sqrMagnitude == 0) side = new Vector3(1, 0, 0);
                Vector3 up2 = Vector3.Cross(side, dir);
                if (on) acc += (rm ? 70 : 140) * fx.Q * dt;
                while (acc >= 1)
                {
                    acc--;
                    if (P.Count >= max) { acc = 0; break; }
                    float a = Rand() * Mathf.PI * 2, c = Mathf.Sqrt(Rand()) * 0.13f, v = o.speed * (0.75f + Rand() * 0.35f) * spd;
                    Vector3 vel = (dir + (side * Mathf.Cos(a) + up2 * Mathf.Sin(a)) * c) * v;
                    P.Add(new Grain { x = origin.x, y = origin.y, z = origin.z, vx = vel.x, vy = vel.y, vz = vel.z, age = 0, life = 1.3f + Rand() * 0.9f, rot = Rand() * 6.3f, spin = rm ? 0 : (Rand() - 0.5f) * 1.2f, frame = (int)(Rand() * 4), shade = 0.9f + Rand() * 0.1f });
                }
                for (int i = P.Count - 1; i >= 0; i--)
                {
                    var p = P[i]; p.age += dt;
                    if (p.age >= p.life) { P.RemoveAt(i); continue; }
                    float drag = Mathf.Exp(-dt * 2.8f);
                    p.vx *= drag; p.vz *= drag; p.vy = p.vy * drag - 0.6f * dt;
                    p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt;
                    p.size = Mathf.Min(0.05f + p.age * 1.15f, 1.5f);
                    if (p.y < floorY + p.size * 0.3f) { p.y = floorY + p.size * 0.3f; p.vy = 0; }
                    p.rot += p.spin * dt;
                }
                sys.Begin();
                foreach (var p in P)
                {
                    float k = p.age / p.life, a = Smooth(0, 0.04f, p.age) * Mathf.Pow(1 - k, 1.5f) * 0.6f;
                    sys.Push(new Vector3(p.x, p.y, p.z), p.size, p.rot, p.frame, 0.96f * p.shade, 0.96f * p.shade, 0.93f * p.shade, a);
                }
                sys.End();
            }

            public void Dispose()
            {
                if (Group != null) UnityEngine.Object.Destroy(Group);
                sys.Dispose();
            }
        }
    }
}
