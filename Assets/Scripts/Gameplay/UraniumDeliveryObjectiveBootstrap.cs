using UnityEngine;

namespace EDNXR.Gameplay
{
    public static class UraniumDeliveryObjectiveBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureObjectiveController()
        {
            if (Object.FindObjectOfType<UraniumDeliveryObjectiveController>() != null)
                return;

            GameObject controller = new GameObject("Uranium Delivery Objective Controller");
            controller.AddComponent<UraniumDeliveryObjectiveController>();
        }
    }
}
