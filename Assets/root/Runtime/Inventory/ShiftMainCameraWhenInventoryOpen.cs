using System;
using UnityEngine;

public class ShiftMainCameraWhenInventoryOpen : MonoBehaviour, HandUIController.IStateChangeListener
{
    public Vector3 DefaultOffset;
    public Vector3 InventoryOffset;
    ExclusiveCoroutine m_Co;

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
        switch (newState)
        {
            case HandUIController.State.Inventory:
                // Transition to 'inventory' offset
                m_Co.StartCoroutine(this, CoroutineHost.Methods.Lerp(transform, DefaultOffset + InventoryOffset, 0.2f, true, true));
                break;
            default:
                // Transition to 'default' offset
                m_Co.StartCoroutine(this, CoroutineHost.Methods.Lerp(transform, DefaultOffset, 0.2f, true, true));
                break;
        }
        
        
    }
}
