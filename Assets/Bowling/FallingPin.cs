using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingPin : MonoBehaviour
{
    public float fallAngleThreshold = 45f;

    public bool isFallen = false;


    public ParticleSystem celebrationEffect;

    void Update()
    {
        gameObject.SetActive(!isFallen);

        if (isFallen) return;

        // float angle = Vector3.Angle(transform.up, Vector3.up);
        float angle = Vector3.Angle(transform.forward, Vector3.up);
        if (angle > fallAngleThreshold)
        {
            isFallen = true;

            if (celebrationEffect != null)
                celebrationEffect.Play();
        }
    }
}