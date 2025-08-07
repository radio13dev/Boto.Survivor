using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class GemDisplay : MonoBehaviour
{
	public MeshRenderer Renderer;
    public Gem Gem { get; private set; }
    public int InventoryIndex { get; private set; } = -1;
    
    public bool IsInInventory => !IsInSlot;
    public bool IsInSlot => GetComponentInParent<RingFocusDisplay>();

    public void UpdateGem(int inventoryIndex, Gem gem)
    {
        InventoryIndex = inventoryIndex;
        Gem = gem;
        Renderer.gameObject.SetActive(gem.IsValid);
        if (TryGetComponent<DraggableElement>(out var draggableElement)) draggableElement.enabled = gem.IsValid;
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
            var focus = gemSlot.GetComponentInParent<RingFocusDisplay>();
            if (this.IsInInventory)
            {
                // Inv To Slot
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerSlotInventoryGemIntoRing((byte)Game.ClientGame.PlayerIndex, 
                        InventoryIndex,
                        focus.GetEquipmentGemIndex(gemSlot)
                    ));
            }
            else
            {
                // Slot To Slot
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerSwapGemSlots((byte)Game.ClientGame.PlayerIndex, 
                        focus.GetEquipmentGemIndex(this),
                        focus.GetEquipmentGemIndex(gemSlot)
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
                        this.GetComponentInParent<RingFocusDisplay>().GetEquipmentGemIndex(this)
                    ));
            }
            else
            {
                // Invalid
                SnapBackToOrigin();
            }
        }
    }

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
}