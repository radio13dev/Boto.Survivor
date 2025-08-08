using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class GemDisplay : MonoBehaviour, IFocusFilter
{
	public MeshRenderer Renderer;
    public Gem Gem { get; private set; }
    public int Index { get; private set; } = -1;
    
    public bool IsInInventory => !IsInSlot;
    public bool IsInSlot => GetComponentInParent<RingFocusDisplay>();

    public void UpdateGem(int index, Gem gem)
    {
        Index = index;
        Gem = gem;
        Renderer.gameObject.SetActive(gem.IsValid);
        // TODO: Renderer.material = m_Gem.Material;
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
        if (UIFocus.Focus && UIFocus.Focus.TryGetComponent<GemDisplay>(out var gemSlot) && gemSlot.IsInSlot)
        {
            if (this.IsInInventory)
            {
                // Inv To Slot
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerSlotInventoryGemIntoRing((byte)Game.ClientGame.PlayerIndex, 
                        this.Index,
                        gemSlot.Index
                    ));
            }
            else
            {
                // Slot To Slot
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerSwapGemSlots((byte)Game.ClientGame.PlayerIndex, 
                        this.Index,
                        gemSlot.Index
                    ));
            }
        }
        else
        {
            if (this.IsInSlot)
            {
                // Slot To Inv
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerUnslotGem((byte)Game.ClientGame.PlayerIndex, 
                        this.Index
                    ));
            }
            else
            {
                // Invalid
                SnapBackToOrigin();
            }
        }
    }
    
    public bool IsDragEndValid() => (UIFocus.Focus && UIFocus.Focus.TryGetComponent<GemDisplay>(out var gemSlot) && gemSlot.IsInSlot) || (this.IsInSlot);

    public void SnapBackToOrigin()
    {
        if (IsInInventory)
        {
            // Snap back to a random nearby spot in the inventory
            var inventoryContainer = GetComponentInParent<InventoryContainer>();
            transform.SetDisplacedWorldPosition(inventoryContainer.GetRandomNearbyPosition(transform.position));
        }
        else
        {
            // Snap back to center of slot (the below method uses the lightweight physics stuff)
            transform.SetDisplacedLocalPosition(Vector3.zero);
        }
    }
    public void SnapBackToRandom()
    {
        if (IsInInventory)
        {
            // Snap back to a random nearby spot in the inventory
            var inventoryContainer = GetComponentInParent<InventoryContainer>();
            transform.SetDisplacedWorldPosition(inventoryContainer.GetRandomPosition());
        }
        else
        {
            // Snap back to center of slot (the below method uses the lightweight physics stuff)
            transform.SetDisplacedLocalPosition(Vector3.zero);
        }
    }

    public bool CheckFocusFilter(GameObject go)
    {
        if (go.TryGetComponent<RingDisplay>(out _)) return false;
        if (go.TryGetComponent<GemDisplay>(out var otherGem)) return otherGem.IsInSlot;
        return true;
    }
}