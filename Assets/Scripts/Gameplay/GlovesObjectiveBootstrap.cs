using UnityEngine;

namespace EDNXR.Gameplay
{
    public static class GlovesObjectiveBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGlovesObjective()
        {
            GlovesObjectiveController.EnsureInScene();
        }
    }
}
