// One-time editor setup (PORTING.md "Boot"): creates Assets/Scenes/Main.unity (an empty scene) with its build
// settings entry and adds the URP Lit / Unlit and Standard shaders to Always Included Shaders, so builds keep
// the shaders Mat.Lit finds by name. URP assets themselves: Assets/Scripts/URP/Editor/UrpSetup.cs.
using System;
using UnityEditor;

namespace Gyeongbo.Editor
{
    [InitializeOnLoad]
    static class ProjectSetup
    {
        static ProjectSetup() { EditorApplication.delayCall += Run; }

        // Idempotent: does nothing when the scene and the shader entries already exist.
        public static void Run() => throw new NotImplementedException("port: editor setup (Main.unity, build settings, Always Included shaders)");
    }
}
