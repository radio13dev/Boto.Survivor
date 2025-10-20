using System;
using TMPro;
using UnityEngine;

public class KeyIconEnableDisableEffect : MonoBehaviour
{
    private static readonly int OutlineColor = Shader.PropertyToID("_Outline_Color");
    
    public MeshRenderer Background;
    public MeshRenderer Icon;
    public TMP_Text Text;
    public Color DisabledColorMul = new Color(0.25f, 0.25f, 0.25f, 1f);
    
    Color[] Colors {get => m_Colors ??= new Color[] { Background.material.color, Background.material.GetColor(OutlineColor), Icon.material.color, Text.color }; }
    Color[] m_Colors;
    bool m_IsDisabled;

    private void Awake()
    {
        m_Colors = null;
    }

    [EditorButton]
    public void SetEnabled() => Set(true);
    
    [EditorButton]
    public void SetDisabled() => Set(false);
    
    [EditorButton]
    public void Toggle() => Set(!m_IsDisabled);
    
    [EditorButton]
    public void Set(bool v)
    {
        m_IsDisabled = !v;
        Refresh();
    }

    private void Refresh()
    {
        Background.material.color = m_IsDisabled ? DisabledColorMul*Colors[0] : Colors[0];
        Background.material.SetColor(OutlineColor, m_IsDisabled ? DisabledColorMul*Colors[1] : Colors[1]);
        Icon.material.color = m_IsDisabled ? DisabledColorMul*Colors[2] : Colors[2];
        Text.color = m_IsDisabled ? DisabledColorMul*Colors[3] : Colors[3];
        
        //if (TryGetComponent<Collider>(out var collider))
        //    collider.enabled = !m_IsDisabled;
        if (TryGetComponent<SingleActionOnClick>(out var btn))
            btn.enabled = !m_IsDisabled;
        if (TryGetComponent<SingleActionOnInputKeyPress>(out var keybtn))
            keybtn.enabled = !m_IsDisabled;
        //if (TryGetComponent<Focusable>(out var focusable))
        //    focusable.enabled = !m_IsDisabled;
        //if (TryGetComponent<FocusableRequest>(out var focusRequest))
        //    focusRequest.enabled = !m_IsDisabled;
        
    }
}
