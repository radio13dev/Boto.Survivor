using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class CameraTarget : MonoBehaviour
{
    bool setupComplete = false;

    public Entity PlayerE => m_PlayerE;
    Entity m_PlayerE;
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
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].Index == Game.PresentationGame.PlayerIndex)
            {
                var entities = m_Query.ToEntityArray(Allocator.Temp);
                m_PlayerE = entities[i];
                var target = Game.PresentationGame.World.EntityManager.GetComponentData<LocalTransform>(m_PlayerE);
                transform.SetPositionAndRotation(target.Position, target.Rotation);
                
                entities.Dispose();
                players.Dispose();
                
                return;
            }
        }
        players.Dispose();
    }
}