using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PinAudio : MonoBehaviour

{
    private AudioSource audioSource;
    private float enableTime;
    // Start is called before the first frame update
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        enableTime = Time.time + 0.3f;

    }
    void OnCollisionEnter(Collision collision)
    {
        if (Time.time < enableTime) return;

        if (!audioSource.isPlaying)
            audioSource.Play();
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
