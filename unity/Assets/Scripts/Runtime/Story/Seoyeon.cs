// Ports story/seoyeon.js: the 서연 interlude — 정민 writes a message from the shelter (UI Compose), the point of
// view switches to 서연 at 카페 쉼표, the re-read of the message against what 정민 actually did (NoteCheck pins,
// Relay.BuildPins), then 서연's own walk to an entrance she can use.
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.Story
{
    public static class Seoyeon
    {
        // 서연's start in town (JS space).
        public static readonly Spawn CAFE = new Spawn(-118, 0, -12.5f, (float)Math.PI);

        public static Task SeoyeonRelay(Ctx ctx) => throw new NotImplementedException("port: story/seoyeon.js:seoyeonRelay");
    }
}
