using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FingerUI : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler
{
    public RingUIElement Ring;
    public GameObject FingerHighlight;

    public static RingUIElement Held;

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        if (FingerHighlight) FingerHighlight.gameObject.SetActive(true);
        if (Ring) Ring.Description.gameObject.SetActive(true);
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        if (FingerHighlight) FingerHighlight.gameObject.SetActive(false);
        if (Ring && Ring != Held) Ring.Description.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // If something's picked up, swap it in
        if (Held)
        {
            // Swap Held with this Ring
            (Held, Ring) = (Ring, Held);
        }
        // ... otherwise, pick this up for movement
        else if (Ring)
        {
            Held = Ring;
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        // If something's picked up, swap it in
        if (Held)
        {
            // Swap Held with this Ring
            (Held, Ring) = (Ring, Held);
        }
        // ... otherwise, pick this up for movement
        else if (Ring)
        {
            Held = Ring;
        }
    }

    public void OnCancel(BaseEventData eventData)
    {
        // If something's picked up, drop it
        if (Held) Held = null;
        // ... otherwise, close UI
        else HandUI.Home();
    }
}