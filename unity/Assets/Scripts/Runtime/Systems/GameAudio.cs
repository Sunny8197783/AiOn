// Ports audio.js: everything synthesised (PORTING.md "Save, settings, audio": AudioClip.Create PCM + AudioSources)
// — ambience beds per place, the air-raid siren, the radio voice, phone alerts, hits, footsteps, UI blips,
// fire, extinguisher spray and crowd murmur, with master volume, mute and three buses.
using System;
using Gyeongbo.Core;

namespace Gyeongbo.Systems
{
    public sealed class GameAudio : IAudio
    {
        public static GameAudio Create() => throw new NotImplementedException("port: audio.js:createAudio");

        public event Action<string, object> Played;
        void Raise(string name, object arg) => Played?.Invoke(name, arg);

        public void Unlock() => throw new NotImplementedException("port: audio.js:unlock");
        public void SetVolume(float v) => throw new NotImplementedException("port: audio.js:setVolume");
        public void SetMute(bool on) => throw new NotImplementedException("port: audio.js:setMute");
        public void SetBus(string bus, float v) => throw new NotImplementedException("port: audio.js:setBus");
        public void Boom(float distance = 1) => throw new NotImplementedException("port: audio.js:boom");
        public void Hit(string name, float? arg = null) => throw new NotImplementedException("port: audio.js:hit");
        public void Ambience(string name = "none", float fadeS = 1.5f) => throw new NotImplementedException("port: audio.js:ambience");
        public void Siren(bool on) => throw new NotImplementedException("port: audio.js:siren");
        public void PhoneAlert(string kind = "sms") => throw new NotImplementedException("port: audio.js:phoneAlert");
        public void Radio(bool on = true, float clarity = 0) => throw new NotImplementedException("port: audio.js:radio");
        public void Footstep(string surface = "concrete") => throw new NotImplementedException("port: audio.js:footstep");
        public void Ui(string name = "click") => throw new NotImplementedException("port: audio.js:ui");
        public void Fire(float level = 0) => throw new NotImplementedException("port: audio.js:fire");
        public void Spray(bool on) => throw new NotImplementedException("port: audio.js:spray");
        public void Crowd(float level = 0) => throw new NotImplementedException("port: audio.js:crowd");
        public string State => throw new NotImplementedException("port: audio.js:state");
    }
}
