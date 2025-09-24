using System;
using BovineLabs.Saving;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct CompiledStats : IComponentData
{
    public TiledStatsTree CompiledStatsTree;

    public static CompiledStats GetDemo(TiledStatsTree baseStats)
    {
        return new CompiledStats()
        {
            CompiledStatsTree = baseStats + TiledStatsTree.Demo
        };
    }
}

[Save]
public struct CompiledStatsDirty : IComponentData, IEnableableComponent
{
    /// <summary>
    /// Specifices which particular primary effects have been added/removed
    /// </summary>
    public RingPrimaryEffect DirtyFlags;

    public void SetDirty()
    {
        DirtyFlags |= (RingPrimaryEffect)byte.MaxValue;
    }
    public void SetDirty(Ring ring)
    {
        DirtyFlags |= ring.Stats.PrimaryEffect;
    }
    public void SetDirty(RingStats ringStats)
    {
        DirtyFlags |= ringStats.PrimaryEffect;
    }
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
[UpdateBefore(typeof(ProjectileSystemGroup))]
[BurstCompile]
public partial struct CompiledStatsSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CompiledStatsDirty>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<NetworkIdMapping>();
    }

    public void OnUpdate(ref SystemState state)
    {
        using var ecb = new EntityCommandBuffer(Allocator.TempJob);
        new Job()
        {
            ecb = ecb,
            
            Time = SystemAPI.Time.ElapsedTime,
            SharedRandom = SystemAPI.GetSingleton<SharedRandom>(),
            Projectiles = SystemAPI.GetSingletonBuffer<GameManager.Projectiles>(true),
            NetworkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>()
        }.Run();
        ecb.Playback(state.EntityManager);
    }
    
    [RequireMatchingQueriesForUpdate]
    [WithAll(typeof(CompiledStatsDirty))]
    [BurstCompile]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;

        [ReadOnly] public double Time;
        [ReadOnly] public SharedRandom SharedRandom;
        [ReadOnly] public DynamicBuffer<GameManager.Projectiles> Projectiles;
        [ReadOnly] public NetworkIdMapping NetworkIdMapping;


        
        public unsafe void Execute(Entity entity, in LocalTransform transform, in PlayerControlled playerId, 
            ref CompiledStats stats, ref CompiledStatsDirty dirtyData, EnabledRefRW<CompiledStatsDirty> dirtyState, 
            in TiledStatsTree baseStats,
            in DynamicBuffer<Ring> rings, in DynamicBuffer<EquippedGem> gems, 
            ref DynamicBuffer<OwnedProjectiles> ownedProjectiles)
        {
            Debug.Log("Compiling stats...");
            
            // Compile stats
            stats = new();
            
            stats.CompiledStatsTree = baseStats;
            for (int i = 0; i < rings.Length; i++)
                for (int j = 0; j < RingStats.k_MaxStats; j++)
                    if (rings[i].Stats.GetStatBoost(j, out var stat, out var boost))
                        stats.CompiledStatsTree[stat] += boost;
                    
            // Setup...
            var r = SharedRandom.Random;
            
            // Destroy any owned projectiles that are no longer valid
            {
                PrimaryEffectStack stack = new (rings);
                for (int ownedIt = 0; ownedIt < ownedProjectiles.Length; ownedIt++)
                {
                    var key = ownedProjectiles[ownedIt].Key;
                    if (!key.PrimaryEffect.IsPersistent()) continue; // Only manage persistent projectiles
                    if ((dirtyData.DirtyFlags & key.PrimaryEffect) != 0 || stack.Stacks[key.PrimaryEffect.GetMostSigBit()] < key.Tier)
                    {
                        // Destroy this, we don't have the ring equipped anymore
                        ecb.SetComponentEnabled<DestroyFlag>(NetworkIdMapping[ownedProjectiles[ownedIt].NetworkId], true);
                        ownedProjectiles.RemoveAtSwapBack(ownedIt);
                        ownedIt--;
                    }
                }
            }
            
            // Create any missing projectiles
            for (int ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                var ring = rings[ringIndex];
                if (!ring.Stats.PrimaryEffect.IsPersistent()) continue;
                
                PrimaryEffectStack stack = new(in rings, ringIndex);
                
                // Fire projectiles
                for (int effectIt = 0; effectIt < (int)RingPrimaryEffect.Length; effectIt++)
                {
                    byte tier = stack.Stacks[effectIt];
                    if (tier == 0) continue;

                    RingPrimaryEffect effect = (RingPrimaryEffect)(1 << effectIt);
                    // Skip this if we've already created it
                    bool alreadyCreated = false;
                    for (int ownedIt = 0; ownedIt < ownedProjectiles.Length; ownedIt++)
                    {
                        var key = ownedProjectiles[ownedIt].Key;
                        if (key.PrimaryEffect == effect && key.Tier == tier)
                        {
                            alreadyCreated = true;
                            break;
                        }
                    }
                    if (alreadyCreated) continue;
                
                    // Now spawn the persistent projectile
                    switch (effect)
                    {
                        default:
                            Debug.LogWarning($"Unhandled persistent projectile: {effect}");
                            break;
                            
                        case RingPrimaryEffect.Projectile_Seeker:
                            break; // Persistent object generated elsewhere
                            
                        case RingPrimaryEffect.Projectile_Orbit:
                        {
                            var template = Projectiles[OrbitProjectileData.TemplateIndex + (tier - 1)];
                            byte spawnCount = (byte)(5 + stats.CompiledStatsTree.ExtraProjectiles);
                            for (byte projSpawnIt = 0; projSpawnIt < spawnCount; projSpawnIt++)
                            {
                                Debug.Log($"Creating {effect} projectile...");

                                var projectileE = ecb.Instantiate(template.Entity);
                                var projectileT = transform;
                                projectileT.Position += r.NextFloat3Direction();
                                projectileT.Scale = stats.CompiledStatsTree.Size;
                                ecb.SetComponent(projectileE, projectileT);

                                ecb.SetComponent(projectileE, ProjectileLoopTrigger.Empty);
                            
                                ecb.SetComponent(projectileE, new OrbitProjectileData()
                                {
                                    CreateTime = Time,
                                    SeekerCount = spawnCount
                                });

                                ecb.SetComponent(projectileE, new MovementSettings() { Speed = 30f*stats.CompiledStatsTree.ProjectileSpeed });
                                ecb.SetComponent(projectileE, new DestroyAtTime() { DestroyTime = double.MaxValue });
                                ecb.SetComponent(projectileE, new Projectile((int)math.ceil((float)stats.CompiledStatsTree.Damage*Projectile.PerFrameDamageMod)));
                                ecb.SetComponent(projectileE, new OwnedProjectile(){ PlayerId = playerId.Index, Key = new ProjectileKey(effect, tier, projSpawnIt) });
                            }
                            break;
                        }
                    }
                }
            }
            
            // Clear dirty flag
            dirtyData = default;
            dirtyState.ValueRW = false;
            
            // Notify
            GameEvents.Trigger(GameEvents.Type.InventoryChanged, entity);
        }
    }
}