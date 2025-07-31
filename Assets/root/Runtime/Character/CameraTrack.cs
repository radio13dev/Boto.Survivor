using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraTrack : MonoBehaviour
{
    CameraTarget m_CameraTarget;

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
        if (!m_CameraTarget)
        {
            if (Game.ClientGame == null) return;
        
            var targets = Object.FindObjectsByType<CameraTarget>(FindObjectsSortMode.None);
            if (targets.Length == 0) return;
            
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i].PlayerIndex == Game.ClientGame.PlayerIndex)
                {
                    m_CameraTarget = targets[i];
                    break;
                }
            }
            
            if (!m_CameraTarget) return;
        }
    
        if (Keyboard.current.eKey.isPressed) rotOffset += Time.deltaTime * camRotSpeed;
        if (Keyboard.current.qKey.isPressed) rotOffset -= Time.deltaTime * camRotSpeed;

        var cameraTarget = m_CameraTarget.transform;
        transform.position = Vector3.MoveTowards(transform.position, cameraTarget.position, Time.deltaTime * linearCameraChase);
        transform.position += (cameraTarget.position - transform.position) * (Time.deltaTime * relativeCameraChase);

        TorusMapper.GetTorusInfo(transform.position, out _, out _, out var tangent);
        tangent = math.mul(quaternion.AxisAngle(cameraTarget.up, rotOffset), tangent);
        transform.rotation = Quaternion.LookRotation(-cameraTarget.up, tangent);
    }
}