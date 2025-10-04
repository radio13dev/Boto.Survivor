using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
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

        if (ChoiceUI.IsActive || (interact && interact != focus && interact.TryGetComponent<RingDisplay>(out var heldRing)))
        {
            sb.AppendLine("SWAP".Size(36).Color(new Color(0.9960785f, 0.4313726f, 0.3254902f)));
        }

        if (Ring.Stats.IsValid)
        {
            sb.AppendLine($"{Ring.Stats.GetTitleString()}".Size(36));
            sb.AppendLine($"{Ring.Stats.GetDescriptionString()}".Size(30));
        }
        else
        {
            sb.AppendLine("Empty Slot".Color(new Color(0.2f, 0.2f, 0.2f)).Size(30));
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
        
        return data;
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