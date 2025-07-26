using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct RingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GameManager.Projectiles>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        state.Dependency = new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            Time = SystemAPI.Time.ElapsedTime,
            Projectiles = SystemAPI.GetSingletonBuffer<GameManager.Projectiles>()
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public double Time;
        [ReadOnly] public DynamicBuffer<GameManager.Projectiles> Projectiles;

        [BurstCompile]
        public void Execute([EntityIndexInChunk] int Key, in LocalTransform transform, in CompiledStats compiledStats, ref DynamicBuffer<Ring> rings)
        {
            for (int i = 0; i < rings.Length; ++i)
            {
                ref var ring = ref rings.ElementAt(i);
                if (!ring.Stats.PrimaryEffect.IsTimed()) continue;
                
                var cooldown = ring.Stats.PrimaryEffect.GetCooldown(compiledStats.CombinedRingStats.ProjectileRate);
                var activateTime = ring.LastActivateTime + cooldown;
                if (activateTime > Time) continue;
                
                // Activate
                ring.LastActivateTime = Time;
                
                // Get shared stats
                float projectileSpeed = ring.Stats.PrimaryEffect.GetProjectileSpeed(compiledStats.CombinedRingStats.ProjectileSpeed);
                float projectileDuration = ring.Stats.PrimaryEffect.GetProjectileDuration(compiledStats.CombinedRingStats.ProjectileDuration);
                float projectileDamage = ring.Stats.PrimaryEffect.GetProjectileDamage(compiledStats.CombinedRingStats.ProjectileSpeed);
                float projectileSize = ring.Stats.PrimaryEffect.GetProjectileSize(compiledStats.CombinedRingStats.ProjectileDuration);
                
                // Fire projectiles
                switch (ring.Stats.PrimaryEffect)
                {
                    // Fires projectiles in a ring around the player:
                    // - Slight randomization in initial spawn points and angles
                    // - ProjectileCount: TODO: Increase radial count OR DOUBLE the count of projectiles (double sounds funner)
                    case RingPrimaryEffect.Projectile_Ring:
                        const float Projectile_Ring_CharacterOffset = 1f;
                    
                        var template = Projectiles[0];
                        var projectileCount = 8;
                        var angStep = math.PI2/projectileCount;
                        var loopCount = 1 + compiledStats.CombinedRingStats.ProjectileCount;
                        
                        // Instantiate all projectiles at once
                        var allProjectiles = new NativeArray<Entity>(projectileCount*loopCount, Allocator.Temp);
                        ecb.Instantiate(Key, template.Entity, allProjectiles);
                        
                        // Then modify them 1 by 1
                        for (int loopIt = 0; loopIt < loopCount; loopIt++)
                        {
                            var ang0 = loopIt*angStep/loopCount;
                            for (int projIt = 0; projIt < projectileCount; projIt++)
                            {
                                var ang = ang0 + projIt*angStep;
                                var projectileE = allProjectiles[loopIt*projectileCount + projIt];
                                
                                var projectileT = transform;
                                projectileT.Rotation = math.mul(quaternion.AxisAngle(transform.Up(), ang), transform.Rotation);
                                projectileT.Position = projectileT.Position + projectileT.Forward() * Projectile_Ring_CharacterOffset * projectileSize;
                                projectileT.Scale = projectileSize;
                                ecb.SetComponent(Key, projectileE, projectileT);
                                
                                ecb.SetComponent(Key, projectileE, new SurfaceMovement() { Velocity = new float2(projectileSpeed, 0) });
                                ecb.SetComponent(Key, projectileE, new DestroyAtTime(){ DestroyTime = Time + projectileDuration });
                                ecb.SetComponent(Key, projectileE, new Projectile(){ Damage = projectileDamage });
                            }
                        }
                        
                        break;
                }

            }
        }
    }
}