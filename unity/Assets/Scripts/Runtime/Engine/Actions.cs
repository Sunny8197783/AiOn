// Ports engine/actions.js: the body in the world — punches (click/Q), picking up and throwing loose things
// (G), crouching (C), thrown debris, people hitting back.
// Loose things (bricks, bottles, cans...) are scattered on each level's walk zones the first time it is
// entered; thrown ones fly with gravity, bounce off solids and floors and knock people back.
// People are engine.Persons (every CreatePerson); a crowd member answers p.OnHit (flee / knocked down),
// anyone else is pushed back and may hit back. Item state is kept in JS space (level-local, the level root
// sits at the origin) and written to the Transform through J.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed class ActionCallbacks
    {
        public Action<IPerson, string, bool> onHit;      // (person, how: 'punch' | 'throw' | 'debris', knocked out)
        public Action<float, string> onHurt;             // (damage, why: 'fight' | ...)
        public Action<string> onSound;                   // audio.hit name
        public Action<string> onNear;                    // prompt label for a loose thing, or null
    }

    public sealed class Actions
    {
        sealed class Kind { public string name; public Func<GeoData> geo; public int color; public float rough, r, power; }
        static readonly Dictionary<string, Kind> KINDS = new Dictionary<string, Kind>
        {
            ["brick"] = new Kind { name = "벽돌", geo = () => Geo.Box(0.21f, 0.065f, 0.1f), color = 0x9a4a32, rough = 0.95f, r = 0.06f, power = 2 },
            ["rock"] = new Kind { name = "돌멩이", geo = () => Geo.Dodecahedron(0.07f), color = 0x77736c, rough = 1, r = 0.07f, power = 2 },
            ["bottle"] = new Kind { name = "빈 병", geo = () => Geo.Cylinder(0.035f, 0.04f, 0.26f, 10), color = 0x2f6b3a, rough = 0.15f, r = 0.05f, power = 1 },
            ["can"] = new Kind { name = "빈 캔", geo = () => Geo.Cylinder(0.033f, 0.033f, 0.12f, 10), color = 0xb8c0c8, rough = 0.35f, r = 0.04f, power = 0.5f },
            ["box"] = new Kind { name = "빈 상자", geo = () => Geo.Box(0.3f, 0.22f, 0.24f), color = 0xb08a5a, rough = 0.9f, r = 0.12f, power = 0.5f },
        };
        static readonly string[] SCATTER_TOWN = { "brick", "rock", "rock", "bottle", "can" }, SCATTER_DEFAULT = { "bottle", "can", "box" };
        const float G = 12, PICK_R = 1.5f, REACH = 1.4f;

        // mulberry32, bit-exact with the JS rng(seed).
        static Func<double> Rng(uint seed)
        {
            uint a = seed;
            return () => { unchecked { a += 0x6D2B79F5; uint t = (a ^ (a >> 15)) * (a | 1); t ^= t + (t ^ (t >> 7)) * (t | 61); return (t ^ (t >> 14)) / 4294967296.0; } };
        }

        sealed class Item
        {
            public string kind; public GameObject mesh;
            public Vector3 pos, v;            // JS space, level-local
            public float rx, ry, rz;          // JS Euler (XYZ)
            public bool flying, byPlayer;
            public float fromX, fromZ;
            public void Place() { J.SetPos(mesh, pos); J.SetRot(mesh, rx, ry, rz); }
        }
        sealed class Reaction { public float hp = 3, t, push, vx, vz, back; public bool ko; public string anim; }

        GameEngine engine; ActionCallbacks cb;
        readonly Dictionary<string, GeoData> geos = new Dictionary<string, GeoData>();
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        readonly Dictionary<Level, List<Item>> byLevel = new Dictionary<Level, List<Item>>();
        readonly Dictionary<IPerson, Reaction> reactions = new Dictionary<IPerson, Reaction>();
        readonly List<(float due, Action fn)> timers = new List<(float, Action)>();
        Item held, near;
        float punchT, busyT;
        bool enabled = true;

        public static Actions Create(GameEngine engine, ActionCallbacks cb)
        {
            var a = new Actions { engine = engine, cb = cb ?? new ActionCallbacks() };
            engine.OnKey(a.OnKey);
            engine.OnFrame(a.Frame);
            return a;
        }

        void OnHit(IPerson p, string how, bool ko) => cb.onHit?.Invoke(p, how, ko);
        void OnHurt(float d, string why) => cb.onHurt?.Invoke(d, why);
        void OnSound(string n) => cb.onSound?.Invoke(n);
        void OnNear(string l) => cb.onNear?.Invoke(l);
        // JS setTimeout (wall clock), ticked from the engine frame.
        void After(float ms, Action fn) => timers.Add((Time.unscaledTime + ms / 1000f, fn));

        GameObject MeshOf(string kind)
        {
            var k = KINDS[kind];
            if (!geos.TryGetValue(kind, out var g)) geos[kind] = g = k.geo();
            if (!mats.TryGetValue(kind, out var m))
                mats[kind] = m = Mat.Lit(new MatSpec { color = J.Hex(k.color), roughness = k.rough, transparent = kind == "bottle", opacity = kind == "bottle" ? 0.85f : 1 });
            return Geo.Obj(g, m, kind, true, false);
        }

        List<Item> Scatter(Level L)
        {
            int n = L.id == "town" ? 40 : 8; var kinds = L.id == "town" ? SCATTER_TOWN : SCATTER_DEFAULT;
            var r = Rng((uint)(L.id.Length * 7919 + 13));
            var zones = new List<WalkZone>();
            foreach (var w in L.walk ?? new List<WalkZone>()) if (w.ramp == null && (w.x1 - w.x0) * (w.z1 - w.z0) > 4) zones.Add(w);
            var items = new List<Item>();
            for (int i = 0, tries = 0; i < n && zones.Count > 0 && tries < n * 6; tries++)
            {
                var w = zones[(int)Math.Floor(r() * zones.Count)];
                float x = w.x0 + 0.5f + (float)r() * (w.x1 - w.x0 - 1), z = w.z0 + 0.5f + (float)r() * (w.z1 - w.z0 - 1);
                var (cx, cz) = Geom.Collide(x, z, w.y, L.solids ?? new List<Solid>(), 0.3f, 1);
                if (Mathf.Sqrt((cx - x) * (cx - x) + (cz - z) * (cz - z)) > 0.01f) continue;
                string kind = kinds[(int)Math.Floor(r() * kinds.Length)];
                var it = new Item { kind = kind, mesh = MeshOf(kind), pos = new Vector3(x, w.y + KINDS[kind].r, z), ry = (float)r() * 6.28f };
                L.group.Add(it.mesh); it.Place(); items.Add(it); i++;
            }
            byLevel[L] = items;
            return items;
        }
        List<Item> ItemsOf(Level L) => byLevel.TryGetValue(L, out var items) ? items : Scatter(L);

        // People the player can reach: in the scene, visible, not the player's own model.
        List<IPerson> People()
        {
            var o = new List<IPerson>(); var me = engine.Player.Model; var scene = engine.Scene.transform;
            foreach (var p in engine.Persons)
                if (p != me && p.Group != null && p.Group.activeInHierarchy && p.Group.transform.IsChildOf(scene)) o.Add(p);
            return o;
        }

        void Hit(IPerson p, float fromX, float fromZ, string how, float power)
        {
            var wp = J.WorldPos(p.Group);
            float dx = wp.x - fromX, dz = wp.z - fromZ, d = Mathf.Sqrt(dx * dx + dz * dz); if (d == 0) d = 1;
            if (!reactions.TryGetValue(p, out var rec)) rec = new Reaction();
            rec.hp -= power; bool ko = rec.hp <= 0;
            OnSound(how == "punch" ? "punch" : "thud");
            if (p.OnHit != null) p.OnHit(engine.Player.X, engine.Player.Z, ko); // crowd member: flinch, flee or go down
            else // a placed person: pushed back, may hit back
            {
                rec.t = ko ? 12 : 0.45f; rec.push = 0.45f; rec.vx = dx / d * 1.6f; rec.vz = dz / d * 1.6f; rec.ko = ko;
                rec.back = !ko && UnityEngine.Random.value < 0.55f ? 0.6f : 0; rec.anim = p.Anim;
                p.SetAnim(ko ? "down" : "hurt");
            }
            if (ko) rec.hp = 3;
            reactions[p] = rec;
            OnHit(p, how, ko);
        }

        void Punch()
        {
            if (punchT > 0 || engine.Player.c.Busy) return;
            punchT = 0.5f; engine.Act("punch"); After(90, () => OnSound("swing"));
            After(190, () => // the fist arrives ~0.19 s in (people-clips.js punch)
            {
                var P = engine.Player; var mg = P.Model?.Group;
                float yaw = mg != null ? J.GetYaw(mg) : P.Yaw, fx = -Mathf.Sin(yaw), fz = -Mathf.Cos(yaw);
                IPerson best = null; float bd = REACH;
                foreach (var p in People())
                {
                    var wp = J.WorldPos(p.Group);
                    float dx = wp.x - P.X, dz = wp.z - P.Z, d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < bd && Mathf.Abs(wp.y - P.Y) < 1 && (dx * fx + dz * fz) / (d == 0 ? 1 : d) > 0.55f) { bd = d; best = p; }
                }
                if (best != null) Hit(best, P.X, P.Z, "punch", 1);
            });
        }

        // Bend down for it; the hand closes on it at the bottom of the reach (~0.4 s into people-clips.js pickup)
        // and it comes up held ready in front of the chest.
        void Pick(Item it)
        {
            held = it; it.flying = false; busyT = 0.8f;
            engine.Act("pickup"); OnNear(null);
            After(400, () =>
            {
                if (held != it) return;
                var hand = engine.Player.Model?.Bone("rHand");
                if (hand != null)
                {
                    it.mesh.transform.SetParent(hand, false); J.SetPos(it.mesh, 0, -0.08f, -0.02f);
                    it.rx = it.ry = it.rz = 0; it.mesh.transform.localRotation = Quaternion.identity; it.mesh.transform.localScale = Vector3.one;
                }
                else it.mesh.transform.SetParent(engine.Scene.transform, false);
                OnSound("pick"); engine.SetHold(true);
            });
        }

        // The held mesh goes back into a level (JS parent.add keeps the local rotation) at its world position.
        void Release(Item it, GameObject parent)
        {
            var wp = J.WorldPos(it.mesh);
            it.mesh.transform.SetParent(parent.transform, false);
            it.pos = wp; it.Place();
        }

        // Wind up and let go at the top of the whip (~0.3 s into people-clips.js throw), along the line of sight.
        void Toss()
        {
            if (busyT > 0) return;
            var it = held; busyT = 0.6f;
            engine.Act("throw"); engine.SetHold(false);
            After(300, () =>
            {
                held = null;
                var L = engine.Level; if (L == null) return;
                Release(it, L.group);
                var dir = J.FromUnity(engine.Camera.transform.forward);
                it.v = dir * 14 + new Vector3(0, 2.2f, 0); it.flying = true; it.byPlayer = true; it.fromX = engine.Player.X; it.fromZ = engine.Player.Z;
                OnSound("swing");
            });
        }

        void OnKey(KeyPress k)
        {
            if (!enabled || !engine.ControlsEnabled) return;
            if (k.code == "KeyG") { if (held != null) Toss(); else if (near != null) Pick(near); }
            else if (k.code == "Mouse0" || k.code == "KeyQ") { if (held != null) Toss(); else Punch(); }
        }

        void Fly(Item it, Level L, float dt)
        {
            var k = KINDS[it.kind]; var v = it.v; var p = it.pos;
            v.y -= G * dt;
            p += v * dt; it.rx += dt * 9; it.rz += dt * 5;
            if (L.solids != null) foreach (var s in L.solids) // bounce off the face it went through least
            {
                if (s == null || s.off || p.x < s.x0 - k.r || p.x > s.x1 + k.r || p.z < s.z0 - k.r || p.z > s.z1 + k.r || p.y < s.y0 || p.y > s.y1 + k.r) continue;
                float[] pen = { p.x - (s.x0 - k.r), s.x1 + k.r - p.x, p.z - (s.z0 - k.r), s.z1 + k.r - p.z, s.y1 + k.r - p.y };
                int[] axis = { 0, 0, 2, 2, 1 }; float[] sign = { -1, 1, -1, 1, 1 };
                int m = 0; for (int i = 1; i < 5; i++) if (pen[i] < pen[m]) m = i;
                p[axis[m]] += sign[m] * pen[m]; v[axis[m]] *= -0.35f; v *= 0.7f;
                if (v.magnitude > 2) OnSound("thud");
            }
            float gy = Geom.GroundAt(L.walk, p.x, p.z, p.y + 0.3f, false, 50)?.y ?? 0;
            if (p.y <= gy + k.r)
            {
                p.y = gy + k.r;
                if (Mathf.Abs(v.y) > 2) OnSound(it.kind == "bottle" ? "glass" : "thud");
                v.y *= -0.3f; v.x *= 0.55f; v.z *= 0.55f;
                if (v.magnitude < 0.5f) { it.flying = false; it.rx = it.kind == "bottle" || it.kind == "can" ? Mathf.PI / 2 : 0; it.rz = 0; }
            }
            if (v.magnitude > 3) foreach (var per in People())
            {
                var tp = J.WorldPos(per.Group);
                if (Mathf.Sqrt((tp.x - p.x) * (tp.x - p.x) + (tp.z - p.z) * (tp.z - p.z)) < 0.45f && p.y > tp.y && p.y < tp.y + 1.8f)
                { Hit(per, it.fromX, it.fromZ, it.byPlayer ? "throw" : "debris", k.power); v *= -0.2f; break; }
            }
            it.v = v; it.pos = p; it.Place();
        }

        void Frame(float dt, float t)
        {
            for (int i = 0; i < timers.Count; i++)
                if (Time.unscaledTime >= timers[i].due) { var fn = timers[i].fn; timers.RemoveAt(i--); fn(); }
            punchT = Mathf.Max(0, punchT - dt); busyT = Mathf.Max(0, busyT - dt);
            var L = engine.Level; if (L == null) return;
            var items = ItemsOf(L); var P = engine.Player;
            foreach (var it in items.ToArray()) if (it.flying && it != held) Fly(it, L, dt);
            // reactions of placed people (crowds handle their own)
            foreach (var kv in reactions)
            {
                var p = kv.Key; var r = kv.Value;
                if (!(r.t > 0) || p.Group == null) continue;
                r.t -= dt;
                if (r.push > 0)
                {
                    float k = r.push / 0.45f; r.push -= dt;
                    var lp = J.Pos(p.Group); lp.x += r.vx * dt * k * 2; lp.z += r.vz * dt * k * 2; J.SetPos(p.Group, lp);
                }
                if (r.back > 0 && (r.back -= dt) <= 0)
                {
                    var lp = J.Pos(p.Group);
                    if (Mathf.Sqrt((P.X - lp.x) * (P.X - lp.x) + (P.Z - lp.z) * (P.Z - lp.z)) < 1.8f)
                    {
                        p.SetAnim("punch"); p.LookAt(P.X, P.Z); OnSound("punch"); engine.Act("hurt", 0.4f); engine.Shake(0.12f, 0.35f);
                        OnHurt(8 + (float)Math.Floor(UnityEngine.Random.value * 6 + 0.5), "fight");
                        r.t = Mathf.Max(r.t, 0.4f);
                    }
                }
                if (r.t <= 0) p.SetAnim(r.anim != null && r.anim != "hurt" && r.anim != "down" && r.anim != "punch" ? r.anim : "idle");
            }
            // the nearest loose thing within reach, for the G prompt
            Item best = null; float bd = PICK_R;
            if (held == null && engine.ControlsEnabled) foreach (var it in items)
            {
                if (it.flying || it.mesh == null) continue;
                float dx = it.pos.x - P.X, dz = it.pos.z - P.Z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < bd && Mathf.Abs(it.pos.y - P.Y) < 1.4f) { bd = d; best = it; }
            }
            if (best != near) { near = best; OnNear(near != null ? $"G · {KINDS[near.kind].name} 줍기" : null); }
        }

        // A loose thing flung from (x, y, z) with velocity (vx, vy, vz), JS space. kind: brick | rock | bottle | can | box.
        public void Spawn(string kind, float x, float y, float z, float vx = 0, float vy = 0, float vz = 0)
        {
            var L = engine.Level; if (L == null || kind == null || !KINDS.ContainsKey(kind)) return;
            var it = new Item { kind = kind, mesh = MeshOf(kind), pos = new Vector3(x, y, z), v = new Vector3(vx, vy, vz), flying = true, fromX = x, fromZ = z };
            L.group.Add(it.mesh); it.Place();
            ItemsOf(L).Add(it);
        }
        public string Held => held != null ? KINDS[held.kind].name : null;
        public void SetEnabled(bool on) => enabled = on;
        // Leaving a level with something in hand: it is dropped where you stand.
        public void Drop()
        {
            if (held == null) return;
            var it = held; held = null; engine.SetHold(false);
            Release(it, engine.Level?.group ?? engine.Scene);
            it.v = Vector3.zero; it.flying = true; it.fromX = it.pos.x; it.fromZ = it.pos.z;
        }
    }
}
