// The people half of levels/mart.js: shoppers wander the aisles (stepping aside for nobody, stopping for the player),
// a queue forms at the checkouts when busy.
using System;
using System.Collections.Generic;
using System.Linq;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;
using static Gyeongbo.Levels.CommerceGoods;

namespace Gyeongbo.Levels
{
    sealed class MartShopper
    {
        public IPerson p; public bool cart; public string walk; public float speed, off, wait; public float? face; public bool blocked;
        public List<MartStop> path = new List<MartStop>();
    }
    sealed class MartStop { public float x, z, wait; public float? face; }

    sealed class MartShoppers
    {
        static readonly float[] AISLES = Mart.AISLES;
        readonly Func<float> r;
        public readonly List<MartShopper> shoppers;
        public readonly List<IPerson> queue;
        public readonly int nNormal, nBusy;

        public MartShoppers(Kit kit, IPeople people, Func<float> r, GameObject dyn)
        {
            this.r = r;
            float q = kit.Quality == "low" ? 0.5f : kit.Quality == "medium" ? 0.75f : 1;
            nNormal = Round(6 * q); nBusy = Round(12 * q);
            shoppers = Enumerable.Range(0, nBusy).Select(i =>
            {
                bool cart = i % 3 == 0; float x = AISLES[(int)(r() * AISLES.Length)], z = r() < 0.5f ? Mart.FRONT : Mart.BACK;
                var p = Npc(people, "crowd", 300 + i, new NpcAt { x = x, z = z, ry = r() * 6, props = new List<string> { cart ? "cart" : "basket" } });
                // 'walk' for everyone: the 'walker' anim would auto-show a rollator; the cart prop alone sets the grip.
                var s = new MartShopper { p = p, cart = cart, walk = "walk" };
                s.speed = (cart ? 0.75f : 0.85f) + r() * 0.35f; s.off = (r() - 0.5f) * 0.6f; s.wait = r() * 3;
                p.SetSpeed(s.speed); p.SetAnim("idle"); dyn.Add(p.Group); return s;
            }).ToList();
            var Q = new[] { (4.25f, -3.8f, -Mathf.PI / 2), (4.25f, -5.4f, Mathf.PI), (4.3f, -6.0f, Mathf.PI), (4.2f, -6.6f, Mathf.PI), (0.85f, -2.2f, Mathf.PI), (0.75f, -5.8f, Mathf.PI) };
            queue = Q.Take(Round(6 * q)).Select((a, i) =>
            {
                var p = Npc(people, "crowd", 400 + i, new NpcAt { x = a.Item1, z = a.Item2, ry = a.Item3, anim = i == 2 ? "phone" : "idle", props = new List<string> { "basket" } });
                dyn.Add(p.Group); return p;
            }).ToList();
        }

        void Plan(MartShopper s)
        {
            var pos = J.Pos(s.p.Group); bool atFront = Mathf.Abs(pos.z - Mart.FRONT) < Mathf.Abs(pos.z - Mart.BACK);
            float ax = AISLES[(int)(r() * AISLES.Length)] + s.off, z0 = atFront ? Mart.FRONT : Mart.BACK, z1 = atFront ? Mart.BACK : Mart.FRONT;
            float zb = -9.5f - r() * 9; float side = ax > AISLES[6] - 0.5f ? 1 : r() < 0.5f ? -1 : 1;
            s.path = new List<MartStop> { new MartStop { x = ax, z = z0 }, new MartStop { x = ax, z = zb, wait = 2 + r() * 5, face = YawTo(side, 0) }, new MartStop { x = ax, z = z1 } };
        }

        void Step(MartShopper s, float dt, Player pl)
        {
            var g = s.p.Group;
            if (s.wait > 0)
            {
                s.wait -= dt;
                if (s.wait <= 0) s.p.SetAnim(s.walk);
                else if (s.face.HasValue) { float y = J.GetYaw(g); J.SetYaw(g, y + Turn(y, s.face.Value) * Mathf.Min(1, dt * 4)); }
                return;
            }
            if (s.path.Count == 0) Plan(s);
            var t = s.path[0]; var pos = J.Pos(g); float dx = t.x - pos.x, dz = t.z - pos.z, d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < 0.05f) { s.path.RemoveAt(0); if (t.wait > 0) { s.wait = t.wait; s.face = t.face; s.p.SetAnim("idle"); } else s.face = null; return; }
            if (pl != null)
            {
                float px = pl.X - pos.x, pz = pl.Z - pos.z;
                bool block = Mathf.Sqrt(px * px + pz * pz) < 1.15f && px * dx + pz * dz > 0;
                if (block != s.blocked) { s.blocked = block; s.p.SetAnim(block ? "idle" : s.walk); }
                if (block) return;
            }
            float k = Mathf.Min(d, s.speed * dt) / d; pos.x += dx * k; pos.z += dz * k; J.SetPos(g, pos);
            float yaw = J.GetYaw(g); J.SetYaw(g, yaw + Turn(yaw, YawTo(dx, dz)) * Mathf.Min(1, dt * 7));
        }

        public void ApplyCrowd(bool busy)
        {
            for (int i = 0; i < shoppers.Count; i++)
            {
                var s = shoppers[i]; s.p.Group.SetActive(i < (busy ? nBusy : nNormal));
                if (s.p.Group.activeSelf && s.wait == 0 && s.path.Count == 0) s.p.SetAnim(s.walk);
            }
            foreach (var p in queue) p.Group.SetActive(busy);
        }

        public void Update(float dt, Player pl)
        {
            foreach (var s in shoppers) if (s.p.Group.activeSelf) { Step(s, dt, pl); s.p.Update(dt); }
            foreach (var p in queue) if (p.Group.activeSelf) p.Update(dt);
        }
    }
}
