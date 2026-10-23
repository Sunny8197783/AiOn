// Ports levels/town-events.js: what the story switches on in town — closure barricades with guides and
// blinking lights, the emergency water point with its queue, the evacuation buses and the assembly area.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class TownEventsResult
    {
        public GameObject group;
        public List<Solid> solids = new List<Solid>();
        // Applies closure ids; returns the ones now active.
        public Func<List<string>, List<string>> setClosures;
        public Action<bool> setWater, setBuses;
        public Action<float, float, FrameCtx> update;
        // One point per closure guide (id "closure_<closureId>", enabled while that closure is active).
        public List<Point> closurePoints = new List<Point>();
        public List<IPerson> guides = new List<IPerson>();
        public Func<List<string>> active;
    }

    public sealed class AssemblySite
    {
        public XZ queue;
        public RectXZ area;
    }

    public static class TownEvents
    {
        public static readonly float WATER_TAP_Z = -19.6f;
        public static readonly XZ WATER_QUEUE = new XZ(-78.6f, -18.4f);
        public static readonly List<XZ> BUS = new List<XZ> { new XZ(-69.5f, -40.5f), new XZ(-65f, -40.5f) };
        public static readonly AssemblySite ASSEMBLY = new AssemblySite { queue = new XZ(-62.6f, -44.6f), area = new RectXZ(-61.8f, -58.6f, -49f, -40f) };

        public static TownEventsResult BuildEvents(Kit kit, IPeople people, List<WalkZone> walk) => throw new NotImplementedException("port: levels/town-events.js:buildEvents");
    }
}
