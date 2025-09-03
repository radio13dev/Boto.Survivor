using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public class GemDisplay : MonoBehaviour, IFocusFilter, DescriptionUI.ISource
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
        Renderer.GetComponent<MeshFilter>().mesh = gem.Mesh;
        Renderer.material = gem.Material;
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
                        (byte)this.Index,
                        (byte)gemSlot.Index
                    ));
            }
            else
            {
                // Slot To Slot
                Game.ClientGame.RpcSendBuffer.Enqueue(
                    GameRpc.PlayerSwapGemSlots((byte)Game.ClientGame.PlayerIndex, 
                        (byte)this.Index,
                        (byte)gemSlot.Index
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
                        (byte)this.Index
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

    public void GetDescription(out string title, out string description, 
        out List<(string left, string oldVal, float change, string newVal)> rows, 
        out (string left, DescriptionUI.eBottomRowIcon icon, string right) bottomRow)
    {
        if (IsInSlot)
        {
            // Equipped
            title = "(Equipped)".Color(Color.gray).Size(30);
        }
        else
        {
            // Inventory
            title = "(Inventory)".Color(Color.gray).Size(30);
        }
            
        var interact = UIFocus.Interact;
        var focus = gameObject;
        StringBuilder sb = new();
        if (Gem.IsValid)
        {
            if (interact && interact != focus && interact.TryGetComponent<GemDisplay>(out var heldGem))
            {
                if (Gem.ClientId != heldGem.Gem.ClientId)
                {
                    sb.AppendLine("SWAP".Size(36).Color(new Color(0.9960785f, 0.4313726f, 0.3254902f)));
                }
                else if (focus == null)
                {
                    sb.AppendLine("UNSOCKET".Size(36).Color(new Color(0.9960785f, 0.4313726f, 0.3254902f)));
                }
            }
                
            sb.AppendLine(Gem.GetTitleString().Size(36));
            sb.AppendLine($"Size: {Gem.Size.Color(Palette.GemSize(Gem.Size))}".Size(30));
        }
        else if (interact && interact.TryGetComponent<GemDisplay>(out var heldGem))
        {
            sb.AppendLine("INSERT".Size(36).Color(new Color(0.9960785f, 0.4313726f, 0.3254902f)));
            sb.AppendLine(heldGem.Gem.GetTitleString().Size(36));
            sb.AppendLine($"Size: {heldGem.Gem.Size.Color(Palette.GemSize(Gem.Size))}".Size(30));
        }
        else
        {
            sb.AppendLine("Empty Slot".Color(new Color(0.2f,0.2f,0.2f)).Size(30));
        }
        description = sb.ToString();
        
        rows = default;
        bottomRow = default;
    }
}