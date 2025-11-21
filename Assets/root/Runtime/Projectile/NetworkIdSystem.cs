using System;
using System.Collections.Generic;
using BovineLabs.Core.Collections;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct NetworkIdIterator : IComponentData
{
    public long Value;
}

public struct NetworkIdMapping : IComponentData
{
    public const int k_MappingOffset = 8;
    public const int k_EntitiesPerArray = 2 << k_MappingOffset;

    internal int m_Offset;
    internal UnsafeList<UnsafeArray<Entity>> m_Mapping;

    public Entity this[NetworkId id]
    {
        get
        {
            if (id.Value == 0) return Entity.Null;

            int mappingArray = (int)(id.Value >> k_MappingOffset);
            if (mappingArray < m_Offset) return Entity.Null;
            if (mappingArray >= m_Offset + m_Mapping.Length) return Entity.Null;

            int mappingIndex = (int)(id.Value & ~k_EntitiesPerArray);
            return m_Mapping[mappingArray - m_Offset][mappingIndex];
        }
    }
}

public struct LocalTransformComparer : IComparer<int>
{
    [ReadOnly] NativeArray<LocalTransform> m_Transforms;

    public LocalTransformComparer(NativeArray<LocalTransform> transforms)
    {
        m_Transforms = transforms;
        
#if UNITY_EDITOR
        m_DebugEntities = default;
        m_DebugEntityManager = default;
#endif
    }

#if UNITY_EDITOR
    [ReadOnly] EntityManager m_DebugEntityManager;
    [ReadOnly] NativeArray<Entity> m_DebugEntities;
    public LocalTransformComparer(NativeArray<LocalTransform> transforms, NativeArray<Entity> debugEntities, in EntityManager entityManager)
    {
        m_Transforms = transforms;
        m_DebugEntities = debugEntities;
        m_DebugEntityManager = entityManager;
    }
#endif

    public int Compare(int x, int y)
    {
        if (x == y) return 0;
        int c = math.hash(m_Transforms[x].ToMatrix())
            .CompareTo(math.hash(m_Transforms[y].ToMatrix()));
        if (c == 0)
        {
#if UNITY_EDITOR
            Debug.LogError($"Two transforms at same position {x}/{y}. \n" +
                           $"Entities: {(m_DebugEntities.IsCreated ? m_DebugEntities[x] : default)}/{(m_DebugEntities.IsCreated ? m_DebugEntities[y] : default)}\n" +
                           $"Transforms:{m_Transforms[x]}/{m_Transforms[y]}. \n" +
                           $"{(m_DebugEntities.IsCreated ? m_DebugEntityManager.Debug.GetEntityInfo(m_DebugEntities[x]) : default)}\n" +
                           $"{(m_DebugEntities.IsCreated ? m_DebugEntityManager.Debug.GetEntityInfo(m_DebugEntities[y]) : default)}");
#else
            Debug.LogError($"Two transforms at same position: {x}:{m_Transforms[x]} and {y}:{m_Transforms[y]}");
#endif
        }

        return c;
    }
}

/// <summary>
/// PRAYING that entities in these things don't exist for too long.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
[RequireMatchingQueriesForUpdate]
public partial struct NetworkIdSystem : ISystem
{
    [NativeDisableUnsafePtrRestriction] EntityQuery m_Query;

    public void OnCreate(ref SystemState state)
    {
        m_Query = SystemAPI.QueryBuilder().WithAll<LocalTransform>().WithDisabled<NetworkId>().Build();
        state.RequireForUpdate(m_Query);
        state.RequireForUpdate<NetworkIdIterator>();
        state.EntityManager.CreateSingleton<NetworkIdMapping>();
        SystemAPI.SetSingleton(new NetworkIdMapping()
        {
            m_Mapping = new UnsafeList<UnsafeArray<Entity>>(1, Allocator.Persistent)
        });
    }

    public static void InitAfterLoad(EntityManager entityManager)
    {
        using var idQuery = entityManager.CreateEntityQuery(typeof(NetworkId));
        using var ids = idQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
        if (ids.Length == 0) return;

        using var idEntities = idQuery.ToEntityArray(Allocator.Temp);

        NativeArray<int> correctOrdering = new NativeArray<int>(ids.Length, Allocator.Temp);
        for (int i = 0; i < correctOrdering.Length; i++) correctOrdering[i] = i; // Array of initial indexes
        correctOrdering.Sort(new NetworkIdComparer(ids)); // Sort to change it into the order of indexes to process (should be consistent across platform)

        // Setup default offset (for the 'zero' array
        var mapping = entityManager.GetSingleton<NetworkIdMapping>();
        mapping.m_Offset = (int)(ids[correctOrdering[0]].Value >> NetworkIdMapping.k_MappingOffset);
        for (int i = 0; i < correctOrdering.Length; i++)
        {
            // Add them to the mapping
            var networkId = ids[correctOrdering[i]].Value;
            int mappingIndex = (int)(networkId & ~NetworkIdMapping.k_EntitiesPerArray);
            int mappingArray = (int)(networkId >> NetworkIdMapping.k_MappingOffset);
            if (mappingArray >= mapping.m_Offset + mapping.m_Mapping.Length)
            {
                mapping.m_Mapping.Add(new UnsafeArray<Entity>(NetworkIdMapping.k_EntitiesPerArray, Allocator.Persistent, NativeArrayOptions.UninitializedMemory));
            }

            mapping.m_Mapping.ElementAt(mappingArray)[mappingIndex] = idEntities[correctOrdering[i]];
        }

        correctOrdering.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (m_Query.IsEmpty) return;

        using var transforms = m_Query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var entities = m_Query.ToEntityArray(Allocator.Temp);

        // Gotta do this based on the Hash ordering of the gem drops
        NativeArray<int> correctOrdering = new NativeArray<int>(transforms.Length, Allocator.Temp);
        for (int i = 0; i < correctOrdering.Length; i++) correctOrdering[i] = i; // Array of initial indexes

#if UNITY_EDITOR
        correctOrdering.Sort(new LocalTransformComparer(transforms, entities, state.EntityManager));
#else
        correctOrdering.Sort(new LocalTransformComparer(transforms)); // Sort to change it into the order of indexes to process (should be consistent across platform)
#endif

        using EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        var iterator = SystemAPI.GetSingleton<NetworkIdIterator>().Value;
        ref var mapping = ref SystemAPI.GetSingletonRW<NetworkIdMapping>().ValueRW;
        for (int i = 0; i < correctOrdering.Length; i++)
        {
            // Give currency to the entity
            int index = correctOrdering[i];
            ecb.SetComponent(entities[index], new NetworkId(iterator));
            ecb.SetComponentEnabled<NetworkId>(entities[index], true);

            // Add them to the mapping
            int mappingIndex = (int)(iterator & ~NetworkIdMapping.k_EntitiesPerArray);
            int mappingArray = (int)(iterator >> NetworkIdMapping.k_MappingOffset);
            if (mappingArray >= mapping.m_Offset + mapping.m_Mapping.Length)
            {
                mapping.m_Mapping.Add(new UnsafeArray<Entity>(NetworkIdMapping.k_EntitiesPerArray, Allocator.Persistent, NativeArrayOptions.ClearMemory));
            }

            mapping.m_Mapping.ElementAt(mappingArray - mapping.m_Offset)[mappingIndex] = entities[index];

            // Iterate
            iterator++;
        }

        ecb.Playback(state.EntityManager);

        SystemAPI.SetSingleton(new NetworkIdIterator() { Value = iterator });

        correctOrdering.Dispose();

        // Also try to reduce our map size
        if (mapping.m_Mapping.Length > 0 && (iterator >> NetworkIdMapping.k_MappingOffset > mapping.m_Offset))
        {
            var zeroMap = mapping.m_Mapping[0];
            bool stillUsed = false;
            for (int i = 0; i < zeroMap.Length; i++)
            {
                if (SystemAPI.HasComponent<NetworkId>(zeroMap[i]))
                {
                    stillUsed = true;
                    break;
                }
            }

            if (!stillUsed)
            {
                mapping.m_Offset++;
                mapping.m_Mapping.RemoveAt(0);
            }
        }
    }

    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<NetworkIdMapping>(out var mapping))
        {
            for (int i = 0; i < mapping.m_Mapping.Length; i++)
                mapping.m_Mapping[i].Dispose();
            mapping.m_Mapping.Dispose();
            state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<NetworkIdMapping>());
        }
    }
}