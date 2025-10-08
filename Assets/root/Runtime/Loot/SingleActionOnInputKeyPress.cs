using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class SingleActionOnInputKeyPress : MonoBehaviour
{
    public UnityEvent OnKeyPress;
    public KeyCode Key;
    
    public enum KeyCode
    {
        Drop,
        Trash,
        Submit
    }

    private void Update()
    {
        if (GameInput.Inputs?.Player == null) return;
        
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
