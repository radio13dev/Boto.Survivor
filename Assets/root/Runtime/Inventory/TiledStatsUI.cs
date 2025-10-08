using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class TiledStatsUI : MonoBehaviour, HandUIController.IStateChangeListener
{
    private static readonly int UiCenter = Shader.PropertyToID("_UiCenter");
    private static readonly int UiSize = Shader.PropertyToID("_UiSize");
    public Transform CenterTransform;
    public Transform TopTransform;

    public TiledStatsUITile TileTemplate;
    public Transform TilesContainer;
    public List<TiledStatsUITile> Tiles = new();
    public Vector2Int BorderCount = new Vector2Int(1, 1);
    public Vector2 Spacing;

    public Color[] RowColors;
    public Sprite[] ColumnSprites;
    public Sprite[] ColumnSpritesOutlines;
    public Sprite[] Images;

    public RectTransform CompletedParent;
    public GameObject[] CompleteRows;
    public GameObject[] CompleteColumns;

    public Vector2 FocusedPosition = new Vector2(4, 4);
    public Vector2 VisualShift = new Vector2(0.5f, 0.5f);
    public Vector2 BackendShift = new Vector2(4, 4);

    [EditorButton]
    public void ForceRebuild()
    {
        while (Tiles.Count > 0)
        {
            if (Tiles[^1]) DestroyImmediate(Tiles[^1].gameObject);
            Tiles.RemoveAt(Tiles.Count - 1);
        }

        OnValidate();
    }

    private void OnEnable()
    {
        TiledStatsFull.SetImages(ColumnSprites, ColumnSpritesOutlines, RowColors, Images);

        HandUIController.Attach(this);

        GameEvents.OnEvent += OnGameEvent;
        if (CameraTarget.MainTarget) RebuildTilesFull();
        else
        {
            RebuildTiles(new(), TiledStatsTree.Default, new CompiledStats() { CompiledStatsTree = TiledStatsTree.Default }, default);
        }

        // Focus the UI 
        Tiles[0].FocusParentToMe();

        UIFocus.OnFocus += OnFocus;

        ChoiceUI.OnActiveRingChange += OnActiveRingChange;
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);

        GameEvents.OnEvent -= OnGameEvent;

        UIFocus.OnFocus -= OnFocus;

        ChoiceUI.OnActiveRingChange -= OnActiveRingChange;
    }

    ExclusiveCoroutine m_focusRedirectCo;

    private void OnFocus()
    {
        if (!UIFocus.Focus && HandUIController.GetState() == HandUIController.State.Inventory)
        {
            var focusedIndex = (BackendShift - FocusedPosition);
            var tileIndex = (mathu.modabs((int)math.round(focusedIndex.x), TiledStats.TileCount.x + BorderCount.x * 2)) +
                            (mathu.modabs((int)math.round(focusedIndex.y), TiledStats.TileCount.y + BorderCount.y * 2)) * (TiledStats.TileCount.x + BorderCount.x * 2);
            UIFocus.StartFocus(Tiles[tileIndex].GetComponent<Focusable>());
        }
        //m_focusRedirectCo.StartCoroutine(this, _AttemptReFocus());
        RebuildHighlights();
    }

    IEnumerator _AttemptReFocus()
    {
        yield return new WaitForSeconds(0.5f);
        if (!UIFocus.Focus && HandUIController.GetState() == HandUIController.State.Inventory)
        {
            var focusedIndex = (BackendShift - FocusedPosition);
            var tileIndex = (mathu.modabs((int)math.round(focusedIndex.x), TiledStats.TileCount.x + BorderCount.x * 2)) +
                            (mathu.modabs((int)math.round(focusedIndex.y), TiledStats.TileCount.y + BorderCount.y * 2)) * (TiledStats.TileCount.x + BorderCount.x * 2);
            UIFocus.StartFocus(Tiles[tileIndex].GetComponent<Focusable>());
        }
    }

    private void RebuildTilesFull()
    {
        if (CameraTarget.MainTarget)
            OnGameEvent(new(GameEvents.Type.InventoryChanged, CameraTarget.MainTarget.Entity));
    }

    private void OnGameEvent(GameEvents.Data data)
    {
        var eType = data.Type;
        var entity = data.Entity;
        if (eType != GameEvents.Type.InventoryChanged && eType != GameEvents.Type.WalletChanged) return;
        if (!GameEvents.TryGetSharedComponent<PlayerControlled>(entity, out var player)) return;
        if (player.Index != Game.ClientGame.PlayerIndex) return;
        if (GameEvents.TryGetComponent2<Wallet>(entity, out var wallet)
            && GameEvents.TryGetComponent2<TiledStatsTree>(entity, out var baseStats)
            && GameEvents.TryGetComponent2<CompiledStats>(entity, out var compiledStats)
            && GameEvents.TryGetBuffer<Ring>(entity, out var rings))
        {
            RebuildTiles(wallet, baseStats, compiledStats, rings.AsNativeArray());
        }
    }

    internal void RebuildTiles(in Wallet wallet, in TiledStatsTree baseStats, in CompiledStats compiledStats, in NativeArray<Ring> rings)
    {
        int c = (TiledStats.TileCount.x + BorderCount.x * 2) * (TiledStats.TileCount.y + BorderCount.y * 2);
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
            var tile = Instantiate(TileTemplate, TilesContainer);
            Tiles.Add(tile);
        }

        while (Tiles.Count > c)
        {
            DestroyImmediate(Tiles[^1].gameObject);
            Tiles.RemoveAt(Tiles.Count - 1);
        }
        

        for (int x = 0; x < TiledStats.TileCount.x + BorderCount.x * 2; x++)
        for (int y = 0; y < TiledStats.TileCount.y + BorderCount.y * 2; y++)
        {
            var tileArrayIndex = x + y * (TiledStats.TileCount.x + BorderCount.x * 2);
            var tile = Tiles[tileArrayIndex];

            var tileKey = new int2(x % TiledStats.TileCount.x, y % TiledStats.TileCount.y);
            tile.SetImages(ColumnSprites[tileKey.x], ColumnSpritesOutlines[tileKey.x], Images[tileKey.x + tileKey.y * TiledStats.TileCount.x]);
            tile.SetColor(RowColors[tileKey.y]);
            tile.gameObject.name = $"Tile: " + tile.StatIcon.Icon.sprite.name;

            tile.RefreshState(tileKey, in wallet, in baseStats, in compiledStats, in rings);
        }

        for (int x = 0; x < CompleteColumns.Length; x++)
            CompleteColumns[x].SetActive(baseStats.HasCompletedColumn(x % TiledStats.TileCols));
        for (int y = 0; y < CompleteRows.Length; y++)
            CompleteRows[y].SetActive(baseStats.HasCompletedRow(y % TiledStats.TileRows));
            
        RebuildHighlights();
    }
    
    public void RebuildHighlights()
    {
        Ring? highlightedRing = null;
        TiledStat? highlightedStat = null;
        
        if (ChoiceUI.IsActive)
            highlightedRing = ChoiceUI.ActiveRing;
        else if (UIFocus.Focus && UIFocus.Focus.TryGetComponent<TiledStatsUITile>(out var focusedTile))
            if (focusedTile.IsRing)
                highlightedRing = focusedTile.Ring;
            else
                highlightedStat = focusedTile.Stat;
        
        foreach (var tile in Tiles)
        {
            tile.NothingHighlighted();
            if (highlightedRing.HasValue || ChoiceUI.IsActive)
                tile.RingHighlighted(ChoiceUI.IsActive, highlightedRing.Value);
            else if (highlightedStat.HasValue)
                tile.StatHighlighted(false, highlightedStat.Value);
        }
    }

    public float RotationPivot = 250;

    [EditorButton]
    public void RefreshGrid()
    {
        for (int x = 0; x < TiledStats.TileCount.x + BorderCount.x * 2; x++)
        for (int y = 0; y < TiledStats.TileCount.y + BorderCount.y * 2; y++)
        {
            var tileArrayIndex = x + y * (TiledStats.TileCount.x + BorderCount.x * 2);
            var tile = Tiles[tileArrayIndex];

            var tilePos2D = new Vector2(
                mathu.modabs(FocusedPosition.x + x, TiledStats.TileCount.x + BorderCount.x * 2),
                mathu.modabs(FocusedPosition.y + y, TiledStats.TileCount.y + BorderCount.y * 2)
            );
            var tilePos = new float3(
                (tilePos2D.x - (TiledStats.TileCount.x + BorderCount.x) / 2.0f + VisualShift.x) * Spacing.x,
                (tilePos2D.y - (TiledStats.TileCount.y + BorderCount.y) / 2.0f + VisualShift.y) * Spacing.y,
                0);
            tile.transform.localPosition = tilePos;
            tile.transform.localRotation = quaternion.identity;
            tile.SetOffset(tilePos2D);
            
            //var focusEnabled = math.lengthsq(tilePos) < math.lengthsq(Spacing) * 6;
            //tile.GetComponent<Focusable>().enabled = focusEnabled;
            //tile.GetComponent<FocusableRequest>().enabled = focusEnabled;
        }

        for (int x = 0; x < CompleteColumns.Length; x++)
        {
            CompleteColumns[x].transform.localPosition = new float3(
                (mathu.modabs(FocusedPosition.x + x, TiledStats.TileCount.x + BorderCount.x * 2) - (TiledStats.TileCount.x + BorderCount.x) / 2.0f + VisualShift.x) * Spacing.x,
                0,
                60);
        }

        for (int y = 0; y < CompleteRows.Length; y++)
        {
            CompleteRows[y].transform.localPosition = new float3(
                0,
                (mathu.modabs(FocusedPosition.y + y, TiledStats.TileCount.y + BorderCount.y * 2) - (TiledStats.TileCount.y + BorderCount.y) / 2.0f + VisualShift.y) * Spacing.y,
                60);
        }

        m_Dirty = false;
    }

    private void OnActiveRingChange(int ringIndex)
    {
        RebuildTilesFull();
    }

    private void OnValidate()
    {
        CoroutineHost.FixOnValidateError(this, () =>
        {
            var demoStats = TiledStatsTree.Demo;
            RebuildTiles(Wallet.Demo, demoStats, CompiledStats.GetDemo(demoStats), Ring.DemoArray);
            RefreshGrid();
            UpdateMask();
        });
    }

    public float delDist = 0.3f;
    private void Update()
    {
        float dist = math.distance(FocusedPosition, math.round(FocusedPosition));
        if (HandUIController.GetState() == HandUIController.State.Inventory)
        {
            var dir = GameInput.Inputs.UI.Navigate.ReadValue<Vector2>();
            if (dist < delDist && dir.magnitude > 0.5f)
            {
                // Get the current focused tile and select the one in the direction of input
                var focusedIndex = (BackendShift - FocusedPosition) + (Vector2)math.round(dir);
                var tileIndex = (mathu.modabs((int)math.round(focusedIndex.x), TiledStats.TileCount.x + BorderCount.x * 2)) +
                                (mathu.modabs((int)math.round(focusedIndex.y), TiledStats.TileCount.y + BorderCount.y * 2)) * (TiledStats.TileCount.x + BorderCount.x * 2);
                    
                Tiles[tileIndex].FocusParentToMe();
            }
        }
        
        if (m_Dirty)
        {
            RefreshGrid();
        }
        else
        {
            if (!m_Dragging && (FocusedPosition.x % 1 != 0 || FocusedPosition.y % 1 != 0))
            {
                FocusedPosition = mathu.MoveTowards(FocusedPosition, math.round(FocusedPosition), Time.deltaTime * 5);
                RefreshGrid();
            }
        }

        UpdateMask();
    }

    [EditorButton]
    public void UpdateMask()
    {
        if (CameraRegistry.UI)
        {
            var center = (float3)CameraRegistry.UI.WorldToScreenPoint(CenterTransform.position);
            var top = (float3)CameraRegistry.UI.WorldToScreenPoint(TopTransform.position);
            Shader.SetGlobalVector(UiCenter, center.xyxx);
            Shader.SetGlobalFloat(UiSize, math.distance(top.xy, center.xy));
        }
    }

    public Vector2 DragScale = new Vector2(1, 1);
    internal bool m_Dirty;
    internal bool m_Dragging;

    public void StartDrag(TiledStatsUITile dragging)
    {
        m_Dragging = true;
    }

    public void ApplyDragDelta(Vector2 eventDataDelta)
    {
        FocusedPosition += eventDataDelta * DragScale;
        m_Dirty = true;
    }

    public void EndDrag(TiledStatsUITile dragging)
    {
        m_Dragging = false;
    }

    ExclusiveCoroutine co;

    public void MoveTowards(Vector2 offset)
    {
        co.StartCoroutine(this, MoveTowardsCo(offset));
    }

    IEnumerator MoveTowardsCo(Vector2 offset)
    {
        var init = FocusedPosition;
        var change = BackendShift - offset;
        var final = init + change;
        var t = 0f;
        var duration = 0.3f;
        while (t < duration)
        {
            t += Time.deltaTime;
            t = math.min(t, duration);
            var progress = t / duration;
            progress = ease.Mode.cubic_out.Evaluate(progress);
            FocusedPosition = math.lerp(init, final, progress);
            m_Dirty = true;
            yield return null;
        }
    }

    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        if (newState == HandUIController.State.Inventory) OnFocus();
    }
}