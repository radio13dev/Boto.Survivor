using System;
using UnityEngine;

public class TransformToUISpace : MonoBehaviour
{
    int m_ZeroLayer;
    Vector3 m_ZeroScale;
    private void OnEnable()
    {
        m_ZeroScale = transform.localScale;
        m_ZeroLayer = transform.gameObject.layer;
        foreach (Transform child in transform)
            child.gameObject.layer = CameraRegistry.UILayer;
        Update();
    }

    void Update()
    {
        Camera mainCam = CameraRegistry.Main;
        Camera uiCam = CameraRegistry.UI;
        
        var finalPos = transform.parent.position;
        finalPos = mainCam.WorldToScreenPoint(finalPos);
        var uiRay = uiCam.ScreenPointToRay(finalPos);
        finalPos = uiRay.origin + uiRay.direction * (uiCam.farClipPlane - uiCam.nearClipPlane)/2;
        transform.position = finalPos;
        transform.localScale = m_ZeroScale*30;
        transform.LookAt(uiCam.transform);
    }
    
    private void OnDisable()
    {
        if (transform)
        {
            transform.SetLocalPositionAndRotation(default, Quaternion.identity);
            transform.localScale = m_ZeroScale;
            foreach (Transform child in transform)
                child.gameObject.layer = m_ZeroLayer;
        }
    }
}