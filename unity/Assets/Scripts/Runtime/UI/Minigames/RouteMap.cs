// Ports ui/routemap.js: the 2D map of 연무동 drawn from Logic/TownMap: roads, crossings, buildings, the park, Korean
// labels, shelter entrances with status icons, closures only when the player knows them, and up to 3 route options
// as lines told apart by colour AND dash. Open resolves { choice: index | null }; mode 'view' just shows the map (M).
// DrawMap is shared with the HUD minimap (Hud owns that canvas; miniMap's projector rotates by the yaw).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    // drawMap options.
    public sealed class MapDraw
    {
        public int W, H;
        public List<string> closures = new List<string>();
        public Dictionary<string, string> gates = new Dictionary<string, string>();
        public List<RouteOption> options = new List<RouteOption>();
        public int sel = -1;
        public XZ? player, dest;
        public float? playerYaw;
        public bool labels = true;
        public float dpr = 1;
    }

    public static class RouteMap
    {
        static readonly (string color, float[] dash, string name)[] ROUTE_STYLE = { ("#f2b33d", new float[0], "실선"), ("#3cc6b5", new float[] { 12, 7 }, "긴 점선"), ("#c49bff", new float[] { 3, 6 }, "짧은 점선") };
        static readonly Dictionary<string, string> KIND = new Dictionary<string, string> { ["shortest"] = "가장 짧은 길", ["detour"] = "돌아가는 길", ["stepFree"] = "계단 없는 길" };
        static readonly Dictionary<string, string> BCOL = new Dictionary<string, string> { ["apartment"] = "#58656e", ["shop"] = "#6e5d4c", ["mart"] = "#715e45", ["office"] = "#4d5d75", ["civic"] = "#4f6d68", ["villa"] = "#5f5852", ["warehouse"] = "#4c5156" };
        static readonly Dictionary<string, string> AREA_COL = new Dictionary<string, string> { ["park"] = "#24462f", ["parking"] = "#283034", ["plaza"] = "#2c3336", ["complex"] = "#1f2b26" };
        static readonly Dictionary<string, string[]> GATE = new Dictionary<string, string[]> { ["open"] = new[] { "✓", "열림", "#3cc6b5" }, ["closed"] = new[] { "✕", "닫힘", "#e5484d" }, ["unknown"] = new[] { "?", "확인 안 함", "#8b9599" } };
        const string FONT = "'Pretendard','Noto Sans KR','Malgun Gothic',sans-serif";
        static string GateState(string v) => v == "open" || v == "true" ? "open" : v == "closed" || v == "false" ? "closed" : "unknown";
        static string Get(Dictionary<string, string> d, string k) => d != null && k != null && d.TryGetValue(k, out var v) ? v : null;

        // World (x east, z south) -> canvas pixels. rot turns the map (minimap: the way you face is up).
        public static Func<double, double, Vector2> Projector(double cx, double cz, float s, float rot, int W, int H)
        {
            float c = Mathf.Cos(rot), n = Mathf.Sin(rot);
            return (x, z) => { float dx = (float)(x - cx) * s, dz = (float)(z - cz) * s; return new Vector2(W / 2f + dx * c - dz * n, H / 2f + dx * n + dz * c); };
        }
        static double[][] RectPts(MapRect r, double pad = 0) => RectPts(r.x0, r.x1, r.z0, r.z1, pad);
        static double[][] RectPts(double x0, double x1, double z0, double z1, double pad = 0) =>
            new[] { new[] { x0 - pad, z0 - pad }, new[] { x1 + pad, z0 - pad }, new[] { x1 + pad, z1 + pad }, new[] { x0 - pad, z1 + pad } };
        static void Poly(Paint g, Func<double, double, Vector2> P, double[][] pts)
        {
            g.beginPath();
            for (int i = 0; i < pts.Length; i++) { var p = P(pts[i][0], pts[i][1]); if (i > 0) g.lineTo(p.x, p.y); else g.moveTo(p.x, p.y); }
            g.closePath();
        }
        static void Label(Paint g, string text, float x, float y, float px, string color = "#e9eef0", int weight = 600)
        {
            g.font = $"{weight} {Mg.Num(px)}px {FONT}"; g.textAlign = "center"; g.textBaseline = "middle";
            g.lineWidth = px * 0.35f; g.strokeStyle = "rgba(10,14,16,.85)"; g.strokeText(text, x, y); g.fillStyle = color; g.fillText(text, x, y);
        }

        // project(x, z) -> canvas pixel; s = pixels per metre.
        public static void DrawMap(Paint g, Func<double, double, Vector2> project, float s, MapDraw o)
        {
            var P = project; o ??= new MapDraw();
            int W = o.W, H = o.H; float dpr = o.dpr; bool labels = o.labels;
            var closures = o.closures ?? new List<string>(); var gates = o.gates ?? new Dictionary<string, string>(); var options = o.options ?? new List<RouteOption>(); int sel = o.sel;
            float px(float n) => n * dpr;
            g.setLineDash(); g.fillStyle = "#172023"; g.fillRect(0, 0, W, H);
            foreach (var a in TownMap.AREAS)
            {
                g.fillStyle = AREA_COL.TryGetValue(a.kind ?? "", out var ac) ? ac : "#222b2e"; Poly(g, P, RectPts(a)); g.fill();
                if (a.kind == "parking") { g.strokeStyle = "rgba(230,230,220,.18)"; g.lineWidth = px(1); for (double x = a.x0 + 2.5; x < a.x1; x += 2.5) { Vector2 p = P(x, a.z0 + 1), q = P(x, a.z0 + 5); g.beginPath(); g.moveTo(p.x, p.y); g.lineTo(q.x, q.y); g.stroke(); } }
                if (a.kind == "park") for (int i = 0; i < 46; i++) // tree crowns, deterministic
                {
                    double tx = a.x0 + 3 + ((i * 37) % 97) / 97.0 * (a.x1 - a.x0 - 6), tz = a.z0 + 3 + ((i * 61) % 89) / 89.0 * (a.z1 - a.z0 - 6);
                    var c = P(tx, tz); g.fillStyle = i % 5 != 0 ? "rgba(70,120,72,.75)" : "rgba(176,130,48,.7)"; g.beginPath(); g.arc(c.x, c.y, Mathf.Max(px(1.5f), 1.6f * s), 0, Mathf.PI * 2); g.fill();
                }
            }
            g.fillStyle = "#4b5251"; foreach (var r in TownMap.ROADS) { Poly(g, P, RectPts(r, TownMap.SIDEWALK)); g.fill(); }
            g.fillStyle = "#30383c"; foreach (var r in TownMap.ROADS) { Poly(g, P, RectPts(r)); g.fill(); }
            foreach (var r in TownMap.ROADS)
            {
                bool ew = r.x1 - r.x0 > r.z1 - r.z0; double mid = ew ? (r.z0 + r.z1) / 2 : (r.x0 + r.x1) / 2;
                void Line(double off, string color, float[] dash)
                {
                    g.strokeStyle = color; g.setLineDash(dash.Select(d => d * s).ToArray()); g.lineWidth = Mathf.Max(px(0.8f), 0.22f * s); g.beginPath();
                    var a = ew ? P(r.x0, mid + off) : P(mid + off, r.z0); var b = ew ? P(r.x1, mid + off) : P(mid + off, r.z1);
                    g.moveTo(a.x, a.y); g.lineTo(b.x, b.y); g.stroke();
                }
                if (r.id == "yeonmu") { Line(-0.3, "#d6ae38", new float[0]); Line(0.3, "#d6ae38", new float[0]); } else Line(0, "rgba(230,230,225,.5)", new float[] { 3, 3 });
            }
            g.setLineDash(); g.fillStyle = "rgba(236,236,230,.78)";
            foreach (var c in TownMap.CROSSWALKS)
            {
                var r = TownMap.ROADS.FirstOrDefault(x => x.id == c.road); if (r == null) continue;
                bool ew = r.x1 - r.x0 > r.z1 - r.z0; double k0 = ew ? r.z0 : r.x0, k1 = ew ? r.z1 : r.x1;
                for (double k = k0 + 0.4; k < k1 - 0.4; k += 1.2) { Poly(g, P, ew ? RectPts(c.at - 2, c.at + 2, k, k + 0.6) : RectPts(k, k + 0.6, c.at - 2, c.at + 2)); g.fill(); }
            }
            foreach (var b in TownMap.BUILDINGS)
            {
                g.fillStyle = "rgba(0,0,0,.35)"; Poly(g, P, RectPts(b.x0 + 1.2, b.x1 + 1.2, b.z0 + 1.2, b.z1 + 1.2)); g.fill();
                g.fillStyle = BCOL.TryGetValue(b.kind ?? "", out var bc) ? bc : "#5a5a5a"; Poly(g, P, RectPts(b)); g.fill();
                g.strokeStyle = b.id == "culture" ? "#3cc6b5" : "rgba(0,0,0,.45)"; g.lineWidth = px(b.id == "culture" ? 2.5f : 1); g.stroke();
            }
            if (labels)
            {
                foreach (var r in TownMap.ROADS) { var p = P(r.id == "yeonmu" ? 30 : (r.x0 + r.x1) / 2, r.id == "yeonmu" ? 0 : r.id == "munhwa" || r.id == "hanbit" ? -86 : (r.z0 + r.z1) / 2); Label(g, r.name, p.x, p.y, px(10), "#aeb8bb", 500); }
                foreach (var b in TownMap.BUILDINGS) if (!string.IsNullOrEmpty(b.name)) { var p = P((b.x0 + b.x1) / 2, (b.z0 + b.z1) / 2); Label(g, b.name.Replace("한빛아파트 ", ""), p.x, p.y - (b.id == "culture" ? px(8) : 0), px(11)); }
                { var p = P(-90, 31); Label(g, "대피소(지하)", p.x, p.y + px(8), px(11), "#7fe0d3", 700); }
                foreach (var a in TownMap.AREAS) if (a.kind == "park") { var p = P((a.x0 + a.x1) / 2, (a.z0 + a.z1) / 2); Label(g, a.name, p.x, p.y, px(11), "#bfe3c2"); }
            }
            foreach (var id in closures)
            {
                if (id == null || !TownMap.CLOSURES.TryGetValue(id, out var cl)) continue;
                for (int i = 0; i < cl.barricades.Count; i++)
                {
                    var r = cl.barricades[i];
                    Poly(g, P, RectPts(r)); g.fillStyle = "#e5484d"; g.fill(); g.setLineDash(px(4), px(4)); g.strokeStyle = "#fff"; g.lineWidth = px(2); g.stroke(); g.setLineDash();
                    var p = P((r.x0 + r.x1) / 2, (r.z0 + r.z1) / 2);
                    g.fillStyle = "#e5484d"; g.beginPath(); g.arc(p.x, p.y, px(9), 0, Mathf.PI * 2); g.fill(); g.fillStyle = "#fff"; g.fillRect(p.x - px(5), p.y - px(1.6f), px(10), px(3.2f));
                    if (labels && i == 0) Label(g, $"통제 · {cl.label}", p.x, p.y - px(16), px(10), "#ffb3b5", 700);
                }
            }
            var order = Enumerable.Range(0, options.Count).OrderBy(i => i == sel ? 1 : 0).ToList();
            foreach (var i in order)
            {
                var opt = options[i]; var (color, dash, _) = i < ROUTE_STYLE.Length ? ROUTE_STYLE[i] : ROUTE_STYLE[0];
                var pts = (opt?.nodes ?? new List<string>()).Where(n => n != null && TownMap.NODES.ContainsKey(n)).Select(n => P(TownMap.NODES[n].x, TownMap.NODES[n].z)).ToList();
                if (pts.Count < 2) continue;
                void Path() { g.beginPath(); g.moveTo(pts[0].x, pts[0].y); foreach (var p in pts.Skip(1)) g.lineTo(p.x, p.y); }
                g.lineJoin = g.lineCap = "round";
                g.setLineDash(); g.strokeStyle = "rgba(8,12,14,.8)"; g.lineWidth = px(i == sel ? 11 : 8); Path(); g.stroke();
                g.setLineDash(dash.Select(px).ToArray()); g.strokeStyle = color; g.globalAlpha = sel < 0 || i == sel ? 1 : 0.55f; g.lineWidth = px(i == sel ? 6 : 4); Path(); g.stroke(); g.globalAlpha = 1; g.setLineDash();
                if (labels && options.Count > 1) { var p = pts[pts.Count / 2]; g.fillStyle = color; g.beginPath(); g.arc(p.x, p.y, px(10), 0, Mathf.PI * 2); g.fill(); Label(g, (i + 1).ToString(), p.x, p.y, px(12), "#10161a", 800); }
            }
            foreach (var kv in TownMap.SHELTER_ENTRANCES)
            {
                if (kv.Value.place == null || !TownMap.PLACES.TryGetValue(kv.Value.place, out var pl)) continue;
                var p = P(pl.x, pl.z); var st = GateState(Get(gates, kv.Key)); var gi = GATE[st]; float r = px(8);
                g.fillStyle = gi[2]; g.strokeStyle = "#0c1214"; g.lineWidth = px(2);
                if (st == "closed") { g.fillRect(p.x - r, p.y - r, r * 2, r * 2); g.strokeRect(p.x - r, p.y - r, r * 2, r * 2); } else { g.beginPath(); g.arc(p.x, p.y, r, 0, Mathf.PI * 2); g.fill(); g.stroke(); }
                Label(g, gi[0], p.x, p.y + px(0.5f), px(11), "#0c1214", 900);
                if (labels) Label(g, kv.Value.@short ?? "동쪽 출입구", p.x, p.y + px(17), px(10), "#d8f3ef");
            }
            if (o.dest is XZ dest) { var p = P(dest.x, dest.z); g.fillStyle = "#3cc6b5"; g.beginPath(); g.moveTo(p.x, p.y - px(9)); g.lineTo(p.x + px(9), p.y); g.lineTo(p.x, p.y + px(9)); g.lineTo(p.x - px(9), p.y); g.closePath(); g.fill(); }
            if (o.player is XZ pa)
            {
                var p = P(pa.x, pa.z); float x = p.x, y = p.y;
                g.fillStyle = "rgba(242,179,61,.25)"; g.beginPath(); g.arc(x, y, px(16), 0, Mathf.PI * 2); g.fill();
                g.fillStyle = "#f2b33d"; g.strokeStyle = "#10161a"; g.lineWidth = px(2);
                if (o.playerYaw != null) { g.beginPath(); g.moveTo(x, y - px(10)); g.lineTo(x + px(7), y + px(7)); g.lineTo(x, y + px(3)); g.lineTo(x - px(7), y + px(7)); g.closePath(); g.fill(); g.stroke(); }
                else { g.beginPath(); g.arc(x, y, px(7), 0, Mathf.PI * 2); g.fill(); g.stroke(); }
                if (labels) Label(g, "나", x, y - px(20), px(12), "#ffd58a", 800);
            }
            if (labels) // north arrow and a 50 m scale bar
            {
                float nx = W - px(28), ny = px(30);
                g.fillStyle = "#e9eef0"; g.beginPath(); g.moveTo(nx, ny - px(14)); g.lineTo(nx + px(7), ny + px(6)); g.lineTo(nx - px(7), ny + px(6)); g.closePath(); g.fill();
                Label(g, "북", nx, ny + px(18), px(11));
                float sx = px(18), sy = H - px(18);
                g.fillStyle = "#e9eef0"; g.fillRect(sx, sy, 50 * s, px(3)); Label(g, "50m", sx + 25 * s, sy - px(10), px(10));
            }
        }

        static XZ? Node(string id) => id != null && TownMap.NODES.TryGetValue(id, out var n) ? new XZ((float)n.x, (float)n.z) : (XZ?)null;

        // Resolves { choice: index | null }.
        public static Task<RouteMapResult> Open(RouteMapOptions o)
        {
            MinigameStyle.Register();
            o ??= new RouteMapOptions();
            string mode = o.mode ?? "guided";
            var options = o.options ?? new List<RouteOption>();
            var gates = o.gates ?? new Dictionary<string, string>();
            var shown = (o.closures ?? new List<string>()).Concat(o.known?.closures ?? new List<string>()).Distinct().Where(id => id != null && TownMap.CLOSURES.ContainsKey(id)).ToList();
            bool view = mode == "view" || options.Count == 0;
            int sel = view ? -1 : 0;
            ModalHandle<RouteMapResult> m = null;
            // ponytail: a fixed 960 px canvas (JS sizes it to the panel width x devicePixelRatio); the image scales to fit.
            const float CSS_W = 640, DPR = 1.5f;
            var B = TownMap.BOUNDS;
            int W = (int)(CSS_W * DPR), H = (int)Math.Round(W * (B.z1 - B.z0) / (B.x1 - B.x0));
            var cv = new MgCanvas(W, H, "mg-map-canvas");
            var swatches = new List<MgCanvas>();
            void Done() { cv.Dispose(); swatches.ForEach(x => x.Dispose()); m.Close(new RouteMapResult { choice = view ? (int?)null : sel }); }
            m = MiniDom.Modal<RouteMapResult>("mg-map", view ? "연무동 지도" : "대피 경로 고르기", null, e => { if (Mg.Key(e) == "Escape") { Done(); return true; } return false; });
            cv.el.tooltip = "연무동 지도. 같은 내용이 옆 목록에 글로 있습니다.";
            var wrap = Mg.El("div", "mg-map-wrap", cv.el);

            void Draw()
            {
                if (cv.tex == null) return;
                float s = (float)Math.Min(W / (B.x1 - B.x0), H / (B.z1 - B.z0));
                var P = Projector((B.x0 + B.x1) / 2, (B.z0 + B.z1) / 2, s, 0, W, H);
                DrawMap(cv.g, P, s, new MapDraw { W = W, H = H, closures = shown, gates = gates, options = options, sel = sel, player = o.playerAt ?? Node(o.from ?? "home"), dest = Node(o.to ?? "shelter"), dpr = DPR });
                cv.Upload();
            }

            var radios = new List<RadioButton>(); var routeRows = new List<VisualElement>();
            VisualElement routes = null;
            if (!view)
            {
                var box = Mg.Cls(new GroupBox(), "mg-routes");
                box.Add(Mg.El("legend", null, "갈 길 고르기"));
                foreach (var (opt, i) in options.Take(3).Select((x, i) => (x, i)))
                {
                    var (color, dash, dashName) = ROUTE_STYLE[i];
                    var r = new RadioButton { value = i == 0 };
                    radios.Add(r);
                    var sw = new MgCanvas(80, 16, "mg-swatch"); swatches.Add(sw);
                    sw.g.strokeStyle = color; sw.g.lineWidth = 8; sw.g.lineCap = "round"; sw.g.setLineDash(dash.Select(d => d * 2).ToArray());
                    sw.g.beginPath(); sw.g.moveTo(4, 8); sw.g.lineTo(76, 8); sw.g.stroke(); sw.Upload();
                    sw.el.style.width = 34; sw.el.style.height = 7;
                    var ent = opt.entrance != null && TownMap.SHELTER_ENTRANCES.TryGetValue(opt.entrance, out var en) ? en : null;
                    var title = Mg.El("b", null, sw.el, $"경로 {i + 1} · {(opt.kind != null && KIND.TryGetValue(opt.kind, out var kn) ? kn : "다른 길")}", Mg.Text("mg-spec", $" ({dashName})"));
                    title.style.flexDirection = FlexDirection.Row; title.style.alignItems = Align.Center; title.style.flexWrap = Wrap.Wrap;
                    var body = Mg.El("span", "mg-route-body", title, Mg.El("small", null, Routes.Explain(opt)),
                        ent != null ? Mg.El("span", "mg-badges", Mg.El("span", "mg-badge", $"{ent.@short ?? ent.label}로 들어감")) : null);
                    var row = Mg.El("label", "mg-route", r, body);
                    routeRows.Add(row);
                    int idx = i;
                    r.RegisterValueChangedCallback(ev => { if (ev.newValue) { sel = idx; for (int k = 0; k < routeRows.Count; k++) MinigameStyle.RouteChecked(routeRows[k], k == sel); Draw(); } });
                    row.RegisterCallback<ClickEvent>(_ => r.value = true);
                    MinigameStyle.RouteChecked(row, i == 0);
                    box.Add(row);
                }
                routes = box;
            }

            var gateList = Mg.El("ul", "mg-gates", TownMap.SHELTER_ENTRANCES.Select(kv =>
            {
                var st = GateState(Get(gates, kv.Key)); var gi = GATE[st];
                var ico = Mg.Text("mg-gate-ico", gi[0]); MinigameStyle.GateIcon(ico, st);
                var li = Mg.El("li", $"is-{st}", ico, $"{kv.Value.label} · {gi[1]}");
                li.style.flexDirection = FlexDirection.Row; li.style.alignItems = Align.Center; li.style.marginBottom = 4;
                return li;
            }).ToList());
            var closureList = Mg.El("ul", "mg-closures", shown.Count > 0 ? (object)shown.Select(id => Mg.El("li", null, "⊘ " + TownMap.CLOSURES[id].label)).ToList() : Mg.El("li", "mg-closures-why", "알고 있는 도로 통제가 없다. (현장은 다를 수 있다)"));
            var help = view ? null : MiniDom.LadderHelp(mode, () => new List<VisualElement> { Mg.El("p", null, "선을 비교해 보세요. 걸리는 시간, 계단이나 가파른 길, 그리고 지나는 출입구."), Mg.El("p", "mg-why", "첫 번째 길이 막히면 어느 길로 돌아갈지도 함께 봐 두세요.") });
            var go = Mg.Btn("mg-btn mg-primary", Done, view ? "지도 닫기" : "이 길로 정한다");
            var legend = Mg.El("p", "mg-legend", new[] { "● 나", "◆ 대피소", "✓ 열림", "✕ 닫힘", "? 확인 안 함", "⊘ 통제" }.Select(t => { var l = Mg.Text(null, t); l.style.marginRight = 12; return l; }).ToList());

            MiniDom.Put(m.root,
                MiniDom.Header(view ? "지도" : "경로 계획", view ? "연무동 지도" : "집에서 대피소까지", mode, "mg-map-title"),
                Mg.El("div", "mg-map-grid",
                    wrap,
                    Mg.El("div", "mg-map-side", help, routes,
                        Mg.El("h3", "mg-sub", "대피소 출입구"), gateList,
                        Mg.El("h3", "mg-sub", "알고 있는 통제"), closureList,
                        legend)),
                Mg.El("footer", "mg-foot", Mg.El("p", "mg-keys", view ? "Esc 또는 M으로 닫기" : "↑/↓ 경로 바꾸기 · Enter 정하기 · Esc 닫기"), Mg.El("div", "mg-actions", go)));
            m.root.RegisterCallback<KeyDownEvent>(e =>
            {
                var k = Mg.Key(e);
                var onRoute = radios.FindIndex(r => r == e.target || (e.target as VisualElement)?.GetFirstOfType<RadioButton>() == r);
                if (view && k == "m") { e.StopPropagation(); Done(); }
                else if (!view && k == "Enter" && onRoute >= 0) { e.StopPropagation(); Done(); }
                else if (!view && onRoute >= 0 && (k == "ArrowUp" || k == "ArrowDown"))   // native radio-group arrows
                {
                    e.StopPropagation();
                    int n = (onRoute + (k == "ArrowDown" ? 1 : radios.Count - 1)) % radios.Count;
                    radios[n].value = true; radios[n].Focus();
                }
            }, TrickleDown.TrickleDown);
            Draw();
            if (radios.Count > 0) radios[0].Focus();   // data-autofocus
            return m.Done;
        }
    }
}
