using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public class RingDisplay : MonoBehaviour, DescriptionUI.ISource
{
    public MeshRenderer NoRingDisplay;
    public MeshRenderer HasRingDisplay;
    public MeshRenderer RingRenderer;
    public MeshFilter RingFilter;

    public int Index { get; private set; }
    public Ring Ring { get; private set; }
    EquippedGem[] m_Gems;
    public ReadOnlyCollection<EquippedGem> Gems => Array.AsReadOnly(m_Gems);

    private void Awake()
    {
        UpdateRing(-1, default, ReadOnlySpan<EquippedGem>.Empty);
    }

    public void UpdateRing(int index, Ring ring, ReadOnlySpan<EquippedGem> equippedGemsForRing)
    {
        Index = index;
        Ring = ring;
        m_Gems = equippedGemsForRing.ToArray();

        NoRingDisplay.gameObject.SetActive(!ring.Stats.IsValid);
        HasRingDisplay.gameObject.SetActive(ring.Stats.IsValid);

        // Display ring
        if (ring.Stats.IsValid)
        {
            RingRenderer.material = Ring.Stats.Material;
            RingFilter.mesh = Ring.Stats.Mesh;
        }
    }

    public void UpdateRing(int index, Ring ring)
    {
        Index = index;
        Ring = ring;
        m_Gems = Array.Empty<EquippedGem>();

        NoRingDisplay.gameObject.SetActive(!ring.Stats.IsValid);
        HasRingDisplay.gameObject.SetActive(ring.Stats.IsValid);

        // Display ring
        if (ring.Stats.IsValid)
        {
            RingRenderer.material = Ring.Stats.Material;
            RingFilter.mesh = Ring.Stats.Mesh;
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

    public void GetDescription(out string title, out string description, 
        out List<(string left, string oldVal, float change, string newVal)> rows,
        out (string left, DescriptionUI.eBottomRowIcon icon, string right) bottomRow)
    {
        var interact = UIFocus.Interact;
        var focus = gameObject;
        StringBuilder sb = new();

        // Inventory
        if (GetComponentInParent<RingUI>())
            title = "(Rings)".Color(Color.gray).Size(30);
        else
            title = "(Pickup)".Color(Color.mediumPurple).Size(30);

        if (interact && interact != focus && interact.TryGetComponent<RingDisplay>(out var heldRing))
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

        description = sb.ToString();

        rows = default;
        bottomRow = default;
    }
}