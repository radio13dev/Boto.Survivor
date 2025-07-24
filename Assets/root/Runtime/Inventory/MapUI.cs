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

    private void AlignViewToCursor()
    {
        var worldPoint = (Vector3)TorusMapper.ToroidalToCartesian(m_CursorPosition);
        var cameraPoint = (Vector3)TorusMapper.ToroidalToCartesian(m_CursorPosition, 10);
        
        var forwardPointToroidal = m_CursorPosition + new float2(0, 0.1f);
        var forwardPoint = (Vector3)TorusMapper.ToroidalToCartesian(forwardPointToroidal);
        
        MapCameraTransform.position = cameraPoint;
        MapCameraTransform.rotation = Quaternion.LookRotation(worldPoint - cameraPoint, forwardPoint - worldPoint);
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