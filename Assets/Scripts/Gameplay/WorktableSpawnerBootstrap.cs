using UnityEngine;

namespace EDNXR.Gameplay
{
    public static class WorktableSpawnerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureWorktableSpawner()
        {
            if (Object.FindObjectOfType<WorktableParticleSpawner>() != null)
                return;

            GameObject worktable = GameObject.Find("WorkTable");
            GameObject host = worktable != null ? worktable : new GameObject("Worktable Particle Spawner Host");
            host.AddComponent<WorktableParticleSpawner>();
        }
    }
}
