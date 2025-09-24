using System;
using Unity.Burst;
using Unity.Collections;
using UnityEditor;
using UnityEngine;


public static partial class TiledStats
{
    public static NativeArray<TiledStatData>.ReadOnly StatData => m_StatDataPtr.Data;
    public static NativeArray<TiledStatData> m_StatDataPtrWrite;
    [ReadOnly] public static readonly SharedStatic<NativeArray<TiledStatData>.ReadOnly> m_StatDataPtr = SharedStatic<NativeArray<TiledStatData>.ReadOnly>.GetOrCreate<StatDataPtr>();
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
        public readonly TiledStatData Data;
        public readonly Localization Localization;
        public Image Images;

        public Full(TiledStat stat, curve effectA, curve effectB, curve effectC, string[] title, string[] description, string[] effectADesc, string[] effectBDesc,
            string[] effectCDesc)
        {
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
            var x = i%TiledStats.TileCount.x;
            var y = i/TiledStats.TileCount.x;
            StatDataFull[i].Images = new (columnSprites[x], columnSpritesOutlines[x], rowColors[y], images[i]);
        }
    }
    
    public static readonly TiledStatData.Full[] StatDataFull = new TiledStatData.Full[TiledStats.TileRows * TiledStats.TileCols]
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
            TiledStat.Stat_01,
            curve.exponential(0.30f),
            curve.linear(-0.10f),
            curve.zero,
            new string[] { "Heavy Blow" },
            new string[] { "Slower but stronger hits" },
            new string[] { "Damage:" },
            new string[] { "Attack Speed:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_02,
            curve.exponential(0.2f),
            curve.linear(-0.1f),
            curve.zero,
            new string[] { "Quick Hands" },
            new string[] { "Faster weapon swings" },
            new string[] { "Attack Speed:" },
            new string[] { "Damage:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_03,
            curve.exponential(0.1f),
            curve.zero,
            curve.zero,
            new string[] { "Bloodlust" },
            new string[] { "Kill feeds your fury" },
            new string[] { "Damage per Kill (5s):" },
            new string[] { "" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_04,
            curve.linear(1),
            curve.linear(-0.05f),
            curve.zero,
            new string[] { "Iron Skin" },
            new string[] { "Fortify your body" },
            new string[] { "Max HP:" },
            new string[] { "Move Speed:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_05,
            curve.linear(0.15f),
            curve.zero,
            curve.zero,
            new string[] { "Swift Step" },
            new string[] { "Faster on your feet" },
            new string[] { "Move Speed:" },
            new string[] { "" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_06,
            curve.exponential(0.20f),
            curve.linear(-1),
            curve.zero,
            new string[] { "Glass Cannon" },
            new string[] { "Pure offense" },
            new string[] { "Damage:" },
            new string[] { "Max HP:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_07,
            curve.linear(-0.05f),
            curve.zero,
            curve.zero,
            new string[] { "Guardian" },
            new string[] { "Endurance above all" },
            new string[] { "Shield Cooldown (100s default):" },
            new string[] { "" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_08,
            curve.constant(0.05f),
            curve.exponential(0.2f),
            curve.zero,
            new string[] { "Lucky Strike" },
            new string[] { "Chance for stronger crits" },
            new string[] { "Crit Chance:" },
            new string[] { "Crit Damage:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_09,
            curve.exponential(0.1f),
            curve.zero,
            curve.zero,
            new string[] { "Reckless Swing" },
            new string[] { "Attack with abandon" },
            new string[] { "Knockback:" },
            new string[] { "" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_10,
            curve.exponential(0.1f),
            curve.zero,
            curve.zero,
            new string[] { "Arcane Touch" },
            new string[] { "Channel raw energy" },
            new string[] { "Add Topological Damage:" },
            new string[] { "" },
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
            TiledStat.Stat_12,
            curve.exponential(0.05f),
            curve.linear(-0.05f),
            curve.zero,
            new string[] { "Focused Mind" },
            new string[] { "Steady and precise" },
            new string[] { "Crit Chance:" },
            new string[] { "Attack Speed:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_13,
            curve.exponential(0.05f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Frenzy" },
            new string[] { "Fighting builds momentum" },
            new string[] { "Attack Speed per Hit:" },
            new string[] { "Duration (Default 0.5s):" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_14_Homotopy_ProjCount,
            curve.linear(1),
            curve.zero,
            curve.zero,
            new string[] { "Homotopy" },
            new string[] { "Increase projectile count" },
            new string[] { "Projectile Count:" },
            new string[] { "" },
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
            TiledStat.Stat_17_Velocity_ProjSpeed,
            curve.exponential(0.15f),
            curve.zero,
            curve.zero,
            new string[] { "Velocity" },
            new string[] { "Increase projectile speed" },
            new string[] { "Projectile Speed:" },
            new string[] { "" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_18_Frequency_ProjRate,
            curve.exponential(0.15f),
            curve.zero,
            curve.zero,
            new string[] { "Frequency" },
            new string[] { "Projectile rate" },
            new string[] { "Frequency:" },
            new string[] { "" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_19,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_20,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
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
            TiledStat.Stat_22,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_23,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_24,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_25,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
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
            TiledStat.Stat_27,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_28,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_29,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_30,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
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
            TiledStat.Stat_32,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_33,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_34,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
        new(
            TiledStat.Stat_35,
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[] { "Sharp Edge" },
            new string[] { "Your strikes cut deeper" },
            new string[] { "Damage:" },
            new string[] { "Crit Chance:" },
            new string[] { "" }
        ),
    };
}