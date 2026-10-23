// Ports levels/apartment.js: 한빛아파트 103동 — ground-floor lobby with 관리사무소 (박 소장), the stairwell, the
// 3rd-floor corridor (301-304, hydrant + extinguisher, the hallway-fire spot near 304), 302호 and the view out of
// the windows. Room lamps, motion-sensor lamps and battery emergency lamps share a small pool of point lights.
// States: wallet power stock bag radio hallFire smoke seoyeonDoor candles windowView.
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public static class Apartment
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("apartment", BuildApartment);

        public static Task<Level> BuildApartment(Kit kit, IPeople people, IFx fx) => throw new NotImplementedException("port: levels/apartment.js:buildApartment");
    }
}
