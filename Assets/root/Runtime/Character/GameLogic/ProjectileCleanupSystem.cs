using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(GameLogicSystemGroup))]
public partial struct ProjectileCleanupSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.SinglePlayback);// SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var currentTime = SystemAPI.Time.ElapsedTime;
        foreach ((var projectile, var entity) in SystemAPI.Query<RefRO<Projectile>>().WithEntityAccess())
        {
            if (projectile.ValueRO.DestroyTime < currentTime)
                delayedEcb.DestroyEntity(entity);
        }
        delayedEcb.Playback(state.EntityManager);
        delayedEcb.Dispose();
    }
}