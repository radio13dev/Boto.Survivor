using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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
            GameEvents.PlayerHealthChanged(Entity, GameEvents.GetComponent<Health>(Entity));
            GameEvents.WalletChanged(Entity, GameEvents.GetComponent<Wallet>(Entity));
        }
    }
}