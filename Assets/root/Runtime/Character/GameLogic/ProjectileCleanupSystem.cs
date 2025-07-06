using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(GameLogicSystemGroup))]
public partial struct ProjectileCleanupSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            CurrentTime = SystemAPI.Time.ElapsedTime
        }.Schedule();
    }
    
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public Entity ProjectilePrefab;
        [ReadOnly] public double CurrentTime;
    
        public void Execute([ChunkIndexInQuery] int key, Entity entity, in Projectile projectile)
        {
            if (projectile.DestroyTime < CurrentTime)
                ecb.DestroyEntity(key, entity);
        }
    }
}