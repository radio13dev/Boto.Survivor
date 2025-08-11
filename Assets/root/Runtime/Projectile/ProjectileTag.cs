using System;
using System.Collections.Generic;
using BovineLabs.Saving;
using Collisions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct Projectile : IComponentData
{
    public float Damage;
}

[Save]
public struct ProjectileHit : IComponentData, IEnableableComponent
{
}

[Save]
public struct ProjectileIgnoreEntity : IBufferElementData
{
    public NetworkId Value;

    public ProjectileIgnoreEntity(NetworkId value)
    {
        Value = value;
    }
}

[UpdateBefore(typeof(CollisionSystemGroup))]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial class ProjectileSystemGroup : ComponentSystemGroup
{
}

[Save]
public struct NetworkId : IComponentData, IEnableableComponent
{
    public long Value;
}

[Save]
public struct NetworkIdIterator : IComponentData
{
    public long Value;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct NetworkIdSystem : ISystem
{
    EntityQuery m_Query;
    public void OnCreate(ref SystemState state)
    {
        m_Query = SystemAPI.QueryBuilder().WithAll<LocalTransform>().WithDisabled<NetworkId>().Build();
        state.RequireForUpdate(m_Query);
        state.RequireForUpdate<NetworkIdIterator>();
    }

    public void OnUpdate(ref SystemState state)
    {
        using var transforms = m_Query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var entities = m_Query.ToEntityArray(Allocator.Temp);
        
        // Gotta do this based on the Hash ordering of the gem drops
        NativeArray<int> correctOrdering = new NativeArray<int>(transforms.Length, Allocator.Temp);
        for (int i = 0; i < correctOrdering.Length; i++) correctOrdering[i] = i; // Array of initial indexes
        correctOrdering.Sort(new LocalTransformComparer(transforms)); // Sort to change it into the order of indexes to process (should be consistent across platform)
        
        for (int i = 0; i < correctOrdering.Length; i++)
        {
            // Give currency to the entity
            int index = correctOrdering[i];
            if (SystemAPI.HasBuffer<InventoryGem>(collectedBy[index].CollectedBy))
            {
                var inventoryRW = SystemAPI.GetBuffer<InventoryGem>(collectedBy[index].CollectedBy);
                inventoryRW.Add(new InventoryGem(drops[index].Gem));
            }
        }
        
        correctOrdering.Dispose();
    }
}

public struct LocalTransformComparer : IComparer<int>
{
    NativeArray<LocalTransform> m_Transforms;
    
    public LocalTransformComparer(NativeArray<LocalTransform> transforms) => m_Transforms = transforms;

    public int Compare(int x, int y)
    {
        int c;
        c = m_Transforms[x].Position.x.CompareTo(m_Transforms[y].Position.x);
        if (c != 0) return c;
        c = m_Transforms[x].Position.y.CompareTo(m_Transforms[y].Position.y);
        if (c != 0) return c;
        c = m_Transforms[x].Position.z.CompareTo(m_Transforms[y].Position.z);
        if (c != 0) return c;
        
        Debug.LogError($"Compared two transforms with same position: {m_Transforms[x]}, {m_Transforms[y]}");
        return 0;
    }
}