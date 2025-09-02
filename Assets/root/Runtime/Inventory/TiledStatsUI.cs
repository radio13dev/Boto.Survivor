using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;


[Flags]
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

public readonly struct TiledStatData
{
    public readonly curve EffectAValues;
    public readonly curve EffectBValues;
    public readonly curve EffectCValues;
    
    public readonly string[] Localization_Title;
    public readonly string[] Localization_Description;
    public readonly string[] Localization_EffectA;
    public readonly string[] Localization_EffectB;
    public readonly string[] Localization_EffectC;

    public TiledStatData(curve effectA, curve effectB, curve effectC, string[] title, string[] description, string[] effectADesc, string[] effectBDesc, string[] effectCDesc)
    {
        EffectAValues = effectA;
        EffectBValues = effectB;
        EffectCValues = effectC;
        Localization_Title = title;
        Localization_Description = description;
        Localization_EffectA = effectADesc;
        Localization_EffectB = effectBDesc;
        Localization_EffectC = effectCDesc;
    }
}

public static partial class TiledStats
{
    public static readonly Vector2Int TileCount = new Vector2Int(6, 6);

    public static TiledStat Get(Vector2Int tileKey)
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

[Serializable]
public struct UpgradePath
{
    // 2 bits per level, max 32 levels
    const ulong mask_0 = 0b0000_0011;

    public ulong m_all;

    public int Level => (sizeof(ulong) - math.lzcnt((ulong)m_all)) / 2;
    
    /// <summary>
    /// Value will always be: 0, 1, 2, or 3.
    /// </summary>
    /// <param name="level"> Levels go from 0 to 31</param>
    public byte this[int level]
    {
        get => (byte)((m_all & ((ulong)mask_0 << level)) >> level);
        set => m_all = (byte)(m_all | (((ulong)value << level) & (mask_0 << level)));
    }
}

public class TiledStatsUI : MonoBehaviour
{
    public List<TiledStatsUITile> Tiles = new();
    public Vector2Int BorderCount = new Vector2Int(1,1);
    public Vector2 Spacing;
    
    public Color[] RowColors;
    public Sprite[] ColumnSprites;
    public Sprite[] ColumnSpritesOutlines;
    public Sprite[] Images;
    
    public Vector2 Offset;
    
    [EditorButton]
    public void ForceRebuild()
    {
        while (Tiles.Count > 1)
        {
            DestroyImmediate(Tiles[^1].gameObject);
            Tiles.RemoveAt(Tiles.Count-1);
        }
        OnValidate();
    }

    private void OnEnable()
    {
        DescriptionUI.m_CustomZero = Tiles[0].gameObject;
    }

    internal void RebuildTiles()
    {
        int c = (TiledStats.TileCount.x + BorderCount.x*2)*(TiledStats.TileCount.y+BorderCount.y*2);
        for (int i = 0; i < Tiles.Count; i++)
        {
            if (!Tiles[i])
            {
                Tiles.RemoveAt(i);
                i--;
            }
        }
        while (Tiles.Count < c)
        {
            var tile = Instantiate(Tiles[0], Tiles[0].transform.parent);
            Tiles.Add(tile);
        }
        while (Tiles.Count > c)
        {
            DestroyImmediate(Tiles[^1].gameObject);
            Tiles.RemoveAt(Tiles.Count-1);
        }
        
        for (int x = 0; x < TiledStats.TileCount.x + BorderCount.x*2; x++)
        for (int y = 0; y < TiledStats.TileCount.y + BorderCount.y*2; y++)
        {
            var tileArrayIndex = x + y * (TiledStats.TileCount.x + BorderCount.x*2);
            var tile = Tiles[tileArrayIndex];
            
            var tileKey = new Vector2Int(x % TiledStats.TileCount.x, y % TiledStats.TileCount.y);
            tile.SetImages(ColumnSprites[tileKey.x], ColumnSpritesOutlines[tileKey.x], Images[tileKey.x + tileKey.y*TiledStats.TileCount.x]);
            tile.Background.color = RowColors[tileKey.y];
            tile.gameObject.name = $"Tile: " + tile.Image.sprite.name;
            
            tile.RefreshState(tileKey, ref m_unlocked);
        }
    }
    
    internal Dictionary<Vector2Int, int> m_unlocked = new()
    {
        { new Vector2Int(0,0), 1 },
    };
    
    public float RotationPivot = 250;
    [EditorButton]
    public void RefreshGrid()
    {
        for (int x = 0; x < TiledStats.TileCount.x + BorderCount.x*2; x++)
        for (int y = 0; y < TiledStats.TileCount.y + BorderCount.y*2; y++)
        {
            var tileArrayIndex = x + y * (TiledStats.TileCount.x + BorderCount.x*2);
            var tile = Tiles[tileArrayIndex];
            
            var tilePos2D = new Vector2(
                mathu.modabs(Offset.x + x, TiledStats.TileCount.x + BorderCount.x*2),
                mathu.modabs(Offset.y + y, TiledStats.TileCount.y + BorderCount.y*2)
            );
            var tilePos = new float3(
                (tilePos2D.x - (TiledStats.TileCount.x+BorderCount.x-1)/2.0f) * Spacing.x,
                (tilePos2D.y - (TiledStats.TileCount.y+BorderCount.y-1)/2.0f) * Spacing.y,
                0);
            tile.transform.localPosition = tilePos;
            tile.transform.localRotation = quaternion.identity;
            tile.SetOffset(tilePos2D);
            tile.GetComponent<Focusable>().enabled = math.lengthsq(tilePos) < math.lengthsq(Spacing)*6;
            //var forward = tile.transform.localPosition - new Vector3(0,0,RotationPivot);
            //tile.transform.localRotation = quaternion.LookRotationSafe(-forward, math.up());
        }
        m_Dirty = false;
    }

    private void OnValidate()
    {
        RebuildTiles();
        RefreshGrid();
    }

    private void Update()
    {
        if (m_Dirty)
        {
            RefreshGrid();
        }
        else if ((Offset.x % 1 != 0 || Offset.y % 1 != 0))
        {
            Offset = mathu.MoveTowards(Offset, math.round(Offset), Time.deltaTime*5);
            RefreshGrid();
        }
    }

    public Vector2 DragScale = new Vector2(1,1);
    internal bool m_Dirty;
    public void ApplyDragDelta(Vector2 eventDataDelta)
    {
        Offset += eventDataDelta*DragScale;
        m_Dirty = true;
    }

    public void MoveTowards(Vector2 offset)
    {
        StartCoroutine(MoveTowardsCo(offset));
    }
    
    IEnumerator MoveTowardsCo(Vector2 offset)
    {
        var init = Offset;
        var change = new Vector2(4,4) - offset;
        var final = init+change;
        var t = 0f;
        var duration = 0.3f;
        while (t < duration)
        {
            t += Time.deltaTime;
            t = math.min(t, duration);
            var progress = t / duration;
            progress = ease.Mode.cubic_out.Evaluate(progress);
            Offset = math.lerp(init, final, progress);
            m_Dirty = true;
            yield return null;
        }
    }
}