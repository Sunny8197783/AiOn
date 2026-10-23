// Ports levels/town-crowd.js: the town's crowds per mode (none normal busy evacuating sparse queue assembly) on
// the sidewalk paths clipped by the active closures, toward the open shelter entrances, the water queue and the
// assembly point.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownCrowd
    {
        public static readonly string[] CROWD_MODES = { "none", "normal", "busy", "evacuating", "sparse", "queue", "assembly" };

        public static TownCrowd Create(IPeople people, GameObject group, List<WalkZone> walk, string quality = "high") => throw new NotImplementedException("port: levels/town-crowd.js:createTownCrowd");
        // Returns the mode now in force (an unknown mode keeps the old one).
        public string SetMode(string mode) => throw new NotImplementedException("port: levels/town-crowd.js:setMode");
        // Closures and gates changed: re-route the walkers. null = unchanged.
        public void SetContext(List<string> closures = null, System.Collections.Generic.Dictionary<string, string> gates = null) => throw new NotImplementedException("port: levels/town-crowd.js:setContext");
        public void Update(float dt, FrameCtx ctx) => throw new NotImplementedException("port: levels/town-crowd.js:update");
        public string Mode => throw new NotImplementedException("port: levels/town-crowd.js:mode");
    }
}
