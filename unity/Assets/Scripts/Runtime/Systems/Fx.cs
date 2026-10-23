// Ports engine/fx.js: particle effects — fire, smoke, rain, dust motes, extinguisher powder — with procedural
// textures and camera-facing billboards. Implements Core.IFx; the engine creates it with Fx.Create.
using System;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Systems
{
    public sealed class Fx : IFx
    {
        public static Fx Create(Kit kit, bool reduceMotion = false) => throw new NotImplementedException("port: engine/fx.js:createFx");

        public bool Stub => false;
        public IFire Fire(FireOptions o) => throw new NotImplementedException("port: engine/fx.js:fire");
        public ISmoke Smoke(SmokeOptions o) => throw new NotImplementedException("port: engine/fx.js:smoke");
        public IRain Rain(GameObject scene, Camera camera, RainOptions o = null) => throw new NotImplementedException("port: engine/fx.js:rain");
        public IDust Dust(DustOptions o) => throw new NotImplementedException("port: engine/fx.js:dust");
        public ISpray ExtinguisherSpray(SprayOptions o) => throw new NotImplementedException("port: engine/fx.js:extinguisherSpray");
        public bool ReduceMotion => throw new NotImplementedException("port: engine/fx.js:reduceMotion");
        public void SetReduceMotion(bool v) => throw new NotImplementedException("port: engine/fx.js:setReduceMotion");
    }
}
