using Unity.Mathematics;
using UnityEngine;

public class CameraTrack : MonoBehaviour
    {
        public Transform CameraTarget;

        public float linearCameraChase;
        public float relativeCameraChase;
        public float velocityFalloff;
        public float newVelocityEffect;
        public float virtualTargetOffset;

        float3 virtualTarget;
        float3 recentVelocity;
        float3 recentPosition;

        private void Update()
        {
            var target = CameraTarget.transform;
            var targetPosition = (float3)target.position;
            
            recentVelocity *= Time.deltaTime * velocityFalloff;
            recentVelocity += (targetPosition - recentPosition) * newVelocityEffect;
            virtualTarget = targetPosition + recentVelocity * virtualTargetOffset;
            recentPosition = targetPosition;

            Vector3 actualCamTarget = (targetPosition + virtualTarget) / 2;
            transform.position = Vector3.MoveTowards(transform.position, actualCamTarget, Time.deltaTime * linearCameraChase);
            transform.position += (actualCamTarget - transform.position) * (Time.deltaTime * relativeCameraChase);

            transform.rotation = target.rotation;
        }
    }
