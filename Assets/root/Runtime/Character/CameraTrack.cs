using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class CameraTrack : MonoBehaviour
{
    public float linearCameraChase;
    public float relativeCameraChase;
    public float velocityFalloff;
    public float newVelocityEffect;
    public float virtualTargetOffset;

    float3 virtualTarget;
    float3 recentVelocity;
    float3 recentPosition;
    
    EntityQuery m_Query;
    
    bool setupComplete = false;

    private void Update()
    {
        if (!setupComplete)
        {
            if (Game.PresentationGame == null)
                return;

            var entityManager = Game.PresentationGame.World.EntityManager;
            m_Query = entityManager.CreateEntityQuery(new ComponentType(typeof(PlayerControlled)), new ComponentType(typeof(SurvivorTag)),
                new ComponentType(typeof(LocalTransform)));
            setupComplete = true;
        }
        
        var players = m_Query.ToComponentDataArray<PlayerControlled>(Allocator.Temp);
        var transforms = m_Query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].Index == Game.PresentationGame.PlayerIndex)
            {
                var target = transforms[0];
                recentVelocity *= Time.deltaTime * velocityFalloff;
                recentVelocity += (target.Position - recentPosition) * newVelocityEffect;
                virtualTarget = target.Position + recentVelocity * virtualTargetOffset;
                recentPosition = target.Position;

                Vector3 actualCamTarget = (target.Position + virtualTarget) / 2;
                transform.position = Vector3.MoveTowards(transform.position, actualCamTarget, Time.deltaTime * linearCameraChase);
                transform.position += (actualCamTarget - transform.position) * (Time.deltaTime * relativeCameraChase);
            
                transform.rotation = target.Rotation;
                break;
            }
        }
    }
}