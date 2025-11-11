using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class TiledStatsUI_InWorldTorus : MonoBehaviour
{
    public Transform TileContainer;
    public TiledStatsUITile TileTemplate;
    public TorusTerrainTool TerrainTool;
    public int CountX = 6;
    public int CountY = 6;
    public float Radius = 8;
    public float Thickness = 3.5f;
    public float ItemOffset = 0.2f;
    public int MeshRingSegments = 10;
    public int MeshTubeSegments = 10;
    public int2 TargetIndex;
    public float2 FocusedIndex;
    public float2 ZeroOffset = new float2(math.PI, math.PIHALF);
    public float2 ItemGrouping = new float2(0.7f, 0.4f);
    public float2 ItemGroupingSmoothing = new float2(1f, 1f);
    ExclusiveCoroutine m_Co;

    private void OnValidate()
    {
        Demo();
    }
    
    [EditorButton]
    public void Up()
    {
        SetIndex(TargetIndex + new int2(0,1));
    }
    [EditorButton]
    public void Down()
    {
        SetIndex(TargetIndex + new int2(0,-1));
    }
    [EditorButton]
    public void Right()
    {
        SetIndex(TargetIndex + new int2(1,0));
    }
    [EditorButton]
    public void Left()
    {
        SetIndex(TargetIndex + new int2(-1,0));
    }
    public void SetIndex(int2 index)
    {
        TargetIndex = index;
        SetPosition(TargetIndex);
    }
    public void SetPosition(float2 position)
    {
        m_Co.StartCoroutine(this, _SetPositionCo(position));
    }
    
    public float RelativeSpeed = 1f;
    public float LinearSpeed = 1f;
    IEnumerator _SetPositionCo(float2 position)
    {
        float t = 0;
        var length = new float2(CountX, CountY);
        while (t < 10f)
        {
            var old = FocusedIndex;
            //FocusedIndex = mathu.lerprepeat(FocusedIndex, position, Time.deltaTime*RelativeSpeed, length);
            FocusedIndex += mathu.deltarepeat(FocusedIndex, position, length)*math.clamp(Time.deltaTime*LinearSpeed, 0, 0.1f);
            Demo();
            if (math.all(old == FocusedIndex)) break;
            t += Time.deltaTime;
            yield return null;
        }
        FocusedIndex = mathu.lerprepeat(FocusedIndex, position, 1, length);
        Demo();
    }
    
    [EditorButton]
    public void RebuildMesh()
    {
        TerrainTool.GenerateMesh(Radius, Thickness, MeshRingSegments, MeshTubeSegments);
        if (TerrainTool.TryGetComponent<MeshRenderer>(out var meshRenderer) && meshRenderer.sharedMaterial)
        {
            meshRenderer.sharedMaterial.SetFloat("_RingRadius", Radius);
            meshRenderer.sharedMaterial.SetFloat("_Thickness", Thickness);
        }
    }

    [EditorButton]
    public void Clear()
    {
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUITile>();
        foreach (var tile in tiles)
            if (Application.isPlaying) Destroy(tile.gameObject); else DestroyImmediate(tile.gameObject);
    }
    
    [EditorButton]
    public void Demo()
    {
        var torus = new Torus(Radius, Thickness);
        int tileIndex = 0;
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUITile>();
        for (int x = 0; x < CountX; x++)
        for (int y = 0; y < CountY; y++)
        {
            TiledStatsUITile tile;
            if (tileIndex < tiles.Length)
                tile = tiles[tileIndex];
            else
                tile = Instantiate(TileTemplate, TileContainer);
            
            float2 toroidal = new float2((x - FocusedIndex.x)*math.PI2/CountX, (y - FocusedIndex.y)*math.PI2/CountY);
            toroidal.x = mathu.lerprepeat(toroidal.x, 0, ItemGrouping.x*math.clamp(ease.cubic_out(ItemGroupingSmoothing.x*math.abs(mathu.deltaangle(math.PI, toroidal.x))/math.PI),0,1), math.PI);
            toroidal.y = mathu.lerprepeat(toroidal.y, 0, ItemGrouping.y*math.clamp(ease.cubic_out(ItemGroupingSmoothing.y*math.abs(mathu.deltaangle(math.PI, toroidal.y))/math.PI),0,1), math.PI);
            toroidal.x += ZeroOffset.x;
            toroidal.y -= ZeroOffset.y;
            var pos = torus.ToroidalToCartesian(toroidal, ItemOffset);
            var normal = torus.GetNormalQuaternion(pos, math.up());
            tile.transform.SetLocalPositionAndRotation(pos, normal);
            
            tileIndex++;
        }
    }
}
