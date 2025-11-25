using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(ProjectileDamageSystemGroup))]
[BurstCompile]
public partial struct ProjectileHitSystem_Cut : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<NetworkIdMapping>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        
        state.Dependency = new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            networkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>()
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(ProjectileHit))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public NetworkIdMapping networkIdMapping;
    
        public void Execute([ChunkIndexInQuery] int Key, in DynamicBuffer<ProjectileHitEntity> hits, in Cut cut)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var e = networkIdMapping[hits[i].Value];
                if (e != Entity.Null)
                {
                    ecb.AppendToBuffer(Key, e, Pending.Cut(cut.Value));
                    ecb.SetComponentEnabled<Pending.Dirty>(Key, e, true);
                }
            }
        }
    }
}