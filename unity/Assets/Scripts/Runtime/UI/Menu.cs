// Ports ui/menu.js: landing screen, pause menu, settings form and the sources/credits page.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.UI
{
    public static class Menu
    {
        // [chapter, title, when · where, line] for the landing screen.
        public static readonly List<string[]> CHAPTERS = new List<string[]>();

        // Resolves 'new' | 'continue' | 'settings' | 'about'.
        public static Task<string> Landing(bool hasSave = false) => throw new NotImplementedException("port: ui/menu.js:landing");
        // Resolves 'resume' | 'settings' | 'save' | 'menu'.
        public static Task<string> Pause() => throw new NotImplementedException("port: ui/menu.js:pause");
        // Resolves the edited copy (cancel = the values it got).
        public static Task<Settings> SettingsMenu(Settings current) => throw new NotImplementedException("port: ui/menu.js:settings");
        public static Task About(IList<SourceEntry> sources) => throw new NotImplementedException("port: ui/menu.js:about");
    }
}
