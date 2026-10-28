// Ports ui/grab3d.js: taking things by hand. The camera steps up to the shelf (or the stock at home), the products
// stand there in 3D, and a click reaches out and the item flies into the basket (or the backpack); a right click
// puts one back. Same contracts as ui/shop.js (-> { cart, total } | null) and ui/bag.js (-> { bag }).
// Keys: ←/→ choose, Enter/Space take, Backspace put back, Esc leave. All positions JS space (J at the boundary).
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
    public static class Grab3d
    {
        // [shape, colour, w, h, d] in metres; cylinders use w as the diameter.
        static readonly Dictionary<string, (string shape, int col, float w, float h, float d)> LOOK = new Dictionary<string, (string, int, float, float, float)>
        {
            ["water_2l"] = ("cyl", 0x8cc8e6, 0.11f, 0.32f, 0.08f), ["water_500"] = ("cyl", 0xa9d8ef, 0.07f, 0.21f, 0.08f), ["rice_instant"] = ("box", 0xf2ede3, 0.15f, 0.05f, 0.12f),
            ["canned"] = ("cyl", 0xc8a13c, 0.08f, 0.1f, 0.08f), ["ramen"] = ("box", 0xcf3b2c, 0.16f, 0.05f, 0.12f), ["bars"] = ("box", 0xe2ad38, 0.14f, 0.04f, 0.07f),
            ["radio"] = ("box", 0x3b4046, 0.2f, 0.12f, 0.07f), ["batteries_aa"] = ("box", 0xd6a21e, 0.08f, 0.1f, 0.02f), ["flashlight"] = ("cyl", 0x2e3338, 0.04f, 0.18f, 0.08f),
            ["powerbank"] = ("box", 0xe7ebee, 0.07f, 0.13f, 0.02f), ["candles"] = ("box", 0xf3eee2, 0.1f, 0.14f, 0.05f), ["first_aid"] = ("box", 0xf2f4f3, 0.2f, 0.13f, 0.08f),
            ["masks"] = ("box", 0xdbe7f1, 0.18f, 0.08f, 0.1f), ["wipes"] = ("box", 0x58b29e, 0.18f, 0.06f, 0.1f), ["tissue"] = ("cyl", 0xf6f6f2, 0.11f, 0.1f, 0.08f),
            ["trash_bags"] = ("cyl", 0x3f6c4e, 0.07f, 0.1f, 0.08f), ["tape"] = ("cyl", 0x8a8e93, 0.1f, 0.05f, 0.08f), ["raincoat"] = ("box", 0xe5d04a, 0.16f, 0.04f, 0.2f),
            ["blanket"] = ("box", 0xc3c9d0, 0.14f, 0.03f, 0.18f), ["water_jug"] = ("box", 0x4a8fd4, 0.2f, 0.25f, 0.12f), ["gas_stove"] = ("box", 0xc3352d, 0.34f, 0.1f, 0.27f),
            ["butane"] = ("cyl", 0xebe5db, 0.07f, 0.2f, 0.08f), ["whistle"] = ("cyl", 0xef8a25, 0.03f, 0.07f, 0.08f), ["clothes"] = ("box", 0x7b8aa0, 0.25f, 0.08f, 0.2f),
            ["hygiene_kit"] = ("box", 0x9fc7d8, 0.18f, 0.08f, 0.1f), ["cash"] = ("box", 0xd9c79a, 0.15f, 0.01f, 0.07f), ["contact_card"] = ("box", 0xf5f0e0, 0.09f, 0.005f, 0.06f),
            ["doc_copies"] = ("box", 0xf0f0ea, 0.21f, 0.01f, 0.3f), ["id_card"] = ("box", 0xb9d4ec, 0.085f, 0.005f, 0.055f), ["meds"] = ("box", 0xffffff, 0.08f, 0.05f, 0.04f),
        };
        static readonly Dictionary<string, Mesh> geoCache = new Dictionary<string, Mesh>();
        static Mesh GeoOf(string id)
        {
            var (shape, _, w, h, d) = id != null && LOOK.TryGetValue(id, out var l) ? l : ("box", 0, 0.12f, 0.15f, 0.08f);
            var k = $"{shape}{w}{h}{d}";
            if (!geoCache.TryGetValue(k, out var m) || m == null)
                geoCache[k] = m = (shape == "cyl" ? Geo.Cylinder(w / 2, w / 2, h, 14).Translate(0, h / 2, 0) : Geo.Box(w, h, d).Translate(0, h / 2, 0)).ToMesh(k);
            return m;
        }
        static readonly System.Random rnd = new System.Random();
        static float Rand() => (float)rnd.NextDouble();

        // A small price tag / name card as a sprite.
        static MgSprite TagSprite(string title, string sub)
        {
            var g = new Paint(256, 96);
            g.fillStyle = "rgba(20,22,26,.86)"; g.beginPath(); g.roundRect(2, 2, 252, 92, 14); g.fill();
            g.fillStyle = "#fff"; g.font = "bold 30px sans-serif"; g.textAlign = "center"; g.fillText(title.Length > 9 ? $"{title.Substring(0, 9)}…" : title, 128, 42);
            g.fillStyle = "#ffc04d"; g.font = "bold 26px sans-serif"; g.fillText(sub, 128, 80);
            var s = new MgSprite(g, renderOrder: 20);
            s.Scale(0.26f, 0.0975f);
            return s;
        }

        // An open basket or backpack: four walls and a floor.
        static GameObject Container(string kind, List<UnityEngine.Object> owned)
        {
            var g = J.Group("container");
            var m = Gyeongbo.Engine.Mat.Lit(new MatSpec { color = J.Hex(kind == "bag" ? 0x2c3d5a : 0xc8302a), roughness = 0.8f });
            owned.Add(m);
            const float W = 0.42f, D = 0.3f, H = 0.22f, T = 0.012f;
            foreach (var (w, h, d, x, y, z) in new[] { (W, T, D, 0f, 0f, 0f), (W, H, T, 0f, H / 2, -D / 2), (W, H, T, 0f, H / 2, D / 2), (T, H, D, -W / 2, H / 2, 0f), (T, H, D, W / 2, H / 2, 0f) })
            {
                var mesh = Geo.Box(w, h, d).ToMesh("wall"); owned.Add(mesh);
                var b = Geo.Obj(mesh, m, "wall"); J.SetPos(b, x, y, z); g.Add(b);
            }
            return g;
        }

        sealed class GItem { public string id, name, sub; public bool @out; }
        sealed class Count { public int src, dst; }
        sealed class Cfg
        {
            public string kind, title;
            public List<GItem> items;
            public Dictionary<string, Count> st;
            public Func<string, string> canTake;   // null = can take, else the reason
            public Func<List<VisualElement>> foot;
            public List<(string label, bool primary, Action<Action<object>> act)> buttons;
            public Func<object> escape;
            public List<string> info;
        }
        sealed class Slot { public GItem it; public GameObject g; public Material mat; public MgSprite tag; public List<GameObject> copies = new List<GameObject>(); public float glow; }
        sealed class Flight { public GameObject mesh; public Vector3 from, to, mid; public float t, rx, ry; public Action done; }

        static Task<object> GrabRoom(GameEngine engine, Cfg cfg)
        {
            MinigameStyle.Register();
            var tcs = new TaskCompletionSource<object>();
            var camera = engine.Camera; var P = engine.Player; var L = engine.Level;
            var owned = new List<UnityEngine.Object>();
            // Face the shelf: the nearest tall solid within 2.5 m, squared to its side; else straight ahead.
            var f = new Vector3(-Mathf.Sin(P.Yaw), 0, -Mathf.Cos(P.Yaw)); float reach = cfg.kind == "shop" ? 1.3f : 0.95f;
            if (cfg.kind == "shop")
            {
                float best = 2.5f;
                foreach (var s in L?.solids ?? new List<Solid>())
                {
                    if (s == null || s.off || s.y1 < P.Y + 1.2f || s.y0 > P.Y + 0.5f) continue;
                    float cx = Mathf.Min(Mathf.Max(P.X, s.x0), s.x1), cz = Mathf.Min(Mathf.Max(P.Z, s.z0), s.z1), d = Mathf.Sqrt((cx - P.X) * (cx - P.X) + (cz - P.Z) * (cz - P.Z));
                    if (d > 0.05f && d < best) { best = d; f = Mathf.Abs(cx - P.X) > Mathf.Abs(cz - P.Z) ? new Vector3(Mathf.Sign(cx - P.X), 0, 0) : new Vector3(0, 0, Mathf.Sign(cz - P.Z)); reach = d; }
                }
            }
            Vector3 n = -f, right = new Vector3(-f.z, 0, f.x);
            var bas = new Vector3(P.X, P.Y, P.Z) + f * (reach - 0.12f);
            var rows = cfg.kind == "shop" ? new[] { 1.42f, 1.02f, 0.62f } : new[] { 0.82f, 0.5f };
            int count = cfg.items.Count;
            int perRow = Math.Max(1, (int)Math.Ceiling(count / (double)(count > 10 ? 3 : count > 5 ? 2 : 1)));
            var used = rows.Take((int)Math.Ceiling(count / (double)perRow)).ToList();
            float midY = P.Y + (used.Count > 0 ? used.Average() : float.NaN); // middle of the rows in use (NaN with no items, as in JS)
            var camPos = bas + n * (cfg.kind == "shop" ? 1.15f : 1.1f); camPos.y = midY + 0.3f;
            var lookAt = bas; lookAt.y = midY - 0.12f;
            var root = J.Group("grab3d"); engine.Scene.Add(root);
            var fwd = (lookAt - camPos).normalized; var camUp = Vector3.Cross(right, fwd).normalized;
            var box = Container(cfg.kind, owned);
            var boxPos = camPos + fwd * 0.85f + camUp * -0.3f + right * 0.48f; // lower right, clear of the panel
            J.SetPos(box, boxPos); box.transform.localScale = Vector3.one * 0.6f;
            J.LookAt(box.transform, boxPos.x + n.x, boxPos.y + n.y, boxPos.z + n.z); root.Add(box);

            var slots = cfg.items.Select((it, i) =>
            {
                int r = i / perRow, k = i % perRow, cols = Math.Min(perRow, count - r * perRow);
                var pos = bas + right * ((k - (cols - 1) / 2f) * 0.36f); pos.y = P.Y + rows[Math.Min(r, rows.Length - 1)];
                var mat = Gyeongbo.Engine.Mat.Lit(new MatSpec { color = J.Hex(LOOK.TryGetValue(it.id ?? "", out var lk) ? lk.col : 0xb9a27f), roughness = 0.55f, emissive = J.Hex(0xffc04d), emissiveIntensity = 0 });
                var g = J.Group("slot"); J.SetPos(g, pos); J.LookAt(g.transform, pos.x + n.x, pos.y + n.y, pos.z + n.z); root.Add(g);
                var tag = TagSprite(it.name ?? "", it.sub ?? ""); var tp = pos + n * 0.12f; tp.y = pos.y - 0.06f; J.SetPos(tag.go, tp); root.Add(tag.go);
                return new Slot { it = it, g = g, mat = mat, tag = tag };
            }).ToList();
            var inBox = new List<(GameObject m, string id)>();
            void Stock(Slot slot) // show up to four copies of what is still there, one behind the other
            {
                int want = slot.it.@out ? 0 : Math.Min(4, cfg.st[slot.it.id].src);
                while (slot.copies.Count > want) { var last = slot.copies[slot.copies.Count - 1]; slot.copies.RemoveAt(slot.copies.Count - 1); UnityEngine.Object.Destroy(last); }
                while (slot.copies.Count < want) { var m = Geo.Obj(GeoOf(slot.it.id), slot.mat, slot.it.id); J.SetPos(m, 0, 0, -slot.copies.Count * 0.09f); slot.g.Add(m); slot.copies.Add(m); }
                Gyeongbo.Engine.Mat.SetOpacity(slot.tag.mat, want > 0 || slot.it.@out ? 1 : 0.45f);
            }
            slots.ForEach(Stock);

            int sel = 0;
            var flights = new List<Flight>();
            void Fly(GameObject mesh, Vector3 from, Vector3 to, float ry, Action done) { flights.Add(new Flight { mesh = mesh, from = from, to = to, mid = Vector3.Lerp(from, to, 0.5f) + new Vector3(0, 0.25f, 0), ry = ry, done = done }); root.Add(mesh); }

            // ---- DOM: what you hold, the totals, the buttons ----
            var note = Mg.Text("grab3d-toast"); note.pickingMode = PickingMode.Ignore;
            IVisualElementScheduledItem noteT = null;
            void Toast(string t) { note.text = t; UiStyle.Toggle(note, "on", true); noteT?.Pause(); noteT = Mg.Later(note, 1.6f, () => UiStyle.Toggle(note, "on", false)); }
            var held = Mg.El("ul", "grab3d-held"); var sum = Mg.El("div", "grab3d-sum");
            var ui = Mg.El("div", $"grab3d is-{cfg.kind}"); ui.pickingMode = PickingMode.Ignore;
            bool closed = false;
            Action offF = null; Action cleanup = null;
            void Close(object v) { if (closed) return; closed = true; cleanup(); tcs.TrySetResult(v); }
            var btns = cfg.buttons.Select(b => Mg.Btn($"mg-btn {(b.primary ? "mg-primary" : "mg-ghost")}", () => b.act(Close), b.label)).ToList();
            var top = Mg.El("header", "grab3d-top", Mg.El("b", null, cfg.title), cfg.info?.Count > 0 ? Mg.El("ul", null, cfg.info.Select(t => Mg.El("li", null, t)).ToList()) : null);
            top.pickingMode = PickingMode.Ignore;
            var foot = Mg.El("footer", "grab3d-foot", held, sum, Mg.El("div", "mg-actions", btns),
                Mg.El("p", "mg-keys", "클릭: 집기 · 우클릭: 내려놓기 · ←/→ 고르기 · Enter 집기 · Backspace 내려놓기 · Esc 나가기"));
            MiniDom.Put(ui, top, note, foot);
            UiRoot.Overlay.Add(ui);
            var hud = UiRoot.HudLayer; var hudVis = hud.style.visibility;
            hud.style.visibility = Visibility.Hidden;   // html.grab3d-open .hud { visibility: hidden }
            Mg.Focused()?.Blur(); // Enter takes, it does not press a button

            void Refresh()
            {
                Mg.Replace(held, cfg.items.Where(i => cfg.st[i.id].dst > 0).Select(i =>
                {
                    var b = Mg.Btn("grab3d-chip", () => PutBack(slots.First(s => s.it == i)), $"{i.name} ×{cfg.st[i.id].dst}");
                    b.tooltip = "하나 내려놓기";
                    return Mg.El("li", null, b);
                }).ToList());
                if (held.childCount == 0) held.Add(Mg.El("li", "grab3d-empty", cfg.kind == "shop" ? "장바구니가 비어 있다." : "가방이 비어 있다."));
                Mg.Replace(sum, cfg.foot());
            }
            void Take(Slot slot)
            {
                var id = slot.it.id;
                var why = slot.it.@out ? "품절입니다." : cfg.st[id].src <= 0 ? "더 없습니다." : cfg.canTake(id);
                if (why != null) { MiniDom.Announce(why); Toast(why); return; }
                cfg.st[id].src--; cfg.st[id].dst++;
                var from = J.WorldPos(slot.copies[0]);
                Stock(slot); engine.Act("point", 0.45f);
                var m = Geo.Obj(GeoOf(id), slot.mat, id);
                var to = boxPos + new Vector3((Rand() - 0.5f) * 0.25f, 0.04f + Math.Min(inBox.Count, 8) * 0.012f, (Rand() - 0.5f) * 0.15f);
                Fly(m, from, to, 0, () => { m.transform.localScale = Vector3.one * 0.85f; J.SetRot(m, 0, Rand() * 3, 0); inBox.Add((m, id)); });
                GameEvents.Ui("select");
                Refresh(); MiniDom.Announce($"{slot.it.name} 하나 담음");
            }
            void PutBack(Slot slot)
            {
                var id = slot.it.id; if (cfg.st[id].dst <= 0) return;
                cfg.st[id].dst--; cfg.st[id].src++;
                int i = inBox.FindLastIndex(x => x.id == id);
                GameObject m; float ry = 0;
                if (i >= 0) { m = inBox[i].m; inBox.RemoveAt(i); ry = J.GetYaw(m); } else m = Geo.Obj(GeoOf(id), slot.mat, id);
                var from = J.Pos(m); m.transform.localScale = Vector3.one;
                Fly(m, from, J.Pos(slot.g), ry, () => { UnityEngine.Object.Destroy(m); Stock(slot); });
                GameEvents.Ui("close");
                Refresh(); MiniDom.Announce($"{slot.it.name} 하나 내려놓음");
            }
            Refresh();

            // ---- input (canvas pointer events and window keydown, polled each frame) ----
            Slot HitSlot(Vector2 mouse)
            {
                var ray = camera.ScreenPointToRay(mouse);
                Slot hit = null; float bestD = float.MaxValue;
                foreach (var s in slots) foreach (var c in s.copies)
                {
                    var r = c.GetComponent<Renderer>();
                    if (r != null && r.bounds.IntersectRay(ray, out var d) && d < bestD) { bestD = d; hit = s; }
                }
                if (hit != null) return hit;
                foreach (var s in slots)   // the tags (sprites lie over everything)
                {
                    var r = s.tag.go.GetComponent<Renderer>();
                    if (r != null && r.bounds.IntersectRay(ray, out var d) && d < bestD) { bestD = d; hit = s; }
                }
                return hit;
            }
            bool OverPanel(Vector2 mouse)
            {
                var panel = foot.panel; if (panel == null) return false;
                var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(mouse.x, Screen.height - mouse.y)));
                return picked != null && (picked == foot || foot.Contains(picked));
            }
            Vector2? lastMouse = null;
            void Input()
            {
                if (slots.Count == 0) { if (GameInput.Down("Escape")) Close(cfg.escape()); return; }
                var mouse = GameInput.MousePosition; bool over = OverPanel(mouse);
                if (!over && lastMouse != mouse) { var s = HitSlot(mouse); if (s != null) sel = slots.IndexOf(s); }
                lastMouse = mouse;
                if (!over && (GameInput.MouseDown(0) || GameInput.MouseDown(1)))
                {
                    var s = HitSlot(mouse);
                    if (s != null) { sel = slots.IndexOf(s); if (GameInput.MouseDown(1)) PutBack(s); else Take(s); }
                }
                var focus = Mg.Focused(); bool onFootButton = focus != null && foot.Contains(focus);
                bool enter = GameInput.Down("Enter") || GameInput.Down("NumpadEnter") || GameInput.Down("Space");
                if (GameInput.Down("Escape")) Close(cfg.escape());
                else if (GameInput.Down("ArrowRight") || GameInput.Down("ArrowLeft")) { sel = (sel + (GameInput.Down("ArrowRight") ? 1 : slots.Count - 1)) % slots.Count; MiniDom.Announce($"{slots[sel].it.name} · {slots[sel].it.sub}"); }
                else if (enter && !onFootButton) Take(slots[sel]); // a focused button takes Enter itself
                else if (GameInput.Down("Backspace") || GameInput.Down("Delete")) PutBack(slots[sel]);
            }

            // ---- camera + per frame ----
            var view = engine.View; engine.SetView("first"); // hide the player's own body while the camera stands at the shelf
            Vector3 fromP = camera.transform.position; Quaternion fromQ = camera.transform.rotation;
            var aimQ = Quaternion.LookRotation(J.V(lookAt) - J.V(camPos));
            float kk = 0;
            engine.SetCameraOverride((cam, dt) =>
            {
                kk = Mathf.Min(1, kk + dt / 0.5f); float e = kk * kk * (3 - 2 * kk);
                cam.transform.position = Vector3.Lerp(fromP, J.V(camPos), e); cam.transform.rotation = Quaternion.Slerp(fromQ, aimQ, e);
            });
            offF = engine.OnFrame((dt, t) =>
            {
                if (closed) return;
                for (int i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    s.glow += ((i == sel ? 0.35f : 0) - s.glow) * Mathf.Min(1, dt * 10);
                    Gyeongbo.Engine.Mat.SetEmissiveIntensity(s.mat, s.glow);
                    s.tag.Face(camera);
                }
                for (int i = flights.Count - 1; i >= 0; i--)
                {
                    var F = flights[i]; F.t = Mathf.Min(1, F.t + dt / 0.42f); float u = F.t, a = 1 - u;
                    J.SetPos(F.mesh, a * a * F.from + 2 * a * u * F.mid + u * u * F.to);
                    F.rx += dt * 6; J.SetRot(F.mesh, F.rx, F.ry, 0);
                    if (u >= 1) { J.SetRot(F.mesh, 0, F.ry, 0); flights.RemoveAt(i); F.done(); }
                }
                Input();
            });
            cleanup = () =>
            {
                offF?.Invoke(); engine.SetCameraOverride(null); engine.SetView(view);
                ui.RemoveFromHierarchy(); hud.style.visibility = hudVis; noteT?.Pause();
                foreach (var s in slots) { UnityEngine.Object.Destroy(s.mat); s.tag.Dispose(); }
                UnityEngine.Object.Destroy(root);
                foreach (var o in owned) UnityEngine.Object.Destroy(o);
            };
            return tcs.Task;
        }

        const int MAX_QTY = 20;

        // ctx.ui.shop: resolves { cart, total } or null when left without buying.
        public static Func<ShopOptions, Task<ShopResult>> Shop3d(GameEngine engine) => async o =>
        {
            o ??= new ShopOptions();
            var items = o.items ?? new List<ShopItem>(); int money = o.money;
            var st = new Dictionary<string, Count>();
            foreach (var i in items) st[i.id] = new Count { src = i.stock == "out" ? 0 : MAX_QTY, dst = 0 };
            int Total() => items.Sum(i => i.price * st[i.id].dst);
            double Kg() => items.Sum(i => i.weightKg * st[i.id].dst);
            var r = await GrabRoom(engine, new Cfg
            {
                kind = "shop", title = $"장보기 · {o.title ?? "진열대"}", info = (o.info ?? new List<InfoLine>()).Select(i => i.text).ToList(),
                items = items.Select(i => new GItem { id = i.id, name = i.name, sub = i.stock == "out" ? "품절" : $"{MiniDom.Won(i.price)}{(i.stock == "low" ? " · 적음" : "")}", @out = i.stock == "out" }).ToList(),
                st = st, canTake = id => Total() + items.First(i => i.id == id).price > money ? "가진 돈이 모자란다." : null,
                foot = () => new List<VisualElement> { Mg.El("span", null, $"합계 {MiniDom.Won(Total())} / 가진 돈 {MiniDom.Won(money)}"), Mg.El("span", null, $"들고 갈 무게 약 {MiniDom.FmtKg(Kg())}") },
                buttons = new List<(string, bool, Action<Action<object>>)> {
                    ("그만두기", false, c => c(null)),
                    ("계산대로 가져가기", true, c => c(new ShopResult { cart = st.Where(kv => kv.Value.dst > 0).ToDictionary(kv => kv.Key, kv => kv.Value.dst), total = Total() })) },
                escape = () => null,
            });
            return r as ShopResult;
        };

        // ctx.ui.bag: resolves { bag }. The home stock laid out in front, the backpack below.
        public static Func<BagOptions, Task<BagResult>> Bag3d(GameEngine engine) => async o =>
        {
            o ??= new BagOptions();
            string mode = o.mode ?? "guided";
            var limit = o.limit ?? new BagLimit { weightKg = 8, volumeL = 25 };
            var items = o.items ?? new List<BagItem>(); var goals = o.goals ?? new List<BagGoal>();
            var st = new Dictionary<string, Count>();
            foreach (var i in items) st[i.id] = new Count { src = Math.Max(0, i.home), dst = Math.Max(0, i.bag) };
            (double w, double v) Load() => items.Aggregate((w: 0.0, v: 0.0), (a, i) => (a.w + i.weightKg * st[i.id].dst, a.v + i.volumeL * st[i.id].dst));
            BagResult Result() { var r = new BagResult(); foreach (var i in items) r.bag[i.id] = st[i.id].dst; return r; }
            var shown = items.Where(i => st[i.id].src + st[i.id].dst > 0).ToList();
            var res = await GrabRoom(engine, new Cfg
            {
                kind = "bag", title = "가방 꾸리기", items = shown.Select(i => new GItem { id = i.id, name = i.name, sub = MiniDom.FmtKg(i.weightKg) }).ToList(), st = st,
                canTake = id => { var i = items.First(x => x.id == id); var (_, v) = Load(); return v + i.volumeL > limit.volumeL * 1.15 ? "가방에 더 들어가지 않는다." : null; },
                foot = () =>
                {
                    var (w, v) = Load(); string sw = MiniDom.MeterState(w, limit.weightKg), sv = MiniDom.MeterState(v, limit.volumeL);
                    var ew = Mg.Text(null, $"무게 {MiniDom.FmtKg(w)} / {MiniDom.FmtKg(limit.weightKg)} · {MiniDom.METER_WORD[sw]}"); MinigameStyle.TextState(ew, sw);
                    var ev = Mg.Text(null, $"부피 {Mg.Fixed1(v)}L / {Mg.Num(limit.volumeL)}L · {MiniDom.METER_WORD[sv]}"); MinigameStyle.TextState(ev, sv);
                    ew.style.marginRight = ev.style.marginRight = 18;
                    var gl = Mg.El("ul", "grab3d-goals", Bag.GoalStatus(goals, mode, id => st.TryGetValue(id, out var c) ? c.dst : 0, items).Select(x =>
                    { var li = Mg.El("li", x.ok == true ? "grab3d-goal is-done" : "grab3d-goal", (x.ok == null ? "· " : x.ok == true ? "✓ " : "○ ") + x.label); li.style.marginRight = 12; return li; }).ToList());
                    return new List<VisualElement> { ew, ev, gl };
                },
                buttons = new List<(string, bool, Action<Action<object>>)> { ("다 챙겼다", true, c => c(Result())) },
                escape = () => Result(),
            });
            return res as BagResult;
        };
    }
}
