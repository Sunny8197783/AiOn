// Ports levels/town-strike.js: the D-day missile strike on a background villa south of 연무공원 — the intact
// villa is hidden and replaced by a gutted shell, a rubble heap, fires in the openings and a leaning smoke column.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownStrikeResult
    {
        public GameObject group;
        public Action<bool> setHit;
        // (dt, t, camera, dark): dark = night/blackout/dusk (the fires glow more).
        public Action<float, float, Camera, bool> update;
    }

    public static class TownStrike
    {
        const int PUFFS = 22;
        const float TOP = 70;

        sealed class Puff { public GameObject s; public Material m; public float k, spin; }

        public static TownStrikeResult BuildStrike(Kit kit, IFx fx, StrikeSite strike)
        {
            var group = J.Group("strike");
            var b = strike?.box;
            if (b == null) { group.SetActive(false); return new TownStrikeResult { group = group, setHit = _ => { }, update = (_, __, ___, ____) => { } }; }
            float cx = (b.x0 + b.x1) / 2, cz = (b.z0 + b.z1) / 2, W = b.x1 - b.x0, D = b.z1 - b.z0;
            var rand = Kit.Rng(4242);
            var scorched = kit.Mat("paint:#2a2624", new MatSpec { roughness = 0.95f });
            var concrete = kit.Surface("concrete", new SurfaceOpts { color = J.Hex(0x8d8780) });
            var soot = kit.Mat("paint:#121110", new MatSpec { roughness = 1 });

            // Gutted shell: corner piers and broken wall stubs of uneven height, burnt slabs hanging between them.
            var ruin = J.Group("ruin", group);
            foreach (var (x, z) in new[] { (b.x0, b.z0), (b.x1, b.z0), (b.x0, b.z1), (b.x1, b.z1) })
            {
                float h = b.h * (0.35f + rand() * 0.55f);
                ruin.Add(kit.Box(0.9f, h, 0.9f, concrete, new At { x = x + (x == b.x0 ? 0.45f : -0.45f), y = 0, z = z + (z == b.z0 ? 0.45f : -0.45f) }));
            }
            for (int i = 0; i < 9; i++) // jagged wall stubs along the faces
            {
                float along = rand(); int side = i % 4; float h = 1.5f + rand() * b.h * 0.45f, w = 1.5f + rand() * 3;
                float x = side < 2 ? b.x0 + along * W : side == 2 ? b.x0 + 0.2f : b.x1 - 0.2f, z = side < 2 ? (side != 0 ? b.z1 - 0.2f : b.z0 + 0.2f) : b.z0 + along * D;
                ruin.Add(kit.Box(side < 2 ? w : 0.3f, h, side < 2 ? 0.3f : w, i % 3 != 0 ? scorched : concrete, new At { x = x, y = 0, z = z, rz = (rand() - 0.5f) * 0.12f }));
            }
            for (int f = 1; f <= 2; f++) // two floor slabs snapped and tilted into the heap
            {
                var s = kit.Box(W * 0.55f, 0.25f, D * 0.7f, concrete, new At { x = cx + (rand() - 0.5f) * 3, y = f * 2.6f, z = cz + (rand() - 0.5f) * 3 });
                float rx = (rand() - 0.5f) * 0.5f, ry = rand() * 0.6f, rz = (f == 1 ? 1 : -1) * (0.25f + rand() * 0.2f);
                J.SetRot(s, rx, ry, rz); ruin.Add(s);
            }
            // Rubble heap and debris thrown into the street on the park side.
            var chunks = new List<Xform>();
            for (int i = 0; i < 140; i++)
            {
                float r = Mathf.Sqrt(rand()), a = rand() * Mathf.PI * 2; bool @out = i > 110;
                float x = @out ? b.x0 + rand() * W : cx + Mathf.Cos(a) * r * W * 0.55f, z = @out ? b.z0 - 1 - rand() * 7 : cz + Mathf.Sin(a) * r * D * 0.55f;
                float s = @out ? 0.2f + rand() * 0.5f : 0.5f + rand() * 1.6f;
                // JS object literal order: y, sx, (sy), (sz), rx, ry, rz
                float y = @out ? 0 : (1 - r) * 3.2f * rand();
                float sx = s * (0.8f + rand());
                chunks.Add(new Xform { x = x, y = y, z = z, sx = sx, sy = s * 0.6f, sz = s, rx = rand() * 3, ry = rand() * 3, rz = rand() * 3 });
            }
            ruin.Add(kit.Instanced(Geo.Box(1, 1, 1), concrete, chunks, cast: true));
            var scorch = TownBKit.Mesh(Geo.Circle(Mathf.Max(W, D) * 0.85f, 28).RotateX(-Mathf.PI / 2), soot, receive: true);
            J.SetPos(scorch, cx, 0.03f, cz - 2); ruin.Add(scorch);
            kit.Merge(ruin);

            // Fires in the heap (no point light: the light count stays fixed, the glow comes from the flames).
            var fires = new List<IFire>();
            foreach (var (x, y, z, scale) in new[] { (cx - 3, 1.6f, cz - 2, 3.2f), (cx + 4, 0.8f, cz + 1, 2.4f), (cx, 3.2f, cz + 3, 2.8f) })
                fires.Add(fx.Fire(new FireOptions { x = x, y = y, z = z, scale = scale, radius = 0.5f, light = 0, level = 0 }));
            foreach (var f in fires) group.Add(f.Group);

            // Smoke column: big soft puffs that rise, widen and drift east with the wind, recycled at the base.
            var tex = kit.Memo("town:strikeSmoke", () => kit.Texture(kit.Canvas(128, 128, (g, w, _) =>
            {
                var gr = new PaintGradient(w / 2f, w / 2f, 4, w / 2f, w / 2f, w / 2f);
                gr.addColorStop(0, "rgba(255,255,255,0.9)"); gr.addColorStop(0.5f, "rgba(255,255,255,0.45)"); gr.addColorStop(1, "rgba(255,255,255,0)");
                g.fillStyle = gr; g.fillRect(0, 0, w, w);
            })));
            var quad = Geo.Plane(1, 1).ToMesh("strikePuff");
            var puffs = new List<Puff>();
            for (int i = 0; i < PUFFS; i++)
            {
                // ponytail: THREE.Sprite -> a double-sided unlit quad turned to the camera each frame; upgrade: a
                // particle system or a billboard shader.
                var m = Mat.Unlit(new MatSpec { map = tex, color = J.Hex(0x34302d), transparent = true, depthWrite = false, opacity = 0.8f, side = "double" });
                var s = Geo.Obj(quad, m, "puff", false, false); group.Add(s);
                puffs.Add(new Puff { s = s, m = m, k = (float)i / PUFFS, spin = rand() });
            }

            bool hit = false;
            void SetHit(bool on)
            {
                hit = on; group.SetActive(hit); if (strike.group != null) strike.group.SetActive(!hit);
                foreach (var f in fires) f.SetLevel(hit ? 0.9f : 0);
            }
            void Update(float dt, float t, Camera camera, bool night)
            {
                if (!hit) return;
                foreach (var f in fires) f.Update(dt, camera);
                foreach (var p in puffs)
                {
                    p.k = (p.k + dt / 55) % 1;
                    float y = 3 + p.k * TOP, size = 6 + p.k * 34;
                    J.SetPos(p.s, cx + p.k * p.k * 38 + Mathf.Sin(t * 0.2f + p.spin * 6) * 2, y, cz - 2 + Mathf.Cos(t * 0.17f + p.spin * 5) * 2);
                    p.s.transform.localScale = new Vector3(size, size, 1);
                    if (camera != null) p.s.transform.rotation = camera.transform.rotation;
                    // Lit from below by the fire at night, plain grey-black by day (three.js setRGB is linear).
                    float glow = night ? Mathf.Max(0, 1 - p.k * 4) : 0;
                    Mat.SetColor(p.m, new Color(0.2f + glow * 0.55f, 0.19f + glow * 0.22f, 0.18f + glow * 0.05f).gamma);
                    Mat.SetOpacity(p.m, 0.85f * Mathf.Min(1, p.k * 8) * Mathf.Pow(1 - p.k, 0.7f));
                }
            }
            SetHit(false);
            return new TownStrikeResult { group = group, setHit = SetHit, update = Update };
        }
    }
}
