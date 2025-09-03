using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class CameraTarget : EntityLinkMono
{
    public static CameraTarget MainTarget;

    public int PlayerIndex
    {
        get
        {
            if (!HasLink()) return -1;
            if (Game.World.EntityManager.HasComponent<PlayerControlled>(Entity))
                    return Game.World.EntityManager.GetSharedComponent<PlayerControlled>(Entity).Index;
            return -1;
        }
    }

    private void Start()
    {
        if (PlayerIndex == Game.PlayerIndex)
        {
            MainTarget = this;
            GameEvents.Trigger(GameEvents.Type.InventoryChanged, Entity);
            GameEvents.Trigger(GameEvents.Type.PlayerHealthChanged, Entity);
            GameEvents.Trigger(GameEvents.Type.WalletChanged, Entity);
        }
    }
}