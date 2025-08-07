using System;
using UnityEngine;

public class ShiftMainCameraWhenInventoryOpen : MonoBehaviour, HandUIController.IStateChangeListener
{
    public Vector3 InventoryOffset;
    Vector3 m_defaultOffset;
    ExclusiveCoroutine m_Co;

    private void Awake()
    {
        m_defaultOffset = transform.localPosition;
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
        switch (newState)
        {
            case HandUIController.State.Inventory:
                // Transition to 'inventory' offset
                m_Co.StartCoroutine(this, CoroutineHost.Methods.Lerp(transform, m_defaultOffset + InventoryOffset, 0.2f, true, true));
                break;
            default:
                // Transition to 'default' offset
                m_Co.StartCoroutine(this, CoroutineHost.Methods.Lerp(transform, m_defaultOffset, 0.2f, true, true));
                break;
        }
        
        
    }
}
