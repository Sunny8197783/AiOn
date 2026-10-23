// Ports story/cutscenes.js: the opening over 연무동, time passing over the 한빛아파트 complex, the D-day strike,
// the blackout and the air-raid siren. Each resolves 'done' or 'skipped'.
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.Systems
{
    public static class Cutscenes
    {
        public static Task<string> Opening(Ctx ctx) => throw new NotImplementedException("port: story/cutscenes.js:opening");
        // Stands in for the time-skip card; text: one caption or several, shown one after another.
        public static Task<string> Timelapse(Ctx ctx, string from, string to, params string[] text) => throw new NotImplementedException("port: story/cutscenes.js:timelapse");
        // onImpact runs at the blast (the story switches the town to strike 'hit').
        public static Task<string> StrikeScene(Ctx ctx, Action onImpact) => throw new NotImplementedException("port: story/cutscenes.js:strikeScene");
        // cut runs when the lights go out for good.
        public static Task<string> BlackoutScene(Ctx ctx, Action cut) => throw new NotImplementedException("port: story/cutscenes.js:blackoutScene");
        public static Task<string> SirenScene(Ctx ctx) => throw new NotImplementedException("port: story/cutscenes.js:sirenScene");
    }
}
