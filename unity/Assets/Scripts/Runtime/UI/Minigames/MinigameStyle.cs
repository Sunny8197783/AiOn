// Ports ui/minigames.css: the .mg-* classes, registered as UiStyle rules.
using System;

namespace Gyeongbo.UI
{
    public static class MinigameStyle
    {
        // Registers every minigames.css rule with UiStyle (idempotent; the minigames call it before building).
        public static void Register() => throw new NotImplementedException("port: ui/minigames.css");
    }
}
