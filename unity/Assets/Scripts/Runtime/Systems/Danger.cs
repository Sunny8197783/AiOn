// Ports story/danger.js: war in the streets while roaming the town after the war started — shells (whistle,
// blast, debris, crater, fire), collapsing facades, desperate people demanding food. Intensity per chapter.
// Crouching (C) and cover between you and a blast cut the damage. Rubble piles are solids you can climb over.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Systems.SysUtil;

namespace Gyeongbo.Systems
{
    public sealed class Danger
    {
        // shell / collapse / looter: chance weights 0..1 per chapter (null = peace).
        sealed class Weights { public float shell, collapse, looter; }
        static readonly Dictionary<string, Func<Game, Weights>> INTENSITY = new Dictionary<string, Func<Game, Weights>>
        {
            { "dday", G => G.know.strikeSeen ? new Weights { shell = 0.8f, collapse = 0.5f, looter = 0 } : null },
            { "endure", G => new Weights { shell = 0.3f, collapse = 0.3f, looter = 0.7f } },
            { "crossroads", G => new Weights { shell = 0.6f, collapse = 0.5f, looter = 0.5f } },
            { "evac", G => new Weights { shell = 0.6f, collapse = 0.5f, looter = 0.6f } },
        };
        static readonly string[] FOOD = { "water_2l", "water_500", "canned", "bars", "rice_instant", "ramen" };
        static readonly string[][] LOOTER_LINES =
        {
            new[] { "야, 거기. 가방 내려놔.", "물이랑 먹을 거 있지? 다 알아. 순순히 내놔." },
            new[] { "애들이 사흘째 굶었어요…", "먹을 거 좀 나눠 줘요. 안 주면… 나도 어쩔 수 없어." },
        };

        sealed class Puff { public Vector3 pos; public Color color; public float life, age, vx, vy, vz, s0 = 1, s1 = 1, o, size, opacity; }
        sealed class Burning { public IFire fire; public ISmoke smoke; public float x, z, t; public Level L; public float? burnT; }
        sealed class Pending { public float t; public Action run; }
        sealed class Looter { public IPerson p; public Level L; public int hp; public string state; public float hitT; public int talk; public bool down; public float? gone; }

        readonly Ctx ctx; readonly GameEngine engine; readonly IAudio audio; readonly Actions actions; readonly Action<float, string> hurt;
        readonly Light flash; readonly float flashUnit;
        readonly Material craterMat, rubbleMat, ballMat, ringMat;
        readonly Mesh craterMesh;
        readonly GameObject ball, ring;
        readonly Puff[] puffs = new Puff[48];
        readonly FxBillboards puffSprites;
        readonly List<Burning> burning = new List<Burning>();
        readonly List<Pending> pending = new List<Pending>();
        int puffI;
        float shellT = 20, collapseT = 60, looterT = 45, flashK = 0, ballSize = 1;
        Looter looter;

        // hurt(damage, why) is the app's: it lowers health and records the death cause.
        Danger(Ctx ctx, Actions actions, Action<float, string> hurt)
        {
            this.ctx = ctx; engine = ctx.engine; audio = ctx.audio; this.actions = actions; this.hurt = hurt;
            var scene = engine.Scene;
            // One flash light, in the scene from the start.
            flash = PointLight(engine.Kit, J.Hex(0xffb070), 40, 2, out flashUnit); flash.name = "danger-flash";
            scene.Add(flash.gameObject); J.SetPos(flash.gameObject, 0, -500, 0);
            craterMat = Mat.Unlit(new MatSpec { color = J.Hex(0x0b0a09), transparent = true, opacity = 0.75f, depthWrite = false, polygonOffset = true, polygonOffsetFactor = -2 });
            craterMesh = Geo.Circle(1, 20).RotateX(-Mathf.PI / 2).ToMesh("crater");
            try { rubbleMat = engine.Kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0x8d8780) }); } catch (Exception) { rubbleMat = null; }
            if (rubbleMat == null) rubbleMat = Mat.Lit(new MatSpec { color = J.Hex(0x8a847c), roughness = 1 });
            // The fireball: one additive sphere, reused, also in the scene from the start.
            ballMat = Mat.Unlit(new MatSpec { color = J.Hex(0xffa040), transparent = true, opacity = 0, blending = "additive", depthWrite = false, fog = false });
            ball = Geo.Obj(Geo.Sphere(1, 16, 12), ballMat, "danger-ball", false, false); scene.Add(ball); J.SetPos(ball, 0, -500, 0);
            // The shock ring: dust thrown out along the ground.
            ringMat = Mat.Unlit(new MatSpec { color = J.Hex(0xcdbca4), transparent = true, opacity = 0, depthWrite = false, side = "double" });
            ring = Geo.Obj(Geo.Ring(0.7f, 1, 40).RotateX(-Mathf.PI / 2), ringMat, "danger-ring", false, false); scene.Add(ring); J.SetPos(ring, 0, -500, 0);
            // Smoke puffs: a pool of soft grey sprites that billow up from a blast or a collapse.
            var c = new Paint(64, 64); var r = new PaintGradient(32, 32, 2, 32, 32, 31);
            r.addColorStop(0, "rgba(255,255,255,0.9)"); r.addColorStop(0.5f, "rgba(255,255,255,0.45)"); r.addColorStop(1, "rgba(255,255,255,0)");
            c.fillStyle = r; c.fillRect(0, 0, 64, 64);
            var puffTex = c.ToTexture(true, true); puffTex.wrapMode = TextureWrapMode.Clamp;
            puffSprites = new FxBillboards(puffs.Length, Mat.Unlit(new MatSpec { map = puffTex, transparent = true, depthWrite = false, vertexColors = true }), 1, "danger-puffs") { sort = true };
            scene.Add(puffSprites.obj);
            for (int i = 0; i < puffs.Length; i++) puffs[i] = new Puff { pos = new Vector3(0, -500, 0), color = J.Hex(0x3a3532) };
            engine.OnFrame(Frame);
        }

        public static Danger Create(Ctx ctx, Actions actions, Action<float, string> hurt) => new Danger(ctx, actions, hurt);

        void Billow(float x, float y, float z, int n, int color = 0x2e2a28, float up = 1.5f, float size = 8, float life = 20)
        {
            for (int i = 0; i < n; i++)
            {
                var p = puffs[puffI++ % puffs.Length];
                p.life = life * Rnd(0.6f, 1); p.age = 0; p.vx = Rnd(-0.8f, 0.8f); p.vy = up * Rnd(0.5f, 1.2f); p.vz = Rnd(-0.8f, 0.8f);
                p.s0 = Rnd(1.5f, 3); p.s1 = size * Rnd(0.7f, 1.3f); p.o = Rnd(0.55f, 0.85f);
                p.color = J.Hex(color); p.pos = new Vector3(x + Rnd(-1, 1), y + Rnd(0.3f, 2), z + Rnd(-1, 1));
            }
        }

        Level level => engine.Level;
        Weights Intensity() { var G = ctx.G; return G != null && G.chapter != null && INTENSITY.TryGetValue(G.chapter, out var f) ? f(G) : null; }
        bool Active() => ctx.roaming && engine.ControlsEnabled && level?.id == "town";
        void Later(float t, Action run) => pending.Add(new Pending { t = t, run = run });
        float Dist(float x, float z) => Hypot(engine.Player.X - x, engine.Player.Z - z);

        // Something solid between the blast and the body (sampled at 1 m height) takes most of it.
        bool Covered(float x, float z)
        {
            var P = engine.Player; int n = Mathf.CeilToInt(Dist(x, z) / 0.5f);
            for (int i = 1; i < n; i++)
            {
                float k = (float)i / n, sx = x + (P.X - x) * k, sz = z + (P.Z - z) * k, sy = P.Y + 1;
                if ((level.solids ?? new List<Solid>()).Any(s => s != null && !s.off && sx > s.x0 && sx < s.x1 && sz > s.z0 && sz < s.z1 && sy > s.y0 && sy < s.y1)) return true;
            }
            return false;
        }

        int Damage(float x, float z, float[] radii, string why)
        {
            float d = Dist(x, z);
            int bas = d < radii[0] ? 85 : d < radii[1] ? 40 : d < radii[2] ? 15 : 0;
            if (bas == 0) return 0;
            int n = JsRoundI(bas * (engine.Crouching ? 0.4 : 1) * (Covered(x, z) ? 0.3 : 1));
            hurt(n, why);
            return n;
        }

        void Ignite(float x, float y, float z, float seconds)
        {
            var fx = engine.Fx;
            try
            {
                var fire = fx.Fire(new FireOptions { x = x, y = y, z = z, scale = 1.6f, light = 0 });
                var smoke = fx.Smoke(new SmokeOptions { x = x, y = y, z = z, ceiling = 22, spread = 7, radius = 0.9f, count = 90, density = 1, color = J.Hex(0x2a2725) });
                level.group.Add(fire.Group, smoke.Group);
                burning.Add(new Burning { fire = fire, smoke = smoke, x = x, z = z, t = seconds, L = level });
            }
            catch (Exception err) { Debug.LogError("[danger] fire fx failed " + err); }
        }

        // ---- shells ----
        void Shell(Weights I)
        {
            var P = engine.Player; bool close = Rand() < 0.18f * I.shell;
            // mostly in front of the camera, so it is seen; yaw 0 looks toward -z
            float a = Rand() < 0.75f ? P.Yaw + Rnd(-0.9f, 0.9f) : Rand() * Mathf.PI * 2, r = close ? Rnd(3, 8) : Rnd(9, 32);
            float x = P.X - Mathf.Sin(a) * r, z = P.Z - Mathf.Cos(a) * r;
            audio.Hit("whistle", 2.3f);
            ctx.Toast(close ? "휘이이익— 바로 머리 위다! 엎드려! (C)" : "휘이익— 포탄 낙하음!", "site", 2600);
            Later(2.3f, () => Blast(x, z));
        }

        void Blast(float x, float z, bool harm = true, float size = 1)   // harm false: the sight and sound only (cutscenes)
        {
            var L = level; if (L == null || L.id != "town") return;
            float y = (Geom.GroundAt(L.walk, x, z, 0.5f, true) ?? new Ground { y = 0 }).y;
            float d = Dist(x, z);
            audio.Boom(Mathf.Min(1, Mathf.Max(0.3f, d / 40)));
            engine.Shake(Mathf.Max(0.04f, 0.6f - d * 0.02f), 1.2f);
            J.SetPos(flash.gameObject, x, y + 2 * size, z); flashK = 1; ballSize = size; J.SetPos(ball, x, y + 1.2f * size, z); J.SetPos(ring, x, y + 0.15f, z);
            var c = Geo.Obj(craterMesh, craterMat, "crater", false, false); c.transform.localScale = Vector3.one * Rnd(1.4f, 2.2f); J.SetPos(c, x, y + 0.03f, z); L.group.Add(c);
            for (int i = 0; i < 8; i++) { float a = Rand() * 6.28f, v = Rnd(3, 8); actions.Spawn(Rand() < 0.5f ? "brick" : "rock", x, y + 0.4f, z, Mathf.Cos(a) * v, Rnd(4, 9), Mathf.Sin(a) * v); }
            Billow(x, y, z, JsRoundI(14 * Mathf.Min(2, size)), size: 8 * size);
            if (Rand() < 0.6f) Ignite(x, y, z, Rnd(25, 50));   // often a fire that keeps burning
            if (!harm) return;
            int n = Damage(x, z, new float[] { 4, 9, 16 }, "blast");
            if (n >= 30) ctx.Toast("폭풍에 날아가 바닥을 굴렀다. 귀가 먹먹하다.", "self", 4200);
            else if (n > 0) ctx.Toast("파편이 스쳤다. 피가 흐른다.", "self", 3600);
            ctx.Stat("tension", 8);
            ctx.Event("blast", Details.Of("near", JsRoundI(d), "dmg", n));
        }

        // ---- collapse: the nearest damaged facade sheds a rubble pile onto its sidewalk ----
        void Collapse()
        {
            var P = engine.Player;
            var best = TownMap.BUILDINGS.Where(b => b.floors >= 2).Select(b =>
            {
                float x = Mathf.Min(Mathf.Max(P.X, (float)b.x0 + 3), (float)b.x1 - 3), z = Mathf.Min(Mathf.Max(P.Z, (float)b.z0 + 3), (float)b.z1 - 3);
                return (b, d: Hypot(P.X - x, P.Z - z));
            }).Where(o => o.d > 5 && o.d < 35).OrderBy(o => o.d).ToList();
            if (best.Count == 0) return;
            var B = best[0].b;
            float bx0 = (float)B.x0, bx1 = (float)B.x1, bz0 = (float)B.z0, bz1 = (float)B.z1;
            // the face toward the player
            var faces = new List<(string f, float v)> { ("n", bz0 - P.Z), ("s", P.Z - bz1), ("w", bx0 - P.X), ("e", P.X - bx1) }.OrderByDescending(o => o.v).ToList();
            string f = faces[0].f; bool along = f == "n" || f == "s";
            float cx = along ? Mathf.Min(Mathf.Max(P.X, bx0 + 3), bx1 - 3) : f == "w" ? bx0 - 2 : bx1 + 2;
            float cz = along ? (f == "n" ? bz0 - 2 : bz1 + 2) : Mathf.Min(Mathf.Max(P.Z, bz0 + 3), bz1 - 3);
            // never on a door or anything else you interact with: slide along the face, or give up
            bool Blocks(float x, float z) => (level.points ?? new List<Point>()).Any(q => Hypot(q.x - x, q.z - z) < 6);
            foreach (var off in new float[] { 0, 8, -8, 14, -14 })
            {
                float tx = along ? cx + off : cx, tz = along ? cz : cz + off;
                if (!Blocks(tx, tz) && (along ? tx > bx0 + 2 && tx < bx1 - 2 : tz > bz0 + 2 && tz < bz1 - 2)) { cx = tx; cz = tz; break; }
                if (off == -14) return;
            }
            audio.Hit("collapse");
            ctx.Toast("우르르— 금 간 건물 벽에서 콘크리트가 떨어지기 시작한다! 벽에서 떨어져!", "site", 3400);
            engine.Shake(0.08f, 2.5f);
            Later(3, () =>
            {
                var L = level; if (L == null || L.id != "town") return;
                float W = along ? 6 : 4, D = along ? 4 : 6, h = 1.4f;
                var list = new List<Xform>();
                for (int i = 0; i < 60; i++)   // a heap: bigger, higher chunks toward the middle
                {
                    float u = Rnd(-1, 1), v = Rnd(-1, 1), k = 1 - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)), s = Rnd(0.4f, 1.2f);
                    list.Add(new Xform { x = cx + (u * W) / 2, y = k * h * Rnd(0.5f, 1), z = cz + (v * D) / 2, rx = Rnd(0, 3), ry = Rnd(0, 3), rz = Rnd(0, 3), sx = s * Rnd(0.8f, 1.6f), sy = s * 0.55f, sz = s });
                }
                var pile = engine.Kit.Instanced(Geo.Box(1, 1, 1), rubbleMat, list, true, true);
                L.group.Add(pile);
                // ponytail: runtime-only rubble (not in the save); put it in levelStates if it should outlive a reload
                L.solids.Add(new Solid(cx - W / 2, cx + W / 2, cz - D / 2, cz + D / 2, -0.5f, h, "rubble"));
                audio.Boom(0.5f); engine.Shake(0.25f, 1);
                Billow(cx, 0, cz, 12, 0xb3a998, 1.2f, 9, 12);   // the dust cloud
                var P2 = engine.Player;
                if (Mathf.Abs(P2.X - cx) < W / 2 + 0.4f && Mathf.Abs(P2.Z - cz) < D / 2 + 0.4f)
                {
                    hurt(engine.Crouching ? 30 : 55, "debris"); P2.Teleport(new Spawn(P2.X, null, cz + (P2.Z > cz ? 1 : -1) * (D / 2 + 0.6f), P2.Yaw));
                    ctx.Toast("쏟아진 잔해에 깔릴 뻔했다. 온몸이 욱신거린다.", "self", 4200);
                }
                ctx.Event("collapse", Details.Of("x", JsRoundI(cx), "z", JsRoundI(cz)));
            });
        }

        // ---- a looter: walks up, asks; give, refuse (a fight) or run ----
        void SpawnLooter()
        {
            var P = engine.Player; var L = level;
            float a = P.Yaw + Mathf.PI + Rnd(-0.8f, 0.8f), x = P.X - Mathf.Sin(a) * 14, z = P.Z - Mathf.Cos(a) * 14;
            var g = Geom.GroundAt(L.walk, x, z, 0.5f, true); if (!g.HasValue) return;
            var p = engine.People.CreatePerson("crowd", 700 + Mathf.FloorToInt(Rand() * 99));
            J.SetPos(p.Group, x, g.Value.y, z); L.group.Add(p.Group); p.Hostile = true;
            looter = new Looter { p = p, L = L, hp = 4, state = "approach", hitT = 0, talk = Mathf.FloorToInt(Rand() * LOOTER_LINES.Length), down = false };
            p.OnHit = (fx, fz, ko) =>   // the player hits back: actions calls this instead of its own reaction
            {
                if (looter == null || looter.p != p || looter.down) return;
                looter.hp -= ko ? 2 : 1; looter.state = "fight"; p.SetAnim("hurt"); looter.hitT = 0.9f;
                if (looter.hp <= 0) { looter.down = true; looter.state = "down"; p.SetAnim("down"); DropLoot(p); }
            };
            p.SetAnim("walk");
        }

        void DropLoot(IPerson p)
        {
            var w = J.Pos(p.Group);
            actions.Spawn("can", w.x, w.y + 0.5f, w.z, Rnd(-1, 1), 2, Rnd(-1, 1));
            var G = ctx.G;
            if (!G.items.TryGetValue("canned", out var it) || it == null) G.items["canned"] = it = new ItemEntry { home = 0, bag = 0, bought = 0, @checked = false, used = 0 };
            it.bag += 1;
            ctx.Toast("상대가 쓰러졌다. 떨어뜨린 통조림 하나를 챙겼다.", "self", 4200);
            ctx.Event("looter", Details.Of("result", "fought"));
        }

        async Task Confront()
        {
            looter.state = "talking";
            var G = ctx.G; var have = FOOD.Where(id => G.items.TryGetValue(id, out var e) && e != null && e.bag > 0).ToList();
            looter.p.SetAnim("talk"); looter.p.LookAt(engine.Player.X, engine.Player.Z);
            var choices = new List<Choice>();
            if (have.Count > 0) choices.Add(new Choice("give", "가방에서 먹을 것을 내준다"));
            choices.Add(new Choice("refuse", "거절한다")); choices.Add(new Choice("run", "등을 돌려 뛴다"));
            string pick = await ctx.Say("낯선 사람", LOOTER_LINES[looter.talk], choices, "눈빛이 심상치 않다");
            if (looter == null) return;
            if (pick == "give")
            {
                foreach (var id in have.Take(2)) G.items[id].bag -= 1;
                ctx.Toast("먹을 것을 건넸다. 그는 말없이 골목으로 사라졌다.", "self", 4200);
                ctx.Event("looter", Details.Of("result", "gave")); G.karma = (G.karma ?? 0) + 1;
                looter.state = "leave";
            }
            else
            {
                looter.state = pick == "run" ? "chase" : "fight";
                if (pick == "refuse") ctx.Toast("“좋게 말할 때 내놨어야지.” 그가 주먹을 쥔다. (클릭/Q로 맞서기, 멀리 뛰면 포기한다)", "site", 5000);
            }
        }

        void LooterTick(float dt)
        {
            var p = looter.p; var L = looter.L; var P = engine.Player; var w = J.Pos(p.Group);
            if (level != L) { DropLooter(); return; }
            float dx = P.X - w.x, dz = P.Z - w.z, d = Hypot(dx, dz);
            void Step(float v, bool away = false)
            {
                float k = (away ? -1 : 1) * v * dt / (d != 0 ? d : 1); w.x += dx * k; w.z += dz * k;
                var g = Geom.GroundAt(L.walk, w.x, w.z, w.y + 0.3f, false, 2); if (g.HasValue) w.y = g.Value.y;
                J.SetPos(p.Group, w);
                J.SetYaw(p.Group, Mathf.Atan2(away ? dx : -dx, away ? dz : -dz)); p.SetSpeed(v);
            }
            looter.hitT = Mathf.Max(0, looter.hitT - dt);
            if (looter.state == "down") { looter.gone = (looter.gone ?? 15) - dt; if (looter.gone <= 0) DropLooter(); return; }
            if (looter.state == "leave") { if (p.Anim != "walk") p.SetAnim("walk"); Step(1.4f, true); if (d > 30) DropLooter(); return; }
            if (looter.state == "approach") { if (d < 2.6f) _ = Forget(Confront(), "danger"); else { if (p.Anim != "walk") p.SetAnim("walk"); Step(1.6f); } return; }
            if (looter.state == "fight" || looter.state == "chase")
            {
                if (d > 22) { ctx.Toast("뒤쫓던 사람이 포기하고 멈춰 섰다.", "self"); looter.state = "leave"; ctx.Event("looter", Details.Of("result", "escaped")); return; }
                if (looter.hitT > 0) return;
                if (d > 1.1f) { if (p.Anim != "run") p.SetAnim("run"); Step(looter.state == "chase" ? 4.4f : 3.4f); return; }
                p.SetAnim("punch"); p.LookAt(P.X, P.Z); looter.hitT = 1.3f; audio.Hit("punch"); engine.Act("hurt", 0.35f); engine.Shake(0.1f, 0.3f);
                hurt(JsRoundI(Rnd(7, 12)), "fight");
            }
        }

        // JS removeFromParent; a detached GameObject would still render in Unity, so it goes for good.
        void DropLooter()
        {
            if (looter?.p?.Group != null) { engine.Persons.Remove(looter.p); UnityEngine.Object.Destroy(looter.p.Group); }
            looter = null;
        }

        void Frame(float dt, float t)
        {
            // effects keep running (fires burn down) even while a menu is open
            if (flashK > 0)
            {
                flashK = Mathf.Max(0, flashK - dt * 2); flash.intensity = 900 * ballSize * flashK * flashK * flashUnit;
                ball.transform.localScale = Vector3.one * ((2 + (1 - flashK) * 7) * ballSize); Mat.SetOpacity(ballMat, Mathf.Min(1, flashK * flashK * 1.6f));
                ring.transform.localScale = Vector3.one * ((1 + (1 - flashK) * 16) * ballSize); Mat.SetOpacity(ringMat, 0.55f * flashK);
            }
            puffSprites.Begin();
            foreach (var p in puffs)
            {
                if (p.age < p.life)
                {
                    p.age += dt; float k = p.age / p.life;
                    p.pos.x += p.vx * dt; p.pos.y += p.vy * dt * (1 - k * 0.7f); p.pos.z += p.vz * dt;
                    p.size = p.s0 + (p.s1 - p.s0) * Mathf.Sqrt(k); p.opacity = p.o * Mathf.Min(1, p.age * 3) * (1 - k);
                    if (p.age >= p.life) { p.opacity = 0; p.pos.y = -500; }
                }
                if (p.opacity > 0) { var c = p.color.linear; puffSprites.Push(p.pos, p.size, 0, 0, c.r, c.g, c.b, p.opacity); }
            }
            puffSprites.End();
            for (int i = burning.Count - 1; i >= 0; i--)
            {
                var b = burning[i]; b.t -= dt;
                try { b.fire.SetLevel(Mathf.Min(1, b.t / 10)); b.smoke.SetDensity(Mathf.Min(1, Mathf.Max(0, b.t + 12) / 15)); b.fire.Update(dt, engine.Camera); b.smoke.Update(dt, engine.Camera); } catch { /* fx stand-in */ }
                if (b.t <= -12) { b.fire.Dispose(); b.smoke.Dispose(); burning.RemoveAt(i); continue; }
                if (b.t > 3 && b.L == level && Active() && Dist(b.x, b.z) < 1.3f)
                {
                    b.burnT = (b.burnT ?? 0) - dt;
                    if (b.burnT <= 0) { b.burnT = 0.5f; hurt(4, "fire"); ctx.Toast("뜨겁다! 불길에서 떨어져!", "self", 1500); }
                }
            }
            if (!Active()) return;
            for (int i = pending.Count - 1; i >= 0; i--) if ((pending[i].t -= dt) <= 0) { var run = pending[i].run; pending.RemoveAt(i); run(); }
            if (looter != null) LooterTick(dt);
            var I = Intensity(); if (I == null) return;
            if (I.shell != 0 && (shellT -= dt * I.shell) <= 0) { shellT = Rnd(25, 60); Shell(I); }
            if (I.collapse != 0 && (collapseT -= dt * I.collapse) <= 0) { collapseT = Rnd(70, 140); Collapse(); }
            if (I.looter != 0 && looter == null && (looterT -= dt * I.looter) <= 0) { looterT = Rnd(90, 180); SpawnLooter(); }
        }

        // engine/actions.js hit someone: a looter's hits are self-defence (not counted as violence).
        public bool IsHostile(IPerson p) => p != null && p.Hostile;

        // Force an event now: kind 'blast' | 'boom' (harmless, size) | 'shell' | 'collapse' | 'looter'. JS space.
        public void Trigger(string kind, float x, float z, float size = 1)
        {
            var I = Intensity() ?? new Weights { shell = 1 };
            if (kind == "blast") Blast(x, z);
            else if (kind == "boom") Blast(x, z, false, size);
            else if (kind == "shell") Shell(I);
            else if (kind == "collapse") Collapse();
            else if (kind == "looter" && looter == null) SpawnLooter();
        }

        public void Reset() { DropLooter(); pending.Clear(); shellT = 20; collapseT = 60; looterT = 45; }
    }
}
