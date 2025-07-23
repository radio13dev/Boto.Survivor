using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class CameraTarget : MonoBehaviour
{
    bool setupComplete = false;

    EntityQuery m_Query;

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
                var target = transforms[i];
                transform.SetPositionAndRotation(target.Position, target.Rotation);
                return;
            }
        }
    }
}