// Ports ui/compose.js: 정민's message to 서연 (chips or free text), the phone/marked note panel, and the
// re-read captions. Chip labels are the exact fragments that go into the message (Relay.ComposeNote).
using System;
using System.Threading.Tasks;
using Gyeongbo.Logic;

namespace Gyeongbo.UI
{
    public sealed class ComposeContext
    {
        public string route, observedVia, gateSeenAt, sentAt;
    }

    // compose() resolves { chips, text, edited } (text is untrusted: Relay.MakeHandover cleans it).
    public sealed class ComposeResult
    {
        public Chips chips;
        public string text;
        public bool edited;
    }

    public static class Compose
    {
        public static Task<ComposeResult> Write(ComposeContext c) => throw new NotImplementedException("port: ui/compose.js:compose");
        // mode: 'phone' | 'marked' (claims underlined).
        public static void ShowNote(Handover hand, string mode) => throw new NotImplementedException("port: ui/compose.js:showNote");
        public static void HideNote() => throw new NotImplementedException("port: ui/compose.js:hideNote");
        // One step of the re-read: resolves 'next', or 'skip' on Esc.
        public static Task<string> RereadCaption(string text, Pin pin = null, bool last = false) => throw new NotImplementedException("port: ui/compose.js:rereadCaption");
    }
}
