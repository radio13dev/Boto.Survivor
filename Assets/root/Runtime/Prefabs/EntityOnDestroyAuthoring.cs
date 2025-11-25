using System;
using System.Linq;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public struct EntityOnDestroy : IBufferElementData
{
    public Entity Prefab;
}

public class EntityOnDestroyAuthoring : MonoBehaviour
{
    public GameObject[] Prefabs = Array.Empty<GameObject>();

    [Header("OBSOLTE: Use array instead.")]
    public GameObject Prefab;

    public int Count;

    public class Baker : Baker<EntityOnDestroyAuthoring>
    {
        public override void Bake(EntityOnDestroyAuthoring authoring)
        {
            if (!DependsOn(GetComponent<DestroyableAuthoring>()))
            {
                Debug.LogError($"EntityOnDestroyAuthoring must have a DestroyableAuthoring component on the same GameObject: {authoring.gameObject}", authoring);
                return;
            }

            if (!authoring.Prefab && (authoring.Prefabs.Length == 0 || authoring.Prefabs.All(p => !p)))
            {
                Debug.LogError($"EntityOnDestroyAuthoring has no game objects to spawn.", authoring);
                return;
            }

            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            var buffer = AddBuffer<EntityOnDestroy>(entity);
            if (authoring.Prefab) buffer.Add(new EntityOnDestroy() { Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.None) });
            for (int i = 0; i < authoring.Prefabs.Length; i++)
                buffer.Add(new EntityOnDestroy() { Prefab = GetEntity(authoring.Prefabs[i], TransformUsageFlags.None) });
        }
    }
}

[UpdateBefore(typeof(DestroySystem))]
[UpdateInGroup(typeof(DestroySystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct EntityOnDestroySystem : ISystem
{
    EntityQuery m_CleanupQuery;
    BufferTypeHandle<EntityOnDestroy> m_BufferTypeHandle;
    EntityTypeHandle m_EntityTypeHandle;
    ComponentTypeHandle<LocalTransform> m_ComponentTypeHandle;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<EntityOnDestroy, LocalTransform, DestroyFlag>().Build();
        state.RequireForUpdate(m_CleanupQuery);
        m_BufferTypeHandle = state.GetBufferTypeHandle<EntityOnDestroy>(isReadOnly: true);
        m_EntityTypeHandle = state.GetEntityTypeHandle();
        m_ComponentTypeHandle = state.GetComponentTypeHandle<LocalTransform>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        m_BufferTypeHandle.Update(ref state);
        m_EntityTypeHandle.Update(ref state);
        m_ComponentTypeHandle.Update(ref state);

        var sharedRandom = SystemAPI.GetSingleton<SharedRandom>();

        using var chunks = m_CleanupQuery.ToArchetypeChunkArray(Allocator.Temp);
        for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
        {
            var chunk = chunks[chunkIndex];
            var numEntities = chunk.Count;
            var entitiesArray = chunk.GetNativeArray(m_EntityTypeHandle);
            var destroyBufferArray = chunk.GetBufferAccessorRO(ref m_BufferTypeHandle);
            var transformsArray = chunk.GetNativeArray(ref m_ComponentTypeHandle);

            var random = sharedRandom.Random;

            for (int j = 0; j < numEntities; j++)
            {
                var entity = entitiesArray[j];
                var destroyBuffer = destroyBufferArray[j];
                var transform = transformsArray[j];

                for (int onDestroyIt = 0; onDestroyIt < destroyBuffer.Length; onDestroyIt++)
                {
                    var onDestroy = destroyBuffer[onDestroyIt];

                    var newEntity = delayedEcb.Instantiate(onDestroy.Prefab);
                    delayedEcb.SetComponent(newEntity, transform);

                    if (SystemAPI.HasComponent<RotationalInertia>(onDestroy.Prefab))
                    {
                        var inertia = new RotationalInertia();
                        inertia.Set(random.NextFloat3(), 1);
                        delayedEcb.SetComponent(newEntity, inertia);
                    }

                    if (SystemAPI.HasComponent<Movement>(onDestroy.Prefab) && SystemAPI.HasComponent<Movement>(entity))
                    {
                        var movement = SystemAPI.GetComponent<Movement>(entity);
                        var newEntityMovement = new Movement();
                        newEntityMovement.Velocity = movement.Velocity + (math.length(movement.Velocity) / 2 + 1) * random.NextFloat3();
                        delayedEcb.SetComponent(newEntity, newEntityMovement);
                    }

                    // I don't like this but this is here... Dakota.
                    if (SystemAPI.HasComponent<SurvivorTag>(entity) || SystemAPI.HasComponent<DeadSurvivorTag>(entity))
                    {
                        var playerSaveable = SystemAPI.GetComponent<PlayerControlledSaveable>(entity);
                        delayedEcb.SetComponent(newEntity, playerSaveable);
                        delayedEcb.SetComponent(newEntity, SystemAPI.GetComponent<Wallet>(entity));
                        delayedEcb.SetComponent(newEntity, SystemAPI.GetComponent<TiledStatsTree>(entity));

                        var oldRingBuffer = SystemAPI.GetBuffer<Ring>(entity);
                        var newRingBuffer = delayedEcb.SetBuffer<Ring>(newEntity);
                        for (int ringIndex = 0; ringIndex < oldRingBuffer.Length; ringIndex++)
                        {
                            newRingBuffer.Add(oldRingBuffer[ringIndex]);
                        }

                        var oldEquippedGems = SystemAPI.GetBuffer<EquippedGem>(entity);
                        var newEquippedGems = delayedEcb.SetBuffer<EquippedGem>(newEntity);
                        for (int gemIndex = 0; gemIndex < oldEquippedGems.Length; gemIndex++)
                        {
                            newEquippedGems.Add(oldEquippedGems[gemIndex]);
                        }

                        var oldInventoryGems = SystemAPI.GetBuffer<InventoryGem>(entity);
                        var newInventoryGems = delayedEcb.SetBuffer<InventoryGem>(newEntity);
                        for (int gemIndex = 0; gemIndex < oldInventoryGems.Length; gemIndex++)
                        {
                            newInventoryGems.Add(oldInventoryGems[gemIndex]);
                        }

                        if (SystemAPI.HasComponent<SurvivorTag>(entity))
                        {
                            GameEvents.PlayerDied(entity, playerSaveable.Index);
                        }

                        if (SystemAPI.HasComponent<DeadSurvivorTag>(entity))
                        {
                            GameEvents.PlayerRevived(entity, playerSaveable.Index);
                        }
                    }
                }
            }
        }
    }
}