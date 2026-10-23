// SHARED. Facade over audio.js (synthesised ambience, siren, radio, hits, footsteps, UI blips).
using System;

namespace Gyeongbo.Core
{
    public interface IAudio
    {
        void Unlock();
        void SetVolume(float v);
        void SetMute(bool on);
        void SetBus(string bus, float v);                 // 'ambience' | 'effects' | 'ui'
        void Boom(float distance = 1);
        // punch | thud | land | glass | swing | pick | whistle(seconds) | collapse
        void Hit(string name, float? arg = null);
        // none | city | night | blackout | indoor | office | mart | shelter | rain
        void Ambience(string name = "none", float fadeS = 1.5f);
        void Siren(bool on);
        void PhoneAlert(string kind = "sms");
        void Radio(bool on = true, float clarity = 0);
        void Footstep(string surface = "concrete");      // concrete | tile | stairs | ...
        void Ui(string name = "click");                  // open close click tick select confirm error note payoff alert
        void Fire(float level = 0);
        void Spray(bool on);
        void Crowd(float level = 0);
        string State { get; }                            // 'locked' until Unlock()
        // Raised by Boom ("boom", distance), PhoneAlert ("phoneAlert", kind) and Siren ("siren", on):
        // the app shows them as on-screen cues in the 청각 장애 profile.
        event Action<string, object> Played;
    }
}
