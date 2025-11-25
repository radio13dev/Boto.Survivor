using BovineLabs.Core.SingletonCollection;
using Collisions;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(ProjectileDamageSystemGroup))]
[BurstCompile]
public partial struct ProjectileHitSystem_Chain : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<NetworkIdMapping>();
        state.RequireForUpdate<GameManager.Prefabs>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var prefabs = SystemAPI.GetSingletonBuffer<GameManager.Prefabs>(true);
        state.Dependency = new Job()
        {
            ecb = delayedEcb,
            Time = SystemAPI.Time.ElapsedTime,
            SharedRandom = SystemAPI.GetSingleton<SharedRandom>(),
            networkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>(),
            ChainEntity = prefabs[GameManager.Prefabs.PlayerProjectile_Chain].Entity,
            ChainVisualEntity = prefabs[GameManager.Prefabs.PlayerProjectile_ChainVisual].Entity,
            EnemyColliderTree = state.WorldUnmanaged.GetUnsafeSystemRef<EnemyColliderTreeSystem>(state.WorldUnmanaged.GetExistingUnmanagedSystem<EnemyColliderTreeSystem>()).Tree,
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(ProjectileHit))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
        [ReadOnly] public double Time;
        [ReadOnly] public SharedRandom SharedRandom;
        [ReadOnly] public NetworkIdMapping networkIdMapping;
        [ReadOnly] public Entity ChainEntity;
        [ReadOnly] public Entity ChainVisualEntity;
        [ReadOnly] public NativeOctree<(Entity e, NetworkId id, Collider c)> EnemyColliderTree;
    
        public void Execute(in DynamicBuffer<ProjectileHitEntity> hits, in LocalTransform t, in Projectile projectile, in Chain chain)
        {
            var r = SharedRandom.Random;
            // Create a chain projectile entity at X target positions.
            // The rendering side of things will automatically stretch the visuals between those positions.
                    
            // The chain projectiles themselves will just apply the damage as normal in a later frame.
            int chainDamage = projectile.Damage*chain.Value;
            
            // Chain to the 5 nearest enemies
            var visitor = new Chain.NearestVisitorArray();
            var distance = new EnemyColliderTree.DistanceProvider();
            EnemyColliderTree.Nearest(t.Position, Chain.k_MaxChainDistance, ref visitor, distance);
            
            for (int i = 0; i < hits.Length; i++)
            {
                var chainFromE = networkIdMapping[hits[i].Value];
                if (chainFromE != Entity.Null)
                {
                    Chain.Setup(ref ecb, Time, projectile.IsDoT ? 0.05d : 1d, ChainEntity, ChainVisualEntity, chainDamage, t, visitor);
                }
            }
        }
    }
}