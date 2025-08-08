using UnityEngine;
using UnityEngine.EventSystems;

public class RingFocusVirtualGem : MonoBehaviour
{
    public GemDisplay GemDisplay;
    GemDisplay m_Parent;

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
    
    private void OnDraggingEnd(PointerEventData obj)
    {
        m_Parent.Renderer.gameObject.SetActive(!m_Parent.IsDragEndValid() && m_Parent.Gem.IsValid);
        gameObject.SetActive(false);
    }

    public void BeginDrag(GemDisplay parent, PointerEventData eventData)
    {
        m_Parent = parent;
        m_Parent.Renderer.gameObject.SetActive(false);
        
        GemDisplay.UpdateGem(parent.Index, parent.Gem);
        transform.position = transform.position;
        gameObject.SetActive(true);
            
        eventData.pointerDrag = gameObject;
        foreach (var draggable in eventData.pointerDrag.GetComponentsInChildren<IBeginDragHandler>())
            draggable.OnBeginDrag(eventData);
    }
}
