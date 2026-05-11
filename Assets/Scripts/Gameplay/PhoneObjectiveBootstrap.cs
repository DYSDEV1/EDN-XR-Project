using UnityEngine;

namespace EDNXR.Gameplay
{
    public static class PhoneObjectiveBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePhoneObjective()
        {
            PhoneObjectiveController.EnsureInScene();
        }
    }
}
