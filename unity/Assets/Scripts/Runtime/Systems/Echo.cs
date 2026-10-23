// Ports story/echo.js: 전 회차의 나 — the path you walked comes back as a pale ghost, where you fell is marked,
// chalk notes (B) stay on the ground. Kept with Core.Store (key gyeongbo.echo.v1), last 3 runs.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;

namespace Gyeongbo.Systems
{
    public sealed class EchoRun
    {
        public List<EchoTrailPoint> trail = new List<EchoTrailPoint>();
        public List<EchoMark> deaths = new List<EchoMark>();
        public List<EchoMark> notes = new List<EchoMark>();
    }

    // trail entries are [levelId, x, z] in JS.
    public sealed class EchoTrailPoint
    {
        public string lv;
        public float x, z;
    }

    // deaths { lv, x, z, why, yaw } and notes { lv, x, z, text, yaw }.
    public sealed class EchoMark
    {
        public string lv, why, text;
        public float x, z, yaw;
    }

    public sealed class EchoData
    {
        public List<EchoRun> runs = new List<EchoRun>();
    }

    public sealed class Echo
    {
        // Saved runs come from storage: keep only well-formed entries (echo.test.mjs).
        public static EchoData ReadEcho(string raw) => throw new NotImplementedException("port: story/echo.js:readEcho");

        public static Echo Create(GameEngine engine, Ctx ctx) => throw new NotImplementedException("port: story/echo.js:createEcho");
        // A new game: the run being recorded becomes an echo.
        public void Begin() => throw new NotImplementedException("port: story/echo.js:begin");
        // Continuing a save keeps recording into the same run.
        public void Resume() => throw new NotImplementedException("port: story/echo.js:resume");
        public void Death(string why) => throw new NotImplementedException("port: story/echo.js:death");
        // B while roaming: write on the ground (asks through ctx.Say).
        public Task Chalk() => throw new NotImplementedException("port: story/echo.js:chalk");
    }
}
