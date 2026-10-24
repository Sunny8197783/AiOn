// Ports the label half of levels/commerce-goods.js: LABELS (fictional brands), their atlas cells and drawLabel/shade
// (the printed face of every packaged product). Drawn once into one atlas by CommerceGoods.
using System;
using System.Collections.Generic;
using System.Globalization;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    static class CommerceLabels
    {
        public const int GRID = 8;
        // name: [style, label bg, text, accent/cap, title, sub, liquid]. Brands are fictional.
        public static readonly List<KeyValuePair<string, string[]>> LABELS = new List<KeyValuePair<string, string[]>>
        {
            L("water2a", "pet", "#1f6fb2", "#fff", "#1f6fb2", "맑은샘", "먹는샘물 2L"), L("water2b", "pet", "#2f9e7a", "#fff", "#2a7d5f", "한빛수", "먹는샘물 2L"),
            L("water5", "pet", "#1f6fb2", "#fff", "#1f6fb2", "맑은샘", "먹는샘물 500mL"), L("water5b", "pet", "#eef5fb", "#1f4f8c", "#7fb2d8", "산골물", "먹는샘물 500mL"),
            L("cola", "pet", "#c8202f", "#fff", "#c8202f", "톡톡콜라", "500mL", "#3a1a0e"), L("cider", "pet", "#2f9a3c", "#fff", "#2f9a3c", "맑은사이다", "500mL", "#d6f0d2"),
            L("ion", "pet", "#2a6cc2", "#fff", "#e8eef5", "이온워터", "620mL", "#d8ecf8"), L("tea", "pet", "#8a5a2b", "#fff3df", "#6b3f1a", "보리차", "무카페인 500mL", "#b07a3a"),
            L("coffeeCan", "can", "#3b2416", "#f2dcc0", "#c99a62", "진한커피", "175mL"), L("juiceCan", "can", "#f28a1a", "#fff", "#ffd36a", "오렌지", "과즙 100%"),
            L("soda", "can", "#1f8fd0", "#fff", "#bfe8ff", "탄산수", "355mL"), L("vita", "can", "#6b3a1f", "#ffd200", "#f2c230", "비타음료", "100mL"),
            L("ramenR", "bag", "#c4161c", "#fff", "#ffcf33", "얼큰라면", "5개입"), L("ramenB", "bag", "#1d1d1f", "#ffd200", "#e8481c", "짜장라면", "4개입"),
            L("ramenW", "bag", "#f2efe6", "#c4161c", "#e94e1b", "사골라면", "5개입"), L("cupR", "cup", "#d8261c", "#fff", "#f2f2f2", "얼큰 컵라면", "큰컵"),
            L("cupU", "cup", "#f4e9cf", "#2f6b3a", "#f2f2f2", "튀김우동", "컵"), L("rice", "box", "#fbfbf8", "#2b2b2b", "#d64534", "즉석밥", "210g × 3"),
            L("tuna", "tin", "#f2c12e", "#123a7a", "#123a7a", "살코기 참치", "150g"), L("ham", "tin", "#1f4fa8", "#ffd200", "#e2e6ee", "통햄", "200g"),
            L("bar", "box", "#ffb81c", "#4a2a00", "#e8481c", "에너지바", "5개입"), L("cracker", "bag", "#b9853f", "#fff", "#7a4f1c", "건빵", "별사탕 포함"),
            L("chips", "bag", "#f2c230", "#b3171f", "#e94e1b", "감자칩", "오리지널"), L("chips2", "bag", "#2a7d3c", "#fff", "#9ed36a", "양파링", "대용량"),
            L("cookie", "box", "#6b3a1f", "#fff", "#f2c230", "초코쿠키", "12개입"), L("choco", "box", "#5a2a14", "#ffd200", "#c4161c", "초코과자", "12개입"),
            L("tissue", "box", "#f6f2f4", "#c0436e", "#f5b8cc", "도톰 화장지", "30롤"), L("wipes", "bag", "#e8f5ec", "#1f7a4a", "#6cc08f", "순한 물티슈", "100매"),
            L("bags", "box", "#2f5d9c", "#fff", "#9cc4f2", "비닐봉투", "50매"), L("tape", "box", "#1f5fb4", "#fff", "#123a7a", "청테이프", "48mm"),
            L("rain", "bag", "#f4f6f8", "#2b6cb0", "#ffd200", "일회용 우의", "성인용"), L("foil", "bag", "#c9ccd2", "#333", "#e5b84a", "은박 담요", "비상용"),
            L("jug", "box", "#5fa8d3", "#fff", "#1f4f8c", "접이식 물통", "10L"), L("stove", "box", "#2b2b2b", "#fff", "#e8481c", "휴대용 가스레인지", "캠핑·가정용"),
            L("butane", "tin", "#f28a1a", "#fff", "#ffffff", "부탄가스", "220g"), L("whistle", "blister", "#ff7a00", "#fff", "#ff7a00", "호루라기", ""),
            L("radio", "box", "#3b3f46", "#fff", "#f2c230", "휴대용 라디오", "FM · AM"), L("aa", "blister", "#1d1d1f", "#f2c230", "#d4a017", "알카라인 AA", "4개입"),
            L("aaa", "blister", "#7a3b12", "#fff", "#d98b3a", "알카라인 AAA", "4개입"), L("flash", "box", "#1d1d1f", "#fff", "#5fd0ff", "LED 손전등", "건전지 별매"),
            L("power", "box", "#f4f6f8", "#1d1d1f", "#2b6cb0", "보조배터리", "10000mAh"), L("candle", "box", "#f6efe2", "#7a3b12", "#e8a33a", "양초", "6개입"),
            L("aid", "box", "#f6f6f6", "#1f7a4a", "#1f7a4a", "구급함", "가정용"), L("mask", "box", "#ffffff", "#1f4f8c", "#4a90d9", "보건용 마스크", "10매"),
            L("sanit", "pet", "#ffffff", "#1f6fb2", "#1f6fb2", "손소독제", "500mL", "#e8f4ff"), L("band", "box", "#f2d4b8", "#7a3b12", "#e8481c", "밴드", "30매"),
            L("soap", "pet", "#7cc242", "#fff", "#2f8a3c", "주방세제", "1L", "#c8f08a"), L("deter", "box", "#2b8fd8", "#fff", "#ffd200", "세탁세제", "2.5kg"),
            L("towel", "box", "#ffffff", "#5a8f3c", "#a8d08d", "키친타올", "4롤"), L("milk", "carton", "#2b6cb0", "#fff", "#ffffff", "우유", "900mL"),
            L("yogurt", "cup", "#f7c7d8", "#9b2a5a", "#ffffff", "딸기 요구르트", ""), L("tofu", "box", "#ffffff", "#1f7a4a", "#ffe08a", "두부", "300g"),
            L("mix", "box", "#f2c230", "#6b3a1f", "#c4161c", "커피믹스", "100개입"), L("soup", "bag", "#f6efe2", "#7a3b12", "#c4161c", "즉석 미역국", "1인분"),
            L("cereal", "box", "#e8481c", "#fff", "#f2c230", "콘시리얼", "가족용"), L("oil", "pet", "#f2e6b0", "#6b4a10", "#d4a017", "참기름", "350mL", "#d9a426"),
            L("soy", "pet", "#c4161c", "#fff", "#1d1d1f", "진간장", "900mL", "#2a120a"), L("kimchi", "bag", "#f6efe2", "#c4161c", "#2f8a3c", "포기김치", "500g"),
            L("gim", "bag", "#1f3d24", "#fff", "#2f8a3c", "도시락김", "8봉"), L("soymilk", "carton", "#c9a66b", "#fff", "#f6efe2", "두유", "190mL"),
            L("gum", "blister", "#2fae66", "#fff", "#9ee8b8", "자일리톨 껌", ""),
            L("pack6", "pack", "#1f6fb2", "#fff", "#1f6fb2", "맑은샘 2L × 6", "먹는샘물"), L("pack6b", "pack", "#2f9e7a", "#fff", "#2a7d5f", "한빛수 2L × 6", "먹는샘물"),
        };
        public static readonly Dictionary<string, int> CELL = new Dictionary<string, int>();
        static CommerceLabels() { for (int i = 0; i < LABELS.Count; i++) CELL[LABELS[i].Key] = i; }

        static KeyValuePair<string, string[]> L(string k, params string[] v) => new KeyValuePair<string, string[]>(k, v);
        public static string F(double v) => v.ToString(CultureInfo.InvariantCulture);
        static string Font(float size, int weight) => $"{weight} {F(size)}px {Kit.FONT}";

        static void FitText(Paint g, string text, float size, int weight, float maxW)
        {
            float s = size; g.font = Font(s, weight);
            while (g.measureText(text).width > maxW && s > 7) { s -= 1; g.font = Font(s, weight); }
        }

        public static void DrawLabel(Paint g, float c, string[] L)
        {
            string style = L[0], bg = L[1], fg = L[2], ac = L[3], title = L[4], sub = L[5], liquid = L.Length > 6 ? L[6] : "#cfe8f2";
            void txt(string t, float x, float y, float size, string color, int weight = 800, float? maxW = null, string stroke = null)
            {
                if (string.IsNullOrEmpty(t)) return; FitText(g, t, size, weight, maxW ?? c * 0.82f); g.textAlign = "center"; g.textBaseline = "middle";
                if (stroke != null) { g.lineWidth = size * 0.16f; g.strokeStyle = stroke; g.strokeText(t, x, y); }
                g.fillStyle = color; g.fillText(t, x, y);
            }
            PaintGradient vgrad(string a, string b) { var gr = new PaintGradient(0, 0, 0, c); gr.addColorStop(0, a); gr.addColorStop(1, b); return gr; }
            void hsheen(float alpha)
            {
                var gr = new PaintGradient(0, 0, c, 0); gr.addColorStop(0, $"rgba(0,0,0,{F(alpha)})"); gr.addColorStop(0.42f, $"rgba(255,255,255,{F(alpha)})");
                gr.addColorStop(0.58f, "rgba(255,255,255,0)"); gr.addColorStop(1, $"rgba(0,0,0,{F(alpha)})"); g.fillStyle = gr; g.fillRect(0, 0, c, c);
            }
            if (style == "pet")
            { // clear PET bottle: cap, neck, liquid, label band
                g.fillStyle = liquid; g.fillRect(0, 0, c, c);
                g.fillStyle = vgrad("rgba(255,255,255,0.35)", "rgba(0,0,0,0.12)"); g.fillRect(0, 0, c, c);
                g.fillStyle = "rgba(255,255,255,0.45)"; g.fillRect(c * 0.44f, 0, c * 0.05f, c); g.fillRect(c * 0.6f, 0, c * 0.02f, c);
                g.fillStyle = ac; g.fillRect(0, 0, c, c * 0.075f);
                g.fillStyle = bg; g.fillRect(0, c * 0.4f, c, c * 0.3f);
                g.fillStyle = "rgba(255,255,255,0.8)"; g.fillRect(0, c * 0.4f, c, c * 0.02f); g.fillRect(0, c * 0.68f, c, c * 0.02f);
                txt(title, c / 2, c * 0.51f, c * 0.12f, fg, 800, c * 0.46f); txt(sub, c / 2, c * 0.62f, c * 0.055f, fg, 600, c * 0.46f);
            }
            else if (style == "can" || style == "tin")
            {
                g.fillStyle = bg; g.fillRect(0, 0, c, c); hsheen(0.22f);
                if (style == "can") { g.fillStyle = "#c9ccd1"; g.fillRect(0, 0, c, c * 0.06f); g.fillRect(0, c * 0.95f, c, c * 0.05f); }
                g.fillStyle = ac; g.fillRect(0, c * 0.66f, c, c * 0.08f);
                txt(title, c / 2, c * 0.42f, c * 0.13f, fg, 800, c * 0.48f); txt(sub, c / 2, c * 0.82f, c * 0.07f, fg, 600, c * 0.48f);
            }
            else if (style == "bag")
            { // pillow pack: burst, product photo, bold title
                g.fillStyle = vgrad(bg, Shade(bg, 0.72f)); g.fillRect(0, 0, c, c);
                g.fillStyle = ac; g.beginPath();
                for (int i = 0; i < 24; i++) { float a = i / 24f * Mathf.PI * 2, r = i % 2 == 1 ? c * 0.1f : c * 0.16f; g.lineTo(c * 0.78f + Mathf.Cos(a) * r, c * 0.2f + Mathf.Sin(a) * r); }
                g.fill();
                g.fillStyle = "rgba(255,255,255,0.85)"; g.beginPath(); g.ellipse(c * 0.36f, c * 0.7f, c * 0.24f, c * 0.12f, 0, 0, 7); g.fill();
                g.fillStyle = Shade(ac, 0.8f); g.beginPath(); g.ellipse(c * 0.36f, c * 0.68f, c * 0.19f, c * 0.07f, 0, 0, 7); g.fill();
                txt(title, c / 2, c * 0.42f, c * 0.17f, fg, 900, c * 0.86f, "rgba(0,0,0,0.35)"); txt(sub, c * 0.78f, c * 0.86f, c * 0.07f, fg, 700, c * 0.36f);
                g.fillStyle = "rgba(255,255,255,0.18)"; g.fillRect(0, c * 0.06f, c, c * 0.04f);
            }
            else if (style == "blister")
            { // hanging card with a clear window
                g.fillStyle = bg; g.fillRect(0, 0, c, c);
                g.fillStyle = "#ffffff"; g.beginPath(); g.ellipse(c / 2, c * 0.07f, c * 0.1f, c * 0.03f, 0, 0, 7); g.fill();
                g.fillStyle = "rgba(230,236,240,0.9)"; g.fillRect(c * 0.18f, c * 0.16f, c * 0.64f, c * 0.5f);
                g.fillStyle = ac; for (int i = 0; i < 4; i++) g.fillRect(c * (0.23f + i * 0.145f), c * 0.2f, c * 0.1f, c * 0.42f);
                g.fillStyle = "rgba(255,255,255,0.4)"; for (int i = 0; i < 4; i++) g.fillRect(c * (0.245f + i * 0.145f), c * 0.2f, c * 0.025f, c * 0.42f);
                txt(title, c / 2, c * 0.77f, c * 0.1f, fg, 800); txt(sub, c / 2, c * 0.9f, c * 0.07f, fg, 600);
            }
            else if (style == "pack")
            { // shrink-wrapped six-pack: bottles seen through film, brand band, carry handle
                g.fillStyle = vgrad("#e4f1f8", "#a9cfe0"); g.fillRect(0, 0, c, c);
                for (int i = 0; i < 3; i++)
                {
                    float bx = c * (0.08f + i * 0.3f);
                    g.fillStyle = "rgba(120,170,200,0.55)"; g.fillRect(bx, c * 0.16f, c * 0.24f, c * 0.84f);
                    g.fillStyle = ac; g.fillRect(bx + c * 0.08f, c * 0.04f, c * 0.08f, c * 0.08f);
                    g.fillStyle = "rgba(255,255,255,0.5)"; g.fillRect(bx + c * 0.04f, c * 0.2f, c * 0.03f, c * 0.75f);
                }
                g.fillStyle = bg; g.fillRect(0, c * 0.46f, c, c * 0.26f);
                txt(title, c / 2, c * 0.55f, c * 0.1f, fg, 800, c * 0.9f); txt(sub, c / 2, c * 0.66f, c * 0.06f, fg, 600);
                g.fillStyle = "rgba(255,255,255,0.6)"; g.fillRect(c * 0.3f, 0, c * 0.4f, c * 0.03f);
            }
            else if (style == "cup")
            {
                g.fillStyle = bg; g.fillRect(0, 0, c, c); hsheen(0.12f);
                g.fillStyle = ac; g.fillRect(0, 0, c, c * 0.12f);
                g.fillStyle = "rgba(0,0,0,0.15)"; g.fillRect(0, c * 0.12f, c, c * 0.015f);
                txt(title, c / 2, c * 0.5f, c * 0.12f, fg, 900, c * 0.5f); txt(sub, c / 2, c * 0.7f, c * 0.07f, fg, 700, c * 0.5f);
            }
            else
            { // box / carton: printed carton with a top band and a 'photo'
                g.fillStyle = bg; g.fillRect(0, 0, c, c);
                g.fillStyle = ac; g.fillRect(0, 0, c, c * (style == "carton" ? 0.3f : 0.16f));
                g.fillStyle = Shade(ac, 1.1f); g.beginPath(); g.arc(c * 0.5f, c * 0.72f, c * 0.13f, 0, 7); g.fill();
                if (title == "구급함") { g.fillStyle = ac; g.fillRect(c * 0.44f, c * 0.6f, c * 0.12f, c * 0.24f); g.fillRect(c * 0.38f, c * 0.66f, c * 0.24f, c * 0.12f); }
                txt(title, c / 2, c * 0.4f, c * 0.14f, fg, 900); txt(sub, c / 2, c * 0.92f, c * 0.07f, fg, 700);
                g.strokeStyle = "rgba(0,0,0,0.18)"; g.lineWidth = c * 0.01f; g.strokeRect(0, 0, c, c);
            }
        }

        // '#rgb' / '#rrggbb' scaled by k -> 'rgb(r,g,b)' (JS padEnd with the last digit, like the original).
        public static string Shade(string hex, float k)
        {
            var h = hex.Replace("#", ""); h = h.PadRight(6, hex[hex.Length - 1]);
            int n = Convert.ToInt32(h.Substring(0, 6), 16);
            int ch(int v) => Math.Max(0, Math.Min(255, (int)Math.Floor(v * k + 0.5)));
            return $"rgb({ch(n >> 16)},{ch((n >> 8) & 255)},{ch(n & 255)})";
        }
    }
}
