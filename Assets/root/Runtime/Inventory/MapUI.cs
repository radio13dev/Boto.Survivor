using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapUI : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler
{
    public CameraTarget Target;
    public Transform MapCameraTransform;
    
    float2 m_CursorPosition;
    DateTime m_LastAdjustTime;

    private void Update()
    {
        var now = DateTime.UtcNow;
        var timeSinceAdjust = now - m_LastAdjustTime;
        if (this.currentSelectionState != SelectionState.Selected)
        {
            // Regular map update
            SetCursorPosition(Target.transform.position);
            AlignViewToCursor();
        }
        else
        {
            // Listen to inputs and move cursor
            
        }
    }

    private void SetCursorPosition(Vector3 worldPosition)
    {
        m_CursorPosition = TorusMapper.CartesianToToroidal(worldPosition);
    }

    private void AlignViewToCursor()
    {
        var worldPoint = (Vector3)TorusMapper.ToroidalToCartesian(m_CursorPosition);
        var cameraPoint = (Vector3)TorusMapper.ToroidalToCartesian(m_CursorPosition, 10);
        MapCameraTransform.position = cameraPoint;
        MapCameraTransform.rotation = Quaternion.LookRotation(worldPoint - cameraPoint, worldPoint.y >= 0 ? Vector3.up : Vector3.down);
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        // Ping at the selected position
        // TODO
    }

    public void OnSubmit(BaseEventData eventData)
    {
        // Ping at the cursor position
        // TODO
    }

    public void OnCancel(BaseEventData eventData)
    {
        HandUI.Home();
    }
}