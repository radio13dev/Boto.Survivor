using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class SingleActionOnInputKeyPress : MonoBehaviour
{
    public UnityEvent OnKeyPress;
    public KeyCode Key;
    public HandUIController.State StateRestriction = HandUIController.State.Any;
    
    public enum KeyCode
    {
        Drop,
        Trash,
        Submit
    }

    private void Update()
    {
        if (GameInput.Inputs?.Player == null) return;
        if (StateRestriction != HandUIController.State.Any && StateRestriction != HandUIController.GetState()) return;
        
        switch (Key)
        {
            case KeyCode.Drop:
                if (GameInput.Inputs.UI.Drop.WasPressedThisFrame())
                    OnKeyPress?.Invoke();
                break;
            case KeyCode.Trash:
                if (GameInput.Inputs.UI.Trash.WasPressedThisFrame())
                    OnKeyPress?.Invoke();
                break;
            case KeyCode.Submit:
                if (GameInput.Inputs.UI.Submit.WasPressedThisFrame())
                    OnKeyPress?.Invoke();
                break;
        }
    }
}
