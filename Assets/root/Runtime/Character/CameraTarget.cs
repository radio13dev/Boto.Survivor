using Unity.Entities;
using UnityEngine;

public class CameraTarget : EntityLinkMono
{
    public static CameraTarget MainTarget;
    public static Entity MainEntity => MainTarget ? MainTarget.Entity : Entity.Null;

    public int PlayerIndex
    {
        get
        {
            if (!HasLink()) return -1;
            if (Game.World.EntityManager.HasComponent<PlayerControlledSaveable>(Entity))
                    return Game.World.EntityManager.GetComponentData<PlayerControlledSaveable>(Entity).Index;
            return -1;
        }
    }

    private void Start()
    {
        if (Game == null)
        {
            Debug.LogError("No Game found for CameraTarget");
            return;
        }
        
        if (PlayerIndex == Game.PlayerIndex)
        {
            MainTarget = this;
            GameEvents.InventoryChanged(Entity);
            if (GameEvents.TryGetComponent2(Entity, out Health health)) GameEvents.PlayerHealthChanged(Entity, health);
            if (GameEvents.TryGetComponent2(Entity, out Wallet wallet)) GameEvents.WalletChanged(Entity, wallet);
            if (GameEvents.TryGetComponent2(Entity, out PlayerLevel playerLevel))
            {
                GameEvents.PlayerLevelProgress(Entity, playerLevel);
                GameEvents.PlayerLevelUp(Entity, playerLevel);
            }
            
            foreach (var mapRouter in FindObjectsByType<MapDrawingRouter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                mapRouter.ResetExistingMapPoints();
            }
        }
    }
}