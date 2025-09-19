using UnityEngine;

public class RingUI : MonoBehaviour, HandUIController.IStateChangeListener
{
    public TransitionPoint InventoryT;
    public TransitionPoint ClosedT;
    ExclusiveCoroutine Co;
    
    private void OnEnable()
    {
        HandUIController.Attach(this);
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);
    }
    
    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        TransitionPoint target;
        switch (newState)
        {
            case HandUIController.State.Inventory:
                target = InventoryT;
                break;
            default:
            case HandUIController.State.Closed:
                target = ClosedT;
                break;
        }

        Co.StartCoroutine(this, target.Lerp((RectTransform)transform, HandUIController.k_AnimTransitionTime));
    }
}