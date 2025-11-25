using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputActionIconRemapper : MonoBehaviour
{
    public List<ActionKeyIcon> ActionKeyIcons;
    public KeyboardIconDatabase Keyboard;
    public KeyboardIconDatabase KeyboardOutline;
    public GamepadIconDatabase Xbox;
    public GamepadIconDatabase XboxOutline;
    
    private static readonly int KeyboardProperty = Shader.PropertyToID("_Keyboard");
    private static readonly int KeyboardOutlineProperty = Shader.PropertyToID("_KeyboardOutline");
    private static readonly int XboxProperty = Shader.PropertyToID("_Xbox");
    private static readonly int XboxOutlineProperty = Shader.PropertyToID("_XboxOutline");
    private InputSystem_Actions _inputActions;
    private InputDevice _lastDevice;

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => GameInput.Inputs != null);
        _inputActions = GameInput.Inputs;
        UpdateInputKeyMaterials();
        
        _inputActions.UI.Get().actionTriggered += OnInputActionTriggered;
        _inputActions.Player.Get().actionTriggered += OnInputActionTriggered;
    }

    private void OnDestroy()
    {
        if (_inputActions == null) { return; }
        _inputActions.UI.Get().actionTriggered -= OnInputActionTriggered;
        _inputActions.Player.Get().actionTriggered -= OnInputActionTriggered;
    }
    
    private void OnInputActionTriggered(InputAction.CallbackContext context)
    {
        if (context.canceled) { return; }
        var currentDevice = context.control.device;
        if (currentDevice == null || currentDevice == _lastDevice) { return; }
        UpdateInputDevice(currentDevice);
    }
    
    private void UpdateInputDevice(InputDevice newDevice)
    {
        _lastDevice = newDevice;

        switch (newDevice)
        {
            // Keyboard or mouse
            case UnityEngine.InputSystem.Keyboard or Mouse
                when GameInput.CurrentMode != GameInput.Mode.Keyboard:
                GameInput.SetInputMode(GameInput.Mode.Keyboard);
                break;
            // Any gamepad
            case Gamepad
                when GameInput.CurrentMode != GameInput.Mode.Xbox:
                GameInput.SetInputMode(GameInput.Mode.Xbox);
                break;
            // Touch controls
            case Touchscreen
                when GameInput.CurrentMode != GameInput.Mode.Touch:
                GameInput.SetInputMode(GameInput.Mode.Touch);
                break;
        }
    }

    [EditorButton]
    private void UpdateInputKeyMaterials()
    {
        #if UNITY_EDITOR
        // Only create a new input action if not in play mode
        _inputActions = Application.isPlaying ? _inputActions : new InputSystem_Actions();
        #endif

        if (_inputActions == null) { return; }
        
        foreach (var iconBinding in ActionKeyIcons)
        {
            if (iconBinding.Material == null || iconBinding.ActionRef == null) { continue; }
            
            // Find the action in the input system 
            InputAction action = _inputActions.asset.FindAction(iconBinding.ActionRef.action.id);

            if (action == null) { continue; }

            // Check what keys are associated with action
            foreach (InputBinding keyBinding in action.bindings)
            {
                if (keyBinding.isComposite) { continue; }
                
                if (keyBinding.effectivePath.StartsWith("<Keyboard>"))
                {
                    Texture2D texture = Keyboard.GetSprite(keyBinding.effectivePath).texture;
                    Texture2D textureOutline = KeyboardOutline.GetSprite(keyBinding.effectivePath).texture;

                    // Only check texture, allow for null textureOutline
                    if (!texture) { continue; }
                    
                    iconBinding.Material.SetTexture(KeyboardProperty, texture);
                    iconBinding.Material.SetTexture(KeyboardOutlineProperty, textureOutline);
                }
                
                else if (keyBinding.effectivePath.StartsWith("<Gamepad>"))
                {
                    Texture2D texture = Xbox.GetSprite(keyBinding.effectivePath).texture;
                    Texture2D textureOutline = XboxOutline.GetSprite(keyBinding.effectivePath).texture;

                    // Only check texture, allow for null textureOutline
                    if (!texture) { continue; }
                    
                    iconBinding.Material.SetTexture(XboxProperty, texture);
                    iconBinding.Material.SetTexture(XboxOutlineProperty, textureOutline);
                }
            }
        }
    }
}

/// <summary>
/// Matches a player input "action" with the associated key / button material
/// </summary>
[Serializable]
public class ActionKeyIcon
{
    public Material Material;
    public InputActionReference ActionRef;
}
