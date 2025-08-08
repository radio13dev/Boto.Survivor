using System;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.EventSystems;

public class RingDisplay : MonoBehaviour
{
    public MeshRenderer NoRingDisplay;
    public MeshRenderer HasRingDisplay;

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
            Game.ClientGame.RpcSendBuffer.Enqueue(
                GameRpc.PlayerSwapRingSlots((byte)Game.ClientGame.PlayerIndex, 
                    this.Index,
                    ringElement.Index
                ));
            
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
}