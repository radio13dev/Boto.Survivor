using System;
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;


public class LootPopup : MonoBehaviour
{
    public void Focus(Entity nearestE)
    {
        throw new NotImplementedException();
    }
}

public class NearFloorRingPopup : MonoBehaviour
{
    public RingPopup ItemPopup;
    public LootPopup LootPopup;

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
        ItemPopup.gameObject.SetActive(false);
        LootPopup.gameObject.SetActive(false);
    }

    private void Show(Entity nearestE)
    {
        // Determine what shots
        var entityManager = m_Query.World.EntityManager;
        if (entityManager.HasComponent<RingStats>(nearestE))
        {
            ItemPopup.gameObject.SetActive(true);
            ItemPopup.Focus(nearestE);
        }
        else 
            ItemPopup.gameObject.SetActive(false);
            
        if (entityManager.HasComponent<LootGenerator2>(nearestE))
        {
            LootPopup.gameObject.SetActive(true);
            LootPopup.Focus(nearestE);
        }
        else
            LootPopup.gameObject.SetActive(false);
        
        if (entityManager.HasComponent<LocalTransform>(nearestE))
        {
            var nearestT = entityManager.GetComponentData<LocalTransform>(nearestE);
            transform.SetPositionAndRotation(nearestT.Position, nearestT.Rotation);
        }
    }
}
