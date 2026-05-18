using UnityEngine;
using UnityEngine.SceneManagement;

namespace EDNXR.Gameplay
{
    public static class UraniumDeliveryObjectiveBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureObjectiveController()
        {
            if (Object.FindObjectOfType<UraniumDeliveryObjectiveController>() != null)
                return;

            GameObject controller = new GameObject("Uranium Delivery Objective Controller");
            controller.AddComponent<UraniumDeliveryObjectiveController>();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureObjectiveController();
        }
    }
}
