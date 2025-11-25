using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(DestroySystemGroup))]
public partial struct OwnedProjectileCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        
        state.Dependency = new Job()
        {
            ecb = ecb.AsParallelWriter(),
            networkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>()
        }.Schedule(state.Dependency);
    }

    [WithAll(typeof(DestroyFlag))]
    public partial struct Job : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public NetworkIdMapping networkIdMapping;
        
        public void Execute([ChunkIndexInQuery] int Key, in DynamicBuffer<OwnedProjectiles> ownedProjectiles)
        {
            for (int i = 0; i < ownedProjectiles.Length; i++)
            {
                var projectile = networkIdMapping[ownedProjectiles[i].NetworkId];
                if (projectile == Entity.Null)
                {
                    continue;
                }
                
                ecb.SetComponentEnabled<DestroyFlag>(Key, projectile, true);
            }
        }
    }
}