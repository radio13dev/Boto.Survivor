using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapUI : Selectable, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerMoveHandler, ISubmitHandler, ICancelHandler, HandUIController.IStateChangeListener
{
    public CameraTarget Target;
    public Transform MapCameraTransform;
    public Camera MapCamera;
    
    public Transform CursorTransform;
    public Collider MapCollider;
    public Vector2 TextureScale;
    
    public Vector2 DragRate;
    
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
    bool m_Dragging = false;
    bool m_AutoTrack = true;

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
        if (m_AutoTrack)
        {
            // Regular map update
            SetCursorPosition(Target.transform.position);
            AlignViewToCursor();
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

    public void OnPointerMove(PointerEventData eventData)
    {
        // As the mouse moves around on the surface move the cursor
        var worldPos = eventData.pointerCurrentRaycast.worldPosition;
        var screenPos = transform.InverseTransformPoint(worldPos);
        screenPos.x /= TextureScale.x;
        screenPos.y /= TextureScale.y;
        screenPos += new Vector3(0.5f, 0f, 0);
        
        Ray ray = MapCamera.ViewportPointToRay(screenPos);
        
        if (MapCollider.Raycast(ray, out var hitInfo, 10000))
        {
            CursorTransform.position = hitInfo.point;
            CursorTransform.rotation = Quaternion.LookRotation(hitInfo.normal, Vector3.up);
            CursorTransform.gameObject.SetActive(true);
        }
        else
        {
            CursorTransform.gameObject.SetActive(false);
        }
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
            
        // Drag!
        m_Dragging = true;
        m_AutoTrack = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (m_Dragging)
        {
            // Rotate the camera
            var delta = eventData.delta;
            MapCameraTransform.rotation = Quaternion.AngleAxis(delta.x*DragRate.x, MapCameraTransform.up) 
                                          * Quaternion.AngleAxis(delta.y*DragRate.y, MapCameraTransform.right)
                                          * MapCameraTransform.rotation;
        }
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        m_Dragging = false;
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
                m_AutoTrack = true;
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