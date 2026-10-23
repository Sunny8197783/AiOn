// Ports levels/apartment-props.js: 302호 props that follow the director's states — the battery radio on the living-room
// table (dial glows when on), a candle that flickers and lights the room (fire-risk prop), the emergency backpack by the
// door (empty / packed), the wallet with the ID card, and the wall planner board (계획판) the 1장 slots are chosen on.
using System;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ApartmentParts;

namespace Gyeongbo.Levels
{
    public sealed class AptProps
    {
        public Action<string> setRadio, setCandle, setBag, setWallet;   // apartment states radio / candles / bag / wallet
        public Action<float, float> update;
    }

    public static class ApartmentProps
    {
        public static AptProps BuildProps(AptBuilder B, GameObject g, GameObject dyn)
        {
            var kit = B.kit; var M = B.M; double T = ApartmentHome.HOME.top;
            Material P(string hex, double roughness = 0.6, double metalness = 0) => kit.Mat("paint:" + hex, new MatSpec { roughness = (float)roughness, metalness = (float)metalness });

            // ---- radio (휴대용 라디오): body, speaker grille, lit dial window, knobs, handle, extended antenna ----
            var radio = J.Group("radio"); SetPos(radio, -2.2, T, 5.88); SetYaw(radio, Math.PI + 0.35); dyn.Add(radio);
            var dialTex = B.Tex("radioDial", 256, 112, DrawDial);
            var dial = Mat.Lit(new MatSpec { color = J.Hex(0x1a1408), map = dialTex, emissive = J.Hex(0xffb04a), emissiveMap = dialTex, emissiveIntensity = 0, roughness = 0.25f });
            var led = Mat.Lit(new MatSpec { color = J.Hex(0x0b1a0e), emissive = J.Hex(0x3cff6a), emissiveIntensity = 0, roughness = 0.4f });
            radio.Add(B.RBox(0.26, 0.15, 0.075, 0.016, P("#3a3d40", 0.45, 0.2), A()));
            void face(float w, float h, Material m, double x, double y) { var p = Obj(Geo.Plane(w, h), m); SetPos(p, x, y, 0.0385); radio.Add(p); }
            face(0.11f, 0.1f, Mat.Lit(new MatSpec { map = B.Tex("radioGrille", 128, 128, DrawGrille), roughness = 0.6f, metalness = 0.3f }), -0.06, 0.075);
            face(0.1f, 0.044f, dial, 0.065, 0.1);
            radio.Add(B.Cyl(0.017, 0.017, 0.02, M.black, A(x: 0.065, y: 0.025, z: 0.045, rx: Math.PI / 2), 18), B.Cyl(0.011, 0.011, 0.016, M.black, A(x: 0.105, y: 0.03, z: 0.043, rx: Math.PI / 2), 14));
            radio.Add(B.Box(0.008, 0.008, 0.004, led, A(x: 0.02, y: 0.045, z: 0.038)));
            radio.Add(B.Box(0.2, 0.014, 0.022, M.black, A(y: 0.17)), B.Box(0.014, 0.03, 0.02, M.black, A(x: -0.093, y: 0.145)), B.Box(0.014, 0.03, 0.02, M.black, A(x: 0.093, y: 0.145)));
            B.Rod(radio, V3(0.11, 0.15, -0.02), V3(0.2, 0.5, -0.09), 0.0035, M.metal); kit.Merge(radio);
            var radioPt = B.Point("homeRadio", "라디오", -2.0, FL, 5.95, "radio", 1.7);
            void setRadio(string v)
            {
                radio.SetActive(v != "none");
                Mat.SetEmissiveIntensity(dial, v == "on" ? 1.7f : 0); Mat.SetEmissiveIntensity(led, v == "on" ? 2.2f : 0);
                radioPt.label = v == "none" ? "거실 탁자" : "라디오";
            }

            // ---- candle on a saucer: additive flame sprite + halo + a warm flickering light (created once: no recompiles) ----
            var candle = J.Group("candle"); SetPos(candle, -1.62, T, 6.12); dyn.Add(candle);
            candle.Add(B.Cyl(0.055, 0.06, 0.012, P("#f1ede4", 0.3), A(), 20), B.Cyl(0.027, 0.027, 0.1, P("#f6f1e2", 0.7), A(y: 0.012), 18), B.Cyl(0.0018, 0.0018, 0.012, M.black, A(y: 0.112), 6));
            GameObject sprite(Texture2D tex, Color color, float sx, float sy, double y, out Material mat)
            {
                mat = Mat.Unlit(new MatSpec { map = tex, color = color, blending = "additive", depthWrite = false, transparent = true });
                var s = AptBillboard.Make(mat, sx, sy); SetPos(s, 0, y, 0); candle.Add(s); return s;
            }
            var flame = sprite(B.Tex("candleFlame", 64, 128, DrawFlame), Lin(2.6, 1.9, 1.1), 0.026f, 0.06f, 0.148, out _);
            var halo = sprite(B.Tex("candleHalo", 64, 64, DrawHalo), Lin(0.55, 0.32, 0.12), 0.34f, 0.34f, 0.14, out var haloMat); kit.Merge(candle);
            var light = kit.PointLight(J.Hex(0xff9a40), 1, 4.5f, 2); SetPos(light.gameObject, -1.62, T + 0.2, 6.12); dyn.Add(light);
            // shortcut: Kit has no runtime three.js -> Unity intensity setter; the unit is read off a 1-intensity light, assumes Kit scales linearly.
            float unit = light.intensity; light.intensity = 0;
            bool lit = false, wasLit = false;
            void setCandle(string v) { lit = v == "lit"; wasLit |= lit; candle.SetActive(lit || wasLit); flame.SetActive(lit); halo.SetActive(lit); if (!lit) light.intensity = 0; }

            // ---- emergency backpack by the door: packed (full, bottle in the side pocket) or empty (slumped) ----
            var bag = J.Group("bag"); SetPos(bag, -2.25, FL, 1.78); SetYaw(bag, Math.PI / 2); dyn.Add(bag);
            Material fab = P("#2f3642", 0.95), fab2 = P("#3d4656", 0.95);
            bag.Add(B.RBox(0.32, 0.46, 0.22, 0.07, fab, A()), B.RBox(0.26, 0.22, 0.07, 0.03, fab2, A(y: 0.04, z: 0.12)));
            bag.Add(B.Box(0.22, 0.012, 0.004, P("#c9ccd0", 0.2, 0.6), A(y: 0.3, z: 0.111)), B.Box(0.16, 0.018, 0.004, P("#e8e8e2", 0.3), A(y: 0.2, z: 0.157)));
            foreach (var x in new[] { -0.08, 0.08 }) bag.Add(B.Box(0.05, 0.4, 0.02, P("#1b1f26", 0.9), A(x: x, y: 0.04, z: -0.12)));
            B.Rod(bag, V3(-0.04, 0.46, 0), V3(0.04, 0.46, 0), 0.008, P("#1b1f26", 0.9));
            var bottle = B.Cyl(0.033, 0.033, 0.2, P("#bfe3f2", 0.12), A(x: 0.185, y: 0.05), 14); Kit.Keep(bottle); bag.Add(bottle); kit.Merge(bag);
            var bagPt = B.Point("homeBag", "비상가방", -2.0, FL, 1.8, "bag", 1.4);
            void setBag(string v)
            {
                bag.SetActive(v != "none"); bottle.SetActive(v == "packed");
                bag.transform.localScale = v == "packed" ? new Vector3(1, 1, 1) : new Vector3(1, 0.7f, 0.42f);
                SetPos(bag, v == "packed" ? -2.25 : -2.31, FL, 1.78);
                bagPt.label = v == "none" ? "가방 둘 자리" : v == "packed" ? "비상가방 (꾸림)" : "비상가방 (비어 있음)";
            }

            // ---- wallet with the ID card on the living-room table (picked up = gone from the table and from E) ----
            var wallet = J.Group("wallet"); SetPos(wallet, -1.98, T, 6.13); SetYaw(wallet, 0.4); dyn.Add(wallet);
            wallet.Add(B.RBox(0.11, 0.022, 0.09, 0.008, P("#4a2e1e", 0.7), A()));
            wallet.Add(B.Box(0.085, 0.003, 0.054, P("#dfe7ee", 0.4), A(x: 0.035, y: 0.022, z: 0.02, ry: 0.3))); // card peeking out
            var walletPt = B.Point("homeWallet", "지갑 · 신분증", -1.98, FL, 6.13, "pickup", 1.4);
            void setWallet(string v) { wallet.SetActive(v != "taken"); walletPt.enabled = v != "taken"; }

            // ---- 계획판: whiteboard with this week's slots, sticky notes and magnets, on the bathroom wall facing the living room ----
            double pz = 3.6, px = -0.4, py = FL + 1.45;
            g.Add(B.Box(1.0, 0.72, 0.025, M.alu, A(x: px, y: py - 0.36, z: pz + 0.0125)));
            var board = Obj(Geo.Plane(0.96f, 0.68f), Mat.Lit(new MatSpec { map = B.Tex("planner", 512, 364, DrawPlanner), roughness = 0.35f }));
            SetPos(board, px, py, pz + 0.026); g.Add(board);
            g.Add(B.Box(0.6, 0.025, 0.05, M.alu, A(x: px, y: py - 0.38, z: pz + 0.03)), B.Cyl(0.008, 0.008, 0.12, P("#1f5fbf", 0.4), A(x: px - 0.1, y: py - 0.407, z: pz + 0.04, rz: Math.PI / 2), 8));
            B.Point("homePlanner", "달력 (계획판)", px, FL, pz + 0.55, "planner", 1.5);

            void update(float dt, float t)
            {
                if (!lit) return;
                float k = 1 + 0.07f * Mathf.Sin(t * 13.1f) + 0.05f * Mathf.Sin(t * 7.3f + 1.3f) + 0.03f * Mathf.Sin(t * 23.7f);
                light.intensity = 2.2f * k * unit;
                flame.transform.localScale = new Vector3(0.026f, 0.06f * (0.94f + 0.07f * Mathf.Sin(t * 11.3f)), 1); SetPos(flame, 0.0015 * Mathf.Sin(t * 5.1f), 0.148, 0);
                Mat.SetOpacity(haloMat, 0.85f + 0.12f * Mathf.Sin(t * 9.7f));
            }
            setRadio("none"); setCandle("none"); setBag("empty");
            return new AptProps { setRadio = setRadio, setCandle = setCandle, setBag = setBag, setWallet = setWallet, update = update };
        }

        static void DrawDial(Paint c, float w, float h)
        {
            c.fillStyle = "#e9c98a"; c.fillRect(0, 0, w, h); c.fillStyle = "#3a2a10"; c.font = Fnt(600, 15); c.textAlign = "center";
            var marks = new[] { "88", "92", "96", "100", "104", "108" };
            for (int i = 0; i < marks.Length; i++) { float x = 18 + i * 44; c.fillText(marks[i], x, 30); c.fillRect(x - 1, 40, 2, 18); }
            for (double x = 14; x < w - 10; x += 8.8) c.fillRect((float)x, 48, 1, 10);
            c.fillText("FM  MHz", w / 2, 92); c.fillStyle = "#c4161c"; c.fillRect(165, 36, 3, 40);
        }

        static void DrawGrille(Paint c, float w, float h)
        {
            c.fillStyle = "#2b2e31"; c.fillRect(0, 0, w, h); c.fillStyle = "#121416";
            for (int y = 6; y < h; y += 9) for (int x = 6 + (y % 18 != 0 ? 4 : 0); x < w; x += 9) { c.beginPath(); c.arc(x, y, 2.6f, 0, 7); c.fill(); }
        }

        static void DrawFlame(Paint c, float w, float h)
        {
            var gr = new PaintGradient(w / 2, h * 0.68f, 2, w / 2, h * 0.6f, h * 0.45f);
            gr.addColorStop(0, "rgba(255,255,240,1)"); gr.addColorStop(0.25f, "rgba(255,214,120,0.95)"); gr.addColorStop(0.6f, "rgba(255,130,40,0.5)"); gr.addColorStop(1, "rgba(255,80,10,0)");
            c.fillStyle = gr; c.beginPath(); c.moveTo(w / 2, 2); c.bezierCurveTo(w * 0.95f, h * 0.5f, w * 0.85f, h * 0.95f, w / 2, h - 2); c.bezierCurveTo(w * 0.15f, h * 0.95f, w * 0.05f, h * 0.5f, w / 2, 2); c.fill();
        }

        static void DrawHalo(Paint c, float w, float h)
        {
            var gr = new PaintGradient(w / 2, h / 2, 0, w / 2, h / 2, w / 2); gr.addColorStop(0, "rgba(255,200,140,0.9)"); gr.addColorStop(1, "rgba(255,160,80,0)");
            c.fillStyle = gr; c.fillRect(0, 0, w, h);
        }

        static void DrawPlanner(Paint c, float w, float h)
        {
            c.fillStyle = "#fbfbf8"; c.fillRect(0, 0, w, h);
            c.fillStyle = "#2a2f36"; c.font = Fnt(800, 26); c.textAlign = "left"; c.fillText("이번 주 계획", 18, 36);
            var days = new[] { "월", "화", "수", "목", "금", "토", "일" }; const float cw = 54, x0 = 18, y0 = 54;
            for (int i = 0; i < days.Length; i++)
            {
                float x = x0 + i * cw;
                c.fillStyle = i == 2 || i == 3 ? "#fff3d6" : "#f1f2f0"; c.fillRect(x, y0, cw - 4, 270);
                c.fillStyle = i >= 5 ? "#c4161c" : "#3a3f46"; c.font = Fnt(700, 18); c.textAlign = "center"; c.fillText(days[i], x + cw / 2 - 2, y0 + 22);
            }
            void tile(float x, float y) { c.strokeStyle = "#c99a2e"; c.lineWidth = 2; c.setLineDash(5, 4); c.strokeRect(x, y, cw - 14, 34); c.setLineDash(); }
            tile(x0 + 2 * cw + 5, y0 + 226);
            for (int i = 0; i < 5; i++) tile(x0 + 3 * cw + 5, y0 + 40 + i * 46);
            c.fillStyle = "#8a8f96"; c.font = Fnt(500, 13); c.textAlign = "left"; c.fillText("저녁 1칸 · 내일 5칸", x0 + 2 * cw, y0 + 290);
            void note(float x, float y, float r, string bg, string[] lines)
            {
                c.save(); c.translate(x, y); c.rotate(r); c.fillStyle = bg; c.fillRect(0, 0, 112, 92); c.fillStyle = "#3a3320"; c.font = Fnt(600, 15);
                for (int i = 0; i < lines.Length; i++) c.fillText(lines[i], 10, 28 + i * 22);
                c.restore();
            }
            note(396, 60, 0.04f, "#fff07a", new[] { "관리비", "25일까지" });
            note(392, 170, -0.05f, "#ffc3d0", new[] { "분리수거", "화 · 금" });
            c.fillStyle = "#d33"; c.beginPath(); c.arc(452, 64, 6, 0, 7); c.fill(); c.fillStyle = "#36c"; c.beginPath(); c.arc(448, 174, 6, 0, 7); c.fill();
        }
    }
}
