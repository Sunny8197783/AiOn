// Ports levels/apartment-view.js: what the windows of 103동 look out on, driven only by setView('day'|'dusk'|'night'|
// 'blackout') so the view matches the story time whatever interior preset is active: the parking lot and planting in
// front of 103 with parked cars, autumn trees and street lamps (light pools at night), the corridor faces of 102·104·105동
// (corridor lamps and kitchen windows lit at night, dark with green exit dots and a few torches in a blackout), and a far
// ring of city and hills with stars and the moon. Everything is unlit (Unlit, no fog) and tinted per view; nothing merged.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ApartmentParts;

namespace Gyeongbo.Levels
{
    public sealed class AptView
    {
        public GameObject group;
        public Action<string> setView;
        public Action<float> update;
        public List<string> views = new List<string>();
    }

    public static class ApartmentView
    {
        sealed class Mover { public float x, z, v; }

        // three.js color.copy(base).multiplyScalar(k) on a linear colour -> the sRGB colour Mat takes.
        static Color LinScaled(Color lin, float k) => new Color(lin.r * k, lin.g * k, lin.b * k, 1).gamma;

        public static AptView BuildView(AptBuilder B)
        {
            var kit = B.kit; var r = Kit.Rng(1033); var group = J.Group("apt-view");
            var tinted = new List<(Material mat, Color @base)>(); // base: linear (material colour x instance colour)
            Material basic(MatSpec o, int @base = 0xffffff, Color? inst = null)
            {
                o.fog = false; var m = Mat.Unlit(o);
                var b = J.Hex(@base).linear; if (inst.HasValue) b = new Color(b.r * inst.Value.r, b.g * inst.Value.g, b.b * inst.Value.b, 1);
                tinted.Add((m, b)); return m;
            }
            var L = new Vector3(0.3f, 0.85f, 0.45f).normalized;
            GeoData shade(GeoData geo) // bake simple sun shading into vertex colours (Unlit has no lighting); linear like three.js attributes
            {
                geo.color = new List<Color>(geo.normal.Count);
                foreach (var nrm in geo.normal) { float k = 0.5f + 0.5f * Mathf.Max(0, Vector3.Dot(nrm, L)); geo.color.Add(new Color(k, k, k, 1)); }
                return geo;
            }
            GameObject add(GameObject m) { group.Add(m); return m; }
            GameObject inst(GeoData geo, Material m, IList<Xform> list) => add(kit.Instanced(geo, m, list, false, false));

            // ---- far ring (city + hills + sky), sky cap ----
            var far = kit.Canvas(2048, 512); var farTex = kit.Texture(far); farTex.wrapModeV = TextureWrapMode.Clamp;
            var farMat = basic(new MatSpec { map = farTex, side = "back" });
            var ring = add(Geo.Obj(Geo.Cylinder(150, 150, 120, 72, 1, true, Mathf.PI), farMat, "far-ring", false, false)); SetPos(ring, 2, 58, 2);
            var capMat = Mat.Unlit(new MatSpec { fog = false, side = "double" });
            var cap = add(Geo.Obj(Geo.Circle(151, 48), capMat, "sky-cap", false, false)); SetRot(cap, Math.PI / 2, 0, 0); SetPos(cap, 2, 117.9, 2);
            var layout = new AptViewLayout();
            for (int i = 0; i < 900; i++) layout.stars.Add(new AptViewLayout.Star { x = r() * 2048, y = r() * 380, s = r() < 0.08f ? 2 : 1, a = 0.3f + r() * 0.7f });
            foreach (var k in new[] { 0, 1 })
                layout.ridges.Add(Enumerable.Range(0, 257).Select(i => (k != 0 ? 34 : 62) + Math.Sin(i * 0.05 + k * 2) * (k != 0 ? 14 : 26) + Math.Sin(i * 0.17 + k) * 9 + Math.Sin(i * 0.41) * 4).ToArray());
            for (int i = 0; i < 260; i++)
            {
                bool tall = r() < 0.12f, farL = r() < 0.5f;
                float x = r() * 2048, w = 8 + r() * (tall ? 18 : 34), h = (tall ? 70 + r() * 60 : 10 + r() * 46) * (farL ? 0.7f : 1);
                layout.bldgs.Add(new AptViewLayout.Bldg { x = x, w = w, h = h, far = farL });
            }
            layout.bldgs = layout.bldgs.OrderByDescending(b => b.far).ToList(); // stable, far ones first
            for (int i = 0; i < 4; i++) { int i40 = i * 40; layout.crosses.Add(layout.bldgs.Where((b, k) => !b.far && b.h < 40 && k > i40).FirstOrDefault()); }

            // ---- the opposite blocks: corridor faces with a per-view facade canvas; plain tinted end walls and roofs ----
            var fac = kit.Canvas(1024, 720); var facTex = kit.Texture(fac);
            Material facMat = basic(new MatSpec { map = facTex }), sideMat = basic(new MatSpec(), 0xd8d3c8), roofMat = basic(new MatSpec(), 0x8a8780);
            void block(double x0, double x1, double z0, double z1) // BoxGeometry with [side, side, roof, roof, facade, facade]
            {
                float w = (float)(x1 - x0), h = 44.8f, d = (float)(z1 - z0), hp = Mathf.PI / 2;
                var m = J.Group("block");
                m.Add(Geo.Obj(Geo.Merge(new[] { Geo.Plane(d, h).RotateY(hp).Translate(w / 2, 0, 0), Geo.Plane(d, h).RotateY(-hp).Translate(-w / 2, 0, 0) }), sideMat, "ends", false, false));
                m.Add(Geo.Obj(Geo.Merge(new[] { Geo.Plane(w, d).RotateX(-hp).Translate(0, h / 2, 0), Geo.Plane(w, d).RotateX(hp).Translate(0, -h / 2, 0) }), roofMat, "roofs", false, false));
                m.Add(Geo.Obj(Geo.Merge(new[] { Geo.Plane(w, h).Translate(0, 0, d / 2), Geo.Plane(w, h).RotateY(Mathf.PI).Translate(0, 0, -d / 2) }), facMat, "fronts", false, false));
                SetPos(m, (x0 + x1) / 2, 22.4 - 0.05, (z0 + z1) / 2); add(m);
            }
            block(6, 62, 50, 62); block(-74, -22, 58, 70); block(-30, 26, -62, -50);

            // ---- ground: grass base, planting strips, sidewalks, the lot (with bay lines), inner roads ----
            MatSpec surf(string name) { var s = kit.Surface(name); return new MatSpec { map = s.mainTexture, repeat = s.mainTextureScale }; } // kit.surface(name).map
            Material grass = basic(surf("grass"), 0xc8d4b8), walk = basic(surf("sidewalk")), asph = basic(surf("asphalt"));
            void plane(double w, double d, Material m, double x, double z, double y) => add(B.Plane(w, d, m, A(x: x, y: y, z: z, cast: false, receive: false)));
            plane(320, 320, grass, 2, 2, -0.06);
            plane(90, 2.0, walk, 0, 13.5, -0.04); plane(90, 2.0, walk, 0, 33, -0.04); plane(90, 2, walk, 0, 45, -0.04);
            plane(70, 17.5, asph, -2, 23.25, -0.035); plane(110, 7, asph, 0, 40.5, -0.035); plane(80, 6.5, asph, 0, -16.8, -0.035);
            var lines = kit.Canvas(1024, 256, (c, wi, hi) =>
            {
                double w = wi, h = hi; c.clearRect(0, 0, wi, hi); c.fillStyle = "rgba(245,245,240,0.9)";
                for (double x = 0; x <= w; x += w / 28) { c.fillRect((float)x, 6, 3, 66); c.fillRect((float)x, (float)(h - 72), 3, 66); }
                c.fillRect(0, 70, wi, 3); c.fillRect(0, (float)(h - 73), wi, 3); c.fillStyle = "rgba(242,194,48,0.9)"; c.fillRect(0, (float)(h / 2 - 2), wi, 4);
            });
            var lineMat = basic(new MatSpec { map = kit.Texture(lines), repeat = new Vector2(1 / 70f, 1 / 17.5f), transparent = true, depthWrite = false }); // kit.plane UVs are in metres
            plane(70, 17.5, lineMat, -2, 23.25, -0.02);

            // ---- trees (autumn mix), hedge, parked cars, street lamps with light pools ----
            var spots = new List<(double x, double z)>();
            for (int x = -42; x < 50; x += 7) spots.Add((x + (r() - 0.5) * 2, 35.6 + (r() - 0.5)));
            // The row in front of 103동 stands 3 m clear of the balconies (outer rail z = 9.4) with smaller crowns, so no
            // branch pokes through the 302호 베란다 (it used to: z 11.2 with crowns up to 3.4 m wide).
            int nearRow = spots.Count;
            for (int x = -38; x < 40; x += 9) spots.Add((x + (r() - 0.5) * 2, 12.6));
            int nearEnd = spots.Count;
            for (int x = -30; x < 30; x += 8) spots.Add((x + r() * 2, -23 + (r() - 0.5) * 2));
            var crowns = spots.Select((p, i) => { double s = i >= nearRow && i < nearEnd ? 1.8 + r() * 0.7 : 2.2 + r() * 1.2; return X(x: p.x, y: 2.6 + s * 0.9, z: p.z, sx: s, sy: s * 1.15, sz: s, ry: r() * 6); }).ToList();
            var crownPalette = new[] { "#5a7a3a", "#4f6e34", "#c9a23a", "#c46a2a", "#a8442e", "#6b8a40" };
            var crownColors = crowns.Select(_ => crownPalette[(int)(r() * 6)]).ToList();
            add(B.InstancedColors(shade(Geo.Icosahedron(1, 1)), c => basic(new MatSpec { vertexColors = true }, 0xffffff, c), crowns, crownColors, false, false, out _));
            inst(shade(Geo.Cylinder(0.16f, 0.22f, 3, 6).Translate(0, 1.5f, 0)), basic(new MatSpec { vertexColors = true }, 0x4b3a2c), spots.Select(p => X(x: p.x, z: p.z)).ToList());
            var hedge = new List<Xform>();
            for (double x = -40; x < 40; x += 1.6) if (x < -8 || x > 1) hedge.Add(X(x: x, y: 0.3, z: 10.2, sx: 1.0, sy: 0.55, sz: 0.8));
            inst(shade(Geo.Icosahedron(1, 0)), basic(new MatSpec { vertexColors = true }, 0x3f5a2c), hedge);
            var cars = new List<Xform>();
            foreach (var zc in new[] { 17.6, 28.9 }) for (double x = -35.5; x < 31; x += 2.5) if (r() < 0.68f) { double z = zc + (r() - 0.5) * 0.3, ry = (r() - 0.5) * 0.04; cars.Add(X(x: x, y: 0.28, z: z, ry: ry)); }
            var carColors = cars.Select(_ => { float k = r(); return k < 0.36f ? "#ecebe7" : k < 0.56f ? "#1c1d20" : k < 0.76f ? "#b8bcc0" : k < 0.9f ? "#6e7378" : k < 0.95f ? "#26324a" : "#8a2a2a"; }).ToList();
            add(B.InstancedColors(shade(Geo.Box(1.84f, 0.78f, 4.6f).Translate(0, 0.39f, 0)), c => basic(new MatSpec { vertexColors = true }, 0xffffff, c), cars, carColors, false, false, out _));
            inst(shade(Geo.Box(1.6f, 0.56f, 2.5f).Translate(0, 1.06f, -0.2f)), basic(new MatSpec { vertexColors = true }, 0x2a3038), cars);
            var posts = new List<(double x, double z)>();
            foreach (var z in new[] { 14.7, 32.2 }) for (int x = -34; x < 34; x += 13) posts.Add((x, z));
            inst(shade(Geo.Cylinder(0.07f, 0.09f, 6.2f, 8).Translate(0, 3.1f, 0)), basic(new MatSpec { vertexColors = true }, 0x5d6368), posts.Select(p => X(x: p.x, z: p.z)).ToList());
            var headMat = Mat.Unlit(new MatSpec { fog = false });
            inst(Geo.Box(0.55f, 0.12f, 0.28f), headMat, posts.Select(p => X(x: p.x, y: 6.15, z: p.z)).ToList());
            var poolTex = kit.Texture(kit.Canvas(128, 128, (c, w, h) => { var gr = new PaintGradient(w / 2f, h / 2f, 0, w / 2f, h / 2f, w / 2f); gr.addColorStop(0, "rgba(255,214,150,0.55)"); gr.addColorStop(1, "rgba(255,190,120,0)"); c.fillStyle = gr; c.fillRect(0, 0, w, h); }));
            var poolMat = Mat.Unlit(new MatSpec { map = poolTex, transparent = true, depthWrite = false, blending = "additive", fog = false });
            var pools = inst(Geo.Plane(12, 12).RotateX(-Mathf.PI / 2), poolMat, posts.Select(p => X(x: p.x, y: 0.02, z: p.z)).ToList());

            // ---- traffic on the inner road: a few cars loop east-west; head/tail lamps at dusk, at night and in a blackout ----
            var movers = new List<Mover> { new Mover { x = -50, z = 41.7f, v = 7 }, new Mover { x = 8, z = 41.7f, v = 6 }, new Mover { x = 30, z = 39.3f, v = -6.5f }, new Mover { x = -20, z = 39.3f, v = -7.5f } }; // keep right
            var mx = movers.Select(c => X(x: c.x, z: c.z)).ToList();
            var mBody = add(B.InstancedColors(shade(Geo.Box(4.5f, 0.78f, 1.84f).Translate(0, 0.67f, 0)), c => basic(new MatSpec { vertexColors = true }, 0xffffff, c), mx, new[] { "#ecebe7", "#1c1d20", "#b8bcc0", "#26324a" }, false, false, out var bodyItems));
            var mCab = inst(shade(Geo.Box(2.4f, 0.56f, 1.6f).Translate(-0.15f, 1.34f, 0)), basic(new MatSpec { vertexColors = true }, 0x2a3038), mx);
            var lampParts = new List<GeoData>();
            foreach (var (x, y, cr, cg, cb) in new[] { (2.26f, 0.7f, 4f, 4f, 3.4f), (-2.26f, 0.75f, 3f, 0.15f, 0.08f) })
                foreach (var z in new[] { -0.62f, 0.62f })
                {
                    var b = Geo.Box(0.04f, 0.12f, 0.3f).Translate(x, y, z);
                    b.color = Enumerable.Repeat(new Color(cr, cg, cb, 1), b.pos.Count).ToList(); lampParts.Add(b);
                }
            var mLamp = inst(Geo.Merge(lampParts), Mat.Unlit(new MatSpec { fog = false, vertexColors = true }), mx);
            var traffic = movers.Select((_, i) => new[] { bodyItems[i], mCab.transform.GetChild(i).gameObject, mLamp.transform.GetChild(i).gameObject }).ToList();
            void update(float dt)
            {
                for (int i = 0; i < movers.Count; i++)
                {
                    var c = movers[i];
                    c.x += c.v * Mathf.Min(dt, 0.1f); if (c.x > 62) c.x = -62; else if (c.x < -62) c.x = 62;
                    foreach (var m in traffic[i]) { J.SetPos(m, c.x, -0.03f, c.z); J.SetYaw(m, c.v > 0 ? 0 : Mathf.PI); }
                }
            }

            string current = null;
            void setView(string name)
            {
                if (name == null || !ApartmentViewDraw.VIEWS.TryGetValue(name, out var V)) { Debug.LogError($"[apartment] unknown windowView \"{name}\"; use {string.Join(" | ", ApartmentViewDraw.NAMES)}"); return; }
                if (current == name) return;
                current = name;
                ApartmentViewDraw.DrawFar(far, far.width, far.height, V, layout); far.Upload(farTex);
                ApartmentViewDraw.DrawFacade(fac, fac.width, fac.height, V); fac.Upload(facTex);
                foreach (var t in tinted) Mat.SetColor(t.mat, LinScaled(t.@base, V.tint));
                Mat.SetColor(farMat, Lin(V.far, V.far, V.far)); Mat.SetColor(facMat, Lin(V.fac, V.fac, V.fac)); Mat.SetColor(capMat, J.Css(V.cap));
                Mat.SetColor(headMat, V.lamps != 0 ? Lin(5, 4.2, 3) : Scale(J.Hex(0xb9bcc0), V.tint));
                pools.SetActive(V.lamps != 0); mLamp.SetActive(name != "day");
            }
            setView("day"); update(0);
            return new AptView { group = group, setView = setView, update = update, views = ApartmentViewDraw.NAMES.ToList() };
        }
    }
}
