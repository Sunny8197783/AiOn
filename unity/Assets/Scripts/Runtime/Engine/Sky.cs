// Ports engine/sky.js: sky dome (three.js Sky), sun/moon, hemisphere and key lights, clouds, hills ring,
// the distant city and beacons, and the reflection environment per preset.
using System;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed class Sky
    {
        // Direction from azimuth/elevation (degrees), JS space.
        public static Vector3 DirFrom(float az, float el) => throw new NotImplementedException("port: engine/sky.js:dirFrom");

        public static Sky Create(GameObject scene, Kit kit, string quality = "high") => throw new NotImplementedException("port: engine/sky.js:createSky");
        public EnvState State => throw new NotImplementedException("port: engine/sky.js:state");
        public Light Sun => throw new NotImplementedException("port: engine/sky.js:sun");
        public void Apply(EnvState s) => throw new NotImplementedException("port: engine/sky.js:apply");
        public void Update(float dt, Camera camera, Vector3 playerJs, bool reduceMotion) => throw new NotImplementedException("port: engine/sky.js:update");
        // Reflection probe / ambient source for a preset.
        public void Environment(string preset, bool rain) => throw new NotImplementedException("port: engine/sky.js:environment");
        public void SetQuality(string q) => throw new NotImplementedException("port: engine/sky.js:setQuality");
    }
}
