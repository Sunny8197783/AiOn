// Editor-only: creates the URP pipeline asset, its renderer and the default volume profile if missing and
// assigns them in Graphics/Quality settings. Not compiled by Tools~/check.sh.
using System;
using UnityEditor;

namespace Gyeongbo.URP.Editor
{
    [InitializeOnLoad]
    static class UrpSetup
    {
        static UrpSetup() { EditorApplication.delayCall += Run; }

        // Idempotent.
        public static void Run() => throw new NotImplementedException("port: URP pipeline asset / renderer / volume profile setup");
    }
}
