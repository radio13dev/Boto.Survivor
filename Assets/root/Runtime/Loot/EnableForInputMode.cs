using System;
using UnityEngine;

public class EnableForInputMode : MonoBehaviour
{
    public GameInput.Mode Mode;
    public GameObject Target;

    private void OnEnable()
    {
        GameInput.OnInputModeChanged += OnInputModeChanged;
        OnInputModeChanged();
    }

    private void OnDisable()
    {
        GameInput.OnInputModeChanged -= OnInputModeChanged;
    }

    private void OnInputModeChanged()
    {
        Target.SetActive(GameInput.CurrentMode == Mode);
    }
}
