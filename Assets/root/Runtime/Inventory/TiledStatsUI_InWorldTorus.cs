using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class TiledStatsUI_InWorldTorus : MonoBehaviour
{
    public Transform TileContainer;
    public TiledStatsUI_InWorldTorus_Tile TileTemplate;
    public Mesh[] TileMeshes = Array.Empty<Mesh>();
    public Material[] TileTextures = Array.Empty<Material>();
    public Sprite[] TileSprites = Array.Empty<Sprite>();
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
    
    public GameObject RowLineTemplate;
    public Transform RowLineContainer;
    public GameObject ColumnLineTemplate;
    public Transform ColumnLineContainer;
    
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
        TargetIndex = (int2)mathu.repeat(index, new int2(CountX, CountY));
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
        var length = new float2(CountX, CountY)/2;
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
        FocusedIndex = mathu.repeat(position, length*2);
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
        
        if (ColumnLineTemplate.TryGetComponent<TorusTerrainTool>(out var columnLine))
            columnLine.GenerateMesh(Thickness, 0.1f, MeshRingSegments, MeshTubeSegments);
            
        Clear();
        Demo();
    }

    [EditorButton]
    public void Clear()
    {
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUI_InWorldTorus_Tile>();
        foreach (var tile in tiles)
            if (Application.isPlaying) Destroy(tile.gameObject); else DestroyImmediate(tile.gameObject);
        var columns = ColumnLineContainer.GetComponentsInChildren<MeshRenderer>();
        foreach (var obj in columns)
            if (Application.isPlaying) Destroy(obj.gameObject); else DestroyImmediate(obj.gameObject);
    }
    
    [EditorButton]
    public void Demo()
    {
        if (!TileTemplate) return;
        if (TileMeshes.Length == 0) return;
        if (TileTextures.Length == 0) return;
        if (TileSprites.Length == 0) return;
    
        var torus = new Torus(Radius, Thickness);
        int tileIndex = 0;
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUI_InWorldTorus_Tile>();
        for (int x = 0; x < CountX; x++)
        for (int y = 0; y < CountY; y++)
        {
            TiledStatsUI_InWorldTorus_Tile tile;
            if (tileIndex < tiles.Length)
                tile = tiles[tileIndex];
            else
                tile = Instantiate(TileTemplate, TileContainer);
                
            tile.MeshFilter.sharedMesh = TileMeshes[x % TileMeshes.Length];
            tile.SpriteBackgroundMesh.sharedMesh = TileMeshes[x % TileMeshes.Length];
            tile.MeshRenderer.sharedMaterial = TileTextures[y % TileTextures.Length];
            tile.SpriteRenderer.sprite = TileSprites[tileIndex % TileSprites.Length];
            tile.SpriteRenderer.color = TileTextures[y % TileTextures.Length].GetColor("_Dither_ColorA");
            
            float2 toroidal = new float2((x - FocusedIndex.x)*math.PI2/CountX, (y - FocusedIndex.y)*math.PI2/CountY);
            toroidal.x = mathu.lerprepeat(toroidal.x, 0, ItemGrouping.x*math.clamp(ease.cubic_out(ItemGroupingSmoothing.x*math.abs(mathu.deltaangle(math.PI, toroidal.x))/math.PI),0,1), math.PI);
            toroidal.y = mathu.lerprepeat(toroidal.y, 0, ItemGrouping.y*math.clamp(ease.cubic_out(ItemGroupingSmoothing.y*math.abs(mathu.deltaangle(math.PI, toroidal.y))/math.PI),0,1), math.PI);
            toroidal.x += ZeroOffset.x;
            toroidal.y -= ZeroOffset.y;
            var pos = torus.ToroidalToCartesian(toroidal, ItemOffset);
            var normal = torus.GetNormalQuaternion(pos, torus.ToroidalToCartesian(toroidal + new float2(0,0.1f)) - pos);
            tile.transform.SetLocalPositionAndRotation(pos, normal);
            
            tileIndex++;
        }
        
        // Create and position the columns
        if (ColumnLineContainer && ColumnLineTemplate)
        {
            var torusZero = new Torus(Radius, 0.01f);
            var columns = ColumnLineContainer.GetComponentsInChildren<MeshRenderer>();
            for (int x = 0; x < CountX; x++)
            {
                Transform column;
                if (x < columns.Length)
                    column = columns[x].transform;
                else
                    column = Instantiate(ColumnLineTemplate, ColumnLineContainer).transform;
                
                float2 toroidal = new float2((x - FocusedIndex.x)*math.PI2/CountX, 0);
                toroidal.x = mathu.lerprepeat(toroidal.x, 0, ItemGrouping.x*math.clamp(ease.cubic_out(ItemGroupingSmoothing.x*math.abs(mathu.deltaangle(math.PI, toroidal.x))/math.PI),0,1), math.PI);
                //toroidal.y = mathu.lerprepeat(toroidal.y, 0, ItemGrouping.y*math.clamp(ease.cubic_out(ItemGroupingSmoothing.y*math.abs(mathu.deltaangle(math.PI, toroidal.y))/math.PI),0,1), math.PI);
                toroidal.x += ZeroOffset.x;
                //toroidal.y -= ZeroOffset.y;
                var pos = torusZero.ToroidalToCartesian(toroidal, ItemOffset);
                var normal = torusZero.GetNormalQuaternion(pos, torusZero.ToroidalToCartesian(toroidal + new float2(0.1f,0)) - pos);
                column.SetLocalPositionAndRotation(pos, math.mul(normal, quaternion.AxisAngle(math.right(), math.PIHALF)));
            }
        }
        
        // Create and position the rows
        if (RowLineContainer && RowLineTemplate)
        {
            var rows = RowLineContainer.GetComponentsInChildren<MeshRenderer>();
            for (int y = 0; y < CountY; y++)
            {
                Transform row;
                if (y < rows.Length)
                    row = rows[y].transform;
                else
                    row = Instantiate(RowLineTemplate, RowLineContainer).transform;
                
                float2 toroidal = new float2(0, (y - FocusedIndex.y)*math.PI2/CountY);
                //toroidal.x = mathu.lerprepeat(toroidal.x, 0, ItemGrouping.x*math.clamp(ease.cubic_out(ItemGroupingSmoothing.x*math.abs(mathu.deltaangle(math.PI, toroidal.x))/math.PI),0,1), math.PI);
                toroidal.y = mathu.lerprepeat(toroidal.y, 0, ItemGrouping.y*math.clamp(ease.cubic_out(ItemGroupingSmoothing.y*math.abs(mathu.deltaangle(math.PI, toroidal.y))/math.PI),0,1), math.PI);
                //toroidal.x += ZeroOffset.x;
                toroidal.y -= ZeroOffset.y;
                var pos = torus.ToroidalToCartesian(toroidal, ItemOffset);
                row.localPosition = new float3(0, pos.y, 0);
                
                row.localScale = pos.x*Vector3.one;
            }
        }
    }
}
