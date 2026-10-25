// Ports levels/shelter-people.js: people in the shelter. Every evacuee owns a seat on a mat (the camp already laid out
// bags and shoes there); arriving, leaving and toilet trips move that same person between an entrance and the seat, so
// nobody pops in or swaps outfits. Plus the reception queue, a volunteer and two radio listeners on chairs, the shelter
// manager (vest, no baton) and 서연 (hidden until the director shows her). Works without a crowd.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ShelterCamp;

namespace Gyeongbo.Levels
{
    public sealed class ShelterPeopleResult
    {
        public GameObject group;
        public Dictionary<string, IPerson> npcs = new Dictionary<string, IPerson>();   // shelterManager, seoyeon, volunteer
        public Action<string> setCrowd;               // arriving | settled | leaving
        public Action<string> setPhase;               // alert | calm
        public Action<XZ?> setFire;                   // fire position or null: clears the seats around it, pauses toilet trips
        public Action<List<string>> setWays;          // open entrances: arrivals and leavers only use these
        public Action<float, float, Player> update;
    }

    public static class ShelterPeople
    {
        const float PI = Mathf.PI;
        static float yawTo(float dx, float dz) => Mathf.Atan2(-dx, -dz);
        static float turn(float a, float b) => Mathf.Atan2(Mathf.Sin(b - a), Mathf.Cos(b - a));
        const float AN = -6.9f, AS = 9.0f;   // north / south driving aisles (z)

        sealed class Wp { public float x, z, wait; public string anim; public float? face; }
        static Wp W(float x, float z) => new Wp { x = x, z = z };
        // Entrance paths [x, z]: they start out of sight (stair flight, ramp slopes, lobby and toilet doors) and end on an aisle.
        static readonly Dictionary<string, float[][]> WAYS = new Dictionary<string, float[][]>
        {
            ["stairs"] = new[] { new[] { 4.3f, -19.6f }, new[] { 4.3f, -17.3f }, new[] { 5f, -16.0f }, new[] { 5f, -10.4f }, new[] { 3.5f, -9.2f }, new[] { 3.5f, AN } },
            ["access"] = new[] { new[] { -13f, -23 }, new[] { -13f, -15.6f }, new[] { -13f, -10.0f }, new[] { -12.4f, AN } },
            ["east"] = new[] { new[] { 24.6f, -3.4f }, new[] { 22.8f, -0.5f }, new[] { 20.3f, -1.2f }, new[] { 20.3f, AN } },
            ["toilet"] = new[] { new[] { 24.6f, 3.4f }, new[] { 22.8f, 0.5f }, new[] { 20.3f, -1.2f }, new[] { 20.3f, AN } },
            ["ramp"] = new[] { new[] { 11f, 23 }, new[] { 11f, 15.4f }, new[] { 11f, 10.6f }, new[] { 10.2f, AS } },
        };
        static readonly string[] IN = { "stairs", "stairs", "stairs", "east", "ramp", "access" }, OUT = { "stairs", "stairs", "east", "access" };
        public static readonly string[] ENTRANCES = { "stairs", "ramp", "access", "east" };
        static Wp DESK() => new Wp { x = 2.55f, z = -9.5f, wait = 2.6f, anim = "talk", face = PI };   // arrivals by stair A write in the roster
        static readonly float[] COLS = { -19.3f, -17.1f, -14.9f, -12.7f, -10.5f, -3.9f, -1.7f, 2.7f, 4.9f };   // gaps between mats clear of pillars, radio table, bins
        static readonly Dictionary<string, (int seats, int walk)> QUOTA = new Dictionary<string, (int, int)> { ["high"] = (26, 4), ["medium"] = (16, 3), ["low"] = (8, 2) };
        const float FIRE_CLEAR = 6.5f;   // seats this close to a fire are left (their people moved away)

        // Walk-zone height at (x, z); the lowest zone wins where zones overlap (stair foot under the top landing).
        public static float HeightAt(IList<WalkZone> walk, float x, float z)
        {
            float y = float.PositiveInfinity;
            foreach (var w in walk)
            {
                if (x < w.x0 || x > w.x1 || z < w.z0 || z > w.z1) continue;
                var r = w.ramp; float h = r != null ? r.y0 + (r.y1 - r.y0) * (r.axis == "x" ? (x - w.x0) / (w.x1 - w.x0) : (z - w.z0) / (w.z1 - w.z0)) : w.y;
                if (h < y) y = h;
            }
            return float.IsPositiveInfinity(y) ? 0 : y;
        }

        // Entrance -> aisle -> gap column -> seat row -> seat. Reverse it to leave.
        static List<Wp> SeatPath(string way, ShelterSeat s, bool desk = false)
        {
            var pts = WAYS[way].Select(p => W(p[0], p[1])).ToList();
            if (desk && way == "stairs") pts.Insert(4, DESK());
            float az = pts[pts.Count - 1].z, gx = COLS.Aggregate((a, c) => Mathf.Abs(c - s.x) < Mathf.Abs(a - s.x) ? c : a);
            pts.Add(W(gx, az)); pts.Add(W(gx, s.z)); pts.Add(W(s.x, s.z));
            return pts;
        }

        sealed class Ev
        {
            public IPerson p; public ShelterSeat home; public string mode; public List<Wp> path; public int i; public float pause; public float? face;
            public Action<Ev> done; public float speed; public bool brisk; public float acc, away; public int rank;
        }

        public static ShelterPeopleResult BuildPeople(Kit kit, IPeople people, List<ShelterSeat> seats, List<WalkZone> walk)
        {
            var group = J.Group("shelter-people");
            var Q = QUOTA.TryGetValue(kit.Quality ?? "", out var q0) ? q0 : QUOTA["high"]; var r = Kit.Rng(733);
            T pick<T>(IList<T> list) where T : class { int k = (int)(r() * list.Count); return k < list.Count ? list[k] : null; }
            IPerson at(string preset, int seed, float x, float z, float yaw, string anim, float y = 0)
            {
                IPerson p = null;
                try { p = people?.CreatePerson(preset, seed); } catch (Exception err) { Debug.LogError($"[shelter] createPerson failed {preset} {err}"); }
                if (p?.Group == null) p = new NullPerson();
                J.SetPos(p.Group, x, y, z); J.SetYaw(p.Group, yaw); p.SetAnim(anim, null, true);
                group.Add(p.Group);
                return p;
            }
            bool visible(IPerson p) => p.Group.activeSelf;

            // ---- evacuees, one per chosen seat; a shuffled order decides who arrives / leaves first ----
            int n = Math.Min(Q.seats, seats.Count);
            var ev = Enumerable.Range(0, n).Select(i =>
            {
                var s = seats[(int)Math.Floor((double)i * seats.Count / n)];
                return new Ev { p = at("crowd", 600 + i, s.x, s.z, s.yaw, "sit", 0.012f), home = s, mode = "seat", speed = 0.95f + r() * 0.4f };
            }).ToList();
            var order = ev.ToList();
            for (int i = order.Count - 1; i > 0; i--) { int j = (int)(r() * (i + 1)); (order[i], order[j]) = (order[j], order[i]); }
            for (int k = 0; k < order.Count; k++) order[k].rank = k;

            // ---- staff, queue, listeners, 서연 ----
            var manager = at("guide", 41, RECEPTION.x - 1.25f, RECEPTION.z + 0.1f, yawTo(0.9f, -0.95f), "idle");
            manager.SetProp("baton", false);
            var vc = CHAIRS["reception"][1];
            var volunteer = at("crowd", 42, RECEPTION.x + vc[0], RECEPTION.z + vc[1], vc[2], "sitChair");
            var listeners = CHAIRS["radio"].Take(2).Select((c, i) => at(i > 0 ? "elder" : "crowd", 43 + i, RADIO.x + c[0], RADIO.z + c[1], c[2], "sitChair")).ToList();
            var queue = new[] { (1.6f, -9.5f), (1.9f, -10.35f), (2.05f, -11.15f) }.Take(Q.walk > 2 ? 3 : 2)
                .Select((c, i) => at("crowd", 50 + i, c.Item1, c.Item2, PI + (i > 0 ? (r() - 0.5f) * 0.4f : 0), "idle")).ToList();
            var spot = SPOT;
            var seoyeon = at("seoyeon", 7, spot.x + 1.5f, spot.z + 1.9f, yawTo(-1.5f, -0.5f), "idle");
            seoyeon.Group.SetActive(false);   // the director shows her
            var still = new List<IPerson> { manager, volunteer }; still.AddRange(listeners); still.AddRange(queue); still.Add(seoyeon);

            string mode = "settled", phase = "calm"; XZ? fire = null; float timer = 1; int frame = 0; Player last = null; IList<string> ways = ENTRANCES;
            IList<string> open(string[] list) { var l = list.Where(w => ways.Contains(w)).ToList(); return l.Count > 0 ? l : ways; }
            bool @out(ShelterSeat h) => fire.HasValue && ShelterUtil.Hypot(h.x - fire.Value.x, h.z - fire.Value.z) < FIRE_CLEAR;
            bool nearPlayer(Ev e) => last != null && ShelterUtil.Hypot(e.home.x - last.X, e.home.z - last.Z) < 3;
            int busy() => ev.Count(e => e.mode == "walk" || e.mode == "away");

            void seat(Ev e, bool snap)
            {
                var g = e.p.Group;
                e.mode = "seat"; e.path = null; g.SetActive(true); J.SetPos(g, e.home.x, 0.012f, e.home.z);
                if (snap) J.SetYaw(g, e.home.yaw);
                e.p.SetAnim("sit", null, snap); e.p.SetProp("backpack", false);
            }
            void hide(Ev e) { e.mode = "gone"; e.path = null; e.p.Group.SetActive(false); }
            void go(Ev e, List<Wp> path, Action<Ev> done, bool fromSeat = false)
            {
                var g = e.p.Group;
                e.mode = "walk"; e.path = path; e.done = done; e.face = null; g.SetActive(true);
                e.brisk = phase == "alert" && mode == "arriving";
                e.p.SetSpeed(e.speed * (e.brisk ? 1.3f : 1));
                if (fromSeat) { e.i = 0; e.pause = 1.0f; e.p.SetAnim("idle"); }   // stand up first
                else { var p0 = path[0]; J.SetPos(g, p0.x, HeightAt(walk, p0.x, p0.z), p0.z); J.SetYaw(g, yawTo(path[1].x - p0.x, path[1].z - p0.z)); e.i = 1; e.pause = 0; }
            }
            void applyPhase()
            {
                bool alert = phase == "alert";
                manager.SetAnim(alert || mode == "arriving" ? "talk" : "idle");
                for (int i = 0; i < queue.Count; i++) queue[i].SetAnim(alert && i == 1 ? "phone" : "idle");
            }
            void reset()
            {
                timer = 1;
                foreach (var e in ev)
                {
                    if (@out(e.home) || (mode == "arriving" && e.rank >= ShelterUtil.Round(ev.Count * 0.55f))) hide(e);
                    else seat(e, true);
                }
                foreach (var qp in queue) qp.Group.SetActive(mode == "arriving");
                for (int i = 0; i < listeners.Count; i++) listeners[i].Group.SetActive(mode != "leaving" || i == 0);
                applyPhase();
            }
            void launch()
            {
                if (busy() >= (mode == "settled" ? Math.Min(2, Q.walk) : Q.walk)) return;
                if (mode == "arriving")
                {
                    var e = order.FirstOrDefault(x => x.mode == "gone" && !@out(x.home));
                    if (e != null) { e.p.SetProp("backpack", r() < 0.5f); go(e, SeatPath(pick(open(IN)), e.home, true), x => seat(x, false)); }
                }
                else if (mode == "leaving")
                {
                    if (ev.Count(x => x.mode == "seat") <= ShelterUtil.Round(ev.Count * 0.25f)) return;
                    var e = order.FirstOrDefault(x => x.mode == "seat" && !nearPlayer(x));
                    if (e != null) { e.p.SetProp("backpack", true); var path = SeatPath(pick(open(OUT)), e.home); path.Reverse(); go(e, path, hide, true); }
                }
                else if (!fire.HasValue && r() < 0.6f)
                {   // settled: now and then someone goes to the toilets by the lift hall
                    var e = pick(ev.Where(x => x.mode == "seat" && !nearPlayer(x)).ToList());
                    if (e != null) { var path = SeatPath("toilet", e.home); path.Reverse(); go(e, path, x => { x.mode = "away"; x.p.Group.SetActive(false); x.away = 10 + r() * 12; }, true); }
                }
            }
            void step(Ev e, float dt, Player pl)
            {
                var g = e.p.Group;
                if (e.pause > 0)
                {
                    e.pause -= dt;
                    if (e.face != null) { float y0 = J.GetYaw(g); J.SetYaw(g, y0 + turn(y0, e.face.Value) * Math.Min(1, dt * 5)); }
                    if (e.pause > 0) return;
                    e.face = null;
                }
                if (e.i >= e.path.Count) { var f = e.done; e.done = null; f?.Invoke(e); return; }
                var t = e.path[e.i]; var pos = J.Pos(g); float dx = t.x - pos.x, dz = t.z - pos.z, d = ShelterUtil.Hypot(dx, dz);
                if (d < 0.06f) { e.i++; if (t.wait > 0) { e.pause = t.wait; e.face = t.face; e.p.SetAnim(t.anim ?? "idle"); } return; }
                if (pl != null)
                {   // wait for the player instead of walking through them
                    float px = pl.X - pos.x, pz = pl.Z - pos.z;
                    if (px * px + pz * pz < 1.2f && px * dx + pz * dz > 0) { e.p.SetAnim("idle"); return; }
                }
                float k = Math.Min(d, e.speed * (e.brisk ? 1.3f : 1) * dt) / d;
                float nx = pos.x + dx * k, nz = pos.z + dz * k;
                J.SetPos(g, nx, HeightAt(walk, nx, nz), nz);
                float yaw = J.GetYaw(g); J.SetYaw(g, yaw + turn(yaw, yawTo(dx, dz)) * Math.Min(1, dt * 7));
                bool stair = nx > 3.6f && nx < 4.95f && nz < -17.45f;
                e.p.SetAnim(stair ? (dz < 0 ? "stairsUp" : "stairsDown") : e.brisk ? "brisk" : "walk");
            }

            void update(float dt, float t, Player pl)
            {
                frame++; if (pl != null) last = pl;
                timer -= dt;
                if (timer <= 0) { timer = mode == "arriving" ? 2.2f + r() * 1.5f : mode == "leaving" ? 2.8f + r() * 2 : 7 + r() * 6; launch(); }
                foreach (var e in ev)
                {
                    if (e.mode == "walk") { step(e, dt, pl); e.p.Update(dt); }
                    else if (e.mode == "away") { e.away -= dt; if (e.away <= 0) go(e, SeatPath("toilet", e.home), x => seat(x, false)); }
                    else if (e.mode == "seat")
                    {   // seated people animate at 1/3 rate (breathing only)
                        e.acc += dt;
                        if ((frame + e.rank) % 3 != 0) continue;
                        var g = e.p.Group; float y0 = J.GetYaw(g), a = turn(y0, e.home.yaw);
                        if (Mathf.Abs(a) > 0.01f) J.SetYaw(g, y0 + a * Math.Min(1, e.acc * 4));
                        e.p.Update(e.acc); e.acc = 0;
                    }
                }
                foreach (var p in still) if (visible(p)) p.Update(dt);
                bool near(IPerson p, float d) { if (pl == null) return false; var q = J.Pos(p.Group); return ShelterUtil.Hypot(pl.X - q.x, pl.Z - q.z) < d; }
                if (near(manager, 5)) manager.LookAt(pl.X, pl.Z); else manager.LookAt(null, null);
                if (visible(seoyeon) && near(seoyeon, 6)) seoyeon.LookAt(pl.X, pl.Z); else seoyeon.LookAt(null, null);
            }

            reset();
            return new ShelterPeopleResult
            {
                group = group, npcs = new Dictionary<string, IPerson> { ["shelterManager"] = manager, ["seoyeon"] = seoyeon, ["volunteer"] = volunteer },
                setCrowd = m => { mode = m; reset(); },
                setPhase = p => { phase = p; applyPhase(); },
                setFire = pos => { fire = pos; reset(); },
                setWays = list => { ways = list; },
                update = update,
            };
        }

        // JS fallback when createPerson fails: an empty group that ignores every call.
        sealed class NullPerson : IPerson
        {
            public GameObject Group { get; } = J.Group("person");
            public Renderer Mesh => null;
            public Transform Bone(string name) => null;
            public float Height => 1.7f;
            public string Preset => null;
            public float Seat { get; set; }
            public string Anim { get; private set; }
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
            public void Dispose() { }
            public Action<float, float, bool> OnHit { get; set; }
            public bool Hostile { get; set; }
        }
    }
}
