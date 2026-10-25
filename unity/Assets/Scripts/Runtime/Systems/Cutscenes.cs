// Ports story/cutscenes.js: the opening over 연무동, time passing over the 한빛아파트 complex, the D-day strike,
// the blackout and the air-raid siren. Each resolves 'done' or 'skipped'.
// Coordinates: town x = east, z = south, ground 0 (game/townmap.js); the 302호 floor is at y 5.75. JS space.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Levels;
using UnityEngine;

namespace Gyeongbo.Systems
{
    public static class Cutscenes
    {
        static readonly HashSet<string> NIGHT = new HashSet<string> { "night", "blackout" };

        static List<Vector3> Pts(params float[] v)
        {
            var l = new List<Vector3>();
            for (int i = 0; i + 2 < v.Length; i += 3) l.Add(new Vector3(v[i], v[i + 1], v[i + 2]));
            return l;
        }
        static CamPath Cam(float[] p, float[] t, float f0, float f1, float hand) =>
            new CamPath { p = Pts(p), t = Pts(t), fov = new[] { f0, f1 }, hand = hand };
        static List<CaptionCue> Line(float at, string text, int ms) => new List<CaptionCue> { new CaptionCue { at = at, text = text, ms = ms } };

        // Where the player was, so a cutscene shot elsewhere can put them back.
        sealed class Here { public string level; public Spawn at; }
        static Here HereOf(Ctx ctx)
        {
            var P = ctx.engine.Player; var L = ctx.engine.Level;
            return L != null ? new Here { level = L.id, at = new Spawn(P.X, P.Y, P.Z, P.Yaw) } : null;
        }
        static async Task Back(Ctx ctx, Here h) { if (h != null && ctx.engine.Level?.id != h.level) await ctx.Goto(h.level, h.at); }

        // ---- opening: the town on an ordinary Tuesday, then 정민 on his way back into the office ----
        public static async Task<string> Opening(Ctx ctx)
        {
            var cine = ctx.cine;
            ctx.outdoorEnv = "afternoon";
            ctx.SetLevelState("town", "crowd", "normal");
            await ctx.Goto("town", "office", "afternoon");
            IPerson jm = null;
            Vector3 JmAt() => jm != null ? J.Pos(jm.Group) : new Vector3(70, 0, -10);
            return await cine.Play(new List<Shot>
            {
                new Shot { dur = 9, fade = true, cam = Cam(new float[] { 175, 95, 150, 150, 78, 110, 128, 66, 72 }, new float[] { 40, 8, 10, 58, 4, -4, 78, 2, -18 }, 55, 50, 0.3f),
                    lines = Line(1.4f, "전쟁이 나기 사흘 전. 경기도 연무동의 평범한 화요일 오후였다.", 6500) },
                new Shot { dur = 6.5f, cam = Cam(new float[] { 8, 1.7f, 9, 24, 1.7f, 9.5f }, new float[] { 60, 2.2f, 3, 64, 2, 1 }, 48, 48, 0.05f),
                    lines = Line(0.6f, "퇴근길 사람들, 버스를 기다리는 학생들. 아무도 서두르지 않았다.", 5200) },
                new Shot { dur = 8.5f,
                    setup = () => { jm = cine.Actor("jeongmin", new ActorOptions { path = new List<XZ> { new XZ(64, -8.5f), new XZ(76, -13), new XZ(86, -18.8f), new XZ(87, -20.4f) }, speed = 1.45f }); return null; },
                    camFn = (k, s) => { var a = JmAt(); return new CamPose { p = new Vector3(a.x - 4.2f + k * 2.5f, 1.75f + k * 0.2f, a.z + 3.2f - k * 1.2f), t = new Vector3(a.x + 0.4f, a.y + 1.45f, a.z - 0.6f), fov = 42 }; },
                    lines = new List<CaptionCue> { new CaptionCue { at = 0.4f, text = "정민, 31세. 한결물류 연무센터에서 배송 운영을 맡고 있다.", ms = 4000 }, new CaptionCue { at = 4.4f, text = "오늘 오후부터, 배송 취소가 이상하게 많다.", ms = 3800 } } },
            }, new[] { "연무동 · 경기도", "D-3 화요일 17:30" });
        }

        // ---- time passing (stands in for the time-skip card): the sky turns over the apartment complex ----
        static string EnvAt(string label, bool power)
        {
            label = label ?? "";
            var m = Regex.Match(label, @"(\d{1,2}):(\d{2})");
            double h = m.Success ? int.Parse(m.Groups[1].Value) + int.Parse(m.Groups[2].Value) / 60.0 : label.Contains("밤") ? 22 : label.Contains("아침") ? 8 : 13;
            string e = h < 5.5 || h >= 20 ? "night" : h < 7 ? "dusk" : h < 16 ? "day" : h < 18.5 ? "afternoon" : h < 20 ? "dusk" : "night";
            return e == "night" && !power ? "blackout" : e;
        }

        // Stands in for the time-skip card; text: one caption or several, shown one after another while the sky turns.
        public static async Task<string> Timelapse(Ctx ctx, string from, string to, params string[] text)
        {
            var cine = ctx.cine; var h = HereOf(ctx); string a = EnvAt(from, ctx.power), b = EnvAt(to, ctx.power);
            await ctx.Goto("town", "home", a);
            string mid = NIGHT.Contains(a) || NIGHT.Contains(b) ? (a == b ? a : "dusk") : ctx.power ? "night" : "blackout";
            var texts = text ?? new string[0];
            float dur = 4 + texts.Length * 4.2f, step = (dur - 1.2f) / texts.Length;
            var res = await cine.Play(new List<Shot>
            {
                new Shot { dur = dur, fade = true, cam = Cam(new float[] { 26, 30, -2, 34, 33, 2 }, new float[] { 88, 18, 44, 92, 22, 40 }, 50, 46, 0.15f),
                    at = new List<TimedCue>
                    {
                        new TimedCue { at = dur * 0.15f, fn = () => ctx.engine.SetEnv(mid, false, dur * 0.3f) },
                        new TimedCue { at = dur * 0.52f, fn = () => ctx.engine.SetEnv(b, false, dur * 0.32f) },
                        new TimedCue { at = dur * 0.52f, fn = () => cine.Title(new[] { to }) },
                    },
                    lines = texts.Select((t, i) => new CaptionCue { at = 0.8f + i * step, text = t, ms = (int)Math.Round(step * 1000 - 300) }).ToList() },
            }, new[] { from });
            await Back(ctx, h);
            return res;
        }

        // ---- D-day 20:1x: a missile comes down on the villa south of the park ----
        static readonly Vector3 VILLA = new Vector3(12, 9, 78);

        // onImpact runs at the blast (the story switches the town to strike 'hit').
        public static Task<string> StrikeScene(Ctx ctx, Action onImpact)
        {
            var cine = ctx.cine; var engine = ctx.engine; var P = engine.Player;
            float px = P.X, py = P.Y, pz = P.Z;
            var V = VILLA;
            return cine.Play(new List<Shot>
            {
                new Shot { dur = 2.6f, cam = Cam(new float[] { 54, 1.75f, 21, 53.6f, 1.7f, 21.4f }, new float[] { V.x + 30, 70, V.z - 40, V.x + 4, 14, V.z - 4 }, 60, 50, 0.12f), exposure = 0.5f,
                    setup = () => { cine.Missile(new Vector3(V.x + 120, 240, V.z - 220), new Vector3(V.x, V.y, V.z), 2.5f); ctx.audio.Hit("whistle", 2.4f); return null; } },
                new Shot { dur = 4.4f, slow = 0.4f, exposure = 0.6f, cam = Cam(new float[] { 2, 3.4f, 54, 3, 3.1f, 55.5f }, new float[] { V.x, 8, V.z, V.x, 12, V.z }, 46, 40, 0.08f),
                    at = new List<TimedCue>
                    {
                        new TimedCue { at = 0.05f, fn = () => { onImpact?.Invoke(); engine.Shake(0.6f, 1.4f); ctx.Boom(V.x, V.z - 9, 2.6f); } },
                        new TimedCue { at = 0.45f, fn = () => ctx.Boom(V.x - 6, V.z - 9, 1.4f) },
                        new TimedCue { at = 0.8f, fn = () => ctx.Boom(V.x + 6, V.z - 8.5f, 1.2f) },
                    },
                    lines = Line(0.4f, "쾅— 땅이 울렸다. 공원 너머 남쪽 빌라 한 동이 무너져 내린다.", 4200) },
                new Shot { dur = 3.4f, exposure = 0.4f, cam = Cam(new float[] { px + 2.5f, py + 1.4f, pz - 2.2f, px + 2.2f, py + 1.5f, pz - 1.9f }, new float[] { px, py + 1.0f, pz, px - 3, py + 3, pz + 3 }, 50, 54, 0.06f),
                    setup = () => { var jm = cine.Actor("jeongmin", new ActorOptions { at = new XZ(P.X, P.Z), yaw = Mathf.Atan2(-(V.x - P.X), -(V.z - P.Z)) }); jm.SetAnim("crouch"); return null; },
                    lines = Line(0.3f, "불길과 검은 연기가 치솟는다. 멈추지 말고 대피소로.", 3200) },
            }, new[] { "연무동", "D-day 금요일 20:12" });
        }

        // ---- D-day 19:40: the lights go out at home, then across the complex; cut runs when they go out for good ----
        public static Task<string> BlackoutScene(Ctx ctx, Action cut)
        {
            var cine = ctx.cine; float y = ApartmentParts.FL;
            void Flick(bool on) { ctx.SetLevelState("apartment", "power", on ? "on" : "off"); ctx.engine.SetPower(on); }   // the lights stutter first
            return cine.Play(new List<Shot>
            {
                new Shot { dur = 3.2f, cam = Cam(new float[] { -1.0f, y + 1.75f, 3.4f, -1.15f, y + 1.72f, 3.8f }, new float[] { -2.3f, y + 1.1f, 8.6f, -2.4f, y + 1.05f, 8.6f }, 60, 57, 0.03f),
                    at = new List<TimedCue>
                    {
                        new TimedCue { at = 1.4f, fn = () => Flick(false) }, new TimedCue { at = 1.55f, fn = () => Flick(true) },
                        new TimedCue { at = 1.8f, fn = () => Flick(false) }, new TimedCue { at = 1.9f, fn = () => Flick(true) },
                        new TimedCue { at = 2.3f, fn = cut },
                    } },
                new Shot { dur = 3.6f, cam = Cam(new float[] { -1.6f, y + 1.6f, 5.2f, -2.0f, y + 1.55f, 7.0f }, new float[] { -2.4f, y + 1.4f, 12, -2.4f, y + 1.6f, 14 }, 52, 48, 0.04f),
                    lines = Line(0.2f, "정전 — 집 안의 불이 한꺼번에 꺼졌다. 창밖 건너편 동도 캄캄하다.", 3400) },
            }, new[] { "한빛아파트 103동 302호", "D-day 금요일 19:40" });
        }

        // ---- D-day 20:05: the siren over the dark complex ----
        public static async Task<string> SirenScene(Ctx ctx)
        {
            var cine = ctx.cine; var h = HereOf(ctx);
            IPerson lead = null;
            await ctx.Goto("town", "home");
            var res = await cine.Play(new List<Shot>
            {
                new Shot { dur = 6.5f, fade = true, exposure = 1.2f, cam = Cam(new float[] { 50, 1.5f, 12.5f, 49, 1.55f, 11.5f }, new float[] { 64, 1.5f, 15, 38, 1.4f, 9 }, 50, 50, 0.12f),
                    setup = () =>   // residents of the complex hurry out of the gate toward the shelter, phone torches on
                    {
                        var who = new (string preset, float delay, float speed, string[] props)[]
                        {
                            ("crowd", 0, 1.9f, new[] { "flashlight" }), ("crowd", 0.5f, 2.4f, new string[0]), ("elder", 1.1f, 1.2f, new string[0]),
                            ("crowd", 1.6f, 2.6f, new[] { "backpack", "flashlight" }), ("crowd", 2.4f, 2.1f, new[] { "backpack" }),
                        };
                        lead = null;
                        for (int i = 0; i < who.Length; i++)
                        {
                            var w = who[i];
                            var a = cine.Actor(w.preset, new ActorOptions
                            {
                                path = new List<XZ> { new XZ(66 + i * 0.6f, 16 + (i % 2) * 1.2f), new XZ(56, 11 + (i % 3) * 0.7f), new XZ(30, 9 + (i % 2)) },
                                speed = w.speed, delay = w.delay, props = w.props.ToList(),
                            });
                            if (lead == null) lead = a;
                        }
                        return null;
                    },
                    fill = new ShotFill { follow = () => { if (lead == null) return null; var g = J.Pos(lead.Group); return new Vector3(g.x - 1.5f, 2.6f, g.z + 1.5f); }, i = 9, color = J.Css("#e8ecff") },   // phone torches
                    lines = Line(0.5f, "20:05 — 공습경보. 사이렌이 오르내리며 1분 동안 울린다.", 5200) },
                new Shot { dur = 5, exposure = 1.1f, cam = Cam(new float[] { 40, 2, -8, 46, 2.2f, -6 }, new float[] { -20, 6, 30, -30, 10, 34 }, 46, 44, 0.08f),
                    lines = Line(0.4f, "어두운 거리로 사람들이 쏟아져 나온다. 다들 지하로 향한다.", 4200) },
            }, new[] { "연무동", "D-day 금요일 20:05" });
            await Back(ctx, h);
            return res;
        }
    }
}
