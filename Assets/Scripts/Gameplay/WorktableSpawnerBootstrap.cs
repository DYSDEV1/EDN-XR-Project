using UnityEngine;
using UnityEngine.SceneManagement;

namespace EDNXR.Gameplay
{
    public static class WorktableSpawnerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureWorktableSpawner()
        {
            if (Object.FindObjectOfType<WorktableParticleSpawner>() != null)
                return;

            GameObject worktable = GameObject.Find("WorkTable");
            GameObject host = worktable != null ? worktable : new GameObject("Worktable Particle Spawner Host");
            host.AddComponent<WorktableParticleSpawner>();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureWorktableSpawner();
        }
    }
}
