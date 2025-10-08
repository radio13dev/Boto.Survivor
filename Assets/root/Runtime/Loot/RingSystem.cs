using Collisions;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using AABB = NativeTrees.AABB;
using Collider = Collisions.Collider;
using Entity = Unity.Entities.Entity;
using Random = Unity.Mathematics.Random;

[BurstCompile]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial struct RingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkIdMapping>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GameManager.Projectiles>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        state.Dependency = new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            Time = SystemAPI.Time.ElapsedTime,
            Projectiles = SystemAPI.GetSingletonBuffer<GameManager.Projectiles>(true),
            EnemyColliderTree = state.WorldUnmanaged.GetUnsafeSystemRef<EnemyColliderTreeSystem>(state.WorldUnmanaged.GetExistingUnmanagedSystem<EnemyColliderTreeSystem>()).Tree,
            SharedRandom = SystemAPI.GetSingleton<SharedRandom>().Random,
            NetworkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>(),
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true)
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public double Time;
        [ReadOnly] public DynamicBuffer<GameManager.Projectiles> Projectiles;
        [ReadOnly] public NativeOctree<(Entity e, NetworkId id, Collider c)> EnemyColliderTree;
        [ReadOnly] public Random SharedRandom;
        [ReadOnly] public NetworkIdMapping NetworkIdMapping;
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;

        [BurstCompile]
        public void Execute([EntityIndexInChunk] int Key, in PlayerControlled playerId, in LocalTransform transform, in Movement movement, in CompiledStats compiledStats,
            ref DynamicBuffer<Ring> rings, ref DynamicBuffer<EquippedGem> equippedGems,
            ref DynamicBuffer<OwnedProjectiles> ownedProjectiles,
            ref DynamicBuffer<ProjectileLoopTriggerQueue> triggerQueue)
        {
            for (int ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                ref var ring = ref rings.ElementAt(ringIndex);
                if (!ring.Stats.IsValid) continue;
                if (!ring.Stats.PrimaryEffect.IsTimed()) continue;

                if (ring.NextActivateTime > Time) continue;

                // Activate
                ring.NextActivateTime = Time + ring.Stats.PrimaryEffect.GetCooldown(0)*compiledStats.CompiledStatsTree.Cooldown;

                // Activate the ring
                ActivateRing(ringIndex, Key, in playerId, in transform, in movement, in compiledStats, ref rings, ref equippedGems, ref ownedProjectiles, default);
            }

            if (triggerQueue.Length > 0)
            {
                NativeList<(Entity e, NetworkId id, Collider c)> ignoreBuffer = new NativeList<(Entity e, NetworkId id, Collider c)>(4, Allocator.Temp);
                DynamicBuffer<OwnedProjectiles> fakeIgnoredProjectiles = default;
                for (int i = 0; i < triggerQueue.Length; i++)
                {
                    var trigger = triggerQueue[i];
                    if (trigger.RingIndex < 0 || trigger.RingIndex >= rings.Length) continue;
                    if (trigger.LoopCount >= ProjectileLoopTrigger.k_MaxLoopCount) continue;
                    if (!rings[trigger.RingIndex].Stats.IsValid) continue;

                    ignoreBuffer.Clear();
                    EnemyColliderTree.RangeAABB(new AABB(trigger.HitT.Position - new float3(1, 1, 1), trigger.HitT.Position + new float3(1, 1, 1)), ignoreBuffer);
                    ActivateRing(trigger.RingIndex, Key, in playerId, in trigger.HitT, in movement, in compiledStats, ref rings, ref equippedGems, ref fakeIgnoredProjectiles, in ignoreBuffer);
                }

                triggerQueue.Clear();
            }
        }

        private unsafe void ActivateRing(int ringIndex, int Key, in PlayerControlled playerId, in LocalTransform transform, in Movement movement,
            in CompiledStats compiledStats, ref DynamicBuffer<Ring> rings, ref DynamicBuffer<EquippedGem> equippedGems,
            ref DynamicBuffer<OwnedProjectiles> ownedProjectiles,
            in NativeList<(Entity e, NetworkId id, Collider c)> ignoreNearbyBuffer)
        {
            var r = SharedRandom;
            ref var ring = ref rings.ElementAt(ringIndex);

            PrimaryEffectStack stack = new(in rings, ringIndex);

            // Fire projectiles
            for (int effectIt = 0; effectIt < (int)RingPrimaryEffect.Length; effectIt++)
            {
                byte tier = stack.Stacks[effectIt];
                if (tier == 0) continue;

                RingPrimaryEffect effect = (RingPrimaryEffect)(1 << effectIt);

                switch (effect)
                {
                    default:
                        Debug.LogWarning($"Unhandled RingPrimaryEffect: {effect}");
                        break;

                    case RingPrimaryEffect.Projectile_Ring:
                    {
                        const float Projectile_Ring_CharacterOffset = 1f;

                        var template = Projectiles[0];
                        var projectileCount = 8;
                        var angStep = math.PI2 / projectileCount;
                        var loopCount = 1 + compiledStats.CompiledStatsTree.ExtraProjectiles;

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
                                projectileT.Position = projectileT.Position + projectileT.Right() * Projectile_Ring_CharacterOffset * compiledStats.CompiledStatsTree.Size;
                                projectileT.Scale = compiledStats.CompiledStatsTree.Size;
                                ecb.SetComponent(Key, projectileE, projectileT);

                                ecb.SetComponent(Key, projectileE, ProjectileLoopTrigger.Empty);
                                //ecb.SetComponent(Key, projectileE, new ProjectileLoopTrigger(0, (byte)playerId.Index, (byte)ringIndex));

                                ecb.SetComponent(Key, projectileE, new SurfaceMovement() { PerFrameVelocity = new float3(10f*compiledStats.CompiledStatsTree.ProjectileSpeed, 0, 0) });
                                ecb.SetComponent(Key, projectileE, new DestroyAtTime() { DestroyTime = Time + 2 });
                                ecb.SetComponent(Key, projectileE, new Projectile(compiledStats.CompiledStatsTree.Damage));

                                if (ignoreNearbyBuffer.IsCreated)
                                    ecb.SetBuffer<ProjectileIgnoreEntity>(Key, projectileE).AddRange(ignoreNearbyBuffer.AsArray().Reinterpret<ProjectileIgnoreEntity>());
                            }
                        }

                        break;
                    }

                    case RingPrimaryEffect.Projectile_NearestRapid:
                    {
                        const float Projectile_NearestRapid_CharacterOffset = 1f;

                        var template = Projectiles[0];
                        var loopCount = 1 + compiledStats.CompiledStatsTree.ExtraProjectiles;

                        var visitor = new EnemyColliderTree.NearestVisitorCount(){ DesiredHits = tier };
                        var distance = new EnemyColliderTree.DistanceProvider();
                        EnemyColliderTree.Nearest(transform.Position, 30, ref visitor, distance);

                        float3 dir;
                        if (visitor.Hits > 0) dir = visitor.Nearest.Center - transform.Position;
                        else dir = movement.LastDirection;

                        for (int loopIt = 0; loopIt < loopCount; loopIt++)
                        {
                            var projectileE = ecb.Instantiate(Key, template.Entity);
                            var projectileT = transform;
                            projectileT.Rotation = math.mul(quaternion.AxisAngle(transform.Up(), r.NextFloat(-0.05f, 0.05f) - math.PIHALF),
                                quaternion.LookRotationSafe(dir, transform.Up()));
                            projectileT.Position = projectileT.Position + projectileT.Right() * Projectile_NearestRapid_CharacterOffset * compiledStats.CompiledStatsTree.Size;
                            projectileT.Scale = compiledStats.CompiledStatsTree.Size;
                            ecb.SetComponent(Key, projectileE, projectileT);

                            ecb.SetComponent(Key, projectileE, ProjectileLoopTrigger.Empty);
                            //ecb.SetComponent(Key, projectileE, new ProjectileLoopTrigger(0, (byte)playerId.Index, (byte)ringIndex));

                            ecb.SetComponent(Key, projectileE, new SurfaceMovement() { PerFrameVelocity = new float3(20*compiledStats.CompiledStatsTree.ProjectileSpeed, 0, 0) });
                            ecb.SetComponent(Key, projectileE, new DestroyAtTime() { DestroyTime = Time + 1 });
                            ecb.SetComponent(Key, projectileE, new Projectile(compiledStats.CompiledStatsTree.Damage));

                            if (ignoreNearbyBuffer.IsCreated)
                                ecb.SetBuffer<ProjectileIgnoreEntity>(Key, projectileE).AddRange(ignoreNearbyBuffer.AsArray().Reinterpret<ProjectileIgnoreEntity>());
                        }

                        break;
                    }

                    case RingPrimaryEffect.Projectile_Seeker:
                    {
                        // Check if seekers (of our desired tier) are still active
                        byte requiredSeekerCount = (byte)(compiledStats.CompiledStatsTree.ExtraProjectiles + tier + 4);
                        
                        // If not, rebuild those seekers
                        var template = Projectiles[SeekerProjectileData.TemplateIndex + (tier - 1)];
                        for (byte projSpawnIt = 0; projSpawnIt < requiredSeekerCount; projSpawnIt++)
                        {
                            if (ownedProjectiles.IsCreated)
                            {
                                bool hitMatch = false;
                                for (int ownedProjIt = 0; ownedProjIt < ownedProjectiles.Length; ownedProjIt++)
                                {
                                    var proj = ownedProjectiles[ownedProjIt];
                                    if (!LocalTransformLookup.HasComponent(NetworkIdMapping[proj.NetworkId]))
                                    {
                                        ownedProjectiles.RemoveAtSwapBack(ownedProjIt);
                                        ownedProjIt--;
                                        continue;
                                    }

                                    if (proj.Key.PrimaryEffect != RingPrimaryEffect.Projectile_Seeker) continue;
                                    if (proj.Key.Tier != tier) continue;
                                    if (proj.Key.Index != projSpawnIt) continue;

                                    hitMatch = true;
                                    break;
                                }

                                if (hitMatch) continue;
                            }
                                
                            var projectileE = ecb.Instantiate(Key, template.Entity);
                            var projectileT = transform;
                            projectileT.Position += r.NextFloat3Direction();
                            projectileT.Scale = compiledStats.CompiledStatsTree.Size;
                            ecb.SetComponent(Key, projectileE, projectileT);

                            ecb.SetComponent(Key, projectileE, ProjectileLoopTrigger.Empty);
                            
                            ecb.SetComponent(Key, projectileE, new SeekerProjectileData()
                            {
                                CreateTime = Time,
                                SeekerCount = requiredSeekerCount
                            });
                            
                            Projectile.Setup(in Key, ref ecb, in projectileE, in compiledStats, ref r);

                            ecb.SetComponent(Key, projectileE, new MovementSettings() { Speed = compiledStats.CompiledStatsTree.ProjectileSpeed*30f });
                            ecb.SetComponent(Key, projectileE, new DestroyAtTime() { DestroyTime = double.MaxValue });
                            ecb.SetComponent(Key, projectileE, new OwnedProjectile(){ PlayerId = playerId.Index, Key = new ProjectileKey(effect, tier, projSpawnIt) });
                            ecb.SetComponent(Key, projectileE, new Pierce(){ Value = compiledStats.CompiledStatsTree.PierceCount });

                            if (ignoreNearbyBuffer.IsCreated)
                                ecb.SetBuffer<ProjectileIgnoreEntity>(Key, projectileE).AddRange(ignoreNearbyBuffer.AsArray().Reinterpret<ProjectileIgnoreEntity>());
                        }
                        
                        // ... those seekers will register themselves to our 'owned projectiles' list in their own time before this method triggers again
                        break;
                    }

                    case RingPrimaryEffect.Projectile_Band:
                    {
                        // Create the 'band' projectile for our desired tier

                        break;
                    }
                    
                    case RingPrimaryEffect.Projectile_Melee:
                    { 
                        // Does nothing, stat refresh generates these objects
                        break;
                    }
                    
                    case RingPrimaryEffect.Projectile_Returning:
                    {
                        // Does nothing, stat refresh generates these objects
                        break;
                    }
                    
                    case RingPrimaryEffect.Projectile_Mark:
                    {
                        // Scan for enemies within a (minRad, maxRad) band around the player
                        
                        // Create a projectile targeting each of those enemies
                        break;
                    }
                    
                    case RingPrimaryEffect.Projectile_Orbit:
                    {
                        // Does nothing, stat refresh generates these objects
                        break;
                    }
                }
            }
        }
    }
}