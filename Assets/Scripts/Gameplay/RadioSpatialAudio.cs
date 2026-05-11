using UnityEngine;

namespace EDNXR.Gameplay
{
    [RequireComponent(typeof(AudioSource))]
    public class RadioSpatialAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private float volume = 0.8f;
        [SerializeField] private float minDistance = 1.2f;
        [SerializeField] private float maxDistance = 14f;

        private void Awake()
        {
            Configure();
        }

        private void OnValidate()
        {
            Configure();
        }

        public void SetClip(AudioClip clip)
        {
            musicClip = clip;
            Configure();
        }

        private void Configure()
        {
            AudioSource source = GetComponent<AudioSource>();
            if (source == null)
                return;

            source.clip = musicClip;
            source.playOnAwake = true;
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0.2f;

            if (Application.isPlaying && musicClip != null && !source.isPlaying)
                source.Play();
        }
    }
}
