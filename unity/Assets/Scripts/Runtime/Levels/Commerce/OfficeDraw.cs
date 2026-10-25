// The canvases of levels/office.js: the video-wall route map (drawWall), the 2x2 work-screen atlas (drawAtlas),
// the whiteboard (drawBoard), the wall clock (drawClock) and cellPlane (a plane mapped to one atlas cell).
using System;
using System.Linq;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    static class OfficeDraw
    {
        static readonly (string name, string detail)[] ROUTES = { ("서해안 노선", "운송 지연 · 내일부터"), ("거래처 휴업 알림", "3곳"), ("기사 결근 연락", "9명"), ("수도권 간선", "정상 운행"), ("중부 노선", "정상 운행"), ("영동 노선", "정상 운행") };
        static readonly (string name, float x, float y, float rr)[] NODES = { ("연무센터", 390, 400, 26), ("수도권 허브", 210, 190, 18), ("서해안", 130, 540, 18), ("중부", 580, 300, 18), ("남부", 480, 620, 18), ("영동", 680, 160, 18) };

        public static void Font(Paint g, float size, int weight = 700) => g.font = $"{weight} {CommerceLabels.F(size)}px {Kit.FONT}";

        public static void DrawWall(Paint g, float W, float Hh, int alerts, bool blink)
        {
            g.fillStyle = "#0b1216"; g.fillRect(0, 0, W, Hh);
            g.fillStyle = "#14232b"; g.fillRect(0, 0, W, 64);
            g.fillStyle = "#e8eef0"; Font(g, 30); g.textBaseline = "middle"; g.fillText("배송 노선 현황 · 연무센터", 28, 33);
            g.fillStyle = "#7fd1c4"; Font(g, 22, 500); g.fillText("17:25 기준 · 실시간", 1040, 33);
            g.strokeStyle = "#1b2a31"; g.lineWidth = 1; for (int x = 24; x < 760; x += 40) { g.beginPath(); g.moveTo(x, 84); g.lineTo(x, 700); g.stroke(); }
            for (int y = 84; y < 700; y += 40) { g.beginPath(); g.moveTo(24, y); g.lineTo(760, y); g.stroke(); }
            float hx = NODES[0].x, hy = NODES[0].y;
            for (int i = 0; i < NODES.Length - 1; i++)
            {
                var (_, x, y, _) = NODES[i + 1];
                bool hot = i == 1 && alerts >= 1;
                g.strokeStyle = hot ? "#e5484d" : "#3cc6b5"; g.lineWidth = 7; g.setLineDash(hot ? new[] { 18f, 12f } : new float[0]); g.lineDashOffset = blink ? 15 : 0;
                g.beginPath(); g.moveTo(hx, hy); g.quadraticCurveTo((hx + x) / 2 + (y - hy) * 0.15f, (hy + y) / 2 - (x - hx) * 0.15f, x, y); g.stroke();
                if (hot) { g.setLineDash(); g.fillStyle = "#e5484d"; Font(g, 24); g.fillText("▲ 지연", (hx + x) / 2 - 70, (hy + y) / 2 - 10); }
            }
            g.setLineDash();
            foreach (var (name, x, y, rr) in NODES)
            {
                g.fillStyle = "#0b1216"; g.beginPath(); g.arc(x, y, rr + 6, 0, 7); g.fill();
                g.fillStyle = rr > 20 ? "#f2b33d" : "#3cc6b5"; g.beginPath(); g.arc(x, y, rr, 0, 7); g.fill();
                g.fillStyle = "#e8eef0"; Font(g, 22, 600); g.textAlign = "center"; g.fillText(name, x, y + rr + 24); g.textAlign = "left";
            }
            g.fillStyle = "#8fa3ab"; Font(g, 18, 500); g.fillText("━ 정상   ╍ 지연·보류 (▲ 표시)", 36, 684);
            g.fillStyle = "#cfd8dc"; Font(g, 24); g.fillText("오늘의 알림", 800, 100);
            for (int i = 0; i < ROUTES.Length; i++)
            {
                var (name, detail) = ROUTES[i]; float y = 124 + i * 94; bool hot = i < alerts;
                g.fillStyle = hot ? "#3a1d20" : "#16232a"; g.fillRect(796, y, 460, 80);
                g.fillStyle = hot ? (blink ? "#ff6b6b" : "#e5484d") : i < 3 ? "#5b6770" : "#3cc6b5";
                g.beginPath(); g.arc(828, y + 40, 16, 0, 7); g.fill();
                g.fillStyle = "#0b1216"; Font(g, 22, 900); g.textAlign = "center"; g.fillText(hot ? "!" : "✓", 828, y + 41); g.textAlign = "left";
                g.fillStyle = hot ? "#ffb4a8" : "#e8eef0"; Font(g, 25); g.fillText(name, 858, y + 28);
                g.fillStyle = "#8fa3ab"; Font(g, 20, 500); g.fillText(detail, 858, y + 58);
                g.fillStyle = hot ? "#e5484d" : "#24343c"; g.fillRect(1160, y + 24, 82, 32);
                g.fillStyle = "#ffffff"; Font(g, 19); g.textAlign = "center"; g.fillText(hot ? "주의" : i < 3 ? "집계 중" : "정상", 1201, y + 41); g.textAlign = "left";
            }
            g.fillStyle = "#05080a"; g.fillRect(W / 2 - 3, 0, 6, Hh); g.fillRect(0, Hh / 2 - 3, W, 6); // video-wall seams
        }

        // Four generic work screens in one atlas (2 x 2 cells of 512 x 256).
        public static void DrawAtlas(Paint g, Func<float> r)
        {
            void cell(int i, Action fn) { g.save(); g.translate((i % 2) * 512, (i / 2) * 256); g.beginPath(); g.rect(0, 0, 512, 256); g.clip(); fn(); g.restore(); }
            void bars(float x, float y, float w, string c) { g.fillStyle = c; g.fillRect(x, y, w * (0.4f + r() * 0.6f), 6); }
            cell(0, () =>
            { // spreadsheet
                g.fillStyle = "#f7f8fa"; g.fillRect(0, 0, 512, 256); g.fillStyle = "#1f7a6a"; g.fillRect(0, 0, 512, 26);
                g.fillStyle = "#fff"; Font(g, 15); g.fillText("주문 현황.xlsx", 10, 18);
                g.strokeStyle = "#d5d9de"; for (int y = 44; y < 256; y += 18) { g.beginPath(); g.moveTo(0, y); g.lineTo(512, y); g.stroke(); }
                foreach (var x in new[] { 70, 200, 300, 400 }) { g.beginPath(); g.moveTo(x, 26); g.lineTo(x, 256); g.stroke(); }
                for (int y = 32; y < 250; y += 18) foreach (var x in new[] { 6, 76, 206, 306, 406 }) { if (r() < 0.1f) { g.fillStyle = r() < 0.5f ? "#ffe38a" : "#ffc9c9"; g.fillRect(x - 4, y - 5, 92, 17); } bars(x, y, 56, "#6b7480"); }
            });
            cell(1, () =>
            { // ERP order list
                g.fillStyle = "#14202a"; g.fillRect(0, 0, 512, 256); g.fillStyle = "#0e171e"; g.fillRect(0, 0, 90, 256);
                for (int y = 20; y < 240; y += 26) bars(12, y, 60, "#3a4a55");
                g.fillStyle = "#e8eef0"; Font(g, 15); g.fillText("배송 관리 · 주문 목록", 104, 22);
                for (int y = 40; y < 250; y += 24)
                {
                    g.fillStyle = "#1b2a33"; g.fillRect(100, y, 404, 20); bars(108, y + 7, 160, "#8fa3ab");
                    float k = r(); g.fillStyle = k < 0.6f ? "#3cc6b5" : k < 0.85f ? "#f2b33d" : "#e5484d"; g.fillRect(430, y + 3, 64, 14);
                    g.fillStyle = "#0b1216"; Font(g, 11); g.fillText(k < 0.6f ? "배송 완료" : k < 0.85f ? "지연" : "취소", 440, y + 14);
                }
            });
            cell(2, () =>
            { // route map
                g.fillStyle = "#0f1a20"; g.fillRect(0, 0, 512, 256);
                g.strokeStyle = "#2b3d47"; g.lineWidth = 3;
                for (int i = 0; i < 14; i++) { g.beginPath(); float x = r() * 512, y = r() * 256; g.moveTo(x, y); for (int k = 0; k < 4; k++) { x += (r() - 0.5f) * 200; y += (r() - 0.5f) * 120; g.lineTo(x, y); } g.stroke(); }
                g.strokeStyle = "#3cc6b5"; g.lineWidth = 5; g.beginPath(); g.moveTo(40, 220); g.lineTo(160, 150); g.lineTo(300, 170); g.lineTo(460, 40); g.stroke();
                foreach (var (x, y) in new[] { (40, 220), (160, 150), (300, 170), (460, 40) }) { g.fillStyle = "#f2b33d"; g.beginPath(); g.arc(x, y, 7, 0, 7); g.fill(); }
                g.fillStyle = "rgba(20,35,43,0.9)"; g.fillRect(330, 180, 170, 66); g.fillStyle = "#e8eef0"; Font(g, 14); g.fillText("2호차 · 도착 예정", 342, 204); g.fillText("18:10 (+25분)", 342, 230);
            });
            cell(3, () =>
            { // messenger
                g.fillStyle = "#eef1f4"; g.fillRect(0, 0, 512, 256); g.fillStyle = "#ffffff"; g.fillRect(0, 0, 150, 256);
                g.fillStyle = "#1f4f8c"; g.fillRect(0, 0, 512, 24); g.fillStyle = "#fff"; Font(g, 14); g.fillText("사내 메신저", 10, 17);
                var av = new[] { "#f2b33d", "#3cc6b5", "#c0436e", "#6b5b95" };
                for (int y = 36; y < 250; y += 36) { g.fillStyle = av[(y / 36) % 4]; g.beginPath(); g.arc(20, y + 10, 10, 0, 7); g.fill(); bars(38, y + 4, 100, "#8a929b"); }
                for (int y = 40, k = 0; y < 240; y += 40, k++) { bool me = k % 2 == 1; g.fillStyle = me ? "#ffe38a" : "#ffffff"; float w = 120 + r() * 160; g.fillRect(me ? 500 - w : 162, y, w, 26); bars(me ? 508 - w : 170, y + 10, w - 20, "#6b7480"); }
            });
        }

        public static void DrawBoard(Paint g, float W, float Hh)
        {
            g.fillStyle = "#fbfbf8"; g.fillRect(0, 0, W, Hh);
            g.fillStyle = "rgba(80,90,120,0.05)"; for (int i = 0; i < 14; i++) g.fillRect(40 + i * 70, 60 + (i % 3) * 120, 160, 30);
            g.fillStyle = "#1f4f8c"; Font(g, 38, 800); g.fillText("이번 주 배송 일정", 36, 54);
            var cols = new[] { "요일", "수도권", "서해안", "중부", "비고" }; var cx = new[] { 36, 156, 356, 556, 736 }; var rows = new[] { "월", "화", "수", "목", "금" };
            g.strokeStyle = "#2b2b2b"; g.lineWidth = 2;
            for (int i = 0; i <= 6; i++) { g.beginPath(); g.moveTo(30, 80 + i * 56); g.lineTo(990, 80 + i * 56); g.stroke(); }
            foreach (var x in cx.Skip(1).Select(v => v - 12).Concat(new[] { 990 })) { g.beginPath(); g.moveTo(x, 80); g.lineTo(x, 416); g.stroke(); }
            g.fillStyle = "#2b2b2b"; Font(g, 26, 700); for (int i = 0; i < cols.Length; i++) g.fillText(cols[i], cx[i], 118);
            var cellTxt = new[] { new[] { "06:30 ×4", "07:00 ×3", "06:40 ×2", "" }, new[] { "06:30 ×4", "07:00 ×3", "06:40 ×2", "반품 회수 많음" }, new[] { "06:30 ×4", "보류?", "06:40 ×2", "B물류 공지 확인" }, new[] { "06:30 ×4", "", "06:40 ×2", "" }, new[] { "06:30 ×3", "", "06:40 ×2", "주말 근무 조정" } };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 174 + i * 56; g.fillStyle = "#2b2b2b"; Font(g, 26, 700); g.fillText(rows[i], cx[0], y);
                for (int k = 0; k < cellTxt[i].Length; k++) { var t = cellTxt[i][k]; g.fillStyle = t == "보류?" ? "#c4161c" : "#2b2b2b"; Font(g, 24, 600); g.fillText(t, cx[k + 1], y); }
            }
            g.strokeStyle = "#c4161c"; g.lineWidth = 4; g.beginPath(); g.ellipse(410, 278, 90, 26, -0.05f, 0, 7); g.stroke();
            g.fillStyle = "#c4161c"; Font(g, 24, 800); g.fillText("서해안: 내일부터 지연 공지 (B물류 메일)", 36, 456);
            g.fillStyle = "#1f4f8c"; g.fillText("취소 급증 — 원인 확인 중 · 고객에게 추측해서 말하지 않기", 36, 494);
            foreach (var (x, y, c) in new[] { (980, 30, "#e5484d"), (940, 30, "#3cc6b5"), (900, 30, "#f2b33d") }) { g.fillStyle = c; g.beginPath(); g.arc(x, y, 12, 0, 7); g.fill(); }
        }

        public static void DrawClock(Paint g, float W)
        {
            float c = W / 2; g.fillStyle = "#ffffff"; g.beginPath(); g.arc(c, c, c - 4, 0, 7); g.fill();
            g.strokeStyle = "#2b2b2b"; g.lineWidth = 8; g.stroke();
            for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2; g.lineWidth = i % 3 != 0 ? 3 : 6; g.beginPath(); g.moveTo(c + Mathf.Sin(a) * c * 0.78f, c - Mathf.Cos(a) * c * 0.78f); g.lineTo(c + Mathf.Sin(a) * c * 0.9f, c - Mathf.Cos(a) * c * 0.9f); g.stroke(); }
            void hand(float a, float len, float w) { g.lineWidth = w; g.lineCap = "round"; g.beginPath(); g.moveTo(c, c); g.lineTo(c + Mathf.Sin(a) * len, c - Mathf.Cos(a) * len); g.stroke(); }
            hand(5.5f / 12 * Mathf.PI * 2, c * 0.5f, 9); hand(Mathf.PI, c * 0.75f, 6); // 17:30
        }

        // Plane whose UVs point at one cell of a 2 x 2 atlas (canvas row 0 = top = v 0.5..1).
        public static GeoData CellPlane(float w, float h, int cell)
        {
            var g = Geo.Plane(w, h); int col = cell % 2, row = cell / 2;
            for (int i = 0; i < g.uv.Count; i++) g.uv[i] = new Vector2((col + g.uv[i].x) / 2, (1 - row + g.uv[i].y) / 2);
            return g;
        }
    }
}
