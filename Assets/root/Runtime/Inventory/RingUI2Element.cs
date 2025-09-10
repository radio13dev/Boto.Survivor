using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

public class RingUI2Element : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RotateOverTime rotateTarget;
    [SerializeField] private Quaternion snapRotation;

    public void OnPointerEnter(PointerEventData eventData)
    {
        rotateTarget.SetSnap(snapRotation);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        rotateTarget.SetSnap(null);
    }
}