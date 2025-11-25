using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;

public class RingDisplay : MonoBehaviour, DescriptionUI.ISource
{
    private static readonly int UiMaskIgnored = Shader.PropertyToID("_UiMaskIgnored");
    private static readonly int Tier = Shader.PropertyToID("_Tier");

    public MeshRenderer NoRingDisplay;
    public MeshRenderer HasRingDisplay;
    public MeshRenderer RingRenderer;
    public MeshFilter RingFilter;
    public bool ShouldMask;

    public int Index { get; private set; }
    public Ring Ring { get; private set; }
    public bool IsPickup { get; private set; }
    EquippedGem[] m_Gems = Array.Empty<EquippedGem>();
    public ReadOnlyCollection<EquippedGem> Gems => Array.AsReadOnly(m_Gems);
    Material m_CreatedMat;

    private void Awake()
    {
        UpdateRing(-1, default, ReadOnlySpan<EquippedGem>.Empty, true);
    }

    public void UpdateRing(int index, Ring ring, ReadOnlySpan<EquippedGem> equippedGemsForRing, bool isPickup)
    {
        IsPickup = isPickup;
        Index = index;
        Ring = ring;
        m_Gems = equippedGemsForRing.ToArray();

        NoRingDisplay.gameObject.SetActive(!ring.Stats.IsValid);
        HasRingDisplay.gameObject.SetActive(ring.Stats.IsValid);

        // Display ring
        if (ring.Stats.IsValid)
        {
            if (m_CreatedMat) 
                if (!Application.isPlaying) DestroyImmediate(m_CreatedMat); 
                else Destroy(m_CreatedMat);
            RingRenderer.material = Ring.Stats.Material;
            RingFilter.sharedMesh = Ring.Stats.Mesh;
            if (Application.isPlaying)
            {
                m_CreatedMat = RingRenderer.material;
                m_CreatedMat.SetFloat(Tier, ring.Stats.Tier);
                if (ShouldMask)
                    m_CreatedMat.SetFloat(UiMaskIgnored, 0);
            }
        }
    }

    public void UpdateRing(int index, Ring ring, bool isPickup)
    {
        IsPickup = isPickup;
        Index = index;
        Ring = ring;
        m_Gems = Array.Empty<EquippedGem>();

        NoRingDisplay.gameObject.SetActive(!ring.Stats.IsValid);
        HasRingDisplay.gameObject.SetActive(ring.Stats.IsValid);

        // Display ring
        if (ring.Stats.IsValid)
        {
            if (m_CreatedMat) 
                if (!Application.isPlaying) DestroyImmediate(m_CreatedMat); 
                else Destroy(m_CreatedMat);
            RingRenderer.material = Ring.Stats.Material;
            RingFilter.sharedMesh = Ring.Stats.Mesh;
            if (Application.isPlaying)
            {
                m_CreatedMat = RingRenderer.material;
                m_CreatedMat.SetFloat(Tier, ring.Stats.Tier);
                if (ShouldMask)
                    m_CreatedMat.SetFloat(UiMaskIgnored, 0);
            }
        }
    }

    private void OnEnable()
    {
        if (TryGetComponent<DraggableElement>(out var draggable))
        {
            draggable.OnDraggingEnd += OnDraggingEnd;
        }
    }

    private void OnDisable()
    {
        if (TryGetComponent<DraggableElement>(out var draggable))
        {
            draggable.OnDraggingEnd -= OnDraggingEnd;
        }
    }

    private void OnDraggingEnd(PointerEventData eventData)
    {
        if (UIFocus.Focus && UIFocus.Focus.TryGetComponent<RingDisplay>(out var ringElement))
        {
            // Swap with this ring
            if (this.Index >= 0)
            {
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerSwapRingSlots((byte)Game.ClientGame.PlayerIndex,
                        (byte)this.Index,
                        (byte)ringElement.Index
                    ));
            }
            else if (GetComponentInParent<RingPopup>() is { } worldPosPopup)
            {
                // This ring is actually a pickup! Slot it in somewhere
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerPickupRing((byte)Game.ClientGame.PlayerIndex,
                        (byte)ringElement.Index,
                        worldPosPopup.transform.position
                    ));
                SnapBackToOrigin();
            }
            else
            {
                SnapBackToOrigin();
            }
        }
        else if (UIFocus.Focus && UIFocus.Focus.TryGetComponent<TrashCan>(out _))
        {
            // Trash this thing
            if (this.Index >= 0)
            {
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerDropRing((byte)Game.ClientGame.PlayerIndex,
                        (byte)this.Index
                    ));
                SnapBackToOrigin();
            }
        }
        else
        {
            // Snap back to origin
            SnapBackToOrigin();
        }
    }

    public void SnapBackToOrigin()
    {
        transform.SetDisplacedLocalPosition(Vector3.zero);
    }

    public DescriptionUI.Data GetDescription()
    {
        DescriptionUI.Data data = new();
    
        var interact = UIFocus.Interact;
        var focus = gameObject;
        StringBuilder sb = new();

        // Inventory
        if (!IsPickup)
            data.Title = "(Equipped)".Color(Color.gray).Size(30);
        else
            data.Title = "(Pickup)".Color(Color.mediumPurple).Size(30);

        if (Ring.Stats.IsValid)
        {
            sb.AppendLine($"{Ring.Stats.GetTitleString()}".Size(36));
            sb.AppendLine($"{Ring.Stats.GetDescriptionString()}".Size(30));
            
            data.CostFieldText = Ring.Stats.GetSellPrice().ToString();
        }
        else
        {
            sb.AppendLine($"Empty Slot".Size(36));
            sb.AppendLine($"Equip rings you find into this slot...".Size(30));
        }

        data.Description = sb.ToString();
        
        data.BottomVariant = DescriptionUI.eBottomRowVariant.SwapRing;
        data.BottomLeft = "Swap Ring";
        
        // Hate this
        GameEvents.TryGetComponent2<CompiledStats>(CameraTarget.MainTarget ? CameraTarget.MainTarget.Entity : default, out var stats);
        data.TiledStatsData = new DescriptionUI.Data.TiledStatData[RingStats.k_MaxStats];
        for (int i = 0; i < RingStats.k_MaxStats; i++)
        {
            if (Ring.Stats.GetStatBoost(i, out var stat, out var boost))
            {
                data.TiledStatsData[i] = new DescriptionUI.Data.TiledStatData()
                {
                    Stat = stat,
                    Low = stats.CompiledStatsTree[stat] - boost,
                    Boost = boost,
                    RingDisplayParent = this
                };
            }
        }
        
        data.ButtonPress = ChoiceUIGrab;
        data.ButtonPress1 = SellPress;
        data.ButtonPress2 = DropPress;
        
        if (ChoiceUI.IsActive)
        {
            if (Ring.Stats.IsValid) data.ButtonText = "Swap";
            else data.ButtonText = "Equip";
        }
        else if (interact && interact != focus && interact.TryGetComponent<RingDisplay>(out var heldRing))
        {
            // Dragging NO LONGER SUPPORTED
            data.ButtonText = "Swap";
        }
        else if (Ring.Stats.IsValid)
            data.ButtonText = "Move";
        else if (InteractableContextUI.Instance && InteractableContextUI.Instance.TryGetComponent<ShowInInteractRangeUI>(out var show) && show.m_Entity != Entity.Null)
        {
            data.ButtonText = "Equip";
            data.ButtonPress = EquipFromFloor;
            data.ButtonPress1 = null;
            data.ButtonPress2 = null;
        }
        
        return data;
    }
    
    private void EquipFromFloor()
    {
        if (InteractableContextUI.Instance && InteractableContextUI.Instance.TryGetComponent<ShowInInteractRangeUI>(out var show) && show.m_Entity != Entity.Null)
        {
            GameEvents.TryGetComponent2<LocalTransform>(show.m_Entity, out var ringT);
            Game.ClientGame.RpcSendBuffer.Enqueue(
                GameRpc.PlayerPickupRing((byte)Game.ClientGame.PlayerIndex,
                    (byte)this.Index,
                    ringT.Position
                ));
            UIFocus.Refresh();
            if (GetComponentInParent<TiledStatsUI>() is {} ui) ui.RebuildHighlights();
        }
    }

    private void ChoiceUIGrab()
    {
        if (ChoiceUI.ActiveRingIndex == Index)
        {
            this.CopyTransform(ChoiceUI.Instance.RingDisplay);
            ChoiceUI.Instance.Close();
        }
        else if (!ChoiceUI.IsActive)
            ChoiceUI.Instance.Setup(this);
        else
        {
            if (ChoiceUI.ActiveRingIndex >= 0)
            {
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerSwapRingSlots((byte)Game.ClientGame.PlayerIndex,
                        (byte)this.Index,
                        (byte)ChoiceUI.ActiveRingIndex
                    ));
                this.CopyTransform(ChoiceUI.Instance.RingDisplay);
            }
            else
            {
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerPickupRing((byte)Game.ClientGame.PlayerIndex,
                        (byte)this.Index,
                        ChoiceUI.PickupPosition
                    ));
                this.CopyTransform(ChoiceUI.Instance.RingDisplay);
            }
            ChoiceUI.Instance.Close();
        }
        
        UIFocus.Refresh();
        if (GetComponentInParent<TiledStatsUI>() is {} ui) ui.RebuildHighlights();
    }

    public void SellPress()
    {
        if (IsPickup)
        {
            Game.ClientGame.RpcSendBuffer.Enqueue(
                GameRpc.PlayerSellRing((byte)Game.ClientGame.PlayerIndex,
                    ChoiceUI.PickupPosition
                ));
        }
        else
        {
            Game.ClientGame.RpcSendBuffer.Enqueue(
                GameRpc.PlayerSellRing((byte)Game.ClientGame.PlayerIndex,
                    (byte)this.Index
                ));
        }
            
        if (ChoiceUI.ActiveRingIndex == Index)
            ChoiceUI.Instance.Close();
    }
    
    public void DropPress()
    {
        if (!IsPickup)
        {
            Game.ClientGame.RpcSendBuffer.Enqueue(
                GameRpc.PlayerDropRing((byte)Game.ClientGame.PlayerIndex,
                    (byte)this.Index
                ));
        }
        if (ChoiceUI.ActiveRingIndex == Index)
            ChoiceUI.Instance.Close();
    }

    public void CopyTransform(RingDisplay other)
    {
        var myGrabPivot = transform.GetChild(0);
        var otherGrabPivot = other.transform.GetChild(0);
        
        transform.position = otherGrabPivot.position;
        transform.SetDisplacedLocalPosition(Vector3.zero);
        
        var myRotatePivot = myGrabPivot.GetChild(0);
        var otherRotatePivot = otherGrabPivot.GetChild(0);
        myRotatePivot.rotation = otherRotatePivot.rotation;
    }
}