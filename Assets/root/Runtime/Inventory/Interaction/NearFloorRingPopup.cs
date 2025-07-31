using Unity.Entities;
using Unity.Transforms;
using UnityEngine;


public class NearFloorRingPopup : MonoBehaviour
{
    public RingPopup ItemPopup;
    public LootPopup LootPopup;

    void Update()
    {
        if (Game.ClientGame == null) return;
        
        var nearest = Game.ClientGame.GetSingleton<NearestInteractable>();
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
        if (Game.ClientGame.World.EntityManager.HasComponent<RingStats>(nearestE))
        {
            ItemPopup.gameObject.SetActive(true);
            ItemPopup.Focus(Game.ClientGame.World,nearestE);
        }
        else 
            ItemPopup.gameObject.SetActive(false);
            
        if (Game.ClientGame.World.EntityManager.HasComponent<LootGenerator2>(nearestE))
        {
            LootPopup.gameObject.SetActive(true);
            LootPopup.Focus(Game.ClientGame.World, nearestE);
        }
        else
            LootPopup.gameObject.SetActive(false);
        
        if (Game.ClientGame.World.EntityManager.HasComponent<LocalTransform>(nearestE))
        {
            var nearestT = Game.ClientGame.GetComponent<LocalTransform>(nearestE);
            transform.SetPositionAndRotation(nearestT.Position, nearestT.Rotation);
            transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
        }
    }
}
