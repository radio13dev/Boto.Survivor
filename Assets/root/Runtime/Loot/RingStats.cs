using System;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public struct Ring : IBufferElementData
{
    public const int k_RingCount = 6;

    public double NextActivateTime;

    public RingStats Stats;

    public static NativeArray<Ring> DemoArray
    {
        get
        {
            Random r = Random.CreateFromIndex((uint)UnityEngine.Random.Range(1, 100));
            var arr = new NativeArray<Ring>(k_RingCount, Allocator.Temp);
            arr[2] = new Ring()
            {
                Stats = RingStats.Generate(ref r)
            };
            arr[4] = new Ring()
            {
                Stats = RingStats.Generate(ref r)
            };
            arr[5] = new Ring()
            {
                Stats = RingStats.Generate(ref r)
            };
            return arr;
        }
    }
    
    public static void SetupEntity(Entity entity, int playerId, ref Random random, ref EntityCommandBuffer ecb, LocalTransform transform, Movement movement, RingStats stats)
    {
        ecb.SetComponent(entity, stats);

        transform.Rotation = random.NextQuaternionRotation();
        ecb.SetComponent(entity, transform);

        var newDropMovement = new Movement();
        TorusMapper.SnapToSurface(transform.Position, 0, out _, out var surfaceNormal);
        var jumpDir = random.NextFloat(1, 2) * PhysicsSettings.s_GemJump.Data * (surfaceNormal + random.NextFloat3Direction() / 2); // movement.Velocity + 
        newDropMovement.Velocity = jumpDir;
        ecb.SetComponent(entity, newDropMovement);

        // Mark who's allowed to collect this
        ecb.SetComponent(entity, new Collectable() { PlayerId = playerId });
        //ecb.SetComponentEnabled<Collectable>(entity, true);
        
        // CLIENTSIDE ONLY
        if (Game.ClientPlayerIndex.Data != -1 && Game.ClientPlayerIndex.Data != playerId)
        {
            ecb.AddComponent(entity, new Hidden());
        }
    }
}

public enum RingPrimaryEffect : byte
{
    None,

    Projectile_Ring,
    Projectile_NearestRapid,
    Projectile_Seeker,
    Projectile_Band,

    Projectile_Melee,
    Projectile_Returning,
    Projectile_Mark,
    Projectile_Orbit,
    
    k_Length
}

[Save]
[Serializable]
public unsafe struct RingStats : IComponentData
{
    public const int k_MaxStats = 3;

    // Primary effect
    public bool IsValid => PrimaryEffect != RingPrimaryEffect.None;
    public RingPrimaryEffect PrimaryEffect;
    
    [SerializeField] public fixed byte BoostedStats[k_MaxStats];
    [SerializeField] public fixed byte BoostedStatsBoosts[k_MaxStats];

    public byte Tier
    {
        get
        {
            int s = 0;
            for (int i = 0; i < k_MaxStats; i++)
            {
                s += BoostedStatsBoosts[i];
            }
            
            if (s <= 3) return 0;
            if (s <= 6) return 1;
            if (s <= 9) return 2;
            return 3;
        }
    }

    public int GetSellPrice()
    {
        return ((int)Tier*20);
    }

    public bool GetStatBoost(int index, out TiledStat stat, out byte boost)
    {
        if (index < 0 || index >= k_MaxStats || BoostedStatsBoosts[index] == 0)
        {
            stat = default;
            boost = 0;
            return false;
        }
        
        stat = (TiledStat)BoostedStats[index];
        boost = BoostedStatsBoosts[index];
        return true;
    }
    public bool DoesBoostStat(TiledStat stat, out int boost)
    {
        for (int i = 0; i < k_MaxStats; i++)
        {
            if (BoostedStats[i] == (byte)stat)
            {
                boost = BoostedStatsBoosts[i];
                return true;
            }
        }
        boost = default;
        return false;
    }

    public static RingStats Generate(ref Random random)
    {
        RingStats ringStats = new();
        ringStats.PrimaryEffect = (RingPrimaryEffect)random.NextInt(1 + (int)RingPrimaryEffect.k_Length);
        
        for (int i = 0; i < k_MaxStats; i++)
        {
            Retry:
            ringStats.BoostedStats[i] = (byte)random.NextInt(TiledStats.TileCols * TiledStats.TileRows);
            //if (ringStats.BoostedStats[i] >= (byte)TiledStat.Stat_00_Ring0) ringStats.BoostedStats[i]++; // Skip the ring stats
            //if (ringStats.BoostedStats[i] >= (byte)TiledStat.Stat_11_Ring1) ringStats.BoostedStats[i]++;
            //if (ringStats.BoostedStats[i] >= (byte)TiledStat.Stat_16_Ring2) ringStats.BoostedStats[i]++;
            //if (ringStats.BoostedStats[i] >= (byte)TiledStat.Stat_21_Ring3) ringStats.BoostedStats[i]++;
            //if (ringStats.BoostedStats[i] >= (byte)TiledStat.Stat_26_Ring4) ringStats.BoostedStats[i]++;
            //if (ringStats.BoostedStats[i] >= (byte)TiledStat.Stat_31_Ring5) ringStats.BoostedStats[i]++;
            
            // Ensure no duplicates
            for (int j = 0; j < i; j++)
                if (ringStats.BoostedStats[i] == ringStats.BoostedStats[j])
                    goto Retry;
            
            var v = random.NextInt(1000);
            if (v < 700) ringStats.BoostedStatsBoosts[i] = 0;
            else if (v < 850) ringStats.BoostedStatsBoosts[i] = 1;
            else if (v < 900) ringStats.BoostedStatsBoosts[i] = 2;
            else if (v < 950) ringStats.BoostedStatsBoosts[i] = 3;
            else if (v < 970) ringStats.BoostedStatsBoosts[i] = 4;
            else if (v < 990) ringStats.BoostedStatsBoosts[i] = 5;
            else /*if (v <= 1000)*/ ringStats.BoostedStatsBoosts[i] = 6;
        }
        
        return ringStats;
    }

    public string GetTitleString()
    {
        switch (PrimaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
                return "Ring of Projectiles";
            case RingPrimaryEffect.Projectile_NearestRapid:
                return "Rapid Ring";
            case RingPrimaryEffect.Projectile_Seeker:
                return "Seeking Ring";
            case RingPrimaryEffect.Projectile_Band:
                return "Band of Banding";
            case RingPrimaryEffect.Projectile_Melee:
                return "Melee Ring";
            case RingPrimaryEffect.Projectile_Returning:
                return "Returning Circlet";
            case RingPrimaryEffect.Projectile_Mark:
                return "Ring of Marking";
            case RingPrimaryEffect.Projectile_Orbit:
                return "Radio Ring";
            default:
                return PrimaryEffect.ToString();
        }
    }

    public string GetDescriptionString()
    {
        switch (PrimaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
                return "Launches 8 projectiles out in a ring around you";
            case RingPrimaryEffect.Projectile_NearestRapid:
                return "Rapidly fires at the nearest target";
            case RingPrimaryEffect.Projectile_Seeker:
                return "Periodically creates seekers";
            case RingPrimaryEffect.Projectile_Band:
                return "Creates a massive, planet wrapping band";
            case RingPrimaryEffect.Projectile_Melee:
                return "Periodically strikes forward with multiple disks";
            case RingPrimaryEffect.Projectile_Returning:
                return "Releases a returning circlet";
            case RingPrimaryEffect.Projectile_Mark:
                return "Marks targets in a ring around you";
            case RingPrimaryEffect.Projectile_Orbit:
                return "Creates slow, orbiting projectiles";
            default:
                return "???";
        }
    }

    #region CLIENTSIDE

#if UNITY_EDITOR
    static RingVisuals RingVisualsDatabase_Editor => m_RingVisualsDatabase ? m_RingVisualsDatabase : m_RingVisualsDatabase = Database.GetGenericAsset<RingVisuals>("RingVisualsDatabase");
    static RingVisuals m_RingVisualsDatabase;
#endif

    public Material Material
    {
        get
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return RingVisualsDatabase_Editor.Value[PrimaryEffect].Material;
            if (Game.ClientGame?.World == null)
            {
                Debug.LogError($"ATTEMPTED MATERIAL FETCH WHILE WORLD NULL");
                return RingVisualsDatabase_Editor.Value[PrimaryEffect].Material;
            }
#endif

            var visuals = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.RingVisual>(true);
            var instances = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.InstancedResources>(true);
            return instances[visuals[(byte)PrimaryEffect].InstancedResourceIndex].Instance.Value.Material;
        }
    }

    public Mesh Mesh
    {
        get
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return RingVisualsDatabase_Editor.Value[PrimaryEffect].Mesh;
            if (Game.ClientGame?.World == null)
            {
                Debug.LogError($"ATTEMPTED MESH FETCH WHILE WORLD NULL");
                return RingVisualsDatabase_Editor.Value[PrimaryEffect].Mesh;
            }
#endif

            var visuals = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.RingVisual>(true);
            var instances = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.InstancedResources>(true);
            return instances[visuals[(byte)PrimaryEffect].InstancedResourceIndex].Instance.Value.Mesh;
        }
    }

    #endregion
}