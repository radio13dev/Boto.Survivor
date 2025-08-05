using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class RotateOverTime : MonoBehaviour
{
    [SerializeField] private AnimationCurve rotationSpeedCurve = AnimationCurve.EaseInOut(0, 30f, 1, 60f); // degrees per second
    [SerializeField] private float axisChangeSpeed = 0.5f; // how quickly the axis changes (0 = never, 1 = instant)
    [SerializeField] private float returnSpeed = 90f; // speed to rotate back to identity

    private float currentSpeedCurveValue;
    private Vector3 currentAxis;
    private Vector3 targetAxis;
    private Quaternion snapRot;
    private bool isSnapping;
    private float minSnapTime;

    private void Awake()
    {
        // Start with a random rotation and axis
        transform.rotation = Random.rotation;
        currentAxis = Random.onUnitSphere;
        targetAxis = Random.onUnitSphere;
    }

    void Update()
    {
        if (isSnapping || minSnapTime > 0)
        {
            // Smoothly rotate back to the identity rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, snapRot, Time.deltaTime * returnSpeed);
            minSnapTime -= Time.deltaTime;
        }
        else
        {
            // Smoothly interpolate the axis
            currentAxis = Vector3.Slerp(currentAxis, targetAxis, axisChangeSpeed * Time.deltaTime);
            currentAxis.Normalize(); // just to be safe

            // Rotate around the current axis
            if (currentSpeedCurveValue > 0)
                currentSpeedCurveValue = Mathf.Max(0, currentSpeedCurveValue - Time.deltaTime);
            var rotationSpeed = rotationSpeedCurve.Evaluate(currentSpeedCurveValue);
            transform.Rotate(currentAxis, rotationSpeed * Time.deltaTime, Space.World);

            // Occasionally pick a new target axis
            if (Vector3.Angle(currentAxis, targetAxis) < 5f)
            {
                targetAxis = Random.onUnitSphere;
            }
        }
    }
    
    public void SetSnap(Quaternion? snapRotation)
    {
        isSnapping = snapRotation.HasValue;
        if (snapRotation.HasValue)
        {
            this.snapRot = snapRotation.Value;
            this.minSnapTime = 1f;
        }
    }
    
    [EditorButton]
    public void Spin()
    {
        currentAxis = targetAxis = transform.right;
        currentSpeedCurveValue = rotationSpeedCurve.keys[^1].time;
    }
}