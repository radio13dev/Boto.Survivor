using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using UnityEditor;
using UnityEngine;


public static partial class TiledStats
{
    [Pure]
    public static NativeArray<TiledStatData>.ReadOnly StatData => m_StatDataPtr.Data;
    public static NativeArray<TiledStatData> m_StatDataPtrWrite;

    [ReadOnly]
    public static readonly SharedStatic<NativeArray<TiledStatData>.ReadOnly> m_StatDataPtr = SharedStatic<NativeArray<TiledStatData>.ReadOnly>.GetOrCreate<StatDataPtr>();

    class StatDataPtr
    {
    }

    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        if (!m_StatDataPtrWrite.IsCreated)
        {
            m_StatDataPtrWrite = default;
            m_StatDataPtr.Data = default;
        }
    }
}

public readonly struct TiledStatData
{
    public readonly curve EffectAValues;
    public readonly curve EffectBValues;
    public readonly curve EffectCValues;

    private TiledStatData(curve effectA, curve effectB, curve effectC)
    {
        EffectAValues = effectA;
        EffectBValues = effectB;
        EffectCValues = effectC;
    }

    // [English, ... ]
    public readonly struct Localization
    {
        public readonly string[] Localization_Title;
        public readonly string[] Localization_Description;
        public readonly string[] Localization_EffectA;
        public readonly string[] Localization_EffectB;
        public readonly string[] Localization_EffectC;

        public Localization(string[] title, string[] description, string[] effectADesc, string[] effectBDesc, string[] effectCDesc)
        {
            Localization_Title = title;
            Localization_Description = description;
            Localization_EffectA = effectADesc;
            Localization_EffectB = effectBDesc;
            Localization_EffectC = effectCDesc;
        }
    }

    public readonly struct Image
    {
        public readonly Sprite Shape;
        public readonly Sprite ShapeOutline;
        public readonly Color Color;
        public readonly Sprite Icon;

        public Image(Sprite shape, Sprite shapeOutline, Color color, Sprite icon)
        {
            Shape = shape;
            ShapeOutline = shapeOutline;
            Color = color;
            Icon = icon;
        }
    }

    public struct Full
    {
        public readonly TiledStat Stat;
        public readonly TiledStatData Data;
        public readonly Localization Localization;
        public Image Images;

        public Full(TiledStat stat, curve effectA, curve effectB, curve effectC, string[] title, string[] description, string[] effectADesc, string[] effectBDesc,
            string[] effectCDesc)
        {
            Stat = stat;
            Data = new TiledStatData(effectA, effectB, effectC);
            Localization = new Localization(title, description, effectADesc, effectBDesc, effectCDesc);
            Images = default;
        }
    }
}

public static class TiledStatsFull
{
    public static TiledStatData.Full GetFull(this TiledStat stat) => TiledStatsFull.StatDataFull[(int)stat];

    public static void Setup()
    {
        TiledStats.m_StatDataPtrWrite = new NativeArray<TiledStatData>(TiledStats.TileRows * TiledStats.TileCols, Allocator.Persistent);
        TiledStats.m_StatDataPtr.Data = TiledStats.m_StatDataPtrWrite.AsReadOnly();
        for (int i = 0; i < TiledStatsFull.StatDataFull.Length; i++)
            TiledStats.m_StatDataPtrWrite[i] = TiledStatsFull.StatDataFull[i].Data;
    }

    public static void Dispose()
    {
        if (TiledStats.m_StatDataPtrWrite.IsCreated) TiledStats.m_StatDataPtrWrite.Dispose();
    }

    public static void SetImages(Sprite[] columnSprites, Sprite[] columnSpritesOutlines, Color[] rowColors, Sprite[] images)
    {
        for (int i = 0; i < StatDataFull.Length; i++)
        {
            var x = i % TiledStats.TileCount.x;
            var y = i / TiledStats.TileCount.x;
            StatDataFull[i].Images = new(columnSprites[x], columnSpritesOutlines[x], rowColors[y], images[i]);
        }
    }

    public static readonly TiledStatData.Full[] StatDataFull = _GenerateFull();

    static TiledStatData.Full[] _GenerateFull()
    {
        var data = new TiledStatData.Full[TiledStats.TileRows * TiledStats.TileCols]
        {
            new(
                TiledStat.Stat_00_Ring0,
                curve.zero,
                curve.zero,
                curve.zero,
                new string[] { "Ring Slot" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_01_CritChance,
                curve.linear(0.05f, 0.05f),
                curve.zero,
                curve.zero,
                new string[] { "Crit Chance" },
                new string[] { "Chance for {crit_damage} extra damage" },
                new string[] { "Chance:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_02_Rate,
                curve.linear(0, 0.2f),
                curve.zero,
                curve.zero,
                new string[] { "Rate" },
                new string[] { "Reduced ring cooldown" },
                new string[] { "Rate:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_03_Momentum,
                curve.linear(0, 0.2f),
                curve.linear(0, 0.2f),
                curve.zero,
                new string[] { "Momentum" },
                new string[] { "Increased damage based on projectile speed" },
                new string[] { "Projectile Speed:" },
                new string[] { "Speed Damage:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_04_ProjectileCount,
                curve.linear(1, 1),
                curve.zero,
                curve.zero,
                new string[] { "Duplicate" },
                new string[] { "Increases projectile count" },
                new string[] { "Projectile Count:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_05_ProjectileSize,
                curve.linear(0, 0.2f),
                curve.zero,
                curve.zero,
                new string[] { "Proj. Size" },
                new string[] { "Increased projectile size" },
                new string[] { "Size:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_06_Pierce,
                curve.linear(0, 1),
                curve.zero,
                curve.zero,
                new string[] { "Pierce" },
                new string[] { "Number of enemies a projectile can pass through" },
                new string[] { "Pierce:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_07_Chain,
                curve.linear(0.3f, 0.01f),
                curve.zero,
                curve.zero,
                new string[] { "Chain" },
                new string[] { "Chance to chain damage to other enemies" },
                new string[] { "Chain Chance:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_08_Cut,
                curve.linear(0, 0.01f),
                curve.linear(0, 10),
                curve.zero,
                new string[] { "Cut" },
                new string[] { "Chance to cut enemies, adding damage on-hit" },
                new string[] { "Cut Chance:" },
                new string[] { "Cuts Applied:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_09_Degenerate,
                curve.linear(0, 0.01f),
                curve.linear(0, 1),
                curve.zero,
                new string[] { "Degenerate" },
                new string[] { "Chance to degenerate enemies, adding damage-over-time" },
                new string[] { "Degenerate Chance:" },
                new string[] { "Degenerations Applied:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_10_Subdivide,
                curve.linear(0, 0.01f),
                curve.linear(0, 1),
                curve.zero,
                new string[] { "Subdivide" },
                new string[] { "Chance to subdivide enemies, applying exponential damage after X seconds" },
                new string[] { "Subdivide Chance:" },
                new string[] { "Subdivisions Applied:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_11_Ring1,
                curve.zero,
                curve.zero,
                curve.zero,
                new string[] { "Ring Slot" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_12_Decimate,
                curve.linear(0, 0.01f),
                curve.linear(0, 1),
                curve.zero,
                new string[] { "Decimate" },
                new string[] { "Chance to decimate enemies, triggering a blast after destruction or 100 damage" },
                new string[] { "Decimate Chance:" },
                new string[] { "Decimations Applied:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_13_Dissolve,
                curve.linear(0, 0.01f),
                curve.linear(0, 0.01f),
                curve.zero,
                new string[] { "Dissolve" },
                new string[] { "Chance to dissolve enemies, increasing damage taken" },
                new string[] { "Dissolve Chance:" },
                new string[] { "Dissolve Increase:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_14_Poke,
                curve.linear(0, 0.01f),
                curve.linear(0, 5),
                curve.zero,
                new string[] { "Poke" },
                new string[] { "Chance to add damage" },
                new string[] { "Poke Chance:" },
                new string[] { "Poke Damage:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_15_Scale_Scale,
                curve.exponential(0.15f),
                curve.zero,
                curve.zero,
                new string[] { "Scale" },
                new string[] { "Increase projectile scale" },
                new string[] { "Scale:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_16_Ring2,
                curve.zero,
                curve.zero,
                curve.zero,
                new string[] { "Ring Slot" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_17_Reflect,
                curve.constant(5),
                curve.linear(0, 1),
                curve.zero,
                new string[] { "Reflect" },
                new string[] { "Increase rate after being hit" },
                new string[] { "Duration:" },
                new string[] { "Rate Increase:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_18_Overshield,
                curve.linearFromZero(20, -2),
                curve.zero,
                curve.zero,
                new string[] { "Overshield" },
                new string[] { "Protection from a hit of damage" },
                new string[] { "Overshield Cooldown:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_19_Immunity,
                curve.linear(0.3f, 0.1f),
                curve.zero,
                curve.zero,
                new string[] { "Immunity" },
                new string[] { "Increase immunity time after being hit" },
                new string[] { "Time:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_20_RateOnDestroy,
                curve.linear(0, 0.01f),
                curve.zero,
                curve.zero,
                new string[] { "Rate on Destroy" },
                new string[] { "Rate increases after destroying enemies" },
                new string[] { "Destroy Rate Increase:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_21_Ring3,
                curve.zero,
                curve.zero,
                curve.zero,
                new string[] { "Ring Slot" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_22_DmgOnDestroy,
                curve.linear(0, 0.01f),
                curve.zero,
                curve.zero,
                new string[] { "Sharpness on Destroy" },
                new string[] { "Damage increases after destroying enemies" },
                new string[] { "Destroy Damage Increase:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_23_Sharpness,
                curve.linear(0, 0.1f),
                curve.zero,
                curve.zero,
                new string[] { "Sharpness" },
                new string[] { "Increase damage" },
                new string[] { "Damage:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_24_Speed,
                curve.linear(0, 0.2f),
                curve.zero,
                curve.zero,
                new string[] { "Speed" },
                new string[] { "Increase speed" },
                new string[] { "Speed:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_25_SpeedOnDestroy,
                curve.linear(0, 0.01f),
                curve.zero,
                curve.zero,
                new string[] { "Speed on Destroy" },
                new string[] { "Speed increases after destroying enemies" },
                new string[] { "Destroy Speed Increase:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_26_Ring4,
                curve.zero,
                curve.zero,
                curve.zero,
                new string[] { "Ring Slot" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_27_Probability,
                curve.linear(0, 0.2f),
                curve.zero,
                curve.zero,
                new string[] { "Probability" },
                new string[] { "Improve all chance outcomes in your favor" },
                new string[] { "Chance Increase:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_28_Finding,
                curve.linear(0, 0.5f), // Play effect when this occurs
                curve.zero,
                curve.zero,
                new string[] { "Finding" },
                new string[] { "Improves chance for equipment drops" },
                new string[] { "Equipment Chance:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_29_Economical,
                curve.linear(1, 0.2f),
                curve.linear(0f, 0.01f),
                curve.zero,
                new string[] { "Economical" },
                new string[] { "Increases damage based on wealth" },
                new string[] { "Income:" },
                new string[] { "Damage per Wealth:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_30_Quality,
                curve.linear(0, 0.05f), // Play effect when this occurs, can trigger off self
                curve.zero,
                curve.zero,
                new string[] { "Quality" },
                new string[] { "Improves found equipment" },
                new string[] { "Quality Boost Chance:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_31_Ring5,
                curve.zero,
                curve.zero,
                curve.zero,
                new string[] { "Ring Slot" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_32_AbilityCooldown,
                curve.linear(90, -5f),
                curve.zero,
                curve.zero,
                new string[] { "Ability Cooldown" },
                new string[] { "Reduces ability cooldown" },
                new string[] { "Ability Cooldown (sec):" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_33_AbilityCooldownOnDestroy,
                curve.linear(0, 0.2f),
                curve.zero,
                curve.zero,
                new string[] { "Cooldown on Destroy" },
                new string[] { "Reduces ability cooldown on destroy" },
                new string[] { "Destroy Cooldown Refund:" },
                new string[] { "" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_34_RateOnAbility,
                curve.constant(5),
                curve.linear(0, 0.5f), // Fades over time
                curve.zero,
                new string[] { "Rate on Ability" },
                new string[] { "Increases rate on ability usage" },
                new string[] { "Duration:" },
                new string[] { "Rate Increase on Ability:" },
                new string[] { "" }
            ),
            new(
                TiledStat.Stat_35_DamageOnAbility,
                curve.constant(5),
                curve.linear(0, 0.5f), // Fades over time
                curve.zero,
                new string[] { "Sharpness on Ability" },
                new string[] { "Increases damage on ability usage" },
                new string[] { "Duration:" },
                new string[] { "Damage Increase on Ability:" },
                new string[] { "" }
            ),
        };

        data = data.OrderBy(d => d.Stat).ToArray();
        return data;
    }
}