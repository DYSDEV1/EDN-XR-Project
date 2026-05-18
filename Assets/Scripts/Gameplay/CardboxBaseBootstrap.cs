using UnityEngine;
using UnityEngine.SceneManagement;

namespace EDNXR.Gameplay
{
    public static class CardboxBaseBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCardboxBases()
        {
            GameObject[] objects = Object.FindObjectsOfType<GameObject>(true);
            int found = 0;
            int added = 0;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null || !IsCardboxBaseName(objects[i].name))
                    continue;

                found++;
                Debug.Log($"[CardboxBaseBootstrap] Found {objects[i].name}: activeSelf={objects[i].activeSelf}, activeInHierarchy={objects[i].activeInHierarchy}, pos={objects[i].transform.position}, parent={(objects[i].transform.parent != null ? objects[i].transform.parent.name : "none")}");

                if (objects[i].GetComponent<CardboxBaseController>() == null)
                {
                    objects[i].AddComponent<CardboxBaseController>();
                    added++;
                }
            }

            Debug.Log($"[CardboxBaseBootstrap] Done. found={found}, addedControllers={added}");
        }

        private static bool IsCardboxBaseName(string objectName)
        {
            return !string.IsNullOrWhiteSpace(objectName)
                && objectName.StartsWith("CardboxBase", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureCardboxBases();
        }
    }
}
