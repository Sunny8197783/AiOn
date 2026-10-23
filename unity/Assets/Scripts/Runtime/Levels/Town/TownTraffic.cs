// Ports levels/town-traffic.js: cars on TownPlan.LANES obeying the signal cycle (TownPlan.SignalAt), signal
// heads, the cars as moving solids. Modes normal | light | none.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
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
        public static TownTrafficResult BuildTraffic(Kit kit) => throw new NotImplementedException("port: levels/town-traffic.js:buildTraffic");
    }
}
