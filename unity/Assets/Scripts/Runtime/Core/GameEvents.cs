// SHARED. Global UI events (JS window CustomEvent 'gyeongbo:ui'): UI code raises them, the app forwards
// them to audio.Ui(name).
using System;

namespace Gyeongbo.Core
{
    public static class GameEvents
    {
        public static event Action<string> UiSound;

        public static void Ui(string name)
        {
            try { UiSound?.Invoke(name); } catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }
    }
}
