using System;
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class NearFloorRingPopup : MonoBehaviour
{
    public GameObject ItemPopup;
    public GameObject LootPopup;

    (World World, EntityQuery Query) m_Query;
    
    void Update()
    {
        if (Game.PresentationGame == null) return;
        
        if (m_Query.World != Game.PresentationGame.World)
        {
            m_Query = (Game.PresentationGame.World, Game.PresentationGame.World.EntityManager.CreateEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] { typeof(NearestInteractable) },
            }));
        }
        
        var nearest = m_Query.Query.GetSingleton<NearestInteractable>();
        if (nearest.Value == Entity.Null)
        {
            Hide();
        }
        else
        {
            Show(nearest.Value);
        }
    }

    private void Hide()
    {
        ItemPopup.SetActive(false);
        LootPopup.SetActive(false);
    }

    private void Show(Entity nearestE)
    {
        // Lazy stuff
        Hide();
        
        // Determine what shots
        var entityManager = m_Query.World.EntityManager;
        if (entityManager.HasComponent<RingStats>(nearestE))
        {
            ItemPopup.SetActive(true);
        }
        if (entityManager.HasComponent<LootGenerator2>(nearestE))
        {
            LootPopup.SetActive(true);
        }
        if (entityManager.HasComponent<LocalTransform>(nearestE))
        {
            var nearestT = entityManager.GetComponentData<LocalTransform>(nearestE);
            transform.SetPositionAndRotation(nearestT.Position, nearestT.Rotation);
        }
    }
}
