using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class TorusCollider : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    static TorusCollider s_Instance;
    
    public static bool IsMouseOver { get; private set; }
    public static PointerEventData LastRaycast { get; private set; }
    
    private void Awake()
    {
        s_Instance = this;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsMouseOver = true;
    }
    
    public void OnPointerMove(PointerEventData eventData)
    {
        LastRaycast = eventData;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsMouseOver = false;
    }
}
