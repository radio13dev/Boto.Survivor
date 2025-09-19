using UnityEngine;

public class RingTransform : MonoBehaviour, HandUIController.IStateChangeListener
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

    [EditorButton]
    public void GotoClosed()
    {
        foreach (var o in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (o is not HandUIController.IStateChangeListener change)
                continue;
            change.OnStateChanged(HandUIController.State.Inventory, HandUIController.State.Closed);
        }
    }

    [EditorButton]
    public void GotoInventory()
    {
        foreach (var o in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (o is not HandUIController.IStateChangeListener change)
                continue;
            change.OnStateChanged(HandUIController.State.Closed, HandUIController.State.Inventory);
        }
    }
}