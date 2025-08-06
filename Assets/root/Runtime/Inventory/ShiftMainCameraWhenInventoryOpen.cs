using System;
using UnityEngine;

public class ShiftMainCameraWhenInventoryOpen : MonoBehaviour, HandUIController.IStateChangeListener
{
    public Vector3 InventoryOffset;
    Vector3 m_defaultOffset;

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
                transform.localPosition = m_defaultOffset + InventoryOffset;
                break;
            default:
                // Transition to 'default' offset
                transform.localPosition = m_defaultOffset;
                break;
        }
        
        
    }
}
