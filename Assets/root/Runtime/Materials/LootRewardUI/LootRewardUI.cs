using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

[ExecuteInEditMode]
public class LootRewardUI : MonoBehaviour
{
    public LootRewardUI_Item[] RewardTemplates = Array.Empty<LootRewardUI_Item>();
    public Transform RewardContainer;
    public LootRewardUI_Item[] ActiveItems => RewardContainer.GetComponentsInChildren<LootRewardUI_Item>();
    
    public int TargetIndex;
    public float FocusedIndex;
    public float2 AnimFocusedIndex => FocusedIndex + m_RevealAnimOffset;
    
    [Header("Torus Mesh Stuff")]
    public TorusTerrainTool TerrainTool;
    public float Radius = 8;
    public float Thickness = 3.5f;
    public float ItemOffset = 0.2f;
    public int MeshRingSegments = 10;
    public int MeshTubeSegments = 10;
    
    [Header("Reveal Animation")]
    float2 m_ZeroOffset;
    public float2 InitZero;
    public float2 ZeroOffset = new float2(math.PI, math.PIHALF);
    
    float2 m_ItemGrouping;
    public float2 InitItemGrouping = 1f;
    public float2 ItemGrouping = new float2(0.7f, 0.4f);
    
    float2 m_ItemGroupingSmoothing;
    public float2 InitItemGroupingSmoothing = 1f;
    public float2 ItemGroupingSmoothing = new float2(1f, 1f);
    
    float2 m_RevealAnimOffset;
    public float2 InitRevealAnimOffset = new float2(0, 2f);
    
    public float InitTime = 0.2f;
    public float Duration = 1f;
    public float FocusDelay = 0.8f;
    public ease.Mode EasingMode;
    
    [EditorButton]
    public void RebuildMesh()
    {
        TerrainTool.GenerateMesh(Radius, Thickness, MeshRingSegments, MeshTubeSegments);
        Clear();
        AddRewards();
    }
    
    [EditorButton]
    public void Clear()
    {
        var toClear = ActiveItems;
        foreach (var item in toClear)
        {
            if (Application.isPlaying) Destroy(item.gameObject); else DestroyImmediate(item.gameObject);
        }
    }
    
    [EditorButton]
    public void AddRewards()
    {
        if (RewardTemplates.Length == 0) return;
        
        int toSpawn = Random.Range(3,6);
        for (int i = 0; i < toSpawn; i++)
        {
            var spawned = Instantiate(RewardTemplates[Random.Range(0,RewardTemplates.Length)], RewardContainer);
        }
    }

    private void Update()
    {
        // Update positions of spawned
        var items = ActiveItems;
        var torus = new Torus(Radius, 0.01f);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var pos = GetToroidalForXY(index, 0, items.Length);
            item.transform.localPosition = torus.ToroidalToCartesian(pos);
        }
    }

    ExclusiveCoroutine m_RevealItemsCo;
    [EditorButton]
    public void RevealItems()
    {
        m_RevealItemsCo.StartCoroutine(this, RevealItemsCo());
    }
    IEnumerator RevealItemsCo()
    {
        float t = InitTime;
        while (t < Duration)
        {
            bool focus = t < FocusDelay;
            t += Time.deltaTime;
            t = math.clamp(t, 0, Duration);
            if (focus && t >= FocusDelay)
                StartFocus();
                
            var tEase = EasingMode.Evaluate(t/Duration);
            m_ZeroOffset = mathu.lerprepeat(InitZero, ZeroOffset, tEase, math.PI);
            m_ItemGrouping = math.lerp(InitItemGrouping, ItemGrouping, tEase);
            m_ItemGroupingSmoothing = math.lerp(InitItemGroupingSmoothing, ItemGroupingSmoothing, tEase);
            m_RevealAnimOffset = math.lerp(InitRevealAnimOffset, 0, tEase);
            yield return null;
        }
        StartFocus(); // Fallback
    }

    private void OnValidate()
    {
        m_ZeroOffset = ZeroOffset;
        m_ItemGrouping = ItemGrouping;
        m_ItemGroupingSmoothing = ItemGroupingSmoothing;
        m_RevealAnimOffset = 0;
    }
    
    private void ClearFocus()
    {
        var index = TargetIndex;
        var tiles = ActiveItems;
        if (index >= 0 && index < tiles.Length && tiles[index].TryGetComponent<Focusable>(out var focusable))
        {
            if (Application.isPlaying) UIFocus.EndFocus(focusable);
        }
    }
    private void StartFocus()
    {
        var index = TargetIndex;
        var tiles = ActiveItems;
        if (index >= 0 && index < tiles.Length && tiles[index].TryGetComponent<Focusable>(out var focusable))
        {
            if (Application.isPlaying) UIFocus.StartFocus(focusable);
        }
    }
    private float2 GetToroidalForXY(float x, float y, int rewardCount)
    {
        float2 toroidal = new float2((x - AnimFocusedIndex.x) * math.PI2 / rewardCount, (y - AnimFocusedIndex.y) * math.PI2);
        
        if (m_ItemGrouping.x >= 0)
        {
            toroidal.x = mathu.lerprepeat(toroidal.x, 0,
                m_ItemGrouping.x * math.clamp(ease.cubic_out(m_ItemGroupingSmoothing.x * math.abs(mathu.deltaangle(math.PI, toroidal.x)) / math.PI), 0, 1), math.PI);
        }
        else
        {
            toroidal.x = mathu.lerprepeat(toroidal.x, -math.PI,
                -m_ItemGrouping.x * math.clamp(ease.cubic_out(m_ItemGroupingSmoothing.x * (1-math.abs(mathu.deltaangle(math.PI, toroidal.x))) / math.PI), 0, 1), math.PI);
        }
        toroidal.y = mathu.lerprepeat(toroidal.y, 0,
            m_ItemGrouping.y * math.clamp(ease.cubic_out(m_ItemGroupingSmoothing.y * math.abs(mathu.deltaangle(math.PI, toroidal.y)) / math.PI), 0, 1), math.PI);
        
        
        toroidal.x += m_ZeroOffset.x;
        toroidal.y -= m_ZeroOffset.y;
        return toroidal;
    }
}
