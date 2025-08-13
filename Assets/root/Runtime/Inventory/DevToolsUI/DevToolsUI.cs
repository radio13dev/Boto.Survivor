using System;
using UnityEngine;

public class DevToolsUI : MonoBehaviour, HandUIController.IStateChangeListener
{
    public TransitionPoint ClosedT;
    public TransitionPoint InventoryT;
    ExclusiveCoroutine Co;

    private void Awake()
    {
        gameObject.SetActive(false); // Disable on launch
    }

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

        Co.StartCoroutine(this, target.Lerp((RectTransform)transform, HandUIController.k_AnimTransitionTime, false));
    }
}