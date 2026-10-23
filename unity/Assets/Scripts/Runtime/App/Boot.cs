// Creates the whole game from code after any scene loads (PORTING.md "Boot"): pressing Play in any scene runs it.
using UnityEngine;

namespace Gyeongbo.App
{
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindAnyObjectByType<GameApp>() != null) return;
            var go = new GameObject("Gyeongbo");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }
    }
}
