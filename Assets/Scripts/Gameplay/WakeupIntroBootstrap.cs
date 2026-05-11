using UnityEngine;

namespace EDNXR.Gameplay
{
    public static class WakeupIntroBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureWakeupIntro()
        {
            if (Object.FindObjectOfType<WakeupIntroController>() != null)
                return;

            GameObject host = new GameObject("Wakeup Intro Controller");
            host.AddComponent<WakeupIntroController>();
        }
    }
}
