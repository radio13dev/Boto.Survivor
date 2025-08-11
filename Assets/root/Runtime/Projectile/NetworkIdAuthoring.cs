using System;
using BovineLabs.Saving;
using Unity.Entities;
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