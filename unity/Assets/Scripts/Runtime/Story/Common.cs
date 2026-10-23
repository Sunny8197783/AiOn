// Ports story/common.js: scenes several chapters reuse — the radio (portable or the phone FM app), emergency
// texts, official fact notices, the light in your hand, the phone battery.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.Story
{
    public static class Common
    {
        public const string AREA = "[연무구(가상)]";
        // Broadcast scripts by name (normal blackout shelter water rumor crossroads evac); evac depends on G.
        public static readonly Dictionary<string, Func<Game, List<string>>> BROADCASTS = new Dictionary<string, Func<Game, List<string>>>();

        public static bool Has(Game G, string id) => G.items.TryGetValue(id, out var e) && e != null && e.home + e.bag > 0;
        // What the disaster-broadcast station is saying now (the phone radio opened with P).
        public static string CurrentBroadcast(Game G) => throw new NotImplementedException("port: story/common.js:currentBroadcast");
        // The radio scene: heard lines, or null when it could not be played. device 'phone' skips the portable radio.
        public static Task<List<string>> RadioScene(Ctx ctx, string script, string title = "라디오", string device = null) => throw new NotImplementedException("port: story/common.js:radioScene");
        // 'flashlight' when usable, else 'phone' while it has battery, else null.
        public static string LightSource(Ctx ctx) => throw new NotImplementedException("port: story/common.js:lightSource");
        public static Task EmergencyText(Ctx ctx, string kind, string time, string text) => throw new NotImplementedException("port: story/common.js:emergencyText");
        // Official guidance as notices that do not stop play (also noted for the phone).
        public static Task FactCard(Ctx ctx, string title, IList<string> ids) => throw new NotImplementedException("port: story/common.js:factCard");
        public static void DrainBattery(Game G, double n) => G.phone.battery = Math.Max(0, G.phone.battery - n);
    }
}
