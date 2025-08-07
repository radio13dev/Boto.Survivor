using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Redirects dragging to a 'virtual' gem
/// </summary>
public class RingFocusGemSlot : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler
{
    public RingFocusVirtualGem DraggableGem;

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (TryGetComponent<GemDisplay>(out var myGem) && myGem.Gem.IsValid)
        {
            DraggableGem.BeginDrag(myGem, eventData);
        }
        else
        {
            DraggableGem.gameObject.SetActive(false);
            eventData.dragging = false;
        }
    }
}
