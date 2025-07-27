using System;
using System.Collections.Generic;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public struct ItemDropOnDestroy : IBufferElementData
{
    public Entity Drop;
    public int Chance;
    public int MinCount;
    public int MaxCount;
}

public class ItemDropOnDestroyAuthoring : MonoBehaviour
{
    public List<Drop> Drops = new();
    public class Baker : Baker<ItemDropOnDestroyAuthoring>
    {
        public override void Bake(ItemDropOnDestroyAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            var drops = AddBuffer<ItemDropOnDestroy>(entity);
            foreach (var drop in authoring.Drops)
            {
                if (drop.Prefab)
                {
                    drops.Add(new ItemDropOnDestroy()
                    {
                        Drop = GetEntity(drop.Prefab, TransformUsageFlags.WorldSpace),
                        Chance = drop.Chance,
                        MinCount = drop.MinCount,
                        MaxCount = drop.MaxCount
                    });
                }
            }
        }
    }
    
    [Serializable]
    public class Drop
    {
        public GameObject Prefab;
        [Range(0,100)]
        public int Chance;
        public int MinCount;
        public int MaxCount;
    }
}

[UpdateBefore(typeof(DestroySystem))]
[UpdateInGroup(typeof(DestroySystemGroup))]
public partial struct ItemDropOnDestroySystem : ISystem
{
    EntityQuery m_CleanupQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<ItemDropOnDestroy, DestroyFlag>().Build();
        state.RequireForUpdate(m_CleanupQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        state.Dependency = new Job()
        {
            ecb = delayedEcb,
            baseRandom = SystemAPI.GetSingleton<SharedRandom>().Random,
            rotationalInertiaLookup = SystemAPI.GetComponentLookup<RotationalInertia>(true),
            movementLookup = SystemAPI.GetComponentLookup<Movement>(true)
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(DestroyFlag))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
        [ReadOnly] public Random baseRandom;
        [ReadOnly] public ComponentLookup<RotationalInertia> rotationalInertiaLookup;
        [ReadOnly] public ComponentLookup<Movement> movementLookup;
    
        public void Execute(Entity entity, in DynamicBuffer<ItemDropOnDestroy> items, in LocalTransform transform)
        {
            var random = baseRandom;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].Drop == Entity.Null) continue;
                if (random.NextInt(100) >= items[i].Chance) continue;
                
                var count = random.NextInt(items[i].MinCount, items[i].MaxCount + 1);
                for (int loop = 0; loop < count; loop++)
                {
                    var newDropE = ecb.Instantiate(items[i].Drop);
                    ecb.SetComponent(newDropE, transform);
                
                    if (rotationalInertiaLookup.HasComponent(items[i].Drop))
                    {
                        var newDropInertia = new RotationalInertia();
                        newDropInertia.Set(random.NextFloat3(), 1);
                        ecb.SetComponent(newDropE, newDropInertia);
                    }
                
                    if (movementLookup.HasComponent(items[i].Drop) && movementLookup.TryGetComponent(entity, out var movement))
                    {
                        var newDropMovement = new Movement();
                        newDropMovement.Velocity = movement.Velocity + (math.length(movement.Velocity)/2 + 1) * random.NextFloat3();
                        ecb.SetComponent(newDropE, newDropMovement);
                    }
                }
            }
        }
    }
}