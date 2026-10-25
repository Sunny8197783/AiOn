// Ports levels/store.js: 바로편의점 — storefront with a pull door, window bar, ramen station, gondolas, drink
// fridges, the counter with 이수아 (NPC 'clerk'), battery rack, coffee machine, ATM; emergency units when unpowered.
// Frame: floor x -5..5, z -9..0, storefront on the south wall (z = 0), yaw 0 faces north (-z).
// power 'off': ceiling LEDs, fridge glow, screens and real lights die (lights sit in one `powered` group, glowing
// materials are gated off) and battery emergency units light the room.
// States: soldOut (item-group lists) power (on|off) clerk (in|out) outside (null | mood).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.CommerceGoods;

namespace Gyeongbo.Levels
{
    public static class Store
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("store", (kit, people, fx) => BuildStore(kit, people));

        const float H = 2.9f, DOOR = 0.6f, FX0 = -4.92f, FDOOR = 0.76f, PI = Mathf.PI;
        const int FN = 8;
        static readonly string[] SOLD = { "water", "batteries", "food" };

        public static Task<Level> BuildStore(Kit kit, IPeople people)
        {
            try { return Task.FromResult(Build(kit, people)); }
            catch (Exception e) { return Task.FromException<Level>(e); }
        }

        static Level Build(Kit kit, IPeople people)
        {
            var group = J.Group("store");
            var stat = J.Group("stat", group); var dyn = J.Group("dyn", group); var powered = J.Group("powered", dyn);
            var solids = new List<Solid>(); var r = Kit.Rng(19); var goods = CreateGoods(kit); var glow = new List<Material>();
            Material wall = kit.Surface("plaster"), floor = kit.Surface("tile", new SurfaceOpts { roughness = 0.2f, color = J.Hex(0xf2f0ea) }), steel = kit.Mat("steel"), metal = kit.Mat("metal"),
                dark = kit.Mat("darkMetal"), white = kit.Mat("paint:#f1efe9", new MatSpec { roughness = 0.4f }), teal = kit.Mat("paint:#18a58f"), orange = kit.Mat("paint:#f28a1a"),
                top = kit.Mat("paint:#3a3c40", new MatSpec { roughness = 0.3f }), rubber = kit.Mat("rubber"), wood = kit.Surface("wood"), plastic = kit.Mat("plastic"),
                green = kit.Mat("paint:#2f8a57", new MatSpec { roughness = 0.5f }), dglass = kit.Mat("darkGlass"),
                glass = Mat.Lit(new MatSpec { color = J.Hex(0xd6e6ee), roughness = 0.04f, metalness = 0.1f, transparent = true, opacity = 0.16f, depthWrite = false });
            GameObject add(GameObject o, bool solid = false, float pad = 0.03f) { stat.Add(o); if (solid) solids.Add(SolidOf(o, pad)); return o; }
            GameObject keep(GameObject o, GameObject parent = null) { Kit.Keep(o); (parent ?? dyn).Add(o); return o; }
            // Glowing material: follows the env's 'indoor' group and goes dark with the store's power.
            Material lampMat(string hex, float on, Color? color = null)
            {
                var m = Mat.Lit(new MatSpec { color = color ?? J.Hex(0xffffff), emissive = J.Css(hex), roughness = 0.4f });
                kit.Lit("indoor", m, on); glow.Add(m); return m;
            }
            GameObject sign(string[] lines, SignOpts o, float x, float y, float z, float ry = 0, string lightGroup = null)
            {
                var s = SignAt(kit, lines, o, x, y, z, ry);
                if (lightGroup != null) kit.Lit(lightGroup, MatOf(s), o.emissive != 0 ? o.emissive : 0.6f);
                if (lightGroup == "indoor") glow.Add(MatOf(s));
                dyn.Add(s); return s;
            }

            // ---- shell: floor, walls with the brand band, ceiling, skirting ----
            stat.Add(kit.Plane(10, 9, floor, A(z: -4.5f)));
            add(kit.Slab(0.25f, H, 9.5f, wall, A(x: -5.125f, z: -4.5f)), true); add(kit.Slab(0.25f, H, 9.5f, wall, A(x: 5.125f, z: -4.5f)), true);
            add(kit.Slab(10.5f, H, 0.25f, wall, A(z: -9.125f)), true);
            stat.Add(kit.Slab(10.5f, 0.12f, 9.5f, kit.Mat("paint:#f3f2ee", new MatSpec { roughness = 0.9f }), A(y: H, z: -4.5f)));
            foreach (var (x, z, w, d) in new[] { (-4.99f, -4.5f, 0.02f, 9f), (4.99f, -4.5f, 0.02f, 9f), (0f, -8.99f, 10f, 0.02f) })
            {
                stat.Add(kit.Box(w, 0.22f, d, teal, A(x, 2.46f, z, cast: false))); stat.Add(kit.Box(w, 0.04f, d, orange, A(x, 2.42f, z, cast: false)));
                stat.Add(kit.Box(w + 0.01f, 0.1f, d + 0.01f, dark, A(x, 0, z, cast: false)));
            }
            var dome = Geo.Obj(Geo.Sphere(0.07f, 12, 8, 0, PI * 2, 0, PI / 2), dglass, "cctv"); J.SetRot(dome, PI, 0, 0); J.SetPos(dome, 1.8f, H, -4.4f); stat.Add(dome);

            // ---- storefront: glazing, mullions, pull door, stickers ----
            stat.Add(kit.Box(10, 0.3f, 0.12f, dark, A(z: -0.06f))); stat.Add(kit.Box(10, 0.4f, 0.2f, dark, A(y: H - 0.4f, z: -0.1f)));
            foreach (var x in new[] { -5f, -3.75f, -2.5f, -1.25f, 0.1f, 1.1f, 2.5f, 3.75f, 5f }) stat.Add(kit.Box(0.06f, H - 0.7f, 0.1f, metal, A(x, 0.3f, -0.05f)));
            foreach (var (x0, x1) in new[] { (-5f, 0.1f), (1.1f, 5f) }) keep(kit.Box(x1 - x0, H - 0.7f, 0.02f, glass, A((x0 + x1) / 2, 0.3f, -0.05f, cast: false)));
            solids.Add(new Solid(-5, 5, -0.15f, 0.4f, 0, H, "storefront"));
            var leaf = J.Group("door", dyn); J.SetPos(leaf, 0.1f, 0, -0.05f);
            keep(kit.Box(0.98f, 2.2f, 0.02f, glass, A(0.49f, 0.02f, cast: false)), leaf);
            foreach (var (w, h, x, y) in new[] { (0.98f, 0.05f, 0.49f, 0.02f), (0.98f, 0.05f, 0.49f, 2.17f), (0.05f, 2.2f, 0.025f, 0.02f), (0.05f, 2.2f, 0.955f, 0.02f) }) leaf.Add(kit.Box(w, h, 0.05f, metal, A(x, y)));
            leaf.Add(kit.Box(0.03f, 0.5f, 0.03f, steel, A(0.86f, 0.8f, -0.07f)));
            GameObject sticker(string[] lines, float w, float h, string bg, string fg, float x, float y, GameObject parent = null, float z = -0.075f) =>
                keep(SignAt(kit, lines, new SignOpts { w = w, h = h, bg = bg, fg = fg }, x, y, z, PI), parent);
            sticker(new[] { "당기세요", "PULL" }, 0.22f, 0.12f, "#18a58f", "#ffffff", 0.86f, 1.42f, leaf, -0.02f);
            sticker(new[] { "24시간 영업" }, 0.5f, 0.16f, "#ffffff", "#18a58f", -0.6f, 1.6f);
            sticker(new[] { "2+1 행사", "음료 · 생수" }, 0.7f, 0.5f, "#f28a1a", "#ffffff", -3.1f, 1.75f);
            sticker(new[] { "1+1", "컵라면 골라 담기" }, 0.7f, 0.5f, "#c4161c", "#ffffff", 3.1f, 1.75f);
            sticker(new[] { "택배 접수", "교통카드 충전" }, 0.6f, 0.3f, "#ffffff", "#1f4f8c", 1.8f, 1.9f);

            // ---- outside: sidewalk, curb, road, canopy, plastic tables, a street lamp, the painted street ----
            dyn.Add(kit.Plane(14, 4, kit.Surface("sidewalk"), A(z: 2.2f)));
            dyn.Add(kit.Box(14, 0.14f, 0.18f, kit.Mat("curb"), A(y: -0.12f, z: 4.25f)));
            dyn.Add(kit.Plane(40, 12, kit.Surface("asphalt"), A(y: -0.12f, z: 10.3f)));
            dyn.Add(kit.Box(10.4f, 0.16f, 1.5f, teal, A(y: H + 0.02f, z: 0.75f)));
            var edge = Mat.Lit(new MatSpec { color = J.Hex(0xffffff), emissive = J.Hex(0xffe3b0), roughness = 0.4f }); kit.Lit("shops", edge, 1.6f);
            dyn.Add(kit.Box(10.4f, 0.03f, 0.04f, edge, A(y: H, z: 1.48f, cast: false)));
            var tbl = J.Group("tables");
            foreach (var tx in new[] { -3.3f, -1.3f })
            {
                tbl.Add(kit.Cyl(0.42f, 0.42f, 0.03f, plastic, A(tx, 0.7f, 1.9f), 20)); tbl.Add(kit.Cyl(0.03f, 0.03f, 0.7f, metal, A(tx, 0, 1.9f)));
                foreach (var a0 in new[] { 0, PI / 2, PI, 1.5f * PI })
                {
                    float a = a0 + 0.3f, cx = tx + Mathf.Sin(a) * 0.62f, cz = 1.9f + Mathf.Cos(a) * 0.62f;
                    tbl.Add(kit.Box(0.42f, 0.04f, 0.42f, green, A(cx, 0.42f, cz, ry: a)));
                    tbl.Add(kit.Box(0.42f, 0.38f, 0.04f, green, A(cx + Mathf.Sin(a) * 0.2f, 0.46f, cz + Mathf.Cos(a) * 0.2f, ry: a)));
                    foreach (var s in new[] { -1, 1 }) tbl.Add(kit.Box(0.03f, 0.42f, 0.03f, green, A(cx + Mathf.Cos(a) * 0.18f * s, 0, cz - Mathf.Sin(a) * 0.18f * s)));
                }
            }
            dyn.Add(kit.Merge(tbl));
            dyn.Add(kit.Cyl(0.07f, 0.09f, 6.2f, dark, A(4.4f, 0, 3.8f)));
            var head = Mat.Lit(new MatSpec { color = J.Hex(0xffffff), emissive = J.Hex(0xffd9a0), roughness = 0.4f }); kit.Lit("street", head, 3);
            dyn.Add(kit.Box(0.9f, 0.1f, 0.3f, dark, A(4.0f, 6.1f, 3.8f))); dyn.Add(kit.Box(0.6f, 0.03f, 0.2f, head, A(3.85f, 6.08f, 3.8f, cast: false)));
            var view = MakeWindowView(kit, 34, 12, "street", 23);
            J.SetPos(view.mesh, 0, 3.04f, 14.5f); J.SetYaw(view.mesh, PI); dyn.Add(view.mesh);

            // ---- window bar with stools ----
            add(kit.Box(4.55f, 0.04f, 0.42f, wood, A(-2.48f, 1.0f, -0.42f)));
            foreach (var x in new[] { -4.6f, -2.48f, -0.35f }) stat.Add(kit.Box(0.04f, 1.0f, 0.04f, steel, A(x, 0, -0.55f)));
            foreach (var x in new[] { -4.3f, -3.4f, -2.5f, -1.6f, -0.75f })
            {
                stat.Add(kit.Cyl(0.17f, 0.16f, 0.06f, dark, A(x, 0.7f, -0.98f), 18)); stat.Add(kit.Cyl(0.025f, 0.025f, 0.7f, metal, A(x, 0, -0.98f)));
                stat.Add(kit.Cyl(0.2f, 0.22f, 0.03f, metal, A(x, 0, -0.98f), 18));
            }
            solids.Add(new Solid(-4.75f, -0.2f, -1.18f, -0.15f, 0, 1.1f, "windowBar"));
            stat.Add(kit.Box(0.12f, 0.14f, 0.1f, plastic, A(-1.0f, 1.04f, -0.42f)));

            // ---- ramen station: hot water, microwave, bins ----
            add(kit.Box(0.55f, 0.88f, 1.2f, white, A(-4.725f, 0, -2.15f)), true);
            stat.Add(kit.Box(0.58f, 0.03f, 1.24f, steel, A(-4.72f, 0.88f, -2.15f)));
            stat.Add(kit.Box(0.36f, 0.5f, 0.34f, metal, A(-4.75f, 0.91f, -2.48f)));
            stat.Add(kit.Box(0.04f, 0.06f, 0.05f, kit.Mat("paint:#c4161c"), A(-4.55f, 1.06f, -2.56f))); stat.Add(kit.Box(0.04f, 0.06f, 0.05f, kit.Mat("paint:#1f6fb2"), A(-4.55f, 1.06f, -2.4f)));
            stat.Add(kit.Box(0.2f, 0.02f, 0.26f, dark, A(-4.62f, 0.91f, -2.48f)));
            dyn.Add(kit.Box(0.01f, 0.05f, 0.12f, lampMat("#ff5a3a", 1.4f), A(-4.565f, 1.26f, -2.48f, cast: false)));
            stat.Add(kit.Box(0.38f, 0.28f, 0.48f, white, A(-4.76f, 0.91f, -1.84f)));
            stat.Add(kit.Box(0.01f, 0.2f, 0.32f, dglass, A(-4.565f, 0.95f, -1.9f)));
            dyn.Add(kit.Box(0.01f, 0.03f, 0.08f, lampMat("#5aff9a", 1.2f), A(-4.565f, 1.1f, -1.64f, cast: false)));
            stat.Add(kit.Cyl(0.04f, 0.04f, 0.12f, plastic, A(-4.8f, 0.91f, -2.75f)));
            for (int k = 0; k < 6; k++) stat.Add(kit.Cyl(0.04f, 0.035f, 0.1f, white, A(-4.86f, 0.91f + k * 0.02f, -1.56f)));
            sign(new[] { "라면 조리대", "뜨거운 물 조심 · 국물은 오른쪽 통에" }, new SignOpts { w = 1.0f, h = 0.32f, bg = "#ffffff", fg = "#c4161c", border = "#c4161c" }, -4.98f, 1.75f, -2.15f, PI / 2);
            var bins = new[] { ("일반", "#5b6770"), ("캔·페트", "#1f6fb2"), ("국물", "#c4161c") };
            for (int i = 0; i < bins.Length; i++)
            {
                var (lbl, c) = bins[i]; float z = -2.96f - i * 0.32f;
                stat.Add(kit.Box(0.36f, 0.78f, 0.3f, kit.Mat("paint:" + c, new MatSpec { roughness = 0.5f }), A(-4.8f, 0, z)));
                stat.Add(kit.Box(0.3f, 0.02f, 0.24f, dark, A(-4.8f, 0.78f, z)));
                sign(new[] { lbl }, new SignOpts { w = 0.26f, h = 0.1f, bg = "#ffffff", fg = c }, -4.615f, 0.62f, z, PI / 2);
            }
            solids.Add(new Solid(-5, -4.58f, -3.8f, -2.75f, 0, 0.9f));

            StoreFixtures.Build(kit, goods, stat, dyn, powered, solids, r, add, keep, lampMat, sign, glass, steel, metal, dark, white, teal, orange, top, green, dglass, rubber);

            // ---- goods and sold-out cards ----
            var built = goods.Build(); group.Add(built.group);
            var sections = built.sections;
            var cards = new Dictionary<string, (float x, float y, float z, float ry)> { ["water"] = (FX0 + 0.76f, 1.3f, -8.125f, 0), ["batteries"] = (0.8f, 1.08f, -3.6f, PI / 2), ["food"] = (-2.0f, 1.08f, -4.6f, PI / 2) };
            foreach (var sec in SOLD)
            {
                if (!sections.ContainsKey(sec)) { var gs = sections[sec] = new GoodsSection { full = J.Group("full", built.group), @out = J.Group("out", built.group) }; gs.@out.SetActive(false); }
                var (x, y, z, ry) = cards[sec];
                sections[sec].@out.Add(SignAt(kit, new[] { "품절", "입고 일정 미정" }, new SignOpts { w = 0.36f, h = 0.26f, bg = "#ffffff", fg = "#c4161c", border = "#c4161c" }, x, y, z, ry));
            }

            // ---- lights: ceiling LED lines, real lamps (powered), battery emergency units, exit sign ----
            var fixtures = new List<Xform>();
            foreach (var z in new[] { -1.3f, -3.0f, -4.7f, -6.4f, -7.9f }) foreach (var x in new[] { -3.4f, -0.6f, 2.2f }) fixtures.Add(X(x, H - 0.03f, z));
            var pm = Panels(kit, fixtures, w: 2.4f, d: 0.1f, color: "#f4f8ff", on: 2.6f); glow.Add(pm.mat); dyn.Add(pm.obj);
            powered.Add(Lamps(kit, new[] { (-2.6f, -2.4f), (1.6f, -2.6f), (-2.6f, -6.2f), (1.6f, -6.4f) }.Select(p => X(p.Item1, H - 0.3f, p.Item2)).ToList(), J.Hex(0xf6f8ff), 9, 9));
            var emerg = new[] { (DOOR, 2.62f, -0.32f, PI), (-2.0f, 2.55f, -8.92f, 0f), (4.92f, 2.55f, -3.4f, -PI / 2) }.Select(p =>
            {
                var (x, y, z, ry) = p;
                var u = J.Group("emergency-unit", dyn); J.SetPos(u, x, y, z); J.SetYaw(u, ry);
                u.Add(kit.Box(0.34f, 0.12f, 0.08f, white, A(y: 0)));
                var m = Mat.Lit(new MatSpec { color = J.Hex(0xffffff), emissive = J.Hex(0xfff2dc), emissiveIntensity = 0, roughness = 0.3f });
                foreach (var s in new[] { -1, 1 }) u.Add(kit.Cyl(0.035f, 0.045f, 0.06f, m, A(s * 0.11f, -0.05f, 0.06f, rx: 0.6f, cast: false), 10));
                var e = new EmergencyLamp(kit, J.Hex(0xfff2dc), 2.6f, 9, m, 2.6f); J.SetPos(e.l.gameObject, x + Mathf.Sin(ry) * 0.35f, y - 0.12f, z + Mathf.Cos(ry) * 0.35f); dyn.Add(e.l);
                return e;
            }).ToList();
            sign(new[] { "비상구" }, new SignOpts { w = 0.5f, h = 0.18f, bg = "#1f9a4a", fg = "#ffffff", emissive = 1.2f }, DOOR, 2.42f, -0.22f, PI, "emergency");
            var clock = SignAt(kit, new[] { "17:30" }, new SignOpts { w = 0.34f, h = 0.14f, bg = "#101214", fg = "#ff5a3a", emissive = 0.8f }, -4.98f, 2.2f, -5.3f, PI / 2);
            kit.Lit("indoor", MatOf(clock), 0.8f); glow.Add(MatOf(clock)); keep(clock);

            // ---- people ----
            var clerk = Npc(people, "clerk", 7, new NpcAt { x = 3.9f, z = -3.3f, ry = PI / 2, anim = "idle" });
            dyn.Add(clerk.Group);
            var sitter = Npc(people, "crowd", 211, new NpcAt { x = -2.5f, z = -0.98f, ry = PI, anim = "sitChair" }); sitter.Seat = 0.76f;
            var browser = Npc(people, "crowd", 212, new NpcAt { x = -3.6f, z = -7.6f, ry = 0, anim = "idle" });
            var extras = new[] { sitter, browser }; foreach (var p in extras) dyn.Add(p.Group);
            stat.Add(kit.Cyl(0.055f, 0.045f, 0.1f, kit.Mat("paint:#d8261c"), A(-2.5f, 1.04f, -0.42f), 14));
            stat.Add(kit.Cyl(0.057f, 0.057f, 0.006f, white, A(-2.5f, 1.14f, -0.42f), 14));

            // ---- state ----
            var soldOut = new List<string>(); string power = "on", outside = null, clerkState = "in";
            void applyPower(bool on)
            {
                foreach (var l in powered.GetComponentsInChildren<Light>(true)) kit.GateLit(l, on); // dim, never hide: light count stays fixed
                foreach (var m in glow) kit.GateLit(m, on);
                foreach (var e in emerg) e.Set(on);
                foreach (var p in extras) p.Group.SetActive(on);
                clerk.SetProp("flashlight", !on);
            }
            // desc: what E shows when no chapter handles the point (story/world.js); gets the run context.
            bool darkCtx(Ctx c) => c != null && c.power == false;
            Func<Ctx, string[]> lit(string dark, string bright) => c => new[] { darkCtx(c) ? dark : bright };
            var points = new List<Point>
            {
                Pt("storeExit", "밖으로 나가기", DOOR, -0.75f, "door", 1.4f), Pt("storeCounter", "계산대 · 이수아", 2.1f, -3.3f, "npc", 1.7f),
                Pt("storeShelf", "진열대 살펴보기", -1.1f, -4.6f, "shelf", 2.4f), Pt("storeFridge", "음료 냉장고", -1.6f, -7.5f, "shelf", 2.2f),
                Pt("storeAtm", "ATM (자동화기기)", 4.05f, -0.62f, "atm", 1.2f),
                Pt("storeCoffee", "셀프 커피머신", 4.0f, -1.75f, "look", 1.0f, lit("전원이 꺼져 있다. 버튼을 눌러도 아무 반응이 없다.", "원두커피 셀프 머신. 컵은 계산대에서 받는다.")),
                Pt("storeSteamer", "찐빵 찜기", 2.5f, -2.25f, "look", 0.9f, lit("찜기가 식어 있다. 안의 찐빵도 차갑다.", "김이 서린 유리 너머로 찐빵이 따뜻하게 데워지고 있다.")),
                Pt("storeRack", "건전지 진열대", 2.4f, -4.3f, "look", 0.9f, c => new[] { "AA·AAA 건전지와 휴대폰 충전 케이블이 걸려 있다.", "라디오와 손전등은 건전지가 들어 있어야 쓸 수 있다." }),
                Pt("storeRamen", "라면 조리대", -4.35f, -2.15f, "look", 1.2f, lit("온수기가 꺼져 있다. 정전에는 컵라면도 끓는 물이 없으면 못 먹는다.", "온수기, 전자레인지, 분리수거함. 컵라면 국물은 여기 버린다.")),
                Pt("storeIce", "아이스크림 냉동고", 2.2f, -7.9f, "look", 1.2f, lit("냉동고가 멈췄다. 뚜껑 안쪽에 물방울이 맺혀 있다. 열지 않는 게 낫겠다.", "하드와 콘이 가득하다. 가을인데도 잘 팔린다고 한다.")),
                Pt("storeWindowBar", "창가 자리", -2.5f, -1.4f, "look", 1.4f, c => new[] { "창밖으로 단지 앞 도로가 보인다. 플라스틱 의자가 몇 개 놓여 있다." }),
            };
            void setState(string key, object value)
            {
                if (key == "soldOut")
                {
                    var list = Sv.StrList(value);
                    foreach (var id in list) if (!SOLD.Contains(id)) Debug.LogError($"[store] unknown soldOut section {id} ; known: {string.Join(",", SOLD)}");
                    soldOut = list.Where(SOLD.Contains).ToList();
                    foreach (var sec in SOLD) { bool o = soldOut.Contains(sec); sections[sec].full.SetActive(!o); sections[sec].@out.SetActive(o); }
                }
                else if (key == "power")
                {
                    var v = Sv.Str(value); if (v != "on" && v != "off") { Debug.LogError($"[store] power must be on|off {v}"); return; }
                    power = v; applyPower(v == "on");
                }
                else if (key == "clerk")
                { // 'out': 이수아 has left town; nobody behind the counter
                    var v = Sv.Str(value); if (v != "in" && v != "out") { Debug.LogError($"[store] clerk must be in|out {v}"); return; }
                    clerkState = v; clerk.Group.SetActive(v == "in");
                    points.First(p => p.id == "storeCounter").label = v == "in" ? "계산대 · 이수아" : "계산대 (아무도 없다)";
                }
                else if (key == "outside") { var v = Sv.Str(value); if (v != null && !MOODS.Contains(v)) { Debug.LogError($"[store] outside must be null or {string.Join(",", MOODS)} {v}"); return; } outside = v; }
                else Debug.LogError($"[store] unknown state {key} {value}");
            }

            kit.Merge(stat);
            float door = 0;
            return new Level
            {
                id = "store", group = group, env = "interior",
                walk = new List<WalkZone> { new WalkZone(-5, 5, -9, 0, 0) },
                solids = solids, points = points,
                spawns = new Dictionary<string, Spawn> { ["entry"] = new Spawn(DOOR, 0, -1.35f, 0), ["counter"] = new Spawn(2.0f, 0, -3.3f, -PI / 2) },
                npcs = new Dictionary<string, IPerson> { ["clerk"] = clerk },
                states = new Dictionary<string, object>
                {
                    ["soldOut"] = new[] { new string[0], new[] { "water" }, new[] { "batteries" }, new[] { "water", "batteries" }, new[] { "water", "batteries", "food" } },
                    ["power"] = new[] { "on", "off" }, ["outside"] = new[] { null, "day", "dusk", "night", "blackout" },
                },
                setState = setState,
                update = (dt, t, ctx) =>
                {
                    var pl = ctx?.player; bool near = pl != null && Mathf.Abs(pl.X - DOOR) < 1.3f && pl.Z > -1.9f;
                    door += Mathf.Max(-dt * 2, Mathf.Min(dt * 2, (near ? 1 : 0) - door));
                    J.SetYaw(leaf, door * 1.25f);
                    view.Set(outside ?? MoodFor(ctx?.env, ctx?.env == "interiorNight" ? "night" : "dusk"));
                    clerk.Update(dt);
                    foreach (var p in extras) if (p.Group.activeSelf) p.Update(dt);
                },
                anchors = new Dictionary<string, Anchor>
                {
                    ["overview"] = new Anchor { x = -4.3f, y = 2.3f, z = -0.8f }, ["counter"] = new Anchor { x = 1.6f, y = 1.6f, z = -3.3f }, ["fridge"] = new Anchor { x = -1.6f, y = 1.6f, z = -6.6f },
                },
            };
        }
    }
}
