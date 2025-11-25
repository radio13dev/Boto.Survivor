using System;
using System.Collections.Generic;
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
    
    [EditorButton]
    public void UpdateInputKeyIcons()
    {
        _inputActions = new InputSystem_Actions();
        
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
/// A matches a player input "action" with the associated key / button material
/// </summary>
[Serializable]
public class ActionKeyIcon
{
    public Material Material;
    public InputActionReference ActionRef;
}
