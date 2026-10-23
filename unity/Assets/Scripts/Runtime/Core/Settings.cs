// SHARED. app.js settings (the pause-menu values) and the experience-mode profile the engine applies.
// Stored as settings.json in Application.persistentDataPath; a missing or broken file gives the defaults.
using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Gyeongbo.Core
{
    public sealed class Settings
    {
        public string quality = "high";          // low | medium | high
        public float volume = 0.7f;
        public bool mute;
        public bool captions = true;
        public bool reduceMotion;
        public float textScale = 1;
        public string view = "third";            // first | third
        public float sensitivity = 1;
        public bool minimap = true;
        public float volAmbience = 1, volEffects = 1, volUi = 1;
        public string profile = "none";          // none | mobility | wheelchair | lowvision | deaf | guardian

        public Settings Clone() => (Settings)MemberwiseClone();

        static string PathOf => System.IO.Path.Combine(Application.persistentDataPath, "settings.json");

        // Defaults overlaid with the stored file (JS: { ...defaults, ...safeParse(store) }).
        public static Settings Load()
        {
            var s = new Settings();
            try { if (File.Exists(PathOf)) JsonConvert.PopulateObject(File.ReadAllText(PathOf), s); }
            catch (Exception e) { Debug.LogWarning("[settings] unreadable settings.json, using defaults: " + e.Message); return new Settings(); }
            return s;
        }

        public void Save()
        {
            try { SaveStore.WriteAtomic(PathOf, JsonConvert.SerializeObject(this, Formatting.Indented)); }
            catch (Exception e) { Debug.LogWarning("[settings] not saved: " + e.Message); }
        }
    }

    // app.js PROFILES entry: what the body can do in an experience mode (engine.setProfile).
    public sealed class Profile
    {
        public bool run = true, jump = true, stairs = true;
        public float eye = 1.62f;
        public float speed = 1;
        public Profile Clone() => (Profile)MemberwiseClone();
    }
}
