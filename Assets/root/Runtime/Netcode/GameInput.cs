using System;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

public static class GameInput
{
    public enum Mode
    {
        Keyboard,
        Xbox,
        Touch
    }
    
    public static Mode CurrentMode = Mode.Keyboard;
    public static InputSystem_Actions Inputs;
    private static readonly int InputMode = Shader.PropertyToID("_InputMode");
    public static event Action OnInputModeChanged;

    public static void Init()
    {
        Inputs = new InputSystem_Actions();
        Inputs.Player.Enable();
        Inputs.UI.Enable();
    }

    public static void Dispose()
    {
        Inputs.Dispose();
    }
    
    public static void SetInputMode(Mode mode)
    {
        CurrentMode = mode;
        Shader.SetGlobalInteger(InputMode, (int)mode);
        OnInputModeChanged?.Invoke();
    }
}