using System;
using Unity.Mathematics;
using UnityEngine;

public class ElementFocusRequest : MonoBehaviour
{
    public Camera UICamera;
    
    public Transform Zero;
    public float OffsetDistance = 1f;
    public Transform Visual;
    public WiggleAround Wiggle;
    public float Speed = 10f;
    public Vector3 FocusedScale = new Vector3(0.4f,0.4f,0.4f);
    public float FocusScaleSpeed = 10f;
    bool m_Active = false;

    private void Update()
    {
        var visualT = Visual.gameObject.transform;
        
        Vector3 pos = Vector3.zero;
        Vector3 scale = Vector3.zero;
        
        if (UIFocusRequest.Interact && (!UIFocusRequest.Focus || !UIFocusRequest.CheckFocus(UIFocusRequest.Focus)))
        {
            pos = UIFocusRequest.Interact.transform.position;
            if (UIFocusRequest.Interact.layer != CameraRegistry.UILayer)
            {
                // Convert the world position to a canvas position
                pos = Camera.main.WorldToScreenPoint(pos);
                var uiRay = UICamera.ScreenPointToRay(pos);
                pos = uiRay.origin + uiRay.direction * (UICamera.farClipPlane - UICamera.nearClipPlane)/2;
            }
            
            scale = FocusedScale;
            
            if (UIFocusRequest.Interact.TryGetComponent<IFocusScale>(out var scaler))
                scale *= (float3)scaler.GetFocusScale();
        }
        else if (UIFocusRequest.Focus)
        {
            pos = UIFocusRequest.Focus.transform.position;
            if (UIFocusRequest.Focus.layer != CameraRegistry.UILayer)
            {
                // Convert the world position to a canvas position
                pos = Camera.main.WorldToScreenPoint(pos);
                var uiRay = UICamera.ScreenPointToRay(pos);
                pos = uiRay.origin + uiRay.direction * (UICamera.farClipPlane - UICamera.nearClipPlane)/2;
            }
            
            scale = Vector3.one;
            
            if (UIFocusRequest.Focus.TryGetComponent<IFocusScale>(out var scaler))
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
        if (m_Active)
        {
            var rad = math.length(scale)*OffsetDistance;
            var dir = (transform.position - Zero.position).normalized;
            var rot = Quaternion.LookRotation(transform.forward, Vector3.Cross(transform.forward, dir));
            Wiggle.Set(rad, rot);
        }
    }
}