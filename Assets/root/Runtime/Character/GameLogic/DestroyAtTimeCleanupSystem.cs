using Unity.Collections;
using Unity.Entities;

public struct DestroyAtTime : IComponentData
{
    public double DestroyTime;
}

[UpdateInGroup(typeof(GameLogicSystemGroup))]
public partial struct DestroyAtTimeCleanupSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var currentTime = SystemAPI.Time.ElapsedTime;
        foreach ((var projectile, var entity) in SystemAPI.Query<RefRO<DestroyAtTime>>().WithEntityAccess())
        {
            if (projectile.ValueRO.DestroyTime < currentTime)
                delayedEcb.AddComponent<DestroyFlag>(entity);
        }
    }
}