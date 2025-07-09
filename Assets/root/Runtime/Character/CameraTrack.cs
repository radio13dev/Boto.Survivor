using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

public class CameraTrack : MonoBehaviour
{
    Transform _target;

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
        
        transform.position = _target.position;
    }
}
