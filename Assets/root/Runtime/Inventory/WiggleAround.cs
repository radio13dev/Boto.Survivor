using Unity.Mathematics;
using UnityEngine;

public class WiggleAround : MonoBehaviour
{
    public float WiggleRadiusMin = 30;
    public float WiggleRadiusMax = 30;
    public float WiggleSpeed = 1f;
    
    public float RollLimitDegrees = 5f;
    public float YawLimitDegrees = 10f;
    public float RollYawSpeed = 1f;
    
    public float CorrectionSpeed = 10f;
    
    public Quaternion Zero = Quaternion.identity;

    public void Set(float rad, Quaternion rot)
    {
        WiggleRadiusMin = rad - 5f;
        WiggleRadiusMax = rad + 5f;
        Zero = rot;
    }

    private void Update()
    {
        // Smoothly bob our transform in and out of the wiggle radius min/max (with some slight randomization)
        // while also randomly rolling and yawing. Should give the object a bit of life.
        var t = (math.sin(Time.time*WiggleSpeed)+1)/2;
        var radius = math.lerp(WiggleRadiusMin, WiggleRadiusMax, t);
        transform.localPosition = -transform.right*radius;
        var t2 = (math.sin(Time.time*RollYawSpeed)+1)/2;
        var roll = math.lerp(-RollLimitDegrees, RollLimitDegrees, t2);
        var yaw = math.lerp(-YawLimitDegrees, YawLimitDegrees, 1-t2);
        transform.localRotation = Zero * Quaternion.Euler(0, yaw, roll);
    }
}