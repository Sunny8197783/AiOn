// Ports the stand-ins in engine/core.js: capsule people and no-op fx, used when the real implementations
// fail to start so the game keeps running.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Engine
{
    // core.js capsulePerson / emptyCrowd behind IPeople.
    public sealed class CapsulePeople : IPeople
    {
        public bool Fallback => true;
        public IPerson CreatePerson(string preset = "crowd", int? seed = null) => throw new NotImplementedException("port: engine/core.js:capsulePerson");
        public ICrowd CreateCrowd(CrowdOptions o) => throw new NotImplementedException("port: engine/core.js:emptyCrowd");
    }

    // core.js stubFx behind IFx.
    public sealed class StubFx : IFx
    {
        public bool Stub => true;
        public IFire Fire(FireOptions o) => throw new NotImplementedException("port: engine/core.js:stubFx");
        public ISmoke Smoke(SmokeOptions o) => throw new NotImplementedException("port: engine/core.js:stubFx");
        public IRain Rain(GameObject scene, Camera camera, RainOptions o = null) => throw new NotImplementedException("port: engine/core.js:stubFx");
        public IDust Dust(DustOptions o) => throw new NotImplementedException("port: engine/core.js:stubFx");
        public ISpray ExtinguisherSpray(SprayOptions o) => throw new NotImplementedException("port: engine/core.js:stubFx");
        public bool ReduceMotion => false;
        public void SetReduceMotion(bool v) { }
    }
}
