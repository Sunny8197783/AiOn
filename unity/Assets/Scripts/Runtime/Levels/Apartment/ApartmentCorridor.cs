// Ports levels/apartment-corridor.js: the 3rd-floor corridor (복도식) — doors 301-304 (numbers only), kitchen windows
// with security grilles, gas pipe and meters, the elevator, the stair fire door, the 옥내소화전 cabinet with a 분말소화기,
// everyday clutter, and the spot near 304 where the hallway fire can appear. 303's entry is modelled behind its door
// so 서연 can stand in it.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ApartmentParts;

namespace Gyeongbo.Levels
{
    public static class ApartmentCorridor
    {
        // Unit door spans along x: 301..304.
        public static readonly Dictionary<int, double[]> DOORS = new Dictionary<int, double[]>
        {
            [301] = new[] { -12.6, -11.65 }, [302] = new[] { -1.75, -0.8 }, [303] = new[] { 9.0, 9.95 }, [304] = new[] { 16.0, 16.95 },
        };
        static readonly Dictionary<int, double[]> KITCHEN = new Dictionary<int, double[]>
        {
            [301] = new[] { -10.4, -9.1 }, [302] = new[] { -6.2, -4.9 }, [303] = new[] { 11.0, 12.3 }, [304] = new[] { 19.9, 21.2 },
        };
        static readonly int[] IDS = { 301, 302, 303, 304 };   // Object.entries order
        public static readonly Vector3 FIRE_SPOT = new Vector3(19.2f, ApartmentParts.F3, -0.42f);   // JS space

        public static void BuildCorridor(AptBuilder B, GameObject g, GameObject dyn, AptHandles H)
        {
            var kit = B.kit; var M = B.M; double Y = F3, CY = Y + 2.4;
            g.Add(B.Slab(38, 0.2, 2.1, M.corridorFloor, A(x: 3, y: Y - 0.2, z: -0.95)));
            B.Floor(-16, 22, -2, 0, Y);
            g.Add(B.Slab(38, 0.1, 2.1, M.ceiling, A(x: 3, y: CY, z: -0.95)));
            g.Add(B.Slab(38.4, 0.3, 2.5, M.concrete, A(x: 3, y: CY + 0.1, z: -1.05)));

            var wins = new List<WallHole>();
            for (double x = -15.6; x < 21; x += 4) wins.Add(new WallHole(x, Math.Min(x + 3.2, 21.6), Y + 0.95, Y + 2.15));
            B.WallZ(g, -1.9, -16, 22, Y - 0.2, CY + 0.1, M.corridorWall, new WallOpts { t = 0.25, side = -1, holes = wins });
            foreach (var w in wins)
            {
                B.Window(g, "z", -2.02, w.a, w.b, w.y0, w.y1, mat: M.alu, split: 2);
                g.Add(B.Box(w.b - w.a, 0.03, 0.12, M.stone, A(x: (w.a + w.b) / 2, y: Y + 0.92, z: -1.86)));
            }
            var holes = new List<WallHole> { new WallHole(0.6, 1.8, Y, Y + 2.1), new WallHole(4.55, 5.45, Y, Y + 2.1, true) };
            foreach (var id in IDS) { var d = DOORS[id]; holes.Add(new WallHole(d[0], d[1], Y, Y + 2.1, id == 302 || id == 303)); }
            foreach (var id in IDS) { var k = KITCHEN[id]; holes.Add(new WallHole(k[0], k[1], Y + 1.1, Y + 1.9)); }
            B.WallZ(g, 0, -16, 22, Y - 0.2, CY + 0.1, M.corridorWall, new WallOpts { side = -1, holes = holes });
            B.WallX(g, -16, -2, 0, Y - 0.2, CY + 0.1, M.corridorWall, new WallOpts { side = 1 });
            B.WallX(g, 22, -2, 0, Y - 0.2, CY + 0.1, M.corridorWall, new WallOpts { side = -1 });
            foreach (var z in new[] { -0.106, -1.894 }) g.Add(B.Box(38, 0.09, 0.012, M.skirting, A(x: 3, y: Y, z: z, cast: false)));

            // Unit doors (steel, opening outward), number plates, digital locks, delivery boxes.
            foreach (var id in IDS)
            {
                var d = DOORS[id]; double a = d[0], b = d[1];
                bool live = id == 302 || id == 303;
                var D = B.Door(live ? dyn : g, "z", 0, a, b, Y, 2.1, M.unitDoor, hinge: id == 303 ? "b" : "a", dir: -1, @lock: true, solid: live);
                if (id == 302) H.door302 = B.AutoDoor(D, 1.4);
                if (id == 303) H.door303 = D;
                B.Plate(g, id.ToString(), (a + b) / 2, Y + 2.3, -0.106, ry: Math.PI, w: 0.26, h: 0.12, bg: "#efe9dc", fg: "#33302b", border: "#8a8378");
                g.Add(B.Box(0.24, 0.3, 0.05, M.metal, A(x: b + 0.32, y: Y + 1.0, z: -0.125)));
            }
            // Kitchen windows onto the corridor: frosted, lit from inside while there is power; 방범창 grilles; gas meters.
            var meter = kit.Mat("paint:#cfd2d0", new MatSpec { roughness = 0.5f });
            foreach (var id in IDS)
            {
                var k = KITCHEN[id]; double a = k[0], b = k[1];
                var pane = B.OnPower(Mat.Lit(new MatSpec { color = J.Hex(0xdfe4e2), emissive = J.Hex(0xffd6a0), emissiveIntensity = id == 302 ? 0.15f : (float)(0.3 + (id % 3) * 0.12), roughness = 0.4f, transparent = true, opacity = 0.9f }));
                if (id == 301) H.candleWindow = pane;
                B.Window(g, "z", 0, a, b, Y + 1.1, Y + 1.9, mat: M.alu, glass: pane, split: 2, depth: 0.08);
                for (double x = a + 0.12; x < b - 0.05; x += 0.13) g.Add(B.Box(0.016, 0.82, 0.016, M.darkMetal, A(x: x, y: Y + 1.09, z: -0.17)));
                foreach (var yy in new[] { Y + 1.11, Y + 1.86 }) g.Add(B.Box(b - a + 0.06, 0.03, 0.03, M.darkMetal, A(x: (a + b) / 2, y: yy, z: -0.17)));
                g.Add(B.Box(0.24, 0.3, 0.13, meter, A(x: b + 0.35, y: Y + 1.55, z: -0.165)));
                B.Rod(g, V3(b + 0.35, Y + 1.85, -0.07), V3(b + 0.35, Y + 2.25, -0.07), 0.014, M.yellowPipe);
            }
            B.Rod(g, V3(-15.9, Y + 2.25, -0.07), V3(21.9, Y + 2.25, -0.07), 0.016, M.yellowPipe);

            ApartmentCore.BuildElevator(B, g, (float)Y, H);
            B.Point("elevator", "엘리베이터", 1.2, Y, -0.6, "elevator");
            B.Point("stairs3F", "계단", 5.0, Y, -0.7, "stairs");
            B.ExitSign(g, 5.0, Y + 2.25, -0.13, Math.PI, "exit");
            B.ExitSign(g, -6.5, Y + 0.9, -0.12, Math.PI, "pass", "left");
            B.ExitSign(g, 14.5, Y + 0.9, -0.12, Math.PI, "pass", "right");
            if (kit.Quality != "low") { var gl = kit.Lit("emergency", kit.PointLight(J.Hex(0x35ff7a), 1.2f, 5, 2), 1.2f); SetPos(gl.gameObject, 5.0, Y + 2.0, -0.5); dyn.Add(gl); }
            foreach (var x in new[] { -14, -10, -6, -2, 2, 6, 10, 14, 18 }) B.sensors.Add(new Sensor { x = x, y = (float)(CY - 0.02), z = -0.95f, kind = "sensor", floor = 3 });

            // 옥내소화전 cabinet (with bell and the always-lit red location lamp) and a 분말소화기 beside it.
            var hyd = B.Tex("hydrant", 256, 448, (c, w, h) =>
            {
                c.fillStyle = "#c4161c"; c.fillRect(0, 0, w, h); c.strokeStyle = "#7d0d10"; c.lineWidth = 6; c.strokeRect(10, 10, w - 20, h - 20);
                c.fillStyle = "#fff"; c.textAlign = "center"; c.font = Fnt(800, 64); c.fillText("소화전", w / 2, 120); c.font = Fnt(600, 26); c.fillText("옥내소화전", w / 2, 168);
                c.strokeStyle = "#fff"; c.lineWidth = 8; c.beginPath(); c.arc(w / 2, 290, 70, 0, 7); c.stroke(); c.beginPath(); c.arc(w / 2, 290, 28, 0, 7); c.stroke();
                c.fillStyle = "#e8e8e8"; c.fillRect(w * 0.68f, 382, 44, 16);
            });
            g.Add(B.Box(0.85, 1.5, 0.18, M.red, A(x: 6.125, y: Y + 0.25, z: -0.19)));
            var face = Obj(Geo.Plane(0.8f, 1.42f), Mat.Lit(new MatSpec { map = hyd, roughness = 0.4f, metalness = 0.2f }));
            SetPos(face, 6.125, Y + 1.0, -0.282); SetYaw(face, Math.PI); g.Add(face);
            g.Add(B.Cyl(0.08, 0.08, 0.05, M.metal, A(x: 5.95, y: Y + 1.86, z: -0.2, rx: Math.PI / 2)));
            g.Add(B.Cyl(0.04, 0.04, 0.05, B.redLamp ??= kit.Lit("emergency", B.GlowMat("#ff2a1a", 3), 3), A(x: 6.3, y: Y + 1.82, z: -0.2)));
            B.Solid(5.7, 6.55, -0.32, 0, Y, Y + 1.8);
            var ext = J.Group("extinguisher"); SetPos(ext, 6.95, Y, -0.24); g.Add(ext);
            var band = B.Tex("extLabel", 256, 64, (c, w, h) => { c.fillStyle = "#f5f1e6"; c.fillRect(0, 0, w, h); c.fillStyle = "#b01318"; c.font = Fnt(800, 30); c.textAlign = "center"; c.fillText("분말소화기", w / 2, 44); });
            ext.Add(B.Cyl(0.075, 0.075, 0.44, M.red, A(), 20), B.Cyl(0.05, 0.075, 0.04, M.red, A(y: 0.44), 20), B.Cyl(0.02, 0.02, 0.06, M.darkMetal, A(y: 0.48)));
            ext.Add(B.Cyl(0.0765, 0.0765, 0.14, Mat.Lit(new MatSpec { map = band, roughness = 0.5f }), A(y: 0.16), 20));
            ext.Add(B.Box(0.15, 0.018, 0.03, M.metal, A(x: 0.03, y: 0.54)), B.Box(0.12, 0.012, 0.03, M.metal, A(x: 0.03, y: 0.51)));
            ext.Add(B.Cyl(0.022, 0.022, 0.012, M.white, A(x: -0.035, y: 0.49, z: -0.03, rx: Math.PI / 2)));
            B.Rod(ext, V3(0.045, 0.49, 0), V3(0.09, 0.12, -0.03), 0.009, M.rubber);
            B.Plate(g, "소화기", 6.95, Y + 1.55, -0.106, ry: Math.PI, w: 0.3, h: 0.14, bg: "#c4161c", fg: "#ffffff");
            B.Solid(6.85, 7.05, -0.34, -0.12, Y, Y + 0.6);
            B.Point("extinguisher", "소화기·소화전", 6.6, Y, -0.65, "extinguisher");

            // Clutter: parcels at 301, a doormat and plant at 303, a shoe rack at 304.
            g.Add(B.Box(0.5, 0.34, 0.4, M.cardboard, A(x: -13.2, y: Y, z: -0.32, ry: 0.08)), B.Box(0.36, 0.22, 0.3, M.cardboard, A(x: -13.18, y: Y + 0.34, z: -0.3, ry: -0.1)));
            g.Add(B.Box(0.14, 0.004, 0.09, M.white, A(x: -13.2, y: Y + 0.56, z: -0.3, cast: false)));
            B.Solid(-13.5, -12.9, -0.55, -0.1, Y, Y + 0.6);
            g.Add(B.Box(0.8, 0.012, 0.5, M.rubber, A(x: 9.475, y: Y, z: -0.4, cast: false)));
            g.Add(B.Cyl(0.16, 0.12, 0.34, kit.Mat("paint:#d8d2c6"), A(x: 10.75, y: Y, z: -0.3)));
            var leaf = Obj(Geo.Icosahedron(0.26f, 1), kit.Mat("foliage"), cast: true); SetPos(leaf, 10.75, Y + 0.62, -0.3); leaf.transform.localScale = new Vector3(1, 1.3f, 1); g.Add(leaf);
            for (int i = 0; i < 3; i++) g.Add(B.Box(1.0, 0.02, 0.3, M.darkMetal, A(x: 18.1, y: Y + 0.05 + i * 0.25, z: -0.25)));
            foreach (var x in new[] { 17.62, 18.58 }) g.Add(B.Box(0.02, 0.6, 0.3, M.darkMetal, A(x: x, y: Y, z: -0.25)));
            var shoes = new List<Xform>();
            for (int i = 0; i < 7; i++) shoes.Add(X(x: 17.75 + (i % 4) * 0.24, y: Y + 0.11 + (i / 4) * 0.25, z: -0.25, ry: (i % 2) * 0.2));
            g.Add(B.InstancedPaint(Geo.Box(0.11f, 0.08f, 0.27f), "#ffffff", 0.7, shoes, new[] { "#2b2b2b", "#e8e4dc", "#7a4b2c", "#3d5a80", "#c94f4f", "#e8e4dc", "#2b2b2b" }, cast: false));
            B.Solid(17.6, 18.6, -0.42, -0.1, Y, Y + 0.6);

            // The hallway-fire spot: recycling boxes and a newspaper bundle (normal / scorched), soot and extinguisher powder.
            var fp = J.Group("fire-spot"); SetPos(fp, FIRE_SPOT.x, Y, FIRE_SPOT.z); dyn.Add(fp);
            // JS hides scorched here; it stays active until Merge (which may skip inactive objects) and setFire('none') hides it.
            var normal = J.Group("normal"); var scorched = J.Group("scorched"); fp.Add(normal, scorched);
            var @char = kit.Mat("paint:#2a211b", new MatSpec { roughness = 1 }); var paper = kit.Mat("paint:#d9d6cc", new MatSpec { roughness = 1 }); var ash = kit.Mat("paint:#4a4541", new MatSpec { roughness = 1 });
            var boxes = new[] { new[] { 0.55, 0.4, 0.42, -0.1, 0, 0.02, 0.1 }, new[] { 0.4, 0.3, 0.34, -0.12, 0.4, 0.04, -0.2 }, new[] { 0.42, 0.18, 0.3, 0.38, 0, 0, 0.3 } };
            for (int i = 0; i < boxes.Length; i++)
            {
                var bx = boxes[i]; double w = bx[0], h = bx[1], d = bx[2], x = bx[3], y = bx[4], z = bx[5], ry = bx[6];
                normal.Add(B.Box(w, h, d, i == 2 ? paper : M.cardboard, A(x: x, y: y, z: z, ry: ry)));
                scorched.Add(B.Box(w * 0.96, h * (i == 1 ? 0.5 : 0.85), d * 0.96, i == 2 ? ash : @char, A(x: x, y: y, z: z, ry: ry + 0.05)));
            }
            var soot = Obj(Geo.Plane(1.4f, 1.6f), Mat.Lit(new MatSpec { map = B.Tex("soot", 128, 128, Blot("rgba(18,14,12,")), transparent = true, depthWrite = false, roughness = 1 }));
            SetPos(soot, 0, 0.85, 0.31); SetYaw(soot, Math.PI); soot.SetActive(false); fp.Add(soot);
            var powder = Obj(Geo.Plane(2.4f, 1.7f), Mat.Lit(new MatSpec { map = B.Tex("powder", 256, 256, Blot("rgba(250,248,240,")), transparent = true, depthWrite = false, roughness = 1 }));
            SetRot(powder, -Math.PI / 2, 0, 0); SetPos(powder, -0.2, 0.006, -0.45); powder.SetActive(false); fp.Add(powder);
            H.fire = new AptFire { group = fp, normal = normal, scorched = scorched, soot = soot, powder = powder };
            B.Solid(FIRE_SPOT.x - 0.45, FIRE_SPOT.x + 0.65, -0.7, 0, Y, Y + 0.7);
            H.firePoint = B.Point("hallFire", "304호 앞 복도", FIRE_SPOT.x - 0.4, Y, -1.0, "fire", 3.0);

            // 303 entry behind its door (seen when 서연 opens it): tiles, step, shoe cabinet, a warm lamp.
            var e = J.Group("entry-303"); g.Add(e);
            e.Add(B.Slab(1.6, 0.2, 1.7, M.entryTile, A(x: 9.5, y: Y - 0.2, z: 0.85)), B.Slab(1.6, 0.15, 0.8, M.wood, A(x: 9.5, y: Y, z: 1.3)));
            B.WallX(e, 8.7, 0, 1.7, Y, CY, M.wallpaper, new WallOpts { side = 1, solid = false });
            B.WallX(e, 10.3, 0, 1.7, Y, CY, M.wallpaper, new WallOpts { side = -1, solid = false });
            B.WallZ(e, 1.7, 8.7, 10.3, Y, CY, M.wallpaper, new WallOpts { side = -1, solid = false });
            e.Add(B.Slab(1.6, 0.1, 1.7, M.ceiling, A(x: 9.5, y: CY - 0.1, z: 0.85)), B.Box(0.35, 2.0, 1.1, M.white, A(x: 10.1, y: Y, z: 0.8)));
            e.Add(B.Box(0.5, 0.4, 0.03, M.lightWood, A(x: 9.3, y: Y + 1.3, z: 1.66)), B.Cyl(0.15, 0.15, 0.02, B.OnPower(B.GlowMat("#ffdcae", 2.5)), A(x: 9.5, y: CY - 0.12, z: 0.8, cast: false)));
            B.Solid(9.0, 9.95, 0.05, 0.4, Y, Y + 2.1);
            H.seoyeonSpot = new Spawn(9.475f, (float)Y, 0.62f, 0);
            B.Point("seoyeonDoor", "303호", 9.475, Y, -0.65, "door");
            B.Point("homeDoor", "302호 (집)", -1.275, Y, -0.6, "door");
        }

        // Soft irregular blotch texture (alpha falls off from the centre), for soot and extinguisher powder.
        static Action<Paint, float, float> Blot(string rgba) => (c, w, h) =>
        {
            for (int i = 0; i < 70; i++)
            {
                float a = UnityEngine.Random.value * 6.28f, r = UnityEngine.Random.value * w * 0.3f, x = w / 2 + Mathf.Cos(a) * r, y = h / 2 + Mathf.Sin(a) * r * 0.8f, s = w * (0.08f + UnityEngine.Random.value * 0.14f);
                var gr = new PaintGradient(x, y, 0, x, y, s); gr.addColorStop(0, rgba + "0.22)"); gr.addColorStop(1, rgba + "0)");
                // JS fills the whole canvas; outside radius s the gradient is fully transparent, so only its square changes.
                c.fillStyle = gr; c.fillRect(x - s, y - s, 2 * s, 2 * s);
            }
        };
    }
}
