using System;
using Unity.Burst;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/GameDebug", order = 1)]
public class GameDebug : ScriptableObject
{
    [Range(0,10)] public float _A;
    public static readonly SharedStatic<float> s_A = SharedStatic<float>.GetOrCreate<float, k_A>();
    private class k_A { }
    public static float A => s_A.Data;
    
    [Range(0,10)] public float _B;
    public static readonly SharedStatic<float> s_B = SharedStatic<float>.GetOrCreate<float, k_B>();
    private class k_B { }
    public static float B => s_B.Data;
    
    [Range(0,10)] public float _C;
    public static readonly SharedStatic<float> s_C = SharedStatic<float>.GetOrCreate<float, k_C>();
    private class k_C { }
    public static float C => s_C.Data;
    
    [Range(0,10)] public float _D;
    public static readonly SharedStatic<float> s_D = SharedStatic<float>.GetOrCreate<float, k_D>();
    private class k_D { }
    public static float D => s_D.Data;
    
    [Range(0,10)] public float _E;
    public static readonly SharedStatic<float> s_E = SharedStatic<float>.GetOrCreate<float, k_E>();
    private class k_E { }
    public static float E => s_E.Data;
    
    [Range(0,10)] public float _F;
    public static readonly SharedStatic<float> s_F = SharedStatic<float>.GetOrCreate<float, k_F>();
    private class k_F { }
    public static float F => s_F.Data;
    
    [Range(0,10)] public float _G;
    public static readonly SharedStatic<float> s_G = SharedStatic<float>.GetOrCreate<float, k_G>();
    private class k_G { }
    public static float G => s_G.Data;
    
    [Range(0,10)] public float _H;
    public static readonly SharedStatic<float> s_H = SharedStatic<float>.GetOrCreate<float, k_H>();
    private class k_H { }
    public static float H => s_H.Data;


    public static void Initialize(GameDebug source)
    {
        s_A.Data = source._A;
        s_B.Data = source._B;
        s_C.Data = source._C;
        s_D.Data = source._D;
        s_E.Data = source._E;
        s_F.Data = source._F;
        s_G.Data = source._G;
        s_H.Data = source._H;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            Initialize(this);
    }
}