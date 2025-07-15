using System;
using TMPro;
using UnityEngine;

public class FPS : MonoBehaviour
{
    public TMP_Text Display;

    private void Update()
    {
        Display.text = (1.0f/Time.deltaTime).ToString("N2");
    }
}
