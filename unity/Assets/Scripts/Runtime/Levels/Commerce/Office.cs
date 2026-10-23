// Ports levels/office.js: 한결물류 연무센터 운영팀 — two desk islands, the player's desk with the director-drawn
// dashboard, 윤 팀장 (NPC 'coworker', seated), a 2x2 video wall with the route map, whiteboard, ribbon window over
// the yard. States: alerts (0..3 rows red) dashboard (a Paint the prologue draws; never saved in the snapshot)
// outside (null | mood).
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public static class Office
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("office", (kit, people, fx) => BuildOffice(kit, people));

        public static Task<Level> BuildOffice(Kit kit, IPeople people) => throw new NotImplementedException("port: levels/office.js:buildOffice");
    }
}
