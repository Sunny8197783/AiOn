// Ports levels/mart.js: 연무마트 ground floor — aisles per MART_SECTIONS, checkouts with cashiers and a queue,
// shoppers with carts, the water pallet, the chilled wall, produce, the travelator, the automatic doors.
// States: crowd (normal|busy) soldOut (section id lists) power (on|off) outside (null | mood).
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public static class Mart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("mart", BuildMart);

        public static Task<Level> BuildMart(Kit kit, IPeople people, IFx fx) => throw new NotImplementedException("port: levels/mart.js:buildMart");
    }
}
