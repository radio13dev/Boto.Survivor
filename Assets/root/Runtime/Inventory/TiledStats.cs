using System.Collections.Generic;
using System.Diagnostics.Contracts;
using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum TiledStat
{
    Stat_00,
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
    Stat_14,
    Stat_15,
    Stat_16,
    Stat_17,
    Stat_18,
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
    public bool CanLevelUp(int2 key) => CanLevelUp(GetStat(key));
    [Pure]
    public bool CanLevelUp(TiledStat stat)
    {
        var c = math.int2(((int)stat)%TiledStats.TileCols, ((int)stat)/TiledStats.TileCols);
        return this[c+math.int2(1,0)] > 0 ||
               this[c+math.int2(0,1)] > 0 ||
               this[c+math.int2(-1,0)] > 0 ||
               this[c+math.int2(0,-1)] > 0
        ;
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
    
    public static string GetTitle(this TiledStat stat)
    {
        return StatData[(int)stat].Localization_Title[0];
    }
    
    public static string GetDescription(this TiledStat stat)
    {
        return string.Empty;
    }
    
    
    public static List<(string left, string oldVal, float change, string newVal)> GetDescriptionRows(this TiledStat stat, int specificLevel)
    {
        var statData = StatData[(int)stat];
    
        List<(string left, string oldVal, float change, string newVal)> ret = new();
        
        if (statData.EffectAValues != default)
            ret.Add((statData.Localization_EffectA[0], default, default, statData.EffectAValues[specificLevel].ToMulString()));
            
        if (statData.EffectBValues != default)
            ret.Add((statData.Localization_EffectB[0], default, default, statData.EffectBValues[specificLevel].ToMulString()));
            
        if (statData.EffectCValues != default)
            ret.Add((statData.Localization_EffectC[0], default, default, statData.EffectCValues[specificLevel].ToMulString()));

        return ret;
    }
    public static List<(string left, string oldVal, float change, string newVal)> GetDescriptionRows(this TiledStat stat, int oldLvl, int newLvl)
    {
        var statData = StatData[(int)stat];
    
        List<(string left, string oldVal, float change, string newVal)> ret = new();
        
        if (statData.EffectAValues != default)
        {
            var a = statData.EffectAValues[oldLvl];
            var b = statData.EffectAValues[newLvl];
            ret.Add((statData.Localization_EffectA[0], a.ToMulString(), b - a, b.ToMulString()));
        }
            
        if (statData.EffectBValues != default)
        {
            var a = statData.EffectBValues[oldLvl];
            var b = statData.EffectBValues[newLvl];
            ret.Add((statData.Localization_EffectB[0], a.ToMulString(), b - a, b.ToMulString()));
        }
        
        if (statData.EffectCValues != default)
        {
            var a = statData.EffectCValues[oldLvl];
            var b = statData.EffectCValues[newLvl];
            ret.Add((statData.Localization_EffectC[0], a.ToMulString(), b - a, b.ToMulString()));
        }
        return ret;
    }

}