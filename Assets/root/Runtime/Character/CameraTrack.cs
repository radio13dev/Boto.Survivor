using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraTrack : MonoBehaviour
    {
        public Transform CameraTarget;

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
            if (Keyboard.current.eKey.isPressed) rotOffset += Time.deltaTime*camRotSpeed;
            if (Keyboard.current.qKey.isPressed) rotOffset -= Time.deltaTime*camRotSpeed;
        
            transform.position = Vector3.MoveTowards(transform.position, CameraTarget.position, Time.deltaTime * linearCameraChase);
            transform.position += (CameraTarget.position - transform.position) * (Time.deltaTime * relativeCameraChase);

            TorusMapper.GetTorusInfo(transform.position, out _, out _, out var tangent);
            tangent = math.mul(quaternion.AxisAngle(CameraTarget.up, rotOffset), tangent);
            transform.rotation = Quaternion.LookRotation(-CameraTarget.up, tangent);
        }
    }
