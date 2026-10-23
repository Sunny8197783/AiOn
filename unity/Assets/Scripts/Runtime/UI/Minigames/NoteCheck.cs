// Ports notecheck.js: the 3D pieces of the "다시 읽기" scene — scripted camera (서연's eye, a crane view),
// 정민's walked path, labelled pins and links between them. The step order and text live in Story/Seoyeon.
// Positions are JS space.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.UI
{
    public sealed class NoteCheck
    {
        // start: 서연's position and yaw (JS space).
        public static NoteCheck CreateReread(GameEngine world, Spawn start, bool reduceMotion = false) => throw new NotImplementedException("port: notecheck.js:createReread");
        // First person at the start, easing the gaze to pitch.
        public void Eye(float pitch) => throw new NotImplementedException("port: notecheck.js:eye");
        public void Crane(Vector3? to = null, Vector3? target = null) => throw new NotImplementedException("port: notecheck.js:crane");
        public void ShowPath(IList<XZ> points) => throw new NotImplementedException("port: notecheck.js:showPath");
        // A numbered pin at `at` with what the message said (was) and what holds now (now).
        public void Pin(int n, Vector3 at, string was, string now, float scale = 2.6f) => throw new NotImplementedException("port: notecheck.js:pin");
        public void Link(Vector3 a, Vector3 b) => throw new NotImplementedException("port: notecheck.js:link");
        public void Dispose() => throw new NotImplementedException("port: notecheck.js:dispose");
    }
}
