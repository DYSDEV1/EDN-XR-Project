using UnityEngine;
using UnityEngine.XR;

namespace EDNXR.Gameplay
{
    public static class PcGameplayBootstrap
    {
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
    }
}
