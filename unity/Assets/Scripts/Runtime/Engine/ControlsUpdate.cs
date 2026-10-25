// engine/controls.js per frame: movement against walk zones and solids, vault / climb, the model, the camera rig,
// the flashlight and the nearest interaction point. All maths in JS space; the camera and model through J.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed partial class Controls
    {
        static readonly List<WalkZone> NO_WALK = new List<WalkZone>();
        static readonly List<Solid> NO_SOLIDS = new List<Solid>();

        // A point is behind a wall when solids block the line to it at both 0.3 m and 1.5 m (low furniture does not
        // hide it). Its own furniture is ignored: solids holding the point, and the last 0.35 m before it.
        bool Hidden(Point p, List<Solid> solids)
        {
            float dx = p.x - position.x, dz = p.z - position.z, d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < 0.4f) return false;
            float k = (d - 0.35f) / d, dy = ((p.y ?? position.y) - position.y) * k;
            foreach (var h in new[] { 0.3f, 1.5f })
            {
                var so = new Vector3(position.x, position.y + h, position.z); var sd = new Vector3(dx * k, dy, dz * k);
                bool any = false;
                foreach (var s in solids)
                    if (s != null && !s.off && !(p.x >= s.x0 && p.x <= s.x1 && p.z >= s.z0 && p.z <= s.z1) && Geom.SegBox(so, sd, s, 0) <= 1) { any = true; break; }
                if (!any) return false;
            }
            return true;
        }

        // ---- interaction: nearest enabled point within r, in front (< 75°) of the camera or the body, not behind a wall ----
        Point FindNearest(Level L)
        {
            Point best = null; float bestScore = float.PositiveInfinity;
            if (L?.points == null) return null;
            foreach (var p in L.points)
            {
                if (p == null || !p.enabled) continue;
                float dx = p.x - position.x, dz = p.z - position.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > p.r || Mathf.Abs((p.y ?? position.y) - position.y) > 2.2f) continue;
                float off = 0;
                if (d > 0.45f)
                {
                    float a = Mathf.Atan2(-dx, -dz);
                    off = Mathf.Min(Mathf.Abs(Wrap(a - camYaw)), Mathf.Abs(Wrap(a - bodyYaw)));
                    if (off > 75 * D2R) continue;
                }
                float score = d + off * 0.6f;
                if (score < bestScore && !Hidden(p, L.solids ?? NO_SOLIDS)) { bestScore = score; best = p; }
            }
            return best;
        }

        // ---- vault / climb (Space in front of a low solid) ----
        // A solid 0.3..1.5 m tall and up to 1 m thick with a free landing behind it is vaulted; anything up to
        // 2.3 m with room on top is climbed onto. Returns false when there is nothing to get over (a plain jump).
        bool Vault()
        {
            var L = getLevel(); if (L == null || !prof.jump) return false;
            float yaw = vel.magnitude > 0.3f ? Mathf.Atan2(-vel.x, -vel.y) : view == "first" ? camYaw : bodyYaw;
            float dx = -Mathf.Sin(yaw), dz = -Mathf.Cos(yaw), feet = position.y;
            var solids = new List<Solid>(); foreach (var s in L.solids ?? NO_SOLIDS) if (s != null && !s.off) solids.Add(s);
            var so = new Vector3(position.x, feet + 0.25f, position.z); var sd = new Vector3(dx, 0, dz);
            Solid best = null; float bt = float.PositiveInfinity;
            foreach (var s in solids)
            {
                float hh = s.y1 - feet; if (hh < 0.3f || hh > 2.3f || s.y0 > feet + 0.3f) continue;
                float t = Geom.SegBox(so, sd, s, Player.RADIUS * 0.6f); if (t < bt) { bt = t; best = s; }
            }
            if (best == null) return false;
            float exitT = Mathf.Min(dx > 1e-6f ? (best.x1 - position.x) / dx : dx < -1e-6f ? (best.x0 - position.x) / dx : float.PositiveInfinity,
                dz > 1e-6f ? (best.z1 - position.z) / dz : dz < -1e-6f ? (best.z0 - position.z) / dz : float.PositiveInfinity);
            float h = best.y1 - feet, thick = exitT - bt;
            var walk = L.walk ?? NO_WALK;
            bool Free(float x, float z, float y) { var (cx, cz) = Geom.Collide(x, z, y, solids); return Mathf.Sqrt((cx - x) * (cx - x) + (cz - z) * (cz - z)) < 0.05f; }
            var others = new List<Solid>(); foreach (var s in solids) if (s != best) others.Add(s);
            var surf = new List<WalkZone>(walk); surf.AddRange(Geom.TopsOf(others));
            if (h <= 1.5f && thick <= 1.0f)
            { // over it
                float lx = position.x + dx * (exitT + 0.5f), lz = position.z + dz * (exitT + 0.5f);
                var gl = Geom.GroundAt(surf, lx, lz, best.y1, false, 60);
                if (gl != null && Free(lx, lz, gl.Value.y))
                {
                    float y1 = Mathf.Max(gl.Value.y, feet - 0.4f);
                    move = new Move { t = 0, dur = 0.5f + h * 0.15f, x0 = position.x, z0 = position.z, y0 = feet, x1 = lx, z1 = lz, y1 = y1, arc = best.y1 + 0.2f - (feet + y1) / 2, clip = "vault" };
                    stamina = Mathf.Max(0, stamina - 0.08f); model?.Play(move.clip, move.dur); return true;
                }
            }
            if (thick >= 0.4f)
            { // onto it
                float k = bt + Mathf.Min(0.45f, thick / 2), lx = position.x + dx * k, lz = position.z + dz * k;
                if (!Free(lx, lz, best.y1)) return false;
                move = new Move { t = 0, dur = 0.35f + h * 0.3f, x0 = position.x, z0 = position.z, y0 = feet, x1 = lx, z1 = lz, y1 = best.y1, arc = 0.2f, clip = h > 1 ? "climb" : "vault" };
                stamina = Mathf.Max(0, stamina - 0.05f - h * 0.05f); model?.Play(move.clip, move.dur); return true;
            }
            return false;
        }

        // ponytail: JS clips the first-person body at the collar with a clipping plane (no such thing in URP Lit);
        // here the head bone is shrunk while the camera rides it (its shadow loses the head). Upgrade: a lit shader
        // with a clip plane keyword for the player's material.
        void CutHead(Transform head)
        {
            if (cutHead == head) return;
            RestoreHead();
            cutHead = head; cutHeadScale = head.localScale; head.localScale = cutHeadScale * 0.001f;
        }
        void RestoreHead() { if (cutHead != null) cutHead.localScale = cutHeadScale; cutHead = null; }

        public void Update(float dt)
        {
            Poll();
            var L = getLevel(); var walk = L?.walk ?? NO_WALK; var allSolids = L?.solids ?? NO_SOLIDS;
            // look + move input
            float kx = 0, kz = 0;
            bool alt = keys.Contains("AltLeft") || keys.Contains("AltRight");
            foreach (var c in keys)
            {
                if (!MOVE.TryGetValue(c, out var m)) continue;
                if (alt && c.StartsWith("Arrow", StringComparison.Ordinal)) { lookYaw -= m[0] * 1.9f * dt; lookPitch = Clamp(lookPitch + m[1] * 1.2f * dt, -1.25f, 1.1f); continue; }
                kx += m[0]; kz += m[1];
            }
            float len = Mathf.Sqrt(kx * kx + kz * kz); if (len > 1) { kx /= len; kz /= len; }
            // Running spends stamina (faster with a heavy bag); empty, you walk until it is back to 35 %.
            bool running = len > 0 && prof.run && !winded && enabled && !crouch && (keys.Contains("ShiftLeft") || keys.Contains("ShiftRight"));
            float use = Clamp(player.StaminaUse == 0 || float.IsNaN(player.StaminaUse) ? 1 : player.StaminaUse, 0.5f, 3);
            stamina = Clamp(stamina + (running ? -dt / 7 * use : dt / (len > 0 ? 11 : 5)), 0, 1);
            if (stamina <= 0) winded = true; else if (winded && stamina > 0.35f) winded = false;
            float speed = (running ? Player.RUN : Player.WALK) * Clamp(player.SpeedMul == 0 || float.IsNaN(player.SpeedMul) ? 1 : player.SpeedMul, 0.2f, 1.5f) * (air ? 0.9f : 1) * (crouch ? 0.4f : 1);
            float sy = Mathf.Sin(lookYaw), cy = Mathf.Cos(lookYaw);
            float tx = (-sy * kz + cy * kx) * speed, tz = (-cy * kz - sy * kx) * speed;
            float a = Damp(len > 0 ? 9 : 12, dt);
            vel.x += (tx - vel.x) * a; vel.y += (tz - vel.y) * a;
            if (Mathf.Abs(vel.x) < 1e-3f && Mathf.Abs(vel.y) < 1e-3f) vel = Vector2.zero;

            // move in sub-steps (no tunnelling through thin walls), slide along solids
            var near = new List<Solid>();
            foreach (var s in allSolids) if (s != null && !s.off && s.x0 - 3 < position.x && s.x1 + 3 > position.x && s.z0 - 3 < position.z && s.z1 + 3 > position.z) near.Add(s);
            List<WalkZone> surf = walk;
            if (near.Count > 0) { surf = new List<WalkZone>(walk); surf.AddRange(Geom.TopsOf(near)); }
            Ground? g = null; float climb = 0;
            if (move != null)
            { // scripted vault / climb: ease along, with an arc over the top
                move.t = Mathf.Min(1, move.t + dt / move.dur);
                float k = move.t, e = k * k * (3 - 2 * k);
                position = new Vector3(move.x0 + (move.x1 - move.x0) * e, move.y0 + (move.y1 - move.y0) * e + Mathf.Sin(Mathf.PI * k) * move.arc, move.z0 + (move.z1 - move.z0) * e);
                vel = Vector2.zero;
                if (k >= 1) { move = null; lastGround = peakY = position.y; air = true; vy = 0; }
            }
            else
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(vel.magnitude * dt / 0.12f));
                float px = position.x, pz = position.z;
                for (int i = 0; i < steps; i++) (px, pz) = Geom.Collide(px + vel.x * dt / steps, pz + vel.y * dt / steps, position.y, near);
                // Without stairs (wheelchair) a stair flight is a wall; the director may offer help (onBlocked).
                if (!prof.stairs && !air)
                {
                    var ng = Geom.GroundAt(walk, px, pz, position.y);
                    var here = Geom.GroundAt(walk, position.x, position.z, position.y);
                    bool stop = ng != null && ng.Value.slope > STAIR_SLOPE && !(here != null && here.Value.slope > STAIR_SLOPE);
                    if (stop) { if (!blocked) Emit(blockedFns, new Blocked { x = position.x, z = position.z, toX = px, toZ = pz }); px = position.x; pz = position.z; vel = Vector2.zero; }
                    blocked = stop;
                }
                if (dt > 0)
                { // never keep pushing into a wall
                    float ax = (px - position.x) / dt, az = (pz - position.z) / dt;
                    if (Mathf.Abs(ax) < Mathf.Abs(vel.x)) vel.x = ax;
                    if (Mathf.Abs(az) < Mathf.Abs(vel.y)) vel.y = az;
                }
                position.x = px; position.z = pz;
                g = Geom.GroundAt(surf, px, pz, position.y, false, air ? 50 : 0.6f);
                // Walked off a ledge (a floor further down, and no floor within a body width): fall.
                if (!air && g == null && Geom.GroundAt(surf, px, pz, position.y, false, 60) != null && Geom.GroundNear(surf, px, pz, position.y) == null) { air = true; vy = 0; peakY = position.y; }
                if (air)
                { // jump / fall arc; land on the highest surface under the feet
                    vy -= GRAVITY * dt; position.y += vy * dt; peakY = Mathf.Max(peakY, position.y);
                    if (g == null) g = Geom.GroundAt(surf, px, pz, position.y, false, 60);
                    if (vy <= 0 && g != null && position.y <= g.Value.y)
                    {
                        float drop = peakY - g.Value.y;
                        position.y = lastGround = g.Value.y; air = false; vy = 0;
                        if (drop > 0.35f) model?.Play("land", 0.3f + Mathf.Min(0.35f, drop * 0.06f), Mathf.Min(1, drop / 4));
                        if (drop > 1.2f) { Shake(Mathf.Min(0.35f, drop * 0.03f), 0.4f); Emit(landFns, drop); }
                    }
                    else if (g == null && position.y < lastGround - 3) { position.y = lastGround; air = false; vy = 0; } // no floor at all: never fall forever
                }
                else if (g != null)
                {
                    float gy = g.Value.y;
                    climb = gy - lastGround; lastGround = gy;
                    position.y = snap || Mathf.Abs(gy - position.y) < 0.002f ? gy : position.y + (gy - position.y) * Damp(gy > position.y ? 22 : 15, dt);
                }
            }
            float hs = vel.magnitude;

            // body faces the move direction (third person) or the camera (first person)
            if (view == "first" && viewK < 0.5f) bodyYaw = camYaw;
            else if (hs > 0.25f) bodyYaw += Wrap(Mathf.Atan2(-vel.x, -vel.y) - bodyYaw) * Damp(10, dt);

            // model: posed before the camera, which in first person rides on its head
            var head = model?.Bone("head");
            bool headDriven = head != null && prof.eye == Player.EYE;
            Vector3 headW = Vector3.zero;
            if (model?.Group != null)
            {
                J.SetPos(model.Group, position); J.SetYaw(model.Group, bodyYaw);
                bool vis = !modelHidden && (overrideFn != null ? view != "first" : headDriven || viewK > 0.35f);
                if (model.Group.activeSelf != vis) model.Group.SetActive(vis);
                airT = air && move == null ? airT + dt : 0;
                string next = hs < 0.15f ? "idle" : carrying == "water" ? "carry" : "walk"; // walk..run is one blend by speed
                if (g != null && g.Value.slope > 0.35f && hs > 0.15f && carrying != "water") next = climb >= 0 ? "stairsUp" : "stairsDown";
                if (crouch) next = "crouch";
                if (airT > 0.12f) next = "air";
                if (move != null) next = "idle"; // the vault / climb clip owns the body
                if ((actT -= dt) > 0 && actAnim != null) next = actAnim;
                if (next != anim) { anim = next; model.SetAnim(next); }
                model.SetSpeed(hs);
                // Lean into speeding up and into turns; look up/down with the upper body.
                float bf = -Mathf.Sin(bodyYaw) * vel.x - Mathf.Cos(bodyYaw) * vel.y, turn = dt > 0 ? Wrap(bodyYaw - lastYaw) / dt : 0;
                accel += ((dt > 0 ? (bf - lastFwd) / dt : 0) - accel) * Damp(8, dt); lastFwd = bf; lastYaw = bodyYaw;
                model.SetLean(RM ? 0 : Clamp(accel * 0.035f, -0.22f, 0.22f), RM ? 0 : Clamp(turn * hs * 0.035f, -0.3f, 0.3f));
                model.SetAim(camPitch * (view == "first" && camPitch < 0 ? 0.35f : 0.5f));
                if (!modelBroken) { try { model.Update(dt); } catch (Exception e) { Debug.LogError("[engine/controls] player model update failed " + e); modelBroken = true; } }
                if (headDriven)
                {
                    headW = J.FromUnity(head.position);
                    bool fp = viewK < 0.5f && overrideFn == null;
                    if (model.Mesh != null) { if (fp) CutHead(head); else RestoreHead(); }
                }
                else RestoreHead();
            }
            Rig(dt, walk, allSolids, near, hs, running, headDriven, headW);
            var n = enabled ? FindNearest(L) : null;
            if (n != nearest) { nearest = n; Emit(nearestFns, n); }
            snap = false;
        }

        // camera rig, override / blend, shake, flashlight
        void Rig(float dt, List<WalkZone> walk, List<Solid> allSolids, List<Solid> near, float hs, bool running, bool headDriven, Vector3 headW)
        {
            camYaw += Wrap(lookYaw - camYaw) * Damp(24, dt); camPitch += (lookPitch - camPitch) * Damp(24, dt);
            viewK += ((view == "third" ? 1 : 0) - viewK) * (snap || RM ? 1 : Damp(7, dt));
            if (view == "first" && viewK < 0.002f) viewK = 0;
            if (view == "third") lookPitch = Clamp(lookPitch, -0.95f, 0.6f);
            phase += hs * dt * Mathf.PI * 2 / 1.44f; // one stride (two steps) per 1.44 m
            bobAmp += ((RM ? 0 : Mathf.Min(1, hs / Player.WALK) * (running ? 1.3f : 1)) - bobAmp) * Damp(6, dt);
            float roll = Mathf.Sin(phase) * 0.004f * bobAmp * (1 - viewK);
            // The eyes follow what the body is doing (look down at what you pick up, at the wall you vault).
            lookOff += ((model?.ClipLook ?? 0) * (1 - viewK * 0.8f) - lookOff) * (RM ? 1 : Damp(14, dt));
            // Euler (pitch, yaw, roll) in YXZ order, three.js maths in JS space.
            float p = Clamp(camPitch + lookOff, -1.4f, 1.2f), sP = Mathf.Sin(p), cP = Mathf.Cos(p), sY = Mathf.Sin(camYaw), cY = Mathf.Cos(camYaw), sR = Mathf.Sin(roll), cR = Mathf.Cos(roll);
            var fwd = new Vector3(-sY * cP, sP, -cY * cP);
            var up = new Vector3(-sR * cY + cR * sP * sY, cR * cP, sR * sY + cR * sP * cY);
            var right = new Vector3(Mathf.Cos(camYaw), 0, -Mathf.Sin(camYaw));
            float eye = prof.eye * (crouch ? 0.6f : 1), tpH = TP_HEIGHT * (eye / Player.EYE);
            Vector3 want;
            if (headDriven)
            { // first person: the eyes are on the head the animation moves (bob, landing dip, vault arc)
                float fx = -Mathf.Sin(bodyYaw), fz = -Mathf.Cos(bodyYaw), k = 1 - viewK, ef = EYE_FWD + 0.08f * Mathf.Max(0, -camPitch);
                want = new Vector3(position.x + ((headW.x - position.x) * 0.6f + fx * ef) * k, (headW.y + EYE_UP) * k + (position.y + tpH) * viewK, position.z + ((headW.z - position.z) * 0.6f + fz * ef) * k);
            }
            else want = new Vector3(position.x, position.y + eye + (tpH - eye) * viewK, position.z);
            if (snap) pivot = want;
            else
            {
                float kxz = Damp(60 + (13 - 60) * viewK, dt), kyy = Damp(60 + (9 - 60) * viewK, dt);
                pivot.x += (want.x - pivot.x) * kxz; pivot.z += (want.z - pivot.z) * kxz; pivot.y += (want.y - pivot.y) * kyy;
            }
            var camPos = pivot;
            if (viewK > 0.01f)
            { // over the shoulder, pulled in by solids between the head and the camera
                var seg = right * (TP_SHOULDER * viewK) - fwd * (TP_DIST * viewK);
                float segLen = seg.magnitude, t = 1;
                foreach (var s in allSolids) if (s != null && !s.off) t = Mathf.Min(t, Geom.SegBox(pivot, seg, s, 0.18f));
                // Stair flights and floors are walk zones, not solids: stop before the camera sinks into one.
                for (int i = 1; i <= 8 && t > 0; i++)
                {
                    float k = i / 8f * t, sx = pivot.x + seg.x * k, sy = pivot.y + seg.y * k, sz = pivot.z + seg.z * k;
                    var f = Geom.GroundAt(walk, sx, sz, pivot.y + 0.35f, false, 50);
                    if (f != null && sy < f.Value.y + 0.3f) { t = (i - 1) / 8f * t; break; }
                }
                float frac = Clamp((t * segLen - 0.12f) / segLen, 0.05f, 1);
                camFrac = frac < camFrac || snap ? frac : camFrac + (frac - camFrac) * Damp(3.5f, dt);
                camPos += seg * camFrac;
                camPos.y = Mathf.Max(camPos.y, position.y + 0.3f);
            }
            float bob = headDriven ? 0 : 1 - viewK; // gentle first-person head bob (the animated head bobs by itself)
            camPos += up * (Mathf.Sin(phase * 2) * 0.028f * bobAmp * bob) + right * (Mathf.Sin(phase) * 0.018f * bobAmp * bob);
            var ct = camera.transform;
            ct.SetPositionAndRotation(J.V(camPos), Quaternion.LookRotation(J.V(fwd), J.V(up)));
            float nearZ = viewK < 0.5f ? 0.05f : 0.08f; // first person: the own chest and hands come close to the lens
            if (camera.nearClipPlane != nearZ) camera.nearClipPlane = nearZ;
            float fov = FOV + (running && hs > 3.6f && !RM ? FOV_RUN : 0);
            if (Mathf.Abs(camera.fieldOfView - fov) > 0.01f) camera.fieldOfView += (fov - camera.fieldOfView) * Damp(6, dt);

            if (overrideFn != null)
            {
                try { overrideFn(camera, dt); } catch (Exception e) { Debug.LogError("[engine/controls] camera override failed; clearing it " + e); overrideFn = null; }
            }
            else if (blend != null)
            { // ease back from a cinematic camera
                blend.k = Mathf.Min(1, blend.k + dt / 0.8f); float e = blend.k * blend.k * (3 - 2 * blend.k);
                ct.SetPositionAndRotation(Vector3.Lerp(blend.pos, ct.position, e), Quaternion.Slerp(blend.rot, ct.rotation, e));
                if (blend.k >= 1) blend = null;
            }
            if (shakeT > 0)
            { // a blast nearby: short decaying jolt (off with reduced motion)
                shakeT = Mathf.Max(0, shakeT - dt);
                float k = RM ? 0 : shakeA * (shakeT / shakeDur) * (shakeT / shakeDur);
                ct.position += new Vector3((UnityEngine.Random.value - 0.5f) * k, (UnityEngine.Random.value - 0.5f) * k * 0.6f, (UnityEngine.Random.value - 0.5f) * k);
            }

            // flashlight: at the right hand in third person, just below the camera in first person; hand-held lag
            if (flashOn)
            {
                var cpos = J.FromUnity(ct.position);
                float cb = Mathf.Cos(bodyYaw), sb = Mathf.Sin(bodyYaw);
                var handLocal = new Vector3(0.26f * cb + -0.3f * sb, 1.05f, -0.26f * sb + -0.3f * cb) + position;
                var origin = cpos + right * 0.16f + up * -0.2f + fwd * 0.32f; // ahead of the own body, which would shadow it
                origin = Vector3.Lerp(origin, handLocal, viewK);
                var tgt = cpos + fwd * 16;
                aim = snap ? tgt : Vector3.Lerp(aim, tgt, Damp(RM ? 30 : 14, dt));
                var dir = aim - origin;
                var rot = dir.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(J.V(dir)) : Quaternion.identity;
                spot.transform.SetPositionAndRotation(J.V(origin), rot);
                beam.transform.SetPositionAndRotation(J.V(origin), rot);
                // A torch held close to a wall or a person floods it: dim with the distance to whatever the beam hits.
                float hit = 1;
                foreach (var s in near) hit = Mathf.Min(hit, Geom.SegBox(origin, dir, s, 0));
                float hitD = hit * dir.magnitude, wantK = Clamp(Mathf.Pow(hitD / 4.5f, 1.5f), 0.12f, 1);
                flashK += (wantK - flashK) * Damp(8, dt);
                spot.intensity = Kit.ToUnity(FLASH * flashK);
                float str = 0.05f * beamStrength * (0.3f + 0.7f * viewK) * (0.4f + 0.6f * flashK);
                Mat.SetColor(beamMat, new Color(1 * str, 0.93f * str, 0.82f * str).gamma);
            }
        }
    }
}
