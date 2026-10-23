// Ports levels/shelter.js: the 연무 생활문화센터 underground car park as a civil-defence shelter — shell,
// services (lights, fans, CCTV, a faulty tube), the camp (mats, reception, notice board, radio corner,
// pallets), the people, the fire corner, points, spawns and anchors. Runs on its own generator (power).
// States: crowd (arriving|settled|leaving) phase (alert|calm) power (grid|emergency) fire (none|large)
// boardText (null | lines) ways (non-empty subset of ShelterPeople.ENTRANCES).
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public static class Shelter
    {
        public static readonly XZ FIRE = new XZ(-18.6f, -9.2f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("shelter", BuildShelter);

        public static Task<Level> BuildShelter(Kit kit, IPeople people, IFx fx) => throw new NotImplementedException("port: levels/shelter.js:buildShelter");
    }
}
