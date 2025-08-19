using System;
using UnityEngine;

public class ElementFocus : MonoBehaviour
{
    public Camera UICamera;
    
    public Transform Visual;
    public float Speed = 10f;
    public Vector3 FocusedScale = new Vector3(0.4f,0.4f,0.4f);
    public float FocusScaleSpeed = 10f;
    bool m_Active = false;

    private void Update()
    {
        var visualT = Visual.gameObject.transform;
        
        Vector3 pos = Vector3.zero;
        Vector3 scale = Vector3.zero;
        
        
        if (UIFocus.Interact && (!UIFocus.Focus || !UIFocus.CheckFocus(UIFocus.Focus)))
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
}
