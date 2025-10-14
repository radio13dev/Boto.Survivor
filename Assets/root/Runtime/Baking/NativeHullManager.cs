using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using Vella.UnityNativeHull;

public unsafe class NativeHullManager : MonoBehaviour
{
    public MeshDatabase Database;
    NativeArray<NativeHull> m_localHulls;

    public static readonly SharedStatic<IntPtr> m_Hulls = SharedStatic<IntPtr>.GetOrCreate<m_HullsKey>();

    class m_HullsKey
    {
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        m_Hulls.Data = IntPtr.Zero;
    }

    private void Awake()
    {
        m_localHulls = new NativeArray<NativeHull>(Database.Assets.Count, Allocator.Persistent);
        m_Hulls.Data = (IntPtr)m_localHulls.GetUnsafePtr();
        int i = 0;
        foreach (var mesh in Database.Assets)
            if (mesh)
                m_localHulls[i++] = HullFactory.CreateFromMesh(mesh);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < m_localHulls.Length; i++)
            if (m_localHulls[i].IsCreated)
                m_localHulls[i].Dispose();
        m_localHulls.Dispose();
    }
}