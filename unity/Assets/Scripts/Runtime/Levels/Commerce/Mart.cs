// Ports levels/mart.js: 연무마트 ground floor — aisles per MART_SECTIONS, checkouts with cashiers and a queue,
// shoppers with carts, the water pallet, the chilled wall, produce, the travelator, the automatic doors.
// Frame: floor x -16..16, z -24..0, entrance on the south wall (z = 0), yaw 0 faces north (-z).
// States: crowd (normal|busy) soldOut (section id lists) power (on|off) outside (null | mood).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Levels.CommerceGoods;

namespace Gyeongbo.Levels
{
    public static class Mart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("mart", BuildMart);

        const float H = 3.6f, PI = Mathf.PI;
        internal static readonly float[] AISLES = { -14.26f, -10.9f, -7.5f, -4.1f, -0.7f, 2.7f, 6.2f };
        internal const float FRONT = -7.4f, BACK = -21.3f;
        static readonly float[] GX = { -12.6f, -9.2f, -5.8f, -2.4f, 1.0f, 4.4f };
        static readonly float[] LANES = { 1.6f, 5.0f, 8.4f };
        // section: [colour, aisle, point id]
        static readonly (string sec, string color, int a, string id)[] SEC = { ("water", "#1f6fb2", 0, "shelfWater"), ("food", "#d9682b", 1, "shelfFood"), ("daily", "#2f8a57", 2, "shelfDaily"), ("elec", "#3b3f46", 3, "shelfElec"), ("health", "#1d8a8a", 4, "shelfHealth") };
        static bool IsSec(string id) => SEC.Any(s => s.sec == id);
        // Gondola faces west (neg) / east (pos): [section, product pool].
        static readonly (string sec, string[] pool)[][] FACES =
        {
            new[] { ("water", new[] { "pack6", "water5", "drink", "can" }), ("food", new[] { "ramen", "cup", "ramen", "soup" }) },
            new[] { ("food", new[] { "rice", "tuna", "ham", "bar", "cracker" }), ("daily", new[] { "tissue", "wipes", "towel", "bags", "foil" }) },
            new[] { ("daily", new[] { "stove", "butane", "tape", "rain", "jug", "whistle" }), ("elec", new[] { "aa", "flash", "radio", "power" }) },
            new[] { ("elec", new[] { "candle", "aa", "power", "flash", "radio" }), ("health", new[] { "mask", "aid", "band", "sanit" }) },
            new[] { ("health", new[] { "sanit", "mask", "band", "aid" }), ("misc", new[] { "deter", "soap", "mix", "cereal", "oil" }) },
            new[] { ("misc", new[] { "chips", "cookie", "gim", "chips" }), ("misc", new[] { "cereal", "mix", "cookie", "chips", "gim" }) },
        };
        static List<string> Pick(string[] pool, int lvl)
        {
            var l = new List<string> { pool[(lvl * 2) % pool.Length], pool[(lvl * 2 + 1) % pool.Length] };
            if (lvl % 2 == 1) l.Add(pool[(lvl + 3) % pool.Length]);
            return l;
        }

        public static Task<Level> BuildMart(Kit kit, IPeople people, IFx fx)
        {
            try { return Task.FromResult(Build(kit, people)); }
            catch (Exception e) { return Task.FromException<Level>(e); }
        }

        static Level Build(Kit kit, IPeople people)
        {
            var group = J.Group("mart");
            var stat = J.Group("stat", group); var dyn = J.Group("dyn", group);
            var solids = new List<Solid>(); var r = Kit.Rng(77);
            var goods = CreateGoods(kit);
            Material wall = kit.Surface("plaster"), floor = kit.Surface("tile", new SurfaceOpts { roughness = 0.22f }), steel = kit.Mat("steel"), dark = kit.Mat("darkMetal"),
                glass = kit.Mat("glass"), white = kit.Mat("paint:#ece9e2"), green = kit.Mat("paint:#2f8a57"), wood = kit.Surface("wood"),
                top = kit.Mat("paint:#3a3c40", new MatSpec { roughness = 0.3f }), clad = kit.Mat("paint:#b9bcc0", new MatSpec { metalness = 0.6f, roughness = 0.35f }), rubber = kit.Mat("rubber");
            GameObject add(GameObject o, bool solid = false, float pad = 0.04f) { stat.Add(o); if (solid) solids.Add(SolidOf(o, pad)); return o; }
            // Mains power is level-local: real lights sit in `powered`, glowing materials go dark (kit.GateLit).
            // (The env presets still drive the shared 'indoor' group on top of this.)
            var powered = J.Group("powered"); var glow = new List<Material>();
            Material onMains(Material m) { glow.Add(m); return m; }
            GameObject sign(string[] lines, SignOpts o, float x, float y, float z, float ry = 0, string lightGroup = null)
            {
                var s = SignAt(kit, lines, o, x, y, z, ry);
                if (lightGroup != null) kit.Lit(lightGroup, MatOf(s), o.emissive != 0 ? o.emissive : 0.6f);
                if (lightGroup == "indoor") onMains(MatOf(s));
                return s;
            }

            // ---- shell: floor, walls with brand stripe, T-bar ceiling with a travelator opening, roof ----
            stat.Add(kit.Plane(32, 24, floor, A(z: -12)));
            add(kit.Slab(0.3f, H, 24.6f, wall, A(x: -16.15f, z: -12)), true); add(kit.Slab(0.3f, H, 24.6f, wall, A(x: 16.15f, z: -12)), true);
            add(kit.Slab(32.6f, H, 0.3f, wall, A(x: 0, z: -24.15f)), true);
            foreach (var (x0, x1) in new[] { (-16f, -14f), (-4f, 16f) }) stat.Add(kit.Slab(x1 - x0, H, 0.3f, wall, A(x: (x0 + x1) / 2, z: 0.15f)));
            stat.Add(kit.Slab(10, H - 2.6f, 0.3f, wall, A(x: -9, y: 2.6f, z: 0.15f)));
            solids.Add(new Solid(-16, 16, 0, 0.5f, 0, H, "southWall"));
            foreach (var (x, z, w, d) in new[] { (-15.99f, -12f, 0.02f, 24f), (15.99f, -12f, 0.02f, 24f), (0f, -23.99f, 32f, 0.02f), (6f, -0.01f, 20f, 0.02f) }) stat.Add(kit.Box(w, 0.25f, d, green, A(x: x, y: 2.05f, z: z, cast: false)));
            var ceil = Mat.Lit(new MatSpec
            {
                map = kit.Texture(kit.Canvas(256, 256, (g, W, Hh) =>
                {
                    g.fillStyle = "#efeeea"; g.fillRect(0, 0, 256, 256); g.fillStyle = "rgba(0,0,0,0.04)"; for (int i = 0; i < 900; i++) g.fillRect(r() * 256, r() * 256, 2, 2);
                    g.fillStyle = "#c9c8c3"; for (int i = 0; i < 256; i += 64) { g.fillRect(i, 0, 3, 256); g.fillRect(0, i, 256, 3); }
                })), repeat = new Vector2(1 / 2.4f, 1 / 2.4f), roughness = 0.9f,
            });
            foreach (var (x0, x1, z0, z1) in new[] { (-16f, 13.2f, -24f, 0f), (13.2f, 16f, -24f, -21f), (13.2f, 16f, -14f, 0f) }) stat.Add(kit.Slab(x1 - x0, 0.25f, z1 - z0, ceil, A(x: (x0 + x1) / 2, y: H, z: (z0 + z1) / 2)));
            stat.Add(kit.Box(2.8f, 0.1f, 7, dark, A(x: 14.6f, y: H + 1.6f, z: -17.5f)));
            stat.Add(kit.Slab(33, 0.3f, 25, kit.Surface("concrete"), A(x: 0, y: H + 1.7f, z: -12)));

            // ---- entrance: glazing, sliding doors, mat, outside view, exit sign ----
            foreach (var (x0, x1) in new[] { (-14f, -10.25f), (-7.75f, -4f) })
            {
                var pane = kit.Box(x1 - x0, 2.6f, 0.03f, glass, A(x: (x0 + x1) / 2, z: 0.05f, cast: false)); Kit.Keep(pane); stat.Add(pane);
                foreach (var x in new[] { x0, x1 }) stat.Add(kit.Box(0.08f, 2.6f, 0.12f, dark, A(x: x, z: 0.05f)));
            }
            stat.Add(kit.Box(3.2f, 0.25f, 0.3f, dark, A(x: -9, y: 2.35f, z: -0.1f)));
            var leaves = new[] { -1, 1 }.Select(s => { var m = kit.Box(1.3f, 2.3f, 0.04f, glass, A(x: -9 + s * 0.62f, z: -0.05f, cast: false)); Kit.Keep(m); dyn.Add(m); return m; }).ToArray();
            stat.Add(kit.Box(2.6f, 0.012f, 1.8f, rubber, A(x: -9, z: -1.1f, cast: false)));
            var view = MakeWindowView(kit, 40, 14, "street", 11);
            J.SetPos(view.mesh, -9, 4.2f, 10); J.SetYaw(view.mesh, PI); dyn.Add(view.mesh);
            dyn.Add(kit.Plane(40, 10, kit.Surface("asphalt"), A(x: -9, y: -0.02f, z: 5)));
            dyn.Add(kit.Box(12, 0.15f, 3, white, A(x: -9, y: 3.0f, z: 1.6f)));
            dyn.Add(sign(new[] { "비상구" }, new SignOpts { w = 0.6f, h = 0.22f, bg = "#1f9a4a", fg = "#ffffff", emissive = 1.2f }, -9, 2.8f, -0.3f, PI, "emergency"));
            dyn.Add(sign(new[] { "감사합니다 · 안녕히 가세요" }, new SignOpts { w = 3, h = 0.32f, bg = "#2f8a57", fg = "#ffffff", emissive = 0.4f }, -9, 3.25f, -0.2f, PI, "indoor"));

            // ---- aisles: west wall water shelving + six double gondolas ----
            void stockRuns(List<StockRun> runs, (string sec, string[] pool)[] pools, int seed)
            {
                for (int i = 0; i < runs.Count; i++)
                {
                    var run = runs[i]; int pi = run.side < 0 ? 0 : 1; var (sec, pool) = pi < pools.Length ? pools[pi] : pools[0];
                    goods.Stock(run.With(sec, Pick(pool, i % 5), seed + i));
                }
            }
            var wallG = Gondola(kit, new GondolaOpts { x = -15.97f, z = -14.5f, len = 15, sides = "pos", depth = 0.5f });
            add(wallG.group, true); stockRuns(wallG.runs, new[] { ("water", new[] { "pack6", "pack6", "water2", "water2" }) }, 3);
            for (int i = 0; i < GX.Length; i++) { var gd = Gondola(kit, new GondolaOpts { x = GX[i], z = -14, len = 11 }); add(gd.group, true); stockRuns(gd.runs, FACES[i], 20 + i * 10); }
            var chill = Gondola(kit, new GondolaOpts { x = -4.7f, z = -23.95f, len = 21.4f, axis = "x", sides = "pos", depth = 0.62f, levels = new[] { 0.3f, 0.68f, 1.04f, 1.4f }, height = 2.0f, color = "#f2f2f2" });
            add(chill.group, true);
            for (int i = 0; i < chill.runs.Count; i++) goods.Stock(chill.runs[i].With("misc", Pick(new[] { "milk", "yogurt", "tofu", "kimchi", "soymilk", "milk" }, i), 90 + i));
            stat.Add(kit.Box(21.4f, 0.14f, 0.3f, white, A(x: -4.7f, y: 1.98f, z: -23.2f)));
            var chillStrip = Panels(kit, new[] { X(x: -4.7f, y: 1.96f, z: -23.08f, sx: 21.2f) }, w: 1, d: 0.05f, on: 3); onMains(chillStrip.mat); dyn.Add(chillStrip.obj);

            // Pallet of water six-packs at the front with the purchase limit notice.
            add(kit.Slab(1.2f, 0.14f, 1.0f, wood, A(x: -6.6f, z: -4.05f)), true);
            goods.Stock(new StockRun { sec = "water", x = -7.15f, z = -3.57f, dir = new[] { 1f, 0 }, face = new[] { 0, 1f }, len = 1.1f, depth = 0.95f, y = 0.14f, clear = 1.1f, items = new List<string> { "pack6" }, rows = 4, stack = 3, fill = 1, edge = false, seed = 5 });
            stat.Add(kit.Cyl(0.015f, 0.015f, 1.3f, steel, A(x: -5.85f, z: -3.5f)));
            dyn.Add(sign(new[] { "1인 6병 한정", "생수 2L" }, new SignOpts { w = 0.42f, h = 0.3f, bg = "#ffd23a", fg = "#1d1d1f" }, -5.85f, 1.45f, -3.5f, -PI / 3));

            // ---- checkout lanes ----
            var beltTex = kit.Texture(kit.Canvas(64, 64, (g, W, Hh) => { g.fillStyle = "#1b1c1e"; g.fillRect(0, 0, 64, 64); g.fillStyle = "#2c2e31"; for (int y = 0; y < 64; y += 8) g.fillRect(0, y, 64, 3); }));
            var beltRepeat = new Vector2(2, 4); var beltMat = Mat.Lit(new MatSpec { map = beltTex, repeat = beltRepeat, roughness = 0.75f });
            var cashiers = new List<IPerson>();
            for (int i = 0; i < LANES.Length; i++)
            {
                float x = LANES[i];
                add(kit.RBox(0.7f, 0.84f, 3.2f, 0.03f, white, A(x: x, z: -3.4f)), true);
                stat.Add(kit.Box(0.76f, 0.03f, 3.26f, top, A(x: x, y: 0.84f, z: -3.4f)));
                dyn.Add(kit.Plane(0.5f, 1.5f, beltMat, A(x: x, y: 0.875f, z: -4.2f)));
                stat.Add(kit.Box(0.4f, 0.06f, 0.35f, glass, A(x: x, y: 0.87f, z: -3.25f)));
                stat.Add(kit.Cyl(0.02f, 0.02f, 0.35f, steel, A(x: x + 0.25f, y: 0.87f, z: -2.9f)));
                stat.Add(kit.Box(0.34f, 0.24f, 0.04f, dark, A(x: x + 0.25f, y: 1.2f, z: -2.9f, ry: PI / 2)));
                dyn.Add(sign(new[] { i < 2 ? "계산대" : "마감", $"{i + 1}" }, new SignOpts { w = 0.22f, h = 0.18f, bg = "#0d2a3a", fg = "#7fe0c6", emissive = 0.8f }, x + 0.22f, 1.21f, -2.9f, -PI / 2, "indoor"));
                stat.Add(kit.Box(0.12f, 0.05f, 0.2f, dark, A(x: x - 0.3f, y: 0.87f, z: -2.7f)));
                stat.Add(kit.Cyl(0.03f, 0.03f, 2.2f, steel, A(x: x - 0.32f, z: -5.05f)));
                dyn.Add(sign(new[] { $"{i + 1}" }, new SignOpts { w = 0.34f, h = 0.34f, bg = i < 2 ? "#2f8a57" : "#6b6e72", fg = "#ffffff", emissive = i < 2 ? 1.1f : 0.15f }, x - 0.32f, 2.38f, -5.08f, PI, "indoor"));
                var rack = Gondola(kit, new GondolaOpts { x = x - 0.55f, z = -3.0f, len = 0.6f, sides = "neg", depth = 0.18f, levels = new[] { 0.9f, 1.15f, 1.4f }, height = 1.6f, color = "#c8202f" });
                add(rack.group, true);
                for (int k = 0; k < rack.runs.Count; k++) goods.Stock(rack.runs[k].With("misc", new[] { new[] { "gum", "vita" }[k % 2] }, 140 + i * 3 + k, false));
                if (i < 2) cashiers.Add(Npc(people, "clerk", 41 + i, new NpcAt { x = x + 0.75f, z = -3.3f, ry = PI / 2, anim = "idle" }));
            }
            dyn.Add(sign(new[] { "이 계산대는 마감했습니다", "옆 계산대를 이용해 주세요" }, new SignOpts { w = 0.66f, h = 0.3f, bg = "#ffffff", fg = "#333333", border = "#c8202f" }, 7.62f, 0.8f, -5.13f, PI));
            foreach (var x in new[] { 7.25f, 7.98f }) stat.Add(kit.Cyl(0.025f, 0.03f, 0.95f, steel, A(x: x, z: -5.1f)));
            solids.Add(new Solid(7.2f, 8.05f, -5.2f, -5.0f, 0, 1));
            foreach (var p in cashiers) dyn.Add(p.Group);

            // ---- ATM by the doors ----
            var atm = J.Group("atm");
            atm.Add(kit.RBox(0.9f, 1.75f, 0.75f, 0.04f, kit.Mat("paint:#d7dbe0", new MatSpec { metalness = 0.5f, roughness = 0.35f }), A(x: -2.6f, z: -0.42f)));
            atm.Add(kit.Box(0.9f, 0.3f, 0.8f, green, A(x: -2.6f, y: 1.75f, z: -0.42f)));
            atm.Add(kit.Box(0.3f, 0.12f, 0.2f, dark, A(x: -2.6f, y: 0.95f, z: -0.85f, rx: -0.4f)));
            add(atm, true);
            dyn.Add(sign(new[] { "24시 자동화기기", "현금 인출 · 입금 · 이체", "카드를 넣어 주세요" }, new SignOpts { w = 0.5f, h = 0.36f, bg = "#0f3d6e", fg = "#e8f4ff", emissive = 0.9f }, -2.6f, 1.3f, -0.81f, PI, "indoor"));
            dyn.Add(sign(new[] { "ATM" }, new SignOpts { w = 0.6f, h = 0.22f, bg = "#2f8a57", fg = "#ffffff", emissive = 0.7f }, -2.6f, 1.9f, -0.83f, PI, "indoor"));

            // ---- carts, baskets ----
            var carts = new List<Xform>();
            foreach (var x in new[] { -15.3f, -14.6f }) for (int k = 0; k < 9; k++) carts.Add(X(x: x, z: -1.2f - k * 0.3f));
            var baskets = new List<Xform>();
            foreach (var (x, n) in new[] { (-3.75f, 7), (-3.25f, 5) }) for (int k = 0; k < n; k++) baskets.Add(X(x: x, y: 0.12f + k * 0.06f, z: -1.3f));
            add(CartsAndBaskets(kit, carts, baskets));
            solids.Add(new Solid(-15.7f, -14.2f, -4.4f, -0.8f, 0, 1.1f));
            add(kit.Box(1.1f, 0.12f, 0.45f, steel, A(x: -3.5f, z: -1.3f)), true);

            // ---- produce tables (east) ----
            var produce = new List<Xform>(); var col = new List<string>();
            var KINDS = new[] { ("#b8262a", 0.042f), ("#f08a1c", 0.04f), ("#c79a5a", 0.038f), ("#a07848", 0.036f), ("#8fbf5a", 0.09f), ("#d8c06a", 0.045f), ("#e9e6d8", 0.05f) };
            foreach (var tx in new[] { 8.3f, 11.5f }) foreach (var tz in new[] { -11.8f, -17.2f })
                {
                    add(kit.Slab(1.8f, 0.72f, 4.4f, wood, A(x: tx, z: tz)), true);
                    for (int cx = 0; cx < 3; cx++) for (int cz = 0; cz < 8; cz++)
                        {
                            float x = tx - 0.6f + cx * 0.6f, z = tz - 1.93f + cz * 0.55f; var (c, s) = KINDS[(int)(r() * KINDS.Length)];
                            stat.Add(kit.Slab(0.56f, 0.12f, 0.5f, wood, A(x: x, y: 0.72f, z: z)));
                            int n = s > 0.08f ? 4 : 14;
                            for (int k = 0; k < n; k++)
                            {
                                float px = x + (r() - 0.5f) * 0.4f, py = 0.84f + s * 0.8f + (k > n * 0.6f ? s * 1.2f : 0), pz = z + (r() - 0.5f) * 0.36f;
                                produce.Add(X(x: px, y: py, z: pz, s: s, sy: s * (c == "#e9e6d8" ? 3 : 0.92f), rx: r() * 3)); col.Add(c);
                            }
                        }
                }
            var prodColors = col.Select(c => Scaled(J.Css(c), Step(0.85f + r() * 0.3f))).ToList();
            dyn.Add(InstancedByColor(kit, Geo.Icosahedron(1, 1), c => Mat.Lit(new MatSpec { color = c, roughness = 0.55f }), produce, prodColors, cast: false, name: "produce"));

            // ---- travelator to the 2nd floor (east wall) ----
            float runL = 13.4f, ang = Mathf.Atan2(H, runL), L = runL / Mathf.Cos(ang) + 1.5f;
            var prism = Geo.Obj(Geo.Extrude(new[] { new Vector2(0, 0), new Vector2(runL + 2, H + 0.54f), new Vector2(runL + 2, 0) }, 2.2f).RotateY(PI / 2), clad, "travelator", true, true);
            J.SetPos(prism, 13.3f, 0, -6.5f); stat.Add(prism);
            var tread = kit.Texture(kit.Canvas(64, 64, (g, W, Hh) => { g.fillStyle = "#8d9196"; g.fillRect(0, 0, 64, 64); g.fillStyle = "#5d6166"; for (int x = 0; x < 64; x += 4) g.fillRect(x, 0, 2, 64); g.fillStyle = "#3a3d40"; g.fillRect(0, 0, 64, 2); }));
            var treadRepeat = new Vector2(2.8f, 2.5f); var treadMat = Mat.Lit(new MatSpec { map = tread, repeat = treadRepeat, metalness = 0.7f, roughness = 0.4f });
            var tilt = J.Group("tilt", dyn); J.SetPos(tilt, 14.4f, 0.02f, -6.5f); J.SetRot(tilt, ang, 0, 0);
            tilt.Add(kit.Plane(1.05f, L, treadMat, A(z: -L / 2)));
            foreach (var s in new[] { -1, 1 })
            {
                tilt.Add(kit.Box(0.08f, 0.06f, L, rubber, A(x: s * 0.62f, y: 0.92f, z: -L / 2)));
                var gl = kit.Box(0.02f, 0.86f, L, glass, A(x: s * 0.62f, y: 0.05f, z: -L / 2, cast: false)); Kit.Keep(gl); tilt.Add(gl);
            }
            stat.Add(kit.Box(1.4f, 0.02f, 0.6f, kit.Mat("paint:#c9a227", new MatSpec { metalness = 0.6f }), A(x: 14.4f, z: -6.1f)));
            solids.Add(new Solid(13.2f, 15.6f, -21, -6.4f, 0, H, "travelator"));
            dyn.Add(sign(new[] { "2층 · 주방 · 문구 · 의류", "카트는 바퀴를 고정해 주세요" }, new SignOpts { w = 1.4f, h = 0.5f, bg = "#1d1d1f", fg = "#ffffff", emissive = 0.5f }, 14.4f, 2.5f, -5.95f, 0, "indoor"));

            // ---- signs: hanging section boards, posters ----
            void hang(string[] lines, string color, float x, float z, float w = 1.9f)
            {
                foreach (var ry in new[] { 0, PI }) dyn.Add(sign(lines, new SignOpts { w = w, h = 0.42f, bg = color, fg = "#ffffff", emissive = 0.35f }, x, 2.75f, z + (ry != 0 ? -0.01f : 0.01f), ry, "indoor"));
                foreach (var s in new[] { -1, 1 }) stat.Add(kit.Cyl(0.004f, 0.004f, H - 2.96f, steel, A(x: x + s * w * 0.4f, y: 2.96f, z: z, cast: false)));
            }
            foreach (var (sec, color, a, _) in SEC) hang(new[] { Items.MART_SECTIONS[sec].label }, color, AISLES[a], -8.4f);
            hang(new[] { "세제 · 조미료" }, "#6b5b95", AISLES[5], -8.4f); hang(new[] { "과자 · 간식" }, "#c0436e", AISLES[6], -8.4f);
            hang(new[] { "채소 · 과일" }, "#5a8f3c", 9.9f, -14.5f, 2.4f); hang(new[] { "신선 · 유제품" }, "#2b6cb0", -4.7f, -21.9f, 2.6f);
            var posters = new[]
            {
                (new[] { "이번 주 행사", "라면 · 즉석밥 1+1" }, "#c4161c", -15.98f, 2.75f, -10f, PI / 2), (new[] { "연무마트 포인트", "적립 2배 데이" }, "#2f8a57", -15.98f, 2.75f, -18f, PI / 2),
                (new[] { "장바구니를 이용하면", "비닐봉투 값을 아껴요" }, "#1f6fb2", 15.98f, 2.6f, -3.2f, -PI / 2), (new[] { "오늘의 특가", "제철 사과 1봉 7,900원" }, "#e8481c", 3f, 2.75f, -0.02f, PI),
            };
            foreach (var (lines, bg, x, y, z, ry) in posters) dyn.Add(sign(lines, new SignOpts { w = 1.2f, h = 0.85f, bg = bg, fg = "#ffffff" }, x, y, z, ry));

            // ---- goods, sold-out notices ----
            var built = goods.Build(); group.Add(built.group);
            var sections = built.sections;
            foreach (var (sec, _, a, _) in SEC)
            {
                if (!sections.ContainsKey(sec)) { var gs = sections[sec] = new GoodsSection { full = J.Group("full", built.group), @out = J.Group("out", built.group) }; gs.@out.SetActive(false); }
                foreach (var gx in new float?[] { a < GX.Length ? GX[a] : GX[0], a - 1 >= 0 ? GX[a - 1] : (float?)null }.Where(v => v.HasValue).Select(v => v.Value))
                {
                    var s = sign(new[] { "품절", "다음 입고 일정 미정" }, new SignOpts { w = 0.4f, h = 0.3f, bg = "#ffffff", fg = "#c4161c", border = "#c4161c" }, gx + (gx == GX[a] ? -0.22f : 0.22f), 1.45f, -8.45f);
                    sections[sec].@out.Add(s);
                }
            }

            // ---- lights ----
            var fixtures = new List<Xform>();
            foreach (var x in AISLES.Take(6)) foreach (var z in new[] { -9.5f, -12f, -14.5f, -17f, -19.5f }) fixtures.Add(X(x: x, y: H - 0.03f, z: z, ry: PI / 2));
            for (int k = 0; -14 + k * 2.4f <= 13; k++) foreach (var z in new[] { -2.2f, -5.4f, -22.2f }) fixtures.Add(X(x: -14 + k * 2.4f, y: H - 0.03f, z: z));
            foreach (var x in new[] { 8.3f, 11.5f }) foreach (var z in new[] { -10f, -13f, -16f, -19f }) fixtures.Add(X(x: x, y: H - 0.03f, z: z));
            var ceilPanels = Panels(kit, fixtures); onMains(ceilPanels.mat); dyn.Add(ceilPanels.obj); dyn.Add(powered);
            powered.Add(Lamps(kit, new[] { -12f, -4f, 4f, 11f }.SelectMany(x => new[] { -5.5f, -16f }.Select(z => X(x: x, y: H - 0.35f, z: z))).ToList(), intensity: 13));
            var emerg = new[] { (-9f, 3.1f, -0.6f), (-4.7f, 3.1f, -23.3f), (12f, 3.1f, -1f) }.Select(p =>
            {
                var m = Mat.Lit(new MatSpec { color = J.Hex(0xffffff), emissive = J.Hex(0xffffff), emissiveIntensity = 0 });
                var e = new EmergencyLamp(kit, J.Hex(0xf4f7ff), 4, 12, m, 2.5f); J.SetPos(e.l.gameObject, p.Item1, p.Item2, p.Item3); dyn.Add(e.l);
                dyn.Add(kit.Box(0.35f, 0.08f, 0.12f, m, A(x: p.Item1, y: p.Item2 + 0.1f, z: p.Item3, cast: false))); return e;
            }).ToList();

            // ---- people: shoppers wander the aisles, a queue forms when busy ----
            var crowd = new MartShoppers(kit, people, r, dyn);

            // ---- state ----
            string crowdState = "normal", power = "on", outside = null; var soldOut = new List<string>();
            float doorOpen = 0, beltOff = 0, treadOff = 0;
            crowd.ApplyCrowd(false);
            void setState(string key, object value)
            {
                if (key == "crowd") { var v = Sv.Str(value); if (v != "normal" && v != "busy") { Debug.LogError($"[mart] crowd must be normal|busy {v}"); return; } crowdState = v; crowd.ApplyCrowd(v == "busy"); }
                else if (key == "soldOut")
                {
                    var list = Sv.StrList(value);
                    foreach (var id in list) if (!IsSec(id)) Debug.LogError($"[mart] unknown soldOut section {id}");
                    soldOut = list.Where(IsSec).ToList();
                    foreach (var (sec, _, _, _) in SEC) { bool o = soldOut.Contains(sec); sections[sec].full.SetActive(!o); sections[sec].@out.SetActive(o); }
                }
                else if (key == "power")
                {
                    var v = Sv.Str(value); if (v != "on" && v != "off") { Debug.LogError($"[mart] power must be on|off {v}"); return; }
                    power = v; bool on = v == "on";
                    foreach (var l in powered.GetComponentsInChildren<Light>(true)) kit.GateLit(l, on); // dim, never hide: light count stays fixed
                    foreach (var m in glow) kit.GateLit(m, on);
                    foreach (var e in emerg) e.Set(on);
                }
                else if (key == "outside") { var v = Sv.Str(value); if (v != null && !MOODS.Contains(v)) { Debug.LogError($"[mart] outside must be {string.Join(",", MOODS)} {v}"); return; } outside = v; }
                else Debug.LogError($"[mart] unknown state {key} {value}");
            }

            // desc: what E shows when no chapter handles the point (story/world.js); gets the run context.
            bool off() => power == "off";
            Func<Ctx, string[]> say(params string[] l) => c => l;
            var points = new List<Point>
            {
                Pt("martExit", "출입구", -9, -1.0f, "door", 1.8f), Pt("checkout", "계산대", 0.75f, -3.6f, "counter", 1.8f), Pt("atm", "ATM", -2.6f, -1.4f, "atm", 1.4f),
            };
            foreach (var (sec, _, a, id) in SEC) points.Add(Pt(id, $"{Items.MART_SECTIONS[sec].label} 진열대", AISLES[a], -14, "shelf", 3));
            points.AddRange(new[]
            {
                Pt("martCarts", "카트 보관대", -14.9f, -4.9f, "look", 1.3f, say("카트가 줄지어 서 있다. 오늘은 카트를 끌고 다니는 사람이 유난히 많다.", "들고 갈 수 있는 만큼만 사는 게 좋겠다. 집까지 걸어가야 한다.")),
                Pt("martBaskets", "장바구니", -3.5f, -2.0f, "look", 1.1f, say("플라스틱 장바구니. 가득 담으면 꽤 무겁다.")),
                Pt("martPallet", "생수 팔레트", -6.6f, -5.0f, "look", 1.4f, say("2L 생수 6병 묶음이 쌓여 있다. 노란 안내판: \"1인 6병 한정\"", "한 사람이 많이 사 가면 다른 사람 몫이 줄어든다.")),
                Pt("martChill", "신선·유제품 냉장고", -4.7f, -22.6f, "look", 2.6f, c => off() ? new[] { "냉장고 조명이 꺼지고 팬 소리도 멈췄다.", "문이 없는 오픈 냉장고라 냉기가 금방 빠진다. 유제품은 오래 못 버틴다." } : new[] { "우유, 두부, 요구르트, 김치. 정전이 되면 가장 먼저 상하는 것들이다." }),
                Pt("martProduceN", "채소·과일 매대", 9.9f, -11.8f, "look", 2.2f, say("사과, 귤, 양파, 감자. 손질 없이 먹을 수 있는 과일은 비상시에도 쓸모가 있다.")),
                Pt("martProduceS", "채소·과일 매대", 9.9f, -17.2f, "look", 2.2f, say("배추와 대파. 냉장 보관이 필요 없는 감자·양파는 오래 간다.")),
                Pt("martTravelator", "무빙워크 (2층)", 14.4f, -5.4f, "look", 1.3f, c => off() ? new[] { "무빙워크가 멈춰 있다. 2층은 셔터가 내려졌다." } : new[] { "2층은 주방용품·문구·의류. 오늘 필요한 건 1층에 다 있다." }),
                Pt("martDetergent", "세제·조미료 진열대", AISLES[5], -14, "look", 2.6f, say("세제, 간장, 식용유. 지금 급한 물건은 아니다.")),
                Pt("martSnacks", "과자·간식 진열대", AISLES[6], -14, "look", 2.6f, say("과자 진열대. 건빵과 에너지바는 식품 진열대에 따로 있다.")),
                Pt("martLane3", "3번 계산대 (마감)", 8.4f, -5.6f, "look", 1.2f, say("\"이 계산대는 마감했습니다. 옆 계산대를 이용해 주세요.\"")),
                Pt("martPoster", "행사 포스터", 3, -0.9f, "look", 1.3f, say("\"오늘의 특가 · 제철 사과 1봉 7,900원\"", "그 옆에 손글씨 안내가 붙어 있다: \"생수·건전지는 1인 구매 수량을 제한합니다.\"")),
            });

            kit.Merge(stat);
            return new Level
            {
                id = "mart", group = group, env = "interior",
                walk = new List<WalkZone> { new WalkZone(-16, 16, -24, 0, 0) },
                solids = solids, points = points,
                spawns = new Dictionary<string, Spawn> { ["entry"] = new Spawn(-9, 0, -2.4f, 0) },
                npcs = new Dictionary<string, IPerson> { ["cashier"] = cashiers[0], ["cashier2"] = cashiers[1] },
                states = new Dictionary<string, object>
                {
                    ["crowd"] = new[] { "normal", "busy" }, ["soldOut"] = new[] { new string[0], new[] { "water" }, new[] { "water", "elec" }, new[] { "water", "food", "daily", "elec", "health" } },
                    ["power"] = new[] { "on", "off" }, ["outside"] = new[] { null, "day", "dusk", "night", "blackout" },
                },
                setState = setState,
                update = (dt, t, ctx) =>
                {
                    var pl = ctx?.player; bool on = power == "on";
                    if (on)
                    {
                        bool near = pl != null && Mathf.Abs(pl.X + 9) < 2.4f && pl.Z > -3.6f;
                        doorOpen += Mathf.Max(-dt * 1.4f, Mathf.Min(dt * 1.4f, (near ? 1 : 0) - doorOpen));
                        for (int i = 0; i < leaves.Length; i++) { var p = J.Pos(leaves[i]); p.x = -9 + (i == 1 ? 1 : -1) * (0.62f + doorOpen * 1.2f); J.SetPos(leaves[i], p); }
                        beltOff -= dt * 0.25f; treadOff += dt * 1.2f;
                        Mat.SetTiling(beltMat, beltRepeat, new Vector2(0, beltOff)); Mat.SetTiling(treadMat, treadRepeat, new Vector2(0, treadOff));
                    }
                    view.Set(outside ?? MoodFor(ctx?.env, "day"));
                    crowd.Update(dt, pl);
                    foreach (var p in cashiers) p.Update(dt);
                },
                anchors = new Dictionary<string, Anchor>
                {
                    ["overview"] = new Anchor { x = 12.5f, y = 3.0f, z = -2.5f }, ["entrance"] = new Anchor { x = -9, y = 1.6f, z = -3.2f },
                    ["waterAisle"] = new Anchor { x = -14.26f, y = 1.6f, z = -7.5f }, ["checkout"] = new Anchor { x = 0.75f, y = 1.6f, z = -6.2f },
                },
            };
        }
    }
}
