// app.js per-frame work: the phone torch draining the battery, stamina / compass / minimap and the pointer-lock
// hint, footsteps on heel strikes, the wheelchair stair help (onBlocked), the guardian who walks along.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.App
{
    public sealed partial class GameApp
    {
        static readonly Dictionary<string, string> STEP_FLOOR = new Dictionary<string, string> { ["town"] = "concrete", ["shelter"] = "concrete", ["apartment"] = "tile", ["office"] = "tile", ["mart"] = "tile", ["store"] = "tile" };

        Label lockHint;
        bool touchOnly;
        float mapT;
        int? stepN;
        bool helpBusy;
        float helpedAt = -1e9f;
        IPerson buddy;
        Level buddyLevel;
        string buddyAnim = "";

        static float NowMs => Time.realtimeSinceStartup * 1000;   // performance.now()

        void SetupFrame()
        {
            // Phone as the torch: 1 % battery per 20 s of light.
            engine.OnFrame((dt, t) =>
            {
                if (!flashOn || ctx.lightSource != "phone" || G == null) return;
                if ((flashT += dt) > 20)
                {
                    flashT = 0; G.phone.battery = Math.Max(0, G.phone.battery - 1); hud.SetBattery(G.phone.battery);
                    if (G.phone.battery <= 0) { flashOn = false; engine.Player.SetFlashlight(false); ctx.Toast("휴대폰이 꺼졌다."); }
                }
            });

            // ---- per frame: stamina, compass and minimap, the pointer-lock hint ----
            lockHint = new Label("화면을 한 번 클릭하면 마우스만 움직여 둘러볼 수 있습니다");
            lockHint.AddToClassList("lock-hint");
            // ponytail: inline stand-in for style.css .lock-hint; upgrade: a UiStyle rule owned by uiCore.
            lockHint.style.position = Position.Absolute; lockHint.style.left = 0; lockHint.style.right = 0; lockHint.style.bottom = Length.Percent(14);
            lockHint.style.unityTextAlign = TextAnchor.MiddleCenter; lockHint.style.color = new Color(1, 1, 1, 0.85f); lockHint.pickingMode = PickingMode.Ignore;
            lockHint.style.display = DisplayStyle.None;
            try { UiRoot.Root.Add(lockHint); } catch (Exception e) { Debug.LogException(e); }
            touchOnly = SystemInfo.deviceType == DeviceType.Handheld;   // matchMedia('(hover: none)')
            engine.OnFrame((dt, t) =>
            {
                if (G == null) { lockHint.style.display = DisplayStyle.None; return; }
                var p = engine.Player;
                hud.SetStamina(p.Stamina, p.Winded);
                var hidden = touchOnly || !ctx.roaming || !engine.ControlsEnabled || engine.PointerLocked;
                lockHint.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
                if ((mapT -= dt) > 0) return;
                mapT = 0.066f;
                hud.SetCompass(-p.Yaw * 180 / Mathf.PI);
                var L = engine.Level;
                hud.SetMap(settings.minimap == false || L == null ? null : new HudMap { x = p.X, y = p.Y, z = p.Z, yaw = p.Yaw, walk = L.walk, solids = L.solids, points = L.points, scale = L.id == "town" ? 2 : 9 });
            });

            // ---- footsteps: one per heel strike of the player's walk / run cycle ----
            engine.OnFrame((dt, t) =>
            {
                var m = engine.Player.Model;
                if (G == null || m == null || m.Phase == 0 || !ctx.roaming) { stepN = null; return; }
                var n = (int)Math.Floor((m.Phase - Mathf.PI / 2) / Mathf.PI);
                // ponytail: the engine exposes no controls.busy; a one-shot clip playing (m.Playing) stands in. Contract request: GameEngine.Busy.
                if (stepN != null && n != stepN && m.Stride > 0.25f && m.Anim != "air" && m.Anim != "crouch" && m.Playing == null)
                    audio.Footstep(m.Anim == "stairsUp" || m.Anim == "stairsDown" ? "stairs" : engine.Level != null && STEP_FLOOR.TryGetValue(engine.Level.id, out var f) ? f : "concrete");
                stepN = n;
            });

            engine.OnBlocked(b => Forget(OnBlocked(b), "stair help"));
            engine.OnFrame(Guardian);
        }

        // ---- wheelchair: a stair flight is a wall; ask for help to be carried across it ----
        static bool Steep(WalkZone w) => w.ramp != null && Math.Abs(w.ramp.y1 - w.ramp.y0) / (NonZero(w.ramp.axis == "x" ? w.x1 - w.x0 : w.z1 - w.z0)) > 0.2f;
        static float NonZero(float v) => v == 0 ? 1 : v;

        async Task OnBlocked(Blocked b)
        {
            if (G == null || !ctx.roaming || helpBusy || ProfileId != "wheelchair") return;
            var w = (engine.Level?.walk ?? new List<WalkZone>()).FirstOrDefault(z => Steep(z) && b.toX >= z.x0 && b.toX <= z.x1 && b.toZ >= z.z0 && b.toZ <= z.z1);
            if (w == null) return;
            helpBusy = true;
            try
            {
                var recent = NowMs - helpedAt < 90000; // the same helper keeps going down the next flights
                if (!recent)
                {
                    var pick = await ctx.Modal(() => ui.Talk(new TalkOptions { speaker = "계단", role = "휠체어로는 혼자 이용할 수 없다",
                        lines = new List<string> { "계단 앞이다. 휠체어로는 오르내릴 수 없다.", "주변 사람에게 도움을 청할까? 시간이 걸린다. 경사로나 엘리베이터가 있다면 그쪽이 낫다." },
                        choices = new List<Choice> { new Choice("help", "도움을 청한다"), new Choice("no", "다른 길을 찾는다") } }));
                    if (pick != "help") return;
                }
                var r = w.ramp; var alongX = r.axis == "x";
                float lo = alongX ? w.x0 : w.z0, hi = alongX ? w.x1 : w.z1, cur = alongX ? b.x : b.z;
                var up = Math.Abs(cur - lo) < Math.Abs(cur - hi);
                float end = up ? hi + 0.45f : lo - 0.45f, y = up ? r.y1 : r.y0, mid = alongX ? (w.z0 + w.z1) / 2 : (w.x0 + w.x1) / 2;
                var yaw = engine.Player.Yaw;
                engine.Player.Teleport(alongX ? new Spawn(end, y, mid, yaw) : new Spawn(mid, y, end, yaw));
                helpedAt = NowMs; ctx.Pass(2);
                ctx.Toast(recent ? "도움을 받아 다음 계단도 옮겼다." : "이웃 두 사람이 휠체어를 함께 들어 계단을 옮겨 주었다.", "neighbor");
            }
            finally { helpBusy = false; }
        }

        // ---- 지체 장애 mode: a guardian walks a step behind, to the right ----
        void Guardian(float dt, float t)
        {
            var on = G != null && ProfileId == "guardian" && engine.Level != null;
            if (!on) { if (buddy != null) buddy.Group.SetActive(false); return; }
            if (buddy == null)
            {
                try { buddy = engine.People.CreatePerson("crowd", 23); engine.Scene.Add(buddy.Group); }
                catch (Exception err) { Debug.LogError("[app] guardian model failed\n" + err); buddy = null; return; }
            }
            var p = engine.Player; var L = engine.Level; var g = buddy.Group;
            float s = Mathf.Sin(p.Yaw), c = Mathf.Cos(p.Yaw);
            float tx = p.X + s * 1.3f + c * 0.6f, tz = p.Z + c * 1.3f - s * 0.6f; // behind (+sin, +cos) and right (cos, -sin)
            g.SetActive(true);
            var pos = J.Pos(g);
            float dx = tx - pos.x, dz = tz - pos.z, d = Mathf.Sqrt(dx * dx + dz * dz);
            var moving = false;
            if (L != buddyLevel || d > 8) { pos = new Vector3(tx, p.Y, tz); buddyLevel = L; }
            else if (d > 0.25f) { var v = Mathf.Min(d * 2.4f, 4.2f) * dt; pos.x += dx / d * v; pos.z += dz / d * v; J.SetYaw(g, Mathf.Atan2(-dx, -dz)); moving = true; }
            var ground = Geom.GroundAt(L.walk, pos.x, pos.z, pos.y + 0.3f, false, 1.5f);
            pos.y += ((ground.HasValue ? ground.Value.y : p.Y) - pos.y) * Mathf.Min(1, dt * 12);
            J.SetPos(g, pos);
            var anim = moving ? "walk" : "idle";
            if (anim != buddyAnim) { buddyAnim = anim; buddy.SetAnim(anim); }
            buddy.SetSpeed(moving ? Mathf.Min(d * 2.4f, 4.2f) : 0);
            try { buddy.Update(dt); } catch { /* model stand-in */ }
        }
    }
}
