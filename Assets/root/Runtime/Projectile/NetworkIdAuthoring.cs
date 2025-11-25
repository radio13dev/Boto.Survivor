using System;
using System.Collections.Generic;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class NetworkIdAuthoring : MonoBehaviour
{
    partial class Baker : Baker<NetworkIdAuthoring>
    {
        public override void Bake(NetworkIdAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<NetworkId>(entity);
            SetComponentEnabled<NetworkId>(entity, false);
        }
    }
}

[Save]
public struct NetworkId : IComponentData, IEnableableComponent, IEquatable<NetworkId>
{
    public long Value;

    public NetworkId(long value)
    {
        Value = value;
    }

    public bool Equals(NetworkId other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is NetworkId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static bool operator ==(NetworkId left, NetworkId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NetworkId left, NetworkId right)
    {
        return !left.Equals(right);
    }
}

public struct NetworkIdComparer : IComparer<int>
{
    NativeArray<NetworkId> m_Transforms;

    public NetworkIdComparer(NativeArray<NetworkId> transforms) => m_Transforms = transforms;

    public int Compare(int x, int y)
    {
        if (x == y) return 0;
        int c = m_Transforms[x].Value.CompareTo(m_Transforms[y].Value);
        if (c == 0) Debug.LogError($"Two network ids at same value: {x}:{m_Transforms[x]} and {y}:{m_Transforms[y]}");
        return c;
    }
}