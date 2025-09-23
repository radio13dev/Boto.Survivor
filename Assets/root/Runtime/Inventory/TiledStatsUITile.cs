using System;
using System.Collections.Generic;
using BovineLabs.Saving;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[Save]
public struct Wallet : IComponentData
{
    public long Value;

    public static Wallet Demo
    {
        get
        {
            return new Wallet()
            {
                Value = Random.Range(0, 100)
            };
        }
    }
}

public class TiledStatsUITile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IFocusFilter, DescriptionUI.ISource
{
    public const int MaxLevel = 5;

    public Image[] BodyImages;
    public Image[] OutlineImages;
    public Image[] IconImages;

    public Image Background;
    public Image Border;
    public Image Image;
    public TMP_Text LevelText;

    public Image NotEnoughMoneyOverlay;
    public Image HasEnoughMoneyOverlay;

    public Image HasNoPointOutline;
    public Image HasPointOutline;
    public Image MaxedOutline;
    public Image ModifiedOutline;
    public TMP_Text ModifiedText;

    public Image[] Connecteds = new Image[4];
    public Image[] Connecteds_Ring = new Image[4];

    public PooledParticle LevelUpParticle;
    
    public RingDisplay RingTemplate;
    public RingDisplay m_SpawnedRing;
    public Image Background_Ring;
    public GameObject NotEnoughMoneyOverlay_Ring;
    public GameObject HasEnoughMoneyOverlay_Ring;
    public GameObject HasNoPointOutline_Ring;
    public GameObject HasPointOutline_Ring;
    
    public Transform RingContainer;
    public Transform UiContainer;

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
            if (DescriptionUI.m_CustomZero == gameObject)
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

    public void SetColor(Color rowColor)
    {
        Background.color = rowColor;
        Background_Ring.color = rowColor;
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

    public void RefreshState(int2 tileKey, in Wallet wallet, in TiledStatsTree baseStats, in CompiledStats compiledStats, in NativeArray<Ring> rings)
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

        LevelText.text = $"{Level}/{MaxLevel}";

        m_Cost = baseStats.GetLevelUpCost(TileKey);
        var canAfford = m_Cost <= wallet.Value;
        
        NotEnoughMoneyOverlay.gameObject.SetActive(!canAfford);
        HasEnoughMoneyOverlay.gameObject.SetActive(canAfford && Level < MaxLevel);
        MaxedOutline.gameObject.SetActive(Level >= MaxLevel);
        HasNoPointOutline.gameObject.SetActive(Level == 0 && CompiledLevel == 0);
        transform.localScale = Level > 0 ? Vector3.one : Vector3.one*0.6f;
        HasPointOutline.gameObject.SetActive(Level > 0 && Level < MaxLevel);
        ModifiedOutline.gameObject.SetActive(Level != CompiledLevel);
        ModifiedText.text = (CompiledLevel - Level).ToValueChangeString();
        ModifiedText.color = CompiledLevel > Level ? Palette.MoneyChangePositive : Palette.MoneyChangeNegative;

        bool leveled = Level > 0;
        
        for (int i = 0; i < 4; i++)
        {
            bool dirLeveled = baseStats[tileKey + directions[i]] > 0;
            if (dirLeveled && leveled)
            {
                Connecteds[i].gameObject.SetActive(true);
                Connecteds_Ring[i].gameObject.SetActive(true);
            }
            else
            {
                Connecteds[i].gameObject.SetActive(false);
                Connecteds_Ring[i].gameObject.SetActive(false);
            }
        }
        
        
        
        var ringIndex = TiledStats.Get(TileKey).GetRingIndex();
        if (ringIndex >= 0)
        {
            if (!m_SpawnedRing) m_SpawnedRing = Instantiate(RingTemplate, RingContainer);
            m_SpawnedRing.UpdateRing(ringIndex, rings.IsCreated ? rings[ringIndex] : default, ReadOnlySpan<EquippedGem>.Empty);
        
            NotEnoughMoneyOverlay_Ring.gameObject.SetActive(!canAfford);
            HasEnoughMoneyOverlay_Ring.gameObject.SetActive(canAfford && Level == 0);
            HasNoPointOutline_Ring.gameObject.SetActive(CompiledLevel == 0);
            HasPointOutline_Ring.gameObject.SetActive(CompiledLevel > 0);
        }
        else if (m_SpawnedRing)
        {
            if (Application.isPlaying) Destroy(m_SpawnedRing.gameObject);
            else DestroyImmediate(m_SpawnedRing.gameObject);
            m_SpawnedRing = null;
        }
        
        RingContainer.gameObject.SetActive(ringIndex >= 0);
        UiContainer.gameObject.SetActive(ringIndex < 0);
        
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

        if (Level >= MaxLevel)
        {
            // Maxed out
            bottomRow = ($"Maxed!", DescriptionUI.eBottomRowIcon.None, "-");
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