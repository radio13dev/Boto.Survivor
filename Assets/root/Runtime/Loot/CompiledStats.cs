using System;
using BovineLabs.Saving;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

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

    public (int damageWithMods, int baseDamage, Crit crit, Chain chain, Cut cut, Degenerate degenerate, Subdivide subdivide, Decimate decimate, 
        Dissolve dissolve, Poke poke) RollDamage(ref Random random)
    {
        int baseDamage = CompiledStatsTree.Damage;
        int damageWithMods = baseDamage;

        Crit crit;
        {
            var chance_crit = CompiledStatsTree.EvaluateA(TiledStat.Stat_01_CritChance)*(1+CompiledStatsTree.EvaluateA(TiledStat.Stat_27_Probability));
            crit = new Crit((byte)((int)math.floor(chance_crit) + (math.frac(chance_crit) > random.NextFloat() ? 1 : 0)));
        }

        Chain chain;
        {
            var chance_chain = CompiledStatsTree.EvaluateA(TiledStat.Stat_07_Chain)*(1+CompiledStatsTree.EvaluateA(TiledStat.Stat_27_Probability));
            chain = new Chain((byte)((int)math.floor(chance_chain) + (math.frac(chance_chain) > random.NextFloat() ? 1 : 0)));
        }
        
        Cut cut;
        {
            var chance_cut = CompiledStatsTree.EvaluateA(TiledStat.Stat_08_Cut)*(1+CompiledStatsTree.EvaluateA(TiledStat.Stat_27_Probability));
            cut = new Cut((byte)((int)math.floor(chance_cut) + (math.frac(chance_cut) > random.NextFloat() ? 1 : 0)));
        }
        
        Degenerate degenerate;
        {
            var chance_degenerate = CompiledStatsTree.EvaluateA(TiledStat.Stat_09_Degenerate)*(1+CompiledStatsTree.EvaluateA(TiledStat.Stat_27_Probability));
            degenerate = new Degenerate((byte)((int)math.floor(chance_degenerate) + (math.frac(chance_degenerate) > random.NextFloat() ? 1 : 0)));
        }
        
        Subdivide subdivide;
        {
            var chance_subdivide = CompiledStatsTree.EvaluateA(TiledStat.Stat_10_Subdivide)*(1+CompiledStatsTree.EvaluateA(TiledStat.Stat_27_Probability));
            subdivide = new Subdivide((byte)((int)math.floor(chance_subdivide) + (math.frac(chance_subdivide) > random.NextFloat() ? 1 : 0)));
        }
        
        Decimate decimate;
        {
            var chance_decimate = CompiledStatsTree.EvaluateA(TiledStat.Stat_12_Decimate)*(1+CompiledStatsTree.EvaluateA(TiledStat.Stat_27_Probability));
            decimate = new Decimate((byte)((int)math.floor(chance_decimate) + (math.frac(chance_decimate) > random.NextFloat() ? 1 : 0)));
        }
        
        Dissolve dissolve;
        {
            var chance_dissolve = CompiledStatsTree.EvaluateA(TiledStat.Stat_13_Dissolve)*(1+CompiledStatsTree.EvaluateA(TiledStat.Stat_27_Probability));
            dissolve = new Dissolve((byte)((int)math.floor(chance_dissolve) + (math.frac(chance_dissolve) > random.NextFloat() ? 1 : 0)));
        }
        
        Poke poke;
        {
            var chance_poke = CompiledStatsTree.EvaluateA(TiledStat.Stat_14_Poke)*(1+CompiledStatsTree.EvaluateA(TiledStat.Stat_27_Probability));
            poke = new Poke((byte)((int)math.floor(chance_poke) + (math.frac(chance_poke) > random.NextFloat() ? 1 : 0)));
        }
        
        if (poke) damageWithMods += poke.Value*(int)CompiledStatsTree.EvaluateB(TiledStat.Stat_14_Poke);
        if (crit) damageWithMods *= 2 << crit.Value;
        
        return (damageWithMods, baseDamage, crit, chain, cut, degenerate, subdivide, decimate, dissolve, poke);
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

                                ecb.SetComponent(projectileE, new OrbitProjectileData()
                                {
                                    CreateTime = Time,
                                    SeekerCount = spawnCount
                                });

                                Projectile.Setup(ref ecb, ref r, in projectileE, in stats, in playerId, in effect, in tier, (byte)projSpawnIt, double.MaxValue);
                                Projectile.SetSpeed(ref ecb, in projectileE, in stats, 30);
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