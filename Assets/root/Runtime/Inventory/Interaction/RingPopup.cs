using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RingPopup : Selectable, IPointerClickHandler
{
    public GameObject InteractionNotification;
    public GameObject HeldHighlight;
    Entity m_LastEntity;
    public float3 ItemPosition => transform.parent.position;

    public void Focus(World world, Entity nearestE)
    {
        if (m_LastEntity != nearestE)
        {
            // Update the display
        }
        
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            OnPointerClick(default);
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (HandUIController.LastPressed != this)
        {
            HandUIController.LastPressed = this;
            for (int i = 0; i < FingerUI.Instances.Length; i++)
            {
                if (!FingerUI.Instances[i].Ring.isActiveAndEnabled)
                {
                    FingerUI.Instances[i].Select();
                    return;
                }
            }
            FingerUI.Instances[0].Select();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (HandUIController.LastPressed == this)
        {
            HandUIController.LastPressed = null;
            HandUIController.SetState(HandUIController.State.Closed);
        }
        m_LastEntity = Entity.Null;
    }
}
