using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LootPopup : Selectable
{
    public GameObject InteractionNotification;
    public GameObject HeldHighlight;
    Entity m_LastEntity;

    public void Focus(Entity nearestE)
    {
        if (m_LastEntity != nearestE)
        {
            // Update the display
        }
        
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (HandUIController.LastPressed != this)
            {
                HandUIController.LastPressed = this;
                for (int i = 0; i < FingerUI.Instances.Length; i++)
                {
                    if (!FingerUI.Instances[i].Ring)
                    {
                        FingerUI.Instances[i].Select();
                        break;
                    }
                }
            }
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (HandUIController.LastPressed == this) HandUIController.LastPressed = null;
        m_LastEntity = Entity.Null;
    }
}
