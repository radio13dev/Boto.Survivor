using Unity.Burst;
using UnityEngine;

public static class WebGpu
{
    public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<bool, EnabledKey>();
    private class EnabledKey {}
    
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        Enabled.Data = false;
    }
}