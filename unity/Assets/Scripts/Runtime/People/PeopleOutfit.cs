// Ports engine/people-outfit.js: the named characters' outfits and the seeded random crowd member (age group
// drives height, posture, clothing and hair).
using System;
using System.Collections.Generic;

namespace Gyeongbo.People
{
    public sealed class Garment
    {
        public string kind, c, pbr, len, sole, style;
        public bool open;
    }

    // An outfit spec { H, sex, girth, belly, stoop, skin, hair, top, outer, bottom, shoes, extras, props, bake }.
    public sealed class PersonSpec
    {
        public float H = 1.72f, girth = 1, belly, stoop;
        public string sex = "m", skin;
        public Garment hair, top, outer, bottom, shoes;
        public Dictionary<string, string> extras = new Dictionary<string, string>();   // scarf, lanyard, cap, glasses, nameTag
        public List<string> props = new List<string>();
        public Dictionary<string, string> bake = new Dictionary<string, string>();     // backpack / crossbody colour
    }

    public static class PeopleOutfit
    {
        public static readonly Dictionary<string, PersonSpec> PRESETS = new Dictionary<string, PersonSpec>();
        public static PersonSpec CrowdSpec(int seed) => throw new NotImplementedException("port: engine/people-outfit.js:crowdSpec");
        public static PersonSpec PresetSpec(string preset, int seed) => throw new NotImplementedException("port: engine/people-outfit.js:presetSpec");
    }
}
