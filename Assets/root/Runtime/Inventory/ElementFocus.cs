using System;
using Unity.Mathematics;
using UnityEngine;

public class ElementFocus : MonoBehaviour
{
    public static ElementFocus Instance { get; private set; }

    public Camera UICamera;
    
    public Transform Visual;
    public float Speed = 10f;
    public Vector3 FocusedScale = new Vector3(0.4f,0.4f,0.4f);
    public float FocusScaleSpeed = 10f;
    bool m_Active = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        var visualT = Visual.gameObject.transform;
        
        Vector3 pos = Vector3.zero;
        Vector3 scale = Vector3.zero;
        
        
        if (m_ForcedFocus)
        {
            pos = m_ForcedFocus.transform.position;
            if (m_ForcedFocus.gameObject.layer != CameraRegistry.UILayer)
            {
                // Convert the world position to a canvas position
                pos = Camera.main.WorldToScreenPoint(pos);
                var uiRay = UICamera.ScreenPointToRay(pos);
                pos = uiRay.origin + uiRay.direction * (UICamera.farClipPlane - UICamera.nearClipPlane)/2;
            }
            
            scale = Vector3.one;
            
            if (m_ForcedFocus.TryGetComponent<IFocusScale>(out var scaler))
                scale *= (float3)scaler.GetFocusScale();
            
            if (!m_Active)
            {
                m_Active = true;
                transform.position = pos;
            }
        }
        else if (UIFocus.Interact && (!UIFocus.Focus || !UIFocus.CheckFocus(UIFocus.Focus)))
        {
            pos = UIFocus.Interact.transform.position;
            if (UIFocus.Interact.layer != CameraRegistry.UILayer)
            {
                // Convert the world position to a canvas position
                pos = Camera.main.WorldToScreenPoint(pos);
                var uiRay = UICamera.ScreenPointToRay(pos);
                pos = uiRay.origin + uiRay.direction * (UICamera.farClipPlane - UICamera.nearClipPlane)/2;
            }
            
            scale = FocusedScale;
            
            if (UIFocus.Interact.TryGetComponent<IFocusScale>(out var scaler))
                scale *= (float3)scaler.GetFocusScale();
        }
        else if (UIFocus.Focus)
        {
            pos = UIFocus.Focus.transform.position;
            if (UIFocus.Focus.layer != CameraRegistry.UILayer)
            {
                // Convert the world position to a canvas position
                pos = Camera.main.WorldToScreenPoint(pos);
                var uiRay = UICamera.ScreenPointToRay(pos);
                pos = uiRay.origin + uiRay.direction * (UICamera.farClipPlane - UICamera.nearClipPlane)/2;
            }
            
            scale = Vector3.one;
            
            if (UIFocus.Focus.TryGetComponent<IFocusScale>(out var scaler))
                scale *= (float3)scaler.GetFocusScale();
            
            if (!m_Active)
            {
                m_Active = true;
                transform.position = pos;
            }
        }
        else
        {
            pos = transform.position;
            scale = Vector3.zero;
            
            m_Active = false;
        }
        
        transform.position = Vector3.Lerp(transform.position, pos, Time.deltaTime*Speed);
        visualT.localScale = Vector3.Lerp(visualT.localScale, scale, Time.deltaTime*FocusScaleSpeed);
    }

    Focusable m_ForcedFocus;
    public static void SetForcedFocus(Focusable mFocusable)
    {
        if (!Instance) return;
        if (Instance.m_ForcedFocus == mFocusable) return;
        if (Instance.m_ForcedFocus)
            EndForcedFocus(Instance.m_ForcedFocus);
        Instance.m_ForcedFocus = mFocusable;
    }
    public static void EndForcedFocus(Focusable mFocusable)
    {
        if (!Instance) return;
        if (Instance.m_ForcedFocus == mFocusable)
        {
            Instance.m_ForcedFocus = null;
        }
    }
}
