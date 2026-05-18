using UnityEngine;
using UnityEngine.SceneManagement;

namespace EDNXR.Gameplay
{
    public static class WakeupIntroBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureWakeupIntro()
        {
            EnsureWakeupIntroExists();
        }

        public static void EnsureWakeupIntroExists()
        {
            if (Object.FindObjectOfType<WakeupIntroController>() != null)
                return;

            GameObject host = new GameObject("Wakeup Intro Controller");
            host.AddComponent<WakeupIntroController>();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureWakeupIntroExists();
        }
    }
}
