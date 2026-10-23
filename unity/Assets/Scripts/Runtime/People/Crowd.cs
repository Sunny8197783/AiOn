// Ports engine/crowd.js: seeded neighbourhood people (Person 'crowd' preset) moving along polylines with simple
// separation steering, a keep-right lane on sidewalks, player avoidance and distance LOD.
// Modes and their opts (all optional unless noted):
//   stroll  wander along paths (keep right, change paths at shared ends, short stops: phone / talk / idle);
//           with no paths: wander inside the area.
//   brisk   hurry to the END of each path (paths should end at the targets) or, with no paths, straight to the
//           nearest of opts.targets. opts.then: 'respawn' (default: reappear at a path start out of the viewer's
//           sight, a continuous stream) | 'despawn' (hide) | 'idle' (stand near the end).
//   queue   line at opts.at (required), extending along opts.dir (default {0,1}), opts.spacing 0.75 m; every
//           opts.serve s (default 10) the head leaves along opts.leave (default: right of the line) carrying
//           opts.carry (a prop name, e.g. 'waterJug') and rejoins at the tail. Others shuffle forward.
//   sit     seat on opts.spots or a grid over the area (opts.spacing 1.5 m); opts.standing (0.12) of them stand
//           beside their spot; opts.y lifts sitters (mat thickness, default 0.03).
//   idle    stand where they are (or a random placement): idle / phone / talk (talkers face a neighbour).
//   none    hide everyone.
// Shared opts: count (active members, capped by quality: high 40, medium 24, low 12), paths, area, heightAt,
//   props { name: probability }, instant (snap into place; default true for queue / sit, else members keep
//   their positions and walk to the new behaviour). Paths, areas and spots are JS space.
// Update(dt, ctx): ctx.camera / ctx.player are optional. Without them the viewer is taken from the camera that
// last rendered a member and the player is assumed to be at the viewer. Behaviour is in CrowdTick.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.People
{
    public sealed partial class Crowd : ICrowd
    {
        public static readonly string[] MODES = { "stroll", "brisk", "queue", "sit", "idle", "none" };
        static readonly Dictionary<string, int> CAP = new Dictionary<string, int> { ["high"] = 40, ["medium"] = 24, ["low"] = 12 };
        static readonly string[] STAND = { "idle", "idle", "phone", "talk" };
        const float SEP = 0.62f, AVOID = 1.05f, LOD_ANIM = 45, LOD_SHADOW = 25, RESPAWN_HIDE = 20;
        const float TAU = Mathf.PI * 2;
        static float Clamp(float x, float a, float b) => Math.Min(b, Math.Max(a, x));
        static float LerpAngle(float a, float b, float k) => a + ((((b - a + Mathf.PI) % TAU) + TAU) % TAU - Mathf.PI) * k;
        static bool OkPt(XZ? q) => q.HasValue && !float.IsNaN(q.Value.x) && !float.IsInfinity(q.Value.x) && !float.IsNaN(q.Value.z) && !float.IsInfinity(q.Value.z);
        static float Dist(float ax, float az, float bx, float bz) => CrowdPathP.Hyp(ax - bx, az - bz);

        // The mode options as the JS M = { ...opts, targets } plus what apply() derives (dirN, leaveN).
        sealed class ModeOpts
        {
            public int? count; public Dictionary<string, float> props; public bool? instant; public string then;
            public List<XZ> targets = new List<XZ>(); public XZ? at, dir, leave; public float? spacing, serve, standing, y;
            public string carry; public List<CrowdSpot> spots; public XZ dirN, leaveN;
        }

        readonly Func<float> r;
        readonly GameObject group;
        Vector3 viewer; bool hasViewer;
        Func<float, float, float> H;
        readonly Player player;
        List<CrowdPathP> paths = new List<CrowdPathP>();
        RectXZ area;
        string mode = "none";
        ModeOpts M = new ModeOpts();
        int active, frame; float serveT; bool warned;
        readonly List<CrowdAgent> line = new List<CrowdAgent>();
        CrowdTmp tmp = new CrowdTmp { tz = 1 };
        readonly List<CrowdAgent> agents = new List<CrowdAgent>();
        readonly List<IPerson> members;

        T Pick<T>(T[] arr) => PeopleOutfit.Pick(r, arr);
        T Pick<T>(List<T> arr) => arr[Math.Min(arr.Count - 1, (int)(r() * arr.Count))];

        // createCrowd({ count, seed, people, quality|kit, heightAt, player })
        public static Crowd Create(CrowdOptions o) => new Crowd(o ?? new CrowdOptions());

        Crowd(CrowdOptions o)
        {
            string q = o.quality ?? o.kit?.Quality ?? "high";
            int cap = CAP.TryGetValue(q, out var cq) ? cq : CAP["high"];
            Func<string, int?, IPerson> make = o.people != null ? (Func<string, int?, IPerson>)o.people.CreatePerson : (pr, sd) => Person.Create(pr, sd);
            r = Kit.Rng(PeopleOutfit.ToUint32(o.seed * 2654435761.0) ^ 0x5bd1e995u);
            group = J.Group("crowd");
            H = o.heightAt ?? ((x, z) => 0);
            player = o.player;

            // ---- members (built up front so the level build absorbs the cost) ----
            int n = Math.Max(0, Math.Min(o.count, cap));
            for (int i = 0; i < n; i++)
            {
                IPerson p;
                try { p = make("crowd", o.seed * 1000 + i + 1); } catch (Exception err) { Debug.LogError("[crowd] createPerson failed " + err); continue; }
                if (p?.Group == null) { Debug.LogError("[crowd] createPerson returned no group"); continue; }
                if (p.Mesh != null) p.Mesh.gameObject.AddComponent<ViewerHook>().onRender = (cam) => { viewer = J.FromUnity(cam.transform.position); hasViewer = true; };
                p.Group.SetActive(false);
                group.Add(p.Group);
                var a = new CrowdAgent { p = p, i = i, slow = ((p as Person)?.Spec.stoop ?? 0) > 0.1f ? 0.72f : 1 };
                agents.Add(a);
                // Hit by the player (engine/actions.js): flinch, then run from (x, z); ko = knocked down for a while.
                p.OnHit = (x, z, ko) => { a.hurtT = 0.45f; a.fx = x; a.fz = z; if (ko) a.downT = 14; else a.fleeT = 7; };
            }
            members = agents.Select(a => a.p).ToList();
        }

        public GameObject Group => group;
        public string Mode => mode;
        public int Active => active;
        public IReadOnlyList<IPerson> Members => members;

        static void Show(CrowdAgent a, bool on) { if (a.p.Group.activeSelf != on) a.p.Group.SetActive(on); }
        static void SetProps(CrowdAgent a, HashSet<string> want)
        {
            foreach (var name in a.props) if (!want.Contains(name)) a.p.SetProp(name, false);
            foreach (var name in want) if (!a.props.Contains(name)) a.p.SetProp(name, true);
            a.props = want;
        }
        void RandomPathPoint(CrowdAgent a)
        {
            float total = 0; foreach (var P in paths) total += P.len;
            float u = r() * total; int pi = 0;
            while (pi < paths.Count - 1 && u > paths[pi].len) u -= paths[pi++].len;
            a.path = pi; a.s = Clamp(u, 0, paths[pi].len);
        }
        XZ AreaPoint()
        {
            if (area != null) { float x = area.x0 + r() * (area.x1 - area.x0); return new XZ(x, area.z0 + r() * (area.z1 - area.z0)); }
            float px = (r() - 0.5f) * 10; return new XZ(px, (r() - 0.5f) * 10);
        }
        XZ LaneGoal(CrowdAgent a) // carrot on the path, offset to the walker's right (keep right on sidewalks)
        {
            paths[a.path].At(a.s, ref tmp);
            float dx = tmp.tx * a.dir, dz = tmp.tz * a.dir;
            return new XZ(tmp.x - dz * a.lane, tmp.z + dx * a.lane);
        }
        static void PlaceAt(CrowdAgent a, float x, float z, float? yaw = null) { a.x = x; a.z = z; a.vx = a.vz = 0; a.yaw = yaw ?? a.yaw; a.placed = true; }

        // ---- per-mode setup ----
        void SetupPath(CrowdAgent a, bool keep, bool brisk)
        {
            if (keep) { var pr = CrowdPathP.Project(paths, a.x, a.z).Value; a.path = pr.pi; a.s = pr.s; } else RandomPathPoint(a);
            paths[a.path].At(a.s, ref tmp);
            float ahead = -Mathf.Sin(a.yaw) * tmp.tx - Mathf.Cos(a.yaw) * tmp.tz; // keep walking the way they face
            a.dir = brisk ? 1 : keep ? (ahead >= 0 ? 1 : -1) : (r() < 0.5f ? 1 : -1);
            a.lane = brisk ? (r() - 0.5f) * 1.4f : 0.25f + r() * 0.6f;
            if (!keep) { var g = LaneGoal(a); PlaceAt(a, g.x, g.z, Mathf.Atan2(-tmp.tx * a.dir, -tmp.tz * a.dir)); }
        }
        XZ Slot(int k)
        {
            XZ d = M.dirN; float s = M.spacing ?? 0.75f;
            return new XZ(M.at.Value.x + d.x * (0.5f + k * s), M.at.Value.z + d.z * (0.5f + k * s));
        }
        void SetupSit(List<CrowdAgent> list) // always snaps onto the spots
        {
            var spots = M.spots?.Where(s => OkPt(new XZ(s.x, s.z))).ToList();
            if (spots == null || spots.Count == 0)
            {
                if (area == null) { Debug.LogError("[crowd] sit mode needs opts.spots or an area (setArea / opts.area)"); spots = new List<CrowdSpot> { new CrowdSpot { x = 0, z = 0 } }; }
                else
                {
                    double sp = M.spacing ?? 1.5f; spots = new List<CrowdSpot>();
                    for (double z = area.z0 + sp / 2; z < area.z1; z += sp) for (double x = area.x0 + sp / 2; x < area.x1; x += sp)
                        { float sx = (float)x + (r() - 0.5f) * 0.3f; spots.Add(new CrowdSpot { x = sx, z = (float)z + (r() - 0.5f) * 0.3f }); }
                }
            }
            var keyed = spots.Select(s => (s, k: r())).OrderBy(e => e.k).Select(e => e.s).ToList();
            for (int k = 0; k < list.Count; k++)
            {
                var a = list[k];
                if (k >= keyed.Count) { a.state = "gone"; Show(a, false); continue; }
                var s = keyed[k];
                float yaw = s.yaw.HasValue && !float.IsNaN(s.yaw.Value) && !float.IsInfinity(s.yaw.Value) ? s.yaw.Value + (r() - 0.5f) * 0.4f : Pick(new[] { 0, 1, 2, 3 }) * Mathf.PI / 2 + (r() - 0.5f) * 0.6f;
                bool standing = r() < (M.standing ?? 0.12f);
                a.state = "stand"; a.goal = null;
                if (standing) { a.standAnim = Pick(new[] { "idle", "phone", "talk" }); a.dy = 0; PlaceAt(a, s.x + Mathf.Cos(yaw) * 0.65f, s.z - Mathf.Sin(yaw) * 0.65f, yaw); }
                else { a.standAnim = "sit"; a.dy = M.y ?? 0.03f; PlaceAt(a, s.x, s.z, yaw); }
                a.face = a.yaw;
            }
        }
        float FaceNeighbour(CrowdAgent a)
        {
            CrowdAgent best = null; float bd = 9;
            foreach (var b in agents) if (b != a && b.on && b.state != "gone") { float d = (b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z); if (d < bd) { bd = d; best = b; } }
            return best != null ? Mathf.Atan2(-(best.x - a.x), -(best.z - a.z)) : a.yaw;
        }

        public void SetMode(string m, CrowdModeOptions opts = null)
        {
            if (Array.IndexOf(MODES, m) < 0) { Debug.LogError($"[crowd] unknown mode \"{m}\"; known: {string.Join(", ", MODES)}"); return; }
            opts = opts ?? new CrowdModeOptions();
            if (opts.paths != null) SetPaths(opts.paths, false);
            if (opts.area != null) SetArea(opts.area, false);
            if (opts.heightAt != null) H = opts.heightAt;
            mode = m;
            M = new ModeOpts
            {
                count = opts.count, props = opts.props, instant = opts.instant, then = opts.then, at = opts.at, dir = opts.dir, leave = opts.leave,
                spacing = opts.spacing, serve = opts.serve, standing = opts.standing, y = opts.y, carry = opts.carry, spots = opts.spots,
                targets = opts.targets != null ? opts.targets.Where(t => OkPt(t)).ToList() : new List<XZ>(),
            };
            Apply();
        }
        public void SetPaths(List<List<XZ>> paths) => SetPaths(paths, true);
        void SetPaths(List<List<XZ>> list, bool reapply)
        {
            if (list == null) { Debug.LogError("[crowd] setPaths expects an array of [{x,z},...] polylines"); return; }
            paths = list.Select(CrowdPathP.Prep).Where(p => p != null).ToList();
            if (paths.Count < list.Count) Debug.LogWarning($"[crowd] {list.Count - paths.Count} path(s) ignored (need 2+ finite {{x,z}} points)");
            if (reapply && mode != "none") Apply();
        }
        public void SetArea(RectXZ rect) => SetArea(rect, true);
        void SetArea(RectXZ rect, bool reapply)
        {
            bool Fin(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
            if (rect == null || !(Fin(rect.x0) && Fin(rect.x1) && Fin(rect.z0) && Fin(rect.z1)) || rect.x1 <= rect.x0 || rect.z1 <= rect.z0)
            {
                Debug.LogError("[crowd] setArea expects {x0,x1,z0,z1} with x0<x1, z0<z1"); return;
            }
            area = new RectXZ(rect.x0, rect.x1, rect.z0, rect.z1);
            if (reapply && (mode == "sit" || ((mode == "stroll" || mode == "idle") && paths.Count == 0))) Apply();
        }

        public void Dispose()
        {
            foreach (var a in agents) a.p.Dispose();
            UnityEngine.Object.Destroy(group); // JS removeFromParent
        }
    }
}
