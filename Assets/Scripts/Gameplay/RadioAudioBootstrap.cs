using UnityEngine;
using UnityEngine.SceneManagement;

namespace EDNXR.Gameplay
{
    public static class RadioAudioBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRadioAudio()
        {
            GameObject radio = GameObject.Find("Radio");

            if (radio == null)
                radio = GameObject.Find("radi");

            if (radio == null)
                return;

            RadioSpatialAudio spatialAudio = radio.GetComponent<RadioSpatialAudio>();

            if (spatialAudio == null)
                spatialAudio = radio.AddComponent<RadioSpatialAudio>();

            AudioClip clip = Resources.Load<AudioClip>("RadioMusic");

            if (clip != null)
                spatialAudio.SetClip(clip);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRadioAudio();
        }
    }
}
