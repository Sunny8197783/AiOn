// Ports levels/shelter-services.js: services hung from the shell — fluorescent tubes (one faulty), emergency
// lamps and LEDs, ventilation fans, CCTV, hydrant cabinets, signs along the aisles.
using System;
using System.Collections.Generic;
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class ShelterServicesResult
    {
        public GameObject group, live;
        public List<Material> tubes = new List<Material>();
        public List<Light> lights = new List<Light>(), emLights = new List<Light>();
        public Material emLamp, emLed, cctvLed, faulty;
        public List<GameObject> fans = new List<GameObject>();
    }

    public static class ShelterServices
    {
        public static ShelterServicesResult BuildServices(Kit kit, ShelterTex T, ShelterShellResult shell) => throw new NotImplementedException("port: levels/shelter-services.js:buildServices");
    }
}
