// Ports levels/town.js: 연무동 — assembles ground, buildings, park, props, fences, fronts, the shelter site,
// events, traffic, the strike and the crowd; points from game/townmap.js PLACES plus vending machines and the
// bus stop; spawns; NPC guides; notice-board texts; states and per-frame update.
// States: traffic (normal|light|none) crowd (TownCrowd.CROWD_MODES) closures (string list of CLOSURES ids)
// gates ({ east|stairs|ramp|access: open|closed }) waterPoint buses (on|off) notices (none|dday|water|evac)
// noticeText ({ boardId: lines | null }) strike (none|hit) season (autumn).
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public static class Town
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("town", BuildTown);

        public static Task<Level> BuildTown(Kit kit, IPeople people, IFx fx) => throw new NotImplementedException("port: levels/town.js:buildTown");
    }
}
