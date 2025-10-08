using BovineLabs.Core.SingletonCollection;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(ProjectileSystemGroup))]
[BurstCompile]
public partial struct ProjectileHitSystem_Chain : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkIdMapping>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new Job()
        {
            ecb = ,
            networkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>()
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(ProjectileHit))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
        [ReadOnly] public SharedRandom SharedRandom;
        [ReadOnly] public NetworkIdMapping networkIdMapping;
    
        public void Execute(in DynamicBuffer<ProjectileHitEntity> hits, in Projectile projectile, in Chain chain)
        {
            var r = SharedRandom.Random;
            // Create a chain projectile entity at X target positions.
            // The rendering side of things will automatically stretch the visuals between those positions.
                    
            // The chain projectiles themselves will just apply the damage as normal in a later frame.
            int chainDamage;
                    
            // FIRST: Any DoT projectiles need to roll to determine if the effect occurs
            if (projectile.IsDoT)
            {
                
                chainDamage = projectile.Damage*Projectile.InvPerFrameDamageMod;
            }
            else
            {
                chainDamage = projectile.Damage*chain.Value;
            }
            
            for (int i = 0; i < hits.Length; i++)
            {
                var e = networkIdMapping[hits[i].Value];
                if (e != Entity.Null)
                {
                }
            }
        }
    }
}