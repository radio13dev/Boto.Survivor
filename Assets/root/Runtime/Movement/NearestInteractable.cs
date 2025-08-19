using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct NearestInteractable : IComponentData
{
    public Entity Value;

    public NearestInteractable(Entity value)
    {
        Value = value;
    }
}

public struct Interactable : IComponentData
{
}

[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
public partial struct FindNearestInteractableSystem : ISystem
{
    public const float k_PickupRange = 2 * 2;

    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingleton<NearestInteractable>();
        state.RequireForUpdate<NearestInteractable>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (Game.ClientGame == null) return;

        // Get last interactable
        var lastInteractableE = SystemAPI.GetSingleton<NearestInteractable>().Value;
        Entity newInteractableE;

        // Get player
        Entity playerE;
        var playerIndex = Game.ClientGame.PlayerIndex;
        using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(LocalTransform));
        playerQuery.SetSharedComponentFilter(new PlayerControlled() { Index = playerIndex });
        using var playersE = playerQuery.ToEntityArray(Allocator.Temp);
        if (playersE.Length > 0)
            playerE = playersE[0];
        else
            playerE = Entity.Null;

        if (playerE == Entity.Null)
        {
            newInteractableE = Entity.Null;
        }
        else
        {
            var playerT = SystemAPI.GetComponent<LocalTransform>(playerE);

            // Get pickup
            Entity pickupE = Entity.Null;
            float pickupD = float.MaxValue;
            foreach (var (testPickupT, testPickupE) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Interactable>().WithEntityAccess())
            {
                var d = math.distancesq(testPickupT.ValueRO.Position, playerT.Position);
                if (d < pickupD)
                {
                    pickupE = testPickupE;
                    pickupD = d;
                }
            }

            if (pickupD > k_PickupRange)
            {
                newInteractableE = Entity.Null;
            }
            else
            {
                newInteractableE = pickupE;
            }
        }
        
        if (lastInteractableE != newInteractableE)
        {
            
            SystemAPI.SetSingleton(new NearestInteractable(newInteractableE));
            
            GameEvents.Trigger(GameEvents.Type.InteractableEnd, lastInteractableE);
            GameEvents.Trigger(GameEvents.Type.InteractableStart, newInteractableE);
        }
    }
}