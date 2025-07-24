using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class HandUIIdle : Selectable, ICancelHandler, HandUIController.IStateChangeListener
{
    public Transform ClosedT;
    public Transform NeatralT;
    public Transform InventoryT;
    public Transform MapT;    
    ExclusiveCoroutine Co;

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

    public void OnCancel(BaseEventData eventData)
    {
        HandUIController.SetState(HandUIController.State.Closed);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        HandUIController.SetState(HandUIController.State.Neutral);
    }

    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        Transform target;
        switch (newState)
        {
            case HandUIController.State.Closed:
                target = ClosedT;
                break;
            case HandUIController.State.Inventory:
                target = InventoryT;
                break;
            case HandUIController.State.Map:
                target = MapT;
                break;
            case HandUIController.State.Neutral:
            default:
                target = NeatralT;
                this.Select();
                break;
        }

        Co.StartCoroutine(this, CoroutineHost.Methods.Lerp(transform, target, HandUIController.k_AnimTransitionTime));
    }
}