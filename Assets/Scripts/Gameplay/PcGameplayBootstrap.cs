using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace EDNXR.Gameplay
{
    public static class PcGameplayBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePcGameplayControls()
        {
            if (XRSettings.isDeviceActive)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            if (camera.GetComponent<PcPlayerController>() == null)
                camera.gameObject.AddComponent<PcPlayerController>();

            if (camera.GetComponent<PcMouseGrabber>() == null)
                camera.gameObject.AddComponent<PcMouseGrabber>();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsurePcGameplayControls();
        }
    }
}
