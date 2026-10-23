// Ports story/danger.js: war in the streets while roaming the town after the war started — shells (whistle,
// blast, debris, crater, fire), collapsing facades, desperate people demanding food. Intensity per chapter.
using System;
using Gyeongbo.Core;
using Gyeongbo.Engine;

namespace Gyeongbo.Systems
{
    public sealed class Danger
    {
        // hurt(damage, why) is the app's: it lowers health and records the death cause.
        public static Danger Create(Ctx ctx, Actions actions, Action<float, string> hurt) => throw new NotImplementedException("port: story/danger.js:createDanger");
        // engine/actions.js hit someone: a looter's hits are self-defence (not counted as violence).
        public bool IsHostile(IPerson p) => throw new NotImplementedException("port: story/danger.js:isHostile");
        // Force an event now: kind 'blast' | 'boom' (harmless, size) | 'shell' | 'collapse' | 'looter'. JS space.
        public void Trigger(string kind, float x, float z, float size = 1) => throw new NotImplementedException("port: story/danger.js:trigger");
        public void Reset() => throw new NotImplementedException("port: story/danger.js:reset");
    }
}
