using BovineLabs.Saving;
using Unity.Entities;

[Save]
public struct DestroyAtTime : IComponentData
{
    public double DestroyTime;
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
        foreach ((var projectile, var entity) in SystemAPI.Query<RefRO<DestroyAtTime>>().WithAbsent<DestroyFlag>().WithEntityAccess())
        {
            if (projectile.ValueRO.DestroyTime < currentTime)
                delayedEcb.AddComponent<DestroyFlag>(entity);
        }
    }
}