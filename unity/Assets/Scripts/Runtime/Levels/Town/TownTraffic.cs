// Ports levels/town-traffic.js: three procedural vehicle types (sedan, SUV, 1t box truck) with paint tinted per
// car, parked cars in every marked bay row, moving cars on TownPlan.LANES that follow each other (IDM), stop at
// red/amber and for the player, signal heads and pedestrian signals that run the CYCLE (TownPlan.SignalAt),
// headlights and road glow at night. Moving cars carry live solids. Modes normal | light | none.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownTrafficResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        public Action<string> setMode;
        public Action<float, float, FrameCtx> update;
        public Func<string> mode;
    }

    public static class TownTraffic
    {
        sealed class Parked { public string type, color; public float x, z, yaw; }
        sealed class Car
        {
            public int lane, rank; public string type, color; public float u, v, v0, len; public bool active; public Solid solid;
            public GameObject obj, beam;
        }
        sealed class SigLamps { public Material red, amber, green; }
        sealed class PedLamps { public Material red, green; }

        public static TownTrafficResult BuildTraffic(Kit kit)
        {
            var group = J.Group("traffic");
            var rand = Kit.Rng(31337); var solids = new List<Solid>();
            var LANES = TownPlan.LANES; float LANE_SPAN = TownPlan.LANE_SPAN, CURB = TownPlan.CURB;
            string PickW(List<KeyValuePair<string, float>> list) => TownUtil.Pick(rand, list);
            var typeWeights = TownTrafficCars.TYPES.Select(t => new KeyValuePair<string, float>(t.Key, t.Value.weight)).ToList();
            string RandType() => PickW(typeWeights);

            // ---- parked cars ----
            var parked = new List<Parked>();
            foreach (var (x0, x1, zl, depth, step, kind) in TownTrafficCars.ROWS)
                for (double x = x0; x + step <= x1 + 1e-6; x += step)
                {
                    float cx = (float)(x + step / 2), cz = zl + depth / 2;
                    if (rand() > (kind != null ? 0.7f : 0.62f) || TownTrafficCars.KEEP_CLEAR.Any(r => cx > r.x0 && cx < r.x1 && cz > r.z0 && cz < r.z1)) continue;
                    var type = kind ?? RandType();
                    float px = cx + (rand() - 0.5f) * 0.2f, pz = cz + (rand() - 0.5f) * 0.3f;
                    float yaw = (kind != null ? Mathf.PI : rand() < 0.5f ? 0 : Mathf.PI) + (rand() - 0.5f) * 0.05f;
                    parked.Add(new Parked { type = type, x = px, z = pz, yaw = yaw });
                }
            foreach (var (zc, yaw) in new[] { (57.3f, Mathf.PI / 2), (62.7f, -Mathf.PI / 2), (-62.7f, Mathf.PI / 2), (-57.3f, -Mathf.PI / 2) })
                for (double x = -40; x <= 40; x += 6.5)
                {
                    if (Math.Abs(x) < 5 || rand() < 0.45f) continue;
                    var type = rand() < 0.3f ? "truck" : RandType();
                    parked.Add(new Parked { type = type, x = (float)x + rand(), z = zc, yaw = yaw });
                }
            foreach (var p in parked)
            {
                var T = TownTrafficCars.Type(p.type); bool along = Mathf.Abs(Mathf.Sin(p.yaw)) > 0.5f;
                float hx = (along ? T.L : T.W) / 2 + 0.05f, hz = (along ? T.W : T.L) / 2 + 0.05f;
                solids.Add(new Solid(p.x - hx, p.x + hx, p.z - hz, p.z + hz, -0.5f, 1.8f, "car"));
                p.color = p.type == "truck" ? PickW(TownTrafficCars.TRUCK_PALETTE) : PickW(TownTrafficCars.PALETTE);
            }

            // ---- moving pool ----
            var pool = new List<Car>();
            for (int li = 0; li < LANES.Count; li++)
            {
                var lane = LANES[li]; int n = lane.axis == "x" ? 4 : 2;
                for (int i = 0; i < n; i++)
                {
                    var type = RandType();
                    float u = -LANE_SPAN + (i + 0.2f + rand() * 0.6f) * (2 * LANE_SPAN / n);
                    float v0 = lane.vmax * TownTrafficCars.Type(type).v * (0.9f + rand() * 0.2f);
                    pool.Add(new Car { lane = li, type = type, rank = i, u = u, v = lane.vmax * 0.8f, v0 = v0, len = TownTrafficCars.Type(type).L, active = true,
                        color = PickW(TownTrafficCars.PALETTE), solid = new Solid(0, 0, 0, 0, -0.5f, 1.8f, "movingCar") { off = true } });
                }
            }
            foreach (var c in pool) solids.Add(c.solid);

            // ---- parts per type ----
            // ponytail: MeshPhysicalMaterial clear coat -> plain URP Lit (no clear-coat layer); a car shader is the upgrade.
            var paint = kit.Memo("town:carPaint", () => Mat.Lit(new MatSpec { color = Color.white, roughness = 0.32f, metalness = 0.45f }));
            var lampTex = kit.Memo("town:carLampTex", () =>
            {
                var t = kit.Texture(kit.Canvas(2, 1, (g, w, h) => { g.fillStyle = "#fff4dc"; g.fillRect(0, 0, 1, 1); g.fillStyle = "#ff2a1c"; g.fillRect(1, 0, 1, 1); }));
                t.filterMode = FilterMode.Point; return t;
            });
            var lampMat = Mat.Lit(new MatSpec { map = lampTex, emissiveMap = lampTex, emissive = Color.white, emissiveIntensity = 0.25f, roughness = 0.25f });
            var mats = new Dictionary<string, Material>
            {
                ["body"] = paint, ["glass"] = kit.Mat("darkGlass", new MatSpec { roughness = 0.04f }), ["dark"] = kit.Mat("paint:#141517", new MatSpec { roughness = 0.7f }), ["lamp"] = lampMat,
            };
            Material PartMat(string part, string color) => part == "body" ? TownUtil.Tinted(kit, "town:carPaint", paint, color) : mats[part];

            // Headlight glow on the road in front of moving cars (night only).
            var beamTex = kit.Memo("town:beamTex", () => kit.Texture(kit.Canvas(64, 256, (g, w, h) =>
            {
                var gr = new PaintGradient(0, h, 0, 0); gr.addColorStop(0, "rgba(255,255,255,0.9)"); gr.addColorStop(1, "rgba(255,255,255,0)");
                g.fillStyle = gr; g.beginPath(); g.moveTo(w * 0.3f, h); g.lineTo(w * 0.7f, h); g.lineTo(w, 0); g.lineTo(0, 0); g.fill();
            })));
            var beamGeo = Geo.Plane(3.4f, 11).RotateX(-Mathf.PI / 2).Translate(0, 0.03f, -5.5f);
            var beamMat = Mat.Unlit(new MatSpec { map = beamTex, color = J.Hex(0xffe9c4), transparent = true, opacity = 0.32f, blending = "additive", depthWrite = false });

            // ponytail: three.js InstancedMesh with per-instance matrix/colour -> parked cars through Kit.Instanced (one
            // body batch per paint colour), moving cars as one GameObject each moved per frame. GPU instancing with an
            // instance colour buffer is the upgrade if the car count grows.
            foreach (var (type, T) in TownTrafficCars.TYPES)
            {
                var moving = pool.Where(c => c.type == type).ToList(); var still = parked.Where(p => p.type == type).ToList();
                if (moving.Count + still.Count == 0) continue;
                var geo = TownTrafficCars.VehicleGeo(T);
                foreach (var part in TownTrafficCars.PARTS)
                {
                    bool cast = part == "body" || part == "dark", receive = part != "lamp";
                    foreach (var col in part == "body" ? still.Select(p => p.color).Distinct().ToList() : new List<string> { null })
                    {
                        var list = still.Where(p => col == null || p.color == col).Select(p => new Xform { x = p.x, y = 0, z = p.z, ry = p.yaw }).ToList();
                        if (list.Count == 0) continue;
                        var im = kit.Instanced(TownTrafficCars.Part(geo, part), PartMat(part, col), list, cast, receive);
                        im.name = $"car:{type}:{part}"; group.Add(im);
                    }
                }
                foreach (var c in moving)
                {
                    c.obj = J.Group($"car:{type}"); group.Add(c.obj);
                    foreach (var part in TownTrafficCars.PARTS)
                        c.obj.Add(Geo.Obj(TownTrafficCars.Part(geo, part), PartMat(part, c.color), $"car:{type}:{part}", part == "body" || part == "dark", part != "lamp"));
                    c.beam = TownBKit.Mesh(beamGeo, beamMat); c.beam.name = "carBeams"; c.obj.Add(c.beam);
                    J.SetPos(c.beam, 0, 0, -c.len / 2); c.beam.SetActive(false);
                }
            }

            // ---- signal heads and pedestrian signals ----
            var sig = J.Group("signals"); group.Add(sig);
            Material Lamp(int hex) => Mat.Lit(new MatSpec { color = J.Hex(0x1b1c1e), emissive = J.Hex(hex), emissiveIntensity = 0, roughness = 0.3f });
            var L = new Dictionary<string, SigLamps>
            {
                ["EW"] = new SigLamps { red = Lamp(0xff2a1a), amber = Lamp(0xffa424), green = Lamp(0x27e08a) },
                ["NS"] = new SigLamps { red = Lamp(0xff2a1a), amber = Lamp(0xffa424), green = Lamp(0x27e08a) },
            };
            Texture2D PedTex(bool walk) => kit.Memo($"town:ped:{(walk ? "true" : "false")}", () => kit.Texture(kit.Canvas(64, 64, (g, w, _) =>
            {
                g.fillStyle = "#000"; g.fillRect(0, 0, w, w); g.fillStyle = "#fff"; g.beginPath(); g.arc(32, 12, 6, 0, 7); g.fill(); g.lineWidth = 7; g.strokeStyle = "#fff"; g.lineCap = "round";
                if (walk) { g.beginPath(); g.moveTo(32, 22); g.lineTo(29, 40); g.lineTo(20, 56); g.moveTo(29, 40); g.lineTo(40, 55); g.moveTo(31, 26); g.lineTo(20, 36); g.moveTo(31, 26); g.lineTo(42, 34); g.stroke(); }
                else { g.beginPath(); g.moveTo(32, 22); g.lineTo(32, 42); g.lineTo(27, 58); g.moveTo(32, 42); g.lineTo(37, 58); g.moveTo(32, 24); g.lineTo(24, 40); g.moveTo(32, 24); g.lineTo(40, 40); g.stroke(); }
            })));
            Material PedMat(bool walk, int hex) => Mat.Lit(new MatSpec { color = J.Hex(0x151515), emissive = J.Hex(hex), emissiveMap = PedTex(walk), emissiveIntensity = 0, roughness = 0.3f });
            var P = new Dictionary<string, PedLamps>
            {
                ["EW"] = new PedLamps { red = PedMat(false, 0xff3322), green = PedMat(true, 0x2ae68e) },
                ["NS"] = new PedLamps { red = PedMat(false, 0xff3322), green = PedMat(true, 0x2ae68e) },
            };
            var housing = kit.Mat("paint:#2a2c2f", new MatSpec { roughness = 0.5f, metalness = 0.4f });
            var pole = kit.Mat("paint:#8a9196", new MatSpec { roughness = 0.45f, metalness = 0.6f });
            void AddLampDisc(Material mat, float x, float y, float z, float nx, float nz, float rr = 0.13f)
            {
                var m = TownBKit.Mesh(Geo.Circle(rr, 18), mat); J.SetPos(m, x + nx * 0.16f, y, z + nz * 0.16f); J.SetYaw(m, Mathf.Atan2(nx, nz)); sig.Add(m);
            }
            // JS Map keyed `${axis}:${dir}:${st}`: first insertion fixes the order, the last lane wins the value.
            var headKeys = new List<string>(); var heads = new Dictionary<string, (Lane lane, float st)>();
            foreach (var lane in LANES) foreach (var st in lane.stops)
            {
                var key = $"{lane.axis}:{lane.dir}:{st}";
                if (!heads.ContainsKey(key)) headKeys.Add(key);
                heads[key] = (lane, st);
            }
            foreach (var key in headKeys)
            {
                var (lane, st) = heads[key];
                var road = lane.axis == "x" ? TownMap.ROADS[0] : TownMap.ROADS.First(r => r.z1 - r.z0 > r.x1 - r.x0 && lane.c > r.x0 && lane.c < r.x1);
                var cwAt = TownMap.CROSSWALKS.FirstOrDefault(c => c.road == road.id && Math.Abs(c.at - (st + lane.dir * 2.9)) < 1.2);
                float far = cwAt != null ? (float)cwAt.at + lane.dir * 3.3f : st + lane.dir * 10.6f;
                float lat = (float)(lane.axis == "x" ? (lane.dir > 0 ? road.z1 + 0.65 : road.z0 - 0.65) : (lane.dir < 0 ? road.x1 + 0.65 : road.x0 - 0.65));
                float centre = (float)(lane.axis == "x" ? (road.z0 + road.z1) / 2 : (road.x0 + road.x1) / 2), armLen = Mathf.Abs(lat - centre) * 0.78f;
                float px = lane.axis == "x" ? far : lat, pz = lane.axis == "x" ? lat : far;
                float toC = Math.Sign(centre - lat), ax = lane.axis == "x" ? 0 : toC, az = lane.axis == "x" ? toC : 0;
                sig.Add(kit.Cyl(0.11f, 0.14f, 6.2f, pole, new At { x = px, y = CURB, z = pz }, 12));
                var arm = Geo.Obj(Geo.Cylinder(0.07f, 0.08f, armLen, 10), pole, null, true, false);
                J.SetRot(arm, az != 0 ? Mathf.PI / 2 : 0, 0, ax != 0 ? Mathf.PI / 2 : 0);
                J.SetPos(arm, px + ax * armLen / 2, CURB + 6.0f, pz + az * armLen / 2); sig.Add(arm);
                solids.Add(new Solid(px - 0.2f, px + 0.2f, pz - 0.2f, pz + 0.2f, -0.5f, 4, "signalPole"));
                float hx = px + ax * (armLen - 0.3f), hz = pz + az * (armLen - 0.3f), hy = CURB + 5.6f;
                float nx = lane.axis == "x" ? -lane.dir : 0, nz = lane.axis == "z" ? -lane.dir : 0; // faces the approaching cars
                float lx = lane.axis == "x" ? 0 : lane.dir, lz = lane.axis == "x" ? -lane.dir : 0; // driver's left (red side)
                sig.Add(kit.Box(lane.axis == "x" ? 0.3f : 1.35f, 0.42f, lane.axis == "x" ? 1.35f : 0.3f, housing, new At { x = hx, y = hy - 0.21f, z = hz }));
                sig.Add(kit.Box(0.04f, 0.5f, 0.04f, pole, new At { x = hx, y = hy + 0.2f, z = hz }));
                var G = L[lane.group];
                AddLampDisc(G.red, hx + lx * 0.42f, hy, hz + lz * 0.42f, nx, nz); AddLampDisc(G.amber, hx, hy, hz, nx, nz); AddLampDisc(G.green, hx - lx * 0.42f, hy, hz - lz * 0.42f, nx, nz);
            }
            foreach (var c in TownMap.CROSSWALKS)
            {
                var road = TownMap.ROADS.First(r => r.id == c.road); bool ew = road.z1 - road.z0 < road.x1 - road.x0;
                if (!new[] { "yeonmu", "munhwa", "hanbit" }.Contains(road.id)) continue;
                var grp = ew ? "NS" : "EW"; // crossing an EW road is allowed while NS traffic has green
                foreach (var side in new[] { -1, 1 })
                {
                    float edge = (float)(side < 0 ? (ew ? road.z0 : road.x0) - 1.6 : (ew ? road.z1 : road.x1) + 1.6);
                    float off = ew ? (side < 0 ? 2.9f : -2.9f) : (side < 0 ? -2.9f : 2.9f);
                    float x = ew ? (float)c.at + off : edge, z = ew ? edge : (float)c.at + off;
                    float nx = ew ? 0 : -side, nz = ew ? -side : 0;
                    sig.Add(kit.Cyl(0.06f, 0.07f, 2.4f, pole, new At { x = x, y = CURB, z = z }, 10),
                        kit.Box(ew ? 0.36f : 0.26f, 0.78f, ew ? 0.26f : 0.36f, housing, new At { x = x + nx * 0.12f, y = CURB + 2.15f, z = z + nz * 0.12f }));
                    solids.Add(new Solid(x - 0.15f, x + 0.15f, z - 0.15f, z + 0.15f, -0.5f, 3, "pedSignal"));
                    foreach (var (mat, dy) in new[] { (P[grp].red, 0.18f), (P[grp].green, -0.18f) })
                    {
                        var m = TownBKit.Mesh(Geo.Plane(0.28f, 0.28f), mat); J.SetPos(m, x + nx * 0.26f, CURB + 2.54f + dy, z + nz * 0.26f); J.SetYaw(m, Mathf.Atan2(nx, nz)); sig.Add(m);
                    }
                }
            }
            kit.Merge(sig);

            // ---- per-frame ----
            string mode = "normal"; bool power = true, night = false;
            var cfg = new Dictionary<string, (int x, int z)> { ["normal"] = (4, 2), ["light"] = (2, 1), ["none"] = (0, 0) };
            void SetMode(string m)
            {
                if (m == null || !cfg.ContainsKey(m)) { Debug.LogError($"[town] traffic \"{m}\" unknown; use normal | light | none"); return; }
                mode = m;
                foreach (var c in pool) c.active = c.rank < (LANES[c.lane].axis == "x" ? cfg[m].x : cfg[m].z);
            }
            SignalState SetLamps(float t)
            {
                var s = TownPlan.SignalAt(t);
                foreach (var g in new[] { "EW", "NS" })
                {
                    var st = g == "EW" ? s.EW : s.NS;
                    Mat.SetEmissiveIntensity(L[g].red, power && st == "red" ? 5 : 0);
                    Mat.SetEmissiveIntensity(L[g].amber, power && st == "amber" ? 5 : 0);
                    Mat.SetEmissiveIntensity(L[g].green, power && st == "green" ? 5 : 0);
                    bool walk = st == "green" && !(s.left < 5 && Mathf.FloorToInt(t * 2) % 2 != 0);
                    Mat.SetEmissiveIntensity(P[g].green, power && walk ? 4 : 0);
                    Mat.SetEmissiveIntensity(P[g].red, power && st != "green" ? 4 : 0);
                }
                return s;
            }
            void Update(float dt, float t, FrameCtx ctx)
            {
                ctx ??= new FrameCtx();
                var env = ctx.env;
                power = ctx.power && env != "blackout";
                bool isNight = env == "night" || env == "blackout" || env == "dusk";
                if (isNight != night) { night = isNight; Mat.SetEmissiveIntensity(lampMat, night ? 3 : 0.25f); }
                bool beamsOn = night && mode != "none";
                var s = SetLamps(t); var pl = ctx.player;
                for (int li = 0; li < LANES.Count; li++)
                {
                    var lane = LANES[li];
                    var cars = pool.Where(c => c.lane == li && c.active).OrderBy(c => c.u).ToList();
                    var stopsU = lane.stops.Select(x => x * lane.dir).OrderBy(x => x).ToList();
                    var light = power ? (lane.group == "EW" ? s.EW : s.NS) : "green";
                    float? pu = pl != null && pl.Y < 0.6f && Mathf.Abs((lane.axis == "x" ? pl.Z : pl.X) - lane.c) < 1.7f ? (lane.axis == "x" ? pl.X : pl.Z) * lane.dir : (float?)null;
                    for (int i = 0; i < cars.Count; i++)
                    {
                        var c = cars[i];
                        bool hasLead = i + 1 < cars.Count || cars.Count > 1;
                        float leadU = 0, leadV = 0, leadLen = 0;
                        if (i + 1 < cars.Count) { leadU = cars[i + 1].u; leadV = cars[i + 1].v; leadLen = cars[i + 1].len; }
                        else if (cars.Count > 1) { leadU = cars[0].u + 2 * LANE_SPAN; leadV = cars[0].v; leadLen = cars[0].len; }
                        float front = c.u + c.len / 2;
                        float gap = hasLead ? leadU - c.u - (c.len + leadLen) / 2 : 1e9f, vl = hasLead ? leadV : c.v0;
                        if (light != "green") foreach (var su in stopsU)
                        {
                            float d = su - front; if (d < -0.5f) continue;
                            if (d < 80 && (light == "red" || d > c.v * c.v / 8 + 2) && d < gap) { gap = Mathf.Max(0.05f, d); vl = 0; }
                            break;
                        }
                        if (pu != null) { float d = pu.Value - front - 1.4f; if (d > -2.5f && d < 30 && d < gap) { gap = Mathf.Max(0.05f, d); vl = 0; } }
                        float dv = c.v - vl, sStar = 1.6f + Mathf.Max(0, c.v * 1.2f + c.v * dv / (2 * Mathf.Sqrt(2.2f * 3.2f)));
                        float acc = Mathf.Max(-9, 2.2f * (1 - Mathf.Pow(c.v / c.v0, 4) - Mathf.Pow(sStar / Mathf.Max(gap, 0.05f), 2)));
                        c.v = Mathf.Max(0, c.v + acc * dt); c.u += c.v * dt;
                        if (c.u > LANE_SPAN) c.u -= 2 * LANE_SPAN;
                    }
                }
                foreach (var c in pool)
                {
                    var lane = LANES[c.lane]; float s0 = c.u * lane.dir;
                    float x = lane.axis == "x" ? s0 : lane.c, z = lane.axis == "x" ? lane.c : s0;
                    float yaw = lane.axis == "x" ? TownUtil.YawTo(lane.dir, 0) : TownUtil.YawTo(0, lane.dir);
                    if (c.obj != null)
                    {
                        if (c.obj.activeSelf != c.active) c.obj.SetActive(c.active);
                        if (c.active) { J.SetPos(c.obj, x, 0, z); J.SetYaw(c.obj, yaw); }
                        if (c.beam.activeSelf != beamsOn) c.beam.SetActive(beamsOn);
                    }
                    var S = c.solid; float hl = c.len / 2 + 0.05f, hw = TownTrafficCars.Type(c.type).W / 2 + 0.05f;
                    S.off = !c.active || Mathf.Abs(x) > 135 || Mathf.Abs(z) > 115;
                    if (lane.axis == "x") { S.x0 = x - hl; S.x1 = x + hl; S.z0 = z - hw; S.z1 = z + hw; } else { S.x0 = x - hw; S.x1 = x + hw; S.z0 = z - hl; S.z1 = z + hl; }
                }
            }
            SetMode("normal");
            Update(0, 0, new FrameCtx());
            return new TownTrafficResult { group = group, solids = solids, setMode = SetMode, update = Update, mode = () => mode };
        }
    }
}
