using BovineLabs.Saving;
using Collisions;
using Unity.Entities;

[Save]
public struct SpawnTimeCreated : IComponentData
{
    public double TimeCreated;

    public SpawnTimeCreated(double time)
    {
        TimeCreated = time;
    }
}

[Save]
public struct DestroyAtTime : IComponentData
{
    public double DestroyTime;

    public DestroyAtTime(double time)
    {
        DestroyTime = time;
    }
}

[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial struct DestroyAtTimeCleanupSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var currentTime = SystemAPI.Time.ElapsedTime;
        foreach ((var projectile, var entity) in SystemAPI.Query<RefRO<DestroyAtTime>>().WithDisabled<DestroyFlag>().WithEntityAccess())
        {
            if (projectile.ValueRO.DestroyTime < currentTime)
            {
                delayedEcb.SetComponentEnabled<DestroyFlag>(entity, true);
                if (SystemAPI.HasComponent<EnableColliderOnDestroy>(entity))
                    delayedEcb.SetComponentEnabled<Collider>(entity, true);
            }
        }
    }
}