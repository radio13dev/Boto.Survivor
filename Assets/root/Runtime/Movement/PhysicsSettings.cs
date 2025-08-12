using System;
using Unity.Burst;
using UnityEngine;

public class PhysicsSettings : MonoBehaviour
{
    public static readonly SharedStatic<float> s_GemJump = SharedStatic<float>.GetOrCreate<float, k_GemJump>();
    private class k_GemJump { }
    public static readonly SharedStatic<float> s_GemSpin = SharedStatic<float>.GetOrCreate<float, k_GemSpin>();
    private class k_GemSpin { }

    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        s_GemJump.Data = 0;
        s_GemSpin.Data = 0;
    }

    public float GemJump = 30;
    public float GemSpin = 1;

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        s_GemJump.Data = GemJump;
        s_GemSpin.Data = GemSpin;
    }

    private void Awake()
    {
        OnValidate();
    }
}