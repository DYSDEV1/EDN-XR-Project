using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PinCounter : MonoBehaviour
{
    public FallingPin[] pins;
    public int fallenCount = 0;

    public TextMeshProUGUI scoreText;

    void Start()
    {
        pins = GetComponentsInChildren<FallingPin>();
    }

    void Update()
    {
        int count = 0;

        if (pins != null)
        {
            for (int i = 0; i < pins.Length; i++)
            {
                if (pins[i] != null && pins[i].isFallen)
                    count++;
            }
        }

        fallenCount = count;

        if (scoreText != null)
            scoreText.text = fallenCount.ToString();
    }
}
