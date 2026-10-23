// Ports levels/town-strike.js: the D-day missile strike on a background villa south of 연무공원 — the intact
// villa is hidden and replaced by a gutted shell, a rubble heap, fires in the openings and a leaning smoke column.
using System;
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
        public static TownStrikeResult BuildStrike(Kit kit, IFx fx, StrikeSite strike) => throw new NotImplementedException("port: levels/town-strike.js:buildStrike");
    }
}
