using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TiledStatsUI_InWorldTorus : MonoBehaviour, IPointerClickHandler
{
    public MeshRenderer TorusMesh;
    public Transform TileContainer;
    public TiledStatsUI_InWorldTorus_Tile TileTemplate;
    public Mesh[] TileMeshes = Array.Empty<Mesh>();
    public Mesh[] TileOutlineMeshes = Array.Empty<Mesh>();
    public Material[] TileTextures = Array.Empty<Material>();
    public Material[] TileDisabledTextures = Array.Empty<Material>();
    public Sprite[] TileSprites = Array.Empty<Sprite>();
    public TorusTerrainTool TerrainTool;
    public int CountX = 6;
    public int CountY = 6;
    public float Radius = 8;
    public float Thickness = 3.5f;
    public float ItemOffset = 0.2f;
    public int MeshRingSegments = 10;
    public int MeshTubeSegments = 10;

    public float ColumnLineOffset = 0.00f;
    public float ColumnLineThickness = 0.01f;
    public float RowLineOffset = 0.001f;
    public float RowLineThickness = 0.01f;


    public int2 TargetIndex;
    public float2 FocusedIndex;
    public float2 ZeroOffset = new float2(math.PI, math.PIHALF);
    public float2 ItemGrouping = new float2(0.7f, 0.4f);
    public float2 ItemGroupingSmoothing = new float2(1f, 1f);

    public Transform RowLineContainer;
    public Transform ColumnLineContainer;
    public Material[] RowLineTextures = Array.Empty<Material>();
    public Material[] ColumnLineTextures = Array.Empty<Material>();
    public Material[] RowVertexMats = Array.Empty<Material>();
    public Material[] ColumnVertexMats = Array.Empty<Material>();
    
    public float RowHeight = 0.01f;
    public float ColumnWidth = 0.01f;

    public eState[] Unlocked;
    
    public TorusTerrainTool CylinderTemplate;

    public enum eState
    {
        Locked,
        Available,
        Purchased
    }

    ExclusiveCoroutine m_Co;

    private void OnValidate()
    {
        Demo();
    }

    [EditorButton]
    public void Up()
    {
        SetIndex(TargetIndex + new int2(0, 1));
    }

    [EditorButton]
    public void Down()
    {
        SetIndex(TargetIndex + new int2(0, -1));
    }

    [EditorButton]
    public void Right()
    {
        SetIndex(TargetIndex + new int2(1, 0));
    }

    [EditorButton]
    public void Left()
    {
        SetIndex(TargetIndex + new int2(-1, 0));
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
        var length = new float2(CountX, CountY) / 2;
        while (t < 10f)
        {
            var old = FocusedIndex;
            //FocusedIndex = mathu.lerprepeat(FocusedIndex, position, Time.deltaTime*RelativeSpeed, length);
            FocusedIndex += mathu.deltarepeat(FocusedIndex, position, length) * math.clamp(Time.deltaTime * LinearSpeed, 0, 0.1f);
            Demo();
            if (math.all(old == FocusedIndex)) break;
            t += Time.deltaTime;
            yield return null;
        }

        FocusedIndex = mathu.repeat(position, length * 2);
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

        if (CylinderTemplate)
            CylinderTemplate.GenerateCylinderMesh(ColumnLineThickness, MeshRingSegments, MeshTubeSegments);

        Clear();
        Demo();
    }

    [EditorButton]
    public void Clear()
    {
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUI_InWorldTorus_Tile>();
        foreach (var tile in tiles)
            if (Application.isPlaying) Destroy(tile.gameObject);
            else DestroyImmediate(tile.gameObject);
        var columns = ColumnLineContainer.GetComponentsInChildren<MeshRenderer>();
        foreach (var obj in columns)
            if (Application.isPlaying) Destroy(obj.gameObject);
            else DestroyImmediate(obj.gameObject);
        var rows = RowLineContainer.GetComponentsInChildren<MeshRenderer>();
        foreach (var obj in rows)
            if (Application.isPlaying) Destroy(obj.gameObject);
            else DestroyImmediate(obj.gameObject);
    }

    [EditorButton]
    public void Demo()
    {
        if (!TileTemplate) return;
        if (TileMeshes.Length == 0) return;
        if (TileOutlineMeshes.Length == 0) return;
        if (TileTextures.Length == 0) return;
        if (TileDisabledTextures.Length == 0) return;
        if (TileSprites.Length == 0) return;
        if (RowLineTextures.Length == 0) return;
        if (ColumnLineTextures.Length == 0) return;
        
        TorusMesh.sharedMaterial.SetVector("_OffsetPercentage", new float4(FocusedIndex/new float2(CountX,CountY),0,0));

        if (Unlocked.Length != CountX * CountY)
        {
            Array.Resize(ref Unlocked, CountX * CountY);
        }

        var torus = new Torus(Radius, Thickness);
        var torusZero = new Torus(Radius, 0.001f);
        int tileIndex = 0;
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUI_InWorldTorus_Tile>();
        var columns = ColumnLineContainer.GetComponentsInChildren<MeshRenderer>();
        var rows = RowLineContainer.GetComponentsInChildren<MeshRenderer>();
        
        for (int x = 0; x < CountX; x++)
        for (int y = 0; y < CountY; y++)
        {
            TiledStatsUI_InWorldTorus_Tile tile;
            if (tileIndex < tiles.Length)
                tile = tiles[tileIndex];
            else
                tile = Instantiate(TileTemplate, TileContainer);

            tile.SetMesh(TileMeshes[x % TileMeshes.Length]);
            tile.SetOutlineMesh(TileOutlineMeshes[x % TileMeshes.Length]);
            tile.SetMaterials(TileTextures[y % TileTextures.Length], TileDisabledTextures[y % TileTextures.Length]);
            tile.SetSprite(TileSprites[tileIndex % TileSprites.Length]);
            {
                bool isUnlocked = Unlocked[tileIndex] == eState.Purchased;
                bool neighbourUnlocked = Unlocked[GetIndex(new int2(x,y+1))] == eState.Purchased || 
                                         Unlocked[GetIndex(new int2(x,y-1))] == eState.Purchased || 
                                         Unlocked[GetIndex(new int2(x+1,y))] == eState.Purchased || 
                                         Unlocked[GetIndex(new int2(x-1,y))] == eState.Purchased;
                tile.SetUnlocked(isUnlocked ? eState.Purchased : neighbourUnlocked ? eState.Available : eState.Locked);
            }
            if (math.all(new int2(x, y) == TargetIndex))
                tile.DemoHovered();
            else
                tile.DemoUnhovered();

            var toroidal = GetToroidalForXY(x,y);
            var pos = torus.ToroidalToCartesian(toroidal, ItemOffset);
            var normal = torus.GetNormalQuaternion(pos, torus.ToroidalToCartesian(toroidal + new float2(0, 0.1f)) - pos);
            tile.transform.SetLocalPositionAndRotation(pos, normal);
            
            // Each tile has a vertical and horizontal line
            var nextToroidal = GetToroidalForXY(x+1,y+1);
            var delta = mathu.deltarepeat(toroidal, nextToroidal, math.PI);
            if (ColumnLineContainer && CylinderTemplate)
            {
                MeshRenderer column;
                if (tileIndex < columns.Length)
                    column = columns[tileIndex];
                else
                    column = Instantiate(CylinderTemplate.gameObject, ColumnLineContainer).GetComponent<MeshRenderer>();
                
                if (ColumnVertexMats.Length != CountX*CountY)
                    Array.Resize(ref ColumnVertexMats, CountX*CountY);
#if UNITY_EDITOR
                if (!ColumnVertexMats[tileIndex])
                {
                    var matToCopy = CylinderTemplate.GetComponent<MeshRenderer>().sharedMaterial;
                    var newMat = new Material(matToCopy);
                    var path = AssetDatabase.GenerateUniqueAssetPath(AssetDatabase.GetAssetPath(matToCopy));
                    AssetDatabase.CreateAsset(newMat, path);
                    ColumnVertexMats[tileIndex] = newMat;
                }
#endif
                
                column.sharedMaterial = ColumnVertexMats[tileIndex];
                column.sharedMaterial.CopyMatchingPropertiesFromMaterial(ColumnLineTextures[x % ColumnLineTextures.Length]);
                column.sharedMaterial.SetVector("_MinAngleDeltaAngle", new Vector4(toroidal.x-ColumnWidth, toroidal.y, ColumnWidth*2, delta.y));
                column.sharedMaterial.SetFloat("_Offset", FocusedIndex.y/CountY);
                bool isUnlocked = Unlocked[tileIndex] == eState.Purchased;
                bool neighbourUnlocked = Unlocked[GetIndex(new int2(x,y+1))] == eState.Purchased;
                column.sharedMaterial.SetColor("_Color", 
                    isUnlocked && neighbourUnlocked ? Color.white :
                    isUnlocked || neighbourUnlocked ? Color.white :
                    new Color(0.1f,0.1f,0.1f,1f));
            }
            if (RowLineContainer && CylinderTemplate)
            {
                MeshRenderer row;
                if (tileIndex < rows.Length)
                    row = rows[tileIndex];
                else
                    row = Instantiate(CylinderTemplate.gameObject, RowLineContainer).GetComponent<MeshRenderer>();
                
                if (RowVertexMats.Length != CountX*CountY)
                    Array.Resize(ref RowVertexMats, CountX*CountY);
#if UNITY_EDITOR
                if (!RowVertexMats[tileIndex])
                {
                    var matToCopy = CylinderTemplate.GetComponent<MeshRenderer>().sharedMaterial;
                    var newMat = new Material(matToCopy);
                    var path = AssetDatabase.GenerateUniqueAssetPath(AssetDatabase.GetAssetPath(matToCopy));
                    AssetDatabase.CreateAsset(newMat, path);
                    RowVertexMats[tileIndex] = newMat;
                }
#endif
                
                row.sharedMaterial = RowVertexMats[tileIndex];
                row.sharedMaterial.CopyMatchingPropertiesFromMaterial(RowLineTextures[x % RowLineTextures.Length]);
                row.sharedMaterial.SetVector("_MinAngleDeltaAngle", new Vector4(toroidal.x, toroidal.y-RowHeight, delta.x, RowHeight*2));
                row.sharedMaterial.SetFloat("_Offset", FocusedIndex.y/CountY);
                bool isUnlocked = Unlocked[tileIndex] == eState.Purchased;
                bool neighbourUnlocked = Unlocked[GetIndex(new int2(x+1,y))] == eState.Purchased;
                row.sharedMaterial.SetColor("_Color", 
                    isUnlocked && neighbourUnlocked ? Color.white :
                    isUnlocked || neighbourUnlocked ? Color.white :
                    new Color(0.1f,0.1f,0.1f,1f));
            }
            
            tileIndex++;
        }

        /*
        // Create and position the columns
        if (ColumnLineContainer && ColumnLineTemplate)
        {
            for (int x = 0; x < CountX; x++)
            {
                MeshRenderer column;
                if (x < columns.Length)
                    column = columns[x];
                else
                    column = Instantiate(ColumnLineTemplate.gameObject, ColumnLineContainer).GetComponent<MeshRenderer>();
                column.sharedMaterial = ColumnLineTextures[x % ColumnLineTextures.Length];

                float2 toroidal = new float2((x - FocusedIndex.x) * math.PI2 / CountX, (TargetIndex.y - FocusedIndex.y) * math.PI2 / CountY);
                toroidal.x = mathu.lerprepeat(toroidal.x, 0,
                    ItemGrouping.x * math.clamp(ease.cubic_out(ItemGroupingSmoothing.x * math.abs(mathu.deltaangle(math.PI, toroidal.x)) / math.PI), 0, 1), math.PI);
                toroidal.y = mathu.lerprepeat(toroidal.y, 0,
                    ItemGrouping.y * math.clamp(ease.cubic_out(ItemGroupingSmoothing.y * math.abs(mathu.deltaangle(math.PI, toroidal.y)) / math.PI), 0, 1), math.PI);
                toroidal.x += ZeroOffset.x;
                toroidal.y -= ZeroOffset.y;
                var pos = torusZero.ToroidalToCartesian(toroidal, ItemOffset);
                var normal = torusZero.GetNormalQuaternion(pos, torusZero.ToroidalToCartesian(toroidal + new float2(0.01f, 0)) - pos);
                column.transform.SetLocalPositionAndRotation(pos,
                    math.mul(normal, quaternion.AxisAngle(math.right(), math.PIHALF)));
            }
        }

        // Create and position the rows
        if (RowLineContainer && RowLineTemplate)
        {
            for (int y = 0; y < CountY; y++)
            {
                MeshRenderer row;
                if (y < rows.Length)
                    row = rows[y];
                else
                    row = Instantiate(RowLineTemplate.gameObject, RowLineContainer).GetComponent<MeshRenderer>();
                row.sharedMaterial = RowLineTextures[y % RowLineTextures.Length];

                float2 toroidal = new float2(0, (y - FocusedIndex.y) * math.PI2 / CountY);
                //toroidal.x = mathu.lerprepeat(toroidal.x, 0, ItemGrouping.x*math.clamp(ease.cubic_out(ItemGroupingSmoothing.x*math.abs(mathu.deltaangle(math.PI, toroidal.x))/math.PI),0,1), math.PI);
                toroidal.y = mathu.lerprepeat(toroidal.y, 0,
                    ItemGrouping.y * math.clamp(ease.cubic_out(ItemGroupingSmoothing.y * math.abs(mathu.deltaangle(math.PI, toroidal.y)) / math.PI), 0, 1), math.PI);
                //toroidal.x += ZeroOffset.x;
                toroidal.y -= ZeroOffset.y;
                var pos = torus.ToroidalToCartesian(toroidal, ItemOffset);
                row.transform.localPosition = new float3(0, pos.y, 0);

                row.transform.localScale = pos.x * Vector3.one;
            }
        }
        */
    }

    private float2 GetToroidalForXY(float x, float y)
    {
        float2 toroidal = new float2((x - FocusedIndex.x) * math.PI2 / CountX, (y - FocusedIndex.y) * math.PI2 / CountY);
        toroidal.x = mathu.lerprepeat(toroidal.x, 0,
            ItemGrouping.x * math.clamp(ease.cubic_out(ItemGroupingSmoothing.x * math.abs(mathu.deltaangle(math.PI, toroidal.x)) / math.PI), 0, 1), math.PI);
        toroidal.y = mathu.lerprepeat(toroidal.y, 0,
            ItemGrouping.y * math.clamp(ease.cubic_out(ItemGroupingSmoothing.y * math.abs(mathu.deltaangle(math.PI, toroidal.y)) / math.PI), 0, 1), math.PI);
        toroidal.x += ZeroOffset.x;
        toroidal.y -= ZeroOffset.y;
        return toroidal;
    }
    
    public int2 GetIndex2D(int index)
    {
        var index2d = new int2(mathu.modabs(index / CountY,CountX),mathu.modabs(index, CountY));
        return index2d;
    }
    public int GetIndex(int2 index2D)
    {
        return mathu.modabs(index2D.x, CountX)*CountY + mathu.modabs(index2D.y, CountY);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject.TryGetComponent<TiledStatsUI_InWorldTorus_Tile>(out var tile))
        {
            // Try find the index of this tile
            var tiles = TileContainer.GetComponentsInChildren<TiledStatsUI_InWorldTorus_Tile>();
            var index = Array.IndexOf(tiles, tile);
            if (index >= 0)
            {
                var index2d = new int2(index / CountX, index % CountX);
                if (math.all(index2d == TargetIndex))
                {
                    Unlocked[index] = Unlocked[index] == eState.Locked ? eState.Available :
                        Unlocked[index] == eState.Available ? eState.Purchased :
                        eState.Locked;
                }
                else
                    SetIndex(index2d);
            }
        }
    }
}