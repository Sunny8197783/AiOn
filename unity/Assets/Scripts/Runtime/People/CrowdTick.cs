// Crowd behaviour (engine/crowd.js apply / tick / pose / update): mode setup, path following, queues, steering
// with separation and player avoidance, animation and LOD. JS space; only PoseAgent touches Transforms.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.People
{
    public sealed partial class Crowd
    {
        void Apply()
        {
            int want = mode == "none" ? 0 : Math.Max(0, Math.Min(M.count ?? agents.Count, agents.Count));
            active = want;
            bool instant = M.instant ?? (mode == "queue" || mode == "sit");
            var list = new List<CrowdAgent>();
            for (int k = 0; k < agents.Count; k++)
            {
                var a = agents[k];
                bool on = k < want;
                if (!on) { a.on = false; Show(a, false); SetProps(a, new HashSet<string>()); continue; }
                bool keep = !instant && a.on && a.placed && a.state != "gone";
                a.on = true; a.dy = 0; a.face = null; a.goal = null; a.stopT = 4 + r() * 16; a.timer = 0;
                var props = new HashSet<string>();
                if (M.props != null) foreach (var e in M.props) if (r() < e.Value) props.Add(e.Key);
                SetProps(a, props);
                list.Add(a);
                if (mode == "stroll" || mode == "brisk")
                {
                    bool brisk = mode == "brisk";
                    a.speed = (brisk ? 1.55f + r() * 0.45f : 1.0f + r() * 0.4f) * a.slow;
                    a.moveAnim = brisk ? "brisk" : "walk"; a.standAnim = brisk ? "idle" : Pick(STAND); a.state = "move";
                    if (paths.Count > 0) SetupPath(a, keep, brisk);
                    else
                    {
                        a.path = -1;
                        if (!keep) { var p = AreaPoint(); PlaceAt(a, p.x, p.z, r() * TAU); }
                        if (brisk && M.targets.Count == 0 && !warned) { warned = true; Debug.LogWarning("[crowd] brisk mode without paths or targets: members stroll in the area"); }
                    }
                }
                else if (mode == "idle")
                {
                    if (!keep) { if (paths.Count > 0) SetupPath(a, false, false); else { var p = AreaPoint(); PlaceAt(a, p.x, p.z, r() * TAU); } }
                    a.state = "stand"; a.standAnim = Pick(STAND);
                }
                else if (mode == "queue")
                {
                    a.speed = 0.9f * a.slow; a.moveAnim = "walk"; a.standAnim = r() < 0.3f ? "phone" : "idle"; a.state = "line"; a.jit = (r() - 0.5f) * 0.18f;
                    if (!a.placed) { var p = AreaPoint(); PlaceAt(a, p.x, p.z); }
                }
                if (!keep) { a.p.SetAnim(a.standAnim, null, true); a.anim = a.standAnim; a.moving = false; }
                Show(a, true);
            }
            if (mode == "sit") SetupSit(list);
            if (mode == "idle") foreach (var a in list) a.face = a.standAnim == "talk" ? FaceNeighbour(a) : a.yaw;
            if (mode == "queue")
            {
                if (!OkPt(M.at)) { Debug.LogError("[crowd] queue mode needs opts.at {x,z}"); M.at = area != null ? new XZ((area.x0 + area.x1) / 2, (area.z0 + area.z1) / 2) : new XZ(0, 0); }
                XZ d = OkPt(M.dir) ? M.dir.Value : new XZ(0, 1); float l = CrowdPathP.Hyp(d.x, d.z); if (l == 0) l = 1;
                M.dirN = new XZ(d.x / l, d.z / l);
                XZ lv = OkPt(M.leave) ? M.leave.Value : new XZ(-M.dirN.z, M.dirN.x); float ll = CrowdPathP.Hyp(lv.x, lv.z); if (ll == 0) ll = 1;
                M.leaveN = new XZ(lv.x / ll, lv.z / ll);
                line.Clear();
                XZ at = M.at.Value;
                list = list.OrderBy(a => Dist(a.x, a.z, at.x, at.z)).ToList(); // stable, like Array.prototype.sort
                line.AddRange(list);
                if (instant) for (int k = 0; k < line.Count; k++) { var a = line[k]; var s = Slot(k); PlaceAt(a, s.x + M.dirN.z * a.jit, s.z - M.dirN.x * a.jit, Mathf.Atan2(M.dirN.x, M.dirN.z)); }
                serveT = (M.serve ?? 10) * (0.5f + r());
            }
            foreach (var a in list) if (a.state != "gone") PoseAgent(a);
        }

        // ---- behaviour ----
        void EndOfPath(CrowdAgent a)
        {
            var P = paths[a.path]; XZ end = a.dir > 0 ? P.p[P.p.Count - 1] : P.p[0];
            if (mode == "brisk") { Arrive(a, end); return; }
            var next = new List<(int qi, float s, int d)>();
            for (int qi = 0; qi < paths.Count; qi++)
            {
                if (qi == a.path) continue;
                var Q = paths[qi];
                if (Dist(Q.p[0].x, Q.p[0].z, end.x, end.z) < 4) next.Add((qi, 0, 1));
                if (Dist(Q.p[Q.p.Count - 1].x, Q.p[Q.p.Count - 1].z, end.x, end.z) < 4) next.Add((qi, Q.len, -1));
            }
            if (next.Count > 0 && r() > 0.15f) { var nx = Pick(next); a.path = nx.qi; a.s = nx.s; a.dir = nx.d; }
            else { a.dir = -a.dir; a.s = Clamp(a.s, 0, P.len); if (r() < 0.5f) Stop(a, 2 + r() * 4); }
        }
        void Arrive(CrowdAgent a, XZ end)
        {
            string then = M.then ?? "respawn";
            if (then == "idle") { a.state = "stand"; a.goal = null; a.standAnim = Pick(new[] { "idle", "idle", "phone" }); a.face = Mathf.Atan2(-(end.x - a.x), -(end.z - a.z)); return; }
            a.state = "gone"; a.timer = 0.5f + r(); Show(a, false);
        }
        void Stop(CrowdAgent a, float s) { a.state = "stand"; a.timer = s; a.standAnim = Pick(STAND); a.face = a.standAnim == "talk" ? FaceNeighbour(a) : (float?)null; }
        void Respawn(CrowdAgent a, float dt)
        {
            if ((M.then ?? "respawn") != "respawn" || paths.Count == 0) return;
            if ((a.timer -= dt) > 0) return;
            a.timer = 0.6f;
            int pi = Math.Min(paths.Count - 1, (int)(r() * paths.Count)); XZ st = paths[pi].p[0];
            if (hasViewer && CrowdPathP.Hyp(st.x - viewer.x, st.z - viewer.z) < RESPAWN_HIDE) return;
            a.path = pi; a.s = 0; a.dir = 1; a.lane = (r() - 0.5f) * 1.4f;
            var g = LaneGoal(a);
            PlaceAt(a, g.x, g.z, Mathf.Atan2(-tmp.tx, -tmp.tz)); a.state = "move"; Show(a, true);
        }

        void QueueTick(float dt)
        {
            serveT -= dt;
            if (serveT <= 0 && line.Count > 1)
            {
                serveT = (M.serve ?? 10) * (0.7f + 0.6f * r());
                var a = line[0]; line.RemoveAt(0);
                a.state = "leave"; a.goal = new XZ(M.at.Value.x + M.leaveN.x * 18, M.at.Value.z + M.leaveN.z * 18);
                if (M.carry != null) { SetProps(a, new HashSet<string>(a.props) { M.carry }); if (M.carry == "waterJug") a.moveAnim = "carry"; }
            }
        }

        // Steer toward (gx, gz) at speed v with separation and player avoidance. Returns the distance to the goal.
        float Steer(CrowdAgent a, float gx, float gz, float v, float dt, XZ? pl)
        {
            float dx = gx - a.x, dz = gz - a.z, d = CrowdPathP.Hyp(dx, dz);
            float wx = 0, wz = 0;
            if (d > 0.04f) { float k = Math.Min(v, d * 1.8f) / d; wx = dx * k; wz = dz * k; }
            foreach (var b in agents)
            {
                if (b == a || !b.on || b.state == "gone") continue;
                float ex = a.x - b.x, ez = a.z - b.z, e2 = ex * ex + ez * ez;
                if (e2 < SEP * SEP && e2 > 1e-6f) { float e = (float)Math.Sqrt(e2), f = ((SEP - e) / SEP) * 1.5f / e; wx += ex * f; wz += ez * f; }
            }
            if (pl.HasValue)
            {
                float ex = a.x - pl.Value.x, ez = a.z - pl.Value.z, e = CrowdPathP.Hyp(ex, ez);
                if (e < AVOID * 2.4f && e > 1e-3f && d > 0.04f && (dx * -ex + dz * -ez) / d > 0.2f * e) // player ahead: step to the right
                {
                    float k = 0.6f * (1 - e / (AVOID * 2.4f)) * v; wx += (-dz / d) * k; wz += (dx / d) * k;
                }
                if (e < AVOID && e > 1e-3f) { float f = ((AVOID - e) / AVOID) * 2.2f / e; wx += ex * f; wz += ez * f; }
            }
            float kk = Math.Min(1, dt * 5);
            a.vx += (wx - a.vx) * kk; a.vz += (wz - a.vz) * kk;
            a.x += a.vx * dt; a.z += a.vz * dt;
            return d;
        }

        void Tick(CrowdAgent a, float dt, XZ? pl)
        {
            if (a.state == "gone") { Respawn(a, dt); return; }
            if (a.hurtT > 0 || a.downT > 0 || a.fleeT > 0) // ponytail: flees in a straight line, through walls; navmesh if it shows
            {
                a.hurtT = Math.Max(0, a.hurtT - dt);
                if (a.downT > 0) a.downT -= dt;
                if (a.hurtT > 0 || a.downT > 0) { Steer(a, a.x, a.z, 0, dt, pl); return; }
                a.fleeT -= dt;
                float fdx = a.x - a.fx, fdz = a.z - a.fz, fd = CrowdPathP.Hyp(fdx, fdz); if (fd == 0) fd = 1;
                Steer(a, a.x + (fdx / fd) * 4, a.z + (fdz / fd) * 4, 4.2f, dt, pl);
                return;
            }
            XZ? goal = null; float v = a.speed;
            if (mode == "stroll" || mode == "brisk")
            {
                if (a.state == "stand")
                {
                    a.timer -= dt;
                    if (a.timer <= 0 && (mode == "stroll" || (a.path < 0 && M.targets.Count == 0))) a.state = "move";
                }
                else if (a.path >= 0)
                {
                    if (mode == "stroll" && (a.stopT -= dt) <= 0) { a.stopT = 10 + r() * 20; if (r() < 0.3f) Stop(a, 3 + r() * 6); }
                    var g = LaneGoal(a);
                    if (CrowdPathP.Hyp(g.x - a.x, g.z - a.z) < 1.4f) a.s += a.dir * a.speed * dt * 1.15f;
                    if (a.dir > 0 ? a.s >= paths[a.path].len : a.s <= 0)
                    {
                        bool near = CrowdPathP.Hyp(g.x - a.x, g.z - a.z) < (mode == "brisk" ? 1.0f : 1.6f);
                        if (near) EndOfPath(a);
                    }
                    if (a.state == "gone") return;
                    if (a.state == "move") goal = LaneGoal(a);
                }
                else if (mode == "brisk" && M.targets.Count > 0) // straight to the nearest target
                {
                    XZ t = M.targets.Aggregate((b, q) => Dist(q.x, q.z, a.x, a.z) < Dist(b.x, b.z, a.x, a.z) ? q : b);
                    if (Dist(t.x, t.z, a.x, a.z) < 1.0f) Arrive(a, t); else goal = t;
                }
                else // wander in the area
                {
                    if (a.goal == null || Dist(a.goal.Value.x, a.goal.Value.z, a.x, a.z) < 0.6f) { if (a.goal != null) Stop(a, 2 + r() * 4); a.goal = AreaPoint(); }
                    if (a.state == "move") goal = a.goal;
                }
            }
            else if (mode == "queue")
            {
                if (a.state == "line")
                {
                    int k = line.IndexOf(a); var s = Slot(k);
                    goal = new XZ(s.x + M.dirN.z * a.jit, s.z - M.dirN.x * a.jit); v = 0.9f * a.slow;
                    a.face = Mathf.Atan2(M.dirN.x, M.dirN.z);
                }
                else if (a.state == "leave")
                {
                    goal = a.goal; v = 1.1f * a.slow;
                    if (Dist(a.goal.Value.x, a.goal.Value.z, a.x, a.z) < 0.8f) // out of the scene: rejoin at the visible tail
                    {
                        SetProps(a, new HashSet<string>(a.props.Where(nm => nm != M.carry))); a.moveAnim = "walk";
                        var s = Slot(line.Count); PlaceAt(a, s.x, s.z); a.state = "line"; line.Add(a); goal = null;
                    }
                }
            }
            if (goal != null) Steer(a, goal.Value.x, goal.Value.z, v, dt, pl);
            else Steer(a, a.x, a.z, 0, dt, pl); // standing: only step aside for neighbours and the player
        }

        // Animation, facing, placement and LOD for one member.
        void PoseAgent(CrowdAgent a, float dt = 0)
        {
            float sp = CrowdPathP.Hyp(a.vx, a.vz);
            if (sp > 0.25f) a.moving = true; else if (sp < 0.12f) a.moving = false;
            string anim = a.downT > 0 ? "down" : a.hurtT > 0 ? "hurt" : a.moving ? (a.fleeT > 0 ? "run" : a.moveAnim) : a.standAnim;
            if (anim != a.anim) { a.p.SetAnim(anim); a.anim = anim; }
            a.p.SetSpeed(a.moving ? sp : (float?)null);
            float target = a.moving ? Mathf.Atan2(-a.vx, -a.vz) : a.face ?? a.yaw;
            a.yaw = dt != 0 ? LerpAngle(a.yaw, target, Math.Min(1, dt * 5)) : target;
            J.SetPos(a.p.Group, a.x, H(a.x, a.z) + a.dy, a.z);
            J.SetYaw(a.p.Group, a.yaw);
        }

        public void Update(float dt, FrameCtx ctx = null)
        {
            dt = Math.Min(Math.Max(0, float.IsNaN(dt) ? 0 : dt), 0.1f);
            frame++;
            if (ctx?.camera != null) { viewer = J.FromUnity(ctx.camera.transform.position); hasViewer = true; }
            var pp = ctx?.player ?? player;
            XZ? pl = pp != null ? new XZ(pp.X, pp.Z) : hasViewer ? new XZ(viewer.x, viewer.z) : (XZ?)null;
            if (active == 0 || dt == 0) return;
            if (mode == "queue") QueueTick(dt);
            foreach (var a in agents)
            {
                if (!a.on) continue;
                if (mode != "sit") Tick(a, dt, pl);
                if (a.state == "gone") continue;
                PoseAgent(a, dt);
                float d2 = hasViewer ? (a.x - viewer.x) * (a.x - viewer.x) + (a.z - viewer.z) * (a.z - viewer.z) : 0;
                bool sh = d2 < LOD_SHADOW * LOD_SHADOW;
                if (sh != a.sh) { a.sh = sh; a.p.SetShadow(sh); }
                a.acc += dt;
                if (d2 < LOD_ANIM * LOD_ANIM || (frame + a.i) % 3 == 0) { a.p.Update(a.acc); a.acc = 0; }
            }
        }
    }
}
