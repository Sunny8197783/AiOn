// Ports levels/town-fronts.js: shop fronts and civic entrances — Hangul signboards from two atlases (light group
// 'shops'), fake lit interiors behind glass, awnings, canopies, 돌출간판, the convenience-store terrace, cafe deck,
// mart carts, flags (태극기) and the notice boards (shelterNotice, aptNotice, centerNotice) whose text follows
// the notices state. Painters and flags live in TownFrontsPaint.cs.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using static Gyeongbo.Levels.TownUtil;
using static Gyeongbo.Levels.TownFrontsPaint;

namespace Gyeongbo.Levels
{
    public sealed class TownFrontsResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public List<WalkZone> walk = new List<WalkZone>();
        public Dictionary<string, Board> boards = new Dictionary<string, Board>();   // notice-board id -> board
    }

    public static class TownFronts
    {
        const float Y = TownPlan.CURB;
        const float PI = Mathf.PI;

        public static TownFrontsResult BuildFronts(Kit kit)
        {
            var B = new Dictionary<string, Building>();
            foreach (var bd in TownMap.BUILDINGS) B[bd.id] = bd;
            var group = J.Group("fronts");
            var stat = J.Group("static", group);
            var solids = new List<Solid>();
            void Block(float x0, float x1, float z0, float z1, float y1 = 2.5f, string id = "front") => solids.Add(new Solid(x0, x1, z0, z1, -0.5f, y1, id));
            Material steel = kit.Mat("steel"), dark = kit.Mat("darkMetal"), glass = kit.Mat("glass", new MatSpec { opacity = 0.35f }), white = kit.Mat("whiteWall"), metal = kit.Mat("metal");

            var hs = TownAtlas.Make(kit, "signs", 1024, 1020, 2, 12, PaintSign);
            var vs = TownAtlas.Make(kit, "vsigns", 1024, 512, 8, 1, PaintVSign);
            var ins = TownAtlas.Make(kit, "interiors", 1024, 1024, 2, 4, PaintInterior);
            Material Glow(Texture map, string lightGroup, float v) => kit.Lit(lightGroup, Mat.Lit(new MatSpec { map = map, emissiveMap = map, emissive = Color.white, emissiveIntensity = v, roughness = 0.45f }), v);
            var signMat = kit.Memo("town:signMat", () => Glow(hs.tex, "shops", 0.9f));
            var vMat = kit.Memo("town:vsignMat", () => Glow(vs.tex, "shops", 1.1f));
            var shopIn = kit.Memo("town:shopInterior", () => Glow(ins.tex, "shops", 0.75f));
            var civIn = kit.Memo("town:civInterior", () => Glow(ins.tex, "indoor", 0.6f));

            float SideYaw(string side) => side == "s" ? 0 : side == "n" ? PI : side == "e" ? PI / 2 : -PI / 2;
            // Flat panel on a face. side: n|s|e|w (outward normal), a = along-face centre, y = centre height.
            GameObject OnFace(GeoData geo, Material mat, string side, float c, float a, float y, float @out = 0.06f)
            {
                var m = Obj(geo, mat); float o = side == "n" || side == "w" ? -@out : @out;
                if (side == "n" || side == "s") J.SetPos(m, a, y, c + o); else J.SetPos(m, c + o, y, a);
                J.SetYaw(m, SideYaw(side));
                stat.Add(m); return m;
            }
            float FaceOf(Building b, string side) => (float)(side == "n" ? b.z0 : side == "s" ? b.z1 : side == "e" ? b.x1 : b.x0);
            void Sign(int i, Building b, string side, float a, float y, float w, float h, float depth = 0.18f)
            {
                float c = FaceOf(b, side);
                OnFace(Geo.Box(w + 0.1f, h + 0.1f, depth), dark, side, c, a, y, depth / 2);
                OnFace(hs.Plane(i, w, h), signMat, side, c, a, y, depth + 0.01f);
            }
            void ShopWindow(string k, Building b, string side, float a, float y0, float w, float h, bool civic = false)
            {
                float c = FaceOf(b, side);
                OnFace(ins.Plane(Array.IndexOf(INTERIORS, k), w, h), civic ? civIn : shopIn, side, c, a, y0 + h / 2, 0.05f);
                Kit.Keep(OnFace(Geo.Plane(w, h), glass, side, c, a, y0 + h / 2, 0.08f));
                foreach (var s in new[] { -1, 1 }) OnFace(Geo.Box(0.08f, h, 0.12f), dark, side, c, a + s * w / 2, y0 + h / 2, 0.1f);
                OnFace(Geo.Box(w, 0.1f, 0.14f), dark, side, c, a, y0 + h, 0.1f);
                for (float x = -w / 2 + 2.4f; x < w / 2 - 0.5f; x += 2.4f) OnFace(Geo.Box(0.05f, h, 0.08f), dark, side, c, a + x, y0 + h / 2, 0.1f);
            }
            void VSign(int i, float x, float y, float z, float ry)
            {
                float nx = Mathf.Sin(ry) * 0.056f, nz = Mathf.Cos(ry) * 0.056f;
                foreach (var s in new[] { 1, -1 }) { var m = Obj(vs.Plane(i, 0.55f, 2.4f), vMat); J.SetPos(m, x + nx * s, y, z + nz * s); J.SetYaw(m, s > 0 ? ry : ry + PI); stat.Add(m); }
                stat.Add(kit.Box(0.6f, 2.5f, 0.1f, dark, At(x, y - 1.25f, z, ry)));
            }

            // ---- 바로편의점 (north face) ----
            var st = B["store"];
            ShopWindow("store", st, "n", 36, Y + 0.1f, 11.2f, 2.75f);
            Sign(0, st, "n", 36, Y + 3.35f, 11.6f, 0.75f);
            var parasolMat = kit.Memo("town:parasol", () => Mat.Lit(new MatSpec
            {
                side = "double", roughness = 0.8f,
                map = kit.Texture(kit.Canvas(256, 64, (g, w, h) => { for (int k = 0; k < 8; k++) { g.fillStyle = k % 2 != 0 ? "#ffffff" : "#0f8a6c"; g.fillRect(k * w / 8f, 0, w / 8f, h); } })),
            }));
            foreach (var (x, z) in new[] { (33f, 9.6f), (40.5f, 9.6f) })
            {
                stat.Add(kit.Cyl(0.42f, 0.42f, 0.04f, white, At(x, Y + 0.7f, z), 20), kit.Cyl(0.03f, 0.03f, 0.7f, steel, At(x, Y, z), 8), kit.Cyl(0.025f, 0.025f, 2.2f, steel, At(x, Y + 0.7f, z), 6));
                var para = Obj(Geo.Cone(1.15f, 0.38f, 8, 1, true), parasolMat, true, false);
                J.SetPos(para, x, Y + 2.7f, z); stat.Add(para);
                for (int k = 0; k < 4; k++)
                {
                    float a = k * PI / 2 + 0.4f, cx = x + Mathf.Cos(a) * 0.68f, cz = z + Mathf.Sin(a) * 0.68f;
                    stat.Add(kit.Box(0.42f, 0.04f, 0.42f, kit.Mat("paint:#e9e9e4"), At(cx, Y + 0.44f, cz, -a)), kit.Box(0.42f, 0.42f, 0.04f, kit.Mat("paint:#e9e9e4"), At(cx + Mathf.Cos(a) * 0.2f, Y + 0.46f, cz + Mathf.Sin(a) * 0.2f, -a + PI / 2)));
                }
                Block(x - 0.95f, x + 0.95f, z - 0.95f, z + 0.95f, 1.2f, "storeTable");
            }
            var pack = kit.Memo("town:waterPack", () => Mat.Lit(new MatSpec
            {
                roughness = 0.25f,
                map = kit.Texture(kit.Canvas(128, 64, (g, w, h) => { g.fillStyle = "#cfe6f2"; g.fillRect(0, 0, w, h); for (int k = 0; k < 3; k++) { g.fillStyle = "#8fc3e0"; g.fillRect(6 + k * 41, 4, 34, h - 8); g.fillStyle = "#1f5fa8"; g.fillRect(6 + k * 41, h * 0.4f, 34, 10); } })),
            }));
            for (int k = 0; k < 6; k++) stat.Add(kit.Box(0.38f, 0.33f, 0.26f, pack, At(41.2f - (k % 2) * 0.4f, Y + (k / 2) * 0.33f, 11.5f)));
            stat.Add(kit.Box(1.2f, 0.8f, 0.6f, white, At(30.9f, Y, 11.55f)), kit.Box(1.15f, 0.02f, 0.55f, glass, At(30.9f, Y + 0.8f, 11.55f)));
            Block(40.7f, 41.7f, 11.3f, 12, 1.2f, "storeStock"); Block(30.2f, 31.6f, 11.2f, 12, 1, "freezer");

            // ---- 상가: pharmacy + snack bar, 2F academy, awning, protruding signs ----
            var sh = B["shopsS"];
            ShopWindow("pharmacy", sh, "n", 19.4f, Y + 0.1f, 5.6f, 2.7f); ShopWindow("snack", sh, "n", 25.8f, Y + 0.1f, 5.4f, 2.7f);
            Sign(1, sh, "n", 19.4f, Y + 3.2f, 5.8f, 0.7f); Sign(2, sh, "n", 25.8f, Y + 3.2f, 5.6f, 0.7f); Sign(10, sh, "n", 22.5f, Y + 5.4f, 8, 0.8f);
            var awning = kit.Memo("town:awning", () => Mat.Lit(new MatSpec
            {
                side = "double", roughness = 0.85f,
                map = kit.Texture(kit.Canvas(256, 64, (g, w, h) => { for (int k = 0; k < 12; k++) { g.fillStyle = k % 2 != 0 ? "#f2efe6" : "#c8312a"; g.fillRect(k * w / 12f, 0, w / 12f, h); } })),
            }));
            var aw = Obj(Geo.Plane(5.4f, 1.3f), awning, true, false); J.SetPos(aw, 25.8f, Y + 2.62f, 11.45f); J.SetRot(aw, -PI / 2 + 0.42f, 0, 0); stat.Add(aw);
            VSign(0, 16.6f, Y + 5.6f, 11.3f, PI / 2); VSign(1, 28.6f, Y + 5.6f, 11.3f, PI / 2);

            // ---- 연무마트 (south face): big sign, entrance canopy, glass, X-banners, cart bay ----
            var mt = B["mart"]; float mh = TownBuildings.HeightOf(mt);
            Sign(3, mt, "s", -16, mh - 2.0f, 14, 1.8f, 0.3f);
            ShopWindow("mart", mt, "s", -16, Y + 0.1f, 8, 3.0f); ShopWindow("mart", mt, "s", -29, Y + 0.6f, 9, 2.4f); ShopWindow("mart", mt, "s", -3, Y + 0.6f, 9, 2.4f);
            stat.Add(kit.Box(9, 0.25f, 3, kit.Mat("paint:#d8d4ca", new MatSpec { roughness = 0.6f }), At(-16, Y + 3.4f, -22.5f)));
            foreach (var s in new[] { -1, 1 }) { stat.Add(kit.Box(0.2f, 3.4f, 0.2f, steel, At(-16 + s * 4.3f, Y, -21.2f))); Block(-16 + s * 4.3f - 0.15f, -16 + s * 4.3f + 0.15f, -21.35f, -21.05f, 3, "martCanopy"); }
            foreach (var (x, t) in new[] { (-21.6f, new[] { "가을 정기 세일", "생활용품 최대 30%" }), (-10.4f, new[] { "신선 코너", "오늘의 과일" }) })
            {
                var ban = kit.Sign(t, new SignOpts { w = 0.6f, h = 1.6f, bg = "#c8312a", single = false }); J.SetPos(ban, x, Y + 1.1f, -21.6f); stat.Add(ban, kit.Box(0.5f, 0.05f, 0.4f, dark, At(x, Y, -21.6f)));
            }
            var cartM = kit.Memo("town:cartWire", () => Mat.Lit(new MatSpec
            {
                map = kit.Texture(kit.Canvas(64, 64, (g, w, h) => { g.strokeStyle = "#d0d4d8"; g.lineWidth = 3; for (int k = 2; k < w; k += 9) { g.beginPath(); g.moveTo(k, 0); g.lineTo(k, w); g.stroke(); g.beginPath(); g.moveTo(0, k); g.lineTo(w, k); g.stroke(); } })),
                alphaTest = 0.5f, side = "double", metalness = 0.8f, roughness = 0.3f,
            }));
            for (int k = 0; k < 5; k++) { float x = -8.2f + k * 0.32f; stat.Add(kit.Box(0.55f, 0.45f, 0.9f, cartM, At(x, Y + 0.45f, -21.6f, cast: false)), kit.Box(0.5f, 0.04f, 0.1f, kit.Mat("paint:#c8312a"), At(x, Y + 1.0f, -21.15f))); }
            Block(-8.6f, -6.4f, -22.1f, -21.1f, 1.2f, "carts");

            // ---- 한결물류 (south face) ----
            var of = B["office"]; float oh = TownBuildings.HeightOf(of);
            Sign(4, of, "s", 87, oh - 1.5f, 12, 1.6f, 0.25f);
            ShopWindow("office", of, "s", 87, Y + 0.1f, 6, 2.8f, true);
            stat.Add(kit.Box(7, 0.22f, 2.6f, kit.Mat("glass", new MatSpec { opacity = 0.5f }), At(87, Y + 3.1f, -20.7f, cast: false)), kit.Box(7.2f, 0.12f, 0.12f, steel, At(87, Y + 3.1f, -19.45f)));

            // ---- 행정복지센터 (south face): sign, canopy, doors, flags, notice board ----
            var ce = B["center"]; float chh = TownBuildings.HeightOf(ce);
            Sign(5, ce, "s", -89, chh - 1.6f, 12, 1.2f, 0.25f);
            ShopWindow("civic", ce, "s", -89, Y + 0.1f, 5, 2.8f, true); ShopWindow("civic", ce, "s", -98, Y + 0.9f, 7, 2.0f, true); ShopWindow("civic", ce, "s", -80, Y + 0.9f, 7, 2.0f, true);
            stat.Add(kit.Slab(6.4f, 0.3f, 3.2f, kit.Surface("concrete"), At(-89, Y + 3.3f, -26.4f)));
            foreach (var s in new[] { -1, 1 }) { stat.Add(kit.Box(0.35f, 3.3f, 0.35f, white, At(-89 + s * 2.9f, Y, -25.1f))); Block(-89 + s * 2.9f - 0.2f, -89 + s * 2.9f + 0.2f, -25.3f, -24.9f, 3.3f, "centerCanopy"); }
            var flags = new List<GameObject>();
            var poles = new[] { -102f, -100.4f, -98.8f };
            for (int k = 0; k < poles.Length; k++)
            {
                float x = poles[k];
                stat.Add(kit.Cyl(0.04f, 0.06f, 7, metal, At(x, Y, -26.2f), 10), kit.Box(0.3f, 0.3f, 0.3f, kit.Surface("concrete"), At(x, Y, -26.2f)));
                Block(x - 0.2f, x + 0.2f, -26.4f, -26, 3, "flagpole");
                flags.Add(Flag(kit, k, x, Y + 6.9f, -26.2f));
            }
            foreach (var f in flags) group.Add(f);

            // ---- 카페 쉼표 (south face): sign, glass, awning, wooden deck with tables, menu board ----
            var cf = B["cafe"];
            Sign(7, cf, "s", -118, Y + 3.45f, 6, 0.8f);
            ShopWindow("cafe", cf, "s", -118, Y + 0.1f, 13, 2.9f);
            var awc = Obj(Geo.Plane(14, 1.6f), kit.Mat("paint:#2f4a3a", new MatSpec { roughness = 0.9f, side = "double" }), true, false); J.SetPos(awc, -118, Y + 2.85f, -15.35f); J.SetRot(awc, -PI / 2 + 0.35f, 0, 0); stat.Add(awc);
            stat.Add(kit.Slab(15, 0.08f, 2.6f, kit.Surface("wood", new SurfaceOpts { color = J.Hex(0xa47a52) }), At(-118, Y, -14.7f)));
            foreach (var x in new[] { -123.4f, -121.2f, -114.6f, -112.4f })
            {
                stat.Add(kit.Cyl(0.32f, 0.32f, 0.03f, kit.Mat("paint:#3a2a20"), At(x, Y + 0.74f, -14.6f), 16), kit.Cyl(0.03f, 0.03f, 0.74f, dark, At(x, Y, -14.6f), 6));
                foreach (var s in new[] { -1, 1 }) stat.Add(kit.Box(0.38f, 0.04f, 0.38f, kit.Surface("wood"), At(x + s * 0.55f, Y + 0.45f, -14.6f)), kit.Box(0.04f, 0.45f, 0.04f, dark, At(x + s * 0.55f, Y, -14.6f)));
                Block(x - 0.85f, x + 0.85f, -15.1f, -14.1f, 1, "cafeTable");
            }
            var menu = kit.Sign(new[] { "오늘의 커피", "아메리카노 2,500", "라떼 3,200" }, new SignOpts { w = 0.55f, h = 0.75f, bg = "#1e2420", fg = "#f3e3c3", single = false });
            J.SetPos(menu, -110.9f, Y + 0.55f, -13.3f); J.SetRot(menu, -0.15f, 0, 0); stat.Add(menu); Block(-111.3f, -110.5f, -13.6f, -13, 1, "menuBoard");

            // ---- culture centre name signs, 관리사무소 ----
            var cu = B["culture"]; float cuh = TownBuildings.HeightOf(cu);
            Sign(6, cu, "e", 31, cuh - 2.2f, 10, 1.1f, 0.25f); Sign(6, cu, "n", -96, cuh - 2.0f, 10, 1.1f, 0.25f);
            Sign(8, B["aptOffice"], "n", 121, Y + 2.6f, 4, 0.6f);

            // ---- apartment entrances: canopy, columns, lobby glass, 동 sign ----
            foreach (var (id, x, si) in new[] { ("apt103", 78f, 9), ("apt102", 88f, 22), ("apt101", 88f, 23) })
            {
                var b = B[id]; float z = (float)b.z0;
                stat.Add(kit.Slab(4.4f, 0.25f, 2.6f, kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0xd9d4c8) }), At(x, Y + 2.75f, z - 1.3f)));
                foreach (var s in new[] { -1, 1 }) { stat.Add(kit.Box(0.3f, 2.75f, 0.3f, white, At(x + s * 2.0f, Y, z - 2.4f))); Block(x + s * 2 - 0.18f, x + s * 2 + 0.18f, z - 2.58f, z - 2.22f, 3, "canopy"); }
                ShopWindow("lobby", b, "n", x, Y + 0.05f, 2.6f, 2.4f, true);
                Sign(si, b, "n", x, Y + 3.45f, 3.4f, 0.5f, 0.12f);
                stat.Add(kit.Box(0.25f, 0.4f, 0.06f, dark, At(x + 1.6f, Y + 1.2f, z - 0.06f)));
            }

            // ---- background streets: shop signs and 돌출간판 on n1 (faces 시장길), villa name plates ----
            var n1 = B["n1"]; var cells = new List<(float a0, float a1)>();
            {
                float W = (float)(n1.x1 - n1.x0), nx0 = (float)n1.x0; int nx = Math.Max(1, (int)Round(W / 17)); float cw = (W - 3 * (nx - 1)) / nx;
                for (int i = 0; i < nx; i++) cells.Add((nx0 + i * (cw + 3), nx0 + i * (cw + 3) + cw));
            }
            int gi = 0; float n1z1 = (float)n1.z1;
            foreach (var (a0, a1) in cells) foreach (var f in new[] { 0, 1 })
            {
                float w = (a1 - a0) / 2 - 0.6f;
                foreach (var k in new[] { 0, 1 }) Sign(11 + (gi++ % 11), n1, "s", a0 + w / 2 + 0.4f + k * (w + 0.4f), Y + 4.1f + f * 3.5f, w, 0.75f, 0.14f);
            }
            for (int k = 0; k < cells.Count; k++) VSign((2 + k * 2) % VSIGNS.Length, cells[k].a0 + 0.5f, Y + 6.2f, n1z1 + 0.4f, PI / 2);
            VSign(3, cells[0].a1 - 0.5f, Y + 9.2f, n1z1 + 0.4f, PI / 2);

            // ---- notice boards (text follows the notices state) ----
            var boards = new Dictionary<string, Board>();
            // back: +1 = board stands south of its point and faces north (-z); -1 = stands north, faces south.
            foreach (var (id, back) in new[] { ("shelterNotice", 1), ("aptNotice", 1), ("centerNotice", -1) })
            {
                var p = TownMap.PLACES[id]; float px = (float)p.x, pz = (float)p.z; var bd = MakeBoard(kit, 512, 384); int b = back;
                var m = Obj(Geo.Plane(1.4f, 1.05f), Mat.Lit(new MatSpec { map = bd.texture, roughness = 0.6f }));
                J.SetPos(m, px, Y + 1.45f, pz + 0.25f * b); J.SetYaw(m, b > 0 ? PI : 0); group.Add(m);
                stat.Add(kit.Box(1.55f, 1.2f, 0.06f, kit.Mat("paint:#8a9198", new MatSpec { metalness = 0.6f, roughness = 0.4f }), At(px, Y + 0.85f, pz + 0.3f * b)), kit.Box(1.7f, 0.06f, 0.32f, dark, At(px, Y + 2.05f, pz + 0.25f * b)));
                foreach (var s in new[] { -1, 1 }) stat.Add(kit.Box(0.07f, 2.05f, 0.07f, dark, At(px + s * 0.72f, Y, pz + 0.3f * b)));
                Block(px - 0.85f, px + 0.85f, pz + Math.Min(0.15f * b, 0.45f * b), pz + Math.Max(0.15f * b, 0.45f * b), 2.2f, "noticeBoard");
                boards[id] = bd;
            }

            kit.Merge(stat);
            var walk = new List<WalkZone> { new WalkZone(-125.5f, -110.5f, -16, -13.4f, Y + 0.08f) };
            return new TownFrontsResult { group = group, solids = solids, walk = walk, boards = boards };
        }
    }
}
