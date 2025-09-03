using System.Collections.Generic;
using BovineLabs.Saving;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Save]
public struct Wallet : IComponentData
{
    public long Value;
}

public class TiledStatsUITile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IFocusFilter, DescriptionUI.ISource
{
    public Image[] BodyImages;
    public Image[] OutlineImages;
    public Image[] IconImages;

    public Image Background;
    public Image Border;
    public Image Image;
    public TMP_Text LevelText;

    public Image NotAvailableOverlay;
    public Image NotEnoughMoneyOverlay;

    public Image HasPointOutline;
    public Image MaxedOutline;
    public Image ModifiedOutline;

    public Image[] Connections = new Image[4];
    public Image[] Connecteds = new Image[4];

    public PooledParticle LevelUpParticle;

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Send drag movement to parent ui
        //var ui = GetComponentInParent<TiledStatsUI>();
        //if (ui)
        //{
        //    ui.ApplyDragDelta(eventData.delta);
        //}
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        if (UIFocus.Focus == gameObject)
        {
            if (DescriptionUI.m_CustomZero == gameObject && !NotAvailableOverlay.gameObject.activeSelf)
                DoLevelUp();
            FocusParentToMe();
        }
    }

    public void FocusParentToMe()
    {
        var ui = GetComponentInParent<TiledStatsUI>();
        if (ui)
        {
            ui.MoveTowards(Offset);
            DescriptionUI.m_CustomZero = gameObject;
        }
    }

    public void DoLevelUp()
    {
        var ui = GetComponentInParent<TiledStatsUI>();
        if (ui)
        {
            if (Keyboard.current.shiftKey.isPressed)
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.AdminPlayerLevelStat((byte)Game.ClientGame.PlayerIndex, 
                        (TiledStat)(this.TileKey.x + this.TileKey.y*TiledStats.TileCols),
                        true
                    ));
            else if (Keyboard.current.ctrlKey.isPressed)
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.AdminPlayerLevelStat((byte)Game.ClientGame.PlayerIndex,
                        (TiledStat)(this.TileKey.x + this.TileKey.y * TiledStats.TileCols),
                        false
                    ));
            else
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerLevelStat((byte)Game.ClientGame.PlayerIndex,
                        (TiledStat)(this.TileKey.x + this.TileKey.y * TiledStats.TileCols)
                    ));
        }
    }

    public Vector2 Offset;

    public void SetOffset(Vector2 offset)
    {
        Offset = offset;
    }

    public void SetImages(Sprite body, Sprite bodyOutline, Sprite icon)
    {
        foreach (var img in BodyImages)
            img.sprite = body;
        foreach (var img in OutlineImages)
            img.sprite = bodyOutline;
        foreach (var img in IconImages)
            img.sprite = icon;
    }

    int2 TileKey;
    int Level;
    int CompiledLevel;
    long m_Cost;

    static readonly int2[] directions = new int2[]
        {
            new int2(0,1),
            new int2(1,0),
            new int2(0,-1),
            new int2(-1,0)
        };

    public void RefreshState(int2 tileKey, in Wallet wallet, in TiledStatsTree baseStats, in CompiledStats compiledStats)
    {
        bool init = math.any(TileKey != tileKey);
        TileKey = tileKey;
        
        var oldLevel = Level;
        Level = baseStats[TileKey];
        CompiledLevel = compiledStats.CompiledStatsTree[TileKey];

        if (Application.isPlaying && !init && oldLevel < Level && CoroutineHost.Instance)
        {
            var particle = LevelUpParticle.GetFromPool();
            particle.transform.SetPositionAndRotation(transform.position, transform.rotation);
            particle.transform.localScale = Vector3.one * 100;
        }

        LevelText.text = $"{CompiledLevel}/{5}";

        m_Cost = baseStats.GetLevelUpCost(TileKey);
        var canAfford = m_Cost <= wallet.Value;
        
        NotEnoughMoneyOverlay.gameObject.SetActive(baseStats.CanLevelUp(TileKey) && !canAfford);
        MaxedOutline.gameObject.SetActive(Level >= 5);
        HasPointOutline.gameObject.SetActive(Level > 0 && Level < 5);
        ModifiedOutline.gameObject.SetActive(Level != CompiledLevel);

        bool leveled = Level > 0;
        bool locked = !leveled;
        for (int i = 0; i < 4; i++)
        {
            bool dirLeveled = baseStats[tileKey + directions[i]] > 0;
            if (dirLeveled && leveled)
            {
                Connecteds[i].gameObject.SetActive(true);
                Connections[i].gameObject.SetActive(false);
                locked = false;
            }
            else if (dirLeveled || leveled)
            {
                Connecteds[i].gameObject.SetActive(false);
                Connections[i].gameObject.SetActive(true);
                locked = false;
            }
            else
            {
                Connecteds[i].gameObject.SetActive(false);
                Connections[i].gameObject.SetActive(false);
            }
        }

        NotAvailableOverlay.gameObject.SetActive(locked);
        
        if (DescriptionUI.m_CustomZero == gameObject && (DescriptionUI.m_CustomZero == UIFocus.Focus || !UIFocus.Focus))
            UIFocus.Refresh();
    }

    public bool CheckFocusFilter(GameObject go)
    {
        return go == null;
    }

    public void GetDescription(out string title, out string description, out List<(string left, string oldVal, float change, string newVal)> rows,
        out (string left, DescriptionUI.eBottomRowIcon icon, string right) bottomRow)
    {
        if (DescriptionUI.m_CustomZero != gameObject)
        {
            DescriptionUI.m_CustomZero.GetComponent<TiledStatsUITile>().GetDescription(out title, out description, out rows, out bottomRow);
            return;
        }

        var stat = TiledStats.Get(TileKey);
        title = stat.GetTitle();
        description = stat.GetDescription();

        //if (Level == 0 || Level >= 5)
        //    rows = stat.GetDescriptionRows(math.max(CompiledLevel, 1));
        //else
            rows = stat.GetDescriptionRows(CompiledLevel, CompiledLevel + 1);

        if (Level >= 5)
        {
            // Maxed out
            bottomRow = ($"Maxed!", DescriptionUI.eBottomRowIcon.None, "-");
        }
        else if (NotAvailableOverlay.gameObject.activeSelf)
        {
            // Not available
            bottomRow = ($"Locked", DescriptionUI.eBottomRowIcon.None, "No adjacent unlock");
        }
        else
        {
            // Cost
            GameEvents.TryGetComponent2<Wallet>(CameraTarget.MainTarget.Entity, out var wallet);
            bool canAfford = m_Cost <= wallet.Value;
            bottomRow = ($"Cost", DescriptionUI.eBottomRowIcon.Gem, 
                $"{m_Cost.ToGemString()}{(canAfford ? "" : "  (Not enough)")}".Color(canAfford ? Palette.Money : Palette.MoneyChangeNegative));
        }
    }
}