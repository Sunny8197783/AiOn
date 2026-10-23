// Ports ui/phone.js: the phone (battery, signal, recent messages, map app, actions) and the full-screen
// emergency alert (위급재난문자 / 안전안내문자).
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.UI
{
    public static class Phone
    {
        // Resolves the action id.
        public static Task<string> Show(PhoneOptions o) => throw new NotImplementedException("port: ui/phone.js:phone");
        public static Task EmergencyAlert(AlertOptions o) => throw new NotImplementedException("port: ui/phone.js:emergencyAlert");
    }
}
