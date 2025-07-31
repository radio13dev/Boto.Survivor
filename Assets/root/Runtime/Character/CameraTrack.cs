using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraTrack : MonoBehaviour
{
    public float linearCameraChase;
    public float relativeCameraChase;
    public float velocityFalloff;
    public float newVelocityEffect;
    public float virtualTargetOffset;
    public float camRotSpeed = 2f;

    float3 virtualTarget;
    float3 recentVelocity;
    float3 recentPosition;
    float rotOffset = 0;

    private void Update()
    {
        if (!CameraTarget.MainTarget) return;
    
        if (Keyboard.current.eKey.isPressed) rotOffset += Time.deltaTime * camRotSpeed;
        if (Keyboard.current.qKey.isPressed) rotOffset -= Time.deltaTime * camRotSpeed;

        var cameraTarget = CameraTarget.MainTarget.transform;
        transform.position = Vector3.MoveTowards(transform.position, cameraTarget.position, Time.deltaTime * linearCameraChase);
        transform.position += (cameraTarget.position - transform.position) * (Time.deltaTime * relativeCameraChase);

        TorusMapper.GetTorusInfo(transform.position, out _, out _, out var tangent);
        tangent = math.mul(quaternion.AxisAngle(cameraTarget.up, rotOffset), tangent);
        transform.rotation = Quaternion.LookRotation(-cameraTarget.up, tangent);
    }
}