// Ports levels/shelter-people.js: the shelter's people — sitters on the mats, arrivals and leavers through the
// open entrances, toilet trips, the shelter manager, 서연 and a volunteer; reacts to the alert/calm phase and
// clears the seats around the fire.
using System;
using System.Collections.Generic;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class ShelterPeopleResult
    {
        public GameObject group;
        public Dictionary<string, IPerson> npcs = new Dictionary<string, IPerson>();   // shelterManager, seoyeon, volunteer
        public Action<string> setCrowd;               // arriving | settled | leaving
        public Action<string> setPhase;               // alert | calm
        public Action<XZ?> setFire;                   // fire position or null
        public Action<List<string>> setWays;          // open entrances
        public Action<float, float, Player> update;
    }

    public static class ShelterPeople
    {
        public static readonly string[] ENTRANCES = { "stairs", "ramp", "access", "east" };
        public static float HeightAt(IList<WalkZone> walk, float x, float z) => throw new NotImplementedException("port: levels/shelter-people.js:heightAt");
        public static ShelterPeopleResult BuildPeople(Kit kit, IPeople people, List<ShelterSeat> seats, List<WalkZone> walk) => throw new NotImplementedException("port: levels/shelter-people.js:buildPeople");
    }
}
