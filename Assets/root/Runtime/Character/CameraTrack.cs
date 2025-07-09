using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

public class CameraTrack : MonoBehaviour
{
    public float linearCameraChase;
    public float relativeCameraChase;
    public float velocityFalloff;
    public float newVelocityEffect;
    public float virtualTargetOffset;
    
    Transform _target;
    Vector3 virtualTarget;
    Vector3 recentVelocity;
    Vector3 recentPosition;

    private void Update()
    {
        if (!_target)
        {
            var entityManager = ClientServerBootstrap.ClientWorld.EntityManager;
            var query = entityManager.CreateEntityQuery(new ComponentType(typeof(GhostOwnerIsLocal)), new ComponentType(typeof(SurvivorTag)), new ComponentType(typeof(GenericPrefabProxy)));
            var transforms = query.ToComponentDataArray<GenericPrefabProxy>(Allocator.Temp);
            if (transforms.Length == 0 || !transforms[0].Spawned) return;
            _target = transforms[0].Spawned.Value.transform;
        }
        
        recentVelocity *= Time.deltaTime*velocityFalloff;
        recentVelocity += (_target.position - recentPosition)*newVelocityEffect;
        virtualTarget = _target.position + recentVelocity*virtualTargetOffset;
        recentPosition = _target.position;
        
        var actualCamTarget = (_target.position + virtualTarget)/2;
        transform.position = Vector3.MoveTowards(transform.position, actualCamTarget, Time.deltaTime*linearCameraChase);
        transform.position += (actualCamTarget - transform.position) * (Time.deltaTime * relativeCameraChase);
    }
}
