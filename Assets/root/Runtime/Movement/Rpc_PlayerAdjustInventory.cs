using BovineLabs.Saving;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct Rpc_PlayerAdjustInventory : IComponentData
{
    public int PlayerIndex;
    public int From;
    public int To;

    public Rpc_PlayerAdjustInventory(byte playerIndex, byte fromTo)
    {
        PlayerIndex = playerIndex;
        From = fromTo & 0x0F; // lower 4 bits
        To = (fromTo >> 4) & 0x0F; // upper 4 bits
    }
}

public partial struct Rpc_PlayerAdjustInventory_System : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.Resources>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<Rpc_PlayerAdjustInventory>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ringDropTemplate = SystemAPI.GetSingleton<GameManager.Resources>().ItemDropTemplate;
        var ecb = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.SinglePlayback);
        int lastTo = -1;
        foreach (var (rpcRO, rpcE) in SystemAPI.Query<RefRO<Rpc_PlayerAdjustInventory>>().WithEntityAccess())
        {
            ecb.DestroyEntity(rpcE);

            var rpc = rpcRO.ValueRO;
            var playerIndex = rpc.PlayerIndex;
            var from = rpc.From;
            var to = rpc.To;
            
            if (lastTo == to)
                continue;
            
            lastTo = to;

            Entity playerE = Entity.Null;
            foreach (var (testPlayer, testPlayerE) in SystemAPI.Query<RefRO<PlayerControlled>>().WithEntityAccess())
            {
                if (testPlayer.ValueRO.Index == playerIndex)
                {
                    playerE = testPlayerE;
                    break;
                }
            }

            if (playerE == Entity.Null) continue;
            if (!SystemAPI.HasBuffer<Ring>(playerE)) continue;
            var inventory = SystemAPI.GetBuffer<Ring>(playerE);
            var playerT = SystemAPI.GetComponent<LocalTransform>(playerE);

            if (to == byte.MaxValue)
            {
                // Validate the 'from' movement
                if (from < 0 || from >= inventory.Length)
                    continue; // Invalid 'from' index

                if (inventory[from].Stats.PrimaryEffect == RingPrimaryEffect.None)
                    continue; // Nothing to drop

                // Remove item from inventory
                var taken = inventory[from].Stats;
                inventory[from] = default;

                //  and spawn it in world
                var ringDrop = ecb.Instantiate(ringDropTemplate);
                ecb.SetComponent(ringDrop, playerT);
                ecb.SetComponent(ringDrop, new Movement() { Velocity = SystemAPI.GetSingleton<SharedRandom>().Random.NextFloat3Direction() * 2f });
                ecb.SetComponent(ringDrop, taken);
            }
            else if (from >= byte.MaxValue / 2)
            {
                // Validate the 'to' movement
                if (to < 0 || to >= inventory.Length)
                    continue; // Invalid 'to' index

                // Validate the 'pickup' exists
                Entity pickupE = Entity.Null;
                float pickupD = float.MaxValue;
                foreach (var (testPickupT, testPickupE) in SystemAPI.Query<RefRO<LocalTransform>>().WithAny<RingStats, LootGenerator2>().WithEntityAccess())
                {
                    var d = math.distancesq(testPickupT.ValueRO.Position, playerT.Position);
                    if (d < pickupD)
                    {
                        pickupE = testPickupE;
                        pickupD = d;
                    }
                    else if (d == pickupD)
                    {
                        // Desync!
                        Debug.LogError($"Desync occured during item pickup!");
                    }
                }

                if (pickupD > 2 * 2) continue; // Too far away

                // Pick it up and destroy it
                RingStats pickup;
                if (SystemAPI.HasComponent<LootGenerator2>(pickupE))
                    pickup = SystemAPI.GetComponent<LootGenerator2>(pickupE).GetRingStats(from - byte.MaxValue / 2);
                else
                    pickup = SystemAPI.GetComponent<RingStats>(pickupE);

                ecb.DestroyEntity(pickupE);

                // Remove item from inventory
                var taken = inventory[to].Stats;
                inventory[to] = new Ring() { Stats = pickup };

                // and spawn it in world
                if (taken.PrimaryEffect != RingPrimaryEffect.None)
                {
                    var ringDrop = ecb.Instantiate(ringDropTemplate);
                    ecb.SetComponent(ringDrop, SystemAPI.GetComponent<LocalTransform>(playerE));
                    ecb.SetComponent(ringDrop, new Movement() { Velocity = SystemAPI.GetSingleton<SharedRandom>().Random.NextFloat3Direction() * 2f });
                    ecb.SetComponent(ringDrop, taken);
                }
            }
            else
            {
                // Validate the 'from' and 'to' movement
                if (from < 0 || from >= inventory.Length)
                    continue; // Invalid 'from' index
                if (to < 0 || to >= inventory.Length)
                    continue; // Invalid 'to' index

                (inventory[from], inventory[to]) = (inventory[from], inventory[to]);
            }

            SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

public struct NearestInteractable : IComponentData
{
    public Entity Value;

    public NearestInteractable(Entity value)
    {
        Value = value;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
public partial struct FindNearestInteractableSystem : ISystem
{
    public const float k_PickupRange = 2 * 2;

    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingleton<NearestInteractable>();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Get player
        var playerIndex = Game.PresentationGame.PlayerIndex;
        Entity playerE = Entity.Null;
        foreach (var (testPlayer, testPlayerE) in SystemAPI.Query<RefRO<PlayerControlled>>().WithEntityAccess())
        {
            if (testPlayer.ValueRO.Index == playerIndex)
            {
                playerE = testPlayerE;
                break;
            }
        }

        if (playerE == Entity.Null)
        {
            SystemAPI.SetSingleton(new NearestInteractable(Entity.Null));
            return;
        }

        var playerT = SystemAPI.GetComponent<LocalTransform>(playerE);

        // Get pickup
        Entity pickupE = Entity.Null;
        float pickupD = float.MaxValue;
        foreach (var (testPickupT, testPickupE) in SystemAPI.Query<RefRO<LocalTransform>>().WithAny<RingStats, LootGenerator2>().WithEntityAccess())
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
            SystemAPI.SetSingleton(new NearestInteractable(Entity.Null));
            return; // Too far away
        }
        
        SystemAPI.SetSingleton(new NearestInteractable(pickupE));
    }
}