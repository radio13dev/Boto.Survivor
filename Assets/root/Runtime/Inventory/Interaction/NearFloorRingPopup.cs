using BovineLabs.Core.Extensions;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;


public class NearFloorRingPopup : MonoBehaviour
{
    public RingPopup ItemPopup;

    void Update()
    {
        if (Game.ClientGame == null) return;

        var nearest = Game.ClientGame.World.EntityManager.GetSingleton<NearestInteractable>();
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
        if (ItemPopup) ItemPopup.gameObject.SetActive(false);
    }

    private void Show(Entity nearestE)
    {
        if (Game.ClientGame.World.EntityManager.HasComponent<LocalTransform>(nearestE))
        {
            var nearestT = Game.ClientGame.World.EntityManager.GetComponentData<LocalTransform>(nearestE);
            transform.SetPositionAndRotation(nearestT.Position, nearestT.Rotation);
            transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
        }
    }
}