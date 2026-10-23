// Ports levels/apartment-core.js: 103동 ground floor and stairwell — lobby (glass entrance, mailboxes, notice board,
// 관리사무소 window, parcel lockers, elevator, fire door), and the switchback stairwell from 1F to the 4F landing.
// The stairs are walkable to the 3F landing; the flight above 3F is roped off for wet cleaning.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ApartmentParts;

namespace Gyeongbo.Levels
{
    public sealed class StairDims
    {
        public double x0 = 3.4, x1 = 6.6, zLand = 1.4, zMid = 3.6, zEnd = 5.0, top = 11.2;
        public double[] laneA = { 5.1, 6.6 }, laneB = { 3.4, 4.9 };
    }

    // The elevator car's wandering (H.elev in the JS).
    public sealed class ElevState
    {
        public int floor = 7, target = 1;
        public float wait, step;
    }

    // Things the level wires up later (H in the JS): door302, door303, elev*, fire, firePoint, seoyeonSpot, candleWindow.
    public sealed class AptHandles
    {
        public AptDoor door302, door303;
        public ElevState elev;
        public Paint elevCanvas;
        public Texture2D elevTex;
        public Material elevMat, candleWindow;
        public AptFire fire;
        public Point firePoint;
        public Spawn seoyeonSpot;
    }

    public sealed class AptFire
    {
        public GameObject group, normal, scorched, soot, powder;
    }

    public static class ApartmentCore
    {
        public static readonly StairDims STAIR = new StairDims();
        static double YOf(int n) => 2.8 * (n - 1);   // FH * (n - 1), in JS doubles

        public static void BuildLobby(AptBuilder B, GameObject g)
        {
            var kit = B.kit; var M = B.M;
            g.Add(B.Slab(10, 0.2, 6.6, M.lobbyFloor, A(x: 4, y: -0.2, z: -3.3)));
            B.Floor(-1, 9, -6.5, 0, 0);
            g.Add(B.Slab(10.2, 0.08, 6.7, M.ceiling, A(x: 4, y: 2.6, z: -3.3)));
            g.Add(B.Slab(10.6, 0.45, 7.0, M.concrete, A(x: 4, y: 2.68, z: -3.45))); // roof of the entrance volume
            B.WallX(g, -1, -6.5, 0, 0, 2.6, M.lobbyWall, new WallOpts { side = 1 });
            var win = new List<WallHole> { new WallHole(-5.2, -3.0, 0.95, 2.0) };
            B.WallX(g, 9, -6.5, 0, 0, 2.6, M.lobbyWall, new WallOpts { side = -1, holes = win });
            B.WallZ(g, 0, -1, 9, 0, 2.6, M.lobbyWall, new WallOpts { side = -1, holes = { new WallHole(0.6, 1.8, 0, 2.1), new WallHole(4.55, 5.45, 0, 2.1, true) } });
            foreach (var (x0, x1, z) in new[] { (-0.9, 8.9, -0.1), (-0.9, 8.9, -6.4) }) g.Add(B.Box(x1 - x0, 0.09, 0.015, M.stone, A(x: (x0 + x1) / 2, z: z, cast: false)));

            // North glass front with the double entrance doors (closed; 'lobbyExit' leads out).
            foreach (var x in new[] { -0.95, 1, 3, 5, 7, 8.95 }) g.Add(B.Box(0.1, 2.6, 0.14, M.alu, A(x: x, z: -6.5)));
            g.Add(B.Box(10, 0.35, 0.12, M.alu, A(x: 4, y: 2.25, z: -6.5)));
            g.Add(B.Box(10, 0.1, 0.12, M.alu, A(x: 4, z: -6.5)));
            foreach (var (a, b) in new[] { (-0.9, 0.95), (1.05, 2.95), (5.05, 6.95), (7.05, 8.9) }) g.Add(B.Box(b - a, 2.15, 0.012, M.glass, A(x: (a + b) / 2, y: 0.1, z: -6.5, cast: false)));
            foreach (var x in new[] { 3.5, 4.5 }) // door leaves: aluminium frame, glass, long pull bar
            {
                g.Add(B.Box(0.96, 2.15, 0.012, M.glass, A(x: x, y: 0.1, z: -6.5, cast: false)));
                foreach (var dx in new[] { -0.46, 0.46 }) g.Add(B.Box(0.05, 2.15, 0.05, M.alu, A(x: x + dx, y: 0.1, z: -6.5)));
                g.Add(B.Box(0.96, 0.12, 0.05, M.alu, A(x: x, y: 0.1, z: -6.5)));
                B.Rod(g, V3(x + (x < 4 ? 0.36 : -0.36), 0.75, -6.42), V3(x + (x < 4 ? 0.36 : -0.36), 1.45, -6.42), 0.014, M.metal);
            }
            B.Solid(-1, 9, -6.62, -6.38, 0, 2.6);
            B.ExitSign(g, 4, 2.42, -6.38, 0, "exit");
            g.Add(B.Box(2.6, 0.015, 1.3, M.rubber, A(x: 4, z: -5.75, cast: false))); // entrance mat

            // Mailboxes (60 units, 15 floors x 4 lines) and the notice board on the west wall.
            var mail = B.Tex("mailboxes", 768, 384, (c, w, h) =>
            {
                var gr = new PaintGradient(0, 0, w, h); gr.addColorStop(0, "#b8bcbf"); gr.addColorStop(1, "#8f9497"); c.fillStyle = gr; c.fillRect(0, 0, w, h);
                int cols = 12, rows = 5; float cw = w / cols, rh = h / rows;
                c.font = Fnt(600, rh * 0.16f); c.textAlign = "center";
                for (int r = 0; r < rows; r++) for (int k = 0; k < cols; k++)
                {
                    int i = r * cols + k, fl = 15 - i / 4, unit = (i % 4) + 1;
                    c.strokeStyle = "#6d7275"; c.lineWidth = 2; c.strokeRect(k * cw + 3, r * rh + 3, cw - 6, rh - 6);
                    c.fillStyle = "#3d4245"; c.fillRect(k * cw + cw * 0.2f, r * rh + rh * 0.3f, cw * 0.6f, rh * 0.06f);
                    c.fillStyle = "#2a2d2f"; c.fillText(fl + "0" + unit, k * cw + cw / 2, r * rh + rh * 0.78f);
                }
            });
            g.Add(B.Box(0.26, 1.4, 2.8, Mat.Lit(new MatSpec { map = mail, roughness = 0.35f, metalness = 0.6f }), A(x: -0.77, y: 0.5, z: -4.5)));
            B.Solid(-0.9, -0.62, -5.9, -3.1, 0, 1.9);
            var notice = B.Tex("lobbyNotice", 600, 400, (c, w, h) =>
            {
                c.fillStyle = "#8a6a48"; c.fillRect(0, 0, w, h); c.fillStyle = "#c9a77c"; c.fillRect(14, 54, w - 28, h - 68);
                c.fillStyle = "#ffffff"; c.font = Fnt(800, 34); c.textAlign = "center"; c.fillText("103동 게시판", w / 2, 40);
                var notes = new[] { ("관리비 고지서 배부", "이번 달 납부 마감 25일"), ("엘리베이터 정기 점검", "목요일 10:00 ~ 12:00"), ("분리수거 요일", "화 · 금 19시 이후"), ("주차 등록 안내", "관리사무소 방문") };
                for (int i = 0; i < notes.Length; i++)
                {
                    var (t, s) = notes[i];
                    float x = 30 + (i % 2) * 285, y = 70 + (i / 2) * 160;
                    c.fillStyle = i == 1 ? "#fff7d6" : "#ffffff"; c.fillRect(x, y, 255, 140);
                    c.fillStyle = "#c4161c"; c.beginPath(); c.arc(x + 127, y + 8, 6, 0, 7); c.fill();
                    c.fillStyle = "#222"; c.font = Fnt(700, 25); c.fillText(t, x + 127, y + 52); c.font = Fnt(500, 20); c.fillStyle = "#555"; c.fillText(s, x + 127, y + 92);
                }
            });
            var nb = Obj(Geo.Plane(1.5f, 1.0f), Mat.Lit(new MatSpec { map = notice, roughness = 0.8f }));
            SetPos(nb, -0.88, 1.45, -1.6); SetYaw(nb, Math.PI / 2); g.Add(nb);
            B.Point("lobbyNotice", "게시판 보기", -0.4, 0, -1.6, "notice");

            // Parcel lockers (무인택배함) with a small screen.
            var lockers = B.Tex("lockers", 512, 512, (c, w, h) =>
            {
                c.fillStyle = "#d7dadc"; c.fillRect(0, 0, w, h);
                for (int r = 0; r < 6; r++) for (int k = 0; k < 4; k++) { c.strokeStyle = "#8b9093"; c.lineWidth = 4; c.strokeRect(k * 128 + 6, r * 85 + 6, 116, 73); c.fillStyle = "#5c6164"; c.fillRect(k * 128 + 98, r * 85 + 36, 10, 14); }
                c.fillStyle = "#1f6fb2"; c.fillRect(0, 0, w, 22); c.fillStyle = "#fff"; c.font = Fnt(700, 16); c.textAlign = "center"; c.fillText("무인택배함", w / 2, 17);
            });
            g.Add(B.Box(2.0, 1.9, 0.5, Mat.Lit(new MatSpec { map = lockers, roughness = 0.45f, metalness = 0.3f }), A(x: 7.8, z: -0.35)));
            g.Add(B.Box(0.3, 0.2, 0.01, B.OnPower(B.GlowMat("#7fc4ff", 1.4)), A(x: 7.2, y: 1.15, z: -0.605, cast: false)));
            B.Solid(6.8, 8.8, -0.6, -0.1, 0, 1.9);

            // Fire alarm set (bell + always-on red location lamp) by the stair door, potted plants, CCTV dome.
            g.Add(B.Box(0.32, 0.5, 0.1, M.red, A(x: 6.2, y: 1.0, z: -0.15)));
            g.Add(B.Cyl(0.07, 0.07, 0.04, M.metal, A(x: 6.2, y: 1.35, z: -0.2, rx: Math.PI / 2)));
            g.Add(B.Cyl(0.035, 0.035, 0.03, B.redLamp ??= kit.Lit("emergency", B.GlowMat("#ff2a1a", 3), 3), A(x: 6.2, y: 1.52, z: -0.2)));
            foreach (var x in new double[] { 0, 8 })
            {
                g.Add(B.Cyl(0.24, 0.19, 0.5, M.stone, A(x: x, z: -5.9)));
                var f = Obj(Geo.Icosahedron(0.42f, 1), kit.Mat("foliage"), cast: true); SetPos(f, x, 0.95, -5.9); f.transform.localScale = new Vector3(1, 1.25f, 1); g.Add(f);
            }
            var dome = Obj(Geo.Sphere(0.07f, 12, 8, 0, Mathf.PI * 2, 0, Mathf.PI / 2), M.darkMetal); SetRot(dome, Math.PI, 0, 0); SetPos(dome, 8.5, 2.6, -6.0); g.Add(dome);
            foreach (var (x, z) in new[] { (1.5, -2.0), (4.0, -2.0), (6.5, -2.0), (1.5, -4.6), (4.0, -4.6), (6.5, -4.6) })
                g.Add(B.Cyl(0.11, 0.11, 0.015, B.lobbyLamp ??= B.OnPower(B.GlowMat("#fff3e2", 3.2)), A(x: x, y: 2.585, z: z, cast: false)));
            B.ExitSign(g, 5.0, 2.42, -0.12, Math.PI, "exit"); // above the stair door, lobby side (faces north)

            // 관리사무소: window with counter, desk, CCTV monitor, binders; 박 소장 sits behind it.
            var office = J.Group("office"); g.Add(office);
            office.Add(B.Slab(3.5, 0.2, 5, M.wood, A(x: 10.75, y: -0.2, z: -4)));
            office.Add(B.Slab(3.5, 0.08, 5, M.ceiling, A(x: 10.75, y: 2.6, z: -4)));
            B.WallX(office, 9, -6.5, -1.5, 0, 2.6, M.paintWhite, new WallOpts { side = 1, holes = win, solid = false });
            B.WallX(office, 12.5, -6.5, -1.5, 0, 2.6, M.paintWhite, new WallOpts { side = -1, solid = false });
            B.WallZ(office, -6.5, 9, 12.5, 0, 2.6, M.paintWhite, new WallOpts { side = 1, solid = false });
            B.WallZ(office, -1.5, 9, 12.5, 0, 2.6, M.paintWhite, new WallOpts { side = -1, solid = false });
            B.Window(office, "x", 9, -5.2, -3.0, 0.95, 2.0, mat: M.alu, split: 2);
            office.Add(B.Box(0.5, 0.04, 2.3, M.lightWood, A(x: 9.0, y: 0.93, z: -4.1)));
            office.Add(B.Box(0.7, 0.74, 2.0, M.lightWood, A(x: 9.5, z: -4.1)));
            var cctv = B.Tex("cctv", 256, 160, (c, w, h) =>
            {
                var labels = new[] { "현관", "주차장", "엘리베이터", "놀이터" };
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2) * w / 2, y = (i / 2) * h / 2;
                    var gr = new PaintGradient(x, y, x, y + h / 2); gr.addColorStop(0, "#5d6668"); gr.addColorStop(1, "#2d3335"); c.fillStyle = gr; c.fillRect(x + 2, y + 2, w / 2 - 4, h / 2 - 4);
                    c.fillStyle = "#9aa3a5"; c.fillRect(x + 20 + i * 9, y + 40, 40, 6); c.fillStyle = "#fff"; c.font = Fnt(600, 11); c.fillText(labels[i], x + 8, y + 16);
                }
            });
            office.Add(B.Box(0.05, 0.36, 0.55, M.black, A(x: 9.45, y: 0.82, z: -4.6)));
            office.Add(B.Box(0.01, 0.32, 0.5, B.OnPower(Mat.Lit(new MatSpec { color = J.Hex(0x050505), emissive = J.Hex(0xffffff), emissiveMap = cctv, emissiveIntensity = 1.3f })), A(x: 9.48, y: 0.84, z: -4.6, cast: false)));
            office.Add(B.Box(0.5, 0.45, 0.5, M.darkMetal, A(x: 10.4, z: -4.1))); // chair seat block
            office.Add(B.Box(0.08, 0.6, 0.5, M.darkMetal, A(x: 10.68, y: 0.45, z: -4.1)));
            var bind = new List<Xform>();
            for (int i = 0; i < 28; i++) bind.Add(X(x: 12.3, y: 0.3 + (i / 14) * 0.7 + 0.15, z: -5.8 + (i % 14) * 0.08, sy: 0.9 + (i % 3) * 0.1));
            office.Add(B.Box(0.35, 1.9, 1.3, M.lightWood, A(x: 12.3, z: -5.35)));
            var bindColors = new[] { "#2f5d8c", "#8c2f3a", "#3d7a4a", "#d8b23a" };
            office.Add(B.InstancedPaint(Geo.Box(0.28f, 0.3f, 0.07f), "#2f5d8c", 0.6, bind, bind.Select((_, i) => bindColors[i % 4]).ToList(), cast: false));
            office.Add(B.Box(1.1, 0.05, 1.1, B.OnPower(B.GlowMat("#fff3e2", 2.5)), A(x: 10.75, y: 2.55, z: -4, cast: false)));
            B.Plate(g, "관리사무소", 8.88, 2.28, -4.1, ry: -Math.PI / 2, w: 0.9, h: 0.22, bg: "#2f4e6e", fg: "#ffffff");
            B.Point("managerWindow", "관리사무소 창구", 8.4, 0, -4.1, "npc");

            // Outside: the 103동 entrance canopy over a short slice of paving.
            g.Add(B.Slab(6.2, 0.25, 3.2, M.exterior, A(x: 4, y: 2.85, z: -8.1)));
            g.Add(B.Plane(16, 7, M.paving, A(x: 4, y: 0.005, z: -10)));
            foreach (var x in new[] { 2.5, 5.5 }) g.Add(B.Cyl(0.12, 0.12, 0.02, B.lobbyLamp, A(x: x, y: 2.835, z: -8.2, cast: false)));
            B.Plate(g, "103동", 4, 2.97, -9.71, ry: Math.PI, w: 1.1, h: 0.22, bg: "#e6e1d6", fg: "#2f4e6e");
            g.Add(B.Box(0.14, 1.2, 0.1, M.darkMetal, A(x: 5.5, z: -6.75))); // 공동현관 keypad post
            g.Add(B.Box(0.1, 0.06, 0.012, B.OnPower(B.GlowMat("#62c1ff", 1.5)), A(x: 5.5, y: 1.12, z: -6.81, cast: false)));
            B.Point("lobbyExit", "밖으로 나가기", 4, 0, -6.0, "door");
            B.Point("elevator", "엘리베이터", 1.2, 0, -0.6, "elevator");
            B.Point("stairs1F", "계단", 5.0, 0, -0.7, "stairs");
        }

        // Elevator front at floor level y: brushed doors, surround, floor indicator (shared canvas), call buttons.
        public static void BuildElevator(AptBuilder B, GameObject g, float y, AptHandles H)
        {
            var kit = B.kit; var M = B.M;
            foreach (var x in new[] { 0.525, 1.875 }) g.Add(B.Box(0.15, 2.35, 0.06, M.metal, A(x: x, y: y, z: -0.12)));
            g.Add(B.Box(1.5, 0.25, 0.06, M.metal, A(x: 1.2, y: y + 2.1, z: -0.12)));
            g.Add(B.Box(1.2, 2.1, 0.02, M.darkMetal, A(x: 1.2, y: y, z: -0.09, cast: false)));
            foreach (var x in new[] { 0.9, 1.5 }) g.Add(B.Box(0.595, 2.09, 0.03, kit.Mat("metal", new MatSpec { roughness = 0.28f, color = J.Hex(0xb9bec2) }), A(x: x, y: y, z: -0.115)));
            g.Add(B.Box(1.2, 0.012, 0.12, M.alu, A(x: 1.2, y: y, z: -0.16, cast: false)));
            if (H.elevMat == null)
            {
                H.elevCanvas = kit.Canvas(128, 64); H.elevTex = kit.Texture(H.elevCanvas);
                H.elevMat = B.OnPower(Mat.Lit(new MatSpec { color = J.Hex(0x050505), emissive = J.Hex(0xffffff), emissiveMap = H.elevTex, emissiveIntensity = 1.8f, roughness = 0.2f }));
                H.elev = new ElevState { floor = 7, target = 1, wait = 0, step = 0 };
                DrawElevator(H);
                B.ticks.Add((dt, t, S) => TickElevator(H, dt));
            }
            g.Add(B.Box(0.4, 0.17, 0.03, M.black, A(x: 1.2, y: y + 2.14, z: -0.165)));
            g.Add(B.Box(0.36, 0.13, 0.005, H.elevMat, A(x: 1.2, y: y + 2.16, z: -0.183, cast: false)));
            g.Add(B.Box(0.1, 0.24, 0.02, M.metal, A(x: 2.1, y: y + 0.95, z: -0.11)));
            var btn = B.OnPower(B.GlowMat("#ffb347", 1.2));
            foreach (var dy in new[] { 0.05, 0.15 }) g.Add(B.Cyl(0.022, 0.022, 0.012, btn, A(x: 2.1, y: y + 0.95 + dy, z: -0.12, rx: Math.PI / 2, cast: false)));
            B.Solid(0.6, 1.8, -0.2, 0.1, y, y + 2.2);
        }

        static void DrawElevator(AptHandles H)
        {
            var c = H.elevCanvas; var e = H.elev;
            c.fillStyle = "#000"; c.fillRect(0, 0, 128, 64);
            c.fillStyle = "#ff7a1a"; c.font = Fnt(700, 44); c.textAlign = "center"; c.textBaseline = "middle";
            c.fillText(e.floor.ToString(CultureInfo.InvariantCulture), 80, 34);
            if (e.target != e.floor) { bool up = e.target > e.floor; c.beginPath(); c.moveTo(30, up ? 16 : 48); c.lineTo(44, up ? 36 : 28); c.lineTo(16, up ? 36 : 28); c.closePath(); c.fill(); }
            c.Upload(H.elevTex);
        }

        static void TickElevator(AptHandles H, float dt) // the car wanders between floors; one floor per 1.6 s, then waits
        {
            var e = H.elev;
            if (e.floor == e.target)
            {
                e.wait -= dt;
                if (e.wait <= 0) { e.target = 1 + UnityEngine.Random.Range(0, 15); e.wait = 4 + UnityEngine.Random.value * 6; DrawElevator(H); }
                return;
            }
            e.step += dt;
            if (e.step >= 1.6f) { e.step = 0; e.floor += Math.Sign(e.target - e.floor); DrawElevator(H); }
        }

        // The stairwell: walls, landings, flights (8 risers x 0.175 m, treads 0.275 m), balustrades, painted floor
        // numbers, motion-sensor lamps under each landing and battery emergency lamps on the walls.
        public static void BuildStairs(AptBuilder B, GameObject g, GameObject dyn)
        {
            var kit = B.kit; var M = B.M; var S = STAIR;
            foreach (var n in new[] { 1, 2, 3, 4 }) // fire doors: 1F opens toward the lobby, 3F into the stairwell; 2F/4F stay shut
            {
                bool auto = n == 1 || n == 3;
                var D = B.Door(auto ? dyn : g, "z", 0, 4.55, 5.45, YOf(n), 2.1, M.fireDoor, hinge: "a", dir: n == 1 ? -1 : 1); // 2F/4F keep a solid: the wall's door gap runs full height
                if (auto) B.AutoDoor(D);
            }
            B.WallX(g, 3.3, 0, 5.1, -0.2, S.top, M.stairWall, new WallOpts { side = 1 });
            B.WallX(g, 6.7, 0, 5.1, -0.2, S.top, M.stairWall, new WallOpts { side = -1 });
            B.WallZ(g, 5.1, 3.3, 6.7, -0.2, S.top, M.stairWall, new WallOpts { side = -1, holes = new[] { 1, 2, 3 }.Select(n => new WallHole(4.3, 5.7, YOf(n) + 2.35, YOf(n) + 3.45)).ToList() });
            B.WallZ(g, 0, 3.3, 6.7, -0.2, S.top, M.stairWall, new WallOpts { side = 1, holes = new[] { 1, 2, 3, 4 }.Select(n => new WallHole(4.55, 5.45, YOf(n), YOf(n) + 2.1, n == 1 || n == 3)).ToList() });
            g.Add(B.Slab(3.4, 0.2, 5.1, M.ceiling, A(x: 5, y: S.top, z: 2.55)));
            foreach (var n in new[] { 1, 2, 3 }) B.Window(g, "z", 5.05, 4.3, 5.7, YOf(n) + 2.35, YOf(n) + 3.45, mat: M.alu);

            var balusters = new List<Xform>(); var laneA = S.laneA; var laneB = S.laneB;
            void flight(double y0, string lane)
            {
                var ab = lane == "A" ? laneA : laneB; double xa = ab[0], xb = ab[1], w = xb - xa, cx = (xa + xb) / 2;
                for (int i = 0; i < 8; i++)
                {
                    double top = y0 + (i + 1) * 0.175, z0 = lane == "A" ? S.zLand + i * 0.275 : S.zMid - (i + 1) * 0.275;
                    g.Add(B.Slab(w, 0.375, 0.275, M.step, A(x: cx, y: top - 0.375, z: z0 + 0.1375)));
                    g.Add(B.Box(w - 0.12, 0.006, 0.05, M.nosing, A(x: cx, y: top, z: lane == "A" ? z0 + 0.03 : z0 + 0.245, cast: false)));
                    double bx = lane == "A" ? xa + 0.04 : xb - 0.04;
                    balusters.Add(X(x: bx, y: top + 0.45, z: z0 + 0.1375, sy: 0.9));
                }
                double L = Math.Sqrt(2.2 * 2.2 + 1.4 * 1.4), ang = Math.Atan2(1.4, 2.2);
                var soffit = Obj(Geo.Box((float)w, 0.16f, (float)(L + 0.12)), M.step, cast: true, receive: true);
                SetPos(soffit, cx, y0 + 0.7 - 0.27, (S.zLand + S.zMid) / 2); SetRot(soffit, lane == "A" ? -ang : ang, 0, 0);
                g.Add(soffit);
                double rx = lane == "A" ? xa + 0.04 : xb - 0.04, wx = lane == "A" ? xb - 0.06 : xa + 0.06;
                double[] lo = lane == "A" ? new[] { S.zLand, y0 } : new[] { S.zMid, y0 + 1.4 }, hi = lane == "A" ? new[] { S.zMid, y0 + 1.4 } : new[] { S.zLand, y0 + 2.8 };
                B.Rod(g, V3(rx, lo[1] + 0.9, lo[0]), V3(rx, hi[1] + 0.9, hi[0]), 0.028, M.darkWood);
                B.Rod(g, V3(wx, lo[1] + 0.85, lo[0]), V3(wx, hi[1] + 0.85, hi[0]), 0.022, M.rail);
            }
            for (int n = 1; n <= 4; n++)
            {
                double y = YOf(n);
                g.Add(B.Slab(3.2, 0.2, 1.3, M.step, A(x: 5, y: y - 0.2, z: 0.75)));
                g.Add(B.Box(3.2, 0.1, 0.012, M.stairBand, A(x: 5, y: y, z: 0.107, cast: false)));
                foreach (var x in new[] { 3.407, 6.593 }) g.Add(B.Box(0.012, 1.0, 1.3, M.stairBand, A(x: x, y: y, z: 0.75, cast: false)));
                B.Plate(g, n + "F", 3.95, y + 1.6, 0.106, w: 0.62, h: 0.44, bg: "#e9e5d8", fg: "#2f5a3c");
                B.sensors.Add(new Sensor { x = 5.0f, y = (float)(y + 2.58), z = 0.75f, kind = "sensor", floor = n });
                EmergencyLamp(B, g, 3.41, y + 2.25, 0.75);
                if (n <= 3) B.Floor(3.4, 6.6, 0.1, 1.6, y);
                if (n == 4) continue;
                g.Add(B.Slab(3.2, 0.2, 1.4, M.step, A(x: 5, y: y + 1.2, z: 4.3)));
                foreach (var x in new[] { 3.407, 6.593 }) g.Add(B.Box(0.012, 0.9, 1.4, M.stairBand, A(x: x, y: y + 1.4, z: 4.3, cast: false)));
                g.Add(B.Box(3.2, 0.9, 0.012, M.stairBand, A(x: 5, y: y + 1.4, z: 4.993, cast: false)));
                B.sensors.Add(new Sensor { x = 5.0f, y = (float)Math.Min(y + 3.98, S.top - 0.02), z = 4.3f, kind = "sensor", floor = n + 0.5f });
                EmergencyLamp(B, g, 3.41, y + 3.65, 4.3);
                flight(y, "A"); flight(y, "B");
                B.Rod(g, V3(laneA[0] + 0.04, y + 2.3, S.zMid), V3(laneB[1] - 0.04, y + 2.3, S.zMid), 0.028, M.darkWood); // well guard at the mid landing
                if (n < 3) { B.Ramp(laneA[0], laneA[1], 1.2625, 3.4625, "z", y, y + 1.4); B.Floor(3.4, 6.6, 3.4, 5.0, y + 1.4); B.Ramp(laneB[0], laneB[1], 1.5375, 3.7375, "z", y + 2.8, y + 1.4); }
            }
            foreach (var n in new[] { 1, 2, 3 }) foreach (var zz in new[] { S.zLand, S.zMid }) balusters.Add(X(x: 5.0, y: YOf(n) + (zz == S.zLand ? 0 : 1.4) + 0.45, z: zz, sy: 0.9));
            g.Add(kit.Instanced(Geo.Box(0.022f, 1, 0.022f), M.rail, balusters, cast: true));

            // Lane divider between the flights, closet under the first flight, and the cleaning sign blocking 3F -> 4F.
            B.Solid(4.9, 5.1, 1.4, 3.6, 0, S.top);
            g.Add(B.Slab(1.5, 2.35, 0.06, M.stairWall, A(x: 4.15, y: 0, z: 1.43)));
            g.Add(B.Box(0.7, 1.9, 0.02, M.fireDoor, A(x: 4.15, y: 0.02, z: 1.465)));
            B.Solid(3.4, 4.9, 1.4, 3.6, 0, 1.3);
            var wet = J.Group("wet-sign"); SetPos(wet, 5.85, YOf(3), 1.15); g.Add(wet);
            var wetTex = B.Tex("wetSign", 256, 384, (c, w, h) =>
            {
                c.fillStyle = "#f2c230"; c.fillRect(0, 0, w, h); c.fillStyle = "#1b1b1b"; c.font = Fnt(800, 46); c.textAlign = "center"; c.fillText("물청소 중", w / 2, 120);
                c.font = Fnt(600, 30); c.fillText("계단 이용 주의", w / 2, 180);
                c.beginPath(); c.moveTo(w / 2, 230); c.lineTo(w / 2 + 60, 340); c.lineTo(w / 2 - 60, 340); c.closePath(); c.lineWidth = 10; c.stroke();
            });
            foreach (var s in new[] { -1, 1 })
            {
                var p = Obj(Geo.Plane(0.34f, 0.6f), Mat.Lit(new MatSpec { map = wetTex, roughness = 0.5f, side = "double" }), cast: true);
                SetPos(p, 0, 0.3, s * 0.1); SetRot(p, s * 0.17, 0, 0); wet.Add(p);
            }
            B.Solid(5.1, 6.6, 1.25, 1.75, YOf(3), YOf(4));
            B.ExitSign(g, 5.0, 2.42, 0.12, 0, "exit"); // above the 1F door, stair side
        }

        // 비상조명등: wall box with two lamp heads that light up on battery when the power fails.
        static void EmergencyLamp(AptBuilder B, GameObject g, double x, double y, double z)
        {
            var M = B.M;
            B.emLampMat ??= B.OnBackup(B.GlowMat("#eaf2ff", 4), 4);
            g.Add(B.Box(0.08, 0.12, 0.32, M.white, A(x: x + 0.04, y: y, z: z)));
            foreach (var dz in new[] { -0.1, 0.1 }) g.Add(B.Cyl(0.035, 0.03, 0.05, B.emLampMat, A(x: x + 0.1, y: y - 0.02, z: z + dz, cast: false)));
            B.sensors.Add(new Sensor { x = (float)(x + 0.3), y = (float)(y - 0.05), z = (float)z, kind = "backup" });
        }
    }
}
