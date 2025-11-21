using System;
using System.Collections;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TiledStatsUI_InWorldTorus : MonoBehaviour, IPointerClickHandler, HandUIController.IStateChangeListener
{
    static event Action s_DoOnceOnClose;
    
    public MeshRenderer TorusMesh;
    public Transform TileContainer;
    public TiledStatsUI_InWorldTorus_Tile TileTemplate;
    public Mesh[] TileMeshes = Array.Empty<Mesh>();
    public Mesh[] TileOutlineMeshes = Array.Empty<Mesh>();
    public Material[] TileTextures = Array.Empty<Material>();
    public Material[] TileDisabledTextures = Array.Empty<Material>();
    public Sprite[] TileSprites = Array.Empty<Sprite>();
    public TorusTerrainTool TerrainTool;
    public float Radius = 8;
    public float Thickness = 3.5f;
    public float ItemOffset = 0.2f;
    public int MeshRingSegments = 10;
    public int MeshTubeSegments = 10;

    public float ColumnLineOffset = 0.00f;
    public float ColumnLineThickness = 0.01f;
    public float RowLineOffset = 0.001f;
    public float RowLineThickness = 0.01f;
    
    public float PurchasedTextureOffset = 0.3f;


    public int2 TargetIndex;
    public float2 FocusedIndex;
    public float2 AnimFocusedIndex => FocusedIndex + m_RevealAnimOffset;
    
    public float2 ZeroOffset = new float2(math.PI, math.PIHALF);
    float2 m_ZeroOffset;
    public float2 ItemGrouping = new float2(0.7f, 0.4f);
    float2 m_ItemGrouping;
    public float2 ItemGroupingSmoothing = new float2(1f, 1f);
    float2 m_ItemGroupingSmoothing;
    

    public Transform RowLineContainer;
    public Transform ColumnLineContainer;
    public Material[] RowLineTextures = Array.Empty<Material>();
    public Material[] ColumnLineTextures = Array.Empty<Material>();
    public Material[] RowVertexMats = Array.Empty<Material>();
    public Material[] ColumnVertexMats = Array.Empty<Material>();
    
    public float RowHeight = 0.01f;
    public float ColumnWidth = 0.01f;
    
    public TorusTerrainTool CylinderTemplate;
    
    [Header("UI Stuff")]
    public TMP_Text RemainingPointsText;

    public enum eState
    {
        Locked,
        AvailableNoPoints,
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
        // Unfocus current
        ClearFocus();
        TargetIndex = (int2)mathu.repeat(index, new int2(TiledStats.TileCols, TiledStats.TileRows));
        SetPosition(TargetIndex);
        StartFocus();
    }

    // Close on first awake
    private void Awake()
    {
        HandUIController.Attach(this);
        SetIndex(default);
        Close();
    }

    private void OnDestroy()
    {
        HandUIController.Detach(this);
    }

    ExclusiveCoroutine m_RevealItemsCo;
    private void OnEnable()
    {
        GameEvents.OnInventoryChanged += OnInventoryChanged;
        GameEvents.OnWalletChanged += OnWalletChanged;
        if (CameraTarget.MainTarget) Demo();
        else
        {
            Demo();
        }
    }

    private void OnDisable()
    {
        GameEvents.OnInventoryChanged -= OnInventoryChanged;
        GameEvents.OnWalletChanged -= OnWalletChanged;
        
        ClearFocus();
    }

    private void OnInventoryChanged(Entity entity)
    {
        if (entity != CameraTarget.MainEntity) return;
        Demo();
    }

    private void OnWalletChanged(Entity entity, Wallet wallet)
    {
        if (entity != CameraTarget.MainEntity) return;
        Demo();
    }

    [Header("Reveal Animation")]
    public float2 InitZero;
    public float2 InitItemGrouping = 1f;
    public float2 InitItemGroupingSmoothing = 1f;
    public float2 InitRevealAnimOffset = new float2(0, 2f);
    public float InitTime = 0.2f;
    public float Duration = 1f;
    public float FocusDelay = 0.8f;
    public ease.Mode EasingMode;
    float2 m_RevealAnimOffset;
    
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
            Demo();
            yield return null;
        }
        StartFocus(); // Fallback
    }

    public static void OpenUI(Action DoOnceOnClose)
    {
        s_DoOnceOnClose += DoOnceOnClose;
        HandUIController.SetState(HandUIController.State.Skills);
    }

    [EditorButton]
    public void Open()
    {
        HandUIController.SetState(HandUIController.State.Skills);
    }
    
    [EditorButton]
    public void Close()
    {
        HandUIController.SetState(HandUIController.State.Closed);
    }
    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        if (newState == HandUIController.State.Skills)
        {
            if (oldState != HandUIController.State.Skills)
            {
                ClearFocus();
                RevealItems();
            }
        }
        else
        {
            if (Application.isPlaying)
            {
                ClearFocus();
                var close = s_DoOnceOnClose;
                s_DoOnceOnClose = null;
                close?.Invoke();
            }
        }
    }

    private void ClearFocus()
    {
        var index = GetIndex(TargetIndex);
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUI_InWorldTorus_Tile>();
        if (index >= 0 && index < tiles.Length && tiles[index].TryGetComponent<Focusable>(out var focusable))
        {
            if (Application.isPlaying) UIFocus.EndFocus(focusable);
        }
    }

    private void StartFocus()
    {
        var index = GetIndex(TargetIndex);
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUI_InWorldTorus_Tile>();
        if (index >= 0 && index < tiles.Length && tiles[index].TryGetComponent<Focusable>(out var focusable))
        {
            if (Application.isPlaying) UIFocus.StartFocus(focusable);
        }
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
        var length = new float2(TiledStats.TileCols, TiledStats.TileRows) / 2;
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

    public long GetAvailalePoints()
    {
        if (!CameraTarget.MainTarget || Game.ClientGame == null) return 0;
        return GameEvents.GetComponent<PlayerLevel>(CameraTarget.MainTarget.Entity).Level - GameEvents.GetComponent<TiledStatsTree>(CameraTarget.MainTarget.Entity).GetLevelsSpent();
    }
    public long GetSpentPoints()
    {
        if (!CameraTarget.MainTarget || Game.ClientGame == null) return 0;
        return GameEvents.GetComponent<TiledStatsTree>(CameraTarget.MainTarget.Entity).GetLevelsSpent();
    }
    private TiledStatsTree GetUnlocked()
    {
        if (!CameraTarget.MainTarget || Game.ClientGame == null) return default;
        return GameEvents.GetComponent<TiledStatsTree>(CameraTarget.MainTarget.Entity);
    }

    public const float KeyboardInputCD = 0.1f;
    public float TimeSinceKeyboardInput = 0f;
    private void Update()
    {
        if (HandUIController.GetState() == HandUIController.State.Skills)
        {
            var dir = GameInput.Inputs.UI.Navigate.ReadValue<Vector2>();
            if (dir.magnitude > 0.5f)
            {
                if (TimeSinceKeyboardInput > 0)
                {
                    TimeSinceKeyboardInput -= Time.deltaTime;
                }
                else
                {
                    // Get the current focused tile and select the one in the direction of input
                    SetIndex((int2)math.round(dir*1.2f) + TargetIndex);
                    TimeSinceKeyboardInput = KeyboardInputCD;
                }
            }
            else
            {
                TimeSinceKeyboardInput = 0;
            }
        }
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
        
        TorusMesh.sharedMaterial.SetVector("_OffsetPercentage", new float4(AnimFocusedIndex/new float2(TiledStats.TileCols, TiledStats.TileRows),0,0));
        
        var floorPos = math.floor(AnimFocusedIndex);
        var ceilPos = math.ceil(AnimFocusedIndex);
        if (floorPos.y != ceilPos.y)
        {
            var t = math.unlerp(floorPos.y, ceilPos.y, AnimFocusedIndex.y);
            var floorMat = TileTextures[(int)mathu.modabs(floorPos.y, TileTextures.Length)];
            var ceilMat = TileTextures[(int)mathu.modabs(ceilPos.y, TileTextures.Length)];
            TorusMesh.sharedMaterial.SetColor("_Dither_ColorB", Color.Lerp(floorMat.GetColor("_Dither_ColorA"), ceilMat.GetColor("_Dither_ColorA"), t));
        }
        else
        {
            TorusMesh.sharedMaterial.SetColor("_Dither_ColorB", TileTextures[(int)mathu.modabs(AnimFocusedIndex.y, TileTextures.Length)].GetColor("_Dither_ColorA"));
        }
        
        var unlocked = GetUnlocked();
        
        var torus = new Torus(Radius, Thickness);
        var torusZero = new Torus(Radius, 0.001f);
        int tileIndex = 0;
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUI_InWorldTorus_Tile>();
        var columns = ColumnLineContainer.GetComponentsInChildren<MeshRenderer>();
        var rows = RowLineContainer.GetComponentsInChildren<MeshRenderer>();
        var availablePoints = GetAvailalePoints();
        var numberUnlocked = GetSpentPoints();
        
        for (int x = 0; x < TiledStats.TileCols; x++)
        for (int y = 0; y < TiledStats.TileRows; y++)
        {
            TiledStatsUI_InWorldTorus_Tile tile;
            if (tileIndex < tiles.Length)
                tile = tiles[tileIndex];
            else
                tile = Instantiate(TileTemplate, TileContainer);

            tile.SetStat((TiledStat)tileIndex);
            tile.SetMesh(TileMeshes[x % TileMeshes.Length]);
            tile.SetOutlineMesh(TileOutlineMeshes[x % TileMeshes.Length]);
            tile.SetMaterials(TileTextures[y % TileTextures.Length], 
                TileDisabledTextures[y % TileDisabledTextures.Length], 
                TileTextures[(y + (int)math.ceil(PurchasedTextureOffset*TileTextures.Length)) % TileTextures.Length]
                );
            tile.SetSprite(TileSprites[tileIndex % TileSprites.Length]);
            {
                bool isUnlocked = unlocked[tileIndex]>0;
                bool neighbourUnlocked = numberUnlocked == 0 ||
                                         unlocked[GetIndex(new int2(x,y+1))]>0 || 
                                         unlocked[GetIndex(new int2(x,y-1))]>0 || 
                                         unlocked[GetIndex(new int2(x+1,y))]>0 || 
                                         unlocked[GetIndex(new int2(x-1,y))]>0;
                tile.SetUnlocked(isUnlocked ? eState.Purchased : neighbourUnlocked && availablePoints > 0 ? eState.Available : neighbourUnlocked ? eState.AvailableNoPoints : eState.Locked);
            }
            if (math.all(new int2(x, y) == TargetIndex))
            {
                RemainingPointsText.text = $"LEVEL UP SKILLS\n{availablePoints} Points Left";
            }

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
                
                if (ColumnVertexMats.Length != TiledStats.TileCols*TiledStats.TileRows)
                    Array.Resize(ref ColumnVertexMats, TiledStats.TileCols*TiledStats.TileRows);
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
                column.sharedMaterial.SetFloat("_Offset", AnimFocusedIndex.y/TiledStats.TileRows);
                bool isUnlocked = unlocked[tileIndex]>0;
                bool neighbourUnlocked = unlocked[GetIndex(new int2(x,y+1))]>0;
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
                
                if (RowVertexMats.Length != TiledStats.TileCols*TiledStats.TileRows)
                    Array.Resize(ref RowVertexMats, TiledStats.TileCols*TiledStats.TileRows);
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
                row.sharedMaterial.SetFloat("_Offset", AnimFocusedIndex.y/TiledStats.TileRows);
                bool isUnlocked = unlocked[tileIndex]>0;
                bool neighbourUnlocked = unlocked[GetIndex(new int2(x+1,y))]>0;
                row.sharedMaterial.SetColor("_Color", 
                    isUnlocked && neighbourUnlocked ? Color.white :
                    isUnlocked || neighbourUnlocked ? Color.white :
                    new Color(0.1f,0.1f,0.1f,1f));
            }
            
            tileIndex++;
        }
        
        UIFocus.Refresh();
    }

    private float2 GetToroidalForXY(float x, float y)
    {
        float2 toroidal = new float2((x - AnimFocusedIndex.x) * math.PI2 / TiledStats.TileCols, (y - AnimFocusedIndex.y) * math.PI2 / TiledStats.TileRows);
        toroidal.x = mathu.lerprepeat(toroidal.x, 0,
            m_ItemGrouping.x * math.clamp(ease.cubic_out(m_ItemGroupingSmoothing.x * math.abs(mathu.deltaangle(math.PI, toroidal.x)) / math.PI), 0, 1), math.PI);
        toroidal.y = mathu.lerprepeat(toroidal.y, 0,
            m_ItemGrouping.y * math.clamp(ease.cubic_out(m_ItemGroupingSmoothing.y * math.abs(mathu.deltaangle(math.PI, toroidal.y)) / math.PI), 0, 1), math.PI);
        toroidal.x += m_ZeroOffset.x;
        toroidal.y -= m_ZeroOffset.y;
        return toroidal;
    }
    
    public int2 GetIndex2D(int index)
    {
        var index2d = new int2(mathu.modabs(index / TiledStats.TileRows,TiledStats.TileCols),mathu.modabs(index, TiledStats.TileRows));
        return index2d;
    }
    public int GetIndex(int2 index2D)
    {
        return mathu.modabs(index2D.x, TiledStats.TileCols)*TiledStats.TileRows + mathu.modabs(index2D.y, TiledStats.TileRows);
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
                var index2d = GetIndex2D(index);
                if (math.all(index2d == TargetIndex))
                {
                    UnlockTile(tile);
                }
                else
                    SetIndex(index2d);
            }
        }
    }
    
    public void UnlockTile(TiledStatsUI_InWorldTorus_Tile tile)
    {
        // Try find the index of this tile
        var tiles = TileContainer.GetComponentsInChildren<TiledStatsUI_InWorldTorus_Tile>();
        var index = Array.IndexOf(tiles, tile);
        
        if (Keyboard.current.shiftKey.isPressed)
            Game.ClientGame.RpcSendBuffer.Enqueue(
                GameRpc.AdminPlayerLevelStat((byte)Game.ClientGame.PlayerIndex, 
                    (TiledStat)(index),
                    true
                ));
        else if (Keyboard.current.ctrlKey.isPressed)
            Game.ClientGame.RpcSendBuffer.Enqueue(
                GameRpc.AdminPlayerLevelStat((byte)Game.ClientGame.PlayerIndex,
                    (TiledStat)(index),
                    false
                ));
        else
            Game.ClientGame.RpcSendBuffer.Enqueue(
                GameRpc.PlayerLevelStat((byte)Game.ClientGame.PlayerIndex,
                    (TiledStat)(index)
                ));
    }
}