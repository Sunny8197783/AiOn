// Ports levels/store.js: 바로편의점 — storefront with a pull door, window bar, ramen station, gondolas, drink
// fridges, the counter with 이수아 (NPC 'clerk'), battery rack, coffee machine, ATM; emergency units when unpowered.
// States: soldOut (item-group lists) power (on|off) outside (null | mood); see setState for the clerk/counter label.
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public static class Store
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => LevelRegistry.Register("store", (kit, people, fx) => BuildStore(kit, people));

        public static Task<Level> BuildStore(Kit kit, IPeople people) => throw new NotImplementedException("port: levels/store.js:buildStore");
    }
}
