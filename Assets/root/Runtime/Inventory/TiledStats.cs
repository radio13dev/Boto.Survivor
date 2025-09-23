using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public enum TiledStat
{
    Stat_00_SharpEdge,
    Stat_01,
    Stat_02,
    Stat_03,
    Stat_04,
    Stat_05,
    Stat_06,
    Stat_07,
    Stat_08,
    Stat_09,
    Stat_10,
    Stat_11,
    Stat_12,
    Stat_13,
    Stat_14_Homotopy_ProjCount,
    Stat_15_Scale_Scale,
    Stat_16_Intersect_Pierce,
    Stat_17_Velocity_ProjSpeed,
    Stat_18_Frequency_ProjRate,
    Stat_19,
    Stat_20,
    Stat_21,
    Stat_22,
    Stat_23,
    Stat_24,
    Stat_25,
    Stat_26,
    Stat_27,
    Stat_28,
    Stat_29,
    Stat_30,
    Stat_31,
    Stat_32,
    Stat_33,
    Stat_34,
    Stat_35,
}

[Save]
public unsafe struct TiledStatsTree : IComponentData
{
    public fixed int Levels[TiledStats.TileCols * TiledStats.TileRows];

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
            return Levels[mathu.modabs(index, (TiledStats.TileCols * TiledStats.TileRows))];
        }
        set
        {
            Levels[mathu.modabs(index, (TiledStats.TileCols * TiledStats.TileRows))] = value;
        }
    }

    public static TiledStatsTree Default
    {
        get
        {
            var fake = new TiledStatsTree();
            fake[0] = 1;
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
            if (Levels[i] > 0)
            {
                levels += (long)Levels[i];
            }
        }
        return levels;
    }

    [Pure]
    public long GetLevelUpCost(int2 key) => GetLevelUpCost(GetStat(key));
    [Pure]
    public long GetLevelUpCost(TiledStat stat)
    {
        return (10L << (int)GetLevelsSpent());
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
    public int Damage => 100 + (int)GetData(TiledStat.Stat_00_SharpEdge, out var lvl).EffectAValues.Evaluate(lvl);
    [Pure]
    public float ProjectileSpeed => 1 + GetData(TiledStat.Stat_17_Velocity_ProjSpeed, out var lvl).EffectAValues.Evaluate(lvl);
    [Pure]
    public int ExtraProjectiles => (int)GetData(TiledStat.Stat_14_Homotopy_ProjCount, out var lvl).EffectAValues.Evaluate(lvl);
    [Pure]
    public float Size => 1 + GetData(TiledStat.Stat_15_Scale_Scale, out var lvl).EffectAValues.Evaluate(lvl);
    [Pure]
    public byte PierceCount => (byte)GetData(TiledStat.Stat_16_Intersect_Pierce, out var lvl).EffectAValues.Evaluate(lvl);
    [Pure]
    public float Cooldown => 1 + (byte)GetData(TiledStat.Stat_18_Frequency_ProjRate, out var lvl).EffectAValues.Evaluate(lvl);

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
}

public static partial class TiledStats
{
    public const int TileRows = 6;
    public const int TileCols = 6;
    public static readonly int2 TileCount = new int2(TileCols, TileRows);

    public static TiledStat Get(int2 tileKey)
    {
        return (TiledStat)(tileKey.x + tileKey.y * TileCount.x);
    }
    
    public static TiledStatData Get(this TiledStat stat) => StatData[(int)stat];
    
    public static string GetTitle(this TiledStat stat)
    {
        return TiledStatsFull.StatDataFull[(int)stat].Localization.Localization_Title[0];
    }
    
    public static string GetDescription(this TiledStat stat)
    {
        return string.Empty;
    }
    
    public static int GetRingIndex(this TiledStat stat)
    {
        if (stat == TiledStat.Stat_00_SharpEdge) return 0;
        if (stat == TiledStat.Stat_11) return 1;
        if (stat == TiledStat.Stat_16_Intersect_Pierce) return 2;
        if (stat == TiledStat.Stat_21) return 3;
        if (stat == TiledStat.Stat_26) return 4;
        if (stat == TiledStat.Stat_31) return 5;
        return -1;
    }
    
    
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
    public static List<(string left, string oldVal, float change, string newVal)> GetDescriptionRows(this TiledStat stat, int oldLvl, int newLvl)
    {
        var statData = TiledStatsFull.StatDataFull[(int)stat];
    
        List<(string left, string oldVal, float change, string newVal)> ret = new();
        
        if (statData.Data.EffectAValues != default)
        {
            var a = statData.Data.EffectAValues[oldLvl];
            var b = statData.Data.EffectAValues[newLvl];
            ret.Add((statData.Localization.Localization_EffectA[0], a.ToMulString(), b - a, b.ToMulString()));
        }
            
        if (statData.Data.EffectBValues != default)
        {
            var a = statData.Data.EffectBValues[oldLvl];
            var b = statData.Data.EffectBValues[newLvl];
            ret.Add((statData.Localization.Localization_EffectB[0], a.ToMulString(), b - a, b.ToMulString()));
        }
        
        if (statData.Data.EffectCValues != default)
        {
            var a = statData.Data.EffectCValues[oldLvl];
            var b = statData.Data.EffectCValues[newLvl];
            ret.Add((statData.Localization.Localization_EffectC[0], a.ToMulString(), b - a, b.ToMulString()));
        }
        return ret;
    }

}