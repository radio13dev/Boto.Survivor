using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct Rpc_PlayerAdjustInventory : IComponentData
{
    public byte PlayerIndex;
    public byte FromTo;
    
    public byte From => (byte)((FromTo >> 4) & 0x0F);
    public byte To => (byte)(FromTo & 0x0F);
    
    public bool IsFromFloor => From >= Ring.k_RingCount;
    public bool IsToFloor => To == (byte.MaxValue >> 4);
    public int FloorIndex => From - Ring.k_RingCount;
    
    public static byte GetFloorIndexByte(int index)
    {
        return (byte)(Ring.k_RingCount + index);
    }
    
    public Rpc_PlayerAdjustInventory(byte playerIndex, byte fromTo)
    {
        PlayerIndex = playerIndex;
        FromTo = fromTo;
    }
}

[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
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
        byte lastTo = 0;
        foreach (var (rpcRO, rpcE) in SystemAPI.Query<RefRO<Rpc_PlayerAdjustInventory>>().WithEntityAccess())
        {
            ecb.DestroyEntity(rpcE);

            var rpc = rpcRO.ValueRO;
            var playerIndex = rpc.PlayerIndex;
            
            if (lastTo == rpc.FromTo)
                continue;
            if (rpc.From == rpc.To)
                continue;
            
            lastTo = rpc.FromTo;

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

            if (rpc.IsToFloor)
            {
                // Validate the 'from' movement
                var from = rpc.From;
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
                
                Debug.Log($"{from} from hand to floor");
            }
            else if (rpc.IsFromFloor)
            {
                // Validate the 'to' movement
                var to = rpc.To;
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
                    pickup = SystemAPI.GetComponent<LootGenerator2>(pickupE).GetRingStats(rpc.FloorIndex);
                else
                    pickup = SystemAPI.GetComponent<RingStats>(pickupE);

                ecb.DestroyEntity(pickupE);

                // Remove item from inventory
                var taken = inventory[to].Stats;
                inventory[to] = new Ring() { Stats = pickup };
                Debug.Log($"{rpc.FloorIndex} from floor to {to}");

                // and spawn it in world
                if (taken.PrimaryEffect != RingPrimaryEffect.None)
                {
                    var ringDrop = ecb.Instantiate(ringDropTemplate);
                    ecb.SetComponent(ringDrop, SystemAPI.GetComponent<LocalTransform>(playerE));
                    ecb.SetComponent(ringDrop, new Movement() { Velocity = SystemAPI.GetSingleton<SharedRandom>().Random.NextFloat3Direction() * 2f });
                    ecb.SetComponent(ringDrop, taken);
                    Debug.Log($"{to} from hand to floor");
                }
            }
            else
            {
                var from = rpc.From;
                var to = rpc.To;
            
                // Validate the 'from' and 'to' movement
                if (from < 0 || from >= inventory.Length)
                    continue; // Invalid 'from' index
                if (to < 0 || to >= inventory.Length)
                    continue; // Invalid 'to' index

                (inventory[from], inventory[to]) = (inventory[to], inventory[from]);
                Debug.Log($"{from} from hand to {to}");
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
        if (Game.ClientGame == null) return;
    
        // Get player
        var playerIndex = Game.ClientGame.PlayerIndex;
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