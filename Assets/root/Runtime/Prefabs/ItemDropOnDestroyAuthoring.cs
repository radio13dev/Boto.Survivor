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
                drops.Add(new ItemDropOnDestroy()
                {
                    Drop = GetEntity(drop.Prefab, TransformUsageFlags.None),
                    Chance = drop.Chance
                });
            }
        }
    }
    
    [Serializable]
    public class Drop
    {
        public GameObject Prefab;
        [Range(0,100)]
        public int Chance;
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
        foreach (var (onDestroy, transform, movement) in SystemAPI.Query<RefRO<ItemDropOnDestroy>, RefRO<LocalTransform>, RefRO<Movement>>()
            .WithAll<DestroyFlag>()
            )
        {
            Random random = SystemAPI.GetSingleton<SharedRandom>().Random;
            Debug.Log($"Creating {10} entities on destroy");
            for (int i = 0; i < 10; i++)
            {
                var entity = delayedEcb.Instantiate(onDestroy.ValueRO.Prefab);
                delayedEcb.SetComponent(entity, transform.ValueRO);
                
                var inertia = new RotationalInertia();
                inertia.Set(random.NextFloat3(), 1);
                delayedEcb.SetComponent(entity, inertia);
                
                var newEntityMovement = new Movement();
                newEntityMovement.Velocity = movement.ValueRO.Velocity + (math.length(movement.ValueRO.Velocity)/2 + 1) * random.NextFloat3();
                delayedEcb.SetComponent(entity, newEntityMovement);
            }
        }
    }
}