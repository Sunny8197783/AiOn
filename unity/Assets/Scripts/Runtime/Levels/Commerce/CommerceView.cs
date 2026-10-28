// Ports the windowView canvas of levels/commerce-goods.js: the street or skyline painted per mood.
using System.Collections.Generic;
using Gyeongbo.Engine;

namespace Gyeongbo.Levels
{
    static class CommerceView
    {
        static readonly Dictionary<string, string[]> SKY = new Dictionary<string, string[]>
        { ["day"] = new[] { "#8fb8e0", "#dfe9f0" }, ["dusk"] = new[] { "#3b3a6b", "#f29a5c" }, ["night"] = new[] { "#0c1424", "#2a3046" }, ["blackout"] = new[] { "#05070d", "#0d1220" } };
        static readonly Dictionary<string, float> LIT = new Dictionary<string, float> { ["day"] = 0, ["dusk"] = 0.3f, ["night"] = 0.55f, ["blackout"] = 0.02f };
        static readonly string[] SHOPS = { "약국", "세탁", "분식", "부동산", "미용실", "정육점", "철물점" };
        static readonly string[] SHOP_BG = { "#1f6fb2", "#c4161c", "#2f8a57", "#e8a33a" };

        public static void Draw(Paint g, float W, float H, string kind, string mood, int seed)
        {
            var r = Kit.Rng((uint)seed); bool dark = mood == "night" || mood == "blackout", street = kind == "street";
            var sky = SKY[mood];
            var gr = new PaintGradient(0, 0, 0, H * 0.75f); gr.addColorStop(0, sky[0]); gr.addColorStop(1, sky[1]); g.fillStyle = gr; g.fillRect(0, 0, W, H);
            if (mood == "blackout") { g.fillStyle = "#fff"; for (int i = 0; i < 160; i++) g.fillRect(r() * W, r() * H * 0.45f, 1.5f, 1.5f); }
            float lit = LIT[mood], @base = street ? H * 0.72f : H * 0.8f;
            for (float x = -20; x < W;)
            {
                float bw = 60 + r() * 140, bh = (street ? 140 : 40) + r() * (street ? 200 : 110), tone = 150 + r() * 70;
                float k = mood == "day" ? 1 : mood == "dusk" ? 0.45f : 0.16f;
                g.fillStyle = $"rgb({(int)(tone * k)},{(int)((tone - 8) * k)},{(int)((tone - 16) * k)})"; g.fillRect(x, @base - bh, bw, bh);
                for (float wy = @base - bh + 12; wy < @base - 30; wy += street ? 26 : 12)
                    for (float wx = x + 8; wx < x + bw - 12; wx += street ? 22 : 10)
                    {
                        bool on = r() < lit; g.fillStyle = on ? (r() < 0.75f ? "#ffd08a" : "#dff0ff") : mood == "day" ? "#5d7184" : "#141a24";
                        g.fillRect(wx, wy, street ? 12 : 5, street ? 14 : 6);
                    }
                if (street)
                { // ground-floor shop sign
                    var sg = SHOPS[(int)(r() * 7)];
                    g.fillStyle = mood == "blackout" ? "#1a1d22" : SHOP_BG[(int)(r() * 4)]; g.fillRect(x + 6, @base - 48, bw - 12, 22);
                    g.fillStyle = mood == "blackout" ? "#3a3d42" : "#fff"; g.font = $"700 16px {Kit.FONT}"; g.textAlign = "center"; g.fillText(sg, x + bw / 2, @base - 31);
                }
                else if (r() < 0.3f) { g.fillStyle = mood == "blackout" ? "#300" : "#ff3b30"; g.fillRect(x + bw / 2, @base - bh - 3, 3, 3); }
                x += bw + r() * 12;
            }
            if (street)
            {
                g.fillStyle = mood == "day" ? "#8f8a84" : dark ? "#1b1c1f" : "#4a4644"; g.fillRect(0, @base, W, H * 0.08f);
                g.fillStyle = mood == "day" ? "#3a3c3f" : "#101113"; g.fillRect(0, @base + H * 0.08f, W, H);
                g.fillStyle = mood == "blackout" ? "#2a2a2a" : "#e8e8e0"; for (float x = 0; x < W; x += 90) g.fillRect(x, H * 0.92f, 50, 4);
                for (float x = 80; x < W; x += 300)
                {
                    g.fillStyle = "#2a2d31"; g.fillRect(x, @base - 170, 5, 170 + H * 0.08f);
                    if (mood == "dusk" || mood == "night")
                    {
                        var lg = new PaintGradient(x + 2, @base - 170, 2, x + 2, @base - 170, 60); lg.addColorStop(0, "rgba(255,230,180,1)"); lg.addColorStop(1, "rgba(255,200,120,0)");
                        g.fillStyle = lg; g.fillRect(x - 60, @base - 230, 124, 124);
                    }
                }
            }
        }
    }
}
