using UnityEngine;

namespace EDNXR.Gameplay
{
    public static class DoorObjectiveBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDoorObjective()
        {
            DoorObjectiveController.EnsureInScene();
        }
    }
}
