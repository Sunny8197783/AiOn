// Ports engine/crowd.js: seeded neighbourhood people moving along polylines with separation steering, a
// keep-right lane, player avoidance and distance LOD. Modes stroll | brisk | queue | sit | idle | none
// (see the JS header for every option). Paths, areas and spots are JS space.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.People
{
    public sealed class Crowd : ICrowd
    {
        public static readonly string[] MODES = { "stroll", "brisk", "queue", "sit", "idle", "none" };

        public static Crowd Create(CrowdOptions o) => throw new NotImplementedException("port: engine/crowd.js:createCrowd");

        public GameObject Group => throw new NotImplementedException("port: engine/crowd.js:group");
        public void Update(float dt, FrameCtx ctx = null) => throw new NotImplementedException("port: engine/crowd.js:update");
        public void SetMode(string mode, CrowdModeOptions o = null) => throw new NotImplementedException("port: engine/crowd.js:setMode");
        public void SetPaths(List<List<XZ>> paths) => throw new NotImplementedException("port: engine/crowd.js:setPaths");
        public void SetArea(RectXZ area) => throw new NotImplementedException("port: engine/crowd.js:setArea");
        public string Mode => throw new NotImplementedException("port: engine/crowd.js:mode");
        public int Active => throw new NotImplementedException("port: engine/crowd.js:active");
        public IReadOnlyList<IPerson> Members => throw new NotImplementedException("port: engine/crowd.js:members");
        public void Dispose() => throw new NotImplementedException("port: engine/crowd.js:dispose");
    }
}
