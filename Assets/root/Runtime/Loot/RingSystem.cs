using Collisions;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
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
            Projectiles = SystemAPI.GetSingletonBuffer<GameManager.Projectiles>(),
            EnemyColliderTree = state.WorldUnmanaged.GetUnsafeSystemRef<EnemyColliderTreeSystem>(state.WorldUnmanaged.GetExistingUnmanagedSystem<EnemyColliderTreeSystem>()).Tree,
            SharedRandom = SystemAPI.GetSingleton<SharedRandom>().Random
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public double Time;
        [ReadOnly] public DynamicBuffer<GameManager.Projectiles> Projectiles;
        [ReadOnly] public NativeOctree<Entity> EnemyColliderTree;
        [ReadOnly] public Random SharedRandom;

        [BurstCompile]
        public void Execute([EntityIndexInChunk] int Key, in LocalTransform transform, in Movement movement, in CompiledStats compiledStats, ref DynamicBuffer<Ring> rings, ref DynamicBuffer<EquippedGem> equippedGems)
        {
            var r = SharedRandom;
            for (int i = 0; i < rings.Length; ++i)
            {
                ref var ring = ref rings.ElementAt(i);
                if (!ring.Stats.PrimaryEffect.IsTimed()) continue;

                var cooldown = ring.Stats.PrimaryEffect.GetCooldown(compiledStats.ProjectileRate);
                var activateTime = ring.LastActivateTime + cooldown;
                if (activateTime > Time) continue;

                // Activate
                ring.LastActivateTime = Time;

                // Get shared stats
                float projectileSpeed = ring.Stats.PrimaryEffect.GetProjectileSpeed(compiledStats.ProjectileSpeed);
                float projectileDuration = ring.Stats.PrimaryEffect.GetProjectileDuration(compiledStats.ProjectileDuration);
                float projectileDamage = ring.Stats.PrimaryEffect.GetProjectileDamage(compiledStats.ProjectileSpeed);
                float projectileSize = ring.Stats.PrimaryEffect.GetProjectileSize(compiledStats.ProjectileDuration);
                
                // Get gem mods
                int mod_ProjectileCount= 0;
                
                var gemMin = i*Gem.k_GemsPerRing;
                var gemMax = (i + 1)*Gem.k_GemsPerRing;
                for (int gemIndex = gemMin; gemIndex < gemMax; gemIndex++)
                {
                    switch (equippedGems[gemIndex].Gem.GemType)
                    {
                        case Gem.Type.Multishot:
                            mod_ProjectileCount++;
                            break;
                    }
                }

                // Fire projectiles
                switch (ring.Stats.PrimaryEffect)
                {
                    // Fires projectiles in a ring around the player:
                    // - Slight randomization in initial spawn points and angles
                    // - ProjectileCount: TODO: Increase radial count OR DOUBLE the count of projectiles (double sounds funner)
                    case RingPrimaryEffect.Projectile_Ring:
                    {
                        const float Projectile_Ring_CharacterOffset = 1f;

                        var template = Projectiles[0];
                        var projectileCount = 8;
                        var angStep = math.PI2 / projectileCount;
                        var loopCount = 1 + mod_ProjectileCount;

                        // Instantiate all projectiles at once
                        var allProjectiles = new NativeArray<Entity>(projectileCount * loopCount, Allocator.Temp);
                        ecb.Instantiate(Key, template.Entity, allProjectiles);

                        // Then modify them 1 by 1
                        for (int loopIt = 0; loopIt < loopCount; loopIt++)
                        {
                            var ang0 = loopIt * angStep / loopCount;
                            for (int projIt = 0; projIt < projectileCount; projIt++)
                            {
                                var ang = ang0 + projIt * angStep;
                                var projectileE = allProjectiles[loopIt * projectileCount + projIt];

                                var projectileT = transform;
                                projectileT.Rotation = math.mul(quaternion.AxisAngle(transform.Up(), ang), transform.Rotation);
                                projectileT.Position = projectileT.Position + projectileT.Right() * Projectile_Ring_CharacterOffset * projectileSize;
                                projectileT.Scale = projectileSize;
                                ecb.SetComponent(Key, projectileE, projectileT);

                                ecb.SetComponent(Key, projectileE, new SurfaceMovement() { Velocity = new float2(projectileSpeed, 0) });
                                ecb.SetComponent(Key, projectileE, new DestroyAtTime() { DestroyTime = Time + projectileDuration });
                                ecb.SetComponent(Key, projectileE, new Projectile() { Damage = projectileDamage });
                            }
                        }

                        break;
                    }

                    case RingPrimaryEffect.Projectile_NearestRapid:
                    {
                        const float Projectile_NearestRapid_CharacterOffset = 1f;

                        var template = Projectiles[0];
                        var projectileCount = 1;
                        var loopCount = 1 + mod_ProjectileCount;

                        var visitor = new EnemyColliderTree.NearestVisitor();
                        var distance = new EnemyColliderTree.DistanceProvider();
                        EnemyColliderTree.Nearest(transform.Position, 30, ref visitor, distance);

                        float3 dir;
                        if (visitor.Hits > 0) dir = visitor.Nearest.Center - transform.Position;
                        else dir = movement.LastDirection;

                        for (int loopIt = 0; loopIt < loopCount; loopIt++)
                        {
                            var projectileE = ecb.Instantiate(Key, template.Entity);
                            var projectileT = transform;
                            projectileT.Rotation = math.mul(quaternion.AxisAngle(transform.Up(), r.NextFloat(-0.05f, 0.05f) - math.PIHALF), quaternion.LookRotationSafe(dir, transform.Up()));
                            projectileT.Position = projectileT.Position + projectileT.Right() * Projectile_NearestRapid_CharacterOffset * projectileSize;
                            projectileT.Scale = projectileSize;
                            ecb.SetComponent(Key, projectileE, projectileT);

                            ecb.SetComponent(Key, projectileE, new SurfaceMovement() { Velocity = new float2(projectileSpeed, 0) });
                            ecb.SetComponent(Key, projectileE, new DestroyAtTime() { DestroyTime = Time + projectileDuration });
                            ecb.SetComponent(Key, projectileE, new Projectile() { Damage = projectileDamage });
                        }

                        break;
                    }
                }
            }
        }
    }
}