// Ports levels/apartment-home.js (second half): the living room, the bedroom, the 베란다, the flat's points, spawns
// and anchors, and the canvas drawings (분전반 face, picture, clock, laptop screen).
using System;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ApartmentParts;

namespace Gyeongbo.Levels
{
    public static partial class ApartmentHome
    {
        // pleated panel: alternating tilted strips
        static void Curtain(HomeKit k, double a, double b, double z)
        {
            int n = Math.Max(4, JsRound((b - a) / 0.09)); double w = (b - a) / n;
            for (int i = 0; i < n; i++) k.box(w * 1.15, HOME.ch - FL - 0.08, 0.02, k.W.linen, a + (i + 0.5) * w, FL + 0.02, z + (i % 2 != 0 ? 0.025 : -0.025), ry: i % 2 != 0 ? 0.5 : -0.5);
        }

        static void Foliage(HomeKit k, double s, double x, double y, double z)
        {
            var f = Obj(Geo.Icosahedron((float)s, 1), k.W.green, cast: true); SetPos(f, x, y, z); k.g.Add(f);
        }

        // ---- living room: sofa, rug, low table, wall TV on a stand, stand air conditioner, lamp, plant, picture, curtains ----
        static void BuildLiving(HomeKit k)
        {
            var B = k.B; var g = k.g; var M = k.M; var W = k.W; double X1 = HOME.x1, ZB = HOME.zb, CH = HOME.ch, T = HOME.top;
            foreach (var (dx, dz) in new[] { (-0.4, 5.0), (0.38, 5.0), (-0.4, 7.0), (0.38, 7.0) }) k.cyl(0.025, 0.025, 0.06, M.darkWood, -3.5 + dx, FL, dz);
            k.rbox(0.92, 0.36, 2.2, 0.05, W.sofa, -3.5, FL + 0.06, 6.0); k.rbox(0.24, 0.5, 2.2, 0.07, W.sofa, -3.84, FL + 0.42, 6.0);
            foreach (var z in new[] { 4.98, 7.02 }) k.rbox(0.92, 0.22, 0.16, 0.06, W.sofa, -3.5, FL + 0.42, z);
            foreach (var z in new[] { 5.47, 6.53 }) k.rbox(0.66, 0.12, 1.02, 0.05, W.cushion, -3.38, FL + 0.42, z);
            k.rbox(0.13, 0.36, 0.38, 0.06, W.pillow, -3.62, FL + 0.52, 5.3, rz: -0.28, ry: 0.15); k.rbox(0.13, 0.34, 0.36, 0.06, k.P("#7c8a86", 0.95), -3.62, FL + 0.52, 6.72, rz: -0.3, ry: -0.2);
            k.rbox(0.5, 0.06, 0.4, 0.03, k.P("#c7b9a4", 1), -3.35, FL + 0.54, 6.25, ry: 0.2);
            B.Solid(-4.0, -3.02, 4.88, 7.12, FL, FL + 0.9);
            k.box(2.2, 0.01, 1.7, W.rug, -1.9, FL, 6.0, cast: false);
            k.box(1.0, 0.04, 0.55, M.lightWood, -1.95, FL + 0.38, 6.0);
            foreach (var (dx, dz) in new[] { (-0.46, -0.24), (0.46, -0.24), (-0.46, 0.24), (0.46, 0.24) }) k.box(0.04, 0.38, 0.04, M.lightWood, -1.95 + dx, FL, 6.0 + dz);
            k.box(0.05, 0.016, 0.17, M.black, -1.75, T, 5.82, ry: 0.3); k.cyl(0.04, 0.036, 0.09, k.P("#e8e2d6", 0.3), -1.48, T, 5.86); k.box(0.22, 0.008, 0.3, k.P("#4a6b8a", 0.7), -2.3, T, 6.15, ry: -0.15);
            B.Solid(-2.47, -1.43, 5.71, 6.29, FL, FL + 0.45);
            k.box(0.42, 0.4, 1.9, k.P("#e8e3da", 0.45), 0.09, FL, 6.0);
            foreach (var z in new[] { 5.37, 6.0, 6.63 }) k.box(0.004, 0.36, 0.004, W.gap, -0.121, FL + 0.02, z, cast: false);
            k.box(0.08, 0.06, 0.8, M.darkMetal, 0.0, FL + 0.4, 6.0); k.box(0.2, 0.04, 0.2, M.black, 0.12, FL + 0.4, 6.75); k.box(0.004, 0.006, 0.02, k.glow("green"), 0.019, FL + 0.42, 6.75, cast: false);
            k.box(0.05, 0.72, 1.26, M.black, 0.255, FL + 0.8, 6.0); k.box(0.004, 0.68, 1.22, W.screen, 0.228, FL + 0.82, 6.0, cast: false);
            k.box(0.004, 0.008, 0.02, k.glow("red"), 0.2255, FL + 0.806, 6.45, cast: false);
            B.Solid(-0.13, X1, 5.04, 6.96, FL, FL + 1.55);
            k.rbox(0.36, 1.75, 0.3, 0.05, M.white, 0.08, FL, 7.55); k.box(0.004, 0.5, 0.22, M.darkMetal, -0.101, FL + 1.05, 7.55); k.box(0.004, 0.04, 0.08, k.glow("cyan"), -0.102, FL + 1.32, 7.55, cast: false);
            B.Solid(-0.12, 0.28, 7.38, 7.72, FL, FL + 1.75);
            k.cyl(0.14, 0.16, 0.03, M.black, -3.62, FL, 7.5); B.Rod(g, V3(-3.62, FL + 0.03, 7.5), V3(-3.62, FL + 1.45, 7.5), 0.012, M.black);
            k.cyl(0.15, 0.2, 0.26, k.glow("shade"), -3.62, FL + 1.42, 7.5, cast: false, seg: 20); B.Solid(-3.78, -3.46, 7.34, 7.66, FL, FL + 1.7);
            k.cyl(0.17, 0.13, 0.34, W.pot, 0.02, FL, 4.62);
            foreach (var (dx, dy, dz, s) in new[] { (0.0, 0.6, 0.0, 0.3), (-0.12, 0.85, 0.05, 0.22), (0.08, 0.8, -0.08, 0.24) }) Foliage(k, s, 0.02 + dx, FL + dy, 4.62 + dz);
            B.Solid(-0.2, 0.25, 4.4, 4.86, FL, FL + 1.0);
            k.box(0.03, 0.58, 0.84, M.darkWood, -3.985, FL + 1.25, 6.0);
            var art = Obj(Geo.Plane(0.76f, 0.5f), Mat.Lit(new MatSpec { map = B.Tex("art302", 256, 168, DrawArt), roughness = 0.8f }));
            SetPos(art, -3.968, FL + 1.54, 6.0); SetYaw(art, Math.PI / 2); g.Add(art);
            k.cyl(0.16, 0.16, 0.03, Mat.Lit(new MatSpec { map = B.Tex("clock", 256, 256, DrawClock), roughness = 0.4f }), X1 - 0.016, FL + 1.85, 4.3, rz: Math.PI / 2, seg: 28);
            k.box(4.2, 0.03, 0.05, M.alu, -1.85, CH - 0.04, ZB - 0.17); Curtain(k, -4.0, -3.45, ZB - 0.17); Curtain(k, -0.12, 0.28, ZB - 0.17);
            k.box(0.62, 0.05, 0.62, M.white, -1.85, CH - 0.05, 5.9); k.box(0.58, 0.004, 0.58, k.glow("lamp"), -1.85, CH - 0.054, 5.9, cast: false);
            B.fixtures.Add(new Fixture { x = -1.85f, y = (float)(CH - 0.35), z = 5.9f, color = 0xfff2e0, intensity = 5, distance = 9 });
        }

        // ---- bedroom: wardrobe, bed, nightstand + lamp, desk with laptop and lamp, office chair, curtain, ceiling light ----
        static void BuildBedroom(HomeKit k)
        {
            var B = k.B; var g = k.g; var M = k.M; var W = k.W; double X0 = HOME.x0, ZB = HOME.zb, CH = HOME.ch;
            k.box(0.6, 2.2, 1.7, W.cabinet, -7.27, FL, 4.6);
            foreach (var z in new[] { 4.17, 4.6, 5.03 }) k.box(0.004, 2.14, 0.004, W.gap, -6.968, FL + 0.03, z, cast: false);
            foreach (var z in new[] { 4.52, 4.68 }) k.box(0.02, 0.3, 0.015, M.metal, -6.96, FL + 1.0, z);
            B.Solid(X0, -6.95, 3.7, 5.47, FL, FL + 2.2);
            k.box(2.05, 0.28, 1.6, M.lightWood, -6.53, FL, 6.4); k.rbox(1.98, 0.22, 1.52, 0.06, M.white, -6.53, FL + 0.28, 6.4);
            k.rbox(1.45, 0.1, 1.58, 0.05, W.duvet, -6.25, FL + 0.47, 6.4, rz: 0.02); k.rbox(0.5, 0.06, 1.6, 0.03, k.P("#8c99a6", 0.95), -5.85, FL + 0.55, 6.4);
            foreach (var z in new[] { 6.02, 6.78 }) k.rbox(0.38, 0.12, 0.62, 0.05, M.white, -7.25, FL + 0.5, z, ry: z > 6.4 ? 0.08 : -0.05);
            k.box(0.06, 0.95, 1.62, M.lightWood, -7.54, FL, 6.4);
            B.Solid(X0, -5.48, 5.58, 7.22, FL, FL + 0.62);
            k.box(0.42, 0.48, 0.38, M.lightWood, -7.34, FL, 7.5); k.cyl(0.06, 0.07, 0.05, M.white, -7.34, FL + 0.48, 7.5);
            k.cyl(0.1, 0.13, 0.17, k.glow("shade"), -7.34, FL + 0.66, 7.5, cast: false, seg: 18); B.Rod(g, V3(-7.34, FL + 0.53, 7.5), V3(-7.34, FL + 0.66, 7.5), 0.008, M.metal);
            B.Solid(X0, -7.1, 7.3, 7.72, FL, FL + 0.6);
            k.box(1.3, 0.03, 0.6, M.lightWood, -6.2, FL + 0.7, 4.0);
            foreach (var dx in new[] { -0.62, 0.62 }) k.box(0.03, 0.7, 0.56, M.lightWood, -6.2 + dx, FL, 4.0);
            k.box(0.33, 0.014, 0.23, M.alu, -6.2, FL + 0.73, 4.08);
            var lid = J.Group("laptop-lid"); SetPos(lid, -6.2, FL + 0.744, 3.965); SetRot(lid, -0.28, 0, 0); g.Add(lid);
            lid.Add(B.Box(0.33, 0.22, 0.008, M.alu, A(z: -0.004)));
            var scr = Obj(Geo.Plane(0.3f, 0.19f), B.OnPower(Mat.Lit(new MatSpec { color = J.Hex(0x050505), emissive = J.Hex(0xffffff), emissiveMap = B.Tex("laptop", 320, 200, DrawLaptop), emissiveIntensity = 1.1f, roughness = 0.2f })));
            SetPos(scr, 0, 0.11, 0.0005); lid.Add(scr);
            k.box(0.21, 0.01, 0.15, k.P("#e8d7a8", 0.9), -5.8, FL + 0.73, 4.12, ry: 0.18); B.Rod(g, V3(-5.7, FL + 0.742, 4.07), V3(-5.66, FL + 0.742, 4.2), 0.004, M.black);
            k.cyl(0.04, 0.036, 0.09, k.P("#5f7f6a", 0.35), -6.68, FL + 0.73, 4.12);
            k.cyl(0.07, 0.08, 0.02, M.black, -5.72, FL + 0.73, 3.85); B.Rod(g, V3(-5.72, FL + 0.75, 3.85), V3(-5.78, FL + 1.12, 3.95), 0.008, M.black); B.Rod(g, V3(-5.78, FL + 1.12, 3.95), V3(-5.95, FL + 1.06, 4.05), 0.008, M.black);
            k.cyl(0.035, 0.06, 0.07, k.glow("lamp"), -5.97, FL + 0.99, 4.06, cast: false);
            k.box(0.3, 0.04, 0.06, M.white, -6.7, FL, 3.8); k.box(0.01, 0.006, 0.01, k.glow("red"), -6.6, FL + 0.04, 3.83, cast: false); // 멀티탭
            B.Solid(-6.87, -5.53, 3.7, 4.32, FL, FL + 0.75);
            var ch = J.Group("desk-chair"); SetPos(ch, -6.15, FL, 4.78); SetYaw(ch, 0.25); g.Add(ch);
            for (int i = 0; i < 5; i++) { double a = i * Math.PI * 0.4; ch.Add(B.Box(0.04, 0.03, 0.3, M.black, A(x: Math.Sin(a) * 0.15, y: 0.04, z: Math.Cos(a) * 0.15, ry: a))); }
            ch.Add(B.Cyl(0.025, 0.025, 0.36, M.steel, A(y: 0.06)), B.RBox(0.48, 0.08, 0.46, 0.03, k.P("#33373d", 0.9), A(y: 0.42)), B.RBox(0.44, 0.5, 0.06, 0.03, k.P("#33373d", 0.9), A(y: 0.58, z: 0.22, rx: -0.12)));
            B.Solid(-6.42, -5.88, 4.52, 5.04, FL, FL + 0.95);
            k.box(2.6, 0.03, 0.05, M.alu, -5.8, CH - 0.04, ZB - 0.17); Curtain(k, -7.1, -6.55, ZB - 0.17); Curtain(k, -5.05, -4.5, ZB - 0.17);
            k.cyl(0.2, 0.2, 0.035, k.glow("lamp"), -5.8, CH - 0.035, 5.8, cast: false, seg: 24);
            B.fixtures.Add(new Fixture { x = -5.8f, y = (float)(CH - 0.3), z = 5.8f, color = 0xfff0dc, intensity = 3.5f, distance = 7 });
            B.Door(g, "x", -4.05, 4.0, 4.85, FL, 2.05, W.door, hinge: "a", dir: -1, open: 0.85, solid: false, frameMat: M.frame);
        }

        // ---- 베란다: washing machine, laundry, drying poles with clothes, plants, drain, 경량칸막이 panel, bulb ----
        static void BuildBalcony(HomeKit k)
        {
            var B = k.B; var g = k.g; var kit = k.kit; var M = k.M; var W = k.W; double X0 = HOME.x0, CH = HOME.ch, BF = HOME.bf;
            k.rbox(0.62, 0.86, 0.62, 0.04, M.white, -7.25, BF, 9.03);
            k.cyl(0.2, 0.2, 0.03, kit.Mat("darkGlass"), -6.93, BF + 0.48, 9.03, rz: Math.PI / 2, seg: 24); k.cyl(0.235, 0.235, 0.02, M.metal, -6.94, BF + 0.48, 9.03, rz: Math.PI / 2, seg: 24);
            k.box(0.004, 0.07, 0.5, M.black, -6.937, BF + 0.75, 9.03); k.box(0.004, 0.02, 0.05, k.glow("green"), -6.942, BF + 0.765, 8.86, cast: false);
            B.Rod(g, V3(-7.3, BF + 1.2, 9.36), V3(-7.3, BF + 0.9, 9.3), 0.012, M.metal); k.box(0.06, 0.06, 0.05, M.metal, -7.3, BF + 1.18, 9.34);
            B.Solid(X0, -6.92, 8.7, 9.36, BF, BF + 0.9);
            k.cyl(0.2, 0.17, 0.34, k.P("#d8cdb5", 0.9), -6.55, BF, 8.35, seg: 18);
            foreach (var z in new[] { 8.45, 8.95 })
            {
                B.Rod(g, V3(-6.3, CH - 0.38, z), V3(-1.6, CH - 0.38, z), 0.012, M.alu);
                foreach (var x in new[] { -6.0, -1.9 }) B.Rod(g, V3(x, CH, z), V3(x, CH - 0.38, z), 0.008, M.alu);
            }
            var clothes = new[] { (-5.6, 8.45, "#e8e4dc", 0.5, 0.62), (-4.9, 8.45, "#5d7fa3", 0.55, 0.5), (-4.2, 8.95, "#f2efe6", 0.4, 0.7), (-3.5, 8.45, "#8a3a3a", 0.45, 0.55), (-2.7, 8.95, "#3f4a56", 0.6, 0.62) };
            for (int i = 0; i < clothes.Length; i++) { var (x, z, c, w, h) = clothes[i]; k.box(w, h, 0.012, k.P(c, 0.98), x, CH - 0.38 - h, z, ry: (i % 3 - 1) * 0.08); }
            foreach (var (x, s) in new[] { (-2.2, 0.22), (-1.75, 0.16) }) { k.cyl(s * 0.75, s * 0.6, s * 1.3, W.pot, x, BF, 9.15); Foliage(k, s, x, BF + s * 1.3 + s * 0.6, 9.15); }
            k.box(0.12, 0.003, 0.12, M.steel, -5.0, BF, 9.2, cast: false);
            foreach (var (w, h, x, y, z) in new[] { (0.012, 2.0, X0 + 0.006, BF + 0.05, 8.12), (0.012, 2.0, X0 + 0.006, BF + 0.05, 8.68), (0.012, 0.02, X0 + 0.006, BF + 2.05, 8.4) })
                k.box(w, h, z == 8.4 ? 0.58 : 0.02, k.P("#9e988c", 0.6), x, y, z, cast: false);
            B.Plate(g, "경량칸막이", X0 + 0.014, BF + 1.55, 8.4, ry: Math.PI / 2, w: 0.32, h: 0.09, bg: "#fff4d6", fg: "#8a1c1c", border: "#8a1c1c");
            k.cyl(0.06, 0.06, 0.03, k.glow("lamp"), -4.0, CH - 0.03, 8.7, cast: false);
        }

        // ---- points, spawns, anchors ----
        static void BuildPoints(AptBuilder B)
        {
            B.Point("homeExitDoor", "현관문 (복도로)", -1.275, F3, 0.5, "door", 1.3);
            B.Point("homeBreaker", "분전반 (두꺼비집)", -2.1, F3, 0.45, "inspect", 1.3);
            B.Point("homeFridge", "냉장고", -2.92, FL, 1.2, "inspect", 1.4);
            B.Point("homeSink", "싱크대", -5.55, FL, 1.0, "inspect", 1.4);
            B.Point("homeGas", "가스 중간밸브", -3.85, FL, 1.0, "inspect", 1.4);
            B.Point("homeDesk", "책상 (노트북)", -6.2, FL, 4.75, "inspect", 1.5);
            B.Point("homeBed", "침대", -6.0, FL, 6.4, "bed", 1.9);
            B.Point("homeWindow", "거실 창밖 보기", -2.4, FL, 7.45, "window", 1.6);
            B.spawns["home"] = new Spawn(-1.6f, FL, 4.5f, Mathf.PI);
            B.spawns["homeEntry"] = new Spawn(-1.275f, F3, 0.75f, Mathf.PI);
            B.anchors["homeLiving"] = new Anchor { x = -0.2f, y = FL + 1.9f, z = 4.0f, target = new Vector3(-2.6f, FL + 0.6f, 7.2f) };
            B.anchors["homeKitchen"] = new Anchor { x = -2.8f, y = FL + 1.8f, z = 3.2f, target = new Vector3(-5.4f, FL + 0.8f, 0.4f) };
            B.anchors["homeBalcony"] = new Anchor { x = -0.7f, y = FL + 1.6f, z = 8.5f, target = new Vector3(-4.0f, FL + 0.9f, 9.0f) };
        }

        // 분전반 face: steel door with a smoked window over the main ELB (yellow test button) and the branch breakers.
        static void DrawBreaker(Paint c, float w, float h)
        {
            c.fillStyle = "#d6d1c4"; c.fillRect(0, 0, w, h);
            c.fillStyle = "#2f4e6e"; c.fillRect(0, 0, w, 40); c.fillStyle = "#fff"; c.font = Fnt(700, 24); c.textAlign = "center"; c.fillText("분전반", w / 2, 29);
            c.fillStyle = "#3c4146"; c.fillRect(22, 60, w - 44, 200);
            c.fillStyle = "#20242a"; c.fillRect(34, 76, 56, 120); c.fillStyle = "#f2c230"; c.fillRect(48, 92, 28, 16); c.fillStyle = "#e8e8e8"; c.fillRect(52, 130, 20, 44);
            for (int i = 0; i < 6; i++) { float x = 104 + i * 22; c.fillStyle = "#ecebe6"; c.fillRect(x, 84, 18, 104); c.fillStyle = "#555"; c.fillRect(x + 5, 104, 8, 34); }
            c.fillStyle = "rgba(170,200,220,0.18)"; c.fillRect(22, 60, (w - 44) * 0.45f, 200);
            c.fillStyle = "#222"; c.font = Fnt(600, 15); c.textAlign = "left";
            c.fillText("누전차단기", 30, 222); c.fillText("전등 · 전열 · 주방 · 에어컨", 30, 246);
            c.fillStyle = "#9a958a"; c.fillRect(w - 40, h - 46, 18, 26);
        }

        static void DrawArt(Paint c, float w, float h)
        {
            var gr = new PaintGradient(0, 0, 0, h); gr.addColorStop(0, "#d9c7a6"); gr.addColorStop(0.6f, "#e9dcc3"); gr.addColorStop(1, "#a9b3a0"); c.fillStyle = gr; c.fillRect(0, 0, w, h);
            c.fillStyle = "#7d8d86"; c.beginPath(); c.moveTo(0, h * 0.7f); c.quadraticCurveTo(w * 0.3f, h * 0.35f, w * 0.55f, h * 0.62f); c.quadraticCurveTo(w * 0.8f, h * 0.45f, w, h * 0.66f); c.lineTo(w, h); c.lineTo(0, h); c.fill();
            c.fillStyle = "#c96f4a"; c.beginPath(); c.arc(w * 0.72f, h * 0.3f, 18, 0, 7); c.fill();
        }

        static void DrawClock(Paint c, float w, float h)
        {
            c.fillStyle = "#f7f5f0"; c.fillRect(0, 0, w, h); c.strokeStyle = "#333"; c.lineCap = "round";
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6; c.lineWidth = i % 3 != 0 ? 4 : 8;
                c.beginPath(); c.moveTo(w / 2 + Mathf.Sin(a) * 100, h / 2 - Mathf.Cos(a) * 100); c.lineTo(w / 2 + Mathf.Sin(a) * 118, h / 2 - Mathf.Cos(a) * 118); c.stroke();
            }
            c.lineWidth = 9; c.beginPath(); c.moveTo(w / 2, h / 2); c.lineTo(w / 2 - 44, h / 2 - 40); c.stroke();
            c.lineWidth = 6; c.beginPath(); c.moveTo(w / 2, h / 2); c.lineTo(w / 2 + 70, h / 2 - 50); c.stroke();
        }

        static void DrawLaptop(Paint c, float w, float h)
        {
            var gr = new PaintGradient(0, 0, w, h); gr.addColorStop(0, "#2b4d6e"); gr.addColorStop(1, "#5b7f95"); c.fillStyle = gr; c.fillRect(0, 0, w, h);
            c.fillStyle = "#f4f6f8"; c.fillRect(40, 30, 200, 130); c.fillStyle = "#dfe5ea"; c.fillRect(40, 30, 200, 16);
            c.fillStyle = "#c8d6c0"; c.fillRect(48, 54, 120, 98); c.strokeStyle = "#ffffff"; c.lineWidth = 5; c.beginPath(); c.moveTo(48, 120); c.lineTo(110, 96); c.lineTo(168, 108); c.stroke();
            c.fillStyle = "#333"; c.font = Fnt(600, 11); c.fillText("지도", 176, 66); c.fillText("메모", 176, 86);
            c.fillStyle = "rgba(0,0,0,0.45)"; c.fillRect(0, h - 18, w, 18);
        }
    }
}
