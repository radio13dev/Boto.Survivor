using System;
using UnityEngine;

public class DisableOnAwake : MonoBehaviour
{
    private void Start()
    {
        gameObject.SetActive(false);
    }
}
