using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public struct EntityOnDestroy : IComponentData
{
    public Entity Prefab;
    public int Count;
}

public class EntityOnDestroyAuthoring : MonoBehaviour
{
    public GameObject Prefab;
    public int Count;
    public class Baker : Baker<EntityOnDestroyAuthoring>
    {
        public override void Bake(EntityOnDestroyAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new EntityOnDestroy(){ Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.None), Count = authoring.Count });
        }
    }
}

[UpdateBefore(typeof(DestroySystem))]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
public partial struct EntityOnDestroySystem : ISystem
{
    EntityQuery m_CleanupQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<EntityOnDestroy, DestroyFlag>().Build();
        state.RequireForUpdate(m_CleanupQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        Random random = new(unchecked((uint)SystemAPI.Time.ElapsedTime));
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var (onDestroy, transform, movement) in SystemAPI.Query<RefRO<EntityOnDestroy>, RefRO<LocalTransform>, RefRO<Movement>>()
            .WithAll<DestroyFlag>()
            )
        {
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