using UnityEngine;

namespace EDNXR.Gameplay
{
    public static class ObjectiveHudBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureObjectiveHud()
        {
            if (Object.FindObjectOfType<ObjectiveHud>() != null)
                return;

            GameObject hud = new GameObject("Objective HUD");
            hud.AddComponent<ObjectiveHud>();
        }
    }
}
