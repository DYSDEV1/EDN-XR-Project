using UnityEngine;
using UnityEngine.SceneManagement;

namespace EDNXR.Gameplay
{
    public static class PhoneObjectiveBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePhoneObjective()
        {
            PhoneObjectiveController.EnsureInScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsurePhoneObjective();
        }
    }
}
