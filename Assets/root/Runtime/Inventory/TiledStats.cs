using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public enum TiledStat : byte
{
    Stat_00_Ring0,
    Stat_01_CritChance,
    Stat_02_Rate,
    Stat_03_Momentum,
    Stat_04_ProjectileCount,
    Stat_05_ProjectileSize,
    Stat_06_Pierce,
    Stat_07_Chain,
    Stat_08_Cut,
    Stat_09_Degenerate,
    Stat_10_Subdivide,
    Stat_11_Ring1,
    Stat_12_Decimate,
    Stat_13_Dissolve,
    Stat_14_Poke,
    Stat_15_Scale_Scale,
    Stat_16_Ring2,
    Stat_17_Reflect,
    Stat_18_Overshield,
    Stat_19_Immunity,
    Stat_20_RateOnDestroy,
    Stat_21_Ring3,
    Stat_22_DmgOnDestroy,
    Stat_23_Sharpness,
    Stat_24_Speed,
    Stat_25_SpeedOnDestroy,
    Stat_26_Ring4,
    Stat_27_Probability,
    Stat_28_Finding,
    Stat_29_Economical,
    Stat_30_Quality,
    Stat_31_Ring5,
    Stat_32_AbilityCooldown,
    Stat_33_AbilityCooldownOnDestroy,
    Stat_34_RateOnAbility,
    Stat_35_DamageOnAbility,
    
    // Go up all the way to Stat_80
    Stat_36,
    Stat_37,
    Stat_38,
    Stat_39,
    Stat_40,
    Stat_41,
    Stat_42,
    Stat_43,
    Stat_44,
    Stat_45,
    Stat_46,
    Stat_47,
    Stat_48,
    Stat_49,
    Stat_50,
    Stat_51,
    Stat_52,
    Stat_53,
    Stat_54,
    Stat_55,
    Stat_56,
    Stat_57,
    Stat_58,
    Stat_59,
    Stat_60,
    Stat_61,
    Stat_62,
    Stat_63,
    Stat_64,
    Stat_65,
    Stat_66,
    Stat_67,
    Stat_68,
    Stat_69,
    Stat_70,
    Stat_71,
    Stat_72,
    Stat_73,
    Stat_74,
    Stat_75,
    Stat_76,
    Stat_77,
    Stat_78,
    Stat_79,
    Stat_80
    
}

[Save]
public unsafe struct TiledStatsTree : IComponentData
{
    [SerializeField] fixed int m_Levels[TiledStats.TileCols * TiledStats.TileRows];

    public int this[int2 tileKey]
    {
        [Pure]
        get
        {
            return this[GetStat(tileKey)];
        }
    }

    public int this[TiledStat index]
    {
        [Pure]
        get => this[(int)index];
        set => this[(int)index] = value;
    }
    public int this[int index]
    {
        [Pure]
        get
        {
            return m_Levels[mathu.modabs(index, (TiledStats.TileCols * TiledStats.TileRows))];
        }
        set
        {
            m_Levels[mathu.modabs(index, (TiledStats.TileCols * TiledStats.TileRows))] = value;
        }
    }

    public static TiledStatsTree Default
    {
        get
        {
            var fake = new TiledStatsTree();
            fake[TiledStat.Stat_00_Ring0] = 1;
            return fake;
        }
    }

    [Pure]
    private static TiledStat GetStat(int2 tileKey)
    {
        var modx = mathu.modabs(tileKey.x, TiledStats.TileCols);
        var mody = mathu.modabs(tileKey.y, TiledStats.TileRows);
        return (TiledStat)(modx + mody * TiledStats.TileCols);
    }

    [Pure]
    public long GetLevelsSpent()
    {
        long levels = 0;
        for (int i = 0; i < (TiledStats.TileCols * TiledStats.TileRows); i++)
        {
            if (m_Levels[i] > 0)
            {
                levels += (long)m_Levels[i];
            }
        }
        return levels;
    }

    [Pure]
    public long GetLevelUpCost(int2 key) => GetLevelUpCost(GetStat(key));
    [Pure]
    public long GetLevelUpCost(TiledStat stat)
    {
        return 0;//(10L * (int)GetLevelsSpent());
    }

    [Pure]
    public bool HasCompletedColumn(int x)
    {
        for (int y = 0; y < TiledStats.TileRows; y++)
        {
            if (this[new int2(x, y)] <= 0)
                return false;
        }
        return true;
    }

    [Pure]
    public bool HasCompletedRow(int y)
    {
        for (int x = 0; x < TiledStats.TileCols; x++)
        {
            if (this[new int2(x, y)] <= 0)
                return false;
        }
        return true;
    }
    
    /* Values for game to use */
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TiledStatData GetData(TiledStat stat, out int lvl)
    {
        lvl = this[stat];
        return stat.Get();
    }
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float EvaluateA(TiledStat stat)
    {
        var lvl = this[stat];
        return stat.Get().EffectAValues.Evaluate(lvl);
    }
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float EvaluateB(TiledStat stat)
    {
        var lvl = this[stat];
        return stat.Get().EffectBValues.Evaluate(lvl);
    }
    
    [Pure]
    public int Damage => 100 + (int)GetData(TiledStat.Stat_23_Sharpness, out var lvl).EffectAValues.Evaluate(lvl);
    [Pure]
    public float ProjectileSpeed => 1 + GetData(TiledStat.Stat_03_Momentum, out var lvl2).EffectAValues.Evaluate(lvl2);
    [Pure]
    public byte ExtraProjectiles => (byte)GetData(TiledStat.Stat_04_ProjectileCount, out var lvl).EffectAValues.Evaluate(lvl);
    [Pure]
    public float Size => 1 + GetData(TiledStat.Stat_05_ProjectileSize, out var lvl).EffectAValues.Evaluate(lvl);
    [Pure]
    public byte PierceCount => (byte)GetData(TiledStat.Stat_16_Ring2, out var lvl).EffectAValues.Evaluate(lvl);
    [Pure]
    public float Cooldown => 1 + (byte)GetData(TiledStat.Stat_02_Rate, out var lvl).EffectAValues.Evaluate(lvl);

    public static TiledStatsTree Demo
    {
        get
        {
            TiledStatsTree demo = new TiledStatsTree();
            
            for (int i = 0; i < TiledStats.TileCols * TiledStats.TileRows; i++)
                demo[i] = math.clamp(Random.Range(0, 50) - 42, 0, 7);
            
            //demo[TiledStat.Stat_06] = 1;
            //demo[TiledStat.Stat_07] = 2;
            //
            //demo[TiledStat.Stat_15_Scale_Scale]=3;
            //demo[TiledStat.Stat_16_Intersect_Pierce] = 1;
            //
            //demo[TiledStat.Stat_26] = 1;
            //demo[TiledStat.Stat_27]=2;
            //
            //demo[TiledStat.Stat_31] = 1;
            return demo;
        }
    }
    
    public static TiledStatsTree operator +(TiledStatsTree left, TiledStatsTree right)
    {
        TiledStatsTree result = new TiledStatsTree();
        for (int i = 0; i < TiledStats.TileCols * TiledStats.TileRows; i++)
            result[i] = left[i] + right[i];
        return result;
    }
}

public static partial class TiledStats
{
    public const int TileRows = 9;
    public const int TileCols = 9;
    public static readonly int2 TileCount = new int2(TileCols, TileRows);

    public static TiledStat Get(int2 tileKey)
    {
        return (TiledStat)(tileKey.x + tileKey.y * TileCount.x);
    }
    
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TiledStatData Get(this TiledStat stat) => StatData[(int)stat];

    
    public static string GetTitle(this TiledStat stat)
    {
        return stat.GetFull().Localization.Localization_Title[0].Size(36);
    }
    
    public static string GetDescription(this TiledStat stat)
    {
        return stat.GetFull().Localization.Localization_Description[0].Size(30);
    }
    
    //public static int GetRingIndex(this TiledStat stat)
    //{
    //    if (stat == TiledStat.Stat_00_Ring0) return 0;
    //    if (stat == TiledStat.Stat_11_Ring1) return 1;
    //    if (stat == TiledStat.Stat_16_Ring2) return 2;
    //    if (stat == TiledStat.Stat_21_Ring3) return 3;
    //    if (stat == TiledStat.Stat_26_Ring4) return 4;
    //    if (stat == TiledStat.Stat_31_Ring5) return 5;
    //    return -1;
    //}
    
    
    public static List<(string left, string oldVal, float change, string newVal)> GetDescriptionRows(this TiledStat stat, int specificLevel)
    {
        var statData = TiledStatsFull.StatDataFull[(int)stat];
    
        List<(string left, string oldVal, float change, string newVal)> ret = new();
        
        if (statData.Data.EffectAValues != default)
            ret.Add((statData.Localization.Localization_EffectA[0], default, default, statData.Data.EffectAValues[specificLevel].ToMulString()));
            
        if (statData.Data.EffectBValues != default)
            ret.Add((statData.Localization.Localization_EffectB[0], default, default, statData.Data.EffectBValues[specificLevel].ToMulString()));
            
        if (statData.Data.EffectCValues != default)
            ret.Add((statData.Localization.Localization_EffectC[0], default, default, statData.Data.EffectCValues[specificLevel].ToMulString()));

        return ret;
    }
    public static List<DescriptionUI.Data.Row> GetDescriptionRows(this TiledStat stat, int oldLvl, int newLvl)
    {
        var statData = TiledStatsFull.StatDataFull[(int)stat];
    
        var ret = new List<DescriptionUI.Data.Row>();
        
        if (statData.Data.EffectAValues != default)
        {
            var a = statData.Data.EffectAValues[oldLvl];
            var b = statData.Data.EffectAValues[newLvl];
            ret.Add(new (statData.Localization.Localization_EffectA[0], a.ToMulString(), b - a, b.ToMulString()));
        }
            
        if (statData.Data.EffectBValues != default)
        {
            var a = statData.Data.EffectBValues[oldLvl];
            var b = statData.Data.EffectBValues[newLvl];
            ret.Add(new (statData.Localization.Localization_EffectB[0], a.ToMulString(), b - a, b.ToMulString()));
        }
        
        if (statData.Data.EffectCValues != default)
        {
            var a = statData.Data.EffectCValues[oldLvl];
            var b = statData.Data.EffectCValues[newLvl];
            ret.Add(new (statData.Localization.Localization_EffectC[0], a.ToMulString(), b - a, b.ToMulString()));
        }
        return ret;
    }

}