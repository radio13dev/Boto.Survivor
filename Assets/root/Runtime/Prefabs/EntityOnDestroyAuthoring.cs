using BovineLabs.Saving;
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
            if (!DependsOn(authoring.Prefab))
            {
                Debug.LogError($"EntityOnDestroyAuthoring must have a Prefab assigned: {authoring.gameObject}", authoring);
                return;
            }
            if (!DependsOn(GetComponent<DestroyableAuthoring>()))
            {
                Debug.LogError($"EntityOnDestroyAuthoring must have a DestroyableAuthoring component on the same GameObject: {authoring.gameObject}", authoring);
                return;
            }
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new EntityOnDestroy(){ Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.None), Count = authoring.Count });
        }
    }
}

[UpdateBefore(typeof(DestroySystem))]
[UpdateInGroup(typeof(DestroySystemGroup))]
public partial struct EntityOnDestroySystem : ISystem
{
    EntityQuery m_CleanupQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<EntityOnDestroy, DestroyFlag>().Build();
        state.RequireForUpdate(m_CleanupQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var (onDestroy, transform, movement, entity) in SystemAPI.Query<RefRO<EntityOnDestroy>, RefRO<LocalTransform>, RefRO<Movement>>()
            .WithAll<DestroyFlag>().WithEntityAccess()
            )
        {
            Random random = SystemAPI.GetSingleton<SharedRandom>().Random;
            Debug.Log($"Creating {onDestroy.ValueRO.Count} entities on destroy");
            for (int i = 0; i < onDestroy.ValueRO.Count; i++)
            {
                var newEntity = delayedEcb.Instantiate(onDestroy.ValueRO.Prefab);
                delayedEcb.SetComponent(newEntity, transform.ValueRO);
                
                if (SystemAPI.HasComponent<RotationalInertia>(onDestroy.ValueRO.Prefab))
                {
                    var inertia = new RotationalInertia();
                    inertia.Set(random.NextFloat3(), 1);
                    delayedEcb.SetComponent(newEntity, inertia);
                }
                
                if (SystemAPI.HasComponent<Movement>(onDestroy.ValueRO.Prefab))
                {
                    var newEntityMovement = new Movement();
                    newEntityMovement.Velocity = movement.ValueRO.Velocity + (math.length(movement.ValueRO.Velocity)/2 + 1) * random.NextFloat3();
                    delayedEcb.SetComponent(newEntity, newEntityMovement);
                }

                if (SystemAPI.HasComponent<SurvivorTag>(entity))
                {
                    delayedEcb.SetComponent(newEntity, SystemAPI.GetComponent<PlayerControlledSaveable>(entity));
                }
            }
        }
    }
}