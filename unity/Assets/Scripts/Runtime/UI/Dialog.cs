// Ports ui/dialog.js: talk (speaker, lines one by one, choices), story cards, and the time-skip card.
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;

namespace Gyeongbo.UI
{
    public static class Dialog
    {
        // Resolves the picked choice id, or null when there were no choices.
        public static Task<string> Talk(TalkOptions o) => throw new NotImplementedException("port: ui/dialog.js:talk");
        // Resolves the action id.
        public static Task<string> Card(CardOptions o) => throw new NotImplementedException("port: ui/dialog.js:card");
        public static Task TimeSkip(string from, string to, params string[] text) => throw new NotImplementedException("port: ui/dialog.js:timeSkip");
    }
}
