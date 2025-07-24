using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FingerUI : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler, HandUIController.IStateChangeListener
{
    public RingUIElement Ring;
    public GameObject FingerHighlight;

    public static RingUIElement Held;

    protected override void Awake()
    {
        base.Awake();
        OnDeselect(default);
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        HandUIController.Attach(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        HandUIController.Detach(this);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        HandUIController.SetState(HandUIController.State.Inventory);
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
        else HandUIController.SetState(HandUIController.State.Closed);
    }

    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        if (newState == HandUIController.State.Closed) this.Deselect();
    }
}