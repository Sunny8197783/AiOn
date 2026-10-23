// Ports ui/radio.js: tuning a portable radio (or the phone FM app) — dial, signal meter, lines heard per
// station (Drills.RadioSignal), noting what was heard. onSignal drives the audio radio voice.
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.UI
{
    public static class Radio
    {
        public static Task<RadioResult> Open(RadioOptions o) => throw new NotImplementedException("port: ui/radio.js:radio");
    }
}
