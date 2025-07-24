using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapUI : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler, HandUIController.IStateChangeListener
{
    public CameraTarget Target;
    public Transform MapCameraTransform;
    public Camera MapCamera;
    
    public float PositionChaseRate = 10.0f;
    public float RotationChaseRate = 100.0f;
    
    public float MaxInnerAngle = 2.4f;
    public float SwapInnerAngle = 2.9f;
    public float InnerAngleTransition = 0.1f;
    
    public Transform ClosedT;
    public Transform NeatralT;
    public Transform InventoryT;
    public Transform MapT;
    ExclusiveCoroutine Co;
    
    float2 m_CursorPosition;
    DateTime m_LastAdjustTime;

    protected override void Awake()
    {
        base.Awake();
        OnDeselect(default);
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        HandUIController.Attach(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        HandUIController.Detach(this);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        HandUIController.SetState(HandUIController.State.Map);
    }

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

    bool m_Alignment;
    private void AlignViewToCursor()
    {
        var alignToroidal = m_CursorPosition;
        if (alignToroidal.y > MaxInnerAngle || alignToroidal.y < -MaxInnerAngle)
        {
            if (m_Alignment && alignToroidal.y < 0 && alignToroidal.y > -SwapInnerAngle)
                m_Alignment = false; // Swap to negative alignment
            if (!m_Alignment && alignToroidal.y > 0 && alignToroidal.y < SwapInnerAngle)
                m_Alignment = true; // Swap to positive alignment
            
            if (m_Alignment && alignToroidal.y > 0)
            {
                alignToroidal.y = Mathf.Lerp(alignToroidal.y, MaxInnerAngle + InnerAngleTransition, (alignToroidal.y - MaxInnerAngle)/InnerAngleTransition);
            }
            else if (!m_Alignment && alignToroidal.y < 0)
            {
                alignToroidal.y = Mathf.Lerp(alignToroidal.y, -(MaxInnerAngle + InnerAngleTransition), (-alignToroidal.y - MaxInnerAngle)/InnerAngleTransition);
            }
            else
            {
                alignToroidal.y = m_Alignment ? (MaxInnerAngle+InnerAngleTransition) : -(MaxInnerAngle+InnerAngleTransition);
            }
            
        }
        else
            m_Alignment = alignToroidal.y > 0;
        
        var worldPoint = (Vector3)TorusMapper.ToroidalToCartesian(alignToroidal);
        var cameraPoint = (Vector3)TorusMapper.ToroidalToCartesian(alignToroidal, 10);
        
        var forwardPointToroidal = alignToroidal + new float2(0, -0.1f);
        var forwardPoint = (Vector3)TorusMapper.ToroidalToCartesian(forwardPointToroidal);
        
        MapCameraTransform.position = Vector3.zero;//Vector3.MoveTowards(MapCameraTransform.position, cameraPoint, Time.deltaTime * PositionChaseRate);
        MapCameraTransform.rotation = Quaternion.RotateTowards(MapCameraTransform.rotation, Quaternion.LookRotation(worldPoint - cameraPoint, forwardPoint - worldPoint), Time.deltaTime * RotationChaseRate);
        Debug.DrawLine(cameraPoint, worldPoint);
        Debug.DrawLine(worldPoint, forwardPoint);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ping at the selected position
        Debug.Log(eventData.pointerCurrentRaycast.gameObject);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        // Ping at the cursor position
        // TODO
    }

    public void OnCancel(BaseEventData eventData)
    {
        HandUIController.SetState(HandUIController.State.Closed);
    }

    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        Transform target;
        switch (newState)
        {
            case HandUIController.State.Closed:
                target = ClosedT;
                this.Deselect();
                break;
            case HandUIController.State.Inventory:
                target = InventoryT;
                break;
            case HandUIController.State.Map:
                target = MapT;
                this.Select();
                break;
            case HandUIController.State.Neutral:
            default:
                target = NeatralT;
                break;
        }

        Co.StartCoroutine(this, CoroutineHost.Methods.Lerp(transform, target, HandUIController.k_AnimTransitionTime));
    }
}