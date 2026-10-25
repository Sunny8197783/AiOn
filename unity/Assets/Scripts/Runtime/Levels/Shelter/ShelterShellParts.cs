// The four ways into the shelter (second half of levels/shelter-shell.js buildShell): stair A (switchback core
// north of the opening), car ramp B (rising south to the street), accessible ramp C (1:12 north, turning east)
// and the east core (lift hall behind the open fire doors). JS space throughout.
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.ShelterShell;

namespace Gyeongbo.Levels
{
    delegate GameObject ShellAdd(GameObject m, GameObject parent = null);
    delegate void ShellSolid(float x0, float x1, float z0, float z1, float y0 = -1, float y1 = 6.5f);
    delegate GameObject ShellSlopeZ(float x0, float x1, float zA, float yA, float zB, float yB, float th, Material mat, GameObject parent = null);
    delegate GameObject ShellSlopeX(float xA, float yA, float xB, float yB, float z0, float z1, float th, Material mat);
    delegate void ShellTactile(float x0, float x1, float z0, float z1, float y = 0.008f);

    static class ShelterShellParts
    {
        const float PI = Mathf.PI;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static At at(float x = 0, float y = 0, float z = 0, float rx = 0, float ry = 0, float rz = 0) => new At { x = x, y = y, z = z, rx = rx, ry = ry, rz = rz };
        static WalkZone ramp(float x0, float x1, float z0, float z1, string axis, float y0, float y1) => new WalkZone(x0, x1, z0, z1) { ramp = new Ramp(axis, y0, y1) };

        // ---- stair A: switchback core north of the opening; flight 1 rises north, flight 2 returns south ----
        public static void StairA(Kit kit, ShelterShellResult res, ShellAdd add, ShellSolid solid)
        {
            var M = res.M;
            float sx0 = STAIR.x0, sx1 = STAIR.x1, sm = 4.95f, sm1 = 5.05f, zFoot = -17.45f, zLand = -19.97f, zEnd = -21.37f, R = 0.17f, TR = 0.28f;
            void core(float x0, float x1, float z0, float z1, float y0 = 0, float y1 = 6.2f) { add(kit.Slab(x1 - x0, y1 - y0, z1 - z0, M["wall"], at((x0 + x1) / 2, y0, (z0 + z1) / 2))); solid(x0, x1, z0, z1, y0 - 1, y1); }
            core(sx0 - 0.25f, sx0, zEnd - 0.25f, ROOM.z0); core(sx1, sx1 + 0.25f, zEnd - 0.25f, ROOM.z0); core(sx0 - 0.25f, sx1 + 0.25f, zEnd - 0.25f, zEnd);
            core(sm, sm1, zLand, zFoot);                                                      // wall between the flights
            add(kit.Plane(sx1 - sx0, -16.25f - zEnd, M["step"], at(5, 0.002f, (zEnd - 16.25f) / 2)));
            add(kit.Slab(sx1 - sx0 + 0.5f, 0.25f, -16 - zEnd + 0.25f, M["ceil"], at(5, 6.2f, (zEnd - 16) / 2)));
            for (int k = 1; k <= 9; k++)
            {
                float z1 = zFoot - TR * (k - 1), z2 = zLand + TR * (k - 1);
                add(kit.Box(sm - sx0, R * k, TR, M["step"], at((sx0 + sm) / 2, 0, z1 - TR / 2)));
                add(kit.Box(sm - sx0, 0.025f, 0.05f, M["dark"], at((sx0 + sm) / 2, R * k, z1 - 0.03f)));
                add(kit.Box(sx1 - sm1, R + 0.2f, TR, M["step"], at((sm1 + sx1) / 2, 1.7f + R * k - R - 0.2f, z2 + TR / 2)));
                add(kit.Box(sx1 - sm1, 0.025f, 0.05f, M["dark"], at((sm1 + sx1) / 2, 1.7f + R * k, z2 + 0.03f)));
            }
            add(kit.Box(sx1 - sx0, 1.7f, zLand - zEnd, M["step"], at(5, 0, (zLand + zEnd) / 2)));               // landing
            add(kit.Box(sx1 - sx0, 0.22f, -16.25f - zFoot, M["step"], at(5, 3.4f - 0.22f, (zFoot - 16.25f) / 2))); // top landing
            add(kit.Box(sx1 - sm1, 3.0f, 0.08f, M["wall"], at((sm1 + sx1) / 2, 0, zFoot + 0.04f)));                // closes the space under flight 2
            res.solids.Add(new Solid(sm1, sx1, zLand, zFoot + 0.1f, -1, 1.6f));
            foreach (var y in new[] { 3.9f, 4.5f }) add(Rod(V(sx0, y, zFoot - 0.04f), V(sm, y, zFoot - 0.04f), 0.022f, M["stainless"])); // top landing guard over flight 1
            solid(sx0, sm1, zFoot - 0.12f, zFoot, 3.2f, 4.6f);
            foreach (var h in new[] { new[] { sx0 + 0.06f, 0.9f, zFoot, 2.6f, zLand }, new[] { sm - 0.06f, 0.9f, zFoot, 2.6f, zLand }, new[] { sx1 - 0.06f, 2.6f, zLand, 4.3f, zFoot }, new[] { sm1 + 0.06f, 2.6f, zLand, 4.3f, zFoot } })
            {
                float x = h[0], ya = h[1], za = h[2], yb = h[3], zb = h[4];
                if (ya < 1) add(Rod(V(x, ya, za + 0.3f), V(x, ya, za), 0.022f, M["stainless"]));
                add(Rod(V(x, ya, za), V(x, yb, zb), 0.022f, M["stainless"]));
            }
            add(kit.Box(0.06f, 2.1f, 0.95f, M["door"], at(sx1 - 0.03f, 3.4f, -16.85f)));                        // exit door at the top
            add(kit.Box(0.05f, 0.05f, 0.7f, M["metal"], at(sx1 - 0.09f, 4.45f, -16.85f)));
            res.walk.Add(new WalkZone(sx0, sx1, zFoot, ROOM.z0, 0));
            res.walk.Add(ramp(sx0, sm, zLand, zFoot, "z", 1.7f, 0));
            res.walk.Add(new WalkZone(sx0, sx1, zEnd, zLand, 1.7f));
            res.walk.Add(ramp(sm1, sx1, zLand, zFoot, "z", 1.7f, 3.4f));
            res.walk.Add(new WalkZone(sx0, sx1, zFoot, -16.25f, 3.4f));
            add(kit.Box(sx1 - sx0 + 0.5f, 0.3f, 0.32f, M["dark"], at(5, 2.2f, -15.84f)));                      // rolled-up fire shutter box
            foreach (var x in new[] { sx0 - 0.08f, sx1 + 0.08f }) add(kit.Box(0.08f, 2.2f, 0.1f, M["steel"], at(x, 0, -15.95f)));
            res.fixtures.Add(new ShelterFixture { x = 5, y = 6.05f, z = -18.7f, ry = PI / 2 });
            res.fixtures.Add(new ShelterFixture { x = 5, y = 6.05f, z = -16.9f, ry = PI / 2 });
            res.lights.Add(new ShelterLamp { pos = V(5, 5.2f, -18.6f), color = 0xe8f2ff, @base = 9, dist = 9, circuit = "A" });
        }

        // ---- ramp B: car ramp rising south to the street, anti-slip rings, chevron walls, barrier at the top ----
        public static void RampB(Kit kit, ShelterShellResult res, ShellAdd add, ShellSolid solid, ShellSlopeZ slopeZ)
        {
            var M = res.M;
            float rz0 = 16.25f, rz1 = 36, rTop = 3.4f, rIn = 0.0f;
            float ry(float z) => rIn + (z - rz0) / (rz1 - rz0) * rTop;
            foreach (var w in new[] { new[] { RAMP.x0 - 0.25f, RAMP.x0 }, new[] { RAMP.x1, RAMP.x1 + 0.25f } }) { add(kit.Slab(w[1] - w[0], 6.6f, rz1 - ROOM.z1, M["wall"], at((w[0] + w[1]) / 2, 0, (ROOM.z1 + rz1) / 2))); solid(w[0], w[1], ROOM.z1, rz1); }
            slopeZ(RAMP.x0, RAMP.x1, rz0, 0, rz1, rTop, 0.3f, M["groove"]);
            slopeZ(RAMP.x0 - 0.25f, RAMP.x1 + 0.25f, rz0, 2.75f, rz1, rTop + 2.75f, 0.3f, M["ceil"]);
            foreach (var x in new[] { RAMP.x0 + 0.012f, RAMP.x1 - 0.012f }) ShelterUtil.NoShadow(slopeZ(x - 0.01f, x + 0.01f, rz0, 0.55f, rz1, rTop + 0.55f, 0.5f, M["hazard"]));
            add(kit.Box(RAMP.x1 - RAMP.x0, 0.07f, 0.4f, M["hazard"], at(11, 0, 15.2f)));                            // speed bump
            add(Rod(V(RAMP.x0 + 0.2f, 2.32f, 15.75f), V(RAMP.x1 - 0.2f, 2.32f, 15.75f), 0.07f, M["hazard"], 12)); // height bar
            foreach (var x in new[] { RAMP.x0 + 0.5f, RAMP.x1 - 0.5f }) add(Rod(V(x, 2.38f, 15.75f), V(x, 2.5f, 15.75f), 0.012f, M["steel"]));
            var hb = add(kit.Sign(new[] { "높이제한 2.3m" }, new SignOpts { w = 1.2f, h = 0.3f, bg = "#e9b21c", fg = "#151515", single = true })); J.SetPos(hb, 11, 2.32f, 15.66f); J.SetYaw(hb, PI);
            var mirror = add(Geo.Obj(Geo.Cylinder(0.42f, 0.42f, 0.06f, 28), Mat.Lit(new MatSpec { color = J.Hex(0xdfe6ea), metalness = 1, roughness = 0.04f }), "mirror", false, false));
            var mirrorPos = V(RAMP.x1 - 0.3f, 2.2f, 15.5f);
            J.SetPos(mirror, mirrorPos); J.SetRot(mirror, PI / 2, 0, 0.6f);
            J.SetPos(add(Geo.Obj(Geo.Torus(0.43f, 0.035f, 8, 28), kit.Mat("paint:#f07a1a"), "mirror-rim", false, false)), mirrorPos + V(0, 0, 0.02f));
            // barrier gate at the top (pedestrians stop here in this level)
            float bz = 31, by = ry(bz);
            add(kit.Box(0.4f, 1.05f, 0.32f, kit.Mat("paint:#e9e6dc"), at(RAMP.x1 - 0.45f, by, bz)));
            add(Rod(V(RAMP.x1 - 0.5f, by + 0.92f, bz), V(RAMP.x0 + 0.3f, by + 0.92f, bz), 0.045f, kit.Mat("paint:#d23a33")));   // the arm
            for (int i = 0; i < 5; i++) add(kit.Box(0.3f, 0.1f, 0.1f, kit.Mat("paint:#f4f2ec"), at(RAMP.x1 - 1.2f - i * 1.0f, by + 0.87f, bz)));
            var inSign = add(kit.Sign(new[] { "입차" }, new SignOpts { w = 0.5f, h = 0.25f, bg = "#05140a", fg = "#56f27a", emissive = 1.4f, single = true }));
            J.SetPos(inSign, RAMP.x1 - 0.45f, by + 1.75f, bz - 0.17f); J.SetYaw(inSign, PI); kit.Lit("signs", ShelterUtil.MatOf(inSign), 1.4f);
            add(Rod(V(RAMP.x1 - 0.45f, by, bz + 0.25f), V(RAMP.x1 - 0.45f, by + 1.6f, bz + 0.25f), 0.04f, M["steel"]));
            solid(RAMP.x0, RAMP.x1, bz - 0.2f, bz + 0.2f);
            res.walk.Add(ramp(RAMP.x0, RAMP.x1, ROOM.z1, bz, "z", 0, ry(bz)));
            // night outside the top opening: follows the street lights (dark in a blackout)
            var night = Mat.Lit(new MatSpec { color = J.Hex(0x000000), emissive = J.Hex(0x2a3c52), emissiveIntensity = 0.5f, roughness = 1 });
            kit.Lit("street", night, 0.5f);
            var sky = add(Geo.Obj(Geo.Plane(RAMP.x1 - RAMP.x0, 3), night, "night", false, false)); J.SetPos(sky, 11, rTop + 1.5f, rz1 - 0.05f); J.SetYaw(sky, PI);
            foreach (var z in new float[] { 20, 26 }) res.fixtures.Add(new ShelterFixture { x = 11, y = ry(z) + 2.6f, z = z, ry = 0, rx = Mathf.Atan2(rTop, rz1 - rz0) });
        }

        // ---- ramp C: accessible ramp 1:12 north, landing every 0.75 m rise, handrails both sides, turn east at the top ----
        public static void RampC(Kit kit, ShelterShellResult res, ShellAdd add, ShellSolid solid, ShellSlopeZ slopeZ, ShellSlopeX slopeX, ShellTactile tactile)
        {
            var M = res.M;
            float ax0 = ACCESS.x0, ax1 = ACCESS.x1; var az = new[] { -16.25f, -25, -26.5f, -35.5f, -38.5f };
            void aw(float x0, float x1, float z0, float z1, float y1 = 5.2f) { add(kit.Slab(x1 - x0, y1, z1 - z0, M["wall"], at((x0 + x1) / 2, 0, (z0 + z1) / 2))); solid(x0, x1, z0, z1); }
            aw(ax0 - 0.25f, ax0, az[4] - 0.25f, ROOM.z0); aw(ax1, ax1 + 0.25f, az[3], ROOM.z0); aw(ax0 - 0.25f, ax1 + 0.25f, az[4] - 0.25f, az[4]);
            slopeZ(ax0, ax1, az[1], 0.75f, az[0], 0, 0.3f, M["rubber"]);
            add(kit.Box(ax1 - ax0, 0.75f, az[1] - az[2], M["rubber"], at((ax0 + ax1) / 2, 0, (az[1] + az[2]) / 2)));
            slopeZ(ax0, ax1, az[3], 1.5f, az[2], 0.75f, 0.3f, M["rubber"]);
            add(kit.Box(ax1 - ax0, 1.5f, az[3] - az[4], M["rubber"], at((ax0 + ax1) / 2, 0, (az[3] + az[4]) / 2)));
            foreach (var c in new[] { new[] { az[1], az[0], 3.0f }, new[] { az[3], az[1], 3.8f }, new[] { az[4] - 0.25f, az[3], 4.2f } }) add(kit.Slab(ax1 - ax0 + 0.5f, 0.25f, c[1] - c[0], M["ceil"], at((ax0 + ax1) / 2, c[2], (c[0] + c[1]) / 2)));
            foreach (var c in new[] { new[] { az[1], 3.0f, 3.8f }, new[] { az[3], 3.8f, 4.2f } }) add(kit.Box(ax1 - ax0, c[2] - c[1], 0.2f, M["ceil"], at((ax0 + ax1) / 2, c[1], c[0])));
            tactile(ax0, ax1, az[1] - 0.6f, az[1], 0.758f); tactile(ax0, ax1, az[2], az[2] + 0.3f, 0.758f); tactile(ax0, ax1, az[3] - 0.6f, az[3], 1.508f);
            var railPts = new[] { new[] { az[0] + 0.3f, 0 }, new[] { az[0], 0 }, new[] { az[1], 0.75f }, new[] { az[2], 0.75f }, new[] { az[3], 1.5f }, new[] { az[4] + 0.2f, 1.5f } };
            foreach (var x in new[] { ax0 + 0.07f, ax1 - 0.07f }) foreach (var h in new[] { 0.85f, 0.65f }) for (int i = 0; i < railPts.Length - 1; i++)
                    {
                        if (x > ax1 - 0.1f && railPts[i + 1][0] < az[3] - 0.1f) continue;   // east side opens at the turn
                        add(Rod(V(x, railPts[i][1] + h, railPts[i][0]), V(x, railPts[i + 1][1] + h, railPts[i + 1][0]), 0.02f, M["stainless"], 8));
                    }
            // the turn east at the top landing and the start of the next ramp
            aw(ax1, -7.3f, az[4] - 0.25f, az[4]); aw(ax1, -7.3f, az[3], az[3] + 0.25f); aw(-7.55f, -7.3f, az[4], az[3]);
            slopeX(ax1, 1.5f, -7.55f, 1.83f, az[4], az[3], 0.3f, M["rubber"]);
            add(kit.Slab(-7.3f - ax1, 0.25f, 3.5f, M["ceil"], at((ax1 - 7.3f) / 2, 4.3f, (az[3] + az[4]) / 2)));
            foreach (var z in new[] { az[4] + 0.4f, az[3] - 0.4f }) add(kit.Cyl(0.03f, 0.03f, 0.95f, M["stainless"], at(ax1 + 0.15f, 1.5f, z)));
            add(Rod(V(ax1 + 0.15f, 2.38f, az[4] + 0.4f), V(ax1 + 0.15f, 2.38f, az[3] - 0.4f), 0.025f, kit.Mat("paint:#c8261f"), 6));
            res.solids.Add(new Solid(ax1, ax1 + 0.4f, az[4], az[3], 0, 5));
            res.walk.Add(ramp(ax0, ax1, az[1], ROOM.z0, "z", 0.75f, 0));
            res.walk.Add(new WalkZone(ax0, ax1, az[2], az[1], 0.75f));
            res.walk.Add(ramp(ax0, ax1, az[3], az[2], "z", 1.5f, 0.75f));
            res.walk.Add(new WalkZone(ax0, ax1, az[4], az[3], 1.5f));
            res.fixtures.Add(new ShelterFixture { x = -13, y = 2.92f, z = -20.5f, ry = PI / 2 });
            res.fixtures.Add(new ShelterFixture { x = -13, y = 3.72f, z = -31, ry = PI / 2 });
            res.fixtures.Add(new ShelterFixture { x = -9.6f, y = 4.22f, z = -37, ry = 0 });
            res.lights.Add(new ShelterLamp { pos = V(-13, 2.9f, -27), color = 0xf0f4ff, @base = 8, dist = 14, circuit = "A" });
        }

        // ---- east core: lift hall behind the open fire doors, stairs up to the lobby, toilets ----
        public static void EastCore(Kit kit, ShelterShellResult res, ShellAdd add, ShellSolid solid)
        {
            var M = res.M;
            float ex0 = ROOM.x1 + 0.25f, ex1 = 27;
            add(kit.Plane(ex1 - ex0, 8, M["tile"], at((ex0 + ex1) / 2, 0.003f, 0)));
            add(kit.Plane(0.25f, EAST.z1 - EAST.z0, M["step"], at(ROOM.x1 + 0.125f, 0.003f, 0)));
            foreach (var w in new[] { new[] { ex0, ex1 + 0.25f, -4.25f, -4 }, new[] { ex0, ex1 + 0.25f, 4, 4.25f }, new[] { ex1, ex1 + 0.25f, -4, 4 } }) { add(kit.Slab(w[1] - w[0], 2.9f, w[3] - w[2], M["gyp"], at((w[0] + w[1]) / 2, 0, (w[2] + w[3]) / 2))); solid(w[0], w[1], w[2], w[3]); }
            add(kit.Slab(ex1 - ex0 + 0.25f, 0.2f, 8.5f, M["gyp"], at((ex0 + ex1) / 2, 2.6f, 0)));
            res.walk.Add(new WalkZone(ROOM.x1, ex1, -4, 4, 0));
            foreach (var s in new[] { -1f, 1f })
            { // fire-door leaves held open against the hall walls
                add(kit.Box(0.05f, 2.1f, 1.08f, M["door"], at(ex0 + 0.04f, 0, s * (EAST.z1 + 0.56f))));
                add(kit.Box(0.04f, 0.04f, 0.3f, M["metal"], at(ex0 + 0.09f, 1.05f, s * (EAST.z1 + 0.85f))));
            }
            add(kit.Box(0.06f, 2.35f, 1.5f, M["stainless"], at(ex1 - 0.03f, 0, -1.6f)));                            // lift frame + doors
            foreach (var dz in new[] { -0.28f, 0.28f }) add(kit.Box(0.04f, 2.1f, 0.54f, kit.Mat("metal", new MatSpec { roughness = 0.3f, color = J.Hex(0xb8bcc0) }), at(ex1 - 0.06f, 0, -1.6f + dz)));
            var liftTex = kit.SignTexture(new[] { "▼ B1" }, new SignStyle { w = 256, h = 96, bg = "#0b0b0b", fg = "#ff5a36" });
            var disp = Mat.Lit(new MatSpec { map = liftTex, emissive = J.Hex(0xffffff), emissiveMap = liftTex, emissiveIntensity = 1.2f });
            var dispM = add(Geo.Obj(Geo.Plane(0.42f, 0.16f), disp, "lift-display", false, false)); J.SetPos(dispM, ex1 - 0.07f, 2.48f, -1.6f); J.SetYaw(dispM, -PI / 2);
            var btn = Mat.Lit(new MatSpec { color = J.Hex(0x222222), emissive = J.Hex(0x9fd4ff), emissiveIntensity = 1.1f });
            add(kit.Box(0.03f, 0.22f, 0.1f, M["stainless"], at(ex1 - 0.05f, 1.0f, -0.55f))); add(kit.Cyl(0.025f, 0.025f, 0.02f, btn, at(ex1 - 0.07f, 1.1f, -0.55f, rz: PI / 2)));
            res.lift.Add(disp); res.lift.Add(btn);
            var up = add(kit.Sign(new[] { "계단 · 1층 로비" }, new SignOpts { w = 1.1f, h = 0.26f, bg = "#1f4e8c", single = true })); J.SetPos(up, 24.6f, 2.32f, -3.98f);
            var wc = add(kit.Sign(new[] { "화장실" }, new SignOpts { w = 0.7f, h = 0.26f, bg = "#3a4048", single = true })); J.SetPos(wc, 24.6f, 2.32f, 3.98f); J.SetYaw(wc, PI);
            add(kit.Box(1.0f, 2.1f, 0.05f, M["door"], at(24.6f, 0, -3.97f))); add(kit.Box(1.0f, 2.1f, 0.05f, kit.Mat("paint:#cfd6da"), at(24.6f, 0, 3.97f)));
            res.fixtures.Add(new ShelterFixture { x = 24.6f, y = 2.58f, z = 0, ry = PI / 2, panel = true });
            res.lights.Add(new ShelterLamp { pos = V(24.6f, 2.3f, 0), color = 0xfff4e6, @base = 5, dist = 8, circuit = "A" });
        }
    }
}
