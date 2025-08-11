using System;
using UnityEngine;

public class DevToolsUI : MonoBehaviour, HandUIController.IStateChangeListener
{
    public Transform ClosedT;
    public Transform InventoryT;
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
        Transform target;
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

        Co.StartCoroutine(this, CoroutineHost.Methods.LerpSmooth(transform, target, HandUIController.k_AnimTransitionTime));
    }
}