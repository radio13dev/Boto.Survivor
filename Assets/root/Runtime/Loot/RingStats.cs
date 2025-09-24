using System;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
}

[Flags]
public enum RingPrimaryEffect : byte
{
    None = 0,

    Projectile_Ring = 1 << 0,
    Projectile_NearestRapid = 1 << 1,
    Projectile_Seeker = 1 << 2,
    Projectile_Band = 1 << 3,

    Projectile_Melee = 1 << 4,
    Projectile_Returning = 1 << 5,
    Projectile_Mark = 1 << 6,
    Projectile_Orbit = 1 << 7,

    Length = 8
}

public static class RingPrimaryEffectExtension
{
    public static int GetMostSigBit(this RingPrimaryEffect eff)
    {
        return sizeof(int) * 8 - math.lzcnt((uint)eff) - 1;
    }
}

public unsafe struct PrimaryEffectStack
{
    public fixed byte Stacks[(int)RingPrimaryEffect.Length];

    public PrimaryEffectStack(in DynamicBuffer<Ring> rings)
    {
        int depth;
        int init;
        const int mask = 1;

        for (int i = 0; i < rings.Length; i++)
        {
            depth = 0;
            init = (int)rings[i].Stats.PrimaryEffect;
            while (init != 0)
            {
                if ((init & mask) != 0)
                    Stacks[depth]++;

                init >>= 1;
                depth++;
            }
        }
    }

    public PrimaryEffectStack(in DynamicBuffer<Ring> rings, int ringIndex)
    {
        int main = (int)rings[ringIndex].Stats.PrimaryEffect;
        int comp;
        int depth;
        int init;
        const int mask = 1;

        for (int i = 0; i <= ringIndex; i++)
        {
            comp = main;
            depth = 0;
            init = (int)rings[i].Stats.PrimaryEffect;
            while (comp != 0 && init != 0)
            {
                if ((comp & mask) != 0 && (init & mask) != 0)
                    Stacks[depth]++;

                comp >>= 1;
                init >>= 1;
                depth++;
            }
        }
    }
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
    
    public bool GetStatBoost(int index, out TiledStat stat, out byte boost)
    {
        if (index < 0 || index >= k_MaxStats)
        {
            stat = default;
            boost = 0;
            return false;
        }
        
        stat = (TiledStat)BoostedStats[index];
        boost = BoostedStatsBoosts[index];
        return true;
    }

    public static RingStats Generate(ref Random random)
    {
        RingStats ringStats = new();
        ringStats.PrimaryEffect = (RingPrimaryEffect)(1 << random.NextInt((int)RingPrimaryEffect.Length));
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
            return instances[visuals[PrimaryEffect.GetMostSigBit()].InstancedResourceIndex].Instance.Value.Material;
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
            return instances[visuals[PrimaryEffect.GetMostSigBit()].InstancedResourceIndex].Instance.Value.Mesh;
        }
    }

    #endregion
}