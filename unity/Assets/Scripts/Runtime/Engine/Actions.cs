// Ports engine/actions.js: the body in the world — punches (click/Q), picking up and throwing loose things
// (G), crouching (C), thrown debris, people hitting back.
using System;
using Gyeongbo.Core;

namespace Gyeongbo.Engine
{
    public sealed class ActionCallbacks
    {
        public Action<IPerson, string, bool> onHit;      // (person, how: 'punch' | 'throw' | 'debris', knocked out)
        public Action<float, string> onHurt;             // (damage, why: 'fight' | ...)
        public Action<string> onSound;                   // audio.hit name
        public Action<string> onNear;                    // prompt label for a loose thing, or null
    }

    public sealed class Actions
    {
        public static Actions Create(GameEngine engine, ActionCallbacks cb) => throw new NotImplementedException("port: engine/actions.js:createActions");
        // A loose thing flung from (x, y, z) with velocity (vx, vy, vz), JS space. kind: brick | rock | bottle | can | box.
        public void Spawn(string kind, float x, float y, float z, float vx = 0, float vy = 0, float vz = 0) => throw new NotImplementedException("port: engine/actions.js:spawn");
        public string Held => throw new NotImplementedException("port: engine/actions.js:held");
        public void SetEnabled(bool on) => throw new NotImplementedException("port: engine/actions.js:setEnabled");
        // Leaving a level with something in hand: it is dropped where you stand.
        public void Drop() => throw new NotImplementedException("port: engine/actions.js:drop");
    }
}
