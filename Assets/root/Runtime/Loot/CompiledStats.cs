using System;
using BovineLabs.Saving;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct CompiledStats : IComponentData
{
    public float ProjectileRate;
    public float ProjectileSpeed;
    public float ProjectileDuration;
    public float ProjectileSize;
    public float ProjectileDamage;
}

[Save]
public struct CompiledStatsDirty : IComponentData, IEnableableComponent
{
    /// <summary>
    /// Specifices which particular primary effects have been added/removed
    /// </summary>
    public RingPrimaryEffect DirtyFlags;

    public void SetDirty(Ring ring)
    {
        DirtyFlags |= ring.Stats.PrimaryEffect;
    }
    public void SetDirty(RingStats ringStats)
    {
        DirtyFlags |= ringStats.PrimaryEffect;
    }
}

[Serializable]
public readonly struct ProjectileStats
{
    public readonly float Speed;
    public readonly float Duration;
    public readonly float Damage;
    public readonly float Size;
    
    public readonly byte ProjectileCount;
    public readonly byte PierceCount;

    public ProjectileStats(in DynamicBuffer<Ring> rings, in int ringIndex, in CompiledStats compiledStats, in DynamicBuffer<EquippedGem> equippedGems)
    {            
        var ring = rings[ringIndex];
        
        // Get shared stats
        Speed = ring.Stats.PrimaryEffect.GetProjectileSpeed(compiledStats.ProjectileSpeed);
        Duration = ring.Stats.PrimaryEffect.GetProjectileDuration(compiledStats.ProjectileDuration);
        Damage = ring.Stats.PrimaryEffect.GetProjectileDamage(compiledStats.ProjectileDamage);
        Size = ring.Stats.PrimaryEffect.GetProjectileSize(compiledStats.ProjectileSize);

        // Get gem mods
        ProjectileCount = 0;
        PierceCount = 0;

        var gemMin = ringIndex * Gem.k_GemsPerRing;
        var gemMax = (ringIndex + 1) * Gem.k_GemsPerRing;
        for (int gemIndex = gemMin; gemIndex < gemMax; gemIndex++)
        {
            switch (equippedGems[gemIndex].Gem.GemType)
            {
                case Gem.Type.Multishot:
                    ProjectileCount++;
                    break;
                case Gem.Type.Pierce:
                    PierceCount++;
                    break;
            }
        }
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
            in DynamicBuffer<Ring> rings, in DynamicBuffer<EquippedGem> gems, 
            ref DynamicBuffer<OwnedProjectiles> ownedProjectiles)
        {
            Debug.Log("Compiling stats...");
            
            // Compile stats
            stats = new();
            
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
                ProjectileStats projectileStats = new ProjectileStats(in rings, in ringIndex, in stats, in gems);
                
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
                            
                        case RingPrimaryEffect.Projectile_Orbit:
                        {
                            var template = Projectiles[OrbitProjectileData.TemplateIndex + (tier - 1)];
                            byte spawnCount = (byte)(5 + projectileStats.ProjectileCount);
                            for (byte projSpawnIt = 0; projSpawnIt < spawnCount; projSpawnIt++)
                            {
                                Debug.Log($"Creating {effect} projectile...");

                                var projectileE = ecb.Instantiate(template.Entity);
                                var projectileT = transform;
                                projectileT.Position += r.NextFloat3Direction();
                                projectileT.Scale = projectileStats.Size;
                                ecb.SetComponent(projectileE, projectileT);

                                ecb.SetComponent(projectileE, ProjectileLoopTrigger.Empty);
                            
                                ecb.SetComponent(projectileE, new OrbitProjectileData()
                                {
                                    CreateTime = Time,
                                    SeekerCount = spawnCount
                                });

                                ecb.SetComponent(projectileE, new MovementSettings() { Speed = projectileStats.Speed });
                                ecb.SetComponent(projectileE, new DestroyAtTime() { DestroyTime = double.MaxValue });
                                ecb.SetComponent(projectileE, new Projectile() { Damage = projectileStats.Damage });
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