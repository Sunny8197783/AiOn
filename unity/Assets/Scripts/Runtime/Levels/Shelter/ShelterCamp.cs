// Ports levels/shelter-camp.js: the car park as a civil-defence shelter — rows of mats (돗자리·은박매트) with bags,
// blankets, bottles and shoes left at the edge, a reception table with the roster clipboard and a battery lantern, a
// wheeled notice board, the shared radio table, supply pallets, recycling bins, a cloth banner, and the cars still
// parked in the east.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ShelterShell;
using static Gyeongbo.Levels.ShelterUtil;

namespace Gyeongbo.Levels
{
    // A seat on a mat: position, facing, which mat (index into ShelterCampResult.mats).
    public sealed class ShelterSeat
    {
        public float x, z, yaw;
        public int mat;
    }

    public sealed class ShelterMat
    {
        public float x, z;
        public bool used, spot;
    }

    public sealed class ShelterCampResult
    {
        public GameObject group, live, banner, cloth;
        public Material dial, boardMat;
        public List<ShelterMat> mats = new List<ShelterMat>();
        public List<ShelterSeat> seats = new List<ShelterSeat>();
        public List<Solid> solids = new List<Solid>();
        public Light lanternLight;
    }

    public static class ShelterCamp
    {
        const float PI = Mathf.PI;
        public static readonly float[] MAT_COLS = Enumerable.Range(0, 13).Select(i => (float)(-20.4 + 2.2 * i)).ToArray();
        public static readonly float[] MAT_ROWS = { -3.9f, -1.55f, 1.55f, 3.9f };
        public static readonly float[] SOUTH_MAT_COLS = { -20.6f, -18.4f, -16.2f, -14, -11.8f, -9.6f }, SOUTH_MAT_ROWS = { 12.3f, 14.5f };
        public static readonly XZ RECEPTION = new XZ(2.0f, -8.6f), BOARD = new XZ(-3.0f, -13.6f), RADIO = new XZ(-5.5f, 7.4f), PALLETS = new XZ(-19.3f, -13.2f);
        // [dx, dz, ry] from the table.
        public static readonly Dictionary<string, float[][]> CHAIRS = new Dictionary<string, float[][]>
        {
            ["reception"] = new[] { new[] { -0.45f, 0.7f, 0 }, new[] { 0.5f, 0.75f, 0.2f } },
            ["radio"] = new[] { new[] { -0.9f, 0.2f, -PI / 2 }, new[] { 0.9f, 0.25f, PI / 2 }, new[] { 0.1f, 0.75f, 0 } },
        };
        // The free mat the player can take (MAT_COLS[9], MAT_ROWS[3]).
        public static XZ SPOT => new XZ(MAT_COLS[9], MAT_ROWS[3]);

        static At at(float x = 0, float y = 0, float z = 0, float rx = 0, float ry = 0, float rz = 0) => new At { x = x, y = y, z = z, rx = rx, ry = ry, rz = rz };
        // M4(x, y, z, ry, sx, sy, sz, rx)
        static Matrix4x4 M4(float x, float y, float z, float ry = 0, float sx = 1, float sy = 1, float sz = 1, float rx = 0) => T4(x, y, z, rx, ry, 0, sx, sy, sz);
        static Color C(string hex) => J.Css(hex).linear;   // new THREE.Color(hex): linear working space

        public static ShelterCampResult BuildCamp(Kit kit, ShelterTex T)
        {
            var g = J.Group("shelter-camp");
            var live = J.Group("shelter-camp-live");
            var res = new ShelterCampResult { group = g, live = live };
            GameObject add(GameObject m, GameObject parent = null) { (parent ?? g).Add(m); return m; }
            var r = Kit.Rng(1987);
            void inst(GeoData geo, Material mat, List<Matrix4x4> ms, bool cast = true, List<Color> cs = null) => Inst(g, geo, mat, ms, cast, true, cs);
            var woven = Mat.Lit(new MatSpec
            {
                map = kit.Texture(kit.Canvas(128, 128, (c, w, h) =>
                {
                    c.fillStyle = "#ffffff"; c.fillRect(0, 0, 128, 128);
                    for (int y = 0; y < 128; y += 4) { c.fillStyle = y % 16 < 8 ? "rgba(0,0,0,0.12)" : "rgba(255,255,255,0.1)"; c.fillRect(0, y, 128, 2); }
                    c.fillStyle = "rgba(0,0,0,0.25)"; c.fillRect(0, 0, 128, 3); c.fillRect(0, 125, 128, 3);
                })), roughness = 0.72f, vertexColors = true,
            });
            var foil = Mat.Lit(new MatSpec
            {
                color = J.Hex(0xc8ccd0), metalness = 0.8f, roughness = 0.3f,
                normalMap = kit.Texture(kit.NormalFrom(kit.Canvas(128, 128, (c, w, h) =>
                {
                    c.fillStyle = "#808080"; c.fillRect(0, 0, 128, 128);
                    for (int i = 0; i < 90; i++) { c.strokeStyle = r() < 0.5f ? "#a0a0a0" : "#5a5a5a"; c.lineWidth = 2 + r() * 3; c.beginPath(); c.moveTo(r() * 128, r() * 128); c.lineTo(r() * 128, r() * 128); c.stroke(); }
                }), 3), false),
            });
            Material fabric = Mat.Lit(new MatSpec { color = J.Hex(0xffffff), roughness = 0.95f, vertexColors = true }), plastic = Mat.Lit(new MatSpec { color = J.Hex(0xffffff), roughness = 0.45f, vertexColors = true }),
                bottle = Mat.Lit(new MatSpec { color = J.Hex(0xcfe3f0), roughness = 0.15f, metalness = 0.1f, transparent = true, opacity = 0.75f }),
                tableTop = kit.Mat("paint:#ecebe6", new MatSpec { roughness = 0.5f }), legs = kit.Mat("steel"), seatM = kit.Mat("paint:#3a3f46", new MatSpec { roughness = 0.6f }),
                wood = kit.Mat("paint:#a07a4c", new MatSpec { roughness = 0.85f }), tyre = kit.Mat("rubber"), rim = kit.Mat("metal", new MatSpec { roughness = 0.3f }),
                glass = kit.Mat("darkGlass"), lens = kit.Mat("paint:#dfe6ea", new MatSpec { roughness = 0.1f, metalness = 0.4f }), tail = kit.Mat("paint:#7c1616", new MatSpec { roughness = 0.25f });

            // ---- mats and what people brought ----
            var mats = res.mats; var seats = res.seats;
            List<Matrix4x4> wovenM = new List<Matrix4x4>(), foilM = new List<Matrix4x4>(), bags = new List<Matrix4x4>(), blankets = new List<Matrix4x4>(), bottles = new List<Matrix4x4>(), shoes = new List<Matrix4x4>(), pillows = new List<Matrix4x4>();
            List<Color> wovenC = new List<Color>(), bagsC = new List<Color>(), blanketsC = new List<Color>(), shoesC = new List<Color>(), pillowsC = new List<Color>();
            var bagCols = new[] { "#2e3a4f", "#5b2d2d", "#3c4a3a", "#1d1f24", "#7a6a55", "#28516e", "#8a3b52" }.Select(C).ToArray();
            var blanketCols = new[] { "#c9b79c", "#8d9bb0", "#b0646a", "#d9d4c7", "#6e7f62", "#e0c6a8" }.Select(C).ToArray();
            var matCols = new[] { "#3d6fb0", "#4f8a5c", "#6f86a0", "#b7a37a", "#9e4b45", "#58708a" }.Select(C).ToArray();
            var shoeCols = new[] { "#202226", "#e8e6e0", "#5a4636", "#2d3a55", "#8b8f96" }.Select(C).ToArray();
            Color pick(Color[] l) => l[(int)(r() * l.Length)];
            var all = MAT_COLS.SelectMany(x => MAT_ROWS.Select(z => new XZ(x, z))).Concat(SOUTH_MAT_COLS.SelectMany(x => SOUTH_MAT_ROWS.Select(z => new XZ(x, z)))).ToList();
            var spotXZ = SPOT;
            foreach (var m in all)
            {
                bool isSpot = m.x == spotXZ.x && m.z == spotXZ.z;
                string kind = r() < 0.3f ? "foil" : "woven"; float ry = (r() - 0.5f) * 0.08f;
                if (kind == "foil") foilM.Add(M4(m.x, 0.006f, m.z, ry));
                else { wovenM.Add(M4(m.x, 0.006f, m.z, ry)); wovenC.Add(pick(matCols)); }
                bool used = !isSpot && r() < 0.66f;
                mats.Add(new ShelterMat { x = m.x, z = m.z, used = used, spot = isSpot });
                if (!used) continue;
                int edge = m.z < 0 ? (m.z < -2.5f ? -1 : 1) : (m.z > 2.5f ? 1 : -1);   // mat side toward the nearest walkway
                int n = r() < 0.45f ? 2 : 1;
                for (int k = 0; k < n; k++)
                    seats.Add(new ShelterSeat { x = m.x + (n == 2 ? (k > 0 ? 0.42f : -0.42f) : (r() - 0.5f) * 0.3f), z = m.z - edge * (0.1f + r() * 0.3f), yaw = (edge > 0 ? PI : 0) + (r() - 0.5f) * 1.6f, mat = mats.Count - 1 });
                float cx = m.x + (r() < 0.5f ? -0.62f : 0.62f), cz = m.z - edge * 0.72f;
                bags.Add(M4(cx, 0.012f, cz, r() * 6, 1, 0.8f + r() * 0.4f, 1)); bagsC.Add(pick(bagCols));
                if (r() < 0.8f) { blankets.Add(M4(m.x - (cx - m.x) * 0.9f, 0.012f, m.z - edge * 0.6f, (r() - 0.5f) * 0.4f, 1, 0.6f + r() * 0.8f, 1)); blanketsC.Add(pick(blanketCols)); }
                for (int b = 0, nb = 1 + (int)(r() * 3); b < nb; b++) bottles.Add(M4(cx + 0.25f + b * 0.09f, 0.012f, cz + 0.12f, 0, 1, b == 2 ? 0.55f : 1, 1));
                if (r() < 0.35f) { pillows.Add(M4(m.x + (r() - 0.5f) * 0.8f, 0.012f, m.z - edge * 0.65f, r())); pillowsC.Add(pick(blanketCols)); }
                for (int k = 0; k < n; k++)
                { // shoes paired at the walkway edge, toes toward the mat
                    float sx = m.x + (k - (n - 1) / 2f) * 0.55f, sz = m.z + edge * 1.12f; var col = pick(shoeCols);
                    foreach (var d in new[] { -0.07f, 0.07f }) { shoes.Add(M4(sx + d, 0, sz, (edge > 0 ? PI : 0) + (r() - 0.5f) * 0.3f)); shoesC.Add(col); }
                }
            }
            inst(Geo.Box(1.8f, 0.012f, 2.0f), woven, wovenM, false, wovenC);
            inst(Geo.Box(1.8f, 0.008f, 2.0f), foil, foilM, false);
            inst(Geo.Box(0.32f, 0.42f, 0.2f).Translate(0, 0.21f, 0), fabric, bags, true, bagsC);
            inst(Geo.Box(0.62f, 0.1f, 0.42f).Translate(0, 0.05f, 0), fabric, blankets, true, blanketsC);
            inst(Geo.Cylinder(0.034f, 0.036f, 0.22f, 10).Translate(0, 0.11f, 0), bottle, bottles, false);
            inst(Geo.Box(0.42f, 0.1f, 0.28f).Translate(0, 0.05f, 0), fabric, pillows, true, pillowsC);
            inst(Geo.Box(0.1f, 0.08f, 0.27f).Translate(0, 0.04f, 0), plastic, shoes, false, shoesC);

            // ---- folding tables and chairs ----
            GameObject table(float x, float z, float ry = 0, float w = 1.8f, float d = 0.6f)
            {
                var t = J.Group("table"); J.SetPos(t, x, 0, z); J.SetYaw(t, ry);
                t.Add(kit.Box(w, 0.04f, d, tableTop, at(y: 0.7f)));
                foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 }) t.Add(kit.Cyl(0.016f, 0.016f, 0.7f, legs, at(sx * (w / 2 - 0.08f), 0, sz * (d / 2 - 0.06f)), 6));
                t.Add(kit.Box(w - 0.2f, 0.03f, 0.03f, legs, at(y: 0.15f)));
                return add(t);
            }
            GameObject chair(float x, float z, float ry = 0)
            {
                var c = J.Group("chair"); J.SetPos(c, x, 0, z); J.SetYaw(c, ry);
                c.Add(kit.Box(0.42f, 0.03f, 0.4f, seatM, at(y: 0.45f))); c.Add(kit.Box(0.42f, 0.32f, 0.03f, seatM, at(y: 0.62f, z: 0.2f)));
                foreach (var sx in new[] { -0.19f, 0.19f }) { c.Add(kit.Box(0.025f, 0.95f, 0.025f, legs, at(sx, 0, 0.21f, rx: 0.12f))); c.Add(kit.Box(0.025f, 0.5f, 0.025f, legs, at(sx, 0, -0.17f, rx: -0.2f))); }
                return add(c);
            }

            // ---- reception: roster clipboard, pens, masks, sanitiser, battery lantern ----
            table(RECEPTION.x, RECEPTION.z);
            foreach (var c in CHAIRS["reception"]) chair(RECEPTION.x + c[0], RECEPTION.z + c[1], c[2]);   // staff sit south, facing the queue
            float rx = RECEPTION.x, rz = RECEPTION.z;
            add(kit.Box(0.24f, 0.012f, 0.33f, wood, at(rx - 0.35f, 0.74f, rz - 0.05f, ry: 0.1f)));
            var sheet = add(Geo.Obj(Geo.Plane(0.21f, 0.28f), Mat.Lit(new MatSpec { map = T.RosterTex(), roughness = 0.8f }), "roster", false, false));
            J.SetRot(sheet, -PI / 2, 0, PI + 0.1f); J.SetPos(sheet, rx - 0.35f, 0.753f, rz - 0.06f);
            add(kit.Box(0.08f, 0.02f, 0.03f, legs, at(rx - 0.35f, 0.75f, rz - 0.2f, ry: 0.1f)));
            foreach (var d in new[] { 0, 0.03f }) add(kit.Cyl(0.005f, 0.005f, 0.14f, kit.Mat("paint:#1d3f8f"), at(rx - 0.1f + d, 0.73f, rz - 0.1f, ry: 0.3f, rz: PI / 2), 6));
            add(kit.Box(0.2f, 0.1f, 0.11f, kit.Mat("paint:#e9eef3"), at(rx + 0.25f, 0.72f, rz + 0.12f)));
            add(kit.Cyl(0.035f, 0.035f, 0.16f, kit.Mat("paint:#cfe8f5", new MatSpec { roughness = 0.2f }), at(rx + 0.5f, 0.72f, rz), 10));
            add(kit.Cyl(0.008f, 0.008f, 0.06f, kit.Mat("plastic"), at(rx + 0.5f, 0.88f, rz), 6));
            var lantern = kit.Lit("emergency", Mat.Lit(new MatSpec { color = J.Hex(0xfff3d6), emissive = J.Hex(0xffd9a0), roughness = 0.3f }), 2.4f);
            add(kit.Cyl(0.06f, 0.07f, 0.04f, seatM, at(rx + 0.72f, 0.72f, rz - 0.12f), 12));
            add(kit.Cyl(0.055f, 0.055f, 0.13f, lantern, at(rx + 0.72f, 0.76f, rz - 0.12f), 12));
            add(kit.Cyl(0.06f, 0.06f, 0.03f, seatM, at(rx + 0.72f, 0.89f, rz - 0.12f), 12));
            var desk = add(kit.Sign(new[] { "접수 · 대피자 명부 작성" }, new SignOpts { w = 0.9f, h = 0.22f, bg = "#1f4e8c", single = true })); J.SetPos(desk, rx, 0.6f, rz - 0.31f); J.SetYaw(desk, PI);
            var af = add(kit.Sign(new[] { "대피소 접수", "이쪽입니다" }, new SignOpts { w = 0.6f, h = 0.8f, bg = "#f7f5ee", fg = "#1f4e8c", border = "#1f4e8c", single = true }));
            J.SetPos(af, rx - 1.6f, 0.75f, rz - 0.9f); J.SetRot(af, -0.12f, PI, 0);
            add(kit.Box(0.62f, 0.02f, 0.4f, legs, at(rx - 1.6f, 0.0f, rz - 0.75f)));
            var lanternLight = kit.PointLight(J.Hex(0xffc98a), 1.2f, 4, 2); lanternLight.shadows = LightShadows.None;
            J.SetPos(lanternLight.gameObject, rx + 0.72f, 1.0f, rz - 0.12f); live.Add(lanternLight); res.lanternLight = lanternLight;

            // ---- wheeled notice board: aluminium frame on castors, sheets drawn from text the director passes ----
            float bx = BOARD.x, bz = BOARD.z;
            var boardMat = Mat.Lit(new MatSpec { map = T.DrawBoard(null), roughness = 0.55f }); res.boardMat = boardMat;
            var face = add(Geo.Obj(Geo.Plane(1.8f, 1.125f), boardMat, "board", false, false)); J.SetPos(face, bx, 1.45f, bz + 0.03f);
            add(kit.Box(1.86f, 1.19f, 0.04f, rim, at(bx, 0.885f, bz)));
            foreach (var sx in new[] { -0.88f, 0.88f })
            {
                add(kit.Box(0.04f, 1.5f, 0.04f, rim, at(bx + sx, 0.1f, bz))); add(kit.Box(0.04f, 0.03f, 0.6f, rim, at(bx + sx, 0.08f, bz)));
                foreach (var sz in new[] { -0.27f, 0.27f }) add(kit.Cyl(0.035f, 0.035f, 0.07f, tyre, at(bx + sx, 0.008f, bz + sz, rz: PI / 2), 8));
            }

            // ---- the shared radio on its table, chairs around it ----
            table(RADIO.x, RADIO.z, 0, 1.2f, 0.6f);
            foreach (var c in CHAIRS["radio"]) chair(RADIO.x + c[0], RADIO.z + c[1], c[2]);
            var radio = J.Group("radio"); J.SetPos(radio, RADIO.x, 0.72f, RADIO.z - 0.05f); J.SetYaw(radio, 0.15f); add(radio, live);
            radio.Add(kit.RBox(0.34f, 0.2f, 0.11f, 0.02f, kit.Mat("paint:#3b3e42", new MatSpec { roughness = 0.5f })));
            var grille = Geo.Obj(Geo.Circle(0.06f, 20), kit.Mat("paint:#1d1f22", new MatSpec { roughness = 0.9f }), "grille", false, false); J.SetPos(grille, -0.08f, 0.1f, -0.056f); J.SetYaw(grille, PI); radio.Add(grille);
            var dial = Mat.Lit(new MatSpec { color = J.Hex(0x2a2015), emissive = J.Hex(0xffb24a), emissiveIntensity = 0, roughness = 0.3f }); res.dial = dial;
            var disp = Geo.Obj(Geo.Plane(0.13f, 0.04f), dial, "dial", false, false); J.SetPos(disp, 0.07f, 0.14f, -0.056f); J.SetYaw(disp, PI); radio.Add(disp);
            foreach (var dx in new[] { 0.04f, 0.11f }) radio.Add(kit.Cyl(0.015f, 0.015f, 0.02f, legs, at(dx, 0.06f, -0.065f, rx: PI / 2), 10));
            var ant = Geo.Obj(Geo.Cylinder(0.003f, 0.004f, 0.55f, 5), rim, "antenna", false, false); J.SetPos(ant, 0.13f, 0.45f, 0.02f); J.SetRot(ant, 0, 0, -0.35f); radio.Add(ant);
            radio.Add(kit.Box(0.24f, 0.02f, 0.025f, legs, at(y: 0.21f, z: 0.02f)));
            add(kit.Box(0.09f, 0.03f, 0.06f, kit.Mat("paint:#c8a640"), at(RADIO.x + 0.3f, 0.72f, RADIO.z + 0.1f)));
            add(kit.Box(0.2f, 0.015f, 0.28f, kit.Mat("paint:#f2efe6"), at(RADIO.x - 0.35f, 0.72f, RADIO.z + 0.08f, ry: -0.2f)));
            add(kit.Cyl(0.04f, 0.04f, 0.26f, kit.Mat("paint:#7c8a95", new MatSpec { metalness = 0.6f, roughness = 0.3f }), at(RADIO.x + 0.45f, 0.72f, RADIO.z - 0.12f), 12));

            // ---- supplies: 2 L six-packs on pallets, cartons of 500 mL water and blankets ----
            var packMat = Mat.Lit(new MatSpec { map = T.WaterPackTex(), roughness = 0.3f, metalness = 0.05f });
            var cartonMats = new[] { "생수 500mL × 20", "담요", "비상식량" }.Select(t => Mat.Lit(new MatSpec { map = T.CartonTex(t), roughness = 0.9f })).ToArray();
            void pallet(float x, float z) { foreach (var dz in new[] { -0.45f, 0, 0.45f }) add(kit.Box(1.1f, 0.1f, 0.12f, wood, at(x, 0, z + dz))); add(kit.Box(1.1f, 0.03f, 1.1f, wood, at(x, 0.1f, z))); }
            var packs = new List<Matrix4x4>();
            foreach (var (px, pz, layers) in new[] { (PALLETS.x - 0.7f, PALLETS.z - 1.0f, 4), (PALLETS.x + 0.6f, PALLETS.z - 1.0f, 3) })
            {
                pallet(px, pz);
                for (int l = 0; l < layers; l++) for (int i = 0; i < 3; i++) for (int j = 0; j < 5; j++) if (!(l == layers - 1 && r() < 0.35f)) packs.Add(M4(px - 0.35f + i * 0.35f, 0.13f + l * 0.31f, pz - 0.42f + j * 0.21f, l % 2 == 1 ? PI : 0));
            }
            inst(Geo.Box(0.33f, 0.31f, 0.21f).Translate(0, 0.155f, 0), packMat, packs);
            pallet(PALLETS.x - 0.1f, PALLETS.z + 1.4f);
            for (int k = 0; k < cartonMats.Length; k++)
            {
                var items = new List<Matrix4x4>();
                for (int l = 0; l < 3 - k; l++) for (int i = 0; i < 2; i++) items.Add(M4(PALLETS.x - 0.35f + i * 0.5f + (r() - 0.5f) * 0.04f, 0.13f + l * 0.3f, PALLETS.z + 1.0f + k * 0.38f, (r() - 0.5f) * 0.08f));
                inst(Geo.Box(0.46f, 0.3f, 0.34f).Translate(0, 0.15f, 0), cartonMats[k], items);
            }
            var sup = add(kit.Sign(new[] { "구호물품 · 배부 시 안내" }, new SignOpts { w = 1.2f, h = 0.26f, bg = "#3a4048", single = true })); J.SetPos(sup, PALLETS.x, 1.9f, ROOM.z0 + 0.03f);

            // ---- recycling bins ----
            foreach (var (i, label, col) in new[] { (0, "일반쓰레기", "#5d6168"), (1, "재활용", "#2e66b2") })
            {
                float x = 7.4f, z = -4.4f + i * 0.75f;
                add(kit.RBox(0.55f, 0.85f, 0.55f, 0.05f, kit.Mat("paint:" + col, new MatSpec { roughness = 0.5f }), at(x, 0, z)));
                var s = add(kit.Sign(new[] { label }, new SignOpts { w = 0.45f, h = 0.14f, bg = "#ffffff", fg = "#111", single = true })); J.SetPos(s, x - 0.28f, 0.6f, z); J.SetYaw(s, -PI / 2);
            }
            // priority area stand in the south-west bays
            var pr = add(kit.Sign(new[] { "노약자 · 영유아 동반 구역" }, new SignOpts { w = 1.1f, h = 0.3f, bg = "#f7f5ee", fg = "#1f4e8c", border = "#1f4e8c", single = true })); J.SetPos(pr, -13.8f, 1.15f, 10.96f); J.SetYaw(pr, PI);
            add(kit.Cyl(0.02f, 0.02f, 1.0f, legs, at(-13.8f, 0, 11.0f), 6)); add(kit.Cyl(0.16f, 0.18f, 0.03f, seatM, at(-13.8f, 0, 11.0f), 12));

            // ---- banner hung from the drain line on the south wall (sways in the ventilation draught) ----
            var banner = J.Group("banner"); J.SetPos(banner, -11.5f, 2.78f, ROOM.z1 - 0.12f); add(banner, live); res.banner = banner;
            var cloth = Geo.Obj(Geo.Plane(5.6f, 0.82f, 16, 2), Mat.Lit(new MatSpec { map = T.BannerTex("연무 생활문화센터 민방위 대피소"), roughness = 0.9f, side = "double" }), "banner-cloth", false, false);
            J.SetPos(cloth, 0, -0.45f, 0); J.SetYaw(cloth, PI); banner.Add(cloth); res.cloth = cloth;

            // ---- parked cars in the east bays ----
            // ponytail: MeshPhysicalMaterial clearcoat dropped (URP Lit has none); a clear-coat shader in the quality pass.
            Material paint(int hex) => kit.Memo($"sh:carpaint:{hex}", () => Mat.Lit(new MatSpec { color = J.Hex(hex), metalness = 0.55f, roughness = 0.32f }));
            var plate = kit.Sign(new[] { "12가 3456" }, new SignOpts { w = 0.52f, h = 0.11f, bg = "#f5f5f2", fg = "#111", single = true });
            bool plateUsed = false;
            var cover = kit.Mat("paint:#8c9095", new MatSpec { roughness = 0.95f });
            void car(float x, float z, float ry, int color, bool covered)
            {
                var c = J.Group("car"); J.SetPos(c, x, 0, z); J.SetYaw(c, ry);
                var p = paint(color);
                if (covered) c.Add(kit.RBox(1.95f, 1.38f, 4.7f, 0.35f, cover, at(y: 0.06f)));
                else
                {
                    c.Add(kit.RBox(1.82f, 0.66f, 4.62f, 0.16f, p, at(y: 0.26f)));
                    c.Add(kit.RBox(1.6f, 0.56f, 2.45f, 0.2f, glass, at(y: 0.86f, z: 0.25f)));
                    c.Add(kit.RBox(1.5f, 0.07f, 1.9f, 0.03f, p, at(y: 1.39f, z: 0.3f)));
                    foreach (var sx in new[] { -1, 1 })
                    {
                        c.Add(kit.Box(0.4f, 0.09f, 0.05f, lens, at(sx * 0.6f, 0.68f, -2.29f)));
                        c.Add(kit.Box(0.35f, 0.09f, 0.05f, tail, at(sx * 0.62f, 0.74f, 2.29f)));
                        c.Add(kit.Box(0.08f, 0.1f, 0.16f, p, at(sx * 0.95f, 0.96f, -0.75f)));
                    }
                    foreach (var pz in new[] { -2.32f, 2.32f })
                    {
                        var pl = plateUsed ? UnityEngine.Object.Instantiate(plate) : plate; plateUsed = true;   // plate.clone()
                        J.SetPos(pl, 0, 0.45f, pz); J.SetYaw(pl, pz < 0 ? PI : 0); c.Add(pl);
                    }
                }
                foreach (var sx in new[] { -0.82f, 0.82f }) foreach (var sz in new[] { -1.45f, 1.4f })
                    {
                        c.Add(kit.Cyl(0.33f, 0.33f, 0.22f, tyre, at(sx, 0.22f, sz, rz: PI / 2), 18));
                        c.Add(kit.Cyl(0.2f, 0.2f, 0.225f, rim, at(sx * 1.002f, 0.2125f, sz, rz: PI / 2), 12));
                    }
                add(c);
            }
            var cars = new (float x, float z, float ry, int col, bool cov)[]
            {
                (9.7f, -13.4f, 0, 0xe9e9e6, false), (12.15f, -13.4f, 0, 0x1b1c1f, false), (17.9f, -13.4f, 0, 0xa7abb0, true), (20.6f, -13.4f, 0, 0x4a4e54, false),
                (9.7f, -2.6f, PI, 0x22304a, false), (14.6f, 2.6f, 0, 0xe9e9e6, false), (17.9f, -2.6f, PI, 0xa7abb0, false), (20.6f, 2.6f, 0, 0x1b1c1f, false), (17.9f, 13.4f, PI, 0xe9e9e6, false),
            };
            var carSolids = new List<Solid>();
            foreach (var c in cars) { car(c.x, c.z, c.ry, c.col, c.cov); carSolids.Add(new Solid(c.x - 0.95f, c.x + 0.95f, c.z - 2.35f, c.z + 2.35f, -1, 1.5f)); }
            // wheel stoppers in the car bays
            var stops = new List<Matrix4x4>();
            foreach (var c in cars) foreach (var d in new[] { -0.6f, 0.6f })
                    stops.Add(M4(c.x + d, 0, c.z + (Mathf.Abs(c.z) > 10 ? Math.Sign(c.z) * 2.2f : (c.ry != 0 ? -1 : 1) * 2.35f * (c.z != 0 ? Math.Sign(c.z) : 1)), 0));
            inst(Geo.Box(0.5f, 0.1f, 0.15f).Translate(0, 0.05f, 0), T.HazardMaterial(), stops, false);

            res.solids.Add(new Solid(RECEPTION.x - 0.95f, RECEPTION.x + 0.95f, RECEPTION.z - 0.35f, RECEPTION.z + 0.35f, -1, 0.8f));
            res.solids.Add(new Solid(RADIO.x - 0.65f, RADIO.x + 0.65f, RADIO.z - 0.35f, RADIO.z + 0.35f, -1, 0.8f));
            res.solids.Add(new Solid(BOARD.x - 0.95f, BOARD.x + 0.95f, BOARD.z - 0.32f, BOARD.z + 0.32f, -1, 2.1f));
            res.solids.Add(new Solid(PALLETS.x - 1.3f, PALLETS.x + 1.2f, PALLETS.z - 1.6f, PALLETS.z + 2.0f, -1, 1.5f));
            res.solids.Add(new Solid(7.1f, 7.7f, -4.7f, -3.3f, -1, 0.9f));
            res.solids.AddRange(carSolids);
            return res;
        }
    }
}
